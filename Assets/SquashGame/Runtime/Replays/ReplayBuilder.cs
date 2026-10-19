// ===========================================================
// Assets/SquashGame/Runtime/Replays/ReplayBuilder.cs
// P2-M6 Replay Theatre: a replay turned into something to watch.
//
// One pass of the headless ReplaySim, a few milliseconds a frame, keeps
// each rally as 60 frames a second (the same RenderSnapshot the court's
// views already draw) with its sim events (for the sound): from its
// serve set-up (trimmed to 1.5 s before the toss) to 1.5 s after it
// ended. A rally can be watched as soon as the pass is past it.
// ===========================================================

using System.Collections.Generic;
using System.Diagnostics;
using SquashSim;
using SquashSim.Replays;

namespace SquashGame.Replays
{
    public sealed class RallyClip
    {
        public int Index;
        public RallyRec Rec;
        public RallyTag Tags;
        /// <summary>The score before it (as the bug showed when it was served).</summary>
        public int Before0, Before1, GamesBefore0, GamesBefore1;
        public int Server;
        public long From, To;
        public readonly List<RenderSnapshot> Frames = new List<RenderSnapshot>();
        public readonly List<List<SimEvent>> Events = new List<List<SimEvent>>();
        /// <summary>The frame the rally ended on (the call).</summary>
        public int EndFrame;
        public bool Ready;
        public float Seconds { get { return Frames.Count / SimRunnerBase.FrameRate; } }

        internal List<SimEvent> Pending = new List<SimEvent>();
        internal int TossFrame = -1;
        internal long Trimmed;
    }

    public sealed class ReplayBuilder
    {
        public const int StepsPerFrame = 4;                 // 240 Hz steps -> 60 frames a second
        public const double LeadSeconds = 1.5, TailSeconds = 1.5;

        public readonly ReplayFile File;
        public readonly RallyClip[] Clips;
        readonly ReplaySim _sim;
        readonly SimConfig _config;
        int _first;

        public bool Done { get; private set; }
        public bool Diverged { get { return _sim.Diverged; } }
        public int FirstMismatch { get { return _sim.FirstMismatch; } }
        public float Progress { get { return File.TotalSteps <= 0 ? 1f : (float)_sim.Sim.StepCount / File.TotalSteps; } }
        public int ReadyCount { get { int n = 0; foreach (var c in Clips) if (c.Ready) n++; return n; } }

        /// <summary>Set up the pass (the replay's config is set while it runs; Run restores the caller's).</summary>
        public ReplayBuilder(ReplayFile file)
        {
            File = file;
            var tags = Highlights.Tag(file);
            Clips = new RallyClip[file.Rallies.Count];
            double dt = 1.0 / 240;
            for (int k = 0; k < Clips.Length; k++)
            {
                var r = file.Rallies[k];
                var c = new RallyClip { Index = k, Rec = r, Tags = tags[k], From = r.ServeStep, To = System.Math.Min(r.EndStep + (long)(TailSeconds / dt), file.TotalSteps) };
                if (k > 0)
                {
                    var p = file.Rallies[k - 1];
                    bool newGame = (tags[k - 1] & (RallyTag.GameBall | RallyTag.MatchBall)) != 0;
                    c.Before0 = newGame ? 0 : p.Points0; c.Before1 = newGame ? 0 : p.Points1;
                    c.GamesBefore0 = p.Games0; c.GamesBefore1 = p.Games1;
                }
                Clips[k] = c;
            }
            var keep = PhysicsConfig.Current;
            _sim = new ReplaySim(file);
            _config = PhysicsConfig.Current;
            PhysicsConfig.Current = keep;
            for (int k = 0; k < Clips.Length; k++) Clips[k].Server = k == 0 ? file.Setup.FirstServer : -1;
        }

        /// <summary>Carry on for up to this many milliseconds (or the whole thing: ms &lt;= 0).</summary>
        public void Run(double ms)
        {
            if (Done) return;
            var keep = PhysicsConfig.Current;
            PhysicsConfig.Current = _config;
            try
            {
                var sw = Stopwatch.StartNew();
                while (!Done && (ms <= 0 || sw.Elapsed.TotalMilliseconds < ms))
                    for (int i = 0; i < 48 && !Done; i++) StepOnce();
            }
            finally { PhysicsConfig.Current = keep; }
        }

        void StepOnce()
        {
            var m = _sim.Sim;
            var phase = m.Phase;
            _sim.Step();
            long s = m.StepCount;
            for (int k = _first; k < Clips.Length && Clips[k].From <= s; k++)
            {
                var c = Clips[k];
                if (c.Ready) { if (k == _first) _first++; continue; }
                if (s > c.To) { Finish(c); if (k == _first) _first++; continue; }
                if (c.Server < 0 && phase == MatchPhase.AwaitToss) c.Server = m.Score.Server;
                c.Pending.AddRange(m.StepEvents);
                if (c.TossFrame < 0 && phase == MatchPhase.AwaitToss && m.Phase == MatchPhase.Rally) c.TossFrame = c.Frames.Count;
                if ((s - c.From) % StepsPerFrame == 0)
                {
                    c.Frames.Add(RenderSnapshot.Capture(m.Ball, m.Players));
                    c.Events.Add(c.Pending);
                    c.Pending = new List<SimEvent>();
                }
            }
            if (_sim.Done)
            {
                Done = true;
                foreach (var c in Clips) if (!c.Ready) Finish(c);
            }
        }

        /// <summary>The rally is captured: trim the wait before the toss, mark where it ended.</summary>
        void Finish(RallyClip c)
        {
            int lead = (int)(LeadSeconds * SimRunnerBase.FrameRate);
            int cut = c.TossFrame > lead ? c.TossFrame - lead : 0;
            if (cut > 0) { c.Frames.RemoveRange(0, cut); c.Events.RemoveRange(0, cut); c.Trimmed = cut; }
            c.EndFrame = (int)((c.Rec.EndStep - c.From) / StepsPerFrame) - cut;
            if (c.EndFrame >= c.Frames.Count) c.EndFrame = c.Frames.Count - 1;
            if (c.EndFrame < 0) c.EndFrame = 0;
            if (c.Server < 0) c.Server = 0;
            c.Ready = true;
        }
    }
}
