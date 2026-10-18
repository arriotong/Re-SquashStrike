// ===========================================================
// Assets/SquashGame/Runtime/UI/LockerScreens.cs
// P2-M5: the Locker Room, in the old SquashStrike look.
//
//   LockerRoomScreen     kit, racket, celebration, banner, title, stats,
//                        achievements; the player card on the right
//   KitScreen            shirt .. wristband   } the demo's athlete wears
//   RacketScreen         frame, grip, strings } what you browse, filmed
//                                               up close (MenuCinematics)
//   StatsScreen          lifetime totals
//   AchievementsScreen   the grid: done, progress, locked, hidden
//
// A row cycles through its slot's items with left / right (locked ones
// too - you see them on court, with how to unlock them); Enter wears
// it if you own it. Everything here is cosmetic.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>A slot's row: ← → browses (and previews), Enter wears.</summary>
    public sealed class LockerRow : MenuItem
    {
        public readonly Slot Slot;
        readonly List<Item> _items;
        int _browse;
        readonly Label _value = new Label(), _note = new Label();
        static Progress P { get { return ProfileStore.Current.Progress; } }

        public LockerRow(Slot slot) : base(Cosmetics.SlotNames[(int)slot])
        {
            Slot = slot;
            _items = Cosmetics.InSlot(slot);
            _browse = Mathf.Max(0, _items.FindIndex(i => i.Id == P.Loadout.Get(slot)));
            var box = new VisualElement(); box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center;
            var left = new Label("‹"); left.AddToClassList("fe-item__arrow");
            var right = new Label("›"); right.AddToClassList("fe-item__arrow");
            _value.AddToClassList("fe-item__value");
            _note.AddToClassList("fe-item__note");
            left.RegisterCallback<ClickEvent>(e => { Change(-1); e.StopPropagation(); });
            right.RegisterCallback<ClickEvent>(e => { Change(1); e.StopPropagation(); });
            box.Add(_note); box.Add(left); box.Add(_value); box.Add(right);
            Root.Add(box);
        }

        public Item Browsing { get { return _items[_browse]; } }
        public bool Owned { get { return Rewards.Owns(Browsing.Id); } }
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
            if (!Owned) return;
            P.Loadout.Set(Slot, Browsing.Id);
            ProfileStore.Save();
            Refresh();
            OnBrowse?.Invoke(this);
        }

        /// <summary>Back to what is worn.</summary>
        public void Revert() { _browse = Mathf.Max(0, _items.FindIndex(i => i.Id == P.Loadout.Get(Slot))); Refresh(); }

        public System.Action<LockerRow> OnBrowse;

        public override void Refresh()
        {
            base.Refresh();
            var it = Browsing;
            bool worn = P.Loadout.Get(Slot) == it.Id;
            _value.text = it.Name;
            _note.text = !Owned ? "locked: " + it.HowToUnlock : worn ? "worn" : HasNew ? "NEW" : "Enter: wear";
            _note.EnableInClassList("lk-note--locked", !Owned);
            _note.EnableInClassList("lk-note--new", Owned && !worn && HasNew);
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

        public void Rebuild()
        {
            Clear();
            var p = ProfileStore.Current.Progress;
            var banner = Cosmetics.Find(p.Loadout.Banner) ?? Cosmetics.Find("banner_charcoal");
            style.backgroundColor = banner.A;
            style.borderLeftColor = banner.B;
            var title = Cosmetics.Find(p.Loadout.Title);
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

    public sealed class LockerRoomScreen : MenuScreen
    {
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Your player"; } }
        PlayerCard _card;

        protected override void Build()
        {
            Add(new ButtonItem("Kit", () => Fe.Push(new KitScreen()), true, () => Note(Cosmetics.KitSlots, 0, 7)));
            Add(new ButtonItem("Racket", () => Fe.Push(new RacketScreen()), false, () => Note(Cosmetics.KitSlots, 7, 3)));
            foreach (var slot in new[] { Slot.Celebration, Slot.Banner, Slot.Title })
            {
                var row = Add(new LockerRow(slot));
                row.OnBrowse = r => { if (_card != null) _card.Rebuild(); };
            }
            Add(new ButtonItem("Stats", () => Fe.Push(new StatsScreen())));
            Add(new ButtonItem("Achievements", () => Fe.Push(new AchievementsScreen()), false, AchNote));
            Add(new ButtonItem("Back", () => Fe.Pop()));
            var p = Career.Panel(Root);
            _card = new PlayerCard();
            p.Add(_card);
        }

        static string Note(Slot[] slots, int from, int n)
        {
            var P = ProfileStore.Current.Progress;
            for (int i = from; i < from + n; i++)
                foreach (var it in Cosmetics.InSlot(slots[i])) if (it.Unlock != UnlockKind.Free && P.Owned.Contains(it.Id) && !P.Seen.Contains(it.Id)) return "NEW";
            return "";
        }

        static string AchNote()
        {
            int done = 0; foreach (var a in ProfileStore.Current.Progress.Achievements) if (a.Done) done++;
            return done + " / " + Achievements.All().Count;
        }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = MenuCinematics.LockerScene;
            if (_card != null) _card.Rebuild();
        }

        public override void OnHide()
        {
            foreach (var i in Items) if (i is LockerRow r) r.Revert();
            ProfileStore.Save();
        }
    }

    /// <summary>Kit or racket: rows for the slots; the athlete on court wears what you browse.</summary>
    public abstract class WardrobeScreen : MenuScreen
    {
        protected abstract Slot[] Slots { get; }
        protected abstract int CameraScene { get; }
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        Label _info;

        protected override void Build()
        {
            foreach (var s in Slots) { var row = Add(new LockerRow(s)); row.OnBrowse = Preview; }
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
            var p = Career.Panel(Root);
            _info = Career.L(p, "", "cr-note");
            p.style.top = new Length(70, LengthUnit.Percent);
        }

        void Preview(LockerRow changed)
        {
            var l = ProfileStore.Current.Progress.Loadout.Clone();
            foreach (var i in Items) if (i is LockerRow r) l.Set(r.Slot, r.Browsing.Id);
            KitDirector.Preview = l;
            var b = changed.Browsing;
            _info.text = b.Name + "  ·  " + Cosmetics.SlotNames[(int)b.Slot] + (changed.Owned ? "" : "\nlocked: " + b.HowToUnlock);
        }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = CameraScene;
            foreach (var i in Items) if (i is LockerRow r) { Preview(r); break; }
        }

        public override void OnHide()
        {
            KitDirector.Preview = null;
            foreach (var i in Items) if (i is LockerRow r) r.Revert();
            ProfileStore.Save();
        }
    }

    public sealed class KitScreen : WardrobeScreen
    {
        protected override string Title { get { return "Kit"; } }
        protected override Slot[] Slots { get { return new[] { Slot.Shirt, Slot.Shorts, Slot.Socks, Slot.Shoes, Slot.Skin, Slot.Headband, Slot.Wristband }; } }
        protected override int CameraScene { get { return MenuCinematics.LockerScene; } }
    }

    public sealed class RacketScreen : WardrobeScreen
    {
        protected override string Title { get { return "Racket"; } }
        protected override Slot[] Slots { get { return new[] { Slot.Frame, Slot.Grip, Slot.Strings }; } }
        protected override int CameraScene { get { return MenuCinematics.RacketScene; } }
    }

    // ---------------- stats and achievements ----------------

    public sealed class StatsScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string RootClass { get { return "cr-wide"; } }
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Stats"; } }

        protected override void Build()
        {
            var p = ProfileStore.Current.Progress;
            var s = p.Stats;
            var cols = new VisualElement(); cols.AddToClassList("cr-draw");
            var a = Column(cols, "MATCHES");
            Row(a, "Quick Match", WL(s.Quick)); Row(a, "Career", WL(s.Career)); Row(a, "Local Versus", WL(s.Versus));
            Row(a, "Rallies won", s.RalliesWon + " / " + s.Rallies);
            Row(a, "Longest rally", s.LongestRally + " shots");
            Row(a, "Time on the T", (100 * s.TShare).ToString("0") + "%");
            var b = Column(cols, "SHOTS");
            int n = Mathf.Max(1, s.Shots);
            Row(b, "Drives", Pct(s.Drives, n)); Row(b, "Lobs", Pct(s.Lobs, n)); Row(b, "Drops", Pct(s.Drops, n)); Row(b, "Kills", Pct(s.Kills, n)); Row(b, "Boasts", Pct(s.Boasts, n));
            Row(b, "Top pace", s.TopPaceKmh > 0 ? s.TopPaceKmh.ToString("0") + " km/h" : "-");
            Row(b, "Great hits", s.GreatHits.ToString()); Row(b, "Nicks", s.Nicks.ToString()); Row(b, "Aces", s.Aces.ToString());
            var c = Column(cols, "ERRORS  ·  MODES");
            Row(c, "Winners", s.Winners.ToString()); Row(c, "Errors", s.Errors.ToString());
            Row(c, "Down (tin)", s.Tins.ToString()); Row(c, "Out", s.Outs.ToString()); Row(c, "Not up", s.NotUps.ToString()); Row(c, "Faults", s.Faults.ToString());
            int stars = 0; foreach (var r in ProfileStore.Current.Arcade) stars += r.Stars;
            Row(c, "Arcade stars", stars.ToString());
            Row(c, "Academy lessons", ProfileStore.Current.Academy.Done.Count + " / " + SquashSim.Academy.Lessons.All().Count);
            Row(c, "Career titles", s.CareerTitles + (s.CareerBestRank > 0 ? "   (best #" + s.CareerBestRank + ")" : ""));
            Content.Add(cols);
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }

        static string WL(ModeTotals m) { return m.Won + " won  ·  " + (m.Played - m.Won) + " lost"; }
        static string Pct(int x, int n) { return x + "  (" + (100f * x / n).ToString("0") + "%)"; }
        static VisualElement Column(VisualElement cols, string head)
        {
            var c = new VisualElement(); c.AddToClassList("lk-stats-col");
            Career.L(c, head, "cr-label");
            var box = new VisualElement(); box.AddToClassList("pm-stats"); c.Add(box);
            cols.Add(c);
            return box;
        }
        static void Row(VisualElement box, string name, string value) { Career.Stat(box, name, value); }
    }

    public sealed class AchievementsScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string RootClass { get { return "cr-wide"; } }
        protected override string Kicker { get { return "LOCKER ROOM"; } }
        protected override string Title { get { return "Achievements"; } }

        protected override void Build()
        {
            var grid = new VisualElement(); grid.AddToClassList("lk-grid");
            foreach (var a in Achievements.All())
            {
                var st = Rewards.State(a.Id);
                bool hidden = a.Hidden && !st.Done;
                var tile = new VisualElement(); tile.AddToClassList("lk-ach");
                tile.AddToClassList("lk-ach--" + a.Tier.ToString().ToLowerInvariant());
                if (st.Done) tile.AddToClassList("lk-ach--done");
                Career.L(tile, (st.Done ? "✓  " : "") + (hidden ? "???" : a.Name), "lk-ach-name");
                Career.L(tile, hidden ? "A hidden achievement." : a.Desc + (a.Target > 1 && !st.Done ? "   " + st.Progress + " / " + a.Target : ""), "lk-ach-desc");
                if (a.Reward != null && !hidden) { var r = Cosmetics.Find(a.Reward); if (r != null) Career.L(tile, "→ " + r.Name + " (" + Cosmetics.SlotNames[(int)r.Slot].ToLowerInvariant() + ")", "lk-ach-reward"); }
                grid.Add(tile);
            }
            Content.Add(grid);
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }
    }
}
