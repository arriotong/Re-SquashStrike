// ===========================================================
// Tools/MatchBench/ProBench.cs
// 2026-11-22 the top 10's own games (ProPersonas): each against the same
// opponent - the shots they choose (share of each), the rally length,
// points won. The personas should read differently: the grinder all
// length, the shot-makers' drops, the attackers' kills.
//
//   --pros [--games N] [--opp retriever] [--d1 0.8]
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;
using SquashSim.Replays;

static class ProBench
{
    static readonly string[] Shots = { "straight", "cross", "lob", "drop", "kill", "boast" };

    public static void Run(int games, string opp, double d1)
    {
        Console.WriteLine("PROS  " + games + " games each v " + opp + " (" + d1 + ")   shot shares: straight cross lob drop kill boast");
        for (int r = 1; r <= 10; r++)
        {
            string id = "star:s" + r.ToString("00");
            var count = new Dictionary<string, int>();
            int pts = 0, ptsAll = 0, shots = 0, rallies = 0, choices = 0;
            for (int g = 0; g < games; g++)
            {
                var s = new ReplaySetup { Opponent = opp, Difficulty = (float)d1, Scoring = "par", GamesToWin = 1, BotSeed = (uint)(900 + g * 13), Lets = true };
                ReplayMatch.Configure(s);
                var m = ReplayMatch.Build(s);
                // the pro plays as athlete 0 (the profile as Quick Match makes it)
                var star = SquashSim.Career.Stars.Find(id);
                m.Players[0].Player.Attributes.CopyFrom(star.Attributes);
                m.Players[0].Player.Mods = RunMods.From(star.Signature);
                var brain = new OpponentBrain(AiProfile.For(id, 0.85), (uint)(77 + g));
                m.Bots[0] = brain;
                Program.ArcadeAi(m);
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                int seen = 0;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
                {
                    m.Step(dt);
                    if (brain.Choices != seen) { seen = brain.Choices; string sh = brain.ArcadeShot ?? "?"; count[sh] = count.TryGetValue(sh, out int n) ? n + 1 : 1; choices++; }
                }
                foreach (var o in m.History) { if (o.Winner < 0) continue; ptsAll++; if (o.Winner == 0) pts++; }
                foreach (var n in m.ShotsPerRally) { shots += n; rallies++; }
                PhysicsConfig.ResetToDefaults();
            }
            var parts = new List<string>();
            foreach (var sh in Shots) parts.Add((100.0 * (count.TryGetValue(sh, out int n) ? n : 0) / Math.Max(1, choices)).ToString("0").PadLeft(3));
            Console.WriteLine("  " + id.PadRight(9) + string.Join("  ", parts) + "    rally " + ((double)shots / Math.Max(1, rallies)).ToString("0.0").PadLeft(5) +
                              "   points " + (100.0 * pts / Math.Max(1, ptsAll)).ToString("0").PadLeft(3) + "%");
        }
    }
}
