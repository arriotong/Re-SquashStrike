// ===========================================================
// Tools/SquashServer/Hub.cs
// Everything the server knows and decides, apart from the sockets (so
// the tests drive it directly):
//
//   hello      the game's version and sim fingerprint must match (the
//              same inputs must give the same match on both machines);
//              a player is the id and secret the game made
//   rooms      a 5-letter code; the host picks the length, scoring and
//              court; rematch when both ask
//   quick      first come, first paired (best of 3, PAR, show court)
//   ranked     paired within 100 rating, the band widening by 50 every
//              10 s of waiting; Elo after the match
//   a match    the inputs are relayed to the other player and kept; the
//              server steps its own copy of the match from them, and its
//              result is the result. Each client's rally hashes are held
//              against the server's: a different one is a desync (the
//              match ends, unrated)
//   dropped    the other waits (their game holds for the missing input);
//              back within 20 s, the player gets every frame so far and
//              catches up; otherwise a forfeit
//   input delay from both pings at the start: the one-way time from one
//              player to the other, in ticks, plus one (2..8)
//
// One lock around it all: the sims share PhysicsConfig.Current, so each
// match puts its own config back before it steps (TickSim.Use).
// ===========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;

namespace SquashServer
{
    public interface IPeerIo
    {
        void Send(byte[] message);
        void Close();
    }

    public sealed class HubOptions
    {
        public int QuickGamesToWin = 2, RankedGamesToWin = 2;
        public double GraceSeconds = 20, SilenceSeconds = 30, PingEvery = 2;
        public int MinDelay = 2, MaxDelay = 8;
        public int Seed = Environment.TickCount;
    }

    public sealed class Peer
    {
        public IPeerIo Io;
        public PlayerRec Player;
        public string Name = "", Kit = "";
        public double LastHeard, LastPing, Rtt = -1;
        public bool Gone;
        public Match Match; public int Slot;
        public Room Room;
        public QueueKind? Queued; public double QueuedAt;
        public string Id { get { return Player != null ? Player.Id : ""; } }
    }

    public sealed class Room
    {
        public string Code = "";
        public Peer Host, Guest;
        public int GamesToWin = 2; public string Scoring = "par", Court = "show";
        public Match Current;
        public int LastFirstServer = 1;
        public readonly bool[] Rematch = new bool[2];
    }

    public sealed class Match
    {
        public long Id;
        public string Kind = "";
        public bool Ranked;
        public string Court = "show";
        public Room Room;
        public TickSim Sim;
        public readonly Peer[] P = new Peer[2];
        public readonly PlayerRec[] Rec = new PlayerRec[2];
        public readonly string[] Names = new string[2], Kits = new string[2];
        public readonly List<QFrame>[] Frames = { new List<QFrame>(), new List<QFrame>() };
        public readonly List<(long tick, ReplayCommand kind)> Cmds = new List<(long, ReplayCommand)>();
        public readonly Dictionary<int, ulong>[] ClientHash = { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        public readonly double[] AwaySince = { -1, -1 };
        public ReplaySetup Setup;
        public bool Over;
        public MatchRec Result;
        /// <summary>The next tick this slot may send.</summary>
        public long NextTick(int s) { return Sim.Delay + Frames[s].Count; }
    }

    public sealed class Hub
    {
        public readonly object Gate = new object();
        public readonly Store Store;
        public readonly HubOptions Opt;
        readonly Func<double> _now;
        readonly Random _rng;
        readonly List<Peer> _peers = new List<Peer>();
        readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        readonly List<Match> _matches = new List<Match>();
        double _lastSave;

        public const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int MaxName = 16, MaxKit = 4096;

        public Hub(Store store, Func<double> now, HubOptions opt = null)
        {
            Store = store; _now = now; Opt = opt ?? new HubOptions();
            _rng = new Random(Opt.Seed);
        }

