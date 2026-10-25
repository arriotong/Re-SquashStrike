// ===========================================================
// Tools/SquashServer.Tests/Program.cs
// The relay server's tests. Most drive the Hub directly through
// in-memory links on a clock the test moves; the bots are the game's
// own LockstepSession with an AI standing in for the person. The last
// one goes over real WebSockets to the real web host.
//
//   Elo and the ranked band          rooms: create, join, a bad code
//   hello: the wrong game, the wrong secret
//   quick and ranked pairing         relaying keeps order
//   a ranked match played out: the server's result is both clients',
//     ratings move by Elo
//   a client lying in its hashes: desync, unrated
//   a dropped player: a forfeit after the grace; back in time: it
//     catches up and the match plays on
// ===========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using SquashServer;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;

namespace SquashServer.Tests
{
    /// <summary>A connection in memory: what the server sends waits in the client's inbox; what the client sends goes straight in.</summary>
    sealed class Link : IPeerIo, ITransport
    {
        readonly Hub _hub;
        public readonly Peer Peer;
        readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        public bool Closed;
        public Func<byte[], byte[]> Tamper;           // the lying client: changes what it sends
        public Link(Hub hub) { _hub = hub; Peer = hub.Connect(this); }
        void IPeerIo.Send(byte[] m) { if (!Closed) _inbox.Enqueue(m); }
        void IPeerIo.Close() { Closed = true; }
        void ITransport.Send(byte[] m) { if (Closed) return; if (Tamper != null) m = Tamper(m); _hub.Receive(Peer, m); }
        public bool TryReceive(out byte[] m) { m = null; if (_inbox.Count == 0) return false; m = _inbox.Dequeue(); return true; }
        public bool Connected { get { return !Closed; } }
        public void Close() { if (Closed) return; Closed = true; _hub.Disconnected(Peer); }
        public void Send(byte[] m) { ((ITransport)this).Send(m); }
    }

    /// <summary>A player: says hello, answers pings, plays any match it is given with an AI at the controls.</summary>
    sealed class Bot
    {
        public string Id, Secret, Name, Profile;
        public int Seed;
        public Link L;
        public LockstepSession S;
        public MatchStartInfo Start;
        public MatchEndInfo End;
        public readonly List<byte[]> Other = new List<byte[]>();
        public readonly InputIntent Live = new InputIntent();
        IAthleteBrain _ai;
        public Func<byte[], byte[]> Tamper;

        public Bot(string name, int seed, string profile = "attacker")
        {
            Name = name; Seed = seed; Profile = profile;
            Id = "id" + name.ToLowerInvariant() + seed.ToString("000000");
            Secret = "secret-" + name + "-" + seed + "-0123456789";
        }

        public Bot Connect(Hub hub)
        {
            L = new Link(hub) { Tamper = Tamper };
            L.Send(NetMessages.Hello(Id, Secret, Name, "{}", "test", SimFingerprint.Current));
            S = null;
            return this;
        }

        public void Send(byte[] m) { L.Send(m); }

        public void Pump(double dt)
        {
            if (L == null) return;
            while (L.TryReceive(out var m))
            {
                var t = NetMessages.TypeOf(m);
                if (t == Msg.Ping) { L.Send(NetMessages.Pong(NetMessages.ReadLong(m))); continue; }
                if (t == Msg.MatchStart)
                {
                    Start = NetMessages.ReadMatchStart(m);
                    S = new LockstepSession(Start, L);
                    _ai = new PersonStandIn(new OpponentBrain(AiProfile.ByName(Profile).WithDifficulty(0.7), (uint)Seed));
                    End = null;
                    continue;
                }
                if (t == Msg.MatchEnd) End = NetMessages.ReadMatchEnd(m);
                if (S != null && S.Handle(m)) continue;
                Other.Add(m);
            }
            if (S != null && !S.Finished && !L.Closed) S.Pump(dt, Live, false, false, live => _ai.Fill(live, S.Sim, S.Me, LockstepSession.TickSeconds));
        }

        public bool Got(Msg type) { return Other.Any(m => NetMessages.TypeOf(m) == type); }
        public string LastError { get { var e = Other.LastOrDefault(m => NetMessages.TypeOf(m) == Msg.Error); return e == null ? null : NetMessages.ReadString(e); } }
    }

