// ===========================================================
// Assets/SquashSim/Player/ShotPreview.cs
// Where would this aim send the ball? The launch the swing would give
// (the same string-bed maths as RacketPhysics.TryContact, used by the AI
// to choose its shots), then the ball flown to the front wall through
// the real ball physics.
//
// Read-only: it never writes the athlete, the ball or the match. The
// game draws the always-on aim target from it (M6); OpponentBrain uses
// PredictLaunch for its rollouts.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class ShotPreview
    {
        /// <summary>A front-wall point is known.</summary>
        public bool Valid;
        /// <summary>From the predicted contact and the real flight (true), or a straight-line guess
        /// from where the athlete stands because no contact is predicted yet (false).</summary>
        public bool FromContact;
        /// <summary>Where the ball meets the front wall (sim space).</summary>
        public readonly V3 FrontPoint = new V3();
        /// <summary>The ball would hit the tin / the floor before the front wall.</summary>
        public bool Down;
        /// <summary>It reaches a side wall first (a boast).</summary>
        public bool ViaSideWall;
        /// <summary>Expected spread of the real hit around FrontPoint, metres (radius).</summary>
        public double Spread;
        /// <summary>Front-wall target aim: the point picked (front or side wall) and which wall (0 front, -1/+1 side).</summary>
        public readonly V3 Target = new V3();
        public int TargetWall;

        readonly AimState _aim = new AimState();
        readonly BallState _ball = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _launch = new V3();
        readonly V3 _origin = new V3();

        const double FlightStep = 1.0 / 120;
        const double MaxFlight = 1.5;
        /// <summary>An auto-timed Quick tap meets the ball at ~75% of the planned head speed (MatchBench --calibrate).</summary>
        public const double QuickHeadFactor = 0.75;

        /// <summary>Preview the stroke this intent would play for <paramref name="ctrl"/>.</summary>
        public void Update(TestPlayerController ctrl, InputIntent intent, BallState ball, List<Surface> surfaces)
        {
            Valid = false; Down = false; ViaSideWall = false;
            var pred = ctrl.Prediction;
            var sw = ctrl.Swing;
            bool prepared = sw.State == SwingStates.Preparing || sw.Mode == "prepared";
            bool touch = intent.Touch.Held || sw.Stroke == "touch" && sw.State != SwingStates.Idle;

            bool wallAim = ctrl.WallAimEnabled && PhysicsConfig.Current.WallAim != null;
            if (pred.Valid && ball != null && !ball.Asleep)
            {
                _origin.CopyFrom(pred.Point);
                // Front-wall aim: exactly the aim the athlete will swing with (solved on its last step).
                if (wallAim) { _aim.CopyFrom(ctrl.Aim); Target.CopyFrom(_aim.WallTarget); TargetWall = _aim.TargetWall; }
                else { AimController.Step(_aim, intent, _origin); TargetWall = 0; }
                if (ctrl.ArcadeHitEnabled && PhysicsConfig.Current.ArcadeHit != null && wallAim && TargetWall == 0)
                {
                    // The simple hitting system sends the ball to the target itself: show that (a charged special where it goes).
                    FrontPoint.CopyFrom(Target);
                    ctrl.Arcade.SpecialTarget(FrontPoint);
                    FromContact = true;
                    Spread = 0.2;
                    Valid = true;
                    return;
                }
                var plan = SwingPlanner.Plan(touch ? "touch" : "hit", _aim, ctrl.Player.SwingSide >= 0 ? 1 : -1, new SwingContext
                {
                    Mode = prepared ? "prepared" : "quick", Quality = ctrl.Assist.Quality,
                    ReachState = ctrl.Assist.ReachState, IsVolley = pred.IsVolley
                });
                if (PredictLaunch(ctrl, pred.Vel, plan, _aim, prepared, QuickHeadFactor, _launch) && Fly(surfaces, ball.Temperature))
                {
                    FromContact = true;
                    Spread = SpreadFor(ctrl, prepared, pred.IsVolley);
                    Valid = true;
                    return;
                }
            }

            // No contact predicted yet (the ball is going to the opponent) or no clean launch:
            // the aim's straight line from where the athlete stands, at racket height.
            var p = ctrl.Player.Pos;
            _origin.X = p.X; _origin.Y = 1.0; _origin.Z = p.Z;
            AimController.Step(_aim, intent, _origin);
            if (wallAim) { TargetWall = WallAim.TargetFor(_aim.BiasX, _aim.BiasY, _origin, Target); FrontPoint.CopyFrom(Target); }   // the point you picked
            else AimController.WallIntersection(FrontPoint, _origin, _aim.DesiredYaw, _aim.DesiredPitch);
            FromContact = false;
            Spread = 0.35;
            Valid = true;
        }

        /// <summary>Fly the launched ball to its first front-wall impact.</summary>
        bool Fly(List<Surface> surfaces, double temperature)
        {
            _ball.Reset(_origin, _launch, null, temperature);
            int steps = (int)(MaxFlight / FlightStep);
            for (int i = 0; i < steps; i++)
            {
                _events.Clear();
                BallPhysics.Step(_ball, surfaces, FlightStep, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall") { FrontPoint.CopyFrom(e.Point); return true; }
                    if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") { FrontPoint.CopyFrom(e.Point); FrontPoint.Z = 0; Down = true; return true; }
                    if (e.SurfaceClass == "sideWall") ViaSideWall = true;
                }
            }
            return false;
        }

        /// <summary>How tight the real hit will be: a set, prepared stroke is precise; quick, stretched or volleyed ones are not.</summary>
        static double SpreadFor(TestPlayerController ctrl, bool prepared, bool volley)
        {
            double q = ctrl.Assist.Quality;
            if (q < 0) q = 0; else if (q > 1) q = 1;
            double s = 0.18 + 0.35 * (1 - q);
            if (!prepared) s += 0.12;
            if (volley) s += 0.10;
            return s;
        }

        /// <summary>
        /// The ball velocity this plan would produce at an on-centre contact: the planned head
        /// velocity along the swing path the body can commit to, the face the commitment solver
        /// can reach, and the same string-bed impulse as RacketPhysics.TryContact. Returns false
        /// when the strings would not meet the ball.
        /// </summary>
        public static bool PredictLaunch(TestPlayerController ctrl, V3 ballVel, SwingPlan plan, AimState aim,
                                         bool prepared, double quickHeadFactor, V3 output)
        {
            var c = PhysicsConfig.Current.Commitment;
            var rc = PhysicsConfig.Current.Racket;
            double q = Clamp(ctrl.Assist.Quality, 0, 1);
            double range = c.RangeQualityFloor + (1 - c.RangeQualityFloor) * q;
            double pathYaw = Clamp(aim.DesiredYaw, -c.MaxBodyYaw * range, c.MaxBodyYaw * range);
            double faceYaw = Clamp(aim.DesiredYaw, pathYaw - c.MaxFaceOffset * range, pathYaw + c.MaxFaceOffset * range);
            double facePitch = Clamp(aim.DesiredPitch + plan.FacePitch, rc.FacePitchMin, rc.FacePitchMax);

            double cp = DMath.Cos(facePitch);
            double nx = DMath.Sin(faceYaw) * cp, ny = DMath.Sin(facePitch), nz = -DMath.Cos(faceYaw) * cp;

            // Head velocity at contact: speed along the path, plus the path's rise. A Prepared stroke
            // meets the ball at the peak of its swing; an auto-timed Quick tap usually does not.
            double head = plan.HeadSpeed * (prepared ? 1.0 : quickHeadFactor);
            double hvx = DMath.Sin(pathYaw) * head;
            double hvy = plan.Duration > 0 ? plan.PathRise / plan.Duration : 0;
            double hvz = -DMath.Cos(pathYaw) * head;

            double ux = ballVel.X - hvx, uy = ballVel.Y - hvy, uz = ballVel.Z - hvz;
            double un = ux * nx + uy * ny + uz * nz;
            if (un >= 0) return false;

            var cb = PhysicsConfig.Current.Ball;
            double e = rc.Restitution, m = cb.Mass;
            double ox = -e * un * nx, oy = -e * un * ny, oz = -e * un * nz;
            double tx = ux - un * nx, ty = uy - un * ny, tz = uz - un * nz;
            double ut = System.Math.Sqrt(tx * tx + ty * ty + tz * tz);
            if (ut > 1e-6)
            {
                double jn = (1 + e) * m * System.Math.Abs(un);
                double jStick = ut * m / (1 + 1 / cb.InertiaFactor);
                double jt = System.Math.Min(jStick, rc.Friction * jn);
                double after = ut - jt / m;
                ox += after * tx / ut; oy += after * ty / ut; oz += after * tz / ut;
            }
            output.X = hvx + ox; output.Y = hvy + oy; output.Z = hvz + oz;
            return true;
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
