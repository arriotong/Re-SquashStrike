// ===========================================================
// Assets/SquashGame/Runtime/Replays/ArcadeBests.cs
// P2-M6: your best play on each Target Arcade level, kept as its input
// log (the session is seeded, so the log plays it again exactly) in
// Replays/arcade/<challenge>.log. "Watch best" plays it back.
// The log keeps the exact numbers (no quantising): the arcade session
// steps on the raw intent, so that is what is stored.
// ===========================================================

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using SquashSim.Arcade;
using UnityEngine;

namespace SquashGame.Replays
{
    public static class ArcadeBests
    {
        const uint Magic = 0x4C415353;     // "SSAL"
        static string Folder { get { return Path.Combine(ReplayLibrary.Folder, "arcade"); } }
        static string PathOf(string id) { return Path.Combine(Folder, id + ".log"); }

        public static bool Has(string challengeId) { return !string.IsNullOrEmpty(challengeId) && File.Exists(PathOf(challengeId)); }

        /// <summary>Keep a play as this level's best (with its score, to check it plays back to the same).</summary>
        public static void Save(InputLog log, int score)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(PathOf(log.ChallengeId), ToBytes(log, score));
            }
            catch (System.Exception e) { Debug.LogWarning("[Replays] could not keep the arcade best: " + e.Message); }
        }

        public static InputLog Load(string challengeId, out int score)
        {
            score = 0;
            try { return FromBytes(File.ReadAllBytes(PathOf(challengeId)), out score); }
            catch (System.Exception e) { Debug.LogWarning("[Replays] could not read the arcade best: " + e.Message); return null; }
        }

        public static byte[] ToBytes(InputLog log, int score)
        {
            var raw = new MemoryStream();
            using (var w = new BinaryWriter(raw, Encoding.UTF8, true))
            {
                w.Write(log.ChallengeId ?? ""); w.Write(log.Seed); w.Write(score);
                // runs of identical frames
                var f = log.Frames;
                int i = 0;
                var runs = new List<(IntentFrame frame, int count)>();
                while (i < f.Count) { int j = i + 1; while (j < f.Count && Same(f[j], f[i])) j++; runs.Add((f[i], j - i)); i = j; }
                w.Write(runs.Count);
                foreach (var (fr, n) in runs) { w.Write(n); Write(w, fr); }
            }
            var file = new MemoryStream();
            using (var w = new BinaryWriter(file, Encoding.UTF8, true)) w.Write(Magic);
            using (var z = new DeflateStream(file, System.IO.Compression.CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(z); }
            return file.ToArray();
        }

        public static InputLog FromBytes(byte[] bytes, out int score)
        {
            var file = new MemoryStream(bytes);
            using (var h = new BinaryReader(file, Encoding.UTF8, true)) if (h.ReadUInt32() != Magic) throw new InvalidDataException("not an arcade log");
            var raw = new MemoryStream();
            using (var z = new DeflateStream(file, CompressionMode.Decompress)) z.CopyTo(raw);
            raw.Position = 0;
            using (var r = new BinaryReader(raw, Encoding.UTF8))
            {
                var log = new InputLog { ChallengeId = r.ReadString(), Seed = r.ReadUInt32() };
                score = r.ReadInt32();
                int runs = r.ReadInt32();
                for (int k = 0; k < runs; k++) { int n = r.ReadInt32(); var fr = Read(r); for (int i = 0; i < n; i++) log.Frames.Add(fr); }
                return log;
            }
        }

        static bool Same(IntentFrame a, IntentFrame b)
        {
            return a.MoveX == b.MoveX && a.MoveZ == b.MoveZ && a.MoveMagnitude == b.MoveMagnitude && a.AimX == b.AimX && a.AimY == b.AimY &&
                   a.AimRawX == b.AimRawX && a.AimRawY == b.AimRawY && a.HitSeconds == b.HitSeconds && a.HasAim == b.HasAim &&
                   a.HitPressed == b.HitPressed && a.HitReleased == b.HitReleased && a.HitHeld == b.HitHeld && a.TouchPressed == b.TouchPressed &&
                   a.TouchReleased == b.TouchReleased && a.TouchHeld == b.TouchHeld && a.Boast == b.Boast && a.Dive == b.Dive && a.Sprint == b.Sprint &&
                   a.SplitStep == b.SplitStep && a.Focus == b.Focus;
        }

        static void Write(BinaryWriter w, IntentFrame f)
        {
            w.Write(f.MoveX); w.Write(f.MoveZ); w.Write(f.MoveMagnitude); w.Write(f.AimX); w.Write(f.AimY); w.Write(f.AimRawX); w.Write(f.AimRawY); w.Write(f.HitSeconds);
            int b = (f.HasAim ? 1 : 0) | (f.HitPressed ? 2 : 0) | (f.HitReleased ? 4 : 0) | (f.HitHeld ? 8 : 0) | (f.TouchPressed ? 16 : 0) | (f.TouchReleased ? 32 : 0) |
                    (f.TouchHeld ? 64 : 0) | (f.Boast ? 128 : 0) | (f.Dive ? 256 : 0) | (f.Sprint ? 512 : 0) | (f.SplitStep ? 1024 : 0) | (f.Focus ? 2048 : 0);
            w.Write((ushort)b);
        }

        static IntentFrame Read(BinaryReader r)
        {
            var f = new IntentFrame
            {
                MoveX = r.ReadDouble(), MoveZ = r.ReadDouble(), MoveMagnitude = r.ReadDouble(), AimX = r.ReadDouble(), AimY = r.ReadDouble(),
                AimRawX = r.ReadDouble(), AimRawY = r.ReadDouble(), HitSeconds = r.ReadDouble(),
            };
            int b = r.ReadUInt16();
            f.HasAim = (b & 1) != 0; f.HitPressed = (b & 2) != 0; f.HitReleased = (b & 4) != 0; f.HitHeld = (b & 8) != 0; f.TouchPressed = (b & 16) != 0;
            f.TouchReleased = (b & 32) != 0; f.TouchHeld = (b & 64) != 0; f.Boast = (b & 128) != 0; f.Dive = (b & 256) != 0; f.Sprint = (b & 512) != 0;
            f.SplitStep = (b & 1024) != 0; f.Focus = (b & 2048) != 0;
            return f;
        }
    }
}
