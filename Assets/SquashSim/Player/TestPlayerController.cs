// ===========================================================
// Assets/SquashSim/Player/TestPlayerController.cs
// Port of lab/player/TestPlayerController.js. ORCHESTRATION ONLY.
//
//   INPUT -> MOVEMENT -> CONTACT PREDICTION -> MOVEMENT ASSIST -> FACING
//         -> AIM -> INTERPRETATION (label only) -> SWING
//         -> COMMITMENT SOLVER -> RACKET -> CONTACT -> (ball physics)
//
// Owns no logic; each stage is replaceable in isolation.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class TestPlayerController
    {
        public readonly TestPlayer Player;
        public readonly AimState Aim = new AimState();
        public readonly Prediction Prediction = new Prediction();
        public readonly MovementAssist Assist = new MovementAssist();
        public readonly SwingController Swing = new SwingController();
        public readonly RacketIntent IntentSolver = new RacketIntent();
        public readonly Racket Racket = new Racket();
        public ShotLabel Interpretation = new ShotLabel("STRAIGHT", "drive");
        public SimEvent LastContact;
        public bool StrokeFinished;

        readonly V3 _origin = new V3(0, 1.0, 0);

        public TestPlayerController() : this(null) { }

        public TestPlayerController(V3 spawn)
        {
            Player = spawn != null ? new TestPlayer(spawn) : new TestPlayer();
        }

        /// <summary>One fixed step of the player pipeline. Returns a racket contact event or null.
        /// Runs BEFORE the ball integrates, so a struck ball flies on the step it was hit.</summary>
        public SimEvent Step(InputIntent intent, BallState ball, List<Surface> surfaces, double dt)
        {
            StrokeFinished = false;

            PlayerMovement.Step(Player, intent, Assist, dt);
            ContactPrediction.Step(Prediction, ball, Player, surfaces);
            MovementAssistance.Step(Assist, Player, Prediction, intent, dt);
            PlayerFacing.Step(Player, ball, dt);

            if (Prediction.Valid)
            {
                _origin.X = Prediction.Point.X; _origin.Y = Prediction.Point.Y; _origin.Z = Prediction.Point.Z;
            }
            else
            {
                _origin.X = Player.Pos.X; _origin.Y = PhysicsConfig.Current.Racket.RestHeight; _origin.Z = Player.Pos.Z;
            }
            AimController.Step(Aim, intent, _origin);

            string strokeForLabel = Swing.State == SwingStates.Idle ? "hit" : Swing.Stroke;
            Interpretation = ShotInterpreter.Interpret(Aim, strokeForLabel, Prediction.IsVolley);

            bool wasSwinging = Swing.State == SwingStates.Swinging;
            SquashSim.Swing.Step(Swing, intent, Aim, Player, Prediction,
                new SwingCtx { Quality = Assist.Quality, ReachState = Assist.ReachState }, dt, IntentSolver);

            // Preparation improves control up to a cap; balance and reach set the ceiling.
            bool strokeLive = Swing.State != SwingStates.Idle && Swing.State != SwingStates.Recovering;
            double prep = strokeLive ? Swing.PrepQuality : 1;
            double controlQuality = Assist.Quality * (0.8 + 0.2 * prep);
            RacketIntentSolver.Step(IntentSolver, Aim, Swing, controlQuality, dt);

            RacketPhysics.Step(Racket, Swing, IntentSolver, Aim, Player, ball, Prediction, dt);
            SimEvent contact = RacketPhysics.TryContact(Racket, ball, Swing, dt);
            if (contact != null)
            {
                contact.Label = Interpretation.Label;
                contact.ReachState = Assist.ReachState;
                contact.Balance = Assist.Balance;
                contact.Deception = RacketIntentSolver.RecordDeception(IntentSolver, Aim);
                contact.TimingCorrection = Swing.TimingCorrection;
                LastContact = contact;
            }

            if (wasSwinging && Swing.State != SwingStates.Swinging)
            {
                Assist.ReleaseBudget();
                StrokeFinished = true;            // the input layer recentres the aim
            }
            return contact;
        }

        public void Reset()
        {
            Player.Reset();
            Swing.Reset();
            Assist.Reset();
            IntentSolver.Reset();
            Racket.MarkTeleported();
            Prediction.Valid = false;
            LastContact = null;
            Racket.LastContact = null;
        }
    }
}
