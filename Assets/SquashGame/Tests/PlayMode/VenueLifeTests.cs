// ===========================================================
// Assets/SquashGame/Tests/PlayMode/VenueLifeTests.cs
// 2026-11-22 evening: the legacy courts' life (VenueView.LegacyLife), the
// Tournament of Champions redo (VenueView.GrandCentral) and the referee's
// desk - each court built, things on it moving, and filmed:
// Captures/VENUE_<id>.png (the establishing shot, the match camera, and a
// look back from the front wall at the referee's desk).
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class VenueLifeTests
    {
        static readonly string[] Ids = { "beach", "peak", "orbit", "neon", "reef", "temple", "volcano", "desert", "rooftop", "tour:grandcentral" };

        [UnityTest]
        public IEnumerator TheLegacyCourtsMoveAndTheRefereeSitsBehind()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            var fe = Object.FindFirstObjectByType<SquashGame.UI.FrontEnd>();
            if (fe != null) fe.enabled = false;                                   // (the title screen would change the court every few seconds)
            const int CW = 960, CH = 540;
            var rt = new RenderTexture(CW, CH, 24);
            foreach (var id in Ids)
            {
                Courts.CurrentId = id;
                for (int f = 0; f < 10; f++) yield return null;
                // (the frame-rate governor can rebuild a venue at another detail mid-way: look again then)
                bool moved = false; int count = 0; string probeName = "";
                for (int attempt = 0; attempt < 4 && !moved; attempt++)
                {
                    if (VenueView.Detail != 2) { Debug.Log("[Venue] detail was " + VenueView.Detail + " - back to 2"); VenueView.Detail = 2; for (int f = 0; f < 10; f++) yield return null; }
                    var venue = GameObject.Find("Venue");
                    Assert.That(venue, Is.Not.Null, id);
                    var motions = venue.GetComponentsInChildren<VenueMotion>();
                    count = motions.Length;
                    int all = 0; foreach (var m in Object.FindObjectsByType<VenueMotion>(FindObjectsSortMode.None)) if (m != null) all++;
                    Debug.Log("[Venue] look " + id + " #" + attempt + ": venue " + venue.GetInstanceID() + " under " + (venue.transform.parent ? venue.transform.parent.name : "-") + ", children " + venue.transform.childCount + ", motions " + count + " / scene " + all);
                    if (count == 0) { for (int f = 0; f < 10; f++) yield return null; continue; }
                    Assert.That(GameObject.Find("RefereeDesk"), Is.Not.Null, id + ": the referee's desk");
                    var probe = motions[motions.Length / 2].transform;
                    probeName = probe.name;
                    Vector3 p0 = probe.position; Quaternion r0 = probe.rotation; Vector3 s0 = probe.localScale;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 0.5f) yield return null;
                    if (probe == null) continue;
                    moved = (probe.position - p0).sqrMagnitude > 1e-6f || Quaternion.Angle(probe.rotation, r0) > 0.01f || (probe.localScale - s0).sqrMagnitude > 1e-8f;
                }
                Assert.That(count, Is.GreaterThan(5), id + ": things that move");
                Assert.That(moved, Is.True, id + ": " + probeName + " moves");

                var sheet = new Texture2D(CW * 3, CH, TextureFormat.RGB24, false);
                for (int shot = 0; shot < 3; shot++)
                {
                    if (shot == 0)
                    {
                        cam.fieldOfView = GameplayCameraRig.EstablishingFov;
                        cam.transform.position = SimToWorld.Pos(GameplayCameraRig.EstablishingPos.x, GameplayCameraRig.EstablishingPos.y, GameplayCameraRig.EstablishingPos.z);
                        cam.transform.LookAt(SimToWorld.Pos(GameplayCameraRig.EstablishingLook.x, GameplayCameraRig.EstablishingLook.y, GameplayCameraRig.EstablishingLook.z));
                    }
                    else if (shot == 1)
                    {
                        cam.fieldOfView = 52f;
                        cam.transform.position = SimToWorld.Pos(0, 3.95, 9.75 + 6.6);
                        cam.transform.LookAt(SimToWorld.Pos(0, 1.55, 3.2));
                    }
                    else
                    {
                        cam.fieldOfView = 50f;
                        cam.transform.position = SimToWorld.Pos(1.5, 2.2, 1.0);
                        cam.transform.LookAt(SimToWorld.Pos(0, 2.4, 9.75 + 7.0));
                    }
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CW, CH), shot * CW, 0);
                    cam.targetTexture = null; RenderTexture.active = null;
                }
                sheet.Apply();
                string file = "VENUE_" + id.Replace(":", "_") + ".png";
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file)), sheet.EncodeToPNG());
                Object.Destroy(sheet);
                Debug.Log("[Venue] " + id + ": " + count + " moving");
            }
            rt.Release();
            Courts.CurrentId = "show";
        }
    }
}
