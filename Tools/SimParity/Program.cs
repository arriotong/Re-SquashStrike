// ===========================================================
// Tools/SimParity/Program.cs
// Replays every golden trace recorded from the HTML lab through
// SquashSim.Core and checks each fixed step is BIT-IDENTICAL.
//
//   dotnet run --project Tools/SimParity -c Release -- [--traces <dir>] [--only <name>]
//                                                     [--export-replays <dir>]
//
// Exit code 0 = every scenario identical; 1 = a mismatch or config gap.
//
// Per scenario it checks:
//   1. the trace's config maps completely onto SimConfig (no unknown keys)
//   2. for scenarios without overrides, SimConfig.Defaults() == the lab's defaults
//   3. every step's frame hash matches; on the first mismatch it runs on to
//      the next full frame and prints exactly which fields differ.
// ===========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using SquashSim;
using SquashSim.Lab;
using SquashSim.Testing;

namespace SimParity
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            string traces = Arg(args, "--traces") ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Traces");
            if (!Directory.Exists(traces)) traces = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "SimParity", "Traces");
            string only = Arg(args, "--only");
            string exportDir = Arg(args, "--export-replays");
            if (exportDir != null) Directory.CreateDirectory(exportDir);

            var files = Directory.GetFiles(traces, "*.json.gz").OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (files.Count == 0) { Console.Error.WriteLine("No traces in " + Path.GetFullPath(traces)); return 1; }

            Console.WriteLine("SimParity - replaying " + files.Count + " lab traces from " + Path.GetFullPath(traces));
            Console.WriteLine();

            int failed = 0, passed = 0;
            long totalSteps = 0;
            var sw = Stopwatch.StartNew();
            foreach (string file in files)
            {
                string name = Path.GetFileName(file).Replace(".json.gz", "");
                if (only != null && name != only) continue;
                bool ok = RunTrace(file, exportDir, out int steps);
                totalSteps += steps;
                if (ok) passed++; else failed++;
            }
            Console.WriteLine();
            Console.WriteLine((failed == 0 ? "PASS" : "FAIL") + ": " + passed + " identical, " + failed + " failed, "
                + totalSteps + " steps compared in " + sw.ElapsedMilliseconds + " ms");
            return failed == 0 ? 0 : 1;
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static JsonDocument ReadTrace(string file)
        {
            using (var fs = File.OpenRead(file))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                gz.CopyTo(ms);
                return JsonDocument.Parse(ms.ToArray());
            }
        }

        static bool RunTrace(string file, string exportDir, out int stepsRun)
        {
            stepsRun = 0;
            using (JsonDocument doc = ReadTrace(file))
            {
                JsonElement t = doc.RootElement;
                string name = t.GetProperty("name").GetString();
                string kind = t.GetProperty("kind").GetString();

                // ---- 1 + 2: config ----
                var problems = new List<string>();
                SimConfig cfg = ConfigLoader.Load(t.GetProperty("config"), problems);
                if (!HasConfigOverride(name))
                {
                    var diffs = new List<string>();
                    ConfigLoader.Diff(SimConfig.Defaults(), cfg, "", diffs);
                    foreach (var d in diffs) problems.Add("default differs: " + d);
                }
                if (problems.Count > 0)
                {
                    Console.WriteLine("  FAIL " + name + " - config");
                    foreach (var p in problems.Take(20)) Console.WriteLine("       " + p);
                    return false;
                }

                Replay replay = ToReplay(t, cfg);

                // ---- full frames by step (diagnostics only) ----
                var full = new Dictionary<int, JsonElement>();
                foreach (var e in t.GetProperty("full").EnumerateArray())
                    full[e[0].GetInt32()] = e[1];

                // ---- 3: replay every step ----
                SimConfig saved = PhysicsConfig.Current;
                try
                {
                    var stepper = new ReplayStepper(replay);
                    int firstMismatch = -1;
                    for (int step = 0; step < replay.Steps; step++)
                    {
                        List<object> frame = stepper.Step(step);
                        stepsRun++;
                        if (firstMismatch < 0 && Replay.ParseHash(Snapshot.Hash(frame)) != replay.Hashes[step]) firstMismatch = step;
                        if (firstMismatch >= 0 && full.TryGetValue(step, out JsonElement jsFrame))
                        {
                            Console.WriteLine("  FAIL " + name + " - first mismatch at step " + firstMismatch
                                + " (t=" + (firstMismatch * stepper.Dt).ToString("0.000") + "s); field diff at step " + step + ":");
                            var diffs = new List<string>();
                            CompareFrame(frame, jsFrame, "", diffs, kind);
                            foreach (var d in diffs.Take(25)) Console.WriteLine("       " + d);
                            if (diffs.Count == 0) Console.WriteLine("       (frames equal here; mismatch was transient - rerun recorder with --full-every 1)");
                            return false;
                        }
                    }
                    if (firstMismatch >= 0)
                    {
                        Console.WriteLine("  FAIL " + name + " - first mismatch at step " + firstMismatch + " (no later full frame)");
                        return false;
                    }

                    string extra = "";
                    if (exportDir != null)
                    {
                        // Write the binary replay, read it back, and prove the round trip still replays identically.
                        string path = Path.Combine(exportDir, name + ".ssreplay");
                        using (var fs = File.Create(path)) replay.Write(fs);
                        Replay back;
                        using (var fs = File.OpenRead(path)) back = Replay.Read(fs);
                        ReplayResult rr = ReplayRunner.Run(back);
                        if (!rr.Identical)
                        {
                            Console.WriteLine("  FAIL " + name + " - exported replay diverges at step " + rr.FirstMismatch);
                            return false;
                        }
                        extra = ", replay exported";
                    }

                    Console.WriteLine("  ok   " + name.PadRight(28) + " " + replay.Steps.ToString().PadLeft(5) + " steps identical"
                        + "  (" + stepper.Impacts + " impacts" + (kind == "lab" ? ", " + stepper.RacketContacts + " racket contacts" : "") + extra + ")");
                    return true;
                }
                finally
                {
                    PhysicsConfig.Current = saved;
                }
            }
        }

        static Replay ToReplay(JsonElement t, SimConfig cfg)
        {
            var r = new Replay
            {
                Name = t.GetProperty("name").GetString(),
                Kind = t.GetProperty("kind").GetString(),
                Preset = t.GetProperty("preset").GetString(),
                Repeat = t.GetProperty("repeat").GetBoolean(),
                Steps = t.GetProperty("steps").GetInt32(),
                Launch = ParseOverride(t.GetProperty("launchOverride")),
                Config = cfg,
                Hashes = t.GetProperty("hashes").EnumerateArray().Select(h => Replay.ParseHash(h.GetString())).ToArray()
            };
            if (r.Kind == "lab")
            {
                r.Inputs = new double[r.Steps * 5];
                r.Flags = new byte[r.Steps * 2];
                int s = 0;
                foreach (var row in t.GetProperty("inputs").EnumerateArray())
                {
                    for (int k = 0; k < 5; k++) r.Inputs[s * 5 + k] = row[k].GetDouble();
                    r.Flags[s * 2] = (byte)row[5].GetInt32();
                    r.Flags[s * 2 + 1] = (byte)row[6].GetInt32();
                    s++;
                }
            }
            return r;
        }

        // Scenarios whose recorded config intentionally differs from defaults.
        static bool HasConfigOverride(string name)
        {
            return name == "ball_spin_magnus" || name == "lab_free_noassist";
        }

        static LaunchOverride ParseOverride(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Null) return null;
            var o = new LaunchOverride();
            foreach (var p in e.EnumerateObject())
            {
                switch (p.Name)
                {
                    case "position": o.Position = V(p.Value); break;
                    case "spin": o.Spin = V(p.Value); break;
                    case "speed": o.Speed = p.Value.GetDouble(); break;
                    case "azimuthDeg": o.AzimuthDeg = p.Value.GetDouble(); break;
                    case "elevationDeg": o.ElevationDeg = p.Value.GetDouble(); break;
                    case "temperature": o.Temperature = p.Value.GetDouble(); break;
                    case "repeatIntervalSeconds": o.RepeatIntervalSeconds = p.Value.GetDouble(); break;
                    case "carryTemperature": o.CarryTemperature = p.Value.GetBoolean(); break;
                    default: throw new InvalidDataException("unknown launch override key " + p.Name);
                }
            }
            return o;
        }

        static V3 V(JsonElement e)
        {
            return new V3(e.GetProperty("x").GetDouble(), e.GetProperty("y").GetDouble(), e.GetProperty("z").GetDouble());
        }

        // ---------------- field-level diff for diagnostics ----------------

        static readonly string[][] LabNames =
        {
            new[] { "ball.pos.x","ball.pos.y","ball.pos.z","ball.vel.x","ball.vel.y","ball.vel.z","ball.omega.x","ball.omega.y","ball.omega.z","ball.temperature","ball.resting","ball.asleep","ball.floorSinceFrontWall","ball.floorBounces","ball.simTime" },
            new[] { "player.pos.x","player.pos.z","player.vel.x","player.vel.z","player.facing","player.speed","player.balance","player.swingSide","player.movementState","player.reachState","player.adjusting" },
            new[] { "aim.biasX","aim.biasY","aim.desiredYaw","aim.desiredPitch","aim.neutralPitch","aim.intendedWall" },
            new[] { "pred.valid","pred.time","pred.point.x","pred.point.y","pred.point.z","pred.vel.x","pred.vel.y","pred.vel.z","pred.distance","pred.bouncesBefore","pred.isVolley","pred.stepsSinceRefresh" },
            new[] { "assist.active","assist.travelUsed","assist.hasIdealSpot","assist.idealSpot.x","assist.idealSpot.z","assist.reachState","assist.balance","assist.quality","assist.correction.x","assist.correction.z" },
            new[] { "swing.state","swing.stroke","swing.mode","swing.holdSeconds","swing.prepQuality","swing.armedAge","swing.expectedStartIn","swing.buffered","swing.bufferedAge","swing.startIn","swing.rushed","swing.timingCorrection","swing.swingTime","swing.duration","swing.phase","swing.totalArc","swing.startAngle","swing.contactAngle","swing.contactHeight","swing.center.x","swing.center.z","swing.reach","swing.side","swing.pathYaw","swing.hasContacted","swing.cooldown","swing.plan.technique","swing.plan.headSpeed" },
            new[] { "ri.phase","ri.bodyYaw","ri.faceYaw","ri.facePitch","ri.pathYaw","ri.rangeScale","ri.faceLimited" },
            new[] { "racket.anchor.x","racket.anchor.y","racket.anchor.z","racket.headPos.x","racket.headPos.y","racket.headPos.z","racket.headVel.x","racket.headVel.y","racket.headVel.z","racket.faceNormal.x","racket.faceNormal.y","racket.faceNormal.z","racket.angle","racket.reach","racket.headSpeed","racket.assistApplied","racket.assistMiss" }
        };

        static string NameFor(string kind, string path)
        {
            // path like "[1][4]"
            var idx = path.Trim('[', ']').Split(new[] { "][" }, StringSplitOptions.None);
            if (idx.Length == 2 && int.TryParse(idx[0], out int a) && int.TryParse(idx[1], out int b))
            {
                if (kind == "ball" && a == 0 && b < LabNames[0].Length) return LabNames[0][b];
                if (kind == "lab" && a < LabNames.Length && b < LabNames[a].Length) return LabNames[a][b];
            }
            if (kind == "lab" && path == "[8]") return "interpretation.label";
            return "events" + path;
        }

        static void CompareFrame(object cs, JsonElement js, string path, List<string> diffs, string kind)
        {
            if (cs is List<object> list)
            {
                if (js.ValueKind != JsonValueKind.Array || js.GetArrayLength() != list.Count)
                {
                    diffs.Add(NameFor(kind, path) + ": C# list[" + list.Count + "] vs JS " + Short(js));
                    return;
                }
                for (int i = 0; i < list.Count; i++) CompareFrame(list[i], js[i], path + "[" + i + "]", diffs, kind);
                return;
            }
            bool equal;
            if (cs == null) equal = js.ValueKind == JsonValueKind.Null;
            else if (cs is double d) equal = js.ValueKind == JsonValueKind.Number && js.GetDouble() == d;
            else equal = js.ValueKind == JsonValueKind.String && js.GetString() == (string)cs;
            if (!equal)
            {
                string csText = cs is double dd ? dd.ToString("R") : (cs == null ? "null" : "\"" + cs + "\"");
                string extra = "";
                if (cs is double d1 && js.ValueKind == JsonValueKind.Number)
                {
                    double d2 = js.GetDouble();
                    long ulps = Math.Abs(BitConverter.DoubleToInt64Bits(d1) - BitConverter.DoubleToInt64Bits(d2));
                    extra = "  (" + ulps + " ulp, delta " + (d1 - d2).ToString("G3") + ")";
                }
                diffs.Add(NameFor(kind, path) + ": C# " + csText + " vs JS " + Short(js) + extra);
            }
        }

        static string Short(JsonElement e)
        {
            string s = e.GetRawText();
            return s.Length > 60 ? s.Substring(0, 60) + "..." : s;
        }
    }
}
