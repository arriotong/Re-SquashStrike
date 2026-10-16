// ===========================================================
// Assets/SquashSim/Player/PlayerMovement.cs
// Port of lab/player/PlayerMovement.js. Court movement only.
// "Easy to move, difficult to position perfectly": immediate response,
// real momentum, a cost to reversing, crisp braking. No sprint button,
// no precision button.
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public static class PlayerMovement
    {
        public static void Step(TestPlayer player, InputIntent intent, MovementAssist assist, double dt)
        {
            var cfg = PhysicsConfig.Current.Player;

            bool adjusting = assist != null && assist.HasIdealSpot &&
                DMath.Hypot(assist.IdealSpot.X - player.Pos.X, assist.IdealSpot.Z - player.Pos.Z) < cfg.AdjustRange;

            double analog = intent.MoveMagnitude > 0 ? intent.MoveMagnitude : 1;
            double maxSpeed = adjusting
                ? DMath.Min(cfg.AdjustSpeed, cfg.MaxSpeed * analog)
                : cfg.MaxSpeed * analog;

            bool wantsMove = intent.MoveMagnitude > 0.01;
            player.Adjusting = adjusting;

            double desiredX = intent.Move.X * maxSpeed;
            double desiredZ = intent.Move.Z * maxSpeed;

            double dvx = desiredX - player.Vel.X;
            double dvz = desiredZ - player.Vel.Z;
            double deltaMag = DMath.Hypot(dvx, dvz);

            double rate = wantsMove ? cfg.Acceleration : cfg.Braking;

            if (wantsMove && deltaMag > 1e-6)
            {
                double speed = DMath.Hypot(player.Vel.X, player.Vel.Z);
                if (speed > 0.25)
                {
                    double vdx = player.Vel.X / speed;
                    double vdz = player.Vel.Z / speed;
                    double inputMag = DMath.Hypot(intent.Move.X, intent.Move.Z);
                    if (inputMag == 0) inputMag = 1;           // JS: `|| 1`
                    double idx = intent.Move.X / inputMag;
                    double idz = intent.Move.Z / inputMag;
                    double alignment = vdx * idx + vdz * idz;
                    double reversal = (1 - alignment) * 0.5;
                    rate *= 1 - cfg.DirectionChangeCost * reversal;
                }
            }

            double maxDelta = rate * dt;
            if (deltaMag > maxDelta && deltaMag > 1e-8)
            {
                double s = maxDelta / deltaMag;
                dvx *= s; dvz *= s;
            }

            player.Vel.X += dvx;
            player.Vel.Z += dvz;

            player.Pos.X += player.Vel.X * dt;
            player.Pos.Z += player.Vel.Z * dt;

            ResolveCourtBounds(player, cfg.Radius);

            player.Speed = DMath.Hypot(player.Vel.X, player.Vel.Z);
            player.MovementState = DeriveMovementState(player, intent, adjusting);
        }

        /// <summary>Which footwork is the athlete using? Derived; drives readout/animation, never physics.</summary>
        static string DeriveMovementState(TestPlayer player, InputIntent intent, bool adjusting)
        {
            var cfg = PhysicsConfig.Current.Player;
            bool moving = intent.MoveMagnitude > 0.01;

            if (player.ReachState == ReachStates.Stretch) return "stretch";
            if (player.ReachState == ReachStates.DeepLunge) return "deep lunge";
            if (player.ReachState == ReachStates.Lunge) return "lunge";
            if (!moving && player.Speed < 0.25) return "set";
            if (adjusting) return "adjust steps";
            if (player.Speed > cfg.MaxSpeed * 0.75) return "run";

            double lateral = System.Math.Abs(intent.Move.X);
            double depth = System.Math.Abs(intent.Move.Z);
            if (lateral > depth * 1.6) return "shuffle";
            if (depth > lateral * 1.6) return "drive step";
            return "crossover";
        }

        /// <summary>Per-axis bounds; only the blocked axis is zeroed, so walls slide.</summary>
        static void ResolveCourtBounds(TestPlayer player, double r)
        {
            double minX = -HALF_W + r, maxX = HALF_W - r;
            double minZ = r, maxZ = LENGTH - r;

            if (player.Pos.X < minX) { player.Pos.X = minX; if (player.Vel.X < 0) player.Vel.X = 0; }
            else if (player.Pos.X > maxX) { player.Pos.X = maxX; if (player.Vel.X > 0) player.Vel.X = 0; }

            if (player.Pos.Z < minZ) { player.Pos.Z = minZ; if (player.Vel.Z < 0) player.Vel.Z = 0; }
            else if (player.Pos.Z > maxZ) { player.Pos.Z = maxZ; if (player.Vel.Z > 0) player.Vel.Z = 0; }
        }
    }
}
