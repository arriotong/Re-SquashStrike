// ===========================================================
// Assets/SquashGame/Runtime/Replays/ReplayLibrary.cs
// P2-M6 Replay Theatre: the replays on disk.
//
//   Replays/                 next to the profile
//     index.json             a card's worth per replay (so listing
//                            never opens the files)
//     <id>.ssr               the replays (SquashSim.Replays.ReplayFile)
//
//   autosave    every finished (or left) match with a rally in it; the
//               last 20 are kept
//   pinned      kept until unpinned
//   career      kept while that career lives (outside the 20)
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using SquashGame.Profile;
using SquashSim.Replays;
using UnityEngine;

namespace SquashGame.Replays
{
    [Serializable]
    public sealed class ReplayEntry
    {
        public string Id;
        public long Created;
        public string Mode;              // quick / versus / career
        public string P1, P2;            // names as on the score bug
        public string Opponent;          // "Attacker (Hard)", the pro's name
        public string Event, Round;      // career
        public string CareerKey;         // the career it belongs to (its seed), or ""
        public string Scores;            // "11-7 9-11 11-5"
        public int Winner = -1;          // -1: not finished
        public bool Retired;
        public int Rallies, Tagged;
        public bool Pinned;
        public string Fingerprint;
        public int You;                  // P2-M8: the athlete you played (online: 0 or 1)

        public DateTime When { get { return new DateTime(Created); } }
        public bool SameVersion { get { return Fingerprint == SimFingerprint.Current.ToString("x16"); } }
        /// <summary>"W", "L", "RET" from your side.</summary>
        public string Result { get { return Retired ? "RET" : Winner < 0 ? "–" : Winner == You ? "W" : "L"; } }
    }

    [Serializable]
    sealed class ReplayIndex { public List<ReplayEntry> Entries = new List<ReplayEntry>(); }

    public static class ReplayLibrary
    {
        public const int Keep = 20;
        public const string Extension = ".ssr";
        static ReplayIndex _index;
        static string _folderFor;

        /// <summary>Raised when a replay is added, pinned or deleted.</summary>
        public static event Action Changed;

        public static string Folder { get { return Path.Combine(ProfileStore.Folder ?? Application.persistentDataPath, "Replays"); } }
        static string IndexPath { get { return Path.Combine(Folder, "index.json"); } }
        public static string PathOf(ReplayEntry e) { return Path.Combine(Folder, e.Id + Extension); }

        static ReplayIndex Index
        {
            get
            {
                if (_index != null && _folderFor == Folder) return _index;
                _folderFor = Folder;
                _index = new ReplayIndex();
                try { if (File.Exists(IndexPath)) _index = JsonUtility.FromJson<ReplayIndex>(File.ReadAllText(IndexPath)) ?? new ReplayIndex(); }
                catch (Exception e) { Debug.LogWarning("[Replays] index unreadable, starting a new one: " + e.Message); }
                _index.Entries.RemoveAll(x => x == null || !File.Exists(PathOf(x)));
                return _index;
            }
        }

        /// <summary>Every replay, newest first.</summary>
        public static List<ReplayEntry> Entries { get { var l = new List<ReplayEntry>(Index.Entries); l.Sort((a, b) => b.Created.CompareTo(a.Created)); return l; } }

        public static ReplayEntry Latest { get { var l = Entries; return l.Count > 0 ? l[0] : null; } }

        /// <summary>Forget the cached index (tests change the profile folder).</summary>
        public static void Reset() { _index = null; }

        /// <summary>Listen for finished matches (once, from the front end).</summary>
        public static void Hook()
        {
            MatchRunner.ReplayFinished -= OnFinished;
            MatchRunner.ReplayFinished += OnFinished;
        }

        static void OnFinished(ReplayFile f)
        {
            try { Add(f); }
            catch (Exception e) { Debug.LogWarning("[Replays] could not save the replay: " + e.Message); }
        }

        /// <summary>Save a replay and list it (then keep the library to its size).</summary>
        public static ReplayEntry Add(ReplayFile f)
        {
            Directory.CreateDirectory(Folder);
            var when = new DateTime(f.CreatedTicks > 0 ? f.CreatedTicks : DateTime.Now.Ticks);
            string id = when.ToString("yyyyMMdd-HHmmss-fff");
            while (File.Exists(Path.Combine(Folder, id + Extension))) id += "b";
            var e = new ReplayEntry
            {
                Id = id, Created = when.Ticks, Mode = f.Get("mode", "quick"), P1 = f.Get("p1", "YOU"), P2 = f.Get("p2", "OPPONENT"),
                Opponent = f.Get("opponent", f.Get("p2")), Event = f.Get("event"), Round = f.Get("round"), CareerKey = f.Get("career"),
                Scores = Highlights.GameScores(f), Winner = f.Winner, Retired = f.Retired, Rallies = f.Rallies.Count, Tagged = Highlights.Count(f),
                Fingerprint = f.Fingerprint.ToString("x16"), You = f.Get("you", "0") == "1" ? 1 : 0,
            };
            File.WriteAllBytes(PathOf(e), f.ToBytes());
            Index.Entries.Add(e);
            Trim();
            Save();
            Changed?.Invoke();
            return e;
        }

        public static ReplayFile Load(ReplayEntry e) { return ReplayFile.FromBytes(File.ReadAllBytes(PathOf(e))); }

        public static void Pin(ReplayEntry e, bool on)
        {
            var x = Find(e.Id); if (x == null) return;
            x.Pinned = on; e.Pinned = on;
            Trim(); Save(); Changed?.Invoke();
        }

        public static void Delete(ReplayEntry e)
        {
            var x = Find(e.Id); if (x == null) return;
            try { File.Delete(PathOf(x)); } catch (Exception ex) { Debug.LogWarning("[Replays] " + ex.Message); }
            Index.Entries.Remove(x);
            Save(); Changed?.Invoke();
        }

        public static ReplayEntry Find(string id) { foreach (var x in Index.Entries) if (x.Id == id) return x; return null; }

        /// <summary>A career replay is kept while its career lives.</summary>
        static bool CareerKept(ReplayEntry e)
        {
            var c = CareerStore.Current;
            return !string.IsNullOrEmpty(e.CareerKey) && c != null && c.S.Seed.ToString() == e.CareerKey;
        }

        /// <summary>The autosave: beyond the last 20 (not pinned, not a living career's), the oldest go.</summary>
        static void Trim()
        {
            var auto = Index.Entries.FindAll(x => !x.Pinned && !CareerKept(x));
            auto.Sort((a, b) => b.Created.CompareTo(a.Created));
            for (int i = Keep; i < auto.Count; i++)
            {
                try { File.Delete(PathOf(auto[i])); } catch (Exception ex) { Debug.LogWarning("[Replays] " + ex.Message); }
                Index.Entries.Remove(auto[i]);
            }
        }

        static void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(IndexPath, JsonUtility.ToJson(Index, true));
        }
    }
}
