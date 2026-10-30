// ===========================================================
// Assets/SquashGame/Runtime/UI/GearDropScreens.cs
// P4: Gear Drops' screens (the rules: Progression/GearDrops).
//
//   (the banners themselves: StoreScreen, since 2026-10-29)
//                      of it, the pity count, Pull x1 / Pull x10, and the
//                      odds and rules in full - and the PRO SHOP page:
//                      today's 8 for coins, and gold-plating
//   PullRevealScreen   the cards turn over one by one, in their rarity's
//                      colour (gold editions sparkle); NEW or the coins a
//                      duplicate paid; wear the best new piece
//   GoldPlateScreen    an item you own, gold-plated with coins
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class GearUi
    {
        public static string Wallet()
        {
            return "OPEN 1: " + GearDrops.PullCost + "   ·   OPEN 10: " + GearDrops.TenPullCost.ToString("N0");          // (the coins: top right)
        }

        /// <summary>An item for cards: its shape (a shirt, a shoe, a racket...) in its own colour, on a backing that contrasts with it
        /// (2026-10-25 playtest: flat colour blocks didn't read as gear, and dark items vanished on the dark cards).</summary>
        public static VisualElement Swatch(Item it, string cls = "gd-sw")
        {
            var sw = new VisualElement(); sw.AddToClassList(cls); sw.pickingMode = PickingMode.Ignore;
            float lum = 0.2126f * it.A.r + 0.7152f * it.A.g + 0.0722f * it.A.b;
            sw.style.backgroundColor = lum > 0.55f ? new Color(0.13f, 0.14f, 0.17f) : new Color(0.82f, 0.84f, 0.87f);
            var icon = new SlotIcon(it.Slot, new Color(it.A.r, it.A.g, it.A.b, 1f));
            icon.AddToClassList("gd-icon");
            sw.Add(icon);
            if (it.B != it.A) { var b = new VisualElement(); b.AddToClassList("gd-sw-b"); b.style.backgroundColor = it.B; b.pickingMode = PickingMode.Ignore; sw.Add(b); }
            return sw;
        }

        public static string SlotName(Item it) { return Cosmetics.SlotNames[(int)it.Slot]; }
        public static Color RarityColour(Item it) { return it.Gold ? GearDrops.GoldAccent : GearDrops.RarityColours[Mathf.Clamp(it.Rarity, 0, 4)]; }
    }

    public sealed class PullRevealScreen : GameScreen
    {
        readonly List<Drop> _drops;
        readonly string _banner;
        readonly List<VisualElement> _cards = new List<VisualElement>();
        int _shown;
        float _t;
        const float Every = 0.32f;

        public PullRevealScreen(List<Drop> drops, string banner) { _drops = drops; _banner = banner; }
        protected override string Kicker { get { var b = GearDrops.Banner(_banner); return (b != null ? b.Name.ToUpperInvariant() : "GEAR DROPS") + "  ·  " + GearUi.Wallet(); } }
        protected override string Title { get { return _drops.Count == 1 ? "One pull" : "Ten pulls"; } }
        protected override bool ShowChip { get { return false; } }
        public int Revealed { get { return _shown; } }

        protected override void Compose()
        {
            var grid = Kit.Box(Content, _drops.Count == 1 ? "gd-reveal gd-reveal--one" : "gd-reveal");
            foreach (var d in _drops)
            {
                var c = Kit.Box(grid, "gd-card");
                c.AddToClassList("gd-card--hidden");
                c.style.borderTopColor = GearUi.RarityColour(d.Item);
                c.Add(GearUi.Swatch(d.Item, "gd-sw-reveal"));
                var k = Kit.L(c, (d.Gold ? "★ GOLD  ·  " : "") + GearDrops.RarityNames[(int)d.Rarity].ToUpperInvariant(), "gd-card-k");
                k.style.color = GearUi.RarityColour(d.Item);
                Kit.L(c, d.Item.Name, "gd-card-name");
                Kit.L(c, GearUi.SlotName(d.Item), "gd-card-slot");
                var tag = Kit.L(c, d.New ? "NEW" : "+" + d.Coins + " coins", "gd-card-tag");
                if (d.New) tag.AddToClassList("gd-card-tag--new");
                if (d.Rarity >= Rarity.Epic || d.Gold) c.AddToClassList("gd-card--big");
                _cards.Add(c);
            }
            AddTo(Root, new PrimaryItem("Done", () => { RevealAll(); Fe.Pop(); Fe.Replace(StoreScreen.For(_banner)); }));
            var best = Best();
            if (best != null)
            {
                var wear = new ActionItem("Wear " + best.Item.Name, () => { RevealAll(); var p = ProfileStore.Current.Progress; p.Loadout.Set(best.Item.Slot, best.Item.Id); ProfileStore.Save(); Sounds.Play("stamp"); }, PromptBar.Btn.Y);
                wear.Root.AddToClassList("k-action--chip");
                AddTo(TopBar, wear);
            }
        }

        /// <summary>The best new piece (rarest, gold first): the one to try on.</summary>
        Drop Best()
        {
            Drop best = null;
            foreach (var d in _drops)
                if (d.New && d.Item.Slot != Slot.Title && (best == null || (d.Gold ? 10 : 0) + (int)d.Rarity > (best.Gold ? 10 : 0) + (int)best.Rarity)) best = d;
            return best;
        }

        public void RevealAll() { while (_shown < _cards.Count) RevealNext(); }

        void RevealNext()
        {
            if (_shown >= _cards.Count) return;
            var d = _drops[_shown];
            _cards[_shown].RemoveFromClassList("gd-card--hidden");
            Sounds.Play(d.Rarity >= Rarity.Epic || d.Gold ? "star" : "tick");
            _shown++;
        }

        public override void Tick(float dt)
        {
            if (_shown >= _cards.Count) return;
            _t += dt;
            if (_t >= Every) { _t = 0; RevealNext(); }
        }

        public override void OnActionX() { RevealAll(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Done"), (PromptBar.Btn.X, "Show all"), (PromptBar.Btn.Y, "Wear it"), (PromptBar.Btn.B, "Back")); }
        public override void Back() { RevealAll(); Fe.Pop(); Fe.Replace(StoreScreen.For(_banner)); }
    }

    public sealed class GoldPlateScreen : GameScreen
    {
        protected override string Kicker { get { return "PRO SHOP  ·  " + GearUi.Wallet(); } }
        protected override string Title { get { return "Make it gold"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var list = new List<Item>();
            foreach (var b in GearDrops.Banners()) foreach (var it in b.Items) if (GearDrops.PlateCost(it) > 0) list.Add(it);
            list.Sort((a, c) => c.Rarity != a.Rarity ? c.Rarity.CompareTo(a.Rarity) : string.CompareOrdinal(a.Name, c.Name));
            if (list.Count == 0) { Kit.L(Content, "Nothing to make gold yet. Open some mystery packs first!", "k-cr-empty"); return; }
            var row = Kit.Box(Content, "k-row");
            foreach (var it in list.GetRange(0, Mathf.Min(6, list.Count)))
            {
                var item = it;
                var card = AddTo(row, new CardItem(it.Name, GearDrops.RarityNames[it.Rarity].ToUpperInvariant() + "  ·  " + GearUi.SlotName(it).ToUpperInvariant(), () => Plate(item))
                    .Line(() => "Gold edition: " + GearDrops.PlateCost(item).ToString("N0") + " coins").Badge(() => GearDrops.PlateCost(item).ToString("N0")));
                card.Art.Add(GearUi.Swatch(Cosmetics.Find(it.Id + "_gold") ?? it, "gd-sw-card"));
                card.Locked = () => GearDrops.Wallet.Coins < GearDrops.PlateCost(item);
                card.LockLine = () => "not enough coins";
            }
            Kit.L(Content, "Your six rarest pieces that can be plated. A gold edition is the same piece with a gold finish - frames glow.", "k-cr-note");
        }

        void Plate(Item it)
        {
            if (!GearDrops.GoldPlate(it)) { Sounds.Play("back"); return; }
            Sounds.Play("star");
            Fe.Replace(new GoldPlateScreen());
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Make it gold"), (PromptBar.Btn.B, "Back")); }
    }
}
