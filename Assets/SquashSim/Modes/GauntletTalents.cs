// ===========================================================
// Assets/SquashSim/Modes/GauntletTalents.cs
// P5 the Gauntlet roguelite: talents - permanent upgrades, bought with
// the coins runs earn (kept between runs, even a run that ends badly).
//
//   hearty     +1 starting heart a level (2)
//   reroll     +1 reroll a run a level (3)
//   wider      4 cards at every pick (1)
//   banish     +1 banish a run a level: strike a card from the run (3)
//   kit        start each run with a random common ability a level (2)
//   revive     once a run, the last heart lost gives one back (1)
//   treasure   +10% coins a level (3)
//   unlock_X   puts a locked ability into the pool (one each)
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Modes
{
    public sealed class Talent
    {
        public string Id, Name, Text;
        public int[] Costs;                 // per level
        public int MaxLevel { get { return Costs.Length; } }
        /// <summary>For an unlock: the ability it adds to the pool.</summary>
        public string Unlocks;
    }

    public static class GauntletTalents
    {
        static List<Talent> _all;

        public static List<Talent> All()
        {
            if (_all != null) return _all;
            _all = new List<Talent>();
            void T(string id, string name, string text, params int[] costs) { _all.Add(new Talent { Id = id, Name = name, Text = text, Costs = costs }); }
            T("hearty", "Hearty", "+1 starting heart", 60, 160);
            T("reroll", "Reroll", "+1 reroll each run", 40, 100, 180);
            T("wider", "Wider View", "4 cards at every pick", 260);
            T("banish", "Banish", "+1 banish each run: strike a card out of the run", 60, 130, 220);
            T("kit", "Kit Bag", "Start each run with a random common ability", 80, 200);
            T("revive", "Revive", "Once a run, losing your last heart gives one back", 320);
            T("treasure", "Treasure", "+10% coins", 50, 110, 200);
            foreach (var a in GauntletAbilities.All())
                if (a.Locked && !a.Retired) _all.Add(new Talent { Id = "unlock_" + a.Id, Name = a.Name, Text = "Adds " + a.Name + " to the cards: " + a.Blurb, Costs = new[] { UnlockCost(a) }, Unlocks = a.Id });
            return _all;
        }

        static int UnlockCost(Ability a) { return a.Rarity == Rarity.Epic ? 220 : a.Rarity == Rarity.Rare ? 150 : 100; }

        public static Talent Find(string id) { foreach (var t in All()) if (t.Id == id) return t; return null; }

        public static int Level(List<RunMod> owned, string id)
        {
            if (owned != null) foreach (var t in owned) if (t != null && t.Id == id) return t.Level;
            return 0;
        }

        /// <summary>The next level's price (-1: maxed).</summary>
        public static int NextCost(List<RunMod> owned, string id)
        {
            var t = Find(id);
            if (t == null) return -1;
            int l = Level(owned, id);
            return l >= t.MaxLevel ? -1 : t.Costs[l];
        }

        /// <summary>Buy the next level with these coins (false: maxed, or not enough).</summary>
        public static bool Buy(List<RunMod> owned, ref int coins, string id)
        {
            int cost = NextCost(owned, id);
            if (cost < 0 || coins < cost) return false;
            coins -= cost;
            foreach (var t in owned) if (t.Id == id) { t.Level++; return true; }
            owned.Add(new RunMod { Id = id, Level = 1 });
            return true;
        }

        /// <summary>Is this ability in the pool (not locked, or unlocked - by a talent or a boss)?</summary>
        public static bool Unlocked(List<RunMod> owned, Ability a) { return !a.Locked || Level(owned, "unlock_" + a.Id) > 0; }
    }
}
