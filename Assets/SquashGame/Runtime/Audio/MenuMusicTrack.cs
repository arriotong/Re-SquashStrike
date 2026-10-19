// ===========================================================
// Assets/SquashGame/Runtime/Audio/MenuMusicTrack.cs
// The menus' music (playtest 2026-10-19: "FC-style home page vibes,
// chill"): an original chill-house loop, composed and synthesised here -
// no audio files. 118 BPM, A minor, 32 bars (~65 s), seamless:
//
//   bars  1-4   pads and soft hats (the intro the loop comes back to)
//   bars  5-12  + kick (four on the floor), clap, offbeat bass
//   bars 13-20  + Rhodes stabs and the melody (pentatonic, echoed)
//   bars 21-24  breakdown: pads, melody, no kick
//   bars 25-32  everything
//
// Chords: Fmaj9 - Am9 - Dm9 - Em7, two bars each. Pads duck with the kick
// (the house "pump"). Pure function of nothing: the same track every time.
// Rendered once off the main thread (MenuMusic).
// ===========================================================

using System;

namespace SquashGame.Audio
{
    public static class MenuMusicTrack
    {
        public const int Rate = 32000;
        public const double Bpm = 118;
        public const int Bars = 32;
        static double Beat { get { return 60.0 / Bpm; } }

        // MIDI notes: four chords, voiced for the pads / stabs, and their roots for the bass
        static readonly int[][] Chords =
        {
            new[] { 53, 57, 60, 64, 67 },   // Fmaj9: F A C E G
            new[] { 57, 60, 64, 67, 71 },   // Am9:   A C E G B
            new[] { 50, 53, 57, 60, 64 },   // Dm9:   D F A C E
            new[] { 52, 55, 59, 62, 66 },   // Em7(add9): E G B D F#
        };
        static readonly int[] Roots = { 41, 45, 38, 40 };     // F2 A2 D2 E2
        // the melody: (beat in a 4-bar phrase, note, beats long); A minor pentatonic
        static readonly double[,] Phrase =
        {
            { 0.5, 76, 0.5 }, { 1.5, 74, 0.5 }, { 2.0, 72, 1.0 }, { 3.5, 69, 0.5 },
            { 4.5, 72, 0.5 }, { 5.0, 74, 1.5 }, { 7.0, 76, 0.5 },
            { 8.5, 79, 0.5 }, { 9.0, 76, 1.0 }, { 10.5, 74, 0.5 }, { 11.0, 72, 1.0 },
            { 12.5, 69, 0.5 }, { 13.0, 72, 0.5 }, { 13.5, 74, 2.0 },
        };

        static double Hz(double midi) { return 440.0 * Math.Pow(2, (midi - 69) / 12.0); }

