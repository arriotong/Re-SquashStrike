// ===========================================================
// Assets/SquashGame/Runtime/Audio/CrowdSounds.cs
// PR4: the crowd, synthesised like the court's sounds (no recordings
// needed; a file in Resources/Audio/ with the same name still wins):
//
//   crowdBed   a seamless 8 s loop of a hall full of people talking low:
//              many band-limited voices, each swelling and fading
//   applause   claps - short bright noise bursts at random - building fast
//              and dying away; three takes
//   ooh        a crowd's "ooh": a few dozen detuned voices through an "oo"
//              vowel, swelling then falling in pitch; two takes
//   cheer      applause with a shouted "yeah" swell on top, for a game won
//
// Deterministic (seeded LCG) so the takes are the same every run.
// ===========================================================

using System;

namespace SquashGame.Audio
{
    public static class CrowdSounds
    {
        public const int Rate = ProceduralSounds.Rate;
        public static readonly string[] Names = { "crowdBed", "applause", "ooh", "cheer" };
        public static int Takes(string name) { return name == "applause" ? 3 : name == "ooh" ? 2 : 1; }
        /// <summary>Loops seamlessly (the bed).</summary>
        public static bool Loops(string name) { return name == "crowdBed"; }

        public static float[] Samples(string name, int take = 0)
        {
            uint seed = 0xC0FFEEu + (uint)take * 7919u + (uint)name.Length * 104729u;
            switch (name)
            {
                case "crowdBed": return Bed(seed);
                case "applause": return Normalise(Applause(seed, 3.2, 1.0), 0.85f);
                case "ooh": return Normalise(Ooh(seed, 1.4), 0.8f);
                case "cheer": return Normalise(Cheer(seed), 0.9f);
                default: return new float[Rate / 10];
            }
        }

        public static UnityEngine.AudioClip Clip(string name, int take = 0)
        {
            float[] s = Samples(name, take);
            var clip = UnityEngine.AudioClip.Create("crowd_" + name + "_" + take, s.Length, 1, Rate, false);
            clip.SetData(s, 0);
            return clip;
        }

        // ---------------- the sounds ----------------

        /// <summary>The hall: 28 murmuring voices (noise through a speech-band resonator, each with its own slow
        /// syllable rhythm and swell), mixed and looped with a crossfade so it has no seam.</summary>
        static float[] Bed(uint seed)
        {
            double len = 8.0, fade = 0.6;
            int n = (int)(len * Rate), f = (int)(fade * Rate);
            var x = new double[n + f];
            uint r = seed;
            for (int v = 0; v < 28; v++)
            {
                double centre = 260 + Rand(ref r) * 700;                     // the voice's formant
                double syll = 2.5 + Rand(ref r) * 3.0;                        // syllables a second
                double swellHz = 0.05 + Rand(ref r) * 0.15, swellPh = Rand(ref r) * 6.28;
                double gain = 0.4 + Rand(ref r) * 0.6;
                double w = 2 * Math.PI * centre / Rate, rad = 0.985;
                double c1 = 2 * rad * Math.Cos(w), c2 = -rad * rad, y1 = 0, y2 = 0;
                double phase = Rand(ref r);
                for (int i = 0; i < x.Length; i++)
                {
                    double t = (double)i / Rate;
                    double white = Rand(ref r) * 2 - 1;
                    double yy = c1 * y1 + c2 * y2 + white * (1 - rad);
                    y2 = y1; y1 = yy;
                    double syl = Math.Max(0, Math.Sin(2 * Math.PI * (syll * t + phase)));
                    double talk = Math.Max(0, Math.Sin(2 * Math.PI * swellHz * t + swellPh));   // talking or listening
                    x[i] += yy * syl * syl * (0.25 + 0.75 * talk) * gain;
                }
            }
            // A soft low rumble (the hall) and the loop's crossfade.
            var o = new float[n];
            double lp = 0; r = seed ^ 0xABCDu;
            double peak = 1e-9;
            var mixed = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                lp += ((Rand(ref r) * 2 - 1) - lp) * 0.01;
                mixed[i] = x[i] + lp * 0.8;
            }
            for (int i = 0; i < n; i++)
            {
                double s = mixed[i];
                if (i < f) { double k = (double)i / f; s = mixed[i] * k + mixed[n + i] * (1 - k); }
                o[i] = (float)s;
                peak = Math.Max(peak, Math.Abs(s));
            }
            for (int i = 0; i < n; i++) o[i] = (float)(o[i] / peak * 0.6);
            return o;
        }

        /// <summary>Claps: each a 1-2 ms rise and a 25-60 ms bright decay, timed at random with a density that builds
        /// in ~0.3 s and dies away over the rest.</summary>
        static double[] Applause(uint seed, double seconds, double density)
        {
            int n = (int)(seconds * Rate);
            var x = new double[n];
            uint r = seed;
            double t = 0;
            while (t < seconds)
            {
                double env = Math.Min(1, t / 0.3) * Math.Exp(-Math.Max(0, t - 0.4) / (seconds * 0.35));
                double rate = 6 + 70 * env * density;                            // claps a second
                t += -Math.Log(1 - Rand(ref r) * 0.999) / rate;
                if (t >= seconds) break;
                int start = (int)(t * Rate);
                double decay = 0.025 + Rand(ref r) * 0.035, amp = (0.4 + Rand(ref r) * 0.6) * (0.3 + 0.7 * env);
                double hp = 0, prev = 0, tone = 0.3 + Rand(ref r) * 0.5;
                int len = (int)(decay * 5 * Rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    double tt = (double)i / Rate;
                    double white = Rand(ref r) * 2 - 1;
                    hp = tone * (hp + white - prev); prev = white;                    // a bright clap
                    double e = Math.Min(1, tt / 0.0015) * Math.Exp(-tt / decay);
                    x[start + i] += hp * e * amp;
                }
            }
            Fade(x, 0.002, 0.25);
            return x;
        }

