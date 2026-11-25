// ===========================================================
// Assets/SquashGame/Runtime/Audio/GameSfx.cs
// 2026-11-25 (the user: "i want more variety of sound effects if
// possible"): the moments that had no sound of their own, made in code
// like the rest (no audio files): several takes of each, picked at random
// and pitched a little, so nothing repeats like a machine.
//
//   on court   swing (a racket's whoosh), step (shoes), dive (a thud and
//              a skid)
//   abilities  focus_in / focus_out (the world slowing, then back),
//              charge (the Special Shot's squat and jump), timestop,
//              cutin (the slash), duel_win, duel_lose
//   the ref    review (the video: a whirr and three beeps), overturn,
//              upheld
//   rewards    coin, flip (a card turned), reveal0..4 (a pull, by rarity:
//              Common's blip up to Legendary's fanfare), wheel_tick
//   walk-on    pop (confetti), firework (a boom and crackle)
//
// 2D (the menus' and the moments'), at the game's SFX volume; a few voices
// of their own so they can overlap.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Audio
{
    public static class GameSfx
    {
        const int Rate = 44100;
        const int Voices = 6;
        static readonly List<AudioSource> _pool = new List<AudioSource>();
        static int _next;
        static Dictionary<string, AudioClip[]> _clips;
        /// <summary>The last sound asked for and how many times each has played (tests).</summary>
        public static string Last { get; private set; }
        public static readonly Dictionary<string, int> Played = new Dictionary<string, int>();
        public static float Volume = 0.55f;

        public static readonly string[] Names =
        {
            "swing", "step", "dive", "focus_in", "focus_out", "charge", "timestop", "cutin", "duel_win", "duel_lose",
            "review", "overturn", "upheld", "coin", "flip", "reveal0", "reveal1", "reveal2", "reveal3", "reveal4", "wheel_tick",
            "pop", "firework",
        };

        public static int Takes(string name) { if (_clips == null) Build(); return _clips.TryGetValue(name, out var a) ? a.Length : 0; }

        /// <summary>Play one (a random take, pitched by +-<paramref name="vary"/>).</summary>
        public static void Play(string name, float volume = 1f, float vary = 0.05f)
        {
            Last = name;
            Played[name] = Played.TryGetValue(name, out int n) ? n + 1 : 1;
            if (_clips == null) Build();
            if (!_clips.TryGetValue(name, out var takes) || takes.Length == 0 || volume <= 0.01f) return;
            if (_pool.Count == 0 || _pool[0] == null)
            {
                _pool.Clear();
                var go = new GameObject("GameSfx");
                if (Application.isPlaying) Object.DontDestroyOnLoad(go);
                for (int i = 0; i < Voices; i++) { var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0f; _pool.Add(s); }
            }
            var src = _pool[_next]; _next = (_next + 1) % _pool.Count;
            src.clip = takes[Random.Range(0, takes.Length)];
            src.pitch = 1f + Random.Range(-vary, vary);
            src.volume = Mathf.Clamp01(volume) * Volume * AudioDirector.SfxVolume;
            src.Play();
        }

        // ---------------- the sounds ----------------

        static void Build()
        {
            _clips = new Dictionary<string, AudioClip[]>();
            Add("swing", 4, k => Whoosh(0.14 + 0.02 * k, 0.55, 0.06, 0.32, 101u + (uint)k));
            Add("step", 4, k => Mix(Thump(95 + 12 * k, 0.05, 0.55), Hiss(0.025, 0.18, 0.5, 202u + (uint)k)));
            Add("dive", 3, k => Mix(Thump(68 + 6 * k, 0.22, 0.9), Delay(Whoosh(0.32, 0.4, 0.05, 0.12, 303u + (uint)k), 0.04)));
            Add("focus_in", 2, k => Mix(Glide(950 - 60 * k, 160, 0.5, 0.35), Whoosh(0.5, 0.35, 0.3, 0.03, 404u + (uint)k)));
            Add("focus_out", 2, k => Mix(Glide(170, 900 + 60 * k, 0.28, 0.3), Whoosh(0.28, 0.3, 0.03, 0.3, 505u + (uint)k)));
            Add("charge", 2, k => Tremolo(Glide(220 + 20 * k, 1500, 0.6, 0.35), 18));
            Add("timestop", 1, k => Mix(Mix(Thump(52, 1.1, 0.9), Swell(0.45, 0.4, 606u)), Delay(Bell(new[] { 1990.0, 2987, 4130 }, 0.9, 0.22), 0.4)));
            Add("cutin", 3, k => Mix(Whoosh(0.12, 0.8, 0.4, 0.05, 707u + (uint)k), Delay(Thump(150 + 25 * k, 0.3, 0.8), 0.06)));
            Add("duel_win", 2, k => Mix(Bell(new[] { 1320.0 + 40 * k, 1980, 2970 }, 0.6, 0.45), Arp(new[] { 784.0, 1047, 1568 }, 0.06, 0.35)));
            Add("duel_lose", 2, k => Mix(Glide(320 - 20 * k, 80, 0.42, 0.5), Thump(70, 0.3, 0.6)));
            Add("review", 1, k => Cat(Tremolo(Glide(300, 620, 0.35, 0.2), 30), Arp(new[] { 1000.0, 1000, 1000 }, 0.09, 0.35)));
            Add("overturn", 1, k => Arp(new[] { 660.0, 990 }, 0.14, 0.45));
            Add("upheld", 1, k => Arp(new[] { 660.0, 440 }, 0.14, 0.45));
            Add("coin", 3, k => Cat(Bell(new[] { 2637.0 * (1 + 0.03 * k), 3951 }, 0.07, 0.4), Bell(new[] { 3136.0 * (1 + 0.03 * k), 4699 }, 0.28, 0.4)));
            Add("flip", 3, k => Whoosh(0.06, 0.6, 0.5, 0.1, 808u + (uint)k));
            Add("reveal0", 2, k => Arp(new[] { 660.0 + 20 * k, 880 }, 0.05, 0.3));
            Add("reveal1", 2, k => Arp(new[] { 660.0, 880 + 20 * k, 1047 }, 0.055, 0.32));
            Add("reveal2", 2, k => Mix(Arp(new[] { 784.0, 988, 1175 + 20 * k, 1568 }, 0.06, 0.34), Delay(Shimmer(0.4, 0.12, 909u + (uint)k), 0.15)));
            Add("reveal3", 2, k => Mix(Arp(new[] { 523.0, 659, 784, 1047, 1319 + 20 * k }, 0.065, 0.38), Delay(Shimmer(0.7, 0.18, 910u + (uint)k), 0.2)));
            Add("reveal4", 1, k => Mix(Mix(Arp(new[] { 523.0, 659, 784, 1047, 1319, 1568 }, 0.07, 0.4), Delay(Chord(new[] { 523.0, 659, 784, 1047 }, 1.2, 0.3), 0.42)),
                                       Delay(Shimmer(1.2, 0.2, 911u), 0.4)));
            Add("wheel_tick", 3, k => Bell(new[] { 2800.0 + 150 * k }, 0.025, 0.35));
            Add("pop", 3, k => Mix(Hiss(0.05, 0.6, 0.8, 1001u + (uint)k), Glide(900 + 100 * k, 300, 0.06, 0.3)));
            Add("firework", 3, k => Mix(Thump(60 + 8 * k, 0.5, 0.7), Delay(Crackle(0.7, 0.35, 1101u + (uint)k), 0.12)));
        }

        static void Add(string name, int takes, System.Func<int, float[]> make)
        {
            var a = new AudioClip[takes];
            for (int k = 0; k < takes; k++)
            {
                var s = Normalise(make(k), 0.9f);
                a[k] = AudioClip.Create("sfx_" + name + "_" + k, Mathf.Max(1, s.Length), 1, Rate, false);
                a[k].SetData(s, 0);
            }
            _clips[name] = a;
        }

        // ---------------- building blocks ----------------

        static float[] Buf(double seconds) { return new float[Mathf.Max(1, (int)(seconds * Rate))]; }
        static uint Rnd(ref uint r) { r = r * 1664525u + 1013904223u; return r; }
        static float White(ref uint r) { return (Rnd(ref r) >> 8) / 16777216f * 2f - 1f; }

        /// <summary>A low sine thump, quick attack, decaying.</summary>
        static float[] Thump(double hz, double seconds, double amp)
        {
            var s = Buf(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, env = System.Math.Min(1, t / 0.002) * System.Math.Exp(-t / (seconds * 0.3));
                double f = hz * (1 + 0.6 * System.Math.Exp(-t / 0.02));                     // a pitch drop on the hit
                s[i] = (float)(amp * env * System.Math.Sin(2 * System.Math.PI * f * t));
            }
            return s;
        }

        /// <summary>Filtered noise swept from one brightness to another under a bell envelope (a whoosh).</summary>
        static float[] Whoosh(double seconds, double amp, double bright0, double bright1, uint seed)
        {
            var s = Buf(seconds); uint r = seed; float lp = 0, lp2 = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double u = (double)i / s.Length;
                float a = (float)(bright0 + (bright1 - bright0) * u);
                lp += (White(ref r) - lp) * a; lp2 += (lp - lp2) * a;
                s[i] = (float)(amp * lp2 * 2.2 * System.Math.Sin(System.Math.PI * u));
            }
            return s;
        }

        static float[] Hiss(double seconds, double amp, double bright, uint seed)
        {
            var s = Buf(seconds); uint r = seed; float lp = 0;
            for (int i = 0; i < s.Length; i++)
            {
                lp += (White(ref r) - lp) * (float)bright;
                s[i] = (float)(amp * lp * System.Math.Exp(-(double)i / s.Length * 4));
            }
            return s;
        }

        /// <summary>Noise growing to a cut (a reversed cymbal).</summary>
        static float[] Swell(double seconds, double amp, uint seed)
        {
            var s = Buf(seconds); uint r = seed; float lp = 0;
            for (int i = 0; i < s.Length; i++) { lp += (White(ref r) - lp) * 0.5f; double u = (double)i / s.Length; s[i] = (float)(amp * lp * u * u); }
            return s;
        }

        static float[] Glide(double f0, double f1, double seconds, double amp)
        {
            var s = Buf(seconds); double ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double u = (double)i / s.Length, f = f0 * System.Math.Pow(f1 / f0, u);
                ph += 2 * System.Math.PI * f / Rate;
                double env = System.Math.Min(1, u * 30) * (1 - u * 0.7);
                s[i] = (float)(amp * env * (System.Math.Sin(ph) + 0.3 * System.Math.Sin(2 * ph)));
            }
            return s;
        }

        /// <summary>Struck metal: inharmonic partials ringing out.</summary>
        static float[] Bell(double[] hz, double seconds, double amp)
        {
            var s = Buf(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, env = System.Math.Min(1, t / 0.001) * System.Math.Exp(-t / (seconds * 0.3)), v = 0;
                for (int k = 0; k < hz.Length; k++) v += System.Math.Sin(2 * System.Math.PI * hz[k] * t) / (k + 1);
                s[i] = (float)(amp * env * v);
            }
            return s;
        }

        static float[] Arp(double[] hz, double each, double amp)
        {
            var parts = new List<float[]>();
            for (int i = 0; i < hz.Length; i++) parts.Add(Bell(new[] { hz[i], hz[i] * 2 }, i == hz.Length - 1 ? each * 3 : each, amp));
            return Cat(parts.ToArray());
        }

        static float[] Chord(double[] hz, double seconds, double amp) { return Bell(hz, seconds, amp); }

        /// <summary>A sparkle: quick random high pings.</summary>
        static float[] Shimmer(double seconds, double amp, uint seed)
        {
            var s = Buf(seconds); uint r = seed;
            for (int k = 0; k < (int)(seconds * 22); k++)
            {
                int at = (int)((Rnd(ref r) >> 8) % (uint)s.Length);
                double hz = 2500 + (Rnd(ref r) >> 8) % 3500;
                var b = Bell(new[] { hz }, 0.08, amp * (1 - (double)at / s.Length));
                for (int i = 0; i < b.Length && at + i < s.Length; i++) s[at + i] += b[i];
            }
            return s;
        }

        /// <summary>A firework's crackle: sparse sharp clicks thinning out.</summary>
        static float[] Crackle(double seconds, double amp, uint seed)
        {
            var s = Buf(seconds); uint r = seed;
            for (int k = 0; k < 60; k++)
            {
                double u = ((Rnd(ref r) >> 8) / 16777216.0); u = u * u;
                int at = (int)(u * (s.Length - 400));
                for (int i = 0; i < 300; i++) s[at + i] += (float)(amp * White(ref r) * System.Math.Exp(-i / 40.0) * (1 - u));
            }
            return s;
        }

        static float[] Tremolo(float[] s, double hz)
        {
            for (int i = 0; i < s.Length; i++) s[i] *= (float)(0.65 + 0.35 * System.Math.Sin(2 * System.Math.PI * hz * i / Rate));
            return s;
        }

        static float[] Delay(float[] s, double seconds)
        {
            int d = (int)(seconds * Rate);
            var o = new float[s.Length + d];
            s.CopyTo(o, d);
            return o;
        }

        static float[] Cat(params float[][] parts)
        {
            int n = 0; foreach (var p in parts) n += p.Length;
            var s = new float[n]; int k = 0;
            foreach (var p in parts) { p.CopyTo(s, k); k += p.Length; }
            return s;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var s = new float[System.Math.Max(a.Length, b.Length)];
            for (int i = 0; i < s.Length; i++) s[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
            return s;
        }

        static float[] Normalise(float[] s, float peak)
        {
            float m = 1e-6f; foreach (var x in s) m = Mathf.Max(m, Mathf.Abs(x));
            float k = peak / m;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            // (the last few ms fade out: no click at the end)
            int f = Mathf.Min(s.Length, 220);
            for (int i = 0; i < f; i++) s[s.Length - 1 - i] *= (float)i / f;
            return s;
        }
    }
}
