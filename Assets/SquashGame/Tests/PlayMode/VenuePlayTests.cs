// ===========================================================
// Assets/SquashGame/Tests/PlayMode/VenuePlayTests.cs
// Courts per tournament, part 1: every Challenger venue (ClubVenues)
// builds in the real scene and is filmed from the TV camera; a career
// event at a venue plays there, the rest on their tier's court.
// Writes Captures/VENUES.png (a contact sheet, 8 across) and three big
// frames (one of each layout) for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class VenuePlayTests
    {
        [UnityTest]
        public IEnumerator EveryChallengerVenueBuildsAndIsFilmed()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            const int CW = 400, CHt = 225, Across = 8;
            int n = ClubVenues.All.Length;
            int down = (n + Across - 1) / Across;
            var sheet = new Texture2D(CW * Across, CHt * down, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CW, CHt, 24);
            var big = new RenderTexture(1600, 900, 24);
            foreach (ClubKind k in System.Enum.GetValues(typeof(ClubKind))) Debug.Log("[venues] " + k + ": " + System.Array.FindAll(ClubVenues.All, v => v.Kind == k).Length);
            for (int i = 0; i < n; i++)
            {
                var v = ClubVenues.All[i];
                Courts.CurrentId = Courts.VenuePrefix + v.Key;
                yield return null; yield return null; yield return null;
                Assert.That(Courts.Current.Id, Is.EqualTo(Courts.VenuePrefix + v.Key));
                Assert.That(GameObject.Find("Venue"), Is.Not.Null, v.Key + ": no venue");
                Assert.That(GameObject.Find("Banner"), Is.Not.Null, v.Key + ": its name on the wall");
                cam.fieldOfView = 24f;
                cam.transform.position = SimToWorld.Pos(0, 6.0, 22.25);
                cam.transform.LookAt(SimToWorld.Pos(0, 0.9, 1.5));
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, CW, CHt), (i % Across) * CW, (down - 1 - i / Across) * CHt);
                // one big frame of each layout, from behind the glass (looking back into the hall too)
                if (v.Key == "dublin" || v.Key == "madrid" || v.Key == "karachi")
                {
                    foreach (var (name, pos, look) in new[] { ("front", SimToWorld.Pos(0, 6.0, 22.25), SimToWorld.Pos(0, 0.9, 1.5)), ("hall", SimToWorld.Pos(2.5, 3.0, 2.0), SimToWorld.Pos(-1.5, 3.5, 20.0)) })
                    {
                        cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = 50f;
                        cam.targetTexture = big; cam.Render();
                        RenderTexture.active = big;
                        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "VENUE_" + v.Key + "_" + name + ".png")), tex.EncodeToPNG());
                        Object.Destroy(tex);
                    }
                }
                cam.targetTexture = null; RenderTexture.active = null;
            }
            sheet.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "VENUES.png")), sheet.EncodeToPNG());
            Object.Destroy(sheet); rt.Release(); big.Release();
            Courts.CurrentId = "show";
        }

        /// <summary>Courts per tournament, part 2: every World Tour stage builds and is filmed - from the TV camera and the high
        /// establishing shot (Captures/TOUR_TV.png, TOUR_WIDE.png, and each stage's own VENUE_tour_KEY.png).</summary>
        [UnityTest]
        public IEnumerator EveryWorldTourStageBuildsAndIsFilmed()
        {
            VenueView.Detail = 2;
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var cam = Camera.main;
            cam.GetComponent<GameplayCameraRig>().enabled = false;
            const int CW = 480, CHt = 270, Across = 5;
            int n = TourVenues.All.Length, down = (n + Across - 1) / Across;
            var tv = new Texture2D(CW * Across, CHt * down, TextureFormat.RGB24, false);
            var wide = new Texture2D(CW * Across, CHt * down, TextureFormat.RGB24, false);
            var rt = new RenderTexture(CW, CHt, 24);
            var big = new RenderTexture(1600, 900, 24);
            for (int i = 0; i < n; i++)
            {
                var v = TourVenues.All[i];
                Courts.CurrentId = Courts.TourPrefix + v.Key;
                yield return null; yield return null; yield return null;
                Assert.That(Courts.Current.Id, Is.EqualTo(Courts.TourPrefix + v.Key));
                Assert.That(GameObject.Find("Venue"), Is.Not.Null, v.Key + ": no venue");
                Assert.That(Camera.main.farClipPlane, Is.GreaterThanOrEqualTo(v.FarClip - 1f), v.Key + ": the camera must see its whole venue");
                for (int shot = 0; shot < 2; shot++)
                {
                    if (shot == 0) { cam.fieldOfView = 34f; cam.transform.position = SimToWorld.Pos(0, 6.0, 22.25); cam.transform.LookAt(SimToWorld.Pos(0, 2.2, 0)); }
                    else
                    {
                        cam.fieldOfView = GameplayCameraRig.EstablishingFov;
                        cam.transform.position = SimToWorld.Pos(GameplayCameraRig.EstablishingPos.x, GameplayCameraRig.EstablishingPos.y, GameplayCameraRig.EstablishingPos.z);
                        cam.transform.LookAt(SimToWorld.Pos(GameplayCameraRig.EstablishingLook.x, GameplayCameraRig.EstablishingLook.y, GameplayCameraRig.EstablishingLook.z));
                    }
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    (shot == 0 ? tv : wide).ReadPixels(new Rect(0, 0, CW, CHt), (i % Across) * CW, (down - 1 - i / Across) * CHt);
                }
                cam.fieldOfView = 34f; cam.transform.position = SimToWorld.Pos(0, 6.0, 22.25); cam.transform.LookAt(SimToWorld.Pos(0, 2.2, 0));
                cam.targetTexture = big; cam.Render();
                RenderTexture.active = big;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "VENUE_tour_" + v.Key + ".png")), tex.EncodeToPNG());
                Object.Destroy(tex);
                cam.targetTexture = null; RenderTexture.active = null;
            }
            tv.Apply(); wide.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "TOUR_TV.png")), tv.EncodeToPNG());
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "TOUR_WIDE.png")), wide.EncodeToPNG());
            Object.Destroy(tv); Object.Destroy(wide); rt.Release(); big.Release();
            Courts.CurrentId = "show";
        }

        [Test]
        public void TheWorldTourStagesAreTheirEvents()
        {
            foreach (var v in TourVenues.All)
            {
                Assert.That(World.CityOf(v.City), Is.Not.Null, v.Key + ": " + v.City);
                foreach (var e in v.Events)
                {
                    Assert.That(System.Array.Exists(Tour.Calendar, x => x.Name == e), Is.True, v.Key + ": '" + e + "' is on the calendar");
                    Assert.That(Courts.ForCareerEvent(e, 0), Is.EqualTo(Courts.TourPrefix + v.Key), e);
                }
                if (v.FinalsCity != null) Assert.That(Courts.ForCareerEvent("Tour Finals", (int)Tier.WorldTourFinals, v.FinalsCity), Is.EqualTo(Courts.TourPrefix + v.Key));
                Assert.That(Courts.Find(Courts.TourPrefix + v.Key).Name, Is.EqualTo(v.Name));
            }
            // every season's Finals host has a stage (Kuala Lumpur: the Bukit Jalil centre)
            foreach (var city in Tour.FinalsCities)
                Assert.That(Courts.IsVenue(Courts.ForCareerEvent("Tour Finals", (int)Tier.WorldTourFinals, city)), Is.True, city);
            Assert.That(Courts.ForCareerEvent("Tour Finals", (int)Tier.WorldTourFinals, "Kuala Lumpur"), Is.EqualTo(Courts.VenuePrefix + "kualalumpur"));
        }

        [Test]
        public void TheVenuesAreTheCalendarsChallengers()
        {
            Assert.That(ClubVenues.All.Length, Is.EqualTo(53), "39 venues (Alexandria moved to the Bibliotheca stage) (the user: \"pick maybe like 40\") and 14 generic city clubs");
            var keys = new System.Collections.Generic.HashSet<string>();
            int events = 0;
            foreach (var v in ClubVenues.All)
            {
                Assert.That(keys.Add(v.Key), Is.True, v.Key);
                Assert.That(World.CityOf(v.City), Is.Not.Null, v.Key + ": " + v.City + " is on the map");
                foreach (var e in v.Events)
                {
                    var ev = System.Array.Find(Tour.Calendar, x => x.Name == e);
                    Assert.That(ev, Is.Not.Null, v.Key + ": '" + e + "' is on the calendar");
                    Assert.That(ev.City, Is.EqualTo(v.City), e + " is played in " + v.City);
                    Assert.That(Tour.Info(ev.Tier).WorldTour, Is.False, e + " is a Challenger");
                    Assert.That(Courts.ForCareerEvent(e, (int)ev.Tier), Is.EqualTo(Courts.VenuePrefix + v.Key));
                    Assert.That(Courts.ForCareerEvent(e + " Qualifying", (int)ev.Tier), Is.EqualTo(Courts.VenuePrefix + v.Key));
                    events++;
                }
                var t = Courts.Find(Courts.VenuePrefix + v.Key);
                Assert.That(t, Is.Not.Null); Assert.That(t.Name, Is.EqualTo(v.Venue));
                Assert.That(t.Walls, Is.EqualTo(v.Kind == ClubKind.Centre ? WallStyle.Glass : WallStyle.Plaster));
                Assert.That(ClubVenues.Sign(v.Venue), Does.Match("^[A-Z0-9 .:-]+$"), "the LED font has the letters");
            }
            Assert.That(events, Is.EqualTo(54), "Mumbai's two events share JVPG");
            Assert.That(Courts.ForCareerEvent("British Open", (int)Tier.Diamond), Is.EqualTo(Courts.TourPrefix + "rep"), "a World Tour stage of its own");
            Assert.That(Courts.ForCareerEvent("Charlottesville Open", (int)Tier.Copper), Is.EqualTo(Courts.ForCareerTier((int)Tier.Copper)), "the rest: their tier's court");
            Assert.That(Courts.ForCareerEvent("Toronto Open", (int)Tier.Challenger15), Is.EqualTo(Courts.ForCareerTier((int)Tier.Challenger15)), "a reuse: its tier's court until Toronto's is built");
            Assert.That(Courts.Find("venue:nowhere"), Is.Null);
        }
    }
}