    sealed class World
    {
        public double Now;
        public readonly Hub Hub;
        public readonly List<Bot> Bots = new List<Bot>();
        double _tick;
        public World(HubOptions opt = null) { Hub = new Hub(new Store(null), () => Now, opt ?? new HubOptions { Seed = 5 }); }
        public Bot Add(Bot b) { Bots.Add(b); b.Connect(Hub); return b; }
        public void Run(double seconds, Func<bool> until = null)
        {
            const double dt = 1.0 / 120;
            double end = Now + seconds;
            while (Now < end && (until == null || !until()))
            {
                Now += dt;
                foreach (var b in Bots) b.Pump(dt);
                _tick += dt;
                if (_tick >= 0.25) { _tick = 0; Hub.Tick(); }
            }
        }
    }

    static class Program
    {
        static int _fail;

        static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }

        static void Test(string name, Action body)
        {
            var sw = Stopwatch.StartNew();
            try { body(); Console.WriteLine("  ok    " + name + "  (" + sw.Elapsed.TotalSeconds.ToString("0.0") + " s)"); }
            catch (Exception e) { _fail++; Console.WriteLine("  FAIL  " + name + ": " + e.Message); }
        }

        static int Main()
        {
            Console.WriteLine("re:SquashStrike server tests");

            Test("Elo", () =>
            {
                Check(Elo.Delta(1000, 1000, 0, true) == 16 && Elo.Delta(1000, 1000, 0, false) == -16, "even: +-16");
                Check(Elo.Delta(1000, 1000, 30, true) == 8, "K 16 after 30 games");
                Check(Elo.Delta(1000, 1400, 0, true) == 29 && Elo.Delta(1400, 1000, 0, false) == -29, "the upset pays more");
                Check(Math.Abs(Elo.Expected(1200, 1000) - 0.7597) < 0.001, "expected");
            });

            Test("the ranked band widens", () =>
            {
                Check(Hub.Band(0) == 100 && Hub.Band(9.9) == 100 && Hub.Band(10) == 150 && Hub.Band(40) == 300, "100, +50 per 10 s");
            });

            Test("hello: the wrong game, the wrong secret", () =>
            {
                var w = new World();
                var l = new Link(w.Hub);
                l.Send(NetMessages.Hello("abcdefgh1234", "0123456789abcdef0123", "X", "", "", SimFingerprint.Current ^ 1));
                Check(l.Closed, "a different sim is turned away");
                var a = w.Add(new Bot("Ann", 1)); w.Run(0.1);
                Check(a.Got(Msg.Welcome), "welcome");
                var thief = new Bot("Ann", 1) { Secret = "not-the-secret-at-all-xx" };
                w.Add(thief); w.Run(0.1);
                Check(thief.L.Closed && thief.LastError != null && thief.LastError.Contains("someone else"), "the wrong secret");
                Check(!a.L.Closed, "and Ann stays on");
            });

            Test("rooms: create, join, a bad code", () =>
            {
                var w = new World();
                var a = w.Add(new Bot("Ann", 1)); var b = w.Add(new Bot("Ben", 2));
                w.Run(0.1);
                a.Send(NetMessages.CreateRoom(1, "english", "arena")); w.Run(0.1);
                var made = a.Other.First(m => NetMessages.TypeOf(m) == Msg.RoomCreated);
                string code = NetMessages.ReadString(made);
                Check(code.Length == 5 && code.All(c => Hub.Letters.Contains(c)), "a 5-letter code: " + code);
                b.Send(NetMessages.JoinRoom("ZZZZZ")); w.Run(0.1);
                Check(b.LastError != null && b.S == null, "a bad code: an error");
                b.Send(NetMessages.JoinRoom(code.ToLowerInvariant())); w.Run(0.1);
                Check(a.Start != null && b.Start != null, "both start");
                Check(a.Start.Slot == 0 && b.Start.Slot == 1, "host 0, guest 1");
                Check(a.Start.Setup.BotSeed == b.Start.Setup.BotSeed && b.Start.Setup.Scoring == "english" && b.Start.Setup.GamesToWin == 1 && b.Start.Court == "arena", "the host's settings");
                Check(a.Start.OppName == "Ben" && b.Start.OppName == "Ann", "each other's names");
                var c = w.Add(new Bot("Cat", 3)); w.Run(0.1);
                c.Send(NetMessages.JoinRoom(code)); w.Run(0.1);
                Check(c.LastError != null && c.LastError.Contains("full"), "a third: full");
            });

            Test("quick match pairs the first two", () =>
            {
                var w = new World();
                var a = w.Add(new Bot("Ann", 1)); var b = w.Add(new Bot("Ben", 2)); var c = w.Add(new Bot("Cat", 3));
                w.Run(0.1);
                a.Send(NetMessages.Queue(QueueKind.Quick)); w.Run(0.1);
                Check(a.Start == null, "alone: waits");
                b.Send(NetMessages.Queue(QueueKind.Quick)); c.Send(NetMessages.Queue(QueueKind.Quick)); w.Run(0.1);
                Check(a.Start != null && b.Start != null && c.Start == null, "Ann and Ben");
                Check(a.Start.Setup.GamesToWin == 2 && a.Start.Setup.Scoring == "par" && !a.Start.Ranked, "best of 3, PAR, unranked");
            });

            Test("ranked pairs within the band", () =>
            {
                var w = new World();
                var a = w.Add(new Bot("Ann", 1)); var b = w.Add(new Bot("Ben", 2));
                w.Run(0.1);
                w.Hub.Store.Find(b.Id).Rating = 1300;
                a.Send(NetMessages.Queue(QueueKind.Ranked)); b.Send(NetMessages.Queue(QueueKind.Ranked)); w.Run(5);
                Check(a.Start == null, "300 apart: not yet");
                w.Run(36, () => a.Start != null);
                Check(a.Start != null && a.Start.Ranked && w.Now > 40, "after 40 s the band is 300: paired at " + w.Now.ToString("0.0"));
                Check(a.Start.OppRating == 1300 && b.Start.MyRating == 1300, "ratings shown");
            });

            Test("relaying keeps order", () =>
            {
                var w = new World();
                var a = w.Add(new Bot("Ann", 1)); var b = w.Add(new Bot("Ben", 2));
                w.Run(0.1);
                a.Send(NetMessages.CreateRoom(1, "par", "show")); w.Run(0.01);
                b.Send(NetMessages.JoinRoom(NetMessages.ReadString(a.Other.First(m => NetMessages.TypeOf(m) == Msg.RoomCreated))));
                // read B's inbox by hand: no bot pumping
                var link = b.L; while (link.TryReceive(out _)) { }
                int d = w.Hub.DelayFor(a.L.Peer, b.L.Peer);
                for (int i = 0; i < 100; i++) a.L.Send(NetMessages.Input(d + i, new QFrame { MoveX = (short)i }));
                a.L.Send(NetMessages.Input(d + 50, new QFrame()));                    // a repeat: dropped
                var got = new List<long>();
                while (link.TryReceive(out var m)) if (NetMessages.TypeOf(m) == Msg.Input) { var r = NetMessages.Read(m); long t = r.ReadInt64(); var f = NetMessages.ReadFrame(r); Check(f.MoveX == t - d, "the frame for its tick"); got.Add(t); }
                Check(got.Count == 100 && got.SequenceEqual(Enumerable.Range(d, 100).Select(x => (long)x)), "100 in order");
            });

            Test("a ranked match: the server's result is both clients'", () =>
            {
                var w = new World(new HubOptions { Seed = 9, RankedGamesToWin = 1 });
                var a = w.Add(new Bot("Ann", 11, "attacker")); var b = w.Add(new Bot("Ben", 12, "grinder"));
                w.Run(0.1);
                w.Run(2.5);                                                            // a ping each: the delay from them
                a.Send(NetMessages.Queue(QueueKind.Ranked)); b.Send(NetMessages.Queue(QueueKind.Ranked));
                w.Run(3000, () => a.End != null && b.End != null && a.S.Finished && b.S.Finished);
                Check(a.S != null && a.S.Finished && b.S.Finished, "both played it out");
                var res = w.Hub.Finished.Single();
                Check(res.Reason == "" && res.Rated, "played out, rated: " + res.Reason);
                Check(res.Winner == a.S.Winner && res.Winner == b.S.Winner && a.S.EndReason == "" && b.S.EndReason == "", "the same winner everywhere");
                Check(a.S.RallyHashes.SequenceEqual(b.S.RallyHashes), "the same rallies");
                var win = res.Winner == 0 ? a : b; var lose = res.Winner == 0 ? b : a;
                Check(win.End.Rated && win.End.RatingAfter == 1016 && lose.End.RatingAfter == 984, "Elo: 1016 / 984");
                Check(w.Hub.Store.Top(10).First().Id == win.Id && w.Hub.Store.RankOf(w.Hub.Store.Find(lose.Id)) == 2, "the leaderboard");
                var v = ReplaySim.Verify(ReplayFile.FromBytes(a.S.File.ToBytes()));
                Check(v.Ok, "the client's recording verifies: " + v);
                Check(a.S.Delay == 2, "no latency: the least delay");
                Console.WriteLine("        " + a.S.RallyHashes.Count + " rallies, delay " + a.S.Delay + ", " + w.Now.ToString("0") + " s");
            });

            Test("a client lying in its hashes: desync, unrated", () =>
            {
                var w = new World(new HubOptions { Seed = 9, RankedGamesToWin = 1 });
                var a = w.Add(new Bot("Ann", 11)); var b = new Bot("Ben", 12, "grinder")
                {
                    Tamper = m =>
                    {
                        if (NetMessages.TypeOf(m) != Msg.RallyHash) return m;
                        var r = NetMessages.Read(m); int i = r.ReadInt32(); ulong h = r.ReadUInt64(); int win = r.ReadInt32();
                        return NetMessages.RallyHash(i, h ^ 0xDEAD, win);
                    },
                };
                w.Add(b); w.Run(0.1);
                a.Send(NetMessages.Queue(QueueKind.Ranked)); b.Send(NetMessages.Queue(QueueKind.Ranked));
                w.Run(600, () => w.Hub.Finished.Count > 0);
                var res = w.Hub.Finished.Single();
                Check(res.Reason == "desync" && !res.Rated && res.Winner == -1, "desync, unrated: " + res.Reason);
                w.Run(0.1);
                Check(a.S.Finished && a.S.EndReason == "desync" && a.End != null && !a.End.Rated, "told");
                Check(w.Hub.Store.Find(a.Id).Rating == 1000 && w.Hub.Store.Find(b.Id).Rating == 1000, "no rating moves");
            });

            Test("dropped for good: a forfeit after the grace", () =>
            {
                var w = new World(new HubOptions { Seed = 9, RankedGamesToWin = 1 });
                var a = w.Add(new Bot("Ann", 11)); var b = w.Add(new Bot("Ben", 12, "grinder"));
                w.Run(0.1);
                a.Send(NetMessages.Queue(QueueKind.Ranked)); b.Send(NetMessages.Queue(QueueKind.Ranked));
                w.Run(30);
                Check(a.S != null && !a.S.Finished, "playing");
                a.L.Close();
                w.Run(15);
                Check(w.Hub.Finished.Count == 0 && b.S.WaitingSeconds > 10, "inside the grace: Ben waits");
                w.Run(10);
                var res = w.Hub.Finished.Single();
                Check(res.Reason == "forfeit" && res.Winner == b.Start.Slot && res.Rated, "a rated forfeit");
                Check(b.S.Finished && b.S.EndReason == "forfeit" && b.S.Winner == b.Start.Slot && b.End.RatingAfter == 1016, "Ben wins it");
            });

            Test("dropped and back: it catches up and plays on", () =>
            {
                var w = new World(new HubOptions { Seed = 9, RankedGamesToWin = 1 });
                var a = w.Add(new Bot("Ann", 11)); var b = w.Add(new Bot("Ben", 12, "grinder"));
                w.Run(0.1);
                a.Send(NetMessages.Queue(QueueKind.Ranked)); b.Send(NetMessages.Queue(QueueKind.Ranked));
                w.Run(60);
                Check(a.S != null && !a.S.Finished && a.S.RallyHashes.Count > 0, "some rallies in");
                long before = a.S.Tick;
                a.L.Close();
                w.Run(5);
                a.Connect(w.Hub);
                w.Run(0.5);
                Check(a.S != null && a.Start.Resuming && a.S.Tick >= before - 1 && !a.S.Resuming, "caught up to tick " + (a.S == null ? -1 : a.S.Tick) + " of " + before);
                w.Run(3000, () => a.S.Finished && b.S.Finished && w.Hub.Finished.Count > 0);
                var res = w.Hub.Finished.Single();
                Check(res.Reason == "" && a.S.EndReason == "" && b.S.EndReason == "", "played out: " + res.Reason + "/" + a.S.EndReason + "/" + b.S.EndReason);
                Check(res.Winner == a.S.Winner && res.Winner == b.S.Winner && a.S.RallyHashes.SequenceEqual(b.S.RallyHashes), "the same match");
                Console.WriteLine("        " + a.S.RallyHashes.Count + " rallies, back at tick " + before + ", " + w.Now.ToString("0") + " s");
            });

            Test("over real WebSockets", () =>
            {
                int port; var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
                var app = Program_Server.Start(port, out var hub);
                try
                {
                    var http = new HttpClient();
                    var health = http.GetStringAsync("http://127.0.0.1:" + port + "/").Result;
                    Check(health.Contains("\"ok\":true"), "health: " + health);
                    var ta = new WebSocketTransport("ws://127.0.0.1:" + port + "/ws");
                    var tb = new WebSocketTransport("ws://127.0.0.1:" + port + "/ws");
                    Wait(() => ta.Connected && tb.Connected, "connected");
                    ta.Send(NetMessages.Hello("wsann000001", "wsann-secret-0123456789", "Ann", "{}", "t", SimFingerprint.Current));
                    tb.Send(NetMessages.Hello("wsben000002", "wsben-secret-0123456789", "Ben", "{}", "t", SimFingerprint.Current));
                    ta.Send(NetMessages.CreateRoom(1, "par", "show"));
                    var code = NetMessages.ReadString(Next(ta, Msg.RoomCreated));
                    tb.Send(NetMessages.JoinRoom(code));
                    var sa = NetMessages.ReadMatchStart(Next(ta, Msg.MatchStart));
                    var sb = NetMessages.ReadMatchStart(Next(tb, Msg.MatchStart));
                    Check(sa.Slot == 0 && sb.Slot == 1 && sa.MatchId == sb.MatchId, "a match over the wire");
                    ta.Send(NetMessages.Input(sa.Delay, new QFrame { MoveX = 77 }));
                    var r = NetMessages.Read(Next(tb, Msg.Input)); Check(r.ReadInt64() == sa.Delay && NetMessages.ReadFrame(r).MoveX == 77, "relayed");
                    var lb = http.GetStringAsync("http://127.0.0.1:" + port + "/leaderboard?id=wsann000001").Result;
                    Check(lb.Contains("\"you\""), "leaderboard: " + lb);
                    ta.Close(); tb.Close();
                }
                finally { app.StopAsync().Wait(); }
            });

            Console.WriteLine(_fail == 0 ? "all passed" : _fail + " failed");
            return _fail == 0 ? 0 : 1;
        }

        static void Wait(Func<bool> ok, string what)
        {
            var sw = Stopwatch.StartNew();
            while (!ok()) { if (sw.Elapsed.TotalSeconds > 10) throw new Exception("timed out: " + what); System.Threading.Thread.Sleep(5); }
        }

        static byte[] Next(ITransport t, Msg type)
        {
            byte[] got = null;
            Wait(() => { while (t.TryReceive(out var m)) if (NetMessages.TypeOf(m) == type) { got = m; return true; } return false; }, type.ToString());
            return got;
        }
    }

    static class Program_Server
    {
        public static Microsoft.AspNetCore.Builder.WebApplication Start(int port, out Hub hub)
        {
            var app = SquashServer.Program.Build(null, "http://127.0.0.1:" + port, null, null, out hub);
            app.StartAsync().Wait();
            return app;
        }
    }
}
