// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PlaytestPlayTests.cs
// The playtest recorder (2026-10-30) in the real scene: a Quick Match
// played by an autopilot with the recorder on, F8 pressed in the middle
// of it - the session file has the session, the screens, the match, your
// shots (where each died), the rallies, the felt-bad mark with its last
// shot, and the coins the match paid.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PlaytestPlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        [TearDown] public void Off() { Playtest.Close(); Playtest.Enabled = false; }

        [UnityTest, Timeout(300000)]
        public IEnumerator AMatchIsRecordedWithAFeltBadMark()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            if (Directory.Exists(Playtest.Folder)) Directory.Delete(Playtest.Folder, true);
            Playtest.Enabled = true;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.3f, GamesToWin = 1, Scoring = "par", WarmUp = false });
            var r = fe.Match;
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 5));
            r.SetTimeScale(10);
            yield return Wait(60f, () => r.Sim.History.Count >= 4);
            Press(kb.f8Key); yield return null; Release(kb.f8Key); yield return null;     // that felt wrong
            Assert.That(fe.ToastLog.Exists(t => t.Contains("Marked")), Is.True, "a thank-you");
            yield return Wait(120f, () => fe.Top is PostMatchScreen);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>());
            yield return null; yield return null;

            Assert.That(Playtest.FilePath, Is.Not.Null);
            Playtest.Close();
            var lines = File.ReadAllLines(Playtest.FilePath);
            string all = string.Join("\n", lines);
            Debug.Log("[Playtest] " + lines.Length + " lines in " + Playtest.FilePath);
            foreach (var ev in new[] { "\"ev\":\"session\"", "\"ev\":\"screen\"", "\"ev\":\"match\"", "\"ev\":\"shot\"", "\"ev\":\"rally\"", "\"ev\":\"felt_bad\"", "\"ev\":\"match_end\"", "\"ev\":\"coins\"" })
                Assert.That(all, Does.Contain(ev), ev);
            Assert.That(all, Does.Contain("\"where\":\"back_corner\"").Or.Contain("\"where\":\"middle\"").Or.Contain("\"where\":\"side\""), "where the shots died");
            Assert.That(System.Array.FindAll(lines, l => l.Contains("\"ev\":\"match\"")).Length, Is.EqualTo(1), "the match once");
            Assert.That(System.Array.FindAll(lines, l => l.Contains("\"ev\":\"match_end\"")).Length, Is.EqualTo(1), "its end once");
            var felt = System.Array.Find(lines, l => l.Contains("\"ev\":\"felt_bad\""));
            Assert.That(felt, Does.Contain("\"in_match\":true"));
            Assert.That(felt, Does.Contain("\"last_shot\""));
            foreach (var l in lines) Assert.That(l.StartsWith("{") && l.EndsWith("}"), Is.True, "one JSON object a line: " + l);
            File.Copy(Playtest.FilePath, Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "playtest-sample.jsonl")), true);
        }
    }
}
