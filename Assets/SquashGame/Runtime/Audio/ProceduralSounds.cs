// ===========================================================
// Assets/SquashGame/Runtime/Audio/ProceduralSounds.cs
// The M5 sound set, synthesised at start-up: no audio files, no new
// packages. Each sound is a few damped partials plus shaped noise,
// tuned by ear to what a glass court sounds like:
//
//   ping       racket strings on the sweet spot (bright, short)
//   frame      off-centre / frame contact (dry clack, hollow)
//   front      front wall (plaster: low thud with a slap)
//   side       side wall (a little higher, shorter)
//   glass      back wall glass (thud + glassy ring)
//   tin        the tin (inharmonic metallic clang, long tail)
//   floor      floor bounce (soft, wooden)
//   nick       the nick (sharp crack, dead)
//   body       ball or player hitting a body (dull)
//   squeak     shoe squeak on a hard stop (chirp with vibrato)
//   call       referee call chime (two soft tones)
//   gameCall   end of game chime (three tones)
//
// Pure functions of a seed: the same samples every run (EditMode-testable).
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Audio
{
    public static class ProceduralSounds
    {
        public const int Rate = 44100;

        public static readonly string[] Names = { "ping", "frame", "front", "side", "glass", "tin", "floor", "nick", "body", "squeak", "call", "gameCall" };

        /// <summary>One sound's samples (mono, -1..1).</summary>
        public static float[] Samples(string name)
        {
            switch (name)
            {
                case "ping":     return Mix(0.075, new[] { P(1150, 0.018, 0.9), P(2380, 0.010, 0.45), P(4200, 0.005, 0.25) }, N(0.008, 0.6, 0.35), 1);
                case "frame":    return Mix(0.070, new[] { P(640, 0.020, 0.7), P(3500, 0.004, 0.6), P(1730, 0.008, 0.3) }, N(0.004, 0.9, 0.5), 2);
                case "front":    return Mix(0.160, new[] { P(118, 0.045, 1.0), P(240, 0.022, 0.45), P(1300, 0.004, 0.2) }, N(0.012, 0.25, 0.45), 3);
                case "side":     return Mix(0.120, new[] { P(165, 0.032, 0.9), P(390, 0.016, 0.4) }, N(0.010, 0.3, 0.4), 4);
                case "glass":    return Mix(0.300, new[] { P(140, 0.035, 0.8), P(930, 0.090, 0.30), P(1710, 0.070, 0.22), P(2890, 0.050, 0.12) }, N(0.010, 0.4, 0.3), 5);
                case "tin":      return Mix(0.700, new[] { P(523, 0.250, 0.55), P(1311, 0.180, 0.45), P(2207, 0.120, 0.35), P(3397, 0.080, 0.25), P(170, 0.040, 0.5) }, N(0.006, 0.8, 0.4), 6);
                case "floor":    return Mix(0.110, new[] { P(210, 0.026, 0.8), P(470, 0.012, 0.35) }, N(0.008, 0.2, 0.4), 7);
                case "nick":     return Mix(0.140, new[] { P(95, 0.030, 0.9), P(2600, 0.003, 0.7) }, N(0.003, 1.0, 0.8), 8);
                case "body":     return Mix(0.120, new[] { P(88, 0.040, 0.9), P(180, 0.020, 0.3) }, N(0.020, 0.12, 0.5), 9);
                case "squeak":   return Squeak();
                case "call":     return Tones(new[] { 784.0, 659.3 }, 0.22);
                case "gameCall": return Tones(new[] { 659.3, 784.0, 1046.5 }, 0.26);
                default: return new float[1];
            }
        }

        public static AudioClip Clip(string name)
        {
            float[] s = Samples(name);
            var clip = AudioClip.Create("sq_" + name, s.Length, 1, Rate, false);
            clip.SetData(s, 0);
            return clip;
        }

        public static Dictionary<string, AudioClip> AllClips()
        {
            var d = new Dictionary<string, AudioClip>();
            foreach (var n in Names) d[n] = Clip(n);
            return d;
        }

        // ---------------- building blocks ----------------

        struct Partial { public double F, Decay, Amp; }
        struct Noise { public double Decay, Brightness, Amp; }

        static Partial P(double hz, double decaySeconds, double amp) { return new Partial { F = hz, Decay = decaySeconds, Amp = amp }; }
        static Noise N(double decaySeconds, double brightness, double amp) { return new Noise { Decay = decaySeconds, Brightness = brightness, Amp = amp }; }

        /// <summary>Damped partials + one-pole-filtered noise burst, with a 1 ms attack and peak-normalised.</summary>
        static float[] Mix(double seconds, Partial[] partials, Noise noise, uint seed)
        {
            int n = (int)(seconds * Rate);
            var o = new float[n];
            uint r = 0x9E3779B9u * seed + 1;
            double lp = 0;
            double a = Mathf.Clamp01((float)noise.Brightness);       // 0 = dark, 1 = bright
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double v = 0;
                foreach (var p in partials) v += p.Amp * System.Math.Exp(-t / p.Decay) * System.Math.Sin(2 * System.Math.PI * p.F * t);
                r = r * 1664525u + 1013904223u;
                double white = (r / 4294967296.0) * 2 - 1;
                lp += (white - lp) * (0.05 + 0.95 * a);
                v += noise.Amp * System.Math.Exp(-t / noise.Decay) * lp;
                double attack = System.Math.Min(1.0, t / 0.001);
                double tail = System.Math.Min(1.0, (seconds - t) / 0.01);   // no click at the end
                o[i] = (float)(v * attack * tail);
            }
            return Normalise(o, 0.9f);
        }

        static float[] Squeak()
        {
            double seconds = 0.13;
            int n = (int)(seconds * Rate);
            var o = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate, u = t / seconds;
                double f = 1750 + 850 * u + 90 * System.Math.Sin(2 * System.Math.PI * 38 * t);   // rubber chirp with judder
                phase += 2 * System.Math.PI * f / Rate;
                double env = System.Math.Sin(System.Math.PI * u);
                env *= env;
                o[i] = (float)(env * (0.7 * System.Math.Sin(phase) + 0.25 * System.Math.Sin(2 * phase + 0.4) + 0.1 * System.Math.Sin(3 * phase)));
            }
            return Normalise(o, 0.6f);
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
            return Normalise(o, 0.5f);
        }

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
