// ===========================================================
// Assets/SquashSim.Testing/Editor/LabParityTests.cs
// Runs every golden replay (Tools/SimParity/Replays/*.ssreplay) inside
// the Unity Editor, i.e. on Unity's Mono runtime rather than .NET 8.
// Passing here means SquashSim.Core produces the HTML lab's simulation
// bit for bit on a second runtime/JIT - the property rollback netcode
// and server-verified results depend on.
//
// Regenerate the replays: see Tools/README.md.
// ===========================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SquashSim.Testing;
using UnityEngine;

namespace SquashSim.Tests
{
    public class LabParityTests
    {
        static string ReplayDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "SimParity", "Replays")); }
        }

        static IEnumerable<string> ReplayNames()
        {
            if (!Directory.Exists(ReplayDir)) return new[] { "<missing replay folder>" };
            return Directory.GetFiles(ReplayDir, "*.ssreplay")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, System.StringComparer.Ordinal);
        }

        [Test]
        public void ReplaysExist()
        {
            Assert.That(Directory.Exists(ReplayDir), "Missing " + ReplayDir);
            Assert.That(Directory.GetFiles(ReplayDir, "*.ssreplay").Length, Is.GreaterThan(0));
        }

        [TestCaseSource(nameof(ReplayNames))]
        public void ReplayIsBitIdentical(string name)
        {
            string path = Path.Combine(ReplayDir, name + ".ssreplay");
            Assert.That(File.Exists(path), "Missing replay " + path);

            Replay replay;
            using (var fs = File.OpenRead(path)) replay = Replay.Read(fs);
            ReplayResult result = ReplayRunner.Run(replay);

            Assert.That(result.Identical,
                name + ": diverged from the HTML lab at step " + result.FirstMismatch +
                " (t=" + (result.FirstMismatch * replay.Config.Sim.FixedDt).ToString("0.000") + "s)." +
                " Run Tools/SimParity on .NET to see which fields differ.");
            Debug.Log("[SimParity/Mono] " + name + ": " + result.Steps + " steps identical (" + result.Impacts +
                      " impacts, " + result.RacketContacts + " racket contacts)");
        }

        [Test]
        public void DMathTracksSystemMath()
        {
            // DMath is fdlibm: within 1 ulp of a good libm. This guards against a
            // transcription error, not against tiny differences (those are the point).
            var rng = new System.Random(1234);
            for (int i = 0; i < 20000; i++)
            {
                double x = (rng.NextDouble() - 0.5) * 40;
                Assert.That(DMath.Sin(x), Is.EqualTo(System.Math.Sin(x)).Within(1e-15));
                Assert.That(DMath.Cos(x), Is.EqualTo(System.Math.Cos(x)).Within(1e-15));
                double ax = rng.NextDouble() - 0.5;
                Assert.That(DMath.Atan2(x, ax), Is.EqualTo(System.Math.Atan2(x, ax)).Within(1e-15));
                double e = (rng.NextDouble() - 0.5) * 20;
                Assert.That(DMath.Exp(e), Is.EqualTo(System.Math.Exp(e)).Within(1e-12).Percent);
                double p = rng.NextDouble();
                Assert.That(DMath.Pow(p, 1.35), Is.EqualTo(System.Math.Pow(p, 1.35)).Within(1e-12).Percent);
            }
        }
    }
}
