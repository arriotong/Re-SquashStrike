// ===========================================================
// Assets/SquashSim.Testing/Editor/GauntletTests.cs
// P5 the Gauntlet roguelite's rules:
//   - floors: game to 7, elites every 5th, bosses every 10th (a game to
//     11, legends), the endless climb (attributes past 30, Cannon past 60)
//   - cards: seeded, new / level-up, slots enforced, evolutions only with
//     both parts at the top, boss picks two levels, rerolls and banishes
//   - hearts: lose one, golden racket, Revive; Showtime and Nick Bank heal
//   - talents: bought with coins, change a new run; locked abilities only
//     in the pool once unlocked
//   - the player built: attributes and sim abilities from the levels
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Modes;

namespace SquashSim.Tests
{
    public class GauntletTests
    {
        static List<RunMod> Talents(params (string id, int lv)[] t)
        {
            var l = new List<RunMod>();
            foreach (var x in t) l.Add(new RunMod { Id = x.id, Level = x.lv });
            return l;
        }

        [Test] public void FloorsGetHarderWithElitesAndBosses()
        {
            var run = Gauntlet.NewRun(7);
            var f1 = Gauntlet.Floor(run, 1);
            Assert.That(f1.Kind, Is.EqualTo(FloorKind.Normal)); Assert.That(f1.Points, Is.EqualTo(7)); Assert.That(f1.Traits, Is.Empty);
            var f5 = Gauntlet.Floor(run, 5);
            Assert.That(f5.Kind, Is.EqualTo(FloorKind.Elite)); Assert.That(f5.Traits.Count, Is.EqualTo(1)); Assert.That(f5.Title, Does.StartWith("Elite"));
            var f10 = Gauntlet.Floor(run, 10);
            Assert.That(f10.Kind, Is.EqualTo(FloorKind.Boss)); Assert.That(f10.Points, Is.EqualTo(11));
            Assert.That(f10.Name, Is.EqualTo(SquashSim.Career.Stars.FullName(SquashSim.Career.Stars.Find("s45"))), "P6: the first boss is the world No. 45");
            Assert.That(f10.OpponentId, Is.EqualTo("star:s45")); Assert.That(f10.Signature, Is.Not.Empty, "with their signature shots");
            Assert.That(f10.Traits.Count, Is.EqualTo(1), "the first boss: one trait");
            Assert.That(f5.OpponentId, Does.StartWith("star:"), "an elite is a star too");
            var f100 = Gauntlet.Floor(run, 100);
            Assert.That(f100.OpponentId, Is.EqualTo("star:s01"), "floor 100: the world No. 1");
            Assert.That(f100.Difficulty, Is.EqualTo(1.0));
            Assert.That(Gauntlet.Floor(run, 90).OpponentId, Is.EqualTo("star:s05"));
            Assert.That(Gauntlet.Floor(run, 110).OpponentId, Is.EqualTo("star:s10"), "then round the top 10");
            Assert.That(Gauntlet.Floor(run, 7).OpponentId, Is.EqualTo(Gauntlet.Floor(run, 7).Style), "an ordinary floor: a style");
            Assert.That(Gauntlet.Floor(run, 20).Traits.Count, Is.EqualTo(2), "from the second: two");
            Assert.That(Gauntlet.Difficulty(20), Is.GreaterThan(Gauntlet.Difficulty(2)));
            Assert.That(Gauntlet.Difficulty(200), Is.EqualTo(1.0));
            Assert.That(Gauntlet.Floor(run, 12).Attributes, Is.Null, "a neutral athlete early on");
            Assert.That(Gauntlet.Floor(run, 48).Attributes.Power, Is.GreaterThan(5), "stronger past 30");
            var late = Gauntlet.Floor(run, 81);
            Assert.That(late.Traits.Find(t => t.Id == "e_cannon").Level, Is.GreaterThanOrEqualTo(3), "past 60 the Cannon grows: endless");
        }

        [Test] public void CardsAreSeededAndFollowTheRules()
        {
            var a = Gauntlet.NewRun(42); var b = Gauntlet.NewRun(42);
            Gauntlet.Won(a, 3, 0, 0); Gauntlet.Won(b, 3, 0, 0);
            Assert.That(a.AwaitingPick, Is.True);
            Assert.That(a.Offer.Count, Is.EqualTo(3));
            Assert.That(a.Offer, Is.EqualTo(b.Offer), "the same seed, the same cards");
            Assert.That(new HashSet<string>(a.Offer).Count, Is.EqualTo(3), "three different cards");
            foreach (var id in a.Offer) Assert.That(GauntletAbilities.Find(id).Locked, Is.False, "nothing locked comes up: " + id);
            string first = a.Offer[0];
            Assert.That(Gauntlet.Pick(a, first), Is.True);
            Assert.That(Gauntlet.Pick(a, first), Is.False, "one pick a floor");
            var ab = GauntletAbilities.Find(first);
            if (ab.Kind != AbilityKind.Instant) Assert.That(Gauntlet.Level(a, first), Is.EqualTo(1));
            Assert.That(a.Floor, Is.EqualTo(2));
        }

