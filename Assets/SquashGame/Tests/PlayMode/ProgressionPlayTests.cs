// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ProgressionPlayTests.cs
// P2-M5: progression in the real scenes, on the keyboard.
//   - main menu -> Locker Room -> Kit: browsing a shirt dresses the
//     demo's athlete in it; Enter wears it; it is saved and reloads;
//     a locked item shows how to unlock it and cannot be worn
//   - Racket, Stats and Achievements open
//   - a Quick Match won (an AI stands in) gives XP, a level, the first
//     achievements (with toasts) on the post-match screen
// Writes Captures/P2M5_UI_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ProgressionPlayTests : InputTestFixture
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

        IEnumerator FocusRow(Keyboard kb, FrontEnd fe, string name)
        {
            for (int i = 0; i < 30 && fe.Top.Focused.Name != name; i++) yield return Tap(kb.downArrowKey);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo(name));
        }

        static IEnumerator Menu(FrontEnd fe)
        {
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        static Athlete.AthleteFigure You()
        {
            foreach (var a in Object.FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None)) if (a.Index == 0) return a;
            return null;
        }

        [UnityTest]
        public IEnumerator TheLockerRoomDressesYourAthlete()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            yield return HubNav.Open(fe, kb, Tap, "YOU", "YOUR KIT");
            Assert.That(fe.Top, Is.InstanceOf<LockerRoomScreen>());
            yield return Wait(1.5f, () => false);
            var locker = (LockerRoomScreen)fe.Top;
            Assert.That(locker.Tabs.Current, Is.EqualTo("CLOTHES"));
            var row = (LockerRow)fe.Top.Focused;
            Assert.That(row.Slot, Is.EqualTo(Slot.Shirt));
            Assert.That(row.Browsing.Id, Is.EqualTo("shirt_charcoal"), "the worn shirt first");
            // 2026-10-29: the athlete stands still in the Locker Room, so the kit can be seen
            Assert.That(MatchRunner.Showcase, Is.True);
            Assert.That(locker.Coins, Is.Not.Null, "2026-10-29: the coins top right");
            Assert.That(locker.Coins.Text, Is.EqualTo(GearDrops.Wallet.Coins.ToString("N0")));
            var still = You().transform.position;
            Assert.That(MatchRunner.Studio, Is.True, "2026-11-20: in the photo studio, off the court");
            Assert.That(Vector3.Distance(still, KitStudio.World), Is.LessThan(0.2f), "on the studio's spot");
            yield return Wait(1.0f, () => false);
            Assert.That(Vector3.Distance(still, You().transform.position), Is.LessThan(0.02f), "not running about");

            yield return Tap(kb.rightArrowKey);                                      // the next shirt: white (free)
            Assert.That(row.Browsing.Id, Is.EqualTo("shirt_white"));
            Assert.That(KitDirector.Preview.Shirt, Is.EqualTo("shirt_white"), "previewed");
            yield return null; yield return null;
            var you = You();
            Assert.That(you.Worn.Shirt, Is.EqualTo("shirt_white"), "the demo's athlete wears it");
            Assert.That(you.Rig.RegionMaterial(Athlete.AthleteRig.Region.ShirtBody).color, Is.EqualTo(Cosmetics.Find("shirt_white").A));
            Assert.That(ProfileStore.Current.Progress.Loadout.Shirt, Is.EqualTo("shirt_white"), "batch 2: yours is worn as you browse");

            // 2026-11-20 the grid: every item listed - yours first, the locked ones after (tried on, never worn)
            Assert.That(LockerRow.ShowLocked, Is.True);
            Assert.That(row.Items.Count, Is.EqualTo(row.TotalCount), "everything shown");
            Assert.That(row.OwnedCount, Is.LessThan(row.TotalCount));
            Assert.That(locker.Grid, Is.Not.Null, "the slot's items as a grid");
            Assert.That(locker.Grid.Count, Is.EqualTo(row.TotalCount), "a tile for each");
            int firstLocked = row.Items.FindIndex(it => !Rewards.Owns(it.Id));
            Assert.That(firstLocked, Is.EqualTo(row.OwnedCount), "the locked ones come after yours");
            row.BrowseTo(firstLocked);                                               // (a click on its tile)
            Assert.That(row.Owned, Is.False);
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "P2M5_UI_1_LockerRoom.png");
            Assert.That(ProfileStore.Current.Progress.Loadout.Shirt, Is.EqualTo("shirt_white"), "a locked item is only tried on");
            row.Select("shirt_white");
            Assert.That(row.Browsing.Id, Is.EqualTo("shirt_white"));

            // Randomise (Y): an outfit from what you own
            yield return Tap(kb.rKey);
            var l = ProfileStore.Current.Progress.Loadout;
            foreach (var slot in new[] { Slot.Shirt, Slot.Shorts, Slot.Socks, Slot.Shoes })
                Assert.That(Rewards.Owns(l.Get(slot)), Is.True, "randomised from your own: " + slot);
            row.Select("shirt_white");
            Assert.That(ProfileStore.Current.Progress.Loadout.Shirt, Is.EqualTo("shirt_white"), "picked again");

            // LOOK: a celebration plays on the athlete as you browse
            yield return Tap(kb.eKey);
            yield return null; yield return null;
            Assert.That(locker.Tabs.Current, Is.EqualTo("LOOK"));
            yield return FocusRow(kb, fe, "Celebration");
            yield return null;
            Assert.That(KitDirector.PreviewPose, Is.Not.Null, "the celebration is shown");
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "P2M5_UI_2_Kit.png");
            yield return Tap(kb.eKey);                                                 // RACKET
            yield return Wait(1.0f, () => false);
            Assert.That(((LockerRow)fe.Top.Focused).Slot, Is.EqualTo(Slot.Frame));
            Assert.That(KitDirector.PreviewPose, Is.Null);
            yield return CaptureUI(fe, "P2M5_UI_3_Racket.png");
            yield return Tap(kb.eKey);                                                 // CARD
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "U5_Locker_Card.png");

            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
            Assert.That(KitDirector.Preview, Is.Null, "the preview ends with the screen");
            Assert.That(MatchRunner.Showcase, Is.False, "the menus' rally plays again");
            Assert.That(KitDirector.PreviewPose, Is.Null);
            yield return null; yield return null;
            Assert.That(You().Worn.Shirt, Is.EqualTo("shirt_white"), "still wearing the chosen shirt");
            ProfileStore.Reset();
            Assert.That(ProfileStore.Current.Progress.Loadout.Shirt, Is.EqualTo("shirt_white"), "saved");

            yield return HubNav.Open(fe, kb, Tap, "YOU", "STATS");
            Assert.That(fe.Top, Is.InstanceOf<StatsScreen>());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P2M5_UI_4_Stats.png");
            yield return Tap(kb.eKey);
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "U5_Stats_Shots.png");
            yield return Tap(kb.escapeKey);
            yield return HubNav.Open(fe, kb, Tap, "YOU", "ACHIEVEMENTS");
            Assert.That(fe.Top, Is.InstanceOf<AchievementsScreen>());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P2M5_UI_5_Achievements.png");
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
        }

        /// <summary>P3-R2: the CHALLENGES tab and the challenges screen, with one daily half done.</summary>
        [UnityTest]
        public IEnumerator TheChallengesTabAndScreen()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var st = Challenges.Current();
            st.Daily[1].Progress = 1; st.Daily[1].Done = true;                       // (playable challenges: today's puzzle done)
            st.Streak = 2; st.LastStreakDay = st.Day; st.BestStreak = 4;
            yield return HubNav.Open(fe, kb, Tap, "CHALLENGES", "THIS WEEK");
            Assert.That(fe.Top, Is.InstanceOf<ChallengesScreen>());
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "P3_Challenges.png");
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P3_Hub_Challenges.png");
        }

        /// <summary>P5: the Gauntlet roguelite - start a run, a floor won (the pick), a real floor played (a game to 7) and back,
        /// talents bought with coins, the codex.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TheGauntletRunsFloorByFloor()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "MODES", "GAUNTLET");
            Assert.That(fe.Top, Is.InstanceOf<GauntletScreen>());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P3_Gauntlet_Start.png");
            yield return Tap(kb.enterKey);                                            // Start a run
            yield return null;
            Assert.That(GauntletFlow.Run, Is.Not.Null);
            Assert.That(fe.Top, Is.InstanceOf<GauntletScreen>());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P3_Gauntlet_Floor.png");

            GauntletFlow.AfterMatch(fe, true, 4);                                    // as if floor 1 was cleared
            yield return null; yield return null;
            var run = GauntletFlow.Run;
            Assert.That(run.AwaitingPick, Is.True);
            Assert.That(run.Offer.Count, Is.EqualTo(3), "three cards");
            Assert.That(run.Coins, Is.GreaterThan(0), "coins from the floor");
            Assert.That(ProfileStore.Current.Progress.Xp, Is.GreaterThan(0), "the floor's XP");
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P3_Gauntlet_Perks.png");
            string first = run.Offer[0];
            yield return Tap(kb.enterKey);                                            // the first card
            yield return null;
            Assert.That(run.AwaitingPick, Is.False);
            Assert.That(run.Floor, Is.EqualTo(2));
            var ab = SquashSim.Modes.GauntletAbilities.Find(first);
            if (ab.Kind != SquashSim.Modes.AbilityKind.Instant) Assert.That(SquashSim.Modes.Gauntlet.Level(run, first), Is.EqualTo(1), "picked: " + first);

            var settings = GauntletFlow.Settings(run);
            Assert.That(settings.Gauntlet, Is.True); Assert.That(settings.GamesToWin, Is.EqualTo(1));
            Assert.That(settings.GauntletFloor, Is.Not.Null, "2026-11-21: a floor's target, not a game"); Assert.That(settings.GauntletFloor.Floor, Is.EqualTo(2));
            Assert.That(settings.Difficulty, Is.EqualTo((float)SquashSim.Modes.Gauntlet.Difficulty(2)));
            // UI remake: a different court every floor, from all of them; a boss on a World Tour stage; the same for the same run
            Assert.That(settings.Court, Is.EqualTo(GauntletFlow.CourtFor(run, 2)));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int f = 1; f <= 40; f++)
            {
                string c = GauntletFlow.CourtFor(run, f);
                Assert.That(Courts.Find(c), Is.Not.Null, "floor " + f + ": " + c);
                if (f > 1) Assert.That(c, Is.Not.EqualTo(GauntletFlow.CourtFor(run, f - 1)), "floor " + f + " moves on");
                if (SquashSim.Modes.Gauntlet.KindOf(f) == SquashSim.Modes.FloorKind.Boss) Assert.That(c, Does.StartWith(Courts.TourPrefix), "a boss on a big stage");
                seen.Add(c);
            }
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(25), "40 floors, nearly all on different courts");
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P5_Gauntlet_NextFloor.png");

            // ---- floor 2, played for real (an autopilot), and back to the run ----
            GauntletFlow.Play(fe);
            yield return Wait(2f, () => fe.Match != null && fe.Match.Settings.Gauntlet && !fe.Match.Demo);
            var r = fe.Match;
            Assert.That(r.Floor, Is.Not.Null, "2026-11-21: a floor - a target in its rallies");
            Assert.That(r.Floor.Target, Is.EqualTo(SquashSim.Modes.Gauntlet.TargetFor(2)));
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 5));
            r.SetTimeScale(12);
            bool barSeen = false, wheelSeen = false;
            yield return Wait(300f, () =>
            {
                if (r.Floor != null && r.Floor.Played > 0 && !barSeen) barSeen = true;
                if (fe.Top is WheelScreen) wheelSeen = true;
                return fe.Top is FloorResultScreen;
            });
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(fe.Top, Is.InstanceOf<FloorResultScreen>(), "the floor's result");
            Assert.That(barSeen, Is.True);
            var play = r.Floor;
            Assert.That(play.Done, Is.True);
            Assert.That(play.Cleared || play.Played >= play.Rallies, Is.True, "cleared, or out of rallies: " + play.Score + "/" + play.Target + " in " + play.Played + "/" + play.Rallies);
            Debug.Log("[gauntlet floor] " + play.Score + "/" + play.Target + " in " + play.Played + "/" + play.Rallies + " spins " + play.Spins + " wheel shown " + wheelSeen);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P5_Gauntlet_FloorResult.png");
            bool won = play.Cleared;
            yield return Tap(kb.enterKey);                                            // Continue
            yield return Wait(2f, () => fe.Top is GauntletScreen);
            Assert.That(fe.Top, Is.InstanceOf<GauntletScreen>(), "back to the run");
            if (won) Assert.That(GauntletFlow.Run.AwaitingPick, Is.True, "cleared: a pick");
            else Assert.That(GauntletFlow.Run.Over, Is.True, "failed: the run is over");

            // ---- talents and the codex ----
            GauntletFlow.Abandon();
            Assert.That(GauntletFlow.State.Coins, Is.GreaterThan(0), "the run's coins are kept");
            GauntletFlow.State.Coins += 500;
            fe.Push(new TalentsScreen());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P5_Talents.png");
            Assert.That(GauntletFlow.BuyTalent("reroll"), Is.True);
            Assert.That(SquashSim.Modes.GauntletTalents.Level(GauntletFlow.State.Talents, "reroll"), Is.EqualTo(1));
            fe.Pop();
            fe.Push(new CodexScreen());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "P5_Codex.png");
            fe.Pop();
            GauntletFlow.NewRun();
            Assert.That(GauntletFlow.Run.Rerolls, Is.EqualTo(2), "the talent counts in the next run");
            GauntletFlow.Abandon();
        }

        /// <summary>P3-R6: Scenarios - the screen, a scenario's match starting part-way, its stars back on the screen.</summary>
        [UnityTest]
        public IEnumerator AGauntletFloorBarAndTheWheel()
        {
            // 2026-11-21 the arcade floors: the HUD's bar over a floor, the wheel spinning over the paused match
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            GauntletFlow.NewRun();
            GauntletFlow.Play(fe);
            yield return Wait(3f, () => fe.Match != null && fe.Match.Settings.Gauntlet && !fe.Match.Demo);
            var r = fe.Match;
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 5));
            r.SetTimeScale(6);
            yield return Wait(120f, () => r.Floor != null && r.Floor.Played >= 1 && r.Sim.Phase == MatchPhase.Rally);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(r.Floor.Played, Is.GreaterThanOrEqualTo(1));
            yield return Wait(0.3f, () => false);
            yield return CaptureUI(fe, "P5_Gauntlet_FloorBar.png");
            // charge the wheel and end the streak: it spins
            r.Floor.Power = 3; r.Floor.SpinDue = true;
            yield return Wait(1f, () => fe.Top is WheelScreen);
            Assert.That(fe.Top, Is.InstanceOf<WheelScreen>(), "the wheel");
            Assert.That(r.Clock.Paused, Is.True, "the match waits");
            yield return Wait(1.2f, () => false);
            yield return CaptureUI(fe, "P5_Gauntlet_Wheel.png");
            var wheel = (WheelScreen)fe.Top;
            yield return Wait(2f, () => wheel.Spins.Count > 0 && false);
            yield return CaptureUI(fe, "P5_Gauntlet_WheelPrize.png");
            yield return Wait(8f, () => !(fe.Top is WheelScreen));
            Assert.That(fe.Top, Is.Not.InstanceOf<WheelScreen>(), "spun: on with the floor");
            Assert.That(r.Floor.Spins, Is.EqualTo(1));
            Assert.That(r.Floor.Done || !r.Clock.Paused, Is.True, "the floor goes on");
            GauntletFlow.Abandon();
            fe.QuitToMenu();
        }

        [UnityTest]
        public IEnumerator AScenarioStartsPartWayAndScoresStars()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "MODES", "BIG MOMENTS");
            Assert.That(fe.Top, Is.InstanceOf<ScenarioScreen>());
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "P3_Scenarios.png");

            var sc = SquashSim.Modes.Scenarios.Find("sixten");                      // a Comebacks one (locked: play it straight)
            ScenarioFlow.Play(fe, sc);
            yield return Wait(3f, () => fe.Match != null && fe.Match.Settings.Scenario == "sixten");
            yield return Wait(1f, () => false);
            Assert.That(fe.Match.Sim.Score.Points, Is.EqualTo(new[] { 6, 10 }), "6-10 down, as the story says");
            yield return CaptureUI(fe, "P3_Scenario_Match.png");

            var facts = new MatchFacts { Won = true, GamesToWin = 1, Rallies = 8, RalliesWon = 8, Tins = 0, Games = new System.Collections.Generic.List<int[]> { new[] { 12, 10 } } };
            ScenarioFlow.AfterMatch(fe, facts);                                      // as if saved: 8 rallies, no tins
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<ScenarioScreen>());
            Assert.That(ScenarioFlow.StarsOf("sixten"), Is.EqualTo(3), "the win, no tins, inside 9 rallies");
            yield return Wait(1f, () => false);
            yield return CaptureUI(fe, "P3_Scenario_Result.png");
        }

        /// <summary>P4-G3: drop gear in play - a legendary racket glows, your shots trail in your colour; the Locker's "???".</summary>
        [UnityTest]
        public IEnumerator DropGearGlowsAndTrails()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            var arena = GearDrops.Banner("arena");
            var frame = arena.Of(Rarity.Legendary).Find(i => i.Slot == Slot.Frame);
            var trail = arena.Of(Rarity.Legendary).Find(i => i.Slot == Slot.Trail);
            var p = ProfileStore.Current.Progress;
            p.Owned.Add(frame.Id); p.Owned.Add(trail.Id); p.Owned.Add(arena.Items[2].Id + "_gold");
            p.Loadout.Frame = frame.Id; p.Loadout.Trail = trail.Id;
            ProfileStore.Save();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "YOU", "YOUR KIT");
            yield return Tap(kb.eKey); yield return Tap(kb.eKey);                        // RACKET
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "P4_Locker_Racket.png");
            yield return Tap(kb.escapeKey);

            fe.StartMatch(MatchSettings.QuickGame());
            var r = fe.Match;
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("attacker").WithDifficulty(0.8), 5));
            var rig = GameObject.Find("Athlete0").GetComponent<Athlete.AthleteRig>();
            var ball = Object.FindFirstObjectByType<BallView>();
            yield return Wait(30f, () => r.LastRacketContact != null && r.LastRacketContact.PlayerIndex == 0 && r.Ball.Vel.Z < -8);
            yield return null; yield return null;
            Assert.That(rig.FrameGlowing, Is.True, "a legendary frame glows");
            Assert.That(ball.TrailColour, Is.EqualTo(trail.A), "your shots trail in your colour");
            yield return CaptureUI(fe, "P4_Glow_Trail.png");
        }

        /// <summary>P4-G4 / 2026-10-29: the store - the three gacha banners, a x10 pull and its reveal, the Pro Shop's pull.</summary>
        [UnityTest]
        public IEnumerator GearDropsPullAndShop()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            GearDrops.Wallet.Coins = 2000 + GearDrops.TenPullCost;
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "STORE", "CLOTHES");
            Assert.That(fe.Top, Is.InstanceOf<StoreScreen>());
            var store = (StoreScreen)fe.Top;
            yield return null; yield return null;
            Assert.That(store.Tabs.Current, Is.EqualTo(StoreScreen.CosmeticsTab));
            Assert.That(store.Pulls[StoreScreen.CosmeticsTab][1].IsEnabled, Is.True, "x10 affordable");
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P4_GearDrops_Banner.png");
            store.Tabs.Select(0);                                                       // CARD PACKS
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "S1_Store_Cards.png");
            store.Tabs.Select(2);                                                       // PRO SHOP
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "S1_Store_ProShop.png");
            store.Tabs.Select(1);
            yield return null;

            var drops = GearDrops.Pull("arena", GearDrops.TenPull);
            Assert.That(drops.Count, Is.EqualTo(10));
            int back = 0; foreach (var d in drops) back += d.Coins;
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(2000 + back), "paid in coins");
            fe.Push(new PullRevealScreen(drops, "arena"));
            yield return Wait(4.5f, () => ((PullRevealScreen)fe.Top).Revealed >= 10);
            Assert.That(((PullRevealScreen)fe.Top).Revealed, Is.EqualTo(10), "the cards turn over by themselves");
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "P4_GearDrops_Reveal.png");
            fe.Top.Back();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<StoreScreen>(), "back to the store");
            Assert.That(((StoreScreen)fe.Top).Banner, Is.EqualTo("arena"), "on the banner pulled");
            // the Pro Shop is a gacha now: a pull gives its gear
            GearDrops.Wallet.Coins = GearDrops.PullCost;
            var shop = GearDrops.Pull(GearDrops.ProShopId, 1);
            Assert.That(shop.Count, Is.EqualTo(1));
            Assert.That(GearDrops.ProShop().Items, Does.Contain(shop[0].Item), "the kit store's gear");
        }

        [UnityTest]
        public IEnumerator AWonMatchGivesXpALevelAndAchievements()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu(null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.0f, GamesToWin = 1 });
            var r = fe.Match;
            r.Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 31);
            r.SetTimeScale(12);
            yield return Wait(90f, () => fe.Top is PostMatchScreen);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
            Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>());
            var pm = (PostMatchScreen)fe.Top;
            Assert.That(r.Sim.Score.MatchWinner, Is.EqualTo(0), "the stand-in won");
            Assert.That(pm.Reward.Xp, Is.GreaterThan(200));
            Assert.That(pm.Reward.LevelledUp, Is.True, "a fresh player levels up from a won match");
            Assert.That(pm.Reward.Unlocked.Count, Is.GreaterThan(0), "level 2 gives an item");
            var ids = pm.Reward.Achieved.ConvertAll(a => a.Id);
            Assert.That(ids, Does.Contain("first_win"));
            Assert.That(fe.ToastLog, Does.Contain("First win"), "a toast");
            Assert.That(ProfileStore.Current.Progress.Stats.Quick.Won, Is.EqualTo(1));
            Assert.That(ProfileStore.Current.Progress.Stats.Shots, Is.GreaterThan(0), "the shot mix is counted");
            Debug.Log("[P2M5] won match: +" + pm.Reward.Xp + " XP, level " + pm.Reward.ToLevel + ", achievements " + string.Join(", ", ids) +
                      ", unlocked " + string.Join(", ", pm.Reward.Unlocked.ConvertAll(i => i.Id)) + "; shots " + ProfileStore.Current.Progress.Stats.Shots +
                      ", top pace " + ProfileStore.Current.Progress.Stats.TopPaceKmh.ToString("0") + " km/h, T " + (100 * ProfileStore.Current.Progress.Stats.TShare).ToString("0") + "%");
            yield return Wait(1.5f, () => false);
            Assert.That(You().MomentShown, Is.EqualTo("cel_fist"), "the winner celebrates (the default celebration)");
            yield return CaptureUI(fe, "P2M5_UI_6_PostMatchRewards.png");
        }

        /// <summary>Each celebration, up close, for review (the practice court; the camera rig set aside).</summary>
        [UnityTest]
        public IEnumerator CelebrationsPoseSheet()
        {
            LocalMatchTests.FreshProfile();
            SceneManager.LoadScene("WarmUpCourt");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var you = You();
            var cam = Camera.main;
            var rig = cam.GetComponent<GameplayCameraRig>();
            rig.enabled = false;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            court.Base.Clock.Pause();
            foreach (var it in Cosmetics.InSlot(Slot.Celebration))
            {
                string pose = it.Pose;
                you.MomentSource = () => pose;
                yield return Wait(0.8f, () => false);
                Assert.That(you.MomentShown, Is.EqualTo(pose));
                Vector3 at = you.transform.position;
                cam.transform.position = at + new Vector3(0.6f, 1.15f, 2.6f);
                cam.transform.LookAt(at + new Vector3(0, 0.85f, 0));
                cam.fieldOfView = 45f;
                var rt = new RenderTexture(640, 640, 24);
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
                RenderTexture.active = null;
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "P2M5_Celebration_" + pose + ".png"));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex); rt.Release();
            }
            you.MomentSource = null;
            rig.enabled = true;
        }
    }
}
