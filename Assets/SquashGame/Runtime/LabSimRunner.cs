// ===========================================================
// Assets/SquashGame/Runtime/LabSimRunner.cs
// The warm-up court: SquashSim's LabSimulation (one athlete, the feed
// launcher, the lab presets) on the shared frame loop, with the game's
// match ball so practice feels like the match. With MatchBall off it is
// bit-identical to the HTML lab at any frame rate (see ClockTests).
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Lab;
using UnityEngine;

namespace SquashGame
{
    public sealed class LabSimRunner : SimRunnerBase
    {
        [Tooltip("Preset selected on start (lab TestPresets id).")]
        public string StartPreset = "free";
        [Tooltip("Start with the repeat feed running.")]
        public bool StartWithRepeatFeed = true;
        [Tooltip("Practise with the game's match ball (double yellow at PSA pace). Off = the HTML lab's ball.")]
        public bool MatchBall = true;

        public LabSimulation Sim { get; private set; }
        public string CurrentPreset { get; private set; }
        TestPlayerController[] _athletes;

        public override BallState Ball { get { return Sim.Ball; } }
        public override TestPlayerController[] Athletes { get { return _athletes; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Sim.StepEvents; } }
        public override List<Surface> Surfaces { get { return Sim.Surfaces; } }

        protected override void CreateSimulation()
        {
            PhysicsConfig.ResetToDefaults();
            if (MatchBall) PhysicsConfig.UseGameProfile();
            Sim = new LabSimulation();
            Sim.Player.WallAimEnabled = MatchBall;      // practise with the match's aim
            _athletes = new[] { Sim.Player };
            CurrentPreset = TestPresets.Find(StartPreset).Id;
            Sim.SelectPreset(CurrentPreset);
            Sim.Launcher.SetRepeat(StartWithRepeatFeed);
        }

        protected override void SimStep(double dt) { Sim.Step(Input.Intent, dt); }

        // ---------------- lab actions ----------------

        public void SelectPreset(string id)
        {
            CurrentPreset = TestPresets.Find(id).Id;
            ClearDebugHistory();
            Sim.SelectPreset(CurrentPreset);
            Clock.Flush();
            SnapViews();
        }

        public void CyclePreset(int dir)
        {
            var all = TestPresets.All();
            int i = all.FindIndex(p => p.Id == CurrentPreset);
            int next = ((i + dir) % all.Count + all.Count) % all.Count;
            SelectPreset(all[next].Id);
        }

        /// <summary>RESET BALL: only the ball, back to the preset's start state.</summary>
        public void ResetBall()
        {
            Trail.Clear();
            Sim.Launcher.Fire(fresh: true);
            Clock.Flush();
            SnapViews();
        }

        /// <summary>RESET FULL: player, racket, ball, preset; resumes at 1x.</summary>
        public void ResetFull()
        {
            Trail.Clear();
            Sim.Launcher.SetRepeat(false);
            Sim.SelectPreset(CurrentPreset);
            Clock.Resume();
            Clock.TimeScale = 1.0;
            Clock.Flush();
            SnapViews();
        }

        public void FeedSingle() { Sim.Launcher.Fire(); }
        public bool ToggleRepeat() { return Sim.Launcher.ToggleRepeat(); }
    }
}
