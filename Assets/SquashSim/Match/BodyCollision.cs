// ===========================================================
// Assets/SquashSim/Match/BodyCollision.cs
// The two collisions a lab with one athlete never needed:
//
//   Ball vs body   Each athlete is a vertical capsule (Player.Radius,
//                  Player.Height). The ball bounces off it with very low
//                  restitution, relative to the athlete's own velocity.
//                  The step's path is sub-sampled so a 70 m/s ball cannot
//                  slip through a thin graze. Emits BodyHit; what it MEANS
//                  (let, stroke, losing the rally) is the referee's call.
//
//   Body vs body   Athletes cannot pass through each other. Overlap is
//                  pushed apart symmetrically and part of the closing
//                  speed is removed, so contact feels firm, not sticky.
//                  Emits PlayerContact on the step contact begins
//                  (the future interference system reads it).
//
// Like everything in the sim: deterministic, no randomness.
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public static class BodyCollision
    {
        const double SKIN = 1e-4;

        /// <summary>Resolve the ball against one athlete. <paramref name="prevPos"/> is the
        /// ball position at the start of the step. Returns a BodyHit event, or null.</summary>
        public static SimEvent BallVsBody(BallState ball, V3 prevPos, TestPlayer body, int index, double time)
        {
            var cfg = PhysicsConfig.Current.Body;
            if (!cfg.Enabled || ball.Asleep) return null;

            double rb = PhysicsConfig.Current.Ball.Radius;
            double R = PhysicsConfig.Current.Player.Radius;
            double H = PhysicsConfig.Current.Player.Height;
            double reach = R + rb;

            // Find the first sub-sample of this step's path that overlaps the capsule.
            int n = cfg.SweepSamples < 1 ? 1 : cfg.SweepSamples;
            double hx = 0, hy = 0, hz = 0;
            bool hit = false;
            for (int i = 1; i <= n && !hit; i++)
            {
                double t = (double)i / n;
                double x = prevPos.X + (ball.Pos.X - prevPos.X) * t;
                double y = prevPos.Y + (ball.Pos.Y - prevPos.Y) * t;
                double z = prevPos.Z + (ball.Pos.Z - prevPos.Z) * t;
                double cy = PhysicsConfig.Clamp(y, R, H - R);
                double dx = x - body.Pos.X, dy = y - cy, dz = z - body.Pos.Z;
                if (dx * dx + dy * dy + dz * dz < reach * reach) { hx = x; hy = y; hz = z; hit = true; }
            }
            if (!hit) return null;

            // Contact normal from the capsule axis to the ball centre.
            double cyc = PhysicsConfig.Clamp(hy, R, H - R);
            double nx = hx - body.Pos.X, ny = hy - cyc, nz = hz - body.Pos.Z;
            double d = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (d < 1e-9) { nx = 0; ny = 0; nz = 1; d = 1; }   // dead centre: push toward the back wall
            nx /= d; ny /= d; nz /= d;

            // Relative velocity (the athlete is moving too; the ball meets a moving body).
            double rvx = ball.Vel.X - body.Vel.X, rvy = ball.Vel.Y, rvz = ball.Vel.Z - body.Vel.Z;
            double vn = rvx * nx + rvy * ny + rvz * nz;

            // Place the ball on the capsule surface at the contact point.
            ball.Pos.X = body.Pos.X + nx * (reach + SKIN);
            ball.Pos.Z = body.Pos.Z + nz * (reach + SKIN);
            ball.Pos.Y = cyc + ny * (reach + SKIN);

            if (vn >= 0) return null;    // already separating: just un-overlapped

            double tx = rvx - vn * nx, ty = rvy - vn * ny, tz = rvz - vn * nz;
            double e = cfg.Restitution, k = cfg.TangentialRetain;
            ball.Vel.X = body.Vel.X + (-e * vn * nx + tx * k);
            ball.Vel.Y = (-e * vn * ny + ty * k);
            ball.Vel.Z = body.Vel.Z + (-e * vn * nz + tz * k);
            ball.Omega.X *= cfg.SpinRetain; ball.Omega.Y *= cfg.SpinRetain; ball.Omega.Z *= cfg.SpinRetain;
            BallPhysics.Wake(ball);

            return new SimEvent
            {
                Type = SimEventType.BodyHit,
                PlayerIndex = index,
                Point = new V3(ball.Pos.X - nx * rb, ball.Pos.Y - ny * rb, ball.Pos.Z - nz * rb),
                ImpactSpeed = System.Math.Sqrt(rvx * rvx + rvy * rvy + rvz * rvz),
                NormalSpeed = -vn,
                Restitution = e,
                Temperature = ball.Temperature,
                Time = time
            };
        }

        /// <summary>Keep two athletes apart. Returns a PlayerContact event on the step they
        /// first touch (wasTouching = whether they touched last step), else null.</summary>
        public static SimEvent BodyVsBody(TestPlayer a, int ia, TestPlayer b, int ib, bool wasTouching,
                                          out bool touching, double time)
        {
            touching = false;
            var cfg = PhysicsConfig.Current.PlayerContact;
            if (!cfg.Enabled) return null;

            double R = PhysicsConfig.Current.Player.Radius;
            double min = R * 2;
            double dx = b.Pos.X - a.Pos.X, dz = b.Pos.Z - a.Pos.Z;
            double d = System.Math.Sqrt(dx * dx + dz * dz);
            if (d >= min) return null;
            touching = true;

            double nx, nz;
            if (d < 1e-9) { nx = 1; nz = 0; } else { nx = dx / d; nz = dz / d; }
            double push = (min - d) * 0.5;
            a.Pos.X -= nx * push; a.Pos.Z -= nz * push;
            b.Pos.X += nx * push; b.Pos.Z += nz * push;
            ClampToCourt(a, R);
            ClampToCourt(b, R);

            // Remove part of the closing speed, shared equally.
            double rel = (b.Vel.X - a.Vel.X) * nx + (b.Vel.Z - a.Vel.Z) * nz;
            if (rel < 0)
            {
                double j = rel * cfg.Softness * 0.5;
                a.Vel.X += nx * j; a.Vel.Z += nz * j;
                b.Vel.X -= nx * j; b.Vel.Z -= nz * j;
            }

            if (wasTouching) return null;
            return new SimEvent
            {
                Type = SimEventType.PlayerContact,
                PlayerIndex = ia,
                OtherIndex = ib,
                Point = new V3((a.Pos.X + b.Pos.X) * 0.5, 1.0, (a.Pos.Z + b.Pos.Z) * 0.5),
                ImpactSpeed = -rel,
                Time = time
            };
        }

        static void ClampToCourt(TestPlayer p, double r)
        {
            p.Pos.X = PhysicsConfig.Clamp(p.Pos.X, -HALF_W + r, HALF_W - r);
            p.Pos.Z = PhysicsConfig.Clamp(p.Pos.Z, r, LENGTH - r);
        }
    }
}
