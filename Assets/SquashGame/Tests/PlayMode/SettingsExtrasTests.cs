// ===========================================================
// Assets/SquashGame/Tests/PlayMode/SettingsExtrasTests.cs
// Settings added after the review (2026-10-19): rebinding a key through
// Settings > Controls > Change keys (the new key hits, it is saved, it
// comes back after a restart); the match camera presets; the version on
// the title screen. Writes captures.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SquashGame.Tests
{
    public class SettingsExtrasTests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }
        IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

        [TearDown] public void Defaults()
        {
            IntentSource.ResetBindings();
            if (ProfileStore.Current != null) { ProfileStore.Current.Settings.Bindings = ""; ProfileStore.Current.Settings.Camera = 0; }
            GameplayCameraRig.Preset = 0;
        }

        [UnityTest]
        public IEnumerator RebindHitToKThenKHits()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            LabSimRunner r = Object.FindFirstObjectByType<WarmUpCourt>().Runner;
            yield return WaitReal(0.3f);
            Assert.That(IntentSource.BindingLabel("Hit", false), Is.EqualTo("LMB"));

            fe.Push(new RebindScreen());
            yield return WaitReal(0.2f);
            var screen = (RebindScreen)fe.Top;
            yield return FrontEndTests.CaptureUI(fe, "Settings_ChangeKeys.png");
            yield return Tap(kb.enterKey);                                     // the first row: Hit
            yield return WaitReal(0.3f);
            Assert.That(screen.Listening, Is.EqualTo("Hit"), "waiting for the new key");
            yield return Tap(kb.kKey);
            yield return WaitReal(0.3f);
            Debug.Log("[Keys] Hit is now " + IntentSource.BindingLabel("Hit", false));
            Assert.That(IntentSource.BindingLabel("Hit", false), Is.EqualTo("K"));
            Assert.That(ProfileStore.Current.Settings.Bindings, Does.Contain("<Keyboard>/k"), "saved in the profile");
            fe.Pop();
            yield return null;

            // K drops and hits the ball in free play
            var free = r.Free;
            Assert.That(free != null && free.InHand, Is.True);
            Press(kb.kKey); yield return null; yield return null; Release(kb.kKey);
            float t0 = Time.realtimeSinceStartup;
            while (free.InHand && Time.realtimeSinceStartup - t0 < 1f) yield return null;
            Assert.That(free.InHand, Is.False, "K hits now");

            // a restart: the profile's bindings come back
            string saved = ProfileStore.Current.Settings.Bindings;
            IntentSource.SetOverrides("");
            Assert.That(IntentSource.BindingLabel("Hit", false), Is.EqualTo("LMB"));
            ProfileStore.Current.Settings.Bindings = saved;
            ProfileStore.Apply(ProfileStore.Current.Settings, false);
            Assert.That(IntentSource.BindingLabel("Hit", false), Is.EqualTo("K"), "back after a restart");
        }

        [UnityTest]
        public IEnumerator CameraPresetsAndTheVersion()
        {
            LocalMatchTests.FreshProfile();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return WaitReal(0.3f);
            Assert.That(fe.Top, Is.InstanceOf<TitleScreen>());
            Assert.That(fe.Top.Root.Q<Label>(className: "fe-version").text, Does.Contain(Application.version));
            fe.StartMatch(MatchSettings.QuickGame());
            yield return WaitReal(0.4f);
            var cam = Camera.main.transform;
            float back = cam.position.y;
            GameplayCameraRig.Preset = 1;
            yield return WaitReal(0.2f);
            float high = cam.position.y;
            yield return FrontEndTests.CaptureUI(fe, "Camera_High.png");
            GameplayCameraRig.Preset = 2;
            yield return WaitReal(0.2f);
            float low = cam.position.y;
            yield return FrontEndTests.CaptureUI(fe, "Camera_LowGlass.png");
            Debug.Log("[Camera] back wall " + back.ToString("0.0") + " m, high " + high.ToString("0.0") + " m, low glass " + low.ToString("0.0") + " m");
            Assert.That(high, Is.GreaterThan(back + 1f));
            Assert.That(low, Is.LessThan(back - 1f));
        }
    }
}
