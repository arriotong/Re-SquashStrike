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

        public LabSimRunner Runner { get; private set; }

        void Awake()
        {
            if (Palette == null) { Debug.LogError("WarmUpCourt: no GreyboxPalette assigned."); enabled = false; return; }

            Runner = gameObject.AddComponent<LabSimRunner>();

            var court = new GameObject("Court").AddComponent<CourtView>();
            court.transform.SetParent(transform, false);
            court.Build(Palette);

            var ball = new GameObject("BallView").AddComponent<BallView>();
            ball.transform.SetParent(transform, false);
            ball.Build(Runner, Palette);

            // M4: the Gate A athlete on the Gate B skeleton (replaces the greybox capsule + racket).
            var athlete = new GameObject("Athlete0").AddComponent<Athlete.AthleteFigure>();
            athlete.transform.SetParent(transform, false);
            athlete.Build(Runner, 0, Palette, Greybox.Hex(0x7d93ab));

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Runner, Palette);

            var aim = new GameObject("AimTarget0").AddComponent<AimTargetView>();
            aim.transform.SetParent(transform, false);
            aim.Build(Runner, 0, Palette, new Color(1f, 0.78f, 0.22f));
            aim.gameObject.AddComponent<TimingFeedbackView>().Build(Runner, aim, 0);

            var popups = new GameObject("ArcadePopups").AddComponent<ArcadePopups>();
            popups.transform.SetParent(transform, false);
            popups.Build(Runner);

            var audio = new GameObject("AudioDirector").AddComponent<Audio.AudioDirector>();
            audio.transform.SetParent(transform, false);
            audio.Build(Runner);

            if (GameplayCamera == null) GameplayCamera = Camera.main;
            if (GameplayCamera != null)
            {
                var rig = GameplayCamera.GetComponent<GameplayCameraRig>();
                if (rig == null) rig = GameplayCamera.gameObject.AddComponent<GameplayCameraRig>();
                rig.Runner = Runner;
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
            if (menu) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; SceneManager.LoadScene(MenuScene); }
        }

        GUIStyle _hint;

        void OnGUI()
        {
            if (_hint == null)
            {
                _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
                _hint.normal.textColor = new Color(0.80f, 0.83f, 0.88f);
            }
            GUI.Label(new Rect(Screen.width - 420, 10, 408, 22), "PRACTICE   M / pad View: back to the menu", _hint);
        }
    }
}
