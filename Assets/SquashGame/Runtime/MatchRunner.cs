// ===========================================================
// Assets/SquashGame/Runtime/MatchRunner.cs
// One game against the wall-bot: SquashSim's MatchSimulation on the
// shared frame loop. The local player is athlete 0 (input from
// IntentSource); athlete 1 is the WallBot, which plays through the same
// InputIntent a human does.
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
        [Tooltip("Seed for the wall-bot's choices (deterministic).")]
        public uint BotSeed = 12345;

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
            Sim.Bots[1] = new WallBot(BotSeed);
        }

        protected override void SimStep(double dt) { Sim.Step(dt); }

        protected override void Update()
        {
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
            Sim.Bots[1] = new WallBot(BotSeed);
            Sim.Restart(0);
            Clock.Flush();
            SnapViews();
        }
    }
}
