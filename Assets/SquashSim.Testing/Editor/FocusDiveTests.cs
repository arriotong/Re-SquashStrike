// ===========================================================
// Assets/SquashSim.Testing/Editor/FocusDiveTests.cs
// 2026-11-22 evening (the user: "make the ai opponent players go slower
// ... they need to use focus or dive when ball is too far away ... both of
// them in space bar, when you hold you get into focus mode ... if your slow
// mo ends you release and then your character dives"; answers: a smart
// dive - only with the ball out of reach; an empty meter ends the slow
// motion, no dive; a dive costs Special meter):
//   - the computer runs AiRunScale of its speed
//   - Focus let go with the ball out of reach: a dive, paid from the meter
//     (no stamina); in reach: no dive; no meter: no dive
//   - the computer dives for balls it can't run down (and pays the same)
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class FocusDiveTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }
        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static MatchSimulation Match(int seed, bool special = true)
        {
            var c = PhysicsConfig.Current.Match;
            c.WarmUpEnabled = false;
            c.GamesToWin = 3;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), (uint)(31 + seed));
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.8), (uint)(32 + seed));
            for (int i = 0; i < 2; i++)
            {
                m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true;
                if (special) m.Players[i].Player.Mods = RunMods.From(new[] { new RunMod { Id = "special", Level = 1 } });
            }
            return m;
        }

        static double TopSpeed(bool ai)
        {
            var p = new TestPlayer();
            p.AiPaced = ai; p.ArcadeMovement = true;
            var it = new InputIntent();
            it.Move.X = 1; it.MoveMagnitude = 1;
            var assist = new MovementAssist();
            double top = 0;
            for (int k = 0; k < 120; k++) { PlayerMovement.Step(p, it, assist, Dt); top = System.Math.Max(top, DMath.Hypot(p.Vel.X, p.Vel.Z)); if (p.Pos.X > 2.5) { p.Pos.X = -2.5; } }
            return top;
        }

        [Test] public void TheComputerRunsSlowerThanAPerson()
        {
            double person = TopSpeed(false), ai = TopSpeed(true);
            Assert.That(ai / person, Is.EqualTo(PlayerMovement.AiRunScale).Within(0.02), "the computer's top speed");
            var m = Match(0);
            m.Bots[0] = null;                                                 // a person on 0
            m.Step(Dt);
            Assert.That(m.Players[0].Player.AiPaced, Is.False, "a person runs flat out");
            Assert.That(m.Players[1].Player.AiPaced, Is.True, "the computer is paced");
        }

        /// <summary>Play AI v AI until the ball comes to player 0 with <paramref name="reachable"/> as asked; then hand 0 to a person.</summary>
        static MatchSimulation MomentWhen(bool reachable, out bool found)
        {
            found = false;
            for (int seed = 0; seed < 12 && !found; seed++)
            {
                var m = Match(seed);
                m.Players[0].Player.Mods.Power = 1;
                while (m.Time < 400 && m.Phase != MatchPhase.MatchOver)
                {
                    m.Step(Dt);
                    if (m.Phase == MatchPhase.GameOver) m.NextGame();
                    if (!m.BallComing(0) || !m.Players[0].Arcade.CanDive || m.Ball.FloorSinceFrontWall > 0) continue;
                    if (!reachable)
                    {
                        // (2026-11-22 evening: the AI keeps the ball from the T now - out of reach is made: player 0 put a dive's
                        // length behind the ball's path, on the far side)
                        var pp = m.Players[0].Player; var bb = m.Ball.Pos;
                        pp.Pos.Set(bb.X >= 0 ? -CourtDimensions.HALF_W + 0.5 : CourtDimensions.HALF_W - 0.5, 0, System.Math.Min(CourtDimensions.LENGTH - 0.5, bb.Z + 1.0));
                        pp.Vel.Set(0, 0, 0);
                    }
                    if (m.Reachable(0) != reachable) continue;
                    m.Bots[0] = null;
                    m.Intents[0] = new InputIntent();
                    m.Players[0].Player.Mods.Power = 1;
                    found = true;
                    return m;
                }
            }
            return null;
        }

        [Test] public void FocusLetGoWithTheBallOutOfReachDivesAndCostsMeter()
        {
            var m = MomentWhen(false, out bool found);
            Assert.That(found, Is.True, "a ball out of reach came");
            var mm = m.Players[0].Player.Mods;
            double stamina = m.Players[0].Player.Stamina;
            int dives = m.Players[0].Arcade.DiveCount;
            m.Intents[0].FocusHeld = true; m.Step(Dt);
            Assert.That(m.Focusing(0), Is.True, "holding: focus");
            // held until a moment the ball is out of reach (as the match judges it at the let-go)
            for (int k = 0; k < 60 && (m.Reachable(0) || !m.BallComing(0)); k++) m.Step(Dt);
            Assume.That(m.BallComing(0) && !m.Reachable(0), "still out of reach");
            double before = mm.Power;
            m.Intents[0].FocusHeld = false; m.Step(Dt);
            Assert.That(m.Players[0].Arcade.DiveCount, Is.EqualTo(dives + 1), "let go: a dive");
            Assert.That(m.Players[0].Arcade.Diving, Is.True);
            Assert.That(mm.Power, Is.EqualTo(before - MatchSimulation.DiveMeterCost).Within(1e-9), "paid from the meter");
            Assert.That(m.Players[0].Player.Stamina, Is.GreaterThanOrEqualTo(stamina - 0.02), "not from stamina");
        }

        [Test] public void FocusLetGoWithTheBallInReachDoesNotDive()
        {
            var m = MomentWhen(true, out bool found);
            Assert.That(found, Is.True, "a ball in reach came");
            int dives = m.Players[0].Arcade.DiveCount;
            m.Intents[0].FocusHeld = true; m.Step(Dt);
            m.Intents[0].FocusHeld = false; m.Step(Dt);
            Assert.That(m.Players[0].Arcade.DiveCount, Is.EqualTo(dives), "in reach: no dive, just out of the slow motion");
            Assert.That(m.Focusing(0), Is.False);
            Assert.That(m.DivesDeclined, Is.GreaterThan(0));
        }

        [Test] public void NoMeterNoDive()
        {
            var m = MomentWhen(false, out bool found);
            Assert.That(found, Is.True);
            var mm = m.Players[0].Player.Mods;
            int dives = m.Players[0].Arcade.DiveCount;
            m.Intents[0].FocusHeld = true; m.Step(Dt);
            mm.Power = MatchSimulation.DiveMeterCost * 0.5;                    // (the slow motion ran it nearly dry)
            m.Intents[0].FocusHeld = false; m.Step(Dt);
            Assert.That(m.Players[0].Arcade.DiveCount, Is.EqualTo(dives), "can't pay: no dive");
        }

        [Test] public void TheComputerDivesForBallsItCannotRunDown()
        {
            int chosen = 0, dives = 0, smart = 0;
            for (int seed = 0; seed < 3; seed++)
            {
                var m = Match(seed);
                while (m.Phase != MatchPhase.MatchOver && m.Time < 900) { m.Step(Dt); if (m.Phase == MatchPhase.GameOver) m.NextGame(); }
                for (int i = 0; i < 2; i++) { chosen += ((OpponentBrain)m.Bots[i]).DivesChosen; dives += m.Players[i].Arcade.DiveCount; }
                smart += m.SmartDives;
            }
            UnityEngine.Debug.Log("[AiDive] chosen " + chosen + ", smart dives " + smart + ", dives " + dives);
            Assert.That(smart, Is.GreaterThan(0), "the computer dives");
            Assert.That(dives, Is.EqualTo(smart), "every dive a smart one (no free dives)");
        }
    }
}
