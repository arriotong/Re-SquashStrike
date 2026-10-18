// ===========================================================
// Tools/MatchBench/ArcadeBench.cs
// P2-M2: the human-style ArcadeBot plays every Target Arcade challenge
// and drill many times (different seeds): score spread per challenge,
// for the star thresholds (1 star ~ median, 2 ~ top quarter, 3 above
// the best). --arcade [--games N] [--only id]
// ===========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SquashSim;
using SquashSim.Arcade;

static class ArcadeBench
{
    public static void Run(int plays, string only)
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.UseGameProfile();
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        Console.WriteLine("ARCADE  human-style bot, " + plays + " plays each   (score: median / p75 / max   stars now: 1 / 2 / 3)");
        foreach (var c in Challenges.All())
        {
            if (only != null && c.Id != only) continue;
            var scores = new List<int>();
            int hits = 0, attempts = 0, great = 0;
            double secs = 0;
            for (int g = 0; g < plays; g++)
            {
                var s = new ArcadeSession(c, (uint)(9000 + g * 31));
                var bot = new ArcadeBot((uint)(100 + g));
                var intent = new InputIntent();
                int guard = 0;
                while (!s.Over && guard++ < 240 * 400) { bot.Fill(intent, s, dt); s.Step(intent, dt); }
                scores.Add(s.Score); hits += s.Hits; attempts += s.Attempts; great += s.GreatHits; secs += s.Time;
            }
            scores.Sort();
            int med = scores[scores.Count / 2], p75 = scores[(int)(scores.Count * 0.75)], max = scores[scores.Count - 1];
            Console.WriteLine("  " + c.Id.PadRight(24) + (med + " / " + p75 + " / " + max).PadLeft(20) +
                "   hit " + (100.0 * hits / Math.Max(1, attempts)).ToString("0") + "% of " + (attempts / (double)plays).ToString("0") +
                ", great " + (100.0 * great / Math.Max(1, hits)).ToString("0") + "%, " + (secs / plays).ToString("0") + " s" +
                "   [" + string.Join(" / ", c.Stars) + "]");
        }
    }
}
