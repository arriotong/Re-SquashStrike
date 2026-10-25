// ===========================================================
// Assets/SquashSim/Match/Fatigue.cs
// Fatigue (Phase 1 decision 2: built after M3, tuned from MatchBench
// rally data, visible on the body - never a bar).
//
// Stamina is 0..1 per athlete:
//   drains   with effort: (speed / max speed)^2 per second while moving,
//            plus a cost for every lunge, deep lunge and stretch stroke
//   recovers slowly while standing in a rally, faster between rallies
//
// A tired athlete is a little slower to accelerate and has a lower top
// speed, and every reach rung costs more balance (so a stretched stroke
// loses more pace and angle). The effects are deliberately small: long
// rallies should matter, not decide everything.
//
// TestPlayer.Stamina stays exactly 1.0 in the lab (nothing calls this),
// and PlayerMovement / MovementAssistance only touch it below 1.0, so
// the lab parity traces are unaffected.
// ===========================================================

namespace SquashSim
{
    public static class Fatigue
    {
        /// <summary>Call once per fixed step for an athlete in a match.</summary>
        public static void Update(TestPlayer p, bool rallyLive, double dt)
        {
            var f = PhysicsConfig.Current.Fatigue;
            if (!f.Enabled) return;
            double max = PhysicsConfig.Current.Player.MaxSpeed;
            double frac = max > 0 ? p.Speed / max : 0;
            double s = p.Stamina;
            double drain = f.DrainRate * frac * frac * dt;
            double k = p.Attributes.DrainScale;                    // P3-C1: exactly 1 when neutral
            s -= k != 1.0 ? drain * k : drain;
            if (!rallyLive) s += f.RecoverBetweenRallies * dt;
            else if (p.Speed < f.StandingSpeed) s += f.RecoverStanding * dt;
            p.Stamina = s < f.Floor ? f.Floor : (s > 1 ? 1 : s);
        }

        /// <summary>The effort of the stroke just played (by reach rung).</summary>
        public static void OnStroke(TestPlayer p, string reachState)
        {
            var f = PhysicsConfig.Current.Fatigue;
            if (!f.Enabled) return;
            double cost;
            switch (reachState)
            {
                case ReachStates.Lunge: cost = f.LungeCost; break;
                case ReachStates.DeepLunge: cost = f.DeepLungeCost; break;
                case ReachStates.Stretch: cost = f.StretchCost; break;
                default: cost = 0; break;
            }
            double k = p.Attributes.DrainScale;
            if (k != 1.0) cost *= k;
            double s = p.Stamina - cost;
            p.Stamina = s < f.Floor ? f.Floor : s;
        }

        /// <summary>Multiplier on top speed and acceleration (1 when fresh).</summary>
        public static double MovementScale(TestPlayer p)
        {
            return 1 - PhysicsConfig.Current.Fatigue.SpeedLoss * (1 - p.Stamina);
        }

        /// <summary>Multiplier on the balance cost of each reach rung (1 when fresh).</summary>
        public static double BalanceCostScale(TestPlayer p)
        {
            return 1 + PhysicsConfig.Current.Fatigue.BalanceCostGain * (1 - p.Stamina);
        }
    }
}
