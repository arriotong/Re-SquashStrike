// ===========================================================
// Assets/SquashGame/Tests/PlayMode/CareerPlayTests.cs
// P2-M4: the Career in the real scene, on the keyboard.
//   - main menu -> Career -> create a player (name, nation, matches:
//     one step each) -> the hub; its tabs
//   - CONTINUE enters the home club's open -> the match preview -> the match
//     starts with the players' names, the pro's style and strength
//   - the match is played (an AI stands in for you, sped up) -> post-
//     match Continue -> the result is recorded and saved
//   - a simulated match, the draw (it opens on your match), the season
//     review
//   - in the next match, Quit = retiring: a loss, back to the hub
// Writes Captures/P2M4_UI_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class CareerPlayTests : InputTestFixture
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

        IEnumerator FocusRow(Keyboard kb, FrontEnd fe, string startsWith)
        {
            for (int i = 0; i < 30 && !fe.Top.Focused.Name.StartsWith(startsWith); i++) yield return Tap(kb.downArrowKey);
            Assert.That(fe.Top.Focused.Name, Does.StartWith(startsWith));
        }

        /// <summary>An AI plays for you, sped up, until the match is over.</summary>
        static IEnumerator PlayItOut(FrontEnd fe)
        {
            var r = fe.Match;
            r.Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 77);
            r.SetTimeScale(12);
            yield return Wait(90f, () => fe.Top is PostMatchScreen);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
        }

        [UnityTest]
        public IEnumerator ACareerFromTheMenuToTheHubAndThroughAMatch()
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

            yield return HubNav.Open(fe, kb, Tap, "CAREER", "START A CAREER");
            Assert.That(fe.Top, Is.InstanceOf<CreatePlayerScreen>());
            var create = (CreatePlayerScreen)fe.Top;

            // step 1, the name: typing is the field's job; the test sets it
            var name = (TextItem)fe.Top.Find("Name");
            name.Field.value = "Tester";
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Next"));
            yield return Tap(kb.enterKey);
            yield return null; yield return null;
            Assert.That(create.Step, Is.EqualTo(1));
            // step 2, the nation
            Assert.That(fe.Top.Focused, Is.InstanceOf<CarouselItem>());
            yield return Tap(kb.rightArrowKey);
            Assert.That(((CarouselItem)fe.Top.Focused).Value, Is.EqualTo("SCO"));
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            yield return Tap(kb.enterKey);
            yield return null; yield return null;
            // step 3, the matches
            Assert.That(create.Step, Is.EqualTo(2));
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Match"));
            yield return Tap(kb.leftArrowKey);                                   // Best of 3 -> One game
            Assert.That(((SelectorItem)fe.Top.Focused).Value, Is.EqualTo("One game"));
            yield return CaptureUI(fe, "P2M4_UI_1_CreatePlayer.png");
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Start career"));
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;

            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            var hub = (CareerHubScreen)fe.Top;
            var c = CareerStore.Current;
            Assert.That(c, Is.Not.Null);
            Assert.That(c.S.Name, Is.EqualTo("Tester"));
            Assert.That(c.S.Nat, Is.EqualTo("SCO"));
            Assert.That(c.S.GamesToWin, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(ProfileStore.Folder, CareerStore.FileName)), Is.True, "saved");
            Assert.That(fe.Top.Focused.Name, Does.Contain("Club Open"), "the home club's open is the newcomer's way in");
            Assert.That(hub.Reachable().Count, Is.LessThanOrEqualTo(6), "few choices in view");
            yield return CaptureUI(fe, "P2M4_UI_2_Hub.png");

            // the other tabs: calendar, rankings, player, and round to Central
            foreach (var tab in new[] { "CALENDAR", "RANKINGS", "PLAYER", "GOALS" })
            {
                yield return Tap(kb.eKey);
                yield return null;
                Assert.That(hub.Tabs.Current, Is.EqualTo(tab));
                yield return CaptureUI(fe, "U3_" + tab + ".png");
            }
            yield return Tap(kb.eKey);
            Assert.That(hub.Tabs.Current, Is.EqualTo("CENTRAL"));

            // CONTINUE: into the best event open to you
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Continue"));
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(c.S.Current, Is.Not.Null);
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            Assert.That(fe.Top.Focused.Name, Does.StartWith("Round of 16 v "), "your next match");
            yield return CaptureUI(fe, "U3_Central_Match.png");
            int opp = c.NextOpponent();
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<MatchPreviewScreen>());
            yield return CaptureUI(fe, "P2M4_UI_3_Scouting.png");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Play"));
            yield return Tap(kb.enterKey);

            Assert.That(fe.Top, Is.Null, "into the match");
            var m = fe.Match;
            Assert.That(m.Demo, Is.False);
            Assert.That(m.Settings.Career, Is.True);
            Assert.That(m.Settings.Opponent, Is.EqualTo(c.Pro(opp).OpponentId));
            Assert.That(m.Settings.Difficulty, Is.EqualTo((float)c.Pro(opp).Strength).Within(1e-6));
            Assert.That(m.Names[0], Is.EqualTo("TESTER"));
            Assert.That(m.Names[1], Is.EqualTo(c.Pro(opp).Bug));
            yield return Wait(1f, () => false);
            yield return CaptureUI(fe, "P2M4_UI_4_Match.png");

            yield return PlayItOut(fe);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>());
            bool won = m.Sim.Score.MatchWinner == m.HumanIndex;
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Continue"));
            yield return Tap(kb.enterKey);
            Assert.That(c.S.Matches.Count, Is.EqualTo(1), "the match is recorded");
            Assert.That(c.S.Matches[0].Won, Is.EqualTo(won));
            Assert.That(c.S.Matches[0].Simulated, Is.False);
            Debug.Log("[P2M4] first career match: " + (won ? "won " : "lost ") + c.S.Matches[0].Score + " v " + c.NameOf(opp) + ", rating now " + c.S.Rating.ToString("0.000"));
            yield return null; yield return null;
            if (!won)
            {
                Assert.That(fe.Top, Is.InstanceOf<EventResultScreen>(), "out: the event's result");
                yield return Wait(1.5f, () => false);
                yield return CaptureUI(fe, "P2M4_UI_5_EventResult.png");
                yield return Tap(kb.enterKey);
            }
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());

            // a simulated match: next month's event (or the next round)
            if (c.S.Current == null)
            {
                yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
                yield return Tap(kb.enterKey);                                     // CONTINUE: enter
                yield return null; yield return null; yield return null;
            }
            Assert.That(c.NextOpponent(), Is.GreaterThanOrEqualTo(0));
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            yield return Tap(kb.enterKey);                                         // CONTINUE: the preview
            Assert.That(fe.Top, Is.InstanceOf<MatchPreviewScreen>());
            int simmed = c.S.Matches.Count;
            yield return Tap(kb.fKey);                                             // X: simulate
            yield return null;
            Assert.That(fe.Top, Is.InstanceOf<SimResultScreen>());
            Assert.That(c.S.Matches.Count, Is.EqualTo(simmed + 1));
            Assert.That(c.S.Matches[simmed].Simulated, Is.True);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "U3_SimResult.png");
            yield return Tap(kb.enterKey);
            yield return null; yield return null;
            if (fe.Top is EventResultScreen)
            {
                yield return Wait(1.5f, () => false);
                yield return CaptureUI(fe, "U3_EventResult.png");
                yield return Tap(kb.enterKey);
            }
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());

            // the draw, from the hub's draw card
            if (c.S.Current == null)
            {
                yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
                yield return Tap(kb.enterKey);
                yield return null; yield return null; yield return null;
            }
            for (int i = 0; i < 6 && !(fe.Top.Focused is CardItem card && card.Title == "THE DRAW"); i++) yield return Tap(kb.rightArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<DrawScreen>());
            yield return null; yield return null; yield return null;
            Assert.That(fe.Top.Focused, Is.InstanceOf<BracketMatchItem>());
            Assert.That(((BracketMatchItem)fe.Top.Focused).YouIn, Is.True, "the draw opens on your match");
            yield return CaptureUI(fe, "P2M4_UI_8_Draw.png");
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());

            // a match to retire from
            int before = c.S.Matches.Count;
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            yield return Tap(kb.enterKey);                                         // the preview
            yield return Tap(kb.enterKey);                                         // play
            Assert.That(fe.Top, Is.Null);
            yield return Wait(0.5f, () => false);
            yield return Tap(kb.escapeKey);                                        // pause
            yield return Wait(1f, () => fe.Top is PauseScreen);
            Assert.That(fe.Top, Is.InstanceOf<PauseScreen>());
            Assert.That(fe.Top.Find("Restart match"), Is.Null, "no restarting a career match");
            yield return FocusRow(kb, fe, "Quit: retire");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<RetireScreen>());
            yield return FocusRow(kb, fe, "Retire");
            yield return Tap(kb.enterKey);
            yield return null; yield return null;
            Assert.That(c.S.Matches.Count, Is.EqualTo(before + 1));
            Assert.That(c.S.Matches[before].Retired, Is.True);
            Assert.That(c.S.Matches[before].Won, Is.False, "retiring is a loss");
            Assert.That(fe.Match.Demo, Is.True, "back to the menus");
            if (fe.Top is EventResultScreen) yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            yield return CaptureUI(fe, "U3_Central_AfterMonth.png");
            yield return Tap(kb.eKey); yield return Tap(kb.eKey); yield return Tap(kb.eKey);
            yield return CaptureUI(fe, "U3_PLAYER_History.png");

            // a World Tour draw (a Gold event): rank the player up, move on to it
            c.S.Results.Add(new ResultRec { Player = CareerSession.You, Slot = c.S.Slot - 1, Tier = (int)Tier.Diamond, Reached = 0, Points = 40000 });
            while (c.S.Current != null) { while (c.NextOpponent() >= 0) c.SimulateYours(); c.FinishSlot(); }
            Assert.That(c.S.Last, Is.Not.Null);
            fe.Push(new EventResultScreen(c.S.Last));                                // the result screen, whatever the matches did (skipping a month clears Last)
            yield return Wait(1.5f, () => false);
            yield return CaptureUI(fe, "U3_EventResult.png");
            fe.Pop();
            while (System.Array.IndexOf(c.EventsNow(), Tier.Gold) < 0) c.Skip();
            c.Enter(Tier.Gold);
            c.SimulateYours();
            fe.Push(new DrawScreen(c.S.Current));
            yield return null; yield return null; yield return null;
            yield return CaptureUI(fe, "P2M4_UI_9_TourDraw.png");
            fe.Pop();

            // the season's end: the review
            while (c.NextOpponent() >= 0) c.SimulateYours();
            c.FinishSlot();
            while (!c.S.SeasonJustEnded) c.Skip();
            fe.Replace(new CareerHubScreen());
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<SeasonSummaryScreen>(), "the hub shows the season review");
            yield return Wait(1.5f, () => false);
            yield return CaptureUI(fe, "U3_SeasonSummary.png");
            yield return Tap(kb.enterKey);
            Assert.That(c.S.SeasonJustEnded, Is.False);
            // P3-R4: ranked now, the sponsors call - two offers; sign one
            yield return null; yield return null;
            if (c.OffersWaiting)
            {
                Assert.That(fe.Top, Is.InstanceOf<SponsorScreen>(), "the offers come up after the review");
                yield return Wait(0.6f, () => false);
                yield return CaptureUI(fe, "P3_Sponsor.png");
                c.Sign(0); CareerStore.Save();
                fe.Pop();
                fe.Replace(new CareerHubScreen());
                yield return null; yield return null;
                Assert.That(c.S.Sponsor, Is.Not.Null);
            }
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            ((CareerHubScreen)fe.Top).Tabs.Select(4);                                  // GOALS
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P3_Goals.png");

            // it is all on disk
            CareerStore.Save();
            int matches = c.S.Matches.Count;
            CareerStore.Reset();
            Assert.That(CareerStore.Current.S.Matches.Count, Is.EqualTo(matches), "the career loads back");
            CareerStore.Delete();
        }
    }
}
