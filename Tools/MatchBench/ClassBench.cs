// ===========================================================
// Tools/MatchBench/ClassBench.cs
// 2026-11-22 the player classes, balanced: the same player (an AI stand-in)
// in each class against the same opponent - win rate, points won, shots a
// rally. A fair set has every class close to the All-rounder.
//
//   --classes [--games N] [--opp retriever] [--d1 0.75]
// ===========================================================

using System;
using SquashSim;
using SquashSim.Replays;

static class ClassBench
{
    public static void Run(int games, string opp, double d1)
    {
        Console.WriteLine("CLASSES  " + games + " games each, an attacker (0.75) in each class v " + opp + " (" + d1 + ")");
        foreach (var c in PlayerClasses.All)
        {
            int won = 0, pts = 0, ptsAll = 0, shots = 0, rallies = 0;
            for (int g = 0; g < games; g++)
            {
                var s = new ReplaySetup
                {
                    Opponent = opp, Difficulty = (float)d1, Scoring = "par", GamesToWin = 1, BotSeed = (uint)(500 + g * 7), Lets = true,
                    Opponent0 = "attacker", Difficulty0 = 0.75f,
                };
                s.Attr0 = PlayerClasses.Attributes(s.Attr0, c.Id);
                s.Mods0 = PlayerClasses.Mods(null, c.Id);
                ReplayMatch.Configure(s);
                var m = ReplayMatch.Build(s);
                Program.ArcadeAi(m);
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 1800) m.Step(dt);
                if (m.Score.MatchWinner == 0) won++;
                foreach (var o in m.History) { if (o.Winner < 0) continue; ptsAll++; if (o.Winner == 0) pts++; }
                foreach (var n in m.ShotsPerRally) { shots += n; rallies++; }
                PhysicsConfig.ResetToDefaults();
            }
            Console.WriteLine("  " + c.Name.PadRight(12) + " won " + (100.0 * won / games).ToString("0").PadLeft(3) + "%   points " +
                              (100.0 * pts / Math.Max(1, ptsAll)).ToString("0.0").PadLeft(5) + "%   shots a rally " + ((double)shots / Math.Max(1, rallies)).ToString("0.0"));
        }
    }
}
