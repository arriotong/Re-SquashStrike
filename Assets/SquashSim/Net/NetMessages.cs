// ===========================================================
// Assets/SquashSim/Net/NetMessages.cs
// P2-M8 online: what the game and the relay server say to each other,
// and its compact binary form ([type byte][fields], little-endian).
// Shared by the game, the server (Tools/SquashServer) and MatchBench.
//
//   client -> server   Hello, CreateRoom, JoinRoom, Queue, Cancel,
//                      Rematch, Leave, Ping
//   server -> client   Welcome, RoomCreated, MatchStart, MatchEnd,
//                      OpponentLeft, Error, Pong
//   relayed (client -> server -> the other client)
//                      Input (one tick's frame), Command, RallyHash
// ===========================================================

using System;
using System.IO;
using System.Text;
using SquashSim.Replays;

namespace SquashSim.Net
{
    public enum Msg : byte
    {
        Hello = 1, Welcome, CreateRoom, RoomCreated, JoinRoom, Queue, Cancel, MatchStart, Input, Command, RallyHash,
        Ping, Pong, MatchEnd, Error, Leave, Rematch, OpponentLeft,
    }

    public enum QueueKind : byte { Quick = 0, Ranked = 1 }

    /// <summary>The match two clients play: the setup both build it from, who is who, the input delay.</summary>
    public sealed class MatchStartInfo
    {
        public long MatchId;
        public int Slot;                     // this client's athlete: 0 or 1
        public int Delay = 3;                // input delay, ticks
        public bool Ranked;
        public string Court = "show";
        public string OppName = "", OppKit = "";
        public int OppRating, MyRating;
        public ReplaySetup Setup = new ReplaySetup { TwoPlayer = true, GamesToWin = 2, Scoring = "par" };
    }

    public sealed class MatchEndInfo
    {
        public int Winner = -1;              // the slot that won (-1: no result)
        public string Reason = "";           // "", "forfeit", "desync", "left"
        public int RatingBefore, RatingAfter;
        public bool Rated;
    }

    public static class NetMessages
    {
        public const int Version = 1;

        // ---------------- writing ----------------

