// ===========================================================
// Assets/SquashGame/Runtime/UI/SetupScreens.cs
// 2026-11-22 a match set up in steps (the user: "spread the clicking
// process into a few tabs ... quick match, choose the par 11 or 9, then
// the court, then the classes"; and the pick screens like a fighting
// game's select: "the list grid on the left and a visual look of what you
// are picking and the details on the right"):
//
//   Quick Match     Format -> Opponent -> Court -> Class -> Ready (PLAY)
//   Local Versus    Format -> Court -> Class (P1) -> Class (P2) -> Ready
//
//   PickScreen      the grid (left) and the preview (right): a 3D look,
//                   the name, what it is in plain words. Moving over a tile
//                   shows it; Enter picks it and goes on. Back: one step.
//   OpponentPick    the ladder (club players to the world No. 1), each
//                   with their class and style
//   CourtPick       every court, the court itself behind the menu
//   ClassPick       the five classes: strengths, weakness, perk, the bars
//
// Plan: SQUASH FRONTEND MODIFIED/RE_SQUASHSTRIKE_PROS_CLASSES_PLAN.md.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>The steps, in order, and the jumps back to one.</summary>
    public static class SetupFlow
    {
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        /// <summary>The next step after <paramref name="from"/> (Quick Match or Local Versus).</summary>
        public static void Next(FrontEnd fe, MenuScreen from, bool two)
        {
            ProfileStore.Save();
            if (!two)
            {
                if (from is QuickMatchScreen) fe.Push(new OpponentPickScreen());
                else if (from is OpponentPickScreen) fe.Push(new CourtPickScreen(false));
                else if (from is CourtPickScreen) fe.Push(new ClassPickScreen(0, false));
                else fe.Push(new QuickMatchReadyScreen());
            }
            else
            {
                if (from is LocalVersusScreen) fe.Push(new ControlsScreen());              // 2026-11-23: who plays with what
                else if (from is ControlsScreen) fe.Push(new CourtPickScreen(true));
                else if (from is CourtPickScreen) fe.Push(new ClassPickScreen(0, true));
                else if (from is ClassPickScreen cp && cp.Player == 0) fe.Push(new ClassPickScreen(1, true));
                else fe.Push(new LocalVersusReadyScreen());
            }
        }

        /// <summary>Back to the step of this kind (the Ready screen's "change"): the ones after it closed.</summary>
        public static void BackTo<T>(FrontEnd fe, Func<T, bool> which = null) where T : MenuScreen
        {
            for (int guard = 0; guard < 8 && fe.Top != null; guard++)
            {
                if (fe.Top is T t && (which == null || which(t))) return;
                fe.Pop();
            }
        }

        // ---- the classes' colours and names (the pros' too) ----
        public static Color ClassColour(string id)
        {
            switch (PlayerClasses.FamilyOf(id))                                 // 2026-11-22 evening: 35 classes, five colours
            {
                case "power": return new Color32(255, 90, 74, 255);
                case "accuracy": return new Color32(60, 200, 255, 255);
                case "speed": return new Color32(200, 255, 61, 255);
                case "touch": return new Color32(190, 110, 255, 255);
                default: return new Color32(255, 196, 64, 255);
            }
        }
        public static string ClassName(string id) { var d = PlayerClasses.Find(id); return d != null ? d.Name : "All-rounder"; }

        /// <summary>An opponent's class: a star's from their athlete and shots; a club player's from their style.</summary>
        public static string OpponentClass(string id)
        {
            var pp = ProPersonas.For(id);
            if (pp != null && PlayerClasses.Find(pp.Class) != null) return pp.Class;          // the top 10: their own
            var star = SquashSim.Career.Stars.Find(id);
            if (star != null) return PlayerClasses.Nearest(star.Attributes, (uint)star.Rank);   // 2026-11-22 evening: the nearest of all 35
            var club = SquashSim.Career.Stars.FindClub(id);
            if (club != null) return club.Class;
            string style = SquashSim.Career.Stars.Describe(id).style;
            switch (style)
            {
                case "attacker": case "powerhouse": return "power";
                case "retriever": case "counter-puncher": return "speed";
                case "shotmaker": case "magician": return "touch";
                case "volleyer": return "accuracy";
                default: return "allround";
            }
        }

        /// <summary>Your class now (a saved id that's gone: the All-rounder).</summary>
        public static string Mine(int player = 0)
        {
            string id = player == 0 ? C.Class : C.VsClass2;
            return PlayerClasses.Find(id) != null ? id : PlayerClasses.Default;
        }
    }

    // ---------------- the pick screen: the grid and the preview ----------------

    public abstract class PickScreen : GameScreen
    {
        protected override bool ShowChip { get { return false; } }
        public override bool Spatial { get { return true; } }
        /// <summary>2026-11-22 evening (the user: "give the selection page a world background or else when you pick things the
        /// background keep changing especially when picking courts that makes it very laggy"): every step, Versus Night.</summary>
        public override MenuWorld Backdrop { get { return MenuWorld.Play; } }
        protected readonly List<(MenuItem item, string id)> Tiles = new List<(MenuItem, string)>();
        /// <summary>2026-11-22 opened on its own (not a step): the pick is handed here and the screen closes.</summary>
        public Action<string> Done;
        /// <summary>2026-11-24 a collection (the pro cards, the perks): A acts on the tile and the screen stays.</summary>
        protected virtual bool Browse { get { return false; } }
        /// <summary>Redraw the panel (after a browse pick).</summary>
        protected void Refresh() { _shown = null; }
        ScrollView _scroll;
        protected VisualElement Preview, Details, Pic;
        MenuItem _shown;
        protected abstract bool TwoPlayer { get; }
        /// <summary>The ids in the grid (in order), their tile's name, kicker and colour.</summary>
        protected abstract IEnumerable<string> Ids { get; }
        protected abstract string TileName(string id);
        protected abstract string TileKicker(string id);
        protected abstract Color TileColour(string id);
        protected virtual string TileBadge(string id) { return ""; }
        /// <summary>The tile's big letters (initials).</summary>
        protected virtual string TileGlyph(string id)
        {
            var words = TileName(id).Split(' ');
            string g = "";
            foreach (var w in words) if (w.Length > 0 && char.IsLetter(w[0]) && g.Length < 2) g += char.ToUpperInvariant(w[0]);
            return g;
        }
        /// <summary>The id picked now (focused at the start).</summary>
        protected abstract string Current { get; }
        protected abstract void Choose(string id);
        /// <summary>The preview's picture: the vignette shot ("them", "you", "court"), or null.</summary>
        protected abstract string Shot(string id);
        protected abstract void Describe(string id, VisualElement into);
        /// <summary>The look follows the tile in focus (the opponent's kit, the court behind).</summary>
        protected virtual void Hover(string id) { }
        /// <summary>2026-11-22 evening: tabs over the grid (Q / E, LB / RB) - null: one grid. Which tab a tile is on, its order there.</summary>
        protected virtual string[] TabNames { get { return null; } }
        protected virtual int TabOf(string id) { return 0; }
        /// <summary>2026-11-25 (the user: "an ALL tab that shows every class"): a tile shows on its own tab and on ALL (tab 0) when
        /// the screen has one.</summary>
        protected virtual bool HasAll { get { return false; } }
        bool InTab(string id, int tab) { return TabOf(id) == tab || (HasAll && tab == 0); }
        /// <summary>The tab it opens on: the one the current pick is on.</summary>
        protected virtual int StartTab(string current) { return TabOf(current); }
        protected virtual int TabOrder(string id) { return 0; }
        TabBar _tabBar;
        /// <summary>The tab shown (tests).</summary>
        public int TabShown { get; private set; }
        void ShowTab(int t)
        {
            TabShown = t;
            foreach (var tile in Tiles) tile.item.Root.style.display = InTab(tile.id, t) ? DisplayStyle.Flex : DisplayStyle.None;
            var f = Focused;
            if (f == null || f.Root.style.display == DisplayStyle.None)
                foreach (var tile in Tiles) if (InTab(tile.id, t)) { Focus(tile.item); break; }
            if (_scroll != null) _scroll.scrollOffset = Vector2.zero;
        }
        public override void OnTab(int d) { if (_tabBar != null) _tabBar.Step(d); }

        protected override void Compose()
        {
            var wrap = Kit.Box(Content, "pk");
            var left = Kit.Box(wrap, "pk-left");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("pk-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var names = TabNames;
            if (names != null) { _tabBar = new TabBar(names); _tabBar.Changed += ShowTab; left.Add(_tabBar); }
            left.Add(_scroll);
            var grid = Kit.Box(_scroll, "pk-grid");
            var ids = new List<string>(Ids);
            if (names != null) ids.Sort((a, b) => TabOf(a) != TabOf(b) ? TabOf(a).CompareTo(TabOf(b)) : TabOrder(a).CompareTo(TabOrder(b)));
            foreach (var id in ids)
            {
                string pick = id;
                // a portrait-like tile: its colour (the class, the walls), big initials, the name under
                Color col = TileColour(id);
                var item = AddTo(grid, new CardItem(TileName(id), TileKicker(id), () => { Choose(pick); if (Browse) { Refresh(); return; } if (Done != null) { Fe.Pop(); Done(pick); } else SetupFlow.Next(Fe, this, TwoPlayer); }, CardSize.S)
                    .Glyph(TileGlyph(id))
                    .Badge(() => TileBadge(pick), false));
                item.Root.AddToClassList("pk-tile");
                item.Art.style.backgroundColor = new Color(col.r * 0.32f, col.g * 0.32f, col.b * 0.32f, 1f);
                item.Root.style.borderTopColor = col; item.Root.style.borderTopWidth = 4;
                item.Art.Q<Label>(className: "k-card-glyph").style.color = col;
                Tiles.Add((item, id));
            }
            Preview = Kit.Box(wrap, "pk-right");
            Pic = Kit.Box(Preview, "pk-pic");
            Details = Kit.Box(Preview, "pk-details");
        }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = TwoPlayer ? 1 : 0;
            MatchRunner.Showcase = true; MatchRunner.Studio = true; MatchRunner.StudioPair = true; KitStudio.Show(true);
            string cur = Current;
            if (_tabBar != null) { int want = StartTab(cur); _tabBar.Select(want); ShowTab(want); }
            foreach (var t in Tiles) if (t.id == cur) { Focus(t.item); break; }
            _shown = null;
        }

        public override void OnHide()
        {
            ProfileStore.Save();
            MatchRunner.Showcase = false; MatchRunner.Studio = false; MatchRunner.StudioPair = false; KitStudio.Show(false);
        }

        string _picShot;
        public override void Tick(float dt)
        {
            if (Focused == null || Focused == _shown) return;
            _shown = Focused;
            _scroll.ScrollTo(_shown.Root);
            string id = null;
            foreach (var t in Tiles) if (t.item == _shown) id = t.id;
            if (id == null) return;
            Hover(id);
            string shot = Shot(id);
            if (shot != _picShot) { Pic.Clear(); _picShot = shot; if (shot != null) Vignettes.Attach(Pic, shot, 640, 520); }
            Pic.style.display = shot != null ? DisplayStyle.Flex : DisplayStyle.None;            // 2026-11-24 a collection without a picture
            Details.Clear();
            Describe(id, Details);
            Juice.FadeIn(Details);
        }

        /// <summary>The id in focus now (tests).</summary>
        public string FocusedId { get { foreach (var t in Tiles) if (t.item == Focused) return t.id; return null; } }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Pick"), (PromptBar.Btn.B, "Back")); }

        /// <summary>A stat bar (0..10) with its word.</summary>
        protected static void Bar(VisualElement into, string name, double v, Color c)
        {
            var row = Kit.Box(into, "pk-bar-row");
            Kit.L(row, name, "pk-bar-name");
            var track = Kit.Box(row, "pk-bar");
            var fill = Kit.Box(track, "pk-bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01((float)v / 10f) * 100f);
            fill.style.backgroundColor = c;
        }
    }

    // ---------------- the steps ----------------

    /// <summary>Quick Match, step 1: how long, the scoring, the abilities.</summary>
    public sealed class QuickMatchScreen : FormatScreen
    {
        protected override bool TwoPlayer { get { return false; } }
        protected override string Title { get { return "Quick Match"; } }
    }

    /// <summary>Local Versus, step 1.</summary>
    public sealed class LocalVersusScreen : FormatScreen
    {
        protected override bool TwoPlayer { get { return true; } }
        protected override string Title { get { return "Local Versus"; } }
    }

    public abstract class FormatScreen : GameScreen
    {
        protected abstract bool TwoPlayer { get; }
        public override MenuWorld Backdrop { get { return MenuWorld.Play; } }       // 2026-11-22 Versus Night behind step 1
        protected override string Kicker { get { return "PLAY  ·  STEP 1  ·  THE MATCH"; } }
        protected override bool ShowChip { get { return false; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }
        static GameSettings S { get { return ProfileStore.Current.Settings; } }
        Label _hint;

        protected override void Compose()
        {
            var board = Kit.Box(Content, "k-vs-mid");
            board.AddToClassList("k-vs-board"); board.AddToClassList("pk-format");
            if (!TwoPlayer)
            {
                AddTo(board, new SelectorItem("Match", VersusScreen.LengthTiles, () => C.Length, v => C.Length = v));
                AddTo(board, new SelectorItem("Scoring", VersusScreen.ScoringTiles, () => Mathf.Clamp(C.Scoring, 0, 1), v => C.Scoring = v));
                if (MatchChoices.QuickMatchAbilities)
                {
                    AddTo(board, new SelectorItem("Abilities", VersusScreen.AbilityTiles, () => C.Abilities ? 1 : 0, v => { C.Abilities = v == 1; _hint.text = C.Abilities ? MatchChoices.AbilityHint : ""; }));
                    _hint = Kit.L(board, C.Abilities ? MatchChoices.AbilityHint : "", "k-vs-hint");
                }
            }
            else
            {
                AddTo(board, new SelectorItem("Match", VersusScreen.LengthTiles, () => C.VsLength, v => C.VsLength = v));
                AddTo(board, new SelectorItem("Scoring", VersusScreen.ScoringTiles, () => C.VsScoring, v => C.VsScoring = v));
                AddTo(board, new SelectorItem(MatchChoices.ControlName, VersusScreen.AimTiles, () => S.Assisted ? 1 : 0, v => { S.Assisted = v == 1; ProfileStore.Apply(S, false); }));
            }
            AddTo(Root, new PrimaryItem(TwoPlayer ? "Next: the controls" : "Next: your opponent", () => SetupFlow.Next(Fe, this, TwoPlayer)));
        }

        public override void OnHide() { ProfileStore.Save(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Next"), (PromptBar.Btn.B, "Back")); }
    }

    /// <summary>Quick Match, step 2: who you play - the ladder, the pro in 3D, their class and style.</summary>
    public sealed class OpponentPickScreen : PickScreen
    {
        protected override bool TwoPlayer { get { return false; } }
        protected override string Kicker { get { return "QUICK MATCH  ·  STEP 2  ·  YOUR OPPONENT"; } }
        protected override string Title { get { return "Choose your opponent"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        protected override IEnumerable<string> Ids { get { return MatchChoices.Ladder; } }
        // 2026-11-22 evening (the user's answer: tabs, Pros / Club): the pros by world rank, the club players by rating
        protected override string[] TabNames { get { return new[] { "ALL", "PROS", "CLUB" }; } }          // 2026-11-25 ALL
        protected override bool HasAll { get { return true; } }
        protected override int TabOf(string id) { return id != null && id.StartsWith("star:") ? 1 : 2; }
        protected override int TabOrder(string id) { var s = SquashSim.Career.Stars.Find(id); return s != null ? s.Rank : -SquashSim.Career.Stars.Describe(id).rating; }
        protected override string TileName(string id) { var d = SquashSim.Career.Stars.Describe(id); return d.name; }
        protected override string TileKicker(string id)
        {
            var star = SquashSim.Career.Stars.Find(id);
            var d = SquashSim.Career.Stars.Describe(id);
            return (star != null ? "No. " + star.Rank : "CLUB") + "  ·  " + d.rating;
        }
        protected override Color TileColour(string id) { return SetupFlow.ClassColour(SetupFlow.OpponentClass(id)); }
        protected override string TileBadge(string id) { return SetupFlow.ClassName(SetupFlow.OpponentClass(id)).ToUpperInvariant(); }
        protected override string Current { get { return C.OpponentId; } }
        protected override void Choose(string id) { C.OpponentId = id; }
        protected override string Shot(string id) { return "them"; }

        protected override void Hover(string id)
        {
            var star = SquashSim.Career.Stars.Find(id);
            var d = SquashSim.Career.Stars.Describe(id);
            KitDirector.OpponentPreview = star != null ? Cosmetics.AiKit(star.KitSeed) : Cosmetics.Signature(d.style);
        }

        protected override void Describe(string id, VisualElement into)
        {
            var star = SquashSim.Career.Stars.Find(id);
            var d = SquashSim.Career.Stars.Describe(id);
            string cls = SetupFlow.OpponentClass(id);
            Kit.L(into, star != null ? "WORLD No. " + star.Rank + "  ·  RATED " + d.rating : "CLUB PLAYER  ·  RATED " + d.rating, "pk-kicker");
            Kit.L(into, d.name, "pk-name");
            var badge = Kit.L(into, SetupFlow.ClassName(cls).ToUpperInvariant() + "  ·  " + SquashSim.Career.Roster.StyleName(d.style).ToUpperInvariant(), "pk-class");
            badge.style.backgroundColor = SetupFlow.ClassColour(cls);
            int at = MatchChoices.LadderIndex(id), n = MatchChoices.Ladder.Length;
            Kit.L(into, "DIFFICULTY  ·  " + VersusScreen.TierNames[VersusScreen.Tier(at, n)], "pk-line");
            var persona = ProPersonas.For(id);
            if (persona != null) Kit.L(into, persona.Summary, "pk-perk");                         // 2026-11-22 their own game
            if (star != null && !string.IsNullOrEmpty(star.Bio)) Kit.L(into, star.Bio, "pk-text");
            else foreach (var line in SquashSim.Career.Roster.StyleLines(d.style)) Kit.L(into, line, "pk-text");
            if (star != null && star.Signature.Count > 0)
                Kit.L(into, "Signature: " + string.Join(", ", star.Signature.ConvertAll(m => { var a = SquashSim.Modes.GauntletAbilities.Find(m.Id); return a != null ? a.Name : m.Id; })), "pk-text");
            var a2 = star != null ? star.Attributes : new AthleteAttributes();
            var bars = Kit.Box(into, "pk-bars");
            Color c = SetupFlow.ClassColour(cls);
            Bar(bars, "SPEED", a2.Speed, c); Bar(bars, "POWER", a2.Power, c); Bar(bars, "CONTROL", a2.Control, c); Bar(bars, "STAMINA", a2.Stamina, c);
        }

        public override void OnHide() { base.OnHide(); KitDirector.OpponentPreview = null; }
    }

    /// <summary>The court: every one in the grid, the court itself behind and in the preview.</summary>
    public sealed class CourtPickScreen : PickScreen
    {
        readonly bool _two;
        public CourtPickScreen(bool two) { _two = two; }
        protected override bool TwoPlayer { get { return _two; } }
        protected override string Kicker { get { return (_two ? "LOCAL VERSUS  ·  STEP 2" : "QUICK MATCH  ·  STEP 3") + "  ·  THE COURT"; } }
        protected override string Title { get { return "Choose the court"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        protected override IEnumerable<string> Ids { get { foreach (var c in Courts.Pickable) yield return c.Id; } }
        static int Index(string id) { return Array.FindIndex(Courts.Pickable, c => c.Id == id); }
        protected override string TileName(string id) { var i = Index(id); return i >= 0 ? Courts.Pickable[i].Name : id; }
        protected override string TileKicker(string id) { var i = Index(id); if (i < 0) return ""; var t = Courts.Pickable[i]; return i < Courts.All.Length ? "LEGACY" : (t.Blurb ?? "").ToUpperInvariant(); }
        // 2026-11-25 (the user: "make the court cards more colourful like how it is for the opponent selection ... another ALL tab ...
        // LEGACY, and LOCATION tab for courts")
        protected override string[] TabNames { get { return new[] { "ALL", "LEGACY", "LOCATION" }; } }
        protected override bool HasAll { get { return true; } }
        protected override int TabOf(string id) { return Index(id) < Courts.All.Length ? 1 : 2; }
        protected override int TabOrder(string id) { return Index(id); }
        protected override int StartTab(string current) { return 0; }                  // (every court, as before)
        protected override string TileBadge(string id) { var i = Index(id); if (i < 0) return ""; var t = Courts.Pickable[i]; return t.Outdoor ? "OUTDOOR" : t.Walls == WallStyle.Plaster ? "CLUB" : "GLASS"; }
        /// <summary>Each legacy court's own colour (its look: lava, aurora, neon...); a tour venue's, its front wall's - brightened.</summary>
        static readonly Dictionary<string, Color32> LegacyColours = new Dictionary<string, Color32>
        {
            { "show", new Color32(70, 120, 255, 255) }, { "club", new Color32(235, 70, 60, 255) }, { "desert", new Color32(255, 150, 50, 255) },
            { "station", new Color32(225, 180, 90, 255) }, { "rooftop", new Color32(190, 110, 255, 255) }, { "arena", new Color32(40, 220, 255, 255) },
            { "warehouse", new Color32(80, 210, 120, 255) }, { "leisure", new Color32(255, 110, 80, 255) }, { "county", new Color32(90, 140, 255, 255) },
            { "peak", new Color32(110, 255, 190, 255) }, { "beach", new Color32(255, 190, 70, 255) }, { "orbit", new Color32(130, 170, 255, 255) },
            { "neon", new Color32(255, 60, 200, 255) }, { "reef", new Color32(40, 230, 210, 255) }, { "temple", new Color32(140, 220, 60, 255) },
            { "volcano", new Color32(255, 80, 30, 255) },
        };
        protected override Color TileColour(string id)
        {
            var i = Index(id); if (i < 0) return Color.white;
            if (LegacyColours.TryGetValue(id, out var c)) return c;
            var v = id.StartsWith(Courts.TourPrefix) ? TourVenues.Find(id.Substring(Courts.TourPrefix.Length)) : null;
            if (v == null) return new Color32(120, 200, 255, 255);
            Color.RGBToHSV(v.Mid, out float h, out float s, out float b);
            return Color.HSVToRGB(h, Mathf.Max(s, 0.55f), Mathf.Max(b, 0.9f));
        }
        protected override string Current { get { var p = Courts.Pickable; int i = Mathf.Clamp(_two ? C.VsCourt : C.Court, 0, p.Length - 1); return p[i].Id; } }
        protected override void Choose(string id) { int i = Mathf.Max(0, Index(id)); if (_two) C.VsCourt = i; else C.Court = i; Courts.CurrentId = id; }
        protected override string Shot(string id) { return "court"; }
        // 2026-11-22 evening: the court behind the preview is rebuilt only once the focus rests on it (HoverRest) - every step
        // of the arrows rebuilt a whole venue, the lag the user saw
        public const float HoverRest = 0.3f;
        string _pending; float _restFor;
        protected override void Hover(string id) { _pending = id; _restFor = 0; }
        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (_pending == null) return;
            _restFor += Time.unscaledDeltaTime;
            if (_restFor >= HoverRest) { if (Courts.CurrentId != _pending) { Courts.CurrentId = _pending; CourtRebuilds++; } _pending = null; }
        }
        /// <summary>The courts built for the preview (tests: one for a quick run along the grid).</summary>
        public static int CourtRebuilds;

        protected override void Describe(string id, VisualElement into)
        {
            int i = Index(id);
            if (i < 0) return;
            var t = Courts.Pickable[i];
            Kit.L(into, Courts.SectionOf(i).ToUpperInvariant(), "pk-kicker");
            Kit.L(into, t.Name, "pk-name");
            var badge = Kit.L(into, t.Walls == WallStyle.Plaster ? "CLUB WALLS" : t.Walls == WallStyle.Glass ? "ALL GLASS" : "GLASS SHOW COURT", "pk-class");
            badge.style.backgroundColor = TileColour(id);
            Kit.L(into, t.Blurb ?? "", "pk-text");
            if (t.Outdoor) Kit.L(into, "Out in the open air", "pk-text");
        }
    }

    /// <summary>Your class (player 2's too in local versus): the five, each its strengths, weakness, perk and bars.</summary>
    public sealed class ClassPickScreen : PickScreen
    {
        public readonly int Player;
        readonly bool _two;
        readonly string _where, _current;
        public ClassPickScreen(int player, bool two) { Player = player; _two = two; }
        /// <summary>On its own (Career, the Gauntlet, online): where it's for, the class now, and what to do with the pick.</summary>
        public ClassPickScreen(string where, string current, Action<string> done) { Player = 0; _where = where; _current = current; Done = done; }
        protected override bool TwoPlayer { get { return _two; } }
        protected override string Kicker
        {
            get { return _where != null ? _where + "  ·  YOUR CLASS" : (_two ? "LOCAL VERSUS  ·  STEP " + (3 + Player) + "  ·  PLAYER " + (Player + 1) : "QUICK MATCH  ·  STEP 4") + "  ·  YOUR CLASS"; }
        }
        protected override string Title { get { return _two ? "Player " + (Player + 1) + ": choose a class" : "Choose your class"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        protected override IEnumerable<string> Ids { get { foreach (var c in PlayerClasses.All) yield return c.Id; } }
        // 2026-11-22 evening: 35 classes - a tab for each family (Q / E), seven or so in each
        static readonly string[] Families = { "power", "accuracy", "speed", "touch", "allround" };
        protected override string[] TabNames { get { return new[] { "ALL", "POWER", "ACCURACY", "SPEED", "TOUCH", "ALL-ROUND" }; } }   // 2026-11-25 ALL
        protected override bool HasAll { get { return true; } }
        protected override int TabOf(string id) { return 1 + System.Math.Max(0, System.Array.IndexOf(Families, PlayerClasses.FamilyOf(id))); }
        protected override int TabOrder(string id) { return System.Array.FindIndex(PlayerClasses.All, c => c.Id == id); }
        protected override string TileName(string id) { return SetupFlow.ClassName(id); }
        protected override string TileKicker(string id) { var d = PlayerClasses.Find(id); return d != null ? d.Short.ToUpperInvariant() : ""; }
        protected override Color TileColour(string id) { return SetupFlow.ClassColour(id); }
        protected override string Current { get { return _where != null ? (PlayerClasses.Find(_current) != null ? _current : PlayerClasses.Default) : SetupFlow.Mine(Player); } }
        protected override void Choose(string id) { if (_where != null) return; if (Player == 0) C.Class = id; else C.VsClass2 = id; }
        protected override string Shot(string id) { return Player == 0 ? "you" : "them"; }

        protected override void Describe(string id, VisualElement into)
        {
            var d = PlayerClasses.Find(id);
            if (d == null) return;
            Color c = SetupFlow.ClassColour(id);
            Kit.L(into, "CLASS", "pk-kicker");
            Kit.L(into, d.Name, "pk-name");
            var badge = Kit.L(into, d.Short.ToUpperInvariant(), "pk-class"); badge.style.backgroundColor = c;
            foreach (var s in d.Strengths) Kit.L(into, "+  " + s, "pk-good");
            Kit.L(into, (d.Weakness == "None" ? "·  No weakness" : "-  " + d.Weakness), d.Weakness == "None" ? "pk-text" : "pk-bad");
            Kit.L(into, "PERK  ·  " + d.Perk.ToUpperInvariant() + ":  " + d.PerkText, "pk-perk");
            var a = PlayerClasses.Attributes(new AthleteAttributes(), id);
            var bars = Kit.Box(into, "pk-bars");
            Bar(bars, "SPEED", a.Speed, c); Bar(bars, "POWER", a.Power, c); Bar(bars, "CONTROL", a.Control, c); Bar(bars, "STAMINA", a.Stamina, c);
        }
    }

    // ---------------- the last step: ready ----------------

    public sealed class QuickMatchReadyScreen : VersusScreen
    {
        protected override bool TwoPlayer { get { return false; } }
        protected override string Kicker { get { return "QUICK MATCH  ·  READY"; } }
        protected override string Title { get { return "Quick Match"; } }
    }

    public sealed class LocalVersusReadyScreen : VersusScreen
    {
        protected override bool TwoPlayer { get { return true; } }
        protected override string Kicker { get { return "LOCAL VERSUS  ·  READY"; } }
        protected override string Title { get { return "Local Versus"; } }
    }
}
