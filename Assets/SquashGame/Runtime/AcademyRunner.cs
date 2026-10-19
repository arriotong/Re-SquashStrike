// ===========================================================
// Assets/SquashGame/Runtime/AcademyRunner.cs
// P2-M3: an Academy lesson on the shared frame loop - SquashSim's
// AcademySession, played in the practice court scene.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Academy;

namespace SquashGame
{
    public sealed class AcademyRunner : SimRunnerBase
    {
        public Lesson Lesson { get; private set; }
        public AcademySession Session { get; private set; }
        TestPlayerController[] _athletes, _staged;

        /// <summary>The session's events (progress, notes, quiz) as they happen, one fixed step at a time.</summary>
        public event System.Action<AcademyEvent> Judged;
        /// <summary>A bot plays the lesson (tests).</summary>
        public LessonBot Autopilot;

        public override BallState Ball { get { return Session.Ball; } }
        /// <summary>P2-M7: the opponent too while the lesson stages a two-athlete moment.</summary>
        public override TestPlayerController[] Athletes { get { return Session.Staged ? _staged : _athletes; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Session.StepEvents; } }
        public override List<Surface> Surfaces { get { return Session.Surfaces; } }

        protected override void CreateSimulation()
        {
            Lesson = Lessons.Find(SessionLaunch.LessonId) ?? Lessons.All()[0];
            NewSession();
        }

        void NewSession()
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            Session = new AcademySession(Lesson, (uint)System.Environment.TickCount);
            _athletes = new[] { Session.Player };
            _staged = new[] { Session.Player, Session.Opponent };
        }

        /// <summary>The lesson from the start.</summary>
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

        public void Play(Lesson l) { Lesson = l; Restart(); }

        protected override void SimStep(double dt)
        {
            if (Session.Done) { Input.Intent.ClearEdges(); return; }
            if (Autopilot != null) Autopilot.Fill(Input.Intent, Session, dt);
            Session.Step(Input.Intent, dt);
            var h = Judged;
            if (h != null) foreach (var e in Session.Events) h(e);
        }
    }
}
