// ===========================================================
// Assets/SquashGame/Runtime/ArcadeRunner.cs
// P2-M2: a Target Arcade challenge or a drill on the shared frame
// loop - SquashSim.Arcade.ArcadeSession, played from the practice
// court scene. Each play gets a fresh seed; the inputs are recorded
// (InputLog) so the play can be replayed exactly later.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Arcade;
using UnityEngine;

namespace SquashGame
{
    /// <summary>What the practice court scene should run (set by the menus before it loads).</summary>
    public static class SessionLaunch
    {
        /// <summary>A Target Arcade challenge or drill id; null = free practice.</summary>
        public static string ChallengeId;
        /// <summary>An Academy lesson id (P2-M3); null = none.</summary>
        public static string LessonId;
        /// <summary>Free practice: open the Coach panel at once.</summary>
        public static bool OpenCoach;
        /// <summary>P2-M6: play this saved best back instead of being played (its score, to show).</summary>
        public static InputLog Watch;
        public static int WatchScore;
    }

    public sealed class ArcadeRunner : SimRunnerBase
    {
        public Challenge Challenge { get; private set; }
        public ArcadeSession Session { get; private set; }
        public InputLog Log { get; private set; }
        TestPlayerController[] _athletes;

        /// <summary>A new session event (hits, misses, targets...) this fixed step.</summary>
        public event System.Action<ArcadeEvent> Judged;

        public override BallState Ball { get { return Session.Ball; } }
        public override TestPlayerController[] Athletes { get { return _athletes; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Session.StepEvents; } }
        public override List<Surface> Surfaces { get { return Session.Surfaces; } }

        protected override void CreateSimulation()
        {
            Challenge = Challenges.Find(SessionLaunch.ChallengeId) ?? Challenges.All()[0];
            Watching = SessionLaunch.Watch; WatchingScore = SessionLaunch.WatchScore;
            SessionLaunch.Watch = null;
            NewSession();
        }

        void NewSession()
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            uint seed = Watching != null ? Watching.Seed : (uint)System.Environment.TickCount ^ 0x5EEDu;
            _watchAt = 0;
            Session = new ArcadeSession(Challenge, seed);
            _athletes = new[] { Session.Player };
            Log = new InputLog { ChallengeId = Challenge.Id, Seed = seed };
        }

        /// <summary>The same challenge again (a new seed).</summary>
        public void Restart()
        {
            NewSession();
            ClearDebugHistory();
            Clock.Resume();
            Clock.TimeScale = GameSpeed;
            Clock.Flush();
            Input.Intent.ClearEdges();
            SnapViews();
        }

        /// <summary>Another challenge in the same scene.</summary>
        public void Play(Challenge c)
        {
            Challenge = c;
            Watching = null;
            Restart();
        }

        /// <summary>A bot plays instead of the player (tests; later the title screen's attract mode).</summary>
        public ArcadeBot Autopilot;

        /// <summary>P2-M6: a saved best being played back (its log feeds every step), or null.</summary>
        public InputLog Watching { get; private set; }
        public int WatchingScore { get; private set; }
        int _watchAt;

        protected override void SimStep(double dt)
        {
            if (Session.Over) { Input.Intent.ClearEdges(); return; }          // the play is over: nothing more to record
            if (Watching != null) { if (_watchAt < Watching.Frames.Count) Watching.Frames[_watchAt++].Into(Input.Intent); else Input.Intent.ClearEdges(); }
            else if (Autopilot != null) Autopilot.Fill(Input.Intent, Session, dt);
            Log.Record(Input.Intent);
            Session.Step(Input.Intent, dt);
            var h = Judged;
            if (h != null) foreach (var e in Session.Events) h(e);
        }
    }
}
