// ===========================================================
// Assets/SquashGame/Tests/Editor/SoundTests.cs
// The synthesised on-court sounds (v2): every take is well formed -
// finite, not clipped, starts and ends at silence, has a tail from the
// court - and is the same every run. Also writes each one to
// Captures/Sounds/<name>_<take>.wav to listen to outside the game.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Audio;

namespace SquashGame.Tests
{
    public class SoundTests
    {
        [Test] public void EveryTakeIsWellFormedAndWritten()
        {
            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Captures", "Sounds"));
            Directory.CreateDirectory(dir);
            foreach (var n in ProceduralSounds.Names)
            {
                int takes = ProceduralSounds.Varies(n) ? ProceduralSounds.Variants : 1;
                for (int v = 0; v < takes; v++)
                {
                    float[] s = ProceduralSounds.Samples(n, v);
                    Assert.That(s.Length, Is.GreaterThan(ProceduralSounds.Rate / 20), n);
                    float peak = 0;
                    foreach (var x in s) { Assert.That(float.IsNaN(x) || float.IsInfinity(x), Is.False, n); peak = System.Math.Max(peak, System.Math.Abs(x)); }
                    Assert.That(peak, Is.InRange(0.3f, 1.0f), n + ": loud enough, never clipped");
                    Assert.That(System.Math.Abs(s[s.Length - 1]), Is.LessThan(0.01f), n + ": ends in silence (no click)");
                    Assert.That(System.Math.Abs(s[0]), Is.LessThan(0.05f), n + ": starts from silence");
                    Assert.That(ProceduralSounds.Samples(n, v), Is.EqualTo(s), n + ": the same every run");
                    Wav(Path.Combine(dir, n + "_" + v + ".wav"), s);
                }
            }
            Assert.That(ProceduralSounds.Samples("front", 0), Is.Not.EqualTo(ProceduralSounds.Samples("front", 1)), "the takes differ");
            UnityEngine.Debug.Log("[Sound] written to " + dir);
        }

        [Test] public void TheMenuMusicLoops()
        {
            float[] m = MenuMusicTrack.Render();
            int frames = m.Length / 2;
            double seconds = (double)frames / MenuMusicTrack.Rate;
            UnityEngine.Debug.Log("[Sound] menu music: " + seconds.ToString("0.0") + " s loop");
            Assert.That(seconds, Is.InRange(60.0, 70.0), "32 bars at 118 BPM");
            float peak = 0; foreach (var x in m) { Assert.That(float.IsNaN(x), Is.False); peak = System.Math.Max(peak, System.Math.Abs(x)); }
            Assert.That(peak, Is.InRange(0.5f, 1.0f));
            // seamless: the end flows into the start (no step bigger than the music's own movement)
            float jump = System.Math.Abs(m[0] - m[m.Length - 2]);
            Assert.That(jump, Is.LessThan(0.2f), "the loop point does not click");
            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Captures", "Sounds"));
            Directory.CreateDirectory(dir);
            WavStereo(Path.Combine(dir, "menu_music.wav"), m, MenuMusicTrack.Rate);
        }

        static void WavStereo(string path, float[] s, int rate)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = s.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var x in s) w.Write((short)(System.Math.Max(-1f, System.Math.Min(1f, x)) * 32767));
            }
        }

        static void Wav(string path, float[] s)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int rate = ProceduralSounds.Rate, bytes = s.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var x in s) w.Write((short)(System.Math.Max(-1f, System.Math.Min(1f, x)) * 32767));
            }
        }
    }
}
