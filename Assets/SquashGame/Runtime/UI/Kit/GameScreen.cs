// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/GameScreen.cs
// UI redesign (U1): the base of every redesigned screen.
//
//   full screen, a 96 px safe margin, the court behind (a dark fade at
//   the bottom so cards read)
//   top bar     kicker + title left, tabs centre (LB / RB), the player
//               chip right
//   pages       one per tab (Page("NAME")); only the current one shows
//   prompts     the action legend bottom left, for the device in use
//   focus       2D: the arrows / stick go to the nearest choice
// ===========================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public abstract class GameScreen : MenuScreen
    {
        public override bool Spatial { get { return true; } }
        protected override bool Sidebar { get { return false; } }
        protected override string Hint { get { return null; } }
        protected override string RootClass { get { return "k-screen"; } }
        /// <summary>Show the player chip top right.</summary>
        protected virtual bool ShowChip { get { return true; } }
        /// <summary>What pressing the player tag does (null: it is just a tag). The main menu: your profile.</summary>
        protected virtual Action OnChip { get { return null; } }
        /// <summary>The tag as a menu item, when it does something.</summary>
        public ChipItem ChipButton { get; private set; }

        protected VisualElement TopBar, TopLeft;
        public TabBar Tabs { get; private set; }
        public PromptBar Prompts { get; private set; }
        public PlayerChip Chip { get; private set; }
        readonly List<VisualElement> _pages = new List<VisualElement>();
        readonly List<string> _pageNames = new List<string>();
        public int PageIndex { get { return Tabs != null ? Tabs.Index : 0; } }

        protected sealed override void Build()
        {
            // the top bar: the kicker and title MenuScreen made, the tabs, the chip
            TopBar = Kit.Box(null, "k-topbar");
            TopLeft = Kit.Box(TopBar, "k-topbar-left");
            foreach (var c in new List<VisualElement>(Root.Children()))
                if (c.ClassListContains("fe-kicker") || c.ClassListContains("fe-title")) { c.RemoveFromHierarchy(); TopLeft.Add(c); }
            Root.Insert(0, TopBar);
            var fade = Kit.Box(null, "k-fade"); Root.Insert(0, fade);          // the bottom darkens so cards read over the court
            fade.style.backgroundImage = new StyleBackground(Palette.BottomFade());
            var top = Kit.Box(null, "k-fade-top"); Root.Insert(0, top);         // and the top, so the tabs read over a court sign
            top.style.backgroundImage = new StyleBackground(Palette.TopFade());
            Content.AddToClassList("k-content");

            Compose();

            if (_pages.Count > 1)
            {
                Tabs = new TabBar(_pageNames.ToArray());
                Tabs.Changed += i => ShowPage(i);
                TopBar.Insert(1, Tabs);
                ShowPage(0);
            }
            if (ShowChip)
            {
                Chip = new PlayerChip();
                if (OnChip != null) ChipButton = AddTo(TopBar, new ChipItem(Chip, OnChip));
                else TopBar.Add(Chip);
            }
            Prompts = new PromptBar();
            Root.Add(Prompts);
            SetPrompts();
        }

        /// <summary>Build the screen: cards, widgets, pages (Page), the primary action (AddTo(Root, new PrimaryItem(...))).</summary>
        protected abstract void Compose();

        /// <summary>The prompt bar's actions (default: Select, Back, Tabs when there are tabs).</summary>
        protected virtual void SetPrompts()
        {
            var items = new List<(PromptBar.Btn, string)> { (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back") };
            if (_pages.Count > 1) items.Add((PromptBar.Btn.Tabs, "Tabs"));
            Prompts.Set(items.ToArray());
        }

        /// <summary>A tab page: its own container, shown when its tab is picked.</summary>
        protected VisualElement Page(string name)
        {
            var p = Kit.Box(Content, "k-page");
            p.pickingMode = PickingMode.Position;
            _pages.Add(p); _pageNames.Add(name);
            return p;
        }

        void ShowPage(int i)
        {
            for (int k = 0; k < _pages.Count; k++) _pages[k].style.display = k == i ? DisplayStyle.Flex : DisplayStyle.None;
            Root.schedule.Execute(() => { FocusFirstShown(); OnPage(i); });
        }

        /// <summary>The tab changed (after the page is shown).</summary>
        protected virtual void OnPage(int i) { }

        public override void OnTab(int d) { if (Tabs != null) Tabs.Step(d); }

        public override void OnShow()
        {
            base.OnShow();
            if (Chip != null) Chip.Refresh();
            Root.schedule.Execute(() => { if (Focused == null || Focused.Root.resolvedStyle.display == DisplayStyle.None) FocusFirstShown(); });
        }
    }

    /// <summary>A developer's look at every kit component (F12 on the main menu; the U1 test).</summary>
    public sealed class GalleryScreen : GameScreen
    {
        protected override string Kicker { get { return "UI KIT"; } }
        protected override string Title { get { return "Gallery"; } }
        int _diff = 1, _len = 1, _score;
        public SelectorItem Difficulty { get; private set; }
        public BracketView Bracket { get; private set; }
        public CardItem Locked { get; private set; }
        string _picked = "";
        public string Picked { get { return _picked; } }

        protected override void Compose()
        {
            // ---- CARDS: a hub row ----
            var cards = Page("CARDS");
            var row = Kit.Box(cards, "k-row");
            AddTo(row, new CardItem("QUICK MATCH", "PLAY NOW", () => _picked = "quick", CardSize.Hero).Vignette("versus")
                .Line("You v Attacker  ·  Medium  ·  Best of 3").Badge(() => "LAST: W 11-7", true));
            AddTo(row, new CardItem("TARGET ARCADE", "SOLO", () => _picked = "arcade", CardSize.M).Drawn("rings", new Color32(20, 23, 30, 255), Color.white)
                .Line("6 modes  ·  18 levels").Badge(() => "★ 23 / 54"));
            Locked = AddTo(row, new CardItem("WORLD FINALS", "CAREER", () => _picked = "finals", CardSize.M).Drawn("trophy", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255)));
            Locked.Locked = () => true; Locked.LockLine = () => "season's top 8";
            AddTo(row, new CardItem("YOUR KIT", "LOCKER", () => _picked = "kit", CardSize.M).Vignette("you")
                .Line("3 new items").Badge(() => "LV 7")).IsNew = () => true;

            // ---- CONTROLS ----
            var controls = Page("CONTROLS");
            var left = Kit.Panel(controls, "MATCH SETUP");
            Difficulty = AddTo(left, new SelectorItem("Difficulty", new[] { "Easy", "Medium", "Hard" }, () => _diff, v => _diff = v, true));
            AddTo(left, new SelectorItem("Match", new[] { "1 game", "Best of 3", "Best of 5" }, () => _len, v => _len = v));
            AddTo(left, new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => _score, v => _score = v));
            var right = Kit.Panel(controls, "TIERS");
            var tiers = Kit.Box(right, "k-tierrow");
            string[] names = { "CLUB", "REGIONAL", "CHALLENGER", "TOUR", "MAJOR", "WORLD FINALS" };
            for (int t = 0; t < names.Length; t++) { var b = Kit.Box(tiers, "k-tieritem"); b.Add(new TierBadge(t, 30)); Kit.L(b, names[t], "k-tier-name"); }

            // ---- DATA ----
            var data = Page("DATA");
            var grid = Kit.Box(data, "k-grid3");
            var tape = Kit.Panel(grid, "TALE OF THE TAPE");
            tape.Add(new StatBars().Row("Rallies won", 11, 7).Row("Winners", 6, 4).Row("Errors", 2, 5, null, null, true).Row("Top pace", 138, 121, "138", "121").Row("Great hits", 5, 3));
            var table = Kit.Panel(grid, "WORLD RANKING");
            table.Add(new RankTable(new[]
            {
                new RankTable.R { Pos = 16, Move = 2, Name = "Rohan Garrowby", Chip = "IRL", Points = 1135 },
                new RankTable.R { Pos = 17, Move = -1, Name = "Ines Pellerin", Chip = "FRA", Points = 1090 },
                new RankTable.R { Pos = 18, Move = 6, Name = "Tester", Chip = "ENG", Points = 1060, You = true },
                new RankTable.R { Pos = 19, Move = -3, Name = "Soren Falkenrath", Chip = "KOR", Points = 1025, Star = true },
                new RankTable.R { Pos = 20, Move = 0, Name = "Dario Larkspur", Chip = "HKG", Points = 1020 },
            }));
            var nums = Kit.Panel(grid, "RESULT");
            nums.Add(new CountUp(12450, 1.2, "N0", "", " pts"));
            nums.Add(new Stamp("NEW BEST", 0.5f));

            var lower = Kit.Box(data, "k-grid2");
            var bp = Kit.Panel(lower, "THE DRAW");
            Bracket = new BracketView();
            bp.Add(Bracket);
            string[,] r1 = { { "[1] Madigan", "Rookwood" }, { "Calderwood", "[4] Tester" }, { "[3] Sefton", "Ivory" }, { "Okonkwo", "[2] Juniper" } };
            int[] w1 = { 0, 1, 0, 1 }; string[] s1 = { "11-4 11-2", "6-11 9-11", "11-8 11-7", "4-11 11-13" };
            Bracket.Column(0, "QUARTER-FINALS"); Bracket.Column(1, "SEMI-FINALS"); Bracket.Column(2, "FINAL");
            for (int i = 0; i < 4; i++)
            {
                var m = new BracketMatchItem(0, i, r1[i, 0], r1[i, 1], w1[i], s1[i], false, r1[i, 1].Contains("Tester"), x => _picked = x.Name);
                AddTo(Bracket, m); Bracket.Place(m, 4);
            }
            var sf1 = new BracketMatchItem(1, 0, "[1] Madigan", "[4] Tester", -1, null, false, true, x => _picked = x.Name);
            var sf2 = new BracketMatchItem(1, 1, "[3] Sefton", "[2] Juniper", -1, null, false, false, x => _picked = x.Name);
            var fin = new BracketMatchItem(2, 0, null, null, -1, null, false, false, x => _picked = x.Name);
            foreach (var m in new[] { sf1, sf2, fin }) { AddTo(Bracket, m); Bracket.Place(m, 4); }
            var tl = Kit.Panel(lower, "SEASON");
            var months = new List<(string, string, int)>();
            string[] mo = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            for (int i = 0; i < 12; i++) months.Add((mo[i], i == 0 ? "QF" : i == 1 ? "W" : "", i < 2 ? -1 : i == 2 ? 0 : 1));
            tl.Add(new Timeline(months));

            AddTo(Root, new PrimaryItem("Play", () => _picked = "primary"));
        }
    }
}
