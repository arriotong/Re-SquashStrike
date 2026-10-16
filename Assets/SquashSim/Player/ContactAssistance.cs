// ===========================================================
// Assets/SquashSim/Player/ContactAssistance.cs
// Port of lab/player/ContactAssistance.js. Accessibility WITHOUT fake
// trajectories: if the ball passes close to the racket path in the
// contact window, nudge the head POSITION a little. Never touches the
// ball; head velocity is always from the uncorrected path, so assist
// can never manufacture pace.
// ===========================================================

namespace SquashSim
{
    public struct AssistResult
    {
        public double X, Y, Z;
        public double Applied;
        public double MissDistance;
        public bool InRange;
    }

    public static class ContactAssistance
    {
        public static AssistResult Compute(V3 headRawPos, V3 faceNormal, BallState ball, bool active)
        {
            var cfg = PhysicsConfig.Current.Assist;
            var result = new AssistResult { MissDistance = double.PositiveInfinity };

            if (!cfg.Enabled || !active || ball == null) return result;

            double dx = ball.Pos.X - headRawPos.X;
            double dy = ball.Pos.Y - headRawPos.Y;
            double dz = ball.Pos.Z - headRawPos.Z;

            // Only the in-face-plane miss matters.
            V3 n = faceNormal;
            double along = dx * n.X + dy * n.Y + dz * n.Z;
            double px = dx - along * n.X;
            double py = dy - along * n.Y;
            double pz = dz - along * n.Z;

            double lateralMiss = System.Math.Sqrt(px * px + py * py + pz * pz);
            double strings = PhysicsConfig.Current.Racket.HeadRadius;

            // Aim for the middle of the strings, not merely inside the rim.
            double gap = lateralMiss - strings * 0.30;
            result.MissDistance = DMath.Max(0, gap);

            if (gap <= 0) return result;
            if (gap > cfg.Radius) return result;

            result.InRange = true;

            double move = DMath.Min(gap * cfg.Strength, cfg.MaxCorrection);
            double inv = move / lateralMiss;
            result.X = px * inv;
            result.Y = py * inv;
            result.Z = pz * inv;
            result.Applied = move;

            return result;
        }
    }
}
