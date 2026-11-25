// ===========================================================
// Assets/SquashGame/Tests/Editor/DepthTests.cs
// 2026-11-25 (the user: "work on the thin ones aim for everything being deep
// to deeper medium"):
//   - Quick Match's ladder: a first win pays (and a pro's card), again
//     doesn't; milestones give titles; the streak grows and resets
//   - Local Versus: the head to head adds up; a series ends at its target
//   - Target Arcade: platinum above 3 stars, paid once; your top five
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;

namespace SquashGame.Tests
{
    public class DepthTests
    {
        [SetUp]
        public void Fresh()
        {
            var dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeDepth");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            ProfileStore.Folder = dir;
            ProfileStore.Reset();
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); }

        [Test]
        public void TheQuickMatchLadder()
        {
            int coins = GearDrops.Wallet.Coins;
            var first = QuickLadder.Record("star:s20", true);
            Assert.That(first.First, Is.True);
            Assert.That(first.FirstCoins, Is.GreaterThanOrEqualTo(QuickLadder.FirstWinCoins("star:s20")));
            Assert.That(first.Card, Is.Not.Null, "a pro: their card");
            Assert.That(Machines.TierOf(first.Card.Id), Is.EqualTo(0));
            Assert.That(QuickLadder.Beaten("star:s20"), Is.True);
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(coins + first.Coins));
            var again = QuickLadder.Record("star:s20", true);
            Assert.That(again.First, Is.False, "once");
            Assert.That(again.Streak, Is.EqualTo(2));
            Assert.That(again.StreakCoins, Is.EqualTo(QuickLadder.StreakStep));
            var lost = QuickLadder.Record("star:s19", false);
            Assert.That(QuickLadder.Streak, Is.Zero, "a loss ends the streak");
            Assert.That(QuickLadder.Beaten("star:s19"), Is.False);
            Assert.That(QuickLadder.FirstWinCoins(SquashSim.Career.Stars.AllOpponents[0]), Is.LessThan(QuickLadder.FirstWinCoins("star:s01")), "the better, the more");
            // ten beaten: the first milestone
            var ops = SquashSim.Career.Stars.AllOpponents;
            QuickLadder.Result last = null;
            for (int i = 0; QuickLadder.Count < 10; i++) last = QuickLadder.Record(ops[i], true);
            Assert.That(last.Reached, Is.Not.Null);
            Assert.That(last.Reached.Beaten, Is.EqualTo(10));
            Assert.That(ProfileStore.Current.Progress.Owned, Does.Contain(last.Reached.Title));
            Assert.That(Cosmetics.Find(last.Reached.Title), Is.Not.Null);
            Assert.That(QuickLadder.StreakMax, Is.GreaterThanOrEqualTo(QuickLadder.Record(ops[50], true).StreakCoins), "the streak bonus is capped");
        }

        [Test]
        public void VersusHeadToHeadAndSeries()
        {
            VersusRecord.NewSeries(1);                                         // best of 3
            Assert.That(VersusRecord.Need, Is.EqualTo(2));
            VersusRecord.RecordMatch(0, 2, 1, 30, 25, 18);
            Assert.That(VersusRecord.InSeries, Is.True);
            VersusRecord.RecordMatch(1, 0, 2, 12, 22, 9);
            Assert.That(VersusRecord.InSeries, Is.True, "1-1");
            VersusRecord.RecordMatch(1, 1, 2, 20, 24, 25);
            Assert.That(VersusRecord.SeriesWinner, Is.EqualTo(1));
            Assert.That(VersusRecord.InSeries, Is.False);
            var r = VersusRecord.Record;
            Assert.That(r.Matches, Is.EqualTo(3));
            Assert.That(r.Wins0, Is.EqualTo(1)); Assert.That(r.Wins1, Is.EqualTo(2));
            Assert.That(r.Games0, Is.EqualTo(3)); Assert.That(r.Games1, Is.EqualTo(5));
            Assert.That(r.Longest, Is.EqualTo(25));
            Assert.That(r.Series1, Is.EqualTo(1)); Assert.That(r.Series0, Is.Zero);
            Assert.That(r.BestRun1, Is.EqualTo(2));
            ProfileStore.Reset();
            Assert.That(VersusRecord.Record.Matches, Is.EqualTo(3), "kept");
            VersusRecord.NewSeries(0);
            VersusRecord.RecordMatch(0, 1, 0, 11, 5, 4);
            Assert.That(VersusRecord.InSeries, Is.False, "one match: no series");
            Assert.That(VersusRecord.Record.Series0, Is.Zero);
        }

        [Test]
        public void OnlineCanBeTakenOut()
        {
            // 2026-11-25 v0.7.0 ships without a server: the ONLINE section goes with Features.Online
            bool was = Features.Online;
            try
            {
                Features.Online = false;
                Assert.That(SquashGame.UI.MainMenuScreen.PlaySections, Does.Not.Contain("ONLINE"));
                Assert.That(SquashGame.UI.MainMenuScreen.PlaySections, Does.Contain("PLAY"));
                Features.Online = true;
                Assert.That(SquashGame.UI.MainMenuScreen.PlaySections, Does.Contain("ONLINE"));
            }
            finally { Features.Online = was; }
            Assert.That(Features.ReleaseOnline, Is.False, "v0.7.0: no online in the built game");
        }

        [Test]
        public void ArcadePlatinumAndTopFive()
        {
            var c = SquashSim.Arcade.Challenges.Get(SquashSim.Arcade.ArcadeMode.TimeAttack, 0);
            Assert.That(c.Platinum, Is.GreaterThan(c.Stars[2]));
            int coins = GearDrops.Wallet.Coins;
            ProfileStore.Submit(c.Id, c.Stars[2], 3);
            Assert.That(ArcadePlatinum.Check(c, c.Stars[2], out _), Is.False, "3 stars isn't platinum");
            ProfileStore.Submit(c.Id, c.Platinum, 3);
            Assert.That(ArcadePlatinum.Check(c, c.Platinum, out bool all), Is.True);
            Assert.That(all, Is.False);
            Assert.That(GearDrops.Wallet.Coins, Is.EqualTo(coins + ArcadePlatinum.Coins));
            Assert.That(ArcadePlatinum.Check(c, c.Platinum + 5, out _), Is.False, "once");
            for (int s = 1; s <= 7; s++) ProfileStore.Submit(c.Id, s, 0);
            var top = ProfileStore.Record(c.Id).Top;
            Assert.That(top.Count, Is.EqualTo(5));
            Assert.That(top[0], Is.EqualTo(c.Platinum));
            for (int i = 1; i < top.Count; i++) Assert.That(top[i], Is.LessThanOrEqualTo(top[i - 1]));
            Assert.That(ArcadePlatinum.Total, Is.GreaterThan(10));
        }
    }
}
