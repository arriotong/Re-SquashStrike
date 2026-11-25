// ===========================================================
// Assets/SquashGame/Tests/Editor/MachineTests.cs
// 2026-11-24 the new machines (Machines): Pro Cards, Signature Specials,
// Walk-on & Celebration, Training Perks.
//   - four banners, found by id; every opponent has a card; valid items
//   - a pro card's copies raise its tier (Silver 3, Gold 6, Holo 10), then pay
//   - a perk's copies level it; three worn at most; small, capped, and only
//     where the setup asks for them
//   - the defaults (Classic special, Classic walk-on) are free and worn
//   - the new celebrations are poses the athlete knows
// ===========================================================

using System;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using UnityEngine;

namespace SquashGame.Tests
{
    public class MachineTests
    {
        [SetUp]
        public void Fresh()
        {
            var dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeMachines");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            ProfileStore.Folder = dir;
            ProfileStore.Reset();
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); }

        [Test]
        public void FourMachinesEveryOpponentACard()
        {
            Assert.That(Machines.All().Count, Is.EqualTo(4));
            foreach (var id in new[] { Machines.ProCardsId, Machines.SpecialsId, Machines.WalkOnId, Machines.PerksId })
            {
                var b = GearDrops.Banner(id);
                Assert.That(b, Is.Not.Null, id);
                Assert.That(b.Items.Count, Is.GreaterThanOrEqualTo(10), id);
                for (int r = 0; r < 5; r++) Assert.That(b.Of((Rarity)r).Count, Is.GreaterThan(0), id + " has " + (Rarity)r);
                foreach (var it in b.Items) { Assert.That(it.Unlock, Is.EqualTo(UnlockKind.Gacha)); Assert.That(it.Banner, Is.EqualTo(id)); Assert.That(Cosmetics.Find(it.Id), Is.SameAs(it)); }
                Assert.That(GearDrops.Banners().Exists(x => x.Id == id), Is.False, "not a cosmetics chip");
            }
            var cards = Machines.Find(Machines.ProCardsId);
            Assert.That(cards.Items.Count, Is.EqualTo(SquashSim.Career.Stars.AllOpponents.Length), "a card for everyone you can play");
            foreach (var o in SquashSim.Career.Stars.AllOpponents) Assert.That(cards.Items.Exists(i => i.Ref == o), Is.True, o);
            Assert.That(cards.Items.Find(i => i.Ref == "star:" + SquashSim.Career.Stars.No1.Id).Rarity, Is.EqualTo((int)Rarity.Legendary), "the No. 1 is Legendary");
            foreach (var it in Machines.Find(Machines.SpecialsId).Items) { Assert.That(it.Style, Is.InRange(0, Machines.SpecialPatterns.Length - 1)); Assert.That(it.Pose, Is.Not.Empty); }
        }

        [Test]
        public void DefaultsAreFreeAndWorn()
        {
            var l = new Loadout();
            Assert.That(Cosmetics.Find(l.Special).Unlock, Is.EqualTo(UnlockKind.Free));
            Assert.That(Cosmetics.Find(l.WalkOn).Unlock, Is.EqualTo(UnlockKind.Free));
            Assert.That(Machines.SpecialOf(null).Id, Is.EqualTo("sp_classic"));
            Assert.That(Machines.WalkOnOf(new Loadout { WalkOn = "nonsense" }).Id, Is.EqualTo("wo_classic"));
            l.Set(Slot.Special, "sp_dragon"); Assert.That(l.Get(Slot.Special), Is.EqualTo("sp_dragon"));
            l.Set(Slot.WalkOn, "wo_spot"); Assert.That(l.Get(Slot.WalkOn), Is.EqualTo("wo_spot"));
        }

        [Test]
        public void NewCelebrationsArePoses()
        {
            var lib = SquashGame.Athlete.DefaultPoses.Build();
            foreach (var it in Machines.Find(Machines.WalkOnId).Items)
                if (it.Slot == Slot.Celebration) Assert.That(lib.Exists(p => p.Name == it.Pose), Is.True, it.Pose);
        }

