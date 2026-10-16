// ===========================================================
// Assets/SquashGame/Runtime/SimRunnerBase.cs
// Drives a SquashSim simulation from Unity's frame loop (port of the
// lab/main.js render loop), shared by the warm-up court and the match:
//
//   1. sample input into intent (IntentSource)
//   2. hand REAL elapsed time to the fixed-step clock (SimulationClock)
//   3. after each fixed step keep the previous and current render
//      snapshots, so views interpolate between them
//
// Runs in Update, NOT FixedUpdate: Unity's physics timestep has no say
// over the simulation. Views read View / Ball / Athletes; nothing here
// writes simulation state except through the simulation's own API.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Lab;
using UnityEngine;

namespace SquashGame
{
    /// <summary>One athlete as drawn (world space).</summary>
    public struct AthleteSnapshot
    {
        public Vector3 Pos;
        public Vector3 Forward;
        public Vector3 ShoulderForward;     // commitment solver BODY yaw: what an opponent reads
        public Vector3 RacketAnchor;
        public Vector3 RacketHead;
        public Vector3 RacketFaceNormal;

        public static AthleteSnapshot Capture(TestPlayerController c)
        {
            return new AthleteSnapshot
            {
                Pos = SimToWorld.Pos(c.Player.Pos),
                Forward = SimToWorld.YawForward(c.Player.Facing),
                ShoulderForward = SimToWorld.YawForward(c.IntentSolver.BodyYaw),
                RacketAnchor = SimToWorld.Pos(c.Racket.Anchor),
                RacketHead = SimToWorld.Pos(c.Racket.HeadPos),
                RacketFaceNormal = SimToWorld.Dir(c.Racket.FaceNormal)
            };
        }

        public static AthleteSnapshot Lerp(AthleteSnapshot a, AthleteSnapshot b, float t)
        {
            return new AthleteSnapshot
            {
                Pos = Vector3.LerpUnclamped(a.Pos, b.Pos, t),
                Forward = Vector3.Slerp(a.Forward, b.Forward, t),
                ShoulderForward = Vector3.Slerp(a.ShoulderForward, b.ShoulderForward, t),
                RacketAnchor = Vector3.LerpUnclamped(a.RacketAnchor, b.RacketAnchor, t),
                RacketHead = Vector3.LerpUnclamped(a.RacketHead, b.RacketHead, t),
                RacketFaceNormal = Vector3.Slerp(a.RacketFaceNormal, b.RacketFaceNormal, t)
            };
        }
    }

    /// <summary>Everything the views draw, captured once per fixed step.</summary>
    public struct RenderSnapshot
    {
        public Vector3 BallPos;
        public int Count;
        public AthleteSnapshot A0, A1;

        public AthleteSnapshot Athlete(int i) { return i == 0 ? A0 : A1; }

        public static RenderSnapshot Capture(BallState ball, TestPlayerController[] athletes)
        {
            var s = new RenderSnapshot { BallPos = SimToWorld.Pos(ball.Pos), Count = athletes.Length };
            if (athletes.Length > 0) s.A0 = AthleteSnapshot.Capture(athletes[0]);
            if (athletes.Length > 1) s.A1 = AthleteSnapshot.Capture(athletes[1]);
            return s;
        }

        public static RenderSnapshot Lerp(RenderSnapshot a, RenderSnapshot b, float t)
        {
            return new RenderSnapshot
            {
                BallPos = Vector3.LerpUnclamped(a.BallPos, b.BallPos, t),
                Count = b.Count,
                A0 = AthleteSnapshot.Lerp(a.A0, b.A0, t),
                A1 = AthleteSnapshot.Lerp(a.A1, b.A1, t)
            };
        }
    }

    [DefaultExecutionOrder(-100)]
    public abstract class SimRunnerBase : MonoBehaviour
    {
        [Tooltip("Interpolate views between fixed steps. Off = show the last step exactly.")]
        public bool Interpolate = true;

        public SimulationClock Clock { get; private set; }
        public IntentSource Input { get; private set; }

        /// <summary>The state views should draw this frame.</summary>
        public RenderSnapshot View { get; private set; }

        public abstract BallState Ball { get; }
        public abstract TestPlayerController[] Athletes { get; }
        public abstract IReadOnlyList<SimEvent> StepEvents { get; }
        /// <summary>Index of the athlete the local player controls (aim recentring, HUD).</summary>
        public virtual int HumanIndex { get { return 0; } }

        // ---- debug state (presentation only) ----
        public bool FreezeOnCollision;
        public bool FreezeOnRacket = true;
        public readonly Queue<Vector3> Trail = new Queue<Vector3>();
        public const int TrailMaxPoints = 400;
        public readonly List<SimEvent> RecentEvents = new List<SimEvent>();
        public const int RecentEventMax = 24;
        public SimEvent LastImpact { get; private set; }
        public SimEvent LastRacketContact { get; private set; }
        public string LastFirstWall { get; private set; }
        bool _awaitingFirstWall;

        /// <summary>Raised after every fixed step with that step's events (audio, VFX, debug).</summary>
        public event System.Action<IReadOnlyList<SimEvent>> Stepped;

        RenderSnapshot _prev, _curr;

