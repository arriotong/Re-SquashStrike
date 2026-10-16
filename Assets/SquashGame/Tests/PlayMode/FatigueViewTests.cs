// ===========================================================
// Assets/SquashGame/Tests/PlayMode/FatigueViewTests.cs
// Fatigue is visible on the body, not as a bar: a tired athlete stoops.
// Writes Captures/M35_Fatigue.png (fresh athlete left, tired right).
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class FatigueViewTests
    {
        [UnityTest]
        public IEnumerator TiredAthleteStoops()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            r.BeginMatch(MatchSettings.QuickGame());
            r.Clock.Pause();

            // Fresh you, exhausted opponent, side by side.
            r.Sim.Players[0].Player.Stamina = 1.0;
            r.Sim.Players[1].Player.Stamina = 0.25;
            r.Sim.Players[0].Player.Pos.Set(-1.0, 0, 5.5);
            r.Sim.Players[1].Player.Pos.Set(1.0, 0, 5.5);
            r.Sim.Players[0].Player.Facing = 0; r.Sim.Players[1].Player.Facing = 0;
            r.StepOnce();                          // capture the new state into the render snapshot
            yield return null; yield return null; yield return null;

            // M4 athletes: compare the chest's lean from vertical (the ready stance itself leans a little).
            Transform fresh = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteFigure>().Rig.Bones["Chest"];
            Transform tired = GameObject.Find("Athlete1").GetComponent<Athlete.AthleteFigure>().Rig.Bones["Chest"];
            float freshLean = Vector3.Angle(fresh.up, Vector3.up);
            float tiredLean = Vector3.Angle(tired.up, Vector3.up);
            Debug.Log("[M3.5] chest lean: fresh " + freshLean.ToString("0.0") + " deg, tired " + tiredLean.ToString("0.0") + " deg");
            Assert.That(tiredLean, Is.GreaterThan(freshLean + 6f), "a tired athlete should visibly stoop");

            Camera cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            cam.transform.position = SimToWorld.Pos(2.6, 1.5, 9.4);    // inside the back corner
            cam.transform.LookAt(SimToWorld.Pos(0, 1.0, 5.5));
            Capture(cam);
        }

        static void Capture(Camera cam)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "M35_Fatigue.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
