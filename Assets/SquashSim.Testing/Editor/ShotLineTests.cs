// ===========================================================
// Assets/SquashSim.Testing/Editor/ShotLineTests.cs
// Shot lines: a person picks the shot and straight or cross; the line
// comes from the shot. Straight or cross is whichever line the aim is
// nearer (2026-10-20: not the half of the wall - a cross from the front
// was read as a drive). Drives from all over the court, flown through the
// real ball physics: a straight drive comes back down its own wall and
// deep, a cross goes to the far back corner. The shot is pulled only part
// of the way onto its line: most of it Assisted, a little without.
// (Logged against the raw aim.)
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
                for (double a = 0.1; a <= 3.0; a += 0.35)                         // the ball's half, wherever it reads straight
                {
                    double tx = side * a;
                    if (ArcadeStroke.IsCross(bx, bz, "drive", tx)) continue;
                    var line = Bounce(bx, bz, Line(bx, bz, tx, ty, speed), ty, speed);
                    var raw = Bounce(bx, bz, tx, ty, speed);
                    n++;
                    if (line != null && line.X * side > HALF_W - 1.1 && line.Z > 5.0) good++;
                    if (raw != null && raw.X * side > HALF_W - 1.1 && raw.Z > 5.0) rawGood++;
                }
            }
            UnityEngine.Debug.Log("[Lines] straight drives down the wall: " + good + " / " + n + " (the raw aim: " + rawGood + " / " + n + ")");
            Assert.That(n, Is.GreaterThan(60));
            Assert.That(good, Is.GreaterThanOrEqualTo(n * 0.9), "a straight drive's line comes down its own wall");
        }

        [Test] public void CrossDrivesGoToTheFarCorner()
        {
            double ty = 1.9, speed = PhysicsConfig.Current.ArcadeHit.DriveSpeed;
            int n = 0, good = 0, rawGood = 0;
            foreach (var bx in Xs) foreach (var bz in Zs)
            {
                int side = bx > 0 ? 1 : -1;
                for (double tx = -3.0; tx <= 3.0; tx += 0.35)                    // the whole wall, wherever it reads cross
                {
                    if (!ArcadeStroke.IsCross(bx, bz, "drive", tx)) continue;
                    var line = Bounce(bx, bz, Line(bx, bz, tx, ty, speed), ty, speed);
                    var raw = Bounce(bx, bz, tx, ty, speed);
                    n++;
                    if (line != null && line.X * side < -1.2 && line.Z > 4.5) good++;
                    if (raw != null && raw.X * side < -1.2 && raw.Z > 4.5) rawGood++;
                }
            }
            UnityEngine.Debug.Log("[Lines] cross drives to the far side: " + good + " / " + n + " (the raw aim: " + rawGood + " / " + n + ")");
            Assert.That(n, Is.GreaterThan(60));
            Assert.That(good, Is.GreaterThanOrEqualTo(n * 0.9));
        }

        [Test] public void TheNearerLinePicksStraightOrCross()
        {
            // From the front, right of centre: the cross meets the front wall on the ball's own half...
            double cross = ArcadeStroke.ShotLine(2.0, 4.0, "drive", -3.0), straight = ArcadeStroke.ShotLine(2.0, 4.0, "drive", 2.9);
            Assert.That(cross, Is.GreaterThan(0), "a cross from the front is aimed on your own half");
            // ...and aiming there is still a cross (it used to be read as a straight drive)
            Assert.That(ArcadeStroke.IsCross(2.0, 4.0, "drive", cross), Is.True);
            Assert.That(ArcadeStroke.ShotLine(2.0, 4.0, "drive", cross), Is.EqualTo(cross).Within(1e-9));
            Assert.That(ArcadeStroke.IsCross(2.0, 4.0, "drive", 2.9), Is.False, "by the wall: straight");
            Assert.That(straight, Is.GreaterThan(cross));
            // the split is halfway between the two lines
            double mid = (cross + straight) / 2;
            Assert.That(ArcadeStroke.IsCross(2.0, 4.0, "drive", mid - 0.05), Is.True);
            Assert.That(ArcadeStroke.IsCross(2.0, 4.0, "drive", mid + 0.05), Is.False);
            Assert.That(ArcadeStroke.ShotLine(0.1, 6, "drive", -1), Is.LessThan(0), "from the middle: the corner you aimed at");
        }

        [Test] public void ACrossFromTheFrontStaysACrossInBothModes()
        {
            double ty = 1.9, speed = PhysicsConfig.Current.ArcadeHit.DriveSpeed;
            double aim = ArcadeStroke.ShotLine(2.0, 4.0, "drive", -3.0);           // the true cross point
            foreach (bool assisted in new[] { false, true })
            {
                double tx = ArcadeStroke.PullToLine(aim, Line(2.0, 4.0, aim, ty, speed), assisted);
                var b = Bounce(2.0, 4.0, tx, ty, speed);
                UnityEngine.Debug.Log("[Lines] cross from the front, " + (assisted ? "assisted" : "non-assisted") + ": lands (" + b.X.ToString("0.00") + ", " + b.Z.ToString("0.00") + ")");
                Assert.That(b.X, Is.LessThan(-0.8), "it lands on the far side - a cross, not a drive");
            }
        }

        [Test] public void TheShotIsPulledPartOfTheWayToItsLine()
        {
            // Assisted helps, it does not play the perfect shot: 1 m off the line ends 0.4 m off; Non-assisted 0.6 m.
            Assert.That(ArcadeStroke.PullToLine(1.0, 2.0, true), Is.EqualTo(1.6).Within(1e-9));
            Assert.That(ArcadeStroke.PullToLine(1.0, 2.0, false), Is.EqualTo(1.4).Within(1e-9));
            Assert.That(GameTuning.Hit.LineSnapAssisted, Is.LessThan(1.0), "never the line exactly");
            Assert.That(GameTuning.Hit.LineSnapNonAssisted, Is.LessThan(GameTuning.Hit.LineSnapAssisted), "Non-assisted is the harder mode");
        }
    }
}
