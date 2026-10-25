// ===========================================================
// Assets/SquashGame/Runtime/Profile/PlayerNames.cs
// P6 the name editor: your own names for the stars (any names - real
// ones too), in players.json next to the profile - on this PC only,
// never in the game's files. Set at startup into Stars.NameOverride, so
// every name follows: Quick Match, the career, the Gauntlet, the HUD.
//
//   Export   writes every renamed star to players-export.json, to share
//   Import   reads players-import.json (an export, renamed) over yours
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using SquashSim.Career;
using UnityEngine;

namespace SquashGame.Profile
{
    [Serializable]
    public sealed class PlayerNameEntry
    {
        public string Id, First, Last;
    }

    [Serializable]
    public sealed class PlayerNamesFile
    {
        public int Version = 1;
        public List<PlayerNameEntry> Players = new List<PlayerNameEntry>();
    }

    public static class PlayerNames
    {
        public const string FileName = "players.json", ExportName = "players-export.json", ImportName = "players-import.json";
        public const int MaxLength = 20;
        public static string Folder { get { return ProfileStore.Folder ?? Application.persistentDataPath; } }
        public static string ExportPath { get { return Path.Combine(Folder, ExportName); } }
        public static string ImportPath { get { return Path.Combine(Folder, ImportName); } }

        static PlayerNamesFile _f;
        static readonly Dictionary<string, PlayerNameEntry> _byId = new Dictionary<string, PlayerNameEntry>();

        /// <summary>Read players.json (if any) and hand the names to the stars.</summary>
        public static void Load()
        {
            _f = Read(Path.Combine(Folder, FileName)) ?? new PlayerNamesFile();
            Index();
            Stars.NameOverride = Lookup;
        }

        /// <summary>Forget what was loaded (tests: a new folder).</summary>
        public static void Reset() { _f = null; _byId.Clear(); Stars.NameOverride = null; }

        static PlayerNamesFile File { get { if (_f == null) Load(); return _f; } }

        static void Index()
        {
            _byId.Clear();
            foreach (var e in _f.Players) if (e != null && !string.IsNullOrEmpty(e.Id) && Stars.Find(e.Id) != null) _byId[e.Id] = e;
        }

        static (string first, string last)? Lookup(string id)
        {
            return _byId.TryGetValue(id, out var e) ? (e.First, e.Last) : ((string, string)?)null;
        }

        /// <summary>How many stars have your names.</summary>
        public static int Count { get { var _ = File; return _byId.Count; } }

        public static bool Renamed(string id) { var _ = File; return _byId.ContainsKey(id); }

        /// <summary>Rename a star (blank keeps that part as it was; the star's own name again: the rename is dropped).</summary>
        public static void Set(string id, string first, string last)
        {
            var star = Stars.Find(id);
            if (star == null) return;
            var now = Stars.Names(star);
            first = Clean(first, now.first); last = Clean(last, now.last);
            File.Players.RemoveAll(e => e.Id == star.Id);
            if (first != star.First || last != star.Last) File.Players.Add(new PlayerNameEntry { Id = star.Id, First = first, Last = last });
            Index();
            Save();
        }

        public static void ResetOne(string id) { var s = Stars.Find(id); if (s != null) Set(s.Id, s.First, s.Last); }

        static string Clean(string v, string keep)
        {
            v = (v ?? "").Trim();
            if (v.Length == 0) return keep;
            return v.Length > MaxLength ? v.Substring(0, MaxLength) : v;
        }

        public static void Save() { Write(Path.Combine(Folder, FileName), File); }

        /// <summary>Every renamed star into players-export.json. Returns how many.</summary>
        public static int Export() { Write(ExportPath, File); return File.Players.Count; }

        /// <summary>players-import.json over your names (the stars it names; the rest stay). -1: no file, or it does not read.</summary>
        public static int Import()
        {
            var f = Read(ImportPath);
            if (f == null) return -1;
            int n = 0;
            foreach (var e in f.Players)
            {
                if (e == null || Stars.Find(e.Id) == null) continue;
                Set(e.Id, e.First, e.Last);
                n++;
            }
            return n;
        }

        static PlayerNamesFile Read(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                var f = JsonUtility.FromJson<PlayerNamesFile>(System.IO.File.ReadAllText(path));
                if (f != null && f.Players == null) f.Players = new List<PlayerNameEntry>();
                return f;
            }
            catch (Exception e) { Debug.LogWarning("Player names: could not read " + path + " (" + e.Message + ")"); return null; }
        }

        static void Write(string path, PlayerNamesFile f)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                System.IO.File.WriteAllText(tmp, JsonUtility.ToJson(f, true));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.File.Move(tmp, path);
            }
            catch (Exception e) { Debug.LogWarning("Player names: could not save (" + e.Message + ")"); }
        }
    }
}
