// ===========================================================
// Tools/MatchBench/GearBench.cs
// P7 phase 5: does career gear change how you play, not just how well?
// The human-style model, at Quick Match levels (5s), wears one item at a
// time (the rest the starters) against a ~50% club player; per item:
// rallies won, the model's winners (the ball not got up) and errors (tin,
// out) per 100 rallies, the mean rally and stamina at its end.
//
//   --gear [--games N]
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;
using SquashSim.Career;
using SquashSim.Replays;

static class GearBench
{
    public static void Run(int games)
    {
        Console.WriteLine("GEAR  the human-style model at level 5 in every stat, one item on, v club retriever 70 - " + games + " games each");
        Console.WriteLine("  item                 stats                      rallies won   winners/100  errors/100   rally s   stamina");
        var loadouts = new List<GearItem> { null };
        foreach (var g in Gear.All) if (g.Price > 0) loadouts.Add(g);
        foreach (var g in loadouts)
        {
            var attr = Gear.Apply(new AthleteAttributes(), g != null ? new[] { g } : new GearItem[0]);
            int won = 0, total = 0, winners = 0, errors = 0;
            double secs = 0, sta = 0; int n = 0;
            for (int k = 0; k < games; k++)
            {
                var s = new ReplaySetup { Opponent = Stars.ClubId("retriever", 70), Difficulty = 0.15f, GamesToWin = 1, Scoring = "par", BotSeed = (uint)(6000 + 97 * k), Lets = true, Attr0 = attr };
                ReplayMatch.DressStar(s);
                ReplayMatch.Configure(s);
                var m = ReplayMatch.Build(s);
                m.Bots[0] = new HumanStyle((uint)(6000 + 97 * k) * 2654435761u + 3) { Arcade = true };
                m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
                Program.ArcadeAi(m);
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 1800) m.Step(dt);
                foreach (var r in m.Stats)
                {
                    var o = r.Outcome;
                    if (o == null || o.Winner < 0) continue;
                    total++; if (o.Winner == 0) won++;
                    if (o.Winner == 0 && o.Kind == RallyEnd.NotUp) winners++;
                    if (o.Loser == 0 && (o.Kind == RallyEnd.Down || o.Kind == RallyEnd.Out)) errors++;
                    secs += r.Seconds; sta += r.Stamina0; n++;
                }
                PhysicsConfig.ResetToDefaults();
            }
            Console.WriteLine("  " + (g != null ? g.Name : "(starters)").PadRight(20) + " " + (g != null ? g.Line : "NEUTRAL").PadRight(26) +
                              (100.0 * won / Math.Max(1, total)).ToString("0.0").PadLeft(8) + "%" + (100.0 * winners / Math.Max(1, total)).ToString("0.0").PadLeft(13) +
                              (100.0 * errors / Math.Max(1, total)).ToString("0.0").PadLeft(12) + (secs / Math.Max(1, n)).ToString("0.0").PadLeft(10) + (sta / Math.Max(1, n)).ToString("0.00").PadLeft(10));
        }
    }
}
