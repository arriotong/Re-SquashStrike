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

        /// <summary>2026-11-21 every hub tab its own toon world (both hubs; none repeated), each built and filmed
        /// (Captures/WORLD_*.png, WORLD_*_Hub.png, WORLDS.png with the Gauntlet's island too).</summary>
        [UnityTest]
        public IEnumerator EveryMenuWorldIsBuiltAndTheHubFollowsItsTab()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var sheet = new Texture2D(640 * 3, 360 * 4, TextureFormat.RGB24, false);
            var seen = new System.Collections.Generic.HashSet<MenuWorld>();
            int n = 0;
            foreach (var which in new[] { MainMenuScreen.Hub.Play, MainMenuScreen.Hub.Others })
            {
                fe.Push(new MainMenuScreen(which));
                yield return Wait(0.6f, () => false);
                var hub = (MainMenuScreen)fe.Top;
                foreach (var tab in hub.MySections)
                {
                    for (int i = 0; i < hub.MySections.Length && hub.Tabs.Current != tab; i++) { hub.OnTab(1); yield return null; }
                    Assert.That(hub.Tabs.Current, Is.EqualTo(tab));
                    yield return Wait(0.5f, () => false);
                    var world = Dioramas.Current;
                    Assert.That(world, Is.Not.EqualTo(MenuWorld.None), tab + ": a world of its own");
                    Assert.That(seen.Add(world), Is.True, tab + ": not another tab's world (" + world + ")");
                    Assert.That(Vector3.Distance(Camera.main.transform.position, Dioramas.Origin(world)), Is.LessThan(200f), tab + ": the camera on it");
                    Shot("WORLD_" + world + ".png", fe, false);
                    yield return CaptureUI(fe, "WORLD_" + world + "_Hub.png");
                    Paste(sheet, "WORLD_" + world + ".png", n++);
                }
                fe.Pop();
                yield return null; yield return null;
            }
            Assert.That(seen.Count, Is.EqualTo(9), "nine tabs, nine worlds (2026-11-21: PACKS off the Play hub)");
            fe.Push(new SettingsScreen());
            yield return Wait(0.6f, () => false);
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.Settings), "Settings: the snowy workshop");
            Assert.That(seen.Contains(MenuWorld.Settings), Is.False, "its own world");
            Shot("WORLD_Settings.png", fe, false);
            yield return CaptureUI(fe, "WORLD_Settings_Screen.png");
            Paste(sheet, "WORLD_Settings.png", n++);
            fe.Pop();
            yield return null; yield return null;
            fe.Push(new GauntletScreen());
            yield return Wait(0.5f, () => false);
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.Gauntlet));
            Paste(sheet, "GAUNTLET_Island.png", n++);
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "WORLDS.png")), sheet.EncodeToPNG());
            Object.Destroy(sheet);
        }

        static void Paste(Texture2D sheet, string file, int i)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            if (!File.Exists(path)) return;
            var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(path));
            var rt = RenderTexture.GetTemporary(640, 360); Graphics.Blit(t, rt);
            RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, 640, 360), (i % 3) * 640, (3 - i / 3) * 360); RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt); Object.Destroy(t);
        }
    }
}