        // ---- referee replay: the last few seconds of render state, replayed from a referee camera ----
        public const int HistorySteps = 240 * 8;
        readonly RenderSnapshot[] _history = new RenderSnapshot[HistorySteps];
        int _historyCount, _historyHead;
        readonly System.Collections.Generic.List<RenderSnapshot> _replay = new System.Collections.Generic.List<RenderSnapshot>();
        double _replayCursor, _replaySpeed;
        public bool ReplayActive { get; private set; }
        public string ReplayCaption { get; private set; }
        /// <summary>World point the replay camera looks at.</summary>
        public Vector3 ReplayFocus { get; private set; }
        public float ReplayProgress { get { return _replay.Count == 0 ? 1f : (float)(_replayCursor / _replay.Count); } }

        /// <summary>Replay the last <paramref name="seconds"/> of play at <paramref name="speed"/> x, then carry on.</summary>
        public void StartReplay(double seconds, double speed, string caption)
        {
            int steps = Mathf.Min(_historyCount, (int)(seconds / PhysicsConfig.Current.Sim.FixedDt));
            if (steps < 2) return;
            _replay.Clear();
            for (int i = steps; i >= 1; i--)
                _replay.Add(_history[((_historyHead - i) % HistorySteps + HistorySteps) % HistorySteps]);
            _replayCursor = 0;
            _replaySpeed = speed;
            ReplayCaption = caption;
            var last = _replay[_replay.Count - 1];
            ReplayFocus = last.Count > 1 ? (last.A0.Pos + last.A1.Pos) * 0.5f : last.A0.Pos;
            ReplayActive = true;
        }

        public void StopReplay() { ReplayActive = false; View = _curr; }

        bool AdvanceReplay(float dt)
        {
            if (!ReplayActive) return false;
            _replayCursor += dt * _replaySpeed / PhysicsConfig.Current.Sim.FixedDt;
            int i = (int)_replayCursor;
            if (i >= _replay.Count - 1) { StopReplay(); return false; }
            float f = (float)(_replayCursor - i);
            View = RenderSnapshot.Lerp(_replay[i], _replay[i + 1], f);
            return true;
        }

        protected abstract void CreateSimulation();
        protected abstract void SimStep(double dt);

        protected virtual void Awake()
        {
            Input = new IntentSource();
            Clock = new SimulationClock(OnFixedStep);
            CreateSimulation();
            SnapViews();
        }

        protected virtual void OnDestroy()
        {
            if (Input != null) Input.Dispose();
        }

        protected virtual void Update()
        {
            float rawDt = Time.unscaledDeltaTime;
            Input.Sample(rawDt);
            if (Input.ConsumePause()) { if (ReplayActive) StopReplay(); else Clock.TogglePause(); }

            // During a referee replay the simulation waits; the views show the recording.
            if (AdvanceReplay(rawDt)) return;

            Clock.Advance(rawDt);

            float alpha = Interpolate ? (float)Clock.Alpha : 1f;
            View = RenderSnapshot.Lerp(_prev, _curr, alpha);
        }

        void OnFixedStep(double dt)
        {
            _prev = _curr;
            SimStep(dt);
            _curr = RenderSnapshot.Capture(Ball, Athletes);
            _history[_historyHead] = _curr;
            _historyHead = (_historyHead + 1) % HistorySteps;
            if (_historyCount < HistorySteps) _historyCount++;

            var athletes = Athletes;
            int h = HumanIndex;
            if (h >= 0 && h < athletes.Length && athletes[h].StrokeFinished) Input.BeginRecenter();

            if ((Clock.TotalSteps & 1) == 0)
            {
                Trail.Enqueue(_curr.BallPos);
                while (Trail.Count > TrailMaxPoints) Trail.Dequeue();
            }

            var events = StepEvents;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent ev = events[i];
                if (ev.Type == SimEventType.PlayerContact) continue;
                RecentEvents.Add(ev);
                if (RecentEvents.Count > RecentEventMax) RecentEvents.RemoveAt(0);

                if (ev.Type == SimEventType.RacketContact)
                {
                    LastRacketContact = ev;
                    _awaitingFirstWall = true;
                    if (FreezeOnCollision && FreezeOnRacket) Clock.Pause();
                }
                else if (ev.Type == SimEventType.Impact)
                {
                    LastImpact = ev;
                    if (_awaitingFirstWall && ev.SurfaceClass != "floor" && ev.SurfaceClass != "ceiling" &&
                        ev.SurfaceClass != "outBackstop")
                    {
                        _awaitingFirstWall = false;
                        LastFirstWall = ev.SurfaceId;
                    }
                    if (FreezeOnCollision && ev.SurfaceClass != "ceiling" && ev.SurfaceClass != "outBackstop") Clock.Pause();
                }
            }

            var handler = Stepped;
            if (handler != null) handler(events);
        }

        /// <summary>Show the current state with no interpolation (after a reset or teleport).</summary>
        protected void SnapViews()
        {
            _curr = RenderSnapshot.Capture(Ball, Athletes);
            _prev = _curr;
            View = _curr;
        }

        protected void ClearDebugHistory()
        {
            Trail.Clear();
            RecentEvents.Clear();
        }

        public void SetTimeScale(double s) { Clock.Resume(); Clock.TimeScale = s; }
        public void TogglePause() { Clock.TogglePause(); }
        public void StepOnce() { Clock.StepOnce(); }
    }
}
