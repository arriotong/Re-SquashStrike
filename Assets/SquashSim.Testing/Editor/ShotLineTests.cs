// ===========================================================
// Assets/SquashSim.Testing/Editor/ShotLineTests.cs
// Shot lines: a person picks the shot and straight or cross; the line
// comes from the shot. Drives from all over the court, aimed anywhere on
// a half of the front wall, flown through the real ball physics: a
// straight drive comes back down its own wall and deep, a cross goes to
// the far back corner - not to the middle. (Logged against the raw aim.)
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using static SquashSim.CourtDimensions;

namespace SquashSim.Tests
{
    public class ShotLineTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>The line a person's drive plays from (bx, 0.7, bz) aimed at tx.</summary>
        static double Line(double bx, double bz, double tx, double ty, double speed)
        {
            var ball = new BallState();
            ball.Reset(new V3(bx, 0.7, bz), new V3(0, 0, 0), null, 40);
            return new ArcadeStroke(3).LineFor(ball, CourtSurfaces.Create(), "drive", tx, ty, speed);
        }

        /// <summary>Strike from (bx, 0.7, bz) to the front wall at (tx, ty): where it first bounces.</summary>
        static V3 Bounce(double bx, double bz, double tx, double ty, double speed)
        {
            var surfaces = CourtSurfaces.Create();
            var ball = new BallState();
            ball.Reset(new V3(bx, 0.7, bz), new V3(0, 0, 0), null, 40);
            var s = new ArcadeStroke(3);
            s.Solve(ball, surfaces, tx, ty, 0, 0, speed);
            ball.Vel.CopyFrom(s.LaunchVelocity);
            ball.DipOffFrontWall = PhysicsConfig.Current.ArcadeHit.FrontWallRiseKeep < 1;   // a drive leaves the front wall heading down
            BallPhysics.Wake(ball);
            var evs = new List<SimEvent>();
            bool front = false;
            for (int i = 0; i < 240 * 3; i++)
            {
                evs.Clear();
                BallPhysics.Step(ball, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
                foreach (var e in evs)
                {
                    if (e.Type != SimEventType.Impact) continue;
                    if (e.SurfaceClass == "frontWall") front = true;
                    if (e.SurfaceClass == "floor") return front ? e.Point.Clone() : null;
                }
            }
            return null;
        }

        static readonly double[] Xs = { -2.5, -1.4, 1.4, 2.5 }, Zs = { 5.5, 7.0, 8.4 };

        [Test] public void StraightDrivesComeBackDownTheirWall()
        {
            double ty = 1.9, speed = PhysicsConfig.Current.ArcadeHit.DriveSpeed;
            int n = 0, good = 0, rawGood = 0;
            foreach (var bx in Xs) foreach (var bz in Zs)
            {
                int side = bx > 0 ? 1 : -1;
                for (double a = 0.1; a <= 3.0; a += 0.35)                         // anywhere on the ball's half
                {
                    double tx = side * a;
                    var line = Bounce(bx, bz, Line(bx, bz, tx, ty, speed), ty, speed);
                    var raw = Bounce(bx, bz, tx, ty, speed);
                    n++;
                    if (line != null && line.X * side > HALF_W - 1.1 && line.Z > 5.0) good++;
                    if (raw != null && raw.X * side > HALF_W - 1.1 && raw.Z > 5.0) rawGood++;
                }
            }
            UnityEngine.Debug.Log("[Lines] straight drives down the wall: " + good + " / " + n + " (the raw aim: " + rawGood + " / " + n + ")");
            Assert.That(good, Is.GreaterThanOrEqualTo(n * 0.9), "a straight drive is a straight drive, wherever on your half you aimed");
        }

        [Test] public void CrossDrivesGoToTheFarCorner()
        {
            double ty = 1.9, speed = PhysicsConfig.Current.ArcadeHit.DriveSpeed;
            int n = 0, good = 0, rawGood = 0;
            foreach (var bx in Xs) foreach (var bz in Zs)
            {
                int side = bx > 0 ? 1 : -1;
                for (double a = 0.1; a <= 3.0; a += 0.35)
                {
                    double tx = -side * a;                                        // anywhere on the other half
                    var line = Bounce(bx, bz, Line(bx, bz, tx, ty, speed), ty, speed);
                    var raw = Bounce(bx, bz, tx, ty, speed);
                    n++;
                    if (line != null && line.X * side < -1.2 && line.Z > 4.5) good++;
                    else if (a < 0.2) UnityEngine.Debug.Log("[Lines] cross miss from (" + bx + ", " + bz + "): " + (line == null ? "no bounce" : "(" + line.X.ToString("0.00") + ", " + line.Z.ToString("0.00") + ")"));
                    if (raw != null && raw.X * side < -1.2 && raw.Z > 4.5) rawGood++;
                }
            }
            UnityEngine.Debug.Log("[Lines] cross drives to the far side: " + good + " / " + n + " (the raw aim: " + rawGood + " / " + n + ")");
            Assert.That(good, Is.GreaterThanOrEqualTo(n * 0.9));
        }

        [Test] public void TheHalfOfTheWallPicksTheLine()
        {
            double s1 = ArcadeStroke.ShotLine(-2.4, 8, "drive", -0.2), s2 = ArcadeStroke.ShotLine(-2.4, 8, "drive", -2.9);
            Assert.That(s1, Is.EqualTo(s2), "same half: the same straight line");
            Assert.That(ArcadeStroke.ShotLine(-2.4, 8, "drive", 0.3), Is.GreaterThan(0), "across: the cross line");
            Assert.That(ArcadeStroke.ShotLine(0.1, 6, "drive", -1), Is.LessThan(0), "from the middle: the corner you aimed at");
        }
    }
}
