// ===========================================================
// Assets/SquashSim/Player/ArcadeStroke.cs
// The simple hitting system (game profile; local human players only),
// after the original SquashStrike (player.js attemptHit):
//
//   hold Hit / Touch   charge (power builds up to ChargeFull seconds)
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
//   a FULL charge      (squash is fast: only when you have the time) turns
//                      the stroke into a special: Hit = a very fast KILL kept
//                      low over the tin, Touch = a pin-point DROP just above
//                      it.
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
        // A full charge: Hit = a kill (much faster, kept low, very accurate); Touch = a drop (soft, just above the tin).
        public double KillPace, KillMaxHeight, KillMargin, KillSpread, DropSpeed, DropHeight, DropSpread;
    }

    public sealed class ArcadeStroke
    {
        double _charge;
        bool _charging;
        string _stroke = "hit";
        double _pending = -1;          // grace time left for a released stroke waiting for the ball
        double _pendingCharge, _lunged;
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
        /// <summary>"hit" or "touch": the stroke being held.</summary>
        public string Stroke { get { return _stroke; } }

        /// <summary>Where a fully charged stroke would really go (kill: kept low; drop: just above the tin).</summary>
        public void SpecialTarget(V3 target)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            if (c == null || ChargeRatio < 0.999 || !(_charging || Waiting)) return;
            if (_stroke == "touch") target.Y = CourtDimensions.TIN_H + c.DropHeight;
            else target.Y = Clamp(target.Y, CourtDimensions.TIN_H + c.KillMargin, c.KillMaxHeight);
        }
        public bool Waiting { get { return _pending > 0; } }

        public ArcadeStroke(uint seed) { _rng = seed * 2654435761u + 12345u; }
        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        /// <summary>Horizontal distance from the athlete to the ball, if the ball is hittable at this height.</summary>
        static bool InReach(TestPlayer p, BallState ball, ArcadeHitSection c, out double dist)
        {
            double dx = ball.Pos.X - p.Pos.X, dz = ball.Pos.Z - p.Pos.Z;
            dist = System.Math.Sqrt(dx * dx + dz * dz);
            return dist <= c.Reach && ball.Pos.Y >= c.MinHeight && ball.Pos.Y <= c.MaxHeight;
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
            _stroke = "hit"; _charging = false; _pending = -1;
            return Hit(ctrl, ball, surfaces, 0, 0, PhysicsConfig.Current.ArcadeHit);
        }

        SimEvent Hit(TestPlayerController ctrl, BallState ball, List<Surface> surfaces, double dist, double charge, ArcadeHitSection c)
        {
            var player = ctrl.Player;
            var aim = ctrl.Aim;
            double missRatio = Clamp((dist - c.SweetSpot) / (c.Reach - c.SweetSpot), 0, 1);

            // ---- where it goes ----
            double tx, ty, tz = 0;
            int wall = aim.HasWallTarget ? aim.TargetWall : 0;
            if (aim.HasWallTarget) { tx = aim.WallTarget.X; ty = aim.WallTarget.Y; tz = aim.WallTarget.Z; }
            else { tx = aim.IntendedPoint.X; ty = aim.IntendedPoint.Y; tz = 0; }
            // The serve must go above the service line (the original game did the same).
            if (ctrl.ServeStroke && wall == 0) ty = System.Math.Max(ty, CourtDimensions.SERVICE_LINE_H + 0.3);
            bool touch = _stroke == "touch";
            string special = charge >= 0.999 && !ctrl.ServeStroke ? (touch ? "drop" : "kill") : null;
            double speed, spread;
            if (special == "kill")
            {
                ty = Clamp(ty, CourtDimensions.TIN_H + c.KillMargin, c.KillMaxHeight);
                speed = c.DriveSpeed * c.KillPace;
                spread = c.KillSpread;
            }
            else if (special == "drop")
            {
                ty = CourtDimensions.TIN_H + c.DropHeight;
                speed = c.DropSpeed;
                spread = c.DropSpread;
            }
            else
            {
                speed = touch ? c.TouchSpeed : (ty >= c.LobHeight ? c.LobSpeed : c.DriveSpeed);
                spread = missRatio * missRatio * c.MaxSpread;
            }

            double ex = (Rnd() - 0.5) * spread, ey = (Rnd() - 0.5) * spread * 0.9;
            if (wall == 0) tx += ex; else tz += ex;
            ty += ey;
            if (special == null && missRatio > 0 && Rnd() < missRatio * c.GrossMissChance)
            {
                if (Rnd() < 0.5) ty = CourtDimensions.TIN_H - 0.15;                                    // into the tin
                else ty += (Rnd() < 0.5 ? 1 : -1) * (0.4 + missRatio * 0.6);                             // well off
            }
            if (special == null) speed *= 1.0 - 0.15 * missRatio;

            // ---- the launch that gets there (the real ball physics, a few corrections) ----
            Solve(ball, surfaces, tx, ty, tz, wall, speed);
            // High shots: soft enough to come down before the back wall's out line (playable, not long).
            for (int k = 0; k < 8 && BackWallHeight(ball, surfaces) > c.BackWallSafeHeight; k++)
            {
                speed *= 0.9;
                Solve(ball, surfaces, tx, ty, tz, wall, speed);
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
                Technique = special != null ? "charged " + special : (sw.Plan != null ? sw.Plan.Technique : null),
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
