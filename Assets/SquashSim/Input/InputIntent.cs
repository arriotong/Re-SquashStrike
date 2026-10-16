// ===========================================================
// Assets/SquashSim/Input/InputIntent.cs
// The semantic intent gameplay consumes (lab/core/InputIntent.js's
// `intent` object). Devices are NOT handled here: the game assembly
// translates Unity Input System actions into this, and a network peer
// or the AI fills the same structure.
//
// Stroke edges (Pressed / Released) are one-shot: whoever drives the
// fixed step clears them after the step that consumed them.
// ===========================================================

namespace SquashSim
{
    public sealed class StrokeButton
    {
        public bool Pressed;
        public bool Released;
        public bool Held;
        public double HeldSeconds;
    }

    public sealed class InputIntent
    {
        /// <summary>Court-relative move direction (x lateral, z depth; -z = toward the front wall).</summary>
        public readonly V2 Move = new V2();
        public double MoveMagnitude;

        /// <summary>Raw analog aim, -1..1. Shaped by AimController, not here.</summary>
        public double AimRawX, AimRawY;

        public readonly StrokeButton Hit = new StrokeButton();
        public readonly StrokeButton Touch = new StrokeButton();

        public bool MomentumFocus;   // held state of the split-step button
        /// <summary>Split-step press edge (Space / LT). One-shot, cleared with the stroke edges.</summary>
        public bool SplitStepPressed;
        /// <summary>"Let, please" (Q / pad Y). One-shot edge.</summary>
        public bool AppealPressed;
        public bool PausePressed;

        public void ClearEdges()
        {
            Hit.Pressed = false; Hit.Released = false;
            Touch.Pressed = false; Touch.Released = false;
            SplitStepPressed = false;
            AppealPressed = false;
        }
    }
}