        public int Online { get { lock (Gate) return _peers.Count(p => p.Player != null); } }
        public int Playing { get { lock (Gate) return _matches.Count; } }
        public int Rooms { get { lock (Gate) return _rooms.Count; } }
        /// <summary>The finished matches since the start (tests read results here).</summary>
        public readonly List<MatchRec> Finished = new List<MatchRec>();

        // ---------------- connections ----------------

        public Peer Connect(IPeerIo io)
        {
            lock (Gate)
            {
                var p = new Peer { Io = io, LastHeard = _now(), LastPing = _now() - Opt.PingEvery };     // pinged at once: the delay needs it
                _peers.Add(p);
                return p;
            }
        }

        public void Disconnected(Peer p)
        {
            lock (Gate) Drop(p);
        }

        void Drop(Peer p)
        {
            if (p.Gone) return;
            p.Gone = true;
            _peers.Remove(p);
            p.Queued = null;
            if (p.Match != null && !p.Match.Over)
            {
                var m = p.Match;
                m.P[p.Slot] = null;
                m.AwaySince[p.Slot] = _now();
                if (m.P[0] == null && m.P[1] == null) Finish(m, -1, "abandoned");
            }
            else LeaveRoom(p);
            try { p.Io.Close(); } catch { }
        }

        void Send(Peer p, byte[] m) { if (p != null && !p.Gone) { try { p.Io.Send(m); } catch { } } }
        void Error(Peer p, string text) { Send(p, NetMessages.Error(text)); }

        // ---------------- messages ----------------

        public void Receive(Peer p, byte[] m)
        {
            lock (Gate)
            {
                if (p.Gone) return;
                p.LastHeard = _now();
                try { Handle(p, m); }
                catch (Exception) { Error(p, "That message could not be read."); Drop(p); }
            }
        }

        void Handle(Peer p, byte[] m)
        {
            var type = NetMessages.TypeOf(m);
            if (type == Msg.Ping) { Send(p, NetMessages.Pong(NetMessages.ReadLong(m))); return; }
            if (type == Msg.Pong) { p.Rtt = Math.Max(0, _now() - NetMessages.ReadLong(m) / 1000.0); return; }
            if (p.Player == null && type != Msg.Hello) { Error(p, "Say hello first."); return; }
            switch (type)
            {
                case Msg.Hello: Hello(p, m); break;
                case Msg.CreateRoom: CreateRoom(p, m); break;
                case Msg.JoinRoom: JoinRoom(p, NetMessages.ReadString(m)); break;
                case Msg.Queue: Queue(p, (QueueKind)NetMessages.Read(m).ReadByte()); break;
                case Msg.Cancel: p.Queued = null; if (p.Match == null) LeaveRoom(p); break;
                case Msg.Leave: Leave(p); break;
                case Msg.Rematch: Rematch(p); break;
                case Msg.Input: Input(p, m); break;
                case Msg.Command: Command(p, m); break;
                case Msg.RallyHash: Hash(p, m); break;
            }
        }

        void Hello(Peer p, byte[] m)
        {
            var r = NetMessages.Read(m);
            int version = r.ReadInt32();
            string id = r.ReadString(), secret = r.ReadString(), name = r.ReadString(), kit = r.ReadString(), game = r.ReadString();
            ulong fingerprint = r.ReadUInt64();
            if (version != NetMessages.Version || fingerprint != SimFingerprint.Current)
            {
                Error(p, "Your game is a different version from the server's - update the game to play online.");
                Drop(p); return;
            }
            if (string.IsNullOrEmpty(id) || id.Length < 8 || id.Length > 64 || !id.All(char.IsLetterOrDigit) || (secret ?? "").Length < 16)
            {
                Error(p, "That player id is not valid."); Drop(p); return;
            }
            var rec = Store.SignIn(id, secret, Clean(name), (long)_now());
            if (rec == null) { Error(p, "That player id belongs to someone else."); Drop(p); return; }
            var old = _peers.FirstOrDefault(o => o != p && o.Player == rec);
            if (old != null) Drop(old);                                                    // the same player again: the old link is dead
            p.Player = rec; p.Name = rec.Name;
            p.Kit = kit != null && kit.Length <= MaxKit ? kit : "";
            Send(p, NetMessages.Welcome(rec.Rating, Store.RankOf(rec), rec.Games));
            // back in a match it dropped out of: everything so far, to catch up
            var back = _matches.FirstOrDefault(x => !x.Over && x.Rec[0].Id != x.Rec[1].Id && (x.Rec[0].Id == id && x.P[0] == null || x.Rec[1].Id == id && x.P[1] == null));
            if (back != null) Resume(p, back, back.Rec[0].Id == id && back.P[0] == null ? 0 : 1);
        }

