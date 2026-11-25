// ===========================================================
// Assets/SquashGame/Runtime/UI/StoreScreen.cs
// The store (2026-10-29, the user: "make three main gacha systems, card
// pack, cosmetics, and proshop, we are changing pro shop into gacha now,
// and make the gacha page look like this like the photo, it's too dull
// now") - a bright banner per gacha, after the anime gachas' supply pages:
//
//   CARD PACKS   the Gauntlet's cards (GauntletCards): a card a pull
//   COSMETICS    Gear Drops' six themed banners (chips switch them)
//   PRO SHOP     the kit store's gear, as a gacha (GearDrops.ProShop) -
//                2026-11-24 ("put the proshop thing ... specifically in the
//                career mode"): career's Shop opens it, alone; the store has
//                the new machines instead (Machines): PRO CARDS, SPECIALS,
//                WALK-ON, PERKS
//
// Each: a gradient banner, the big title, what is guaranteed and when
// (top right), the featured prizes large in the middle, and two big
// yellow buttons - 1x and 10x, with the coin and the price. The odds and
// rules are one button away. Coins only, never real money.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Progression;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.UIElements;
using GearRarity = SquashGame.Progression.Rarity;

namespace SquashGame.UI
{
    /// <summary>A gacha's pull button: what (1x / 10x), the coin and the price.</summary>
    public sealed class PullButton : MenuItem
    {
        readonly System.Action _run;
        public override bool WantsSlant { get { return false; } }

        public PullButton(string label, int price, System.Action run, string note = null) : base(label)
        {
            _run = run;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("gx-pull");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("gx-pull-label");
            var price_ = new CoinCount(() => price, 24f);
            price_.AddToClassList("gx-pull-price");
            Root.Add(price_);
            if (note != null) { var n = Kit.L(Root, note, "gx-pull-note"); n.pickingMode = PickingMode.Ignore; }
        }

        public override void Submit() { if (IsEnabled) _run(); else Sounds.Play("back"); }
    }

