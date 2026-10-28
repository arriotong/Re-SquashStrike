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
    /// <summary>The swing controller's state as a small number the views can key poses on.</summary>
    public static class SwingPhase
    {
        public const int Idle = 0, Armed = 1, Preparing = 2, Scheduled = 3, Swinging = 4, Recovering = 5;

        public static int Of(string state)
        {
            switch (state)
            {
                case SwingStates.Armed: return Armed;
                case SwingStates.Preparing: return Preparing;
                case SwingStates.Scheduled: return Scheduled;
                case SwingStates.Swinging: return Swinging;
                case SwingStates.Recovering: return Recovering;
                default: return Idle;
            }
        }
    }

    /// <summary>One athlete as drawn (world space).</summary>
    public struct AthleteSnapshot
    {
        public Vector3 Pos;
        public Vector3 Forward;
        public Vector3 ShoulderForward;     // commitment solver BODY yaw: what an opponent reads
        public Vector3 RacketAnchor;
        public Vector3 RacketHead;
        public Vector3 RacketFaceNormal;
        public float Stamina;               // 0..1 (match fatigue); 1 in the lab
        public Vector3 Velocity;            // world, m/s
        public int Reach;                   // 0 normal, 1 lunge, 2 deep lunge, 3 stretch
        public int SwingSide;               // +1 forehand, -1 backhand
        public bool Swinging;
        public int Swing;                   // SwingPhase
        public float SwingT;                // 0..1 through the swing (contact at 0.5) while Swinging
        public bool Touch;                  // the stroke is a touch (soft) stroke
        public bool HasPred;                // a contact is predicted
        public Vector3 PredContact;         // where (world)
        public bool Diving;                 // mid-dive (the original SquashStrike dive)
        public float GetUp;                 // 0..1 of the post-dive recovery left

        public static AthleteSnapshot Capture(TestPlayerController c)
        {
            return new AthleteSnapshot
            {
                Pos = SimToWorld.Pos(c.Player.Pos),
                Forward = SimToWorld.YawForward(c.Player.Facing),
                ShoulderForward = SimToWorld.YawForward(c.IntentSolver.BodyYaw),
                RacketAnchor = SimToWorld.Pos(c.Racket.Anchor),
                RacketHead = SimToWorld.Pos(c.Racket.HeadPos),
                RacketFaceNormal = SimToWorld.Dir(c.Racket.FaceNormal),
                Stamina = (float)c.Player.Stamina,
                Velocity = SimToWorld.Dir(c.Player.Vel),
                Reach = ReachIndex(c.Player.ReachState),
                SwingSide = c.Player.SwingSide >= 0 ? 1 : -1,
                Swinging = c.Swing.State == SwingStates.Swinging,
                Swing = SwingPhase.Of(c.Swing.State),
                SwingT = (float)c.Swing.Phase,
                Touch = c.Swing.Stroke == "touch",
                HasPred = c.Prediction.Valid,
                PredContact = c.Prediction.Valid ? SimToWorld.Pos(c.Prediction.Point) : Vector3.zero,
                Diving = c.ArcadeHitEnabled && c.Arcade.Diving,
                GetUp = c.ArcadeHitEnabled && PhysicsConfig.Current.ArcadeHit != null && PhysicsConfig.Current.ArcadeHit.DiveRecovery > 0
                    ? (float)(c.Arcade.DiveRecovery / PhysicsConfig.Current.ArcadeHit.DiveRecovery) : 0f
            };
        }

        static int ReachIndex(string r)
        {
            switch (r)
            {
                case ReachStates.Lunge: return 1;
                case ReachStates.DeepLunge: return 2;
                case ReachStates.Stretch: case ReachStates.Unreachable: return 3;
                default: return 0;
            }
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
                RacketFaceNormal = Vector3.Slerp(a.RacketFaceNormal, b.RacketFaceNormal, t),
                Stamina = Mathf.LerpUnclamped(a.Stamina, b.Stamina, t),
                Velocity = Vector3.LerpUnclamped(a.Velocity, b.Velocity, t),
                Reach = b.Reach, SwingSide = b.SwingSide, Swinging = b.Swinging, Swing = b.Swing,
                SwingT = a.Swing == b.Swing ? Mathf.LerpUnclamped(a.SwingT, b.SwingT, t) : b.SwingT,
                Touch = b.Touch, HasPred = b.HasPred,
                PredContact = a.HasPred && b.HasPred ? Vector3.LerpUnclamped(a.PredContact, b.PredContact, t) : b.PredContact,
                Diving = b.Diving, GetUp = b.GetUp
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
        public abstract List<Surface> Surfaces { get; }
        /// <summary>Index of the athlete the local player controls (aim recentring, HUD).</summary>
        public virtual int HumanIndex { get { return 0; } }
        /// <summary>Shot lines for the people here (off where the exact spot is the game: the Target Arcade).</summary>
        protected virtual bool ShotLines { get { return ShotLinesEnabled; } }
        /// <summary>Tests that check the exact aim switch shot lines off here.</summary>
        public bool ShotLinesEnabled = true;

        // ---- debug state (presentation only) ----
        public bool FreezeOnCollision;
        public bool FreezeOnRacket = true;
        public readonly Queue<Vector3> Trail = new Queue<Vector3>();
        public const int TrailMaxPoints = 400;
        public readonly List<SimEvent> RecentEvents = new List<SimEvent>();
        public const int RecentEventMax = 24;
        public SimEvent LastImpact { get; private set; }
        /// <summary>P2-M8: stepping through a lot of play at once (back after a dropped connection): no sounds for it.</summary>
        public bool CatchingUp { get; protected set; }
        public SimEvent LastRacketContact { get; private set; }
        /// <summary>Camera work: the rally had a nick (reset as a rally starts); the last stroke was struck in a dive.</summary>
        public bool RallyNick, LastShotDived;
        public string LastFirstWall { get; private set; }
        bool _awaitingFirstWall;

        /// <summary>Raised after every fixed step with that step's events (audio, VFX, debug).</summary>
        public event System.Action<IReadOnlyList<SimEvent>> Stepped;
        /// <summary>Raised when an athlete's swing ends: (athlete, did it touch the ball).</summary>
        public event System.Action<int, bool> StrokeEnded;

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
            ReplayNumber++; ReplayBig = false;
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

        public void StopReplay() { ReplayActive = false; CinematicKind = null; View = _curr; }

        /// <summary>Camera work: a cutscene (Replays.Cutscenes) plays its frames through the replay path - the sim waits.</summary>
        public void PlayCinematic(List<RenderSnapshot> frames, string kind, float orbitShare, string title, string sub)
        {
            if (frames == null || frames.Count < 2) return;
            _replay.Clear(); _replay.AddRange(frames);
            _replayCursor = 0;
            _replaySpeed = PhysicsConfig.Current.Sim.FixedDt * Replays.Cutscenes.Rate;
            ReplayCaption = "";
            ReplayNumber++; ReplayBig = false;
            CinematicKind = kind; CinematicOrbit = orbitShare; CinematicTitle = title; CinematicSub = sub;
            ReplayActive = true;
        }
        /// <summary>The cutscene showing (null: none), the orbit's share of it, its title card.</summary>
        public string CinematicKind { get; private set; }
        public float CinematicOrbit { get; private set; }
        public string CinematicTitle { get; private set; }
        public string CinematicSub { get; private set; }
        /// <summary>Camera work: replays so far (the director varies its cuts), and whether this one ends on a big moment.</summary>
        public int ReplayNumber { get; private set; }
        public bool ReplayBig;

        // ---- P2-M6: the replay theatre - recorded frames (60 a second) shown instead of the sim ----
        public const float FrameRate = 60f;
        IList<RenderSnapshot> _frames;
        IList<List<SimEvent>> _frameEvents;
        double _frameCursor;
        int _lastEmitted = -1;
        /// <summary>Frames are being shown (the sim waits).</summary>
        public bool FramesActive { get { return _frames != null; } }
        public int FrameCount { get { return _frames != null ? _frames.Count : 0; } }
        /// <summary>Playback speed (1, 0.5, 0.25) and pause.</summary>
        public double FrameSpeed = 1;
        public bool FramesPaused;
        /// <summary>Each frame's sim events as playback passes them (not when scrubbing): for the sound. (events, speed)</summary>
        public event System.Action<IReadOnlyList<SimEvent>, float> FrameSounds;

        /// <summary>Where playback is, in frames. Setting it scrubs (no sounds for what is skipped).</summary>
        public double FrameCursor
        {
            get { return _frameCursor; }
            set { if (_frames == null) return; _frameCursor = Mathd(value, 0, _frames.Count - 1); _lastEmitted = (int)_frameCursor; ShowFrame(); }
        }

        static double Mathd(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>Show these frames from the start (events: per frame, for sound; may be null).</summary>
        public void PlayFrames(IList<RenderSnapshot> frames, IList<List<SimEvent>> events)
        {
            if (frames == null || frames.Count == 0) { StopFrames(); return; }
            if (ReplayActive) StopReplay();
            _frames = frames; _frameEvents = events;
            _frameCursor = 0; _lastEmitted = -1; FramesPaused = false;
            ShowFrame();
        }

        /// <summary>Back to the sim's own view.</summary>
        public void StopFrames()
        {
            _frames = null; _frameEvents = null;
            SnapViews();
        }

        void ShowFrame()
        {
            int i = (int)_frameCursor;
            int j = System.Math.Min(i + 1, _frames.Count - 1);
            View = RenderSnapshot.Lerp(_frames[i], _frames[j], (float)(_frameCursor - i));
        }

        bool AdvanceFrames(float dt)
        {
            if (_frames == null) return false;
            if (!FramesPaused) _frameCursor = Mathd(_frameCursor + dt * FrameRate * FrameSpeed, 0, _frames.Count - 1);
            int i = (int)_frameCursor;
            var h = FrameSounds;
            if (_frameEvents != null && h != null && !FramesPaused)
                for (int k = _lastEmitted + 1; k <= i && k < _frameEvents.Count; k++)
                    if (_frameEvents[k] != null && _frameEvents[k].Count > 0) h(_frameEvents[k], (float)FrameSpeed);
            _lastEmitted = System.Math.Max(_lastEmitted, i);
            ShowFrame();
            return true;
        }

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

        /// <summary>
        /// The whole simulation runs at this fraction of real time: every flight, bounce and distance is
        /// exactly the same (the fixed step is unchanged), there is just more time to get there.
        /// </summary>
        public const double GameSpeed = GameTuning.Speed.Game;
        /// <summary>A local player's dive: a moment of slow motion (real seconds, and the speed during it).</summary>
        public const float DiveSlowMoSeconds = (float)GameTuning.Speed.DiveSlowMoSeconds;
        public const double DiveSlowMoScale = GameTuning.Speed.DiveSlowMo;
        float _slowMo;
        readonly int[] _dives = new int[4];
        /// <summary>P5 Focus: slow the game to <paramref name="scale"/> of its speed for <paramref name="seconds"/> real seconds.</summary>
        public void SlowMo(float seconds, double scale)
        {
            _slowMo = seconds;
            Clock.TimeScale = GameSpeed * scale;
        }
        public bool SlowMoActive { get { return _slowMo > 0; } }

        /// <summary>A local player started a dive (athlete index).</summary>
        public event System.Action<int> Dived;

        void WatchDives(float rawDt)
        {
            var athletes = Athletes;
            for (int i = 0; athletes != null && i < athletes.Length && i < _dives.Length; i++)
            {
                if (SourceFor(i) == null || !athletes[i].ArcadeHitEnabled) continue;
                int n = athletes[i].Arcade.DiveCount;
                if (n != _dives[i])
                {
                    bool fresh = n > _dives[i];
                    _dives[i] = n;
                    if (!fresh) continue;
                    if (_slowMo <= 0 && System.Math.Abs(Clock.TimeScale - GameSpeed) < 1e-9) _slowMo = DiveSlowMoSeconds;
                    if (_slowMo > 0) Clock.TimeScale = GameSpeed * DiveSlowMoScale;
                    var d = Dived; if (d != null) d(i);
                }
            }
            if (_slowMo > 0)
            {
                _slowMo -= rawDt;
                if (_slowMo <= 0) { _slowMo = 0; Clock.TimeScale = GameSpeed; }
            }
        }

        protected abstract void CreateSimulation();

        /// <summary>Sample any further local players before the clock runs. Returns true if one pressed pause.</summary>
        protected virtual bool SampleExtraInput(float dt) { return false; }

        /// <summary>The input source steering athlete i, or null when it is not a local player.</summary>
        protected virtual IntentSource SourceFor(int i) { return i == HumanIndex ? Input : null; }

        /// <summary>The local input steering athlete i (null for the AI).</summary>
        public IntentSource LocalSource(int i) { return SourceFor(i); }
        protected abstract void SimStep(double dt);

        protected virtual void Awake()
        {
            Profile.ProfileStore.Apply(Profile.ProfileStore.Current.Settings);   // P2-M1: settings live in the profile
            Input = new IntentSource();
            Clock = new SimulationClock(OnFixedStep);
            CreateSimulation();
            Clock.TimeScale = GameSpeed;
            SnapViews();
        }

        protected virtual void OnDestroy()
        {
            if (Input != null) Input.Dispose();
        }

        protected virtual void Update()
        {
            float rawDt = Time.unscaledDeltaTime;
            GraphicsTier.Governor.Tick(rawDt);                               // batch 1: Auto holds the frame rate
            Input.Sample(rawDt);
            bool pause = Input.ConsumePause();
            pause |= SampleExtraInput(rawDt);
            if (pause && PauseAllowed && !UI.FrontEnd.BlocksGamePause) { if (ReplayActive) StopReplay(); else Clock.TogglePause(); }

            // The theatre's frames, or a referee replay: the simulation waits; the views show the recording.
            if (AdvanceFrames(rawDt)) return;
            if (AdvanceReplay(rawDt)) return;

            WatchDives(rawDt);
            var ath = Athletes;
            for (int i = 0; ath != null && i < ath.Length; i++)
                if (SourceFor(i) != null && ath[i].ArcadeHitEnabled)
                {
                    ath[i].Arcade.Assisted = IntentSource.Assisted;   // the control mode
                    ath[i].Arcade.ShotLines = ShotLines;               // a person: the shot plays its line
                }
            Clock.Advance(rawDt);

            float alpha = Interpolate ? (float)Clock.Alpha : 1f;
            View = RenderSnapshot.Lerp(_prev, _curr, alpha);
        }

        void OnFixedStep(double dt)
        {
            SimStep(dt);
            AfterSimStep(true);
        }

        /// <summary>What follows a sim step: the snapshots, the review history, strokes, the trail, the events.
        /// <paramref name="movePrev"/>: this step starts a new interpolation span (online: only a tick's first step).</summary>
        protected void AfterSimStep(bool movePrev)
        {
            if (movePrev) _prev = _curr;
            _curr = RenderSnapshot.Capture(Ball, Athletes);
            _history[_historyHead] = _curr;
            _historyHead = (_historyHead + 1) % HistorySteps;
            if (_historyCount < HistorySteps) _historyCount++;

            var athletes = Athletes;
            for (int a = 0; a < athletes.Length; a++)
            {
                if (!athletes[a].StrokeFinished) continue;
                IntentSource src = SourceFor(a);
                if (src != null) src.BeginRecenter();
                var ended = StrokeEnded;
                if (ended != null) ended(a, athletes[a].Swing.HasContacted);
            }

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
                    LastShotDived = ev.PlayerIndex >= 0 && ev.PlayerIndex < _curr.Count && _curr.Athlete(ev.PlayerIndex).Diving;   // camera work
                    _awaitingFirstWall = true;
                    if (FreezeOnCollision && FreezeOnRacket) Clock.Pause();
                }
                else if (ev.Type == SimEventType.Impact)
                {
                    LastImpact = ev;
                    if (ev.Nick) RallyNick = true;                                 // camera work: a big moment
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

        /// <summary>P2-M8 online: draw between the last two ticks (the sim steps a tick at a time there).</summary>
        protected void ShowBetween(float alpha) { View = RenderSnapshot.Lerp(_prev, _curr, Interpolate ? Mathf.Clamp01(alpha) : 1f); }

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

        /// <summary>Can the game's pause key pause it now? (Not in the menus' demo rally.)</summary>
        protected virtual bool PauseAllowed { get { return true; } }
        public void StepOnce() { Clock.StepOnce(); }
    }
}
