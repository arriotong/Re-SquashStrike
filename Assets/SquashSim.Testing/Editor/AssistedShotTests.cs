// ===========================================================
// Assets/SquashSim.Testing/Editor/AssistedShotTests.cs
// Playtest 2026-10-27 (assisted mode): lobs that didn't reach the back
// (too much to the side), touch lobs and drops that went "so slow", and
// crosses from a dive at the front "bouncing off both side walls a couple
// of times". MatchBench --shot-audit measures all of it; these keep it.
//   - an assisted lob, straight or cross, hit or touch: lands deep
//   - a stretched assisted drop keeps its pace (without assist it loses some)
//   - no shot runs sideways at more than MaxSideSlope from the front
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class AssistedShotTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static double LandZ(BallState from, V3 vel, List<Surface> surfaces, out int sides)
        {
            var b = new BallState(); b.Reset(from.Pos, vel, null, from.Temperature);
            var ev = new List<SimEvent>();
            bool front = false; sides = 0;
            for (int i = 0; i < 1200; i++)
            {
                ev.Clear();
                BallPhysics.Step(b, surfaces, 1.0 / 240, ev);
                foreach (var e in ev)
                {
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (front && e.SurfaceClass == "sideWall") sides++;
                    else if (front && e.SurfaceClass == "floor") return e.Point.Z;
                }
            }
            return -1;
        }

        [Test] public void AssistedLobsLandDeep()
        {
            var surfaces = CourtSurfaces.Create();
            var st = new ArcadeStroke(1) { Assisted = true, ShotLines = true };
            var c = PhysicsConfig.Current.ArcadeHit;
            var ball = new BallState();
            foreach (double bz in new[] { 4.0, 6.0, 8.4 })
                foreach (double bx in new[] { -2.6, -1.2, 1.2, 2.6 })
                    foreach (bool cross in new[] { false, true })
                    {
                        ball.Reset(new V3(bx, 0.7, bz), new V3(0, 0, 0), null, 40);
                        double aim = (cross ? -1 : 1) * System.Math.Sign(bx) * 2.0;
                        double tx = ArcadeStroke.PullToLine(aim, ArcadeStroke.ShotLine(bx, bz, "lob", aim), true, "lob");
                        st.SolveLob(ball, surfaces, tx, c.LobHeight);
                        double z = LandZ(ball, st.LaunchVelocity.Clone(), surfaces, out _);
                        Assert.That(z, Is.GreaterThan(7.8), "a lob from " + bx + "," + bz + (cross ? " cross" : " straight") + " lands at " + z.ToString("0.00"));
                    }
            Assert.That(GameTuning.Hit.TouchLobSolved, Is.True, "a touch lob takes the lob's own arc, not touch pace");
        }

        [Test] public void AssistedDropsKeepTheirPace()
        {
            Assert.That(ArcadeStroke.MissPace(11.0, "drop", 1.0, true), Is.EqualTo(11.0));
            Assert.That(ArcadeStroke.MissPace(11.0, "lob", 1.0, true), Is.EqualTo(11.0));
            Assert.That(ArcadeStroke.MissPace(11.0, "drop", 1.0, false), Is.LessThan(11.0), "without assist a stretched drop still costs pace");
            Assert.That(ArcadeStroke.MissPace(20.0, "drive", 1.0, true), Is.LessThan(20.0), "a stretched drive still loses some");
        }

        [Test] public void NoShotRunsSidewaysFromTheFront()
        {
            var surfaces = CourtSurfaces.Create();
            var st = new ArcadeStroke(1) { Assisted = true, ShotLines = true };
            var ball = new BallState();
            int pinball = 0, n = 0;
            foreach (double bz in new[] { 0.5, 1.0, 2.0 })
                foreach (double bx in new[] { -2.8, -1.8, 1.8, 2.8 })
                {
                    ball.Reset(new V3(bx, 0.35, bz), new V3(0, 0, 0), null, 40);
                    double far = -System.Math.Sign(bx) * 3.0;
                    double tx = ArcadeStroke.KeepAngle(bx, bz, far);
                    Assert.That(System.Math.Abs(tx - bx), Is.LessThanOrEqualTo(GameTuning.Hit.MaxSideSlope * System.Math.Max(0.25, bz) + 1e-9));
                    st.Solve(ball, surfaces, tx, 1.4, 0, 0, PhysicsConfig.Current.ArcadeHit.DriveSpeed);
                    var b = new BallState(); b.Reset(ball.Pos, st.LaunchVelocity.Clone(), null, 40);
                    var ev = new List<SimEvent>(); int sides = 0, floors = 0; bool front = false;
                    for (int i = 0; i < 1200 && floors < 2; i++)
                    {
                        ev.Clear(); BallPhysics.Step(b, surfaces, 1.0 / 240, ev);
                        foreach (var e in ev) { if (e.SurfaceClass == "frontWall") front = true; else if (front && e.SurfaceClass == "sideWall") sides++; else if (front && e.SurfaceClass == "floor") floors++; }
                    }
                    n++; if (sides >= 3) pinball++;
                }
            Assert.That(pinball, Is.EqualTo(0), "no pinball off the side walls (" + pinball + " of " + n + ")");
        }
    }
}
