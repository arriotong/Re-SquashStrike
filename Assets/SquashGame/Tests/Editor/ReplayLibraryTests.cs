// ===========================================================
// Assets/SquashGame/Tests/Editor/ReplayLibraryTests.cs
// P2-M6 R3: the replay library on disk - the last 20 are kept, pinned
// ones survive, a living career's replays survive (outside the 20),
// deleting works, the index is read back, a card's facts are right.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Replays;
using SquashSim.Replays;

namespace SquashGame.Tests
{
    public class ReplayLibraryTests
    {
        string _folder;

        [SetUp] public void Fresh()
        {
            _folder = Path.Combine(Path.GetTempPath(), "reSquashStrikeReplayLib");
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            Directory.CreateDirectory(_folder);
            ProfileStore.Folder = _folder;
            ProfileStore.Reset();
            CareerStore.Reset();
            ReplayLibrary.Reset();
        }

        [TearDown] public void Done() { CareerStore.Reset(); ProfileStore.Reset(); ReplayLibrary.Reset(); ProfileStore.Folder = null; }

        static ReplayFile Match(int i, string career = null)
        {
            var f = new ReplayFile { CreatedTicks = new System.DateTime(2026, 10, 19).Ticks + i * 10_000_000L, Winner = i % 2, TotalSteps = 1000 };
            f.Rallies.Add(new RallyRec { EndStep = 500, Winner = i % 2, Points0 = 11, Points1 = 7, Games0 = 1, Games1 = 0, Shots = 25 });
            f.Meta["mode"] = career != null ? "career" : "quick"; f.Meta["p1"] = "YOU"; f.Meta["opponent"] = "Attacker (Hard)";
            if (career != null) f.Meta["career"] = career;
            return f;
        }

        [Test] public void TheLastTwentyArePinnedOnesAndACareersKept()
        {
            var first = ReplayLibrary.Add(Match(0));
            ReplayLibrary.Pin(first, true);
            CareerStore.New("Tester", "ENG", 2, false);
            string key = CareerStore.Current.S.Seed.ToString();
            var career = ReplayLibrary.Add(Match(1, key));
            for (int i = 2; i < 30; i++) ReplayLibrary.Add(Match(i));

            var all = ReplayLibrary.Entries;
            Assert.That(all.Count, Is.EqualTo(22), "20 + the pinned one + the career's");
            Assert.That(ReplayLibrary.Find(first.Id), Is.Not.Null, "pinned: kept");
            Assert.That(ReplayLibrary.Find(career.Id), Is.Not.Null, "the living career's: kept");
            Assert.That(all[0].Created, Is.GreaterThan(all[1].Created), "newest first");
            Assert.That(Directory.GetFiles(ReplayLibrary.Folder, "*" + ReplayLibrary.Extension).Length, Is.EqualTo(22), "the files follow the index");

            // the card's facts
            var e = all[0];
            Assert.That(e.Scores, Is.EqualTo("11-7"));
            Assert.That(e.Opponent, Is.EqualTo("Attacker (Hard)"));
            Assert.That(e.Tagged, Is.GreaterThan(0), "the rally is tagged");

            // read back from disk
            ReplayLibrary.Reset();
            Assert.That(ReplayLibrary.Entries.Count, Is.EqualTo(22));
            Assert.That(ReplayLibrary.Load(ReplayLibrary.Find(first.Id)).Rallies.Count, Is.EqualTo(1));

            // unpin: it is the oldest, so the autosave lets it go
            ReplayLibrary.Pin(ReplayLibrary.Find(first.Id), false);
            Assert.That(ReplayLibrary.Find(first.Id), Is.Null);
            // the career ends: its replay joins the 20 (and, oldest, goes) at the next save
            CareerStore.Delete();
            ReplayLibrary.Add(Match(40));
            Assert.That(ReplayLibrary.Find(career.Id), Is.Null);
            Assert.That(ReplayLibrary.Entries.Count, Is.EqualTo(ReplayLibrary.Keep));

            // delete
            var gone = ReplayLibrary.Latest;
            ReplayLibrary.Delete(gone);
            Assert.That(ReplayLibrary.Find(gone.Id), Is.Null);
            Assert.That(File.Exists(ReplayLibrary.PathOf(gone)), Is.False);
        }
    }
}
