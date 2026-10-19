// ===========================================================
// Assets/SquashSim/Academy/AcademySession.cs
// P2-M3: one Academy lesson as a deterministic sim: one athlete on the
// simple hitting system, the ball, the court, the feeder - and the
// lesson's steps, one at a time, each with its pass condition.
//
//   controls steps  watched every fixed step (spots, the reticle, a
//                   sprint, dives) or on the shot's life (ShotTracker:
//                   contact -> front wall -> first bounce -> done)
//   card steps      wait for the player to read on (NextCard)
//   quiz steps      fly a real shot, then wait for the call (Answer);
//                   2 of the lesson's 3 right to pass, else RetryQuiz
//   staged steps    P2-M7: a two-athlete moment (InterferenceStage) runs
//                   and is mirrored here - the athletes and the ball;
//                   a card loops it, a quiz stops as the striker would
//                   ask and wants your call
// ===========================================================

using System.Collections.Generic;
using SquashSim.Arcade;
using static SquashSim.CourtDimensions;

namespace SquashSim.Academy
{
    public enum AcademyEventKind { StepStarted, Progress, StepPassed, LessonPassed, Note, QuizAsked, QuizRight, QuizWrong, QuizFailed }

    public sealed class AcademyEvent
    {
        public AcademyEventKind Kind;
        public string Note;
        public V3 Point;
    }

    public sealed class AcademySession
    {
        public readonly Lesson Lesson;
        public readonly List<Surface> Surfaces = CourtSurfaces.Create();
        public readonly BallState Ball = new BallState();
        public readonly TestPlayerController Player = new TestPlayerController();
        /// <summary>P2-M7: the opponent in a staged moment (shown only while Staged).</summary>
        public readonly TestPlayerController Opponent = new TestPlayerController();
        /// <summary>A two-athlete moment is on (the views show the opponent).</summary>
        public bool Staged { get { return _stage != null; } }
        public InterferenceStage Stage { get { return _stage; } }
        InterferenceStage _stage;
        double _stageHold;
        public readonly Lab.BallLauncher Launcher;
        public readonly List<SimEvent> StepEvents = new List<SimEvent>();
        public readonly List<AcademyEvent> Events = new List<AcademyEvent>();
        /// <summary>What to draw now (the current spot / ring / landing zone).</summary>
        public readonly List<Target> Targets = new List<Target>();

        public int StepIndex { get; private set; }
        public LessonStep Current { get { return StepIndex < Lesson.Steps.Count ? Lesson.Steps[StepIndex] : null; } }
        public int Progress { get; private set; }
        public bool Done { get; private set; }
        public double Time { get; private set; }
        public long StepCount { get; private set; }
        public bool Serving { get { return _serving; } }
        public int ServeSide { get; private set; } = 1;
        /// <summary>A quiz shot has been played and the call is wanted (Answer).</summary>
        public bool AwaitingAnswer { get; private set; }
        public int QuizAsked { get; private set; }
        public int QuizRight { get; private set; }
        public bool QuizFailed { get; private set; }
        /// <summary>The last quiz answer: the right call (for the reveal).</summary>
        public string LastCorrect { get; private set; }

        public const double FeedGap = 0.9, StepPause = 1.0, ReticleHold = 0.5, QuizTimeout = 3.0;

        readonly ShotTracker _shot = new ShotTracker();
        uint _rng;
        bool _live, _serving, _advancing, _sprintSeen, _sawFault, _quizResolved;
        double _gap = 1.0, _hold, _advanceIn, _stepTime, _quizTime;
        int _divesAtStart, _spot;
        bool _quizFront;

        public AcademySession(Lesson lesson, uint seed = 1)
        {
            Lesson = lesson;
            Launcher = new Lab.BallLauncher(Ball);
            _rng = seed * 2654435761u + 7u;
            Player.ArcadeHitEnabled = true;
            Player.WallAimEnabled = true;
            Player.Prediction.MaxFloorBounces = 1;
            Player.Player.Spawn.X = 0; Player.Player.Spawn.Y = 0; Player.Player.Spawn.Z = PhysicsConfig.Current.Match.TZ;
            Player.Reset();
            Park();
            BeginStep();
        }

        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        void Park()
        {
            // Out of sight behind the front wall: a parked ball on the court is "playable", and the
            // movement assist would pull a walking player towards it.
            Ball.Reset(new V3(0, 0.03, -1.5), new V3(0, 0, 0), null, 40);
            Ball.Asleep = true;
        }

        // ---------------- steps ----------------

