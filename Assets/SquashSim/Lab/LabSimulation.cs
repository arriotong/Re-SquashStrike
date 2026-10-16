// ===========================================================
// Assets/SquashSim/Lab/LabSimulation.cs
// The Warm-Up Lab's fixed step (lab/main.js onFixedStep) as one object:
// one player, one ball, the court and the feed launcher.
//
// This is what the Unity warm-up court (M1) drives and what SimParity
// replays. It is still the LAB: one athlete, no rules. MatchSimulation
// (two players, referee, scoring) arrives in M2.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Lab
{
    public sealed class LabSimulation
    {
        public readonly List<Surface> Surfaces = CourtSurfaces.Create();
        public readonly BallState Ball = new BallState();
        public readonly TestPlayerController Player = new TestPlayerController();
        public readonly BallLauncher Launcher;
        public readonly List<SimEvent> StepEvents = new List<SimEvent>();
        public SimEvent LastRacketContact;
        public long StepCount;

        public LabSimulation()
        {
            Launcher = new BallLauncher(Ball);
        }

        public void SelectPreset(string id)
        {
            TestPresets.Apply(TestPresets.Find(id), Launcher, Player);
        }

        /// <summary>One fixed step. Consumes intent's one-shot edges.</summary>
        public void Step(InputIntent intent, double dt)
        {
            StepEvents.Clear();

            // Feed timer on simulated time, so repeat feeds stay reproducible.
            Launcher.Step(dt);

            // Player before ball: the racket is tested against the ball the player
            // saw, and a struck ball flies on the very step it was hit.
            SimEvent contact = Player.Step(intent, Ball, Surfaces, dt);

            // Edges are produced once per input sample; never let one press
            // become several strokes across steps.
            intent.ClearEdges();

            if (contact != null)
            {
                LastRacketContact = contact;
                StepEvents.Add(contact);
            }

            BallPhysics.Step(Ball, Surfaces, dt, StepEvents);
            StepCount++;
        }
    }
}
