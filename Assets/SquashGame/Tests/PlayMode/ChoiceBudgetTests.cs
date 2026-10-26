// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ChoiceBudgetTests.cs
// UI redesign U5: "not too many buttons on one screen". Every screen,
// on every one of its tabs, shows at most 6 choices at once - besides
// its one primary action. The draw's matches, the Academy trail's
// lessons and the world map's pins are maps you browse, not choices,
// and are not counted.
// ===========================================================

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim.Arcade;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ChoiceBudgetTests : InputTestFixture
    {
        public const int Budget = 6;
        public static int Count(MenuScreen s) { return Choices(s); }

        static int Choices(MenuScreen s)
        {
            int n = 0;
            foreach (var it in s.Reachable()) if (!(it is PrimaryItem) && !(it is BracketMatchItem) && !(it is TrailNode) && !(it is RallyNode) && !(it is MapPinItem) && !(it is TrophyItem)) n++;
            return n;
        }

        /// <summary>Show the screen, count each of its tabs, close it; the counts go into the log.</summary>
        static IEnumerator Check(FrontEnd fe, string name, MenuScreen s, List<string> log)
        {
            fe.Push(s);
            yield return null; yield return null;
            int tabs = s is GameScreen g && g.Tabs != null ? g.Tabs.Count : 1;
            for (int t = 0; t < tabs; t++)
            {
                string tab = name;
                if (s is GameScreen gs && gs.Tabs != null) { gs.Tabs.Select(t); tab += " / " + gs.Tabs.Current; }
                yield return null; yield return null;
                int n = Choices(s);
                log.Add(tab + ": " + n);
                Assert.That(n, Is.LessThanOrEqualTo(Budget), tab + " shows " + n + " choices");
            }
            if (fe.Top == s) fe.Pop();
        }

        [UnityTest]
        public IEnumerator NoScreenShowsMoreThanSixChoices()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f && UnityEngine.Object.FindFirstObjectByType<FrontEnd>() == null) yield return null;
            yield return null; yield return null;
            var fe = UnityEngine.Object.FindFirstObjectByType<FrontEnd>();
            var log = new List<string>();

            // the hub is already up: its tabs
            var hub = (MainMenuScreen)fe.Top;
            for (int t = 0; t < hub.Tabs.Count; t++)
            {
                hub.Tabs.Select(t);
                yield return null; yield return null;
                log.Add("Main menu / " + hub.Tabs.Current + ": " + Choices(hub));
                Assert.That(Choices(hub), Is.LessThanOrEqualTo(Budget), "main menu " + hub.Tabs.Current);
            }
            hub.Tabs.Select(0);

            var screens = new List<(string, Func<MenuScreen>)>
            {
                ("Quick Match", () => new QuickMatchScreen()), ("Local Versus", () => new LocalVersusScreen()),
                ("Target Arcade", () => new TargetArcadeScreen()), ("Levels", () => new LevelScreen(ArcadeMode.TimeAttack)),
                ("Practice", () => new PracticeScreen()), ("Academy", () => new AcademyScreen()),
                ("Locker Room", () => new LockerRoomScreen()), ("Stats", () => new StatsScreen()), ("Achievements", () => new AchievementsScreen()),
                ("Settings", () => new SettingsScreen()), ("Controls", () => new ControlsHelpScreen()),
                ("Confirm", () => new ConfirmScreen("Quit?", () => { })), ("Create player", () => new CreatePlayerScreen()),
            };
            foreach (var (name, make) in screens) yield return Check(fe, name, make(), log);

            // a career: the hub between events, then in one (the preview, the draw)
            CareerStore.New("Budget", "ENG", 2, false);
            var c = CareerStore.Current;
            yield return Check(fe, "Career hub (events)", new CareerHubScreen(), log);
            yield return Check(fe, "Equipment", new EquipmentScreen(), log);
            yield return Check(fe, "Trophy cabinet", new TrophyCabinetScreen(), log);
            c.Enter(SquashSim.Career.Tour.HomeIndex);
            yield return Check(fe, "Career hub (match)", new CareerHubScreen(), log);
            yield return Check(fe, "Match preview", new MatchPreviewScreen(c.NextOpponent()), log);
            yield return Check(fe, "Draw", new DrawScreen(c.S.Current), log);
            yield return Check(fe, "World map", new WorldMapScreen(), log);
            yield return Check(fe, "Career settings", new CareerSettingsScreen(), log);
            CareerStore.Delete();

            // in a match: the pause panel
            fe.StartMatch(MatchSettings.QuickGame());
            yield return null; yield return null;
            fe.Match.Clock.Pause();                                                  // the front end puts up the pause panel
            for (int i = 0; i < 4 && !(fe.Top is PauseScreen); i++) yield return null;
            Assert.That(fe.Top, Is.InstanceOf<PauseScreen>());
            yield return null;
            int pause = Choices(fe.Top);
            log.Add("Pause: " + pause);
            Assert.That(pause, Is.InRange(1, Budget), "the pause panel");

            Debug.Log("[U5] choices in view: " + string.Join("  |  ", log));
        }
    }
}
