// ===========================================================
// Assets/SquashSim/Player/LungeStep.cs
// M6 (game profile only; null in the lab): the visible lunge.
//
// A committed swing's racket path is fixed in the world and passes
// through the predicted contact. When that contact is further from the
// body than an arm and a racket can cover (ArmReach), the athlete steps
// toward it during the swing, arriving at contact - the "auto jump
// closer" that a real player's lunge is. The step is limited (MaxStep),
// so the ball must still be within ArmReach + MaxStep of where the swing
// started: total reach from standing is a little less than the lab's
// arm-only 1.55 m, and you SEE the body get there.
//
// It moves the body only: the racket path, the contact and the ball are
// unchanged, so the AI's shot model and the referee need nothing new.
// ===========================================================

namespace SquashSim
{
    public sealed class LungeSection
    {
        public bool Enabled;
        /// <summary>Horizontal body-to-contact distance an arm and racket cover without stepping.</summary>
        public double ArmReach;
        /// <summary>The longest lunge step during one swing.</summary>
        public double MaxStep;
    }

    public static class LungeStep
    {
        public static void Step(TestPlayer player, SwingController sw, double dt)
        {
            var cfg = PhysicsConfig.Current.Lunge;
            if (cfg == null || !cfg.Enabled) return;
            if (sw.State != SwingStates.Swinging || sw.HasContacted) return;

            // The contact on the committed arc (SwingController.Commit, inverted).
            double cx = sw.Center.X + DMath.Sin(sw.ContactAngle) * sw.Reach;
            double cz = sw.Center.Z - DMath.Cos(sw.ContactAngle) * sw.Reach;
            double dx = cx - player.Pos.X, dz = cz - player.Pos.Z;
            double d = System.Math.Sqrt(dx * dx + dz * dz);
            double over = d - cfg.ArmReach;
            double allowed = cfg.MaxStep - sw.LungeUsed;
            if (over <= 0 || allowed <= 0 || d < 1e-6) return;

            // Arrive as the racket meets the ball (contact is at half the swing).
            double toContact = sw.Duration * 0.5 - sw.SwingTime;
            double want = System.Math.Min(over, allowed);
            double move = toContact > dt ? want * dt / toContact : want;
            player.Pos.X += dx / d * move;
            player.Pos.Z += dz / d * move;
            sw.LungeUsed += move;
            PlayerMovement.ClampToCourt(player);
        }
    }
}
