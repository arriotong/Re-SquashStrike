// ===========================================================
// Assets/SquashGame/Tests/Editor/BroadcastHudTests.cs
// PR5: the game-ball chip's rule, PAR (win by two) and English
// (only the server scores).
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashGame.Tests
{
    public class BroadcastHudTests
    {
        static void Win(MatchScore s, int p, int times = 1)
        {
            for (int i = 0; i < times; i++) s.Apply(new RallyOutcome { Kind = RallyEnd.NotUp, Winner = p, Loser = 1 - p });
        }

        [Test] public void ParGameBallNeedsTenAndALead()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 2, false);
            Win(s, 0, 9); Win(s, 1, 9);
            Assert.That(HudLayer.GameBall(s, 0), Is.False, "9-9");
            Win(s, 0);
            Assert.That(HudLayer.GameBall(s, 0), Is.True, "10-9");
            Assert.That(HudLayer.GameBall(s, 1), Is.False, "9-10 for the other");
            Win(s, 1);
            Assert.That(HudLayer.GameBall(s, 0) || HudLayer.GameBall(s, 1), Is.False, "10-10: win by two");
            Win(s, 1);
            Assert.That(HudLayer.GameBall(s, 1), Is.True, "10-11");
        }

        [Test] public void EnglishGameBallIsTheServers()
        {
            var s = new MatchScore(ScoringSystem.English, 0, 1, false);
            Win(s, 0, s.GameTarget - 1);
            Assert.That(s.Server, Is.EqualTo(0));
            Assert.That(HudLayer.GameBall(s, 0), Is.True, "server one from the target");
            Win(s, 1);                                                           // hand-out
            Assert.That(HudLayer.GameBall(s, 0), Is.False, "not serving: can't score");
        }
    }
}
