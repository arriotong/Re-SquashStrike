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
            // The menus show the court last chosen for a quick match (a match then sets its own: MatchRunner.BeginMatch).
            var choices = Profile.ProfileStore.Current.Choices;
            Courts.CurrentId = Courts.All[Mathf.Clamp(choices.Court, 0, Courts.All.Length - 1)].Id;
            Courts.CrowdFill = 1f;

            var court = new GameObject("Court").AddComponent<CourtView>();
            court.transform.SetParent(transform, false);
            court.Build(Palette);

            var ball = new GameObject("BallView").AddComponent<BallView>();
            ball.transform.SetParent(transform, false);
            ball.Build(Runner, Palette);

            // PR2: a quick mark wherever the ball hits.
            var marks = new GameObject("ImpactMarks").AddComponent<ImpactMarkView>();
            marks.transform.SetParent(transform, false);
            marks.Build(Runner, Palette);

            // M4: two Gate A athletes on the Gate B skeleton (replace the greybox capsules + rackets).
            Color[] colours = { Greybox.Hex(0x7d93ab), Greybox.Hex(0xab7d7d) };
            for (int i = 0; i < 2; i++)
            {
                var athlete = new GameObject("Athlete" + i).AddComponent<Athlete.AthleteFigure>();
                athlete.transform.SetParent(transform, false);
                athlete.Build(Runner, i, Palette, colours[i]);
                // P2-M5: dressed - you in your loadout, the opponent in theirs (Progression.KitDirector)
                int who = i;
                athlete.KitSource = () => Progression.KitDirector.For(Runner, who);          // online you may be athlete 1
                athlete.MomentSource = () => Progression.KitDirector.Moment(Runner, who);   // the celebration
            }

            var overlay = new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            overlay.transform.SetParent(transform, false);
            overlay.Build(Runner, Palette);


            // M6: an always-on aim target per local player (athlete 1 shows one only in local 2P).
            Color[] aimColours = { new Color(1f, 0.78f, 0.22f), new Color(0.35f, 0.85f, 1f) };
            for (int i = 0; i < 2; i++)
            {
                var aim = new GameObject("AimTarget" + i).AddComponent<AimTargetView>();
                aim.transform.SetParent(transform, false);
                aim.Build(Runner, i, Palette, aimColours[i]);
                var timing = aim.gameObject.AddComponent<TimingFeedbackView>();
                timing.Build(Runner, aim, i);
            }

            var popups = new GameObject("ArcadePopups").AddComponent<ArcadePopups>();
            popups.transform.SetParent(transform, false);
            popups.Build(Runner);

            // Where the ball will land: a light square on the floor (playtest 2026-10-19).
            var landing = new GameObject("LandingMarker").AddComponent<LandingMarkerView>();
            landing.transform.SetParent(transform, false);
            landing.Build(Runner, Palette);

            // P2-M7: the referee review's drawing on the floor.
            var obstruction = new GameObject("ObstructionOverlay").AddComponent<ObstructionOverlay>();
            obstruction.transform.SetParent(transform, false);
            obstruction.Build(Runner, Palette);

            // P2-M1: the front end (menus over the live court, the HUD, pause, post-match).
            var fe = new GameObject("FrontEnd").AddComponent<UI.FrontEnd>();
            fe.transform.SetParent(transform, false);
            fe.Build(Runner);

            var audio = new GameObject("AudioDirector").AddComponent<Audio.AudioDirector>();
            audio.transform.SetParent(transform, false);
            audio.Build(Runner);

            // PR4: the crowd in the venue.
            var crowd = new GameObject("CrowdAudio").AddComponent<Audio.CrowdAudio>();
            crowd.transform.SetParent(transform, false);
            crowd.Build(Runner, 1f);

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
