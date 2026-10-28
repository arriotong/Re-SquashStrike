// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ArcadeTests.cs
// P2-M2: the Target Arcade and the Practice Court in the real scenes.
//   - main menu -> Target Arcade -> Time Attack -> Easy; the human-
//     style bot plays it (sped up); the results screen shows the score
//     and stars; the record is saved; Retry plays again
//   - a drill runs to its results; Quit goes back to the Practice menu
//   - free practice: Tab opens the Coach panel (the game waits),
//     changing the feed changes what the feeder sends
// Writes Captures/P2M2_UI_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
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
    public class ArcadeTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false;
            ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            ps.targetTexture = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        static IEnumerator Loaded(string scene)
        {
            yield return Wait(5f, () => SceneManager.GetActiveScene().name == scene && Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator TimeAttackFromTheMenuToTheResults()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Loaded("Match");
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "MODES", "TARGET ARCADE");
            Assert.That(fe.Top, Is.InstanceOf<TargetArcadeScreen>());
            yield return CaptureUI(fe, "P2M2_UI_1_TargetArcade.png");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Time Attack"));
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<LevelScreen>());
            yield return CaptureUI(fe, "P2M2_UI_2_Levels.png");
            yield return Tap(kb.enterKey);                                   // Easy

            yield return Loaded("WarmUpCourt");
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            ArcadeRunner r = court.Arcade;
            fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(r, Is.Not.Null, "the practice court runs the challenge");
            Assert.That(r.Challenge.Id, Is.EqualTo("timeattack-easy"));
            Assert.That(fe.Top, Is.Null, "straight into play");
            yield return Wait(2.5f, () => r.Session.Targets.Count > 0);
            yield return Wait(0.6f, () => false);                            // let the views draw it
            yield return CaptureUI(fe, "P2M2_UI_3_TimeAttack.png");
            var targets = Object.FindFirstObjectByType<TargetView>();
            Assert.That(targets.Showing, Is.GreaterThan(0), "the target is drawn");

            r.Autopilot = new ArcadeBot(5);
            r.SetTimeScale(10);
            yield return Wait(60f, () => fe.Top is ResultsScreen);
            Assert.That(fe.Top, Is.InstanceOf<ResultsScreen>(), "the results come up when the clock runs out");
            Debug.Log("[P2M2] time attack: score " + r.Session.Score + ", hits " + r.Session.Hits + "/" + r.Session.Attempts + ", " + r.Session.Stars +
                      " stars; targets drawn hit " + targets.HitsShown + ", missed " + targets.MissesShown);
            Assert.That(r.Session.Hits, Is.GreaterThan(0));
            Assert.That(targets.HitsShown, Is.EqualTo(r.Session.Hits), "every hit flashes");
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            yield return Wait(2.6f, () => false);                            // the score counts up, the stars pop in
            Assert.That(((ResultsScreen)fe.Top).StarsShown, Is.EqualTo(r.Session.Stars), "every star popped in");
            yield return CaptureUI(fe, "P2M2_UI_4_Results.png");
            var rec = ProfileStore.Record("timeattack-easy");
            Assert.That(rec, Is.Not.Null, "the record is saved");
            Assert.That(rec.Best, Is.EqualTo(r.Session.Score));
            Assert.That(r.Log.Frames.Count, Is.EqualTo((int)r.Session.StepCount), "every step's input is recorded");

            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Retry"));
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.Null);
            Assert.That(r.Session.Over, Is.False, "a new play");
            Assert.That(r.Session.Score, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ADrillRunsAndQuitGoesBackToThePracticeMenu()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchChallenge("drill-length");
            yield return Loaded("WarmUpCourt");
            var r = Object.FindFirstObjectByType<WarmUpCourt>().Arcade;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            r.Autopilot = new ArcadeBot(9);
            r.SetTimeScale(10);
            yield return Wait(60f, () => fe.Top is ResultsScreen);
            Assert.That(fe.Top, Is.InstanceOf<ResultsScreen>());
            Assert.That(r.Session.BallsUsed, Is.EqualTo(20));
            Debug.Log("[P2M2] length drill: " + r.Session.Hits + " / " + r.Session.Attempts);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            yield return HubNav.Seek(fe, "Practice menu", kb.leftArrowKey, kb.upArrowKey, Tap);
            yield return Tap(kb.enterKey);
            yield return Loaded("Match");
            fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.InstanceOf<PracticeScreen>(), "back on the Practice menu");
            Assert.That(fe.Top.Find("Length"), Is.Not.Null);
            yield return null; yield return null;
            Assert.That(((PracticeScreen)fe.Top).Tabs.Current, Is.EqualTo("DRILLS"), "on the drills");
            yield return CaptureUI(fe, "U4_Practice_Drills.png");
            yield return Tap(kb.qKey);
            yield return null;
            yield return CaptureUI(fe, "U4_Practice_Free.png");
        }

        [UnityTest]
        public IEnumerator TheCoachPanelChangesTheFeed()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            yield return Loaded("WarmUpCourt");
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(court.Runner, Is.Not.Null, "free practice is the lab");
            Assert.That(fe.Coach, Is.Not.Null);
            // Free play is the court's main mode: no feeds, the ball in your hand; a click drops and hits it.
            var free = court.Runner.Free;
            Assert.That(free, Is.Not.Null, "free play by default");
            Assert.That(free.InHand, Is.True);
            yield return null;
            Assert.That(fe.PracticeHud.HintText, Does.Contain("Ball in hand"));
            yield return CaptureUI(fe, "Practice_FreePlay.png");
            var mouse = Mouse.current;
            Press(mouse.leftButton); yield return null; yield return null; Release(mouse.leftButton);
            float tf = Time.realtimeSinceStartup;
            while (free.InHand && Time.realtimeSinceStartup - tf < 1f) yield return null;
            Assert.That(free.InHand, Is.False, "dropped and hit");
            Assert.That(fe.Coach.Fed, Is.EqualTo(0), "no feeds in free play");
            int fed = fe.Coach.Fed;
            yield return Tap(kb.tabKey);
            Assert.That(fe.Top, Is.InstanceOf<CoachScreen>(), "Tab opens the Coach panel");
            Assert.That(court.Runner.Clock.Paused, Is.True, "the game waits");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Mode"));
            yield return Tap(kb.rightArrowKey);                             // Free play -> Feeder
            Assert.That(ProfileStore.Current.Coach.Feeder, Is.True);
            yield return CaptureUI(fe, "P2M2_UI_5_Coach.png");
            yield return Tap(kb.downArrowKey);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Feed"));
            yield return Tap(kb.rightArrowKey);
            yield return Tap(kb.rightArrowKey);                             // Warm-up -> Length -> Drop
            Assert.That(ProfileStore.Current.Coach.Feed, Is.EqualTo(2));
            while (fe.Top.Focused.Name != "Feed now") yield return Tap(kb.downArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.Null, "feeding closes the panel");
            Assert.That(court.Runner.Clock.Paused, Is.False);
            Assert.That(fe.Coach.Fed, Is.EqualTo(fed + 1));
            Assert.That(court.Runner.Free, Is.Null, "the feeder took over");
            var cfg = court.Runner.Sim.Launcher.Config;
            Assert.That(cfg.Position.Z, Is.EqualTo(6.0).Within(1e-6), "the drop feed comes from closer in");
        }
    }
}
