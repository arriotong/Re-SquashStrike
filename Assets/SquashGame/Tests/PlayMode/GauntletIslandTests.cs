// ===========================================================
// Assets/SquashGame/Tests/PlayMode/GauntletIslandTests.cs
// 2026-11-20 the Gauntlet's floating island: up behind every Gauntlet
// menu (a dialog over one keeps it), the camera on it and seeing its sky,
// gone again on the way out. Writes Captures/GAUNTLET_Island.png (the
// island alone) and GAUNTLET_Start.png (with the menu).
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class GauntletIslandTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t = 0;
            while (t < seconds && !done()) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static void Shot(string file, FrontEnd fe, bool ui)
        {
            var rt = new RenderTexture(1920, 1080, 24);
            var cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file)), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release();
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
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
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file)), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release();
        }

        [UnityTest]
        public IEnumerator TheGauntletMenusStandOnTheIsland()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(GauntletIsland.Shown, Is.False, "not on the home screens");

            fe.Push(new GauntletScreen());
            yield return Wait(1.0f, () => false);
            Assert.That(GauntletIsland.Shown, Is.True, "the island behind the Gauntlet");
            var cam = Camera.main;
            float d = Vector3.Distance(cam.transform.position, GauntletIsland.World);
            Debug.Log("[Island] camera " + d.ToString("0") + " m from it, far plane " + cam.farClipPlane.ToString("0") + ", fov " + cam.fieldOfView.ToString("0"));
            Assert.That(d, Is.LessThan(200f), "the camera is on the island");
            Assert.That(cam.farClipPlane, Is.GreaterThanOrEqualTo(GauntletIsland.FarClip), "and sees its sky");
            Shot("GAUNTLET_Island.png", fe, false);
            yield return CaptureUI(fe, "GAUNTLET_Start.png");

            fe.Push(new GauntletHelpScreen());
            yield return null; yield return null;
            Assert.That(GauntletIsland.Shown, Is.True, "a dialog over it keeps the island");
            yield return CaptureUI(fe, "GAUNTLET_Help.png");
            fe.Pop();
            fe.Push(new TalentsScreen());
            yield return null; yield return null;
            Assert.That(GauntletIsland.Shown, Is.True, "Upgrades too");
            fe.Pop(); fe.Pop();
            yield return Wait(0.5f, () => false);
            Assert.That(GauntletIsland.Shown, Is.False, "gone on the way out");
            Assert.That(Vector3.Distance(Camera.main.transform.position, GauntletIsland.World), Is.GreaterThan(1000f), "the camera back on the court");
        }

        /// <summary>2026-11-20 every mode's toon world: built, filmed (Captures/WORLD_*.png and WORLDS.png), the Play hub's tab picking
        /// it, the Others hub keeping the court.</summary>
        [UnityTest]
        public IEnumerator EveryMenuWorldIsBuiltAndTheHubFollowsItsTab()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.Push(new MainMenuScreen(MainMenuScreen.Hub.Play));
            yield return Wait(0.6f, () => false);
            var hub = (MainMenuScreen)fe.Top;
            var sheet = new Texture2D(960 * 2, 540 * 3, TextureFormat.RGB24, false);
            var want = new[] { ("PLAY", MenuWorld.Home), ("CAREER", MenuWorld.Career), ("CHALLENGES", MenuWorld.Challenges), ("TRAINING", MenuWorld.Training) };
            int n = 0;
            foreach (var (tab, world) in want)
            {
                for (int i = 0; i < hub.MySections.Length && hub.Tabs.Current != tab; i++) { hub.OnTab(1); yield return null; }
                Assert.That(hub.Tabs.Current, Is.EqualTo(tab));
                yield return Wait(0.5f, () => false);
                Assert.That(Dioramas.Current, Is.EqualTo(world), tab + ": its world");
                Assert.That(Dioramas.Shown(world), Is.True);
                Assert.That(Vector3.Distance(Camera.main.transform.position, Dioramas.Origin(world)), Is.LessThan(200f), tab + ": the camera on it");
                Shot("WORLD_" + world + ".png", fe, false);
                yield return CaptureUI(fe, "WORLD_" + world + "_Hub.png");
                Paste(sheet, "WORLD_" + world + ".png", n++);
            }
            fe.Push(new GauntletScreen());
            yield return Wait(0.5f, () => false);
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.Gauntlet));
            Paste(sheet, "GAUNTLET_Island.png", n++);
            fe.Pop(); fe.Pop();
            yield return null; yield return null;
            fe.Push(new MainMenuScreen(MainMenuScreen.Hub.Others));
            yield return Wait(0.5f, () => false);
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.None), "the Others hub keeps the court");
            Assert.That(Vector3.Distance(Camera.main.transform.position, Dioramas.Origin(MenuWorld.Home)), Is.GreaterThan(1000f));
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "WORLDS.png")), sheet.EncodeToPNG());
            Object.Destroy(sheet);
        }

        static void Paste(Texture2D sheet, string file, int i)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            if (!File.Exists(path)) return;
            var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(path));
            var rt = RenderTexture.GetTemporary(960, 540); Graphics.Blit(t, rt);
            RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, 960, 540), (i % 2) * 960, (2 - i / 2) * 540); RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt); Object.Destroy(t);
        }
    }
}
