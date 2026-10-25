// ===========================================================
// Assets/SquashGame/Tests/Editor/ChallengeTests.cs
// P3-R2: daily and weekly challenges.
//   - the day's three are the same however often they are rolled, never
//     two from one group; a new day brings new ones
//   - a match counts toward them; a finished one pays its XP once
//   - the streak: consecutive days extend it, a missed day resets it,
//     3 days unlocks the first streak item
//   - the weekly pays 600 XP and the next weekly item
//   - a version 2 profile gets an empty challenge state
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;

namespace SquashGame.Tests
{
    public class ChallengeTests
    {
        string _dir;
        DateTime _now;

        [SetUp]
        public void Fresh()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeChallenges");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
            Challenges.Enabled = true;
            _now = new DateTime(2026, 10, 26, 10, 0, 0);          // a Monday
            Challenges.Now = () => _now;
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); Challenges.Now = () => DateTime.Now; }

        /// <summary>A match that does everything a daily might ask (a won Hard match, 40 rallies, nicks, aces...).</summary>
        static MatchFacts Big()
        {
            return new MatchFacts
            {
                Won = true, AgainstHard = true, GamesToWin = 1, Rallies = 40, RalliesWon = 40, LongestRally = 20, GreatHits = 20, Nicks = 5,
                Winners = 20, Kills = 10, Drops = 10, WinningBoasts = 5, Aces = 2, TopPaceKmh = 140, CameBackFrom0_5 = true, TSeconds = 60, Seconds = 100,
                Games = new List<int[]> { new[] { 11, 5 } },
            };
        }

        [Test] public void TheDaysThreeAreFixedAndVaried()
        {
            var a = Challenges.Current();
            var ids = a.Daily.ConvertAll(c => c.Id);
            Assert.That(ids.Count, Is.EqualTo(Challenges.DailyCount));
            var groups = new HashSet<string>();
            foreach (var id in ids) Assert.That(groups.Add(Challenges.Find(id).Group), Is.True, "one per group: " + string.Join(", ", ids));
            ProfileStore.Reset();
            Challenges.Current();
            Assert.That(Challenges.Current().Daily.ConvertAll(c => c.Id), Is.EqualTo(ids), "the same day, the same three");
            var seen = new HashSet<string>(ids);
            int changed = 0;
            for (int d = 1; d <= 5; d++)
            {
                _now = _now.AddDays(1);
                foreach (var c in Challenges.Current().Daily) if (seen.Add(c.Id)) changed++;
            }
            Assert.That(changed, Is.GreaterThan(4), "new days bring new challenges");
            Assert.That(Challenges.Current().Weekly.Id, Is.Not.Null.And.Not.Empty);
        }

        [Test] public void AMatchCountsAndPaysOnce()
        {
            var r = Rewards.Match(Big());
            var s = Challenges.Current();
            int done = s.Daily.FindAll(c => c.Done).Count;
            Assert.That(done, Is.GreaterThan(0), "a big win finishes something: " + string.Join(", ", s.Daily.ConvertAll(c => c.Id + " " + c.Progress)));
            Assert.That(r.Challenges.Count, Is.GreaterThanOrEqualTo(done));
            Assert.That(r.Lines.Exists(l => l.StartsWith("Challenge done")), Is.True);
            int xp = ProfileStore.Current.Progress.Xp;
            var again = Rewards.Match(new MatchFacts { Won = false, GamesToWin = 1, Rallies = 1, Games = new List<int[]>() });
            Assert.That(again.Lines.FindAll(l => l.StartsWith("Challenge done")).Count, Is.EqualTo(0), "a finished challenge pays once");
            Assert.That(ProfileStore.Load().Progress.Challenges.DailiesDone, Is.EqualTo(s.DailiesDone), "saved");
        }

        [Test] public void TheStreakGrowsDayByDayAndBreaksOnAMissedDay()
        {
            var r = new RewardResult();
            for (int d = 0; d < 3; d++)
            {
                r = Rewards.Match(Big());
                Assert.That(Challenges.StreakNow(), Is.EqualTo(d + 1), "day " + (d + 1));
                _now = _now.AddDays(1);
            }
            Assert.That(Rewards.Owns(Challenges.StreakItems[0]), Is.True, "3 days: the first streak item");
            Assert.That(Challenges.StreakBonus(3), Is.EqualTo(0.2).Within(1e-9));
            _now = _now.AddDays(1);                                   // a day missed
            Assert.That(Challenges.StreakNow(), Is.EqualTo(0));
            Rewards.Match(Big());
            Assert.That(Challenges.StreakNow(), Is.EqualTo(1), "a new streak");
            Assert.That(Challenges.Current().BestStreak, Is.EqualTo(3));
        }

        [Test] public void TheWeeklyPaysXpAndAnItem()
        {
            var s = Challenges.Current();
            var d = Challenges.Find(s.Weekly.Id);
            s.Weekly.Progress = d.Target - 1;
            var e = new ChallengeEvent { Match = Big(), ArcadeStars = 10, GauntletFloor = 10 };
            var r = new RewardResult();
            Challenges.Modes.Add("gauntlet");
            Challenges.Record(r, e);
            Challenges.Modes.Remove("gauntlet");
            Assert.That(s.Weekly.Done, Is.True, d.Id);
            Assert.That(r.Lines.Exists(l => l.StartsWith("Weekly done") && l.EndsWith("+" + Challenges.WeeklyXp)), Is.True);
            Assert.That(r.Unlocked.ConvertAll(i => i.Id), Does.Contain(Challenges.WeeklyItems[0]));
        }

        [Test] public void EveryChallengeItemIsInTheCatalogue()
        {
            foreach (var id in Challenges.StreakItems) Assert.That(Cosmetics.Find(id)?.Unlock, Is.EqualTo(UnlockKind.Challenge), id);
            foreach (var id in Challenges.WeeklyItems) Assert.That(Cosmetics.Find(id)?.Unlock, Is.EqualTo(UnlockKind.Challenge), id);
            foreach (var d in Challenges.All()) Assert.That(d.Describe(), Does.Not.Contain("{"), d.Id);
        }

        [Test] public void AVersion2ProfileGetsChallenges()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":2,\"Progress\":{\"Xp\":500}}");
            ProfileStore.Reset();
            var p = ProfileStore.Current;
            Assert.That(p.Version, Is.EqualTo(3));
            Assert.That(p.Progress.Xp, Is.EqualTo(500));
            Assert.That(p.Progress.Challenges, Is.Not.Null);
            Assert.That(Challenges.Current().Daily.Count, Is.EqualTo(Challenges.DailyCount));
        }
    }
}
