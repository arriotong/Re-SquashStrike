// ===========================================================
// Assets/SquashSim.Testing/Editor/ShotLineTests.cs
// Shot lines: a person picks the shot and the corner; the line comes
// from the shot. The corner is the half of the front wall the aim is on
// (playtest 2026-10-28: "if your aim is more to the right guide it to the
// right corner") - straight when that is the ball's side, else a cross.
// Drives from all over the court, flown through the real ball physics: a
// straight drive comes back down its own wall and deep, a cross goes to
// the far back corner. The shot is pulled most of the way onto its line,
// the aim keeping a little of its own. (Logged against the raw aim.)
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

        [Test] public void TheAimsHalfPicksTheCorner()
        {
            // From the back right: anywhere on the right half is the straight drive, anywhere on the left the cross
            foreach (double tx in new[] { 0.2, 1.0, 2.9 }) Assert.That(ArcadeStroke.IsCross(2.6, 7.0, "drive", tx), Is.False, "right of centre: straight, " + tx);
            foreach (double tx in new[] { -0.2, -1.0, -2.9 }) Assert.That(ArcadeStroke.IsCross(2.6, 7.0, "drive", tx), Is.True, "left of centre: cross, " + tx);
            // the line is the same wherever on that half you aim
            Assert.That(ArcadeStroke.ShotLine(2.6, 7.0, "drive", 0.2), Is.EqualTo(ArcadeStroke.ShotLine(2.6, 7.0, "drive", 2.9)).Within(1e-9));
            // a cross from the front meets the front wall on the ball's own half - still the left corner's line
            double cross = ArcadeStroke.ShotLine(2.0, 4.0, "drive", -3.0);
            Assert.That(cross, Is.GreaterThan(0), "a cross from the front is aimed on your own half");
            Assert.That(ArcadeStroke.ShotLine(0.1, 6, "drive", -1), Is.LessThan(0), "from the middle: the corner you aimed at");
            Assert.That(ArcadeStroke.ShotLine(0.1, 6, "drive", 1), Is.GreaterThan(0));
        }

        [Test] public void ACrossFromTheFrontStaysACrossInBothModes()
        {
            double ty = 1.9, speed = PhysicsConfig.Current.ArcadeHit.DriveSpeed;
            double aim = -1.0;                                                      // the left half: the cross
            foreach (bool assisted in new[] { false, true })
            {
                double tx = ArcadeStroke.PullToLine(aim, Line(2.0, 4.0, aim, ty, speed), assisted);
                var b = Bounce(2.0, 4.0, tx, ty, speed);
                UnityEngine.Debug.Log("[Lines] cross from the front, " + (assisted ? "assisted" : "non-assisted") + ": lands (" + b.X.ToString("0.00") + ", " + b.Z.ToString("0.00") + ")");
                Assert.That(b.X, Is.LessThan(-0.8), "it lands on the far side - a cross, not a drive");
            }
        }

        /// <summary>A person's drive through the real stroke (a clean contact, set in position): where it dies (the second
        /// bounce, x) and whether the aim's corner got it.</summary>
        static double DiesAt(double bx, double bz, double tx, double ty, bool assisted)
        {
            var m = new MatchSimulation(0);
            var p = m.Players[0];
            p.ArcadeHitEnabled = true; p.WallAimEnabled = true;
            p.Arcade.ShotLines = true; p.Arcade.Assisted = assisted;
            p.Player.Pos.X = bx + 0.5; p.Player.Pos.Z = bz + 0.3;
            p.Aim.HasWallTarget = true; p.Aim.TargetWall = 0; p.Aim.FromPoint = true;
            p.Aim.WallTarget.X = tx; p.Aim.WallTarget.Y = ty; p.Aim.WallTarget.Z = 0;
            m.Ball.Reset(new V3(bx, 0.6, bz), new V3(0, 0, 0), null, 40);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ArcadeStroke).GetField("_setTime", flags).SetValue(p.Arcade, 1.0);
            var c = PhysicsConfig.Current.ArcadeHit;
            typeof(ArcadeStroke).GetMethod("Hit", flags).Invoke(p.Arcade, new object[] { p, m.Ball, m.Surfaces, c.SweetSpot * 0.5, 0.0, c });
            var evs = new List<SimEvent>();
            bool front = false; int floors = 0;
            for (int i = 0; i < 240 * 4; i++)
            {
                evs.Clear();
                BallPhysics.Step(m.Ball, m.Surfaces, 1.0 / 240, evs);
                foreach (var e in evs)
                {
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (front && e.SurfaceClass == "floor" && ++floors == 2) return e.Point.X;
                }
            }
            return m.Ball.Pos.X;
        }

        [Test] public void AimRightGoesToTheRightCornerAimLeftToTheLeft()
        {
            // playtest 2026-10-28: "95% of the time the ball just skits out the side wall and lands in the middle"
            foreach (bool assisted in new[] { false, true })
            {
                int n = 0, corner = 0, wrong = 0;
                foreach (double bx in new[] { -2.6, -1.4, 1.4, 2.6 }) foreach (double bz in new[] { 5.0, 7.0, 8.2 })
                    foreach (double tx in new[] { -2.6, -1.2, -0.3, 0.3, 1.2, 2.6 }) foreach (double ty in new[] { 1.4, 2.2 })
                    {
                        double x = DiesAt(bx, bz, tx, ty, assisted);
                        int to = tx > 0 ? 1 : -1;
                        n++;
                        if (x * to > 1.0) corner++;
                        if (x * to < -1.0) wrong++;
                    }
                UnityEngine.Debug.Log("[Lines] " + (assisted ? "assisted" : "normal") + ": the aim's corner " + corner + " / " + n + ", the other " + wrong);
                Assert.That(wrong, Is.EqualTo(0), "never the other corner");
                Assert.That(corner, Is.GreaterThanOrEqualTo(n * 0.9), "into the aim's corner");
            }
        }

        [Test] public void TheShotIsPulledMostOfTheWayToItsLine()
        {
            // 1 m off the line ends 0.08 m off Assisted, 0.2 m Non-assisted; never more than LineFreedom off
            Assert.That(ArcadeStroke.PullToLine(1.0, 2.0, true), Is.EqualTo(2.0 - 0.08).Within(1e-9));
            Assert.That(ArcadeStroke.PullToLine(1.0, 2.0, false), Is.EqualTo(2.0 - 0.2).Within(1e-9));
            Assert.That(ArcadeStroke.PullToLine(-2.0, 2.0, false), Is.EqualTo(2.0 - GameTuning.Hit.LineFreedom).Within(1e-9), "a wild aim: LineFreedom at most");
            Assert.That(GameTuning.Hit.LineSnapAssisted, Is.LessThan(1.0), "never the line exactly");
            Assert.That(GameTuning.Hit.LineSnapNonAssisted, Is.LessThan(GameTuning.Hit.LineSnapAssisted), "Non-assisted is the harder mode");
        }
    }
}