        static string Clean(string name)
        {
            var s = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (s.Length > MaxName) s = s.Substring(0, MaxName).Trim();
            return s.Length == 0 ? "Player" : s;
        }

        // ---------------- rooms ----------------

        void CreateRoom(Peer p, byte[] m)
        {
            if (p.Match != null) { Error(p, "You are in a match."); return; }
            var r = NetMessages.Read(m);
            int games = Math.Clamp(r.ReadInt32(), 1, 3);
            string scoring = r.ReadString() == "english" ? "english" : "par";
            string court = r.ReadString();
            LeaveRoom(p); p.Queued = null;
            string code;
            do { code = new string(Enumerable.Range(0, 5).Select(_ => Letters[_rng.Next(Letters.Length)]).ToArray()); } while (_rooms.ContainsKey(code));
            var room = new Room { Code = code, Host = p, GamesToWin = games, Scoring = scoring, Court = string.IsNullOrEmpty(court) ? "show" : court };
            _rooms[code] = room; p.Room = room;
            Send(p, NetMessages.RoomCreated(code));
        }

        void JoinRoom(Peer p, string code)
        {
            if (p.Match != null) { Error(p, "You are in a match."); return; }
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!_rooms.TryGetValue(code, out var room)) { Error(p, "No room with the code " + code + "."); return; }
            if (room.Host == p) { Error(p, "That is your own room - give the code to a friend."); return; }
            if (room.Guest != null) { Error(p, "That room is full."); return; }
            LeaveRoom(p); p.Queued = null;
            room.Guest = p; p.Room = room;
            StartRoomMatch(room);
        }

        void StartRoomMatch(Room room)
        {
            room.Rematch[0] = room.Rematch[1] = false;
            room.LastFirstServer = 1 - room.LastFirstServer;
            var setup = new ReplaySetup { TwoPlayer = true, GamesToWin = room.GamesToWin, Scoring = room.Scoring, FirstServer = room.LastFirstServer };
            room.Current = StartMatch(room.Host, room.Guest, setup, room.Court, false, "room", room);
        }

        void Rematch(Peer p)
        {
            var room = p.Room;
            if (room == null || room.Current != null || room.Host == null || room.Guest == null) return;
            int s = room.Host == p ? 0 : 1;
            room.Rematch[s] = true;
            if (room.Rematch[0] && room.Rematch[1]) StartRoomMatch(room);
            else Send(s == 0 ? room.Guest : room.Host, NetMessages.Simple(Msg.Rematch));       // they want another
        }

        void LeaveRoom(Peer p)
        {
            var room = p.Room;
            if (room == null) return;
            p.Room = null;
            if (room.Host == p)
            {
                _rooms.Remove(room.Code);
                if (room.Guest != null) { room.Guest.Room = null; Send(room.Guest, NetMessages.Simple(Msg.OpponentLeft)); }
            }
            else if (room.Guest == p)
            {
                room.Guest = null;
                Send(room.Host, NetMessages.Simple(Msg.OpponentLeft));
            }
        }

