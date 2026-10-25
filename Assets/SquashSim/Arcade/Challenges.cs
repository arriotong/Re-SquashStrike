// ===========================================================
// Assets/SquashSim/Arcade/Challenges.cs
// P2-M2: every Target Arcade challenge and drill, as data.
// 6 modes x Easy / Medium / Hard = 18 challenges, plus 5 drills.
// A challenge is {mode, level, seed, sizes, counts, star scores};
// ArcadeSession plays it.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Arcade
{
    public enum ArcadeMode { TimeAttack, Precision, ShotMenu, Streak, ServeTargets, WallRally, Drill }
    public enum DrillKind { None, Length, Drop, BoastDrive, Ghosting, NickHunt }

    public sealed class Challenge
    {
        public string Id, Name, Description;
        public ArcadeMode Mode;
        public DrillKind Drill;
        public int Level;                       // 0 easy, 1 medium, 2 hard
        public uint Seed;
        public double Seconds;                  // > 0: a clock
        public int Balls;                       // > 0: a ball count
        public double RingRadius;               // front-wall rings
        public double FeedSpeed, FeedSpeedSpread;
        public bool FeedBothSides;              // else always to the forehand side
        public double ZoneScale = 1;            // floor zones (1 = generous)
        public string[] Shots;                  // Shot Menu: the shots it may call
        public int[] Stars = { 0, 0, 0 };       // scores for 1 / 2 / 3 stars

        public static readonly string[] LevelNames = { "Easy", "Medium", "Hard" };
        public string LevelName { get { return LevelNames[System.Math.Max(0, System.Math.Min(2, Level))]; } }

        /// <summary>Stars (0..3) for a score.</summary>
        public int StarsFor(int score)
        {
            int s = 0;
            for (int i = 0; i < 3; i++) if (Stars[i] > 0 && score >= Stars[i]) s = i + 1;
            return s;
        }
    }

    public static class Challenges
    {
        public static readonly string[] ModeNames = { "Time Attack", "Precision", "Shot Menu", "Streak", "Serve Targets", "Wall Rally" };
        public static readonly string[] ModeBlurbs =
        {
            "60 seconds. Hit as many targets as you can - keep the combo going.",
            "20 balls, one target each. Closer to the centre scores more.",
            "20 balls. Play the shot it calls, into its zone.",
            "Targets until the first miss. They get smaller.",
            "10 serves. Lob or hard - land them deep in the back corner.",
            "Just you and the front wall. Hit the targets, keep it going.",
        };

        static Challenge Make(ArcadeMode mode, int level)
        {
            var c = new Challenge { Mode = mode, Level = level, Seed = (uint)(1000 + (int)mode * 37 + level * 7) };
            c.Name = ModeNames[(int)mode];
            c.Description = ModeBlurbs[(int)mode];
            c.Id = mode.ToString().ToLowerInvariant() + "-" + Challenge.LevelNames[level].ToLowerInvariant();
            double[] ring = { 0.55, 0.42, 0.30 };
            double[] speed = { 22, 25, 28 };
            double[] spread = { 1, 2, 3 };
            c.RingRadius = ring[level];
            c.FeedSpeed = speed[level];
            c.FeedSpeedSpread = spread[level];
            c.FeedBothSides = level > 0;
            c.ZoneScale = new[] { 1.0, 0.8, 0.6 }[level];
            switch (mode)
            {
                case ArcadeMode.TimeAttack: c.Seconds = 60; break;
                case ArcadeMode.Precision: c.Balls = 20; c.RingRadius = new[] { 0.65, 0.50, 0.38 }[level]; break;
                case ArcadeMode.ShotMenu:
                    c.Balls = 20;
                    c.Shots = level == 0 ? new[] { "drive", "kill", "lob" }                 // the drop only on Hard (see DefaultStars)
                            : level == 1 ? new[] { "drive", "lob", "kill", "boast" }
                            : new[] { "drive", "lob", "drop", "kill", "boast" };
                    break;
                case ArcadeMode.Streak: c.RingRadius = new[] { 0.60, 0.48, 0.38 }[level]; break;
                case ArcadeMode.ServeTargets: c.Balls = 10; break;
                case ArcadeMode.WallRally: c.Seconds = 90; c.FeedSpeed = 22; break;
            }
            c.Stars = DefaultStars(mode, level);
            return c;
        }

        /// <summary>Star scores, calibrated with MatchBench --arcade (a human-style bot): 1 star ~ its median,
        /// 2 stars ~ its top quarter, 3 stars above its best.</summary>
        static int[] DefaultStars(ArcadeMode mode, int level)
        {
            switch (mode)
            {
                // MatchBench --arcade, 20 plays each (2026-10-18): 1 star ~ 70% of the bot's median, 2 ~ its top quarter, 3 ~ its best.
                case ArcadeMode.TimeAttack: return new[] { new[] { 2200, 3700, 4600 }, new[] { 1800, 2850, 3900 }, new[] { 1250, 2400, 2700 } }[level];
                case ArcadeMode.Precision: return new[] { new[] { 3700, 5900, 7700 }, new[] { 2600, 5350, 6250 }, new[] { 2250, 4100, 4850 } }[level];
                // 2026-10-25 playtest: Easy called the drop (the hardest shot to land) and scored below Hard - the drop is
                // Hard's now, the stars re-measured; Streak Hard's first star was above the median player
                case ArcadeMode.ShotMenu: return new[] { new[] { 1600, 2600, 4300 }, new[] { 1600, 2850, 4350 }, new[] { 1200, 2100, 2500 } }[level];
                case ArcadeMode.Streak: return new[] { new[] { 2, 4, 7 }, new[] { 2, 4, 6 }, new[] { 1, 3, 6 } }[level];
                case ArcadeMode.ServeTargets: return new[] { new[] { 1700, 3100, 4750 }, new[] { 1600, 3000, 4450 }, new[] { 1550, 2850, 4300 } }[level];
                case ArcadeMode.WallRally: return new[] { new[] { 300, 975, 1650 }, new[] { 250, 600, 1450 }, new[] { 150, 450, 1350 } }[level];
            }
            return new[] { 0, 0, 0 };
        }

        static Challenge Drill(DrillKind kind, string name, string blurb, int balls, double seconds, int[] stars)
        {
            return new Challenge
            {
                Id = "drill-" + kind.ToString().ToLowerInvariant(), Name = name, Description = blurb,
                Mode = ArcadeMode.Drill, Drill = kind, Level = 0, Seed = (uint)(5000 + (int)kind * 13),
                Balls = balls, Seconds = seconds, RingRadius = 0.5, FeedSpeed = 23, FeedSpeedSpread = 2,
                FeedBothSides = true, ZoneScale = 1, Stars = stars
            };
        }

        static List<Challenge> _all;

        /// <summary>The 18 arcade challenges (by mode, then level) and the 5 drills.</summary>
        public static List<Challenge> All()
        {
            if (_all != null) return _all;
            _all = new List<Challenge>();
            for (int m = 0; m < 6; m++) for (int l = 0; l < 3; l++) _all.Add(Make((ArcadeMode)m, l));
            _all.Add(Drill(DrillKind.Length, "Length", "Drive it deep: the first bounce behind the service box.", 20, 0, new[] { 7, 12, 15 }));
            _all.Add(Drill(DrillKind.Drop, "Drop", "Soft and short: the first bounce in front of the short line (aim low, no charge).", 20, 0, new[] { 7, 10, 13 }));
            _all.Add(Drill(DrillKind.BoastDrive, "Boast-Drive", "Boast, then drive, then boast... as the feeder alternates.", 20, 0, new[] { 8, 13, 16 }));
            _all.Add(Drill(DrillKind.Ghosting, "Ghosting", "No ball: touch the lit spots and come back to the T. 45 seconds.", 0, 45, new[] { 16, 22, 26 }));          // 2026-10-25: 24 was below the bot's median
            _all.Add(Drill(DrillKind.NickHunt, "Nick Hunt", "Kill it into the join of the wall and the floor.", 20, 0, new[] { 1, 3, 4 }));          // with near-nicks (ArcadeSession.NearNick)
            return _all;
        }

        public static Challenge Find(string id)
        {
            foreach (var c in All()) if (c.Id == id) return c;
            return null;
        }

        public static Challenge Get(ArcadeMode mode, int level) { return Find(mode.ToString().ToLowerInvariant() + "-" + Challenge.LevelNames[level].ToLowerInvariant()); }
    }
}
