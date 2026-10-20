// ===========================================================
// Assets/SquashSim.Testing/Editor/AiDeepBallTests.cs
// Playtest 2026-10-20: "I hit it into the back corners and the AI just
// stood there and let it bounce twice." It got there and pressed Hit,
// but its timing error could put the release after the contact - a
// moment the prediction never reaches - so it held on until the ball
// died (about one turn in five). Staged drives deep into the back court,
// the AI on the T, many seeds: it gets a racket on nearly all of them.
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class AiDeepBallTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheAiPlaysDeepBallsInsteadOfWatchingThemDie()
        {
            var drives = new[]
            {
                (new V3(-1.0, 1.0, 8.0), new V3(0.0, 7.0, -16.0)),     // back down the left
                (new V3(1.0, 1.0, 8.0), new V3(0.0, 7.0, -16.0)),      // back down the right
                (new V3(-1.2, 1.0, 6.5), new V3(1.2, 6.0, -17.0)),     // a cross to the right corner
                (new V3(1.2, 1.0, 6.5), new V3(-1.2, 6.0, -17.0)),     // a cross to the left corner
            };
            int n = 0, played = 0, died = 0;
            for (int seed = 0; seed < 15; seed++)
                foreach (var (from, vel) in drives)
                {
                    var s = new ReplaySetup { TwoPlayer = true, GamesToWin = 1 };
                    ReplayMatch.Configure(s);
                    PhysicsConfig.Current.Match.AppealsEnabled = false;
                    var m = ReplayMatch.Build(s);
                    m.Bots[0] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(GameTuning.Ai.Levels[1]), (uint)(100 + seed));
                    m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
                    m.StageRally(from, vel, 0, 5.5, -2.6, 2.0);          // the AI on the T, the other player out of the way
                    n++;
                    bool hit = false;
                    for (int i = 0; i < 240 * 4 && !hit && m.Referee.Outcome == null; i++)
                    {
                        m.Step(PhysicsConfig.Current.Sim.FixedDt);
                        foreach (var e in m.StepEvents) if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 0) hit = true;
                    }
                    if (hit) played++;
                    else if (m.Referee.Outcome != null && m.Referee.Outcome.Kind == RallyEnd.NotUp) died++;
                }
            UnityEngine.Debug.Log("[AI] deep balls played " + played + " / " + n + ", let die " + died);
            Assert.That(played, Is.GreaterThanOrEqualTo(n * 0.95), "the AI goes for the ball and plays it");
        }
    }
}
