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

        [Test]
        public void TheVenuesAreTheCalendarsChallengers()
        {
            Assert.That(ClubVenues.All.Length, Is.EqualTo(54), "40 venues (the user: \"pick maybe like 40\") and 14 generic city clubs");
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
            Assert.That(events, Is.EqualTo(55), "Mumbai's two events share JVPG");
            Assert.That(Courts.ForCareerEvent("British Open", (int)Tier.Diamond), Is.EqualTo(Courts.ForCareerTier((int)Tier.Diamond)), "the rest: their tier's court");
            Assert.That(Courts.ForCareerEvent("Toronto Open", (int)Tier.Challenger15), Is.EqualTo(Courts.ForCareerTier((int)Tier.Challenger15)), "a reuse: its tier's court until Toronto's is built");
            Assert.That(Courts.Find("venue:nowhere"), Is.Null);
        }
    }
}
