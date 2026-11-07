// ===========================================================
// Assets/SquashGame/Runtime/Athlete/MoveBook.cs
// 2026-11-07 one situation, one move: the way an arcade racket game
// reads, the same situation always plays the same animation. The sim's
// facts (wing, reach, height, touch, serve) pick a row of this table,
// nothing random; AthleteBody locks the row as the stroke commits.
// The moves are cut from our squash motion capture (a model's ghosted
// forehands, an elite junior rally; see Resources/Players/CREDITS.md).
// Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame.Athlete
{
    public static class MoveBook
    {
        /// <summary>A move: its clip and its moments in seconds - where it starts (the racket back / the step in), the ball,
        /// the follow-through's end, and back toward the ready.</summary>
        public readonly struct Move
        {
            public readonly string Name, Clip; public readonly float Start, Contact, End, Recover;
            public Move(string name, string clip, float start, float contact, float end, float recover)
            { Name = name; Clip = clip; Start = start; Contact = contact; End = end; Recover = recover; }
            /// <summary>The clip's time for the sim's swing: the start held while preparing, the ball at SwingT 0.5.</summary>
            public float At(int phase, float swingT, float recoverClock)
            {
                if (phase == SwingPhase.Swinging)
                    return swingT < 0.5f ? Mathf.Lerp(Start, Contact, swingT / 0.5f) : Mathf.Lerp(Contact, End, (swingT - 0.5f) / 0.5f);
                if (phase == SwingPhase.Recovering || phase == SwingPhase.Idle) return Mathf.Min(End + recoverClock, Recover);
                return Start;
            }
        }

        // the rows' places in All
        public const int FhDrive = 0, BhDrive = 1, FhLunge = 2, BhLunge = 3, FhTouch = 4, BhTouch = 5, FhHigh = 6, BhHigh = 7, Serve = 8;

        // the hand's path read in the body's frame (Blender, 2026-11-02): a forehand takes it from the right across to the
        // left shoulder, a backhand from the left shoulder out to the right
        public static readonly Move[] All =
        {
            new Move("fh_drive", "Mocap_Swings", 0.40f, 0.74f, 1.35f, 1.9f),
            new Move("bh_drive", "Mocap_EliteRally", 7.33f, 7.62f, 7.9f, 7.95f),
            new Move("fh_lunge", "Mocap_EliteRally", 0.0f, 0.55f, 0.8f, 1.2f),
            new Move("bh_lunge", "Mocap_EliteRally", 5.9f, 6.33f, 6.75f, 7.1f),
            new Move("fh_touch", "Mocap_Swings", 0.60f, 0.74f, 0.92f, 1.4f),                   // the short swing of a soft shot
            new Move("bh_touch", "Mocap_EliteRally", 7.50f, 7.62f, 7.76f, 7.95f),
            new Move("fh_high", "Mocap_ReadySwings", 0.20f, 0.55f, 1.2f, 1.8f),                // the finish up by the shoulder
            new Move("bh_high", "Mocap_EliteRally", 2.0f, 2.32f, 2.6f, 2.9f),                  // the finish high on the right
            new Move("serve", "Mocap_ReadySwings", 2.4f, 3.0f, 3.6f, 4.2f),                    // a calm standing swing
        };

        /// <summary>The stance's length, front foot to back foot, metres (the user's PSA photos: one foot out, the other leg long).</summary>
        public static float Stride(int move)
        {
            if (move == FhLunge || move == BhLunge) return 1.3f;
            if (move == Serve) return 0.65f;
            return 0.9f;
        }

        /// <summary>The racket-side foot goes in front: on a lunge either wing and on a backhand; else the other foot.</summary>
        public static bool RightFootFront(int move) { return move == FhLunge || move == BhLunge || (move != Serve && move % 2 == 1); }

        /// <summary>Below this the ball is dug out of the floor, above this it's taken high (metres at contact).</summary>
        public const float DigHeight = 0.45f, HighHeight = 1.6f;

        /// <summary>The move for a situation. The serve first; then a reach or a ball off the floor (a lunge); a high ball;
        /// a touch; else the drive.</summary>
        public static int Pick(bool fore, int reach, float height, bool touch, bool serve)
        {
            if (serve) return Serve;
            if (reach >= 1 || height < DigHeight) return fore ? FhLunge : BhLunge;
            if (height > HighHeight) return fore ? FhHigh : BhHigh;
            if (touch) return fore ? FhTouch : BhTouch;
            return fore ? FhDrive : BhDrive;
        }
    }
}