        [Test]
        public void ProCardCopiesRaiseTheTier()
        {
            var item = Machines.Find(Machines.ProCardsId).Items[0];
            Assert.That(Machines.TierOf(item.Id), Is.EqualTo(-1));
            string said = null;
            for (int n = 1; n <= Machines.TierAt[3]; n++)
            {
                said = Machines.Copy(item, Rarity.Legendary, out int coins);
                Assert.That(coins, Is.Zero);
                if (n == 1) Assert.That(said, Is.EqualTo("NEW"));
                else if (n == 3) Assert.That(said, Is.EqualTo("SILVER"));
                else if (n == 6) Assert.That(said, Is.EqualTo("GOLD"));
                else if (n == 10) Assert.That(said, Is.EqualTo("HOLO"));
                else Assert.That(said, Is.Null, "a copy towards the next, " + n);
            }
            Assert.That(Machines.TierOf(item.Id), Is.EqualTo(3));
            Assert.That(GearDrops.Owns(item), Is.True);
            said = Machines.Copy(item, Rarity.Legendary, out int paid);
            Assert.That(said, Is.Null); Assert.That(paid, Is.EqualTo(GearDrops.DupeCoins[4]), "past Holo: coins");
        }

        [Test]
        public void PullingAMachineCountsCopies()
        {
            GearDrops.Wallet.Coins = GearDrops.TenPullCost * 3;
            for (int i = 0; i < 3; i++)
            {
                var drops = GearDrops.Pull(Machines.PerksId, GearDrops.TenPull);
                Assert.That(drops.Count, Is.EqualTo(10));
                foreach (var d in drops) Assert.That(d.Item.Slot, Is.EqualTo(Slot.Perk));
            }
            int copies = 0; foreach (var c in ProfileStore.Current.Progress.Copies) copies += c.N;
            int coins = GearDrops.Wallet.Coins;
            Assert.That(copies + 0, Is.LessThanOrEqualTo(30));
            Assert.That(copies, Is.GreaterThan(0));
            Assert.That(ProfileStore.Current.Progress.Copies.Exists(c => c.N >= 2), Is.True, "30 pulls on 15 perks: some came twice");
            Debug.Log("[Machines] 30 perk pulls: " + ProfileStore.Current.Progress.Copies.Count + " perks, " + copies + " copies, " + coins + " coins back");
        }

        [Test]
        public void PerksAreSmallCappedAndThreeAtMost()
        {
            var p = ProfileStore.Current.Progress;
            string[] ids = { "pk_routine", "pk_wrist", "pk_gym", "pk_banana" };
            foreach (var id in ids) { p.Owned.Add(id); p.Copies.Add(new ItemCount { Id = id, N = 6 }); }       // all at level 3
            Assert.That(Machines.Toggle("pk_lungs"), Is.False, "not owned");
            for (int i = 0; i < 3; i++) Assert.That(Machines.Toggle(ids[i]), Is.True);
            Assert.That(Machines.Toggle(ids[3]), Is.False, "three at most");
            Assert.That(Machines.Equipped.Count, Is.EqualTo(3));
            Assert.That(Machines.LevelOf("pk_gym"), Is.EqualTo(3));
            var t = Machines.Total();
            // power: routine 0.2x2 + wrist 0.3x2 + gym 0.25x2 = 1.5 -> capped at 1.0 (4%)
            Assert.That(t[2], Is.EqualTo(Machines.MaxPerStat).Within(1e-5), "each stat capped");
            Assert.That(t[3], Is.EqualTo(Mathf.Min(1f, 0.4f * 2 + 0.15f * 2)).Within(1e-5));
            var a = Machines.Boost(new AthleteAttributes());
            Assert.That(a.Power, Is.EqualTo(AthleteAttributes.Neutral + 1.0).Within(1e-5));
            Assert.That(a.PowerScale, Is.LessThanOrEqualTo(1.0401), "a 4% edge at most");
            Assert.That(Machines.Toggle(ids[0]), Is.True, "taken off");
            Assert.That(Machines.Equipped.Count, Is.EqualTo(2));
            Assert.That(Machines.Describe(Machines.Total()), Does.Contain("Power"));
        }
    }
}
