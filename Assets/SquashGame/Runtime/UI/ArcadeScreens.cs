// ===========================================================
// Assets/SquashGame/Runtime/UI/ArcadeScreens.cs
// The Target Arcade and Practice Court screens. UI redesign U4.
//
//   TargetArcadeScreen   six cabinet cards (drawn art, stars, best)
//   LevelScreen          Easy / Medium / Hard cards; the focused one's
//                        star scores, your best and the next star
//   PracticeScreen       tabs FREE PRACTICE / DRILLS: the free court
//                        (live) and the Coach tray; five drill cards
//   ArcadePauseScreen    a side panel: resume, restart, settings, quit
//   ResultsScreen        the score counts up, the stars pop in one by
//                        one, NEW BEST stamps on, the next-star bar,
//                        the rewards; Retry
//   CoachScreen          the practice court's feeder: a slim tray on
//                        the right edge, the court stays in view
// ===========================================================

using System;
using SquashGame.Profile;
using SquashSim.Arcade;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class Stars
    {
        public static string Of(int n) { return new string('★', n) + new string('☆', 3 - n); }
        public static string Best(string id)
        {
            var r = ProfileStore.Record(id);
            return r == null ? "not played" : Of(r.Stars) + "   best " + r.Best.ToString("N0");
        }

        public static int Mode(ArcadeMode mode)
        {
            int n = 0;
            for (int l = 0; l < 3; l++) { var r = ProfileStore.Record(Challenges.Get(mode, l).Id); if (r != null) n += r.Stars; }
            return n;
        }

        public static int All() { int n = 0; for (int m = 0; m < Challenges.ModeNames.Length; m++) n += Mode((ArcadeMode)m); return n; }

        /// <summary>The cabinet art for a challenge (CardArt kinds).</summary>
        public static string Art(Challenge c)
        {
            if (c.Mode == ArcadeMode.Drill)
                switch (c.Drill) { case DrillKind.Length: return "corner"; case DrillKind.Drop: return "drop"; case DrillKind.BoastDrive: return "angle"; case DrillKind.Ghosting: return "spots"; default: return "nick"; }
            return Art(c.Mode);
        }

        public static string Art(ArcadeMode m)
        {
            switch (m) { case ArcadeMode.TimeAttack: return "clock"; case ArcadeMode.Precision: return "rings"; case ArcadeMode.ShotMenu: return "menu"; case ArcadeMode.Streak: return "chevrons"; case ArcadeMode.ServeTargets: return "corner"; default: return "wall"; }
        }

        static readonly Color32[] Bgs =
        {
            new Color32(36, 22, 18, 255), new Color32(20, 23, 30, 255), new Color32(22, 30, 22, 255),
            new Color32(34, 18, 34, 255), new Color32(16, 28, 36, 255), new Color32(30, 28, 16, 255),
        };
        public static Color Bg(int i) { return Bgs[((i % Bgs.Length) + Bgs.Length) % Bgs.Length]; }

        /// <summary>"60 s  ·  big rings  ·  gentle feed  ·  both sides" (2026-10-25 playtest: words, not metres and m/s).</summary>
        public static string Rules(Challenge c)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (c.Seconds > 0) parts.Add(c.Seconds.ToString("0") + " s");
            if (c.Balls > 0) parts.Add(c.Balls + " balls");
            if (c.Mode == ArcadeMode.TimeAttack || c.Mode == ArcadeMode.Precision || c.Mode == ArcadeMode.Streak || c.Mode == ArcadeMode.WallRally)
                parts.Add(c.RingRadius >= 0.5 ? "big rings" : c.RingRadius >= 0.4 ? "medium rings" : "small rings");
            if (c.Mode == ArcadeMode.ShotMenu && c.Shots != null) parts.Add(string.Join(" / ", c.Shots));
            if (c.Mode != ArcadeMode.ServeTargets && c.Drill != DrillKind.Ghosting) parts.Add(c.FeedSpeed <= 23 ? "gentle feed" : c.FeedSpeed <= 26 ? "quicker feed" : "fast feed");
            if (c.Mode != ArcadeMode.ServeTargets && c.Drill != DrillKind.Ghosting) parts.Add(c.FeedBothSides ? "both sides" : "forehand side");
            return string.Join("  ·  ", parts);
        }

        /// <summary>The three star scores as chips, the ones you have lit; your best; a bar to the next star.</summary>
        public static void Ladder(VisualElement panel, Challenge c)
        {
            var rec = ProfileStore.Record(c.Id);
            int best = rec != null ? rec.Best : 0, stars = rec != null ? rec.Stars : 0;
            var row = Kit.Box(panel, "k-ta-thr-row");
            for (int i = 0; i < 3; i++)
            {
                var t = Kit.Box(row, "k-ta-thr");
                if (i < stars) t.AddToClassList("k-ta-thr--on");
                Kit.L(t, new string('★', i + 1), "k-ta-thr-s");
                Kit.L(t, c.Stars[i].ToString("N0"), "k-ta-thr-v");
            }
            var bestRow = Kit.Box(panel, "k-ta-bestrow");
            Kit.L(bestRow, "YOUR BEST", "k-ta-best-k");
            Kit.L(bestRow, rec == null ? "NOT PLAYED YET" : best.ToString("N0"), rec == null ? "k-ta-best-none" : "k-ta-best-v");
            if (stars < 3 && c.Stars[stars] > 0)
            {
                Kit.L(panel, "Next star at " + c.Stars[stars].ToString("N0"), "k-ta-next-k");
                var bar = Kit.Box(panel, "k-ta-bar");
                var fill = Kit.Box(bar, "k-ta-bar-fill");
                fill.style.width = Length.Percent(Mathf.Clamp01((float)best / c.Stars[stars]) * 100f);
            }
            else if (stars == 3) Kit.L(panel, "Three stars. Beat your best.", "k-ta-next-k");
        }
    }

    // ---------------- the arcade ----------------

    public sealed class TargetArcadeScreen : GameScreen
    {
        protected override string Kicker { get { return "MODES"; } }
        protected override string Title { get { return "Target Arcade"; } }

        protected override void Compose()
        {
            var head = Kit.Box(Content, "k-ta-head");
            Kit.L(head, "★ " + Stars.All() + " / 54", "k-ta-total");
            Kit.L(head, "Just you, the feeder and the targets. Three stars on every level.", "k-ta-sub");
            var row = Kit.Box(Content, "k-row"); row.AddToClassList("k-ta-cabs");
            for (int m = 0; m < Challenges.ModeNames.Length; m++)
            {
                var mode = (ArcadeMode)m;
                var card = AddTo(row, new CardItem(Challenges.ModeNames[m], "CABINET " + (m + 1), () => Fe.Push(new LevelScreen(mode)))
                    .Drawn(Stars.Art(mode), Stars.Bg(m), Color.white)
                    .Line(() => Challenges.ModeBlurbs[(int)mode] + "\n" + BestLine(mode))
                    .Badge(() => "★ " + Stars.Mode(mode) + " / 9", Stars.Mode(mode) == 9));
                card.Root.AddToClassList("k-card--cab");
            }
            AddTo(Root, new PrimaryItem("Next challenge", PlayNext));
        }

        static string BestLine(ArcadeMode mode)
        {
            for (int l = 2; l >= 0; l--)
            {
                var c = Challenges.Get(mode, l);
                var r = ProfileStore.Record(c.Id);
                if (r != null) return c.LevelName.ToUpperInvariant() + "  " + Stars.Of(r.Stars) + "  " + r.Best.ToString("N0");
            }
            return "NOT PLAYED YET";
        }

        /// <summary>The first level without three stars (easy levels first, across the modes).</summary>
        public static Challenge NextUp()
        {
            for (int l = 0; l < 3; l++)
                for (int m = 0; m < Challenges.ModeNames.Length; m++)
                {
                    var c = Challenges.Get((ArcadeMode)m, l);
                    var r = ProfileStore.Record(c.Id);
                    if (r == null || r.Stars < 3) return c;
                }
            return Challenges.Get(ArcadeMode.TimeAttack, 2);
        }

        static void PlayNext() { FrontEnd.LaunchChallenge(NextUp().Id); }

        public override void OnShow() { base.OnShow(); MenuCinematics.Scene = 3; }
    }

    public sealed class LevelScreen : GameScreen
    {
        readonly ArcadeMode _mode;
        public LevelScreen(ArcadeMode mode) { _mode = mode; }
        protected override string Kicker { get { return "TARGET ARCADE"; } }
        protected override string Title { get { return Challenges.ModeNames[(int)_mode]; } }
        protected override bool ShowChip { get { return false; } }
        VisualElement _detail;
        CardItem _shown;
        ActionItem _watch;
        readonly CardItem[] _cards = new CardItem[3];

        protected override void Compose()
        {
            Kit.L(Content, Challenges.ModeBlurbs[(int)_mode], "k-ta-sub");
            var stage = Kit.Box(Content, "k-ta-levels");
            var row = Kit.Box(stage, "k-row");
            for (int l = 0; l < 3; l++)
            {
                var c = Challenges.Get(_mode, l);
                _cards[l] = AddTo(row, new CardItem(c.LevelName, Challenges.ModeNames[(int)_mode].ToUpperInvariant(), () => FrontEnd.LaunchChallenge(c.Id), CardSize.L)
                    .Drawn(Stars.Art(_mode), Color.Lerp(Stars.Bg((int)_mode), new Color(0.3f, 0.05f, 0.05f), l * 0.3f), Color.white)
                    .Line(Stars.Rules(c))
                    .Badge(() => { var r = ProfileStore.Record(c.Id); return r == null ? "☆☆☆" : Stars.Of(r.Stars); }, ProfileStore.Record(c.Id) != null && ProfileStore.Record(c.Id).Stars == 3));
                _cards[l].Root.AddToClassList("k-card--level");
            }
            _detail = Kit.Panel(stage, "THE STARS");
            _detail.AddToClassList("k-ta-detail");
            AddTo(Root, new PrimaryItem("Play", () => FrontEnd.LaunchChallenge(Challenges.Get(_mode, Focused is CardItem ? Level(Focused) : 0).Id)));
        }

        int Level(MenuItem m) { for (int l = 0; l < 3; l++) if (_cards[l] == m) return l; return 0; }

        public override void Tick(float dt)
        {
            if (!(Focused is CardItem card) || card == _shown) return;
            _shown = card;
            var c = Challenges.Get(_mode, Level(card));
            _detail.Clear();
            Kit.L(_detail, c.LevelName.ToUpperInvariant() + "  ·  THE STARS", "k-panel-title");
            Stars.Ladder(_detail, c);
            if (Replays.ArcadeBests.Has(c.Id))
            {
                // P2-M6: watch your best play of this level again
                if (_watch != null) { Items.Remove(_watch); }
                var cc = c;
                _watch = AddTo(_detail, new ActionItem("Watch best", () => FrontEnd.WatchArcadeBest(cc.Id), "▶"));
                _watch.Refresh();
            }
        }
    }

    // ---------------- practice ----------------

    public sealed class PracticeScreen : GameScreen
    {
        readonly bool _drills;
        public PracticeScreen(bool drills = false) { _drills = drills; }
        protected override string Kicker { get { return "PRACTICE"; } }
        protected override string Title { get { return "Practice Court"; } }
        CardItem _lastDrill;
        bool _opened;

        protected override void Compose()
        {
            // ---- FREE PRACTICE ----
            var free = Kit.Box(Page("FREE PRACTICE"), "k-ta-free");
            var hero = AddTo(free, new CardItem("FREE PRACTICE", "THE COURT", () => FrontEnd.LaunchFreePractice(), CardSize.Hero).Vignette("court")
                .Line("Just you and the wall: walk anywhere with the ball in your hand, drop it and hit, keep it going. Miss, and it is back in your hand. Want feeds? Switch the Coach tray to the feeder."));
            hero.Root.AddToClassList("k-card--free");
            var tray = Kit.Panel(free, "THE COACH TRAY  ·  TAB / VIEW WHILE YOU PLAY");
            tray.AddToClassList("k-ta-traypanel");
            Line(tray, "MODE", "free play (ball in hand, the default)  ·  feeder");
            Line(tray, "FEED", string.Join("  ·  ", CoachFeeder.FeedNames));
            Line(tray, "SIDE", string.Join("  ·  ", CoachFeeder.SideNames));
            Line(tray, "SPEED", string.Join("  ·  ", CoachFeeder.SpeedNames));
            Line(tray, "FEEDING", "automatic, every " + string.Join(" / ", CoachFeeder.IntervalNames) + ", or on your button (F)");
            Line(tray, "SLOW MOTION", string.Join("  ·  ", CoachFeeder.SlowNames));
            Line(tray, "TRAIL", "short, or long: see where every shot went");
            Line(tray, "BALL", "cold, or warm like a match ball");

            // ---- DRILLS ----
            var drills = Kit.Box(Page("DRILLS"), "k-row");
            drills.AddToClassList("k-ta-drills");
            int i = 0;
            foreach (var c in Challenges.All())
            {
                if (c.Mode != ArcadeMode.Drill) continue;
                var cc = c;
                var card = AddTo(drills, new CardItem(c.Name, "DRILL " + (++i), () => FrontEnd.LaunchChallenge(cc.Id))
                    .Drawn(Stars.Art(c), Stars.Bg(i + 1), Color.white)
                    .Line(() => cc.Description + "\n" + Best(cc))
                    .Badge(() => { var r = ProfileStore.Record(cc.Id); return r == null ? "☆☆☆" : Stars.Of(r.Stars); }));
                card.Root.AddToClassList("k-card--drill");
            }
            AddTo(Root, new PrimaryItem("Start", Start));
        }

        static void Line(VisualElement panel, string k, string v)
        {
            var row = Kit.Box(panel, "k-ta-trayrow");
            Kit.L(row, k, "k-ta-tray-k");
            Kit.L(row, v, "k-ta-tray-v");
        }

        static string Best(Challenge c)
        {
            var r = ProfileStore.Record(c.Id);
            return r == null ? "NOT PLAYED YET" : "BEST " + r.Best + (c.Balls > 0 ? " / " + c.Balls : "");
        }

        void Start()
        {
            if (PageIndex == 0) { FrontEnd.LaunchFreePractice(); return; }
            var c = _lastDrill;
            if (c == null) foreach (var it in Items) if (it is CardItem ci && ci.Title != "FREE PRACTICE") { c = ci; break; }
            c?.Submit();
        }

        public override void Tick(float dt) { if (Focused is CardItem c && c.Title != "FREE PRACTICE") _lastDrill = c; }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = 4;
            if (_drills && !_opened) { _opened = true; Tabs.Select(1); }
        }
    }

    // ---------------- in play ----------------

    public sealed class ArcadePauseScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-side"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Hint { get { return null; } }
        bool Drill { get { return Fe.Arcade.Challenge.Mode == ArcadeMode.Drill; } }
        protected override string Kicker { get { return (Drill ? "DRILL  ·  " : "TARGET ARCADE  ·  ") + Fe.Arcade.Challenge.Name.ToUpperInvariant(); } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            var s = Fe.Arcade.Session;
            var box = Kit.Box(Content, "k-side-score");
            var row = Kit.Box(box, "k-side-row");
            Kit.L(row, "SCORE", "k-side-name");
            Kit.L(row, s.Score.ToString("N0"), "k-side-points");
            Kit.L(Content, "Hits " + s.Hits + " / " + s.Attempts + "   ·   best combo " + s.BestCombo + "   ·   " + Fe.Arcade.Challenge.LevelName, "k-side-stats");
            Add(new ActionItem("Resume", () => Fe.Resume(), "▶"));
            Add(new ActionItem("Restart", () => { Fe.Clear(); Fe.Arcade.Restart(); }));
            Add(new ActionItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ActionItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ActionItem(Drill ? "Quit to Practice" : "Quit to Target Arcade", () => Fe.BackToArcadeMenu()));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Resume"));
            Root.Add(prompts);
        }
        public override void Back() { Fe.Resume(); }
    }

    public sealed class ResultsScreen : GameScreen
    {
        readonly int _score, _stars;
        readonly bool _best;
        readonly int _prev;
        readonly Challenge _c;
        readonly ArcadeSession _s;

        public ResultsScreen(ArcadeRunner r)
        {
            _s = r.Session;
            _c = r.Challenge;
            _score = _s.Score;
            _stars = _s.Stars;
            IsReplay = r.Watching != null;
            var rec = ProfileStore.Record(_c.Id);
            if (IsReplay) { _prev = rec != null ? rec.Best : int.MinValue; ReplayScore = r.WatchingScore; return; }   // watching a best: nothing to submit
            _prev = rec != null ? rec.Best : int.MinValue;
            int starsBefore = rec != null ? rec.Stars : 0;
            _best = ProfileStore.Submit(_c.Id, _score, _stars);
            if (_best && _c.Mode != ArcadeMode.Drill) Replays.ArcadeBests.Save(r.Log, _score);          // P2-M6: "Watch best"
            // P2-M5: XP, new stars, the arcade achievements
            Reward = Progression.Rewards.Arcade(_c.Id, _c.Mode == ArcadeMode.Drill, starsBefore, _stars, _score, _c.Mode == ArcadeMode.Streak);
        }

        /// <summary>This play set a new best (tests).</summary>
        public bool NewBest { get { return _best; } }
        /// <summary>P2-M6: this was a saved best played back (and the score it was saved with).</summary>
        public bool IsReplay { get; private set; }
        public int ReplayScore { get; private set; }
        public Progression.RewardResult Reward { get; private set; }
        /// <summary>Stars shown so far (they pop in one by one).</summary>
        public int StarsShown { get; private set; }

        protected override bool ShowChip { get { return false; } }
        bool IsDrill { get { return _c.Mode == ArcadeMode.Drill; } }
        protected override string Kicker { get { return (IsDrill ? "DRILL" : _c.LevelName.ToUpperInvariant()) + "  ·  RESULTS"; } }
        protected override string Title { get { return _c.Name; } }

        protected override void Compose()
        {
            if (IsReplay)
            {
                AddTo(Root, new PrimaryItem("Target Arcade", () => Fe.BackToArcadeMenu()));
                var rc = Kit.Box(Content, "k-cr-result"); rc.AddToClassList("k-cr-result--card");
                var st = new Stamp("YOUR BEST, AGAIN", 0.2f); st.AddToClassList("k-stamp--good"); rc.Add(st);
                Kit.L(rc, _score.ToString("N0"), "k-ta-score");
                Kit.L(rc, _score == ReplayScore ? "Played back exactly: the same " + _score.ToString("N0") + "." : "Saved as " + ReplayScore.ToString("N0") + " - this version plays it differently.", "k-ta-res-best");
                var again = Kit.Box(rc, "k-cr-pv-acts");
                AddTo(again, new ActionItem("Watch again", () => { Fe.Clear(); Fe.Arcade.Restart(); }));
                return;
            }
            AddTo(Root, new PrimaryItem("Retry", () => { Fe.Clear(); Fe.Arcade.Restart(); }));
            var col = Kit.Box(Content, "k-cr-result");
            var top = Kit.Box(col, "k-ta-res-top");
            Kit.L(top, _c.Mode == ArcadeMode.Streak ? "STREAK" : IsDrill ? (_c.Drill == DrillKind.Ghosting ? "SPOTS" : "ON TARGET") : "SCORE", "k-ta-res-k");
            if (IsDrill) Kit.L(top, _score + " / " + Mathf.Max(_s.Attempts, _c.Balls), "k-ta-score");
            else { var cu = new CountUp(_score, 1.1); cu.AddToClassList("k-ta-score"); top.Add(cu); }

            var stars = Kit.Box(col, "k-ta-stars");
            for (int i = 0; i < 3; i++)
            {
                var st = Kit.L(stars, "★", "k-ta-star");
                if (i >= _stars) continue;
                int k = i;
                st.schedule.Execute(() => { st.AddToClassList("k-ta-star--on"); StarsShown = k + 1; Sounds.Play("star"); }).StartingIn(1200 + 380 * i);
            }
            if (_best) stars.Add(new Stamp(_prev == int.MinValue ? "FIRST SCORE" : "NEW BEST", 1.3f + 0.38f * _stars));
            else Kit.L(col, "Best " + ProfileStore.Record(_c.Id).Best.ToString("N0"), "k-ta-res-best");
            if (_best && _prev != int.MinValue) Kit.L(col, "was " + _prev.ToString("N0"), "k-ta-res-best");

            var tiles = Kit.Box(col, "k-cr-tiles");
            Career.Tile(tiles, _c.Drill == DrillKind.Ghosting ? "SPOTS" : "TARGETS HIT", _s.Hits + " / " + _s.Attempts);
            Career.Tile(tiles, "BEST COMBO", _s.BestCombo.ToString());
            Career.Tile(tiles, "GREAT HITS", _s.GreatHits.ToString());
            if (_stars < 3 && _c.Stars[_stars] > 0)
            {
                var next = Kit.Box(col, "k-ta-nextbox");
                Kit.L(next, "Next star at " + _c.Stars[_stars].ToString("N0"), "k-ta-next-k");
                var bar = Kit.Box(next, "k-ta-bar");
                Kit.Box(bar, "k-ta-bar-fill").style.width = Length.Percent(Mathf.Clamp01((float)_score / _c.Stars[_stars]) * 100f);
            }
            col.Add(new RewardStrip(Reward));
            Fe.ToastAchievements(Reward);

            var acts = Kit.Box(col, "k-cr-pv-acts");
            if (!IsDrill && _c.Level < 2)
            {
                var next = Challenges.Get(_c.Mode, _c.Level + 1);
                AddTo(acts, new ActionItem("Next level: " + next.LevelName, () => { Fe.Clear(); Fe.Arcade.Play(next); }));
            }
            AddTo(acts, new ActionItem(IsDrill ? "Practice menu" : "Target Arcade", () => Fe.BackToArcadeMenu()));
        }

        public override void Back() { Fe.BackToArcadeMenu(); }
    }

    public sealed class CoachScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-tray"; } }
        public override bool Spatial { get { return true; } }
        protected override string Kicker { get { return "PRACTICE COURT"; } }
        protected override string Title { get { return "Coach"; } }
        protected override string Hint { get { return null; } }
        static CoachChoices C { get { return ProfileStore.Current.Coach; } }
        CoachFeeder Feeder { get { return Fe.Coach; } }

        readonly VisualElement[] _pages = new VisualElement[2];
        TabBar _tabs;

        protected override void Build()
        {
            // two short pages (Q / E, LB / RB): the feed, and how you see it - a few choices at a time
            _tabs = new TabBar("FEED", "VIEW");
            _tabs.Changed += Page;
            Content.Add(_tabs);
            _pages[0] = Kit.Box(Content, "k-tray-page");
            AddTo(_pages[0], new SelectorItem("Mode", CoachFeeder.ModeNames, () => C.Feeder ? 1 : 0, v => C.Feeder = v == 1));
            AddTo(_pages[0], new CarouselItem("Feed", CoachFeeder.FeedNames, () => C.Feed, v => C.Feed = v)).Enabled = () => C.Feeder;
            AddTo(_pages[0], new SelectorItem("Side", CoachFeeder.SideNames, () => C.Side, v => C.Side = v)).Enabled = () => C.Feeder;
            AddTo(_pages[0], new SelectorItem("Speed", CoachFeeder.SpeedNames, () => C.Speed, v => C.Speed = v)).Enabled = () => C.Feeder;
            AddTo(_pages[0], new SelectorItem("Feeding", CoachFeeder.FeedingNames, () => C.Auto ? C.Interval : CoachFeeder.FeedingNames.Length - 1,
                v => { C.Auto = v < CoachFeeder.FeedingNames.Length - 1; if (C.Auto) C.Interval = v; })).Enabled = () => C.Feeder;
            _pages[1] = Kit.Box(Content, "k-tray-page");
            AddTo(_pages[1], new SelectorItem("Slow-mo", CoachFeeder.SlowNames, () => C.Slow, v => C.Slow = v));
            AddTo(_pages[1], new SelectorItem("Trail", new[] { "Short", "Long" }, () => C.LongTrail ? 1 : 0, v => { C.LongTrail = v == 1; Feeder.Apply(); }));
            AddTo(_pages[1], new SelectorItem("Ball", new[] { "Cold", "Warm" }, () => C.WarmBall ? 1 : 0, v => C.WarmBall = v == 1));
            Add(new ActionItem("Feed now", () => { Fe.Pop(); C.Feeder = true; Feeder.Apply(); Feeder.FeedNow(); }, "▶"));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.Tabs, "Page"), (PromptBar.Btn.B, "Close"));
            Root.Add(prompts);
            Page(0);
        }

        void Page(int i)
        {
            for (int k = 0; k < 2; k++) _pages[k].style.display = k == i ? DisplayStyle.Flex : DisplayStyle.None;
            Root.schedule.Execute(FocusFirstShown);
        }

        public override void OnTab(int d) { _tabs.Step(d); }

        public override void OnShow() { base.OnShow(); Fe.Runner.Clock.Pause(); }
        public override void OnHide()
        {
            ProfileStore.Save();
            if (Feeder != null) Feeder.Apply();
            Fe.Runner.Clock.Resume();
        }
    }
}
