// ===========================================================
// Assets/SquashGame/Runtime/UI/CardScreens.cs
// The Gauntlet's card decks (2026-10-28; SquashSim.Modes.GauntletCards).
//
//   DeckScreen   every card, yours first: confirm puts one in the deck or
//                takes it out (up to 20), Y fills it with your best, X
//                evolves a level-3 card for coins; the panel says what the
//                card does, its copies to the next level, its evolve price
//   PackScreen   the five cards a pack gave - new, or a copy toward the
//                next level; open another from here
// ===========================================================

using System.Collections.Generic;
using SquashGame.Progression;
using SquashSim.Modes;
using Rarity = SquashSim.Modes.Rarity;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class DeckScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  " + GearDrops.Wallet.Coins.ToString("N0") + " COINS"; } }
        protected override string Title { get { return "Your deck  ·  " + GauntletFlow.Cards.Deck.Count + " / " + GauntletCards.DeckSize; } }
        protected override bool ShowChip { get { return false; } }

        readonly string _focus;
        readonly List<(MenuItem item, Ability card)> _rows = new List<(MenuItem, Ability)>();
        ScrollView _scroll;
        VisualElement _detail;
        MenuItem _shown;

        public DeckScreen(string focus = null) { _focus = focus; }

        static CardCollection C { get { return GauntletFlow.Cards; } }

        protected override void Compose()
        {
            var wrap = Kit.Box(Content, "k-tro-wrap");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("k-tro-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            wrap.Add(_scroll);
            _detail = Kit.Panel(wrap, "THE CARD");
            _detail.AddToClassList("k-tro-detail");

            // yours (the strongest first), then the ones still to find
            var cards = GauntletCards.All();
            cards.Sort((x, y) =>
            {
                var rx = GauntletCards.Find(C, x.Id); var ry = GauntletCards.Find(C, y.Id);
                int sx = rx == null ? -1 : GauntletCards.DeckLevel(rx) * 10 + (int)x.Rarity, sy = ry == null ? -1 : GauntletCards.DeckLevel(ry) * 10 + (int)y.Rarity;
                return sx != sy ? sy.CompareTo(sx) : string.CompareOrdinal(x.Name, y.Name);
            });
            foreach (var a in cards)
            {
                var card = a;
                var item = AddTo(_scroll, new ActionItem(Line(a), () => Toggle(card), C.Deck.Contains(a.Id) ? "■" : "□"));
                item.Enabled = () => GauntletCards.Find(C, card.Id) != null;
                _rows.Add((item, a));
            }
        }

        static string Line(Ability a)
        {
            var r = GauntletCards.Find(C, a.Id);
            string state = r == null ? "not yet yours" : r.Evolved ? "EVOLVED" : "LV " + GauntletCards.LevelFor(r.Copies);
            return a.Name + "   ·   " + GauntletAbilities.RarityNames[(int)a.Rarity] + "   ·   " + state;
        }

        void Toggle(Ability a)
        {
            if (GauntletCards.Toggle(C, a.Id)) { Profile.ProfileStore.Save(); Sounds.Play("tick"); Fe.Replace(new DeckScreen(a.Id)); }
            else Sounds.Play("back");                                      // the deck is full
        }

        public override void OnShow()
        {
            base.OnShow();
            if (_focus != null) foreach (var r in _rows) if (r.card.Id == _focus) { Focus(r.item); break; }
        }

        /// <summary>Y: the deck is your best 20.</summary>
        public override void OnActionY()
        {
            GauntletCards.AutoDeck(C);
            Profile.ProfileStore.Save();
            Sounds.Play("stamp");
            Fe.Replace(new DeckScreen(Focused != null ? Card(Focused)?.Id : null));
        }

        /// <summary>X: evolve the card in focus (level 3, for coins).</summary>
        public override void OnActionX()
        {
            var a = Focused != null ? Card(Focused) : null;
            if (a != null && GauntletFlow.Evolve(a.Id)) { Sounds.Play("stamp"); Fe.Replace(new DeckScreen(a.Id)); }
            else Sounds.Play("back");
        }

        Ability Card(MenuItem it) { foreach (var r in _rows) if (r.item == it) return r.card; return null; }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.A, "In / out"), (PromptBar.Btn.Y, "Best " + GauntletCards.DeckSize), (PromptBar.Btn.X, "Evolve"), (PromptBar.Btn.B, "Back"));
        }

        public override void Tick(float dt)
        {
            if (Focused == null || Focused == _shown) return;
            _shown = Focused;
            _scroll.ScrollTo(_shown.Root);
            Show(Card(_shown));
        }

        void Show(Ability a)
        {
            _detail.Clear();
            if (a == null) return;
            var r = GauntletCards.Find(C, a.Id);
            var t = Kit.L(_detail, (a.Family != Family.None ? a.FamilyName.ToUpperInvariant() + "  ·  " : "") + a.KindName.ToUpperInvariant() + "  ·  " + GauntletAbilities.RarityNames[(int)a.Rarity].ToUpperInvariant(), "k-panel-title");
            t.style.color = GauntletFlow.RarityColour(a.Rarity);
            Kit.L(_detail, a.Name, "k-tro-big");
            Kit.L(_detail, a.Blurb, "gt-text");
            int dl = r != null ? GauntletCards.DeckLevel(r) : 1;
            Kit.L(_detail, (r == null ? "At level 1: " : r.Evolved ? "Evolved: " : "Now: ") + a.Level(GauntletCards.PlayLevel(a, dl)), "gt-text");
            if (r == null) { Kit.L(_detail, "Find it in a pack (" + GauntletCards.PackCost + " coins, on the Gauntlet screen).", "k-tro-line"); return; }
            if (!r.Evolved)
            {
                int lv = GauntletCards.LevelFor(r.Copies);
                if (lv < GauntletCards.MaxLevel) Kit.L(_detail, "Copies " + r.Copies + "  ·  " + GauntletCards.ToNext(r.Copies) + " more for level " + (lv + 1), "k-tro-line");
                else
                {
                    int cost = GauntletCards.EvolveCostOf(a.Id);
                    Kit.L(_detail, "Level 3  ·  evolve for " + cost.ToString("N0") + " coins (X)" + (GearDrops.Wallet.Coins < cost ? "  ·  you have " + GearDrops.Wallet.Coins.ToString("N0") : ""), "k-tro-line");
                    Kit.L(_detail, "Evolved: " + a.Level(a.MaxLevel), "gt-text");
                }
            }
            Kit.L(_detail, C.Deck.Contains(a.Id) ? "In your deck" : "Not in your deck", "k-tro-line");
        }
    }

    public sealed class PackScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  A PACK  ·  " + GearDrops.Wallet.Coins.ToString("N0") + " COINS LEFT"; } }
        protected override string Title { get { return "Your cards"; } }
        protected override bool ShowChip { get { return false; } }
        readonly List<string> _got;

        public PackScreen(List<string> got) { _got = got; }
        /// <summary>The cards shown (tests).</summary>
        public int Count { get { return _got.Count; } }

        protected override void Compose()
        {
            var row = Kit.Box(Content, "k-row");
            // each card's copies before the pack, then counted up through it in order (two of one card: new, then a copy)
            var copiesNow = new Dictionary<string, int>();
            foreach (var id in _got)
                if (!copiesNow.ContainsKey(id)) { var rec = GauntletCards.Find(GauntletFlow.Cards, id); copiesNow[id] = (rec != null ? rec.Copies : 0) - _got.FindAll(x => x == id).Count; }
            foreach (var id in _got)
            {
                var a = GauntletAbilities.Find(id);
                if (a == null) continue;
                int copies = ++copiesNow[id];
                bool fresh = copies == 1;
                int lv = GauntletCards.LevelFor(copies);
                string kicker = fresh ? "NEW CARD" : lv > GauntletCards.LevelFor(copies - 1) ? "LEVEL UP  ·  LV " + lv : "COPY  ·  " + (lv >= GauntletCards.MaxLevel ? "LV 3" : GauntletCards.ToNext(copies) + " TO LV " + (lv + 1));
                var card = AddTo(row, new CardItem(a.Name, kicker, () => { })
                    .Drawn(GauntletFlow.Art(a), new Color32(18, 20, 26, 255), GauntletFlow.RarityColour(a.Rarity), a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : (Color?)null)
                    .Line(a.Blurb)
                    .Badge(() => GauntletAbilities.RarityNames[(int)a.Rarity].ToUpperInvariant(), a.Rarity >= Rarity.Epic));
                card.Root.AddToClassList("gt-card");
            }
            var again = AddTo(Root, new PrimaryItem("Open another  ·  " + GauntletCards.PackCost, () =>
            {
                var got = GauntletFlow.BuyPack();
                if (got.Count > 0) { Sounds.Play("stamp"); Fe.Replace(new PackScreen(got)); } else Sounds.Play("back");
            }));
            again.Enabled = () => GearDrops.Wallet.Coins >= GauntletCards.PackCost;
            var more = Kit.Box(Content, "gt-more");
            AddTo(more, new ActionItem("Your deck", () => Fe.Replace(new DeckScreen()), "▶"));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }
}
