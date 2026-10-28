// ===========================================================
// Assets/SquashSim.Testing/Editor/GauntletCardsTests.cs
// The Gauntlet's card decks (2026-10-28):
//   - the starter set: every common and some rares, at level 1, in the deck
//   - packs: five cards, the same seed and number the same cards, by
//     rarity; copies level a card to 3; level 3 evolves
//   - Auto: the deck is the best 20; a full deck takes no more
//   - a run: nothing active at first; a floor won draws cards from the
//     deck only, none already active; a pick plays at its card's level
//     (evolved: the ability's top); the floors keep getting harder
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Modes;

namespace SquashSim.Tests
{
    public class GauntletCardsTests
    {
        [Test] public void TheStarterSetIsTheCommonsAndSomeRares()
        {
            var c = GauntletCards.Starter(42);
            int commons = GauntletCards.All().FindAll(a => a.Rarity == Rarity.Common).Count;
            Assert.That(c.Cards.Count, Is.EqualTo(commons + GauntletCards.StarterRares));
            foreach (var r in c.Cards) Assert.That(GauntletCards.LevelFor(r.Copies), Is.EqualTo(1), r.Id);
            Assert.That(c.Deck.Count, Is.EqualTo(c.Cards.Count), "all in the deck");
            Assert.That(GauntletCards.Starter(42).Cards.ConvertAll(r => r.Id), Is.EqualTo(c.Cards.ConvertAll(r => r.Id)), "the same seed, the same set");
        }

        [Test] public void PacksAreSeededAndCopiesLevelACard()
        {
            Assert.That(GauntletCards.Pack(7, 3), Is.EqualTo(GauntletCards.Pack(7, 3)));
            Assert.That(GauntletCards.Pack(7, 3), Is.Not.EqualTo(GauntletCards.Pack(7, 4)));
            var counts = new int[4];
            for (int p = 0; p < 400; p++) foreach (var id in GauntletCards.Pack(11, p)) counts[(int)GauntletAbilities.Find(id).Rarity]++;
            Assert.That(counts[(int)Rarity.Common], Is.GreaterThan(counts[(int)Rarity.Rare]));
            Assert.That(counts[(int)Rarity.Rare], Is.GreaterThan(counts[(int)Rarity.Epic]));
            Assert.That(counts[(int)Rarity.Epic], Is.GreaterThan(0));

            var c = new CardCollection();
            Assert.That(GauntletCards.Add(c, "meteor", out bool fresh), Is.EqualTo(1)); Assert.That(fresh, Is.True);
            Assert.That(GauntletCards.Add(c, "meteor", out fresh), Is.EqualTo(1)); Assert.That(fresh, Is.False);
            Assert.That(GauntletCards.Add(c, "meteor", out _), Is.EqualTo(2), "3 copies: level 2");
            Assert.That(GauntletCards.CanEvolve(c, "meteor"), Is.False);
            for (int i = 0; i < 3; i++) GauntletCards.Add(c, "meteor", out _);
            Assert.That(GauntletCards.Level(c, "meteor"), Is.EqualTo(3), "6 copies: level 3");
            Assert.That(GauntletCards.Add(c, "meteor", out _), Is.EqualTo(3), "3 is the top");
            Assert.That(GauntletCards.Evolve(c, "meteor"), Is.True);
            Assert.That(GauntletCards.DeckLevel(GauntletCards.Find(c, "meteor")), Is.EqualTo(GauntletCards.MaxLevel + 1));
            Assert.That(GauntletCards.Evolve(c, "meteor"), Is.False, "once");
        }

        [Test] public void AutoIsTheBestTwentyAndAFullDeckTakesNoMore()
        {
            var c = new CardCollection();
            foreach (var a in GauntletCards.All()) c.Cards.Add(new CardRec { Id = a.Id, Copies = a.Rarity == Rarity.Epic ? 6 : 1 });
            GauntletCards.AutoDeck(c);
            Assert.That(c.Deck.Count, Is.EqualTo(GauntletCards.DeckSize));
            foreach (var a in GauntletCards.All()) if (a.Rarity == Rarity.Epic) Assert.That(c.Deck, Does.Contain(a.Id), "the level-3 cards first: " + a.Id);
            var outside = c.Cards.Find(r => !c.Deck.Contains(r.Id));
            Assert.That(GauntletCards.Toggle(c, outside.Id), Is.False, "full");
            Assert.That(GauntletCards.Toggle(c, c.Deck[0]), Is.True, "out");
            Assert.That(GauntletCards.Toggle(c, outside.Id), Is.True, "and another in");
        }

        [Test] public void ARunDrawsOnlyFromTheDeckAndPlaysTheCardsLevel()
        {
            var c = new CardCollection();
            string[] ids = { "meteor", "curve", "quick", "engine", "heavy" };
            foreach (var id in ids) c.Cards.Add(new CardRec { Id = id, Copies = 3 });
            GauntletCards.Find(c, "meteor").Copies = 6; GauntletCards.Find(c, "meteor").Evolved = true;
            c.Deck.AddRange(ids);
            var run = Gauntlet.NewRun(99, null, GauntletCards.RunDeck(c));
            Assert.That(run.Abilities, Is.Empty, "nothing active at first");
            for (int f = 0; f < 4; f++)
            {
                Gauntlet.Won(run, 3, 0, 0, null);
                Assert.That(run.AwaitingPick, Is.True);
                foreach (var id in run.Offer) Assert.That(ids, Does.Contain(id), "from the deck only");
                foreach (var id in run.Offer) Assert.That(Gauntlet.Level(run, id), Is.EqualTo(0), "none already active");
                string pick = run.Offer.Contains("meteor") ? "meteor" : run.Offer[0];
                int want = pick == "meteor" ? GauntletAbilities.Find("meteor").MaxLevel : 2;
                if (run.BossPick) want = System.Math.Min(GauntletAbilities.Find(pick).MaxLevel, want + 1);
                Assert.That(Gauntlet.Pick(run, pick), Is.True);
                Assert.That(Gauntlet.Level(run, pick), Is.EqualTo(want), pick + ": its card's level (evolved: the top)");
            }
            Gauntlet.Won(run, 3, 0, 0, null);
            Gauntlet.Pick(run, run.Offer[0]);
            Gauntlet.Won(run, 3, 0, 0, null);
            Assert.That(run.Offer, Is.EqualTo(new List<string> { "life" }).Or.EqualTo(new List<string> { "coins" }), "the deck spent: a bonus instead");
        }

        [Test] public void TheFloorsKeepGettingHarder()
        {
            for (int f = 2; f <= 60; f++) Assert.That(Gauntlet.Difficulty(f), Is.GreaterThanOrEqualTo(Gauntlet.Difficulty(f - 1)));
            Assert.That(Gauntlet.Difficulty(30) - Gauntlet.Difficulty(20), Is.GreaterThan(Gauntlet.Difficulty(14) - Gauntlet.Difficulty(4)), "steeper past the knee");
            Assert.That(Gauntlet.Floor(new GauntletRun { Seed = 5 }, 70).Traits.Count, Is.GreaterThan(Gauntlet.Floor(new GauntletRun { Seed = 5 }, 30).Traits.Count), "more traits late");
        }
    }
}
