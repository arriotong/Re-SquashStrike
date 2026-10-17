// ===========================================================
// Assets/SquashSim/Player/WallAim.cs
// Front-wall target aim (game profile; local human players only).
//
// The lab's aim is an ANGLE from the contact point, so the point it
// meets on the wall slides as the athlete moves, and a straight line at
// "rally height" lands low once gravity and a soft tap have their say.
// Here the stick / mouse picks a POINT on the front wall instead - it
// stays put however the athlete moves - and the yaw and pitch are solved
// so the ball really goes there: the swing's own launch maths
// (ShotPreview.PredictLaunch) and the real ball flight, corrected a few
// times a second. The lowest point you can aim at sits safely above the
// tin: gameplay over strict realism.
//
// Boasts: push the aim past the edge of the front wall (the outer
// BoastZone of the stick's travel) and the target moves onto THAT side
// wall, between you and the front wall - the further you push, the nearer
// the front it sits. The shot is solved to strike the side wall there
// first, then come off onto the front wall.
//
// The AI keeps the lab aim (its shot model was built on it), and the lab
// itself never turns this on, so SimParity is untouched.
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed class WallAimSection
    {
        public double HalfWidth;       // the front wall's aimable half-width (m)
        public double NeutralHeight;   // stick centred: this high on the front wall (m)
        public double MinHeight;       // stick fully down (kept above the tin)
        public double MaxHeight;       // stick fully up (a lob)
        public int RefreshSteps;       // re-solve the launch every N steps
        public int Iterations;         // solve iterations per refresh
        public double BoastZone;       // outer part of the left/right travel that aims at the side wall (0 = no boasts)
        public double BoastHeightLow, BoastHeightHigh;   // side-wall target height range (stick down .. up)
    }

    public sealed class WallAim
    {
        /// <summary>0 = the front wall; -1 / +1 = the left / right side wall (a boast).</summary>
        public static int TargetWall(double biasX)
        {
            var c = PhysicsConfig.Current.WallAim;
            if (c == null || c.BoastZone <= 0) return 0;
            return System.Math.Abs(biasX) > 1 - c.BoastZone ? (biasX > 0 ? 1 : -1) : 0;
        }

        /// <summary>
        /// The point the stick picks. Front wall: (x, height, 0). Boast: on the side wall at
        /// x = +-HALF_W, between <paramref name="from"/> (where the ball will be struck) and the front.
        /// </summary>
        public static int TargetFor(double biasX, double biasY, V3 from, V3 output)
        {
            var c = PhysicsConfig.Current.WallAim;
            int wall = TargetWall(biasX);
            if (wall == 0)
            {
                double edge = 1 - System.Math.Max(0, c.BoastZone);
                output.X = Clamp(biasX / edge, -1, 1) * c.HalfWidth;
                output.Y = biasY >= 0
                    ? c.NeutralHeight + (c.MaxHeight - c.NeutralHeight) * biasY
                    : c.NeutralHeight + (c.NeutralHeight - c.MinHeight) * biasY;
                output.Z = 0;
                return 0;
            }
            // Deeper into the boast zone = nearer the front wall (a sharper boast).
            double t = Clamp((System.Math.Abs(biasX) - (1 - c.BoastZone)) / c.BoastZone, 0, 1);
            double fromZ = from != null ? from.Z : LENGTH * 0.7;
            output.X = wall * HALF_W;
            output.Z = System.Math.Max(0.9, fromZ * (0.62 - 0.32 * t));
            output.Y = c.BoastHeightLow + (c.BoastHeightHigh - c.BoastHeightLow) * Clamp((biasY + 1) * 0.5, 0, 1);
            return wall;
        }

        readonly BallState _ball = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _launch = new V3();
        double _yawFix, _pitchFix;
        int _age, _lastWall;

        /// <summary>Did the last solve converge on the target (else the straight-line aim is used)?</summary>
        public bool Solved { get; private set; }

        /// <summary>Turn the aim's bias into a wall target and point yaw / pitch at it.</summary>
        public void Apply(TestPlayerController ctrl, AimState aim, V3 origin, BallState ball, List<Surface> surfaces)
        {
            var cfg = PhysicsConfig.Current.WallAim;
            if (cfg == null) return;
            int wall = TargetFor(aim.BiasX, aim.BiasY, origin, aim.WallTarget);
            aim.HasWallTarget = true;
            aim.TargetWall = wall;
            if (wall != _lastWall) { _yawFix = 0; _pitchFix = 0; _age = 0; _lastWall = wall; }

            // Straight line from the contact to the target...
            double tx = aim.WallTarget.X, ty = aim.WallTarget.Y, tz = aim.WallTarget.Z;
            double dx = tx - origin.X, dz = DMath.Max(0.4, origin.Z - tz);
            double horiz = System.Math.Sqrt(dx * dx + dz * dz);
            double baseYaw = DMath.Atan2(dx, dz);
            double basePitch = DMath.Atan2(ty - origin.Y, horiz);

            var pred = ctrl.Prediction;
            if (!pred.Valid || ball == null || ball.Asleep)
            {
                aim.DesiredYaw = baseYaw; aim.DesiredPitch = basePitch;
                _age = 0; Solved = false;
                return;
            }

            // ...corrected so the launched ball really meets it (re-solved every few steps).
            if (_age++ % System.Math.Max(1, cfg.RefreshSteps) == 0)
            {
                var sw = ctrl.Swing;
                bool prepared = sw.State == SwingStates.Preparing || sw.Mode == "prepared";
                string stroke = sw.State != SwingStates.Idle ? sw.Stroke : "hit";
                var ctx = new SwingContext
                {
                    Mode = prepared ? "prepared" : "quick", Quality = ctrl.Assist.Quality,
                    ReachState = ctrl.Assist.ReachState, IsVolley = pred.IsVolley
                };
                Solved = wall == 0
                    ? SolveFront(ctrl, aim, pred, ball, surfaces, stroke, prepared, ctx, baseYaw, basePitch, tx, ty, horiz, cfg.Iterations)
                    : SolveSide(ctrl, aim, pred, ball, surfaces, stroke, prepared, ctx, baseYaw, basePitch, wall, tz, ty, cfg.Iterations);
            }
            aim.DesiredYaw = baseYaw + _yawFix;
            aim.DesiredPitch = basePitch + _pitchFix;
        }

        bool Launch(TestPlayerController ctrl, AimState aim, Prediction pred, string stroke, bool prepared, SwingContext ctx, double yaw, double pitch)
        {
            aim.DesiredYaw = yaw; aim.DesiredPitch = pitch;
            var plan = SwingPlanner.Plan(stroke, aim, ctrl.Player.SwingSide >= 0 ? 1 : -1, ctx);
            return ShotPreview.PredictLaunch(ctrl, pred.Vel, plan, aim, prepared, ShotPreview.QuickHeadFactor, _launch);
        }

        /// <summary>Front wall: a damped fixed-point correction on the miss (x, height).</summary>
        bool SolveFront(TestPlayerController ctrl, AimState aim, Prediction pred, BallState ball, List<Surface> surfaces,
                        string stroke, bool prepared, SwingContext ctx, double baseYaw, double basePitch,
                        double tx, double ty, double horiz, int iterations)
        {
            bool solved = false;
            for (int it = 0; it < iterations; it++)
            {
                if (!Launch(ctrl, aim, pred, stroke, prepared, ctx, baseYaw + _yawFix, basePitch + _pitchFix)) break;
                if (!FlyFront(pred.Point, ball.Temperature, surfaces, out double hx, out double hy)) break;
                double ex = tx - hx, ey = ty - hy;
                _yawFix = Clamp(_yawFix + 0.9 * ex / horiz, -0.6, 0.6);
                _pitchFix = Clamp(_pitchFix + 0.9 * ey / horiz, -0.6, 0.8);
                solved = true;
                if (ex * ex + ey * ey < 0.05 * 0.05) break;
            }
            return solved;
        }

        /// <summary>Side wall (a boast): Newton steps on (along-wall z, height) with a finite-difference Jacobian.</summary>
        bool SolveSide(TestPlayerController ctrl, AimState aim, Prediction pred, BallState ball, List<Surface> surfaces,
                       string stroke, bool prepared, SwingContext ctx, double baseYaw, double basePitch,
                       int wall, double tz, double ty, int iterations)
        {
            const double h = 0.015;
            bool solved = false;
            for (int it = 0; it < iterations; it++)
            {
                double y0 = baseYaw + _yawFix, p0 = basePitch + _pitchFix;
                if (!SideHit(ctrl, aim, pred, ball, surfaces, stroke, prepared, ctx, y0, p0, wall, out double u0, out double v0)) break;
                double eu = tz - u0, ev = ty - v0;
                solved = true;
                if (eu * eu + ev * ev < 0.06 * 0.06) break;
                if (!SideHit(ctrl, aim, pred, ball, surfaces, stroke, prepared, ctx, y0 + h, p0, wall, out double uy, out double vy)) break;
                if (!SideHit(ctrl, aim, pred, ball, surfaces, stroke, prepared, ctx, y0, p0 + h, wall, out double up, out double vp)) break;
                double a = (uy - u0) / h, b = (up - u0) / h, c = (vy - v0) / h, d = (vp - v0) / h;
                double det = a * d - b * c;
                double dYaw, dPitch;
                if (System.Math.Abs(det) > 1e-6)
                {
                    dYaw = (d * eu - b * ev) / det;
                    dPitch = (-c * eu + a * ev) / det;
                }
                else
                {
                    dYaw = System.Math.Abs(a) > 1e-6 ? eu / a : 0;
                    dPitch = System.Math.Abs(d) > 1e-6 ? ev / d : 0;
                }
                _yawFix = Clamp(_yawFix + Clamp(0.8 * dYaw, -0.25, 0.25), -0.8, 0.8);
                _pitchFix = Clamp(_pitchFix + Clamp(0.8 * dPitch, -0.25, 0.25), -0.6, 0.8);
            }
            return solved;
        }

        /// <summary>Where the launch first meets the target side wall, as (z along it, height).
        /// Reaching the front wall first reads as a NEGATIVE z (how far short of the corner it struck the
        /// front wall), so the solve still sees which way to turn; going down first reads as height 0.</summary>
        bool SideHit(TestPlayerController ctrl, AimState aim, Prediction pred, BallState ball, List<Surface> surfaces,
                     string stroke, bool prepared, SwingContext ctx, double yaw, double pitch, int wall, out double u, out double v)
        {
            u = 0; v = 0;
            if (!Launch(ctrl, aim, pred, stroke, prepared, ctx, yaw, pitch)) return false;
            _ball.Reset(pred.Point, _launch, null, ball.Temperature);
            string target = wall > 0 ? "sideWallRight" : "sideWallLeft";
            for (int i = 0; i < 150; i++)
            {
                _events.Clear();
                BallPhysics.Step(_ball, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceId == target) { u = e.Point.Z; v = e.Point.Y; return true; }
                    if (e.SurfaceClass == "frontWall") { u = -(HALF_W - wall * e.Point.X); v = e.Point.Y; return true; }
                    if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") { u = e.Point.Z; v = 0; return true; }
                    if (e.SurfaceClass == "sideWall") { u = e.Point.Z + 3; v = e.Point.Y; return true; }   // the wrong side wall: way off
                }
            }
            return false;
        }

        /// <summary>Where the launched ball meets the front wall (a ball that goes down counts as height 0).</summary>
        bool FlyFront(V3 origin, double temperature, List<Surface> surfaces, out double x, out double y)
        {
            x = 0; y = 0;
            _ball.Reset(origin, _launch, null, temperature);
            for (int i = 0; i < 180; i++)
            {
                _events.Clear();
                BallPhysics.Step(_ball, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall") { x = e.Point.X; y = e.Point.Y; return true; }
                    if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") { x = e.Point.X; y = 0; return true; }
                }
            }
            return false;
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
