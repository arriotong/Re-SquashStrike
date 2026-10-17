// ===========================================================
// Assets/SquashGame/Runtime/MatchRunner.cs
// A match on the shared frame loop: SquashSim's MatchSimulation, set up
// by the pre-match panel (MatchSetupPanel), against the AI or a second
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
// ===========================================================

using System.Collections.Generic;
using SquashSim;
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
        public bool WarmUp = true;

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
        /// <summary>A local player's pad was unplugged: play is paused until it is back.</summary>
        public string DeviceLost { get; private set; }

        bool _replayRequested;
        string _replayCaption;

        public MatchSimulation Sim { get; private set; }

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

        void ApplyConfig(MatchSettings s)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();          // the game's double yellow at PSA pace
            var m = PhysicsConfig.Current.Match;
            m.Scoring = s.Scoring;
            m.GamesToWin = Mathf.Max(1, s.GamesToWin);
            m.WarmUpEnabled = s.WarmUp;
            m.AutoNextGame = true;
        }

        void NewMatch()
        {
            if (Sim != null) Sim.RallyEnded -= OnRallyEnded;
            Sim = new MatchSimulation(0);
            Sim.Intents[0] = Input.Intent;
            if (Settings.TwoPlayer && Input2 != null)
            {
                Sim.Intents[1] = Input2.Intent;
                Sim.Bots[1] = null;
                OpponentName = "PLAYER 2";
                Names[0] = "PLAYER 1"; Names[1] = "PLAYER 2";
            }
            else
            {
                Sim.Bots[1] = MakeOpponent();
                Names[0] = "YOU"; Names[1] = OpponentName;
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
            ApplyConfig(Settings);
            BotSeed = BotSeed * 1664525u + 1013904223u;   // a different (still deterministic) bot each match
            NewMatch();
            ClearDebugHistory();
            InSetup = false;
            DeviceLost = null;
            Clock.Resume();
            Clock.Flush();
            SnapViews();
        }

        /// <summary>Back to the pre-match panel.</summary>
        public void ReturnToSetup()
        {
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
            base.OnDestroy();
        }

        protected override void SimStep(double dt) { Sim.Step(dt); }

        IAthleteBrain MakeOpponent()
        {
            if (Settings.Opponent == "wallbot") { OpponentName = "WALL-BOT"; return new WallBot(BotSeed); }
            var profile = AiProfile.ByName(Settings.Opponent).WithDifficulty(Settings.Difficulty);
            OpponentName = profile.Name.ToUpperInvariant();
            return new OpponentBrain(profile, BotSeed);
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
            if (InSetup) return;          // the panel is up: nothing moves (the views keep the last frame)

            WatchDevices();

            // Paused: M / pad View leaves the match for the setup panel.
            if (Clock.Paused && DeviceLost == null)
            {
                bool menu = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
                foreach (var pad in Gamepad.all) menu |= pad.selectButton.wasPressedThisFrame;
                if (menu) { Clock.Resume(); ReturnToSetup(); return; }
            }
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
                if (Sim.Phase == MatchPhase.WarmUp) { Sim.SkipWarmUp(); SnapViews(); }
                else if (Sim.Phase == MatchPhase.GameOver) { Sim.NextGame(); SnapViews(); }
                else if (Sim.Phase == MatchPhase.MatchOver) { ReturnToSetup(); return; }
            }
            base.Update();
        }

        /// <summary>The same settings again (new opponent seed).</summary>
        public void RestartMatch() { BeginMatch(Settings); }
    }
}
