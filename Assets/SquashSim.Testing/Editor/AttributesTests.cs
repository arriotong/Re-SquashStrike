// ===========================================================
// Assets/SquashSim.Testing/Editor/AttributesTests.cs
// P3-C1: athlete attributes and a match started part-way.
//   - neutral (all 5) is exactly the game as it was: every multiplier 1
//     (the bundled replays, made before C1, still verify - BundledReplayTests)
//   - Speed raises top speed, Stamina cuts the drain, Power the drives'
//     pace, by the documented factors
//   - a start score and a head start each game; both recorded, and a
//     replay with attributes plays back bit-identically
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class AttributesTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheScalesAreNeutralAtFiveAndDocumentedAtTheEnds()
        {
            var n = new AthleteAttributes();
            Assert.That(n.IsNeutral, Is.True);
            Assert.That(n.SpeedScale, Is.EqualTo(1.0)); Assert.That(n.DrainScale, Is.EqualTo(1.0));
            Assert.That(n.PowerScale, Is.EqualTo(1.0)); Assert.That(n.ControlScale, Is.EqualTo(1.0));
            var top = new AthleteAttributes { Speed = 10, Stamina = 10, Power = 10, Control = 10 };
            Assert.That(top.SpeedScale, Is.EqualTo(1.09).Within(1e-12)); Assert.That(top.DrainScale, Is.EqualTo(0.7).Within(1e-12));
            Assert.That(top.PowerScale, Is.EqualTo(1.09).Within(1e-12)); Assert.That(top.ControlScale, Is.EqualTo(0.5).Within(1e-12));
            var low = new AthleteAttributes { Speed = 0, Stamina = 0, Power = 0, Control = 0 };
            Assert.That(low.SpeedScale, Is.EqualTo(0.91).Within(1e-12)); Assert.That(low.ControlScale, Is.EqualTo(1.5).Within(1e-12));
            Assert.That(new AthleteAttributes { Speed = 7.5 }.SpeedScale, Is.EqualTo(1.045).Within(1e-12), "a straight line between");
        }

        static double TopSpeed(AthleteAttributes a)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            var p = new TestPlayer(new V3(0, 0, 8));
            p.ArcadeMovement = true;
            p.Attributes.CopyFrom(a);
            var intent = new InputIntent();
            intent.Move.X = 0; intent.Move.Z = -1; intent.MoveMagnitude = 1;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int i = 0; i < 120; i++) PlayerMovement.Step(p, intent, null, dt);   // half a second: flat out
            return p.Speed;
        }

        [Test] public void SpeedRaisesTopSpeed()
        {
            double baseline = TopSpeed(new AthleteAttributes()), fast = TopSpeed(new AthleteAttributes { Speed = 10 });
            Assert.That(fast / baseline, Is.EqualTo(1.09).Within(0.002));
        }

        [Test] public void StaminaCutsTheDrain()
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            double Drain(double stamina)
            {
                var p = new TestPlayer();
                p.Attributes.Stamina = stamina;
                p.Speed = PhysicsConfig.Current.Player.MaxSpeed;
                Fatigue.Update(p, true, 1.0);
                return 1 - p.Stamina;
            }
            Assert.That(Drain(10) / Drain(5), Is.EqualTo(0.7).Within(1e-9));
            Assert.That(Drain(0) / Drain(5), Is.EqualTo(1.3).Within(1e-9));
        }

        /// <summary>Player 0's mean drive pace over a match with these attributes (an AI stands in for the person).</summary>
        static double DrivePace(AthleteAttributes a, out ReplayFile file)
        {
            var s = new ReplaySetup { Opponent = "retriever", Difficulty = 0.5f, GamesToWin = 1, BotSeed = 41, Attr0 = a };
            double sum = 0; int n = 0;
            file = ReplayTests.Play(s, new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.7), 17), null, (m, r) =>
            {
                foreach (var e in m.StepEvents)
                    if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 0 && e.Technique == "drive") { sum += e.BallSpeedOut; n++; }
            });
            Assert.That(n, Is.GreaterThan(10));
            return sum / n;
        }

        [Test] public void PowerHitsTheDrivesHarderAndTheReplayKeepsIt()
        {
            double baseline = DrivePace(new AthleteAttributes(), out _);
            double strong = DrivePace(new AthleteAttributes { Power = 10, Speed = 8, Stamina = 3, Control = 9 }, out ReplayFile f);
            UnityEngine.Debug.Log("[P3C1] drive pace " + baseline.ToString("0.00") + " -> " + strong.ToString("0.00") + " m/s at Power 10");
            Assert.That(strong / baseline, Is.InRange(1.03, 1.09));
            var back = ReplayTests.RoundTrip(f, out _);
            Assert.That(back.Setup.Attr0.Power, Is.EqualTo(10)); Assert.That(back.Setup.Attr0.Stamina, Is.EqualTo(3));
            var v = ReplaySim.Verify(back);
            Assert.That(v.Ok, Is.True, "a match with attributes replays bit-identically: " + v);
        }

        [Test] public void AMatchCanStartPartWayAndTheReplayKeepsIt()
        {
            var s = new ReplaySetup { Opponent = "grinder", Difficulty = 0.4f, GamesToWin = 2, BotSeed = 5, StartPoints0 = 6, StartPoints1 = 10, StartGames0 = 1, StartGames1 = 0, StartServer = 1 };
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            Assert.That(sim.Score.Points, Is.EqualTo(new[] { 6, 10 }), "6-10 down");
            Assert.That(sim.Score.Games, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(sim.Score.Server, Is.EqualTo(1), "they serve");
            var f = ReplayTests.Play(s, new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.7), 9));
            var back = ReplayTests.RoundTrip(f, out _);
            Assert.That(back.Setup.StartPoints1, Is.EqualTo(10)); Assert.That(back.Setup.StartServer, Is.EqualTo(1));
            Assert.That(ReplaySim.Verify(back).Ok, Is.True);
        }

        [Test] public void AHeadStartEveryGame()
        {
            ReplayMatch.Configure(new ReplaySetup { GamesToWin = 2, EachGame0 = 2, EachGame1 = 0 });
            var score = MatchScore.FromConfig(0);
            Assert.That(score.Points, Is.EqualTo(new[] { 2, 0 }));
            var o = new RallyOutcome { Kind = RallyEnd.NotUp, Winner = 1, Loser = 0 };
            while (!score.GameOver) score.Apply(o);
            score.NextGame();
            Assert.That(score.Points, Is.EqualTo(new[] { 2, 0 }), "the next game starts 2-0 too");
        }

        [Test] public void AnOlderReplayReadsAsNeutral()
        {
            Assert.That(new ReplaySetup().Attr0.IsNeutral, Is.True);
            Assert.That(new ReplaySetup().StartServer, Is.EqualTo(-1));
        }
    }
}
