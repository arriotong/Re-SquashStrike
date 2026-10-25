// ===========================================================
// Assets/SquashSim/Net/TickSim.cs
// P2-M8 online: the match stepped one network tick at a time from both
// players' quantised inputs - the part every machine runs alike. The
// game's LockstepSession wraps it with a connection; the relay server
// runs one beside every match it relays and takes the result from it
// (so the result never rests on what a client says).
//
//   a tick      4 sim steps; the first gets the frame with its presses,
//               the other three the same frame without them (the
//               recorder's rule: the match records as a normal replay)
//   ticks < D   the first Delay ticks are neutral on both sides (nobody
//               has pressed anything yet)
//   commands    "play now" between games, stamped with the tick
//   config      the match's own SimConfig: Use() puts it back as the
//               current one (a server steps many matches in turn)
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim.Replays;

namespace SquashSim.Net
{
    public sealed class TickSim
    {
        public const int StepsPerTick = 4;

        public readonly MatchSimulation Sim;
        public readonly ReplayFile File = new ReplayFile();
        public readonly SimConfig Config;
        public readonly int Delay;
        /// <summary>The next tick to simulate.</summary>
        public long Tick { get; private set; }
        public bool Over { get { return Sim.Phase == MatchPhase.MatchOver; } }
        /// <summary>Each rally's state hash, in order.</summary>
        public readonly List<ulong> Hashes = new List<ulong>();
        /// <summary>A rally just ended (its index and record).</summary>
        public event Action<int, RallyRec> RallyDone;

        public static double TickSeconds { get { return StepsPerTick * PhysicsConfig.Current.Sim.FixedDt; } }
        /// <summary>A tick in real seconds (the game runs at GameTuning.Speed.Game of real time).</summary>
        public static double TickRealSeconds { get { return TickSeconds / GameTuning.Speed.Game; } }

        readonly InputIntent[] _fed = { new InputIntent(), new InputIntent() };
        readonly Dictionary<long, QFrame>[] _in = { new Dictionary<long, QFrame>(), new Dictionary<long, QFrame>() };
        readonly Dictionary<long, List<ReplayCommand>> _cmds = new Dictionary<long, List<ReplayCommand>>();
        readonly QFrame[] _last = new QFrame[2];
        readonly RallyWatch _watch = new RallyWatch();
        bool _rallyEnded;

        const ushort Edges = (ushort)(IntentCodec.FHitP | IntentCodec.FHitR | IntentCodec.FTouchP | IntentCodec.FTouchR |
                                      IntentCodec.FSplit | IntentCodec.FAppeal | IntentCodec.FDive | IntentCodec.FBoast);

        /// <summary>Build the match (this sets PhysicsConfig.Current, as any match does).</summary>
        public TickSim(ReplaySetup setup, int delay, string gameVersion = "", long createdTicks = 0)
        {
            setup.TwoPlayer = true;
            Delay = Math.Max(1, delay);
            ReplayMatch.Configure(setup);
            Config = PhysicsConfig.Current;
            Sim = ReplayMatch.Build(setup);
            Sim.Intents[0] = _fed[0]; Sim.Intents[1] = _fed[1];
            File.Setup = setup;
            File.GameVersion = gameVersion ?? "";
            File.Fingerprint = SimFingerprint.Current;
            File.CreatedTicks = createdTicks;
            File.Tracks[0] = new List<FrameRun>(); File.Tracks[1] = new List<FrameRun>();
            Sim.RallyEnded += o => _rallyEnded = true;
            for (long t = 0; t < Delay; t++) { _in[0][t] = default(QFrame); _in[1][t] = default(QFrame); }
        }

        /// <summary>This match's config becomes the current one.</summary>
        public void Use() { PhysicsConfig.Current = Config; }

        /// <summary>A player's input for a tick (one already simulated, or a repeat, is ignored).</summary>
        public void Put(int slot, long tick, QFrame f) { if (tick >= Tick) _in[slot][tick] = f; }

        public void PutCommand(long tick, ReplayCommand kind)
        {
            if (!_cmds.TryGetValue(tick, out var l)) _cmds[tick] = l = new List<ReplayCommand>();
            l.Add(kind);
        }

        public bool CanStep { get { return !Over && _in[0].ContainsKey(Tick) && _in[1].ContainsKey(Tick); } }

        /// <summary>Simulate every tick both inputs are in for. How many ran.</summary>
        public int StepAll()
        {
            int n = 0;
            while (CanStep) { StepTick(); n++; }
            return n;
        }

        void StepTick()
        {
            long t = Tick;
            if (_cmds.TryGetValue(t, out var cmds))
            {
                foreach (var c in cmds)
                    if (c == ReplayCommand.NextGame && Sim.Phase == MatchPhase.GameOver) { File.Commands.Add(new ReplayCmd { Step = Sim.StepCount, Kind = c }); Sim.NextGame(); }
                _cmds.Remove(t);
            }
            var q0 = _in[0][t]; var q1 = _in[1][t];
            _in[0].Remove(t); _in[1].Remove(t);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int s = 0; s < StepsPerTick && !Over; s++)
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
                    Hashes.Add(rec.Hash);
                    RallyDone?.Invoke(Hashes.Count - 1, rec);
                }
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

        /// <summary>The match is over (played out, or not: retired).</summary>
        public void Close(bool playedOut)
        {
            File.TotalSteps = Sim.StepCount;
            File.Winner = playedOut ? Sim.Score.MatchWinner : -1;
            File.Retired = !playedOut;
        }
    }
}
