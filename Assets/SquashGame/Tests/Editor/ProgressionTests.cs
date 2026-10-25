// ===========================================================
// Assets/SquashGame/Tests/Editor/ProgressionTests.cs
// P2-M5: XP, levels, the Locker Room's items and achievements.
//   - the XP curve, the cap, prestige
//   - every level 2..50 gives something; every reward is a real item;
//     ids are unique; free starters cover every kit slot
//   - a match's XP and achievements; near misses earn nothing
//   - counted achievements; an achievement is earned once
//   - a v1 profile upgrades keeping its settings and records
//   - the kit clash rule leaves two readable kits
// ===========================================================

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;

namespace SquashGame.Tests
{
    public class ProgressionTests
    {
        string _dir;

        [SetUp]
        public void Fresh()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeProgression");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
            Challenges.Enabled = false;               // exact XP here; ChallengeTests cover the challenges
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); Challenges.Enabled = true; }

        [Test] public void TheNextUnlockIsAlwaysInView()
        {
            // P3-R1: from any XP below the top there is a next unlock, at a higher level, with the XP to it
            foreach (int xp in new[] { 0, 150, 5000, Rewards.TotalFor(30) + 7 })
            {
                int level = Rewards.NextUnlock(xp, out var items, out int toGo);
                Assert.That(level, Is.GreaterThan(Rewards.LevelOf(xp)), "xp " + xp);
                Assert.That(items, Is.Not.Empty);
                Assert.That(toGo, Is.EqualTo(Rewards.TotalFor(level) - xp));
                Assert.That(Rewards.Describe(items), Does.Contain(items[0].Name));
            }
            Assert.That(Rewards.NextUnlock(Rewards.TotalFor(Rewards.MaxLevel), out _, out _), Is.EqualTo(0), "nothing left at the top");
            Assert.That(Rewards.SoFar(10, 6), Is.EqualTo(10 * Rewards.RallyXp + 6 * Rewards.RallyWonXp));
        }

        [Test] public void TheXpCurve()
        {
            Assert.That(Rewards.LevelOf(0), Is.EqualTo(1));
            Assert.That(Rewards.LevelOf(199), Is.EqualTo(1));
            Assert.That(Rewards.LevelOf(200), Is.EqualTo(2));
            Assert.That(Rewards.LevelOf(200 + 260), Is.EqualTo(3));
            Assert.That(Rewards.LevelOf(int.MaxValue / 4), Is.EqualTo(Rewards.MaxLevel), "capped");
            int top = Rewards.TotalFor(Rewards.MaxLevel);
            Assert.That(Rewards.Prestige(top - 1), Is.EqualTo(0));
            Assert.That(Rewards.Prestige(top + Rewards.Need(Rewards.MaxLevel) * 2), Is.EqualTo(2));
            Debug.Log("[P2M5] XP to level 50: " + top + " (level 2: 200, level 10: " + Rewards.TotalFor(10) + ", level 25: " + Rewards.TotalFor(25) + ")");
        }

        [Test] public void EveryLevelGivesSomethingAndEveryRewardIsReal()
        {
            var ids = new HashSet<string>();
            foreach (var it in Cosmetics.All()) Assert.That(ids.Add(it.Id), Is.True, "unique id " + it.Id);
            for (int l = 2; l <= Rewards.MaxLevel; l++) Assert.That(Cosmetics.AtLevel(l).Count, Is.GreaterThan(0), "level " + l + " gives an item");
            foreach (var a in Achievements.All())
                if (a.Reward != null)
                {
                    var it = Cosmetics.Find(a.Reward);
                    Assert.That(it, Is.Not.Null, a.Id + "'s reward " + a.Reward);
                    Assert.That(it.Unlock, Is.EqualTo(UnlockKind.Achievement));
                    Assert.That(it.Achievement, Is.EqualTo(a.Id));
                }
            foreach (var it in Cosmetics.All())
                if (it.Unlock == UnlockKind.Achievement) Assert.That(Achievements.Find(it.Achievement), Is.Not.Null, it.Id + " is some achievement's reward");
            var l0 = new Loadout();
            foreach (Slot s in System.Enum.GetValues(typeof(Slot)))
            {
                var worn = Cosmetics.Find(l0.Get(s));
                Assert.That(worn, Is.Not.Null, "the default " + s);
                Assert.That(worn.Unlock, Is.EqualTo(UnlockKind.Free), "the default " + s + " is free");
            }
            Debug.Log("[P2M5] " + Cosmetics.All().Count + " items, " + Achievements.All().Count + " achievements");
        }

        static MatchFacts Won(int longest)
        {
            return new MatchFacts { Won = true, GamesToWin = 1, Rallies = 20, RalliesWon = 11, LongestRally = longest, GreatHits = 2, MostGreatHitsInAGame = 2, Games = { new[] { 11, 9 } } };
        }

        [Test] public void AMatchGivesXpAndAchievementsButNotNearMisses()
        {
            var r = Rewards.Match(Won(29));
            int xp = 20 * Rewards.RallyXp + 11 * Rewards.RallyWonXp + 2 * Rewards.GreatHitXp + Rewards.MatchWonXp;
            var ids = r.Achieved.ConvertAll(a => a.Id);
            Assert.That(ids, Does.Contain("first_win"));
            Assert.That(ids, Does.Contain("rally10"));
            Assert.That(ids, Does.Not.Contain("rally30"), "29 shots is not 30");
            Assert.That(ids, Does.Contain("first_great"));
            int achXp = 0; foreach (var a in r.Achieved) achXp += a.Xp;
            Assert.That(r.Xp, Is.EqualTo(xp + achXp));
            Assert.That(ProfileStore.Current.Progress.Stats.Quick.Won, Is.EqualTo(1));
            Assert.That(ProfileStore.Load().Progress.Xp, Is.EqualTo(r.Xp), "saved");

            var again = Rewards.Match(Won(30));
            var ids2 = again.Achieved.ConvertAll(a => a.Id);
            Assert.That(ids2, Does.Not.Contain("first_win"), "earned once");
            Assert.That(ids2, Does.Contain("rally30"));
            Assert.That(again.Unlocked.ConvertAll(i => i.Id), Does.Contain("title_marathon"), "its reward");
            Assert.That(Rewards.Owns("title_marathon"), Is.True);
        }

        [Test] public void TheRefereesCallsCount()
        {
            // P2-M7: "Fair play", "Stroke of genius", and calls for / against in the lifetime stats
            var f = new MatchFacts { Won = true, GamesToWin = 1, Lets = 2, Strokes = 1, NoLets = 1, LetsAgainst = 1, StrokesAgainst = 3, WonAfterLet = true, MatchPointOnAStroke = true };
            var got = Achievements.FromMatch(f);
            Assert.That(got, Does.Contain("fair_play"));
            Assert.That(got, Does.Contain("stroke_genius"));
            Assert.That(Achievements.FromMatch(new MatchFacts { Won = true, GamesToWin = 1 }), Has.No.Member("fair_play"));
            var s = new LifetimeStats();
            LifetimeStats.AddMatch(s, f); LifetimeStats.AddMatch(s, f);
            Assert.That(s.Lets, Is.EqualTo(4)); Assert.That(s.StrokesAgainst, Is.EqualTo(6)); Assert.That(s.NoLets, Is.EqualTo(2));
        }

        [Test] public void LevellingUpUnlocksTheLevelTable()
        {
            var r = Rewards.Match(new MatchFacts { Rallies = 100, RalliesWon = 60, GamesToWin = 1 });       // 200 + 180 XP: level 2
            Assert.That(r.FromLevel, Is.EqualTo(1));
            Assert.That(r.ToLevel, Is.EqualTo(2));
            foreach (var it in Cosmetics.AtLevel(2)) Assert.That(r.Unlocked, Does.Contain(it), "level 2 gives " + it.Id);
            foreach (var it in Cosmetics.AtLevel(3)) Assert.That(Rewards.Owns(it.Id), Is.False);
        }

        [Test] public void CountedAchievementsShowProgress()
        {
            for (int i = 0; i < 24; i++) Rewards.Match(new MatchFacts { Won = true, GamesToWin = 1, Rallies = 1, RalliesWon = 1 });
            Assert.That(Rewards.Done("wins25"), Is.False);
            Assert.That(Rewards.State("wins25").Progress, Is.EqualTo(24));
            var r = Rewards.Match(new MatchFacts { Won = true, GamesToWin = 1, Rallies = 1, RalliesWon = 1 });
            Assert.That(r.Achieved.ConvertAll(a => a.Id), Does.Contain("wins25"));
        }

        [Test] public void LocalVersusEarnsNoAchievements()
        {
            var f = Won(40); f.TwoPlayer = true;
            var r = Rewards.Match(f);
            Assert.That(r.Achieved, Is.Empty);
            Assert.That(ProfileStore.Current.Progress.Stats.Versus.Played, Is.EqualTo(1));
            Assert.That(ProfileStore.Current.Progress.Stats.MatchesWon, Is.EqualTo(0), "the lifetime 'you' totals skip shared-screen matches");
        }

        [Test] public void AVersion1ProfileUpgrades()
        {
            string v1 = "{\"Version\":1,\"Settings\":{\"Assisted\":true,\"AimSpeed\":1.4},\"Arcade\":[{\"Id\":\"timeattack-easy\",\"Best\":2650,\"Stars\":1,\"Plays\":3}],\"Academy\":{\"Done\":[\"move\"],\"Finished\":false,\"Suggested\":true}}";
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), v1);
            ProfileStore.Reset();
            var p = ProfileStore.Current;
            Assert.That(p.Version, Is.EqualTo(ProfileStore.CurrentVersion));
            Assert.That(p.Settings.Assisted, Is.True);
            Assert.That(p.Settings.AimSpeed, Is.EqualTo(1.4f).Within(1e-5));
            Assert.That(ProfileStore.Record("timeattack-easy").Best, Is.EqualTo(2650));
            Assert.That(p.Academy.Done, Does.Contain("move"));
            Assert.That(p.Progress, Is.Not.Null);
            Assert.That(p.Progress.Xp, Is.EqualTo(0));
            Assert.That(p.Progress.Loadout.Shirt, Is.EqualTo(new Loadout().Shirt));
        }

        [Test] public void TheClashRuleLeavesTwoReadableKits()
        {
            int changed = 0;
            foreach (var a in Cosmetics.InSlot(Slot.Shirt))
                foreach (var b in Cosmetics.InSlot(Slot.Shirt))
                {
                    var mine = new Loadout { Shirt = a.Id };
                    var theirs = new Loadout { Shirt = b.Id };
                    var after = Cosmetics.AvoidClash(mine, theirs);
                    if (after.Shirt != b.Id) changed++;
                    Assert.That(Cosmetics.Distance(Cosmetics.KitOf(mine).ShirtBody, Cosmetics.KitOf(after).ShirtBody), Is.GreaterThanOrEqualTo(Cosmetics.ClashDistance),
                        a.Id + " v " + b.Id + " -> " + after.Shirt);
                }
            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(Cosmetics.AvoidClash(new Loadout { Shirt = "shirt_red" }, new Loadout { Shirt = "shirt_royal" }).Shirt, Is.EqualTo("shirt_royal"), "no clash: no change");
        }
    }
}
