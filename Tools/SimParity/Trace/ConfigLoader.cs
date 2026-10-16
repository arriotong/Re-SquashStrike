// ===========================================================
// Tools/SimParity/Trace/ConfigLoader.cs
// Maps the lab's PhysicsConfig JSON (as recorded in a trace) onto
// SimConfig, and checks the mapping is COMPLETE: every JS key must land
// on a C# field (except lab-only blocks), and every number must arrive.
// Also diffs a config against SimConfig.Defaults().
// ===========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SquashSim;

namespace SimParity
{
    public static class ConfigLoader
    {
        /// <summary>JS config keys with no C# counterpart, on purpose.</summary>
        static readonly HashSet<string> IgnoredPaths = new HashSet<string>
        {
            "debug"   // lab-only visualisation toggles; the game has its own debug overlay
        };

        static FieldInfo FindField(Type t, string jsonKey)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (string.Equals(f.Name, jsonKey, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        /// <summary>Build a SimConfig from JSON; returns the list of unmapped keys.</summary>
        public static SimConfig Load(JsonElement json, List<string> problems)
        {
            var cfg = SimConfig.Defaults();
            Fill(cfg, json, "", problems);
            return cfg;
        }

        static void Fill(object target, JsonElement obj, string path, List<string> problems)
        {
            Type t = target.GetType();
            foreach (var prop in obj.EnumerateObject())
            {
                string p = path.Length == 0 ? prop.Name : path + "." + prop.Name;
                if (IgnoredPaths.Contains(p)) continue;
                FieldInfo f = FindField(t, prop.Name);
                if (f == null) { problems.Add("JS key has no C# field: " + p); continue; }
                f.SetValue(target, Convert(f.FieldType, f.GetValue(target), prop.Value, p, problems));
            }
        }

        static object Convert(Type type, object existing, JsonElement v, string path, List<string> problems)
        {
            if (type == typeof(double))
            {
                if (v.ValueKind == JsonValueKind.Null) return double.PositiveInfinity;   // JSON wrote Infinity as null
                return v.GetDouble();
            }
            if (type == typeof(int)) return v.GetInt32();
            if (type == typeof(bool)) return v.GetBoolean();
            if (type == typeof(string)) return v.ValueKind == JsonValueKind.Null ? null : v.GetString();
            if (type.IsArray)
            {
                Type et = type.GetElementType();
                var arr = Array.CreateInstance(et, v.GetArrayLength());
                int i = 0;
                foreach (var e in v.EnumerateArray())
                {
                    object item = Activator.CreateInstance(et);
                    Fill(item, e, path + "[" + i + "]", problems);
                    arr.SetValue(item, i++);
                }
                return arr;
            }
            object target = existing ?? Activator.CreateInstance(type);
            Fill(target, v, path, problems);
            return target;
        }

        /// <summary>Field-by-field difference between two configs (exact comparison).</summary>
        public static void Diff(object a, object b, string path, List<string> diffs)
        {
            if (a == null || b == null)
            {
                if (a != b) diffs.Add(path + ": " + Fmt(a) + " vs " + Fmt(b));
                return;
            }
            Type t = a.GetType();
            if (t == typeof(double) || t == typeof(int) || t == typeof(bool) || t == typeof(string))
            {
                if (!a.Equals(b)) diffs.Add(path + ": C# " + Fmt(a) + " vs JS " + Fmt(b));
                return;
            }
            if (t.IsArray)
            {
                var aa = (Array)a; var bb = (Array)b;
                if (aa.Length != bb.Length) { diffs.Add(path + ": length " + aa.Length + " vs " + bb.Length); return; }
                for (int i = 0; i < aa.Length; i++) Diff(aa.GetValue(i), bb.GetValue(i), path + "[" + i + "]", diffs);
                return;
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Diff(f.GetValue(a), f.GetValue(b), path.Length == 0 ? f.Name : path + "." + f.Name, diffs);
        }

        static string Fmt(object o)
        {
            if (o == null) return "null";
            if (o is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            return o.ToString();
        }
    }
}
