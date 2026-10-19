// ===========================================================
// Assets/SquashSim.Testing/Editor/HighlightsTests.cs
// P2-M6: a match's rallies tagged and the reel chosen, on a hand-made
// match: a nick winner, a 25-shot rally, a comeback from 2-6 down, a game
// ball and the match ball.
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class HighlightsTests
    {
        static ReplayFile Match()
        {
            var f = new ReplayFile();
            int p0 = 0, p1 = 0, g0 = 0, g1 = 0;
            long step = 0;
            void Rally(int winner, int shots, double pace = 20, bool nick = false, int last = -1, RallyEnd kind = RallyEnd.NotUp, int great0 = 0)
            {
                if (winner == 0) p0++; else p1++;
                bool game = (p0 >= 11 || p1 >= 11) && System.Math.Abs(p0 - p1) >= 2;
                if (game) { if (p0 > p1) g0++; else g1++; }
                step += 2400;
                f.Rallies.Add(new RallyRec { EndStep = step, Winner = winner, Points0 = p0, Points1 = p1, Games0 = g0, Games1 = g1, Shots = shots, TopPace = pace,
                                             Nick = nick, LastStriker = last < 0 ? winner : last, Kind = (byte)kind, Great0 = great0 });
                if (game) { p0 = 0; p1 = 0; }
            }
            // game 1: you go 2-6 down, come back, win 11-8
            Rally(0, 4); Rally(1, 3); Rally(1, 5); Rally(1, 2, kind: RallyEnd.Down, last: 0); Rally(0, 6); Rally(1, 3); Rally(1, 4); Rally(1, 2);   // 2-6
            Rally(0, 3); Rally(0, 4); Rally(0, 5); Rally(0, 7);                                                                                   // 6-6: the comeback point
            Rally(0, 25); Rally(0, 4, nick: true); Rally(1, 3); Rally(0, 5, pace: 48, great0: 1); Rally(1, 3); Rally(0, 6); Rally(0, 4);   // 12..18: 11-8, the game
            // game 2: 11-3
            for (int i = 0; i < 14; i++) Rally(i % 4 == 3 ? 1 : 0, 3 + i % 3);
            f.Winner = 0;
            return f;
        }

        [Test] public void TheRalliesAreTagged()
        {
            var f = Match();
            var t = Highlights.Tag(f);
            Assert.That(t[12] & RallyTag.Marathon, Is.Not.EqualTo(RallyTag.None), "25 shots: a marathon");
            Assert.That(t[12] & RallyTag.Longest, Is.Not.EqualTo(RallyTag.None), "the longest rally");
            Assert.That(t[13] & RallyTag.Nick, Is.Not.EqualTo(RallyTag.None), "the nick winner");
            Assert.That(t[15] & RallyTag.Rocket, Is.Not.EqualTo(RallyTag.None), "the fastest shot");
            Assert.That(t[15] & RallyTag.GreatHit, Is.Not.EqualTo(RallyTag.None));
            Assert.That(t[11] & RallyTag.Comeback, Is.Not.EqualTo(RallyTag.None), "6-6 from 2-6 down");
            Assert.That(t[18] & RallyTag.GameBall, Is.Not.EqualTo(RallyTag.None), "11-8: the game");
            Assert.That(t[t.Length - 1] & RallyTag.MatchBall, Is.Not.EqualTo(RallyTag.None), "the match ball");
            Assert.That(t[3] & RallyTag.Winner, Is.EqualTo(RallyTag.None), "a tin is not your winner");
            Assert.That(Highlights.GameScores(f), Is.EqualTo("11-8 11-3"));
        }

        [Test] public void TheReelHasTheBestInPlayOrderAndTheMatchBall()
        {
            var f = Match();
            var reel = Highlights.Reel(f);
            Assert.That(reel.Count, Is.InRange(3, 5));
            Assert.That(reel, Does.Contain(f.Rallies.Count - 1), "the match ball");
            Assert.That(reel, Does.Contain(12), "the marathon");
            Assert.That(reel, Does.Contain(13), "the nick");
            for (int i = 1; i < reel.Count; i++) Assert.That(reel[i], Is.GreaterThan(reel[i - 1]), "in the order they were played");
        }
    }
}
