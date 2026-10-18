// ===========================================================
// Assets/SquashSim/Player/ArcadeStroke.cs
// The simple hitting system (game profile; local human players only),
// after the original SquashStrike (player.js attemptHit):
//
//   hold Hit / Touch   charge (up to ChargeFull seconds: more pace on drives / kills)
//   release            if the ball is within REACH of you right now, it is
//                      HIT - no racket arc to line up, no swing timing.
//                      Released a little early (the ball will be in reach
//                      within Grace seconds)? It still connects as the ball
//                      comes in. Otherwise: a miss.
//   auto step          while you hold (or have just let go) and the ball is
//                      about to arrive, the athlete steps toward it (up to
//                      LungeMax) - the Mario-style "get there" help.
//   the shot           goes to your aim target on the wall, accurately; only
//                      a stretched ball (past the sweet spot) drifts a little.
//                      The shot type comes from the aim height (the
//                      original SquashStrike): above LobHeight a LOB; below
//                      LowHeight a DROP - or a KILL if charged past
//                      KillCharge; otherwise a DRIVE.
//   dive               (Space / pad B) a lunge in the move direction: 1.5x
//                      reach (less accurate), then a slow recovery.
//   sprint             (Shift / pad LB) faster, at a stamina cost.
//   boast              tap E / pad RB to arm it for the next stroke: off the
//                      side wall by the ball, across to the aim point on the
//                      other half of the front wall (the ring shows where).
//   early              being set (in position) before the ball arrives makes
//                      the shot tighter: less spread, far fewer mishits.
//   drop               struck at a real pace for its distance, low on the
//                      front wall - not floated.
//
// The hit is a normal racket contact to everything downstream (the
// referee, sounds, pop-ups, the timing note), and the swing still plays
// (backswing while charging, the stroke through the contact) for the
// animation. The AI keeps the physical swing; the lab never sees this.
// Deterministic: its spread uses its own seeded generator.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class ArcadeHitSection
    {
        public double Reach;            // horizontal distance from the body the ball can be hit at
        public double SweetSpot;        // within this, a clean, accurate hit
        public double MinHeight, MaxHeight;
        public double Grace;            // released this early, it still connects when the ball comes in
        public double ChargeFull;       // seconds of holding for a full charge
        public double DriveSpeed, ChargeBonus, LobSpeed, LobHeight, TouchSpeed;
        public double MaxSpread;        // aim error at full stretch (m, on the wall)
        public double GrossMissChance;  // at full stretch, the chance the shot goes badly wrong
        public double LungeMax, LungeSpeed;   // the "auto jump closer": a step toward the ball while the stroke is ready
        public double BackWallSafeHeight;     // a shot that would reach the back wall on the full above this is played softer
        // The original SquashStrike: shot type from aim height; dive; sprint.
        public double LowHeight, KillCharge, KillHeightAboveTin, DropHeightAboveTin, DropSpeed;
        public double DiveSeconds, DiveSpeedScale, DiveRecovery, DiveRecoverySpeed, DiveReachScale, DiveStamina, DiveMinStamina;
        public double SprintScale, SprintDrain, WindedStamina;
        public double BoastPaceScale, BoastMaxHeight, BoastMinHeight, BoastCross;
        public double ServeSpeed, ServeHardCharge, ServeChargeBonus, ServeDepth;
        public double LobRise, LobMaxSpeed, LobDepth, LobWallMax;
        public double EarlyFull, EarlySpreadCut, EarlyMishitCut, EarlyNear;
        public double DropFlightTime, DropSpeedMin, DropSpeedMax;
    }

    public sealed class ArcadeStroke
    {
        double _charge;
        bool _charging;
        string _stroke = "hit";
        double _pending = -1;          // grace time left for a released stroke waiting for the ball
        double _pendingCharge, _lunged;
        bool _boast;
        /// <summary>The next stroke will be a boast (E / pad RB tapped).</summary>
        public bool BoastArmed { get { return _boast; } }
        double _setTime;
        /// <summary>0..1: how early the athlete was set for the last stroke (1 = in position EarlyFull seconds ahead).</summary>
        public double LastEarliness { get; private set; }
        /// <summary>Seconds in position for the ball now coming (presentation and tests).</summary>
        public double SetTime { get { return _setTime; } }
        uint _rng;
        readonly InputIntent _idle = new InputIntent();
        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _vel = new V3();
        readonly V3 _aimPoint = new V3();
        readonly V3 _good = new V3();
        readonly V3 _scratchVel = new V3();
        readonly V3 _best = new V3();
        static readonly Prediction _fakePred = new Prediction();

        /// <summary>0..1 how charged the stroke being held is (presentation).</summary>
        public double ChargeRatio { get { var c = PhysicsConfig.Current.ArcadeHit; return c == null ? 0 : System.Math.Min(1, _charge / c.ChargeFull); } }
        public bool Charging { get { return _charging || _serveCharging; } }

        // ---- the serve: tap = a lob serve (high, deep), hold = charge a hard serve ----
        bool _serveCharging;
        /// <summary>The server is holding Hit: the serve is charging (the ball stays in the hand).</summary>
        public bool ServeCharging { get { return _serveCharging; } }
        public void BeginServeCharge() { _serveCharging = true; _charge = 0; }
        public void ChargeServe(double dt) { if (_serveCharging) _charge += dt; }

        // ---- dive ----
        double _diveT = -1, _diveRecover, _diveX, _diveZ;
        /// <summary>Mid-dive (the lunge).</summary>
        public bool Diving { get { return _diveT > 0; } }
        /// <summary>Seconds of post-dive recovery left (slower, a little less accurate).</summary>
        public double DiveRecovery { get { return _diveRecover; } }
        public int DiveCount { get; private set; }

        /// <summary>Sprint and dive recovery as a top-speed scale; sprinting costs stamina.</summary>
        public double SpeedScaleFor(TestPlayer p, InputIntent intent, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double s = 1.0;
            bool moving = intent.MoveMagnitude > 0.1;
            if (intent.Sprint && moving && p.Stamina > c.WindedStamina)
            {
                s *= c.SprintScale;
                p.Stamina = System.Math.Max(0, p.Stamina - c.SprintDrain * dt);
            }
            return s;
        }

        /// <summary>The dive. Returns true while diving (the athlete is carried by the lunge, not by running).</summary>
        public bool StepDive(TestPlayerController ctrl, InputIntent intent, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var p = ctrl.Player;
            if (_diveRecover > 0) _diveRecover = System.Math.Max(0, _diveRecover - dt);
            if (intent.DivePressed && _diveT <= 0 && _diveRecover <= 0 && p.Stamina >= c.DiveMinStamina)
            {
                double mx = intent.Move.X, mz = intent.Move.Z, m = System.Math.Sqrt(mx * mx + mz * mz);
                if (m < 0.1) { mx = DMath.Sin(p.Facing); mz = -DMath.Cos(p.Facing); m = 1; }
                _diveX = mx / m; _diveZ = mz / m;
                _diveT = c.DiveSeconds;
                p.Stamina = System.Math.Max(0, p.Stamina - c.DiveStamina);
                DiveCount++;
            }
            if (_diveT <= 0) return false;
            double speed = PhysicsConfig.Current.Player.MaxSpeed * c.DiveSpeedScale;
            p.Vel.X = _diveX * speed; p.Vel.Z = _diveZ * speed;
            p.Pos.X += p.Vel.X * dt; p.Pos.Z += p.Vel.Z * dt;
            PlayerMovement.ClampToCourt(p);
            p.Speed = speed;
            _diveT -= dt;
            if (_diveT <= 0) { _diveRecover = c.DiveRecovery; p.Vel.X = 0; p.Vel.Z = 0; }
            return true;
        }

        /// <summary>The shot type the reticle height (and the charge) would play: lob / drop / kill / drive.</summary>
        public string ShotTypeFor(double aimHeight)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            if (c == null) return "drive";
            if (aimHeight > c.LobHeight) return "lob";
            if (aimHeight < c.LowHeight) return ChargeRatio > c.KillCharge ? "kill" : "drop";
            return "drive";
        }
        /// <summary>"hit" or "touch": the stroke being held.</summary>
        public string Stroke { get { return _stroke; } }


        public bool Waiting { get { return _pending > 0; } }

        double _releaseWait;

        public ArcadeStroke(uint seed) { _rng = seed * 2654435761u + 12345u; }
        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        /// <summary>Horizontal distance from the athlete to the ball, if the ball is hittable at this height.</summary>
        double ReachScale { get { return Diving ? PhysicsConfig.Current.ArcadeHit.DiveReachScale : 1.0; } }

        bool InReach(TestPlayer p, BallState ball, ArcadeHitSection c, out double dist)
        {
            double dx = ball.Pos.X - p.Pos.X, dz = ball.Pos.Z - p.Pos.Z;
            dist = System.Math.Sqrt(dx * dx + dz * dz);
            return dist <= c.Reach * ReachScale && ball.Pos.Y >= c.MinHeight && ball.Pos.Y <= c.MaxHeight;
        }

        /// <summary>One fixed step. Returns the contact event when the ball is hit.</summary>
        public SimEvent Step(TestPlayerController ctrl, InputIntent intent, BallState ball, List<Surface> surfaces, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var sw = ctrl.Swing;
            var player = ctrl.Player;

            // The stroke animation (follow-through, recovery) runs on the normal swing clock.
            if (sw.State == SwingStates.Swinging || sw.State == SwingStates.Recovering)
            {
                _idle.ClearEdges();
                Swing.Step(sw, _idle, ctrl.Aim, player, ctrl.Prediction, new SwingCtx { Quality = ctrl.Assist.Quality, ReachState = ctrl.Assist.ReachState }, dt, ctrl.IntentSolver);
            }

            string pressed = intent.Hit.Pressed ? "hit" : (intent.Touch.Pressed ? "touch" : null);
            if (pressed != null)
            {
                _charging = true; _charge = 0; _stroke = pressed; _pending = -1; _lunged = 0;
                if (sw.State == SwingStates.Idle) { sw.State = SwingStates.Preparing; sw.Stroke = pressed; sw.Mode = "prepared"; sw.HoldSeconds = 0; }
            }
            if (_charging)
            {
                bool held = _stroke == "touch" ? intent.Touch.Held : intent.Hit.Held;
                if (held) { _charge += dt; sw.HoldSeconds = _charge; }
                if (!held)
                {
                    _charging = false;
                    _pending = c.Grace;
                    _pendingCharge = System.Math.Min(1, _charge / c.ChargeFull);
                }
            }

            // Tap to arm / disarm a boast for the next stroke.
            if (intent.BoastPressed) _boast = !_boast;

            // Getting there early: time spent in position for the ball that is coming.
            var pred = ctrl.Prediction;
            if (ctrl.ContactEnabled && pred.Valid)
            {
                double ex0 = pred.Point.X - player.Pos.X, ez0 = pred.Point.Z - player.Pos.Z;
                if (System.Math.Sqrt(ex0 * ex0 + ez0 * ez0) <= c.SweetSpot + c.EarlyNear) _setTime += dt;
                else _setTime = System.Math.Max(0, _setTime - 2 * dt);
            }
            else _setTime = 0;

            // Auto step toward the ball while the stroke is ready and the ball is nearly there.
            bool ready = _pending > 0 || (_charging && pred.Valid && pred.Time < 0.35);
            if (ready && ctrl.ContactEnabled && pred.Valid && _lunged < c.LungeMax)
            {
                double dx = pred.Point.X - player.Pos.X, dz = pred.Point.Z - player.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dz * dz);
                double want = d - c.SweetSpot;
                if (want > 0 && d > 1e-6)
                {
                    double step = System.Math.Min(System.Math.Min(c.LungeSpeed * dt, want), c.LungeMax - _lunged);
                    player.Pos.X += dx / d * step;
                    player.Pos.Z += dz / d * step;
                    _lunged += step;
                    PlayerMovement.ClampToCourt(player);
                }
            }

            if (_pending > 0)
            {
                if (ctrl.ContactEnabled && InReach(player, ball, c, out double dist))
                {
                    _releaseWait = c.Grace - _pending;
                    _pending = -1;
                    return Hit(ctrl, ball, surfaces, dist, _pendingCharge, c);
                }
                _pending -= dt;
                if (_pending <= 0)
                {
                    _pending = -1;
                    _boast = false;
                    Whiff(ctrl, ball);
                }
            }
            return null;
        }

        /// <summary>The one-click serve: struck straight from the hand toward the aim target (above the service line).</summary>
        public SimEvent ServeNow(TestPlayerController ctrl, BallState ball, List<Surface> surfaces)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double charge = _serveCharging ? System.Math.Min(1, _charge / c.ChargeFull) : 0;
            _stroke = "hit"; _charging = false; _serveCharging = false; _pending = -1; _boast = false; _releaseWait = 0;
            var ev = Hit(ctrl, ball, surfaces, 0, charge, c);
            _charge = 0;
            return ev;
        }

        SimEvent Hit(TestPlayerController ctrl, BallState ball, List<Surface> surfaces, double dist, double charge, ArcadeHitSection c)
        {
            var player = ctrl.Player;
            var aim = ctrl.Aim;
            double missRatio = Clamp((dist - c.SweetSpot) / (c.Reach * ReachScale - c.SweetSpot), 0, 1);
            double early = ctrl.ServeStroke ? 1 : Clamp(_setTime / c.EarlyFull, 0, 1);
            LastEarliness = early;
            _setTime = 0;
            if (Diving) missRatio = System.Math.Min(1, missRatio + 0.35);            // a diving hit is a scramble
            else if (_diveRecover > 0) missRatio = System.Math.Min(1, missRatio + 0.15);

            // ---- where it goes ----
            double tx, ty, tz = 0;
            int wall = aim.HasWallTarget ? aim.TargetWall : 0;
            if (aim.HasWallTarget) { tx = aim.WallTarget.X; ty = aim.WallTarget.Y; tz = aim.WallTarget.Z; }
            else { tx = aim.IntendedPoint.X; ty = aim.IntendedPoint.Y; tz = 0; }
            // The serve goes where you aim (under the service line the referee calls a fault). Bots, with no
            // reticle, keep the old safe serve: above the line, cross-court when the stick is centred.
            if (ctrl.ServeStroke && wall == 0 && !aim.FromPoint)
            {
                ty = System.Math.Max(ty, CourtDimensions.SERVICE_LINE_H + 0.3);
                if (System.Math.Abs(aim.BiasX) < 0.08) tx = ctrl.ServeDefaultX;    // no drag: the normal cross-court serve
            }
            string shot;
            double speed;
            if (ctrl.ServeStroke && wall == 0 && aim.FromPoint)
            {
                // A local player's serve: tap = lob serve, hold = hard serve (struck from the hand: no spread).
                _boast = false;
                shot = ServeLaunch(ball, surfaces, tx, ty, charge, ctrl.ServeDefaultX > 0 ? -1 : 1, out speed);
            }
            else
            {
            bool touch = _stroke == "touch";
            // The shot type from the aim height (the original SquashStrike); charge adds pace to drives and kills.
            shot = ctrl.ServeStroke ? (ty > c.LobHeight ? "lob" : "serve") : ShotTypeFor(ty);    // bots serve the old way
            switch (shot)
            {
                case "lob": speed = c.LobSpeed; break;
                case "serve": speed = c.ServeSpeed; break;
                case "drop":
                    // A real pace for the distance (a short, firm flight low onto the front wall), not a float.
                    ty = CourtDimensions.TIN_H + c.DropHeightAboveTin;
                    double reach = System.Math.Sqrt((tx - ball.Pos.X) * (tx - ball.Pos.X) + ball.Pos.Z * ball.Pos.Z);
                    speed = Clamp(reach / c.DropFlightTime, c.DropSpeedMin, c.DropSpeedMax);
                    break;
                case "kill": ty = CourtDimensions.TIN_H + c.KillHeightAboveTin; speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
                default: speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
            }
            if (touch && shot != "drop") speed = c.TouchSpeed;
            bool boast = _boast && !ctrl.ServeStroke && wall == 0;
            _boast = false;
            int side = 0;
            if (boast)
            {
                // Off the wall by the ball, across to the aim point - kept on the other half (where the ring shows it).
                side = BoastTarget(ball.Pos.X, tx, out tx);
                if (shot != "drop") ty = Clamp(ty, c.BoastMinHeight, c.BoastMaxHeight);
                if (shot != "drop" && !touch) speed *= c.BoastPaceScale;
            }
            double spread = missRatio * missRatio * c.MaxSpread * (charge > 0.8 ? 0.6 : 1.0);
            spread *= 1 - c.EarlySpreadCut * early;                         // set early: a tighter shot

            double ex = (Rnd() - 0.5) * spread, ey = (Rnd() - 0.5) * spread * 0.9;
            if (wall == 0) tx += ex; else tz += ex;
            ty += ey;
            if (missRatio > 0 && Rnd() < missRatio * c.GrossMissChance * (1 - c.EarlyMishitCut * early))
            {
                if (Rnd() < 0.5) ty = CourtDimensions.TIN_H - 0.15;                                    // into the tin
                else ty += (Rnd() < 0.5 ? 1 : -1) * (0.4 + missRatio * 0.6);                             // well off
            }
            speed *= 1.0 - 0.15 * missRatio;
            // The AI's difficulty (1 for a human): how hard its drives and kills are. Drops, boasts, lobs and serves
            // set their own pace (a softer drop just dies in the tin).
            if (player.PaceScale > 0 && player.PaceScale != 1.0 && (shot == "drive" || shot == "kill")) speed *= player.PaceScale;

            // Assisted controls: a straight drive is kept off its side wall.
            if (Assisted && !boast && wall == 0 && (shot == "drive" || shot == "kill")) tx = StraightAssist(ball, surfaces, tx, ty, speed);

            // ---- the launch that gets there (the real ball physics, a few corrections) ----
            if (boast && !SolveBoast(ball, surfaces, tx, ty, side, ref speed)) boast = false;   // no way round from here: a drive
            if (boast) shot = "boast";
            else if (shot == "lob" && wall == 0 && !touch) speed = SolveLob(ball, surfaces, tx, c.LobHeight);
            else Solve(ball, surfaces, tx, ty, tz, wall, speed);
            // Floor before the back wall, and in: a drive, kill, serve or boast that would reach the back wall on the
            // full - or a drive / kill that would fly over an out line - is struck just soft enough. Lobs keep their own rule.
            _good.CopyFrom(_vel);
            for (int k = 0; k < 14 && (BackWallHeight(ball, surfaces) >= 0 || (!boast && GoesOut(ball, surfaces))); k++)
            {
                speed *= 0.93;
                if (boast)
                {
                    if (!SolveBoastAt(ball, surfaces, tx, ty, side, speed)) { _vel.CopyFrom(_good); break; }   // keep the last boast that worked
                    _good.CopyFrom(_vel);
                }
                else if (shot == "lob" && wall == 0 && !touch) break;   // the arc search already kept it in court
                else Solve(ball, surfaces, tx, ty, tz, wall, speed);
            }
            }

            double before = V3.Length(ball.Vel);
            ball.Vel.CopyFrom(_vel);
            ball.Omega.X = 0; ball.Omega.Y = 0; ball.Omega.Z = 0;
            BallPhysics.Wake(ball);
            ball.FloorSinceFrontWall = 0;
            double m = PhysicsConfig.Current.Ball.Mass;
            BallTemperature.ApplyImpactHeat(ball, 0.5 * m * (before * before + speed * speed) * 0.25, "racket");

            // ---- the swing plays through this contact (animation; the racket meets the ball here) ----
            var sw = ctrl.Swing;
            sw.Stroke = _stroke;
            sw.Mode = charge > 0.25 ? "prepared" : "quick";
            Swing.ArcadeCommit(sw, ctrl, ball.Pos);
            ctrl.Racket.HeadPos.CopyFrom(ball.Pos);
            ctrl.Racket.SweetSpot.CopyFrom(ball.Pos);

            double quality = 1 - missRatio;
            var ev = new SimEvent
            {
                Type = SimEventType.RacketContact,
                Point = new V3(ball.Pos.X, ball.Pos.Y, ball.Pos.Z),
                Stroke = _stroke,
                Mode = sw.Mode,
                Technique = shot,
                Side = player.SwingSide >= 0 ? 1 : -1,
                HeadSpeed = speed * 0.8,
                FaceNormal = new V3(_vel.X / speed, _vel.Y / speed, _vel.Z / speed),
                OffCentre = missRatio * PhysicsConfig.Current.Racket.HeadRadius,
                OffCentreNorm = missRatio,
                Restitution = PhysicsConfig.Current.Racket.Restitution,
                BallSpeedOut = V3.Length(ball.Vel),
                OutVel = new V3(ball.Vel.X, ball.Vel.Y, ball.Vel.Z),
                Temperature = ball.Temperature,
                Time = ball.SimTime,
                SwingPhaseAtContact = 0.5,
                ArcadeQuality = quality,
                Earliness = early,
                ReleaseWait = _releaseWait,
                ArcadeDistance = ctrl.ServeStroke ? -1 : dist
            };
            ctrl.Racket.LastContact = ev;
            ball.LastRacketContact = new RacketTouch { By = "player", Time = ball.SimTime, Speed = ev.BallSpeedOut, Stroke = _stroke, Technique = ev.Technique };
            return ev;
        }

        /// <summary>A released stroke that never met the ball: the swing still plays, at nothing.</summary>
        void Whiff(TestPlayerController ctrl, BallState ball)
        {
            var p = ctrl.Player;
            double side = p.SwingSide >= 0 ? 1 : -1;
            double fx = DMath.Sin(p.Facing), fz = -DMath.Cos(p.Facing);
            var at = new V3(p.Pos.X + fx * 0.5 + fz * 0 + side * 0.6 * DMath.Cos(p.Facing), 0.9, p.Pos.Z + fz * 0.5 + side * 0.6 * DMath.Sin(p.Facing));
            ctrl.Swing.Stroke = _stroke;
            ctrl.Swing.Mode = "quick";
            Swing.ArcadeCommit(ctrl.Swing, ctrl, at);
            ctrl.Swing.HasContacted = false;
        }

        /// <summary>Velocity of the given speed that carries the ball from where it is to the target point
        /// (front wall, or a side wall for a boast): straight-line start, then corrected on the real flight.</summary>
        /// <summary>Start the next Solve this far above the target (a lob's high arc); 0 = straight at it.</summary>
        double _lift;

        /// <summary>
        /// A lob: still RISING when it meets the front wall, so it carries on up and over to the back. Tries a few
        /// arcs (how fast it is still rising at the wall); each starts from the drag-free flight and is corrected on
        /// the real one by Solve; keeps the one landing nearest LobDepth that stays in court (no ceiling, nothing
        /// out, no back-wall hit on the full above the safe height). Returns the pace; the launch is left in the
        /// stroke velocity.
        /// </summary>
        /// <summary>Assisted controls (the game's setting for local players): straight drives kept off the side wall,
        /// serves nudged onto a line that lands deep and legal. Off = the shot goes exactly where you aim.</summary>
        public bool Assisted;

        /// <summary>The last serve launched (or previewed) lands legally in the receiver's back quarter.</summary>
        public bool LastServeLegal { get; private set; }
        /// <summary>Where the last serve met the front wall, x (Assisted may have moved it off the ring).</summary>
        public double LastServeLineX { get; private set; }

        /// <summary>
        /// A local player's serve, launched into the stroke velocity: under ServeHardCharge a lob serve (onto the ring, the
        /// arc that lands deepest and legal), above it a hard serve (straight at the ring, up to +ServeChargeBonus pace,
        /// softened until it bounces before the back wall and stays in). With Assisted on, the line (front-wall x) is
        /// nudged to the nearest one that lands deep and legal. Returns "lob serve" / "hard serve".
        /// </summary>
        public string ServeLaunch(BallState ball, List<Surface> surfaces, double tx, double ty, double charge, int serveSide, out double speed)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            bool hard = charge >= c.ServeHardCharge;
            double pace = hard ? c.ServeSpeed * (1 + c.ServeChargeBonus * (charge - c.ServeHardCharge) / (1 - c.ServeHardCharge)) : c.ServeSpeed;
            if (Assisted)
            {
                // The ring first; if that is not a deep, legal serve, every line across the wall (0.2 m apart), the
                // nearest good one winning.
                ServeAt(ball, surfaces, tx, ty, hard, pace, serveSide, out _);
                double bestScore = ServeScore(ball, surfaces, serveSide), bestX = tx;
                if (bestScore >= 1.5)
                {
                    double lim = CourtDimensions.HALF_W - 0.3;
                    for (double x = -lim; x <= lim + 1e-9; x += 0.2)
                    {
                        ServeAt(ball, surfaces, x, ty, hard, pace, serveSide, out _);
                        double score = ServeScore(ball, surfaces, serveSide) + 1.0 * System.Math.Abs(x - tx);
                        if (score < bestScore) { bestScore = score; bestX = x; }
                    }
                }
                tx = bestX;
            }
            ServeAt(ball, surfaces, tx, ty, hard, pace, serveSide, out speed);
            LastServeLegal = ServeScore(ball, surfaces, serveSide) < 100;
            LastServeLineX = tx;
            return hard ? "hard serve" : "lob serve";
        }

        void ServeAt(BallState ball, List<Surface> surfaces, double tx, double ty, bool hard, double pace, int serveSide, out double speed)
        {
            if (!hard) { speed = SolveLobServe(ball, surfaces, tx, ty, serveSide); return; }
            // A hard serve keeps its pace: if it would go out or reach the back wall on the full from the ring's height,
            // it is struck lower (down to just over the service line); only then, softer.
            speed = pace;
            double floorY = CourtDimensions.SERVICE_LINE_H + 0.25, y = ty;
            for (; ; y -= 0.15)
            {
                if (y < floorY) y = System.Math.Min(ty, floorY);
                Solve(ball, surfaces, tx, y, 0, 0, speed);
                if ((BackWallHeight(ball, surfaces) < 0 && !GoesOut(ball, surfaces)) || y <= floorY || y <= ty - 1.6) break;
            }
            for (int k = 0; k < 14 && (BackWallHeight(ball, surfaces) >= 0 || GoesOut(ball, surfaces)); k++)
            {
                speed *= 0.93;
                Solve(ball, surfaces, tx, y, 0, 0, speed);
            }
        }

        /// <summary>Preview a serve without touching any athlete or ball (the ring's FAULT warning). Leaves LastServeLegal.</summary>
        public bool PreviewServe(V3 from, double temperature, List<Surface> surfaces, double tx, double ty, double charge, int serveSide)
        {
            _preview.Reset(from, null, null, temperature);
            ServeLaunch(_preview, surfaces, tx, ty, charge, serveSide, out _);
            return LastServeLegal;
        }
        readonly BallState _preview = new BallState();

        /// <summary>
        /// Assisted: a straight drive (ball and aim on the same side) that would touch its side wall before the first bounce
        /// is aimed a little further in, step by step (up to 1.2 m), until it runs clean down the wall.
        /// </summary>
        public double StraightAssist(BallState ball, List<Surface> surfaces, double tx, double ty, double speed)
        {
            int side = ball.Pos.X > 0 ? 1 : -1;
            if (System.Math.Abs(ball.Pos.X) < 0.6 || tx * side < 0.6) return tx;          // not a straight shot
            double x = tx;
            for (int k = 0; k <= 12; k++)
            {
                x = tx - side * 0.1 * k;
                if (x * side < 0.5) break;
                Solve(ball, surfaces, x, ty, 0, 0, speed);
                if (!ClipsSide(ball, surfaces, side)) return x;
            }
            return x;
        }

        /// <summary>Does this launch touch side wall <paramref name="side"/> before its first bounce after the front wall?</summary>
        public bool ClipsSide(BallState ball, List<Surface> surfaces, int side)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            string id = side > 0 ? "sideWallRight" : "sideWallLeft";
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceId == id) return true;
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return false;
                    else if (front && e.SurfaceClass == "backWall") return false;
                }
            }
            return false;
        }

        /// <summary>
        /// The lob serve: exactly onto the ring (tx, ty) on the front wall, on the arc that carries it deepest into the
        /// receiver's back quarter while staying in. Returns the pace.
        /// </summary>
        public double SolveLobServe(BallState ball, List<Surface> surfaces, double tx, double ty, int serveSide)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double g = PhysicsConfig.Current.World.Gravity;
            double dx = tx - ball.Pos.X, dz = ball.Pos.Z, d = System.Math.Max(0.5, System.Math.Sqrt(dx * dx + dz * dz));
            double dy = ty - ball.Pos.Y;
            double best = double.MaxValue, bestSpeed = c.ServeSpeed;
            bool any = false;
            for (int k = 0; k < 9; k++)
            {
                double up = 0.5 + k * c.LobRise;
                double t = (-up + System.Math.Sqrt(System.Math.Max(0, up * up + 2 * g * dy))) / g;
                if (t < 0.12) t = 0.12;
                double h = d / t, vy = up + g * t;
                double speed = System.Math.Min(c.LobMaxSpeed, System.Math.Sqrt(h * h + vy * vy) * 1.08);
                _lift = vy / h * d - dy;
                Solve(ball, surfaces, tx, ty, 0, 0, speed);
                _lift = 0;
                double score = ServeScore(ball, surfaces, serveSide);
                if (score < best) { best = score; bestSpeed = speed; _good.CopyFrom(_vel); any = true; }
            }
            if (any) _vel.CopyFrom(_good);
            return bestSpeed;
        }

        /// <summary>
        /// How good this serve launch is (lower = better): a legal landing in the receiver's back quarter first (anything
        /// else costs 100+), then as deep as ServeDepth and toward the side wall. Out of court or the back wall first: 1000.
        /// </summary>
        double ServeScore(BallState ball, List<Surface> surfaces, int serveSide)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            bool front = false;
            for (int i = 0; i < 600; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                if (_scratch.Pos.Y > CourtDimensions.CEILING_H - 0.3) return 1000;
                foreach (var e in _events)
                {
                    if (RallyReferee.OutReason(e) != null) return 1000;
                    if (e.SurfaceClass == "frontWall")
                    {
                        if (e.Point.Y > CourtDimensions.FRONT_WALL_OUT_H - 0.25) return 1000;
                        front = true;
                    }
                    else if (!front) { if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return 1000; }
                    else if (e.SurfaceClass == "backWall") return 1000;
                    else if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.25) return 1000;
                    else if (e.SurfaceClass == "floor")
                    {
                        double want = -serveSide * 1.8;
                        // Legal with a margin (the real flight runs at a finer step than this check).
                        bool legal = e.Point.X * serveSide < -0.45 && e.Point.Z > CourtDimensions.SHORT_LINE_Z + 0.5;
                        return (legal ? 0 : 100) + System.Math.Abs(e.Point.Z - c.ServeDepth) + 0.4 * System.Math.Abs(e.Point.X - want);
                    }
                }
            }
            return 1000;
        }

        /// <summary>Would this launch go out of court (lines, ceiling) before its first bounce after the front wall?</summary>
        bool GoesOut(BallState ball, List<Surface> surfaces)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (RallyReferee.OutReason(e) != null) return true;
                    if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.2) return true;   // with a margin
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (front && e.SurfaceClass == "floor") return false;
                }
            }
            return false;
        }

        public double SolveLob(BallState ball, List<Surface> surfaces, double tx, double ty)
        {
            return SolveArc(ball, surfaces, tx, ty, PhysicsConfig.Current.ArcadeHit.LobDepth);
        }

        /// <summary>
        /// A high arc (lob, lob serve) through front-wall x = tx, landing near <paramref name="depth"/>: tries wall heights from
        /// <paramref name="minWallY"/> up to LobWallMax and, at each, the arcs from barely rising to steeply; keeps the one landing
        /// nearest the depth that stays in court with a margin (never an out lob - with no arc in, the lowest, softest one).
        /// </summary>
        public double SolveArc(BallState ball, List<Surface> surfaces, double tx, double minWallY, double depth)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double lo = System.Math.Min(minWallY, c.LobWallMax), bestScore = double.MaxValue, bestSpeed = 0;
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                double wallY = lo + (c.LobWallMax - lo) * i / 3.0;
                double speed = BestLobArc(ball, surfaces, tx, wallY, depth, out bool found, out double score);
                if (found && score < bestScore - 0.05) { bestScore = score; bestSpeed = speed; _best.CopyFrom(_vel); any = true; }
            }
            if (any) { _vel.CopyFrom(_best); return bestSpeed; }
            for (int drop = 1; drop <= 4; drop++)
            {
                double wallY = System.Math.Max(CourtDimensions.SERVICE_LINE_H + 0.4, lo - 0.3 * drop);
                double speed = BestLobArc(ball, surfaces, tx, wallY, depth, out bool found, out _);
                if (found) return speed;
            }
            return BestLobArc(ball, surfaces, tx, CourtDimensions.SERVICE_LINE_H + 0.4, depth, out _, out _);
        }

        /// <summary>
        /// The arcs for a lob (or lob serve) to (tx, wallY) on the front wall, from barely rising at the wall to steeply:
        /// keeps the one whose first bounce lands nearest <paramref name="depth"/> while staying in court. found = false
        /// when none stays in (the launch left is then the softest arc).
        /// </summary>
        double BestLobArc(BallState ball, List<Surface> surfaces, double tx, double wallY, double depth, out bool found, out double bestScore)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double g = PhysicsConfig.Current.World.Gravity;
            double dx = tx - ball.Pos.X, dz = ball.Pos.Z, d = System.Math.Max(0.5, System.Math.Sqrt(dx * dx + dz * dz));
            double dy = wallY - ball.Pos.Y;
            double best = double.MaxValue, bestSpeed = 0, softest = 0;
            found = false;
            for (int k = 0; k < 9; k++)
            {
                double up = 0.5 + k * c.LobRise;                     // rising this fast at the wall
                double t = (-up + System.Math.Sqrt(System.Math.Max(0, up * up + 2 * g * dy))) / g;
                if (t < 0.12) t = 0.12;
                double h = d / t, vy = up + g * t;
                double speed = System.Math.Min(c.LobMaxSpeed, System.Math.Sqrt(h * h + vy * vy) * 1.08);
                _lift = vy / h * d - dy;
                Solve(ball, surfaces, tx, wallY, 0, 0, speed);
                _lift = 0;
                if (k == 0) { softest = speed; _scratchVel.CopyFrom(_vel); }
                double land = LobLanding(ball, surfaces);
                if (land < 0) continue;
                double score = System.Math.Abs(land - depth);
                if (score < best) { best = score; bestSpeed = speed; _good.CopyFrom(_vel); found = true; }
            }
            bestScore = best;
            if (found) { _vel.CopyFrom(_good); return bestSpeed; }
            _vel.CopyFrom(_scratchVel);
            return softest;
        }

        /// <summary>Where this launch first bounces after the front wall (z), or -1 if it would go out / hit the ceiling / the back wall high on the full.</summary>
        /// <summary>The side-wall out line: 4.57 m at the front wall sloping to 2.13 m at the back.</summary>
        static double SideOutHeight(double z)
        {
            double f = Clamp(z / CourtDimensions.LENGTH, 0, 1);
            return CourtDimensions.FRONT_WALL_OUT_H + (CourtDimensions.BACK_WALL_OUT_H - CourtDimensions.FRONT_WALL_OUT_H) * f;
        }

        double LobLanding(BallState ball, List<Surface> surfaces)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                if (_scratch.Pos.Y > CourtDimensions.CEILING_H - 0.3) return -1;
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall")
                    {
                        if (e.Point.Y > CourtDimensions.FRONT_WALL_OUT_H - 0.25) return -1;
                        front = true;
                    }
                    else if (!front) { if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return -1; }
                    else if (e.SurfaceClass == "backWall") return -1;                      // floor first, like every other shot
                    else if (e.SurfaceClass == "outBackstop" || e.SurfaceClass == "ceiling") return -1;
                    else if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.25) return -1;   // a safe margin under the line
                    else if (e.SurfaceClass == "floor") return e.Point.Z;
                }
            }
            return -1;
        }

        public void Solve(BallState ball, List<Surface> surfaces, double tx, double ty, double tz, int wall, double speed)
        {
            _aimPoint.X = tx; _aimPoint.Y = ty + _lift; _aimPoint.Z = tz;
            for (int it = 0; it < 4; it++)
            {
                double dx = _aimPoint.X - ball.Pos.X, dy = _aimPoint.Y - ball.Pos.Y, dz = _aimPoint.Z - ball.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < 1e-6) d = 1e-6;
                _vel.X = dx / d * speed; _vel.Y = dy / d * speed; _vel.Z = dz / d * speed;

                if (!Fly(ball, surfaces, wall, out double hu, out double hv)) break;
                double wantU = wall == 0 ? tx : tz;
                double eu = wantU - hu, ev = ty - hv;
                if (wall == 0) _aimPoint.X += eu; else _aimPoint.Z += eu;
                _aimPoint.Y += ev;
                if (eu * eu + ev * ev < 0.03 * 0.03) break;
            }
            double dx2 = _aimPoint.X - ball.Pos.X, dy2 = _aimPoint.Y - ball.Pos.Y, dz2 = _aimPoint.Z - ball.Pos.Z;
            double d2 = System.Math.Max(1e-6, System.Math.Sqrt(dx2 * dx2 + dy2 * dy2 + dz2 * dz2));
            _vel.X = dx2 / d2 * speed; _vel.Y = dy2 / d2 * speed; _vel.Z = dz2 / d2 * speed;
        }

        /// <summary>
        /// The side wall a boast is played off (+1 right, -1 left): the one nearer the ball; from the middle,
        /// the one away from the target so the ball comes across.
        /// </summary>
        public static int BoastSide(double ballX, double targetX)
        {
            if (System.Math.Abs(ballX) >= 0.4) return ballX > 0 ? 1 : -1;
            return targetX > 0 ? -1 : 1;
        }

        /// <summary>
        /// The boast for a ball at <paramref name="ballX"/> aimed at <paramref name="aimX"/>: the side wall it goes off
        /// (returned) and where it lands on the front wall - the aim, kept on the other half of the wall (a boast
        /// comes across the court).
        /// </summary>
        public static int BoastTarget(double ballX, double aimX, out double landX)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double cross = c != null ? c.BoastCross : 0.3;
            int side = BoastSide(ballX, aimX);
            double far = CourtDimensions.HALF_W - 0.15;
            landX = side > 0 ? Clamp(aimX, -far, -cross) : Clamp(aimX, cross, far);
            return side;
        }

        /// <summary>
        /// A boast: off side wall <paramref name="side"/>, then onto the front wall at (tx, ty). Aims at the target's
        /// mirror image across that side wall, then corrects on the real flight. False when the ball would not
        /// reach the side wall first (then play it as a drive). The launch is left in the stroke's velocity.
        /// </summary>
        public bool SolveBoast(BallState ball, List<Surface> surfaces, double tx, double ty, int side, ref double speed)
        {
            // The side wall takes a lot of pace off: too soft to get round from here, play it a little firmer.
            for (int k = 0; k < 5; k++)
            {
                if (SolveBoastAt(ball, surfaces, tx, ty, side, speed)) return true;
                speed *= 1.2;
            }
            return false;
        }

        /// <summary>The boast at exactly this pace: true when it goes off the side wall and lands within 0.2 m of the aim.</summary>
        public bool SolveBoastAt(BallState ball, List<Surface> surfaces, double tx, double ty, int side, double speed)
        {
            // Work in the court unfolded across the side wall: there the boast is a straight shot at the mirror image.
            double wx = side * CourtDimensions.HALF_W, want = 2 * wx - tx;
            _aimPoint.X = want; _aimPoint.Y = ty; _aimPoint.Z = 0;
            for (int it = 0; it < 8; it++)
            {
                AimAt(ball, speed);
                int r = FlyBoast(ball, surfaces, side, out double hu, out double hv);
                if (r < 0) break;
                double eu = want - hu, ev = ty - hv;
                _aimPoint.X += eu;
                _aimPoint.Y += ev;
                if (r == 2 && eu * eu + ev * ev < 0.03 * 0.03) break;
            }
            AimAt(ball, speed);
            if (FlyBoast(ball, surfaces, side, out double fu, out double fv) != 2) return false;
            return (want - fu) * (want - fu) + (ty - fv) * (ty - fv) < 0.2 * 0.2;
        }

        /// <summary>The boast's launch (in the stroke velocity) for tests and tools.</summary>
        public V3 LaunchVelocity { get { return _vel; } }

        void AimAt(BallState ball, double speed)
        {
            double dx = _aimPoint.X - ball.Pos.X, dy = _aimPoint.Y - ball.Pos.Y, dz = _aimPoint.Z - ball.Pos.Z;
            double d = System.Math.Max(1e-6, System.Math.Sqrt(dx * dx + dy * dy + dz * dz));
            _vel.X = dx / d * speed; _vel.Y = dy / d * speed; _vel.Z = dz / d * speed;
        }

        /// <summary>
        /// Fly a boast. 2 = off side wall <paramref name="side"/> and then onto the front wall; 1 = it came up short
        /// (floor / tin) or reached the front wall without the side wall; -1 = something else. (u, v) is where it
        /// got to in the unfolded court (x mirrored once it has come off the side wall; v = 0 for the floor).
        /// </summary>
        int FlyBoast(BallState ball, List<Surface> surfaces, int side, out double u, out double v)
        {
            u = 0; v = 0;
            double wx = side * CourtDimensions.HALF_W;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            string sideId = side > 0 ? "sideWallRight" : "sideWallLeft";
            bool offSide = false;
            for (int i = 0; i < 240; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceId == sideId) { offSide = true; continue; }
                    double ux = offSide ? 2 * wx - e.Point.X : e.Point.X;
                    if (e.SurfaceClass == "frontWall") { u = ux; v = e.Point.Y; return offSide ? 2 : 1; }
                    if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin")
                    {
                        // Short: as if it had reached the front wall at floor level (aim higher), along the same line.
                        u = ux; v = e.SurfaceClass == "tin" ? e.Point.Y - 0.3 : -0.3 - 0.1 * e.Point.Z;
                        return 1;
                    }
                    if (e.SurfaceClass == "sideWall" || e.SurfaceClass == "backWall") return -1;
                }
            }
            return -1;
        }

        /// <summary>How high the solved launch meets the back wall on the full (before it bounces twice), or -1.</summary>
        double BackWallHeight(BallState ball, List<Surface> surfaces)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            int floors = 0;
            bool front = false;
            for (int i = 0; i < 400; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (e.SurfaceClass == "floor" && front) { if (++floors >= 1) return -1; }
                    else if (front && (e.SurfaceClass == "backWall" || e.SurfaceClass == "outBackstop" || e.SurfaceClass == "ceiling")) return e.SurfaceClass == "backWall" ? e.Point.Y : 9.0;
                }
            }
            return -1;
        }

        /// <summary>Where this launch first reaches the target wall: front wall (x, height) or side wall (z, height).</summary>
        bool Fly(BallState ball, List<Surface> surfaces, int wall, out double u, out double v)
        {
            u = 0; v = 0;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature);
            string sideId = wall > 0 ? "sideWallRight" : "sideWallLeft";
            for (int i = 0; i < 200; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (wall == 0 && e.SurfaceClass == "frontWall") { u = e.Point.X; v = e.Point.Y; return true; }
                    if (wall != 0 && e.SurfaceId == sideId) { u = e.Point.Z; v = e.Point.Y; return true; }
                    if (wall != 0 && e.SurfaceClass == "frontWall") { u = -(CourtDimensions.HALF_W - wall * e.Point.X); v = e.Point.Y; return true; }
                    if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") { u = wall == 0 ? e.Point.X : e.Point.Z; v = 0; return true; }
                }
            }
            return false;
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
