// ===========================================================
// Assets/SquashSim.Testing/Editor/ReplayTests.cs
// P2-M6 exit test: every saved replay re-simulates bit-identically.
//   - matches are played the way the game plays them: built by
//     ReplayMatch, athlete 0 steered through a "live" intent (an AI
//     stands in for the person, updating it at a 60 Hz frame rate),
//     recorded by MatchRecorder
//   - written to bytes, read back, played again headless: every rally's
//     checksum, the step count and the winner match
//   - two players, the next-game command, Assisted switching mid-match
//   - the file's size, the sim fingerprint, a tampered file diverging
// ===========================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class ReplayTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>Play a recorded match. Person-steered athletes get an AI writing into their live intent once per frame
        /// (every <paramref name="frameSteps"/> steps), as a person's device is sampled once per frame.</summary>
        internal static ReplayFile Play(ReplaySetup s, IAthleteBrain p0, IAthleteBrain p1 = null, Action<MatchSimulation, MatchRecorder> between = null,
                               int frameSteps = 4, double maxSeconds = 1500)
        {
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var live = new[] { new InputIntent(), s.TwoPlayer ? new InputIntent() : null };
            var rec = new MatchRecorder(sim, s, live, "test", 0);
            p0 = new PersonStandIn(p0);
            if (p1 != null) p1 = new PersonStandIn(p1);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            long max = (long)(maxSeconds / dt);
            while (!rec.Finished && sim.StepCount < max)
            {
                between?.Invoke(sim, rec);
                if (sim.StepCount % frameSteps == 0)
                {
                    p0.Fill(live[0], sim, 0, dt * frameSteps);
                    if (p1 != null) p1.Fill(live[1], sim, 1, dt * frameSteps);
                }
                rec.BeforeStep();
                sim.Step(dt);
                rec.AfterStep();
            }
            rec.Finish(true);
            Assert.That(rec.Invalid, Is.False);
            return rec.File;
        }

        internal static ReplayFile RoundTrip(ReplayFile f, out int bytes)
        {
            var b = f.ToBytes();
            bytes = b.Length;
            return ReplayFile.FromBytes(b);
        }

        [Test] public void EveryPersonalityAndLevelReplaysIdentically()
        {
            string[] opps = { "retriever", "attacker", "shotmaker", "grinder" };
            var levels = GameTuning.Ai.Levels;
            int rallies = 0, n = 0;
            foreach (var o in opps)
                for (int l = 0; l < levels.Length; l++, n++)
                {
                    var s = new ReplaySetup { Opponent = o, Difficulty = (float)levels[l], GamesToWin = 1, Scoring = n % 2 == 0 ? "par" : "english", BotSeed = (uint)(1000 + n * 77) };
                    bool assisted = n % 2 == 1;
                    var f = Play(s, new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.55 + 0.1 * l), (uint)(31 + n)),
                                 between: (m, r) => m.Players[0].Arcade.Assisted = assisted);
                    var back = RoundTrip(f, out int bytes);
                    var v = ReplaySim.Verify(back);
                    Assert.That(f.Winner, Is.GreaterThanOrEqualTo(0), o + " " + l + ": the match was played out");
                    Assert.That(v.Ok, Is.True, o + " level " + l + ": " + v);
                    Assert.That(v.SameFingerprint, Is.True);
                    rallies += v.Rallies;
                }
            UnityEngine.Debug.Log("[P2M6] " + n + " matches, " + rallies + " rallies replayed bit-identically");
        }

        [Test] public void TwoPlayersBestOfThreeWithNextGameAndAssistedSwitching()
        {
            var s = new ReplaySetup { TwoPlayer = true, GamesToWin = 2, Scoring = "par", BotSeed = 7 };
            int nexts = 0;
            var f = Play(s, new OpponentBrain(AiProfile.ByName("shotmaker").WithDifficulty(0.7), 3), new OpponentBrain(AiProfile.ByName("grinder").WithDifficulty(0.7), 4),
                (m, r) =>
                {
                    m.Players[0].Arcade.Assisted = (m.History.Count / 5) % 2 == 0;           // the control mode changed now and then (pause -> settings)
                    m.Players[1].Arcade.Assisted = true;
                    m.Players[0].Arcade.ShotLines = true; m.Players[1].Arcade.ShotLines = (m.History.Count / 3) % 2 == 0;   // shot lines, recorded too
                    if (m.Phase == MatchPhase.GameOver && m.PhaseTime > 1.0) { r.Command(ReplayCommand.NextGame); m.NextGame(); nexts++; }   // "play now"
                });
            Assert.That(nexts, Is.GreaterThan(0), "a break was cut short");
            Assert.That(f.Tracks[1], Is.Not.Null, "both people recorded");
            var back = RoundTrip(f, out int bytes);
            var v = ReplaySim.Verify(back);
            Assert.That(v.Ok, Is.True, v.ToString());
            Assert.That(back.Commands.Count, Is.EqualTo(nexts));
            double seconds = back.TotalSteps * PhysicsConfig.Current.Sim.FixedDt;
            UnityEngine.Debug.Log("[P2M6] two-player Bo3: " + back.Rallies.Count + " rallies, " + seconds.ToString("0") + " s, " + bytes + " bytes");
        }

        [Test] public void ATenMinuteMatchFitsIn150KB()
        {
            var s = new ReplaySetup { Opponent = "retriever", Difficulty = 0.5f, GamesToWin = 2, BotSeed = 99 };
            var f = Play(s, new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.7), 12));
            RoundTrip(f, out int bytes);
            double seconds = f.TotalSteps * PhysicsConfig.Current.Sim.FixedDt;
            double tenMinutes = bytes / seconds * 600;
            UnityEngine.Debug.Log("[P2M6] " + seconds.ToString("0") + " s of match in " + bytes + " bytes: " + (bytes / seconds).ToString("0") + " B/s, a 10-minute match ~" + (tenMinutes / 1024).ToString("0") + " KB");
            Assert.That(tenMinutes, Is.LessThan(150 * 1024));
        }

        [Test] public void TheFileKeepsItsSetupAndMeta()
        {
            var s = new ReplaySetup { Opponent = "wallbot", GamesToWin = 1, Scoring = "english", WarmUp = false, BotSeed = 5 };
            var f = Play(s, new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.8), 2), maxSeconds: 20);   // left part-way
            f.Meta["p1"] = "TESTER"; f.Meta["mode"] = "quick";
            var back = RoundTrip(f, out _);
            Assert.That(back.Meta["p1"], Is.EqualTo("TESTER"));
            Assert.That(back.Setup.Opponent, Is.EqualTo("wallbot"));
            Assert.That(back.Setup.Scoring, Is.EqualTo("english"));
            Assert.That(back.Setup.BotSeed, Is.EqualTo(5u));
            Assert.That(back.Setup.Lets, Is.True, "the Lets setting (format 2)");
            Assert.That(back.Rallies.Count, Is.EqualTo(f.Rallies.Count));
            Assert.That(back.Retired, Is.True, "stopped before the end: left");
            Assert.That(ReplaySim.Verify(back).Ok, Is.True, "a match left part-way replays too");
            Assert.Throws<System.IO.InvalidDataException>(() => ReplayFile.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        }

        [Test] public void TheFingerprintChangesWithAnyTuningNumber()
        {
            ulong game = SimFingerprint.Current;
            PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile();
            Assert.That(SimFingerprint.Of(PhysicsConfig.Current), Is.EqualTo(game), "the game profile");
            PhysicsConfig.Current.Player.MaxSpeed += 0.01;
            Assert.That(SimFingerprint.Of(PhysicsConfig.Current), Is.Not.EqualTo(game), "one number changed");
        }

        [Test] public void ATamperedFileDivergesAtTheRightRally()
        {
            var s = new ReplaySetup { Opponent = "attacker", Difficulty = 0.5f, GamesToWin = 1, BotSeed = 21 };
            var f = Play(s, new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), 8));
            Assert.That(f.Rallies.Count, Is.GreaterThan(4));
            // steer athlete 0 hard right for a run in the middle of rally 4 (where the athletes stand is in the checksum)
            long target = (f.Rallies[2].EndStep + f.Rallies[3].EndStep) / 2, at = 0;
            var track = f.Tracks[0];
            for (int i = 0; i < track.Count; i++)
            {
                if (at + track[i].Count > target)
                {
                    var r = track[i]; r.Frame.MoveX = 10000; r.Frame.MoveZ = 0; r.Frame.MoveMag = 10000; track[i] = r;
                    break;
                }
                at += track[i].Count;
            }
            var v = ReplaySim.Verify(ReplayFile.FromBytes(f.ToBytes()));
            Assert.That(v.Ok, Is.False, "the result no longer holds");
            Assert.That(v.FirstMismatch, Is.GreaterThanOrEqualTo(3), "rallies before it still match");
        }
    }
}
