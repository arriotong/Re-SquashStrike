// ===========================================================
// Assets/SquashSim/Arcade/InputLog.cs
// P2-M2: a player's inputs, one frame per fixed step. Replaying the
// log into the same challenge (same seed) gives the same score - the
// basis of replays (M6) and checkable leaderboard scores (online).
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Arcade
{
    public struct IntentFrame
    {
        public double MoveX, MoveZ, MoveMagnitude, AimX, AimY, AimRawX, AimRawY, HitSeconds;
        public bool HasAim, HitPressed, HitReleased, HitHeld, TouchPressed, TouchReleased, TouchHeld;
        public bool Boast, Dive, Sprint, SplitStep, Focus;

        public static IntentFrame Of(InputIntent i)
        {
            return new IntentFrame
            {
                MoveX = i.Move.X, MoveZ = i.Move.Z, MoveMagnitude = i.MoveMagnitude,
                AimX = i.AimPointX, AimY = i.AimPointY, AimRawX = i.AimRawX, AimRawY = i.AimRawY, HasAim = i.HasAimPoint,
                HitPressed = i.Hit.Pressed, HitReleased = i.Hit.Released, HitHeld = i.Hit.Held, HitSeconds = i.Hit.HeldSeconds,
                TouchPressed = i.Touch.Pressed, TouchReleased = i.Touch.Released, TouchHeld = i.Touch.Held,
                Boast = i.BoastPressed, Dive = i.DivePressed, Sprint = i.Sprint, SplitStep = i.SplitStepPressed, Focus = i.MomentumFocus
            };
        }

        public void Into(InputIntent i)
        {
            i.Move.X = MoveX; i.Move.Z = MoveZ; i.MoveMagnitude = MoveMagnitude;
            i.AimPointX = AimX; i.AimPointY = AimY; i.AimRawX = AimRawX; i.AimRawY = AimRawY; i.HasAimPoint = HasAim;
            i.Hit.Pressed = HitPressed; i.Hit.Released = HitReleased; i.Hit.Held = HitHeld; i.Hit.HeldSeconds = HitSeconds;
            i.Touch.Pressed = TouchPressed; i.Touch.Released = TouchReleased; i.Touch.Held = TouchHeld;
            i.BoastPressed = Boast; i.DivePressed = Dive; i.Sprint = Sprint; i.SplitStepPressed = SplitStep; i.MomentumFocus = Focus;
        }
    }

    public sealed class InputLog
    {
        public string ChallengeId;
        public uint Seed;
        public readonly List<IntentFrame> Frames = new List<IntentFrame>();

        /// <summary>Call with the intent just before the session steps on it.</summary>
        public void Record(InputIntent i) { Frames.Add(IntentFrame.Of(i)); }

        /// <summary>Play the whole log into a fresh session of the same challenge; returns that session.</summary>
        public static ArcadeSession Replay(InputLog log, Challenge challenge, double dt)
        {
            var s = new ArcadeSession(challenge, log.Seed);
            var intent = new InputIntent();
            foreach (var f in log.Frames)
            {
                f.Into(intent);
                s.Step(intent, dt);
            }
            return s;
        }
    }
}
