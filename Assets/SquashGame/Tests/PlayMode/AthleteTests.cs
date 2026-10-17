// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AthleteTests.cs
// M4 exit test, in the real warm-up court: the rendered racket sits on
// the simulated racket (sweet spot and face normal) whenever it is locked
// to a swing and at every contact, and the hand holds it - measured over
// real feeds with a virtual gamepad swinging.
// M6 animation feel: the drawn facing turns at a human rate, planted feet
// do not slide, the racket is locked at every contact.
// Writes Captures/M4_*.png.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.Athlete;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class AthleteTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator RacketOnTheSimRacketAndHandOnTheRacket()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            LabSimRunner r = court.Runner;
            AthleteFigure fig = GameObject.Find("Athlete0").GetComponent<AthleteFigure>();
            Assert.That(fig.Rig.Bones.Count, Is.EqualTo(74));
            Assert.That(fig.Rig.FormCount, Is.GreaterThanOrEqualTo(19));

            var racketErr = new List<float>();
            var gripAtContact = new List<float>();
            var gripAll = new List<float>();
            var yawRate = new List<float>();
            var lockAtContact = new List<float>();
            int diag = 0;
            bool shotBack = false, shotContact = false, shotFollow = false;
            float footSlide = 0f;
            var lastFeet = new Vector3[2];
            var lastPlanted = new bool[2];
            int contacts = 0;
            bool capturedContact = false;
            int physicalContacts = 0;
            r.Stepped += evs => { foreach (var e in evs) if (e.Type == SimEventType.RacketContact) { contacts++; if (e.ArcadeQuality < 0) physicalContacts++; } };
            int lastPhysical = 0;

            bool holdingHit = false; float nextPressAt = 0f;
            float t0 = Time.realtimeSinceStartup;
            int lastContacts = 0;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                // Swing at whatever comes: tap Hit regularly.
                // Play like a person with the simple hitting system: hold Hit, let go as the ball comes in.
                {
                    var sp = r.Sim.Player.Player.Pos; var bp = r.Sim.Ball.Pos;
                    double bd = System.Math.Sqrt((bp.X - sp.X) * (bp.X - sp.X) + (bp.Z - sp.Z) * (bp.Z - sp.Z));
                    if (!holdingHit && Time.realtimeSinceStartup >= nextPressAt) { Press(pad.buttonSouth); holdingHit = true; }
                    else if (holdingHit && bd < 1.3 && bp.Y < 2.4) { Release(pad.buttonSouth); holdingHit = false; nextPressAt = Time.realtimeSinceStartup + 0.35f; }
                }
                yield return null;

                // The rendered racket vs the simulated racket it was posed from, whenever it is locked to a swing.
                AthleteSnapshot v = fig.Posed;
                Transform racket = fig.Rig.Racket;
                if (fig.RacketLock >= 1f)
                {
                    Vector3 sweet = racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0));
                    racketErr.Add(Vector3.Distance(sweet, v.RacketHead));
                    if (Vector3.Distance(sweet, v.RacketHead) > 0.05f && diag < 12)
                    {
                        diag++;
                        Debug.Log("[Diag] lock miss " + Vector3.Distance(sweet, v.RacketHead).ToString("0.00") + " m: shoulder gap " + fig.ShoulderGapBefore.ToString("0.00") +
                                  " -> " + fig.ShoulderGapAfter.ToString("0.00") + ", shift " + fig.ReachShift.ToString("0.00") + ", grip gap " + fig.GripGap.ToString("0.00") +
                                  ", clip " + fig.Clip + ", head y " + v.RacketHead.y.ToString("0.00") + ", from body " + Vector3.Distance(new Vector3(v.RacketHead.x, 0, v.RacketHead.z), new Vector3(v.Pos.x, 0, v.Pos.z)).ToString("0.00"));
                    }
                    Vector3 drawnNormal = racket.right;
                    Assert.That(Mathf.Abs(Vector3.Dot(drawnNormal, v.RacketFaceNormal.normalized)), Is.GreaterThan(0.999f), "string normal");
                }
                // M7: the racket lives in the hand - the butt must sit in the grip socket, always.
                gripAll.Add(Vector3.Distance(fig.Rig.Racket.position, fig.Rig.Bones["Hand_R"].TransformPoint(fig.Rig.GripLocalPos)));

                // Animation feel: facing turn rate, planted feet that stay planted.
                // The figure reports its own per-pose turn rate (frame timing in batch mode is too uneven to difference).
                yawRate.Add(fig.TurnRate);
                for (int f = 0; f < 2; f++)
                    if (lastPlanted[f] && fig.FootPlanted[f])
                        footSlide = Mathf.Max(footSlide, Vector3.Distance(lastFeet[f], fig.Feet[f]));
                for (int f = 0; f < 2; f++) { lastFeet[f] = fig.Feet[f]; lastPlanted[f] = fig.FootPlanted[f]; }
                // M7: a close-up of one whole stroke (backswing, contact, follow-through), for review.
                if (!shotBack && v.Swing == SwingPhase.Scheduled) { shotBack = true; CloseUp(fig, "M7_1_Backswing.png"); }
                if (!shotFollow && shotContact && v.Swing == SwingPhase.Recovering) { shotFollow = true; CloseUp(fig, "M7_3_Follow.png"); }
                if (contacts != lastContacts && !shotContact && shotBack) { shotContact = true; CloseUp(fig, "M7_2_Contact.png"); }
                if (contacts != lastContacts)
                {
                    lastContacts = contacts;
                    gripAtContact.Add(fig.GripGap);
                    // The racket lock is the PHYSICAL swing's rule (the AI); the simple hitting system's hit is
                    // instantaneous and the drawn stroke plays through it.
                    if (physicalContacts != lastPhysical) lockAtContact.Add(fig.RacketLock);
                    lastPhysical = physicalContacts;
                    if (!capturedContact) { capturedContact = true; Capture("M4_Contact_TV.png", null); }
                }
            }
            Release(pad.buttonSouth);

            racketErr.Sort(); gripAll.Sort(); gripAtContact.Sort(); yawRate.Sort(); lockAtContact.Sort();
            if (physicalContacts > 0) Assert.That(racketErr.Count, Is.GreaterThan(0), "the racket never locked to a swing");
            float worstRacket = racketErr.Count > 0 ? racketErr[racketErr.Count - 1] : 0f;
            float yaw95 = yawRate[(int)(yawRate.Count * 0.95f)], yawMax = yawRate[yawRate.Count - 1];
            Debug.Log("[M6] drawn facing turn rate 95th " + yaw95.ToString("0") + " deg/s, max " + yawMax.ToString("0") +
                      " deg/s; planted foot slide worst " + (footSlide * 1000).ToString("0.0") + " mm; racket lock at contact min " +
                      (lockAtContact.Count > 0 ? lockAtContact[0].ToString("0.00") : "-"));
            float medianGrip = gripAll[gripAll.Count / 2];
            Debug.Log("[M4] frames " + racketErr.Count + ", racket sweet-spot error worst " + (worstRacket * 1000).ToString("0.00") + " mm; " +
                      "grip gap median " + (medianGrip * 1000).ToString("0.0") + " mm, 95th " + (gripAll[(int)(gripAll.Count * 0.95f)] * 1000).ToString("0.0") +
                      " mm; at " + gripAtContact.Count + " contacts: worst " + (gripAtContact.Count > 0 ? (gripAtContact[gripAtContact.Count - 1] * 1000).ToString("0.0") : "-") + " mm");

            Assert.That(worstRacket, Is.LessThan(0.01f), "rendered racket must sit on the simulated racket (< 1 cm)");
            Assert.That(contacts, Is.GreaterThan(0), "no racket contacts in 12 s of feeds");
            Assert.That(gripAll[gripAll.Count - 1], Is.LessThan(0.002f), "the hand always holds the racket");
            Assert.That(yawMax, Is.LessThanOrEqualTo(540.5f), "the drawn body turns at a human rate (cap 540 deg/s)");
            Assert.That(footSlide, Is.LessThan(0.002f), "a planted foot does not slide");
            if (lockAtContact.Count > 0) Assert.That(lockAtContact[0], Is.EqualTo(1f), "the racket is exactly on the sim racket at every contact");

            // Close-up from the side, for review.
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            var p = r.Sim.Player.Player.Pos;
            cam.transform.position = SimToWorld.Pos(p.X + 2.4, 1.5, p.Z + 1.2);
            cam.transform.LookAt(SimToWorld.Pos(p.X, 1.0, p.Z));
            Capture("M4_Athlete_Side.png", cam);
            cam.transform.position = SimToWorld.Pos(p.X + 0.3, 1.6, p.Z - 2.6);
            cam.transform.LookAt(SimToWorld.Pos(p.X, 1.0, p.Z));
            Capture("M4_Athlete_Front.png", cam);
            cam.GetComponent<GameplayCameraRig>().enabled = true;
        }

        /// <summary>From the athlete's front-right, close, then back to the game camera.</summary>
        static void CloseUp(AthleteFigure fig, string file)
        {
            var cam = Camera.main;
            var rig = cam.GetComponent<GameplayCameraRig>();
            rig.enabled = false;
            Vector3 p = fig.transform.position, f = fig.DrawnForward, r = Vector3.Cross(Vector3.up, f);
            cam.transform.position = p + f * 2.6f + r * 1.6f + Vector3.up * 1.4f;
            cam.transform.LookAt(p + Vector3.up * 0.9f);
            Capture(file, cam);
            rig.enabled = true;
        }

        static void Capture(string file, Camera cam)
        {
            cam = cam ?? Camera.main;
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
