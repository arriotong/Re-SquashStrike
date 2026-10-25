// ===========================================================
// Assets/SquashGame/Runtime/Online/OnlineService.cs
// P2-M8 online: the game's link to the relay server (Tools/SquashServer).
//
//   identity    no account: a random id and secret made on first use and
//               kept in the profile; the name you play under; the server
//               (Settings > Gameplay; the deployed one by default)
//   the link    a WebSocket; hello on opening; pings both ways (your ping
//               on the HUD; the server picks the input delay from them)
//   lobby       a room of your own (its code), joining one, the quick and
//               ranked queues; what the server says back waits here for
//               the screens to read
//   a match     the server's MatchStart waits in Pending until the front
//               end starts it (nothing more is read until then, so the
//               match's first messages reach it); while it plays, its
//               messages go to the match's LockstepSession
//   dropped     mid-match the game reconnects by itself; the server sends
//               the match again (Resuming) with every frame so far
//
// Read on the main thread, once a frame (Pump).
// ===========================================================

using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SquashGame.Profile;
using SquashSim.Net;
using SquashSim.Replays;
using UnityEngine;

namespace SquashGame.Online
{
    public enum NetState { Offline, Connecting, Online, Failed }

    [Serializable] public sealed class BoardRow { public int rank; public string name; public int rating, games, wins; public bool you; }
    [Serializable] public sealed class Board { public BoardRow[] players = new BoardRow[0]; public BoardRow you; }

    public static class OnlineService
    {
        public const string DefaultServer = "wss://re-squashstrike.fly.dev/ws";     // the deployed relay (Tools/SquashServer, Fly.io)
        public const float PingEvery = 2f, ConnectTimeout = 10f, ReconnectFor = 18f;

        public static NetState State { get; private set; }
        /// <summary>The last thing that went wrong, in words (null: nothing).</summary>
        public static string Error { get; set; }
        /// <summary>Round trip to the server, seconds (-1: not yet).</summary>
        public static double Ping { get; private set; } = -1;
        /// <summary>The code of the room you host (null: none).</summary>
        public static string RoomCode { get; private set; }
        /// <summary>In a private room (host or guest): the full-time screen offers a rematch.</summary>
        public static bool InRoom { get; private set; }
        public static bool Queued { get; private set; }
        public static QueueKind QueuedFor { get; private set; }
        public static float QueuedSeconds { get; private set; }
        public static bool OpponentWantsRematch { get; set; }
        /// <summary>The other player left the room (between matches).</summary>
        public static bool OpponentLeftRoom { get; set; }
        /// <summary>A match the server started, waiting for the front end.</summary>
        public static MatchStartInfo Pending { get; private set; }
        /// <summary>The last match's end, as the server decided it (the rating change).</summary>
        public static MatchEndInfo LastEnd { get; private set; }
        /// <summary>The match playing now (MatchRunner sets it).</summary>
        public static LockstepSession Session;

        static WebSocketTransport _t;
        static bool _helloSent, _wasOnline;
        static float _openedAt, _pingAt, _lostAt = -1, _retryAt;
        static int _pumpedFrame = -1;

        public static ITransport Transport { get { return _t; } }
        public static OnlineIdentity Me { get { return Identity(); } }
        public static string ServerUrl { get { var s = Identity().Server; return string.IsNullOrWhiteSpace(s) ? DefaultServer : s.Trim(); } }

        /// <summary>The profile's online identity, made the first time (an id and a secret nobody else has).</summary>
        public static OnlineIdentity Identity()
        {
            var o = ProfileStore.Current.Online;
            if (string.IsNullOrEmpty(o.Id) || string.IsNullOrEmpty(o.Secret))
            {
                o.Id = Hex(16); o.Secret = Hex(32);
                ProfileStore.Save();
            }
            if (string.IsNullOrWhiteSpace(o.Name)) o.Name = CareerStore.Current != null ? CareerStore.Current.S.Name : "Player";
            return o;
        }

        static string Hex(int bytes)
        {
            var b = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
        }

        // ---------------- the link ----------------

        public static void Connect()
        {
            if (State == NetState.Connecting || State == NetState.Online) return;
            Open();
        }

        static void Open()
        {
            Drop();
            Error = null;
            State = NetState.Connecting;
            _helloSent = false;
            _openedAt = Time.unscaledTime;
            try { _t = new WebSocketTransport(ServerUrl); }
            catch (Exception e) { Fail("That server address is not right (" + e.Message + ")."); }
        }

        static void Drop()
        {
            if (_t != null) { try { _t.Close(); } catch { } }
            _t = null;
        }

        public static void Disconnect()
        {
            Drop();
            _wasOnline = false;
            State = NetState.Offline;
            RoomCode = null; Queued = false; Pending = null;
        }

        static void Fail(string why)
        {
            Drop();
            State = NetState.Failed;
            Error = why;
            Queued = false;
        }

