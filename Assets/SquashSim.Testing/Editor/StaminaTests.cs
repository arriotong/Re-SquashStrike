// ===========================================================
// Assets/SquashSim.Testing/Editor/StaminaTests.cs
// P7 phase 1: stamina in the game (every mode, both players):
//   - fresh: no effect; tired below TiredBelow: slower, wider
//   - a live rally drains both players, running or not; between rallies
//     they recover; a new game is fresh
//   - a charged stroke costs breath; the Stamina attribute scales it
//   - the lab never tires (its parity traces stay as they were)
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class StaminaTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }

        [Test] public void TirednessStartsBelowTheLine()
        {
            Game();
            var f = PhysicsConfig.Current.Fatigue;
            var p = new TestPlayer();
            p.Stamina = 1.0; Assert.That(Fatigue.Tiredness(p), Is.EqualTo(0));
            p.Stamina = f.TiredBelow + 0.01; Assert.That(Fatigue.Tiredness(p), Is.EqualTo(0), "just above: fresh enough");
            p.Stamina = (f.TiredBelow + f.Floor) / 2; Assert.That(Fatigue.Tiredness(p), Is.EqualTo(0.5).Within(1e-9));
            p.Stamina = f.Floor; Assert.That(Fatigue.Tiredness(p), Is.EqualTo(1));
            PhysicsConfig.ResetToDefaults();
            p.Stamina = 0.3; Assert.That(Fatigue.Tiredness(p), Is.EqualTo(0), "the lab never tires");
        }

        [Test] public void ATiredAthleteIsSlower()
        {
            Game();
            double Top(double stamina)
            {
                var s = new ReplaySetup { Opponent = "retriever", Difficulty = 0.3f, GamesToWin = 1, Scoring = "par", BotSeed = 3 };
                ReplayMatch.Configure(s);
                var sim = ReplayMatch.Build(s);
                var c = sim.Players[0];
                c.Player.Stamina = stamina; c.Player.ArcadeMovement = true;
                var intent = new InputIntent { MoveMagnitude = 1 }; intent.Move.X = 1; intent.Move.Z = 0;
                double dt = PhysicsConfig.Current.Sim.FixedDt, top = 0;
                for (int i = 0; i < 480; i++) { PlayerMovement.Step(c.Player, intent, c.Assist, dt); top = System.Math.Max(top, c.Player.Speed); c.Player.Pos.X = 0; c.Player.Stamina = stamina; }
                return top;
            }
            double fresh = Top(1.0), tired = Top(PhysicsConfig.Current.Fatigue.Floor);
            Assert.That(tired / fresh, Is.EqualTo(1 - PhysicsConfig.Current.Fatigue.ArcadeSpeedLoss).Within(0.01), "fully tired: " + (tired / fresh).ToString("0.000"));
            Assert.That(Top(0.7), Is.EqualTo(fresh).Within(1e-9), "above the line: no slower");
        }

        [Test] public void ALiveRallyDrainsBothAndANewGameIsFresh()
        {
            Game();
            var s = new ReplaySetup { Opponent = "retriever", Difficulty = 0.3f, GamesToWin = 2, Scoring = "par", BotSeed = 9 };
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var a = sim.Players[0].Player; var b = sim.Players[1].Player;
            Fatigue.Update(a, true, 10.0); Fatigue.Update(b, true, 10.0);      // ten seconds of rally, standing still
            Assert.That(a.Stamina, Is.LessThan(1.0), "the rally itself tires");
            Assert.That(a.Stamina, Is.EqualTo(b.Stamina).Within(1e-12), "standing still: the same for both");
            double after = a.Stamina;
            Fatigue.Update(a, false, 2.0);
            Assert.That(a.Stamina, Is.GreaterThan(after), "between rallies: back");
            a.Stamina = 0.3; b.Stamina = 0.4;
            // play out a game, then the next starts fresh
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            sim.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.5), 4);
            sim.Players[0].WallAimEnabled = true; sim.Players[0].ArcadeHitEnabled = true;
            while (sim.Phase != MatchPhase.GameOver && sim.Time < 900) sim.Step(dt);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.GameOver));
            sim.NextGame();
            Assert.That(a.Stamina, Is.EqualTo(1.0)); Assert.That(b.Stamina, Is.EqualTo(1.0));
        }

        [Test] public void AChargedStrokeCostsBreathAndStaminaScalesIt()
        {
            Game();
            var p = new TestPlayer();
            Fatigue.OnCharge(p, 1.0);
            double cost = 1.0 - p.Stamina;
            Assert.That(cost, Is.EqualTo(PhysicsConfig.Current.Fatigue.ChargeCost).Within(1e-12));
            var fit = new TestPlayer(); fit.Attributes.Stamina = AthleteAttributes.Max;
            Fatigue.OnCharge(fit, 1.0);
            Assert.That(1.0 - fit.Stamina, Is.LessThan(cost), "a fitter athlete tires less");
        }
    }
}
