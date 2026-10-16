// ===========================================================
// Assets/SquashSim/Ball/BallTemperature.cs
// Port of lab/ball/BallTemperature.js. Temperature is ONE NUMBER;
// "Cold / Warming / Match Ready / Hot" are presentation labels.
// Physics may only use the number, via RestitutionMultiplier().
// ===========================================================

namespace SquashSim
{
    public static class BallTemperature
    {
        public static double Clamp(double t)
        {
            var cfg = PhysicsConfig.Current.Temperature;
            return PhysicsConfig.Clamp(t, cfg.Min, cfg.Max);
        }

        /// <summary>Heat deposited by an impact, driven by energy actually dissipated.</summary>
        /// <param name="source">"racket" | "wall" | "floor"</param>
        public static double ApplyImpactHeat(BallState ball, double energyLostJ, string source)
        {
            var cfg = PhysicsConfig.Current.Temperature;
            if (!(energyLostJ > cfg.MinHeatEnergy)) return 0;

            double scale;
            switch (source)
            {
                case "racket": scale = cfg.RacketHeatScale; break;
                case "floor": scale = cfg.FloorHeatScale; break;
                default: scale = cfg.WallHeatScale; break;
            }

            double before = ball.Temperature;
            double delta = cfg.HeatingMultiplier * cfg.HeatPerJoule * energyLostJ * scale;
            ball.Temperature = Clamp(before + delta);
            return ball.Temperature - before;
        }

        /// <summary>Exponential cooling toward ambient; framerate-independent.</summary>
        public static void CoolOverTime(BallState ball, double dt)
        {
            var cfg = PhysicsConfig.Current.Temperature;
            double k = cfg.CoolingRate;
            if (k <= 0) return;
            double ambient = cfg.Ambient;
            ball.Temperature = ambient + (ball.Temperature - ambient) * DMath.Exp(-k * dt);
            ball.Temperature = Clamp(ball.Temperature);
        }

        public static double RestitutionMultiplier(double temperature)
        {
            return PhysicsConfig.SampleCurve(PhysicsConfig.Current.Temperature.Response, temperature);
        }

        /// <summary>Presentation label only. Never branch physics on this.</summary>
        public static string StateName(double temperature)
        {
            var states = PhysicsConfig.Current.Temperature.States;
            for (int i = 0; i < states.Length; i++)
            {
                if (temperature < states[i].Below) return states[i].Name;
            }
            return states[states.Length - 1].Name;
        }

        /// <summary>0..1 through the configured range, for meters.</summary>
        public static double Normalised(double temperature)
        {
            var cfg = PhysicsConfig.Current.Temperature;
            double span = cfg.Max - cfg.Min;
            return span <= 0 ? 0 : PhysicsConfig.Clamp((temperature - cfg.Min) / span, 0, 1);
        }
    }
}
