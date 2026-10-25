// ===========================================================
// Tools/SquashServer.Tests/Smoke.cs
// A deployed server, checked from here: two bot clients over its real
// address (wss://...), a private room (unrated: the ladder stays clean),
// a minute of the match in lockstep, the rallies agreeing, then both leave.
//
//   dotnet run -c Release --project Tools/SquashServer.Tests -- --smoke wss://re-squashstrike.fly.dev/ws
// ===========================================================

using System;
using System.Diagnostics;
using SquashSim;
using SquashSim.Net;
using SquashSim.Replays;

namespace SquashServer.Tests
{
    static class Smoke
    {
        public static int Run(string url)
        {
            Console.WriteLine("smoke: " + url);
            var tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            var t = new[] { new WebSocketTransport(url), new WebSocketTransport(url) };
            var sw = Stopwatch.StartNew();
            while (!(t[0].Connected && t[1].Connected))
            {
                if (sw.Elapsed.TotalSeconds > 15) { Console.WriteLine("  FAIL  could not connect: " + (t[0].Failure ?? t[1].Failure)); return 1; }
                System.Threading.Thread.Sleep(10);
            }
            Console.WriteLine("  ok    connected (" + sw.ElapsedMilliseconds + " ms)");
            for (int i = 0; i < 2; i++) t[i].Send(NetMessages.Hello("smoke" + tag + i, "smoke-secret-" + tag + i + "-0123456789", "Smoke " + i, "{}", "smoke", SimFingerprint.Current));
            t[0].Send(NetMessages.CreateRoom(1, "par", "show"));

            var s = new LockstepSession[2];
            var ai = new IAthleteBrain[2];
            var live = new[] { new InputIntent(), new InputIntent() };
            string code = null;
            long pingAt = 0; double rtt = -1;
            sw.Restart();
            var clock = Stopwatch.StartNew();
            double last = 0;
            while (sw.Elapsed.TotalSeconds < 90)
            {
                for (int i = 0; i < 2; i++)
                    while (t[i].TryReceive(out var m))
                    {
                        var ty = NetMessages.TypeOf(m);
                        if (ty == Msg.Ping) { t[i].Send(NetMessages.Pong(NetMessages.ReadLong(m))); continue; }
                        if (ty == Msg.Pong) { rtt = clock.Elapsed.TotalSeconds - NetMessages.ReadLong(m) / 1000.0; continue; }
                        if (ty == Msg.Error) { Console.WriteLine("  FAIL  server: " + NetMessages.ReadString(m)); return 1; }
                        if (ty == Msg.Welcome && i == 0) Console.WriteLine("  ok    welcome (the same game version as the server)");
                        if (ty == Msg.RoomCreated) { code = NetMessages.ReadString(m); Console.WriteLine("  ok    room " + code); t[1].Send(NetMessages.JoinRoom(code)); }
                        if (ty == Msg.MatchStart)
                        {
                            var info = NetMessages.ReadMatchStart(m);
                            s[i] = new LockstepSession(info, t[i]);
                            ai[i] = new PersonStandIn(new OpponentBrain(AiProfile.ByName(i == 0 ? "attacker" : "grinder").WithDifficulty(0.7), (uint)(30 + i)));
                            if (i == 1) Console.WriteLine("  ok    match " + info.MatchId + " started, input delay " + info.Delay + " ticks");
                            continue;
                        }
                        if (s[i] != null) s[i].Handle(m);
                    }
                double now = clock.Elapsed.TotalSeconds, dt = now - last; last = now;
                if ((long)now > pingAt) { pingAt = (long)now + 2; t[0].Send(NetMessages.Ping((long)(now * 1000))); }
                for (int i = 0; i < 2; i++)
                    if (s[i] != null && !s[i].Finished) { int k = i; s[i].Pump(dt * 3, live[i], false, false, x => ai[k].Fill(x, s[k].Sim, s[k].Me, LockstepSession.TickSeconds)); }
                if (s[0] != null && s[1] != null && s[0].RallyHashes.Count >= 4 && s[1].RallyHashes.Count >= 4) break;
                System.Threading.Thread.Sleep(5);
            }
            if (s[0] == null || s[1] == null) { Console.WriteLine("  FAIL  no match started"); return 1; }
            bool same = true;
            int n = Math.Min(s[0].RallyHashes.Count, s[1].RallyHashes.Count);
            for (int i = 0; i < n; i++) same &= s[0].RallyHashes[i] == s[1].RallyHashes[i];
            bool ok = n >= 4 && same && !s[0].Desynced && !s[1].Desynced;
            Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + n + " rallies, the same on both" + (rtt >= 0 ? "  ·  round trip " + (rtt * 1000).ToString("0") + " ms" : ""));
            for (int i = 0; i < 2; i++) { t[i].Send(NetMessages.Simple(Msg.Leave)); }
            System.Threading.Thread.Sleep(300);
            for (int i = 0; i < 2; i++) t[i].Close();
            Console.WriteLine(ok ? "smoke passed" : "smoke failed");
            return ok ? 0 : 1;
        }
    }
}
