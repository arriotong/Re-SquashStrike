// ===========================================================
// Tools/MatchBench/StaminaBench.cs
// 2026-11-22 evening, stamina by where you play: per player, the share of
// rally time on the T, the stamina at the end of rallies, the lowest, and
// how much of the time they were tired.
//   --stamina-t [--games N]
// ===========================================================

using System;
using SquashSim;

static class StaminaBench
{
    public static void Run(int games)
    {
        foreach (var (a, b) in new[] { ("attacker", "retriever"), ("grinder", "shotmaker") })
        {
            double[] onT = new double[2], live = new double[2], endSum = new double[2], low = { 1, 1 }, tired = new double[2];
            int ends = 0, won0 = 0;
            for (int g = 0; g < games; g++)
            {
                PhysicsConfig.UseGameProfile();
                var c = PhysicsConfig.Current.Match; c.WarmUpEnabled = false; c.GamesToWin = 1;
                var m = new MatchSimulation(0);
                m.Bots[0] = new OpponentBrain(AiProfile.ByName(a).WithDifficulty(0.75), (uint)(700 + g));
                m.Bots[1] = new OpponentBrain(AiProfile.ByName(b).WithDifficulty(0.75), (uint)(800 + g));
                for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                var f = PhysicsConfig.Current.Fatigue;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
                {
                    int before = m.History.Count;
                    m.Step(dt);
                    bool rally = m.Phase == MatchPhase.Rally && m.Referee.Outcome == null;
                    for (int i = 0; i < 2; i++)
                    {
                        var p = m.Players[i].Player;
                        if (rally)
                        {
                            live[i] += dt;
                            if (DMath.Hypot(p.Pos.X, p.Pos.Z - SquashBook.TZ) < 1.3) onT[i] += dt;
                            if (p.Stamina < f.TiredBelow) tired[i] += dt;
                        }
                        low[i] = Math.Min(low[i], p.Stamina);
                    }
                    if (m.History.Count > before) { ends++; for (int i = 0; i < 2; i++) endSum[i] += m.Players[i].Player.Stamina; }
                    if (m.Phase == MatchPhase.GameOver) m.NextGame();
                }
                if (m.Score.MatchWinner == 0) won0++;
            }
            Console.WriteLine(a + " v " + b + "  (" + games + " games, " + a + " won " + won0 + ")");
            for (int i = 0; i < 2; i++)
                Console.WriteLine("  " + (i == 0 ? a : b).PadRight(10) + " on the T " + (100 * onT[i] / Math.Max(1e-9, live[i])).ToString("0").PadLeft(3) + "%   stamina at rally end " +
                                  (endSum[i] / Math.Max(1, ends)).ToString("0.00") + "   lowest " + low[i].ToString("0.00") + "   tired " + (100 * tired[i] / Math.Max(1e-9, live[i])).ToString("0") + "% of rally time");
        }
    }
}
