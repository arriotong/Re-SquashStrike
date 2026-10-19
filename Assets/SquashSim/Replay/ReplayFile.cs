// ===========================================================
// Assets/SquashSim/Replay/ReplayFile.cs
// P2-M6 Replay Theatre: a match as everything the sim was given.
//
//   header    magic, format version, game version, the sim fingerprint
//             (every tuning number), when, free-form meta (names, kits,
//             the career event - presentation only)
//   setup     opponent, difficulty, scoring, games, first server, the
//             bot seed - what the match was built from
//   commands  what the front end did between steps (next game now,
//             skip the knock-up), with the step they came before
//   tracks    each human athlete's quantised input, every step, run-
//             length encoded (input changes at the frame rate, the sim
//             steps at 240 Hz)
//   rallies   per rally: the step it ended on, the score, a hash of the
//             ball and both athletes - so a reader can prove it played
//             the same rally
//
// Written as magic + version, then the body Deflate-compressed.
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace SquashSim.Replays
{
    public sealed class ReplaySetup
    {
        public bool TwoPlayer;
        public string Opponent = "retriever";     // a personality, or "wallbot"
        public float Difficulty = 0.5f;           // as the match was given it (MatchSettings' float)
        public string Scoring = "par";
        public int GamesToWin = 1;
        public bool WarmUp;
        public uint BotSeed;
        public int FirstServer;
        /// <summary>An AI for athlete 0 too (a demo match: bundled replays, the title screen); null = a person.</summary>
        public string Opponent0;
        public float Difficulty0 = 0.5f;
    }

    public enum ReplayCommand : byte { NextGame = 1, SkipWarmUp = 2 }

    public struct ReplayCmd { public long Step; public ReplayCommand Kind; }

    /// <summary>One run of identical frames.</summary>
    public struct FrameRun { public QFrame Frame; public int Count; }

    /// <summary>One rally as it ended: when, the score after it, who won it, the state's hash - and what happened in it
    /// (for highlights, without playing it again).</summary>
    public sealed class RallyRec
    {
        public long EndStep;
        public int Winner;              // -1: a let
        public int Points0, Points1, Games0, Games1;
        public int Shots;
        public ulong Hash;
        // ---- what happened (RallyWatch) ----
        public long ServeStep;          // the step its serve set-up began (where a clip starts)
        public byte Kind;               // RallyEnd
        public double TopPace;          // m/s, the fastest shot
        public int TopPaceBy = -1;
        public bool Nick;
        public int Great0, Great1, Dives;
        public int LastStriker = -1;
        public string LastTech = "";

        public static RallyRec Of(MatchSimulation m)
        {
            var o = m.LastOutcome;
            return new RallyRec
            {
                EndStep = m.StepCount, Winner = o != null ? o.Winner : -1,
                Points0 = m.Score.Points[0], Points1 = m.Score.Points[1], Games0 = m.Score.Games[0], Games1 = m.Score.Games[1],
                Shots = m.ShotsPerRally.Count > 0 ? m.ShotsPerRally[m.ShotsPerRally.Count - 1] : 0,
                Hash = StateHash(m), Kind = (byte)(o != null ? (int)o.Kind : 0),
            };
        }

        public bool SameAs(RallyRec o)
        {
            return o != null && EndStep == o.EndStep && Winner == o.Winner && Points0 == o.Points0 && Points1 == o.Points1 &&
                   Games0 == o.Games0 && Games1 == o.Games1 && Hash == o.Hash;
        }

        /// <summary>FNV-1a over the exact bits of the ball and both athletes.</summary>
        public static ulong StateHash(MatchSimulation m)
        {
            ulong h = 14695981039346656037UL;
            void D(double v) { long b = BitConverter.DoubleToInt64Bits(v); for (int i = 0; i < 8; i++) { h ^= (byte)(b >> (8 * i)); h *= 1099511628211UL; } }
            D(m.Ball.Pos.X); D(m.Ball.Pos.Y); D(m.Ball.Pos.Z); D(m.Ball.Vel.X); D(m.Ball.Vel.Y); D(m.Ball.Vel.Z);
            for (int p = 0; p < 2; p++) { var a = m.Players[p].Player; D(a.Pos.X); D(a.Pos.Z); D(a.Speed); }
            D(m.Time);
            return h;
        }
    }

    public sealed class ReplayFile
    {
        public const uint Magic = 0x50525353;      // "SSRP"
        public const int FormatVersion = 1;

        public string GameVersion = "";
        public ulong Fingerprint;
        public long CreatedTicks;
        public readonly Dictionary<string, string> Meta = new Dictionary<string, string>();
        public ReplaySetup Setup = new ReplaySetup();
        public readonly List<ReplayCmd> Commands = new List<ReplayCmd>();
        /// <summary>Per athlete: its input runs, or null for an AI athlete.</summary>
        public readonly List<FrameRun>[] Tracks = new List<FrameRun>[2];
        public readonly List<RallyRec> Rallies = new List<RallyRec>();
        public long TotalSteps;
        public int Winner = -1;          // the match winner (-1: not finished)
        public bool Retired;             // left before the end (a career retirement, quitting)

        public string Get(string key, string fallback = "") { return Meta.TryGetValue(key, out string v) ? v : fallback; }

        /// <summary>How many steps a track holds.</summary>
        public static long Length(List<FrameRun> t) { long n = 0; if (t != null) foreach (var r in t) n += r.Count; return n; }

        // ---------------- write / read ----------------

        public byte[] ToBytes()
        {
            var raw = new MemoryStream();
            using (var w = new BinaryWriter(raw, Encoding.UTF8, true))
            {
                w.Write(GameVersion ?? ""); w.Write(Fingerprint); w.Write(CreatedTicks);
                w.Write(Meta.Count); foreach (var kv in Meta) { w.Write(kv.Key); w.Write(kv.Value ?? ""); }
                var s = Setup;
                w.Write(s.TwoPlayer); w.Write(s.Opponent ?? ""); w.Write(s.Difficulty); w.Write(s.Scoring ?? "par"); w.Write(s.GamesToWin);
                w.Write(s.WarmUp); w.Write(s.BotSeed); w.Write(s.FirstServer); w.Write(s.Opponent0 ?? ""); w.Write(s.Difficulty0);
                w.Write(Commands.Count); foreach (var c in Commands) { w.Write(c.Step); w.Write((byte)c.Kind); }
                for (int t = 0; t < 2; t++)
                {
                    var track = Tracks[t];
                    w.Write(track != null);
                    if (track == null) continue;
                    w.Write(track.Count);
                    foreach (var run in track) { w.Write(run.Count); WriteFrame(w, run.Frame); }
                }
                w.Write(Rallies.Count);
                foreach (var r in Rallies)
                {
                    w.Write(r.EndStep); w.Write(r.Winner); w.Write(r.Points0); w.Write(r.Points1); w.Write(r.Games0); w.Write(r.Games1); w.Write(r.Shots); w.Write(r.Hash);
                    w.Write(r.ServeStep); w.Write(r.Kind); w.Write(r.TopPace); w.Write(r.TopPaceBy); w.Write(r.Nick); w.Write(r.Great0); w.Write(r.Great1); w.Write(r.Dives);
                    w.Write(r.LastStriker); w.Write(r.LastTech ?? "");
                }
                w.Write(TotalSteps); w.Write(Winner); w.Write(Retired);
            }
            var file = new MemoryStream();
            using (var w = new BinaryWriter(file, Encoding.UTF8, true)) { w.Write(Magic); w.Write(FormatVersion); }
            using (var z = new DeflateStream(file, CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(z); }
            return file.ToArray();
        }

        static void WriteFrame(BinaryWriter w, QFrame f)
        {
            w.Write(f.MoveX); w.Write(f.MoveZ); w.Write(f.MoveMag); w.Write(f.RawX); w.Write(f.RawY); w.Write(f.AimX); w.Write(f.AimY);
            w.Write(f.HitMs); w.Write(f.TouchMs); w.Write(f.Flags);
        }

        static QFrame ReadFrame(BinaryReader r)
        {
            return new QFrame
            {
                MoveX = r.ReadInt16(), MoveZ = r.ReadInt16(), MoveMag = r.ReadInt16(), RawX = r.ReadInt16(), RawY = r.ReadInt16(),
                AimX = r.ReadInt16(), AimY = r.ReadInt16(), HitMs = r.ReadInt32(), TouchMs = r.ReadInt32(), Flags = r.ReadUInt16(),
            };
        }

        /// <summary>Read a file (throws InvalidDataException when it is not one, or a newer format).</summary>
        public static ReplayFile FromBytes(byte[] bytes)
        {
            var file = new MemoryStream(bytes);
            using (var h = new BinaryReader(file, Encoding.UTF8, true))
            {
                if (bytes.Length < 8 || h.ReadUInt32() != Magic) throw new InvalidDataException("not a replay");
                int version = h.ReadInt32();
                if (version > FormatVersion) throw new InvalidDataException("a newer replay format (" + version + ")");
            }
            var raw = new MemoryStream();
            using (var z = new DeflateStream(file, CompressionMode.Decompress)) z.CopyTo(raw);
            raw.Position = 0;
            var f = new ReplayFile();
            using (var r = new BinaryReader(raw, Encoding.UTF8))
            {
                f.GameVersion = r.ReadString(); f.Fingerprint = r.ReadUInt64(); f.CreatedTicks = r.ReadInt64();
                int n = r.ReadInt32(); for (int i = 0; i < n; i++) { string k = r.ReadString(); f.Meta[k] = r.ReadString(); }
                var s = f.Setup;
                s.TwoPlayer = r.ReadBoolean(); s.Opponent = r.ReadString(); s.Difficulty = r.ReadSingle(); s.Scoring = r.ReadString(); s.GamesToWin = r.ReadInt32();
                s.WarmUp = r.ReadBoolean(); s.BotSeed = r.ReadUInt32(); s.FirstServer = r.ReadInt32();
                s.Opponent0 = r.ReadString(); if (s.Opponent0.Length == 0) s.Opponent0 = null; s.Difficulty0 = r.ReadSingle();
                n = r.ReadInt32(); for (int i = 0; i < n; i++) f.Commands.Add(new ReplayCmd { Step = r.ReadInt64(), Kind = (ReplayCommand)r.ReadByte() });
                for (int t = 0; t < 2; t++)
                {
                    if (!r.ReadBoolean()) continue;
                    int runs = r.ReadInt32();
                    var track = new List<FrameRun>(runs);
                    for (int i = 0; i < runs; i++) { int c = r.ReadInt32(); track.Add(new FrameRun { Count = c, Frame = ReadFrame(r) }); }
                    f.Tracks[t] = track;
                }
                n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                    f.Rallies.Add(new RallyRec
                    {
                        EndStep = r.ReadInt64(), Winner = r.ReadInt32(), Points0 = r.ReadInt32(), Points1 = r.ReadInt32(), Games0 = r.ReadInt32(), Games1 = r.ReadInt32(),
                        Shots = r.ReadInt32(), Hash = r.ReadUInt64(),
                        ServeStep = r.ReadInt64(), Kind = r.ReadByte(), TopPace = r.ReadDouble(), TopPaceBy = r.ReadInt32(), Nick = r.ReadBoolean(),
                        Great0 = r.ReadInt32(), Great1 = r.ReadInt32(), Dives = r.ReadInt32(), LastStriker = r.ReadInt32(), LastTech = r.ReadString(),
                    });
                f.TotalSteps = r.ReadInt64(); f.Winner = r.ReadInt32(); f.Retired = r.ReadBoolean();
            }
            return f;
        }
    }

    /// <summary>A hash of every number the game's physics and AI run on (the game profile and GameTuning). A replay
    /// made under another fingerprint was played by different rules: it is flagged, and may not re-simulate.</summary>
    public static class SimFingerprint
    {
        static ulong? _current;

        /// <summary>The game's fingerprint (computed once).</summary>
        public static ulong Current
        {
            get
            {
                if (_current == null)
                {
                    var keep = PhysicsConfig.Current;
                    PhysicsConfig.ResetToDefaults();
                    PhysicsConfig.UseGameProfile();
                    _current = Of(PhysicsConfig.Current);
                    PhysicsConfig.Current = keep;
                }
                return _current.Value;
            }
        }

        /// <summary>The fingerprint of a config (and GameTuning's constants).</summary>
        public static ulong Of(SimConfig cfg)
        {
            var sb = new StringBuilder();
            Walk(sb, "cfg", cfg, 0);
            // reflection gives no order (and .NET and Mono differ): everything by name
            var types = typeof(GameTuning).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(types, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var t in types)
            {
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var f in fields) Value(sb, t.Name + "." + f.Name, f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null));
            }
            ulong h = 14695981039346656037UL;
            foreach (char c in sb.ToString()) { h ^= c; h *= 1099511628211UL; }
            return h;
        }

        static void Walk(StringBuilder sb, string path, object o, int depth)
        {
            if (o == null || depth > 6) { sb.Append(path).Append("=null;"); return; }
            var t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string || o is decimal) { Value(sb, path, o); return; }
            if (o is System.Collections.IEnumerable list) { int i = 0; foreach (var x in list) Walk(sb, path + "[" + i++ + "]", x, depth + 1); return; }
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var f in fields) Walk(sb, path + "." + f.Name, f.GetValue(o), depth + 1);
        }

        static void Value(StringBuilder sb, string path, object v)
        {
            sb.Append(path).Append('=');
            if (v is double d) sb.Append(BitConverter.DoubleToInt64Bits(d));
            else if (v is float f) sb.Append(BitConverter.DoubleToInt64Bits(f));
            else if (v is System.Collections.IEnumerable e && !(v is string)) { foreach (var x in e) Value(sb, "", x); }
            else sb.Append(v);
            sb.Append(';');
        }
    }
}