        [Test] public void AnEliteOffersFourAndABossTwoLevels()
        {
            var run = Gauntlet.NewRun(5);
            run.Floor = 5;
            Gauntlet.Won(run, 2, 0, 0);
            Assert.That(run.Offer.Count, Is.EqualTo(4), "an elite: a pick of four");
            Gauntlet.Pick(run, run.Offer.Find(id => GauntletAbilities.Find(id).Kind != AbilityKind.Instant) ?? run.Offer[0]);
            run.Floor = 10;
            Gauntlet.Won(run, 2, 0, 0);
            Assert.That(run.BossPick, Is.True);
            foreach (var id in run.Offer)
            {
                var ab = GauntletAbilities.Find(id);
                Assert.That(ab.Rarity, Is.Not.EqualTo(Rarity.Common), "a boss's reward: rare and up");
                Assert.That(ab.Kind, Is.Not.EqualTo(AbilityKind.Instant));
            }
            var card = run.Offer[0];
            int before = Gauntlet.Level(run, card);
            Gauntlet.Pick(run, card);
            Assert.That(Gauntlet.Level(run, card), Is.EqualTo(System.Math.Min(GauntletAbilities.Find(card).MaxLevel, before + 2)), "two levels at once");
            Assert.That(run.BossesBeaten, Is.EqualTo(1));
        }

        [Test] public void SlotsMakeABuild()
        {
            var run = Gauntlet.NewRun(9);
            foreach (var id in new[] { "nick", "vdrop", "trickle", "hlob", "rail", "wide" }) run.Abilities.Add(new RunMod { Id = id, Level = 1 });
            Assert.That(Gauntlet.Count(run, true), Is.EqualTo(6));
            for (int i = 0; i < 25; i++)
            {
                run.Roll = i;
                foreach (var id in Gauntlet.OfferFor(run, null, 4))
                {
                    var a = GauntletAbilities.Find(id);
                    if (a.Active) Assert.That(Gauntlet.Level(run, id), Is.GreaterThan(0), "active slots full: only your own come up (" + id + ")");
                }
            }
        }

        [Test] public void EvolutionsNeedBothPartsAtTheTop()
        {
            var run = Gauntlet.NewRun(11);
            run.Abilities.Add(new RunMod { Id = "freeze", Level = 3 });
            run.Abilities.Add(new RunMod { Id = "slowfield", Level = 4 });
            for (int i = 0; i < 20; i++) { run.Roll = i; Assert.That(Gauntlet.OfferFor(run, null), Does.Not.Contain("world")); }
            run.Abilities.Find(m => m.Id == "slowfield").Level = 5;
            run.Roll = 0;
            Assert.That(Gauntlet.OfferFor(run, null), Does.Contain("world"), "ready: on the table");
            Assert.That(GauntletAbilities.Find("world").Rarity, Is.EqualTo(Rarity.Legendary));
        }

        [Test] public void FiveBuildsAndTheOldCardsRetired()
        {
            // the chaos remake: every build has its cards, an ultimate or a payoff, and a legendary evolution
            foreach (var f in new[] { Family.Time, Family.Power, Family.Trick, Family.Jackpot, Family.Guard })
            {
                var cards = GauntletAbilities.All().FindAll(a => a.Family == f && !a.Retired);
                Assert.That(cards.Count, Is.GreaterThanOrEqualTo(5), f + ": a build to make");
                Assert.That(cards.Exists(a => a.Kind == AbilityKind.Evolved), Is.True, f + ": its legendary");
            }
            Assert.That(GauntletAbilities.All().FindAll(a => a.Kind == AbilityKind.Ultimate).Count, Is.EqualTo(3));
            var run = Gauntlet.NewRun(5);
            for (int i = 0; i < 60; i++)
            {
                run.Roll = i; run.Floor = 2 + i;
                foreach (var id in Gauntlet.OfferFor(run, Talents(), 4)) Assert.That(GauntletAbilities.Find(id).Retired, Is.False, "retired: never offered (" + id + ")");
            }
            // the opponents bend the rules too: a chaos trait at level 3 somewhere in the elites (higher past floor 50)
            bool chaos = false;
            for (int fl = 5; fl <= 200; fl += 5) foreach (var t in Gauntlet.Floor(run, fl).Traits) if (!t.Id.StartsWith("e_")) { chaos = true; Assert.That(t.Level, fl <= 50 ? (NUnit.Framework.Constraints.IResolveConstraint)Is.EqualTo(Gauntlet.ChaosTraitLevel) : Is.GreaterThan(Gauntlet.ChaosTraitLevel), "2026-10-28: stronger past floor 50"); }
            Assert.That(chaos, Is.True);
        }

