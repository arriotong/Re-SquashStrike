// ===========================================================
// Assets/SquashGame/Runtime/WarmUpCourt.cs
// Scene bootstrap for the M1 warm-up court. Wiring only - like the
// lab's main.js it owns no physics, constants or rules. The scene holds
// one GameObject with this component, the camera and a palette asset;
// everything else is built from code, so geometry always follows
// CourtDimensions and PhysicsConfig.
// ===========================================================

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SquashGame
{
    [DefaultExecutionOrder(-200)]
    public sealed class WarmUpCourt : MonoBehaviour
    {
        public GreyboxPalette Palette;
        public Camera GameplayCamera;

        /// <summary>The runner: free practice (LabSimRunner) or a Target Arcade challenge / drill (ArcadeRunner).</summary>
        public SimRunnerBase Base { get; private set; }
        /// <summary>Free practice's lab runner (null in a challenge).</summary>
        public LabSimRunner Runner { get { return Base as LabSimRunner; } }
        public ArcadeRunner Arcade { get { return Base as ArcadeRunner; } }
        public AcademyRunner Academy { get { return Base as AcademyRunner; } }

        void Awake()
        {
            if (Palette == null) { Debug.LogError("WarmUpCourt: no GreyboxPalette assigned."); enabled = false; return; }

            // P2-M2: the menus say what to run here (then the request is used up).
            if (SessionLaunch.LessonId != null) Base = gameObject.AddComponent<AcademyRunner>();         // P2-M3
            else if (SessionLaunch.ChallengeId != null) Base = gameObject.AddComponent<ArcadeRunner>();
            else Base = gameObject.AddComponent<LabSimRunner>();
            SessionLaunch.ChallengeId = null;
            SessionLaunch.LessonId = null;

            var court = new GameObject("Court").AddComponent<CourtView>();
            court.transform.SetParent(transform, false);
            court.Build(Palette);

            var ball = new GameObject("BallView").AddComponent<BallView>();
            ball.transform.SetParent(transform, false);
            ball.Build(Base, Palette);

            // M4: the Gate A athlete on the Gate B skeleton (replaces the greybox capsule + racket).
            var athlete = new GameObject("Athlete0").AddComponent<Athlete.AthleteFigure>();
            athlete.transform.SetParent(transform, false);
            athlete.Build(Base, 0, Palette, Greybox.Hex(0x7d93ab));
            athlete.KitSource = Progression.KitDirector.You;                 // P2-M5: your Locker Room kit

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Base, Palette);

            var aim = new GameObject("AimTarget0").AddComponent<AimTargetView>();
            aim.transform.SetParent(transform, false);
            aim.Build(Base, 0, Palette, new Color(1f, 0.78f, 0.22f));
            aim.gameObject.AddComponent<TimingFeedbackView>().Build(Base, aim, 0);

            var popups = new GameObject("ArcadePopups").AddComponent<ArcadePopups>();
            popups.transform.SetParent(transform, false);
            popups.Build(Base);

            var audio = new GameObject("AudioDirector").AddComponent<Audio.AudioDirector>();
            audio.transform.SetParent(transform, false);
            audio.Build(Base);

            // P2-M2: the challenge's targets on the walls and the floor.
            if (Arcade != null)
            {
                var targets = new GameObject("Targets").AddComponent<TargetView>();
                targets.transform.SetParent(transform, false);
                targets.Build(Arcade, Palette);
            }
            // P2-M3: a lesson's spots and rings, and the lines its rules cards explain.
            if (Academy != null)
            {
                var targets = new GameObject("Targets").AddComponent<TargetView>();
                targets.transform.SetParent(transform, false);
                targets.Build(Academy, Palette);
                var lines = new GameObject("CourtHighlight").AddComponent<CourtHighlight>();
                lines.transform.SetParent(transform, false);
                lines.Build(Academy, Palette);
            }

            // P2-M1: the front end (pause menu: resume, settings, controls, back to the main menu).
            var fe = new GameObject("FrontEnd").AddComponent<UI.FrontEnd>();
            fe.transform.SetParent(transform, false);
            fe.Build(Base);

            if (GameplayCamera == null) GameplayCamera = Camera.main;
            if (GameplayCamera != null)
            {
                var rig = GameplayCamera.GetComponent<GameplayCameraRig>();
                if (rig == null) rig = GameplayCamera.gameObject.AddComponent<GameplayCameraRig>();
                rig.Runner = Base;
                GameplayCamera.backgroundColor = Greybox.Hex(0x161a20);
                GameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        /// <summary>The match scene (with its setup panel) is the game's menu.</summary>
        public const string MenuScene = "Match";

        void Update()
        {
            var kb = Keyboard.current;
            bool menu = kb != null && kb.mKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) menu |= pad.selectButton.wasPressedThisFrame;
            if (menu) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; UI.FrontEnd.StartAtTitle = false; SceneManager.LoadScene(MenuScene); }
        }

        GUIStyle _hint;

        void OnGUI()
        {
            if (_hint == null)
            {
                _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
                _hint.normal.textColor = new Color(0.80f, 0.83f, 0.88f);
            }
            if (Academy == null) GUI.Label(new Rect(Screen.width - 420, 10, 408, 22), "PRACTICE   M / pad View: back to the menu", _hint);
        }
    }
}
