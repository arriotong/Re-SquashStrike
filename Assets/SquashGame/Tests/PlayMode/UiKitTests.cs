// ===========================================================
// Assets/SquashGame/Tests/PlayMode/UiKitTests.cs
// UI redesign U1: the component kit, in the real scene (the gallery,
// F12 on the main menu), on the keyboard and on a pad.
//   - cards: 2D focus moves to the neighbour; a locked card does not
//     open; live 3D pictures are drawn (and stop when hidden)
//   - tabs (Q / E, LB / RB) switch pages; the focus follows
//   - a selector changes with left / right
//   - the bracket's matches are focusable
//   - the prompt bar shows the glyphs of the device in use
//   - the sounds play
// Writes Captures/U1_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class UiKitTests : InputTestFixture
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

        [UnityTest]
        public IEnumerator TheKitWorksOnKeyboardAndPad()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
            yield return Tap(kb.f12Key);
            var g = fe.Top as GalleryScreen;
            Assert.That(g, Is.Not.Null, "F12: the gallery");
            yield return null; yield return null;

            // ---- cards ----
            Assert.That(g.Focused, Is.InstanceOf<CardItem>());
            Assert.That(((CardItem)g.Focused).Title, Is.EqualTo("QUICK MATCH"));
            yield return Wait(0.6f, () => false);
            var vig = Vignettes.Active;
            Assert.That(vig, Is.Not.Null);
            Assert.That(vig.Showing, Is.EqualTo(2), "two live pictures on the CARDS page");
            int drawn = vig.Rendered;
            yield return Wait(0.5f, () => false);
            Assert.That(vig.Rendered, Is.GreaterThan(drawn), "they keep being drawn");
            yield return CaptureUI(fe, "U1_Gallery_Cards.png");
            Assert.That(g.Prompts.Text, Does.Contain("Enter"), "keyboard glyphs");

            yield return Tap(kb.rightArrowKey);
            Assert.That(((CardItem)g.Focused).Title, Is.EqualTo("TARGET ARCADE"), "right: the next card");
            Assert.That(Sounds.Last, Is.EqualTo("focus"));
            yield return Tap(kb.rightArrowKey);
            Assert.That(g.Focused, Is.SameAs(g.Locked));
            yield return Tap(kb.enterKey);
            Assert.That(g.Picked, Is.EqualTo(""), "a locked card does not open");
            yield return Tap(kb.leftArrowKey); yield return Tap(kb.leftArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(g.Picked, Is.EqualTo("quick"));

            // ---- tabs: a pad from here ----
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return Tap(pad.rightShoulder);
            Assert.That(g.Tabs.Index, Is.EqualTo(1), "RB: the next tab");
            Assert.That(MenuInput.UsingPad, Is.True);
            yield return Wait(0.4f, () => false);                                 // the bar refreshes a few times a second
            Assert.That(g.Prompts.Text, Does.Contain("A"), "pad glyphs now");
            Assert.That(g.Prompts.Text, Does.Not.Contain("Enter"));
            int drawnHidden = vig.Rendered;
            yield return Wait(0.3f, () => false);
            Assert.That(vig.Rendered, Is.EqualTo(drawnHidden), "pictures on a hidden page are not drawn");
            Assert.That(g.Focused, Is.SameAs(g.Difficulty), "the focus moves to the page");
            int before = g.Difficulty.Index;
            Set(pad.leftStick, new Vector2(1, 0)); yield return null; Set(pad.leftStick, Vector2.zero); yield return null;
            Assert.That(g.Difficulty.Index, Is.EqualTo(before + 1), "right changes the selector");
            yield return CaptureUI(fe, "U1_Gallery_Controls.png");

            yield return Tap(pad.rightShoulder);
            Assert.That(g.Tabs.Index, Is.EqualTo(2));
            yield return null; yield return null;
            Assert.That(g.Focused, Is.InstanceOf<BracketMatchItem>(), "the bracket's matches are focusable");
            yield return Tap(kb.downArrowKey);
            Assert.That(((BracketMatchItem)g.Focused).Round, Is.EqualTo(0));
            yield return Wait(1.4f, () => false);                                 // the count-up and the stamp settle
            yield return CaptureUI(fe, "U1_Gallery_Data.png");

            yield return Tap(pad.buttonEast);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>(), "B: back");
            Assert.That(Sounds.Last, Is.EqualTo("back"));
            yield return null; yield return null;
            Assert.That(vig.Showing, Is.EqualTo(1), "back on the hub: only its PLAY page picture (the gallery's are freed)");
        }
    }
}
