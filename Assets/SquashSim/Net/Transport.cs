// ===========================================================
// Assets/SquashSim/Net/Transport.cs
// P2-M8 online: how messages travel. In order, whole messages (a
// WebSocket is ordered and reliable; the lockstep relies on it).
//
//   WebSocketTransport   the real thing (System.Net.WebSockets: Unity and
//                        .NET alike); a background receive loop queues
//                        messages, read on the caller's thread
//   LoopbackTransport    two ends in memory, with latency and jitter on a
//                        clock the test drives (the lockstep tests)
// ===========================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace SquashSim.Net
{
    public interface ITransport
    {
        void Send(byte[] message);
        bool TryReceive(out byte[] message);
        bool Connected { get; }
        void Close();
    }

    public sealed class LoopbackTransport : ITransport
    {
        sealed class Pending { public double At; public byte[] Data; }
        readonly Queue<Pending> _inbox = new Queue<Pending>();
        LoopbackTransport _peer;
        readonly Func<double> _now;
        readonly double _latency, _jitter;
        readonly System.Random _rng;
        double _lastAt;
        bool _closed;

        LoopbackTransport(Func<double> now, double latency, double jitter, int seed)
        { _now = now; _latency = latency; _jitter = jitter; _rng = new System.Random(seed); }

        /// <summary>Two connected ends. One-way delay = latency + up to jitter (order kept, as on a WebSocket).</summary>
        public static void Pair(Func<double> now, double latency, double jitter, int seed, out LoopbackTransport a, out LoopbackTransport b)
        {
            a = new LoopbackTransport(now, latency, jitter, seed);
            b = new LoopbackTransport(now, latency, jitter, seed + 1);
            a._peer = b; b._peer = a;
        }

        public bool Connected { get { return !_closed && _peer != null && !_peer._closed; } }

        public void Send(byte[] message)
        {
            if (!Connected) return;
            double at = _now() + _latency + _rng.NextDouble() * _jitter;
            if (at < _peer._lastAt) at = _peer._lastAt;              // in order
            _peer._lastAt = at;
            _peer._inbox.Enqueue(new Pending { At = at, Data = (byte[])message.Clone() });
        }

        public bool TryReceive(out byte[] message)
        {
            message = null;
            if (_inbox.Count == 0 || _inbox.Peek().At > _now()) return false;
            message = _inbox.Dequeue().Data;
            return true;
        }

        public void Close() { _closed = true; }
    }

    public sealed class WebSocketTransport : ITransport
    {
        readonly ClientWebSocket _ws = new ClientWebSocket();
        readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();
        readonly BlockingCollection<byte[]> _outbox = new BlockingCollection<byte[]>();
        readonly CancellationTokenSource _stop = new CancellationTokenSource();
        public string Failure { get; private set; }

        public bool Connected { get { return _ws.State == WebSocketState.Open; } }
        public bool Connecting { get { return _ws.State == WebSocketState.Connecting || _ws.State == WebSocketState.None; } }

        /// <summary>Connect in the background; Connected turns true when it is open (Failure says why not).</summary>
        public WebSocketTransport(string url)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _ws.ConnectAsync(new Uri(url), _stop.Token);
                    var send = Task.Run(SendLoop);
                    await ReceiveLoop();
                }
                catch (Exception e) { Failure = e.Message; }
            });
        }

        async Task ReceiveLoop()
        {
            var buf = new byte[64 * 1024];
            var acc = new List<byte>();
            while (_ws.State == WebSocketState.Open && !_stop.IsCancellationRequested)
            {
                var res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _stop.Token);
                if (res.MessageType == WebSocketMessageType.Close) break;
                for (int i = 0; i < res.Count; i++) acc.Add(buf[i]);
                if (res.EndOfMessage) { _inbox.Enqueue(acc.ToArray()); acc.Clear(); }
            }
        }

        async Task SendLoop()
        {
            try
            {
                foreach (var m in _outbox.GetConsumingEnumerable(_stop.Token))
                    await _ws.SendAsync(new ArraySegment<byte>(m), WebSocketMessageType.Binary, true, _stop.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Failure = e.Message; }
        }

        public void Send(byte[] message) { if (!_outbox.IsAddingCompleted) _outbox.Add(message); }
        public bool TryReceive(out byte[] message) { return _inbox.TryDequeue(out message); }

        public void Close()
        {
            try { _outbox.CompleteAdding(); _stop.Cancel(); _ws.Abort(); } catch { }
        }
    }
}
