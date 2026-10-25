// ===========================================================
// Tools/SquashServer/Program.cs
// The relay server's web host.
//
//   /ws                 the game connects here (binary WebSocket messages,
//                       SquashSim.Net.NetMessages)
//   /                   health: version, sim fingerprint, who is online
//   /leaderboard?id=    the top 100 (and you, with your id)
//   /player/{id}        one player's rating and record
//
//   PORT          listen port (default 5180; Fly.io sets 8080 in fly.toml)
//   SQUASH_DATA   the data file (default data/squash.json)
//
//   dotnet run --project Tools/SquashServer
// ===========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SquashServer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "5180";
            var data = Environment.GetEnvironmentVariable("SQUASH_DATA") ?? "data/squash.json";
            var opt = new HubOptions();
            if (int.TryParse(Environment.GetEnvironmentVariable("SQUASH_RANKED_GAMES"), out int g)) opt.RankedGamesToWin = Math.Clamp(g, 1, 3);   // the soak: shorter matches
            var app = Build(args, "http://0.0.0.0:" + port, data, opt, out var hub);
            Console.WriteLine("re:SquashStrike server on port " + port + ", data " + data);
            app.Run();
            lock (hub.Gate) hub.Store.Save();
        }

        static readonly Stopwatch Clock = Stopwatch.StartNew();
        public static double Now() { return Clock.Elapsed.TotalSeconds; }

        /// <summary>The web app (the tests start one on a free port, in memory).</summary>
        public static WebApplication Build(string[] args, string url, string dataPath, HubOptions opt, out Hub hub)
        {
            var builder = WebApplication.CreateBuilder(args ?? new string[0]);
            builder.WebHost.UseUrls(url);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            var app = builder.Build();
            var h = hub = new Hub(new Store(dataPath), Now, opt);

            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
            app.MapGet("/", ctx => ctx.Response.WriteAsJsonAsync(h.Health()));
            app.MapGet("/leaderboard", ctx => ctx.Response.WriteAsJsonAsync(h.Leaderboard(ctx.Request.Query["id"].ToString())));
            app.MapGet("/player/{id}", ctx =>
            {
                var c = h.PlayerCard((string)ctx.Request.RouteValues["id"]);
                if (c == null) { ctx.Response.StatusCode = 404; return Task.CompletedTask; }
                return ctx.Response.WriteAsJsonAsync(c);
            });
            app.Map("/ws", async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                using (var ws = await ctx.WebSockets.AcceptWebSocketAsync())
                    await Serve(h, ws, ctx.RequestAborted);
            });

            var stop = app.Lifetime.ApplicationStopping;
            Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    try { h.Tick(); } catch (Exception e) { Console.WriteLine("tick: " + e); }
                    try { await Task.Delay(250, stop); } catch (OperationCanceledException) { }
                }
            });
            return app;
        }

        /// <summary>One connection: a send queue drained by its own loop; each whole message received goes to the hub.</summary>
        static async Task Serve(Hub hub, WebSocket ws, CancellationToken abort)
        {
            var io = new SocketIo(ws);
            var peer = hub.Connect(io);
            var sending = io.Run(abort);
            var buf = new byte[64 * 1024];
            var acc = new List<byte>();
            try
            {
                while (ws.State == WebSocketState.Open && !abort.IsCancellationRequested)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), abort);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    for (int i = 0; i < res.Count; i++) acc.Add(buf[i]);
                    if (acc.Count > 1 << 20) break;                       // nobody sends a message that big
                    if (res.EndOfMessage) { hub.Receive(peer, acc.ToArray()); acc.Clear(); }
                }
            }
            catch (Exception) { }
            hub.Disconnected(peer);
            io.Close();
            try { await sending; } catch { }
            try { if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
        }

        sealed class SocketIo : IPeerIo
        {
            readonly WebSocket _ws;
            readonly Channel<byte[]> _out = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
            public SocketIo(WebSocket ws) { _ws = ws; }
            public void Send(byte[] m) { _out.Writer.TryWrite(m); }
            public void Close() { _out.Writer.TryComplete(); }
            public async Task Run(CancellationToken abort)
            {
                try
                {
                    while (await _out.Reader.WaitToReadAsync(abort))
                        while (_out.Reader.TryRead(out var m))
                            await _ws.SendAsync(new ArraySegment<byte>(m), WebSocketMessageType.Binary, true, abort);
                }
                catch (Exception) { }
            }
        }
    }
}