        void BeginStep()
        {
            var st = Current;
            Progress = 0; _hold = 0; _spot = 0; _stepTime = 0; _sprintSeen = false; _sawFault = false;
            _advancing = false; _live = false; _gap = 0.8; _serving = false;
            AwaitingAnswer = false; _quizResolved = false; _quizTime = 0; _quizFront = false;
            _shot.Reset();
            Targets.Clear();
            if (st == null) return;
            if (!double.IsNaN(st.StartX))
            {
                var p = Player.Player;
                p.Pos.X = st.StartX; p.Pos.Z = st.StartZ; p.Vel.X = 0; p.Vel.Z = 0;
            }
            _divesAtStart = Player.Arcade.DiveCount;
            switch (st.Goal)
            {
                case GoalKind.ReachSpots: case GoalKind.SprintTo: case GoalKind.ReticleRings:
                    if (st.Targets.Count > 0) Targets.Add(st.Targets[0]);
                    break;
                case GoalKind.ShotType:
                    if (st.MinZ > 0) Targets.Add(Target.Zone(-HALF_W, HALF_W, st.MinZ, LENGTH, 100, "LAND HERE"));
                    else if (st.MaxZ < 50) Targets.Add(Target.Zone(-HALF_W, HALF_W, 0, st.MaxZ, 100, "LAND HERE"));
                    break;
                case GoalKind.Serve: case GoalKind.FaultThenServe:
                    ServeSide = 1;
                    Targets.Add(Target.Zone(-HALF_W, 0, SHORT_LINE_Z, LENGTH, 100, "QUARTER"));
                    break;
            }
            if (st.Setup == StepSetup.NoBall || st.Goal == GoalKind.Card) Park();
            if (st.Setup == StepSetup.Serve) BeginServe();
            _stage = st.Stage ?? (st.Question != null && st.Question.Kind == QuizKind.Interference ? st.Question.Stage : null);
            if (_stage != null) { _stage = _stage.Clone(); _stage.Begin(); _stageHold = 0; Mirror(); }
            if (st.Goal == GoalKind.Quiz) QuizShot();
            Events.Add(new AcademyEvent { Kind = AcademyEventKind.StepStarted, Note = st.Text });
        }

        void Count(V3 at = null)
        {
            if (_advancing) return;
            Progress++;
            Events.Add(new AcademyEvent { Kind = AcademyEventKind.Progress, Point = at != null ? at.Clone() : null });
            if (Progress >= Current.Count) PassStep();
        }

        void PassStep()
        {
            _advancing = true;
            _advanceIn = StepPause;
            Events.Add(new AcademyEvent { Kind = AcademyEventKind.StepPassed });
        }

        void NextStep()
        {
            StepIndex++;
            if (StepIndex >= Lesson.Steps.Count)
            {
                Done = true;
                Targets.Clear();
                Events.Add(new AcademyEvent { Kind = AcademyEventKind.LessonPassed });
                return;
            }
            BeginStep();
        }

        /// <summary>A card has been read: on to the next step.</summary>
        public void NextCard()
        {
            if (Current != null && Current.Goal == GoalKind.Card && !Done) NextStep();
        }

        // ---------------- the fixed step ----------------

        public void Step(InputIntent intent, double dt)
        {
            StepEvents.Clear();
            Events.Clear();
            if (Done) { intent.ClearEdges(); return; }
            Time += dt; StepCount++; _stepTime += dt;
            var st = Current;

            if (_advancing)
            {
                _advanceIn -= dt;
                if (_advanceIn <= 0) { NextStep(); st = Current; if (Done) { intent.ClearEdges(); return; } }
            }

            if (st.Goal == GoalKind.ModeDrive) Player.Arcade.Assisted = st.Assisted;      // the lesson sets the mode for the shot
            if (st.Line != null) Player.Arcade.ShotLines = true;                          // the lesson is about them

            if (_stage != null) { StepStage(st, dt); intent.ClearEdges(); return; }

            // feeds
            if (st.Setup == StepSetup.Feed && !_advancing && !_live)
            {
                _gap -= dt;
                if (_gap <= 0) Feed();
            }

            Launcher.Step(dt);
            SimEvent serve = _serving ? StepServe(intent, dt) : null;
            Player.ContactEnabled = st.Setup == StepSetup.Feed && _live && _shot.FeedFront && !_shot.Struck;
            Player.ServeStroke = serve != null;
            SimEvent contact = Player.Step(intent, Ball, Surfaces, dt);
            Player.ServeStroke = false;
            if (serve != null) contact = serve;
            if (contact != null)
            {
                contact.PlayerIndex = 0;
                StepEvents.Add(contact);
                _shot.OnContact(contact);
            }

            if (!Ball.Asleep) BallPhysics.Step(Ball, Surfaces, dt, StepEvents);
            for (int i = 0; i < StepEvents.Count; i++)
                if (StepEvents[i].Type == SimEventType.Impact) OnImpact(st, StepEvents[i]);

            if (!_advancing) WatchBody(st, intent, dt);
            if (st.Goal == GoalKind.Quiz && !_quizResolved)
            {
                _quizTime += dt;
                if (_quizTime > QuizTimeout) ResolveQuiz();
            }
            intent.ClearEdges();
        }

