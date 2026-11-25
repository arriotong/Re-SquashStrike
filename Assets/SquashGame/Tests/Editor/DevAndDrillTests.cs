// ===========================================================
// Assets/SquashGame/Tests/Editor/DevAndDrillTests.cs
// 2026-11-25:
//   - the developer's code: only the right one turns it on; it tops the
//     coins up, survives a reload, and turns off again
//   - the Academy's match drills: the chapter is there, each drill counts
//     what it asks for (from what the match shows) and nothing else
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashGame.UI;
using SquashSim.Academy;

namespace SquashGame.Tests
{
    public class DevAndDrillTests
    {
        [SetUp]
        public void Fresh()
        {
            var dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeDev");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            ProfileStore.Folder = dir;
            ProfileStore.Reset();
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); }

        [Test]
        public void OnlyTheCodeTurnsDevModeOn()
        {
            foreach (var wrong in new[] { "", "0000", "9080", "0908x", "arrio", null }) Assert.That(DevMode.TryCode(wrong), Is.False, "'" + wrong + "'");
            Assert.That(DevMode.ByCode, Is.False);
            Assert.That(DevMode.TryCode(" 0908 "), Is.True);
            Assert.That(DevMode.On, Is.True);
            Assert.That(GearDrops.Wallet.Coins, Is.GreaterThanOrEqualTo(DevMode.Coins), "unlimited coins");
            GearDrops.Wallet.Coins = 5;
            Assert.That(GearDrops.Wallet.Coins, Is.GreaterThanOrEqualTo(DevMode.Coins), "topped up again");
            ProfileStore.Reset();
            Assert.That(DevMode.On, Is.True, "kept with the profile");
            Assert.That(DevMode.UnlockEverything(), Is.GreaterThan(100));
            Assert.That(Machines.TierOf(Machines.Find(Machines.ProCardsId).Items[0].Id), Is.EqualTo(3), "pro cards at Holo");
            Assert.That(Machines.LevelOf("pk_gel"), Is.EqualTo(3), "perks at level 3");
            ProfileStore.Current.Dev.FullMeter = true;
            Assert.That(DevMode.FullMeter, Is.True);
            DevMode.TurnOff();
            Assert.That(DevMode.ByCode, Is.False);
            Assert.That(DevMode.FullMeter, Is.False);
        }

        [Test]
        public void MatchSkillsChapter()
        {
            var drills = AcademyScreen.InChapter(Chapter.Match);
            Assert.That(drills.Count, Is.EqualTo(5));
            foreach (var l in drills)
            {
                Assert.That(l.IsDrill, Is.True);
                Assert.That(l.Steps.Count, Is.EqualTo(1));
                var s = MatchDrills.SettingsFor(l);
                Assert.That(s.Drill, Is.EqualTo(l.Id));
                Assert.That(s.Mods.Exists(m => m.Id == "special") && s.Mods.Exists(m => m.Id == "practice"), Is.True, "a full meter every rally");
                Assert.That(s.OppMods != null, Is.EqualTo(l.Id == "duel"), "only the duel's opponent has a Special Shot");
            }
            Assert.That(Lessons.Find("calls"), Is.Not.Null, "the rules lesson on nicks and the video");
        }

        static MatchDrills.Observation O(bool focus = false, int dives = 0, int specialBy = -1, int defended = 0, int shots = 0, int last = -1, double x = 0, double z = 5)
        {
            return new MatchDrills.Observation { Focusing = focus, Dives = dives, SpecialBy = specialBy, SpecialsDefended = defended, Shots = shots, LastStriker = last, X = x, Z = z, Me = 0 };
        }

        [Test]
        public void DrillsCountWhatTheyAskFor()
        {
            // Focus: each time it's taken
            var t = new MatchDrills.Tracker(Lessons.Find("focus"));
            t.See(O()); t.See(O(focus: true)); t.See(O(focus: true)); t.See(O()); t.See(O(focus: true)); t.See(O(dives: 1));
            Assert.That(t.Progress, Is.EqualTo(2));
            // the dive
            t = new MatchDrills.Tracker(Lessons.Find("smartdive"));
            t.See(O()); t.See(O(focus: true)); t.See(O(dives: 1)); t.See(O(dives: 1)); t.See(O(dives: 2));
            Assert.That(t.Passed, Is.True);
            // the Special Shot: yours, not theirs
            t = new MatchDrills.Tracker(Lessons.Find("special"));
            t.See(O()); t.See(O(specialBy: 1)); t.See(O()); t.See(O(specialBy: 0)); t.See(O(specialBy: 0));
            Assert.That(t.Progress, Is.EqualTo(1));
            // the duel: their Special Shot defended - not yours defended by them
            t = new MatchDrills.Tracker(Lessons.Find("duel"));
            t.See(O()); t.See(O(specialBy: 0, shots: 2)); t.See(O(specialBy: 0, defended: 1, shots: 3));
            Assert.That(t.Progress, Is.Zero, "yours, defended by them");
            t.See(O(shots: 0)); t.See(O(specialBy: 1, defended: 1, shots: 2)); t.See(O(specialBy: 1, defended: 2, shots: 3));
            Assert.That(t.Passed, Is.True, "theirs, defended by you");
            // the T: on it as they strike
            t = new MatchDrills.Tracker(Lessons.Find("thet"));
            t.See(O(shots: 0));
            t.See(O(shots: 1, last: 0, x: 0.2, z: 5.1));                 // your own shot: not counted
            t.See(O(shots: 2, last: 1, x: 0.3, z: 5.2));                 // they hit, you're on it
            t.See(O(shots: 3, last: 0, x: 2.5, z: 8.0));
            t.See(O(shots: 4, last: 1, x: 2.5, z: 8.0));                 // they hit, you're in the corner
            Assert.That(t.Progress, Is.EqualTo(1));
            Assert.That(t.Note, Is.EqualTo("GET BACK TO THE T"));
        }
    }
}
