// ===========================================================
// Assets/SquashGame/Tests/PlayMode/WorldMapPlayTests.cs
// P7 phase 4: the world map in the real scene, on the keyboard.
//   - the hub's World map card opens it; a pin for each of the week's
//     events and your club's open, on the map where their cities are
//   - focus starts on the biggest event open to you; the arrows move
//     between pins; the panel shows the entry and the trip's cost
//   - a closed pin can't be entered; an open one asks first, then you
//     fly there: your cash pays the trip, the draw is made, back to
//     the hub with your match next
//   - the planner shows the coming weeks
// Writes Captures/P7_Map_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class WorldMapPlayTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false;
            ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            ps.targetTexture = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator TheMapShowsTheWeekAndFliesYouThere()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            // a ranked player in a busy week
            CareerStore.New("Flyer", "ENG", 2, false);
            var c = CareerStore.Current;
            c.S.Results.Add(new ResultRec { Player = CareerSession.You, Slot = c.S.Slot - 1, Tier = (int)Tier.Gold, Reached = 3, Points = 1500 });
            while (Tour.Week(c.S.Slot).Count < 3) c.Skip();
            c.S.Results.Add(new ResultRec { Player = CareerSession.You, Slot = c.S.Slot - 1, Tier = (int)Tier.Gold, Reached = 3, Points = 1500 });
            CareerStore.Save();
            fe.Push(new CareerHubScreen());
            yield return null; yield return null; yield return null;
            var hub = (CareerHubScreen)fe.Top;
            Assert.That(hub.MapCard, Is.Not.Null, "the hub has the map");
            hub.Focus(hub.MapCard);
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<WorldMapScreen>());
            var map = (WorldMapScreen)fe.Top;
            Assert.That(map.Pins.Count, Is.EqualTo(c.Options().Count), "a pin a week's event, and the club's open");
            yield return Wait(1f, () => map.Focused is MapPinItem);
            var first = (MapPinItem)map.Focused;
            Assert.That(first.Option.Open, Is.True, "focus starts on an event open to you");
            Assert.That(first.Option.Home, Is.False, "the biggest open, not the club");
            // where the pins are: their cities, on the map
            var city = World.CityOf(first.Option.City);
            var at = map.Map.Project(city.Lon, city.Lat);
            Assert.That(first.Root.resolvedStyle.left, Is.EqualTo(at.x).Within(4f));
            Assert.That(first.Root.resolvedStyle.top, Is.EqualTo(at.y).Within(4f));
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "P7_Map_ThisWeek.png");

            // browse: the arrows go to another pin
            MenuItem before = map.Focused;
            foreach (var key in new[] { kb.leftArrowKey, kb.rightArrowKey, kb.downArrowKey, kb.upArrowKey })
            {
                yield return Tap(key);
                if (map.Focused != before) break;
            }
            Assert.That(map.Focused, Is.Not.SameAs(before), "the arrows move between pins");
            Assert.That(map.Focused, Is.InstanceOf<MapPinItem>());

            // a closed pin does nothing
            var closed = map.Pins.Find(p => !p.Option.Open);
            if (closed != null) { map.Focus(closed); yield return Tap(kb.enterKey); Assert.That(fe.Top, Is.SameAs(map), "closed: nothing"); }

            // the planner
            map.Tabs.Select(1);
            yield return null; yield return null;
            yield return CaptureUI(fe, "P7_Map_Planner.png");
            map.Tabs.Select(0);
            yield return null; yield return null;

            // fly to the open event
            map.Focus(first);
            yield return null;
            int cash = c.S.Cash, cost = first.Option.Cost;
            yield return Tap(kb.enterKey);
            yield return null;
            Assert.That(fe.Top, Is.InstanceOf<ConfirmScreen>(), "a trip asks first");
            yield return CaptureUI(fe, "P7_Map_Confirm.png");
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>(), "back to the hub");
            Assert.That(c.S.Current, Is.Not.Null, "in the event");
            Assert.That(c.S.Current.Name, Is.EqualTo(first.Option.Name));
            Assert.That(c.S.City, Is.EqualTo(first.Option.City), "you flew there");
            Assert.That(c.S.Cash, Is.EqualTo(cash - cost), "the trip paid");
            Assert.That(((CareerHubScreen)fe.Top).MatchCard, Is.Not.Null, "your match next");
            CareerStore.Delete();
        }
    }
}
