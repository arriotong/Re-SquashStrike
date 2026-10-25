// ===========================================================
// Assets/SquashSim/Net/LockstepSession.cs
// P2-M8 online: one online match, in lockstep.
//
// The sim is deterministic, so two machines that feed it the same inputs
// play the same match: only the inputs travel. Time is cut into ticks of
// 4 sim steps (1/60 s of sim). Each client sends its own input for the
// tick Delay ticks ahead of the one being simulated, and a tick is
// simulated only once both players' inputs for it are in - so a little
// input delay, and a short wait if the other's input is late.
//
//   inputs     quantised (IntentCodec), fed to the sim decoded on both
//              machines; the first step of a tick gets the presses, the
//              other three the same frame without them (a recorder's
//              rule, so the match records as a normal replay)
//   commands   "play now" between games goes in the stream, stamped with
//              the tick it applies to
//   checks     each rally's state hash is sent; a different one from the
//              other machine is a desync (the match ends, unrated)
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
        public const int StepsPerTick = 4;
        /// <summary>How far past the delay a client may run ahead in sending (it then waits for the other).</summary>
        public const int MaxLead = 2;

        public readonly MatchStartInfo Info;
        public readonly MatchSimulation Sim;
        public readonly ReplayFile File = new ReplayFile();
        public int Me { get { return Info.Slot; } }
        public int Them { get { return 1 - Info.Slot; } }
        public int Delay { get { return Info.Delay; } }
        /// <summary>The next tick to simulate.</summary>
        public long Tick { get; private set; }
        /// <summary>The next tick this client will send its input for.</summary>
        public long Sent { get; private set; }
        /// <summary>How long the sim has been held for the other's input (0 when flowing).</summary>
        public double WaitingSeconds { get; private set; }
        public bool Finished { get; private set; }
        public bool Desynced { get; private set; }
        /// <summary>"" a match played out; "desync"; "left" (the other went); "forfeit" (the server ended it).</summary>
        public string EndReason { get; private set; } = "";
        public int Winner { get; private set; } = -1;
        public MatchEndInfo ServerEnd { get; private set; }

        public static double TickSeconds { get { return StepsPerTick * PhysicsConfig.Current.Sim.FixedDt; } }
        /// <summary>A tick in real seconds (the game runs at GameTuning.Speed.Game of real time).</summary>
        public static double TickRealSeconds { get { return TickSeconds / GameTuning.Speed.Game; } }

        readonly ITransport _t;
        readonly InputIntent[] _fed = { new InputIntent(), new InputIntent() };
        readonly Dictionary<long, QFrame>[] _in = { new Dictionary<long, QFrame>(), new Dictionary<long, QFrame>() };
        readonly Dictionary<long, List<ReplayCommand>> _cmds = new Dictionary<long, List<ReplayCommand>>();
        readonly Dictionary<int, ulong> _theirHash = new Dictionary<int, ulong>();
        readonly List<ulong> _myHash = new List<ulong>();
        readonly QFrame[] _last = new QFrame[2];
        readonly RallyWatch _watch = new RallyWatch();
        double _clock;
        bool _rallyEnded;

        const ushort Edges = (ushort)(IntentCodec.FHitP | IntentCodec.FHitR | IntentCodec.FTouchP | IntentCodec.FTouchR |
                                      IntentCodec.FSplit | IntentCodec.FAppeal | IntentCodec.FDive | IntentCodec.FBoast);

        /// <summary>Build the match the server started (this sets PhysicsConfig.Current, as any match does).</summary>
        public LockstepSession(MatchStartInfo info, ITransport transport, string gameVersion = "", long createdTicks = 0)
        {
            Info = info;
            _t = transport;
            info.Setup.TwoPlayer = true;
            ReplayMatch.Configure(info.Setup);
            Sim = ReplayMatch.Build(info.Setup);
            Sim.Intents[0] = _fed[0]; Sim.Intents[1] = _fed[1];
            File.Setup = info.Setup;
            File.GameVersion = gameVersion ?? "";
            File.Fingerprint = SimFingerprint.Current;
            File.CreatedTicks = createdTicks;
            File.Tracks[0] = new List<FrameRun>(); File.Tracks[1] = new List<FrameRun>();
            Sim.RallyEnded += o => _rallyEnded = true;
            // the first Delay ticks: nobody has pressed anything yet (the same neutral frames on both machines)
            for (long t = 0; t < Math.Max(1, info.Delay); t++) { _in[0][t] = default(QFrame); _in[1][t] = default(QFrame); }
            Sent = Math.Max(1, info.Delay);
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
                    _in[Them][tick] = NetMessages.ReadFrame(r);
                    return true;
                }
                case Msg.Command:
                {
                    var r = NetMessages.Read(m);
                    long tick = r.ReadInt64();
                    AddCommand(tick, (ReplayCommand)r.ReadByte());
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
                case Msg.OpponentLeft:
                    End("left", Me);
                    return true;
                case Msg.MatchEnd:
                    ServerEnd = NetMessages.ReadMatchEnd(m);
                    if (!Finished) End(string.IsNullOrEmpty(ServerEnd.Reason) ? "forfeit" : ServerEnd.Reason, ServerEnd.Winner);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A command from this client ("play now"): it applies, on both machines, at the next tick it sends.</summary>
        public void Command(ReplayCommand kind)
        {
            if (Finished) return;
            AddCommand(Sent, kind);
            _t.Send(NetMessages.Command(Sent, kind));
        }

        void AddCommand(long tick, ReplayCommand kind)
        {
            if (!_cmds.TryGetValue(tick, out var l)) _cmds[tick] = l = new List<ReplayCommand>();
            l.Add(kind);
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
                if (Sent - Tick >= Delay + MaxLead) { _clock = tick; break; }     // far enough ahead: wait for the other
                _clock -= tick;
                fill?.Invoke(live);
                var q = IntentCodec.Encode(live, assisted, lines);
                _in[Me][Sent] = q;
                _t.Send(NetMessages.Input(Sent, q));
                Sent++;
                live.ClearEdges();
            }
            bool stepped = false;
            while (!Finished && _in[0].ContainsKey(Tick) && _in[1].ContainsKey(Tick)) { StepTick(Tick); stepped = true; }
            WaitingSeconds = stepped || Sent <= Tick ? 0 : WaitingSeconds + realDt;
        }

        void StepTick(long t)
        {
            if (_cmds.TryGetValue(t, out var cmds))
            {
                foreach (var c in cmds)
                {
                    if (c == ReplayCommand.NextGame && Sim.Phase == MatchPhase.GameOver) { File.Commands.Add(new ReplayCmd { Step = Sim.StepCount, Kind = c }); Sim.NextGame(); }
                }
                _cmds.Remove(t);
            }
            var q0 = _in[0][t]; var q1 = _in[1][t];
            _in[0].Remove(t); _in[1].Remove(t);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int s = 0; s < StepsPerTick && !Finished; s++)
            {
                Feed(0, s == 0 ? q0 : NoEdges(q0));
                Feed(1, s == 0 ? q1 : NoEdges(q1));
                Sim.Step(dt);
                _watch.Observe(Sim);
                if (_rallyEnded)
                {
                    _rallyEnded = false;
                    var rec = RallyRec.Of(Sim);
                    _watch.Fill(rec);
                    File.Rallies.Add(rec);
                    _myHash.Add(rec.Hash);
                    _t.Send(NetMessages.RallyHash(_myHash.Count - 1, rec.Hash, rec.Winner));
                    CheckHashes();
                }
                if (Sim.Phase == MatchPhase.MatchOver) End("", Sim.Score.MatchWinner);
            }
            Tick = t + 1;
        }

        static QFrame NoEdges(QFrame q) { q.Flags = (ushort)(q.Flags & ~Edges); return q; }

        void Feed(int i, QFrame q)
        {
            IntentCodec.Decode(q, _fed[i]);
            Sim.Players[i].Arcade.Assisted = q.Assisted;
            Sim.Players[i].Arcade.ShotLines = q.Lines;
            var track = File.Tracks[i];
            if (track.Count > 0 && _last[i].Equals(q)) { var r = track[track.Count - 1]; r.Count++; track[track.Count - 1] = r; }
            else { track.Add(new FrameRun { Frame = q, Count = 1 }); _last[i] = q; }
        }

        void CheckHashes()
        {
            foreach (var kv in _theirHash)
                if (kv.Key < _myHash.Count && _myHash[kv.Key] != kv.Value) { Desynced = true; End("desync", -1); return; }
        }

        void End(string reason, int winner)
        {
            if (Finished) return;
            Finished = true;
            EndReason = reason;
            Winner = winner;
            File.TotalSteps = Sim.StepCount;
            File.Winner = reason == "" ? Sim.Score.MatchWinner : -1;
            File.Retired = reason != "";
        }

        /// <summary>The rally hashes this machine computed (tests compare the two machines').</summary>
        public IReadOnlyList<ulong> RallyHashes { get { return _myHash; } }
    }
}
