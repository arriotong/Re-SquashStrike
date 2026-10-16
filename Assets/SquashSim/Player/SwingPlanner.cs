// ===========================================================
// Assets/SquashSim/Player/SwingPlanner.cs
// Port of lab/player/SwingPlanner.js. Chooses the athlete's technique
// so the player does not have to. Returns a physical racket MOTION
// (head speed, stroke time, face opening, path rise) - never a ball
// velocity. Pace is a consequence of execution, never a charge.
// ===========================================================

namespace SquashSim
{
    public sealed class SwingPlan
    {
        public string Technique;
        public string Base;
        public string Mode;
        public double HeadSpeed;
        public double Duration;
        public double FacePitch;
        public double PathRise;
        public double PaceScale;
    }

    public struct SwingContext
    {
        public string Mode;           // "quick" | "prepared" | null
        public double PrepQuality;
        public double Quality;
        public string ReachState;
        public bool IsVolley;
        public bool Rushed;
    }

    public static class SwingPlanner
    {
        static string ChooseBase(string stroke, AimState aim, double quality)
        {
            var plan = PhysicsConfig.Current.SwingPlan;
            if (stroke == "touch") return "touch";
            if (aim.BiasY >= plan.HighBias) return "lift";
            if (aim.BiasY <= plan.LowBias && quality > 0.6) return "attack";
            return "neutral";
        }

        static string TechniqueName(string baseName, string mode, int side, AimState aim, SwingContext context)
        {
            string family;
            switch (baseName)
            {
                case "attack": family = "ATTACKING"; break;
                case "lift": family = "DEFENSIVE"; break;
                case "touch": family = "TOUCH"; break;
                default: family = "STANDARD"; break;
            }
            string m = mode == "prepared" ? "PREPARED" : "QUICK";
            string hand = side >= 0 ? "FOREHAND" : "BACKHAND";
            string height = aim.BiasY > 0.35 ? "HIGH" : (aim.BiasY < -0.25 ? "LOW" : "MID");
            string volley = context.IsVolley ? "_VOLLEY" : "";
            string stretched = (context.ReachState == ReachStates.Stretch || context.ReachState == ReachStates.DeepLunge)
                ? "_STRETCHED" : "";
            return family + "_" + m + "_" + hand + volley + "_" + height + stretched;
        }

        /// <param name="stroke">"hit" | "touch"</param>
        /// <param name="side">+1 forehand, -1 backhand</param>
        public static SwingPlan Plan(string stroke, AimState aim, int side, SwingContext context)
        {
            var cfg = PhysicsConfig.Current.Racket;
            var plan = PhysicsConfig.Current.SwingPlan;
            var strokes = PhysicsConfig.Current.Strokes;

            double quality = DMath.Max(0, DMath.Min(1, context.Quality));
            string baseName = ChooseBase(stroke, aim, quality);
            SwingProfile profile = cfg.Swing.Get(baseName) ?? cfg.Swing.Neutral;

            bool prepared = context.Mode == "prepared";
            double modePace = prepared ? strokes.Prepared.PaceScale : strokes.Quick.PaceScale;
            double modeDuration = prepared ? strokes.Prepared.DurationScale : strokes.Quick.DurationScale;

            double paceScale = plan.MinQualityPaceScale + (1 - plan.MinQualityPaceScale) * quality;
            paceScale *= modePace;
            if (context.Rushed) paceScale *= plan.RushedPaceScale;
            if (context.IsVolley) paceScale *= plan.VolleyPaceScale;

            double duration = profile.Duration * modeDuration;
            if (context.IsVolley) duration *= plan.VolleyDurationScale;

            double facePitch = profile.FacePitch;
            if (context.ReachState == ReachStates.DeepLunge) facePitch += 0.10;
            else if (context.ReachState == ReachStates.Stretch) facePitch += 0.20;

            return new SwingPlan
            {
                Technique = TechniqueName(baseName, context.Mode, side, aim, context),
                Base = baseName,
                Mode = context.Mode ?? "quick",
                HeadSpeed = profile.HeadSpeed * paceScale,
                Duration = duration,
                FacePitch = facePitch,
                PathRise = profile.PathRise,
                PaceScale = paceScale
            };
        }
    }
}