        [Test] public void RerollsAndBanishes()
        {
            var run = Gauntlet.NewRun(13, Talents(("banish", 1)));
            Assert.That(run.Rerolls, Is.EqualTo(1)); Assert.That(run.Banishes, Is.EqualTo(1));
            Gauntlet.Won(run, 1, 0, 0);
            var first = new List<string>(run.Offer);
            Assert.That(Gauntlet.Reroll(run, null), Is.True);
            Assert.That(run.Offer, Is.Not.EqualTo(first), "new cards");
            Assert.That(Gauntlet.Reroll(run, null), Is.False, "no rerolls left");
            string gone = run.Offer.Find(id => GauntletAbilities.Find(id).Kind != AbilityKind.Evolved);
            Assert.That(Gauntlet.Banish(run, gone, null), Is.True);
            Assert.That(run.Banished, Does.Contain(gone));
            for (int i = 0; i < 20; i++) { run.Roll = 100 + i; Assert.That(Gauntlet.OfferFor(run, null), Does.Not.Contain(gone), "banished for the run"); }
        }

        [Test] public void HeartsGoldenRacketAndRevive()
        {
            var run = Gauntlet.NewRun(3, Talents(("hearty", 1), ("revive", 1)));
            Assert.That(run.Lives, Is.EqualTo(4));
            run.Golden = true;
            Gauntlet.Lost(run);
            Assert.That(run.Lives, Is.EqualTo(4)); Assert.That(run.Golden, Is.False, "the golden racket took it");
            for (int i = 0; i < 4; i++) Gauntlet.Lost(run);
            Assert.That(run.Over, Is.False); Assert.That(run.Revived, Is.True); Assert.That(run.Lives, Is.EqualTo(1), "Revive: one more");
            Gauntlet.Lost(run);
            Assert.That(run.Over, Is.True);
        }

        [Test] public void ShowtimeAndNickBankHeal()
        {
            var run = Gauntlet.NewRun(4);
            run.Lives = 2;
            run.Abilities.Add(new RunMod { Id = "showtime", Level = 1 });
            run.Abilities.Add(new RunMod { Id = "nickbank", Level = 5 });
            Gauntlet.Won(run, 1, 1, 1);
            Assert.That(run.Lives, Is.EqualTo(4), "an ace (Showtime) and the first nick (Nick Bank 5)");
            Gauntlet.Pick(run, run.Offer[0]);
            int lives = run.Lives;
            Gauntlet.Won(run, 1, 0, 1);
            Assert.That(run.Lives, Is.EqualTo(lives), "Nick Bank heals once a run");
        }

        [Test] public void TalentsAndUnlocks()
        {
            var owned = new List<RunMod>();
            int coins = 100;
            Assert.That(GauntletTalents.Buy(owned, ref coins, "reroll"), Is.True);
            Assert.That(coins, Is.EqualTo(60));
            Assert.That(GauntletTalents.Buy(owned, ref coins, "reroll"), Is.False, "100 for level 2: not enough");
            Assert.That(GauntletTalents.Level(owned, "reroll"), Is.EqualTo(1));
            Assert.That(Gauntlet.NewRun(1, owned).Rerolls, Is.EqualTo(2));
            var focus = GauntletAbilities.Find("focus");
            Assert.That(GauntletTalents.Unlocked(owned, focus), Is.False);
            coins = 1000;
            Assert.That(GauntletTalents.Buy(owned, ref coins, "unlock_focus"), Is.True);
            Assert.That(GauntletTalents.Unlocked(owned, focus), Is.True);
            var kit = Gauntlet.NewRun(77, Talents(("kit", 2)));
            Assert.That(kit.Abilities.Count, Is.EqualTo(2), "Kit Bag: two to start");
        }

        [Test] public void ThePlayerIsBuiltFromTheLevels()
        {
            var run = Gauntlet.NewRun(2);
            run.Abilities.Add(new RunMod { Id = "quick", Level = 3 });
            run.Abilities.Add(new RunMod { Id = "heavy", Level = 5 });
            run.Abilities.Add(new RunMod { Id = "nick", Level = 2 });
            run.Abilities.Add(new RunMod { Id = "head", Level = 2 });
            run.Abilities.Add(new RunMod { Id = "focus", Level = 1 });
            var at = Gauntlet.Attributes(run);
            Assert.That(at.Speed, Is.EqualTo(8)); Assert.That(at.Power, Is.EqualTo(10)); Assert.That(at.Control, Is.EqualTo(5));
            var sim = Gauntlet.SimMods(run);
            Assert.That(sim.ConvertAll(m => m.Id), Is.EquivalentTo(new[] { "nick" }), "only what the sim plays");
            Assert.That(Gauntlet.HeadStart(run), Is.EqualTo(2)); Assert.That(Gauntlet.FocusCharges(run), Is.EqualTo(1));
        }

        [Test] public void EveryAbilityIsDescribed()
        {
            foreach (var a in GauntletAbilities.All())
            {
                Assert.That(a.Name, Is.Not.Empty);
                for (int l = 1; l <= a.MaxLevel; l++) Assert.That(a.Level(l), Is.Not.Empty, a.Id + " level " + l);
                if (a.Recipe != null) foreach (var r in a.Recipe) Assert.That(GauntletAbilities.Find(r), Is.Not.Null, a.Id + " needs " + r);
            }
            foreach (var t in GauntletTalents.All()) Assert.That(t.Costs.Length, Is.GreaterThan(0));
        }
    }
}
