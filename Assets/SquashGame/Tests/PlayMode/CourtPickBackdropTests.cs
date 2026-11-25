// ===========================================================
// Assets/SquashGame/Tests/PlayMode/CourtPickBackdropTests.cs
// 2026-11-25 (the user: "some legacy courts have like effects and when i
// choose the courts they like appear in the background try to make them not
// show"): the court picker over each legacy court, captured; and the new
// sounds of a match's moments.
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
    public class CourtPickBackdropTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> until)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !until()) yield return null;
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

        static IEnumerator Scene()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator TheCourtPickerOverEveryLegacyCourt()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var cp = new CourtPickScreen(false);
            fe.Push(cp);
            yield return Wait(1f, () => false);
            foreach (var c in Courts.All)
            {
                var tile = cp.Reachable().Find(i => i is CardItem ci && ci.Title == c.Name);
                Assert.That(tile, Is.Not.Null, c.Id);
                cp.Focus(tile);
                yield return Wait(2.2f, () => false);
                Assert.That(Courts.CurrentId, Is.EqualTo(c.Id));
                Assert.That(Dioramas.AmbientHeld, Is.True, c.Id + ": its light kept off the world");
                yield return CaptureUI(fe, "PICKBG_" + c.Id + ".png");
            }
        }
    }
}