        /// <summary>"Ooh": voices on a sawtooth at 120-260 Hz with vibrato, through the "oo" vowel's two formants,
        /// rising in loudness and sliding down in pitch.</summary>
        static double[] Ooh(uint seed, double seconds)
        {
            int n = (int)(seconds * Rate);
            var x = new double[n];
            uint r = seed;
            for (int v = 0; v < 36; v++)
            {
                double f0 = 120 + Rand(ref r) * 140, vibHz = 4 + Rand(ref r) * 2, onset = Rand(ref r) * 0.12;
                double ph = Rand(ref r);
                double[] form = { 320, 800 }, bw = { 80, 120 };
                double[] fy1 = new double[2], fy2 = new double[2];
                double gain = 0.5 + Rand(ref r) * 0.5;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / Rate - onset;
                    if (t < 0) continue;
                    double slide = 1 - 0.18 * Math.Min(1, t / seconds);                  // the "oh-h" falls
                    double f = f0 * slide * (1 + 0.012 * Math.Sin(2 * Math.PI * vibHz * t));
                    ph += f / Rate; ph -= Math.Floor(ph);
                    double saw = ph * 2 - 1;
                    double y = 0;
                    for (int k = 0; k < 2; k++)
                    {
                        double rad = Math.Exp(-Math.PI * bw[k] / Rate), w = 2 * Math.PI * form[k] / Rate;
                        double yy = 2 * rad * Math.Cos(w) * fy1[k] - rad * rad * fy2[k] + saw * (1 - rad);
                        fy2[k] = fy1[k]; fy1[k] = yy;
                        y += yy * (k == 0 ? 1.0 : 0.5);
                    }
                    double env = Math.Min(1, t / 0.25) * Math.Exp(-Math.Max(0, t - 0.35) / 0.35);
                    x[i] += y * env * gain;
                }
            }
            // Breath: a little noise through the same vowel.
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                lp += ((Rand(ref r) * 2 - 1) - lp) * 0.08;
                x[i] += lp * 0.25 * Math.Min(1, t / 0.25) * Math.Exp(-Math.Max(0, t - 0.35) / 0.35);
            }
            Fade(x, 0.01, 0.2);
            return x;
        }

        /// <summary>A game won: heavy applause and a rising shouted "yeah" (voices on a brighter vowel, pitch up).</summary>
        static double[] Cheer(uint seed)
        {
            double seconds = 4.0;
            var x = Applause(seed, seconds, 1.6);
            int n = x.Length;
            uint r = seed ^ 0x5151u;
            for (int v = 0; v < 30; v++)
            {
                double f0 = 160 + Rand(ref r) * 180, onset = Rand(ref r) * 0.25, dur = 0.9 + Rand(ref r) * 0.8, ph = Rand(ref r);
                double[] fy1 = new double[2], fy2 = new double[2];
                double[] form = { 650, 1700 }, bw = { 110, 160 };
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / Rate - onset;
                    if (t < 0 || t > dur + 0.4) continue;
                    double f = f0 * (1 + 0.25 * Math.Min(1, t / 0.3));
                    ph += f / Rate; ph -= Math.Floor(ph);
                    double saw = ph * 2 - 1, y = 0;
                    for (int k = 0; k < 2; k++)
                    {
                        double rad = Math.Exp(-Math.PI * bw[k] / Rate), w = 2 * Math.PI * form[k] / Rate;
                        double yy = 2 * rad * Math.Cos(w) * fy1[k] - rad * rad * fy2[k] + saw * (1 - rad);
                        fy2[k] = fy1[k]; fy1[k] = yy;
                        y += yy * (k == 0 ? 1.0 : 0.6);
                    }
                    double env = Math.Min(1, t / 0.08) * Math.Exp(-Math.Max(0, t - dur * 0.5) / (dur * 0.4));
                    x[i] += y * env * 1.4;
                }
            }
            Fade(x, 0.003, 0.4);
            return x;
        }

        // ---------------- helpers ----------------

        static double Rand(ref uint r) { r = r * 1664525u + 1013904223u; return r / 4294967296.0; }

        static void Fade(double[] x, double inS, double outS)
        {
            int a = (int)(inS * Rate), b = (int)(outS * Rate), n = x.Length;
            for (int i = 0; i < a && i < n; i++) x[i] *= (double)i / a;
            for (int i = 0; i < b && i < n; i++) x[n - 1 - i] *= (double)i / b;
        }

        static float[] Normalise(double[] x, float peak)
        {
            double m = 1e-9;
            foreach (var v in x) m = Math.Max(m, Math.Abs(v));
            var o = new float[x.Length];
            for (int i = 0; i < x.Length; i++) o[i] = (float)(x[i] / m * peak);
            return o;
        }
    }
}
