// ===========================================================
// Assets/SquashSim/Net/LockstepSession.cs
// P2-M8 online: one online match, in lockstep.
//
// The sim is deterministic, so two machines that feed it the same inputs
// play the same match: only the inputs travel. Time is cut into ticks of
// 4 sim steps (1/60 s of sim), stepped by TickSim. Each client sends its
// own input for the tick Delay ticks ahead of the one being simulated,
// and a tick is simulated only once both players' inputs for it are in -
// so a little input delay, and a short wait if the other's input is late.
//
//   inputs     quantised (IntentCodec), fed to the sim decoded on both
//              machines
//   commands   "play now" between games goes in the stream, stamped with
//              the tick it applies to
//   checks     each rally's state hash is sent; a different one from the
//              other machine is a desync (the match ends, unrated)
//   resuming   back after a dropped connection: the server sends every
//              frame so far (Frames) and the sim catches up; this client
//              sends nothing until its own frames are in
//
// UnityEngine-free: the game, the server's verifier and MatchBench all
// run it.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim.Replays;

namespace SquashSim.Net
{
    public sealed class LockstepSession
    {
        public const int StepsPerTick = TickSim.StepsPerTick;
        /// <summary>How far past the delay a client may run ahead in sending (it then waits for the other).</summary>
        public const int MaxLead = 2;

        public readonly MatchStartInfo Info;
        public readonly TickSim Core;
        public MatchSimulation Sim { get { return Core.Sim; } }
        public ReplayFile File { get { return Core.File; } }
        public int Me { get { return Info.Slot; } }
        public int Them { get { return 1 - Info.Slot; } }
        public int Delay { get { return Core.Delay; } }
        /// <summary>The next tick to simulate.</summary>
        public long Tick { get { return Core.Tick; } }
        /// <summary>The next tick this client will send its input for.</summary>
        public long Sent { get; private set; }
        /// <summary>How long the sim has been held for the other's input (0 when flowing).</summary>
        public double WaitingSeconds { get; private set; }
        /// <summary>Back after a dropped connection, its own frames not yet in from the server.</summary>
        public bool Resuming { get; private set; }
        public bool Finished { get; private set; }
        public bool Desynced { get; private set; }
        /// <summary>"" a match played out; "desync"; "left" (the other went); "forfeit" (the server ended it).</summary>
        public string EndReason { get; private set; } = "";
        public int Winner { get; private set; } = -1;
        public MatchEndInfo ServerEnd { get; private set; }

        public static double TickSeconds { get { return TickSim.TickSeconds; } }
        public static double TickRealSeconds { get { return TickSim.TickRealSeconds; } }

        readonly ITransport _t;
        readonly Dictionary<int, ulong> _theirHash = new Dictionary<int, ulong>();
        double _clock;

        /// <summary>Build the match the server started (this sets PhysicsConfig.Current, as any match does).</summary>
        public LockstepSession(MatchStartInfo info, ITransport transport, string gameVersion = "", long createdTicks = 0)
        {
            Info = info;
            _t = transport;
            Core = new TickSim(info.Setup, info.Delay, gameVersion, createdTicks);
            Core.RallyDone += (i, rec) => { _t.Send(NetMessages.RallyHash(i, rec.Hash, rec.Winner)); CheckHashes(); };
            Sent = Core.Delay;
            Resuming = info.Resuming;
        }

        /// <summary>A message from the server (the other client's input, a command, a hash, the end). False: not the match's.</summary>
        public bool Handle(byte[] m)
        {
            var type = NetMessages.TypeOf(m);
            switch (type)
            {
                case Msg.Input:
                {
                    var r = NetMessages.Read(m);
                    long tick = r.ReadInt64();
                    Core.Put(Them, tick, NetMessages.ReadFrame(r));
                    return true;
                }
                case Msg.Frames:
                {
                    var f = NetMessages.ReadFrames(m);
                    for (int i = 0; i < f.Frames.Length; i++) Core.Put(f.Slot, f.Start + i, f.Frames[i]);
                    if (f.Slot == Me)
                    {
                        Sent = Math.Max(Sent, f.Start + f.Frames.Length);
                        if (f.Last) Resuming = false;
                    }
                    return true;
                }
                case Msg.Command:
                {
                    var r = NetMessages.Read(m);
                    long tick = r.ReadInt64();
                    Core.PutCommand(tick, (ReplayCommand)r.ReadByte());
                    return true;
                }
                case Msg.RallyHash:
                {
                    var r = NetMessages.Read(m);
                    int rally = r.ReadInt32();
                    _theirHash[rally] = r.ReadUInt64();
                    CheckHashes();
                    return true;
                }
                case Msg.Chat:
                    OnChat?.Invoke(NetMessages.ReadChat(m));                    // batch 4: not part of the match - just shown
                    return true;
                case Msg.OpponentLeft:
                    End("left", Me);
                    return true;
                case Msg.MatchEnd:
                    ServerEnd = NetMessages.ReadMatchEnd(m);
                    // played out (""): the server's result comes with the rating; this machine reaches the same end itself
                    if (!Finished && !string.IsNullOrEmpty(ServerEnd.Reason)) End(ServerEnd.Reason, ServerEnd.Winner);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Batch 4: the opponent said something (QuickChat.Phrases index).</summary>
        public System.Action<int> OnChat;

        /// <summary>Batch 4: say something to the opponent (the server passes it on, a few a minute at most).</summary>
        public void Chat(int phrase) { if (!Finished) _t.Send(NetMessages.Chat(phrase)); }

        /// <summary>A command from this client ("play now"): it applies, on both machines, at the next tick it sends.</summary>
        public void Command(ReplayCommand kind)
        {
            if (Finished || Resuming) return;
            Core.PutCommand(Sent, kind);
            _t.Send(NetMessages.Command(Sent, kind));
        }

        /// <summary>
        /// Each frame: real time passes; this client's input goes out for every tick due (the live intent, quantised; its
        /// presses then cleared); then every tick that both inputs are in for is simulated. <paramref name="fill"/>, when
        /// given, writes the live intent before each tick (an AI standing in for the person: tests, the bench).
        /// </summary>
        public void Pump(double realDt, InputIntent live, bool assisted, bool lines, Action<InputIntent> fill = null)
        {
            if (Finished) return;
            _clock += realDt;
            double tick = TickRealSeconds;
            while (_clock >= tick)
            {
                if (Resuming || Sent - Tick >= Delay + MaxLead) { _clock = tick; break; }     // far enough ahead: wait
                _clock -= tick;
                fill?.Invoke(live);
                var q = IntentCodec.Encode(live, assisted, lines);
                Core.Put(Me, Sent, q);
                _t.Send(NetMessages.Input(Sent, q));
                Sent++;
                live.ClearEdges();
            }
            Core.Use();
            bool stepped = Core.StepAll() > 0;
            if (!Finished && Core.Over) End("", Sim.Score.MatchWinner);
            WaitingSeconds = stepped || Sent <= Tick ? 0 : WaitingSeconds + realDt;
        }

        void CheckHashes()
        {
            var mine = Core.Hashes;
            foreach (var kv in _theirHash)
                if (kv.Key < mine.Count && mine[kv.Key] != kv.Value) { Desynced = true; End("desync", -1); return; }
        }

        void End(string reason, int winner)
        {
            if (Finished) return;
            Finished = true;
            EndReason = reason;
            Winner = winner;
            Core.Close(reason == "");
        }

        /// <summary>The rally hashes this machine computed (tests compare the two machines').</summary>
        public IReadOnlyList<ulong> RallyHashes { get { return Core.Hashes; } }
    }
}
