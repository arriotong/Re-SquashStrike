// ===========================================================
// Assets/SquashSim.Testing/Editor/ArcadeMatchTests.cs
// The simple hitting system inside a real match (game profile, P1 local):
// the serve goes where you aim and you walk about the box while serving;
// a local player is never slowed (no near-ball cap); no let appeals for
// now; getting there early tightens the shot; boasts arm with a tap.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class ArcadeMatchTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); PhysicsConfig.Current.Match.WarmUpEnabled = false; }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static MatchSimulation NewMatch()
        {
            var m = new MatchSimulation(0);
            m.Bots[0] = null;
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.3), 5);
            m.Players[0].WallAimEnabled = true;
            m.Players[0].ArcadeHitEnabled = true;
            return m;
        }

        static void Aim(InputIntent it, double x, double y) { it.HasAimPoint = true; it.AimPointX = x; it.AimPointY = y; }

        [Test] public void TheServerWalksAboutTheBoxAtFullSpeedAndNeverFootFaults()
        {
            var m = NewMatch();
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            double dt = PhysicsConfig.Current.Sim.FixedDt, top = 0;
            var me = m.Players[0]; var it = m.Intents[0];
            for (int s = 0; s < 480; s++)
            {
                it.Move.X = s < 240 ? -1 : 0; it.Move.Z = s < 240 ? 0 : 1; it.MoveMagnitude = 1;
                Aim(it, 1, 2.5);
                m.Step(dt);
                top = System.Math.Max(top, me.Player.Speed);
            }
            UnityEngine.Debug.Log("[Serve] top speed while waiting to serve " + top.ToString("0.00") + " m/s, at (" + me.Player.Pos.X.ToString("0.00") + ", " + me.Player.Pos.Z.ToString("0.00") + ")");
            Assert.That(top, Is.GreaterThan(PhysicsConfig.Current.Player.MaxSpeed * 0.8), "no slow walk before the serve");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "walking about does not end the rally");
            Assert.That(RallyReferee.InServiceBox(m.Score.ServeSide, me.Player.Pos, 0), Is.True, "kept in the service box");
        }

        static V3 ServeAt(double x, double y, out RallyOutcome outcome)
        {
            var m = NewMatch();
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var it = m.Intents[0];
            for (int s = 0; s < 24; s++) { Aim(it, x, y); m.Step(dt); }
            V3 front = null; outcome = null;
            RallyOutcome got = null;
            m.RallyEnded += o => got = o;
            Aim(it, x, y); it.Hit.Pressed = true;
            for (int s = 0; s < 480 && front == null && got == null; s++)
            {
                m.Step(dt);
                foreach (var e in m.StepEvents) if (e.Type == SimEventType.Impact && e.SurfaceClass == "frontWall" && front == null) front = e.Point.Clone();
                Aim(it, x, y);
            }
            for (int s = 0; s < 240 && got == null; s++) m.Step(dt);
            outcome = got;
            return front;
        }

        [Test] public void TheServeGoesWhereYouAim()
        {
            foreach (var t in new[] { new[] { -2.0, 2.4 }, new[] { 1.5, 3.2 }, new[] { -0.5, 2.0 } })
            {
                V3 f = ServeAt(t[0], t[1], out _);
                Assert.That(f, Is.Not.Null);
                double err = System.Math.Sqrt((f.X - t[0]) * (f.X - t[0]) + (f.Y - t[1]) * (f.Y - t[1]));
                UnityEngine.Debug.Log("[Serve] aimed (" + t[0] + ", " + t[1] + "): front wall at (" + f.X.ToString("0.00") + ", " + f.Y.ToString("0.00") + ")");
                Assert.That(err, Is.LessThan(0.25), "the serve lands on the aim");
            }
            ServeAt(0.5, 1.2, out RallyOutcome low);
            Assert.That(low, Is.Not.Null);
            Assert.That(low.Kind, Is.EqualTo(RallyEnd.Fault), "aimed under the service line: a fault");
        }

        [Test] public void ALocalPlayerIsNeverCappedNearTheBall()
        {
            var me = new TestPlayerController();
            me.ArcadeHitEnabled = true;
            me.Player.ArcadeMovement = true;
            me.Player.Pos.X = -2.0; me.Player.Pos.Z = 5.0;
            var assist = new MovementAssist { HasIdealSpot = true };
            assist.IdealSpot.X = me.Player.Pos.X + 0.3; assist.IdealSpot.Z = me.Player.Pos.Z;
            var it = new InputIntent(); it.Move.X = 1; it.MoveMagnitude = 1;
            for (int s = 0; s < 120; s++)
            {
                assist.IdealSpot.X = me.Player.Pos.X + 0.3; assist.IdealSpot.Z = me.Player.Pos.Z;   // the ball always just ahead
                PlayerMovement.Step(me.Player, it, assist, PhysicsConfig.Current.Sim.FixedDt);
            }
            Assert.That(me.Player.Adjusting, Is.False);
            Assert.That(me.Player.Speed, Is.GreaterThan(PhysicsConfig.Current.Player.AdjustSpeed + 1), "full speed near the ball");
        }

        [Test] public void NoLetAppealsInTheGame()
        {
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.False);
            PhysicsConfig.ResetToDefaults();
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.True, "the lab keeps them");
        }

        [Test] public void BoastLandsOnTheFarHalf()
        {
            Assert.That(ArcadeStroke.BoastTarget(2.5, 2.0, out double lx), Is.EqualTo(1));
            Assert.That(lx, Is.LessThanOrEqualTo(-PhysicsConfig.Current.ArcadeHit.BoastCross), "from the right: across to the left half");
            Assert.That(ArcadeStroke.BoastTarget(-2.5, 1.7, out lx), Is.EqualTo(-1));
            Assert.That(lx, Is.EqualTo(1.7).Within(1e-9), "already on the far half: exactly the aim");
        }
    }
}
