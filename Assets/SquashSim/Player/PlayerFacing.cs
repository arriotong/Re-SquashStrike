// ===========================================================
// Assets/SquashSim/Player/PlayerFacing.cs
// Port of lab/player/PlayerFacing.js. Automatic orientation and
// forehand/backhand determination - never chosen by the player.
// ===========================================================

namespace SquashSim
{
    public static class PlayerFacing
    {
        /// <summary>Ball must be this far to one side before the swing side may flip.</summary>
        const double SIDE_HYSTERESIS = 0.16;

        public static void Step(TestPlayer player, BallState ball, double dt)
        {
            var cfg = PhysicsConfig.Current.Player;

            double targetFacing = 0;
            if (ball != null)
            {
                double dx = ball.Pos.X - player.Pos.X;
                double dz = ball.Pos.Z - player.Pos.Z;
                if (DMath.Hypot(dx, dz) > 0.05)
                {
                    targetFacing = DMath.Atan2(dx, -dz);
                }
            }

            double diff = targetFacing - player.Facing;
            while (diff > DMath.PI) diff -= DMath.PI * 2;
            while (diff < -DMath.PI) diff += DMath.PI * 2;

            double maxTurn = cfg.TurnRate * dt;
            player.Facing += DMath.Max(-maxTurn, DMath.Min(maxTurn, diff));

            if (ball != null) UpdateSwingSide(player, ball);
        }

        static void UpdateSwingSide(TestPlayer player, BallState ball)
        {
            double lateral = ball.Pos.X - player.Pos.X;

            if (player.SwingSide == 0)
            {
                player.SwingSide = lateral >= 0 ? 1 : -1;
                return;
            }
            if (player.SwingSide > 0 && lateral < -SIDE_HYSTERESIS) player.SwingSide = -1;
            else if (player.SwingSide < 0 && lateral > SIDE_HYSTERESIS) player.SwingSide = 1;
        }

        public static string SwingSideName(TestPlayer player)
        {
            return player.SwingSide >= 0 ? "Forehand" : "Backhand";
        }
    }
}
