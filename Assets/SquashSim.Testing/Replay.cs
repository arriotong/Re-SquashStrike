// ===========================================================
// Assets/SquashSim.Testing/Replay.cs
// A golden replay: scenario setup + config + per-step input + the frame
// fingerprint the HTML lab produced at every step.
//
// SimParity converts the lab's JSON traces into this compact binary form
// (.ssreplay) so it can be replayed anywhere SquashSim.Core runs -
// .NET 8, Unity's Mono editor, an IL2CPP player, a server - without a
// JSON library. Same replay + same checksums on every runtime and PC is
// the determinism guarantee rollback netcode depends on.
//
// Format (little-endian, BinaryWriter):
//   "SSRP" int32 version
//   string name, kind, preset; bool repeat; int32 steps
//   launch override: bool present, then 8 x (bool has, value)
//   config: int32 count, then entries (string path, byte type, value)
//   inputs (lab only): steps x (5 x double, 2 x byte)
//   hashes: steps x uint64
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SquashSim.Lab;

namespace SquashSim.Testing
{
    public sealed class Replay
    {
        public const int Version = 1;

        public string Name;
        public string Kind;            // "ball" | "lab"
        public string Preset;
        public bool Repeat;
        public int Steps;
        public LaunchOverride Launch;  // ball scenarios only, may be null
        public SimConfig Config;
        public double[] Inputs;        // steps * 5: moveX, moveZ, moveMagnitude, aimX, aimY
        public byte[] Flags;           // steps * 2: hit, touch (1 pressed, 2 released, 4 held)
        public ulong[] Hashes;

        public static ulong ParseHash(string hex) { return Convert.ToUInt64(hex, 16); }
        public static string FormatHash(ulong h) { return h.ToString("x16"); }

        // ---------------- write ----------------

        public void Write(Stream stream)
        {
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                w.Write(new[] { (byte)'S', (byte)'S', (byte)'R', (byte)'P' });
                w.Write(Version);
                w.Write(Name); w.Write(Kind); w.Write(Preset); w.Write(Repeat); w.Write(Steps);

                w.Write(Launch != null);
                if (Launch != null)
                {
                    WriteV(w, Launch.Position); WriteV(w, Launch.Spin);
                    WriteD(w, Launch.Speed); WriteD(w, Launch.AzimuthDeg); WriteD(w, Launch.ElevationDeg);
                    WriteD(w, Launch.Temperature); WriteD(w, Launch.RepeatIntervalSeconds);
                    w.Write(Launch.CarryTemperature.HasValue);
                    if (Launch.CarryTemperature.HasValue) w.Write(Launch.CarryTemperature.Value);
                }

                var entries = new List<KeyValuePair<string, object>>();
                Flatten(Config, "", entries);
                w.Write(entries.Count);
                foreach (var e in entries)
                {
                    w.Write(e.Key);
                    object v = e.Value;
                    if (v is double d) { w.Write((byte)0); w.Write(d); }
                    else if (v is int i) { w.Write((byte)1); w.Write(i); }
                    else if (v is bool b) { w.Write((byte)2); w.Write(b); }
                    else if (v is string s) { w.Write((byte)3); w.Write(s); }
                    else if (v is ArrayLength al) { w.Write((byte)9); w.Write(al.Length); }
                    else if (v == null) { w.Write((byte)8); }
                    else throw new InvalidDataException("unsupported config value at " + e.Key);
                }

                bool lab = Kind == "lab";
                for (int s = 0; s < Steps; s++)
                {
                    if (lab)
                    {
                        for (int k = 0; k < 5; k++) w.Write(Inputs[s * 5 + k]);
                        w.Write(Flags[s * 2]); w.Write(Flags[s * 2 + 1]);
                    }
                }
                for (int s = 0; s < Steps; s++) w.Write(Hashes[s]);
            }
        }

        static void WriteV(BinaryWriter w, V3 v)
        {
            w.Write(v != null);
            if (v != null) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        }

        static void WriteD(BinaryWriter w, double? d)
        {
            w.Write(d.HasValue);
            if (d.HasValue) w.Write(d.Value);
        }

        // ---------------- read ----------------

