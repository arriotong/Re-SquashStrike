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
//   warm-up        each player knocks up in turn with a COLD ball that
//                  warms through the real thermal model
//   breaks         between games the ball sits and cools, players recover
//
// Deterministic: same inputs -> same match, at any frame rate.
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public enum MatchPhase { WarmUp, AwaitToss, Rally, RallyOver, GameOver, MatchOver }

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
        /// <summary>A player asked for a let (presentation: "Let, please?"). Raised before the decision.</summary>
        public event System.Action<int> Appealed;

        double _rallyStart;
        readonly double[] _run = new double[2];
        readonly double[] _lastX = new double[2], _lastZ = new double[2];

        readonly V3 _prevBall = new V3();
        bool _bodiesTouching;
        public readonly SplitStepTracker SplitStep = new SplitStepTracker();

        // ---- warm-up ----
        public int WarmUpOwner { get; private set; }
        public int WarmUpShots { get; private set; }
        public int WarmUpHalf { get; private set; }
        public int WarmUpFeeds { get; private set; }
        double _deadFor;

        // ---- turn queries shared by every brain (rally or warm-up) ----
        /// <summary>Is the ball in play for someone (a live rally, or the knock-up)?</summary>
        public bool BallLive { get { return Phase == MatchPhase.WarmUp || (Phase == MatchPhase.Rally && Referee.Outcome == null); } }
        /// <summary>May player i touch the ball right now?</summary>
        public bool CanStrike(int i)
        {
            if (Phase == MatchPhase.WarmUp) return i == WarmUpOwner && !Ball.Asleep;
            return Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.ExpectedStriker == i;
        }
        /// <summary>Is player i the one going for this ball?</summary>
        public bool GoingForBall(int i)
        {
            if (Phase == MatchPhase.WarmUp) return i == WarmUpOwner;
            return Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.PendingStriker == i;
        }
        /// <summary>Shot counter for this rally (in the knock-up it never reads as a serve).</summary>
        public int TurnShots { get { return Phase == MatchPhase.WarmUp ? WarmUpShots + WarmUpFeeds + 1 : Referee.Shots; } }   // every feed is a new turn
        public int TurnLastHitter { get { return Phase == MatchPhase.WarmUp ? WarmUpOwner : Referee.LastHitter; } }
        /// <summary>Seconds left in the current knock-up half.</summary>
        public double WarmUpRemaining { get { return Phase == MatchPhase.WarmUp ? System.Math.Max(0, PhysicsConfig.Current.Match.WarmUpSecondsEach - PhaseTime) : 0; } }
        public double BreakRemaining { get { return Phase == MatchPhase.GameOver ? System.Math.Max(0, PhysicsConfig.Current.Match.BetweenGamesSeconds - PhaseTime) : 0; } }

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
            if (PhysicsConfig.Current.Match.WarmUpEnabled) StartWarmUp(); else SetUpServe();
        }

        // ---------------- warm-up ----------------

        void StartWarmUp()
        {
            // A new ball is cold: the knock-up is what warms it (BallTemperature does the physics).
            Ball.Reset(null, null, null, PhysicsConfig.Current.Temperature.Initial);
            WarmUpHalf = 0;
            BeginWarmUpHalf(0);
        }

        void BeginWarmUpHalf(int owner)
        {
            WarmUpOwner = owner;
            WarmUpShots = 0;
            Phase = MatchPhase.WarmUp;
            PhaseTime = 0;
            PlaceAt(Players[owner], new V3(0.4, 0, 5.4));
            PlaceAt(Players[1 - owner], new V3(-2.3, 0, 8.6));     // waiting in the back corner
            Feed();
        }

        /// <summary>A feed off the front wall to the player knocking up.</summary>
        void Feed()
        {
            var p = Players[WarmUpOwner].Player.Pos;
            double t = Ball.Temperature;
            Ball.Reset(new V3(p.X + 0.5, 1.4, 1.0), new V3(0, 2.6, 9.0), null, t);
            BallPhysics.Wake(Ball);
            WarmUpFeeds++;
            _deadFor = 0;
        }

        /// <summary>Skip the rest of the knock-up (both players ready).</summary>
        /// <summary>The referee's scenario suite (P2-M7 tests): a live rally at a chosen moment. Athlete 0 served, athlete 1
        /// has just struck a legal return from <paramref name="ballPos"/> with <paramref name="ballVel"/>; athlete 0 plays next.
        /// The athletes stand where given. Everything after this is the real sim.</summary>
        public void StageRally(V3 ballPos, V3 ballVel, double x0, double z0, double x1, double z1)
        {
            Phase = MatchPhase.Rally; PhaseTime = 0;
            Referee.BeginRally(0, 1);
            Referee.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 0, Point = new V3(0, 1, 5) }, new V3(2.4, 0, 5.06));
            Referee.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "frontWall", SurfaceId = "frontWall", Point = new V3(0.5, 3, 0) });
            Referee.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "floor", SurfaceId = "floor", Point = new V3(-1.6, 0, 7.5) });
            Referee.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 1, Point = ballPos.Clone() }, new V3(ballPos.X, 0, ballPos.Z + 0.4));
            Ball.Reset(ballPos, ballVel, null, PhysicsConfig.Current.Match.StartTemperature);
            Players[0].Player.Pos.Set(x0, 0, z0); Players[0].Player.Vel.Set(0, 0, 0);
            Players[1].Player.Pos.Set(x1, 0, z1); Players[1].Player.Vel.Set(0, 0, 0);
            Interference.Reset();
        }

        /// <summary>Assisted give-way: when player <paramref name="i"/> is in the striker's way (the raw geometry), blend a
        /// sideways step off the striker's corridor into the move. Deterministic, from sim state only.</summary>
        readonly double[] _steerX = new double[2], _steerZ = new double[2];

        /// <summary>The reason given when the referee stops play himself (P2-M7).</summary>
        public const string SwingStopReason = "Stroke - opponent in the swing (play stopped)";

        /// <summary>The striker plays the ball with the opponent in its swing: the referee stops play for safety (a stroke),
        /// as a real one would, with no appeal. Lets on only.</summary>
        bool RefereeStopsSwing(int i)
        {
            if (!(PhysicsConfig.Current.Match.AppealsEnabled && Phase == MatchPhase.Rally && Referee.Outcome == null &&
                  Referee.Shots > 0 && Interference.Striker == i && Interference.Current == ObstructionKind.Swing)) return false;
            // only when the racket would really meet the opponent: them right by the ball (the rest is for an appeal)
            var o = Players[1 - i].Player.Pos;
            double dx = o.X - Ball.Pos.X, dz = o.Z - Ball.Pos.Z;
            return System.Math.Sqrt(dx * dx + dz * dz) <= GameTuning.Interference.SwingStopDistance;
        }

        /// <summary>The move player <paramref name="i"/> steered this step (its input, or the Assisted give-way blend):
        /// what the referee counts as trying to move (Interference clearing).</summary>
        public void SteeredMove(int i, out double x, out double z) { x = _steerX[i]; z = _steerZ[i]; }

        bool GiveWayNudge(int i, InputIntent mv)
        {
            var it = Interference;
            if (Phase != MatchPhase.Rally || Referee.Outcome != null || it.Striker != 1 - i || it.Raw == ObstructionKind.None) return false;
            var me = Players[i].Player.Pos; var s = Players[1 - i].Player.Pos;
            double px = it.TargetX - s.X, pz = it.TargetZ - s.Z, len2 = px * px + pz * pz;
            double u = len2 < 1e-9 ? 0 : PhysicsConfig.Clamp(((me.X - s.X) * px + (me.Z - s.Z) * pz) / len2, 0, 1);
            double ax = me.X - (s.X + px * u), az = me.Z - (s.Z + pz * u), ad = System.Math.Sqrt(ax * ax + az * az);
            if (ad < 1e-6) { ax = -pz; az = px; ad = System.Math.Sqrt(ax * ax + az * az); if (ad < 1e-9) return false; }
            ax /= ad; az /= ad;
            double k = GameTuning.Interference.AssistGiveWay;
            double mx = mv.Move.X * mv.MoveMagnitude, mz = mv.Move.Z * mv.MoveMagnitude;
            if (mx * ax + mz * az >= k) return false;              // already stepping out
            mx += ax * k; mz += az * k;
            double m = System.Math.Sqrt(mx * mx + mz * mz);
            if (m < 1e-9) return false;
            mv.Move.X = mx / m; mv.Move.Z = mz / m; mv.MoveMagnitude = System.Math.Min(1.0, System.Math.Max(m, k));
            return true;
        }

        public void SkipWarmUp()
        {
            if (Phase != MatchPhase.WarmUp) return;
            SetUpServe();
        }

        void StepWarmUp(double dt)
        {
            var m = PhysicsConfig.Current.Match;
            if (PhaseTime >= m.WarmUpSecondsEach)
            {
                if (WarmUpHalf == 0) { WarmUpHalf = 1; BeginWarmUpHalf(1 - WarmUpOwner); }
                else SetUpServe();
                return;
            }
            // Dead ball (parked, second bounce, or out of court): feed again shortly.
            bool dead = Ball.Asleep || Ball.FloorSinceFrontWall >= 2 || Ball.Pos.Z > CourtDimensions.LENGTH;
            _deadFor = dead ? _deadFor + dt : 0;
            if (_deadFor >= m.WarmUpRefeedSeconds) Feed();
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
            _lastStriker = -1; _nickBy = -1;                                  // P5
            _chaosStriker = -1; _curveT = 0; _curveArmed = false;               // chaos
            if (!_gameInit) { _gameInit = true; for (int i = 0; i < 2; i++) if (Players[i].Player.Mods != null) Players[i].Player.Mods.NewGame(); }
            for (int i = 0; i < 2; i++) if (Players[i].Player.Mods != null) Players[i].Player.Mods.GhostUsed = false;
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
            for (int i = 0; i < 2; i++)                                       // P5: Matador's slow wears off (chaos: freeze, haste)
            {
                var mm = Players[i].Player.Mods;
                if (mm == null) continue;
                if (mm.SlowTimer > 0) mm.SlowTimer -= dt;
                if (mm.FreezeTimer > 0) mm.FreezeTimer -= dt;
                if (mm.HasteTimer > 0) mm.HasteTimer -= dt;
            }

            // ---- phase logic before anyone moves ----
            if (Phase == MatchPhase.WarmUp)
            {
                StepWarmUp(dt);
            }
            else if (Phase == MatchPhase.GameOver)
            {
                if (PhysicsConfig.Current.Match.AutoNextGame && PhaseTime >= PhysicsConfig.Current.Match.BetweenGamesSeconds)
                    NextGame();
            }
            else if (Phase == MatchPhase.AwaitToss)
            {
                // The ball rides in the server's hand until the toss.
                Ball.Pos.CopyFrom(HandPosition());
                var si = Intents[Score.Server];
                var srv = Players[Score.Server];
                bool arcadeServe = srv.ArcadeHitEnabled && PhysicsConfig.Current.ArcadeHit != null;
                if (arcadeServe)
                {
                    // The simple system: press = start the serve; hold to charge a hard serve; let go = struck from the hand.
                    if (si.Hit.Pressed || si.Touch.Pressed)
                    {
                        si.Hit.Pressed = false; si.Touch.Pressed = false;      // consumed: it must not also start a swing
                        srv.Arcade.BeginServeCharge();
                    }
                    if (srv.Arcade.ServeCharging)
                    {
                        if (si.Hit.Held || si.Touch.Held) srv.Arcade.ChargeServe(dt);
                        else
                        {
                            Toss();
                            srv.ServeStroke = true;
                            srv.ServeDefaultX = -Score.ServeSide * 0.9;
                            SimEvent serve = srv.Arcade.ServeNow(srv, Ball, Surfaces);
                            serve.PlayerIndex = Score.Server;
                            serve.Time = Time;
                            StepEvents.Add(serve);
                            OnRacketContact(Score.Server, serve);
                        }
                    }
                }
                else if (si.Hit.Pressed || si.Touch.Pressed)
                {
                    // The toss press is consumed: it must not also start a swing.
                    si.Hit.Pressed = false; si.Touch.Pressed = false;
                    Toss();                                    // the physical game: toss, then time the swing
                }
            }
            else if (Phase == MatchPhase.RallyOver && PhaseTime >= PhysicsConfig.Current.Match.BetweenRallySeconds)
            {
                if (Score.MatchOver) { Phase = MatchPhase.MatchOver; PhaseTime = 0; }
                else if (Score.GameOver) { Phase = MatchPhase.GameOver; PhaseTime = 0; }   // the break: ball cools, players recover
                else SetUpServe();
            }

            // ---- appeals: the player going for the ball stops and asks for a let (the Lets setting) ----
            if (Phase == MatchPhase.Rally && Referee.Outcome == null && PhysicsConfig.Current.Match.AppealsEnabled)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!Intents[i].AppealPressed) continue;
                    var ap = Appealed;
                    if (ap != null) ap(i);
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
            int stopped = -1;
            for (int i = 0; i < 2; i++)
            {
                var c = Players[i];
                c.ContactEnabled = CanStrike(i) && (c.Player.Mods == null || !c.Player.Mods.Frozen);   // chaos: frozen, no stroke
                c.ServeStroke = Phase == MatchPhase.Rally && Referee.Shots == 0;
                // Assisted, a local non-striker in the way: steered sideways off the striker's corridor (P2-M7).
                var mv = Intents[i];
                double keepX = mv.Move.X, keepZ = mv.Move.Z, keepM = mv.MoveMagnitude;
                bool nudged = Bots[i] == null && c.Arcade.Assisted && GiveWayNudge(i, mv);
                _steerX[i] = mv.Move.X * mv.MoveMagnitude; _steerZ[i] = mv.Move.Z * mv.MoveMagnitude;
                SimEvent contact = c.Step(mv, Ball, Surfaces, dt);
                if (nudged) { mv.Move.X = keepX; mv.Move.Z = keepZ; mv.MoveMagnitude = keepM; }   // the input itself is untouched
                if (contact != null)
                {
                    contact.Time = Time;
                    StepEvents.Add(contact);
                    if (Phase == MatchPhase.WarmUp) WarmUpShots++;
                    else if (Referee.Shots > 0) RallyContacts[i]++;
                    if (RefereeStopsSwing(i)) { stopped = i; continue; }
                    OnRacketContact(i, contact);
                }
            }
            if (stopped >= 0 && Referee.OnAppeal(stopped, new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = SwingStopReason }, Time))
                FinishRally();
            Intents[0].ClearEdges();
            Intents[1].ClearEdges();

            // ---- the simple system's server walks about the service box freely (never out of it: no foot faults) ----
            if (Phase == MatchPhase.AwaitToss && Players[Score.Server].ArcadeHitEnabled && PhysicsConfig.Current.ArcadeHit != null)
            {
                var sp = Players[Score.Server].Player;
                RallyReferee.ServiceBox(Score.ServeSide, out double bx0, out double bx1, out double bz0, out double bz1);
                double r = PhysicsConfig.Current.Player.Radius;
                double cx = System.Math.Max(bx0 + 0.05, System.Math.Min(bx1 - System.Math.Min(r, 0.3), sp.Pos.X));
                double cz = System.Math.Max(bz0 + 0.05, System.Math.Min(bz1 - 0.05, sp.Pos.Z));
                if (cx != sp.Pos.X) { sp.Pos.X = cx; sp.Vel.X = 0; }
                if (cz != sp.Pos.Z) { sp.Pos.Z = cz; sp.Vel.Z = 0; }
                Ball.Pos.CopyFrom(HandPosition());
            }

            // ---- bodies cannot overlap ----
            int giveWayTo = Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.Shots > 0 ? Interference.Striker : -1;
            SimEvent pc = BodyCollision.BodyVsBody(Players[0].Player, 0, Players[1].Player, 1,
                _bodiesTouching, out _bodiesTouching, Time, giveWayTo);
            if (pc != null) StepEvents.Add(pc);

            // ---- the ball ----
            if (!Ball.Asleep || Phase != MatchPhase.AwaitToss)
            {
                _prevBall.CopyFrom(Ball.Pos);
                int before = StepEvents.Count;
                if (_curveT > 0 && !Ball.Asleep) { Ball.Vel.X += _curveA * dt; _curveT -= dt; }   // chaos: Curveball
                BallPhysics.Step(Ball, Surfaces, dt, StepEvents);
                for (int k = before; k < StepEvents.Count; k++) StepEvents[k].Time = Time;
                if (_chaosStriker >= 0) ChaosBall(before);
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
                    case SimEventType.RacketContact: Referee.OnRacketContact(ev, Players[ev.PlayerIndex].Player.Pos); _lastStriker = ev.PlayerIndex; break;
                    case SimEventType.Impact: Referee.OnImpact(ev); if (ev.Nick && _lastStriker >= 0) _nickBy = _lastStriker; break;
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

        /// <summary>The last rally's call on interference (an appeal or the referee's own stop), as it was made; null when the
        /// rally ended any other way (the ball hitting a player included).</summary>
        public CallSnapshot LastCall { get; private set; }

        void FinishRally()
        {
            LastOutcome = Referee.Outcome;
            LastCall = null;
            var o = LastOutcome;
            if (o != null && (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke) && Interference.Striker >= 0 &&
                (o.Reason == null || !o.Reason.Contains("ball hit")))
            {
                int si = Interference.Striker;
                var s = Players[si].Player.Pos; var ob = Players[1 - si].Player.Pos;
                LastCall = new CallSnapshot
                {
                    Kind = o.Kind, Obstruction = Interference.ThisTurn, Striker = si,
                    StrikerX = s.X, StrikerZ = s.Z, TargetX = Interference.TargetX, TargetZ = Interference.TargetZ, ObstructorX = ob.X, ObstructorZ = ob.Z,
                };
            }
            int bonus = RunModsAtRallyEnd();                                  // P5 (0, and nothing changed, without a run)
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
            Score.Apply(LastOutcome, bonus);
            Phase = MatchPhase.RallyOver;
            PhaseTime = 0;
            var h = RallyEnded;
            if (h != null) h(LastOutcome);
        }

        // ---------------- P5: the Gauntlet run's abilities at a rally's end ----------------

        int _lastStriker = -1, _nickBy = -1;

        /// <summary>Second Serve (a fault replayed), then the winner's extra points (an ace, a nick), Vampire, Momentum.</summary>
        int RunModsAtRallyEnd()
        {
            var o = LastOutcome;
            if (o == null) return 0;
            if (Players[0].Player.Mods == null && Players[1].Player.Mods == null) return 0;
            if ((o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.FootFault) && o.Loser >= 0)
            {
                var lm = Players[o.Loser].Player.Mods;
                if (lm != null && lm.SecondServe && !lm.SecondUsed)
                {
                    lm.SecondUsed = true;
                    LastOutcome = new RallyOutcome { Kind = RallyEnd.Let, Reason = "Second serve - serve again", Time = o.Time, Point = o.Point };
                    return 0;
                }
            }
            if (o.Loser >= 0 && o.Kind != RallyEnd.Let && o.Kind != RallyEnd.Fault && o.Kind != RallyEnd.FootFault)
            {
                // chaos: Shield (your own error a let), Rewind (any lost rally a let)
                var lm = Players[o.Loser].Player.Mods;
                if (lm != null)
                {
                    bool ownError = (o.Kind == RallyEnd.Down || o.Kind == RallyEnd.Out) && _lastStriker == o.Loser;
                    if (ownError && lm.ShieldLeft)
                    {
                        lm.ShieldsUsed++;
                        LastOutcome = new RallyOutcome { Kind = RallyEnd.Let, Reason = "Shield - play it again", Time = o.Time, Point = o.Point };
                        Note(o.Loser, "shield", 0);
                        return 0;
                    }
                    if (lm.RewindsUsed < lm.Rewinds)
                    {
                        lm.RewindsUsed++;
                        LastOutcome = new RallyOutcome { Kind = RallyEnd.Let, Reason = "Rewind - the rally again", Time = o.Time, Point = o.Point };
                        Note(o.Loser, "rewind", 0);
                        return 0;
                    }
                }
            }
            int w = o.Winner;
            if (w < 0 || o.Kind == RallyEnd.Let) return 0;
            int bonus = 0;
            var wm = Players[w].Player.Mods;
            if (wm != null)
            {
                bool rallyWon = o.Kind == RallyEnd.NotUp && _lastStriker == w;
                if (rallyWon && Referee.Shots == 1 && w == Score.Server) bonus += wm.AceBonus;
                if (rallyWon && _nickBy == w) bonus += wm.NickBonus;
                if (rallyWon && wm.VampireHeal > 0) Players[w].Player.Stamina = System.Math.Min(1, Players[w].Player.Stamina + wm.VampireHeal);
                // chaos: the points that stack (a jackpot, a fireball, a combo, the walls, a long rally, the first point)
                bool first = Score.Points[0] == 0 && Score.Points[1] == 0;
                int chaos = wm.ChaosBonus(rallyWon, _ultShot[w], _fireShot[w], _curvedShot[w], _chaosStriker == w ? _wallsAfterFront : 0, Referee.Shots, first, wm.Streak);
                if (chaos > 0) { bonus += chaos; Note(w, "bonus", chaos); }
                wm.MeterWin();
                wm.Streak++;
            }
            var losing = Players[1 - w].Player.Mods;
            if (losing != null) losing.Streak = 0;
            return bonus;
        }

        // ---------------- the Gauntlet chaos remake (2026-10-28): the cards that bend the rules ----------------

        /// <summary>What the chaos cards did, in order (the game shows them: the ultimate, a freeze, a fireball, the points).
        /// Only a Gauntlet match ever adds to it.</summary>
        public readonly List<ChaosNote> Chaos = new List<ChaosNote>();
        void Note(int player, string kind, int value) { Chaos.Add(new ChaosNote { Player = player, Kind = kind, Value = value, Time = Time }); }

        bool _gameInit;
        int _chaosStriker = -1, _wallsAfterFront, _floorsAfterFront;
        string _chaosTech;
        bool _afterFront, _curveArmed;
        double _curveA, _curveT;
        readonly bool[] _ultShot = new bool[2], _fireShot = new bool[2], _curvedShot = new bool[2];

        static RunMods ModsOf(TestPlayer p) { if (p.Mods == null) p.Mods = new RunMods(); return p.Mods; }

        /// <summary>A stroke: the ultimate (Time Stop), the fireball spent, the meter, what it does to the opponent.</summary>
        void ChaosAtContact(int i, SimEvent contact)
        {
            var mm = Players[i].Player.Mods;
            _ultShot[i] = false; _fireShot[i] = false; _curvedShot[i] = false;
            if (mm == null) { _chaosStriker = -1; _curveT = 0; return; }
            _chaosStriker = i; _chaosTech = contact.Technique;
            _afterFront = false; _wallsAfterFront = 0; _floorsAfterFront = 0; _curveT = 0; _curveArmed = false;
            mm.ReachCut = 0; mm.GraceCut = 0;                                   // their own stroke made: the cuts against them spent
            var op = Players[1 - i].Player;
            _ultShot[i] = mm.UltNow; _fireShot[i] = mm.ShotFire;
            if (mm.ShotFire) { if (mm.FireShots > 0) mm.FireShots--; Note(i, "fire", mm.FireShots); }
            if (mm.UltNow)
            {
                Note(i, "ult", 0);
                if (mm.FreezeSeconds > 0) { ModsOf(op).FreezeTimer = mm.FreezeSeconds; Note(1 - i, "freeze", 0); }
            }
            mm.UltNow = false;
            mm.MeterShot();
            if (mm.SlowFieldSeconds > 0) { var o = ModsOf(op); o.SlowTimer = mm.SlowFieldSeconds; o.SlowScale = mm.SlowFieldScale; }
            if (mm.HasteSeconds > 0) mm.HasteTimer = mm.HasteSeconds;
            if (mm.HeavyCut > 0 && (contact.Technique == "drive" || contact.Technique == "kill")) ModsOf(op).ReachCut = mm.HeavyCut;
            if (mm.KnuckleCut > 0) ModsOf(op).GraceCut = mm.KnuckleCut;
            _curveArmed = mm.CurveAccel > 0 && (contact.Technique == "drive" || contact.Technique == "kill");
        }

        /// <summary>The ball's walls and bounces after a chaos stroke: the curve starts at the front wall, Ricochet speeds it off
        /// the side walls (and Wall Ride counts them), Dead Drop kills the bounce, Quake shakes the opponent.</summary>
        void ChaosBall(int from)
        {
            var mm = Players[_chaosStriker].Player.Mods;
            if (mm == null) return;
            for (int k = from; k < StepEvents.Count; k++)
            {
                var ev = StepEvents[k];
                if (ev.Type != SimEventType.Impact) continue;
                switch (ev.SurfaceClass)
                {
                    case "frontWall":
                        if (_afterFront) break;
                        _afterFront = true;
                        if (_curveArmed)
                        {
                            _curveArmed = false;
                            _curveA = (Ball.Vel.X >= 0 ? 1 : -1) * mm.CurveAccel; _curveT = RunMods.CurveSeconds;
                            _curvedShot[_chaosStriker] = true;
                            Note(_chaosStriker, "curve", 0);
                        }
                        break;
                    case "sideWall":
                    case "backWall":
                        if (!_afterFront || _floorsAfterFront > 0) break;
                        _wallsAfterFront++;
                        if (ev.SurfaceClass == "sideWall" && mm.Level("ricochet") > 0)
                        {
                            double r = mm.RicochetPace;
                            Ball.Vel.X *= r; Ball.Vel.Y *= r; Ball.Vel.Z *= r;
                            Note(_chaosStriker, "ricochet", _wallsAfterFront);
                        }
                        break;
                    case "floor":
                        if (!_afterFront) break;
                        _floorsAfterFront++;
                        if (_floorsAfterFront != 1) break;
                        if ((_chaosTech == "drop" || _chaosTech == "boast") && mm.Level("deaddrop") > 0)
                        {
                            if (Ball.Vel.Y > 0) Ball.Vel.Y *= mm.DeadDropKeep;
                            Note(_chaosStriker, "deaddrop", 0);
                        }
                        if (_chaosTech == "kill" && mm.QuakeSeconds > 0)
                        {
                            var o = ModsOf(Players[1 - _chaosStriker].Player);
                            o.FreezeTimer = System.Math.Max(o.FreezeTimer, mm.QuakeSeconds);
                            Note(_chaosStriker, "quake", 0);
                        }
                        break;
                }
            }
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
            var mm = Players[i].Player.Mods;                                  // P5: Matador - after your drop or boast, the opponent is slower
            if (mm != null && mm.MatadorSeconds > 0 && (contact.Technique == "drop" || contact.Technique == "boast"))
            {
                var op = Players[1 - i].Player;
                if (op.Mods == null) op.Mods = new RunMods();
                op.Mods.SlowTimer = mm.MatadorSeconds;
                op.Mods.SlowScale = RunMods.MatadorSpeed;
            }
            ChaosAtContact(i, contact);

            SplitStep.Contact(i, Time, Players[1 - i].Player);
        }

        // ---------------- flow control ----------------

        /// <summary>From GameOver: start the next game of the match.</summary>
        public void NextGame()
        {
            if (Phase != MatchPhase.GameOver) return;
            Score.NextGame();
            for (int i = 0; i < 2; i++) if (Players[i].Player.Mods != null) Players[i].Player.Mods.SecondUsed = false;   // P5: a new game, a new second serve
            _gameInit = false;                                                // chaos: shields, rewinds, The World's meter
            if (PhysicsConfig.Current.Fatigue.FreshEachGame) { Players[0].Player.Stamina = 1.0; Players[1].Player.Stamina = 1.0; }   // P7: the break restores
            SetUpServe();
        }

        /// <summary>Start a fresh match with the same settings.</summary>
        public void Restart(int firstServer = 0)
        {
            Score = MatchScore.FromConfig(firstServer);
            History.Clear();
            Chaos.Clear(); _gameInit = false;
            ShotsPerRally.Clear();
            Stats.Clear();
            LastOutcome = null;
            Ball.Reset(null, null, null, PhysicsConfig.Current.Match.StartTemperature);
            Players[0].Player.Stamina = 1.0; Players[1].Player.Stamina = 1.0;
            if (PhysicsConfig.Current.Match.WarmUpEnabled) StartWarmUp(); else SetUpServe();
        }
    }
}
