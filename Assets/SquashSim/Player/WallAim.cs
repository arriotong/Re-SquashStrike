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
// The AI keeps the lab aim (its shot model was built on it), and the lab
// itself never turns this on, so SimParity is untouched.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class WallAimSection
    {
        public double HalfWidth;       // full stick left/right: this far either side of the middle (m)
        public double NeutralHeight;   // stick centred: this high on the front wall (m)
        public double MinHeight;       // stick fully down (kept above the tin)
        public double MaxHeight;       // stick fully up (a lob)
        public int RefreshSteps;       // re-solve the launch every N steps
        public int Iterations;         // solve iterations per refresh
    }

    public sealed class WallAim
    {
        readonly BallState _ball = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _launch = new V3();
        double _yawFix, _pitchFix;
        int _age;

        /// <summary>Did the last solve converge on the target (else the straight-line aim is used)?</summary>
        public bool Solved { get; private set; }

        public static void TargetFor(double biasX, double biasY, V3 output)
        {
            var c = PhysicsConfig.Current.WallAim;
            output.X = biasX * c.HalfWidth;
            output.Y = biasY >= 0
                ? c.NeutralHeight + (c.MaxHeight - c.NeutralHeight) * biasY
                : c.NeutralHeight + (c.NeutralHeight - c.MinHeight) * biasY;
            output.Z = 0;
        }

        /// <summary>Turn the aim's bias into a front-wall target and point yaw / pitch at it.</summary>
        public void Apply(TestPlayerController ctrl, AimState aim, V3 origin, BallState ball, List<Surface> surfaces)
        {
            var cfg = PhysicsConfig.Current.WallAim;
            if (cfg == null) return;
            TargetFor(aim.BiasX, aim.BiasY, aim.WallTarget);
            aim.HasWallTarget = true;

            // Straight line from the contact to the target...
            double tx = aim.WallTarget.X, ty = aim.WallTarget.Y;
            double dx = tx - origin.X, dz = DMath.Max(0.5, origin.Z);
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
                Solved = false;
                for (int it = 0; it < cfg.Iterations; it++)
                {
                    aim.DesiredYaw = baseYaw + _yawFix;
                    aim.DesiredPitch = basePitch + _pitchFix;
                    var plan = SwingPlanner.Plan(stroke, aim, ctrl.Player.SwingSide >= 0 ? 1 : -1, new SwingContext
                    {
                        Mode = prepared ? "prepared" : "quick", Quality = ctrl.Assist.Quality,
                        ReachState = ctrl.Assist.ReachState, IsVolley = pred.IsVolley
                    });
                    if (!ShotPreview.PredictLaunch(ctrl, pred.Vel, plan, aim, prepared, ShotPreview.QuickHeadFactor, _launch)) break;
                    if (!Fly(pred.Point, ball.Temperature, surfaces, out double hx, out double hy)) break;
                    double ex = tx - hx, ey = ty - hy;
                    _yawFix = Clamp(_yawFix + 0.9 * ex / horiz, -0.6, 0.6);
                    _pitchFix = Clamp(_pitchFix + 0.9 * ey / horiz, -0.6, 0.8);
                    if (ex * ex + ey * ey < 0.05 * 0.05) { Solved = true; break; }
                    Solved = true;
                }
            }
            aim.DesiredYaw = baseYaw + _yawFix;
            aim.DesiredPitch = basePitch + _pitchFix;
        }

        /// <summary>Where the launched ball meets the front wall (a ball that goes down counts as height 0).</summary>
        bool Fly(V3 origin, double temperature, List<Surface> surfaces, out double x, out double y)
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
