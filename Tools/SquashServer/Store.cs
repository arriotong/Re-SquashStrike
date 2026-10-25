// ===========================================================
// Tools/SquashServer/Store.cs
// The players and their ratings, kept in one JSON file (written to a
// temporary file first, then moved over the old one), and every ranked
// match's replay beside it. No accounts: a player is the random id and
// secret the game made on first launch (only the secret's hash is kept).
//
//   Elo        start 1000; K 32, then 16 after 30 rated games
// ===========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SquashServer
{
    public sealed class PlayerRec
    {
        public string Id { get; set; } = "";
        public string SecretHash { get; set; } = "";
        public string Name { get; set; } = "Player";
        public int Rating { get; set; } = Elo.Start;
        public int Games { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public long LastSeen { get; set; }
    }

    public sealed class MatchRec
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "";       // room, quick, ranked
        public string P0 { get; set; } = "";
        public string P1 { get; set; } = "";
        public int Winner { get; set; } = -1;
        public string Reason { get; set; } = "";
        public bool Rated { get; set; }
        public int Delta0 { get; set; }
        public int Delta1 { get; set; }
        public long At { get; set; }
    }

    public static class Elo
    {
        public const int Start = 1000;
        public static int K(int games) { return games < 30 ? 32 : 16; }
        public static double Expected(int me, int them) { return 1.0 / (1.0 + Math.Pow(10, (them - me) / 400.0)); }
        /// <summary>The change for a player of rating <paramref name="me"/> with <paramref name="games"/> rated games behind them.</summary>
        public static int Delta(int me, int them, int games, bool won) { return (int)Math.Round(K(games) * ((won ? 1 : 0) - Expected(me, them))); }
    }

    public sealed class Store
    {
        sealed class Data
        {
            public long NextMatch { get; set; } = 1;
            public List<PlayerRec> Players { get; set; } = new List<PlayerRec>();
            public List<MatchRec> Matches { get; set; } = new List<MatchRec>();
        }

        public const int KeepMatches = 2000;
        readonly string _path;          // null: in memory only (tests)
        readonly Dictionary<string, PlayerRec> _byId = new Dictionary<string, PlayerRec>();
        Data _d = new Data();
        public bool Dirty { get; private set; }

        public Store(string path)
        {
            _path = path;
            if (_path != null && File.Exists(_path))
            {
                _d = JsonSerializer.Deserialize<Data>(File.ReadAllText(_path)) ?? new Data();
                foreach (var p in _d.Players) _byId[p.Id] = p;
            }
        }

        public int PlayerCount { get { return _d.Players.Count; } }
        public IReadOnlyList<MatchRec> Matches { get { return _d.Matches; } }
        public long NextMatchId() { Dirty = true; return _d.NextMatch++; }

        public static string Hash(string secret)
        {
            using (var sha = SHA256.Create()) return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("rss:" + (secret ?? ""))));
        }

        public PlayerRec Find(string id) { return id != null && _byId.TryGetValue(id, out var p) ? p : null; }

        /// <summary>The player with this id - new if unknown; null if the secret does not match.</summary>
        public PlayerRec SignIn(string id, string secret, string name, long now)
        {
            var h = Hash(secret);
            var p = Find(id);
            if (p == null)
            {
                p = new PlayerRec { Id = id, SecretHash = h };
                _byId[id] = p; _d.Players.Add(p);
            }
            else if (p.SecretHash != h) return null;
            p.Name = name; p.LastSeen = now;
            Dirty = true;
            return p;
        }

        /// <summary>1-based place among players with a rated game (0: none yet).</summary>
        public int RankOf(PlayerRec p)
        {
            if (p == null || p.Games == 0) return 0;
            return 1 + _d.Players.Count(o => o.Games > 0 && (o.Rating > p.Rating || (o.Rating == p.Rating && string.CompareOrdinal(o.Id, p.Id) < 0)));
        }

        public List<PlayerRec> Top(int n)
        {
            return _d.Players.Where(o => o.Games > 0).OrderByDescending(o => o.Rating).ThenBy(o => o.Id, StringComparer.Ordinal).Take(n).ToList();
        }

        /// <summary>A rated result: both ratings move; the deltas.</summary>
        public (int d0, int d1) Rate(PlayerRec p0, PlayerRec p1, int winner)
        {
            int d0 = Elo.Delta(p0.Rating, p1.Rating, p0.Games, winner == 0);
            int d1 = Elo.Delta(p1.Rating, p0.Rating, p1.Games, winner == 1);
            p0.Rating += d0; p1.Rating += d1;
            p0.Games++; p1.Games++;
            if (winner == 0) { p0.Wins++; p1.Losses++; } else { p1.Wins++; p0.Losses++; }
            Dirty = true;
            return (d0, d1);
        }

        public void Log(MatchRec m)
        {
            _d.Matches.Add(m);
            if (_d.Matches.Count > KeepMatches) _d.Matches.RemoveRange(0, _d.Matches.Count - KeepMatches);
            Dirty = true;
        }

        public void SaveReplay(long matchId, byte[] bytes)
        {
            if (_path == null) return;
            var dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_path)), "replays");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, matchId + ".ssreplay"), bytes);
        }

        public void Save()
        {
            Dirty = false;
            if (_path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_d));
            File.Move(tmp, _path, true);
        }
    }
}
