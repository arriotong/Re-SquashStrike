// ===========================================================
// Assets/SquashGame/Runtime/MatchRunner.cs
// A match on the shared frame loop: SquashSim's MatchSimulation, set up
// by the front end (UI/FrontEnd: Quick Match / Local Versus), against the AI or a second
// local player.
//
//   vs AI      athlete 0 = every device; athlete 1 = OpponentBrain (or
//              the wall-bot), playing through the same InputIntent
//   Local 2P   athlete 0 = keyboard + mouse (or pad 1 when two pads are
//              connected), athlete 1 = a pad of their own
//
// Flow: setup -> knock-up (cold ball) -> games, with a break between
// them in which the ball cools -> result -> back to setup. Ready
// (Enter / pad Y) skips the knock-up or the break; no debug keys needed.
// After every let / no let / stroke call the referee's view is replayed
// automatically (V replays the last rally on demand).
//
// P2-M6: every match is built from a ReplaySetup (SquashSim.Replays), and
// a real match (not the menus' demo) is recorded by a MatchRecorder: the
// sim steps on the quantised copy of each person's input. When the
// match ends - or is left - ReplayFinished hands the file on.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Replays;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame
{
    [System.Serializable]
    public sealed class MatchSettings
    {
        public bool TwoPlayer;
        public string Opponent = "retriever";
        [Range(0, 1)] public float Difficulty = 0.5f;
        public int GamesToWin = 3;
        public string Scoring = "par";
        public bool WarmUp = false;              // the knock-up is gone from the game (kept in the sim for tests)
        /// <summary>P2-M4: a Career match (the post-match and pause screens go back to the career).</summary>
        public bool Career;
        /// <summary>Score-bug names instead of "YOU" and the AI's personality (null = the defaults).</summary>
        public string P1Name, P2Name;
        /// <summary>P2-M5: the opponent's kit from this seed (a career pro); 0 = the personality's signature kit.</summary>
        public uint KitSeed;

        public MatchSettings Clone() { return (MatchSettings)MemberwiseClone(); }
        /// <summary>One game, no knock-up: straight to the first serve (tests, quick play).</summary>
        public static MatchSettings QuickGame(string opponent = "retriever")
        {
            return new MatchSettings { Opponent = opponent, GamesToWin = 1, WarmUp = false };
        }
    }

    public sealed class MatchRunner : SimRunnerBase
    {
        [Tooltip("Show the pre-match panel on load. Off = start Settings at once.")]
        public bool ShowSetup = true;
        public MatchSettings Settings = new MatchSettings();
        [Tooltip("Seed for the opponent's choices (deterministic).")]
        public uint BotSeed = 12345;
        [Tooltip("Seconds of play shown in a referee replay, and its speed.")]
        public float ReplaySeconds = 3.5f, ReplaySpeed = 0.5f;

        public static readonly string[] Opponents = { "retriever", "attacker", "shotmaker", "grinder", "wallbot" };
        public string OpponentName { get; private set; }
        /// <summary>HUD names for athlete 0 and 1.</summary>
        public readonly string[] Names = { "YOU", "OPPONENT" };
        /// <summary>The second local player's input (Local 2P), else null.</summary>
        public IntentSource Input2 { get; private set; }
        /// <summary>The pre-match panel is up; the simulation waits.</summary>
        public bool InSetup { get; private set; }
        /// <summary>P2-M1: the menus' background - an AI-vs-AI rally on the live court, no local player, no HUD.</summary>
        public bool Demo { get; private set; }
        protected override bool PauseAllowed { get { return !Demo && !InSetup; } }

        /// <summary>The menus' demo rally runs in slow motion (the old home screen: 0.4x).</summary>
        public const double DemoSpeed = GameTuning.Speed.MenuDemo;

        /// <summary>Play the menus' demo: two AIs, on and on (a new match when one ends).</summary>
        public void BeginDemo()
        {
            EndRecording(true);
            Settings = new MatchSettings { Opponent = "attacker", Difficulty = 0.42f, GamesToWin = 1, Scoring = "par", WarmUp = false };
            Input.UseDevices(null);
            if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            ApplyConfig(Settings);
            BotSeed = BotSeed * 1664525u + 1013904223u;
            NewMatch();
            Sim.Intents[0] = new InputIntent();                          // the player's own input is not used
            Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.42), BotSeed ^ 0x9E3779B9u);
            Sim.Players[0].WallAimEnabled = true;
            Sim.Players[0].ArcadeHitEnabled = true;
            Names[0] = "RETRIEVER"; Names[1] = "ATTACKER";
            ClearDebugHistory();
            Demo = true;
            InSetup = false;
            DeviceLost = null;
            Clock.Resume();
            Clock.TimeScale = DemoSpeed;
            Clock.Flush();
            SnapViews();
        }
        /// <summary>A local player's pad was unplugged: play is paused until it is back.</summary>
        public string DeviceLost { get; private set; }

        bool _replayRequested;
        string _replayCaption;

        public MatchSimulation Sim { get; private set; }

        /// <summary>P2-M6: the match being recorded (null in the demo and the setup panel).</summary>
        public MatchRecorder Recorder { get; private set; }
        /// <summary>A recording ended: the match was played out, or left (file.Retired). Raised once per match.</summary>
        public static event System.Action<ReplayFile> ReplayFinished;
        ReplaySetup _setup;
        IAthleteBrain _autopilot;
        InputIntent _autoIntent;

        /// <summary>Tests: an AI plays for athlete 0 through the same input path a person's input takes (so it is recorded).</summary>
        public void SetAutopilot(IAthleteBrain brain)
        {
            _autopilot = brain != null ? new PersonStandIn(brain) : null;
            if (brain == null || Demo || InSetup) return;
            _autoIntent = new InputIntent();
            Recorder = new MatchRecorder(Sim, _setup, new[] { _autoIntent, Settings.TwoPlayer && Input2 != null ? Input2.Intent : null },
                                         Application.version, System.DateTime.Now.Ticks);
        }

        /// <summary>The recording ends (the match was left, or a new one starts): hand it on if it holds anything.</summary>
        void EndRecording(bool left)
        {
            var rec = Recorder;
            Recorder = null;
            _autopilot = null;
            if (rec == null) return;
            bool was = rec.Finished;
            rec.Finish(left);
            if (was) return;
            var d = DescribeReplay; if (d != null) d(rec.File, this);
            Publish(rec);
        }

        static void Publish(MatchRecorder rec)
        {
            if (rec.Invalid || rec.File.Rallies.Count == 0) return;
            var h = ReplayFinished; if (h != null) h(rec.File);
        }

        /// <summary>Meta for the replay's library card (names, mode, kits): set by the front end when a match starts.</summary>
        public static System.Action<ReplayFile, MatchRunner> DescribeReplay;

        public override BallState Ball { get { return Sim.Ball; } }
        public override TestPlayerController[] Athletes { get { return Sim.Players; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Sim.StepEvents; } }
        public override List<Surface> Surfaces { get { return Sim.Surfaces; } }

        protected override void CreateSimulation()
        {
            ApplyConfig(ShowSetup ? MatchSettings.QuickGame(Settings.Opponent) : Settings);
            NewMatch();
            InSetup = ShowSetup;
        }

        /// <summary>The setup a match is built from (and recorded with).</summary>
        ReplaySetup SetupOf(MatchSettings s)
        {
            return new ReplaySetup
            {
                TwoPlayer = s.TwoPlayer && Input2 != null, Opponent = s.Opponent, Difficulty = s.Difficulty, Scoring = s.Scoring,
                GamesToWin = Mathf.Max(1, s.GamesToWin), WarmUp = s.WarmUp, BotSeed = BotSeed, FirstServer = 0,
            };
        }

        void ApplyConfig(MatchSettings s) { ReplayMatch.Configure(SetupOf(s)); }

        void NewMatch()
        {
            if (Sim != null) Sim.RallyEnded -= OnRallyEnded;
            _setup = SetupOf(Settings);
            Sim = ReplayMatch.Build(_setup);                 // the config, the opponent, who plays the simple system
            Sim.Intents[0] = Input.Intent;
            if (_setup.TwoPlayer)
            {
                Sim.Intents[1] = Input2.Intent;
                OpponentName = "PLAYER 2";
                Names[0] = "PLAYER 1"; Names[1] = "PLAYER 2";
            }
            else
            {
                OpponentName = Settings.Opponent == "wallbot" ? "WALL-BOT" : AiProfile.ByName(Settings.Opponent).WithDifficulty(Settings.Difficulty).Name.ToUpperInvariant();
                if (!string.IsNullOrEmpty(Settings.P2Name)) OpponentName = Settings.P2Name;
                Names[0] = string.IsNullOrEmpty(Settings.P1Name) ? "YOU" : Settings.P1Name; Names[1] = OpponentName;
            }
            Sim.RallyEnded += OnRallyEnded;
        }

        // ---------------- devices ----------------

        /// <summary>Is Local 2P possible right now (at least one pad)?</summary>
        public static bool CanPlayTwoPlayer { get { return Gamepad.all.Count >= 1; } }

        /// <summary>Who plays with what: two pads -> one each (keyboard stays with P1); one pad -> keyboard vs pad.</summary>
        public static void AssignDevices(out InputDevice[] p1, out InputDevice[] p2)
        {
            var pads = Gamepad.all;
            var kbm = new List<InputDevice>();
            if (Keyboard.current != null) kbm.Add(Keyboard.current);
            if (Mouse.current != null) kbm.Add(Mouse.current);
            if (pads.Count >= 2)
            {
                kbm.Add(pads[0]);
                p1 = kbm.ToArray();
                p2 = new InputDevice[] { pads[1] };
            }
            else
            {
                p1 = kbm.ToArray();
                p2 = pads.Count == 1 ? new InputDevice[] { pads[0] } : new InputDevice[0];
            }
        }

        /// <summary>Start a match with these settings (from the setup panel, or a test).</summary>
        public void BeginMatch(MatchSettings s)
        {
            EndRecording(true);
            Settings = s.Clone();
            if (Settings.TwoPlayer && !CanPlayTwoPlayer) Settings.TwoPlayer = false;
            if (Settings.TwoPlayer)
            {
                AssignDevices(out InputDevice[] p1, out InputDevice[] p2);
                Input.UseDevices(p1);
                if (Input2 == null) Input2 = new IntentSource(p2); else Input2.UseDevices(p2);
            }
            else
            {
                Input.UseDevices(null);
                if (Input2 != null) { Input2.Dispose(); Input2 = null; }
            }
            BotSeed = BotSeed * 1664525u + 1013904223u;   // a different (still deterministic) bot each match
            ApplyConfig(Settings);
            NewMatch();
            Recorder = new MatchRecorder(Sim, _setup, new[] { Input.Intent, _setup.TwoPlayer ? Input2.Intent : null },
                                         Application.version, System.DateTime.Now.Ticks);
            ClearDebugHistory();
            Demo = false;
            InSetup = false;
            DeviceLost = null;
            Clock.Resume();
            Clock.TimeScale = GameSpeed;
            Clock.Flush();
            SnapViews();
        }

        /// <summary>Back to the pre-match panel.</summary>
        public void ReturnToSetup()
        {
            EndRecording(true);
            InSetup = true;
            if (ReplayActive) StopReplay();
        }

        protected override bool SampleExtraInput(float dt)
        {
            if (Input2 == null) return false;
            Input2.Sample(dt);
            return Input2.ConsumePause();
        }

        protected override IntentSource SourceFor(int i)
        {
            if (i == 0) return Input;
            return i == 1 && Settings.TwoPlayer ? Input2 : null;
        }

        protected override void OnDestroy()
        {
            if (Input2 != null) Input2.Dispose();
            EndRecording(true);
            base.OnDestroy();
        }

        protected override void SimStep(double dt)
        {
            var rec = Recorder;
            if (rec == null) { Sim.Step(dt); return; }
            if (_autopilot != null && _autoIntent != null) _autopilot.Fill(_autoIntent, Sim, 0, dt);
            rec.BeforeStep();
            Sim.Step(dt);
            bool was = rec.Finished;
            rec.AfterStep();
            if (!was && rec.Finished)
            {
                var d = DescribeReplay; if (d != null) d(rec.File, this);
                Publish(rec);
            }
        }

        void OnRallyEnded(RallyOutcome o)
        {
            // Interference calls are reviewed on the referee camera.
            if (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke) { _replayRequested = true; _replayCaption = o.Reason; }
        }

        /// <summary>A local player's pad went away (unplugged, battery): pause until it is back.</summary>
        void WatchDevices()
        {
            if (!Settings.TwoPlayer || Input2 == null) { DeviceLost = null; return; }
            bool lost = false;
            foreach (var src in new[] { Input, Input2 })
                if (src.Devices != null)
                    foreach (var d in src.Devices) if (d is Gamepad && !d.added) lost = true;
            if (lost && DeviceLost == null)
            {
                DeviceLost = "Controller disconnected - reconnect it to carry on";
                Clock.Pause();
            }
            else if (DeviceLost != null)
            {
                // Whatever pads are there now: hand them out again and play on.
                AssignDevices(out InputDevice[] p1, out InputDevice[] p2);
                if (p2.Length > 0)
                {
                    Input.UseDevices(p1); Input2.UseDevices(p2);
                    DeviceLost = null;
                    Clock.Resume();
                }
            }
        }

        protected override void Update()
        {
            if (FramesActive) { base.Update(); return; }   // P2-M6: the replay theatre plays recorded frames; the demo waits
            if (InSetup) return;          // the panel is up: nothing moves (the views keep the last frame)
            if (Demo)
            {
                // The menus' demo rally: on and on.
                if (Sim.Phase == MatchPhase.MatchOver || Sim.Phase == MatchPhase.GameOver) { BeginDemo(); return; }
                Input.ConsumeReady();
                base.Update();
                return;
            }

            WatchDevices();
            if (_replayRequested && !ReplayActive)
            {
                _replayRequested = false;
                StartReplay(ReplaySeconds, ReplaySpeed, _replayCaption);
            }
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && !ReplayActive && Sim.LastOutcome != null &&
                (Sim.Phase == MatchPhase.RallyOver || Sim.Phase == MatchPhase.AwaitToss))
                StartReplay(ReplaySeconds + 1.5f, ReplaySpeed, Sim.LastOutcome.Reason);

            // Ready (Enter / pad Y) from either player. Consumed every frame so a Y pressed
            // as an appeal in a rally never skips the next break.
            bool ready = Input.ConsumeReady();
            if (Input2 != null) ready |= Input2.ConsumeReady();
            if (ready && !ReplayActive)
            {
                if (Sim.Phase == MatchPhase.WarmUp) { if (Recorder != null) Recorder.Command(ReplayCommand.SkipWarmUp); Sim.SkipWarmUp(); SnapViews(); }
                else if (Sim.Phase == MatchPhase.GameOver) { if (Recorder != null) Recorder.Command(ReplayCommand.NextGame); Sim.NextGame(); SnapViews(); }
                // (match over: the post-match screen takes it from here)
            }
            base.Update();
        }

        /// <summary>The same settings again (new opponent seed).</summary>
        public void RestartMatch() { BeginMatch(Settings); }
    }
}
