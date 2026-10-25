// ===========================================================
// Tools/SquashServer.Tests/Soak.cs
// The net soak: the real server, as its own process (as deployed: the
// sim's config is one per process) on a free port, and N ranked matches
// between bot clients over real WebSockets, each link lagged 50-250 ms
// (plus jitter) on its way in and out. The clients play faster than real
// time (the lockstep does not mind), several matches at once.
//
//   dotnet run -c Release --project Tools/SquashServer.Tests -- --soak 20
//
// Passes when no match desyncs and the server's result is both clients'.
// ===========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;

namespace SquashServer.Tests
{
    /// <summary>A link with lag: what is sent leaves after the delay, what arrives is held for it (in order both ways).</summary>
    sealed class Laggy : ITransport
    {
        readonly ITransport _inner;
        readonly double _lat, _jit;
        readonly Random _rng;
        readonly Queue<(double at, byte[] m)> _in = new Queue<(double, byte[])>(), _out = new Queue<(double, byte[])>();
        double _lastIn, _lastOut;
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static double Now { get { return Clock.Elapsed.TotalSeconds; } }

        public Laggy(ITransport inner, double oneWay, double jitter, int seed) { _inner = inner; _lat = oneWay / 2; _jit = jitter; _rng = new Random(seed); }
        double At(ref double last) { double at = Math.Max(last, Now + _lat + _rng.NextDouble() * _jit); last = at; return at; }

        public void Send(byte[] m) { _out.Enqueue((At(ref _lastOut), m)); Flush(); }
        public void Flush() { while (_out.Count > 0 && _out.Peek().at <= Now) _inner.Send(_out.Dequeue().m); }
        public bool TryReceive(out byte[] m)
        {
            Flush();
            while (_inner.TryReceive(out var got)) _in.Enqueue((At(ref _lastIn), got));
            m = null;
            if (_in.Count == 0 || _in.Peek().at > Now) return false;
            m = _in.Dequeue().m;
            return true;
        }
        public bool Connected { get { return _inner.Connected; } }
        public void Close() { _inner.Close(); }
    }

    sealed class SoakClient
    {
        public readonly string Id, Name;
        public readonly Laggy L;
        public LockstepSession S;
        public MatchEndInfo End;
        readonly IAthleteBrain _ai;
        readonly InputIntent _live = new InputIntent();
        public SoakClient(string url, int i, double lag, double jitter)
        {
            Id = "soak" + i.ToString("00000000"); Name = "Bot " + i;
            L = new Laggy(new WebSocketTransport(url), lag, jitter, i);
            _ai = new PersonStandIn(new OpponentBrain(AiProfile.ByName(i % 2 == 0 ? "attacker" : "grinder").WithDifficulty(0.7), (uint)(100 + i)));
        }
        public void Hello() { L.Send(NetMessages.Hello(Id, "soak-secret-" + Id + "-0123456789", Name, "{}", "soak", SimFingerprint.Current)); L.Send(NetMessages.Queue(QueueKind.Ranked)); }
        public bool Done { get { return S != null && S.Finished && End != null; } }

        public void Pump(double dt)
        {
            while (L.TryReceive(out var m))
            {
                var t = NetMessages.TypeOf(m);
                if (t == Msg.Ping) { L.Send(NetMessages.Pong(NetMessages.ReadLong(m))); continue; }
                if (t == Msg.MatchStart) { S = new LockstepSession(NetMessages.ReadMatchStart(m), L); continue; }
                if (t == Msg.Error) Console.WriteLine("        " + Name + ": " + NetMessages.ReadString(m));
                if (t == Msg.MatchEnd) End = NetMessages.ReadMatchEnd(m);
                if (S != null) S.Handle(m);
            }
            if (S != null && !S.Finished) S.Pump(dt, _live, false, false, x => _ai.Fill(x, S.Sim, S.Me, LockstepSession.TickSeconds));
            L.Flush();
        }
    }

    static class Soak
    {
        public const double Speed = 12;      // sim-time speed-up on the clients
        public const int AtOnce = 6;          // matches at a time

