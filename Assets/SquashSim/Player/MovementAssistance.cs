// ===========================================================
// Assets/SquashSim/Player/MovementAssistance.cs
// Port of lab/player/MovementAssistance.js. Micro-positioning, the
// reach ladder, and balance. Corrects CENTIMETRES, never metres; never
// routes the player to the ball; never rescues real positional errors.
// ===========================================================

namespace SquashSim
{
    public sealed class MovementAssist
    {
        public bool Active;
        public readonly V2 Correction = new V2();
        public double Applied;
        public double TravelUsed;
        public readonly V2 IdealSpot = new V2();
        public bool HasIdealSpot;
        public string ReachState = ReachStates.Normal;
        public double Balance = 1;
        public double Quality = 1;

        public void Reset()
        {
            Active = false;
            Correction.X = 0; Correction.Z = 0;
            Applied = 0;
            TravelUsed = 0;
            HasIdealSpot = false;
            ReachState = ReachStates.Normal;
            Balance = 1;
            Quality = 1;
        }

        /// <summary>Clear the per-stroke travel budget when a stroke resolves.</summary>
        public void ReleaseBudget()
        {
            TravelUsed = 0;
            Applied = 0;
        }
    }

    public static class MovementAssistance
    {
        static void ComputeIdealSpot(V2 output, Prediction pred, TestPlayer player)
        {
            var cfg = PhysicsConfig.Current.MovementAssist;
            int stance = player.SwingSide >= 0 ? 1 : -1;
            int side = -stance;
            output.X = pred.Point.X + side * cfg.PreferredSpacing;
            output.Z = pred.Point.Z + 0.12;
        }

        public static MovementAssist Step(MovementAssist ma, TestPlayer player, Prediction pred, InputIntent intent, double dt)
        {
            var cfg = PhysicsConfig.Current.MovementAssist;

            ma.Correction.X = 0;
            ma.Correction.Z = 0;
            ma.Active = false;

            if (!pred.Valid)
            {
                ma.HasIdealSpot = false;
                ma.ReachState = ReachStates.Normal;
                UpdateBalance(ma, player, ReachStates.Normal, dt);
                return ma;
            }

            ComputeIdealSpot(ma.IdealSpot, pred, player);
            ma.HasIdealSpot = true;

            ma.ReachState = ContactPrediction.ReachStateFor(pred.Distance);

            if (cfg.Enabled)
            {
                double dx = ma.IdealSpot.X - player.Pos.X;
                double dz = ma.IdealSpot.Z - player.Pos.Z;
                double gap = DMath.Hypot(dx, dz);

                if (gap > 0.02 && gap <= cfg.Range && ma.TravelUsed < cfg.MaxTravel)
                {
                    double budget = DMath.Min(cfg.MaxSpeed, (gap / DMath.Max(dt, 1e-4)) * cfg.Strength);
                    double speed = DMath.Min(budget, cfg.MaxSpeed);

                    double remaining = DMath.Max(0, cfg.MaxTravel - ma.TravelUsed);
                    speed = DMath.Min(speed, remaining / DMath.Max(dt, 1e-4));

                    double cx = (dx / gap) * speed;
                    double cz = (dz / gap) * speed;

                    player.Vel.X += cx * dt * cfg.Responsiveness;
                    player.Vel.Z += cz * dt * cfg.Responsiveness;

                    double moved = DMath.Hypot(cx, cz) * dt;
                    ma.TravelUsed = DMath.Min(cfg.MaxTravel, ma.TravelUsed + moved);
                    ma.Applied = ma.TravelUsed;
                    ma.Correction.X = cx;
                    ma.Correction.Z = cz;
                    ma.Active = true;
                }

                // Never stand on the ball: hard push-out (the game: only as it arrives - PushOutWithin).
                double bx = player.Pos.X - pred.Point.X;
                double bz = player.Pos.Z - pred.Point.Z;
                double bd = DMath.Hypot(bx, bz);
                if (bd > 1e-4 && bd < cfg.MinSpacing && (cfg.PushOutWithin <= 0 || pred.Time <= cfg.PushOutWithin))
                {
                    double push = (cfg.MinSpacing - bd);
                    player.Pos.X += (bx / bd) * push;
                    player.Pos.Z += (bz / bd) * push;
                }
            }

            UpdateBalance(ma, player, ma.ReachState, dt);
            return ma;
        }

        static double BalanceCostFor(string reachState)
        {
            var c = PhysicsConfig.Current.Reach.BalanceCost;
            switch (reachState)
            {
                case ReachStates.Normal: return c.Normal;
                case ReachStates.Lunge: return c.Lunge;
                case ReachStates.DeepLunge: return c.DeepLunge;
                case ReachStates.Stretch: return c.Stretch;
                default: return 0;   // JS: balanceCost[state] ?? 0
            }
        }

        static void UpdateBalance(MovementAssist ma, TestPlayer player, string reachState, double dt)
        {
            double cost = BalanceCostFor(reachState);
            if (player.Stamina < 1.0) cost *= Fatigue.BalanceCostScale(player);   // match only
            double speedPenalty = DMath.Min(0.35, player.Speed / (PhysicsConfig.Current.Player.MaxSpeed * 3));
            double target = DMath.Max(0, 1 - cost - speedPenalty);

            double k = 1 - DMath.Exp(-6.0 * dt);
            ma.Balance += (target - ma.Balance) * k;
            ma.Balance = DMath.Max(0, DMath.Min(1, ma.Balance));

            ma.Quality = ma.Balance;
            player.Balance = ma.Balance;
            player.ReachState = reachState;
        }
    }
}