        static byte[] Build(Msg type, Action<BinaryWriter> body)
        {
            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true)) { w.Write((byte)type); body?.Invoke(w); }
            return ms.ToArray();
        }

        public static byte[] Hello(string id, string secret, string name, string kitJson, string gameVersion, ulong fingerprint)
        {
            return Build(Msg.Hello, w => { w.Write(Version); w.Write(id ?? ""); w.Write(secret ?? ""); w.Write(name ?? ""); w.Write(kitJson ?? ""); w.Write(gameVersion ?? ""); w.Write(fingerprint); });
        }

        public static byte[] Welcome(int rating, int rank, int games) { return Build(Msg.Welcome, w => { w.Write(rating); w.Write(rank); w.Write(games); }); }

        public static byte[] CreateRoom(int gamesToWin, string scoring, string court) { return Build(Msg.CreateRoom, w => { w.Write(gamesToWin); w.Write(scoring ?? "par"); w.Write(court ?? "show"); }); }
        public static byte[] RoomCreated(string code) { return Build(Msg.RoomCreated, w => w.Write(code ?? "")); }
        public static byte[] JoinRoom(string code) { return Build(Msg.JoinRoom, w => w.Write((code ?? "").Trim().ToUpperInvariant())); }
        public static byte[] Queue(QueueKind k) { return Build(Msg.Queue, w => w.Write((byte)k)); }
        public static byte[] Simple(Msg m) { return Build(m, null); }
        public static byte[] Error(string text) { return Build(Msg.Error, w => w.Write(text ?? "")); }
        public static byte[] Ping(long t) { return Build(Msg.Ping, w => w.Write(t)); }
        public static byte[] Pong(long t) { return Build(Msg.Pong, w => w.Write(t)); }

        public static byte[] MatchStart(MatchStartInfo m)
        {
            return Build(Msg.MatchStart, w =>
            {
                w.Write(m.MatchId); w.Write(m.Slot); w.Write(m.Delay); w.Write(m.Ranked); w.Write(m.Court ?? "show");
                w.Write(m.OppName ?? ""); w.Write(m.OppKit ?? ""); w.Write(m.OppRating); w.Write(m.MyRating);
                var s = m.Setup;
                w.Write(s.Scoring ?? "par"); w.Write(s.GamesToWin); w.Write(s.BotSeed); w.Write(s.FirstServer); w.Write(s.Lets);
            });
        }

        public static byte[] Input(long tick, QFrame f) { return Build(Msg.Input, w => { w.Write(tick); WriteFrame(w, f); }); }
        public static byte[] Command(long tick, ReplayCommand kind) { return Build(Msg.Command, w => { w.Write(tick); w.Write((byte)kind); }); }
        public static byte[] RallyHash(int rally, ulong hash, int winner) { return Build(Msg.RallyHash, w => { w.Write(rally); w.Write(hash); w.Write(winner); }); }

        public static byte[] MatchEnd(MatchEndInfo e)
        {
            return Build(Msg.MatchEnd, w => { w.Write(e.Winner); w.Write(e.Reason ?? ""); w.Write(e.Rated); w.Write(e.RatingBefore); w.Write(e.RatingAfter); });
        }

        public static void WriteFrame(BinaryWriter w, QFrame f)
        {
            w.Write(f.MoveX); w.Write(f.MoveZ); w.Write(f.MoveMag); w.Write(f.RawX); w.Write(f.RawY); w.Write(f.AimX); w.Write(f.AimY);
            w.Write(f.HitMs); w.Write(f.TouchMs); w.Write(f.Flags);
        }

        public static QFrame ReadFrame(BinaryReader r)
        {
            return new QFrame
            {
                MoveX = r.ReadInt16(), MoveZ = r.ReadInt16(), MoveMag = r.ReadInt16(), RawX = r.ReadInt16(), RawY = r.ReadInt16(),
                AimX = r.ReadInt16(), AimY = r.ReadInt16(), HitMs = r.ReadInt32(), TouchMs = r.ReadInt32(), Flags = r.ReadUInt16(),
            };
        }

        // ---------------- reading ----------------

        public static Msg TypeOf(byte[] m) { return m != null && m.Length > 0 ? (Msg)m[0] : 0; }

        /// <summary>A reader positioned after the type byte.</summary>
        public static BinaryReader Read(byte[] m) { var r = new BinaryReader(new MemoryStream(m), Encoding.UTF8); r.ReadByte(); return r; }

        public static MatchStartInfo ReadMatchStart(byte[] m)
        {
            var r = Read(m);
            var i = new MatchStartInfo
            {
                MatchId = r.ReadInt64(), Slot = r.ReadInt32(), Delay = r.ReadInt32(), Ranked = r.ReadBoolean(), Court = r.ReadString(),
                OppName = r.ReadString(), OppKit = r.ReadString(), OppRating = r.ReadInt32(), MyRating = r.ReadInt32(),
            };
            i.Setup = new ReplaySetup { TwoPlayer = true, Scoring = r.ReadString(), GamesToWin = r.ReadInt32(), BotSeed = r.ReadUInt32(), FirstServer = r.ReadInt32(), Lets = r.ReadBoolean() };
            return i;
        }

        public static MatchEndInfo ReadMatchEnd(byte[] m)
        {
            var r = Read(m);
            return new MatchEndInfo { Winner = r.ReadInt32(), Reason = r.ReadString(), Rated = r.ReadBoolean(), RatingBefore = r.ReadInt32(), RatingAfter = r.ReadInt32() };
        }

        public static string ReadString(byte[] m) { return Read(m).ReadString(); }
        public static long ReadLong(byte[] m) { return Read(m).ReadInt64(); }
    }
}