        /// <summary>Once a frame: the link's state, pings, and every message waiting (unless a match is waiting to start).</summary>
        public static void Pump()
        {
            if (_pumpedFrame == Time.frameCount) return;
            _pumpedFrame = Time.frameCount;
            if (Queued) QueuedSeconds += Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            bool playing = Session != null && !Session.Finished && _wasOnline;

            // a match dropped in the middle: back again, by itself, inside the server's grace
            if (_t == null || (_helloSent && !_t.Connected))
            {
                if (playing)
                {
                    if (_lostAt < 0) { _lostAt = now; _retryAt = now; }
                    if (now - _lostAt > ReconnectFor) { Fail("The connection to the server was lost."); _lostAt = -1; return; }
                    if (now >= _retryAt) { _retryAt = now + 2f; Open(); }
                }
                else if (_t != null && State != NetState.Failed) Fail("The connection to the server was lost.");
                if (_t == null) return;
            }
            if (!_helloSent)
            {
                if (_t.Connected)
                {
                    var me = Identity();
                    _t.Send(NetMessages.Hello(me.Id, me.Secret, me.Name, JsonUtility.ToJson(Progression.KitDirector.You()), Application.version, SimFingerprint.Current));
                    _helloSent = true;
                    _pingAt = now;
                }
                else if (_t.Failure != null || now - _openedAt > ConnectTimeout)
                {
                    if (playing) { Drop(); return; }                 // keep trying while the match waits
                    Fail("Can't reach the online server at " + ServerUrl + ".");
                    return;
                }
                else return;
            }
            if (now >= _pingAt) { _pingAt = now + PingEvery; _t.Send(NetMessages.Ping((long)(now * 1000))); }

            while (Pending == null && _t != null && _t.TryReceive(out var m)) Handle(m);
        }

        static void Handle(byte[] m)
        {
            switch (NetMessages.TypeOf(m))
            {
                case Msg.Ping: _t.Send(NetMessages.Pong(NetMessages.ReadLong(m))); return;
                case Msg.Pong: Ping = Math.Max(0, Time.unscaledTime - NetMessages.ReadLong(m) / 1000.0); return;
                case Msg.Welcome:
                {
                    var r = NetMessages.Read(m);
                    var me = Identity();
                    me.Rating = r.ReadInt32(); me.Rank = r.ReadInt32(); me.Games = r.ReadInt32();
                    State = NetState.Online; Error = null; _lostAt = -1; _wasOnline = true;
                    return;
                }
                case Msg.Error:
                    Error = NetMessages.ReadString(m);
                    Queued = false;
                    return;
                case Msg.RoomCreated: RoomCode = NetMessages.ReadString(m); OpponentLeftRoom = false; InRoom = true; return;
                case Msg.MatchStart:
                    Pending = NetMessages.ReadMatchStart(m);
                    InRoom = InRoom || _joining;
                    _joining = false;
                    Queued = false; OpponentWantsRematch = false; OpponentLeftRoom = false; LastEnd = null;
                    return;
                case Msg.Rematch: OpponentWantsRematch = true; return;
                case Msg.OpponentLeft:
                    if (Session != null && !Session.Finished) Session.Handle(m);
                    else { OpponentLeftRoom = true; RoomCode = null; InRoom = false; }
                    return;
                case Msg.MatchEnd:
                {
                    LastEnd = NetMessages.ReadMatchEnd(m);
                    if (LastEnd.Rated) { var me = Identity(); me.Rating = LastEnd.RatingAfter; me.Games++; ProfileStore.Save(); }
                    if (Session != null) Session.Handle(m);
                    return;
                }
                default:
                    if (Session != null) Session.Handle(m);
                    return;
            }
        }

        /// <summary>The match the server started (the front end begins it; reading resumes after).</summary>
        public static MatchStartInfo TakePending() { var p = Pending; Pending = null; return p; }

        // ---------------- the lobby ----------------

        static void Send(byte[] m) { if (_t != null && _t.Connected) _t.Send(m); }

        public static void CreateRoom(int gamesToWin, string scoring, string court) { RoomCode = null; OpponentLeftRoom = false; Send(NetMessages.CreateRoom(gamesToWin, scoring, court)); }
        static bool _joining;
        public static void JoinRoom(string code) { Error = null; _joining = true; Send(NetMessages.JoinRoom(code)); }
        public static void Queue(QueueKind kind) { Error = null; Queued = true; QueuedFor = kind; QueuedSeconds = 0; InRoom = false; Send(NetMessages.Queue(kind)); }
        public static void Cancel() { Queued = false; RoomCode = null; InRoom = false; _joining = false; Send(NetMessages.Simple(Msg.Cancel)); }
        /// <summary>Leave the match (a forfeit if it is still on) and the room.</summary>
        public static void Leave() { Queued = false; RoomCode = null; InRoom = false; _joining = false; OpponentWantsRematch = false; Send(NetMessages.Simple(Msg.Leave)); }
        public static void Rematch() { Send(NetMessages.Simple(Msg.Rematch)); }

        // ---------------- the leaderboard (the server's web page) ----------------

        static Task<string> _board;
        public static Board LastBoard { get; private set; }
        public static string BoardError { get; private set; }
        public static bool BoardLoading { get { return _board != null && !_board.IsCompleted; } }

        /// <summary>The http address beside the WebSocket one (ws://host:port/ws -> http://host:port).</summary>
        public static string HttpBase()
        {
            string u = ServerUrl;
            if (u.StartsWith("wss://")) u = "https://" + u.Substring(6); else if (u.StartsWith("ws://")) u = "http://" + u.Substring(5);
            if (u.EndsWith("/ws")) u = u.Substring(0, u.Length - 3);
            return u.TrimEnd('/');
        }

        public static void FetchBoard()
        {
            if (BoardLoading) return;
            BoardError = null;
            string url = HttpBase() + "/leaderboard?id=" + Uri.EscapeDataString(Identity().Id);
            _board = Task.Run(async () => { using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) }) return await http.GetStringAsync(url); });
        }

        /// <summary>The leaderboard's answer, once it is in (the screen calls this each frame).</summary>
        public static void PollBoard()
        {
            if (_board == null || !_board.IsCompleted) return;
            var t = _board; _board = null;
            if (t.IsFaulted || t.IsCanceled) { BoardError = "Can't reach the online server."; return; }
            try { LastBoard = JsonUtility.FromJson<Board>(t.Result); }
            catch (Exception) { BoardError = "The leaderboard could not be read."; }
        }
    }
}
