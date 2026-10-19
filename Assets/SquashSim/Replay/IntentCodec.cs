// ===========================================================
// Assets/SquashSim/Replay/IntentCodec.cs
// P2-M6 Replay Theatre: one step's input, quantised to whole numbers
// (move and raw aim to 1/10000, the aim point to the millimetre, hold
// times to the millisecond, the buttons as bits).
//
// The live sim is fed the *decoded* frame, not the raw intent, so what
// was played and what a replay plays are the same numbers by
// construction - that is what makes a replay bit-identical.
// ===========================================================

using System;

namespace SquashSim.Replays
{
    /// <summary>One step's input for one athlete, as stored in a replay.</summary>
    public struct QFrame : IEquatable<QFrame>
    {
        public short MoveX, MoveZ, MoveMag, RawX, RawY, AimX, AimY;
        public int HitMs, TouchMs;
        public ushort Flags;

        public bool Assisted { get { return (Flags & IntentCodec.FAssisted) != 0; } }
        /// <summary>Shot lines were on (a person in a match).</summary>
        public bool Lines { get { return (Flags & IntentCodec.FLines) != 0; } }

        public bool Equals(QFrame o)
        {
            return MoveX == o.MoveX && MoveZ == o.MoveZ && MoveMag == o.MoveMag && RawX == o.RawX && RawY == o.RawY &&
                   AimX == o.AimX && AimY == o.AimY && HitMs == o.HitMs && TouchMs == o.TouchMs && Flags == o.Flags;
        }
        public override bool Equals(object o) { return o is QFrame f && Equals(f); }
        public override int GetHashCode() { return MoveX ^ (MoveZ << 4) ^ (AimX << 8) ^ (AimY << 12) ^ HitMs ^ Flags; }
    }

    public static class IntentCodec
    {
        public const double UnitScale = 10000;      // move direction, magnitude, raw aim: -3.2767 .. 3.2767
        public const double MetreScale = 1000;      // the aim point: millimetres, -32.767 .. 32.767 m
        public const double SecondScale = 1000;     // hold times: milliseconds

        public const ushort FHasAim = 1 << 0, FHitP = 1 << 1, FHitR = 1 << 2, FHitH = 1 << 3, FTouchP = 1 << 4, FTouchR = 1 << 5, FTouchH = 1 << 6,
                            FFocus = 1 << 7, FSplit = 1 << 8, FAppeal = 1 << 9, FDive = 1 << 10, FSprint = 1 << 11, FBoast = 1 << 12, FAssisted = 1 << 13, FLines = 1 << 14;

        static short S(double v, double scale)
        {
            double q = Math.Round(v * scale);
            if (q > short.MaxValue) q = short.MaxValue;
            if (q < short.MinValue) q = short.MinValue;
            return (short)q;
        }

        static int I(double v, double scale)
        {
            double q = Math.Round(v * scale);
            if (q > int.MaxValue) q = int.MaxValue;
            if (q < int.MinValue) q = int.MinValue;
            return (int)q;
        }

        /// <summary>Quantise an intent (and the athlete's control mode) into a frame.</summary>
        public static QFrame Encode(InputIntent i, bool assisted, bool lines = false)
        {
            ushort f = 0;
            if (i.HasAimPoint) f |= FHasAim;
            if (i.Hit.Pressed) f |= FHitP; if (i.Hit.Released) f |= FHitR; if (i.Hit.Held) f |= FHitH;
            if (i.Touch.Pressed) f |= FTouchP; if (i.Touch.Released) f |= FTouchR; if (i.Touch.Held) f |= FTouchH;
            if (i.MomentumFocus) f |= FFocus; if (i.SplitStepPressed) f |= FSplit; if (i.AppealPressed) f |= FAppeal;
            if (i.DivePressed) f |= FDive; if (i.Sprint) f |= FSprint; if (i.BoastPressed) f |= FBoast;
            if (assisted) f |= FAssisted;
            if (lines) f |= FLines;
            return new QFrame
            {
                MoveX = S(i.Move.X, UnitScale), MoveZ = S(i.Move.Z, UnitScale), MoveMag = S(i.MoveMagnitude, UnitScale),
                RawX = S(i.AimRawX, UnitScale), RawY = S(i.AimRawY, UnitScale),
                AimX = S(i.AimPointX, MetreScale), AimY = S(i.AimPointY, MetreScale),
                HitMs = I(i.Hit.HeldSeconds, SecondScale), TouchMs = I(i.Touch.HeldSeconds, SecondScale),
                Flags = f,
            };
        }

        /// <summary>Write a frame into an intent (every field the sim reads; the pause key is not the sim's).</summary>
        public static void Decode(QFrame q, InputIntent i)
        {
            i.Move.X = q.MoveX / UnitScale; i.Move.Z = q.MoveZ / UnitScale; i.MoveMagnitude = q.MoveMag / UnitScale;
            i.AimRawX = q.RawX / UnitScale; i.AimRawY = q.RawY / UnitScale;
            i.AimPointX = q.AimX / MetreScale; i.AimPointY = q.AimY / MetreScale;
            i.HasAimPoint = (q.Flags & FHasAim) != 0;
            i.Hit.Pressed = (q.Flags & FHitP) != 0; i.Hit.Released = (q.Flags & FHitR) != 0; i.Hit.Held = (q.Flags & FHitH) != 0;
            i.Hit.HeldSeconds = q.HitMs / SecondScale;
            i.Touch.Pressed = (q.Flags & FTouchP) != 0; i.Touch.Released = (q.Flags & FTouchR) != 0; i.Touch.Held = (q.Flags & FTouchH) != 0;
            i.Touch.HeldSeconds = q.TouchMs / SecondScale;
            i.MomentumFocus = (q.Flags & FFocus) != 0; i.SplitStepPressed = (q.Flags & FSplit) != 0; i.AppealPressed = (q.Flags & FAppeal) != 0;
            i.DivePressed = (q.Flags & FDive) != 0; i.Sprint = (q.Flags & FSprint) != 0; i.BoastPressed = (q.Flags & FBoast) != 0;
            i.PausePressed = false;
        }
    }
}
