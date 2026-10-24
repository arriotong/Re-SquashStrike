// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PresentationTests.cs
// The presentation pass (PR1-PR5): the venue builds at every detail
// level and switches live, and the match is captured for review.
// Writes Captures/PR1_Venue*.png, PR2_Ball*.png and PR3_Hair.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PresentationTests
    {
        static MatchRunner StartQuickGame()
        {
            var court = Object.FindFirstObjectByType<MatchCourt>();
            Assert.That(court, Is.Not.Null, "MatchCourt not in scene");
            if (court.Runner.InSetup || court.Runner.Demo)
            {
                var fe = Object.FindFirstObjectByType<UI.FrontEnd>();
                if (fe != null) fe.StartMatch(MatchSettings.QuickGame()); else court.Runner.BeginMatch(MatchSettings.QuickGame());
            }
            return court.Runner;
        }

        [TearDown]
        public void Restore() { VenueView.Detail = 2; }

        [UnityTest]
        public IEnumerator VenueBuildsAndSwitchesLive()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            Assert.That(GameObject.Find("Venue"), Is.Not.Null, "no venue at High");
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length, Is.GreaterThan(4), "High should add the court's spots");
            Assert.That(CrowdBuilder.PeopleBuilt, Is.GreaterThan(1500), "High seats a 3D crowd");
            Debug.Log("[Crowd] " + CrowdBuilder.PeopleBuilt + " people");

            VenueView.Detail = 0;
            yield return null; yield return null;
            Assert.That(GameObject.Find("Venue"), Is.Null, "Off should be the plain court");
            Assert.That(GameObject.Find("Floor"), Is.Not.Null, "the court must still be there");
            Assert.That(RenderSettings.fog, Is.False);

            VenueView.Detail = 1;
            yield return null; yield return null;
            Assert.That(GameObject.Find("Venue"), Is.Not.Null, "no venue at Low");
            Assert.That(GameObject.Find("CourtSpot"), Is.Null, "Low leaves out the extra lights");
        }

        [UnityTest]
        public IEnumerator CaptureTheMatch()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            yield return WaitReal(1.5f);
            Capture("PR1_Venue_Menu.png");                    // the menus' demo rally (cinematic camera)
            StartQuickGame();
            var flyRig = Camera.main.GetComponent<GameplayCameraRig>();
            yield return null; yield return null;
            Assert.That(flyRig.FlyingIn, Is.True, "a match opens on the establishing shot");
            yield return WaitReal(GameplayCameraRig.FlySeconds * 0.3f);
            Capture("PR7_FlyIn.png");
            yield return WaitReal(GameplayCameraRig.FlySeconds * 0.9f);
            Assert.That(flyRig.FlyingIn, Is.False, "the fly-in ends on the match camera");
            Capture("PR1_Venue.png");

            // The frame cost at High, for the record (batch mode timings are only indicative).
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 60; i++) cam.Render();
            GL.Flush();
            cam.targetTexture = null;
            rt.Release();
            Debug.Log("[PR1] 1080p render at High: " + (sw.Elapsed.TotalMilliseconds / 60).ToString("0.00") + " ms a frame (CPU side)");

            // The crowd up close: from the court, looking up into the side stand, and from the glass at the front stand.
            var rig = cam.GetComponent<GameplayCameraRig>();
            rig.enabled = false;
            float fov = cam.fieldOfView;
            cam.fieldOfView = 55f;
            cam.transform.position = SimToWorld.Pos(-1.5, 1.6, 6.0);
            cam.transform.LookAt(SimToWorld.Pos(-8.0, 3.2, 8.0));
            Capture("PR6_Crowd_Side.png");
            cam.transform.position = SimToWorld.Pos(0.5, 4.8, 11.5);
            cam.transform.LookAt(SimToWorld.Pos(0, 4.0, 0));
            Capture("PR6_Court_High.png");
            cam.fieldOfView = GameplayCameraRig.EstablishingFov;
            cam.transform.position = SimToWorld.Pos(GameplayCameraRig.EstablishingPos.x, GameplayCameraRig.EstablishingPos.y, GameplayCameraRig.EstablishingPos.z);
            cam.transform.LookAt(SimToWorld.Pos(GameplayCameraRig.EstablishingLook.x, GameplayCameraRig.EstablishingLook.y, GameplayCameraRig.EstablishingLook.z));
            Capture("PR7_Hall.png");
            cam.fieldOfView = 80f;
            cam.transform.position = SimToWorld.Pos(9, 8, 14);
            cam.transform.LookAt(SimToWorld.Pos(0, 9, -5));
            Capture("PR7_Roof.png");
            cam.fieldOfView = fov;
            rig.enabled = true;
            yield return null;

            VenueView.Detail = 0;
            yield return null; yield return null;
            Capture("PR1_Venue_Off.png");
            VenueView.Detail = 1;
            yield return null; yield return null;
            Capture("PR1_Venue_Low.png");
        }

        /// <summary>PR2: the menus' AI-v-AI rally, filmed from the TV camera: every bounce gets a mark, and the
        /// ball is never smaller than BallView.MinPixels on screen, even at the front wall.</summary>
        [UnityTest]
        public IEnumerator BallStaysVisibleAndBouncesAreMarked()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            Assert.That(court.Runner.Demo, Is.True, "the menus should be playing their demo rally");
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            cam.fieldOfView = 52f;
            cam.transform.position = SimToWorld.Pos(0, 3.95, 16.35);
            cam.transform.LookAt(SimToWorld.Pos(0, 1.55, 3.2));
            var ball = Object.FindFirstObjectByType<BallView>();
            var marks = Object.FindFirstObjectByType<ImpactMarkView>();

            float minSeen = 999f, minZ = 99f, lastZ = 99f; bool captured = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f && (!captured || marks.Shown < 3))
            {
                yield return null;
                Vector3 p = court.Runner.View.BallPos;
                minZ = Mathf.Min(minZ, p.z);
                bool moving = Mathf.Abs(p.z - lastZ) > 1e-4f;
                lastZ = p.z;
                if (p.z > 1.2f || !moving) continue;
                // On-screen diameter of the drawn ball, at a 1600 x 900 capture.
                float d = Vector3.Dot(p - cam.transform.position, cam.transform.forward);
                float px = ball.DrawnDiameter / (2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * cam.pixelHeight;
                minSeen = Mathf.Min(minSeen, px * 1080f / cam.pixelHeight);
                if (!captured) { Capture("PR2_BallFar.png"); captured = true; }
            }
            Assert.That(captured, Is.True, "the ball never reached the front wall in 25 s (closest z " + minZ + ")");
            Assert.That(minSeen, Is.GreaterThanOrEqualTo(BallView.MinPixels - 0.5f), "the ball drew smaller than the minimum");
            Assert.That(marks.Shown, Is.GreaterThanOrEqualTo(3), "bounces should leave marks");
            Debug.Log("[PR2] smallest ball at the front wall: " + minSeen.ToString("0.0") + " px at 1080p; marks " + marks.Shown);
        }

        /// <summary>PR3: every hair style drawn on a paused athlete, face on and from behind, in one contact sheet.</summary>
        [UnityTest]
        public IEnumerator EveryHairStyleIsDrawn()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<MatchCourt>();
            yield return WaitReal(0.5f);
            court.Runner.Clock.Pause();
            var fig = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteFigure>();
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            const int CELL = 320;
            int n = Progression.Cosmetics.HairStyles.Length;
            var sheet = new Texture2D(CELL * n, CELL * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CELL, CELL, 24);
            string[] colours = { "haircol_black", "haircol_darkbrown", "haircol_brown", "haircol_auburn", "haircol_blonde", "haircol_platinum", "haircol_black" };
            string[] skins = { "skin_1", "skin_2", "skin_3", "skin_4", "skin_5", "skin_6", "skin_3" };
            for (int i = 0; i < n; i++)
            {
                var l = new Progression.Loadout { Hair = "hair_" + Progression.Cosmetics.HairStyles[i].id, HairColour = colours[i], Skin = skins[i], Shirt = "shirt_royal" };
                fig.KitSource = () => l;
                yield return null; yield return null;
                Assert.That(fig.Rig.HairStyleShown, Is.EqualTo(i), "style " + i + " not shown");
                Transform head = fig.Rig.Bones["Head"];
                Vector3 h = head.position + Vector3.up * 0.06f, f = fig.DrawnForward;
                for (int row = 0; row < 2; row++)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, f);
                    cam.fieldOfView = 30f;
                    cam.transform.position = h + (row == 0 ? f * 0.75f + side * 0.15f : -f * 0.7f + side * 0.35f) + Vector3.up * 0.05f;
                    cam.transform.LookAt(h);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CELL, CELL), i * CELL, (1 - row) * CELL);
                    cam.targetTexture = null;
                    RenderTexture.active = null;
                }
            }
            sheet.Apply();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "PR3_Hair.png"));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.Destroy(sheet); rt.Release();
        }

        /// <summary>PR4: the crowd hushes in a rally and applauds when it ends (an AI plays for you).</summary>
        [UnityTest]
        public IEnumerator CrowdHushesThenApplauds()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var r = StartQuickGame();
            var crowd = Object.FindFirstObjectByType<Audio.CrowdAudio>();
            Assert.That(crowd, Is.Not.Null);
            // PR5: the walk-on card as the match starts.
            var fe = Object.FindFirstObjectByType<UI.FrontEnd>();
            bool intro = false;
            for (int i = 0; i < 30 && !intro; i++) { yield return null; intro = fe.Hud.IntroShown; }
            Assert.That(intro, Is.True, "the walk-on card should show as the match starts");
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("attacker").WithDifficulty(0.8), 3));
            float inRally = -1f, maxBetween = 0f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f && !crowd.Played.ContainsKey("applause"))
            {
                yield return null;
                if (r.Sim.Phase == SquashSim.MatchPhase.Rally && r.Sim.Referee.Shots >= 2) inRally = crowd.BedLevel;
                else maxBetween = Mathf.Max(maxBetween, crowd.BedLevel);
            }
            Assert.That(crowd.Played.ContainsKey("applause"), Is.True, "no applause after a rally in 40 s");
            Assert.That(inRally, Is.GreaterThanOrEqualTo(0f), "never saw a rally");
            Assert.That(inRally, Is.LessThan(0.15f), "the crowd should hush while the ball is in play");
            Debug.Log("[PR4] bed in rally " + inRally.ToString("0.00") + ", between " + maxBetween.ToString("0.00"));
        }

        /// <summary>Every court, built and filmed from the match camera (top) and the establishing shot (bottom):
        /// Captures/COURTS.png.</summary>
        [UnityTest]
        public IEnumerator EveryCourtBuildsAndIsFilmed()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var cam = Camera.main;
            var rig = cam.GetComponent<GameplayCameraRig>();
            rig.enabled = false;
            const int CW = 640, CHt = 360;
            int n = Courts.All.Length;
            var sheet = new Texture2D(CW * n, CHt * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CW, CHt, 24);
            for (int i = 0; i < n; i++)
            {
                Courts.CurrentId = Courts.All[i].Id;
                yield return null; yield return null; yield return null;
                Assert.That(GameObject.Find("Venue"), Is.Not.Null, Courts.All[i].Id + ": no venue");
                Assert.That(Camera.main.farClipPlane, Is.GreaterThanOrEqualTo(Courts.All[i].FarClip - 1f), Courts.All[i].Id + ": the camera must see its sky");
                for (int row = 0; row < 2; row++)
                {
                    if (row == 0)
                    {
                        cam.fieldOfView = 52f;
                        cam.transform.position = SimToWorld.Pos(0, 3.95, 16.35);
                        cam.transform.LookAt(SimToWorld.Pos(0, 1.55, 3.2));
                    }
                    else
                    {
                        cam.fieldOfView = GameplayCameraRig.EstablishingFov;
                        cam.transform.position = SimToWorld.Pos(GameplayCameraRig.EstablishingPos.x, GameplayCameraRig.EstablishingPos.y, GameplayCameraRig.EstablishingPos.z);
                        cam.transform.LookAt(SimToWorld.Pos(GameplayCameraRig.EstablishingLook.x, GameplayCameraRig.EstablishingLook.y, GameplayCameraRig.EstablishingLook.z));
                    }
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CW, CHt), i * CW, (1 - row) * CHt);
                    cam.targetTexture = null;
                    RenderTexture.active = null;
                }
            }
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "COURTS.png")), sheet.EncodeToPNG());
            Object.Destroy(sheet); rt.Release();
            Courts.CurrentId = "show";
        }

        static IEnumerator WaitReal(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        public static void Capture(string file, int w = 1600, int h = 900)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(w, h, 24);
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
