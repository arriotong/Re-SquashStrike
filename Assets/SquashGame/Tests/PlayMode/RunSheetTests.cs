// ===========================================================
// Assets/SquashGame/Tests/PlayMode/RunSheetTests.cs
// The run, for review (playtest 2026-10-24: "the running animation looks
// goofy"): in free practice the athlete runs at full speed toward the
// front wall, back, sideways and back on the diagonal; a camera beside
// the run films 8 frames of each. Writes Captures/ANIM_Run.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class RunSheetTests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }

        [UnityTest]
        public IEnumerator ARunInFourDirections()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            LabSimRunner r = Object.FindFirstObjectByType<WarmUpCourt>().Runner;
            var fig = Object.FindFirstObjectByType<Athlete.AthleteFigure>();
            var cam = Camera.main;
            var rig = cam.GetComponent<GameplayCameraRig>();
            yield return WaitReal(0.5f);
            while (rig.FlyingIn) yield return null;

            const int COLS = 8, CELL = 280;
            var runs = new (string name, ButtonControl[] keys, double x, double z)[]
            {
                ("forward", new[] { kb.wKey }, 0.0, 8.8),
                ("back", new[] { kb.sKey }, 0.0, 1.2),
                ("side", new[] { kb.aKey }, 0.0, 5.0),
                ("diagonal back", new[] { kb.sKey, kb.aKey }, 0.0, 1.2),
            };
            var sheet = new Texture2D(COLS * CELL, runs.Length * CELL, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CELL, CELL, 24);
            for (int row = 0; row < runs.Length; row++)
            {
                var run = runs[row];
                var p = r.Sim.Player.Player;
                // which way does this key go in x? (start on the other side of the court, so the run fits)
                p.Pos.X = 0; p.Pos.Z = run.z;
                foreach (var k in run.keys) Press(k);
                yield return WaitReal(0.12f);
                double dir = p.Pos.X;
                foreach (var k in run.keys) Release(k);
                yield return WaitReal(0.5f);
                p.Pos.X = System.Math.Abs(dir) > 0.02 ? -System.Math.Sign(dir) * 2.6 : 0; p.Pos.Z = run.z;
                p.Vel.X = 0; p.Vel.Z = 0;
                yield return WaitReal(0.4f);
                foreach (var k in run.keys) Press(k);
                yield return WaitReal(0.3f);                                   // up to speed
                rig.enabled = false;
                for (int i = 0; i < COLS; i++)
                {
                    yield return WaitReal(0.055f);
                    Vector3 at = fig.transform.position;
                    Vector3 v = new Vector3(fig.Posed.Velocity.x, 0, fig.Posed.Velocity.z);
                    Vector3 side = v.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, v.normalized) : Vector3.right;
                    cam.fieldOfView = 42f;
                    Vector3 want = at + side * 3.6f + Vector3.up * 1.0f;
                    // kept inside the court's walls (the club's are solid)
                    Vector3 c0 = SimToWorld.Pos(-CourtDimensions.HALF_W + 0.15, 0, 0.15), c1 = SimToWorld.Pos(CourtDimensions.HALF_W - 0.15, 0, CourtDimensions.LENGTH - 0.15);
                    want.x = Mathf.Clamp(want.x, Mathf.Min(c0.x, c1.x), Mathf.Max(c0.x, c1.x));
                    want.z = Mathf.Clamp(want.z, Mathf.Min(c0.z, c1.z), Mathf.Max(c0.z, c1.z));
                    if ((want - at).magnitude < 1.8f) want = at - v.normalized * 3.2f + Vector3.up * 1.4f;     // no room beside: from behind
                    cam.transform.position = want;
                    cam.transform.LookAt(at + Vector3.up * 0.85f);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CELL, CELL), i * CELL, (runs.Length - 1 - row) * CELL);
                    cam.targetTexture = null;
                    RenderTexture.active = null;
                }
                Vector3 vv = new Vector3(fig.Posed.Velocity.x, 0, fig.Posed.Velocity.z);
                Debug.Log("[run] " + run.name + ": speed " + vv.magnitude.ToString("0.0") + " m/s, facing " + Vector3.SignedAngle(vv, fig.DrawnForward, Vector3.up).ToString("0") +
                          " deg from the run, sim forward " + Vector3.SignedAngle(vv, new Vector3(fig.Posed.Forward.x, 0, fig.Posed.Forward.z), Vector3.up).ToString("0") + ", gait feet " + fig.GaitFeet);
                rig.enabled = true;
                foreach (var k in run.keys) Release(k);
                yield return WaitReal(0.4f);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "ANIM_Run.png")), sheet.EncodeToPNG());
            Object.Destroy(sheet); rt.Release();
            Assert.Pass();
        }
    }
}
