// ===========================================================
// Assets/SquashSim/Player/AimController.cs
// Port of lab/player/AimController.js. V5 analog directional bias.
// No cursor. Mouse and stick share one deadzone + response curve here.
// Output is a DESIRED direction; the body and racket must still get
// there (RacketIntentSolver) and the ball goes where contact sends it.
// IntendedPoint/IntendedWall are debug-only.
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed class AimState
    {
        public double RawX, RawY;
        public double BiasX, BiasY;
        public double DesiredYaw;
        public double DesiredPitch = 0.1;
        public double NeutralPitch = 0.1;
        public readonly V3 IntendedPoint = new V3(0, 1.5, 0);   // debug only
        public string IntendedWall = "frontWall";               // debug only
        /// <summary>Front-wall target aim (WallAim, game only): the point the stick picked on the front wall.</summary>
        public bool HasWallTarget;
        public readonly V3 WallTarget = new V3(0, 1.6, 0);
        /// <summary>0 = front wall; -1 / +1 = left / right side wall (a boast).</summary>
        public int TargetWall;
        /// <summary>The target came from the reticle point (InputIntent.HasAimPoint), not the stick bias.</summary>
        public bool FromPoint;

        public void CopyFrom(AimState o)
        {
            RawX = o.RawX; RawY = o.RawY; BiasX = o.BiasX; BiasY = o.BiasY;
            DesiredYaw = o.DesiredYaw; DesiredPitch = o.DesiredPitch; NeutralPitch = o.NeutralPitch;
            HasWallTarget = o.HasWallTarget; WallTarget.CopyFrom(o.WallTarget); TargetWall = o.TargetWall; FromPoint = o.FromPoint;
        }
    }

    public static class AimController
    {
        /// <summary>Deadzone with rescale, then a power response curve.</summary>
        public static double ShapeAxis(double v, double deadzone, double exponent)
        {
            double a = System.Math.Abs(v);
            if (a <= deadzone) return 0;
            double t = DMath.Min(1, (a - deadzone) / DMath.Max(1e-6, 1 - deadzone));
            return DMath.Sign(v) * DMath.Pow(t, exponent);
        }

        static string FirstWallHit(V3 output, double ox, double oy, double oz, double dx, double dy, double dz)
        {
            double bestT = double.PositiveInfinity;
            string wall = "frontWall";
            if (dz < -1e-6)
            {
                double t = -oz / dz;
                double x = ox + dx * t, y = oy + dy * t;
                if (t > 0 && System.Math.Abs(x) <= HALF_W && y >= 0 && y <= CEILING_H) { bestT = t; wall = "frontWall"; }
            }
            for (int si = 0; si < 2; si++)
            {
                int side = si == 0 ? -1 : 1;
                if (System.Math.Abs(dx) < 1e-6) continue;
                double t = (side * HALF_W - ox) / dx;
                if (t <= 0 || t >= bestT) continue;
                double z = oz + dz * t, y = oy + dy * t;
                if (z >= 0 && z <= LENGTH && y >= 0 && y <= CEILING_H)
                {
                    bestT = t; wall = side > 0 ? "sideWallRight" : "sideWallLeft";
                }
            }
            if (double.IsInfinity(bestT) || double.IsNaN(bestT))
            {
                bestT = dz < -1e-6 ? -oz / dz : 1; wall = "frontWall";
            }
            output.X = ox + dx * bestT; output.Y = oy + dy * bestT; output.Z = oz + dz * bestT;
            return wall;
        }

        /// <param name="origin">where the ball will be struck from</param>
        public static AimState Step(AimState aim, InputIntent intent, V3 origin)
        {
            var cfg = PhysicsConfig.Current.Aim;

            aim.RawX = intent.AimRawX;
            aim.RawY = intent.AimRawY;
            aim.BiasX = ShapeAxis(aim.RawX, cfg.Deadzone, cfg.HorizontalResponse);
            aim.BiasY = ShapeAxis(aim.RawY, cfg.Deadzone, cfg.VerticalResponse);

            aim.DesiredYaw = aim.BiasX * cfg.MaxYaw;

            double distToFront = DMath.Max(0.5, origin.Z) / DMath.Max(0.3, DMath.Cos(aim.DesiredYaw));
            aim.NeutralPitch = DMath.Atan2(cfg.RallyHeight - origin.Y, distToFront);

            double h = aim.BiasY >= 0
                ? cfg.RallyHeight + (cfg.HighHeight - cfg.RallyHeight) * aim.BiasY
                : cfg.RallyHeight + (cfg.RallyHeight - cfg.LowHeight) * aim.BiasY;
            aim.DesiredPitch = DMath.Atan2(h - origin.Y, distToFront);

            double cp = DMath.Cos(aim.DesiredPitch);
            aim.IntendedWall = FirstWallHit(
                aim.IntendedPoint, origin.X, origin.Y, origin.Z,
                DMath.Sin(aim.DesiredYaw) * cp, DMath.Sin(aim.DesiredPitch), -DMath.Cos(aim.DesiredYaw) * cp);
            return aim;
        }

        /// <summary>Debug-only: where a direction from a point meets a wall.</summary>
        public static string WallIntersection(V3 output, V3 origin, double yaw, double pitch)
        {
            double cp = DMath.Cos(pitch);
            return FirstWallHit(output, origin.X, origin.Y, origin.Z,
                DMath.Sin(yaw) * cp, DMath.Sin(pitch), -DMath.Cos(yaw) * cp);
        }
    }
}
