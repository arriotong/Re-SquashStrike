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
//
//   --season [--seasons N]
//       a career where "you" are the human-style model (the one the
//       difficulty levels are balanced against), playing every one of
//       your matches as a real sim match against the pro's personality
//       and strength; each month you enter the best event open to you.
//       Prints each event, the ranking as it moves, and your rating.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;
using SquashSim.Career;

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

    public static void Season(int seasons)
    {
        var c = CareerSession.New("Bench", "ENG", 2026, gamesToWin: 2);
        int game = 0;
        Console.WriteLine("CAREER SEASON  the human-style model plays its matches (best of 3, PAR) - " + seasons + " season(s)");
        SeasonRunner.PlayMatch real = (CareerSession cs, Pro opp, out string score, out int won, out int total) =>
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            var cfg = PhysicsConfig.Current.Match;
            cfg.GamesToWin = cs.S.GamesToWin; cfg.Scoring = cs.S.English ? "english" : "par"; cfg.BetweenGamesSeconds = 1.0;
            var m = new MatchSimulation(0);
            m.Bots[0] = new HumanStyle((uint)(700 + game)) { Arcade = true };
            m.Bots[1] = new OpponentBrain(AiProfile.ByName(opp.Style).WithDifficulty(opp.Strength), (uint)(900 + game));
            m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
            Program.ArcadeAi(m);
            game++;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var games = new List<string>();
            while (m.Phase != MatchPhase.MatchOver && m.Time < 3600)
            {
                var before = m.Phase;
                m.Step(dt);
                if (m.Phase == MatchPhase.GameOver && before != MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver && before != MatchPhase.MatchOver)
                    games.Add(m.Score.Points[0] + "-" + m.Score.Points[1]);
            }
            won = 0; total = 0;
            foreach (var o in m.History) { if (o.Winner < 0) continue; total++; if (o.Winner == 0) won++; }
            score = string.Join(" ", games);
            PhysicsConfig.ResetToDefaults();
            return m.Score.MatchWinner == 0;
        };
        SeasonRunner.Months(c, seasons * Tour.SlotsPerSeason, real, (cs, e) =>
        {
            string what = e == null || !e.YouIn ? "skipped" : e.Name + " (" + Tour.Info((Tier)e.Tier).Name + "): " + CareerSession.ReachedLong(e.YouReached) + "  +" + e.YouPoints;
            int month = (cs.S.Month + Tour.SlotsPerSeason - 1) % Tour.SlotsPerSeason;
            Console.WriteLine("  " + Tour.Month(month) + "  " + what.PadRight(58) + " rank " + cs.RankOf(CareerSession.You).ToString().PadLeft(3) + "   rating " + cs.S.Rating.ToString("0.000"));
        });
        int w = 0, l = 0;
        foreach (var mr in c.S.Matches) if (mr.Won) w++; else l++;
        Console.WriteLine("  matches " + w + "-" + l + ", titles " + c.S.Titles + ", final rank " + c.RankOf(CareerSession.You) + ", rating " + c.S.Rating.ToString("0.000"));
    }
}
