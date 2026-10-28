// ===========================================================
// Assets/SquashSim.Testing/Editor/ChaosTests.cs
// The Gauntlet chaos remake (2026-10-28): the cards that bend the rules
// do what they say, in the sim, and a match with them replays exactly.
//   - the ultimate: a full meter and a charged shot; Time Stop freezes
//     the opponent (no move, no stroke); Inferno's fireballs fly faster
//   - Curveball bends the ball, Dead Drop kills the bounce
//   - Shield and Rewind turn lost rallies into lets
//   - the points stack: one shot can win a game to 7
//   - without the cards nothing changes (RunModsTests: bit-identical)
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class ChaosTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static ReplaySetup Demo(uint seed, params (string id, int lv)[] mods0)
        {
            var s = new ReplaySetup { Opponent0 = "attacker", Difficulty0 = 0.7f, Opponent = "retriever", Difficulty = 0.6f, GamesToWin = 1, Scoring = "par", BotSeed = seed, ParPoints = 7 };
            s.Mods0 = new List<RunMod>();
            foreach (var (id, lv) in mods0) s.Mods0.Add(new RunMod { Id = id, Level = lv });
            return s;
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

        [Test] public void TheUltimateNeedsAFullMeterAndAChargedShot()
        {
            var m = new RunMods(); m.Set("freeze", 1);
            Assert.That(m.HasUltimate, Is.True);
            m.Power = 0.5; m.AtStroke(1.0, false);
            Assert.That(m.UltNow, Is.False, "not full");
            m.Power = 1; m.AtStroke(0.3, false);
            Assert.That(m.UltNow, Is.False, "not charged");
            m.AtStroke(RunMods.UltCharge, false);
            Assert.That(m.UltNow, Is.True); Assert.That(m.Power, Is.EqualTo(0));
            var none = new RunMods(); none.Set("magnet", 3);
            none.MeterShot(); none.MeterWin();
            Assert.That(none.Power, Is.EqualTo(0), "no ultimate card: no meter");
            var inferno = new RunMods(); inferno.Set("inferno", 1); inferno.Power = 1;
            inferno.AtStroke(0.9, false);
            Assert.That(inferno.FireShots, Is.EqualTo(RunMods.FireShotsPerUlt)); Assert.That(inferno.ShotFire, Is.True, "the ultimate shot is the first fireball");
        }

        [Test] public void OneShotCanWinAGameToSeven()
        {
            var m = new RunMods(); m.Set("jackpot", 3); m.Set("combo", 3); m.Set("wallride", 2); m.Set("inferno", 1);
            int b = m.ChaosBonus(true, true, true, false, 2, 4, false, 3);
            Assert.That(1 + b, Is.GreaterThanOrEqualTo(7), "a jackpot fireball off two walls on a combo of 3: " + (1 + b) + " points");
            var allin = new RunMods(); allin.Set("allin", 1);
            Assert.That(allin.ChaosBonus(true, true, false, false, 0, 1, false, 2), Is.EqualTo(4 * 3), "All In: the jackpot times the combo");
            Assert.That(new RunMods().ChaosBonus(true, true, true, true, 3, 20, true, 9), Is.EqualTo(2), "a fireball is only a fireball with Inferno's shots - without cards only that");
        }

        [Test] public void TimeStopFreezesTheOpponent()
        {
            int freezes = 0, strokesWhileFrozen = 0;
            var (played, _) = Play(Demo(61, ("freeze", 3), ("overclock", 5)), 900, sim =>
            {
                var op = sim.Players[1].Player.Mods;
                if (op == null || !op.Frozen) return;
                foreach (var e in sim.StepEvents) if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 1) strokesWhileFrozen++;
                Assert.That(op.MoveScale, Is.EqualTo(0));
            });
            foreach (var n in played.Chaos) if (n.Kind == "freeze") freezes++;
            Assert.That(freezes, Is.GreaterThan(0), "the ultimate fired and froze them");
            Assert.That(strokesWhileFrozen, Is.EqualTo(0), "frozen: no stroke");
        }

        [Test] public void InfernosFireballsFlyFaster()
        {
            double fire = 0, plain = 0; int nf = 0, np = 0;
            var s = Demo(71, ("inferno", 1), ("overclock", 5), ("meteor", 0));
            Play(s, 900, sim =>
            {
                foreach (var e in sim.StepEvents)
                    if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 0 && (e.Technique == "drive" || e.Technique == "kill"))
                    {
                        bool isFire = sim.Players[0].Player.Mods.ShotFire;
                        if (isFire) { fire += e.BallSpeedOut; nf++; } else { plain += e.BallSpeedOut; np++; }
                    }
            });
            Assert.That(nf, Is.GreaterThan(0), "fireballs were struck");
            Assert.That(fire / nf, Is.GreaterThan(plain / np * 1.12), "fireballs faster: " + (plain / np).ToString("0.0") + " -> " + (fire / nf).ToString("0.0"));
        }

        [Test] public void CurveballBendsAndDeadDropDies()
        {
            int curves = 0, bent = 0, deads = 0;
            double vxAt = 0, tAt = -1;
            Play(Demo(83, ("curve", 5), ("deaddrop", 5)), 900, sim =>
            {
                foreach (var e in sim.StepEvents) if (e.Type == SimEventType.RacketContact || (e.Type == SimEventType.Impact && e.SurfaceClass != "frontWall")) tAt = -1;
                if (tAt >= 0 && sim.Time - tAt >= 0.25)
                {
                    if (System.Math.Abs(sim.Ball.Vel.X) > System.Math.Abs(vxAt) + 0.5) bent++;
                    tAt = -1;
                }
                for (int k = sim.Chaos.Count - 1; k >= 0 && sim.Chaos[k].Time == sim.Time; k--)
                {
                    if (sim.Chaos[k].Kind == "curve") { curves++; vxAt = sim.Ball.Vel.X; tAt = sim.Time; }
                    if (sim.Chaos[k].Kind == "deaddrop") deads++;
                }
            });
            Assert.That(curves, Is.GreaterThan(0), "curved shots");
            Assert.That(bent, Is.GreaterThan(0), "and the ball bent: wider and faster sideways");
            Assert.That(deads, Is.GreaterThan(0), "dead drops");
            var m = new RunMods(); m.Set("deaddrop", 5);
            Assert.That(m.DeadDropKeep, Is.EqualTo(0.25).Within(1e-12));
        }

        [Test] public void ShieldAndRewindReplayTheRally()
        {
            var (sim, _) = Play(Demo(97, ("shield", 3), ("rewind", 2)), 1800);
            var reasons = sim.History.ConvertAll(o => o.Reason ?? "");
            Assert.That(reasons.Exists(r => r.StartsWith("Shield") || r.StartsWith("Rewind")), Is.True, "a lost rally played again");
            int perGame = sim.History.FindAll(o => (o.Reason ?? "").StartsWith("Rewind")).Count;
            Assert.That(perGame, Is.LessThanOrEqualTo(2), "Rewind 2: twice a game at most");
        }

        [Test] public void AChaosMatchReplaysExactly()
        {
            var s = Demo(101, ("freeze", 2), ("inferno", 1), ("jackpot", 3), ("combo", 2), ("curve", 3), ("ricochet", 3), ("deaddrop", 2), ("slowfield", 3), ("haste", 2),
                           ("heavyball", 2), ("knuckle", 2), ("quake", 2), ("magnet", 2), ("shield", 1), ("rewind", 1), ("wallride", 2), ("bounty", 3), ("firstblood", 1), ("overclock", 3));
            s.Mods1 = new List<RunMod> { new RunMod { Id = "curve", Level = 3 }, new RunMod { Id = "deaddrop", Level = 3 } };
            var (sim, file) = Play(s);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(sim.Chaos.Count, Is.GreaterThan(10), "the cards were busy");
            var v = ReplaySim.Verify(ReplayFile.FromBytes(file.ToBytes()));
            Assert.That(v.Ok, Is.True, v.ToString());
        }
    }
}
