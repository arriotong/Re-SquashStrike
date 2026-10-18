// ===========================================================
// Assets/SquashSim.Testing/Editor/ArcadeBoastTests.cs
// The boast of the simple hitting system (hold E / pad RB): aimed like a
// drive at a front-wall point, it goes off the side wall nearer the ball
// first and then lands on that point - through the real ball physics.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class ArcadeBoastTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>Fly a launch: the first wall it meets, and where it meets the front wall.</summary>
        static string Flight(V3 from, V3 vel, out V3 front)
        {
            var surfaces = CourtSurfaces.Create();
            var evs = new List<SimEvent>();
            var b = new BallState();
            b.Reset(from, vel, null, 30);
            BallPhysics.Wake(b);
            string first = null; front = null;
            for (int i = 0; i < 240 * 2 && front == null; i++)
            {
                evs.Clear();
                BallPhysics.Step(b, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
                foreach (var e in evs)
                {
                    if (e.Type != SimEventType.Impact) continue;
                    if (first == null) first = e.SurfaceId ?? e.SurfaceClass;
                    if (e.SurfaceClass == "frontWall" && front == null) front = new V3(e.Point.X, e.Point.Y, e.Point.Z);
                }
            }
            return first;
        }

        static void Boast(double bx, double by, double bz, double tx, double ty, double speed)
        {
            var surfaces = CourtSurfaces.Create();
            var ball = new BallState();
            ball.Reset(new V3(bx, by, bz), new V3(0, 0, 0), null, 30);
            var s = new ArcadeStroke(7);
            int side = ArcadeStroke.BoastSide(bx, tx);
            double pace = speed;
            Assert.That(s.SolveBoast(ball, surfaces, tx, ty, side, ref pace), Is.True, "a boast from here should solve");
            var v = s.LaunchVelocity;
            string first = Flight(ball.Pos, new V3(v.X, v.Y, v.Z), out V3 front);
            Assert.That(front, Is.Not.Null, "it reaches the front wall");
            double err = System.Math.Sqrt((front.X - tx) * (front.X - tx) + (front.Y - ty) * (front.Y - ty));
            UnityEngine.Debug.Log("[Boast] from (" + bx + ", " + bz + ") to (" + tx + ", " + ty + "): off " + first +
                                  ", front wall at (" + front.X.ToString("0.00") + ", " + front.Y.ToString("0.00") + "), error " + err.ToString("0.00") + " m, pace " + speed.ToString("0.0") + " -> " + pace.ToString("0.0") + " m/s");
            Assert.That(first, Is.EqualTo(side > 0 ? "sideWallRight" : "sideWallLeft"), "off the side wall first");
            Assert.That(err, Is.LessThan(0.3), "and onto the aim point");
        }

        static double Pace { get { var c = PhysicsConfig.Current.ArcadeHit; return c.DriveSpeed * c.BoastPaceScale; } }

        [Test] public void FromTheBackCornerItLandsOnTheAim() { Boast(2.6, 0.7, 8.0, -1.5, 1.6, Pace); }
        [Test] public void FromTheMiddleItComesAcross() { Boast(0.1, 0.8, 6.5, 1.8, 1.4, Pace); }
        [Test] public void ALowTrickleBoast() { Boast(-2.4, 0.5, 4.0, 1.2, CourtDimensions.TIN_H + 0.17, 9.0); }

        [Test] public void SideChoice()
        {
            Assert.That(ArcadeStroke.BoastSide(2.5, -1), Is.EqualTo(1), "the wall nearer the ball");
            Assert.That(ArcadeStroke.BoastSide(-2.5, -1), Is.EqualTo(-1));
            Assert.That(ArcadeStroke.BoastSide(0.1, 2), Is.EqualTo(-1), "from the middle: away from the target");
        }
    }
}
