// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ScreenTourTests.cs
// The screen tour (2026-10-30, "would a 5-year-old get it?"): every
// screen and every tab, a picture of it and every word it shows, into
// Captures/tour - one PNG and one .txt each, and an index. A person
// reads them through; the test only checks that each screen opened.
// ===========================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SquashGame.Tests
{
    public class ScreenTourTests : InputTestFixture
    {
        static string Dir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "tour")); } }
        int _n;
        readonly List<string> _index = new List<string>();

        static bool Shown(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden || p.resolvedStyle.opacity < 0.05f) return false;
            var b = e.worldBound;
            return b.width > 1 && b.height > 1 && !float.IsNaN(b.x);
        }

        /// <summary>Every word on the screen, top to bottom, and the buttons that have no words.</summary>
        static string Words(FrontEnd fe)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<string>();
            fe.Doc.rootVisualElement.Query<VisualElement>().ForEach(e =>
            {
                if (!Shown(e)) return;
                if (e is TextElement t && !string.IsNullOrWhiteSpace(t.text))
                {
                    string line = t.text.Replace("\n", " / ").Trim();
                    string key = line + "@" + (int)e.worldBound.y;
                    if (seen.Add(key)) sb.Append(e is Button ? "[button] " : "").Append(line).AppendLine();
                }
                else if (e.focusable && e.childCount == 0 && !(e is TextElement))
                    sb.Append("[icon-only control: ").Append(string.Join(".", e.GetClasses())).AppendLine("]");
            });
            return sb.ToString();
        }

        IEnumerator Shot(FrontEnd fe, string name)
        {
            yield return null; yield return null;
            _n++;
            string file = _n.ToString("00") + "_" + name.Replace(" ", "_").Replace("/", "-");
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, file + ".txt"), "# " + name + "  (" + (fe.Top != null ? fe.Top.GetType().Name : "-") + ")\n" + Words(fe));
            yield return FrontEndTests.CaptureUI(fe, Path.Combine("tour", file + ".png"));
            _index.Add(file + "  " + name);
        }

        /// <summary>Open the screen, picture each of its tabs, close it.</summary>
        IEnumerator Visit(FrontEnd fe, string name, Func<MenuScreen> make)
        {
            MenuScreen s, below = fe.Top;
            try { s = make(); fe.Push(s); }
            catch (Exception e) { _index.Add("!! " + name + ": " + e.Message); yield break; }
            yield return null; yield return null;
            if (s is GameScreen g && g.Tabs != null && g.Tabs.Count > 1)
                for (int t = 0; t < g.Tabs.Count; t++) { g.Tabs.Select(t); yield return Shot(fe, name + " - " + g.Tabs.Current); }
            else yield return Shot(fe, name);
            for (int i = 0; i < 8 && fe.Top != null && fe.Top != below; i++) fe.Pop();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TourEveryScreen()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            if (Directory.Exists(Dir)) foreach (var old in Directory.GetFiles(Dir)) File.Delete(old);     // the files, not the folder (a viewer may hold it open)
            FrontEnd.StartAtTitle = true;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f && (UnityEngine.Object.FindFirstObjectByType<FrontEnd>() == null || UnityEngine.Object.FindFirstObjectByType<FrontEnd>().Top == null)) yield return null;
            var fe = UnityEngine.Object.FindFirstObjectByType<FrontEnd>();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot(fe, "Title");
            FrontEnd.StartAtTitle = false;

            yield return Visit(fe, "Home", () => new HomeScreen());
            foreach (var hub in new[] { MainMenuScreen.Hub.Play, MainMenuScreen.Hub.Others })
                yield return Visit(fe, "Hub " + hub, () => new MainMenuScreen(hub));

            var screens = new List<(string, Func<MenuScreen>)>
            {
                ("Quick Match", () => new QuickMatchScreen()), ("Local Versus", () => new LocalVersusScreen()),
                ("Opponent pick", () => new OpponentPickScreen()), ("Court pick", () => new CourtPickScreen(false)), ("Class pick", () => new ClassPickScreen(0, false)),
                ("Quick Match ready", () => new QuickMatchReadyScreen()), ("Local Versus ready", () => new LocalVersusReadyScreen()),
                ("Target Arcade", () => new TargetArcadeScreen()), ("Levels", () => new LevelScreen(SquashSim.Arcade.ArcadeMode.TimeAttack)),
                ("Practice", () => new PracticeScreen()), ("Academy", () => new AcademyScreen()), ("Challenges", () => new ChallengesScreen()),
                ("Scenarios", () => new ScenarioScreen()), ("Gauntlet", () => new GauntletScreen()), ("Deck", () => new DeckScreen()),
                ("Talents", () => new TalentsScreen()), ("Store", () => new StoreScreen()), ("Gold plate", () => new GoldPlateScreen()),
                ("Locker Room", () => new LockerRoomScreen()), ("Profile", () => new ProfileScreen()), ("Stats", () => new StatsScreen()),
                ("Achievements", () => new AchievementsScreen()), ("Codex", () => new CodexScreen()), ("Gallery", () => new GalleryScreen()),
                ("Replays", () => new ReplayLibraryScreen()), ("Leaderboard", () => new LeaderboardScreen()),
                ("Online name", () => new OnlineNameScreen(() => new HomeScreen())), ("Private room", () => new PrivateRoomScreen()),
                ("Settings", () => new SettingsScreen()), ("Controls", () => new ControlsHelpScreen()), ("How to win", () => new RulesBookScreen(11)), ("Rebind", () => new RebindScreen()),
                ("Create player", () => new CreatePlayerScreen()), ("Player editor", () => new PlayerEditorScreen()),
                ("Confirm", () => new ConfirmScreen("Quit the game?", () => { })),
                ("Pick list", () => new PickListScreen("Court", new[] { "Club", "Glass", "Pyramid" }, 0, i => { })),
            };
            foreach (var (name, make) in screens) yield return Visit(fe, name, make);

            // a career: before an event, then in one
            CareerStore.New("Tour", "ENG", 2, false);
            var c = CareerStore.Current;
            yield return Visit(fe, "Career hub", () => new CareerHubScreen());
            yield return Visit(fe, "Career record", () => new CareerRecordScreen());
            yield return Visit(fe, "Equipment", () => new EquipmentScreen());
            yield return Visit(fe, "Trophy cabinet", () => new TrophyCabinetScreen());
            yield return Visit(fe, "Sponsor", () => new SponsorScreen());
            yield return Visit(fe, "Coach", () => new CoachScreen());
            yield return Visit(fe, "World map", () => new WorldMapScreen());
            yield return Visit(fe, "Career settings", () => new CareerSettingsScreen());
            c.Enter(SquashSim.Career.Tour.HomeIndex);
            yield return Visit(fe, "Career hub in event", () => new CareerHubScreen());
            yield return Visit(fe, "Match preview", () => new MatchPreviewScreen(c.NextOpponent()));
            yield return Visit(fe, "Draw", () => new DrawScreen(c.S.Current));
            yield return Visit(fe, "Season summary", () => new SeasonSummaryScreen());
            yield return Visit(fe, "Retire", () => new RetireScreen());
            CareerStore.Delete();

            // a match: the HUD, the pause panel, the end
            fe.StartMatch(MatchSettings.QuickGame());
            var r = fe.Match;
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot(fe, "Match HUD");
            r.Clock.Pause();
            for (int i = 0; i < 6 && !(fe.Top is PauseScreen); i++) yield return null;
            yield return Shot(fe, "Pause");
            if (fe.Top is PauseScreen) fe.Pop();
            r.Clock.Resume();
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("attacker").WithDifficulty(0.9), 5));
            r.SetTimeScale(10);
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 150f && !(fe.Top is PostMatchScreen)) yield return null;
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            if (fe.Top is PostMatchScreen) yield return Shot(fe, "Post match");
            Assert.That(fe.Stats.Coach.Shots, Is.GreaterThan(0), "the coach watched your shots (2026-11-01)");
            Assert.That(fe.Top is PostMatchScreen && fe.Top.Root.Q(className: "k-coach-row") != null, Is.True, "the coach's tip after the match");

            File.WriteAllLines(Path.Combine(Dir, "index.txt"), _index);
            Debug.Log("[Tour] " + _n + " pictures; " + string.Join(" | ", _index.FindAll(l => l.StartsWith("!!"))));
            Assert.That(_n, Is.GreaterThan(30));
        }
    }
}