        void Leave(Peer p)
        {
            p.Queued = null;
            if (p.Match != null && !p.Match.Over) Finish(p.Match, 1 - p.Slot, "forfeit");
            LeaveRoom(p);
        }

        // ---------------- queues ----------------

        void Queue(Peer p, QueueKind kind)
        {
            if (p.Match != null) { Error(p, "You are in a match."); return; }
            LeaveRoom(p);
            p.Queued = kind; p.QueuedAt = _now();
            PairQueues();
        }

        /// <summary>How far apart in rating two ranked players may be after this long in the queue.</summary>
        public static int Band(double waited) { return 100 + 50 * (int)Math.Floor(waited / 10.0); }

        void PairQueues()
        {
            var quick = _peers.Where(o => o.Queued == QueueKind.Quick && o.Match == null).OrderBy(o => o.QueuedAt).ToList();
            for (int i = 0; i + 1 < quick.Count; i += 2) Pair(quick[i], quick[i + 1], false);
            var ranked = _peers.Where(o => o.Queued == QueueKind.Ranked && o.Match == null).OrderBy(o => o.QueuedAt).ToList();
            double now = _now();
            for (int i = 0; i < ranked.Count; i++)
            {
                var a = ranked[i];
                if (a.Queued == null) continue;
                Peer best = null; int bestGap = int.MaxValue;
                for (int j = i + 1; j < ranked.Count; j++)
                {
                    var b = ranked[j];
                    if (b.Queued == null) continue;
                    int gap = Math.Abs(a.Player.Rating - b.Player.Rating);
                    if (gap <= Math.Max(Band(now - a.QueuedAt), Band(now - b.QueuedAt)) && gap < bestGap) { best = b; bestGap = gap; }
                }
                if (best != null) Pair(a, best, true);
            }
        }

        void Pair(Peer a, Peer b, bool ranked)
        {
            a.Queued = null; b.Queued = null;
            var setup = new ReplaySetup { TwoPlayer = true, GamesToWin = ranked ? Opt.RankedGamesToWin : Opt.QuickGamesToWin, Scoring = "par", FirstServer = _rng.Next(2) };
            StartMatch(a, b, setup, "show", ranked, ranked ? "ranked" : "quick", null);
        }

        // ---------------- a match ----------------

        /// <summary>The input delay for these two, in ticks: the one-way time between them, plus one.</summary>
        public int DelayFor(Peer a, Peer b)
        {
            double ra = a.Rtt >= 0 ? a.Rtt : 0.08, rb = b.Rtt >= 0 ? b.Rtt : 0.08;
            int d = (int)Math.Ceiling((ra + rb) / 2 / TickSim.TickRealSeconds) + 1;
            return Math.Clamp(d, Opt.MinDelay, Opt.MaxDelay);
        }

        Match StartMatch(Peer a, Peer b, ReplaySetup setup, string court, bool ranked, string kind, Room room)
        {
            setup.BotSeed = (uint)_rng.Next();
            setup.Lets = true;
            var m = new Match { Id = Store.NextMatchId(), Kind = kind, Ranked = ranked, Court = court, Room = room, Setup = setup };
            m.Sim = new TickSim(setup, DelayFor(a, b));
            m.P[0] = a; m.P[1] = b;
            for (int s = 0; s < 2; s++)
            {
                var p = m.P[s];
                m.Rec[s] = p.Player; m.Names[s] = p.Name; m.Kits[s] = p.Kit;
                p.Match = m; p.Slot = s;
            }
            _matches.Add(m);
            for (int s = 0; s < 2; s++) Send(m.P[s], NetMessages.MatchStart(InfoFor(m, s, false)));
            return m;
        }

