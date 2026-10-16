// ===========================================================
// Assets/SquashSim/Lab/TestPresets.cs
// Port of lab/devlab/PhysicsTestPreset.js. Repeatable tests as DATA:
// a launch + where the player stands. Adding one = adding an entry.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Lab
{
    public sealed class TestPreset
    {
        public string Id;
        public string Name;
        public string Purpose;
        public V3 Player;
        public LaunchOverride Launch;
    }

    public static class TestPresets
    {
        static LaunchOverride L(double px, double py, double pz, double speed, double az, double el,
                                double temperature, double repeat, bool carry)
        {
            return new LaunchOverride
            {
                Position = new V3(px, py, pz), Speed = speed, AzimuthDeg = az, ElevationDeg = el,
                Temperature = temperature, Spin = new V3(0, 0, 0), RepeatIntervalSeconds = repeat,
                CarryTemperature = carry
            };
        }

        /// <summary>Built on demand so the free preset picks up the CURRENT initial temperature,
        /// as the lab's module-level array does at load time with default config.</summary>
        public static List<TestPreset> All()
        {
            double initial = SimConfig.Defaults().Temperature.Initial;
            return new List<TestPreset>
            {
                new TestPreset { Id = "free", Name = "Free Warm-Up", Purpose = "Gentle feed into the middle of the court.",
                    Player = new V3(0, 0, 6.2), Launch = L(0.9, 1.05, 6.4, 16.0, -6, 9, initial, 3.0, true) },
                new TestPreset { Id = "straightLength", Name = "Straight Length", Purpose = "Front-wall rebound and resulting depth.",
                    Player = new V3(-2.0, 0, 6.5), Launch = L(-2.0, 0.90, 7.50, 28.0, 0, 6, 24.0, 3.0, false) },
                new TestPreset { Id = "dropLowBall", Name = "Drop / Low Ball", Purpose = "Low-speed floor interaction and settling.",
                    Player = new V3(0.6, 0, 4.6), Launch = L(0.60, 0.60, 2.20, 11.0, 0, 8, 20.0, 2.5, false) },
                new TestPreset { Id = "backWall", Name = "Back Wall", Purpose = "Rear-wall rebound and the ball that comes off it.",
                    Player = new V3(0, 0, 5.0), Launch = L(0.0, 1.20, 2.50, 22.0, 180, 5, 24.0, 3.0, false) },
                new TestPreset { Id = "tightRail", Name = "Tight Rail", Purpose = "Shallow side-wall grazing stability.",
                    Player = new V3(-2.4, 0, 6.0), Launch = L(-2.60, 1.00, 7.00, 26.0, -4, 5, 26.0, 3.0, false) },
                new TestPreset { Id = "highSpeed", Name = "High-Speed Impact", Purpose = "Tunnelling / CCD stress test at kill-shot speed.",
                    Player = new V3(0, 0, 6.5), Launch = L(0.0, 1.40, 8.50, 72.0, 0, -2, 36.0, 2.0, false) }
            };
        }

        public static TestPreset Find(string id)
        {
            var all = All();
            foreach (var p in all) if (p.Id == id) return p;
            return all[0];
        }

        /// <summary>Configure the launcher, park the player, fire fresh (lab applyPreset).</summary>
        public static void Apply(TestPreset preset, BallLauncher launcher, TestPlayerController ctrl)
        {
            launcher.Apply(preset.Launch);
            if (ctrl != null && preset.Player != null)
            {
                ctrl.Player.Spawn.X = preset.Player.X;
                ctrl.Player.Spawn.Y = preset.Player.Y;
                ctrl.Player.Spawn.Z = preset.Player.Z;
                ctrl.Reset();
            }
            launcher.Fire(fresh: true);
        }
    }
}
