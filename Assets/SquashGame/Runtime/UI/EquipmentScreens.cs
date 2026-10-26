// ===========================================================
// Assets/SquashGame/Runtime/UI/EquipmentScreens.cs
// P7 phase 5 (2026-10-27): the career's Equipment - a racket, shoes and
// a kit, bought with career money (SquashSim.Career.Gear). A tab a slot,
// a card an item: what it gives and what it costs you, its price (or
// OWNED / ON). The panel shows your athlete now and with the item in
// focus, stat by stat (Stability is the gear's own). A: buy (it asks
// first) or put on.
//
// Not the Locker's Pro Shop (cosmetics for coins): this gear plays.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class EquipmentScreen : GameScreen
    {
        static readonly GearSlot[] Slots = { GearSlot.Racket, GearSlot.Shoes, GearSlot.Kit };
        static readonly string[] TabNames = { "RACKET", "SHOES", "KIT" };
        static readonly string[] Stats = { "Speed", "Power", "Accuracy", "Stamina", "Stability" };

        readonly int _startTab;
        public EquipmentScreen(int tab = 0) { _startTab = tab; }

        protected override string Kicker { get { return "CAREER  ·  EQUIPMENT  ·  " + Career.Money(Career.S.Cash); } }
        protected override string Title { get { return "Equipment"; } }

        public readonly List<(CardItem card, GearItem item)> Cards = new List<(CardItem, GearItem)>();
        readonly List<(VisualElement panel, List<(Label val, VisualElement fill, VisualElement ghost)> rows, Label note)> _panels = new List<(VisualElement, List<(Label, VisualElement, VisualElement)>, Label)>();
        MenuItem _shown;
        bool _tabbed;

        protected override void Compose()
        {
            var c = Career.C;
            for (int t = 0; t < Slots.Length; t++)
            {
                var slot = Slots[t];
                var page = Kit.Box(Page(TabNames[t]), "k-eq");
                var row = Kit.Box(page, "k-eq-cards");
                foreach (var g in Gear.In(slot))
                {
                    var item = g;
                    var look = Cosmetics.Find(g.Look);
                    var card = AddTo(row, new CardItem(g.Name, g.Price == 0 ? "STARTER" : Career.Money(g.Price), () => Pick(item))
                        .Drawn("split", new Color32(20, 23, 30, 255), look != null ? look.A : Color.grey)
                        .Line(g.Line + "\n" + g.Blurb)
                        .Badge(() => c.Equipped(item.Slot) == item ? "ON" : c.Owns(item.Id) ? "OWNED" : Career.Money(item.Price), c.Equipped(item.Slot) == item));
                    card.Root.AddToClassList("k-eq-card");
                    card.Locked = () => !c.Owns(item.Id) && c.S.Cash < item.Price;
                    card.LockLine = () => "you have " + Career.Money(c.S.Cash);
                    Cards.Add((card, item));
                }
                var panel = Kit.Panel(page, "YOUR ATHLETE");
                panel.AddToClassList("k-eq-stats");
                var rows = new List<(Label, VisualElement, VisualElement)>();
                foreach (var st in Stats)
                {
                    var r0 = Kit.Box(panel, "k-cr-attr-row");
                    Kit.L(r0, st.ToUpperInvariant(), "k-cr-attr-name");
                    var bar = Kit.Box(r0, "k-cr-attr-bar");
                    var ghost = Kit.Box(bar, "k-eq-ghost");
                    var fill = Kit.Box(bar, "k-cr-attr-fill");
                    var val = Kit.L(r0, "", "k-cr-attr-val");
                    rows.Add((val, fill, ghost));
                }
                var note = Kit.L(panel, "", "k-cr-note");
                Kit.L(panel, "Your levels and your gear. Gear moves a stat by 2 at most, and never past 0 or 10. Stability is the gear's own: steadier when stretched, fewer mishits.", "k-cr-note");
                _panels.Add((panel, rows, note));
            }
        }

        static double Get(AthleteAttributes a, string stat)
        {
            switch (stat) { case "Speed": return a.Speed; case "Power": return a.Power; case "Accuracy": return a.Control; case "Stamina": return a.Stamina; default: return a.Stability; }
        }

        /// <summary>Your stats now and with this item on (instead of what you wear in its slot).</summary>
        void ShowStats(GearItem item)
        {
            var c = Career.C;
            var now = c.Effective;
            var worn = new List<GearItem>();
            foreach (var g in c.Wearing()) worn.Add(g.Slot == item.Slot ? item : g);
            var with = Gear.Apply(c.S.Attributes, worn);
            var (_, rows, note) = _panels[System.Array.IndexOf(Slots, item.Slot)];
            for (int i = 0; i < Stats.Length; i++)
            {
                double a = Get(now, Stats[i]), b = Get(with, Stats[i]);
                var (val, fill, ghost) = rows[i];
                fill.style.width = Length.Percent((float)(Mathf.Min((float)a, (float)b) * 10));
                ghost.style.width = Length.Percent((float)(Mathf.Max((float)a, (float)b) * 10));
                ghost.EnableInClassList("k-eq-ghost--down", b < a);
                val.text = a == b ? a.ToString("0") : a.ToString("0") + "  →  " + b.ToString("0");
                val.EnableInClassList("k-eq-up", b > a); val.EnableInClassList("k-eq-down", b < a);
            }
            note.text = c.Equipped(item.Slot) == item ? "On now: " + item.Name + "." : (c.Owns(item.Id) ? "Yours: A puts it on." : c.CantBuy(item.Id) == null ? "A: buy it for " + Career.Money(item.Price) + "." : "Not enough cash yet: " + Career.Money(item.Price) + ".");
        }

        void Pick(GearItem item)
        {
            var c = Career.C;
            int tab = System.Array.IndexOf(Slots, item.Slot);
            if (c.Equipped(item.Slot) == item) return;
            if (c.Owns(item.Id)) { c.Equip(item.Id); CareerStore.Save(); Sounds.Play("select"); Fe.Replace(new EquipmentScreen(tab)); return; }
            if (c.CantBuy(item.Id) != null) { Sounds.Play("back"); return; }
            Fe.Push(new ConfirmScreen("Buy the " + item.Name + " for " + Career.Money(item.Price) + "? You'd have " + Career.Money(c.S.Cash - item.Price) + " left.", () =>
            {
                if (c.Buy(item.Id)) { CareerStore.Save(); Sounds.Play("stamp"); }
                Fe.Replace(new EquipmentScreen(tab));
            }));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Buy / put on"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Racket · shoes · kit")); }

        public override void OnShow()
        {
            base.OnShow();
            if (!_tabbed && _startTab > 0 && Tabs != null) { _tabbed = true; Tabs.Select(_startTab); }
            _shown = null;
        }

        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is CardItem fc)) return;
            _shown = Focused;
            foreach (var (card, item) in Cards) if (card == fc) { ShowStats(item); break; }
        }
    }
}
