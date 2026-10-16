// ===========================================================
// Assets/SquashSim.Testing/Editor/MatchFlowTests.cs
// M5: the knock-up (a cold ball warmed by play, halves swap, skip),
// the break between games (the ball cools, the next game starts by
// itself), and a full best-of-5 between AI players.
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class MatchFlowTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static MatchSimulation AiMatch(bool warmUp, int gamesToWin)
        {
            var c = PhysicsConfig.Current.Match;
            c.WarmUpEnabled = warmUp;
            c.GamesToWin = gamesToWin;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName("grinder").WithDifficulty(0.8), 11);
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.8), 12);
            return m;
        }

        [Test] public void WarmUpIsOffByDefaultSoTheMatchStartsAtTheServe()
        {
            var m = new MatchSimulation(0);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            Assert.That(m.Ball.Temperature, Is.EqualTo(PhysicsConfig.Current.Match.StartTemperature));
        }

        [Test] public void KnockUpStartsColdAndPlayWarmsTheBall()
        {
            var m = AiMatch(true, 1);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.WarmUp));
            Assert.That(m.WarmUpOwner, Is.EqualTo(0));
            Assert.That(m.Ball.Temperature, Is.EqualTo(PhysicsConfig.Current.Temperature.Initial), "a new ball is cold");

            bool sawSecondHalf = false;
            int shots0 = 0;
            while (m.Phase == MatchPhase.WarmUp && m.Time < 200)
            {
                int before = m.WarmUpShots;
                m.Step(Dt);
                if (m.WarmUpHalf == 0 && m.WarmUpShots > before) shots0++;
                if (m.WarmUpHalf == 1) { sawSecondHalf = true; Assert.That(m.WarmUpOwner, Is.EqualTo(1)); }
                Assert.That(m.Players[1 - m.WarmUpOwner].ContactEnabled, Is.False, "only the player knocking up may strike");
            }
            Assert.That(sawSecondHalf, Is.True, "the halves swap");
            Assert.That(shots0, Is.GreaterThan(8), "the AI knocks up");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "the knock-up ends at the first serve");
            Assert.That(m.Ball.Temperature, Is.GreaterThan(PhysicsConfig.Current.Temperature.Initial + 6), "play has warmed the ball");
            Assert.That(m.History.Count, Is.EqualTo(0), "the knock-up scores nothing");
            Assert.That(m.Score.Points[0] + m.Score.Points[1], Is.EqualTo(0));
        }

        [Test] public void KnockUpCanBeSkipped()
        {
            var m = AiMatch(true, 1);
            for (int i = 0; i < 240; i++) m.Step(Dt);
            m.SkipWarmUp();
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            Assert.That(m.Score.Server, Is.EqualTo(0));
            Assert.That(m.Ball.Asleep, Is.True, "held for the serve");
        }

        [Test] public void BreakBetweenGamesCoolsTheBallAndStartsTheNextGame()
        {
            var m = AiMatch(false, 3);
            while (m.Phase != MatchPhase.GameOver && m.Time < 3600) m.Step(Dt);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.GameOver));
            double hot = m.Ball.Temperature;
            int games = m.Score.Games[0] + m.Score.Games[1];
            Assert.That(games, Is.EqualTo(1));

            double breakSeconds = PhysicsConfig.Current.Match.BetweenGamesSeconds;
            double t0 = m.Time;
            while (m.Phase == MatchPhase.GameOver) m.Step(Dt);
            Assert.That(m.Time - t0, Is.EqualTo(breakSeconds).Within(2 * Dt), "the break lasts its configured time");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "the next game starts by itself");
            Assert.That(m.Ball.Temperature, Is.LessThan(hot - 3), "the ball cools in the break");
            Assert.That(m.Score.Points[0] + m.Score.Points[1], Is.EqualTo(0), "fresh game score");
        }

        [Test] public void FullBestOfFiveCompletesDeterministically()
        {
            string Play(out MatchSimulation m)
            {
                PhysicsConfig.ResetToDefaults();
                m = AiMatch(true, 3);
                while (m.Phase != MatchPhase.MatchOver && m.Time < 4 * 3600) m.Step(Dt);
                return m.Score.Games[0] + "-" + m.Score.Games[1] + "|" + m.History.Count + "|" + m.Time.ToString("R");
            }
            string a = Play(out MatchSimulation m1);
            string b = Play(out _);
            Assert.That(m1.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(System.Math.Max(m1.Score.Games[0], m1.Score.Games[1]), Is.EqualTo(3));
            Assert.That(m1.Score.MatchWinner, Is.GreaterThanOrEqualTo(0));
            Assert.That(a, Is.EqualTo(b), "same seeds, same match");
        }
    }
}
