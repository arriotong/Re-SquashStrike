// ===========================================================
// Assets/SquashGame/Runtime/UI/FrontEnd.cs
// P2-M1: the front end. One UI Toolkit document per scene holding
// the HUD and a stack of menu screens over the live court.
//
//   Match scene    Title -> Main menu (an AI-vs-AI demo rally plays
//                  behind) -> Quick Match / Local Versus setup ->
//                  the match (HUD) -> Pause / Post-match
//   Practice court Pause -> back to the main menu; a Target Arcade
//                  challenge or drill; an Academy lesson (P2-M3)
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
        public ArcadeRunner Arcade { get { return Runner as ArcadeRunner; } }
        public AcademyRunner Academy { get { return Runner as AcademyRunner; } }
        /// <summary>Free practice's feeder (the Coach panel drives it).</summary>
        public CoachFeeder Coach { get; private set; }
        public ArcadeHud ArcadeHud { get; private set; }
        public LessonHud LessonHud { get; private set; }
        /// <summary>A screen to open over the main menu when the match scene loads (back from the arcade).</summary>
        public static System.Func<MenuScreen> OpenOnLoad;
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
            var kit = Resources.Load<StyleSheet>("UI/Kit");                  // the UI redesign's components
            if (kit != null) _root.styleSheets.Add(kit);
            _root.pickingMode = PickingMode.Ignore;

            if (Match != null)
            {
                Hud = new HudLayer();
                _root.Add(Hud);
                Stats = new MatchStats();
                Stats.Attach(Match);
            }
            if (Arcade != null)
            {
                ArcadeHud = new ArcadeHud(Arcade);
                _root.Add(ArcadeHud);
            }
            if (Academy != null)
            {
                LessonHud = new LessonHud(Academy);
                _root.Add(LessonHud);
            }
            if (runner is LabSimRunner lab)
            {
                Coach = gameObject.AddComponent<CoachFeeder>();
                Coach.Build(lab);
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
            // UI redesign: live 3D pictures for cards
            var vig = gameObject.AddComponent<Vignettes>();
            vig.Runner = runner;

            // P2-M6: finished matches go into the replay library, described for its cards
            Replays.ReplayLibrary.Hook();
            Replays.ReplayMeta.Hook();

            if (Match != null)
            {
                Match.BeginDemo();
                if (StartAtTitle) Push(new TitleScreen()); else Push(new MainMenuScreen());
                StartAtTitle = false;                                       // the title only at boot
                if (OpenOnLoad != null) { var open = OpenOnLoad; OpenOnLoad = null; Push(open()); }
            }
            else if (Coach != null && SessionLaunch.OpenCoach)
            {
                SessionLaunch.OpenCoach = false;
                Push(new CoachScreen());
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

        // ---------------- P2-M5: achievement toasts ----------------

        readonly List<(VisualElement box, float age)> _toasts = new List<(VisualElement, float)>();
        VisualElement _toastBox;
        /// <summary>The toasts shown so far (tests).</summary>
        public readonly List<string> ToastLog = new List<string>();

        public void ToastAchievements(Progression.RewardResult r)
        {
            if (r == null) return;
            foreach (var a in r.Achieved) Toast("ACHIEVEMENT  ·  " + a.Tier.ToString().ToUpperInvariant(), a.Name, (int)a.Tier);
        }

        readonly Queue<(string kicker, string text, int tier)> _toastQueue = new Queue<(string, string, int)>();
        public const int MaxToasts = 3;

        /// <summary>A toast top right; tier 0-2 shows an achievement medallion (bronze / silver / gold).</summary>
        public void Toast(string kicker, string text, int tier = -1)
        {
            ToastLog.Add(text);
            if (_toasts.Count >= MaxToasts) { _toastQueue.Enqueue((kicker, text, tier)); return; }     // three at a time; the rest wait
            ShowToast(kicker, text, tier);
        }

        void ShowToast(string kicker, string text, int tier)
        {
            if (_toastBox == null)
            {
                _toastBox = new VisualElement(); _toastBox.AddToClassList("toasts"); _toastBox.pickingMode = PickingMode.Ignore;
                _root.Add(_toastBox);
            }
            var box = new VisualElement(); box.AddToClassList("toast"); box.AddToClassList("toast--in"); box.pickingMode = PickingMode.Ignore;
            if (tier >= 0)
            {
                var medal = new Label(tier == 2 ? "III" : tier == 1 ? "II" : "I"); medal.AddToClassList("toast-medal");
                medal.style.backgroundColor = MedalItem.TierColours[Mathf.Clamp(tier, 0, 2)];
                box.Add(medal);
            }
            var words = new VisualElement(); words.AddToClassList("toast-words");
            var k = new Label(kicker); k.AddToClassList("toast-kicker");
            var t = new Label(text); t.AddToClassList("toast-text");
            words.Add(k); words.Add(t); box.Add(words);
            _toastBox.Add(box);
            box.schedule.Execute(() => box.RemoveFromClassList("toast--in")).StartingIn(30);
            if (tier >= 0) Sounds.Play("star");
            _toasts.Add((box, 0f));
        }

        void TickToasts(float dt)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var (l, age) = _toasts[i];
                age += dt;
                l.style.opacity = Mathf.Clamp01(Mathf.Min(age / 0.25f, (5f - age) / 0.5f));
                if (age > 5f) { l.RemoveFromHierarchy(); _toasts.RemoveAt(i); }
                else _toasts[i] = (l, age);
            }
            while (_toasts.Count < MaxToasts && _toastQueue.Count > 0) { var (k, t, tier) = _toastQueue.Dequeue(); ShowToast(k, t, tier); }
        }

        public void Push(MenuScreen s)
        {
            if (Top != null) { Top.OnHide(); Top.Root.style.display = DisplayStyle.None; }
            s.Create(this);
            _menus.Add(s.Root);
            _stack.Add(s);
            s.OnShow();
            Enter(s, +1);
            UpdateFlags();
        }

        /// <summary>Screens slide in from the way you went (or fade, when centred). Off in batch runs (captures).</summary>
        public static bool AnimateScreens
        {
            get { if (_animate == null) _animate = !Application.isBatchMode; return _animate.Value; }
            set { _animate = value; }
        }
        static bool? _animate;

        static void Enter(MenuScreen s, int dir)
        {
            if (!AnimateScreens) return;
            string cls = s.Root.ClassListContains("fe-screen--centre") ? "fe-in-fade" : dir > 0 ? "fe-in-right" : "fe-in-left";
            s.Root.AddToClassList(cls);
            s.Root.schedule.Execute(() => s.Root.RemoveFromClassList(cls)).StartingIn(20);
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            var s = Top;
            s.OnHide();
            _stack.RemoveAt(_stack.Count - 1);
            s.Root.RemoveFromHierarchy();
            if (Top != null) { Top.Root.style.display = DisplayStyle.Flex; Top.OnShow(); Enter(Top, -1); }
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
            if (Top is PauseScreen || Top is PracticePauseScreen || Top is ArcadePauseScreen || Top is AcademyPauseScreen) Pop();
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

        public void GoPractice() { LaunchFreePractice(); }

        /// <summary>Play a Target Arcade challenge or a drill (the practice court scene runs it).</summary>
        public static void LaunchChallenge(string id)
        {
            ProfileStore.Save();
            SessionLaunch.ChallengeId = id;
            SceneManager.LoadScene(PracticeScene);
        }

        /// <summary>P2-M6: watch the saved best play of a Target Arcade level.</summary>
        public static void WatchArcadeBest(string id)
        {
            var log = Replays.ArcadeBests.Load(id, out int score);
            if (log == null) return;
            SessionLaunch.Watch = log; SessionLaunch.WatchScore = score;
            LaunchChallenge(id);
        }

        public static void LaunchFreePractice(bool coach = false)
        {
            ProfileStore.Save();
            SessionLaunch.ChallengeId = null;
            SessionLaunch.OpenCoach = coach;
            SceneManager.LoadScene(PracticeScene);
        }

        /// <summary>From a challenge back to the Target Arcade (or, from a drill, the Practice menu).</summary>
        public void BackToArcadeMenu()
        {
            bool drill = Arcade != null && Arcade.Challenge.Mode == SquashSim.Arcade.ArcadeMode.Drill;
            OpenOnLoad = drill ? (System.Func<MenuScreen>)(() => new PracticeScreen(true)) : () => new TargetArcadeScreen();
            StartAtTitle = false;
            ProfileStore.Save();
            SceneManager.LoadScene(MatchScene);
        }

        /// <summary>P2-M3: play an Academy lesson (the practice court scene runs it).</summary>
        public static void LaunchLesson(string id)
        {
            ProfileStore.Save();
            SessionLaunch.ChallengeId = null;
            SessionLaunch.LessonId = id;
            SceneManager.LoadScene(PracticeScene);
        }

        /// <summary>From a lesson back to the Academy menu.</summary>
        public void BackToAcademyMenu()
        {
            OpenOnLoad = () => new AcademyScreen();
            StartAtTitle = false;
            ProfileStore.Save();
            SceneManager.LoadScene(MatchScene);
        }

        /// <summary>The match setup, with this difficulty picked (the Academy's last lesson).</summary>
        public static void LaunchQuickMatch(int difficulty)
        {
            ProfileStore.Current.Choices.Difficulty = difficulty;
            OpenOnLoad = () => new QuickMatchScreen();
            StartAtTitle = false;
            ProfileStore.Save();
            SceneManager.LoadScene(MatchScene);
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
            TickToasts(dt);
            if (Match != null) MatchScreens(dt); else if (Arcade != null) ArcadeScreens(dt); else if (Academy != null) AcademyScreens(dt); else PracticeScreens();
            if (Top != null)
            {
                Top.Tick(dt);
                Top.HandleInput(_input);
            }
            // a developer's look at the UI kit (F12 on the main menu)
            var kbd = UnityEngine.InputSystem.Keyboard.current;
            if (kbd != null && kbd.f12Key.wasPressedThisFrame && Top is MainMenuScreen) Push(new GalleryScreen());
            UpdateFlags();
            if (Hud != null) Hud.Refresh(Match, Top == null || Top is PauseScreen);
            if (ArcadeHud != null) ArcadeHud.Refresh(Arcade, Top == null || Top is ArcadePauseScreen);
            if (LessonHud != null) LessonHud.Refresh(Academy, Top == null || Top is AcademyPauseScreen);
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

        float _overFor;

        void ArcadeScreens(float dt)
        {
            var a = Arcade;
            if (Top == null && a.Clock.Paused) Push(new ArcadePauseScreen());
            if (Top is ArcadePauseScreen && !a.Clock.Paused) Pop();
            if (a.Session.Over)
            {
                _overFor += dt;
                if (Top == null && _overFor > 1.2f) Push(new ResultsScreen(a));
            }
            else _overFor = 0f;
        }

        void AcademyScreens(float dt)
        {
            var a = Academy;
            var s = a.Session;
            if (Top == null && a.Clock.Paused) Push(new AcademyPauseScreen());
            if (Top is AcademyPauseScreen && !a.Clock.Paused) Pop();
            if (Top == null && !s.Done && s.Current != null)
            {
                if (s.Current.Goal == SquashSim.Academy.GoalKind.Card)
                {
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (_input.Submit || (mouse != null && mouse.leftButton.wasPressedThisFrame)) { s.NextCard(); a.Input.Intent.ClearEdges(); }
                }
                else if (s.AwaitingAnswer) Push(new QuizScreen(s.Current.Question));
            }
            if (s.Done)
            {
                _overFor += dt;
                if (Top == null && _overFor > 1.0f) Push(new LessonCompleteScreen(a.Lesson));
            }
            else _overFor = 0f;
        }

        void PracticeScreens()
        {
            // Tab / pad View: the Coach panel (the game waits while it is open).
            bool coachKey = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame;
            foreach (var pad in UnityEngine.InputSystem.Gamepad.all) coachKey |= pad.selectButton.wasPressedThisFrame;
            if (coachKey && Coach != null)
            {
                if (Top == null) { Push(new CoachScreen()); return; }
                if (Top is CoachScreen) { Pop(); return; }
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (Top == null && Coach != null && kb != null && kb.fKey.wasPressedThisFrame && !ProfileStore.Current.Coach.Auto) Coach.FeedNow();
            if (Top == null && Runner.Clock.Paused) Push(new PracticePauseScreen());
            if (Top is PracticePauseScreen && !Runner.Clock.Paused) Pop();
        }
    }
}
