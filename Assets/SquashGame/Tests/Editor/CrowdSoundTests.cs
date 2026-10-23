// ===========================================================
// Assets/SquashGame/Tests/Editor/CrowdSoundTests.cs
// PR4: the synthesised crowd - every take finite, loud enough, never
// clipped, the same every run; the bed loops without a click; the
// one-shots start and end in silence. Writes Captures/Sounds/crowd_*.wav.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Audio;

namespace SquashGame.Tests
{
    public class CrowdSoundTests
    {
        [Test] public void EveryCrowdTakeIsWellFormedAndWritten()
        {
            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Captures", "Sounds"));
            Directory.CreateDirectory(dir);
            foreach (var n in CrowdSounds.Names)
                for (int k = 0; k < CrowdSounds.Takes(n); k++)
                {
                    float[] s = CrowdSounds.Samples(n, k);
                    Assert.That(s.Length, Is.GreaterThan(CrowdSounds.Rate / 2), n);
                    float peak = 0;
                    foreach (var x in s) { Assert.That(float.IsNaN(x) || float.IsInfinity(x), Is.False, n); peak = System.Math.Max(peak, System.Math.Abs(x)); }
                    Assert.That(peak, Is.InRange(0.3f, 1.0f), n + ": loud enough, never clipped");
                    if (CrowdSounds.Loops(n))
                        Assert.That(System.Math.Abs(s[s.Length - 1] - s[0]), Is.LessThan(0.08f), n + ": loops without a click");
                    else
                    {
                        Assert.That(System.Math.Abs(s[0]), Is.LessThan(0.02f), n + ": starts from silence");
                        Assert.That(System.Math.Abs(s[s.Length - 1]), Is.LessThan(0.02f), n + ": ends in silence");
                    }
                    Assert.That(CrowdSounds.Samples(n, k), Is.EqualTo(s), n + ": the same every run");
                    SoundTests.Wav(Path.Combine(dir, "crowd_" + n + "_" + k + ".wav"), s);
                }
        }
    }
}
