// ===========================================================
// Assets/SquashSim.Testing/Editor/ArcadeSessionTests.cs
// P2-M2: the Target Arcade and the drills, headless.
//   - targets judge their geometry (rings, precision thirds, zones)
//   - every challenge plays to its end with the human-style bot, each
//     by its own rule (clock, ball count, first miss, the rally ends)
//   - the combo multiplier; a serve under the line scores nothing
//   - same seed + the recorded inputs = the same score (replays)
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Arcade;

namespace SquashSim.Tests
{
    public class ArcadeSessionTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static ArcadeSession Play(Challenge c, uint seed, uint botSeed, InputLog log = null)
        {
            var s = new ArcadeSession(c, seed);
            var bot = new ArcadeBot(botSeed);
            var intent = new InputIntent();
            int guard = 0;
            while (!s.Over && guard++ < 240 * 300)
            {
                bot.Fill(intent, s, Dt);
                if (log != null) log.Record(intent);
                s.Step(intent, Dt);
            }
            return s;
        }

        [Test] public void TargetsJudgeTheirGeometry()
        {
            var ring = Target.Ring(1.0, 2.0, 0.6);
            Assert.That(ring.InRing(new V3(1.3, 2.2, 0)), Is.True);
            Assert.That(ring.InRing(new V3(1.7, 2.0, 0)), Is.False);
            Assert.That(ring.PrecisionRing(new V3(1.1, 2.0, 0)), Is.EqualTo(3), "inner third");
            Assert.That(ring.PrecisionRing(new V3(1.3, 2.0, 0)), Is.EqualTo(2), "middle third");
            Assert.That(ring.PrecisionRing(new V3(1.55, 2.0, 0)), Is.EqualTo(1), "outer third");
            Assert.That(ring.PrecisionRing(new V3(1.7, 2.0, 0)), Is.EqualTo(0));
            var zone = Target.Zone(2.0, 3.2, 8.0, 9.75);
            Assert.That(zone.InZone(new V3(2.5, 0, 9.0)), Is.True);
            Assert.That(zone.InZone(new V3(1.5, 0, 9.0)), Is.False);
            Assert.That(ArcadeSession.ComboMultiplier(1), Is.EqualTo(1));
            Assert.That(ArcadeSession.ComboMultiplier(3), Is.EqualTo(1.5));
            Assert.That(ArcadeSession.ComboMultiplier(5), Is.EqualTo(2));
            Assert.That(ArcadeSession.ComboMultiplier(9), Is.EqualTo(3));
        }

        [Test] public void EveryChallengePlaysToItsEnd()
        {
            foreach (var c in Challenges.All())
            {
                var s = Play(c, 4242, 7);
                UnityEngine.Debug.Log("[Arcade] " + c.Id + ": score " + s.Score + ", hits " + s.Hits + "/" + s.Attempts + ", " + s.Stars + " stars, " + s.Time.ToString("0") + " s");
                Assert.That(s.Over, Is.True, c.Id + " should end");
                switch (c.Mode)
                {
                    case ArcadeMode.TimeAttack: Assert.That(s.Time, Is.EqualTo(c.Seconds).Within(0.01), c.Id + ": the clock ends it"); break;
                    case ArcadeMode.Precision: case ArcadeMode.ShotMenu: case ArcadeMode.ServeTargets:
                        Assert.That(s.BallsUsed, Is.EqualTo(c.Balls), c.Id + ": every ball played"); break;
                    case ArcadeMode.Streak: Assert.That(s.Score, Is.EqualTo(s.Hits), c.Id + ": the streak is the score"); break;
                }
                bool luck = c.Mode == ArcadeMode.Streak || c.Mode == ArcadeMode.WallRally || c.Drill == DrillKind.NickHunt;   // one miss can end these
                if (!luck)
                    Assert.That(s.Hits, Is.GreaterThan(0), c.Id + ": the bot should score something");
            }
        }

        [Test] public void GhostingEndsOnTheClock()
        {
            var s = Play(Challenges.Find("drill-ghosting"), 1, 2);
            Assert.That(s.Time, Is.EqualTo(45).Within(0.01));
            Assert.That(s.Hits, Is.GreaterThan(5), "the spots get touched");
        }

        [Test] public void AServeUnderTheLineScoresNothing()
        {
            var s = new ArcadeSession(Challenges.Find("servetargets-easy"), 3);
            var it = new InputIntent();
            int faults = 0, guard = 0;
            bool held = false;
            while (!s.Over && guard++ < 240 * 120)
            {
                it.HasAimPoint = true; it.AimPointX = s.ServeSide * 0.4; it.AimPointY = 1.2;      // under the service line
                it.Hit.Pressed = false; it.Hit.Released = false;
                if (s.Serving && !held) { it.Hit.Pressed = true; it.Hit.Held = true; held = true; }
                else if (held) { it.Hit.Held = false; it.Hit.Released = true; held = false; }
                s.Step(it, Dt);
                foreach (var e in s.Events) if (e.Kind == ArcadeEventKind.Fault) faults++;
            }
            Assert.That(s.Over, Is.True);
            Assert.That(faults, Is.EqualTo(10), "every serve under the line is a fault");
            Assert.That(s.Score, Is.EqualTo(0));
        }

        [Test] public void TheSameInputsGiveTheSameScore()
        {
            foreach (var id in new[] { "timeattack-medium", "shotmenu-hard", "servetargets-easy", "wallrally-easy" })
            {
                var c = Challenges.Find(id);
                var log = new InputLog { ChallengeId = id, Seed = 777 };
                var played = Play(c, 777, 31, log);
                var replayed = InputLog.Replay(log, c, Dt);
                UnityEngine.Debug.Log("[Arcade] replay " + id + ": " + played.Score + " vs " + replayed.Score + ", steps " + played.StepCount + " vs " + replayed.StepCount);
                Assert.That(replayed.Score, Is.EqualTo(played.Score), id);
                Assert.That(replayed.Hits, Is.EqualTo(played.Hits), id);
                Assert.That(replayed.StepCount, Is.EqualTo(played.StepCount), id);
                Assert.That(replayed.Over, Is.True, id);
            }
        }

        [Test] public void ADifferentSeedGivesDifferentFeeds()
        {
            var c = Challenges.Find("precision-easy");
            var a = new ArcadeSession(c, 1); var b = new ArcadeSession(c, 2);
            var it = new InputIntent();
            for (int i = 0; i < 240 * 2; i++) { a.Step(it, Dt); b.Step(it, Dt); }
            Assert.That(a.Targets.Count, Is.EqualTo(1));
            Assert.That(a.Targets[0].X, Is.Not.EqualTo(b.Targets[0].X), "another target");
        }
    }
}
