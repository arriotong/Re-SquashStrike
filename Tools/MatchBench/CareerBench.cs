// ===========================================================
// Tools/MatchBench/CareerBench.cs
// P2-M4: calibration for the Career's background results.
//
//   --career-calibrate [--games N]
//       AI vs AI (the game's simple hitting system) across a grid of
//       strengths, every personality pairing; prints the share of
//       rallies the stronger side wins at each strength gap and the
//       fitted logistic slope K for RallyModel:
//           p(A wins a rally) = 1 / (1 + exp(-K * (sA - sB)))
//       and, per personality, its rally share against the others at
//       equal strength (RallyModel's matchup table).
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;

public static class CareerBench
{
    static readonly string[] Names = { "retriever", "attacker", "shotmaker", "grinder" };

    /// <summary>Rallies won by side 0 / rallies decided, over games of AI a (d0) vs AI b (d1).</summary>
    static void Play(string a, double d0, string b, double d1, int games, uint seed, ref int won, ref int total)
    {
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(g % 2);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName(a).WithDifficulty(d0), seed + (uint)(g * 2));
            m.Bots[1] = new OpponentBrain(AiProfile.ByName(b).WithDifficulty(d1), seed + (uint)(g * 2 + 1));
            Program.ArcadeAi(m);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (m.Phase != MatchPhase.GameOver && m.Time < 1800) m.Step(dt);
            foreach (var o in m.History)
            {
                if (o.Winner < 0) continue;
                total++;
                if (o.Winner == 0) won++;
            }
        }
        PhysicsConfig.ResetToDefaults();
    }

    public static void Run(int games)
    {
        double[] levels = { 0.0, 0.15, 0.30, 0.45, 0.60 };
        var gapWon = new SortedDictionary<double, int>();
        var gapTotal = new SortedDictionary<double, int>();
        Console.WriteLine("CAREER CALIBRATION  AI vs AI, " + games + " games per pairing, every personality pairing rotated");
        int pair = 0;
        for (int i = 0; i < levels.Length; i++)
            for (int j = i + 1; j < levels.Length; j++)
            {
                int won = 0, total = 0;
                for (int k = 0; k < Names.Length; k++)
                {
                    string strong = Names[k], weak = Names[(k + 1 + pair) % Names.Length];
                    // the stronger side alternates sides of the sim
                    int w = 0, t = 0;
                    Play(strong, levels[j], weak, levels[i], games, (uint)(9000 + 97 * pair + 13 * k), ref w, ref t);
                    won += w; total += t;
                }
                pair++;
                double gap = Math.Round(levels[j] - levels[i], 2);
                gapWon[gap] = (gapWon.TryGetValue(gap, out int a) ? a : 0) + won;
                gapTotal[gap] = (gapTotal.TryGetValue(gap, out int b) ? b : 0) + total;
                Console.WriteLine("  " + levels[j].ToString("0.00") + " vs " + levels[i].ToString("0.00") + "   stronger wins " + (100.0 * won / Math.Max(1, total)).ToString("0.0").PadLeft(5) + "% of " + total + " rallies");
            }

        // fit K: least squares on the logit, weighted by rallies, through the origin
        double num = 0, den = 0;
        Console.WriteLine("  by gap:");
        foreach (var kv in gapTotal)
        {
            double p = (double)gapWon[kv.Key] / Math.Max(1, kv.Value);
            p = Math.Min(0.97, Math.Max(0.03, p));
            double logit = Math.Log(p / (1 - p));
            num += kv.Value * kv.Key * logit;
            den += kv.Value * kv.Key * kv.Key;
            Console.WriteLine("    gap " + kv.Key.ToString("0.00") + "   " + (100 * p).ToString("0.0").PadLeft(5) + "%   (" + kv.Value + " rallies)");
        }
        double K = num / den;
        Console.WriteLine("  fitted K = " + K.ToString("0.000"));
        foreach (var kv in gapTotal)
            Console.WriteLine("    gap " + kv.Key.ToString("0.00") + "   model " + (100 / (1 + Math.Exp(-K * kv.Key))).ToString("0.0").PadLeft(5) + "%");

        // matchups at equal strength (0.30)
        Console.WriteLine("  matchups at 0.30 (row's share of rallies vs column):");
        Console.WriteLine("    " + "".PadRight(10) + string.Concat(Array.ConvertAll(Names, n => n.PadLeft(11))));
        for (int r = 0; r < Names.Length; r++)
        {
            string line = "    " + Names[r].PadRight(10);
            for (int c = 0; c < Names.Length; c++)
            {
                if (r == c) { line += "-".PadLeft(11); continue; }
                int w = 0, t = 0;
                Play(Names[r], 0.30, Names[c], 0.30, games, (uint)(20000 + 31 * r + 7 * c), ref w, ref t);
                line += ((100.0 * w / Math.Max(1, t)).ToString("0.0") + "%").PadLeft(11);
            }
            Console.WriteLine(line);
        }
    }
}
