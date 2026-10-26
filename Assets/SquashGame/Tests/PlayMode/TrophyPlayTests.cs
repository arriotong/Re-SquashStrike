// ===========================================================
// Assets/SquashGame/Tests/PlayMode/TrophyPlayTests.cs
// P7 phase 6: the trophy cabinet and the Tour Finals' groups, on screen.
//   - the PLAYER tab's cabinet button; shelves by band, biggest at the
//     top; an event won twice is one trophy marked x2; focus lists the
//     wins (season, opponent, score)
//   - the Tour Finals' draw shows the two groups' tables and matches,
//     then the semis and the final
// Writes Captures/P7_Trophies.png and P7_FinalsGroups.png for review.
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
    public class TrophyPlayTests : InputTestFixture
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

        static TrophyRec T(int season, int tier, string ev, string city, string opp, string score, bool home = false)
        {
            return new TrophyRec { Season = season, Slot = season * Tour.SlotsPerSeason + 10, Tier = tier, Event = ev, City = city, OppName = opp, Score = score, Home = home, Opp = 7 };
        }

        [UnityTest]
        public IEnumerator TheCabinetAndTheFinalsGroups()
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

            CareerStore.New("Champion", "ENG", 2, false);
            var c = CareerStore.Current;
            c.S.Trophies.AddRange(new[]
            {
                T(1, (int)Tier.Challenger3, "London Club Open", "London", "Wren Ashdale", "11-4 11-6", true),
                T(1, (int)Tier.Challenger3, "London Club Open", "London", "Aled Yardstead", "11-8 11-9", true),
                T(1, (int)Tier.Challenger6, "Johns Creek Challenger", "Johns Creek", "Farid Whitwick", "11-7 8-11 11-5"),
                T(1, (int)Tier.Challenger15, "Lahore Open", "Lahore", "Selim Kerrigan", "11-9 11-9"),
                T(2, (int)Tier.Bronze, "Open Squash Classic", "New York", "Oskar Lindqvist", "12-10 11-8"),
                T(2, (int)Tier.Gold, "London Championships", "London", "Callum Brereton", "11-6 11-7"),
                T(2, (int)Tier.Diamond, "British Open", "Birmingham", "Rafael Montoya", "11-9 9-11 11-7"),
                T(3, (int)Tier.WorldTourFinals, "Tour Finals", "Cairo", "Rafael Montoya", "11-7 9-11 11-9 11-4"),
            });
            CareerStore.Save();
            fe.Push(new CareerHubScreen());
            yield return null; yield return null;
            var hub = (CareerHubScreen)fe.Top;
            hub.Tabs.Select(3);
            yield return null; yield return null;
            var button = fe.Top.Find("Trophy cabinet  ·  8 titles");
            Assert.That(button, Is.Not.Null, "the PLAYER tab's way in, with the count");
            hub.Focus(button);
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<TrophyCabinetScreen>());
            var cab = (TrophyCabinetScreen)fe.Top;
            Assert.That(cab.Items.Count, Is.EqualTo(7), "the club's open twice is one trophy");
            Assert.That(cab.Items[0].Wins[0].Event, Is.EqualTo("Tour Finals"), "the biggest shelf first");
            var club = cab.Items.Find(i => i.Wins[0].Home);
            Assert.That(club.Wins.Count, Is.EqualTo(2));
            cab.Focus(cab.Items[0]);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P7_Trophies.png");
            fe.Pop();

            // the Finals' groups: a week of the Tour Finals with you in it, two match days played
            fe.Pop();
            while (Tour.HeadlineOf(c.S.Slot) != Tier.WorldTourFinals) c.Skip();
            c.S.Results.Add(new ResultRec { Player = CareerSession.You, Slot = c.S.Slot - 1, Tier = (int)Tier.Diamond, Reached = 0, Points = 40000 });   // surely on the Road's top 8
            c.S.Cash = 100000;
            var e = c.Enter(Tier.WorldTourFinals);
            c.RecordYours(true, "11-8 11-6");
            c.RecordYours(false, "9-11 11-7 8-11");
            fe.Push(new DrawScreen(e));
            yield return null; yield return null; yield return null;
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P7_FinalsGroups.png");
            fe.Pop();
            CareerStore.Delete();
        }
    }
}