        public static int Run(int matches)
        {
            Console.WriteLine("net soak: " + matches + " ranked matches over WebSockets, 50-250 ms lag");
            var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); int port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            // the server as its own process, as deployed (the sim's config is one per process)
            var dll = System.IO.Path.Combine(AppContext.BaseDirectory, "SquashServer.dll");
            var data = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "squash-soak-" + port, "squash.json");
            var psi = new ProcessStartInfo("dotnet", "\"" + dll + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.Environment["PORT"] = port.ToString(); psi.Environment["SQUASH_DATA"] = data; psi.Environment["SQUASH_RANKED_GAMES"] = "1";
            var server = Process.Start(psi);
            server.OutputDataReceived += (o, e) => { }; server.ErrorDataReceived += (o, e) => { };
            server.BeginOutputReadLine(); server.BeginErrorReadLine();
            using (var http = new System.Net.Http.HttpClient())
            {
                var up = Stopwatch.StartNew();
                while (true)
                {
                    try { if (http.GetStringAsync("http://127.0.0.1:" + port + "/").Result.Contains("\"ok\":true")) break; } catch { }
                    if (up.Elapsed.TotalSeconds > 20) { Console.WriteLine("  FAIL  the server did not start"); server.Kill(); return 1; }
                    System.Threading.Thread.Sleep(100);
                }
            }
            string url = "ws://127.0.0.1:" + port + "/ws";
            var rng = new Random(1);
            int started = 0, done = 0, bad = 0, rallies = 0;
            var live = new List<SoakClient[]>();
            var sw = Stopwatch.StartNew();
            double last = 0;
            try
            {
                while (done < matches)
                {
                    while (live.Count < AtOnce && started < matches)
                    {
                        // both of a pair: the same rating (1000) and queued together, so the server pairs them with each other
                        var pair = new SoakClient[2];
                        for (int k = 0; k < 2; k++) pair[k] = new SoakClient(url, started * 2 + k, 0.05 + rng.NextDouble() * 0.2, 0.03);
                        var t0 = Stopwatch.StartNew();
                        while (!(pair[0].L.Connected && pair[1].L.Connected) && t0.Elapsed.TotalSeconds < 10) System.Threading.Thread.Sleep(5);
                        foreach (var c in pair) c.Hello();
                        live.Add(pair);
                        started++;
                    }
                    double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - last) * Speed;
                    last = now;
                    var everyone = live.SelectMany(p => p).ToList();
                    foreach (var c in everyone) c.Pump(dt);
                    foreach (var pair in live.ToList())
                    {
                        if (!pair.All(c => c.Done)) continue;
                        live.Remove(pair);
                        done++;
                        var a = pair[0].S; var b = pair[1].S;
                        // each client against the server's verdict (its MatchEnd)
                        foreach (var c in pair)
                        {
                            var e = c.End;
                            bool ok = !c.S.Desynced && c.S.EndReason == "" && e.Reason == "" && e.Winner == c.S.Winner && e.Rated;
                            if (!ok) { bad++; Console.WriteLine("  BAD   match " + c.S.Info.MatchId + ": client " + c.S.EndReason + "/" + c.S.Winner + " server " + e.Reason + "/" + e.Winner); }
                        }
                        rallies += a.RallyHashes.Count;
                        Console.WriteLine("  ok    match " + done + "  (" + a.RallyHashes.Count + " rallies, delay " + a.Delay + ")");
                        foreach (var c in pair) c.L.Close();
                    }
                    if (sw.Elapsed.TotalMinutes > 20) { Console.WriteLine("  FAIL  out of time"); bad++; break; }
                    System.Threading.Thread.Sleep(2);
                }
            }
            finally { try { server.Kill(); } catch { } }
            Console.WriteLine(bad == 0 ? "soak passed: " + done + " matches, " + rallies + " rallies, no desync, the server agreed every time (" + sw.Elapsed.TotalSeconds.ToString("0") + " s)" : bad + " bad");
            return bad == 0 ? 0 : 1;
        }
    }
}
