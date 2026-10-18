// ===========================================================
// Assets/SquashGame/Tests/PlayMode/CareerPlayTests.cs
// P2-M4: the Career in the real scene, on the keyboard.
//   - main menu -> Career -> New career -> create a player -> the hub
//   - enter a Club event -> scouting -> the match starts with the
//     players' names, the pro's style and strength
//   - the match is played (an AI stands in for you, sped up) -> post-
//     match Continue -> the result is recorded and saved
//   - rankings, calendar and the draw open
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

            yield return FocusRow(kb, fe, "Career");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<CareerStartScreen>());
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("New career"));
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<CreatePlayerScreen>());

            // the name: typing is the field's job; the test sets it
            var name = (TextItem)fe.Top.Find("Name");
            name.Field.value = "Tester";
            yield return FocusRow(kb, fe, "Match");
            yield return Tap(kb.leftArrowKey);                                   // Best of 3 -> One game
            Assert.That(((OptionItem)fe.Top.Focused).Value, Is.EqualTo("One game"));
            yield return CaptureUI(fe, "P2M4_UI_1_CreatePlayer.png");
            yield return FocusRow(kb, fe, "Start career");
            yield return Tap(kb.enterKey);

            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            var c = CareerStore.Current;
            Assert.That(c, Is.Not.Null);
            Assert.That(c.S.Name, Is.EqualTo("Tester"));
            Assert.That(c.S.GamesToWin, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(ProfileStore.Folder, CareerStore.FileName)), Is.True, "saved");
            Assert.That(fe.Top.Focused.Name, Does.Contain("Club"), "the Club event is the one open to a newcomer");
            yield return CaptureUI(fe, "P2M4_UI_2_Hub.png");

            yield return Tap(kb.enterKey);                                       // enter it
            Assert.That(c.S.Current, Is.Not.Null);
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());
            Assert.That(fe.Top.Focused.Name, Does.StartWith("Next: Round of 16 v "));
            int opp = c.NextOpponent();
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<ScoutingScreen>());
            yield return CaptureUI(fe, "P2M4_UI_3_Scouting.png");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Play the match"));
            yield return Tap(kb.enterKey);

            Assert.That(fe.Top, Is.Null, "into the match");
            var m = fe.Match;
            Assert.That(m.Demo, Is.False);
            Assert.That(m.Settings.Career, Is.True);
            Assert.That(m.Settings.Opponent, Is.EqualTo(c.Pro(opp).Style));
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
                yield return CaptureUI(fe, "P2M4_UI_5_EventResult.png");
                yield return Tap(kb.enterKey);
            }
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());

            // rankings, calendar
            yield return FocusRow(kb, fe, "Rankings");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<RankingsScreen>());
            yield return CaptureUI(fe, "P2M4_UI_6_Rankings.png");
            yield return Tap(kb.escapeKey);
            yield return FocusRow(kb, fe, "Calendar");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<CalendarScreen>());
            yield return CaptureUI(fe, "P2M4_UI_7_Calendar.png");
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<CareerHubScreen>());

            // a match to retire from: still in the event, or next month's
            if (c.S.Current == null)
            {
                yield return Tap(kb.upArrowKey);
                while (!fe.Top.Focused.Name.Contains("Club")) yield return Tap(kb.upArrowKey);
                yield return Tap(kb.enterKey);
            }
            yield return FocusRow(kb, fe, "View the draw");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<DrawScreen>());
            yield return CaptureUI(fe, "P2M4_UI_8_Draw.png");
            yield return Tap(kb.escapeKey);
            int before = c.S.Matches.Count;
            yield return Tap(kb.upArrowKey);
            while (!fe.Top.Focused.Name.StartsWith("Next:")) yield return Tap(kb.upArrowKey);
            yield return Tap(kb.enterKey);                                         // scouting
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

            // a 32-player draw (a Tour event): rank the player up, move on to March
            c.S.Results.Add(new ResultRec { Player = CareerSession.You, Slot = c.S.Slot - 1, Tier = 4, Reached = 0, Points = 9000 });
            while (c.S.Current != null) { while (c.NextOpponent() >= 0) c.SimulateYours(); c.FinishSlot(); }
            while (System.Array.IndexOf(c.EventsNow(), Tier.Tour) < 0) c.Skip();
            c.Enter(Tier.Tour);
            c.SimulateYours();
            fe.Push(new DrawScreen(c.S.Current));
            yield return null;
            yield return CaptureUI(fe, "P2M4_UI_9_TourDraw.png");
            fe.Pop();

            // it is all on disk
            CareerStore.Save();
            int matches = c.S.Matches.Count;
            CareerStore.Reset();
            Assert.That(CareerStore.Current.S.Matches.Count, Is.EqualTo(matches), "the career loads back");
            CareerStore.Delete();
        }
    }
}