        void WatchBody(LessonStep st, InputIntent intent, double dt)
        {
            var pos = Player.Player.Pos;
            switch (st.Goal)
            {
                case GoalKind.ReachSpots:
                    if (Targets.Count > 0 && Targets[0].OnSpot(pos))
                    {
                        Count(new V3(Targets[0].X, 0, Targets[0].Z0));
                        _spot++;
                        Targets.Clear();
                        if (_spot < st.Targets.Count && !_advancing) Targets.Add(st.Targets[_spot]);
                    }
                    break;
                case GoalKind.SprintTo:
                    if (intent.Sprint && intent.MoveMagnitude > 0.1) _sprintSeen = true;
                    if (Targets.Count > 0 && Targets[0].OnSpot(pos))
                    {
                        if (_sprintSeen && _stepTime <= st.Seconds) Count(new V3(Targets[0].X, 0, Targets[0].Z0));
                        else { Events.Add(new AcademyEvent { Kind = AcademyEventKind.Note, Note = _sprintSeen ? "TOO SLOW - AGAIN" : "HOLD SPRINT - AGAIN" }); BeginStep(); }
                    }
                    else if (_stepTime > st.Seconds + 3) { Events.Add(new AcademyEvent { Kind = AcademyEventKind.Note, Note = "AGAIN" }); BeginStep(); }
                    break;
                case GoalKind.Dive:
                    if (Player.Arcade.DiveCount > _divesAtStart + Progress) Count(pos);
                    break;
                case GoalKind.ReticleRings:
                    if (Targets.Count > 0 && intent.HasAimPoint && Targets[0].InRing(new V3(intent.AimPointX, intent.AimPointY, 0)))
                    {
                        _hold += dt;
                        if (_hold >= ReticleHold)
                        {
                            Count(new V3(Targets[0].X, Targets[0].Y, 0));
                            _hold = 0; _spot++;
                            Targets.Clear();
                            if (_spot < st.Targets.Count && !_advancing) Targets.Add(st.Targets[_spot]);
                        }
                    }
                    else _hold = 0;
                    break;
            }
        }

        void OnImpact(LessonStep st, SimEvent e)
        {
            if (st.Goal == GoalKind.Quiz) { QuizImpact(e); return; }
            if (!_live) return;
            var phase = _shot.OnImpact(e);
            var c = _shot.Contact;
            string tech = c != null ? c.Technique : null;
            switch (phase)
            {
                case ShotTracker.Phase.FrontWall:
                    if (st.Goal == GoalKind.FrontWallHits) Count(e.Point);
                    else if (st.Goal == GoalKind.PaceOver && tech == st.Shot && c.BallSpeedOut >= st.Pace) Count(e.Point);
                    else if (st.Goal == GoalKind.GreatHits && ArcadeStroke.IsGreat(c)) Count(e.Point);
                    else if (st.Goal == GoalKind.Boast && tech == "boast") Count(e.Point);
                    else if (st.Goal == GoalKind.PaceOver && tech == st.Shot) Note("MORE PACE - HOLD LONGER", e.Point);
                    else if ((st.Goal == GoalKind.Serve || st.Goal == GoalKind.FaultThenServe) && e.Point.Y <= SERVICE_LINE_H) ServeFault(st, e.Point);
                    break;
                case ShotTracker.Phase.FirstBounce:
                    if (st.Goal == GoalKind.ShotType)
                    {
                        if (tech != st.Shot) Note("THAT WAS A " + (tech ?? "?").ToUpperInvariant(), e.Point);
                        else if (st.Line != null && !OnLine(st.Line, e.Point)) Note(st.Line == "straight" ? "KEEP IT ON YOUR SIDE" : "SEND IT ACROSS", e.Point);
                        else if (e.Point.Z >= st.MinZ && e.Point.Z <= st.MaxZ) Count(e.Point);
                        else Note(st.MinZ > 0 ? "DEEPER" : "SHORTER", e.Point);
                    }
                    else if (st.Goal == GoalKind.ModeDrive && tech == "drive") Count(e.Point);
                    else if (st.Goal == GoalKind.Serve || st.Goal == GoalKind.FaultThenServe)
                    {
                        bool legal = e.Point.X * ServeSide < 0 && e.Point.Z > SHORT_LINE_Z;
                        if (!legal) ServeFault(st, e.Point);
                        else if (st.Goal == GoalKind.FaultThenServe) { if (_sawFault) Count(e.Point); else Note("NOW AIM UNDER THE LINE", e.Point); }
                        else if (st.ServeKind == null || tech == st.ServeKind) Count(e.Point);
                        else Note("THAT WAS A " + (tech ?? "?").ToUpperInvariant(), e.Point);
                    }
                    break;
                case ShotTracker.Phase.Done:
                    if (_shot.Result == "down" || _shot.Result == "out") Note(_shot.Result == "down" ? "DOWN" : "OUT", e.Point);
                    else if (_shot.Result == "missed") Note("MISSED IT", e.Point);
                    break;
            }
            if (phase == ShotTracker.Phase.Done || (phase == ShotTracker.Phase.FirstBounce && st.Setup == StepSetup.Serve))
            {
                _live = false;
                _gap = FeedGap;
                if (st.Setup == StepSetup.Serve && !_advancing) BeginServe();
            }
        }

