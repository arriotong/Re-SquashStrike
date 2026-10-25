// ===========================================================
// Assets/SquashSim.Testing/Editor/RunModsTests.cs
// P5 the Gauntlet roguelite's sim hooks (RunMods):
//   - no abilities, or abilities that do nothing, play bit-identically
//   - a game to 7 ends at 7 (or 2 clear past it)
//   - an elite's Cannon makes the AI's drives faster; Pressure drains
//     the opponent; Nick Hunter keeps kills lower; bonus points count
//   - a match with abilities records (format 5) and replays exactly
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class RunModsTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static ReplaySetup Demo(uint seed = 11)
        {
            return new ReplaySetup { Opponent0 = "attacker", Difficulty0 = 0.6f, Opponent = "retriever", Difficulty = 0.6f, GamesToWin = 1, Scoring = "par", BotSeed = seed };
        }

        static (MatchSimulation sim, ReplayFile file) Play(ReplaySetup s, double maxSeconds = 1800, System.Action<MatchSimulation> eachStep = null)
        {
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var rec = new MatchRecorder(sim, s, new InputIntent[2], "test", 0);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (!rec.Finished && sim.Time < maxSeconds) { rec.BeforeStep(); sim.Step(dt); rec.AfterStep(); eachStep?.Invoke(sim); }
            rec.Finish(true);
            return (sim, rec.File);
        }

        static List<ulong> Hashes(ReplayFile f) { return f.Rallies.ConvertAll(r => r.Hash); }

        [Test] public void AbilitiesThatDoNothingChangeNothing()
        {
            var plain = Play(Demo(), 240);
            var s = Demo();
            s.Mods0 = new List<RunMod> { new RunMod { Id = "not-an-ability", Level = 3 } };     // the hooks run, at exactly 1
            s.Mods1 = new List<RunMod> { new RunMod { Id = "also-nothing", Level = 1 } };
            var odd = Play(s, 240);
            Assert.That(odd.sim.Players[0].Player.Mods, Is.Not.Null, "the hooks are live");
            Assert.That(Hashes(odd.file), Is.EqualTo(Hashes(plain.file)), "bit-identical rallies");
            Assert.That(odd.sim.StepCount, Is.EqualTo(plain.sim.StepCount));
        }

        [Test] public void AGameToSevenEndsAtSeven()
        {
            var s = Demo(23); s.ParPoints = 7;
            var (sim, _) = Play(s);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            int w = sim.Score.MatchWinner, a = sim.Score.Points[w], b = sim.Score.Points[1 - w];
            Assert.That(a, Is.GreaterThanOrEqualTo(7));
            Assert.That(a == 7 || a - b == 2, Is.True, "to 7, win by 2: " + a + "-" + b);
            Assert.That(a, Is.LessThan(11).Or.EqualTo(b + 2), "not the usual 11");
        }

        [Test] public void AnElitesCannonDrivesHarder()
        {
            double Mean(ReplaySetup s)
            {
                double sum = 0; int n = 0;
                Play(s, 300, m => { foreach (var e in m.StepEvents) if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 1 && (e.Technique == "drive" || e.Technique == "kill")) { sum += e.BallSpeedOut; n++; } });
                return n > 0 ? sum / n : 0;
            }
            double plain = Mean(Demo(37));
            var s = Demo(37); s.Mods1 = new List<RunMod> { new RunMod { Id = "e_cannon", Level = 1 } };
            double cannon = Mean(s);
            Assert.That(cannon, Is.GreaterThan(plain * 1.08), "drives and kills faster: " + plain.ToString("0.0") + " -> " + cannon.ToString("0.0"));
        }

        [Test] public void PressureDrainsTheOpponent()
        {
            PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile();
            var a = new TestPlayer(); var b = new TestPlayer();
            b.OpponentDrain = 1.5;
            a.Speed = b.Speed = PhysicsConfig.Current.Player.MaxSpeed;
            for (int i = 0; i < 240; i++) { Fatigue.Update(a, true, 1.0 / 240); Fatigue.Update(b, true, 1.0 / 240); }
            Assert.That(1 - b.Stamina, Is.EqualTo((1 - a.Stamina) * 1.5).Within(1e-9), "half as much again");
        }

        [Test] public void BonusPointsCount()
        {
            var sc = new MatchScore(ScoringSystem.Par, 0, 1, true);
            sc.Apply(new RallyOutcome { Kind = RallyEnd.NotUp, Winner = 0, Loser = 1 }, 1);
            Assert.That(sc.Points[0], Is.EqualTo(2), "an ace with Ace Machine: two points");
            var en = new MatchScore(ScoringSystem.English, 0, 1, true);
            en.Apply(new RallyOutcome { Kind = RallyEnd.NotUp, Winner = 1, Loser = 0 }, 1);
            Assert.That(en.Points[1], Is.EqualTo(0), "English: winning hand-out scores nothing, bonus or not");
            var m = new RunMods(); m.Set("nickbank", 5); m.Set("exec", 1); m.Set("ace", 1);
            Assert.That(m.NickBonus, Is.EqualTo(3)); Assert.That(m.AceBonus, Is.EqualTo(1));
        }

        [Test] public void NickHunterKillsLowerAndWider()
        {
            var m = new RunMods(); m.Set("nick", 5);
            Assert.That(m.KillHeight, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(m.KillWiden, Is.EqualTo(0.4).Within(1e-12));
            Assert.That(m.Pace("kill", false, false, 0), Is.EqualTo(1.10).Within(1e-12));
            Assert.That(new RunMods().Pace("kill", false, false, 0), Is.EqualTo(1.0), "nothing at level 0");
        }

        [Test] public void AMatchWithAbilitiesReplaysExactly()
        {
            var s = Demo(51); s.ParPoints = 7;
            s.Mods0 = new List<RunMod> { new RunMod { Id = "nick", Level = 3 }, new RunMod { Id = "rail", Level = 2 }, new RunMod { Id = "pressure", Level = 4 }, new RunMod { Id = "ace", Level = 2 }, new RunMod { Id = "second", Level = 1 }, new RunMod { Id = "momentum", Level = 3 }, new RunMod { Id = "matador", Level = 1 } };
            s.Mods1 = new List<RunMod> { new RunMod { Id = "e_tireless", Level = 1 } };
            s.Attr1 = new AthleteAttributes { Speed = 7, Stamina = 6, Power = 7, Control = 6 };
            var (sim, file) = Play(s);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            var back = ReplayFile.FromBytes(file.ToBytes());
            Assert.That(back.Setup.ParPoints, Is.EqualTo(7));
            Assert.That(back.Setup.Mods0.Count, Is.EqualTo(7));
            Assert.That(back.Setup.Attr1.Speed, Is.EqualTo(7));
            var v = ReplaySim.Verify(back);
            Assert.That(v.Ok, Is.True, v.ToString());
        }
    }
}
