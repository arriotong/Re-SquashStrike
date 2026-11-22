// ===========================================================
// Tools/MatchBench/SpecialBench.cs
// 2026-11-22 evening, the Special Shot remade (a duel): how often it comes
// back - no Focus (a small chance), Focus with the duel press (the AI's
// timing), and the duels won / lost.
//   --special [--games N]
// ===========================================================

using System;
using SquashSim;

static class SpecialBench
{
    public static void Run(int games)
    {
        foreach (bool defend in new[] { false, true })
            foreach (double skill in defend ? new[] { 0.3, 0.9 } : new[] { 0.8 })
            {
                int fired = 0, returned = 0, wonPt = 0;
                int duelsWon = 0, duelsLost = 0;
                for (int g = 0; g < games; g++)
                {
                    PhysicsConfig.UseGameProfile();
                    var c = PhysicsConfig.Current.Match; c.WarmUpEnabled = false; c.GamesToWin = 1;
                    var m = new MatchSimulation(0);
                    m.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), (uint)(400 + g));
                    m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(skill), (uint)(500 + g));
                    for (int i = 0; i < 2; i++)
                    {
                        m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true;
                        m.Players[i].Player.Mods = RunMods.From(new[] { new RunMod { Id = "special", Level = 1 } });
                    }
                    double dt = PhysicsConfig.Current.Sim.FixedDt;
                    int state = 0, contactsAt = 0, seen = 0;
                    while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
                    {
                        var a = m.Players[0].Player.Mods; var b = m.Players[1].Player.Mods;
                        if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 2 && m.ZoneBy < 0 && m.ChargeBy < 0 && m.WindupBy < 0 && m.SpecialBy < 0 && state == 0) a.Power = 1;
                        if (m.ZoneBy < 0 && m.ChargeBy < 0 && m.SpecialBy < 0) b.Power = defend ? 0.6 : 0;
                        int before = m.History.Count;
                        m.Step(dt);
                        if (m.SpecialsFired > seen) { seen = m.SpecialsFired; fired++; state = 1; contactsAt = m.RallyContacts[1]; }
                        if (state == 1 && m.RallyContacts[1] > contactsAt) { returned++; state = 2; }
                        if (m.History.Count > before)
                        {
                            if (state == 1 && m.History[m.History.Count - 1].Winner == 0) wonPt++;
                            state = 0;
                        }
                        if (m.Phase == MatchPhase.GameOver) m.NextGame();
                    }
                    duelsWon += m.SpecialsDefended; duelsLost += m.DuelsLost;
                }
                Console.WriteLine((defend ? "focus+duel, defender skill " + skill : "no focus") + ": fired " + fired + ", returned " + returned +
                                  " (" + (100.0 * returned / Math.Max(1, fired)).ToString("0") + "%), won outright " + wonPt +
                                  "   duels won " + duelsWon + " lost " + duelsLost);
            }
    }
}