        public static Replay Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                byte[] magic = r.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'S' || magic[1] != 'S' || magic[2] != 'R' || magic[3] != 'P')
                    throw new InvalidDataException("not a .ssreplay file");
                int version = r.ReadInt32();
                if (version != Version) throw new InvalidDataException("unsupported replay version " + version);

                var rep = new Replay
                {
                    Name = r.ReadString(), Kind = r.ReadString(), Preset = r.ReadString(),
                    Repeat = r.ReadBoolean(), Steps = r.ReadInt32()
                };

                if (r.ReadBoolean())
                {
                    rep.Launch = new LaunchOverride
                    {
                        Position = ReadV(r), Spin = ReadV(r),
                        Speed = ReadD(r), AzimuthDeg = ReadD(r), ElevationDeg = ReadD(r),
                        Temperature = ReadD(r), RepeatIntervalSeconds = ReadD(r)
                    };
                    if (r.ReadBoolean()) rep.Launch.CarryTemperature = r.ReadBoolean();
                }

                rep.Config = SimConfig.Defaults();
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string path = r.ReadString();
                    byte type = r.ReadByte();
                    object value;
                    switch (type)
                    {
                        case 0: value = r.ReadDouble(); break;
                        case 1: value = r.ReadInt32(); break;
                        case 2: value = r.ReadBoolean(); break;
                        case 3: value = r.ReadString(); break;
                        case 8: value = null; break;
                        case 9: value = new ArrayLength(r.ReadInt32()); break;
                        default: throw new InvalidDataException("bad config entry type " + type);
                    }
                    SetPath(rep.Config, path, value);
                }

                bool lab = rep.Kind == "lab";
                if (lab)
                {
                    rep.Inputs = new double[rep.Steps * 5];
                    rep.Flags = new byte[rep.Steps * 2];
                    for (int s = 0; s < rep.Steps; s++)
                    {
                        for (int k = 0; k < 5; k++) rep.Inputs[s * 5 + k] = r.ReadDouble();
                        rep.Flags[s * 2] = r.ReadByte(); rep.Flags[s * 2 + 1] = r.ReadByte();
                    }
                }
                rep.Hashes = new ulong[rep.Steps];
                for (int s = 0; s < rep.Steps; s++) rep.Hashes[s] = r.ReadUInt64();
                return rep;
            }
        }

        static V3 ReadV(BinaryReader r) { return r.ReadBoolean() ? new V3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble()) : null; }
        static double? ReadD(BinaryReader r) { return r.ReadBoolean() ? r.ReadDouble() : (double?)null; }

        // ---------------- config <-> path entries ----------------
        // Paths use field NAMES, never reflection order, so writer and reader
        // agree across runtimes.

        sealed class ArrayLength { public readonly int Length; public ArrayLength(int n) { Length = n; } }

        static void Flatten(object obj, string prefix, List<KeyValuePair<string, object>> output)
        {
            foreach (FieldInfo f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string path = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
                object v = f.GetValue(obj);
                Type t = f.FieldType;
                if (t == typeof(double) || t == typeof(int) || t == typeof(bool) || t == typeof(string))
                {
                    output.Add(new KeyValuePair<string, object>(path, v));
                }
                else if (t.IsArray)
                {
                    var arr = (Array)v;
                    output.Add(new KeyValuePair<string, object>(path, new ArrayLength(arr.Length)));
                    for (int i = 0; i < arr.Length; i++) Flatten(arr.GetValue(i), path + "[" + i + "]", output);
                }
                else if (v != null)
                {
                    Flatten(v, path, output);
                }
            }
        }

        static void SetPath(object root, string path, object value)
        {
            string[] parts = path.Split('.');
            object obj = root;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int index = -1;
                int b = part.IndexOf('[');
                if (b >= 0)
                {
                    index = int.Parse(part.Substring(b + 1, part.Length - b - 2), System.Globalization.CultureInfo.InvariantCulture);
                    part = part.Substring(0, b);
                }
                FieldInfo f = obj.GetType().GetField(part, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) throw new InvalidDataException("unknown config path " + path);
                bool last = i == parts.Length - 1;

                if (index < 0 && last)
                {
                    if (value is ArrayLength al)
                    {
                        Type et = f.FieldType.GetElementType();
                        var arr = Array.CreateInstance(et, al.Length);
                        for (int k = 0; k < al.Length; k++) arr.SetValue(Activator.CreateInstance(et), k);
                        f.SetValue(obj, arr);
                    }
                    else f.SetValue(obj, value);
                    return;
                }
                object next = f.GetValue(obj);
                if (index >= 0) next = ((Array)next).GetValue(index);
                obj = next;
            }
        }
    }
}
