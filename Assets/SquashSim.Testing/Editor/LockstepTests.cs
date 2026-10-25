// ===========================================================
// Assets/SquashSim.Testing/Editor/LockstepTests.cs
// P2-M8 online: two machines in lockstep, in memory.
//   - two LockstepSessions joined by a LoopbackTransport (latency and
//     jitter on a clock the test drives), each with an AI standing in
//     for its person, play a whole best-of-3
//   - at 0, 120 and 300 ms each way: both machines end on the same step
//     with the same rally hashes and the same winner (no desync), and
//     the match each recorded verifies as a replay
//   - a "play now" command reaches both machines on the same tick
//   - the messages read back as written
// ===========================================================

using System;
using NUnit.Framework;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class LockstepTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        sealed class Machine
        {
            public LockstepSession S;
            public LoopbackTransport T;
            public InputIntent Live = new InputIntent();
            public IAthleteBrain Ai;
            public void Pump(double dt, bool playNow = false)
            {
                while (T.TryReceive(out var m)) S.Handle(m);
                if (playNow && S.Sim.Phase == MatchPhase.GameOver && S.Sim.PhaseTime > 1.0) S.Command(ReplayCommand.NextGame);
                S.Pump(dt, Live, false, false, live => Ai.Fill(live, S.Sim, S.Me, LockstepSession.TickSeconds));
            }
        }

        static (Machine a, Machine b, double waited) Play(double latency, double jitter, int delay, bool playNow = false, double maxSeconds = 3600)
        {
            double now = 0;
            LoopbackTransport.Pair(() => now, latency, jitter, 7, out var ta, out var tb);
            var setup = new ReplaySetup { TwoPlayer = true, GamesToWin = 2, Scoring = "par", BotSeed = 77 };
            var a = new Machine { T = ta, Ai = new PersonStandIn(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.7), 11)) };
            var b = new Machine { T = tb, Ai = new PersonStandIn(new OpponentBrain(AiProfile.ByName("grinder").WithDifficulty(0.7), 12)) };
            a.S = new LockstepSession(new MatchStartInfo { Slot = 0, Delay = delay, Setup = setup }, ta);
            b.S = new LockstepSession(new MatchStartInfo { Slot = 1, Delay = delay, Setup = new ReplaySetup { TwoPlayer = true, GamesToWin = 2, Scoring = "par", BotSeed = 77 } }, tb);
            const double dt = 1.0 / 120;
            double waited = 0;
            while (!(a.S.Finished && b.S.Finished) && now < maxSeconds)
            {
                now += dt;
                a.Pump(dt, playNow); b.Pump(dt, playNow);
                waited = Math.Max(waited, Math.Max(a.S.WaitingSeconds, b.S.WaitingSeconds));
            }
            return (a, b, waited);
        }

        [TestCase(0.0, 0.0, 2)]
        [TestCase(0.12, 0.02, 5)]
        [TestCase(0.30, 0.05, 8)]
        public void TwoMachinesPlayTheSameMatch(double latency, double jitter, int delay)
        {
            var (a, b, waited) = Play(latency, jitter, delay);
            Assert.That(a.S.Finished && b.S.Finished, Is.True, "both finished: A tick " + a.S.Tick + " sent " + a.S.Sent + " " + a.S.Sim.Phase + " " + a.S.Sim.Score.Points[0] + "-" + a.S.Sim.Score.Points[1] +
                " games " + a.S.Sim.Score.Games[0] + "-" + a.S.Sim.Score.Games[1] + " step " + a.S.Sim.StepCount + " server " + a.S.Sim.Score.Server + " charging " + a.S.Sim.Players[a.S.Sim.Score.Server].Arcade.ServeCharging + " phaseT " + a.S.Sim.PhaseTime.ToString("0.00") +
                " heldA " + a.S.Sim.Intents[0].Hit.Held + " heldB " + a.S.Sim.Intents[1].Hit.Held + " arcade " + a.S.Sim.Players[a.S.Sim.Score.Server].ArcadeHitEnabled + " | B tick " + b.S.Tick + " sent " + b.S.Sent + " " + b.S.Sim.Phase);
            Assert.That(a.S.Desynced || b.S.Desynced, Is.False, "no desync");
            Assert.That(a.S.EndReason, Is.EqualTo("")); Assert.That(b.S.EndReason, Is.EqualTo(""));
            Assert.That(a.S.Sim.StepCount, Is.EqualTo(b.S.Sim.StepCount), "the same last step");
            Assert.That(a.S.RallyHashes, Is.EqualTo(b.S.RallyHashes), "every rally the same");
            Assert.That(a.S.Winner, Is.EqualTo(b.S.Winner)); Assert.That(a.S.Winner, Is.GreaterThanOrEqualTo(0));
            var v = ReplaySim.Verify(ReplayFile.FromBytes(a.S.File.ToBytes()));
            Assert.That(v.Ok, Is.True, "the match records as a replay: " + v);
            UnityEngine.Debug.Log("[P2M8] " + (latency * 1000).ToString("0") + " ms, delay " + delay + ": " + a.S.RallyHashes.Count + " rallies, " +
                                  (a.S.Sim.StepCount * PhysicsConfig.Current.Sim.FixedDt).ToString("0") + " s of sim, longest wait " + (waited * 1000).ToString("0") + " ms");
        }

        [Test] public void PlayNowReachesBothMachines()
        {
            var (a, b, _) = Play(0.08, 0.02, 4, playNow: true);
            Assert.That(a.S.Desynced || b.S.Desynced, Is.False);
            Assert.That(a.S.File.Commands.Count, Is.GreaterThan(0), "the break was cut short");
            Assert.That(a.S.File.Commands.ConvertAll(c => c.Step), Is.EqualTo(b.S.File.Commands.ConvertAll(c => c.Step)), "on the same step on both");
            Assert.That(a.S.RallyHashes, Is.EqualTo(b.S.RallyHashes));
        }

        [Test] public void MessagesReadBackAsWritten()
        {
            var info = new MatchStartInfo { MatchId = 42, Slot = 1, Delay = 5, Ranked = true, Court = "arena", OppName = "Rival", OppKit = "{}", OppRating = 1180, MyRating = 1010,
                Setup = new ReplaySetup { TwoPlayer = true, Scoring = "english", GamesToWin = 3, BotSeed = 99, FirstServer = 1, Lets = false } };
            var back = NetMessages.ReadMatchStart(NetMessages.MatchStart(info));
            Assert.That(back.MatchId, Is.EqualTo(42)); Assert.That(back.Slot, Is.EqualTo(1)); Assert.That(back.Delay, Is.EqualTo(5)); Assert.That(back.Ranked, Is.True);
            Assert.That(back.Court, Is.EqualTo("arena")); Assert.That(back.OppName, Is.EqualTo("Rival")); Assert.That(back.OppRating, Is.EqualTo(1180));
            Assert.That(back.Setup.Scoring, Is.EqualTo("english")); Assert.That(back.Setup.GamesToWin, Is.EqualTo(3)); Assert.That(back.Setup.BotSeed, Is.EqualTo(99u));
            Assert.That(back.Setup.FirstServer, Is.EqualTo(1)); Assert.That(back.Setup.Lets, Is.False);
            var f = new QFrame { MoveX = -1200, MoveZ = 300, MoveMag = 10000, RawX = 5, RawY = -7, AimX = 1500, AimY = 2400, HitMs = 230, TouchMs = 0, Flags = 0x2A05 };
            var r = NetMessages.Read(NetMessages.Input(123456789L, f));
            Assert.That(r.ReadInt64(), Is.EqualTo(123456789L));
            Assert.That(NetMessages.ReadFrame(r), Is.EqualTo(f));
            var end = NetMessages.ReadMatchEnd(NetMessages.MatchEnd(new MatchEndInfo { Winner = 1, Reason = "forfeit", Rated = true, RatingBefore = 1000, RatingAfter = 1016 }));
            Assert.That(end.Winner, Is.EqualTo(1)); Assert.That(end.Reason, Is.EqualTo("forfeit")); Assert.That(end.RatingAfter, Is.EqualTo(1016));
        }

        [Test] public void TheOtherLeavingEndsIt()
        {
            double now = 0;
            LoopbackTransport.Pair(() => now, 0.05, 0, 3, out var ta, out var tb);
            var s = new LockstepSession(new MatchStartInfo { Slot = 0, Delay = 3, Setup = new ReplaySetup { TwoPlayer = true } }, ta);
            var live = new InputIntent();
            for (int i = 0; i < 60; i++) { now += 1.0 / 60; s.Pump(1.0 / 60, live, false, false); }
            Assert.That(s.WaitingSeconds, Is.GreaterThan(0.5), "nobody on the other end: it waits");
            s.Handle(NetMessages.Simple(Msg.OpponentLeft));
            Assert.That(s.Finished, Is.True); Assert.That(s.EndReason, Is.EqualTo("left")); Assert.That(s.Winner, Is.EqualTo(0), "the one who stayed wins");
        }
    }
}
