// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ReplayScreensTests.cs
// P2-M6 R4: a match watched back entirely through the screens.
//   - full time -> Watch highlights: the reel plays in the theatre
//   - back to the match: the rally strip, a rally picked and watched;
//     speed, camera, the next rally, hiding the overlay
//   - the hub's REPLAYS tab -> the library -> pin -> open
//   - Target Arcade: a new best is kept; Watch best plays it back to
//     the same score
//   - every one of these within the choice budget
// Writes Captures/U6_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Replays;
using SquashGame.UI;
using SquashSim;
using SquashSim.Arcade;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ReplayScreensTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

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

        static void Budget(MenuScreen s, string name)
        {
            int n = ChoiceBudgetTests.Count(s);
            Debug.Log("[U6] " + name + ": " + n + " choices");
            Assert.That(n, Is.LessThanOrEqualTo(ChoiceBudgetTests.Budget), name);
        }

        [UnityTest]
        public IEnumerator AMatchWatchedBackThroughTheScreens()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            fe.StartMatch(new MatchSettings { Opponent = "shotmaker", Difficulty = 0.55f, GamesToWin = 1, Scoring = "par" });
            fe.Match.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.85), 19));
            fe.Match.SetTimeScale(12);
            yield return Wait(120f, () => fe.Top is PostMatchScreen);
            fe.Match.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>());

            // ---- full time: Watch highlights ----
            var watch = fe.Top.Find("Watch highlights");
            Assert.That(watch, Is.Not.Null, "full time offers the highlights");
            watch.Submit();
            yield return Wait(4f, () => fe.Top is TheatrePlayerScreen);
            Assert.That(fe.Top, Is.InstanceOf<TheatrePlayerScreen>(), "the reel plays");
            var t = TheatreSession.Active;
            Assert.That(t, Is.Not.Null);
            Assert.That(t.Reel, Is.Not.Null);
            yield return Wait(1.2f, () => false);
            yield return Capture(fe, "U6_Reel.png");

            // ---- back to the match: the rally strip ----
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<MatchReplayScreen>());
            yield return Wait(0.5f, () => false);
            Budget(fe.Top, "match replay");
            yield return Capture(fe, "U6_MatchReplay.png");
            var first = (RallyNode)fe.Top.Focused;
            yield return Tap(kb.rightArrowKey);
            yield return Tap(kb.rightArrowKey);
            var picked = fe.Top.Focused as RallyNode;
            Assert.That(picked, Is.Not.Null);
            Assert.That(picked.Index, Is.Not.EqualTo(first.Index), "the strip moves");
            while (!t.Builder.Clips[picked.Index].Ready) yield return null;
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<TheatrePlayerScreen>());
            Assert.That(t.Rally, Is.EqualTo(picked.Index));
            Assert.That(t.Reel, Is.Null, "one rally, not the reel");
            yield return Wait(0.8f, () => false);
            yield return Tap(kb.fKey);                                         // X: speed
            Assert.That(t.Speed, Is.EqualTo(0.5));
            var cam = t.Camera;
            yield return Tap(kb.rKey);                                         // Y: camera
            Assert.That(t.Camera, Is.Not.EqualTo(cam));
            yield return Tap(kb.spaceKey);                                     // pause
            Assert.That(t.Paused, Is.True);
            yield return Wait(0.6f, () => false);
            yield return Capture(fe, "U6_Player.png");
            yield return Tap(kb.hKey);
            Assert.That(((TheatrePlayerScreen)fe.Top).Hidden, Is.True, "a clean picture");
            yield return Tap(kb.hKey);
            yield return Tap(kb.eKey);                                         // the next rally
            Assert.That(t.Rally, Is.GreaterThan(picked.Index));
            yield return Tap(kb.escapeKey);
            yield return Tap(kb.escapeKey);
            yield return null;
            Assert.That(TheatreSession.Active, Is.Null, "closed");
            Assert.That(fe.Match.Demo, Is.True);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());

            // ---- the hub: REPLAYS -> the library ----
            yield return HubNav.Open(fe, kb, Tap, "REPLAYS", "LIBRARY");
            Assert.That(fe.Top, Is.InstanceOf<ReplayLibraryScreen>());
            yield return Wait(0.4f, () => false);
            Budget(fe.Top, "replay library");
            yield return Capture(fe, "U6_Library.png");
            yield return Tap(kb.fKey);                                         // X: pin
            yield return null; yield return null;
            Assert.That(ReplayLibrary.Latest.Pinned, Is.True);
            Assert.That(fe.Top, Is.InstanceOf<ReplayLibraryScreen>());
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<MatchReplayScreen>());
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<ReplayLibraryScreen>());
            Assert.That(TheatreSession.Active, Is.Null);
            yield return Tap(kb.escapeKey);
            var hub = (MainMenuScreen)fe.Top;
            Assert.That(hub.Tabs.Current, Is.EqualTo("REPLAYS"));
            yield return Capture(fe, "U6_Hub_REPLAYS.png");
        }

        [UnityTest]
        public IEnumerator AnArcadeBestPlaysBackToTheSameScore()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchChallenge("precision-easy");
            yield return Wait(5f, () => SceneManager.GetActiveScene().name == "WarmUpCourt" && Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var r = Object.FindFirstObjectByType<WarmUpCourt>().Arcade;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            r.Autopilot = new ArcadeBot(3);
            r.SetTimeScale(10);
            yield return Wait(60f, () => fe.Top is ResultsScreen);
            var res = (ResultsScreen)fe.Top;
            Assert.That(res.NewBest, Is.True, "a first play is a best");
            int score = r.Session.Score;
            Assert.That(ArcadeBests.Has("precision-easy"), Is.True, "kept");

            FrontEnd.WatchArcadeBest("precision-easy");
            yield return Wait(5f, () => Object.FindFirstObjectByType<WarmUpCourt>() != null && Object.FindFirstObjectByType<WarmUpCourt>().Arcade != r);
            yield return null; yield return null;
            var r2 = Object.FindFirstObjectByType<WarmUpCourt>().Arcade;
            fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(r2.Watching, Is.Not.Null, "watching the best");
            r2.SetTimeScale(10);
            yield return Wait(60f, () => fe.Top is ResultsScreen);
            var again = (ResultsScreen)fe.Top;
            Assert.That(again.IsReplay, Is.True);
            Assert.That(r2.Session.Score, Is.EqualTo(score), "played back to the same score");
            Assert.That(ProfileStore.Record("precision-easy").Best, Is.EqualTo(score), "a replay is not a new play");
            r2.SetTimeScale(SimRunnerBase.GameSpeed);
            yield return Capture(fe, "U6_ArcadeBest.png");
        }
    }
}