        MatchStartInfo InfoFor(Match m, int s, bool resuming)
        {
            return new MatchStartInfo
            {
                MatchId = m.Id, Slot = s, Delay = m.Sim.Delay, Ranked = m.Ranked, Court = m.Court, Resuming = resuming,
                OppName = m.Names[1 - s], OppKit = m.Kits[1 - s], OppRating = m.Rec[1 - s].Rating, MyRating = m.Rec[s].Rating,
                Setup = new ReplaySetup { TwoPlayer = true, Scoring = m.Setup.Scoring, GamesToWin = m.Setup.GamesToWin, BotSeed = m.Setup.BotSeed, FirstServer = m.Setup.FirstServer, Lets = m.Setup.Lets },
            };
        }

        void Resume(Peer p, Match m, int s)
        {
            m.P[s] = p; m.AwaySince[s] = -1;
            p.Match = m; p.Slot = s; p.Room = m.Room;
            if (m.Room != null) { if (s == 0) m.Room.Host = p; else m.Room.Guest = p; }
            Send(p, NetMessages.MatchStart(InfoFor(m, s, true)));
            const int Chunk = 2048;
            foreach (int slot in new[] { 1 - s, s })                       // its own frames last: they end the catching up
            {
                var all = m.Frames[slot].ToArray();
                int at = 0;
                do
                {
                    int n = Math.Min(Chunk, all.Length - at);
                    Send(p, NetMessages.Frames(slot, m.Sim.Delay + at, at + n >= all.Length, all, at, n));
                    at += n;
                } while (at < all.Length);
            }
            foreach (var c in m.Cmds) Send(p, NetMessages.Command(c.tick, c.kind));
        }

        void Input(Peer p, byte[] msg)
        {
            var m = p.Match;
            if (m == null || m.Over) return;
            var r = NetMessages.Read(msg);
            long tick = r.ReadInt64();
            var f = NetMessages.ReadFrame(r);
            int s = p.Slot;
            if (tick != m.NextTick(s)) return;                                   // a repeat, or out of turn: not this one
            if (tick - m.Sim.Tick > 60 * 30) { Error(p, "Too far ahead."); Drop(p); return; }
            m.Frames[s].Add(f);
            m.Sim.Put(s, tick, f);
            Send(m.P[1 - s], msg);
            m.Sim.Use();
            m.Sim.StepAll();
            CheckHashes(m);
            if (!m.Over && m.Sim.Over) Finish(m, m.Sim.Sim.Score.MatchWinner, "");
        }

        void Command(Peer p, byte[] msg)
        {
            var m = p.Match;
            if (m == null || m.Over) return;
            var r = NetMessages.Read(msg);
            long tick = r.ReadInt64();
            var kind = (ReplayCommand)r.ReadByte();
            if (kind != ReplayCommand.NextGame || tick < m.NextTick(p.Slot)) return;
            m.Cmds.Add((tick, kind));
            m.Sim.PutCommand(tick, kind);
            Send(m.P[1 - p.Slot], msg);
        }

        void Hash(Peer p, byte[] msg)
        {
            var m = p.Match;
            if (m == null || m.Over) return;
            var r = NetMessages.Read(msg);
            int rally = r.ReadInt32();
            ulong h = r.ReadUInt64();
            if (rally < 0 || rally > 100000) return;
            m.ClientHash[p.Slot][rally] = h;
            CheckHashes(m);
        }

        void CheckHashes(Match m)
        {
            if (m.Over) return;
            var mine = m.Sim.Hashes;
            for (int s = 0; s < 2; s++)
            {
                var theirs = m.ClientHash[s];
                if (theirs.Count == 0) continue;
                foreach (var kv in theirs.ToList())
                {
                    if (kv.Key >= mine.Count) continue;
                    if (mine[kv.Key] != kv.Value) { Finish(m, -1, "desync"); return; }
                    theirs.Remove(kv.Key);
                }
            }
        }