        void Note(string text, V3 at) { Events.Add(new AcademyEvent { Kind = AcademyEventKind.Note, Note = text, Point = at != null ? at.Clone() : null }); }

        void ServeFault(LessonStep st, V3 at)
        {
            if (st.Goal == GoalKind.FaultThenServe && !_sawFault) { _sawFault = true; Note("FAULT - NOW A GOOD ONE", at); }
            else Note("FAULT", at);
            _live = false;
            if (!_advancing) BeginServe();
        }

        // ---------------- feeds, serves ----------------

        void Feed()
        {
            double side = Rnd() < 0.5 ? -1 : 1;
            if (Current.Goal == GoalKind.ModeDrive) side = 1;                     // straight drives down the right
            Feeder.Front(Launcher, side * (0.4 + 0.4 * Rnd()), 7.3, side * (0.9 + 0.6 * Rnd()), 2.1 + 0.5 * Rnd(), 22 + 2 * Rnd());
            _shot.Reset();
            _live = true;
        }

        void BeginServe()
        {
            _serving = true;
            _shot.Reset();
            var p = Player.Player;
            p.Pos.X = ServeSide * 2.4; p.Pos.Z = SHORT_LINE_Z + 0.8; p.Vel.X = 0; p.Vel.Z = 0;
            Ball.Asleep = true;
        }

        V3 HandPosition()
        {
            var m = PhysicsConfig.Current.Match;
            var p = Player.Player.Pos;
            return new V3(p.X + m.ServeTossSide, m.ServeTossHeight, p.Z - m.ServeTossForward);
        }

        SimEvent StepServe(InputIntent intent, double dt)
        {
            var p = Player.Player;
            RallyReferee.ServiceBox(ServeSide, out double x0, out double x1, out double z0, out double z1);
            double r = System.Math.Min(PhysicsConfig.Current.Player.Radius, 0.3);
            if (p.Pos.X < x0 + 0.05) { p.Pos.X = x0 + 0.05; p.Vel.X = 0; }
            if (p.Pos.X > x1 - r) { p.Pos.X = x1 - r; p.Vel.X = 0; }
            if (p.Pos.Z < z0 + 0.05) { p.Pos.Z = z0 + 0.05; p.Vel.Z = 0; }
            if (p.Pos.Z > z1 - 0.05) { p.Pos.Z = z1 - 0.05; p.Vel.Z = 0; }
            Ball.Pos.CopyFrom(HandPosition());
            Ball.Asleep = true;
            var arc = Player.Arcade;
            if (intent.Hit.Pressed) { intent.Hit.Pressed = false; arc.BeginServeCharge(); }
            if (!arc.ServeCharging) return null;
            if (intent.Hit.Held) { arc.ChargeServe(dt); return null; }
            var m = PhysicsConfig.Current.Match;
            Ball.Reset(HandPosition(), new V3(0, m.ServeTossSpeed, 0), null, 40);
            BallPhysics.Wake(Ball);
            Player.ServeStroke = true;
            Player.ServeDefaultX = -ServeSide * 0.9;
            _serving = false;
            _live = true;
            _shot.Reset();
            _shot.FromHand();
            return arc.ServeNow(Player, Ball, Surfaces);
        }

