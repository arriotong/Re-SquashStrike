// ===========================================================
// Assets/SquashSim.Testing/Editor/ScenarioTests.cs
// P3-R6: Scenarios.
//   - 15, five a group, unique ids, every one with two star conditions
//   - each one's match starts where its story says (the score, the
//     games, who serves), built the way the game builds it
//   - stars: none for a loss, one for the win, one more for each
//     condition met
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Modes;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class ScenarioTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void FifteenInThreeGroups()
        {
            var all = Scenarios.All();
            Assert.That(all.Count, Is.EqualTo(15));
            var ids = new HashSet<string>();
            foreach (var s in all)
            {
                Assert.That(ids.Add(s.Id), Is.True, s.Id);
                Assert.That(s.Star2, Is.Not.Null, s.Id); Assert.That(s.Star3, Is.Not.Null, s.Id);
                Assert.That(s.Difficulty, Is.InRange(0.1, 1.0), s.Id);
                Assert.That(AiProfile.ByName(s.Opponent), Is.Not.Null, s.Id);
            }
            for (int g = 0; g < 3; g++) Assert.That(Scenarios.InGroup(g).Count, Is.EqualTo(5));
            Assert.That(Scenarios.MaxStars, Is.EqualTo(45));
        }

        [Test] public void EachMatchStartsWhereItsStorySays()
        {
            foreach (var s in Scenarios.All())
            {
                var setup = new ReplaySetup { Opponent = s.Opponent, Difficulty = (float)s.Difficulty, GamesToWin = s.GamesToWin, Scoring = s.Scoring, BotSeed = 3,
                    StartPoints0 = s.StartPoints0, StartPoints1 = s.StartPoints1, StartGames0 = s.StartGames0, StartGames1 = s.StartGames1, StartServer = s.StartServer };
                ReplayMatch.Configure(setup);
                var sim = ReplayMatch.Build(setup);
                Assert.That(sim.Score.Points, Is.EqualTo(new[] { s.StartPoints0, s.StartPoints1 }), s.Id);
                Assert.That(sim.Score.Games, Is.EqualTo(new[] { s.StartGames0, s.StartGames1 }), s.Id);
                Assert.That(sim.Score.MatchOver, Is.False, s.Id + ": still to be played");
                if (s.StartServer >= 0) Assert.That(sim.Score.Server, Is.EqualTo(s.StartServer), s.Id);
            }
        }

        [Test] public void StarsForTheWinAndEachCondition()
        {
            var s = Scenarios.Find("gameball");                       // no tins; two winners
            Assert.That(s.Stars(new ScenarioResult { Won = false, Winners = 5 }), Is.EqualTo(0));
            Assert.That(s.Stars(new ScenarioResult { Won = true, Tins = 1, Winners = 0 }), Is.EqualTo(1));
            Assert.That(s.Stars(new ScenarioResult { Won = true, Tins = 0, Winners = 0 }), Is.EqualTo(2));
            Assert.That(s.Stars(new ScenarioResult { Won = true, Tins = 0, Winners = 2 }), Is.EqualTo(3));
            var last = Scenarios.Find("lastdance");                   // lose 3 rallies at most
            Assert.That(last.Stars(new ScenarioResult { Won = true, Rallies = 30, RalliesWon = 26, Tins = 1 }), Is.EqualTo(1));
            Assert.That(last.Stars(new ScenarioResult { Won = true, Rallies = 30, RalliesWon = 27, Tins = 0 }), Is.EqualTo(3));
        }
    }
}
