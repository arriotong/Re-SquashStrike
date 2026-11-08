// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PlayerPerfTests.cs
// 2026-11-01 the real players' cost: the same rally played with them
// and with the classic figures, frame times compared (headless runs
// measure the CPU side - the pose, the motion capture, the IK).
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PlayerPerfTests
    {
        [UnityTest]
        public IEnumerator RealPlayersCostLittleMoreThanClassic()
        {
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var ms = new float[2]; var p95 = new float[2];
            for (int mode = 0; mode < 2; mode++)
            {
                LocalMatchTests.FreshProfile();
                Profile.ProfileStore.Current.Settings.RealPlayers = mode == 0;
                SceneManager.LoadScene("Match");
                yield return null; yield return null;
                var fe = Object.FindFirstObjectByType<UI.FrontEnd>();
                fe.StartMatch(MatchSettings.QuickGame());
                var r = Object.FindFirstObjectByType<MatchCourt>().Runner;
                r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("retriever").WithDifficulty(0.9), 11));
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 4f) yield return null;          // past the walk-on
                var frames = new List<float>();
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 8f) { frames.Add(Time.unscaledDeltaTime * 1000f); yield return null; }
                frames.Sort();
                float sum = 0; foreach (var f in frames) sum += f;
                ms[mode] = sum / frames.Count; p95[mode] = frames[(int)(frames.Count * 0.95f)];
                var bodies = Object.FindObjectsByType<Athlete.AthleteBody>(FindObjectsSortMode.None);
                if (mode == 0) Assert.That(bodies.Length, Is.EqualTo(2), "both athletes have real bodies");
                foreach (var b in bodies) Assert.That(b.Showing, Is.EqualTo(mode == 0));
                if (mode == 0) foreach (var b in bodies) Debug.Log("[Perf] " + b.ModelId + " stands " + b.Stature.ToString("0.00") + " m, drawn at " + Athlete.GateA.Crown + " m");
            }
            Debug.Log("[Perf] real players " + ms[0].ToString("0.00") + " ms (95%: " + p95[0].ToString("0.00") + "), classic " + ms[1].ToString("0.00") + " ms (95%: " + p95[1].ToString("0.00") + ")");
            Assert.That(ms[0], Is.LessThan(ms[1] + 3f), "the real players add under 3 ms a frame");
            Profile.ProfileStore.Current.Settings.RealPlayers = false;
        }
    }
}
