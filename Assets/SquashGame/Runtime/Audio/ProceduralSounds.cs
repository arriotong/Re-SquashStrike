// ===========================================================
// Assets/SquashGame/Runtime/Audio/ProceduralSounds.cs
// The on-court sound set, synthesised at start-up (no audio files, no
// packages). v2 (2026-10-19, "they sound pretty bad"): the first set was
// plain decaying sine tones, which read as beeps. Now each sound is made
// the way the real one is:
//
//   excitation   the contact itself - a half-sine pulse as long as the
//                ball is squashed against the surface (short = bright,
//                long = dull), plus a burst of filtered noise (the slap)
//   body         a bank of resonators (two-pole filters) driven by it:
//                the surface's own modes - a wall's low thump, the glass
//                panel's ring, the tin's metal, the strings' twang
//   the court    a small baked-in room (early reflections + a short
//                diffuse tail): a squash court is a hard, live box
//   variants     three takes of each impact (detuned modes, new noise),
//                picked at random, so a rally is not a machine gun
//
//   ping       the racket on the ball (crack, pock, strings, thump)
//   power      layered on a hard, fast hit (boom and whack)
//   frame      the frame
//   front      front wall                            side     side wall
//   glass      the glass back wall                   tin      the tin
//   floor      a floor bounce                        nick     the nick
//   body       the ball or a player into a body      squeak   shoes
//   call       referee chime                         gameCall end of game
//
// A recording can replace any of them: put "<name>.wav" (or .ogg) in
// Assets/SquashGame/Resources/Audio/ and it is used instead (AudioDirector).
// Pure functions of the name and the take: the same samples every run.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Audio
{
    public static class ProceduralSounds
    {
        public const int Rate = 44100;
        public const int Variants = 3;

        public static readonly string[] Names = { "ping", "power", "frame", "front", "side", "glass", "tin", "floor", "nick", "body", "squeak", "call", "gameCall" };

        /// <summary>One sound's samples (mono, -1..1). <paramref name="variant"/> 0..Variants-1.</summary>
        public static float[] Samples(string name, int variant = 0)
        {
            uint seed = (uint)(System.Array.IndexOf(Names, name) + 1) * 2654435761u + (uint)variant * 7919u + 17u;
            double d = 1 + 0.035 * (variant - 1);                     // each take detuned a little
            switch (name)
            {
                // the hit (v3, 2026-10-19 "make it powerful, satisfying"): a crack, the ball's pock, the strings, a low thump
                case "ping": return Hit(seed, d, false);
                // a hard, fast hit: layered on top of the ping - a deeper boom and a whack
                case "power": return Hit(seed, d, true);
                // the frame: harder, drier, hollow clack
                case "frame": return Impact(seed, 0.08, 0.0015, 0.9, 0.0020, 0.9,
                    new[] { M(830 * d, 0.022, 0.8), M(2350 * d, 0.010, 0.6), M(3900 * d, 0.006, 0.4), M(310 * d, 0.015, 0.3) }, 0.14, 0.8);
                // front wall: a solid thump and the ball's pock, the court answers
                case "front": return Impact(seed, 0.16, 0.0040, 0.45, 0.0030, 0.55,
                    new[] { M(92 * d, 0.055, 1.0), M(205 * d, 0.030, 0.55), M(640 * d, 0.010, 0.30), M(1750 * d, 0.004, 0.25) }, 0.30, 0.95);
                case "side": return Impact(seed, 0.13, 0.0035, 0.5, 0.0025, 0.55,
                    new[] { M(125 * d, 0.040, 0.9), M(270 * d, 0.022, 0.5), M(760 * d, 0.008, 0.3), M(1900 * d, 0.004, 0.2) }, 0.28, 0.9);
                // the glass: a deeper boom with the panel's ring
                case "glass": return Impact(seed, 0.30, 0.0045, 0.35, 0.0030, 0.45,
                    new[] { M(78 * d, 0.070, 1.0), M(150 * d, 0.050, 0.6), M(690 * d, 0.110, 0.16), M(1240 * d, 0.080, 0.12), M(2210 * d, 0.050, 0.07) }, 0.30, 0.9);
                // the tin: a hollow sheet-metal bonk that rings
                case "tin": return Impact(seed, 0.60, 0.0020, 0.7, 0.0020, 0.6,
                    new[] { M(232 * d, 0.24, 0.8), M(611 * d, 0.18, 0.55), M(1103 * d, 0.13, 0.4), M(1722 * d, 0.09, 0.28), M(2561 * d, 0.06, 0.18), M(110 * d, 0.05, 0.5) }, 0.22, 0.9);
                // the floor: softer, wooden, short
                case "floor": return Impact(seed, 0.12, 0.0050, 0.4, 0.0030, 0.35,
                    new[] { M(135 * d, 0.032, 0.9), M(320 * d, 0.016, 0.45), M(880 * d, 0.006, 0.2) }, 0.24, 0.85);
                // the nick: a sharp crack that dies at once
                case "nick": return Impact(seed, 0.12, 0.0015, 0.9, 0.0015, 0.9,
                    new[] { M(105 * d, 0.025, 0.9), M(480 * d, 0.008, 0.4), M(2900 * d, 0.003, 0.5) }, 0.28, 0.95);
                // a body: dull, padded
                case "body": return Impact(seed, 0.12, 0.0080, 0.15, 0.0060, 0.2,
                    new[] { M(78 * d, 0.040, 1.0), M(165 * d, 0.020, 0.35) }, 0.12, 0.8);
                case "squeak": return Squeak(seed, d);
                case "call": return Tones(new[] { 784.0, 659.3 }, 0.22);
                case "gameCall": return Tones(new[] { 659.3, 784.0, 1046.5 }, 0.26);
                default: return new float[1];
            }
        }

        /// <summary>Does this sound come in takes (Variants)?</summary>
        public static bool Varies(string name) { return name != "call" && name != "gameCall"; }

        public static AudioClip Clip(string name, int variant = 0)
        {
            float[] s = Samples(name, variant);
            var clip = AudioClip.Create("sq_" + name + "_" + variant, s.Length, 1, Rate, false);
            clip.SetData(s, 0);
            return clip;
        }

        /// <summary>Every sound's takes, by name.</summary>
        public static Dictionary<string, AudioClip[]> AllClips()
        {
            var d = new Dictionary<string, AudioClip[]>();
            foreach (var n in Names)
            {
                int k = Varies(n) ? Variants : 1;
                var a = new AudioClip[k];
                for (int v = 0; v < k; v++) a[v] = Clip(n, v);
                d[n] = a;
            }
            return d;
        }

        // ---------------- building blocks ----------------

        struct Mode { public double F, Decay, Gain; }
        static Mode M(double hz, double decaySeconds, double gain) { return new Mode { F = hz, Decay = decaySeconds, Gain = gain }; }

        /// <summary>A contact: a half-sine pulse of <paramref name="contact"/> s and a noise slap (<paramref name="noiseDecay"/> s,
        /// <paramref name="brightness"/> 0..1) ring the surface's modes, with a little of the raw contact on top for the
        /// attack; then the room (<paramref name="wet"/>). <paramref name="seconds"/>: the dry sound (the room adds its tail).</summary>
        static float[] Impact(uint seed, double seconds, double contact, double noiseAmp, double noiseDecay, double brightness,
                              Mode[] modes, double wet, double peak)
        {
            int n = (int)(seconds * Rate);
            var x = new double[n];
            uint r = seed;
            double lp = 0;
            double a = 0.08 + 0.9 * brightness;
            int pulse = System.Math.Max(2, (int)(contact * Rate));
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double v = i < pulse ? System.Math.Sin(System.Math.PI * i / pulse) : 0;      // the squash of the ball
                r = r * 1664525u + 1013904223u;
                double white = (r / 4294967296.0) * 2 - 1;
                lp += (white - lp) * a;                                                     // darker or brighter
                v += noiseAmp * System.Math.Exp(-t / noiseDecay) * lp;
                x[i] = v;
            }
            // the surface's modes, rung by the contact (two-pole resonators)
            var y = new double[n];
            foreach (var m in modes)
            {
                double w = 2 * System.Math.PI * m.F / Rate;
                double rad = System.Math.Exp(-1.0 / (m.Decay * Rate));
                double c1 = 2 * rad * System.Math.Cos(w), c2 = -rad * rad;
                double g = m.Gain * (1 - rad) * 4;                                          // roughly even loudness across decays
                double y1 = 0, y2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double yy = c1 * y1 + c2 * y2 + g * x[i];
                    y2 = y1; y1 = yy;
                    y[i] += yy;
                }
            }
            double dryPeak = 1e-9, xPeak = 1e-9;
            for (int i = 0; i < n; i++) { dryPeak = System.Math.Max(dryPeak, System.Math.Abs(y[i])); xPeak = System.Math.Max(xPeak, System.Math.Abs(x[i])); }
            for (int i = 0; i < n; i++)
            {
                double tail = System.Math.Min(1.0, (n - i) / (0.012 * Rate));
                y[i] = (y[i] / dryPeak + 0.18 * x[i] / xPeak) * tail;
            }
            return Normalise(Room(y, wet), (float)peak);
        }

        /// <summary>The court: a few early reflections off the walls, then a short dark diffuse tail (Schroeder).</summary>
        static float[] Room(double[] dry, double wet)
        {
            int tailN = wet > 0 ? (int)(0.42 * Rate) : 0;
            int n = dry.Length + tailN;
            var o = new double[n];
            for (int i = 0; i < dry.Length; i++) o[i] = dry[i];
            if (wet <= 0) return ToFloat(o);
            // early reflections (ms, gain): the side walls, the front, the floor
            double[] er = { 7.1, 0.42, 11.3, 0.33, 16.9, 0.27, 23.2, 0.2, 31.7, 0.14 };
            var early = new double[n];
            for (int k = 0; k < er.Length; k += 2)
            {
                int off = (int)(er[k] * 0.001 * Rate);
                for (int i = 0; i < dry.Length && i + off < n; i++) early[i + off] += dry[i] * er[k + 1];
            }
            // diffuse tail: four damped combs in parallel, two allpasses in series
            var tail = new double[n];
            foreach (int len in new[] { 1116, 1188, 1277, 1356 })
            {
                var buf = new double[len]; int p = 0; double store = 0;
                for (int i = 0; i < n; i++)
                {
                    double input = (i < dry.Length ? dry[i] : 0) * 0.25;
                    double outv = buf[p];
                    store = outv * 0.55 + store * 0.45;
                    buf[p] = input + store * 0.72;
                    p = (p + 1) % len;
                    tail[i] += outv;
                }
            }
            foreach (int len in new[] { 556, 441 })
            {
                var buf = new double[len]; int p = 0;
                for (int i = 0; i < n; i++)
                {
                    double bo = buf[p], inp = tail[i];
                    buf[p] = inp + bo * 0.5;
                    tail[i] = bo - inp;
                    p = (p + 1) % len;
                }
            }
            for (int i = 0; i < n; i++)
            {
                double fade = i < dry.Length ? 1 : System.Math.Exp(-(i - dry.Length) / (0.11 * Rate));
                o[i] += wet * (early[i] + 0.6 * tail[i]) * fade;
            }
            int end = (int)(0.01 * Rate);
            for (int i = n - end; i < n; i++) o[i] *= (double)(n - i) / end;
            return ToFloat(o);
        }

        /// <summary>The racket on the ball. Layers: a sharp broadband crack (the strings snapping back), the ball's hollow
        /// pock, the string bed's twang, and a low thump with a falling pitch for weight; a little saturation glues them.
        /// The power take drops the twang for a deeper boom and a mid "whack".</summary>
        static float[] Hit(uint seed, double d, bool power)
        {
            double seconds = power ? 0.22 : 0.14;
            int n = (int)(seconds * Rate);
            var o = new double[n];
            uint r = seed;
            double lp = 0, ph = 0;
            double y1 = 0, y2 = 0;                                          // the ball's pock (a resonator on the crack)
            double pf = (power ? 760 : 1050) * d, pw = 2 * System.Math.PI * pf / Rate, prad = System.Math.Exp(-1.0 / (0.014 * Rate));
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                r = r * 1664525u + 1013904223u;
                double white = (r / 4294967296.0) * 2 - 1;
                lp += (white - lp) * 0.55;
                double crack = (white * 0.6 + lp * 0.4) * System.Math.Exp(-t / (power ? 0.0022 : 0.0015));
                double pock = 2 * prad * System.Math.Cos(pw) * y1 - prad * prad * y2 + crack * 0.35;
                y2 = y1; y1 = pock;
                double twang = power ? 0 : (0.55 * System.Math.Sin(2 * System.Math.PI * 520 * d * t) + 0.25 * System.Math.Sin(2 * System.Math.PI * 1180 * d * t)) * System.Math.Exp(-t / 0.032);
                double f = power ? 55 + 95 * System.Math.Exp(-t / 0.03) : 92 + 80 * System.Math.Exp(-t / 0.018);
                ph += 2 * System.Math.PI * f / Rate;
                double thump = System.Math.Sin(ph) * System.Math.Exp(-t / (power ? 0.085 : 0.042)) * (power ? 1.25 : 0.85);
                double whack = power ? lp * System.Math.Exp(-t / 0.03) * 0.5 : 0;
                double v = crack * 0.9 + pock * 3.2 + twang + thump + whack;
                double attack = System.Math.Min(1.0, t / 0.0004);
                double tail = System.Math.Min(1.0, (n - i) / (0.01 * Rate));
                o[i] = System.Math.Tanh(v * 1.6) * attack * tail;            // weight: soft saturation
            }
            return Normalise(Room(o, power ? 0.24 : 0.17), power ? 0.95f : 0.92f);
        }

        /// <summary>A shoe on the court, light (v3, 2026-10-19: the squeal annoyed): a short soft rubber chirp - mostly
        /// band-limited noise with a hint of pitch, not a whistle.</summary>
        static float[] Squeak(uint seed, double d)
        {
            double seconds = 0.075;
            int n = (int)(seconds * Rate);
            var o = new double[n];
            uint r = seed;
            double y1 = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate, u = t / seconds;
                double f = (2600 + 500 * u) * d;
                double w = 2 * System.Math.PI * f / Rate, rad = 0.975;                     // a broad band: a scuff with a little chirp
                r = r * 1664525u + 1013904223u;
                double white = (r / 4294967296.0) * 2 - 1;
                double yy = 2 * rad * System.Math.Cos(w) * y1 - rad * rad * y2 + white * 0.05;
                y2 = y1; y1 = yy;
                double env = System.Math.Sin(System.Math.PI * u);
                o[i] = env * env * yy;
            }
            double pk = 1e-9; for (int i = 0; i < n; i++) pk = System.Math.Max(pk, System.Math.Abs(o[i]));
            for (int i = 0; i < n; i++) o[i] /= pk;
            return Normalise(Room(o, 0.08), 0.32f);
        }

        static float[] Tones(double[] hz, double each)
        {
            int per = (int)(each * Rate);
            var o = new float[per * hz.Length + Rate / 5];
            for (int k = 0; k < hz.Length; k++)
                for (int i = 0; i < per + Rate / 5 && k * per + i < o.Length; i++)
                {
                    double t = (double)i / Rate;
                    double env = System.Math.Min(1.0, t / 0.01) * System.Math.Exp(-t / 0.16);
                    o[k * per + i] += (float)(env * (System.Math.Sin(2 * System.Math.PI * hz[k] * t) + 0.3 * System.Math.Sin(4 * System.Math.PI * hz[k] * t)));
                }
            int end = Rate / 50;                                       // fade out: no click at the end
            for (int i = o.Length - end; i < o.Length; i++) o[i] *= (float)(o.Length - i) / end;
            return Normalise(o, 0.5f);
        }

        static float[] ToFloat(double[] d) { var o = new float[d.Length]; for (int i = 0; i < d.Length; i++) o[i] = (float)d[i]; return o; }

        static float[] Normalise(float[] o, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < o.Length; i++) m = Mathf.Max(m, Mathf.Abs(o[i]));
            float k = peak / m;
            for (int i = 0; i < o.Length; i++) o[i] *= k;
            return o;
        }
    }
}
