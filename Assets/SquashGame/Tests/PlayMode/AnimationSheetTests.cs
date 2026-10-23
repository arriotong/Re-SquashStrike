// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AnimationSheetTests.cs
// The athletes in motion, for review: an AI plays for you in a quick
// match while the camera films athlete 0 side-on (and from the TV
// camera); every few frames go into a contact sheet.
// Writes Captures/ANIM_Side.png and ANIM_TV.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class AnimationSheetTests
    {
        [UnityTest]
        public IEnumerator ContactSheetsOfARally()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            var fe = Object.FindFirstObjectByType<UI.FrontEnd>();
            fe.StartMatch(MatchSettings.QuickGame());
            var r = court.Runner;
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("retriever").WithDifficulty(0.9), 11));
            var fig = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteFigure>();
            var cam = Camera.main;
            // Wait for the rally to be under way.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && !(r.Sim.Phase == SquashSim.MatchPhase.Rally && r.Sim.Referee.Shots >= 2)) yield return null;
            yield return Sheet(cam, fig, "ANIM_Side.png", true);
            yield return Sheet(cam, fig, "ANIM_TV.png", false);
        }

        static IEnumerator Sheet(Camera cam, Athlete.AthleteFigure fig, string file, bool side)
        {
            const int COLS = 8, ROWS = 4, CELL = 280;
            var rig = cam.GetComponent<GameplayCameraRig>();
            var sheet = new Texture2D(COLS * CELL, ROWS * CELL, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CELL, CELL, 24);
            float fov = cam.fieldOfView;
            for (int i = 0; i < COLS * ROWS; i++)
            {
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 0.07f) yield return null;
                if (side)
                {
                    rig.enabled = false;
                    Vector3 p = fig.transform.position, f = fig.DrawnForward, rgt = Vector3.Cross(Vector3.up, f);
                    cam.fieldOfView = 40f;
                    cam.transform.position = p + rgt * 3.4f + Vector3.up * 1.1f + f * 0.3f;
                    cam.transform.LookAt(p + Vector3.up * 0.8f);
                }
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, CELL, CELL), (i % COLS) * CELL, (ROWS - 1 - i / COLS) * CELL);
                cam.targetTexture = null;
                RenderTexture.active = null;
                if (side) { rig.enabled = true; cam.fieldOfView = fov; }
            }
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file)), sheet.EncodeToPNG());
            Object.Destroy(sheet); rt.Release();
        }
    }
}
