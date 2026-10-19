// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ReplayPlayTests.cs
// P2-M6 exit test, live: a real match in the scene - the runner's own
// step loop, frame sampling, the control mode switched mid-match - is
// recorded, saved to the replay library, read back from disk and played
// headless: every rally identical. A match left part-way is kept too.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Replays;
using SquashGame.UI;
using SquashSim;
using SquashSim.Replays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ReplayPlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        [UnityTest]
        public IEnumerator ALiveMatchIsSavedAndReplaysIdentically()
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
            Assert.That(ReplayLibrary.Entries.Count, Is.EqualTo(0));

            // ---- a match played out ----
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.3f, GamesToWin = 1, Scoring = "par" });
            var r = fe.Match;
            Assert.That(r.Recorder, Is.Not.Null, "a real match is recorded");
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 77));
            r.SetTimeScale(12);
            yield return Wait(4f, () => false);
            var s = ProfileStore.Current.Settings;
            s.Assisted = !s.Assisted; ProfileStore.Apply(s, false);           // the control mode, changed mid-match
            yield return Wait(90f, () => fe.Top is PostMatchScreen);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>());

            Assert.That(ReplayLibrary.Entries.Count, Is.EqualTo(1), "saved at full time");
            var e = ReplayLibrary.Latest;
            Assert.That(e.Mode, Is.EqualTo("quick"));
            Assert.That(e.Winner, Is.EqualTo(r.Sim.Score.MatchWinner));
            Assert.That(e.Rallies, Is.EqualTo(r.Sim.History.Count));
            Assert.That(e.Opponent, Does.StartWith("Retriever"));
            var file = ReplayLibrary.Load(e);
            Assert.That(ReplayMeta.Kit(file, 0), Is.Not.Null, "the kits as worn");
            var v = ReplaySim.Verify(file);
            Debug.Log("[P2M6] live match: " + e.Rallies + " rallies, " + e.Scores + ", " + new System.IO.FileInfo(ReplayLibrary.PathOf(e)).Length + " bytes on disk; replay " + v);
            Assert.That(v.Ok, Is.True, v.ToString());
            Assert.That(v.SameFingerprint, Is.True);

            // ---- a match left part-way: kept, marked, still replays ----
            fe.StartMatch(new MatchSettings { Opponent = "grinder", Difficulty = 0.6f, GamesToWin = 2, Scoring = "english" });
            fe.Match.SetAutopilot(new OpponentBrain(AiProfile.ByName("shotmaker").WithDifficulty(0.8), 5));
            fe.Match.SetTimeScale(12);
            yield return Wait(30f, () => fe.Match.Sim.History.Count >= 3);
            fe.QuitToMenu();
            yield return null;
            Assert.That(ReplayLibrary.Entries.Count, Is.EqualTo(2));
            var left = ReplayLibrary.Latest;
            Assert.That(left.Retired, Is.True);
            Assert.That(left.Rallies, Is.GreaterThanOrEqualTo(3));
            var v2 = ReplaySim.Verify(ReplayLibrary.Load(left));
            Assert.That(v2.Ok, Is.True, v2.ToString());

            // the menus' demo is never recorded
            Assert.That(fe.Match.Demo, Is.True);
            Assert.That(fe.Match.Recorder, Is.Null);
        }
    }
}
