// ===========================================================
// Assets/SquashGame/Runtime/UI/MachineScreens.cs
// 2026-11-24 the new machines' own screens:
//
//   ProCardsScreen     your pro cards - every opponent, PROS / CLUB tabs;
//                      the ones you have in their tier's colour, the rest
//                      grey; the panel: who they are, the tier, copies to go
//   PerksScreen        the training perks: A wears one or takes it off
//                      (three at most); the panel: what it gives at its
//                      level, the next level, and all you wear together
//   CareerShopScreen   career's SHOP (2026-11-24: "put the proshop thing ...
//                      specifically in the career mode"): Equipment (gear
//                      that plays, for prize money) and the Pro Shop
// ===========================================================

using System.Collections.Generic;
using SquashGame.Progression;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class ProCardsScreen : PickScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Store; } }
        protected override bool TwoPlayer { get { return false; } }
        protected override bool Browse { get { return true; } }
        protected override bool ShowCoins { get { return true; } }
        protected override string Kicker { get { var b = Bn; int n = GearDrops.OwnedIn(b, out _); return "STORE  ·  PRO CARDS  ·  " + n + " OF " + b.Items.Count; } }
        protected override string Title { get { return "Your pro cards"; } }

        static GearBanner Bn { get { return Machines.Find(Machines.ProCardsId); } }
        static Item It(string id) { return Cosmetics.Find(id); }

        protected override IEnumerable<string> Ids { get { foreach (var it in Bn.Items) yield return it.Id; } }
        protected override string[] TabNames { get { return new[] { "PROS", "CLUB" }; } }
        protected override int TabOf(string id) { var it = It(id); return it != null && it.Ref != null && it.Ref.StartsWith("star:") ? 0 : 1; }
        protected override int TabOrder(string id) { var it = It(id); var s = SquashSim.Career.Stars.Find(it.Ref); return s != null ? s.Rank : -SquashSim.Career.Stars.Describe(it.Ref).rating; }
        protected override string TileName(string id) { return GearUi.NameOf(It(id)); }
        protected override string TileKicker(string id) { var it = It(id); var s = SquashSim.Career.Stars.Find(it.Ref); return (s != null ? "No. " + s.Rank : "CLUB") + "  ·  " + SquashSim.Career.Stars.Describe(it.Ref).rating; }
        protected override Color TileColour(string id) { int t = Machines.TierOf(id); return t < 0 ? new Color(0.35f, 0.37f, 0.4f) : Machines.TierColours[t]; }
        protected override string TileBadge(string id) { int t = Machines.TierOf(id); return t < 0 ? "NOT YET" : (id == Machines.YourCard ? "YOURS  ·  " : "") + Machines.TierNames[t].ToUpperInvariant(); }
        protected override string Current { get { if (Machines.YourCard != null) return Machines.YourCard; foreach (var it in Bn.Items) if (Machines.TierOf(it.Id) >= 0) return it.Id; return Bn.Items[0].Id; } }
        /// <summary>2026-11-25 A: make it your card (on your tag and walk-on, your coach); again: no card.</summary>
        protected override void Choose(string id)
        {
            if (Machines.TierOf(id) < 0) { Sounds.Play("back"); return; }
            Machines.SetYourCard(id);
            Sounds.Play(Machines.YourCard == id ? "stamp" : "tick");
        }
        protected override string Shot(string id) { return null; }

        protected override void Describe(string id, VisualElement into)
        {
            var it = It(id);
            var d = SquashSim.Career.Stars.Describe(it.Ref);
            int tier = Machines.TierOf(id);
            Kit.L(into, GearDrops.RarityNames[Mathf.Clamp(it.Rarity, 0, 4)].ToUpperInvariant() + " CARD", "pk-kicker");
            Kit.L(into, d.name, "pk-name");
            var badge = Kit.L(into, tier < 0 ? "NOT IN YOUR COLLECTION" : Machines.TierNames[tier].ToUpperInvariant(), "pk-class");
            badge.style.backgroundColor = tier < 0 ? new Color(0.25f, 0.27f, 0.3f) : Machines.TierColours[tier];
            var s = SquashSim.Career.Stars.Find(it.Ref);
            Kit.L(into, (s != null ? "World No. " + s.Rank + "  ·  " : "Club player  ·  ") + "rated " + d.rating + "  ·  " + Roster.StyleName(d.style), "pk-text");
            Kit.L(into, "Class: " + SetupFlow.ClassName(SetupFlow.OpponentClass(it.Ref)), "pk-text");
            if (tier < 0) Kit.L(into, "Find this card in the PRO CARDS machine." + (s != null && s.Rank <= Machines.LockedTop ? " Until then they're locked in Quick Match." : ""), "pk-perk");
            else
            {
                int next = Machines.ToNext(it);
                Kit.L(into, Machines.CopiesOf(id) + (Machines.CopiesOf(id) == 1 ? " copy" : " copies") + (next > 0 ? "  ·  " + next + " more to " + Machines.TierNames[tier + 1] : "  ·  the top tier"), "pk-good");
            }
            // 2026-11-25 as your coach: its class's stat, by tier
            var coach = Machines.CoachGives(tier >= 0 ? id : null);
            int st = Machines.CoachStat(it);
            string what = st >= 0 ? Machines.StatNames[st] : "";
            Kit.L(into, (id == Machines.YourCard ? "YOUR CARD  ·  COACHING YOU: " : tier >= 0 ? "A: MAKE IT YOUR CARD  ·  COACHES: " : "AS YOUR CARD IT WOULD COACH: ") + what.ToUpperInvariant() + " +" +
                        Machines.Percent(tier >= 0 ? coach[st] : Machines.CoachAt[0]) + (tier < 0 ? " (more at higher tiers)" : ""), "pk-perk");
            // the sets
            var sets = Kit.Box(into, "pk-bars");
            foreach (var set in Machines.Sets)
                Kit.L(sets, (Machines.SetDone(set) ? "✓  " : "·  ") + set.Name + "   " + Machines.SetHave(set) + " / " + Machines.SetSize(set) + "   ·   " + set.Coins.ToString("N0") + " coins + the title " + Cosmetics.Find(set.Title).Name, Machines.SetDone(set) ? "pk-good" : "pk-text");
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Look"), (PromptBar.Btn.A, "Your card"), (PromptBar.Btn.Tabs, "Pros / club"), (PromptBar.Btn.B, "Back")); }
    }

    public sealed class PerksScreen : PickScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Store; } }
        protected override bool TwoPlayer { get { return false; } }
        protected override bool Browse { get { return true; } }
        protected override string Kicker { get { return "YOUR PERKS  ·  WEAR " + Machines.MaxPerks + " AT ONCE"; } }
        protected override string Title { get { return "Training perks"; } }

        static GearBanner Bn { get { return Machines.Find(Machines.PerksId); } }
        static Item It(string id) { return Cosmetics.Find(id); }

        protected override IEnumerable<string> Ids { get { foreach (var it in Bn.Items) yield return it.Id; } }
        protected override string TileName(string id) { return It(id).Name; }
        protected override string TileKicker(string id)
        {
            var it = It(id);                                                   // the stats it raises (the panel has the numbers)
            return it.Stat2 >= 0 ? Machines.StatNames[it.Stat].ToUpperInvariant() + " + " + Machines.StatNames[it.Stat2].ToUpperInvariant() : Machines.StatNames[it.Stat].ToUpperInvariant();
        }
        protected override Color TileColour(string id) { return GearDrops.Owns(It(id)) ? It(id).A : new Color(0.35f, 0.37f, 0.4f); }
        protected override string TileBadge(string id)
        {
            if (!GearDrops.Owns(It(id))) return "NOT YET";
            return (Machines.Equipped.Contains(id) ? "ON  ·  " : "") + "LV " + Machines.LevelOf(id);
        }
        protected override string Current { get { var l = Machines.Equipped; return l.Count > 0 ? l[0] : Bn.Items[0].Id; } }
        /// <summary>The last wear / take-off was refused (three on already, or not owned) - tests.</summary>
        public bool Refused { get; private set; }
        protected override void Choose(string id)
        {
            Refused = !Machines.Toggle(id);
            Sounds.Play(Refused ? "back" : Machines.Equipped.Contains(id) ? "stamp" : "tick");
        }
        protected override string Shot(string id) { return null; }

        static string Gave(Item it, int level)
        {
            var g = Machines.Gives(it, level);
            var parts = new List<string>();
            for (int k = 0; k < 4; k++) if (g[k] > 0.001f) parts.Add(Machines.StatNames[k].ToUpperInvariant() + " +" + Machines.Percent(g[k]));
            return string.Join("  ·  ", parts);
        }

        protected override void Describe(string id, VisualElement into)
        {
            var it = It(id);
            bool own = GearDrops.Owns(it);
            int lv = Machines.LevelOf(id);
            Kit.L(into, GearDrops.RarityNames[Mathf.Clamp(it.Rarity, 0, 4)].ToUpperInvariant() + " PERK", "pk-kicker");
            Kit.L(into, it.Name, "pk-name");
            var badge = Kit.L(into, !own ? "NOT YET" : Machines.Equipped.Contains(id) ? "WEARING  ·  LEVEL " + lv : "LEVEL " + lv, "pk-class");
            badge.style.backgroundColor = own ? it.A : new Color(0.25f, 0.27f, 0.3f);
            Kit.L(into, "+  " + Gave(it, Mathf.Max(1, lv)), "pk-good");
            if (!own) Kit.L(into, "Find it in the PERKS machine.", "pk-perk");
            else if (lv < Machines.PerkLevelAt.Length) Kit.L(into, "Level " + (lv + 1) + ":  " + Gave(it, lv + 1) + "   ·   " + Machines.ToNext(it) + " more to go", "pk-text");
            else Kit.L(into, "The top level.", "pk-text");
            Kit.L(into, "ALL YOU WEAR  ·  " + Machines.Describe(Machines.Total()), "pk-perk");
            Kit.L(into, "Counts in Quick Match, career and the Gauntlet - not in Local Versus or online. A stat goes up " + Machines.Percent(Machines.MaxPerStat) + " at most.", "pk-text");
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Wear / take off"), (PromptBar.Btn.B, "Back")); }
    }

    public sealed class CareerShopScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Career; } }
        protected override bool ShowCoins { get { return true; } }
        protected override string Kicker { get { return "CAREER  ·  SHOP"; } }
        protected override string Title { get { return "Shop"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var row = Kit.Box(Content, "k-row");
            AddTo(row, new CardItem("EQUIPMENT", "PRIZE MONEY", () => Fe.Push(new EquipmentScreen()), CardSize.Hero)
                .Drawn("chevrons", new Color32(20, 40, 60, 255), new Color32(120, 200, 255, 255))
                .Line("Rackets, shoes and kit that change how you play"));
            AddTo(row, new CardItem("PRO SHOP", "MYSTERY PACK  ·  COINS", () => Fe.Push(new StoreScreen(StoreScreen.ShopTab)))
                .Drawn("chevrons", new Color32(60, 36, 10, 255), new Color32(255, 184, 59, 255))
                .Line("Club kit, rackets and the gear the pros wear"));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }
}
