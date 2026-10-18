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

            // The right stick drives the reticle across the wall (a rate: hold it longer, it goes further).
            // (Batch-mode frames are ~1 ms: wait real time.)
            r.Input.SetReticle(0f, 1.8f);
            Set(pad.rightStick, new Vector2(-0.8f, 0f));
            yield return WaitReal(0.25f);
            float left = aim.WorldPoint.x;
            Set(pad.rightStick, new Vector2(0.8f, 0f));
            yield return WaitReal(0.5f);
            Set(pad.rightStick, Vector2.zero);
            yield return WaitReal(0.1f);
            float right = aim.WorldPoint.x;
            Debug.Log("[M6] target x: stick left " + left.ToString("0.00") + ", stick right " + right.ToString("0.00"));
            Assert.That(Mathf.Abs(right - left), Is.GreaterThan(1.0f), "the stick moves the target across the wall");
            Assert.That(aim.Wall, Is.EqualTo(0), "inside the front wall's width: a front-wall target");

            // Held on: it runs into the corner and stops there, still on the front wall (boasts are E / RB).
            Set(pad.rightStick, new Vector2(1f, 0f));
            yield return WaitReal(0.8f);
            Debug.Log("[M6] full stick right: wall " + aim.Wall + " at " + aim.WorldPoint);
            Assert.That(aim.Wall, Is.EqualTo(0), "held right: it stops at the front-wall edge");
            Assert.That(Mathf.Abs(aim.WorldPoint.x), Is.GreaterThan((float)CourtDimensions.HALF_W - 0.3f), "right in the corner");
            Set(pad.rightStick, new Vector2(-1f, 0f));
            yield return WaitReal(1.6f);
            Assert.That(aim.Wall, Is.EqualTo(0), "and the other way");
            Assert.That(Mathf.Abs(aim.WorldPoint.x), Is.GreaterThan((float)CourtDimensions.HALF_W - 0.3f));
            Set(pad.rightStick, Vector2.zero);

            // Swing at the feeds aiming a drive; compare the last contact-based preview with the real hit.
            yield return null;
            r.Input.SetReticle(0.6f, 2.0f);
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
            bool holdingHit = false; float nextPressAt = 0f;
            float t0 = Time.realtimeSinceStartup;
            bool captured = false;
            while (Time.realtimeSinceStartup - t0 < 20f && errors.Count < 10)
            {
                // Play like a person with the simple hitting system: hold Hit, let go as the ball comes in.
                {
                    var sp = r.Sim.Player.Player.Pos; var bp = r.Sim.Ball.Pos;
                    double bd = System.Math.Sqrt((bp.X - sp.X) * (bp.X - sp.X) + (bp.Z - sp.Z) * (bp.Z - sp.Z));
                    if (!holdingHit && Time.realtimeSinceStartup >= nextPressAt) { Press(pad.buttonSouth); holdingHit = true; }
                    else if (holdingHit && bd < 1.3 && bp.Y < 2.4) { Release(pad.buttonSouth); holdingHit = false; nextPressAt = Time.realtimeSinceStartup + 0.35f; }
                }
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
            Debug.Log("[Flash] great hits flashed " + aim.GreatHits);
            Assert.That(timing.Count, Is.GreaterThan(0), "every stroke gets a timing note");
            var pops = Object.FindFirstObjectByType<ArcadePopups>();
            Debug.Log("[Pop] practice shown: " + string.Join(" ", pops.Shown));
            Assert.That(pops.Shown.Exists(w => w == "Hit!"), Is.True, "strokes pop up");
            Assert.That(pops.Shown.TrueForAll(w => w == "Hit!"), Is.True, "and only a plain Hit!");
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
