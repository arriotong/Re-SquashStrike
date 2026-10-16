// ===========================================================
// Assets/SquashGame/Runtime/LabSimRunner.cs
// Drives SquashSim's LabSimulation from Unity's frame loop.
// Port of the lab/main.js render loop:
//
//   1. sample input into intent (IntentSource)
//   2. hand REAL elapsed time to the fixed-step clock (SimulationClock)
//   3. after each fixed step, keep the previous and current render
//      snapshots so views can interpolate between them
//
// Runs in Update, NOT FixedUpdate: Unity's own physics timestep has no
// say over the simulation. Nothing here writes simulation state except
// through LabSimulation's API.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Lab;
using UnityEngine;

namespace SquashGame
{
    /// <summary>Everything the views draw, captured once per fixed step (world space).</summary>
    public struct RenderSnapshot
    {
        public Vector3 BallPos;
        public Vector3 PlayerPos;
        public Vector3 PlayerForward;
        public Vector3 ShoulderForward;     // commitment solver BODY yaw: what an opponent reads
        public Vector3 RacketAnchor;
        public Vector3 RacketHead;
        public Vector3 RacketFaceNormal;

        public static RenderSnapshot Capture(LabSimulation sim)
        {
            var p = sim.Player;
            return new RenderSnapshot
            {
                BallPos = SimToWorld.Pos(sim.Ball.Pos),
                PlayerPos = SimToWorld.Pos(p.Player.Pos),
                PlayerForward = SimToWorld.YawForward(p.Player.Facing),
                ShoulderForward = SimToWorld.YawForward(p.IntentSolver.BodyYaw),
                RacketAnchor = SimToWorld.Pos(p.Racket.Anchor),
                RacketHead = SimToWorld.Pos(p.Racket.HeadPos),
                RacketFaceNormal = SimToWorld.Dir(p.Racket.FaceNormal)
            };
        }

        public static RenderSnapshot Lerp(RenderSnapshot a, RenderSnapshot b, float t)
        {
            return new RenderSnapshot
            {
                BallPos = Vector3.LerpUnclamped(a.BallPos, b.BallPos, t),
                PlayerPos = Vector3.LerpUnclamped(a.PlayerPos, b.PlayerPos, t),
                PlayerForward = Vector3.Slerp(a.PlayerForward, b.PlayerForward, t),
                ShoulderForward = Vector3.Slerp(a.ShoulderForward, b.ShoulderForward, t),
                RacketAnchor = Vector3.LerpUnclamped(a.RacketAnchor, b.RacketAnchor, t),
                RacketHead = Vector3.LerpUnclamped(a.RacketHead, b.RacketHead, t),
                RacketFaceNormal = Vector3.Slerp(a.RacketFaceNormal, b.RacketFaceNormal, t)
            };
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class LabSimRunner : MonoBehaviour
    {
        [Tooltip("Preset selected on start (lab TestPresets id).")]
        public string StartPreset = "free";
        [Tooltip("Start with the repeat feed running.")]
        public bool StartWithRepeatFeed = true;
        [Tooltip("Interpolate views between fixed steps. Off = show the last step exactly.")]
        public bool Interpolate = true;

        public LabSimulation Sim { get; private set; }
        public SimulationClock Clock { get; private set; }
        public IntentSource Input { get; private set; }
        public string CurrentPreset { get; private set; }

        /// <summary>The state views should draw this frame.</summary>
        public RenderSnapshot View { get; private set; }

        // ---- lab-only debug state (not simulation config) ----
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

        /// <summary>Raised after every fixed step with that step's events (presentation: audio, VFX, debug).</summary>
        public event System.Action<IReadOnlyList<SimEvent>> Stepped;

        RenderSnapshot _prev, _curr;

        void Awake()
        {
            Sim = new LabSimulation();
            Input = new IntentSource();
            Clock = new SimulationClock(OnFixedStep);
            SelectPreset(StartPreset);
            Sim.Launcher.SetRepeat(StartWithRepeatFeed);
        }

        void OnDestroy()
        {
            if (Input != null) Input.Dispose();
        }

        void Update()
        {
            float rawDt = Time.unscaledDeltaTime;
            Input.Sample(rawDt);
            if (Input.ConsumePause()) TogglePause();

            Clock.Advance(rawDt);

            float alpha = Interpolate ? (float)Clock.Alpha : 1f;
            View = RenderSnapshot.Lerp(_prev, _curr, alpha);
        }

        void OnFixedStep(double dt)
        {
            _prev = _curr;
            Sim.Step(Input.Intent, dt);
            _curr = RenderSnapshot.Capture(Sim);

            if (Sim.Player.StrokeFinished) Input.BeginRecenter();

            // Trail is sampled every 2nd step, like the lab's debug view.
            if ((Sim.StepCount & 1) == 0)
            {
                Trail.Enqueue(_curr.BallPos);
                while (Trail.Count > TrailMaxPoints) Trail.Dequeue();
            }

            foreach (var ev in Sim.StepEvents)
            {
                RecentEvents.Add(ev);
                if (RecentEvents.Count > RecentEventMax) RecentEvents.RemoveAt(0);

                if (ev.Type == SimEventType.RacketContact)
                {
                    LastRacketContact = ev;
                    _awaitingFirstWall = true;
                    if (FreezeOnCollision && FreezeOnRacket) Clock.Pause();
                }
                else
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
            if (handler != null) handler(Sim.StepEvents);
        }

        // ---------------- lab actions ----------------

        void SnapViews()
        {
            _curr = RenderSnapshot.Capture(Sim);
            _prev = _curr;
            View = _curr;
        }

        public void SelectPreset(string id)
        {
            CurrentPreset = TestPresets.Find(id).Id;
            Trail.Clear();
            RecentEvents.Clear();
            Sim.SelectPreset(CurrentPreset);
            Clock.Flush();
            SnapViews();
        }

        public void CyclePreset(int dir)
        {
            var all = TestPresets.All();
            int i = all.FindIndex(p => p.Id == CurrentPreset);
            int next = ((i + dir) % all.Count + all.Count) % all.Count;
            SelectPreset(all[next].Id);
        }

        /// <summary>RESET BALL: only the ball, back to the preset's start state.</summary>
        public void ResetBall()
        {
            Trail.Clear();
            Sim.Launcher.Fire(fresh: true);
            Clock.Flush();
            SnapViews();
        }

        /// <summary>RESET FULL: player, racket, ball, preset; resumes at 1x.</summary>
        public void ResetFull()
        {
            Trail.Clear();
            Sim.Launcher.SetRepeat(false);
            Sim.SelectPreset(CurrentPreset);
            Clock.Resume();
            Clock.TimeScale = 1.0;
            Clock.Flush();
            SnapViews();
        }

        public void FeedSingle() { Sim.Launcher.Fire(); }
        public bool ToggleRepeat() { return Sim.Launcher.ToggleRepeat(); }
        public void SetTimeScale(double s) { Clock.Resume(); Clock.TimeScale = s; }
        public void TogglePause() { Clock.TogglePause(); }
        public void StepOnce() { Clock.StepOnce(); }
    }
}
