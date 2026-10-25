// ===========================================================
// Assets/SquashGame/Tests/PlayMode/FrontEndTests.cs
// P2-M1: the front end in the real Match scene with virtual devices.
//   - pause (Esc) -> Settings -> Controls -> change the control mode;
//     Esc in a settings page goes back without un-pausing the game;
//     Esc on the pause screen resumes; the change is in the profile
//   - a whole match (sped up, a bot playing for the human) ends on the
//     post-match screen; Rematch starts a new match
//   - the practice court's pause menu goes back to the main menu
// Writes Captures/P2M1_*.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class FrontEndTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b)
        {
            Press(b); yield return null;
            Release(b); yield return null;
        }

        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        [UnityTest]
        public IEnumerator PauseSettingsAndBackWithTheKeyboard()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>(), "no title when asked not to");

            fe.StartMatch(MatchSettings.QuickGame());
            yield return null;
            Assert.That(fe.Top, Is.Null);
            Assert.That(FrontEnd.MenuOpen, Is.False);

            yield return Tap(kb.escapeKey);
            yield return null;
            Assert.That(r.Clock.Paused, Is.True, "Esc pauses");
            Assert.That(fe.Top, Is.InstanceOf<PauseScreen>(), "the pause screen comes up");
            Capture("P2M1_Pause.png");
            while (fe.Top.Focused.Name != "Settings") yield return Tap(kb.downArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<SettingsScreen>());
            yield return null; yield return null;
            Assert.That(((SettingsScreen)fe.Top).Tabs.Current, Is.EqualTo("CONTROLS"));
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Control mode"));
            bool before = IntentSource.Assisted;
            yield return Tap(kb.rightArrowKey);
            Assert.That(IntentSource.Assisted, Is.EqualTo(!before), "the control mode changes at once");

            yield return Tap(kb.escapeKey);                                  // back to Pause
            Assert.That(fe.Top, Is.InstanceOf<PauseScreen>());
            Assert.That(r.Clock.Paused, Is.True, "Esc in a menu must not un-pause the game");
            yield return Tap(kb.escapeKey);                                  // resume
            yield return null;
            Assert.That(r.Clock.Paused, Is.False, "Esc on the pause screen resumes");
            Assert.That(fe.Top, Is.Null);

            // Saved: a fresh load from disk has the new control mode.
            ProfileStore.Reset();
            Assert.That(ProfileStore.Load().Settings.Assisted, Is.EqualTo(!before), "the setting is in the profile file");
        }

        [UnityTest]
        public IEnumerator AMatchEndsOnThePostMatchScreenAndRematchStartsAnother()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.0f, GamesToWin = 1, Scoring = "english" });
            // A bot plays for the human, and the whole thing runs fast.
            r.Sim.Intents[0] = new InputIntent();
            r.Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.42), 77);
            r.Sim.Players[0].WallAimEnabled = true; r.Sim.Players[0].ArcadeHitEnabled = true;
            r.SetTimeScale(12);
            yield return Wait(90f, () => fe.Top is PostMatchScreen);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>(), "the match ends on the post-match screen");
            Debug.Log("[P2M1] post-match: rallies " + fe.Stats.Rallies + ", longest " + fe.Stats.LongestRally + ", winners " +
                      fe.Stats.Players[0].Winners + "/" + fe.Stats.Players[1].Winners + ", errors " + fe.Stats.Players[0].Errors + "/" + fe.Stats.Players[1].Errors);
            Assert.That(fe.Stats.Rallies, Is.GreaterThan(5), "the stats counted the match");
            Capture("P2M1_PostMatch.png");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Rematch"));
            yield return Tap(kb.enterKey);
            yield return null;
            Assert.That(fe.Top, Is.Null, "rematch closes the menus");
            Assert.That(r.Sim.Phase, Is.EqualTo(MatchPhase.AwaitToss), "a new match, at the first serve");
            Assert.That(r.Sim.Score.Points[0] + r.Sim.Score.Points[1], Is.EqualTo(0));
            Assert.That(fe.Stats.Rallies, Is.EqualTo(0), "fresh stats");
        }

        [UnityTest]
        public IEnumerator PracticePauseGoesBackToTheMainMenu()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.Null, "practice starts in play");
            yield return Tap(kb.escapeKey);
            yield return null;
            Assert.That(fe.Top, Is.InstanceOf<PracticePauseScreen>());
            while (fe.Top.Focused.Name != "Quit to menu") yield return Tap(kb.downArrowKey);
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            var fe2 = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe2.Match, Is.Not.Null, "back in the match scene");
            Assert.That(fe2.Top, Is.InstanceOf<MainMenuScreen>(), "on the main menu, not the title");
        }

        /// <summary>The front end itself, drawn into a texture (batch mode has no screen to grab).</summary>
        internal static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;          // the court behind
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

        [UnityTest]
        public IEnumerator ScreensForReview()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return Wait(0.5f, () => false);
            // P2-M6: real rallies behind the logo (a bundled match: this profile has none of its own)
            yield return Wait(3f, () => fe.Match.FramesActive);
            var title = (TitleScreen)fe.Top;
            Assert.That(title.Attract, Is.Not.Null, "highlights behind the title");
            Assert.That(fe.Match.FramesActive, Is.True, "a replayed rally is showing");
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "P2M1_UI_1_Title.png");
            yield return Tap(kb.enterKey);
            Assert.That(Replays.TheatreSession.Active, Is.Null, "the title's highlights stop with it");
            Assert.That(fe.Match.Demo, Is.True);
            yield return CaptureUI(fe, "P2M1_UI_2_MainMenu.png");
            // U2: every tab of the hub, for review
            foreach (var tab in new[] { "CHALLENGES", "CAREER", "ACADEMY", "LOCKER" })
            {
                yield return Tap(kb.eKey);
                yield return Wait(0.4f, () => false);
                yield return CaptureUI(fe, "U2_Hub_" + tab + ".png");
            }
            for (int i = 0; i < 3; i++) yield return Tap(kb.qKey);
            yield return Tap(kb.enterKey);
            yield return CaptureUI(fe, "P2M1_UI_3_QuickMatch.png");
            yield return Tap(kb.escapeKey);
            yield return Tap(kb.rKey);                                  // Y / R: settings
            Assert.That(fe.Top, Is.InstanceOf<SettingsScreen>());
            foreach (var tab in new[] { "CONTROLS", "VIDEO", "AUDIO", "ACCESSIBILITY", "GAMEPLAY" })
            {
                yield return Wait(0.3f, () => false);
                Assert.That(((SettingsScreen)fe.Top).Tabs.Current, Is.EqualTo(tab));
                yield return CaptureUI(fe, "U5_Settings_" + tab + ".png");
                yield return Tap(kb.eKey);
            }
            yield return Tap(kb.escapeKey);
            fe.Push(new ConfirmScreen("Quit re:SquashStrike?", () => { }));
            yield return Wait(0.3f, () => false);
            yield return CaptureUI(fe, "U5_Confirm.png");
            fe.Pop();
            fe.Push(new ControlsHelpScreen());
            yield return CaptureUI(fe, "U5_Controls.png");
            fe.Pop();
            fe.StartMatch(MatchSettings.QuickGame());
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "P2M1_UI_5_Hud.png");
            yield return Tap(kb.escapeKey);
            yield return null;
            yield return CaptureUI(fe, "P2M1_UI_6_Pause.png");
            Assert.That(fe.Top, Is.InstanceOf<PauseScreen>());
            yield return Tap(kb.escapeKey);
            yield return Wait(0.3f, () => false);
            fe.Toast("ACHIEVEMENT  ·  GOLD", "World No. 1", 2);
            fe.Toast("ACHIEVEMENT  ·  BRONZE", "First win", 0);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "U5_HudToasts.png");
        }

        static void Capture(string file)
        {
            // The camera alone (UI Toolkit draws on top in the game, not into this render).
            Camera cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
