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
//   boast              hold E / pad RB while you hit: off the side wall
//                      nearer the ball, landing on the aim point (aimed
//                      like a drive; the side-wall bounce is solved).
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
        public double BoastPaceScale, BoastMaxHeight;
    }

    public sealed class ArcadeStroke
    {
        double _charge;
        bool _charging;
        string _stroke = "hit";
        double _pending = -1;          // grace time left for a released stroke waiting for the ball
        double _pendingCharge, _lunged;
        bool _boast;
        /// <summary>This stroke will be played as a boast (Boast held since the press).</summary>
        public bool BoastArmed { get { return _boast; } }
        uint _rng;
        readonly InputIntent _idle = new InputIntent();
        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _vel = new V3();
        readonly V3 _aimPoint = new V3();
        static readonly Prediction _fakePred = new Prediction();

        /// <summary>0..1 how charged the stroke being held is (presentation).</summary>
        public double ChargeRatio { get { var c = PhysicsConfig.Current.ArcadeHit; return c == null ? 0 : System.Math.Min(1, _charge / c.ChargeFull); } }
        public bool Charging { get { return _charging; } }

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
            if (_diveRecover > 0) s *= c.DiveRecoverySpeed;
            else if (intent.Sprint && moving && p.Stamina > c.WindedStamina)
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
                _charging = true; _charge = 0; _stroke = pressed; _pending = -1; _lunged = 0; _boast = false;
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

            // Boast held at any time between the press and the contact: the stroke is a boast.
            if (_charging || _pending > 0) _boast |= intent.Boast;
            else if (sw.State == SwingStates.Idle) _boast = false;

            // Auto step toward the ball while the stroke is ready and the ball is nearly there.
            var pred = ctrl.Prediction;
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
                    _pending = -1;
                    return Hit(ctrl, ball, surfaces, dist, _pendingCharge, c);
                }
                _pending -= dt;
                if (_pending <= 0)
                {
                    _pending = -1;
                    Whiff(ctrl, ball);
                }
            }
            return null;
        }

        /// <summary>The one-click serve: struck straight from the hand toward the aim target (above the service line).</summary>
        public SimEvent ServeNow(TestPlayerController ctrl, BallState ball, List<Surface> surfaces)
        {
            _stroke = "hit"; _charging = false; _pending = -1; _boast = false;
            return Hit(ctrl, ball, surfaces, 0, 0, PhysicsConfig.Current.ArcadeHit);
        }

        SimEvent Hit(TestPlayerController ctrl, BallState ball, List<Surface> surfaces, double dist, double charge, ArcadeHitSection c)
        {
            var player = ctrl.Player;
            var aim = ctrl.Aim;
            double missRatio = Clamp((dist - c.SweetSpot) / (c.Reach * ReachScale - c.SweetSpot), 0, 1);
            if (Diving) missRatio = System.Math.Min(1, missRatio + 0.35);            // a diving hit is a scramble
            else if (_diveRecover > 0) missRatio = System.Math.Min(1, missRatio + 0.15);

            // ---- where it goes ----
            double tx, ty, tz = 0;
            int wall = aim.HasWallTarget ? aim.TargetWall : 0;
            if (aim.HasWallTarget) { tx = aim.WallTarget.X; ty = aim.WallTarget.Y; tz = aim.WallTarget.Z; }
            else { tx = aim.IntendedPoint.X; ty = aim.IntendedPoint.Y; tz = 0; }
            // The serve must go above the service line (the original game did the same).
            if (ctrl.ServeStroke && wall == 0)
            {
                ty = System.Math.Max(ty, CourtDimensions.SERVICE_LINE_H + 0.3);
                if (System.Math.Abs(aim.BiasX) < 0.08) tx = ctrl.ServeDefaultX;    // no drag: the normal cross-court serve
            }
            bool touch = _stroke == "touch";
            // The shot type from the aim height (the original SquashStrike); charge adds pace to drives and kills.
            string shot = ctrl.ServeStroke ? "serve" : ShotTypeFor(ty);
            double speed;
            switch (shot)
            {
                case "lob": speed = c.LobSpeed; break;
                case "drop": ty = CourtDimensions.TIN_H + c.DropHeightAboveTin; speed = c.DropSpeed; break;
                case "kill": ty = CourtDimensions.TIN_H + c.KillHeightAboveTin; speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
                default: speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
            }
            if (touch && shot != "drop") speed = c.TouchSpeed;
            bool boast = _boast && !ctrl.ServeStroke && wall == 0;
            if (boast)
            {
                ty = System.Math.Min(ty, c.BoastMaxHeight);
                if (shot != "drop" && !touch) speed *= c.BoastPaceScale;
            }
            double spread = missRatio * missRatio * c.MaxSpread * (charge > 0.8 ? 0.6 : 1.0);

            double ex = (Rnd() - 0.5) * spread, ey = (Rnd() - 0.5) * spread * 0.9;
            if (wall == 0) tx += ex; else tz += ex;
            ty += ey;
            if (missRatio > 0 && Rnd() < missRatio * c.GrossMissChance)
            {
                if (Rnd() < 0.5) ty = CourtDimensions.TIN_H - 0.15;                                    // into the tin
                else ty += (Rnd() < 0.5 ? 1 : -1) * (0.4 + missRatio * 0.6);                             // well off
            }
            speed *= 1.0 - 0.15 * missRatio;

            // ---- the launch that gets there (the real ball physics, a few corrections) ----
            int side = boast ? BoastSide(ball.Pos.X, tx) : 0;
            if (boast && !SolveBoast(ball, surfaces, tx, ty, side, ref speed)) boast = false;   // no way round from here: a drive
            if (boast) shot = "boast";
            else Solve(ball, surfaces, tx, ty, tz, wall, speed);
            // High shots: soft enough to come down before the back wall's out line (playable, not long).
            for (int k = 0; k < 8 && BackWallHeight(ball, surfaces) > c.BackWallSafeHeight; k++)
            {
                speed *= 0.9;
                if (boast) SolveBoastAt(ball, surfaces, tx, ty, side, speed);
                else Solve(ball, surfaces, tx, ty, tz, wall, speed);
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
                ArcadeQuality = quality
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
        void Solve(BallState ball, List<Surface> surfaces, double tx, double ty, double tz, int wall, double speed)
        {
            _aimPoint.X = tx; _aimPoint.Y = ty; _aimPoint.Z = tz;
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