        /// <summary>The whole loop, stereo interleaved (L R L R ...), peak ~0.8.</summary>
        public static float[] Render()
        {
            double beat = Beat;
            int loopN = (int)(Bars * 4 * beat * Rate);
            int tail = Rate * 3;                                     // tails past the end wrap to the start
            var L = new double[loopN + tail];
            var R = new double[loopN + tail];
            var duck = new double[loopN + tail];
            for (int i = 0; i < duck.Length; i++) duck[i] = 1;
            uint rng = 0x2545F491u;

            for (int bar = 0; bar < Bars; bar++)
            {
                bool drums = (bar >= 4 && bar < 20) || bar >= 24;
                bool keys = (bar >= 12 && bar < 20) || bar >= 24;
                bool melody = bar >= 12;
                bool bass = drums;
                int chord = (bar / 2) % 4;
                double t0 = bar * 4 * beat;

                // pads: a chord every two bars, slow swell
                if (bar % 2 == 0)
                    foreach (int n in Chords[chord])
                        Pad(L, R, t0, 8 * beat, Hz(n), 0.055);

                for (int b = 0; b < 4; b++)
                {
                    double tb = t0 + b * beat;
                    if (drums) { Kick(L, R, tb, 0.55); Duck(duck, tb, beat); }
                    // hats on the offbeats (and a soft one on the beat in the intro)
                    Hat(L, R, tb + beat / 2, drums ? 0.07 : 0.035, ref rng, 0.25);
                    if (drums && (b == 1 || b == 3)) Clap(L, R, tb, 0.13, ref rng);
                    if (bass) Bass(L, R, tb + beat / 2, beat * 0.42, Hz(Roots[chord] + (b == 3 && bar % 2 == 1 ? 7 : 0)), 0.26);
                }
                // Rhodes stabs: the "and" of 2 and of 4
                if (keys)
                {
                    foreach (int n in Chords[chord]) { Keys(L, R, t0 + 1.5 * beat, Hz(n + 12), 0.035); Keys(L, R, t0 + 3.5 * beat, Hz(n + 12), 0.03); }
                }
            }

            // melody, into its own echo bus
            var mL = new double[L.Length]; var mR = new double[R.Length];
            for (int p = 12; p < Bars; p += 4)
            {
                if (p >= 20 && p < 24 && p != 20) continue;
                double t0 = p * 4 * beat;
                for (int k = 0; k < Phrase.GetLength(0); k++)
                {
                    double on = t0 + Phrase[k, 0] * beat;
                    if (on * Rate >= loopN) break;
                    Pluck(mL, mR, on, Phrase[k, 2] * beat, Hz(Phrase[k, 1]), 0.11);
                }
            }
            Echo(mL, mR, (int)(0.75 * beat * Rate), 0.38, 0.33);

            // the pump: pads and keys under the kick (applied to everything but the kick's own transient is fine at this level)
            for (int i = 0; i < L.Length; i++) { L[i] = L[i] * duck[i] + mL[i]; R[i] = R[i] * duck[i] + mR[i]; }

            // wrap the tails into the start: a seamless loop
            for (int i = 0; i < tail; i++) { L[i] += L[loopN + i]; R[i] += R[loopN + i]; }

            // warmth: a gentle low-pass and soft saturation, then the level
            double lpL = 0, lpR = 0, a = 0.35, peak = 1e-9;
            var outp = new float[loopN * 2];
            var tmp = new double[loopN * 2];
            for (int i = 0; i < loopN; i++)
            {
                lpL += (L[i] - lpL) * a; lpR += (R[i] - lpR) * a;
                double l = Math.Tanh(lpL * 1.3), r = Math.Tanh(lpR * 1.3);
                tmp[2 * i] = l; tmp[2 * i + 1] = r;
                peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
            }
            double g = 0.8 / peak;
            for (int i = 0; i < tmp.Length; i++) outp[i] = (float)(tmp[i] * g);
            return outp;
        }

        // ---------------- instruments ----------------

        static void Put(double[] L, double[] R, int i, double l, double r)
        {
            if (i < 0 || i >= L.Length) return;
            L[i] += l; R[i] += r;
        }

        /// <summary>A soft saw-ish pad (a few harmonics), two voices detuned left and right, slow swell.</summary>
        static void Pad(double[] L, double[] R, double t0, double dur, double f, double amp)
        {
            int s = (int)(t0 * Rate), n = (int)((dur + 1.2) * Rate);
            double det = Math.Pow(2, 7 / 1200.0);
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double env = Math.Min(1, t / 0.9) * (t < dur ? 1 : Math.Exp(-(t - dur) / 0.35));
                double vl = 0, vr = 0;
                for (int h = 1; h <= 5; h++)
                {
                    double w = 1.0 / (h * h * 0.6 + 0.4);
                    vl += w * Math.Sin(2 * Math.PI * f * h * t / det);
                    vr += w * Math.Sin(2 * Math.PI * f * h * t * det + 0.7);
                }
                double lfo = 1 + 0.06 * Math.Sin(2 * Math.PI * 0.23 * t);
                Put(L, R, s + k, amp * env * lfo * vl, amp * env * lfo * vr);
            }
        }

