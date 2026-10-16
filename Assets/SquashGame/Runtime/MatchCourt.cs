// ===========================================================
// Assets/SquashGame/Runtime/MatchCourt.cs
// Scene bootstrap for the M2 match against the wall-bot. Wiring only,
// like WarmUpCourt: everything is built from code so geometry follows
// CourtDimensions and the rules follow PhysicsConfig.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    [DefaultExecutionOrder(-200)]
    public sealed class MatchCourt : MonoBehaviour
    {
        public GreyboxPalette Palette;
        public Camera GameplayCamera;

        public MatchRunner Runner { get; private set; }

        void Awake()
        {
            if (Palette == null) { Debug.LogError("MatchCourt: no GreyboxPalette assigned."); enabled = false; return; }

            Runner = gameObject.AddComponent<MatchRunner>();

            var court = new GameObject("Court").AddComponent<CourtView>();
            court.transform.SetParent(transform, false);
            court.Build(Palette);

            var ball = new GameObject("BallView").AddComponent<BallView>();
            ball.transform.SetParent(transform, false);
            ball.Build(Runner, Palette);

            // M4: two Gate A athletes on the Gate B skeleton (replace the greybox capsules + rackets).
            Color[] colours = { Greybox.Hex(0x7d93ab), Greybox.Hex(0xab7d7d) };
            for (int i = 0; i < 2; i++)
            {
                var athlete = new GameObject("Athlete" + i).AddComponent<Athlete.AthleteFigure>();
                athlete.transform.SetParent(transform, false);
                athlete.Build(Runner, i, Palette, colours[i]);
            }

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Runner, Palette);

            var hud = new GameObject("MatchHud").AddComponent<MatchHud>();
            hud.transform.SetParent(transform, false);
            hud.Runner = Runner;

            var setup = new GameObject("MatchSetupPanel").AddComponent<MatchSetupPanel>();
            setup.transform.SetParent(transform, false);
            setup.Runner = Runner;

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
    }
}
