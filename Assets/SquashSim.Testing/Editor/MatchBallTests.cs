// ===========================================================
// Assets/SquashSim.Testing/Editor/MatchBallTests.cs
// The game's match ball (PhysicsConfig.UseMatchBall): a double yellow
// at PSA pace. Warming it buys bounce height, not pace off the walls;
// the lab defaults are untouched (SimParity holds them to the JS lab).
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class MatchBallTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>Rebound height as a fraction of a 100-inch drop onto the floor, at a held temperature.</summary>
        static double DropRebound(double temperature)
        {
            var surfaces = CourtSurfaces.Create();
            var evs = new List<SimEvent>();
            var b = new BallState();
            b.Reset(new V3(0, 2.54, 5), new V3(0, 0, 0), null, temperature);
            BallPhysics.Wake(b);
            double peak = 0; bool bounced = false;
            for (int i = 0; i < 240 * 4; i++)
            {
                evs.Clear();
                BallPhysics.Step(b, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
                b.Temperature = temperature;
                if (evs.Count > 0) { if (bounced) break; bounced = true; }
                if (bounced) peak = System.Math.Max(peak, b.Pos.Y - PhysicsConfig.Current.Ball.Radius);
            }
            return peak / (2.54 - PhysicsConfig.Current.Ball.Radius);
        }

        [Test] public void LabDefaultsHaveNoMatchBallTuning()
        {
            Assert.That(PhysicsConfig.Current.Temperature.HotSpeedFade, Is.Null, "the lab's ball must stay the lab's");
        }

        [Test] public void DropReboundIsNearTheDoubleYellowSpec()
        {
            PhysicsConfig.UseGameProfile();
            // WSF double yellow: 12-14% at 23 C, 26-33% at 45 C. A little lively when cold, a little
            // dead when hot is fine; it must never bounce like a single dot when hot.
            Assert.That(DropRebound(23), Is.InRange(0.11, 0.17));
            Assert.That(DropRebound(45), Is.InRange(0.22, 0.33));
            Assert.That(DropRebound(45), Is.GreaterThan(DropRebound(23) * 1.4), "warming still adds bounce height");
        }

        [Test] public void AHotBallStillDiesWhenHitHard()
        {
            PhysicsConfig.UseGameProfile();
            double cold = BallPhysics.EffectiveRestitution("frontWall", 23, 30);
            double hot = BallPhysics.EffectiveRestitution("frontWall", 45, 30);
            double hotSoft = BallPhysics.EffectiveRestitution("floor", 45, 7) / BallPhysics.EffectiveRestitution("floor", 23, 7);
            Assert.That(hot / cold, Is.LessThan(1.12), "a hot ball gains little pace off the wall at 30 m/s");
            Assert.That(hotSoft, Is.GreaterThan(1.2), "but bounces clearly higher on soft impacts");

            PhysicsConfig.ResetToDefaults();
            double labHot = BallPhysics.EffectiveRestitution("frontWall", 45, 30) / BallPhysics.EffectiveRestitution("frontWall", 23, 30);
            Assert.That(labHot, Is.GreaterThan(hot / cold), "the match ball is deader when hot than the lab ball");
        }

        [Test] public void HardDriveDiesAtTheBackOnAHotBall()
        {
            PhysicsConfig.UseGameProfile();
            var surfaces = CourtSurfaces.Create();
            var evs = new List<SimEvent>();
            // A 40 m/s straight length drive from the back, 0.9 m up the front wall, hot ball.
            // (Driven 1.6 m up at that pace it still rises off the wall and hits the glass on the
            // full - a loose drive that sits up, as it would in real squash.)
            double z0 = 7.5, y0 = 0.6, S = 40, t = z0 / S;
            double vy = (0.9 - y0) / t + 0.5 * 9.81 * t;
            var b = new BallState();
            b.Reset(new V3(2.2, y0, z0), new V3(0, vy, -System.Math.Sqrt(S * S - vy * vy)), null, 45);
            BallPhysics.Wake(b);
            double backWallHeight = -1, secondBounceZ = -1;
            int floors = 0;
            for (int i = 0; i < 240 * 6 && floors < 2; i++)
            {
                evs.Clear();
                BallPhysics.Step(b, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
                foreach (var e in evs)
                {
                    if (e.SurfaceClass == "backWall") backWallHeight = e.Point.Y;
                    if (e.SurfaceClass == "floor" && ++floors == 2) secondBounceZ = e.Point.Z;
                }
            }
            Assert.That(secondBounceZ, Is.GreaterThan(6.5), "a hard length drive dies in the back third, not mid-court");
            Assert.That(backWallHeight, Is.LessThan(1.0), "and if it reaches the glass it is low on it");
        }
    }
}
