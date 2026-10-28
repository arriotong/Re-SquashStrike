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

        /// <summary>Coins for this many pulls (2026-10-28: coins are the one currency).</summary>
        static void Give(int pulls) { GearDrops.Wallet.Coins = pulls * GearDrops.PullCost; }

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
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(GearDrops.StartCoins), "a new player starts with coins for ten pulls");
        }

        [Test] public void TheOddsAreThePublishedOnes()
        {
            const int N = 200000;
            var n = new int[5];
            for (int i = 0; i < N; i++) n[(int)GearDrops.RarityFor((Hash((uint)i) / 4294967296.0))]++;
            for (int r = 0; r < 5; r++) Assert.That(n[r] / (double)N, Is.EqualTo(GearDrops.Odds[r]).Within(0.003), ((Rarity)r).ToString());
            // through the real pulls (pity off: a fresh banner each 60 pulls), gold included
            int pulls = 0, gold = 0; var seen = new int[5];
            GearDrops.Wallet.Coins = 100000 * GearDrops.TenPullCost;
            var pg = ProfileStore.Current.Progress;
            if (pg.Gacha == null) pg.Gacha = new GachaState();
            pg.Gacha.Seed = 0x5EED1u;                                                // (a fixed seed: the same draws every run)
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
            GearDrops.Wallet.Coins = 50 * GearDrops.TenPullCost;
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
            GearDrops.Wallet.Coins = 30 * GearDrops.TenPullCost;
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

        [Test] public void CoinsPayForPullsAndTheSameSeedGivesTheSamePulls()
        {
            Give(5);
            Assert.That(GearDrops.Pull("show", 10), Is.Empty, "5 pulls' worth doesn't make a x10");
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(5 * GearDrops.PullCost));
            var a = GearDrops.Pull("show", 1);
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(4 * GearDrops.PullCost + a[0].Coins), "a pull's price (a duplicate pays some back)");
            GearDrops.Wallet.Coins = GearDrops.TenPullCost; var ten = GearDrops.Pull("show", 10);
            Assert.That(ten.Count, Is.EqualTo(10), "a x10 for nine pulls' worth");
            int back = 0; foreach (var d in ten) back += d.Coins;
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(back));
            uint seed = GearDrops.State.Seed;
            var first = ten.ConvertAll(d => d.Item.Id);
            // a second profile with the same seed, the same pull count: the same items
            ProfileStore.Reset(); File.Delete(Path.Combine(_dir, ProfileStore.FileName)); ProfileStore.Reset();
            GearDrops.State.Seed = seed; GearDrops.State.Pulls = 1; GearDrops.Wallet.Coins = GearDrops.TenPullCost;
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

        [Test] public void EveryModePaysCoins()
        {
            // 2026-10-28: "a universal currency that you can get from playing any mode"
            Challenges.Enabled = false;
            Give(0);
            var won = new MatchFacts { Won = true, GamesToWin = 1, Rallies = 10, RalliesWon = 8, Games = new List<int[]> { new[] { 11, 4 } } };
            Assert.That(Rewards.Match(won).Coins, Is.GreaterThan(0), "a match");
            var floor = Rewards.GauntletFloor(5);
            Assert.That(floor.CoinWhy, Does.Contain("a Gauntlet floor"), "a Gauntlet floor");
            Assert.That(floor.Coins, Is.GreaterThanOrEqualTo(GearDrops.PullCost + GearDrops.TenPullCost), "a milestone floor: a x10's worth on top");
            var sc = Rewards.Scenario(3, 1, 3);
            Assert.That(sc.CoinWhy, Does.Contain("new stars"), "Scenarios: new stars");
            Assert.That(sc.Coins, Is.GreaterThanOrEqualTo(2 * GearDrops.PullCost));
            Assert.That(Rewards.Arcade(SquashSim.Arcade.Challenges.All()[0].Id, false, 0, 1, 10, false).Coins, Is.GreaterThan(0), "Target Arcade");
            Challenges.Enabled = true;
            Challenges.Current();
            var p = Rewards.Puzzle(true);
            Assert.That(p.CoinWhy, Does.Contain("a shot puzzle"), "a puzzle solved");
            Assert.That(p.Coins, Is.GreaterThanOrEqualTo(Rewards.DailyPulls * GearDrops.PullCost), "and the daily it finished");
            Assert.That(Rewards.Quiz(Challenges.QuizSize, 5, true).CoinWhy, Does.Contain("a rules quiz"), "a quiz passed");
        }

        [Test] public void OldTicketsTurnIntoCoins()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":7,\"Progress\":{\"Xp\":900,\"Wallet\":{\"Tickets\":3,\"TenTickets\":1,\"Coins\":100}}}");
            ProfileStore.Reset();
            var w = ProfileStore.Current.Progress.Wallet;
            Assert.That(w.Coins, Is.EqualTo(100 + 3 * GearDrops.PullCost + GearDrops.TenPullCost), "at the pull price");
            Assert.That(w.Tickets, Is.EqualTo(0)); Assert.That(w.TenTickets, Is.EqualTo(0));
            ProfileStore.Reset();
            Assert.That(ProfileStore.Current.Progress.Wallet.Coins, Is.EqualTo(100 + 3 * GearDrops.PullCost + GearDrops.TenPullCost), "once");
        }

        [Test] public void CoinsFollowHowLongAndHowHard()
        {
            Challenges.Enabled = false;
            MatchFacts M(int rating, bool won, int rallies, bool two = false, bool online = false, bool ranked = false)
            { return new MatchFacts { Won = won, GamesToWin = 1, Rallies = rallies, RalliesWon = rallies / 2, OppRating = rating, TwoPlayer = two, Online = online, Ranked = ranked, Games = new List<int[]> { new[] { 11, 5 } } }; }
            Assert.That(Rewards.MatchCoins(M(99, true, 20)), Is.GreaterThan(Rewards.MatchCoins(M(62, true, 20))), "a harder opponent pays more");
            Assert.That(Rewards.MatchCoins(M(70, true, 40)), Is.GreaterThan(Rewards.MatchCoins(M(70, true, 20))), "a longer match pays more");
            Assert.That(Rewards.MatchCoins(M(70, true, 20)), Is.GreaterThan(Rewards.MatchCoins(M(70, false, 20))), "a win pays more");
            Assert.That(Rewards.MatchCoins(M(0, true, 20, two: true)), Is.GreaterThan(0), "a local match with a friend pays");
            Assert.That(Rewards.MatchCoins(M(0, true, 20, online: true, ranked: true)), Is.GreaterThan(Rewards.MatchCoins(M(0, true, 20, online: true))), "ranked pays more");
            // the soft cap: local matches past 300 coins a day pay a quarter
            Challenges.Now = () => new DateTime(2026, 10, 27, 9, 0, 0);
            int each = Rewards.MatchCoins(M(0, true, 60, two: true));
            int sum = 0; for (int i = 0; i < 12; i++) sum += Rewards.CapLocal(each);
            Assert.That(sum, Is.LessThan(each * 12), "capped");
            Assert.That(sum, Is.EqualTo(Rewards.LocalDailyCap + (each * 12 - Rewards.LocalDailyCap) / 4).Within(12), "past the cap, a quarter");
            Challenges.Now = () => new DateTime(2026, 10, 28, 9, 0, 0);
            Assert.That(Rewards.CapLocal(each), Is.EqualTo(each), "a new day, the full amount");
            Assert.That(Rewards.Match(M(0, true, 60, two: true)).CoinWhy, Does.Contain("a match won"), "a local match pays");
            Challenges.Now = () => DateTime.Now;
            Challenges.Enabled = true;
        }

        [Test] public void LevelsPayAndTheKitStoreSells()
        {
            Challenges.Enabled = false;
            Give(0); GearDrops.Wallet.Coins = 0;
            var r = Rewards.Match(new MatchFacts { Rallies = 100, RalliesWon = 60, GamesToWin = 1 });       // to level 2
            Assert.That(r.ToLevel, Is.EqualTo(2));
            Assert.That(r.CoinWhy, Does.Contain("level 2"));
            Assert.That(r.Coins, Is.GreaterThanOrEqualTo(Rewards.LevelCoins(2) + GearDrops.PullCost), "a level: its coins and a pull's worth");
            foreach (var it in Cosmetics.AtLevel(2)) Assert.That(Rewards.Owns(it.Id), Is.False, "levels no longer give kit: " + it.Id);
            var item = Cosmetics.AtLevel(2)[0];
            Assert.That(GearDrops.InStore(item), Is.True);
            Assert.That(item.HowToUnlock, Does.Contain("coins"));
            GearDrops.Wallet.Coins = GearDrops.StorePrice(item) - 1;
            Assert.That(GearDrops.BuyFromStore(item), Is.False, "a coin short");
            GearDrops.Wallet.Coins += 1;
            Assert.That(GearDrops.BuyFromStore(item), Is.True);
            Assert.That(Rewards.Owns(item.Id), Is.True);
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(0));
            Assert.That(GearDrops.StorePrice(Cosmetics.AtLevel(50)[0]), Is.GreaterThan(GearDrops.StorePrice(item)), "later kit costs more");
            Assert.That(Rewards.LevelGives(10), Does.Contain((Rewards.LevelCoins(10) + GearDrops.TenPullCost).ToString("N0")), "every 5th level: a x10's worth");
            Challenges.Enabled = true;
        }

        [Test] public void AVersion3ProfileGetsAWallet()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":3,\"Progress\":{\"Xp\":900}}");
            ProfileStore.Reset();
            var p = ProfileStore.Current;
            Assert.That(p.Version, Is.EqualTo(ProfileStore.CurrentVersion));
            Assert.That(p.Progress.Wallet.Coins, Is.EqualTo(GearDrops.StartCoins));
            Assert.That(p.Progress.Loadout.Trail, Is.EqualTo("trail_classic"));
        }
    }
}
