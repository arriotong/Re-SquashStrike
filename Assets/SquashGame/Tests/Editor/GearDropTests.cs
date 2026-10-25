// ===========================================================
// Assets/SquashGame/Tests/Editor/GearDropTests.cs
// P4: Gear Drops, the rules as published on the screen.
//   - six banners of 25, each with its gold editions; valid, unique items
//   - the odds over many pulls (each rarity, and gold at 1%)
//   - every x10 has an Epic; 70 pulls without a Legendary make the next
//     one Legendary, then the count starts again
//   - duplicates pay coins; gold editions never repeat; gold-plating
//   - the same seed, the same pulls; tickets are spent, never minted
//   - the Pro Shop: 8 a day, the same all day; buying with coins
//   - a version 3 profile gets a wallet with its welcome tickets
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;

namespace SquashGame.Tests
{
    public class GearDropTests
    {
        string _dir;

        [SetUp]
        public void Fresh()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeGearDrops");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
            Challenges.Now = () => new DateTime(2026, 10, 26, 12, 0, 0);
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); Challenges.Now = () => DateTime.Now; }

        static void Give(int tickets, int tens = 0) { GearDrops.Wallet.Tickets = tickets; GearDrops.Wallet.TenTickets = tens; }

        [Test] public void SixBannersOfTwentyFiveWithGoldEditions()
        {
            Assert.That(GearDrops.Banners().Count, Is.EqualTo(6));
            var counts = new[] { 7, 6, 6, 4, 2 };
            foreach (var b in GearDrops.Banners())
            {
                Assert.That(b.Items.Count, Is.EqualTo(25), b.Id);
                for (int r = 0; r < 5; r++) Assert.That(b.Of((Rarity)r).Count, Is.EqualTo(counts[r]), b.Id + " " + (Rarity)r);
                foreach (var it in b.Items)
                {
                    Assert.That(it.Unlock, Is.EqualTo(UnlockKind.Gacha));
                    Assert.That(string.IsNullOrEmpty(it.Name), Is.False);
                    var g = Cosmetics.Find(it.Id + "_gold");
                    Assert.That(g, Is.Not.Null, it.Id + " has a gold edition"); Assert.That(g.Gold, Is.True); Assert.That(g.Rarity, Is.EqualTo(it.Rarity));
                }
                Assert.That(b.Of(Rarity.Legendary).Exists(i => i.Slot == Slot.Frame && i.Glow), Is.True, "a glowing legendary racket");
            }
            Assert.That(Rewards.Owns("trail_classic"), Is.True, "the classic trail is everyone's");
            Assert.That(GearDrops.Wallet.Tickets, Is.EqualTo(GearDrops.StartTickets), "a new player starts with tickets");
        }

        [Test] public void TheOddsAreThePublishedOnes()
        {
            const int N = 200000;
            var n = new int[5];
            for (int i = 0; i < N; i++) n[(int)GearDrops.RarityFor((Hash((uint)i) / 4294967296.0))]++;
            for (int r = 0; r < 5; r++) Assert.That(n[r] / (double)N, Is.EqualTo(GearDrops.Odds[r]).Within(0.003), ((Rarity)r).ToString());
            // through the real pulls (pity off: a fresh banner each 60 pulls), gold included
            int pulls = 0, gold = 0; var seen = new int[5];
            Give(0, 100000);
            for (int k = 0; k < 2000; k++)
            {
                var b = GearDrops.Banners()[k % 6].Id;
                foreach (var d in GearDrops.Pull(b, 10)) { pulls++; seen[(int)d.Rarity]++; if (d.Gold) gold++; }
                ProfileStore.Current.Progress.Owned.RemoveAll(id => id.EndsWith("_gold"));      // keep gold possible
            }
            Debug.Log("[P4] " + pulls + " pulls: " + string.Join(" / ", Array.ConvertAll(seen, x => (100.0 * x / pulls).ToString("0.00") + "%")) + ", gold " + (100.0 * gold / pulls).ToString("0.00") + "%");
            Assert.That(gold / (double)pulls, Is.EqualTo(GearDrops.GoldChance).Within(0.002));
            Assert.That(seen[(int)Rarity.Legendary] / (double)pulls, Is.InRange(0.015, 0.025), "1.5% and the pity's top-up");
        }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        [Test] public void EveryTenHasAnEpicAndPityGivesALegendary()
        {
            Give(0, 50);
            for (int k = 0; k < 50; k++)
            {
                var ten = GearDrops.Pull("club", 10);
                Assert.That(ten.Count, Is.EqualTo(10));
                Assert.That(ten.Exists(d => d.Rarity >= Rarity.Epic), Is.True, "x10 #" + k);
            }
            // pity: force a long run without a Legendary - the 70th always is one
            var st = GearDrops.State;
            st.Pity.Clear(); st.Pity.Add(new BannerPity { Id = "desert", Count = GearDrops.Pity - 1 });
            Give(1);
            var one = GearDrops.Pull("desert", 1);
            Assert.That(one[0].Rarity, Is.EqualTo(Rarity.Legendary), "the 70th pull without one");
            Assert.That(GearDrops.PityOf("desert"), Is.EqualTo(0), "and the count starts again");
            Assert.That(GearDrops.PityOf("club"), Is.LessThan(GearDrops.Pity));
        }

        [Test] public void DuplicatesPayCoinsAndGoldNeverRepeats()
        {
            Give(0, 30);
            int coins = 0, news = 0;
            var all = new List<Drop>();
            for (int k = 0; k < 30; k++) all.AddRange(GearDrops.Pull("arena", 10));
            foreach (var d in all) { if (d.New) news++; else { coins += d.Coins; Assert.That(d.Coins, Is.EqualTo(GearDrops.DupeCoins[(int)d.Rarity])); } }
            Assert.That(news, Is.LessThanOrEqualTo(50), "25 items and 25 gold editions at most");
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(coins));
            var golds = all.FindAll(d => d.Gold);
            Assert.That(new HashSet<string>(golds.ConvertAll(d => d.Item.Id)).Count, Is.EqualTo(golds.Count), "no gold edition twice");
            // gold-plating
            var owned = GearDrops.Banner("arena").Items.Find(i => GearDrops.Owns(i) && !ProfileStore.Current.Progress.Owned.Contains(i.Id + "_gold"));
            Assert.That(owned, Is.Not.Null);
            int cost = GearDrops.PlateCost(owned);
            GearDrops.Wallet.Coins = cost - 1;
            Assert.That(GearDrops.GoldPlate(owned), Is.False, "not enough coins");
            GearDrops.Wallet.Coins = cost;
            Assert.That(GearDrops.GoldPlate(owned), Is.True);
            Assert.That(Rewards.Owns(owned.Id + "_gold"), Is.True); Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(0));
            Assert.That(GearDrops.PlateCost(owned), Is.EqualTo(0), "once");
        }

        [Test] public void TicketsAreSpentAndTheSameSeedGivesTheSamePulls()
        {
            Give(5);
            Assert.That(GearDrops.Pull("show", 10), Is.Empty, "5 tickets don't make a x10");
            Assert.That(GearDrops.Wallet.Tickets, Is.EqualTo(5));
            var a = GearDrops.Pull("show", 1);
            Assert.That(GearDrops.Wallet.Tickets, Is.EqualTo(4));
            Give(10); var ten = GearDrops.Pull("show", 10);
            Assert.That(GearDrops.Wallet.Tickets, Is.EqualTo(0), "ten tickets for a x10 with no x10 ticket");
            uint seed = GearDrops.State.Seed;
            var first = ten.ConvertAll(d => d.Item.Id);
            // a second profile with the same seed, the same pull count: the same items
            ProfileStore.Reset(); File.Delete(Path.Combine(_dir, ProfileStore.FileName)); ProfileStore.Reset();
            GearDrops.State.Seed = seed; GearDrops.State.Pulls = 1; Give(10);
            Assert.That(GearDrops.Pull("show", 10).ConvertAll(d => d.Item.Id), Is.EqualTo(first));
        }

        [Test] public void TheProShopIsFixedForADay()
        {
            var today = GearDrops.ShopToday();
            Assert.That(today.Count, Is.EqualTo(GearDrops.ShopSize));
            Assert.That(today, Is.Unique);
            Assert.That(GearDrops.ShopToday(), Is.EqualTo(today), "the same all day");
            var it = today.Find(i => !GearDrops.Owns(i));
            GearDrops.Wallet.Coins = GearDrops.Price(it);
            Assert.That(GearDrops.Buy(it), Is.True);
            Assert.That(GearDrops.Owns(it), Is.True); Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(0));
            Assert.That(GearDrops.Buy(it), Is.False, "not twice");
            Challenges.Now = () => new DateTime(2026, 10, 27, 9, 0, 0);
            Assert.That(GearDrops.ShopToday(), Is.Not.EqualTo(today), "new stock tomorrow");
        }

        [Test] public void PlayingEarnsTickets()
        {
            Challenges.Enabled = false;
            Give(0);
            var won = new MatchFacts { Won = true, GamesToWin = 1, Rallies = 10, RalliesWon = 8, Games = new List<int[]> { new[] { 11, 4 } } };
            Rewards.Match(won); Rewards.Match(won);
            var third = Rewards.Match(won);
            Assert.That(third.Tickets, Is.EqualTo(1), "every 3rd win");
            Assert.That(GearDrops.Wallet.Tickets, Is.EqualTo(1));
            var floor = Rewards.GauntletFloor(5);
            Assert.That(floor.Tickets, Is.EqualTo(1)); Assert.That(floor.TenTickets, Is.EqualTo(1), "a milestone floor: a x10");
            var sc = Rewards.Scenario(3, 1, 3);
            Assert.That(sc.Tickets, Is.EqualTo(2), "a ticket a new star");
            Challenges.Enabled = true;
            var r = new RewardResult();
            var st = Challenges.Current();
            var d = Challenges.Find(st.Daily[0].Id); st.Daily[0].Progress = d.Target - 1;
            Challenges.Record(r, new ChallengeEvent { Match = new MatchFacts { Won = true, AgainstHard = true, Rallies = 40, RalliesWon = 40, LongestRally = 20, GreatHits = 20, Nicks = 5, Winners = 20,
                Kills = 10, Drops = 10, WinningBoasts = 5, Aces = 2, TopPaceKmh = 140, CameBackFrom0_5 = true, TSeconds = 60, Seconds = 100, Games = new List<int[]> { new[] { 11, 0 } } },
                ArcadeStars = 5, Arcade = true, GauntletFloor = 9, ScenarioStars = 3 });
            Assert.That(r.Tickets, Is.GreaterThanOrEqualTo(Rewards.DailyTickets), "a daily pays tickets");
        }

        [Test] public void AVersion3ProfileGetsAWallet()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":3,\"Progress\":{\"Xp\":900}}");
            ProfileStore.Reset();
            var p = ProfileStore.Current;
            Assert.That(p.Version, Is.EqualTo(4));
            Assert.That(p.Progress.Wallet.Tickets, Is.EqualTo(GearDrops.StartTickets));
            Assert.That(p.Progress.Loadout.Trail, Is.EqualTo("trail_classic"));
        }
    }
}
