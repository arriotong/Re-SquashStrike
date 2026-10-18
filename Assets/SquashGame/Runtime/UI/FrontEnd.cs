// ===========================================================
// Assets/SquashGame/Runtime/UI/FrontEnd.cs
// P2-M1: the front end. One UI Toolkit document per scene holding
// the HUD and a stack of menu screens over the live court.
//
//   Match scene    Title -> Main menu (an AI-vs-AI demo rally plays
//                  behind) -> Quick Match / Local Versus setup ->
//                  the match (HUD) -> Pause / Post-match
//   Practice court Pause -> back to the main menu
//
// The pause screen follows the game's own pause (Esc / Start); the
// post-match screen comes up when the match is over. While a menu
// owns the keyboard (anything but the pause screen) the game's pause
// key is held off (BlocksGamePause).
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    [DefaultExecutionOrder(100)]                 // after the runner: a menu press is never also a stroke
    public sealed class FrontEnd : MonoBehaviour
    {
        public const string MatchScene = "Match", PracticeScene = "WarmUpCourt";

        /// <summary>A menu screen is up (the cursor is free, the mouse does not aim).</summary>
        public static bool MenuOpen { get; private set; }
        /// <summary>The menu owns Esc / Start (the game must not toggle its pause).</summary>
        public static bool BlocksGamePause { get; private set; }
        /// <summary>Open the match scene on the title screen (else straight to the main menu).</summary>
        public static bool StartAtTitle = true;

        public SimRunnerBase Runner { get; private set; }
        public MatchRunner Match { get { return Runner as MatchRunner; } }
        public UIDocument Doc { get; private set; }
        public HudLayer Hud { get; private set; }
        public MatchStats Stats { get; private set; }

        readonly List<MenuScreen> _stack = new List<MenuScreen>();
        VisualElement _root, _menus, _dim;
        MenuInput _input;
        float _matchOverFor;

        public MenuScreen Top { get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; } }
        public int Depth { get { return _stack.Count; } }

        public void Build(SimRunnerBase runner)
        {
            Runner = runner;
            var go = new GameObject("UIDocument");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            Doc = go.AddComponent<UIDocument>();
            Doc.panelSettings = MakePanelSettings();
            go.SetActive(true);

            _root = Doc.rootVisualElement;
            _root.AddToClassList("fe-root");
            var sheet = Resources.Load<StyleSheet>("UI/Theme");
            if (sheet != null) _root.styleSheets.Add(sheet);
            _root.pickingMode = PickingMode.Ignore;

            if (Match != null)
            {
                Hud = new HudLayer();
                _root.Add(Hud);
                Stats = new MatchStats();
                Stats.Attach(Match);
            }
            _dim = new VisualElement();
            _dim.AddToClassList("fe-layer"); _dim.AddToClassList("fe-dim");
            _dim.pickingMode = PickingMode.Ignore;
            _root.Add(_dim);
            _menus = new VisualElement();
            _menus.AddToClassList("fe-layer");
            _menus.pickingMode = PickingMode.Ignore;
            _root.Add(_menus);

            _input = new MenuInput();

            if (Match != null)
            {
                Match.BeginDemo();
                if (StartAtTitle) Push(new TitleScreen()); else Push(new MainMenuScreen());
                StartAtTitle = false;                                       // the title only at boot
            }
        }

        static PanelSettings MakePanelSettings()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/ReTheme");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = 10;
            return ps;
        }

        void OnDestroy()
        {
            if (_input != null) _input.Dispose();
            if (Stats != null) Stats.Detach();
            MenuOpen = false; BlocksGamePause = false;
        }

        // ---------------- the stack ----------------

        public void Push(MenuScreen s)
        {
            if (Top != null) { Top.OnHide(); Top.Root.style.display = DisplayStyle.None; }
            s.Create(this);
            _menus.Add(s.Root);
            _stack.Add(s);
            s.OnShow();
            UpdateFlags();
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            var s = Top;
            s.OnHide();
            _stack.RemoveAt(_stack.Count - 1);
            s.Root.RemoveFromHierarchy();
            if (Top != null) { Top.Root.style.display = DisplayStyle.Flex; Top.OnShow(); }
            UpdateFlags();
        }

        public void Replace(MenuScreen s)
        {
            if (_stack.Count > 0)
            {
                var old = Top; old.OnHide(); _stack.RemoveAt(_stack.Count - 1); old.Root.RemoveFromHierarchy();
            }
            Push(s);
        }

        public void Clear()
        {
            while (_stack.Count > 0) { var s = Top; s.OnHide(); _stack.RemoveAt(_stack.Count - 1); s.Root.RemoveFromHierarchy(); }
            UpdateFlags();
        }

        /// <summary>The screen of this type on the stack, if any (tests).</summary>
        public T Find<T>() where T : MenuScreen
        {
            for (int i = _stack.Count - 1; i >= 0; i--) if (_stack[i] is T t) return t;
            return null;
        }

        void UpdateFlags()
        {
            MenuOpen = Top != null;
            BlocksGamePause = Top != null && !Top.EscapeIsGame;
            _dim.style.display = Top != null && Top.Dims ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------- game actions (used by the screens) ----------------

        public void StartMatch(MatchSettings s)
        {
            Clear();
            Stats.Reset();
            Match.BeginMatch(s);
            ClearEdges();
        }

        public void Restart()
        {
            Clear();
            Stats.Reset();
            Match.RestartMatch();
            ClearEdges();
        }

        public void Resume()
        {
            Runner.Clock.Resume();
            ClearEdges();
            if (Top is PauseScreen || Top is PracticePauseScreen) Pop();
        }

        /// <summary>Leave the match for the main menu (the demo rally plays behind it).</summary>
        public void QuitToMenu()
        {
            if (Match == null) { StartAtTitle = false; SceneManager.LoadScene(MatchScene); return; }
            Clear();
            Runner.Clock.Resume();
            Match.BeginDemo();
            Push(new MainMenuScreen());
        }

        public void GoPractice()
        {
            ProfileStore.Save();
            SceneManager.LoadScene(PracticeScene);
        }

        public static void QuitGame()
        {
            ProfileStore.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>A button pressed to leave a menu must not also be a stroke or a dive in play.</summary>
        void ClearEdges()
        {
            Runner.Input.Intent.ClearEdges();
            if (Match != null && Match.Input2 != null) Match.Input2.Intent.ClearEdges();
        }

        // ---------------- per frame ----------------

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _input.Sample(dt);
            if (Match != null) MatchScreens(dt); else PracticeScreens();
            if (Top != null)
            {
                Top.Tick(dt);
                Top.HandleInput(_input);
            }
            UpdateFlags();
            if (Hud != null) Hud.Refresh(Match, Top == null || Top is PauseScreen);
        }

        void MatchScreens(float dt)
        {
            var m = Match;
            bool playing = !m.Demo && !m.InSetup && m.Sim != null;
            if (playing && Top == null && m.Clock.Paused && m.DeviceLost == null && !m.ReplayActive) Push(new PauseScreen());
            if (Top is PauseScreen && !m.Clock.Paused) Pop();                      // resumed with Esc / Start
            if (playing && Top == null && m.Sim.Phase == MatchPhase.MatchOver)
            {
                _matchOverFor += dt;
                if (_matchOverFor > 1.6f) Push(new PostMatchScreen());
            }
            else _matchOverFor = 0f;
        }

        void PracticeScreens()
        {
            if (Top == null && Runner.Clock.Paused) Push(new PracticePauseScreen());
            if (Top is PracticePauseScreen && !Runner.Clock.Paused) Pop();
        }
    }
}
