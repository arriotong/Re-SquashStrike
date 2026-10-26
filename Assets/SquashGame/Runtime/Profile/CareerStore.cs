// ===========================================================
// Assets/SquashGame/Runtime/Profile/CareerStore.cs
// P2-M4: the career on disk - career.json next to the profile, written
// with Unity's JsonUtility after every match and every month. One
// career at a time; a new one replaces it.
// ===========================================================

using System;
using System.IO;
using SquashSim.Career;
using UnityEngine;

namespace SquashGame.Profile
{
    public static class CareerStore
    {
        public const string FileName = "career.json";
        static string PathOf { get { return Path.Combine(ProfileStore.Folder ?? Application.persistentDataPath, FileName); } }

        static CareerSession _current;
        static bool _loaded;

        /// <summary>The career in play, or null if there is none.</summary>
        public static CareerSession Current
        {
            get
            {
                if (!_loaded) { _current = Load(); _loaded = true; }
                return _current;
            }
        }

        public static bool Exists { get { return Current != null; } }

        /// <summary>Forget the cached career (tests).</summary>
        public static void Reset() { _current = null; _loaded = false; }

        static CareerSession Load()
        {
            try
            {
                string path = PathOf;
                if (!File.Exists(path)) return null;
                var s = JsonUtility.FromJson<CareerState>(File.ReadAllText(path));
                if (s == null || s.Roster == null || s.Roster.Count == 0) return null;
                if (s.Version < CareerState.CurrentVersion)
                {
                    // an older tour's career can't be carried over (v1: the 80-player ladder; v2-3: before P7's levels and
                    // world tour - the user chose "start fresh"): keep it aside, start afresh
                    File.Copy(path, path + ".v" + s.Version + ".bak", true);
                    File.Delete(path);
                    Debug.Log("Career: a version " + s.Version + " career was put aside (" + FileName + ".v" + s.Version + ".bak)");
                    return null;
                }
                return new CareerSession(s);
            }
            catch (Exception e) { Debug.LogWarning("Career: could not read it (" + e.Message + ")"); return null; }
        }

        /// <summary>A new career (replacing any other), saved at once.</summary>
        public static CareerSession New(string name, string nat, int gamesToWin, bool english)
        {
            uint seed = unchecked((uint)Environment.TickCount * 2654435761u) | 1u;
            _current = CareerSession.New(name, nat, seed, gamesToWin, english);
            _loaded = true;
            Save();
            return _current;
        }

        public static void Save()
        {
            if (_current == null) return;
            try
            {
                string path = PathOf;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(_current.S));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { Debug.LogWarning("Career: could not save (" + e.Message + ")"); }
        }

        /// <summary>Remove the saved career (tests).</summary>
        public static void Delete()
        {
            try { if (File.Exists(PathOf)) File.Delete(PathOf); } catch (Exception) { }
            Reset();
        }
    }
}
