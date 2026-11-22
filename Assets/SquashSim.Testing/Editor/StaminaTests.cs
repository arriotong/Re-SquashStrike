// ===========================================================
// Assets/SquashSim.Testing/Editor/StaminaTests.cs
// P7 phase 1: stamina in the game (every mode, both players):
//   - fresh: no effect; tired below TiredBelow: slower, wider
//   - running drains; 2026-11-22 evening: off the T drains, standing on the
//     T recovers (no flat rally drain); between rallies they recover
//     faster; a new game is fresh; tired, shots lose pace
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
            // 2026-11-22 evening: where you stand - a back corner drains, the T gives breath back (standing still, ten seconds)
            a.Pos.Set(2.6, 0, 8.8); a.Speed = 0; b.Pos.Set(0, 0, SquashBook.TZ); b.Speed = 0;
            a.Stamina = 0.8; b.Stamina = 0.8;
            Fatigue.Update(a, true, 10.0); Fatigue.Update(b, true, 10.0);
            Assert.That(a.Stamina, Is.LessThan(0.8), "off the T: tiring");
            Assert.That(b.Stamina, Is.GreaterThan(0.8), "on the T: getting your breath back");
            // running the same, the one off the T tires more
            a.Stamina = b.Stamina = 0.9; a.Speed = b.Speed = PhysicsConfig.Current.Player.MaxSpeed; b.Pos.Set(0.6, 0, SquashBook.TZ + 0.5);
            Fatigue.Update(a, true, 3.0); Fatigue.Update(b, true, 3.0);
            Assert.That(a.Stamina, Is.LessThan(b.Stamina - 0.03), "running off the T costs more than running round it");
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

        [Test] public void TiredShotsLosePace()
        {
            Game();
            var f = PhysicsConfig.Current.Fatigue;
            Assert.That(f.TiredPaceLoss, Is.GreaterThan(0.1), "noticeable");
            Assert.That(f.ArcadeSpeedLoss, Is.EqualTo(0.15).Within(1e-9), "15% slower at empty");
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
