// ===========================================================
// Assets/SquashSim.Testing/Editor/PushOutTests.cs
// Playtest 2026-10-20: "the green square blocks my movement". The
// movement assist's "never stand on the ball" push-out held the player
// off the predicted contact point - which for a dropping ball is the
// landing square - the whole time the ball was in the air. In the game
// it now acts only as the ball arrives (GameTuning.Movement.PushOutWithin):
// you can walk across the spot early, and still never stand on the ball
// when it gets there. The lab keeps it always on (SimParity).
// ===========================================================

using System;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class PushOutTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static readonly V3 From = new V3(-1.0, 1.0, 8.0), Drive = new V3(0.0, 7.0, -16.0);

        /// <summary>Walk player 0 through the predicted contact point; the closest it gets while the ball is still
        /// more than 0.3 s away, and at the contact.</summary>
        static (double early, double atContact) WalkThrough()
        {
            var s = new ReplaySetup { TwoPlayer = true, GamesToWin = 1 };
            ReplayMatch.Configure(s);
            PhysicsConfig.Current.Match.AppealsEnabled = false;
            var m = ReplayMatch.Build(s);
            m.StageRally(From, Drive, -2.2, 4.0, 2.4, 2.0);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            double early = 99, late = 99;
            double gx = 0, gz = 0; bool aimed = false;
            var me = m.Players[0];
            for (int i = 0; i < 240 * 3 && m.Referee.Outcome == null; i++)
            {
                var a = m.Intents[0];
                var pr = me.Prediction;
                if (!aimed && pr.Valid && pr.Time > 0.5)
                {
                    // a walk from here through the contact point and 1.5 m on
                    double dx = pr.Point.X - me.Player.Pos.X, dz = pr.Point.Z - me.Player.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
                    gx = pr.Point.X + dx / d * 1.5; gz = pr.Point.Z + dz / d * 1.5; aimed = true;
                }
                if (aimed)
                {
                    double dx = gx - me.Player.Pos.X, dz = gz - me.Player.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (d > 0.1) { a.Move.X = dx / d; a.Move.Z = dz / d; a.MoveMagnitude = 1; } else { a.Move.X = 0; a.Move.Z = 0; a.MoveMagnitude = 0; }
                }
                m.Step(dt);
                if (pr.Valid)
                {
                    double gap = Math.Sqrt((me.Player.Pos.X - pr.Point.X) * (me.Player.Pos.X - pr.Point.X) + (me.Player.Pos.Z - pr.Point.Z) * (me.Player.Pos.Z - pr.Point.Z));
                    if (pr.Time > 0.3) early = Math.Min(early, gap);
                    else if (pr.Time < 0.05) late = Math.Min(late, gap);
                }
            }
            Assert.That(aimed, Is.True, "the ball was predicted early enough to walk through it");
            return (early, late);
        }

        [Test] public void YouCanWalkAcrossTheLandingSpotWhileTheBallIsInTheAir()
        {
            var (early, late) = WalkThrough();
            UnityEngine.Debug.Log("[PushOut] closest to the contact point while the ball is in the air: " + early.ToString("0.00") + " m (push-out " + PhysicsConfig.Current.MovementAssist.MinSpacing + " m)");
            Assert.That(early, Is.LessThan(PhysicsConfig.Current.MovementAssist.MinSpacing * 0.5), "no invisible wall round the spot");
        }

        [Test] public void TheLabStillPushesOutAlways()
        {
            PhysicsConfig.ResetToDefaults();
            Assert.That(PhysicsConfig.Current.MovementAssist.PushOutWithin, Is.EqualTo(0), "SimParity: the lab's rule");
            PhysicsConfig.UseGameProfile();
            Assert.That(PhysicsConfig.Current.MovementAssist.PushOutWithin, Is.EqualTo(GameTuning.Movement.PushOutWithin));
        }
    }
}
