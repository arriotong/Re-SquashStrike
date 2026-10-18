// ===========================================================
// Assets/SquashSim/Arcade/ArcadeSession.cs
// P2-M2: one Target Arcade challenge (or drill) as a deterministic
// sim: one athlete on the simple hitting system, the ball, the court,
// the feed machine, the targets, the score.
//
// A shot ("attempt") runs: feed -> the feed meets the front wall ->
// you strike it -> its first front-wall impact (rings) -> its first
// floor bounce (zones) -> the second bounce ends it. A ball you never
// strike, a shot that goes down or out, are misses. The next feed
// comes a moment after the last shot is decided - not on a timer.
//
//   Time Attack   60 s, rings; combo multiplier, great-hit bonus
//   Precision     20 balls, a ring each; 300 / 200 / 100 by distance
//   Shot Menu     20 balls; the called shot type into its zone
//   Streak        rings until the first miss (they shrink); score = hits
//   Serve Targets 10 serves (tap lob / hold hard) into back-quarter boxes
//   Wall Rally    one feed, then keep it going off the front wall alone
//   Drills        Length, Drop, Boast-Drive, Nick Hunt (hits / balls),
//                 Ghosting (no ball: spots and back to the T)
//
// Kept apart from LabSimulation, which SimParity holds bit-identical.
// Same seed + same inputs = same score (InputLog).
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim.Arcade
{
    public sealed class ArcadeSession
    {
        public readonly Challenge Challenge;
        public readonly List<Surface> Surfaces = CourtSurfaces.Create();
        public readonly BallState Ball = new BallState();
        public readonly TestPlayerController Player = new TestPlayerController();
        public readonly Lab.BallLauncher Launcher;
        /// <summary>This step's sim events (contacts, impacts).</summary>
        public readonly List<SimEvent> StepEvents = new List<SimEvent>();
        /// <summary>This step's arcade events (hits, misses, new targets, over).</summary>
        public readonly List<ArcadeEvent> Events = new List<ArcadeEvent>();
        /// <summary>The targets up now.</summary>
        public readonly List<Target> Targets = new List<Target>();

        public double Time { get; private set; }
        public long StepCount { get; private set; }
        public bool Over { get; private set; }
        public int Score { get; private set; }
        public int Hits { get; private set; }
        public int Attempts { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int GreatHits { get; private set; }
        public int BallsUsed { get; private set; }
        /// <summary>Shot Menu / Boast-Drive: the shot called for this ball.</summary>
        public string CalledShot { get; private set; }
        /// <summary>Serve Targets: the box the server serves from (+1 right, -1 left).</summary>
        public int ServeSide { get; private set; } = 1;
        public bool Serving { get { return _serving; } }

        public double TimeLeft { get { return Challenge.Seconds > 0 ? System.Math.Max(0, Challenge.Seconds - Time) : -1; } }
        public int BallsLeft { get { return Challenge.Balls > 0 ? System.Math.Max(0, Challenge.Balls - BallsUsed) : -1; } }
        public int Stars { get { return Challenge.StarsFor(Score); } }
        public bool IsDrill { get { return Challenge.Mode == ArcadeMode.Drill; } }

        public const double FeedGap = 0.9, QuickGap = 0.55, AttemptTimeout = 7.0, FirstFeedDelay = 1.2;

        enum Stage { Waiting, Fed, Done }
        Stage _stage = Stage.Waiting;
        double _gap = FirstFeedDelay, _attemptTime;
        bool _feedFront, _struck, _frontAfterContact, _great, _serving, _toT;
        int _feedFloors, _floorsAfterFront;
        string _technique;
        V3 _frontPoint;
        double _ringRadius;
        uint _rng;

        /// <summary>The seed this play uses (the challenge's own unless another is given): feeds, targets, called shots.</summary>
        public uint Seed { get; private set; }

        public ArcadeSession(Challenge challenge, uint? seed = null)
        {
            Challenge = challenge;
            Launcher = new Lab.BallLauncher(Ball);
            Seed = seed ?? challenge.Seed;
            _rng = Seed * 2654435761u + 97u;
            _ringRadius = challenge.RingRadius;
            Player.ArcadeHitEnabled = true;
            Player.WallAimEnabled = true;
            Player.Prediction.MaxFloorBounces = 1;                     // like a match: before the second bounce
            Player.Player.Spawn.X = 0; Player.Player.Spawn.Y = 0; Player.Player.Spawn.Z = 6.0;
            if (challenge.Drill == DrillKind.Ghosting) Player.Player.Spawn.Z = PhysicsConfig.Current.Match.TZ;
            Player.Reset();
            Ball.Reset(new V3(0, 0.03, 2.0), new V3(0, 0, 0), null, 40);
            Ball.Asleep = true;
        }

        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        // ---------------- the fixed step ----------------

        /// <summary>One fixed step. Consumes the intent's one-shot edges.</summary>
        public void Step(InputIntent intent, double dt)
        {
            StepEvents.Clear();
            Events.Clear();
            if (Over) { intent.ClearEdges(); return; }
            Time += dt;
            StepCount++;

            if (Challenge.Seconds > 0 && Time >= Challenge.Seconds) { Finish(); intent.ClearEdges(); return; }

            if (_stage == Stage.Waiting)
            {
                _gap -= dt;
                if (_gap <= 0) NextBall();
            }
            else if (_stage == Stage.Fed)
            {
                _attemptTime += dt;
                if (_attemptTime > AttemptTimeout && Challenge.Drill != DrillKind.Ghosting && !_serving)
                    Miss(null, "TOO SLOW");
            }

            Launcher.Step(dt);
            SimEvent serve = _serving ? StepServe(intent, dt) : null;

            Player.ServeStroke = serve != null;
            Player.ContactEnabled = _stage == Stage.Fed && !_serving && (Challenge.Mode == ArcadeMode.WallRally
                ? (_feedFront && !_struck) || (_struck && _frontAfterContact)
                : _feedFront && !_struck);
            SimEvent contact = Player.Step(intent, Ball, Surfaces, dt);
            Player.ServeStroke = false;
            intent.ClearEdges();
            if (serve != null) contact = serve;
            if (contact != null)
            {
                contact.PlayerIndex = 0;
                StepEvents.Add(contact);
                OnContact(contact);
            }

            if (!Ball.Asleep) BallPhysics.Step(Ball, Surfaces, dt, StepEvents);
            for (int i = 0; i < StepEvents.Count; i++)
                if (StepEvents[i].Type == SimEventType.Impact) OnImpact(StepEvents[i]);

            if (Challenge.Drill == DrillKind.Ghosting) StepGhosting();
        }

        // ---------------- feeds ----------------

        void NextBall()
        {
            if (Challenge.Balls > 0 && BallsUsed >= Challenge.Balls) { Finish(); return; }
            BallsUsed++;
            Attempts++;
            _stage = Stage.Fed;
            _attemptTime = 0;
            _feedFront = false; _struck = false; _frontAfterContact = false; _great = false;
            _feedFloors = 0; _floorsAfterFront = 0; _technique = null; _frontPoint = null;
            Targets.Clear();
            CalledShot = null;

            switch (Challenge.Mode)
            {
                case ArcadeMode.ServeTargets:
                    ServeSide = BallsUsed % 2 == 1 ? 1 : -1;
                    SetServeTargets();
                    BeginServe();
                    return;
                case ArcadeMode.ShotMenu:
                    CalledShot = Challenge.Shots[(int)(Rnd() * Challenge.Shots.Length) % Challenge.Shots.Length];
                    Targets.Add(ShotZone(CalledShot));
                    Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.ShotCalled, Note = CalledShot.ToUpperInvariant(), Target = Targets[0] });
                    break;
                case ArcadeMode.Drill:
                    if (Challenge.Drill == DrillKind.Ghosting) { NextSpot(); return; }
                    if (Challenge.Drill == DrillKind.BoastDrive) CalledShot = BallsUsed % 2 == 1 ? "boast" : "drive";
                    Targets.Add(DrillZone());
                    break;
                default:
                    NewRing();
                    break;
            }
            Feed();
        }

        void NewRing()
        {
            Targets.Clear();
            double x = (Rnd() * 2 - 1) * 2.5, y = 0.9 + Rnd() * 2.0;
            var t = Target.Ring(x, y, _ringRadius, Challenge.Mode == ArcadeMode.Precision ? 300 : 100);
            Targets.Add(t);
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.NewTarget, Target = t });
        }

        /// <summary>The feed machine: from behind the player toward the front wall, so the ball comes back to them.</summary>
        void Feed()
        {
            double side = Challenge.FeedBothSides ? (Rnd() < 0.5 ? -1 : 1) : 1;
            double px = side * (0.4 + 0.5 * Rnd()), pz = 7.4;
            // High and firm enough off the (dead) front wall to come back past the short line, like a length ball.
            double wx = side * (0.9 + 0.9 * Rnd()), wy = 2.0 + 0.8 * Rnd();
            if (Challenge.Drill == DrillKind.Drop || Challenge.Drill == DrillKind.NickHunt) { wy = 1.6 + 0.5 * Rnd(); }
            double speed = Challenge.FeedSpeed + (Rnd() * 2 - 1) * Challenge.FeedSpeedSpread;
            Feeder.Front(Launcher, px, pz, wx, wy, speed);
        }

        // ---------------- the serve (Serve Targets) ----------------

        void BeginServe()
        {
            _serving = true;
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
            // In the box (never out of it), the ball in the hand; press = start, hold = charge, let go = struck.
            var p = Player.Player;
            Lab_ServiceBoxClamp(p);
            Ball.Pos.CopyFrom(HandPosition());
            Ball.Asleep = true;
            var arc = Player.Arcade;
            if (intent.Hit.Pressed) { intent.Hit.Pressed = false; arc.BeginServeCharge(); }
            if (!arc.ServeCharging) return null;
            if (intent.Hit.Held) { arc.ChargeServe(dt); return null; }
            var m = PhysicsConfig.Current.Match;
            Ball.Reset(HandPosition(), new V3(0, m.ServeTossSpeed, 0), null, Ball.Temperature > 0 ? Ball.Temperature : 40);
            BallPhysics.Wake(Ball);
            Player.ServeStroke = true;
            Player.ServeDefaultX = -ServeSide * 0.9;
            _serving = false;
            return arc.ServeNow(Player, Ball, Surfaces);
        }

        void Lab_ServiceBoxClamp(TestPlayer p)
        {
            RallyReferee.ServiceBox(ServeSide, out double x0, out double x1, out double z0, out double z1);
            double r = System.Math.Min(PhysicsConfig.Current.Player.Radius, 0.3);
            if (p.Pos.X < x0 + 0.05) { p.Pos.X = x0 + 0.05; p.Vel.X = 0; }
            if (p.Pos.X > x1 - r) { p.Pos.X = x1 - r; p.Vel.X = 0; }
            if (p.Pos.Z < z0 + 0.05) { p.Pos.Z = z0 + 0.05; p.Vel.Z = 0; }
            if (p.Pos.Z > z1 - 0.05) { p.Pos.Z = z1 - 0.05; p.Vel.Z = 0; }
        }

        void SetServeTargets()
        {
            // The receiver's back quarter (the other side): the deep corner box, then the back box.
            double s = -ServeSide, zs = Challenge.ZoneScale;
            double deepZ = LENGTH - 2.0 * zs, cornerX = HALF_W - 1.6 * zs;
            Targets.Add(Target.Zone(s * cornerX, s * HALF_W, deepZ, LENGTH, 300, "DEEP"));
            Targets.Add(Target.Zone(s * 0.0, s * HALF_W, SHORT_LINE_Z + SERVICE_BOX, LENGTH, 200, "BACK"));
        }

        // ---------------- zones ----------------

        Target ShotZone(string shot)
        {
            double zs = Challenge.ZoneScale;
            switch (shot)
            {
                case "drive": return Target.Zone(-HALF_W, HALF_W, LENGTH - 3.6 * zs, LENGTH, 150, "DRIVE");
                case "lob": return Target.Zone(-HALF_W, HALF_W, LENGTH - 2.6 * zs, LENGTH, 150, "LOB");
                case "drop": return Target.Zone(-HALF_W, HALF_W, 0, 0.8 + 2.4 * zs, 150, "DROP");
                case "kill": return Target.Zone(-HALF_W, HALF_W, 0, 1.5 + 2.8 * zs, 150, "KILL");
                default: return Target.Zone(-HALF_W, HALF_W, 0, 1.5 + 3.0 * zs, 150, "BOAST");
            }
        }

        Target DrillZone()
        {
            switch (Challenge.Drill)
            {
                case DrillKind.Length: return Target.Zone(-HALF_W, HALF_W, SHORT_LINE_Z + SERVICE_BOX, LENGTH, 100, "LENGTH");
                case DrillKind.Drop: return Target.Zone(-HALF_W, HALF_W, 0, 2.6, 100, "DROP");
                case DrillKind.BoastDrive:
                    return CalledShot == "boast" ? Target.Zone(-HALF_W, HALF_W, 0, 4.5, 100, "BOAST")
                                                 : Target.Zone(-HALF_W, HALF_W, SHORT_LINE_Z + SERVICE_BOX, LENGTH, 100, "DRIVE");
                default: return new Target { Kind = TargetKind.Nick, Points = 100, Label = "NICK" };
            }
        }

        // ---------------- judging ----------------

        void OnContact(SimEvent e)
        {
            if (_stage != Stage.Fed) return;
            if (Challenge.Mode == ArcadeMode.WallRally && _struck)
            {
                // The next shot of the rally: a new attempt (no feed).
                Attempts++;
                _attemptTime = 0;
                _frontAfterContact = false; _floorsAfterFront = 0;
            }
            _struck = true;
            _technique = e.Technique;
            _great = e.Technique != null && !e.Technique.Contains("serve") && ArcadeStroke.IsGreat(e);
        }

        void OnImpact(SimEvent e)
        {
            if (_stage != Stage.Fed) return;
            string cls = e.SurfaceClass;
            if (!_struck)
            {
                // The feed on its way to you: it must meet the front wall, and you must strike it before it bounces twice.
                if (cls == "frontWall") _feedFront = true;
                else if (_feedFront && cls == "floor" && ++_feedFloors >= 2) Miss(null, "MISSED");
                return;
            }
            if (_struck && RallyReferee.OutReason(e) != null) { Miss(e.Point, "OUT"); return; }
            if (!_frontAfterContact)
            {
                if (cls == "frontWall")
                {
                    _frontAfterContact = true;
                    _frontPoint = e.Point.Clone();
                    JudgeFront(e);
                }
                else if (cls == "floor" || cls == "tin") Miss(e.Point, cls == "tin" ? "TIN" : "DOWN");
                return;
            }
            if (e.Nick && Challenge.Drill == DrillKind.NickHunt && _floorsAfterFront == 0) { Hit(Targets.Count > 0 ? Targets[0] : null, e.Point, 100); return; }
            if (cls == "floor")
            {
                _floorsAfterFront++;
                if (_floorsAfterFront == 1) JudgeFloor(e);
                else if (_floorsAfterFront >= 2 && _stage == Stage.Fed)
                {
                    if (Challenge.Mode == ArcadeMode.WallRally) { Finish(); return; }     // not up: the rally is over
                    if (Challenge.Drill == DrillKind.NickHunt) Miss(e.Point, "NO NICK");
                    else EndAttempt();
                }
            }
        }

        void JudgeFront(SimEvent e)
        {
            var mode = Challenge.Mode;
            if (mode == ArcadeMode.ServeTargets)
            {
                if (e.Point.Y <= SERVICE_LINE_H) Fault(e.Point, "UNDER THE LINE");
                return;
            }
            if (mode == ArcadeMode.ShotMenu || mode == ArcadeMode.Drill) return;          // judged on the floor
            var t = Targets.Count > 0 ? Targets[0] : null;
            if (t == null || t.Kind != TargetKind.FrontRing) return;
            if (mode == ArcadeMode.Precision)
            {
                int ring = t.PrecisionRing(e.Point);
                if (ring > 0) Hit(t, e.Point, 100 * ring); else Miss(e.Point, "MISSED");
            }
            else if (t.InRing(e.Point)) Hit(t, e.Point, t.Points);
            else Miss(e.Point, "MISSED");
            if (mode == ArcadeMode.WallRally && !Over)
            {
                NewRing();                                            // the next target; the rally goes on
                _stage = Stage.Fed;
            }
        }

        void JudgeFloor(SimEvent e)
        {
            var mode = Challenge.Mode;
            if (mode == ArcadeMode.ServeTargets)
            {
                bool legal = e.Point.X * ServeSide < 0 && e.Point.Z > SHORT_LINE_Z;
                if (!legal) { Fault(e.Point, "FAULT"); return; }
                if (Targets[0].InZone(e.Point)) Hit(Targets[0], e.Point, 300);
                else if (Targets[1].InZone(e.Point)) Hit(Targets[1], e.Point, 200);
                else Hit(null, e.Point, 100);
                return;
            }
            if (mode == ArcadeMode.ShotMenu || (mode == ArcadeMode.Drill && Challenge.Drill != DrillKind.NickHunt))
            {
                var t = Targets.Count > 0 ? Targets[0] : null;
                bool rightShot = CalledShot == null || _technique == CalledShot;
                if (Challenge.Drill == DrillKind.Drop && _frontPoint != null && _frontPoint.Y > 1.4) rightShot = false;
                if (!rightShot) Miss(e.Point, "WRONG SHOT");
                else if (t != null && t.InZone(e.Point)) Hit(t, e.Point, t.Points);
                else Miss(e.Point, "MISSED");
            }
        }

        public static double ComboMultiplier(int combo) { return combo >= 8 ? 3 : combo >= 5 ? 2 : combo >= 3 ? 1.5 : 1; }

        void Hit(Target t, V3 at, int basePoints)
        {
            if (_stage != Stage.Fed) return;
            Hits++;
            Combo++;
            if (Combo > BestCombo) BestCombo = Combo;
            int pts;
            if (Challenge.Mode == ArcadeMode.Streak) pts = 1;
            else if (IsDrill) pts = 1;
            else pts = (int)System.Math.Round(basePoints * ComboMultiplier(Combo) * (_great ? 1.5 : 1));
            if (_great) GreatHits++;
            Score += pts;
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.Hit, Target = t, Point = at != null ? at.Clone() : null, Points = pts, Combo = Combo, Great = _great });
            if (Challenge.Mode == ArcadeMode.Streak) _ringRadius = System.Math.Max(0.18, _ringRadius * 0.94);
            if (Challenge.Mode != ArcadeMode.WallRally) EndAttempt();
        }

        void Miss(V3 at, string note)
        {
            if (_stage != Stage.Fed) return;
            Combo = 0;
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.Miss, Point = at != null ? at.Clone() : null, Note = note, Combo = 0 });
            if (Challenge.Mode == ArcadeMode.Streak) { Finish(); return; }
            if (Challenge.Mode == ArcadeMode.WallRally)
            {
                // A missed ring keeps the rally going; down / out ends it.
                if (note == "OUT" || note == "TIN" || note == "DOWN" || note == "TOO SLOW" || (note == "MISSED" && !_frontAfterContact)) Finish();
                return;
            }
            EndAttempt();
        }

        void Fault(V3 at, string note)
        {
            if (_stage != Stage.Fed) return;
            Combo = 0;
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.Fault, Point = at != null ? at.Clone() : null, Note = note });
            EndAttempt();
        }

        void EndAttempt()
        {
            _stage = Stage.Waiting;
            _gap = Challenge.Mode == ArcadeMode.TimeAttack ? QuickGap : FeedGap;
            if (Challenge.Balls > 0 && BallsUsed >= Challenge.Balls) { _gap = 0.6; }
        }

        void Finish()
        {
            if (Over) return;
            Over = true;
            _stage = Stage.Done;
            Targets.Clear();
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.Over, Points = Score });
        }

        // ---------------- ghosting ----------------

        static readonly double[,] Corners = { { -2.3, 1.3 }, { 2.3, 1.3 }, { -2.7, 4.6 }, { 2.7, 4.6 }, { -2.3, 8.4 }, { 2.3, 8.4 } };

        void NextSpot()
        {
            Targets.Clear();
            int i = (int)(Rnd() * 6) % 6;
            var t = Target.StandOn(Corners[i, 0], Corners[i, 1], 0.55);
            Targets.Add(t);
            _toT = false;
            Events.Add(new ArcadeEvent { Kind = ArcadeEventKind.NewTarget, Target = t });
        }

        void StepGhosting()
        {
            if (_stage != Stage.Fed || Targets.Count == 0) return;
            var t = Targets[0];
            if (!t.OnSpot(Player.Player.Pos)) return;
            if (!_toT)
            {
                Hit(t, new V3(t.X, 0, t.Z0), 1);
                _stage = Stage.Fed;                                   // ghosting carries straight on: back to the T
                Targets.Clear();
                var m = PhysicsConfig.Current.Match;
                Targets.Add(Target.StandOn(m.TX, m.TZ, 0.6));
                _toT = true;
            }
            else NextSpot();
        }
    }
}
