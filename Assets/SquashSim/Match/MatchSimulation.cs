// ===========================================================
// Assets/SquashSim/Match/MatchSimulation.cs
// One squash match: two athletes, one ball, the court, the rules.
//
// Built from exactly the same modules as LabSimulation - the player
// pipeline, racket contact and ball physics are untouched - plus the
// things only a match needs:
//
//   serve flow     server placed in the box, receiver in the opposite
//                  back quarter; first Hit/Touch press TOSSES, the
//                  stroke that follows is an ordinary stroke
//   turn gating    only the player the referee expects can touch the ball
//   referee        RallyReferee observes every event
//   scoring        MatchScore (PAR-11 / English)
//   collisions     ball vs body, body vs body
//   recovery       movement penalty after a stroke, by reach
//   split-step     Momentum Focus timed to the opponent's contact
//
// Deterministic: same inputs -> same match, at any frame rate.
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public enum MatchPhase { AwaitToss, Rally, RallyOver, GameOver, MatchOver }

    public sealed class RallyStats
    {
        public int Shots;
        public double Seconds;
        public double Run0, Run1;      // metres covered by each athlete during the rally
        public double Stamina0, Stamina1;   // stamina at the end of the rally
        public RallyOutcome Outcome;
    }

    public sealed class MatchSimulation
    {
        public readonly List<Surface> Surfaces = CourtSurfaces.Create();
        public readonly BallState Ball = new BallState();
        public readonly TestPlayerController[] Players = { new TestPlayerController(), new TestPlayerController() };
        public readonly InputIntent[] Intents = { new InputIntent(), new InputIntent() };
        /// <summary>A brain (wall-bot, AI) drives a player when its slot is non-null; otherwise Intents[i] is filled externally.</summary>
        public readonly IAthleteBrain[] Bots = new IAthleteBrain[2];
        public readonly Interference Interference = new Interference();
        /// <summary>Per finished rally: duration and metres each athlete ran (fatigue tuning data).</summary>
        public readonly List<RallyStats> Stats = new List<RallyStats>();
        /// <summary>Racket contacts per athlete, serves excluded (diagnostics).</summary>
        public readonly int[] RallyContacts = new int[2];

        public readonly RallyReferee Referee = new RallyReferee();
        public MatchScore Score { get; private set; }
        public MatchPhase Phase { get; private set; }
        public RallyOutcome LastOutcome { get; private set; }
        public readonly List<RallyOutcome> History = new List<RallyOutcome>();
        /// <summary>Racket contacts in each finished rally (the serve counts as one).</summary>
        public readonly List<int> ShotsPerRally = new List<int>();

        public readonly List<SimEvent> StepEvents = new List<SimEvent>();
        public double Time { get; private set; }
        public long StepCount { get; private set; }
        public double PhaseTime { get; private set; }

        /// <summary>Raised when a rally ends (after scoring is applied).</summary>
        public event System.Action<RallyOutcome> RallyEnded;

        double _rallyStart;
        readonly double[] _run = new double[2];
        readonly double[] _lastX = new double[2], _lastZ = new double[2];

        readonly V3 _prevBall = new V3();
        bool _bodiesTouching;
        public readonly SplitStepTracker SplitStep = new SplitStepTracker();

        public MatchSimulation(int firstServer = 0)
        {
            Players[0].Index = 0;
            Players[1].Index = 1;
            // The rules allow one bounce after the front wall: predict only contacts before the second.
            Players[0].Prediction.MaxFloorBounces = 1;
            Players[1].Prediction.MaxFloorBounces = 1;
            Ball.Temperature = PhysicsConfig.Current.Match.StartTemperature;   // warmed up in the knock-up
            Score = MatchScore.FromConfig(firstServer);
            Referee.Surfaces = Surfaces;
            SetUpServe();
        }

        public int Receiver { get { return 1 - Score.Server; } }

        // ---------------- serve setup ----------------

        /// <summary>Where each athlete starts a rally, for the current server and box.</summary>
        public static void ServePositions(int serveSide, out V3 serverPos, out V3 receiverPos)
        {
            serverPos = new V3(serveSide * (HALF_W - SERVICE_BOX * 0.5), 0, SHORT_LINE_Z + SERVICE_BOX * 0.5);
            receiverPos = new V3(-serveSide * 1.6, 0, 7.6);
        }

        void SetUpServe()
        {
            int server = Score.Server, receiver = 1 - server;
            ServePositions(Score.ServeSide, out V3 sp, out V3 rp);
            PlaceAt(Players[server], sp);
            PlaceAt(Players[receiver], rp);
            SplitStep.Reset();

            Referee.BeginRally(server, Score.ServeSide);
            Interference.Reset();
            HoldBallForServe(keepTemperature: true);
            Phase = MatchPhase.AwaitToss;
            PhaseTime = 0;
            _bodiesTouching = false;
        }

        static void PlaceAt(TestPlayerController c, V3 pos)
        {
            c.Player.Spawn.CopyFrom(pos);
            c.Reset();
        }

        V3 HandPosition()
        {
            var m = PhysicsConfig.Current.Match;
            var p = Players[Score.Server].Player.Pos;
            // In front of the server, a little to the forehand (right-handed: sim +x) side.
            return new V3(p.X + m.ServeTossSide, m.ServeTossHeight, p.Z - m.ServeTossForward);
        }

        void HoldBallForServe(bool keepTemperature)
        {
            double? t = keepTemperature ? Ball.Temperature : (double?)null;
            Ball.Reset(HandPosition(), null, null, t);
            Ball.Asleep = true;      // held in the hand: not simulated until tossed
        }

        void Toss()
        {
            var m = PhysicsConfig.Current.Match;
            Ball.Reset(HandPosition(), new V3(0, m.ServeTossSpeed, 0), null, Ball.Temperature);
            BallPhysics.Wake(Ball);
            Phase = MatchPhase.Rally;
            PhaseTime = 0;
            _rallyStart = Time;
            _run[0] = 0; _run[1] = 0;
        }

        // ---------------- the fixed step ----------------

        public void Step(double dt)
        {
            StepEvents.Clear();
            Time += dt;
            PhaseTime += dt;
            StepCount++;

            for (int i = 0; i < 2; i++)
                if (Bots[i] != null) Bots[i].Fill(Intents[i], this, i, dt);

            // ---- phase logic before anyone moves ----
            if (Phase == MatchPhase.AwaitToss)
            {
                // The ball rides in the server's hand until the toss.
                Ball.Pos.CopyFrom(HandPosition());
                var si = Intents[Score.Server];
                if (si.Hit.Pressed || si.Touch.Pressed)
                {
                    // The toss press is consumed: it must not also start a swing.
                    si.Hit.Pressed = false; si.Touch.Pressed = false;
                    Toss();
                }
            }
            else if (Phase == MatchPhase.RallyOver && PhaseTime >= PhysicsConfig.Current.Match.BetweenRallySeconds)
            {
                if (Score.MatchOver) Phase = MatchPhase.MatchOver;
                else if (Score.GameOver) Phase = MatchPhase.GameOver;
                else SetUpServe();
            }

            // ---- appeals: the player going for the ball stops and asks for a let ----
            if (Phase == MatchPhase.Rally && Referee.Outcome == null)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!Intents[i].AppealPressed) continue;
                    InterferenceDecision d = Interference.Striker == i
                        ? Interference.Decide()
                        : new InterferenceDecision { Call = InterferenceCall.NoLet, Reason = "No let - opponent was clear" };
                    if (Referee.OnAppeal(i, d, Time)) { FinishRally(); break; }
                }
            }

            // ---- split-step presses ----
            for (int i = 0; i < 2; i++)
                if (Intents[i].SplitStepPressed) SplitStep.Press(i, Time, Players[i].Player);

            // ---- both athletes: movement, prediction, swing, racket, contact ----
            for (int i = 0; i < 2; i++)
            {
                var c = Players[i];
                c.ContactEnabled = Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.ExpectedStriker == i;
                SimEvent contact = c.Step(Intents[i], Ball, Surfaces, dt);
                if (contact != null)
                {
                    contact.Time = Time;
                    StepEvents.Add(contact);
                    if (Referee.Shots > 0) RallyContacts[i]++;
                    OnRacketContact(i, contact);
                }
            }
            Intents[0].ClearEdges();
            Intents[1].ClearEdges();

            // ---- bodies cannot overlap ----
            SimEvent pc = BodyCollision.BodyVsBody(Players[0].Player, 0, Players[1].Player, 1,
                _bodiesTouching, out _bodiesTouching, Time);
            if (pc != null) StepEvents.Add(pc);

            // ---- the ball ----
            if (!Ball.Asleep || Phase != MatchPhase.AwaitToss)
            {
                _prevBall.CopyFrom(Ball.Pos);
                int before = StepEvents.Count;
                BallPhysics.Step(Ball, Surfaces, dt, StepEvents);
                for (int k = before; k < StepEvents.Count; k++) StepEvents[k].Time = Time;
                for (int i = 0; i < 2; i++)
                {
                    SimEvent bh = BodyCollision.BallVsBody(Ball, _prevBall, Players[i].Player, i, Time);
                    if (bh != null) StepEvents.Add(bh);
                }
            }
            else
            {
                BallTemperature.CoolOverTime(Ball, dt);   // a ball held between rallies still cools
            }

            // ---- split-step presses whose window passed unanswered: planted ----
            SplitStep.Update(Time, Players[0].Player, Players[1].Player);

            // ---- fatigue ----
            bool rallyLive = Phase == MatchPhase.Rally && Referee.Outcome == null;
            Fatigue.Update(Players[0].Player, rallyLive, dt);
            Fatigue.Update(Players[1].Player, rallyLive, dt);

            // ---- running totals for rally stats ----
            for (int i = 0; i < 2; i++)
            {
                var p = Players[i].Player.Pos;
                double dx = p.X - _lastX[i], dz = p.Z - _lastZ[i];
                if (Phase == MatchPhase.Rally) _run[i] += System.Math.Sqrt(dx * dx + dz * dz);
                _lastX[i] = p.X; _lastZ[i] = p.Z;
            }

            // ---- the referee watches everything that happened ----
            if (Phase == MatchPhase.Rally)
            {
                Interference.Update(this, dt);
                Referee_Observe();
            }
        }

        void Referee_Observe()
        {
            foreach (var ev in StepEvents)
            {
                if (Referee.Outcome != null) break;
                switch (ev.Type)
                {
                    case SimEventType.RacketContact: Referee.OnRacketContact(ev, Players[ev.PlayerIndex].Player.Pos); break;
                    case SimEventType.Impact: Referee.OnImpact(ev); break;
                    case SimEventType.BodyHit: Referee.OnBodyHit(ev); break;
                }
            }

            if (Referee.TossDropped && Referee.Outcome == null)
            {
                // Dropped toss: back into the hand, no fault.
                Referee.ResetToss();
                HoldBallForServe(keepTemperature: true);
                Phase = MatchPhase.AwaitToss;
                PhaseTime = 0;
                return;
            }

            if (Referee.Outcome != null) FinishRally();
        }

        void FinishRally()
        {
            LastOutcome = Referee.Outcome;
            History.Add(LastOutcome);
            ShotsPerRally.Add(Referee.Shots);
            Stats.Add(new RallyStats
            {
                Shots = Referee.Shots,
                Seconds = Time - _rallyStart,
                Run0 = _run[0], Run1 = _run[1],
                Stamina0 = Players[0].Player.Stamina, Stamina1 = Players[1].Player.Stamina,
                Outcome = LastOutcome
            });
            Score.Apply(LastOutcome);
            Phase = MatchPhase.RallyOver;
            PhaseTime = 0;
            var h = RallyEnded;
            if (h != null) h(LastOutcome);
        }

        // ---------------- recovery + split-step ----------------

        void OnRacketContact(int i, SimEvent contact)
        {
            var rec = PhysicsConfig.Current.Recovery;
            double t;
            switch (contact.ReachState)
            {
                case ReachStates.Lunge: t = rec.Lunge; break;
                case ReachStates.DeepLunge: t = rec.DeepLunge; break;
                case ReachStates.Stretch: t = rec.Stretch; break;
                default: t = rec.Normal; break;
            }
            if (t > 0) Players[i].Player.RecoveryTimer = t;
            Fatigue.OnStroke(Players[i].Player, contact.ReachState);

            SplitStep.Contact(i, Time, Players[1 - i].Player);
        }

        // ---------------- flow control ----------------

        /// <summary>From GameOver: start the next game of the match.</summary>
        public void NextGame()
        {
            if (Phase != MatchPhase.GameOver) return;
            Score.NextGame();
            SetUpServe();
        }

        /// <summary>Start a fresh match with the same settings.</summary>
        public void Restart(int firstServer = 0)
        {
            Score = MatchScore.FromConfig(firstServer);
            History.Clear();
            ShotsPerRally.Clear();
            Stats.Clear();
            LastOutcome = null;
            Ball.Reset(null, null, null, PhysicsConfig.Current.Match.StartTemperature);
            Players[0].Player.Stamina = 1.0; Players[1].Player.Stamina = 1.0;
            SetUpServe();
        }
    }
}
