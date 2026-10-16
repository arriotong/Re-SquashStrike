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

            Color[] colours = { Greybox.Hex(0x4d6d8f), Greybox.Hex(0x8f4d4d) };
            for (int i = 0; i < 2; i++)
            {
                var athlete = new GameObject("AthleteView" + i).AddComponent<AthleteView>();
                athlete.transform.SetParent(transform, false);
                athlete.Build(Runner, i, colours[i], Palette);

                var racket = new GameObject("RacketView" + i).AddComponent<RacketView>();
                racket.transform.SetParent(transform, false);
                racket.Build(Runner, i, Palette);
            }

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Runner, Palette);

            var hud = new GameObject("MatchHud").AddComponent<MatchHud>();
            hud.transform.SetParent(transform, false);
            hud.Runner = Runner;

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