        // ---------------- quiz ----------------

        // ---------------- staged moments (P2-M7) ----------------

        void StepStage(LessonStep st, double dt)
        {
            if (st.Goal == GoalKind.Quiz)
            {
                // Played up to where the striker would ask; then your call.
                if (_quizResolved) return;
                if (_stage.AboutToAsk || !_stage.Step(dt, false)) { ResolveQuiz(); return; }
                Mirror();
                return;
            }
            // A card: the moment over and over (the call, a breath, again).
            if (_stageHold > 0)
            {
                _stageHold -= dt;
                if (_stageHold <= 0) { _stage.Begin(); Mirror(); }
                return;
            }
            if (!_stage.Step(dt, true)) _stageHold = 1.2;
            Mirror();
        }

        /// <summary>Show the staged moment: its athletes and ball are the session's.</summary>
        void Mirror()
        {
            var m = _stage.Sim;
            Copy(m.Players[0].Player, Player.Player);
            Copy(m.Players[1].Player, Opponent.Player);
            Ball.Pos.CopyFrom(m.Ball.Pos); Ball.Vel.CopyFrom(m.Ball.Vel); Ball.Omega.CopyFrom(m.Ball.Omega);
            Ball.Asleep = false;
        }

        static void Copy(TestPlayer from, TestPlayer to)
        {
            to.Pos.CopyFrom(from.Pos); to.Vel.CopyFrom(from.Vel);
        }

        /// <summary>A straight shot lands on the side it was struck from (well off the middle); a cross on the other.</summary>
        bool OnLine(string line, V3 bounce)
        {
            double from = _shot.Contact != null ? _shot.Contact.Point.X : Player.Player.Pos.X;
            int side = from >= 0 ? 1 : -1;
            return line == "straight" ? bounce.X * side > 1.2 : bounce.X * side < -0.8;
        }

        void QuizShot()
        {
            var q = Current.Question;
            if (q.Kind == QuizKind.Scoring) { ResolveQuiz(); return; }
            if (q.Kind == QuizKind.Interference) return;                     // staged: StepStage plays it
            var p = Player.Player;
            p.Pos.X = -2.4; p.Pos.Z = 8.6; p.Vel.X = 0; p.Vel.Z = 0;          // out of the way, watching
            RulesQuiz.Aim(Launcher, q);
        }

        void QuizImpact(SimEvent e)
        {
            if (_quizResolved) return;
            var q = Current.Question;
            int side = q.Px >= 0 ? 1 : -1;
            if (RulesQuiz.Judge(q, e, ref _quizFront, side) != null) ResolveQuiz(e.Point);
        }

        void ResolveQuiz(V3 at = null)
        {
            _quizResolved = true;
            AwaitingAnswer = true;
            QuizAsked++;
            Events.Add(new AcademyEvent { Kind = AcademyEventKind.QuizAsked, Note = Current.Question.Text, Point = at != null ? at.Clone() : null });
        }

        /// <summary>The player's call for the quiz question (index into its Choices). True when right.</summary>
        public bool Answer(int choice)
        {
            if (!AwaitingAnswer) return false;
            var q = Current.Question;
            string right = RulesQuiz.Answer(q);
            LastCorrect = right;
            bool ok = choice >= 0 && choice < q.Choices.Length && q.Choices[choice] == right;
            if (ok) QuizRight++;
            AwaitingAnswer = false;
            Events.Add(new AcademyEvent { Kind = ok ? AcademyEventKind.QuizRight : AcademyEventKind.QuizWrong, Note = right });
            bool lastQuiz = StepIndex + 1 >= Lesson.Steps.Count || Lesson.Steps[StepIndex + 1].Goal != GoalKind.Quiz;
            if (lastQuiz && QuizRight < 2)
            {
                QuizFailed = true;
                Events.Add(new AcademyEvent { Kind = AcademyEventKind.QuizFailed });
                return ok;
            }
            NextStep();
            return ok;
        }

        /// <summary>After a failed quiz: the questions again.</summary>
        public void RetryQuiz()
        {
            int first = Lesson.Steps.FindIndex(s => s.Goal == GoalKind.Quiz);
            if (first < 0) return;
            QuizFailed = false; QuizRight = 0; QuizAsked = 0;
            StepIndex = first;
            BeginStep();
        }
    }
}
