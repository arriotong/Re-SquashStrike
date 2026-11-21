// ===========================================================
// Assets/SquashSim.Testing/Editor/GauntletTests.cs
// P5 the Gauntlet roguelite's rules:
//   - floors (2026-11-21 the arcade remake): a target score in a set of
//     rallies, a boss every 5th (a star, a twist), floor 15 wins the run,
//     then endless (traits, attributes past 10, Cannon past 30)
//   - cards: seeded, new / level-up, slots enforced, evolutions only with
//     both parts at the top, boss picks two levels, rerolls and banishes
//   - no hearts: a failed floor ends the run - the golden racket and
//     Second Chance play it again; Showtime and Nick Bank give rallies
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

        [Test] public void FloorsGetHarderWithBossesAndTwists()
        {
            var run = Gauntlet.NewRun(7);
            var f1 = Gauntlet.Floor(run, 1);
            Assert.That(f1.Kind, Is.EqualTo(FloorKind.Normal)); Assert.That(f1.Target, Is.EqualTo(Gauntlet.FirstTarget)); Assert.That(f1.Traits, Is.Empty); Assert.That(f1.Twists, Is.Empty);
            Assert.That(Gauntlet.RalliesFor(run, f1, null), Is.EqualTo(Gauntlet.Rallies));
            for (int fl = 2; fl <= 20; fl++) if (Gauntlet.KindOf(fl) == Gauntlet.KindOf(fl - 1)) Assert.That(Gauntlet.TargetFor(fl), Is.GreaterThanOrEqualTo(Gauntlet.TargetFor(fl - 1)), "the targets climb");
            foreach (int b in new[] { 5, 10, 15 })
            {
                var f = Gauntlet.Floor(run, b);
                Assert.That(f.Kind, Is.EqualTo(FloorKind.Boss), "a boss every 5th");
                Assert.That(f.OpponentId, Does.StartWith("star:")); Assert.That(f.Signature, Is.Not.Empty, "with their signature shots");
                Assert.That(f.Twists.Count, Is.EqualTo(1), "a boss: one twist");
                Assert.That(Gauntlet.TwistName(f.Twists[0]), Is.Not.EqualTo(f.Twists[0]), "named");
            }
            Assert.That(Gauntlet.Floor(run, 5).OpponentId, Is.EqualTo("star:" + SquashSim.Career.Stars.All()[29].Id), "floor 5: the world No. 30");
            Assert.That(Gauntlet.Floor(run, 15).OpponentId, Is.EqualTo("star:" + SquashSim.Career.Stars.All()[4].Id), "floor 15: the No. 5");
            Assert.That(Gauntlet.Floor(run, 35).OpponentId, Is.EqualTo("star:" + SquashSim.Career.Stars.No1.Id), "endless: the No. 1 at 35");
            Assert.That(Gauntlet.Floor(run, 35).Difficulty, Is.EqualTo(1.0));
            Assert.That(Gauntlet.Floor(run, 20).Twists.Count, Is.EqualTo(2), "from floor 20: two twists");
            Assert.That(Gauntlet.Floor(run, 16).Traits.Count, Is.GreaterThan(0), "endless floors bend the rules");
            Assert.That(Gauntlet.Floor(run, 7).OpponentId, Is.EqualTo(Gauntlet.Floor(run, 7).Style), "an ordinary floor: a style");
            Assert.That(Gauntlet.Difficulty(15), Is.GreaterThan(Gauntlet.Difficulty(1)));
            Assert.That(Gauntlet.Difficulty(200), Is.EqualTo(1.0));
            Assert.That(Gauntlet.Floor(run, 12).Attributes, Is.Null, "a neutral athlete early on");
            Assert.That(Gauntlet.Floor(run, 28).Attributes.Power, Is.GreaterThan(5), "stronger past 10");
            Assert.That(Gauntlet.Floor(run, 61).Traits.Find(t => t.Id == "e_cannon").Level, Is.GreaterThanOrEqualTo(3), "past 30 the Cannon grows: endless");
        }

        /// <summary>A floor cleared just at its target.</summary>
        public static FloorPlay Clear(GauntletRun run, int over = 0)
        {
            var f = Gauntlet.BeginFloor(run, null);
            f.Score = f.Target + over; f.Played = 1; f.Done = true;
            return f;
        }

        [Test] public void CardsAreSeededAndFollowTheRules()
        {
            var a = Gauntlet.NewRun(42); var b = Gauntlet.NewRun(42);
            Gauntlet.Won(a, Clear(a), 0, 0); Gauntlet.Won(b, Clear(b), 0, 0);
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

        [Test] public void ABigScoreOffersFourAndABossTwoLevels()
        {
            var run = Gauntlet.NewRun(5);
            run.Floor = 3;
            Gauntlet.Won(run, Clear(run, Gauntlet.TargetFor(3) / 2), 0, 0);
            Assert.That(run.Offer.Count, Is.EqualTo(4), "half the target again: a pick of four");
            Gauntlet.Pick(run, run.Offer.Find(id => GauntletAbilities.Find(id).Kind != AbilityKind.Instant) ?? run.Offer[0]);
            run.Floor = 10;
            Gauntlet.Won(run, Clear(run), 0, 0);
            Assert.That(run.Offer.Count, Is.EqualTo(3), "just the target: three");
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

        [Test] public void FloorFifteenWinsTheRunThenEndless()
        {
            var a = Gauntlet.NewRun(21); a.Floor = Gauntlet.WinFloor;
            Gauntlet.Won(a, Clear(a), 0, 0);
            Assert.That(a.RunWon, Is.True); Assert.That(a.ChoiceDue, Is.True);
            Gauntlet.Choose(a, false);
            Assert.That(a.Over, Is.True, "take the win: the run ends"); Assert.That(a.Failed, Is.False);
            var b = Gauntlet.NewRun(21); b.Floor = Gauntlet.WinFloor;
            Gauntlet.Won(b, Clear(b), 0, 0);
            Gauntlet.Choose(b, true);
            Assert.That(b.Over, Is.False); Assert.That(b.Endless, Is.True); Assert.That(b.AwaitingPick, Is.True, "endless: the pick, then floor 16");
            Gauntlet.Pick(b, b.Offer[0]);
            Assert.That(b.Floor, Is.EqualTo(Gauntlet.WinFloor + 1));
            Gauntlet.Lost(b, null);
            Assert.That(b.Over, Is.True); Assert.That(b.RunWon, Is.True, "a failed endless floor: still a run won");
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
            Gauntlet.Won(run, Clear(run), 0, 0);
            var first = new List<string>(run.Offer);
            Assert.That(Gauntlet.Reroll(run, null), Is.True);
            Assert.That(run.Offer, Is.Not.EqualTo(first), "new cards");
            Assert.That(Gauntlet.Reroll(run, null), Is.False, "no rerolls left");
            string gone = run.Offer.Find(id => GauntletAbilities.Find(id).Kind != AbilityKind.Evolved);
            Assert.That(Gauntlet.Banish(run, gone, null), Is.True);
            Assert.That(run.Banished, Does.Contain(gone));
            for (int i = 0; i < 20; i++) { run.Roll = 100 + i; Assert.That(Gauntlet.OfferFor(run, null), Does.Not.Contain(gone), "banished for the run"); }
        }

        [Test] public void AFailedFloorEndsTheRunUnlessRetried()
        {
            var plain = Gauntlet.NewRun(3);
            Assert.That(Gauntlet.Lost(plain, null), Is.False);
            Assert.That(plain.Over, Is.True, "no hearts: over at once"); Assert.That(plain.Failed, Is.True);
            var run = Gauntlet.NewRun(3, Talents(("hearty", 1), ("revive", 1)));
            Assert.That(Gauntlet.RalliesFor(run, Gauntlet.Floor(run, 1), Talents(("hearty", 1))), Is.EqualTo(Gauntlet.Rallies + 1), "Extra Rally: one more");
            run.Golden = true;
            Assert.That(Gauntlet.Lost(run, null), Is.True, "the golden racket: again");
            Assert.That(run.Golden, Is.False);
            Assert.That(Gauntlet.Lost(run, null), Is.True, "Second Chance: again"); Assert.That(run.Revived, Is.True);
            Assert.That(Gauntlet.Lost(run, null), Is.False);
            Assert.That(run.Over, Is.True);
        }

        [Test] public void ShowtimeAndNickBankGiveRallies()
        {
            var run = Gauntlet.NewRun(4);
            run.Abilities.Add(new RunMod { Id = "showtime", Level = 1 });
            run.Abilities.Add(new RunMod { Id = "nickbank", Level = 5 });
            Gauntlet.Won(run, Clear(run), 1, 1);
            Assert.That(run.BonusRallies, Is.EqualTo(2), "an ace (Showtime) and the first nick (Nick Bank 5)");
            Gauntlet.Pick(run, run.Offer.Find(id => id != "life") ?? run.Offer[0]);
            Gauntlet.Won(run, Clear(run), 0, 1);
            Assert.That(run.BonusRallies, Is.EqualTo(0), "Nick Bank once a run");
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