    public sealed class StoreScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return Career ? MenuWorld.Career : MenuWorld.Store; } }   // 2026-11-21 its toon world (Dioramas)
        public const string CardsTab = "CARD PACKS", CosmeticsTab = "CLOTHES", ShopTab = "PRO SHOP";
        public const string ProCardsTab = "PRO CARDS", SpecialsTab = "SPECIALS", WalkOnTab = "WALK-ON", PerksTab = "PERKS";
        /// <summary>The store's machines, tab by tab (the Pro Shop is career's: alone, on its own page).</summary>
        public static readonly string[] StoreTabs = { CardsTab, CosmeticsTab, ProCardsTab, SpecialsTab, WalkOnTab, PerksTab };
        static readonly (string tab, string banner)[] MachineTabs =
            { (ProCardsTab, Machines.ProCardsId), (SpecialsTab, Machines.SpecialsId), (WalkOnTab, Machines.WalkOnId), (PerksTab, Machines.PerksId) };
        /// <summary>The career's Pro Shop (2026-11-24): the Pro Shop alone.</summary>
        public bool Career { get { return _tab == ShopTab; } }

        readonly string _tab, _banner;
        /// <summary>Open on a tab (and, for COSMETICS, a banner).</summary>
        public StoreScreen(string tab = CardsTab, string banner = null) { _tab = tab; _banner = banner; }
        /// <summary>The store on a banner (the reveal goes back to it): the Pro Shop's, else a cosmetics one.</summary>
        public static StoreScreen For(string banner)
        {
            if (banner == GearDrops.ProShopId) return new StoreScreen(ShopTab);
            foreach (var m in MachineTabs) if (m.banner == banner) return new StoreScreen(m.tab);
            return new StoreScreen(CosmeticsTab, banner);
        }

        protected override string Kicker { get { return Career ? "CAREER  ·  SHOP" : "SHOP & KIT  ·  MYSTERY PACKS"; } }
        protected override string Title { get { return Career ? "Pro Shop" : "Store"; } }
        protected override bool ShowChip { get { return false; } }
        protected override bool ShowCoins { get { return true; } }
        /// <summary>The cosmetics banner on show.</summary>
        public string Banner { get; private set; }
        /// <summary>Each tab's two pull buttons (tests).</summary>
        public readonly Dictionary<string, PullButton[]> Pulls = new Dictionary<string, PullButton[]>();

        protected override void Compose()
        {
            Banner = _banner ?? GearDrops.Banners()[0].Id;
            if (Career) { Shop(Content); return; }
            Cards(Page(CardsTab));
            Cosmetics_(Page(CosmeticsTab));
            ProCards(Page(ProCardsTab));
            Machine(Page(SpecialsTab), SpecialsTab, Machines.SpecialsId, "SPECIAL", new Color32(255, 70, 40, 255), new Color32(255, 214, 60, 255),
                    new ActionItem("Wear it", () => Fe.Push(new LockerRoomScreen(LockerRoomScreen.ShowtimeTab)), "▶"));
            Machine(Page(WalkOnTab), WalkOnTab, Machines.WalkOnId, "ENTRANCE", new Color32(40, 60, 190, 255), new Color32(255, 110, 180, 255),
                    new ActionItem("Wear it", () => Fe.Push(new LockerRoomScreen(LockerRoomScreen.ShowtimeTab)), "▶"));
            Perks(Page(PerksTab));
            int start = System.Array.IndexOf(StoreTabs, _tab);
            if (start > 0) Root.schedule.Execute(() => Tabs.Select(start));
        }

        // ---------------- the three ----------------

        void Cards(VisualElement page)
        {
            var b = BannerBox(page, "Card Packs", "CARDS", new Color32(92, 64, 236, 255), new Color32(255, 102, 170, 255));
            // 2026-10-30 (the kid test): no "pulls", "×10" or "evolve" - what you get, in plain words
            Info(b, "OPEN 10: AT LEAST ONE SUPER CARD", "Get a card again and it gets stronger");
            var feat = Kit.Box(b, "gx-featured");
            var picks = GauntletCards.All().FindAll(a => a.Rarity == SquashSim.Modes.Rarity.Epic && a.Kind == AbilityKind.Ultimate);
            if (picks.Count < 3) picks = GauntletCards.All().FindAll(a => a.Rarity >= SquashSim.Modes.Rarity.Epic);
            for (int i = 0; i < picks.Count && i < 3; i++)
            {
                var a = picks[i];
                var t = Tile(feat, i, a.Name, GauntletAbilities.RarityNames[(int)a.Rarity].ToUpperInvariant(), GauntletFlow.RarityColour(a.Rarity));
                t.Insert(0, new CardArt(GauntletFlow.Art(a), new Color32(18, 20, 26, 255), GauntletFlow.RarityColour(a.Rarity), a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : (Color?)null));
            }
            var c = GauntletFlow.Cards;
            int owned = 0; foreach (var a in GauntletCards.All()) if (GauntletCards.Find(c, a.Id) != null) owned++;
            Footer(b, "Power cards for the Gauntlet.", "You have " + owned + " of " + GauntletCards.All().Count,
                   new ActionItem("Your cards", () => Fe.Push(new DeckScreen()), "▶"), CardOdds);
            PullsFor(b, CardsTab, GauntletCards.CardCost, GauntletCards.TenCost, n =>
            {
                var got = GauntletFlow.BuyCards(n);
                if (got.Count == 0) { Sounds.Play("back"); return; }
                Sounds.Play("stamp");
                Fe.Push(new PackScreen(got));
            }, n => GearDrops.Wallet.Coins >= GauntletCards.Cost(n), "A super one for sure!");
        }

        void Cosmetics_(VisualElement page)
        {
            var bn = GearDrops.Banner(Banner) ?? GearDrops.Banners()[0];
            var (from, to) = ColoursOf(bn.Id);
            var b = BannerBox(page, bn.Name, "GEAR", from, to);
            var chips = Kit.Box(b, "gx-chips");
            foreach (var other in GearDrops.Banners())
            {
                var id = other.Id;
                var chip = AddTo(chips, new ActionItem(other.Name, () => { if (id != Banner) Fe.Replace(new StoreScreen(CosmeticsTab, id)); }));
                chip.Root.AddToClassList("gx-chip");
                if (id == bn.Id) chip.Root.AddToClassList("gx-chip--on");
            }
            GearInfo(b, bn);
            GearFeatured(b, bn, new List<Item>(bn.Of(GearRarity.Legendary)) { bn.Of(GearRarity.Epic)[0] });
            int owned = GearDrops.OwnedIn(bn, out int gold);
            Footer(b, bn.Blurb, "You have " + owned + " of " + bn.Items.Count + (gold > 0 ? "   ·   ★ " + gold + " gold" : ""),
                   new ActionItem("Make it gold", () => Fe.Push(new GoldPlateScreen()), "▶"), GearOdds);
            GearPulls(b, CosmeticsTab, bn.Id);
        }

        void Shop(VisualElement page)
        {
            var bn = GearDrops.ProShop();
            var b = BannerBox(page, "Pro Shop", "SHOP", new Color32(255, 150, 40, 255), new Color32(255, 214, 92, 255));
            GearInfo(b, bn);
            var top = new List<Item>();
            for (int r = 4; r >= 0 && top.Count < 3; r--) foreach (var it in bn.Of((GearRarity)r)) if (top.Count < 3) top.Add(it);
            GearFeatured(b, bn, top);
            int owned = GearDrops.OwnedIn(bn, out _);
            Footer(b, bn.Blurb, "You have " + owned + " of " + bn.Items.Count, null, GearOdds);
            GearPulls(b, ShopTab, bn.Id);
        }

        // ---------------- 2026-11-24 the new machines ----------------

        void ProCards(VisualElement page)
        {
            var bn = Machines.Find(Machines.ProCardsId);
            var b = BannerBox(page, bn.Name, "CARDS", new Color32(20, 120, 200, 255), new Color32(120, 240, 255, 255));
            Info(b, "OPEN 10: AT LEAST ONE SUPER CARD", "Get a card again: Silver at " + Machines.TierAt[1] + ", Gold at " + Machines.TierAt[2] + ", Holo at " + Machines.TierAt[3]);
            var top = new List<Item>(bn.Of(GearRarity.Legendary));
            GearFeatured(b, bn, top.GetRange(0, Mathf.Min(3, top.Count)));
            int owned = GearDrops.OwnedIn(bn, out _);
            Footer(b, bn.Blurb, "You have " + owned + " of " + bn.Items.Count, new ActionItem("Your cards", () => Fe.Push(new ProCardsScreen()), "▶"), o => MachineOdds(o, Machines.ProCardsId));
            GearPulls(b, ProCardsTab, bn.Id);
        }

        void Perks(VisualElement page)
        {
            var bn = Machines.Find(Machines.PerksId);
            var b = BannerBox(page, bn.Name, "TRAIN", new Color32(30, 150, 80, 255), new Color32(200, 255, 61, 255));
            Info(b, "WEAR " + Machines.MaxPerks + " PERKS AT ONCE", "Quick Match, career and the Gauntlet  ·  get one again to level it up");
            var top = new List<Item>(bn.Of(GearRarity.Legendary)); top.Add(bn.Of(GearRarity.Epic)[0]);
            GearFeatured(b, bn, top);
            int owned = GearDrops.OwnedIn(bn, out _);
            Footer(b, bn.Blurb, "You have " + owned + " of " + bn.Items.Count + "   ·   now: " + Machines.Describe(Machines.Total()),
                   new ActionItem("Your perks", () => Fe.Push(new PerksScreen()), "▶"), o => MachineOdds(o, Machines.PerksId));
            GearPulls(b, PerksTab, bn.Id);
        }

        void Machine(VisualElement page, string tab, string id, string watermark, Color from, Color to, ActionItem extra)
        {
            var bn = Machines.Find(id);
            var b = BannerBox(page, bn.Name, watermark, from, to);
            GearInfo(b, bn);
            var top = new List<Item>();
            for (int r = 4; r >= 0 && top.Count < 3; r--) foreach (var it in bn.Of((GearRarity)r)) if (top.Count < 3) top.Add(it);
            GearFeatured(b, bn, top);
            int owned = GearDrops.OwnedIn(bn, out _);
            Footer(b, bn.Blurb, "You have " + owned + " of " + bn.Items.Count, extra, o => MachineOdds(o, id));
            GearPulls(b, tab, bn.Id);
        }

        static VisualElement MachineOdds(VisualElement o, string id)
        {
            for (int r = 4; r >= 0; r--)
            {
                var line = Kit.Box(o, "gd-odd");
                var dot = Kit.Box(line, "gd-odd-dot"); dot.style.backgroundColor = GearDrops.RarityColours[r];
                Kit.L(line, GearDrops.RarityNames[r], "gd-odd-name");
                Kit.L(line, (GearDrops.Odds[r] * 100).ToString("0.#") + "%", "gd-odd-pct");
            }
            var rules = new List<string>
            {
                "A pull: " + GearDrops.PullCost + " coins; ten for " + GearDrops.TenPullCost.ToString("N0") + " (one free), an Epic or better among them.",
                "After " + GearDrops.Pity + " pulls here without a Legendary, the next one is.",
            };
            if (id == Machines.ProCardsId) rules.Add("A card again: Silver at " + Machines.TierAt[1] + " copies, Gold at " + Machines.TierAt[2] + ", Holo at " + Machines.TierAt[3] + ". Past Holo: coins.");
            else if (id == Machines.PerksId)
            {
                rules.Add("A perk again: level 2 at " + Machines.PerkLevelAt[1] + " copies, level 3 at " + Machines.PerkLevelAt[2] + " (x1.5, x2). Past level 3: coins.");
                rules.Add("Wear " + Machines.MaxPerks + " at once. A stat goes up " + Machines.Percent(Machines.MaxPerStat) + " at most, all perks together.");
                rules.Add("Perks count in Quick Match, career and the Gauntlet - never in Local Versus or online.");
            }
            else rules.Add("Duplicates pay coins: " + string.Join(" / ", System.Array.ConvertAll(GearDrops.DupeCoins, c => c.ToString())) + " (Common to Legendary). Looks only.");
            rules.Add("Coins come from playing every mode. Never real money.");
            foreach (var rule in rules) Kit.L(o, "·  " + rule, "gd-rule");
            return o;
        }

        // ---------------- the parts ----------------

        static (Color, Color) ColoursOf(string banner)
        {
            switch (banner)
            {
                case "club": return (new Color32(30, 140, 90, 255), new Color32(230, 196, 90, 255));
                case "rooftop": return (new Color32(110, 60, 200, 255), new Color32(255, 120, 100, 255));
                case "station": return (new Color32(40, 90, 170, 255), new Color32(220, 170, 90, 255));
                case "desert": return (new Color32(240, 120, 50, 255), new Color32(150, 60, 170, 255));
                case "arena": return (new Color32(20, 170, 220, 255), new Color32(230, 60, 200, 255));
                default: return (new Color32(40, 110, 230, 255), new Color32(255, 200, 70, 255));
            }
        }

        VisualElement BannerBox(VisualElement page, string title, string watermark, Color from, Color to)
        {
            var b = Kit.Box(page, "gx-banner");
            b.style.backgroundImage = new StyleBackground(Gradient(from, to));
            var w = Kit.L(b, watermark, "gx-watermark"); w.pickingMode = PickingMode.Ignore;
            Kit.L(b, title, "gx-title");
            return b;
        }

        static void Info(VisualElement b, string big, string small)
        {
            var box = Kit.Box(b, "gx-info");
            Kit.L(box, big, "gx-info-big");
            Kit.L(box, small, "gx-info-small");
        }

        static void GearInfo(VisualElement b, GearBanner bn)
        {
            int left = GearDrops.Pity - GearDrops.PityOf(bn.Id);
            Info(b, "THE BEST PRIZE IN " + left + (left == 1 ? " OPEN" : " OPENS") + " OR LESS", "Open 10: at least one super prize  ·  Got it already? Coins back");
        }

        static VisualElement Tile(VisualElement row, int i, string name, string rarity, Color colour)
        {
            var t = Kit.Box(row, "gx-feat");
            if (i == 1) t.AddToClassList("gx-feat--mid");
            t.style.borderTopColor = colour; t.style.borderBottomColor = colour; t.style.borderLeftColor = colour; t.style.borderRightColor = colour;
            var tag = Kit.Box(t, "gx-feat-tag");
            var r = Kit.L(tag, "★  " + rarity, "gx-feat-rarity"); r.style.color = colour;
            Kit.L(tag, name, "gx-feat-name");
            return t;
        }

        static void GearFeatured(VisualElement b, GearBanner bn, List<Item> items)
        {
            var feat = Kit.Box(b, "gx-featured");
            for (int i = 0; i < items.Count && i < 3; i++)
            {
                var it = items[i];
                int r = Mathf.Clamp(bn.RarityOf(it), 0, 4);
                var t = Tile(feat, i, GearUi.NameOf(it), GearDrops.RarityNames[r].ToUpperInvariant() + "  ·  " + GearUi.SlotName(it).ToUpperInvariant(), GearDrops.RarityColours[r]);
                t.Insert(0, GearUi.Swatch(it, "gx-feat-art"));
            }
        }

        void Footer(VisualElement b, string blurb, string owned, ActionItem extra, System.Func<VisualElement, VisualElement> odds)
        {
            var f = Kit.Box(b, "gx-foot");
            Kit.L(f, blurb, "gx-blurb");
            Kit.L(f, owned, "gx-owned");
            var acts = Kit.Box(f, "gx-acts");
            var o = AddTo(acts, new ActionItem("What's inside?", () => Fe.Push(new StoreOddsScreen(odds)), "i"));
            o.Root.AddToClassList("gx-small");
            if (extra != null) { AddTo(acts, extra); extra.Root.AddToClassList("gx-small"); }
        }

        void PullsFor(VisualElement b, string tab, int one, int ten, System.Action<int> pull, System.Func<int, bool> can, string tenNote)
        {
            var row = Kit.Box(b, "gx-pulls");
            var p1 = AddTo(row, new PullButton("OPEN 1", one, () => pull(1)));
            var p10 = AddTo(row, new PullButton("OPEN 10", ten, () => pull(10), tenNote));
            p1.Enabled = () => can(1); p10.Enabled = () => can(10);
            Pulls[tab] = new[] { p1, p10 };
        }

        void GearPulls(VisualElement b, string tab, string banner)
        {
            PullsFor(b, tab, GearDrops.PullCost, GearDrops.TenPullCost, n =>
            {
                var drops = GearDrops.Pull(banner, n == 10 ? GearDrops.TenPull : 1);
                if (drops.Count == 0) { Sounds.Play("back"); return; }
                Fe.Push(new PullRevealScreen(drops, banner));
            }, n => GearDrops.CanPull(n == 10 ? GearDrops.TenPull : 1), "A super one for sure!");
        }

        // ---------------- the odds ----------------

        static VisualElement CardOdds(VisualElement o)
        {
            int total = 0; foreach (var w in GauntletCards.PackWeight) total += w;
            for (int r = GauntletCards.PackWeight.Length - 1; r >= 0; r--)
            {
                if (!GauntletCards.All().Exists(a => (int)a.Rarity == r)) continue;
                var line = Kit.Box(o, "gd-odd");
                var dot = Kit.Box(line, "gd-odd-dot"); dot.style.backgroundColor = GauntletFlow.RarityColour((SquashSim.Modes.Rarity)r);
                Kit.L(line, GauntletAbilities.RarityNames[r], "gd-odd-name");
                Kit.L(line, (100f * GauntletCards.PackWeight[r] / total).ToString("0.#") + "%", "gd-odd-pct");
            }
            foreach (var rule in new[]
            {
                "A card a pull: " + GauntletCards.CardCost + " coins; ten for " + GauntletCards.TenCost.ToString("N0") + " (one free), an Epic or better among them.",
                "A copy of a card you have levels it: " + GauntletCards.CopiesFor[2] + " copies level 2, " + GauntletCards.CopiesFor[3] + " level 3. At level 3, evolve it for coins.",
                "Coins come from playing every mode. Never real money.",
            })
                Kit.L(o, "·  " + rule, "gd-rule");
            return o;
        }

        static VisualElement GearOdds(VisualElement o)
        {
            for (int r = 4; r >= 0; r--)
            {
                var line = Kit.Box(o, "gd-odd");
                var dot = Kit.Box(line, "gd-odd-dot"); dot.style.backgroundColor = GearDrops.RarityColours[r];
                Kit.L(line, GearDrops.RarityNames[r], "gd-odd-name");
                Kit.L(line, (GearDrops.Odds[r] * 100).ToString("0.#") + "%", "gd-odd-pct");
            }
            foreach (var rule in new[]
            {
                "A pull: " + GearDrops.PullCost + " coins; ten for " + GearDrops.TenPullCost.ToString("N0") + " (one free), an Epic or better among them.",
                "After " + GearDrops.Pity + " pulls on a banner without a Legendary, the next one is.",
                "Duplicates pay coins: " + string.Join(" / ", System.Array.ConvertAll(GearDrops.DupeCoins, c => c.ToString())) + " (Common to Legendary).",
                "A cosmetics item can come as its gold edition (" + (GearDrops.GoldChance * 100).ToString("0") + "%), never twice.",
                "Coins come from playing every mode. Never real money.",
            })
                Kit.L(o, "·  " + rule, "gd-rule");
            return o;
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.Tabs, "Pages"), (PromptBar.Btn.B, "Back")); }

        // ---------------- the banner's gradient ----------------

        static readonly Dictionary<long, Texture2D> _grads = new Dictionary<long, Texture2D>();

        /// <summary>A diagonal gradient, top left to bottom right, softly lighter in the middle.</summary>
        static Texture2D Gradient(Color a, Color b)
        {
            long key = ((long)ColorUtility.ToHtmlStringRGB(a).GetHashCode() << 32) ^ (uint)ColorUtility.ToHtmlStringRGB(b).GetHashCode();
            if (_grads.TryGetValue(key, out var t) && t != null) return t;
            const int n = 96;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + (n - 1 - y)) / (2f * (n - 1));
                    var c = Color.Lerp(a, b, Mathf.SmoothStep(0f, 1f, u));
                    float glow = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    c = Color.Lerp(c, Color.white, 0.18f * glow);
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            _grads[key] = t;
            return t;
        }
    }

    /// <summary>A gacha's odds and rules, over the store.</summary>
    public sealed class StoreOddsScreen : DialogScreen
    {
        readonly System.Func<VisualElement, VisualElement> _fill;
        public StoreOddsScreen(System.Func<VisualElement, VisualElement> fill) { _fill = fill; }
        protected override string Title { get { return "What's inside?"; } }
        protected override string YesLabel { get { return "OK"; } }
        protected override string NoLabel { get { return "Back"; } }
        protected override void OnYes() { Fe.Pop(); }
        protected override void Build()
        {
            var o = Kit.Box(Content, "gx-odds");
            _fill(o);
            base.Build();
        }
    }
}
