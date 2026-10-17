// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AimTargetTests.cs
// M6: the always-on aim target, in the practice court with a virtual
// pad. It is shown, it moves with the stick, and it tells the truth:
// where the ball really meets the front wall vs where the target showed
// just before contact. Writes Captures/M6_AimTarget.png.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class AimTargetTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator TargetShowsWhereTheBallGoes()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            LabSimRunner r = court.Runner;
            AimTargetView aim = GameObject.Find("AimTarget0").GetComponent<AimTargetView>();
            yield return null;
            Assert.That(aim.Shown, Is.True, "the target is always on in practice");

            // The stick moves it: left vs right across the wall.
            // (Batch-mode frames are ~1 ms: wait real time so the eased target can get there.)
            Set(pad.rightStick, new Vector2(-0.8f, 0f));
            yield return WaitReal(0.6f);
            float left = aim.WorldPoint.x;
            Set(pad.rightStick, new Vector2(0.8f, 0f));
            yield return WaitReal(0.6f);
            float right = aim.WorldPoint.x;
            Debug.Log("[M6] target x: stick left " + left.ToString("0.00") + ", stick right " + right.ToString("0.00"));
            Assert.That(Mathf.Abs(right - left), Is.GreaterThan(1.0f), "the stick moves the target across the wall");
            Assert.That(aim.Wall, Is.EqualTo(0), "inside the front wall's width: a front-wall target");

            // Full stick: past the edge of the front wall, the target moves onto that side wall (a boast).
            Set(pad.rightStick, new Vector2(1f, 0f));
            yield return WaitReal(0.6f);
            Debug.Log("[M6] full stick right: wall " + aim.Wall + " at " + aim.WorldPoint);
            Assert.That(aim.Wall, Is.Not.EqualTo(0), "full stick aims a boast off the side wall");
            Set(pad.rightStick, new Vector2(-1f, 0f));
            yield return WaitReal(0.6f);
            Assert.That(aim.Wall, Is.Not.EqualTo(0), "and the other way");

            // Swing at the feeds with a slight aim; compare the last contact-based preview with the real hit.
            Set(pad.rightStick, new Vector2(0.25f, 0.1f));
            var errors = new List<float>();
            bool awaitingWall = false;
            Vector3 promised = Vector3.zero;
            bool havePromise = false;
            System.Action<IReadOnlyList<SimEvent>> onStep = evs =>
            {
                foreach (var e in evs)
                {
                    if (e.Type == SimEventType.RacketContact && havePromise) awaitingWall = true;
                    else if (e.Type == SimEventType.Impact && awaitingWall)
                    {
                        awaitingWall = false;
                        if (e.SurfaceClass == "frontWall")
                            errors.Add(Vector3.Distance(SimToWorld.Pos(e.Point.X, e.Point.Y, 0.012), promised));
                    }
                }
            };
            r.Stepped += onStep;
            float t0 = Time.realtimeSinceStartup;
            bool captured = false;
            while (Time.realtimeSinceStartup - t0 < 20f && errors.Count < 10)
            {
                if (((int)((Time.realtimeSinceStartup - t0) * 4f)) % 4 == 0) Press(pad.buttonSouth); else Release(pad.buttonSouth);
                yield return null;
                if (aim.Preview.FromContact && !awaitingWall)
                {
                    // The target as drawn (unsmoothed preview point) just before contact.
                    promised = SimToWorld.Pos(aim.Preview.FrontPoint.X, System.Math.Max(0.03, aim.Preview.FrontPoint.Y), 0.012);
                    havePromise = true;
                    if (!captured && r.Sim.Player.Swing.State == SwingStates.Swinging) { captured = true; Capture("M6_AimTarget.png"); }
                }
            }
            r.Stepped -= onStep;
            Release(pad.buttonSouth);
            errors.Sort();
            Debug.Log("[M6] target vs real front-wall hit over " + errors.Count + " shots: median " +
                      (errors.Count > 0 ? errors[errors.Count / 2].ToString("0.00") : "-") + " m, worst " +
                      (errors.Count > 0 ? errors[errors.Count - 1].ToString("0.00") : "-") + " m");
            var timing = aim.GetComponent<TimingFeedbackView>();
            Debug.Log("[M6] timing notes shown " + timing.Count + ", last " + timing.Last);
            Assert.That(timing.Count, Is.GreaterThan(0), "every stroke gets a timing note");
            var pops = Object.FindFirstObjectByType<ArcadePopups>();
            Debug.Log("[Pop] practice shown: " + string.Join(" ", pops.Shown));
            Assert.That(pops.Shown.Exists(w => w == "HIT!" || w == "VOLLEY!" || w == "SMASH!" || w == "TOUCH!"), Is.True, "strokes pop up");
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(3), "not enough shots reached the front wall");
            Assert.That(errors[errors.Count / 2], Is.LessThan(0.6f), "the target should show where the ball goes");
        }

        static IEnumerator WaitReal(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        static void Capture(string file)
        {
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
