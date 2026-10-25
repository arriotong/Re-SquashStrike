// ===========================================================
// Assets/SquashGame/Tests/PlayMode/OnlinePlayTests.cs
// P2-M8 online, in the game: the scene's own MatchRunner plays an online
// match as athlete 1 (the joiner) against a second LockstepSession - the
// "other machine" - over an in-memory link with latency and jitter, an AI
// at the controls on both sides. Both machines end on the same rallies
// and winner; "you" is athlete 1 throughout (names, kit, stats); the
// full-time screen comes up; the replay is kept as an online match from
// your side and plays back identically.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Progression;
using SquashGame.Replays;
using SquashGame.UI;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class OnlinePlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator AnOnlineMatchAsTheJoinerPlaysOutTheSameOnBothMachines()
        {
            LocalMatchTests.FreshProfile();
            ReplayLibrary.Reset();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            const float Speed = 10f;
            double clock = 0;
            LoopbackTransport.Pair(() => clock, 0.04, 0.02, 3, out var mine, out var theirs);
            ReplaySetup Setup() { return new ReplaySetup { TwoPlayer = true, GamesToWin = 1, Scoring = "par", BotSeed = 4242, FirstServer = 0, Lets = true }; }
            var oppKit = JsonUtility.ToJson(Cosmetics.Signature("grinder"));
            fe.StartOnline(new MatchStartInfo { MatchId = 7, Slot = 1, Delay = 4, Court = "arena", OppName = "Rival", OppKit = oppKit, Setup = Setup() }, mine);
            var r = fe.Match;
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), 21));
            r.SetTimeScale(SimRunnerBase.GameSpeed * Speed);
            var other = new LockstepSession(new MatchStartInfo { Slot = 0, Delay = 4, Setup = Setup() }, theirs);
            var ai = new PersonStandIn(new OpponentBrain(AiProfile.ByName("grinder").WithDifficulty(0.8), 22));
            var live = new InputIntent();

            Assert.That(r.OnlineMatch, Is.Not.Null);
            Assert.That(r.HumanIndex, Is.EqualTo(1), "the joiner plays athlete 1");
            Assert.That(r.Names[1], Is.EqualTo("YOU")); Assert.That(r.Names[0], Is.EqualTo("RIVAL"));
            Assert.That(Courts.CurrentId, Is.EqualTo("arena"));
            Assert.That(r.LocalSource(1), Is.Not.Null); Assert.That(r.LocalSource(0), Is.Null, "your input steers athlete 1 only");
            Assert.That(KitDirector.For(r, 1).Shirt, Is.EqualTo(KitDirector.You().Shirt), "your kit on your athlete");

            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 240f && !(fe.Top is PostMatchScreen))
            {
                float dt = Time.unscaledDeltaTime * Speed;
                clock += dt;
                while (theirs.TryReceive(out var m)) other.Handle(m);
                if (!other.Finished) other.Pump(dt, live, false, false, x => ai.Fill(x, other.Sim, 0, LockstepSession.TickSeconds));
                yield return null;
            }
            // the other machine finishes its last tick too
            yield return Wait(10f, () => { clock += 0.1; while (theirs.TryReceive(out var m)) other.Handle(m); other.Pump(0.1, live, false, false); return other.Finished; });
            r.SetTimeScale(SimRunnerBase.GameSpeed);

            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>(), "full time");
            var s = r.OnlineMatch;
            Assert.That(s.Finished && other.Finished, Is.True);
            Assert.That(s.Desynced || other.Desynced, Is.False, "no desync");
            Assert.That(s.EndReason, Is.EqualTo(""));
            Assert.That(s.RallyHashes, Is.EqualTo(other.RallyHashes), "every rally the same on both machines");
            Assert.That(s.Winner, Is.EqualTo(other.Winner));
            Assert.That(r.Sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            var facts = fe.Stats.Facts(r);
            Assert.That(facts.Won, Is.EqualTo(s.Winner == 1), "the stats are yours (athlete 1)");
            Assert.That(facts.Online, Is.True);
            Assert.That(facts.RalliesWon, Is.EqualTo(fe.Stats.Players[1].Rallies));

            var e = ReplayLibrary.Latest;
            Assert.That(e, Is.Not.Null, "the match is kept");
            Assert.That(e.Mode, Is.EqualTo("online"));
            Assert.That(e.You, Is.EqualTo(1));
            Assert.That(e.Result, Is.EqualTo(s.Winner == 1 ? "W" : "L"));
            Assert.That(e.Opponent, Is.EqualTo("Rival"));
            var file = ReplayLibrary.Load(e);
            Assert.That(ReplayMeta.Kit(file, 1).Shirt, Is.EqualTo(KitDirector.You().Shirt), "your kit recorded on athlete 1");
            var v = ReplaySim.Verify(file);
            Assert.That(v.Ok, Is.True, v.ToString());
            Debug.Log("[P2M8] in the game, as athlete 1: " + s.RallyHashes.Count + " rallies, " + e.Scores + ", winner " + s.Winner + ", replay " + v);

            fe.QuitToMenu();
            yield return null;
            Assert.That(r.OnlineMatch, Is.Null, "back to the menus");
            Assert.That(r.Demo, Is.True);
        }
    }
}
