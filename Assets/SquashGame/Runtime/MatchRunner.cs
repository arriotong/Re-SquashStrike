// ===========================================================
// Assets/SquashGame/Runtime/MatchRunner.cs
// A game against the AI opponent: SquashSim's MatchSimulation on the
// shared frame loop. The local player is athlete 0 (input from
// IntentSource); athlete 1 is OpponentBrain (or the M2 wall-bot), which
// plays through the same InputIntent a human does.
//
// After every let / no let call the referee's view is replayed
// automatically (V replays the last rally on demand).
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame
{
    public sealed class MatchRunner : SimRunnerBase
    {
        [Tooltip("\"par\" (PAR-11) or \"english\" (to 9, hand-in/hand-out).")]
        public string Scoring = "par";
        [Tooltip("Games needed to win the match. M2 plays one game.")]
        public int GamesToWin = 1;
        [Tooltip("Seed for the opponent's choices (deterministic).")]
        public uint BotSeed = 12345;
        [Tooltip("retriever | attacker | shotmaker | grinder | wallbot")]
        public string Opponent = "retriever";
        [Range(0, 1)] public float Difficulty = 0.5f;
        [Tooltip("Seconds of play shown in a referee replay, and its speed.")]
        public float ReplaySeconds = 3.5f, ReplaySpeed = 0.5f;

        public static readonly string[] Opponents = { "retriever", "attacker", "shotmaker", "grinder", "wallbot" };
        public string OpponentName { get; private set; }
        bool _replayRequested;
        string _replayCaption;

        public MatchSimulation Sim { get; private set; }

        public override BallState Ball { get { return Sim.Ball; } }
        public override TestPlayerController[] Athletes { get { return Sim.Players; } }
        public override IReadOnlyList<SimEvent> StepEvents { get { return Sim.StepEvents; } }

        protected override void CreateSimulation()
        {
            PhysicsConfig.Current.Match.Scoring = Scoring;
            PhysicsConfig.Current.Match.GamesToWin = GamesToWin;
            NewMatch();
        }

        void NewMatch()
        {
            Sim = new MatchSimulation(0);
            Sim.Intents[0] = Input.Intent;            // the local player
            Sim.Bots[1] = MakeOpponent();
            Sim.RallyEnded += OnRallyEnded;
        }

        protected override void SimStep(double dt) { Sim.Step(dt); }

        IAthleteBrain MakeOpponent()
        {
            if (Opponent == "wallbot") { OpponentName = "WALL-BOT"; return new WallBot(BotSeed); }
            var profile = AiProfile.ByName(Opponent).WithDifficulty(Difficulty);
            OpponentName = profile.Name.ToUpperInvariant();
            return new OpponentBrain(profile, BotSeed);
        }

        void OnRallyEnded(RallyOutcome o)
        {
            // Interference calls are reviewed on the referee camera.
            if (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet) { _replayRequested = true; _replayCaption = o.Reason; }
        }

        protected override void Update()
        {
            if (_replayRequested && !ReplayActive)
            {
                _replayRequested = false;
                StartReplay(ReplaySeconds, ReplaySpeed, _replayCaption);
            }
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && !ReplayActive && Sim.LastOutcome != null &&
                (Sim.Phase == MatchPhase.RallyOver || Sim.Phase == MatchPhase.AwaitToss))
                StartReplay(ReplaySeconds + 1.5f, ReplaySpeed, Sim.LastOutcome.Reason);
            if (kb != null && kb.f6Key.wasPressedThisFrame)
            {
                int i = System.Array.IndexOf(Opponents, Opponent);
                Opponent = Opponents[(i + 1) % Opponents.Length];
                RestartMatch();
            }
            if (kb != null && kb.f7Key.wasPressedThisFrame)
            {
                Difficulty = Difficulty < 0.3f ? 0.5f : Difficulty < 0.7f ? 0.9f : 0.1f;
                RestartMatch();
            }

            // Between games: Enter / pad North starts the next game or a new match.
            bool next = (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
            if (next)
            {
                if (Sim.Phase == MatchPhase.GameOver) { Sim.NextGame(); SnapViews(); }
                else if (Sim.Phase == MatchPhase.MatchOver) { RestartMatch(); }
            }
            base.Update();
        }

        public void RestartMatch()
        {
            ClearDebugHistory();
            BotSeed = BotSeed * 1664525u + 1013904223u;   // a different (still deterministic) bot each match
            Sim.Bots[1] = MakeOpponent();
            Sim.Restart(0);
            Clock.Flush();
            SnapViews();
        }
    }
}
