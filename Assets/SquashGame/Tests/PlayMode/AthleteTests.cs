// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AthleteTests.cs
// M4 exit test, in the real warm-up court: the rendered racket sits on
// the simulated racket (sweet spot and face normal), and the hand holds
// it - measured over real feeds with a virtual gamepad swinging.
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
            int contacts = 0;
            bool capturedContact = false;
            r.Stepped += evs => { foreach (var e in evs) if (e.Type == SimEventType.RacketContact) contacts++; };

            float t0 = Time.realtimeSinceStartup;
            int lastContacts = 0;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                // Swing at whatever comes: tap Hit regularly.
                if (((int)((Time.realtimeSinceStartup - t0) * 4f)) % 4 == 0) Press(pad.buttonSouth); else Release(pad.buttonSouth);
                yield return null;

                // The rendered racket vs the simulated racket it was posed from.
                AthleteSnapshot v = fig.Posed;
                Transform racket = fig.Rig.Racket;
                Vector3 sweet = racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0));
                racketErr.Add(Vector3.Distance(sweet, v.RacketHead));
                Vector3 drawnNormal = racket.right;
                Assert.That(Mathf.Abs(Vector3.Dot(drawnNormal, v.RacketFaceNormal.normalized)), Is.GreaterThan(0.999f), "string normal");
                gripAll.Add(fig.GripGap);
                if (contacts != lastContacts)
                {
                    lastContacts = contacts;
                    gripAtContact.Add(fig.GripGap);
                    if (!capturedContact) { capturedContact = true; Capture("M4_Contact_TV.png", null); }
                }
            }
            Release(pad.buttonSouth);

            racketErr.Sort(); gripAll.Sort(); gripAtContact.Sort();
            float worstRacket = racketErr[racketErr.Count - 1];
            float medianGrip = gripAll[gripAll.Count / 2];
            Debug.Log("[M4] frames " + racketErr.Count + ", racket sweet-spot error worst " + (worstRacket * 1000).ToString("0.00") + " mm; " +
                      "grip gap median " + (medianGrip * 1000).ToString("0.0") + " mm, 95th " + (gripAll[(int)(gripAll.Count * 0.95f)] * 1000).ToString("0.0") +
                      " mm; at " + gripAtContact.Count + " contacts: worst " + (gripAtContact.Count > 0 ? (gripAtContact[gripAtContact.Count - 1] * 1000).ToString("0.0") : "-") + " mm");

            Assert.That(worstRacket, Is.LessThan(0.01f), "rendered racket must sit on the simulated racket (< 1 cm)");
            Assert.That(contacts, Is.GreaterThan(0), "no racket contacts in 12 s of feeds");
            Assert.That(medianGrip, Is.LessThan(0.03f), "the hand should hold the racket");

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
