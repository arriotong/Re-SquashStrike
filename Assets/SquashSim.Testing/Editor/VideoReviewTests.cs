// ===========================================================
// Assets/SquashSim.Testing/Editor/VideoReviewTests.cs
// 2026-11-22 evening, the referee remade (the user: "its kind of inaccurate
// when opponent is far away from you and like ref sometimes gets it wrong
// ... add referee decision review so player and ai opponent can actually
// challenge the ref on calls"):
//   - the line to the front wall narrows with distance: far away, only the
//     body (a ball 0.5 m off it would have missed)
//   - the referee may see a close call wrong, never a clear-cut one
//   - a challenge: overturned - the score put right, the review kept;
//     upheld - the review spent; one a game, back each game
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class VideoReviewTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }
        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        [Test] public void TheLineNarrowsWithDistance()
        {
            var it = new Interference();
            // the user's replay: the ball at (-1.3, 6.5), the opponent at (-0.8, 3.7) - 2.8 m ahead, 0.5 m off
            Assert.That(it.Detect(-1.9, 7.0, -0.8, 3.7, -1.3, 6.5), Is.Not.EqualTo(ObstructionKind.Line), "far and to the side: not in the line");
            Assert.That(it.Detect(-1.9, 7.0, -1.2, 3.7, -1.3, 6.5), Is.EqualTo(ObstructionKind.Line), "far but on it: still a stroke");
            Assert.That(it.Detect(-1.9, 7.0, -0.85, 5.6, -1.3, 6.5), Is.EqualTo(ObstructionKind.Line).Or.EqualTo(ObstructionKind.Swing), "close: the swing's room counts");
            Assert.That(it.LineWidthAt(3.4), Is.LessThan(it.LineWidthAt(0.5)));
        }

        [Test] public void OnlyACloseCallCanBeSeenWrong()
        {
            var clear = new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = "Stroke", Doubt = 0, Other = InterferenceCall.Let };
            var close = new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = "Stroke", Doubt = 1, Other = InterferenceCall.Let };
            for (double roll = 0; roll < 1; roll += 0.05)
                Assert.That(Interference.AsSeen(clear, roll, 1).Call, Is.EqualTo(InterferenceCall.Stroke), "clear-cut: always right");
            Assert.That(Interference.AsSeen(close, 0.1, 0.6).Call, Is.EqualTo(InterferenceCall.Let), "on the line: it can go the other way");
            Assert.That(Interference.AsSeen(close, 0.9, 0.6).Call, Is.EqualTo(InterferenceCall.Stroke));
        }

        static MatchSimulation Match(int seed)
        {
            var c = PhysicsConfig.Current.Match;
            c.WarmUpEnabled = false; c.GamesToWin = 3;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName(seed % 2 == 0 ? "attacker" : "grinder").WithDifficulty(0.8), (uint)(900 + seed));
            m.Bots[1] = new OpponentBrain(AiProfile.ByName(seed % 2 == 0 ? "retriever" : "shotmaker").WithDifficulty(0.8), (uint)(950 + seed));
            for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
            return m;
        }

        /// <summary>Play until a call is open to a challenge - wrong (as asked) or right; the computer's own challenges out of the way.</summary>
        static MatchSimulation OpenCall(bool wrong, out int against)
        {
            against = -1;
            for (int seed = 0; seed < 60; seed++)
            {
                var m = Match(seed);
                while (m.Phase != MatchPhase.MatchOver && m.Time < 3600)
                {
                    m.Step(Dt);
                    if (m.Phase == MatchPhase.GameOver) m.NextGame();
                    if (!m.ReviewOpen || m.PhaseTime > Dt * 1.5) continue;
                    if ((m.CallMade.Call != m.CallTrue.Call) != wrong) continue;
                    for (int i = 0; i < 2; i++) if (m.CanChallenge(i)) against = i;
                    if (against < 0) continue;
                    m.Bots[0] = null; m.Bots[1] = null;                               // (no computer challenge of its own now)
                    m.Intents[0] = new InputIntent(); m.Intents[1] = new InputIntent();
                    return m;
                }
            }
            return null;
        }

        [Test] public void AWrongCallChallengedIsOverturnedAndTheScorePutRight()
        {
            var m = OpenCall(true, out int by);
            Assert.That(m, Is.Not.Null, "the referee got one wrong");
            var truth = m.CallTrue.Call;
            int p0 = m.Score.Points[0], p1 = m.Score.Points[1], left = m.ReviewsLeft[by];
            Assert.That(m.Challenge(by), Is.True);
            for (int k = 0; k < 400 && m.ReviewBy >= 0; k++) m.Step(Dt);
            Assert.That(m.ReviewResult, Is.EqualTo(1), "overturned");
            var o = m.History[m.History.Count - 1];
            Assert.That(o.Kind, Is.EqualTo(truth == InterferenceCall.Stroke ? RallyEnd.Stroke : truth == InterferenceCall.Let ? RallyEnd.Let : RallyEnd.NoLet), "the rally's result put right");
            Assert.That(m.ReviewsLeft[by], Is.EqualTo(left), "kept: they were right");
            int won = o.Winner;
            int now0 = m.Score.Points[0], now1 = m.Score.Points[1];
            Assert.That(now0 + now1, Is.LessThanOrEqualTo(p0 + p1 + 1), "no extra point");
            if (won >= 0) Assert.That(won == 0 ? now0 : now1, Is.GreaterThanOrEqualTo(won == 0 ? p0 - 1 : p1 - 1));
        }

        [Test] public void ARightCallChallengedStandsAndTheReviewIsSpent()
        {
            var m = OpenCall(false, out int by);
            Assert.That(m, Is.Not.Null, "a call");
            int p0 = m.Score.Points[0], p1 = m.Score.Points[1], left = m.ReviewsLeft[by];
            var kind = m.LastOutcome.Kind;
            Assert.That(m.Challenge(by), Is.True);
            for (int k = 0; k < 400 && m.ReviewBy >= 0; k++) m.Step(Dt);
            Assert.That(m.ReviewResult, Is.EqualTo(-1), "upheld");
            Assert.That(m.ReviewsLeft[by], Is.EqualTo(left - 1), "spent");
            Assert.That(m.Score.Points[0], Is.EqualTo(p0)); Assert.That(m.Score.Points[1], Is.EqualTo(p1));
            Assert.That(m.LastOutcome.Kind, Is.EqualTo(kind));
            Assert.That(m.CanChallenge(by), Is.False, "one try");
        }

        [Test] public void TheComputerChallengesSometimes()
        {
            int reviews = 0, errors = 0, calls = 0;
            for (int seed = 0; seed < 8; seed++)
            {
                var m = Match(seed);
                int seen = 0;
                while (m.Phase != MatchPhase.MatchOver && m.Time < 3600)
                {
                    m.Step(Dt);
                    if (m.Phase == MatchPhase.GameOver) m.NextGame();
                    if (m.History.Count > seen) { seen = m.History.Count; var o = m.History[seen - 1]; if (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke) calls++; }
                }
                reviews += m.Reviews; errors += m.RefErrors;
            }
            UnityEngine.Debug.Log("[Review] calls " + calls + ", referee wrong " + errors + ", computer reviews " + reviews);
            Assert.That(reviews, Is.GreaterThan(0), "it challenges");
            Assert.That(errors, Is.LessThan(calls * 0.2), "the referee is mostly right");
        }
    }
}
