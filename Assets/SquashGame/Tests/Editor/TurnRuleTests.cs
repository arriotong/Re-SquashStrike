// ===========================================================
// Assets/SquashGame/Tests/Editor/TurnRuleTests.cs
// Playtest 2026-10-24 (Local Versus): "my opponent can play a shot before
// the ball touches the front wall". The sim never let them strike it, but
// two things made it look that way; these keep both fixed:
//   - a ball off a (running) body goes dead, it does not fly like a shot
//   - letting go while the ball was never yours drops the stroke - no
//     swing at it
// And the rule itself: a local player mashing Hit and chasing the ball
// never strikes the opponent's shot before it reaches the front wall.
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashGame.Tests
{
    public class TurnRuleTests
    {
        [SetUp] public void Profile() { PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Back() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>Chases the ball and mashes Hit, all the time.</summary>
        sealed class Masher : IAthleteBrain
        {
            int _k;
            public void Fill(InputIntent it, MatchSimulation m, int me, double dt)
            {
                var p = m.Players[me].Player.Pos; var b = m.Ball.Pos;
                double dx = b.X - p.X, dz = b.Z - p.Z, d = System.Math.Sqrt(dx * dx + dz * dz);
                it.Move.X = d > 1e-6 ? dx / d : 0; it.Move.Z = d > 1e-6 ? dz / d : 0; it.MoveMagnitude = d > 0.3 ? 1 : 0;
                bool down = (++_k % 6) < 3;
                it.Hit.Pressed = down && !it.Hit.Held; it.Hit.Released = !down && it.Hit.Held; it.Hit.Held = down;
            }
        }

        [Test] public void NobodyStrikesTheOpponentsShotBeforeTheFrontWall()
        {
            int illegal = 0, contacts = 0;
            for (int g = 0; g < 2; g++)
            {
                var m = new MatchSimulation(g % 2);
                m.Bots[0] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.6), (uint)(10 + g));
                m.Bots[1] = new Masher();
                for (int i = 0; i < 2; i++) { m.Players[i].WallAimEnabled = true; m.Players[i].ArcadeHitEnabled = true; }
                int last = -1; bool front = true;
                double dt = PhysicsConfig.Current.Sim.FixedDt;
                for (int s = 0; s < 240 * 60 && m.Phase != MatchPhase.MatchOver && m.Phase != MatchPhase.GameOver; s++)
                {
                    m.Step(dt);
                    foreach (var e in m.StepEvents)
                    {
                        if (e.Type == SimEventType.Impact && e.SurfaceClass == "frontWall") front = true;
                        if (e.Type != SimEventType.RacketContact || m.Phase == MatchPhase.WarmUp) continue;
                        contacts++;
                        if (last >= 0 && !front && e.PlayerIndex != last) illegal++;
                        last = e.PlayerIndex; front = false;
                    }
                    if (m.Phase == MatchPhase.RallyOver) { last = -1; front = true; }
                }
            }
            UnityEngine.Debug.Log("[Turns] " + contacts + " contacts, " + illegal + " before the front wall");
            Assert.That(contacts, Is.GreaterThan(50));
            Assert.That(illegal, Is.EqualTo(0));
        }

        [Test] public void ABallOffARunningBodyGoesDead()
        {
            var ball = new BallState { Asleep = false };
            ball.Pos.X = 0; ball.Pos.Y = 1.0; ball.Pos.Z = 4.0;
            ball.Vel.X = 0; ball.Vel.Y = 0; ball.Vel.Z = -20;          // a drive on its way to the front wall
            var prev = ball.Pos.Clone(); prev.Z = 4.2;
            var body = new TestPlayer();
            body.Pos.X = 0; body.Pos.Z = 3.75;
            body.Vel.X = 0; body.Vel.Z = 5;                            // running back, into it
            var ev = BodyCollision.BallVsBody(ball, prev, body, 1, 0);
            Assert.That(ev, Is.Not.Null, "the ball should hit the body");
            double speed = V3.Length(ball.Vel);
            UnityEngine.Debug.Log("[Turns] off the body at " + speed.ToString("0.0") + " m/s");
            // 20 m/s in, a 5 m/s run back into it: it used to leave at 8.75 m/s (all of the runner's speed); now ~5.
            Assert.That(speed, Is.LessThan(6.0), "a ball off a body drops - it must not fly away like a shot");
        }
    }
}