        void Finish(Match m, int winner, string reason)
        {
            if (m.Over) return;
            m.Over = true;
            m.Sim.Close(reason == "");
            bool rated = m.Ranked && winner >= 0 && (reason == "" || reason == "forfeit") && m.Rec[0].Id != m.Rec[1].Id;
            int[] before = { m.Rec[0].Rating, m.Rec[1].Rating };
            int d0 = 0, d1 = 0;
            if (rated) (d0, d1) = Store.Rate(m.Rec[0], m.Rec[1], winner);
            m.Result = new MatchRec { Id = m.Id, Kind = m.Kind, P0 = m.Rec[0].Id, P1 = m.Rec[1].Id, Winner = winner, Reason = reason, Rated = rated, Delta0 = d0, Delta1 = d1, At = (long)_now() };
            Store.Log(m.Result);
            Finished.Add(m.Result);
            if (m.Ranked) { try { Store.SaveReplay(m.Id, m.Sim.File.ToBytes()); } catch { } }
            _matches.Remove(m);
            for (int s = 0; s < 2; s++)
            {
                var p = m.P[s];
                if (p == null) continue;
                p.Match = null;
                Send(p, NetMessages.MatchEnd(new MatchEndInfo { Winner = winner, Reason = reason, Rated = rated, RatingBefore = before[s], RatingAfter = m.Rec[s].Rating }));
            }
            var room = m.Room;
            if (room != null)
            {
                room.Current = null;
                for (int s = 0; s < 2; s++) if (m.P[s] == null) { if (s == 0) room.Host = null; else room.Guest = null; }
                if (room.Host == null)
                {
                    _rooms.Remove(room.Code);
                    if (room.Guest != null) { room.Guest.Room = null; Send(room.Guest, NetMessages.Simple(Msg.OpponentLeft)); }
                }
                else if (room.Guest == null) Send(room.Host, NetMessages.Simple(Msg.OpponentLeft));
            }
        }

        // ---------------- time ----------------

        /// <summary>A few times a second: queues, pings, the silent, the dropped, saving.</summary>
        public void Tick()
        {
            lock (Gate)
            {
                double now = _now();
                foreach (var p in _peers.ToList())
                {
                    if (now - p.LastHeard > Opt.SilenceSeconds) { Drop(p); continue; }
                    if (now - p.LastPing >= Opt.PingEvery) { p.LastPing = now; Send(p, NetMessages.Ping((long)(now * 1000))); }
                }
                foreach (var m in _matches.ToList())
                    for (int s = 0; s < 2 && !m.Over; s++)
                        if (m.AwaySince[s] >= 0 && now - m.AwaySince[s] > Opt.GraceSeconds) Finish(m, 1 - s, "forfeit");
                PairQueues();
                if (Store.Dirty && now - _lastSave > 5) { _lastSave = now; try { Store.Save(); } catch { } }
            }
        }

        // ---------------- what the web pages show ----------------

        public object Leaderboard(string you)
        {
            lock (Gate)
            {
                var top = Store.Top(100);
                var me = Store.Find(you);
                return new
                {
                    players = top.Select((p, i) => new { rank = i + 1, name = p.Name, rating = p.Rating, games = p.Games, wins = p.Wins, you = me != null && p == me }).ToList(),
                    you = me == null ? null : new { rank = Store.RankOf(me), name = me.Name, rating = me.Rating, games = me.Games, wins = me.Wins },
                };
            }
        }

        public object PlayerCard(string id)
        {
            lock (Gate)
            {
                var p = Store.Find(id);
                if (p == null) return null;
                return new { name = p.Name, rating = p.Rating, rank = Store.RankOf(p), games = p.Games, wins = p.Wins, losses = p.Losses };
            }
        }

        public object Health()
        {
            lock (Gate) return new { ok = true, game = "re:SquashStrike", version = NetMessages.Version, fingerprint = SimFingerprint.Current.ToString("x16"), online = _peers.Count(p => p.Player != null), playing = _matches.Count, players = Store.PlayerCount };
        }
    }
}
