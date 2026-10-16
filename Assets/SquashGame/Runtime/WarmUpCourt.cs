// ===========================================================
// Assets/SquashGame/Runtime/WarmUpCourt.cs
// Scene bootstrap for the M1 warm-up court. Wiring only - like the
// lab's main.js it owns no physics, constants or rules. The scene holds
// one GameObject with this component, the camera and a palette asset;
// everything else is built from code, so geometry always follows
// CourtDimensions and PhysicsConfig.
// ===========================================================

using UnityEngine;

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

            var athlete = new GameObject("AthleteView").AddComponent<AthleteView>();
            athlete.transform.SetParent(transform, false);
            athlete.Build(Runner, 0, Greybox.Hex(0x4d6d8f), Palette);

            var racket = new GameObject("RacketView").AddComponent<RacketView>();
            racket.transform.SetParent(transform, false);
            racket.Build(Runner, 0, Palette);

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Runner, Palette);

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
