// ===========================================================
// Assets/SquashGame/Tests/Editor/BundledReplayTests.cs
// P2-M6: the replays shipped with the game (Resources/Replays, played
// behind the title until you have your own) still play exactly. When a
// tuning number changes they go stale: make them again with
//   dotnet run --project Tools/MatchBench -c Release -- --make-replays
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashGame.Tests
{
    public class BundledReplayTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheBundledReplaysAreCurrent()
        {
            var files = Directory.GetFiles("Assets/SquashGame/Resources/Replays", "*.bytes");
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(3));
            foreach (var path in files)
            {
                var f = ReplayFile.FromBytes(File.ReadAllBytes(path));
                Assert.That(f.Setup.Opponent0, Is.Not.Null, path + ": AI on both sides");
                Assert.That(f.Fingerprint, Is.EqualTo(SimFingerprint.Current), path + " is stale: run MatchBench --make-replays");
                var v = ReplaySim.Verify(f);
                Assert.That(v.Ok, Is.True, path + ": " + v + " - run MatchBench --make-replays");
                Assert.That(Highlights.Reel(f).Count, Is.GreaterThan(0), path + ": something to show");
            }
        }
    }
}