        static void Kick(double[] L, double[] R, double t0, double amp)
        {
            int s = (int)(t0 * Rate), n = (int)(0.32 * Rate);
            double ph = 0;
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double f = 48 + 72 * Math.Exp(-t / 0.035);
                ph += 2 * Math.PI * f / Rate;
                double v = Math.Sin(ph) * Math.Exp(-t / 0.16) + 0.25 * Math.Exp(-t / 0.003) * Math.Sin(2 * Math.PI * 1800 * t);
                Put(L, R, s + k, amp * v, amp * v);
            }
        }

        /// <summary>The sidechain: everything else dips after each kick and swells back (the house pump).</summary>
        static void Duck(double[] duck, double t0, double beat)
        {
            int s = (int)(t0 * Rate), n = (int)(beat * Rate);
            for (int k = 0; k < n && s + k < duck.Length; k++)
            {
                double t = (double)k / Rate;
                duck[s + k] = Math.Min(duck[s + k], 1 - 0.45 * Math.Exp(-t / 0.13));
            }
        }

        static double Noise(ref uint r) { r = r * 1664525u + 1013904223u; return (r / 4294967296.0) * 2 - 1; }

        static void Hat(double[] L, double[] R, double t0, double amp, ref uint r, double pan)
        {
            int s = (int)(t0 * Rate), n = (int)(0.05 * Rate);
            double prev = 0;
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double w = Noise(ref r), hp = w - prev; prev = w;              // a bright high-passed tick
                double v = amp * hp * Math.Exp(-t / 0.014);
                Put(L, R, s + k, v * (1 - pan), v * (1 + pan));
            }
        }

        static void Clap(double[] L, double[] R, double t0, double amp, ref uint r)
        {
            int s = (int)(t0 * Rate), n = (int)(0.22 * Rate);
            double y1 = 0, y2 = 0, w0 = 2 * Math.PI * 1300 / Rate, rad = 0.93;
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double env = Math.Exp(-t / 0.06) * (t < 0.025 ? 0.6 + 0.4 * Math.Abs(Math.Sin(t * 2 * Math.PI * 90)) : 1);
                double x = Noise(ref r) * env;
                double y = 2 * rad * Math.Cos(w0) * y1 - rad * rad * y2 + x * 0.25;
                y2 = y1; y1 = y;
                Put(L, R, s + k, amp * y, amp * y);
            }
        }

        static void Bass(double[] L, double[] R, double t0, double dur, double f, double amp)
        {
            int s = (int)(t0 * Rate), n = (int)((dur + 0.05) * Rate);
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double env = Math.Min(1, t / 0.008) * (t < dur ? Math.Exp(-t / 0.5) : Math.Exp(-dur / 0.5) * Math.Exp(-(t - dur) / 0.02));
                double v = Math.Sin(2 * Math.PI * f * t) + 0.25 * Math.Sin(4 * Math.PI * f * t);
                Put(L, R, s + k, amp * env * v, amp * env * v);
            }
        }

        /// <summary>A Rhodes-ish electric piano: two-operator FM, the brightness fading with the note.</summary>
        static void Keys(double[] L, double[] R, double t0, double f, double amp)
        {
            int s = (int)(t0 * Rate), n = (int)(0.9 * Rate);
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double idx = 0.3 + 1.8 * Math.Exp(-t / 0.08);
                double v = Math.Sin(2 * Math.PI * f * t + idx * Math.Sin(2 * Math.PI * f * t)) * Math.Exp(-t / 0.35) * Math.Min(1, t / 0.003);
                Put(L, R, s + k, amp * v * 0.9, amp * v * 1.1);
            }
        }

        static void Pluck(double[] L, double[] R, double t0, double dur, double f, double amp)
        {
            int s = (int)(t0 * Rate), n = (int)((dur + 0.6) * Rate);
            for (int k = 0; k < n; k++)
            {
                double t = (double)k / Rate;
                double env = Math.Min(1, t / 0.004) * Math.Exp(-t / 0.28);
                double v = Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(6 * Math.PI * f * t) * Math.Exp(-t / 0.05) + 0.15 * Math.Sin(4 * Math.PI * f * t);
                Put(L, R, s + k, amp * env * v, amp * env * v);
            }
        }

        /// <summary>A ping-pong echo on the melody: the repeats bounce left and right.</summary>
        static void Echo(double[] L, double[] R, int delay, double feedback, double wet)
        {
            var dl = new double[L.Length]; var dr = new double[R.Length];
            for (int i = delay; i < L.Length; i++)
            {
                dl[i] = R[i - delay] * wet + dr[i - delay] * feedback;
                dr[i] = L[i - delay] * wet + dl[i - delay] * feedback;
            }
            for (int i = 0; i < L.Length; i++) { L[i] += dl[i]; R[i] += dr[i]; }
        }
    }
}
