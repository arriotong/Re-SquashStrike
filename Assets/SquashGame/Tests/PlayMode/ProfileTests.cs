// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ProfileTests.cs
// The player tag on the main menu opens your profile: a click on it (or
// the arrows onto it and Enter); a new name there is the tag's, the
// versus screen's and online's; the profile's record is filled in.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ProfileTests : InputTestFixture
    {
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

        [UnityTest]
        public IEnumerator ThePlayerTagOpensYourProfileAndRenamesYou()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var hub = (MainMenuScreen)fe.Top;

            Assert.That(hub.ChipButton, Is.Not.Null, "the tag is something you can press");
            Assert.That(hub.Chip.NameShown, Is.EqualTo("Player"));
            Assert.That(hub.Reachable(), Has.Member(hub.ChipButton), "the arrows reach it");
            hub.ChipButton.Submit();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<ProfileScreen>(), "the profile");
            yield return CaptureUI(fe, "Profile.png");

            var profile = (ProfileScreen)fe.Top;
            Assert.That(ChoiceBudgetTests.Count(profile), Is.LessThanOrEqualTo(ChoiceBudgetTests.Budget));
            profile.NameField.Field.value = "Alex Rally";
            profile.Back();                                                       // saves on the way out
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
            Assert.That(ProfileStore.Current.Online.Name, Is.EqualTo("Alex Rally"), "online goes by it");
            Assert.That(ProfileStore.Current.Online.NameChosen, Is.True, "no need to ask again online");
            ((MainMenuScreen)fe.Top).OnShow();
            yield return null;
            Assert.That(((MainMenuScreen)fe.Top).Chip.NameShown, Is.EqualTo("Alex Rally"), "the tag shows it");

            // an empty name changes nothing
            hub.ChipButton.Submit();
            yield return null;
            var p2 = (ProfileScreen)fe.Top;
            p2.NameField.Field.value = "   ";
            p2.Back();
            yield return null;
            Assert.That(ProfileStore.Current.Online.Name, Is.EqualTo("Alex Rally"));
        }
    }
}
