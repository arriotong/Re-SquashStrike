// ===========================================================
// Assets/SquashSim/Modes/GauntletCards.cs
// The Gauntlet's card decks (2026-10-28, the user: "the chaos mode now
// becomes card decks, where you need to get from a gacha machine with the
// universal currency, each deck can have 20 cards, all with unique special
// abilities, moves, passive, buffs, etc. winning a floor lets you activate
// one in 3 that is randomly drawn from your deck, so all of them are not
// activated at first, and all of the cards have 3 levels ... then once you
// get max level you can evolve them to make them even better").
//
//   cards      the Gauntlet's abilities - shots, powers, stats, perks,
//              ultimates (not the one-off bonuses, nor the in-run
//              evolutions two maxed cards make)
//   packs      PackSize cards for PackCost coins, by rarity (PackWeight);
//              a duplicate is a copy of the card
//   levels     1 copy level 1, CopiesFor[2] level 2, CopiesFor[3] level 3;
//              at level 3, EVOLVE for coins: it plays at its ability's top
//              level
//   the deck   up to DeckSize of your cards (Auto: the best of them)
//   a run      nothing is active at first; a floor won draws Gauntlet.Cards
//              of your deck's cards that are not yet active - activate one
//              (at its level; a boss's pick a level higher)
//   start      the commons and StarterRares rares, at level 1
//
// Pure and deterministic (packs from a seed and the pack's number), so
// the sim stays UnityEngine-free; the game keeps the collection in the
// profile and pays for packs and evolutions in coins.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    [Serializable]
    public sealed class CardRec
    {
        public string Id;
        public int Copies;
        public bool Evolved;
    }

    [Serializable]
    public sealed class CardCollection
    {
        public List<CardRec> Cards = new List<CardRec>();
        public List<string> Deck = new List<string>();
        public uint Seed;
        public int Packs;
    }

    public static class GauntletCards
    {
        public const int DeckSize = 20, PackSize = 5, PackCost = 600, MaxLevel = 3, StarterRares = 5;
        /// <summary>Copies for level 1, 2, 3.</summary>
        public static readonly int[] CopiesFor = { 0, 1, 3, 6 };
        /// <summary>A pack's odds by rarity (Common, Rare, Epic, Legendary).</summary>
        public static readonly int[] PackWeight = { 60, 28, 10, 3 };
        /// <summary>Coins to evolve a level-3 card, by rarity.</summary>
        public static readonly int[] EvolveCost = { 500, 1000, 2000, 4000 };

        public static bool IsCard(Ability a) { return a != null && !a.Retired && a.Kind != AbilityKind.Instant && a.Kind != AbilityKind.Evolved; }

        public static List<Ability> All() { return GauntletAbilities.All().FindAll(IsCard); }

        public static int LevelFor(int copies)
        {
            for (int l = MaxLevel; l >= 1; l--) if (copies >= CopiesFor[l]) return l;
            return 0;
        }

        /// <summary>Copies still to find for the next level (0 at level 3).</summary>
        public static int ToNext(int copies) { int l = LevelFor(copies); return l >= MaxLevel ? 0 : CopiesFor[l + 1] - copies; }

        /// <summary>The level a card is in a run's deck: 1-3, or MaxLevel + 1 evolved.</summary>
        public static int DeckLevel(CardRec c) { return c.Evolved ? MaxLevel + 1 : LevelFor(c.Copies); }

        /// <summary>The ability level a card plays at (evolved: its ability's top level).</summary>
        public static int PlayLevel(Ability a, int deckLevel) { return deckLevel > MaxLevel ? a.MaxLevel : Math.Min(Math.Max(1, deckLevel), a.MaxLevel); }

        // ---------------- the collection ----------------

        public static CardRec Find(CardCollection c, string id) { foreach (var r in c.Cards) if (r.Id == id) return r; return null; }
        public static int Level(CardCollection c, string id) { var r = Find(c, id); return r == null ? 0 : LevelFor(r.Copies); }

        /// <summary>A card into the collection: a new one, or a copy. Returns the level it went to (and whether it was new).</summary>
        public static int Add(CardCollection c, string id, out bool fresh)
        {
            var r = Find(c, id);
            fresh = r == null;
            if (fresh) { r = new CardRec { Id = id }; c.Cards.Add(r); }
            r.Copies++;
            if (fresh && c.Deck.Count < DeckSize) c.Deck.Add(id);         // a new card goes into a deck with room
            return LevelFor(r.Copies);
        }

        public static int EvolveCostOf(string id) { var a = GauntletAbilities.Find(id); return a == null ? 0 : EvolveCost[Math.Min(EvolveCost.Length - 1, (int)a.Rarity)]; }
        public static bool CanEvolve(CardCollection c, string id) { var r = Find(c, id); return r != null && !r.Evolved && LevelFor(r.Copies) >= MaxLevel; }

        /// <summary>Evolve a level-3 card (the game takes the coins first).</summary>
        public static bool Evolve(CardCollection c, string id)
        {
            if (!CanEvolve(c, id)) return false;
            Find(c, id).Evolved = true;
            return true;
        }

        /// <summary>How strong a card is for the deck: evolved, then level, then rarity.</summary>
        static int Strength(CardRec r)
        {
            var a = GauntletAbilities.Find(r.Id);
            return (r.Evolved ? 1000 : 0) + LevelFor(r.Copies) * 100 + (a != null ? (int)a.Rarity * 10 : 0);
        }

        /// <summary>Auto: the deck is your best DeckSize cards.</summary>
        public static void AutoDeck(CardCollection c)
        {
            var list = new List<CardRec>(c.Cards);
            list.RemoveAll(r => !IsCard(GauntletAbilities.Find(r.Id)));
            list.Sort((x, y) => { int s = Strength(y).CompareTo(Strength(x)); return s != 0 ? s : string.CompareOrdinal(x.Id, y.Id); });
            c.Deck.Clear();
            for (int i = 0; i < list.Count && i < DeckSize; i++) c.Deck.Add(list[i].Id);
        }

        /// <summary>A card in or out of the deck (false: the deck is full, or it isn't yours).</summary>
        public static bool Toggle(CardCollection c, string id)
        {
            if (c.Deck.Remove(id)) return true;
            if (c.Deck.Count >= DeckSize || Find(c, id) == null) return false;
            c.Deck.Add(id);
            return true;
        }

        /// <summary>The deck as a run takes it: each card and its deck level.</summary>
        public static List<RunMod> RunDeck(CardCollection c)
        {
            var d = new List<RunMod>();
            foreach (var id in c.Deck) { var r = Find(c, id); if (r != null && IsCard(GauntletAbilities.Find(id))) d.Add(new RunMod { Id = id, Level = DeckLevel(r) }); }
            return d;
        }

        // ---------------- packs ----------------

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; return x; }

        /// <summary>Pack number <paramref name="pack"/> from this seed: PackSize card ids, by PackWeight.</summary>
        public static List<string> Pack(uint seed, int pack)
        {
            var cards = All();
            var byRarity = new List<Ability>[PackWeight.Length];
            for (int r = 0; r < byRarity.Length; r++) byRarity[r] = cards.FindAll(a => (int)a.Rarity == r);
            var got = new List<string>();
            uint h = Hash(seed ^ Hash((uint)pack * 2654435761u + 0x9ACu));
            for (int i = 0; i < PackSize; i++)
            {
                h = Hash(h + 0x6D2B79F5u);
                int total = 0;
                for (int r = 0; r < PackWeight.Length; r++) if (byRarity[r].Count > 0) total += PackWeight[r];
                int x = (int)(h % (uint)total), rar = 0;
                for (int r = 0; r < PackWeight.Length; r++) { if (byRarity[r].Count == 0) continue; if (x < PackWeight[r]) { rar = r; break; } x -= PackWeight[r]; }
                h = Hash(h + 0x1B873593u);
                var pool = byRarity[rar];
                got.Add(pool[(int)(h % (uint)pool.Count)].Id);
            }
            return got;
        }

        /// <summary>Open the collection's next pack (the game takes the coins first): the ids, in order.</summary>
        public static List<string> Open(CardCollection c)
        {
            var ids = Pack(c.Seed, c.Packs++);
            foreach (var id in ids) Add(c, id, out _);
            return ids;
        }

        /// <summary>A new collection: every common and StarterRares rares at level 1, all in the deck.</summary>
        public static CardCollection Starter(uint seed)
        {
            var c = new CardCollection { Seed = seed };
            foreach (var a in All()) if (a.Rarity == Rarity.Common) Add(c, a.Id, out _);
            var rares = All().FindAll(a => a.Rarity == Rarity.Rare);
            uint h = Hash(seed ^ 0x57A27u);
            for (int i = 0; i < StarterRares && rares.Count > 0; i++)
            {
                h = Hash(h + 0x6D2B79F5u);
                int k = (int)(h % (uint)rares.Count);
                Add(c, rares[k].Id, out _);
                rares.RemoveAt(k);
            }
            return c;
        }
    }
}
