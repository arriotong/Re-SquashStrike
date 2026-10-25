// ===========================================================
// Assets/SquashGame/Runtime/UI/HubScreens.cs
// UI redesign U2: the main menu hub, the versus screen, the pause panel
// and the full-time screen.
//
//   MainMenuScreen    after 2K's tile hub: PLAY / CAREER / ACADEMY /
//                     LOCKER tabs, at most 4 cards in view, the slow-
//                     motion rally behind; Settings (Y) and Quit (X)
//   QuickMatchScreen  a versus screen: you (live, in your kit) on the
//                     left, the opponent (live, in their kit) on the
//   LocalVersusScreen right; the options as pills between; PLAY
//   PauseScreen       a side panel over the dimmed match: the score,
//                     a stat line, 4 choices
//   PostMatchScreen   "full time": the result, game scores, two-sided
//                     stat bars, highlights, the rewards; Rematch
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    // ---------------- the main menu hub ----------------

    public sealed class MainMenuScreen : GameScreen
    {
        protected override string Kicker { get { return "re:"; } }
        protected override string Title { get { return "<color=#c8ff3d>SQUASH</color>STRIKE"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        /// <summary>The menus' camera angles per card (MenuCinematics' scenes).</summary>
        static readonly Dictionary<string, int> Angles = new Dictionary<string, int>
        {
            { "QUICK MATCH", 0 }, { "LOCAL VERSUS", 1 }, { "CONTINUE CAREER", 2 }, { "START A CAREER", 2 }, { "NEW CAREER", 2 },
            { "TARGET ARCADE", 3 }, { "PRACTICE COURT", 4 }, { "ACADEMY", 5 }, { "ALL LESSONS", 5 },
            { "YOUR KIT", MenuCinematics.LockerScene }, { "STATS", 1 }, { "ACHIEVEMENTS", 2 },
            { "LAST MATCH", 2 }, { "HIGHLIGHTS", 0 }, { "LIBRARY", 1 },
            { "TODAY", 3 }, { "THIS WEEK", 0 }, { "GAUNTLET", 4 }, { "SCENARIOS", 1 },
        };

        protected override void Compose()
        {
            // ---- PLAY ----
            var play = Kit.Box(Page("PLAY"), "k-row");
            AddTo(play, new CardItem("QUICK MATCH", "PLAY NOW", () => Fe.Push(new QuickMatchScreen()), CardSize.Hero).Vignette("versus")
                .Line(() => "You v " + MatchChoices.OpponentLabels[C.Opponent] + "  ·  " + MatchChoices.DifficultyLabels[C.Difficulty] + "  ·  " + MatchChoices.LengthLabels[C.Length])
                .Badge(() => { var q = ProfileStore.Current.Progress.Stats.Quick; return q.Played == 0 ? "NEW" : "W " + q.Won + "  ·  L " + (q.Played - q.Won); }, true));
            AddTo(play, new CardItem("TARGET ARCADE", "SOLO", () => Fe.Push(new TargetArcadeScreen())).Drawn("rings", new Color32(20, 23, 30, 255), Color.white)
                .Line("6 modes  ·  18 levels").Badge(() => "★ " + ArcadeStars() + " / 54"));
            var vs = AddTo(play, new CardItem("LOCAL VERSUS", "COUCH", () => Fe.Push(new LocalVersusScreen())).Drawn("split", new Color32(24, 36, 72, 255), new Color32(72, 24, 24, 255))
                .Line("Pad and keyboard, or two pads").Badge(() => MatchRunner.CanPlayTwoPlayer ? "2 PLAYERS" : "CONNECT A PAD"));
            AddTo(play, new CardItem("PRACTICE COURT", "TRAIN", () => Fe.Push(new PracticeScreen())).Drawn("stripes", new Color32(20, 32, 28, 255), new Color(1, 1, 1, 0.05f))
                .Line("Free play  ·  Feeder  ·  Drills").Badge(() => "5 DRILLS"));

            // ---- CHALLENGES (P3-R2): today's three, the week's one, the streak ----
            var ch = Kit.Box(Page("CHALLENGES"), "k-row");
            AddTo(ch, new CardItem("TODAY", "DAILY CHALLENGES", () => Fe.Push(new ChallengesScreen()), CardSize.Hero)
                .Drawn("chevrons", new Color32(22, 30, 12, 255), new Color32(200, 255, 61, 255))
                .Line(ChallengeText.Today).Badge(() => ChallengeText.DoneToday() + " / " + Challenges.DailyCount + "  ·  STREAK " + Challenges.StreakNow(), true));
            AddTo(ch, new CardItem("THIS WEEK", "WEEKLY CHALLENGE", () => Fe.Push(new ChallengesScreen()))
                .Drawn("clock", new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255))
                .Line(ChallengeText.Week).Badge(() => "+" + Challenges.WeeklyXp + " XP"));

            AddTo(ch, new CardItem("GAUNTLET", "HOW FAR CAN YOU GO", () => Fe.Push(new GauntletScreen()))
                .Drawn("chevrons", new Color32(30, 14, 14, 255), new Color32(255, 90, 74, 255))
                .Line(GauntletFlow.Line).Badge(() => GauntletFlow.Run != null && !GauntletFlow.Run.Over ? "RUN ON" : "BEST " + GauntletFlow.State.BestFloor, true));

            AddTo(ch, new CardItem("SCENARIOS", "CLUTCH SITUATIONS", () => Fe.Push(new ScenarioScreen()))
                .Drawn("rings", new Color32(16, 18, 30, 255), new Color32(255, 184, 59, 255))
                .Line(() => "15 situations: save the match balls, win the decider").Badge(() => "★ " + ScenarioFlow.TotalStars() + " / " + SquashSim.Modes.Scenarios.MaxStars, true));

            // ---- CAREER ----
            var career = Kit.Box(Page("CAREER"), "k-row");
            var cont = AddTo(career, new CardItem("CONTINUE CAREER", "CAREER", () => Fe.Push(new CareerHubScreen()), CardSize.Hero).Vignette("you")
                .Line(CareerLine).Badge(() => CareerStore.Current != null ? Career.Money(CareerStore.Current.S.Earnings) : "", true));
            cont.Visible = () => CareerStore.Exists;
            var start = AddTo(career, new CardItem("START A CAREER", "CAREER", () => Fe.Push(new CreatePlayerScreen()), CardSize.Hero)
                .Drawn("trophy", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255))
                .Line("Club events to the World Finals. Climb the world ranking."));
            start.Visible = () => !CareerStore.Exists;
            var fresh = AddTo(career, new CardItem("NEW CAREER", "CAREER", () => Fe.Push(new ConfirmScreen("Start again? Your career will be lost.", () => Fe.Push(new CreatePlayerScreen()))))
                .Drawn("trophy", new Color32(22, 22, 26, 255), new Color(1, 1, 1, 0.25f)).Line("Start again with a new player"));
            fresh.Visible = () => CareerStore.Exists;

            // ---- ACADEMY ----
            var academy = Kit.Box(Page("ACADEMY"), "k-row");
            AddTo(academy, new CardItem("ACADEMY", "LEARN", () => FrontEnd.LaunchLesson(AcademyScreen.NextUp().Id), CardSize.Hero)
                .Drawn("trail", new Color32(30, 18, 52, 255), Color.white)
                .Line(() => "Next: " + AcademyScreen.NextUp().Title).Badge(() => AcademyScreen.Summary().ToUpperInvariant(), true));
            AddTo(academy, new CardItem("ALL LESSONS", "ACADEMY", () => Fe.Push(new AcademyScreen())).Drawn("stripes", new Color32(26, 20, 40, 255), new Color(1, 1, 1, 0.05f))
                .Line("Controls  ·  Rules of squash"));

            // ---- LOCKER ----
            var locker = Kit.Box(Page("LOCKER"), "k-row");
            AddTo(locker, new CardItem("YOUR KIT", "LOCKER ROOM", () => Fe.Push(new LockerRoomScreen()), CardSize.Hero).Vignette("you")
                .Line("Kit, racket, celebration, card").Badge(() => "LEVEL " + Rewards.LevelOf(ProfileStore.Current.Progress.Xp), true)).IsNew = AnyNew;
            AddTo(locker, new CardItem("STATS", "LOCKER ROOM", () => Fe.Push(new StatsScreen())).Glyph("%")
                .Line(() => ProfileStore.Current.Progress.Stats.MatchesWon + " matches won"));
            AddTo(locker, new CardItem("ACHIEVEMENTS", "LOCKER ROOM", () => Fe.Push(new AchievementsScreen())).Drawn("trophy", new Color32(24, 22, 16, 255), new Color32(255, 184, 59, 255))
                .Badge(() => Done() + " / " + Achievements.All().Count));

            // ---- REPLAYS (P2-M6) ----
            var rep = Kit.Box(Page("REPLAYS"), "k-row");
            var last = AddTo(rep, new CardItem("LAST MATCH", "REPLAY THEATRE", () => { var e = Replays.ReplayLibrary.Latest; if (e != null) Fe.Push(new MatchReplayScreen(e)); }, CardSize.Hero)
                .Vignette("versus")
                .Line(() => { var e = Replays.ReplayLibrary.Latest; return e == null ? "" : e.Result + "  " + e.Scores + "  ·  " + ReplayText.Title(e) + "  ·  " + e.Tagged + " highlights"; })
                .Badge(() => { var e = Replays.ReplayLibrary.Latest; return e != null ? ReplayText.When(e) : ""; }, true));
            last.Locked = () => Replays.ReplayLibrary.Latest == null; last.LockLine = () => "every match you play is kept here";
            var reel = AddTo(rep, new CardItem("HIGHLIGHTS", "LAST MATCH", () => { var e = Replays.ReplayLibrary.Latest; if (e != null) Fe.Push(new MatchReplayScreen(e, true)); })
                .Drawn("bracket", new Color32(26, 30, 14, 255), new Color32(255, 184, 59, 255))
                .Line("The best rallies, back to back"));
            reel.Locked = () => Replays.ReplayLibrary.Latest == null; reel.LockLine = () => "play a match first";
            AddTo(rep, new CardItem("LIBRARY", "REPLAY THEATRE", () => Fe.Push(new ReplayLibraryScreen())).Drawn("stripes", new Color32(20, 24, 30, 255), new Color(1, 1, 1, 0.05f))
                .Line(() => { var l = Replays.ReplayLibrary.Entries; int p = l.FindAll(x => x.Pinned).Count; return l.Count + " replays  ·  " + p + " pinned"; }));

            // ---- settings: a small button top right, and Y ----
            var gear = new ActionItem("Settings", () => Fe.Push(new SettingsScreen()), PromptBar.Btn.Y);
            gear.Root.AddToClassList("k-action--chip");
            AddTo(TopBar, gear);
        }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.Tabs, "Modes"), (PromptBar.Btn.Y, "Settings"), (PromptBar.Btn.X, "Quit"));
        }

        public override void OnActionY() { Fe.Push(new SettingsScreen()); }
        public override void OnActionX() { Fe.Push(new ConfirmScreen("Quit re:SquashStrike?", FrontEnd.QuitGame)); }

        static int ArcadeStars() { int n = 0; foreach (var r in ProfileStore.Current.Arcade) n += r.Stars; return n; }
        static int Done() { int n = 0; foreach (var a in ProfileStore.Current.Progress.Achievements) if (a.Done) n++; return n; }
        static bool AnyNew()
        {
            var p = ProfileStore.Current.Progress;
            foreach (var id in p.Owned) if (!p.Seen.Contains(id)) return true;
            return false;
        }

        static string CareerLine()
        {
            var c = CareerStore.Current;
            if (c == null) return "";
            string next = c.S.Current != null ? "playing " + c.S.Current.Name : SquashSim.Career.Tour.Month(c.S.Month) + ": " + SquashSim.Career.Tour.EventName(c.S.Month, 0);
            return Career.Rank(c.RankOf(SquashSim.Career.CareerSession.You)) + "  ·  Season " + c.S.Season + "  ·  " + next;
        }

        /// <summary>The cards of a page that can be reached now (tests: at most 4 in view).</summary>
        public int CardsInView { get { int n = 0; foreach (var i in Reachable()) if (i is CardItem) n++; return n; } }

        MenuItem _shownFor;
        public override void Tick(float dt)
        {
            if (Focused != _shownFor)
            {
                _shownFor = Focused;
                if (Focused is CardItem card && Angles.TryGetValue(card.Title, out int a)) MenuCinematics.Scene = a;
            }
            // P2-M3: a new player is pointed to the Academy, once.
            var ac = ProfileStore.Current.Academy;
            if (ac.Suggested) return;
            ac.Suggested = true;
            ProfileStore.Save();
            if (ac.Done.Count == 0) Fe.Push(new AcademySuggestScreen());
        }

        public override void OnShow() { base.OnShow(); _shownFor = null; foreach (var i in Items) i.Refresh(); }
        public override void Back() { }
    }

    // ---------------- the versus screen ----------------

    public abstract class VersusScreen : GameScreen
    {
        protected abstract bool TwoPlayer { get; }
        protected override bool ShowChip { get { return false; } }
        protected static MenuChoices C { get { return ProfileStore.Current.Choices; } }
        protected static GameSettings S { get { return ProfileStore.Current.Settings; } }
        Label _oppName, _oppStyle, _oppLines, _youName, _youMeta;

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "k-versus");
            // you
            var you = Kit.Box(stage, "k-vs-side");
            var youPic = Kit.Box(you, "k-vs-pic"); Vignettes.Attach(youPic, "you", 512, 640);
            Kit.L(you, TwoPlayer ? "PLAYER 1" : "YOU", "k-vs-kicker");
            _youName = Kit.L(you, "", "k-vs-name");
            _youMeta = Kit.L(you, "", "k-vs-meta");
            // the options
            var mid = Kit.Box(stage, "k-vs-mid");
            Kit.L(mid, "VS", "k-vs-vs");
            if (!TwoPlayer)
            {
                AddTo(mid, new SelectorItem("Opponent", MatchChoices.OpponentLabels, () => C.Opponent, v => { C.Opponent = v; UpdateOpponent(); }));
                AddTo(mid, new SelectorItem("Difficulty", MatchChoices.DifficultyLabels, () => C.Difficulty, v => C.Difficulty = v, true)).Enabled = () => C.Opponent != 4;
                AddTo(mid, new SelectorItem("Match", Short(MatchChoices.LengthLabels), () => C.Length, v => C.Length = v));
                AddTo(mid, new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => C.Scoring, v => C.Scoring = v));
                // 2026-10-24: the court (the court behind the menu changes as you choose; Controls are in Settings)
                AddTo(mid, new SelectorItem("Court", Courts.Names, () => C.Court, v => { C.Court = v; Courts.CurrentId = Courts.All[v].Id; }));
            }
            else
            {
                AddTo(mid, new SelectorItem("Match", Short(MatchChoices.LengthLabels), () => C.VsLength, v => C.VsLength = v));
                AddTo(mid, new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => C.VsScoring, v => C.VsScoring = v));
                AddTo(mid, new SelectorItem("Court", Courts.Names, () => C.VsCourt, v => { C.VsCourt = v; Courts.CurrentId = Courts.All[v].Id; }));
                AddTo(mid, new SelectorItem("Controls", MatchChoices.ControlLabels, () => S.Assisted ? 1 : 0, v => { S.Assisted = v == 1; ProfileStore.Apply(S, false); }));
            }
            // them
            var them = Kit.Box(stage, "k-vs-side");
            var themPic = Kit.Box(them, "k-vs-pic"); Vignettes.Attach(themPic, "them", 512, 640);
            Kit.L(them, TwoPlayer ? "PLAYER 2" : "OPPONENT", "k-vs-kicker");
            _oppName = Kit.L(them, "", "k-vs-name");
            _oppStyle = Kit.L(them, "", "k-vs-meta");
            _oppLines = Kit.L(them, "", "k-vs-lines");

            var go = AddTo(Root, new PrimaryItem(TwoPlayer ? "Start" : "Play", Start));
            if (TwoPlayer) go.Enabled = () => MatchRunner.CanPlayTwoPlayer;
            UpdateOpponent();
        }

        static string[] Short(string[] l) { return new[] { "1 game", "Best of 3", "Best of 5" }; }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back"));
        }

        void UpdateOpponent()
        {
            var p = ProfileStore.Current.Progress;
            _youName.text = CareerStore.Current != null ? CareerStore.Current.S.Name : "Player";
            var title = Cosmetics.Find(p.Loadout.Title);
            _youMeta.text = "LEVEL " + Rewards.LevelOf(p.Xp) + "  ·  " + (title != null ? title.Name.ToUpperInvariant() : "ROOKIE") + "  ·  " + (S.Assisted ? "ASSISTED" : "NON-ASSISTED");
            if (TwoPlayer)
            {
                _oppName.text = "Player 2";
                int pads = UnityEngine.InputSystem.Gamepad.all.Count;
                _oppStyle.text = pads == 0 ? "CONNECT A PAD TO PLAY" : pads == 1 ? "THE PAD  ·  PLAYER 1 ON KEYBOARD" : "PAD 2";
                _oppLines.text = "";
                KitDirector.OpponentPreview = null;
                return;
            }
            string id = MatchRunner.Opponents[C.Opponent];
            _oppName.text = MatchChoices.OpponentLabels[C.Opponent];
            _oppStyle.text = id == "wallbot" ? "THE PRACTICE WALL" : MatchChoices.DifficultyLabels[C.Difficulty].ToUpperInvariant() + "  ·  " + SquashSim.Career.Roster.StyleName(id).ToUpperInvariant();
            _oppLines.text = id == "wallbot" ? "Sends it back down the middle, every time." : string.Join("\n", SquashSim.Career.Roster.StyleLines(id));
            KitDirector.OpponentPreview = Cosmetics.Signature(id);
        }

        public override void Tick(float dt) { if (TwoPlayer) UpdateOpponent(); }

        void Start()
        {
            ProfileStore.Save();
            KitDirector.OpponentPreview = null;
            if (TwoPlayer)
                Fe.StartMatch(new MatchSettings { TwoPlayer = true, GamesToWin = MatchChoices.LengthGames[C.VsLength], Scoring = MatchChoices.ScoringValues[C.VsScoring], WarmUp = false,
                                                  Court = Courts.All[Mathf.Clamp(C.VsCourt, 0, Courts.All.Length - 1)].Id });
            else
                Fe.StartMatch(new MatchSettings
                {
                    TwoPlayer = false, Opponent = MatchRunner.Opponents[C.Opponent], Difficulty = MatchChoices.DifficultyValues[C.Difficulty],
                    GamesToWin = MatchChoices.LengthGames[C.Length], Scoring = MatchChoices.ScoringValues[C.Scoring], WarmUp = false,
                    Court = Courts.All[Mathf.Clamp(C.Court, 0, Courts.All.Length - 1)].Id
                });
        }

        public override void OnShow() { base.OnShow(); MenuCinematics.Scene = TwoPlayer ? 1 : 0; }
        public override void OnHide() { ProfileStore.Save(); KitDirector.OpponentPreview = null; }
    }

    public sealed class QuickMatchScreen : VersusScreen
    {
        protected override bool TwoPlayer { get { return false; } }
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Quick Match"; } }
    }

    public sealed class LocalVersusScreen : VersusScreen
    {
        protected override bool TwoPlayer { get { return true; } }
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Local Versus"; } }
    }

    // ---------------- pause ----------------

    public sealed class PauseScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-side"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }        // Esc / Start resume the game itself
        protected override string Kicker { get { return "MATCH"; } }
        protected override string Title { get { return "Paused"; } }
        protected override string Hint { get { return null; } }

        protected override void Build()
        {
            var m = Fe.Match;
            var s = m.Sim.Score;
            // the score, as the bug shows it
            var score = Kit.Box(Content, "k-side-score");
            for (int p = 0; p < 2; p++)
            {
                var row = Kit.Box(score, "k-side-row");
                Kit.L(row, s.Server == p ? "●" : "", "k-side-serve");
                Kit.L(row, m.Names[p], "k-side-name");
                Kit.L(row, s.Games[p].ToString(), "k-side-games");
                Kit.L(row, s.Points[p].ToString(), "k-side-points");
            }
            var st = Fe.Stats;
            if (st != null)
                Kit.L(Content, "Rallies won " + st.Players[0].Rallies + "–" + st.Players[1].Rallies + "   ·   Winners " + st.Players[0].Winners + "   ·   Errors " + st.Players[0].Errors, "k-side-stats");
            bool career = m.Settings.Career, gauntlet = m.Settings.Gauntlet;
            Add(new ActionItem("Resume", () => Fe.Resume(), "▶"));
            if (!career && !gauntlet) Add(new ActionItem("Restart match", () => Fe.Push(new ConfirmScreen("Restart the match?", () => Fe.Restart()))));
            Add(new ActionItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ActionItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            if (career) Add(new ActionItem("Quit: retire from the match", () => Fe.Push(new RetireScreen())));
            else if (gauntlet) Add(new ActionItem("Quit: forfeit the floor", () => Fe.Push(new ConfirmScreen("Forfeit this floor? It costs a life.", () => GauntletFlow.AfterMatch(Fe, false, 0)))));
            else Add(new ActionItem("Quit to menu", () => Fe.Push(new ConfirmScreen("Leave the match?", () => Fe.QuitToMenu()))));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Resume"));
            Root.Add(prompts);
        }
        public override void Back() { Fe.Resume(); }
    }

    // ---------------- full time ----------------

    public sealed class PostMatchScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-fulltime"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "FULL TIME"; } }
        protected override string Title { get { return Winner(); } }
        protected override string Hint { get { return null; } }
        public Progression.RewardResult Reward { get; private set; }

        string Winner()
        {
            var m = Fe.Match;
            int w = m.Sim.Score.MatchWinner;
            if (w < 0) w = m.Sim.Score.GameWinner;
            return w < 0 ? "Match over" : (m.Settings.TwoPlayer ? m.Names[w] + " wins" : (w == m.HumanIndex ? "You win" : m.Names[w] + " wins"));
        }

        protected override void Build()
        {
            var m = Fe.Match;
            var st = Fe.Stats;
            // P2-M5: XP, lifetime stats, the match achievements (once, when the screen is made)
            double tier = 0;
            var career = CareerStore.Current;
            if (m.Settings.Career && career != null && career.S.Current != null) tier = Rewards.TierFactor[SquashSim.Career.Tour.Band(career.S.Current.Tier)];
            Reward = Rewards.Match(st.Facts(m), tier);

            // the primary first (it has the focus): Rematch, or Continue in a career
            bool won = m.Sim.Score.MatchWinner == m.HumanIndex;
            if (m.Settings.Career) AddTo(Root, new PrimaryItem("Continue", () => Career.Record(Fe, won, false)));
            else if (m.Settings.Gauntlet) AddTo(Root, new PrimaryItem("Continue", () => GauntletFlow.AfterMatch(Fe, won, Margin())));
            else if (m.Settings.Scenario != null) AddTo(Root, new PrimaryItem("Continue", () => ScenarioFlow.AfterMatch(Fe, st.Facts(m))));
            else AddTo(Root, new PrimaryItem("Rematch", () => Fe.Restart()));

            // the score line: names, games, each game as a chip
            var head = Kit.Box(Content, "k-ft-head");
            Kit.L(head, m.Names[0], "k-ft-name");
            Kit.L(head, m.Sim.Score.Games[0] + " – " + m.Sim.Score.Games[1], "k-ft-games");
            Kit.L(head, m.Names[1], "k-ft-name");
            var chips = Kit.Box(Content, "k-ft-chips");
            foreach (var g in st.Games) { var c = Kit.L(chips, g[0] + "-" + g[1], "k-ft-chip"); if (g[0] > g[1]) c.AddToClassList("k-ft-chip--won"); }

            var cols = Kit.Box(Content, "k-ft-cols");
            var tape = Kit.Panel(cols, "THE MATCH");
            var a = st.Players[0]; var b = st.Players[1];
            tape.Add(new StatBars()
                .Row("Rallies won", a.Rallies, b.Rallies)
                .Row("Winners", a.Winners, b.Winners)
                .Row("Errors", a.Errors, b.Errors, null, null, true)
                .Row("Great hits", a.GreatHits, b.GreatHits)
                .Row("Top pace", a.TopPace * 3.6, b.TopPace * 3.6, (a.TopPace * 3.6).ToString("0"), (b.TopPace * 3.6).ToString("0")));
            var right = Kit.Box(cols, "k-ft-right");
            var hi = Kit.Panel(right, "HIGHLIGHTS");
            var hrow = Kit.Box(hi, "k-ft-highs");
            High(hrow, st.LongestRally.ToString(), "LONGEST RALLY");
            High(hrow, (System.Math.Max(a.TopPace, b.TopPace) * 3.6).ToString("0"), "TOP KM/H");
            High(hrow, a.GreatHits.ToString(), "GREAT HITS");
            right.Add(new RewardStrip(Reward));
            Fe.ToastAchievements(Reward);

            if (m.Settings.Scenario != null)
            {
                var more = Kit.Box(Root, "k-ft-more");
                AddTo(more, new ActionItem("Try again", () => Fe.Restart()));
            }
            else if (!m.Settings.Career && !m.Settings.Gauntlet)
            {
                var more = Kit.Box(Root, "k-ft-more");                        // bottom left, opposite the primary
                AddTo(more, new ActionItem("Change settings", () =>
                {
                    bool two = m.Settings.TwoPlayer;
                    Fe.QuitToMenu();
                    Fe.Push(two ? (MenuScreen)new LocalVersusScreen() : new QuickMatchScreen());
                }));
                AddTo(more, new ActionItem("Main menu", () => Fe.QuitToMenu()));
                AddTo(more, new ActionItem("Watch highlights", () =>
                {
                    var e = Replays.ReplayLibrary.Latest;
                    Fe.QuitToMenu();
                    if (e != null) Fe.Push(new MatchReplayScreen(e, true));
                }, "▶"));
            }
        }

        static void High(VisualElement parent, string value, string name)
        {
            var b = Kit.Box(parent, "k-ft-high");
            Kit.L(b, value, "k-ft-high-v");
            Kit.L(b, name, "k-ft-high-n");
        }

        /// <summary>The last game's winning margin (the Gauntlet scores it).</summary>
        int Margin()
        {
            var g = Fe.Stats != null && Fe.Stats.Games.Count > 0 ? Fe.Stats.Games[Fe.Stats.Games.Count - 1] : null;
            return g == null ? 0 : System.Math.Abs(g[0] - g[1]);
        }

        public override void Back()
        {
            if (Fe.Match.Settings.Career) Career.Record(Fe, Fe.Match.Sim.Score.MatchWinner == Fe.Match.HumanIndex, false);
            else if (Fe.Match.Settings.Gauntlet) GauntletFlow.AfterMatch(Fe, Fe.Match.Sim.Score.MatchWinner == Fe.Match.HumanIndex, Margin());
            else if (Fe.Match.Settings.Scenario != null) ScenarioFlow.AfterMatch(Fe, Fe.Stats.Facts(Fe.Match));
            else Fe.QuitToMenu();
        }
    }
}
