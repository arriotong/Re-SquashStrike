// ===========================================================
// Assets/SquashGame/Runtime/UI/LockerScreens.cs
// The Locker Room, Stats and Achievements. UI redesign U5.
//
//   LockerRoomScreen     a character customizer. Tabs KIT / CARD.
//                        KIT: a rail of slots on the left (drawn icons,
//                        NEW dots), your athlete large in the middle on
//                        a slow turntable (its own camera; the racket
//                        close up for racket slots), the slot's swatches
//                        along the bottom, what it is on the right. A
//                        celebration plays on the athlete as you browse.
//                        CARD: the player card large, banner and title.
//   StatsScreen          tabs MATCHES / SHOTS / RALLY ENDS: win-rate
//                        bars, a shot-mix donut, tiles
//   AchievementsScreen   medallions by group (bronze / silver / gold,
//                        progress rings); the focused one's detail
//
// Left / right browses a slot (locked items too - you see them, with
// how to unlock them); Enter wears it if you own it. Cosmetic only.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>A slot on the rail: its icon, its name, what is browsed; ← → browses (and previews), Enter wears.</summary>
    public sealed class LockerRow : MenuItem
    {
        public readonly Slot Slot;
        readonly List<Item> _items;
        int _browse;
        readonly Label _value = new Label(), _dot = new Label();
        static Progress P { get { return ProfileStore.Current.Progress; } }
        public override bool WantsSlant { get { return false; } }
        public override bool TakesHorizontal { get { return true; } }

        public LockerRow(Slot slot) : base(Cosmetics.SlotNames[(int)slot])
        {
            Slot = slot;
            _items = Cosmetics.InSlot(slot);
            _browse = Mathf.Max(0, _items.FindIndex(i => i.Id == P.Loadout.Get(slot)));
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-slot");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("k-slot-name");
            var icon = new SlotIcon(slot); Root.Insert(0, icon);
            var text = new VisualElement(); text.AddToClassList("k-slot-text"); text.pickingMode = PickingMode.Ignore;
            LabelEl.RemoveFromHierarchy(); text.Add(LabelEl);
            _value.AddToClassList("k-slot-value"); text.Add(_value);
            Root.Add(text);
            _dot.text = "NEW"; _dot.AddToClassList("k-slot-new"); Root.Add(_dot);
        }

        public List<Item> Items { get { return _items; } }
        public int BrowseIndex { get { return _browse; } }
        public Item Browsing { get { return _items[_browse]; } }
        public bool Owned { get { return Rewards.Owns(Browsing.Id); } }
        public bool Worn { get { return P.Loadout.Get(Slot) == Browsing.Id; } }
        /// <summary>The slot has unlocked items not looked at yet.</summary>
        public bool HasNew { get { foreach (var i in _items) if (i.Unlock != UnlockKind.Free && P.Owned.Contains(i.Id) && !P.Seen.Contains(i.Id)) return true; return false; } }

        public override void Change(int d)
        {
            _browse = OptionItem.Step(_browse, d, _items.Count);
            if (!P.Seen.Contains(Browsing.Id)) P.Seen.Add(Browsing.Id);
            Refresh();
            OnBrowse?.Invoke(this);
        }

        /// <summary>Wear what is browsed (if owned).</summary>
        public override void Submit()
        {
            if (!Owned) { Sounds.Play("back"); return; }
            P.Loadout.Set(Slot, Browsing.Id);
            ProfileStore.Save();
            Sounds.Play("stamp");
            Refresh();
            OnBrowse?.Invoke(this);
        }

        /// <summary>Back to what is worn.</summary>
        public void Revert() { _browse = Mathf.Max(0, _items.FindIndex(i => i.Id == P.Loadout.Get(Slot))); Refresh(); }

        public System.Action<LockerRow> OnBrowse;

        public override void Refresh()
        {
            base.Refresh();
            _value.text = Browsing.Name + (Owned ? "" : "  ·  locked");
            _value.EnableInClassList("k-slot-value--locked", !Owned);
            _dot.style.display = HasNew ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>A slot's drawn icon: a shirt, shorts, a sock, a shoe, a face, a band, a racket, a grip, strings, a star.</summary>
    public sealed class SlotIcon : VisualElement
    {
        readonly Slot _slot;
        public SlotIcon(Slot slot)
        {
            _slot = slot;
            AddToClassList("k-slot-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect; if (r.width < 4) return;
            var p = mgc.painter2D;
            float w = r.width, h = r.height;
            p.strokeColor = new Color(1, 1, 1, 0.85f); p.fillColor = new Color(1, 1, 1, 0.85f); p.lineWidth = 2.5f;
            Vector2 V(float x, float y) { return new Vector2(x * w, y * h); }
            void Poly(params Vector2[] pts) { p.BeginPath(); p.MoveTo(pts[0]); for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]); p.ClosePath(); p.Fill(); }
            switch (_slot)
            {
                case Slot.Shirt: Poly(V(.3f, .15f), V(.42f, .2f), V(.58f, .2f), V(.7f, .15f), V(.92f, .35f), V(.8f, .47f), V(.72f, .4f), V(.72f, .88f), V(.28f, .88f), V(.28f, .4f), V(.2f, .47f), V(.08f, .35f)); break;
                case Slot.Shorts: Poly(V(.22f, .2f), V(.78f, .2f), V(.86f, .8f), V(.56f, .8f), V(.5f, .45f), V(.44f, .8f), V(.14f, .8f)); break;
                case Slot.Socks: Poly(V(.38f, .1f), V(.62f, .1f), V(.62f, .62f), V(.84f, .74f), V(.8f, .9f), V(.42f, .9f), V(.38f, .78f)); break;
                case Slot.Shoes: Poly(V(.1f, .5f), V(.38f, .5f), V(.5f, .62f), V(.86f, .66f), V(.92f, .82f), V(.1f, .82f)); break;
                case Slot.Skin: p.BeginPath(); p.Arc(V(.5f, .45f), w * 0.3f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); break;
                case Slot.Hair: p.BeginPath(); p.Arc(V(.5f, .55f), w * 0.32f, Angle.Degrees(180), Angle.Degrees(360)); p.ClosePath(); p.Fill(); break;
                case Slot.HairColour: for (int i = 0; i < 3; i++) { p.BeginPath(); p.Arc(V(.28f + i * .22f, .5f), w * 0.1f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); } break;
                case Slot.Headband: p.lineWidth = 6f; p.BeginPath(); p.Arc(V(.5f, .62f), w * 0.34f, Angle.Degrees(200), Angle.Degrees(340)); p.Stroke(); break;
                case Slot.Wristband: p.lineWidth = 7f; p.BeginPath(); p.Arc(V(.5f, .5f), w * 0.26f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke(); break;
                case Slot.Frame: p.lineWidth = 4f; p.BeginPath(); p.Arc(V(.5f, .36f), w * 0.24f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke(); p.BeginPath(); p.MoveTo(V(.5f, .6f)); p.LineTo(V(.5f, .92f)); p.Stroke(); break;
                case Slot.Grip: p.lineWidth = 9f; p.BeginPath(); p.MoveTo(V(.5f, .12f)); p.LineTo(V(.5f, .88f)); p.Stroke(); p.strokeColor = new Color(0, 0, 0, 0.6f); p.lineWidth = 2f; for (int i = 0; i < 5; i++) { p.BeginPath(); p.MoveTo(V(.42f, .2f + i * .14f)); p.LineTo(V(.58f, .28f + i * .14f)); p.Stroke(); } break;
                case Slot.Strings: p.lineWidth = 1.5f; for (int i = 0; i < 5; i++) { float t = .22f + i * .14f; p.BeginPath(); p.MoveTo(V(t, .16f)); p.LineTo(V(t, .84f)); p.Stroke(); p.BeginPath(); p.MoveTo(V(.16f, t)); p.LineTo(V(.84f, t)); p.Stroke(); } break;
                case Slot.Celebration: Poly(V(.5f, .08f), V(.6f, .38f), V(.92f, .38f), V(.66f, .56f), V(.76f, .88f), V(.5f, .68f), V(.24f, .88f), V(.34f, .56f), V(.08f, .38f), V(.4f, .38f)); break;
                case Slot.Banner: Poly(V(.15f, .2f), V(.85f, .2f), V(.85f, .8f), V(.15f, .8f)); break;
                default: p.lineWidth = 3f; for (int i = 0; i < 3; i++) { p.BeginPath(); p.MoveTo(V(.18f, .3f + i * .2f)); p.LineTo(V(i == 2 ? .6f : .82f, .3f + i * .2f)); p.Stroke(); } break;
            }
        }
    }

    /// <summary>The slot's items along the bottom, the browsed one big in the middle: a colour chip (or the name, for poses and titles).</summary>
    public sealed class SwatchStrip : VisualElement
    {
        public const int Window = 9;
        public SwatchStrip() { AddToClassList("k-swatches"); pickingMode = PickingMode.Ignore; }

        public void Show(LockerRow row)
        {
            Clear();
            var items = row.Items;
            int n = items.Count, half = Window / 2;
            int from = n <= Window ? 0 : Mathf.Clamp(row.BrowseIndex - half, 0, n - Window);
            int to = Mathf.Min(n, from + Window);
            Kit.L(this, from > 0 ? "◀" : "", "k-sw-more");
            string worn = ProfileStore.Current.Progress.Loadout.Get(row.Slot);
            for (int i = from; i < to; i++)
            {
                var it = items[i];
                var sw = Kit.Box(this, "k-sw");
                bool text = row.Slot == Slot.Celebration || row.Slot == Slot.Title;
                if (text) { sw.AddToClassList("k-sw--text"); Kit.L(sw, it.Name, "k-sw-name"); }
                else
                {
                    sw.style.backgroundColor = it.None ? new Color(0.12f, 0.13f, 0.16f) : it.A;
                    if (!it.None && (it.B != it.A)) { var b = Kit.Box(sw, "k-sw-b"); b.style.backgroundColor = it.B; }
                    if (it.None) Kit.L(sw, "NONE", "k-sw-name");
                }
                if (i == row.BrowseIndex) sw.AddToClassList("k-sw--on");
                if (!Rewards.Owns(it.Id)) { sw.AddToClassList("k-sw--locked"); Kit.L(sw, it.Unlock == UnlockKind.Level ? "LV " + it.Level : "★", "k-sw-lock"); }
                if (it.Id == worn) Kit.L(sw, "✓", "k-sw-worn");
            }
            Kit.L(this, to < n ? "▶" : "", "k-sw-more");
        }
    }

    /// <summary>The player card: banner, name, title, level and XP, headline stats.</summary>
    public sealed class PlayerCard : VisualElement
    {
        public PlayerCard()
        {
            AddToClassList("lk-card");
            pickingMode = PickingMode.Ignore;
            Rebuild();
        }

        /// <summary>Draw it (with a previewed banner / title, when browsing).</summary>
        public void Rebuild(Loadout show = null)
        {
            Clear();
            var p = ProfileStore.Current.Progress;
            var l = show ?? p.Loadout;
            var banner = Cosmetics.Find(l.Banner) ?? Cosmetics.Find("banner_charcoal");
            style.backgroundColor = banner.A;
            style.borderLeftColor = banner.B;
            var title = Cosmetics.Find(l.Title);
            string name = CareerStore.Current != null ? CareerStore.Current.S.Name : "Player";
            string nat = CareerStore.Current != null ? CareerStore.Current.S.Nat : "";
            int level = Rewards.LevelOf(p.Xp);
            Add(L((title != null ? title.Name : "Rookie").ToUpperInvariant(), "lk-card-title"));
            Add(L(name, "lk-card-name"));
            var row = new VisualElement(); row.AddToClassList("rw-head");
            row.Add(L("LEVEL " + level + (Rewards.Prestige(p.Xp) > 0 ? "  ★" + Rewards.Prestige(p.Xp) : "") + (nat.Length > 0 ? "   ·   " + nat : ""), "rw-level"));
            row.Add(L(p.Xp.ToString("N0") + " XP", "rw-xp"));
            Add(row);
            var bar = new VisualElement(); bar.AddToClassList("rw-bar");
            var fill = new VisualElement(); fill.AddToClassList("rw-fill"); fill.style.width = Length.Percent(100f * Rewards.Into(p.Xp));
            bar.Add(fill); Add(bar);
            var s = p.Stats;
            var stats = new VisualElement(); stats.AddToClassList("lk-card-stats");
            stats.Add(Stat(s.MatchesWon.ToString(), "MATCHES WON"));
            stats.Add(Stat(s.LongestRally.ToString(), "LONGEST RALLY"));
            int done = 0; foreach (var a in p.Achievements) if (a.Done) done++;
            stats.Add(Stat(done + "/" + Achievements.All().Count, "ACHIEVEMENTS"));
            Add(stats);
        }

        static Label L(string text, string cls) { var l = new Label(text); l.AddToClassList(cls); return l; }
        static VisualElement Stat(string value, string name)
        {
            var b = new VisualElement(); b.AddToClassList("lk-card-stat");
            b.Add(L(value, "lk-card-stat-v")); b.Add(L(name, "lk-card-stat-n"));
            return b;
        }
    }

    public sealed class LockerRoomScreen : GameScreen
    {
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Your kit"; } }
        protected override bool ShowChip { get { return false; } }

        sealed class Tab { public SwatchStrip Strip; public VisualElement Info; }
        readonly Dictionary<LockerRow, Tab> _tabOf = new Dictionary<LockerRow, Tab>();
        PlayerCard _card;
        MenuItem _shown;

        protected override void Compose()
        {
            // ---- the kit, in three tabs of a few slots each; your athlete (or racket) live in the middle ----
            Wardrobe("CLOTHES", new[] { Slot.Shirt, Slot.Shorts, Slot.Socks, Slot.Shoes }, "you");
            Wardrobe("LOOK", new[] { Slot.Skin, Slot.Hair, Slot.HairColour, Slot.Headband, Slot.Wristband, Slot.Celebration }, "you");
            Wardrobe("RACKET", new[] { Slot.Frame, Slot.Grip, Slot.Strings }, "racket");

            // ---- CARD ----
            var card = Kit.Box(Page("CARD"), "k-lk");
            var t2 = Build(card, new[] { Slot.Banner, Slot.Title });
            var cstage = Kit.Box(null, "k-lk-stage");
            card.Insert(1, cstage);
            _card = new PlayerCard(); _card.AddToClassList("lk-card--big");
            cstage.Add(_card);
            cstage.Add(t2.Strip);
        }

        void Wardrobe(string name, Slot[] slots, string shot)
        {
            var page = Kit.Box(Page(name), "k-lk");
            var t = Build(page, slots);
            var stage = Kit.Box(null, "k-lk-stage");
            page.Insert(1, stage);
            Vignettes.Attach(Kit.Box(stage, "k-lk-pic"), shot, 720, 900);
            stage.Add(t.Strip);
        }

        /// <summary>A tab: the rail of slots, then (the caller inserts the stage) the info panel; returns its strip and panel.</summary>
        Tab Build(VisualElement page, Slot[] slots)
        {
            var rail = Kit.Box(page, "k-lk-rail");
            var tab = new Tab { Strip = new SwatchStrip() };
            foreach (var s in slots)
            {
                var row = AddTo(rail, new LockerRow(s));
                row.OnBrowse = r => Show(r, true);
                _tabOf[row] = tab;
            }
            tab.Info = Kit.Panel(page, "THE ITEM");
            tab.Info.AddToClassList("k-lk-info");
            return tab;
        }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.Move, "Browse"), (PromptBar.Btn.A, "Wear"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Tabs"));
        }

        void Show(LockerRow r, bool browsed)
        {
            var tab = _tabOf[r];
            tab.Strip.Show(r);
            var it = r.Browsing;
            tab.Info.Clear();
            Kit.L(tab.Info, Cosmetics.SlotNames[(int)r.Slot].ToUpperInvariant(), "k-panel-title");
            Kit.L(tab.Info, it.Name, "k-lk-item");
            var state = Kit.L(tab.Info, r.Worn ? "✓  WORN" : r.Owned ? "OWNED  ·  ENTER TO WEAR" : "LOCKED", "k-lk-state");
            state.AddToClassList(r.Worn ? "k-lk-state--worn" : r.Owned ? "k-lk-state--owned" : "k-lk-state--locked");
            if (!r.Owned) Kit.L(tab.Info, "Unlock: " + it.HowToUnlock, "k-lk-how");
            if (it.Pose != null) Kit.L(tab.Info, "Plays when you win a match.", "k-lk-how");
            int owned = 0; foreach (var x in r.Items) if (Rewards.Owns(x.Id)) owned++;
            Kit.L(tab.Info, owned + " / " + r.Items.Count + " owned in this slot", "k-lk-count");

            // what the athlete (and the card) wear: everything browsed
            var l = ProfileStore.Current.Progress.Loadout.Clone();
            foreach (var i in Items) if (i is LockerRow row) l.Set(row.Slot, row.Browsing.Id);
            KitDirector.Preview = l;
            KitDirector.PreviewPose = r.Slot == Slot.Celebration ? it.Pose : null;
            bool racket = r.Slot == Slot.Frame || r.Slot == Slot.Grip || r.Slot == Slot.Strings;
            MenuCinematics.Scene = racket ? MenuCinematics.RacketScene : MenuCinematics.LockerScene;
            if (_card != null) _card.Rebuild(l);
        }

        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is LockerRow r)) return;
            _shown = r;
            Show(r, false);
        }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = MenuCinematics.LockerScene;
            _shown = null;
            if (_card != null) _card.Rebuild();
        }

        public override void OnHide()
        {
            KitDirector.Preview = null;
            KitDirector.PreviewPose = null;
            foreach (var i in Items) if (i is LockerRow r) r.Revert();
            ProfileStore.Save();
        }
    }

    // ---------------- stats ----------------

    /// <summary>A ring of segments (the shot mix), drawn.</summary>
    public sealed class Donut : VisualElement
    {
        readonly List<(float value, Color colour)> _parts = new List<(float, Color)>();
        public Donut() { AddToClassList("k-donut"); pickingMode = PickingMode.Ignore; generateVisualContent += Paint; }
        public Donut Part(float v, Color c) { _parts.Add((v, c)); MarkDirtyRepaint(); return this; }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect; if (r.width < 4) return;
            var p = mgc.painter2D;
            float total = 0; foreach (var x in _parts) total += x.value;
            var c = r.center; float rad = Mathf.Min(r.width, r.height) * 0.5f - 18f;
            p.lineWidth = 30f;
            if (total <= 0) { p.strokeColor = new Color(1, 1, 1, 0.08f); p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke(); return; }
            float a = -90f;
            foreach (var x in _parts)
            {
                if (x.value <= 0) continue;
                float sweep = 360f * x.value / total;
                p.strokeColor = x.colour;
                p.BeginPath(); p.Arc(c, rad, Angle.Degrees(a + 1f), Angle.Degrees(a + sweep - 1f)); p.Stroke();
                a += sweep;
            }
        }
    }

    public sealed class StatsScreen : GameScreen
    {
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Stats"; } }
        protected override bool ShowChip { get { return false; } }
        static readonly Color[] ShotColours = { new Color32(200, 255, 61, 255), new Color32(80, 170, 255, 255), new Color32(255, 184, 59, 255), new Color32(255, 90, 74, 255), new Color32(190, 120, 255, 255) };

        protected override void Compose()
        {
            var p = ProfileStore.Current.Progress;
            var s = p.Stats;

            // ---- MATCHES ----
            var m = Kit.Box(Page("MATCHES"), "k-st-grid");
            var wr = Kit.Panel(m, "WIN RATE"); wr.AddToClassList("k-st-wide");
            Bar(wr, "Quick Match", s.Quick);
            Bar(wr, "Career", s.Career);
            Bar(wr, "Local Versus", s.Versus);
            if (s.Quick.Played + s.Career.Played + s.Versus.Played == 0) Kit.L(wr, "Play a match and the numbers start here.", "k-cr-empty");
            var tiles = Kit.Box(m, "k-st-tiles");
            Career.Tile(tiles, "MATCHES WON", s.MatchesWon.ToString());
            Career.Tile(tiles, "RALLIES WON", s.Rallies == 0 ? "–" : (100f * s.RalliesWon / s.Rallies).ToString("0") + "%");
            Career.Tile(tiles, "LONGEST RALLY", s.LongestRally.ToString());
            Career.Tile(tiles, "TIME ON THE T", (100 * s.TShare).ToString("0") + "%");

            // ---- SHOTS ----
            var sh = Kit.Box(Page("SHOTS"), "k-st-grid");
            var mix = Kit.Panel(sh, "SHOT MIX"); mix.AddToClassList("k-st-wide");
            var row = Kit.Box(mix, "k-st-mix");
            var donut = new Donut();
            int[] counts = { s.Drives, s.Lobs, s.Drops, s.Kills, s.Boasts };
            string[] names = { "Drives", "Lobs", "Drops", "Kills", "Boasts" };
            for (int i = 0; i < 5; i++) donut.Part(counts[i], ShotColours[i]);
            var dbox = Kit.Box(row, "k-st-donutbox"); dbox.Add(donut);
            Kit.L(dbox, s.Shots.ToString("N0"), "k-st-donut-n"); Kit.L(dbox, "SHOTS", "k-st-donut-k");
            var legend = Kit.Box(row, "k-st-legend");
            int n = Mathf.Max(1, s.Shots);
            for (int i = 0; i < 5; i++)
            {
                var lr = Kit.Box(legend, "k-st-leg");
                Kit.Box(lr, "k-st-leg-dot").style.backgroundColor = ShotColours[i];
                Kit.L(lr, names[i], "k-st-leg-n");
                Kit.L(lr, counts[i].ToString("N0") + "   " + (100f * counts[i] / n).ToString("0") + "%", "k-st-leg-v");
            }
            var t2 = Kit.Box(sh, "k-st-tiles");
            Career.Tile(t2, "TOP PACE", s.TopPaceKmh > 0 ? s.TopPaceKmh.ToString("0") : "–").Add(Kit.L(null, "KM/H", "k-cr-tile-n"));
            Career.Tile(t2, "GREAT HITS", s.GreatHits.ToString());
            Career.Tile(t2, "NICKS", s.Nicks.ToString());
            Career.Tile(t2, "ACES", s.Aces.ToString());

            // ---- RALLY ENDS ----
            var e = Kit.Box(Page("RALLY ENDS"), "k-st-grid");
            var we = Kit.Panel(e, "WINNERS AND ERRORS"); we.AddToClassList("k-st-wide");
            we.Add(new StatBars().Row("WINNERS  ·  ERRORS", s.Winners, s.Errors));
            int max = Mathf.Max(1, Mathf.Max(Mathf.Max(s.Tins, s.Outs), Mathf.Max(s.NotUps, s.Faults)));
            Count(we, "Down (tin)", s.Tins, max); Count(we, "Out", s.Outs, max); Count(we, "Not up", s.NotUps, max); Count(we, "Faults", s.Faults, max);
            // P2-M7: the referee's calls, for you and against you
            we.Add(new StatBars().Row("LETS  ·  YOU / THEM", s.Lets, s.LetsAgainst));
            we.Add(new StatBars().Row("STROKES  ·  YOU / THEM", s.Strokes, s.StrokesAgainst));
            we.Add(new StatBars().Row("NO LETS  ·  YOU / THEM", s.NoLets, s.NoLetsAgainst));
            var t3 = Kit.Box(e, "k-st-tiles");
            int stars = 0; foreach (var r in ProfileStore.Current.Arcade) stars += r.Stars;
            Career.Tile(t3, "ARCADE STARS", stars + " / 54");
            Career.Tile(t3, "ACADEMY", ProfileStore.Current.Academy.Done.Count + " / " + SquashSim.Academy.Lessons.All().Count);
            Career.Tile(t3, "CAREER TITLES", s.CareerTitles.ToString());
            Career.Tile(t3, "BEST RANK", s.CareerBestRank > 0 ? "#" + s.CareerBestRank : "–");
        }

        static void Bar(VisualElement panel, string name, ModeTotals t)
        {
            var row = Kit.Box(panel, "k-st-bar");
            Kit.L(row, name, "k-st-bar-n");
            var track = Kit.Box(row, "k-st-track");
            float pct = t.Played == 0 ? 0 : (float)t.Won / t.Played;
            Kit.Box(track, "k-st-fill").style.width = Length.Percent(100f * pct);
            Kit.L(row, t.Played == 0 ? "–" : (100f * pct).ToString("0") + "%", "k-st-bar-pct");
            Kit.L(row, t.Won + "-" + (t.Played - t.Won), "k-st-bar-v");
        }

        static void Count(VisualElement panel, string name, int v, int max)
        {
            var row = Kit.Box(panel, "k-st-bar");
            Kit.L(row, name, "k-st-bar-n");
            var track = Kit.Box(row, "k-st-track");
            var f = Kit.Box(track, "k-st-fill"); f.AddToClassList("k-st-fill--err");
            f.style.width = Length.Percent(100f * v / max);
            Kit.L(row, v.ToString(), "k-st-bar-v");
        }
    }

    // ---------------- achievements ----------------

    /// <summary>An achievement as a medallion: its tier's colour, a progress ring, a tick once done.</summary>
    public sealed class MedalItem : MenuItem
    {
        public readonly Achievement A;
        public override bool WantsSlant { get { return false; } }
        public static readonly Color[] TierColours = { new Color32(205, 127, 50, 255), new Color32(192, 200, 210, 255), new Color32(255, 207, 58, 255) };

        public MedalItem(Achievement a) : base(a.Id)
        {
            A = a;
            var st = Rewards.State(a.Id);
            bool hidden = a.Hidden && !st.Done;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-medal");
            LabelEl.style.display = DisplayStyle.None;
            var disc = Kit.Box(Root, "k-medal-disc");
            var col = TierColours[(int)a.Tier];
            disc.style.borderTopColor = disc.style.borderBottomColor = disc.style.borderLeftColor = disc.style.borderRightColor = col;
            if (st.Done) { disc.AddToClassList("k-medal-disc--done"); disc.style.backgroundColor = col; }
            else if (a.Target > 1 && st.Progress > 0) { var ring = new Ring { Fill = col, Width = 5f }; ring.Value = (float)st.Progress / a.Target; disc.Add(ring); }
            Kit.L(disc, st.Done ? "✓" : hidden ? "?" : a.Tier == AchTier.Gold ? "III" : a.Tier == AchTier.Silver ? "II" : "I", "k-medal-mark");
            Kit.L(Root, hidden ? "???" : a.Name, "k-medal-name");
        }
    }

    public sealed class AchievementsScreen : GameScreen
    {
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Achievements"; } }
        protected override bool ShowChip { get { return false; } }
        static readonly string[] Groups = { "Firsts", "Rallies", "Matches", "Shots", "Target Arcade", "Career", "Ranking", "Hidden" };
        static readonly string[] RankingIds = { "top20", "top10", "no1", "rival", "streak10" };
        /// <summary>The tab an achievement is on (the career's are split: events, then ranking).</summary>
        static string TabOf(Achievement a) { return a.Group == "Career" && System.Array.IndexOf(RankingIds, a.Id) >= 0 ? "Ranking" : a.Group; }
        readonly List<VisualElement> _details = new List<VisualElement>();
        MenuItem _shown;

        protected override void Compose()
        {
            int done = 0; var tiers = new int[3];
            foreach (var a in Achievements.All()) if (Rewards.State(a.Id).Done) { done++; tiers[(int)a.Tier]++; }
            var head = Kit.Box(Content, "k-ach-head");
            Kit.L(head, done + " / " + Achievements.All().Count, "k-ach-total");
            for (int t = 0; t < 3; t++)
            {
                var c = Kit.Box(head, "k-ach-tiercount");
                Kit.Box(c, "k-ach-tierdot").style.backgroundColor = MedalItem.TierColours[t];
                Kit.L(c, tiers[t].ToString(), "k-ach-tiern");
            }
            foreach (var g in Groups)
            {
                var page = Kit.Box(Page(g == "Target Arcade" ? "ARCADE" : g.ToUpperInvariant()), "k-ach-page");
                var grid = Kit.Box(page, "k-ach-grid");
                foreach (var a in Achievements.All()) if (TabOf(a) == g) AddTo(grid, new MedalItem(a));
                var d = Kit.Panel(page, "THE ACHIEVEMENT"); d.AddToClassList("k-ach-detail");
                _details.Add(d);
            }
        }

        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is MedalItem m)) return;
            _shown = m;
            var d = _details[Mathf.Clamp(PageIndex, 0, _details.Count - 1)];
            var a = m.A;
            var st = Rewards.State(a.Id);
            bool hidden = a.Hidden && !st.Done;
            d.Clear();
            Kit.L(d, a.Tier.ToString().ToUpperInvariant() + "  ·  " + a.Group.ToUpperInvariant() + "  ·  +" + a.Xp + " XP", "k-panel-title");
            Kit.L(d, hidden ? "???" : a.Name, "k-ac-title");
            var state = Kit.L(d, st.Done ? "✓  DONE" : "NOT YET", "k-ac-status");
            if (st.Done) state.AddToClassList("k-ac-status--done");
            Kit.L(d, hidden ? "A hidden achievement. Keep playing." : a.Desc, "k-ac-blurb");
            if (a.Target > 1)
            {
                Kit.L(d, Mathf.Min(st.Progress, a.Target) + " / " + a.Target, "k-ta-next-k");
                var bar = Kit.Box(d, "k-ta-bar");
                Kit.Box(bar, "k-ta-bar-fill").style.width = Length.Percent(100f * Mathf.Clamp01((float)st.Progress / a.Target));
            }
            if (a.Reward != null && !hidden)
            {
                var r = Cosmetics.Find(a.Reward);
                if (r != null)
                {
                    var rw = Kit.Box(d, "k-ach-reward");
                    var chip = Kit.Box(rw, "k-sw"); chip.style.backgroundColor = r.A;
                    var txt = Kit.Box(rw, "k-ach-reward-t");
                    Kit.L(txt, "UNLOCKS", "k-ac-keys-k");
                    Kit.L(txt, r.Name + "  ·  " + Cosmetics.SlotNames[(int)r.Slot], "k-ach-reward-n");
                }
            }
        }

        protected override void OnPage(int i) { _shown = null; }
    }
}
