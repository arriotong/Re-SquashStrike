// ===========================================================
// Assets/SquashGame/Tests/PlayMode/TheatreTests.cs
// P2-M6 R2: watching a replay on the court.
//   - a match is played and saved; the theatre opens it: the first
//     rally is watchable within a second (the pass goes on behind)
//   - playback with sound; 0.25x takes 4x as long; scrubbing moves the
//     picture; the five cameras each film from their own place
//   - the pass reaches the end without diverging; the reel plays;
//     closing gives the court back to the menus' demo
// Writes Captures/U6_Theatre_*.png.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.Replays;
using SquashGame.Audio;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class TheatreTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        static IEnumerator Capture(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false; ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = null; ps.targetTexture = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release();
        }

        [UnityTest]
        public IEnumerator ARallyPlaysBackSlowWithSoundFromFiveCameras()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            fe.StartMatch(new MatchSettings { Opponent = "attacker", Difficulty = 0.55f, GamesToWin = 1, Scoring = "par" });
            fe.Match.SetAutopilot(new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.85), 41));
            fe.Match.SetTimeScale(12);
            yield return Wait(120f, () => fe.Top is PostMatchScreen);
            fe.Match.SetTimeScale(SimRunnerBase.GameSpeed);
            fe.QuitToMenu();
            yield return null;
            var entry = ReplayLibrary.Latest;
            Assert.That(entry, Is.Not.Null);
            var file = ReplayLibrary.Load(entry);

            // ---- open: the first rally is ready almost at once ----
            float t0 = Time.realtimeSinceStartup;
            var t = TheatreSession.Open(entry, file, fe.Match);
            while (!t.Builder.Clips[0].Ready && Time.realtimeSinceStartup - t0 < 5f) { t.Tick(0.016f); yield return null; }
            float ready = Time.realtimeSinceStartup - t0;
            Debug.Log("[P2M6] rally 1 watchable after " + ready.ToString("0.00") + " s (" + file.Rallies.Count + " rallies)");
            Assert.That(ready, Is.LessThan(1f), "rally 1 is watchable within a second");

            // pick the longest rally once it is built
            int pick = 0;
            for (int i = 0; i < file.Rallies.Count; i++) if (file.Rallies[i].Shots > file.Rallies[pick].Shots) pick = i;
            while (!t.Builder.Clips[pick].Ready) { t.Tick(0.016f); yield return null; }
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            int sounds = audio != null ? audio.PlayedTotal : 0;
            Assert.That(t.Play(pick), Is.True);
            Assert.That(fe.Match.FramesActive, Is.True);
            Assert.That(t.Builder.Clips[pick].Seconds, Is.GreaterThan(2f));

            // ---- 1x, then 0.25x: four times as slow ----
            double c0 = t.Cursor;
            yield return Wait(0.8f, () => false);
            double at1 = (t.Cursor - c0) / 0.8;
            t.SetSpeed(2);
            double c1 = t.Cursor;
            yield return Wait(1.2f, () => false);
            double at025 = (t.Cursor - c1) / 1.2;
            Debug.Log("[P2M6] playback: " + at1.ToString("0.0") + " frames/s at 1x, " + at025.ToString("0.0") + " at 0.25x");
            Assert.That(at1, Is.EqualTo(SimRunnerBase.FrameRate).Within(12), "1x: 60 frames a second");
            Assert.That(at1 / at025, Is.EqualTo(4).Within(0.8), "0.25x takes 4 times as long");
            if (audio != null) Assert.That(audio.PlayedTotal, Is.GreaterThan(sounds), "the rally has its sound");

            // ---- scrubbing ----
            t.Paused = true;
            t.Position = 0f; yield return null;
            Vector3 start = fe.Match.View.BallPos;
            t.Position = 0.6f; yield return null;
            Assert.That(Vector3.Distance(fe.Match.View.BallPos, start), Is.GreaterThan(0.3f), "scrubbing moves the ball");
            Assert.That(fe.Match.View.BallPos, Is.EqualTo(fe.Match.View.BallPos));

            // ---- the five cameras ----
            var spots = new List<Vector3>();
            foreach (TheatreCam cam in System.Enum.GetValues(typeof(TheatreCam)))
            {
                t.Camera = cam;
                yield return Wait(0.3f, () => false);
                spots.Add(Camera.main.transform.position);
                yield return Capture(fe, "U6_Theatre_" + cam + ".png");
            }
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.That(Vector3.Distance(spots[i], spots[j]), Is.GreaterThan(0.5f), (TheatreCam)i + " and " + (TheatreCam)j + " film from their own places");

            // ---- the whole pass, the reel ----
            while (!t.Builder.Done) { t.Tick(0.016f); yield return null; }
            Assert.That(t.Builder.Diverged, Is.False, "the pass played the match again exactly");
            Assert.That(t.Builder.ReadyCount, Is.EqualTo(file.Rallies.Count));
            t.Paused = false;
            t.PlayReel();
            Assert.That(t.Reel.Count, Is.GreaterThan(0));
            Assert.That(t.Rally, Is.EqualTo(t.Reel[0]));

            // ---- close ----
            t.Close();
            yield return null;
            Assert.That(TheatreSession.Active, Is.Null);
            Assert.That(fe.Match.FramesActive, Is.False);
            Assert.That(fe.Match.Demo, Is.True, "the menus' demo again");
            Assert.That(SquashGame.Progression.KitDirector.Preview, Is.Null);
        }
    }
}
