// ===========================================================
// Assets/SquashSim.Testing/Editor/FreePlayTests.cs
// The warm-up court's free play: the ball is in your hand and goes
// where you go; a press drops and hits it to the front wall; a missed
// ball comes back to your hand. The lab's own step (SimParity) is not
// touched by any of it.
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Lab;

namespace SquashSim.Tests
{
    public class FreePlayTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static LabSimulation Lab()
        {
            var sim = new LabSimulation();
            sim.Player.WallAimEnabled = true;
            sim.Player.ArcadeHitEnabled = true;
            return sim;
        }

        [Test] public void TheBallIsInYourHandAndGoesWithYou()
        {
            var sim = Lab(); var f = new FreePlay(); f.Begin(sim);
            var it = new InputIntent();
            double x0 = sim.Ball.Pos.X;
            Assert.That(f.InHand, Is.True);
            for (int i = 0; i < 240; i++) { it.Move.X = 1; it.Move.Z = 0; it.MoveMagnitude = 1; f.Step(sim, it, Dt); }
            Assert.That(f.InHand, Is.True, "walking does not drop it");
            Assert.That(sim.Ball.Pos.X - x0, Is.GreaterThan(1.0), "the ball went with you");
            Assert.That(sim.Ball.Asleep, Is.True);
            Assert.That(System.Math.Abs(sim.Ball.Pos.X - sim.Player.Player.Pos.X), Is.LessThan(0.6), "in your hand");
        }

        [Test] public void APressDropsAndHitsItToTheFrontWallThenAMissComesBack()
        {
            var sim = Lab(); var f = new FreePlay(); f.Begin(sim);
            var it = new InputIntent { HasAimPoint = true, AimPointX = 0.3, AimPointY = 2.6 };
            for (int i = 0; i < 10; i++) f.Step(sim, it, Dt);
            it.Hit.Pressed = true; it.Hit.Held = true;
            f.Step(sim, it, Dt);
            for (int i = 0; i < 20; i++) { it.Hit.Held = true; f.Step(sim, it, Dt); }
            it.Hit.Held = false; it.Hit.Released = true;
            f.Step(sim, it, Dt);
            Assert.That(f.InHand, Is.False, "dropped and struck");
            Assert.That(sim.LastRacketContact, Is.Not.Null);
            bool back = false;
            for (int i = 0; i < 240 * 10 && !back; i++) { f.Step(sim, it, Dt); back = f.InHand; }
            UnityEngine.Debug.Log("[FreePlay] wall hits " + f.WallHits + ", the ball died: " + f.LastDead);
            Assert.That(f.WallHits, Is.EqualTo(1), "it reached the front wall");
            Assert.That(back, Is.True, "nobody hit it back: back in your hand");
            Assert.That(f.LastDead, Is.EqualTo("Not up"));
            Assert.That(f.BestStreak, Is.EqualTo(1));
            Assert.That(f.Streak, Is.EqualTo(0));
        }

        [Test] public void FreePlayIsDeterministic()
        {
            string Run()
            {
                var sim = Lab(); var f = new FreePlay(); f.Begin(sim);
                var it = new InputIntent { HasAimPoint = true, AimPointX = -0.6, AimPointY = 3.0 };
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 240 * 6; i++)
                {
                    it.Move.X = i % 400 < 200 ? 0.5 : -0.5; it.MoveMagnitude = 0.6;
                    if (i % 480 == 40) { it.Hit.Pressed = true; it.Hit.Held = true; }
                    else it.Hit.Held = i % 480 > 40 && i % 480 < 70;
                    f.Step(sim, it, Dt);
                    if (i % 60 == 0) sb.Append(sim.Ball.Pos.X.ToString("R")).Append(sim.Ball.Pos.Z.ToString("R")).Append(f.InHand);
                }
                return sb.ToString() + f.WallHits;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
