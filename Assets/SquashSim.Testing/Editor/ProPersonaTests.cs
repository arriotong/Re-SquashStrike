// ===========================================================
// Assets/SquashSim.Testing/Editor/ProPersonaTests.cs
// 2026-11-22 the top 10 play like themselves (ProPersonas): each has their
// own game and class; the grinder plays length, the attacker kills, the
// shot-maker drops (MatchBench --pros, 10 games each v a retriever: s07
// 79% straight 1% kill; s09 51% kill; s05 43% drop).
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class ProPersonaTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheTopTenEachHaveTheirOwnGame()
        {
            for (int r = 1; r <= 10; r++)
            {
                string id = "s" + r.ToString("00");
                var p = ProPersonas.For(id);
                Assert.That(p, Is.Not.Null, id);
                Assert.That(ProPersonas.For("star:" + id), Is.SameAs(p), "by star id too");
                Assert.That(p.Bias.Length, Is.EqualTo(6));
                Assert.That(p.Summary, Is.Not.Empty);
                Assert.That(PlayerClasses.Find(p.Class), Is.Not.Null, id + "'s class");
                Assert.That(AiProfile.For("star:" + id, 0.8).Persona, Is.SameAs(p), "the AI plays it");
            }
            Assert.That(ProPersonas.For("s11"), Is.Null, "the rest: their style alone");
            Assert.That(AiProfile.For("club:retriever:62", 0.5).Persona, Is.Null);
        }

        static Dictionary<string, int> Mix(string id, int games, out int total)
        {
            var count = new Dictionary<string, int>(); total = 0;
            for (int g = 0; g < games; g++)
            {
                var s = new ReplaySetup { Opponent = "retriever", Difficulty = 0.8f, Scoring = "par", GamesToWin = 1, BotSeed = (uint)(900 + g * 13), Lets = true };
                ReplayMatch.Configure(s);
                var m = ReplayMatch.Build(s);
                var brain = new OpponentBrain(AiProfile.For(id, 0.85), (uint)(77 + g));
                m.Bots[0] = brain;
                for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                int seen = 0;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 900)
                {
                    m.Step(dt);
                    if (brain.Choices != seen) { seen = brain.Choices; string sh = brain.ArcadeShot ?? "?"; count[sh] = count.TryGetValue(sh, out int n) ? n + 1 : 1; total++; }
                }
                PhysicsConfig.ResetToDefaults();
            }
            return count;
        }

        static double Share(Dictionary<string, int> c, string shot, int total) { return total == 0 ? 0 : (c.TryGetValue(shot, out int n) ? n : 0) / (double)total; }

        [Test] public void TheGrinderPlaysLengthAndTheAttackerKills()
        {
            var grind = Mix("star:s07", 3, out int gt);
            var attack = Mix("star:s09", 3, out int at);
            UnityEngine.Debug.Log("[Pros] s07 straight " + Share(grind, "straight", gt).ToString("0.00") + " kill " + Share(grind, "kill", gt).ToString("0.00") +
                                  "   s09 straight " + Share(attack, "straight", at).ToString("0.00") + " kill " + Share(attack, "kill", at).ToString("0.00"));
            Assert.That(Share(grind, "straight", gt), Is.GreaterThan(Share(attack, "straight", at) + 0.25), "the grinder: length");
            Assert.That(Share(attack, "kill", at), Is.GreaterThan(Share(grind, "kill", gt) + 0.25), "the attacker: kills");
        }
    }
}
