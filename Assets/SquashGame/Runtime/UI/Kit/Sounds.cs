// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/Sounds.cs
// UI redesign (U1): the menus' sounds, made in code like the court's
// (no audio assets): focus, tick, select, back, tab, count, star,
// stamp, level up. Quiet, short, and at the game's SFX volume.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.UI
{
    public static class Sounds
    {
        const int Rate = 44100;
        static AudioSource _src;
        static Dictionary<string, AudioClip> _clips;
        /// <summary>The last sound asked for (tests).</summary>
        public static string Last { get; private set; }
        public static float Volume = 0.45f;

        public static void Play(string name)
        {
            Last = name;
            if (_clips == null) Build();
            if (!_clips.TryGetValue(name, out var clip)) return;
            if (_src == null)
            {
                var go = new GameObject("UiSounds");
                Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.playOnAwake = false; _src.spatialBlend = 0f;
            }
            _src.PlayOneShot(clip, Volume * Audio.AudioDirector.SfxVolume);
        }

        static void Build()
        {
            _clips = new Dictionary<string, AudioClip>
            {
                { "focus", Clip("focus", Tone(1760, 0.028, 0.35)) },
                { "tick", Clip("tick", Tone(1320, 0.020, 0.30)) },
                { "select", Clip("select", Cat(Tone(660, 0.045, 0.5), Tone(990, 0.07, 0.5))) },
                { "back", Clip("back", Cat(Tone(880, 0.04, 0.45), Tone(587, 0.06, 0.45))) },
                { "tab", Clip("tab", Noise(0.06, 0.25)) },
                { "count", Clip("count", Tone(2400, 0.012, 0.18)) },
                { "star", Clip("star", Cat(Tone(880, 0.06, 0.45), Tone(1175, 0.06, 0.45), Tone(1568, 0.12, 0.5))) },
                { "stamp", Clip("stamp", Mix(Tone(92, 0.16, 0.9), Noise(0.08, 0.35))) },
                { "levelup", Clip("levelup", Cat(Tone(523, 0.08, 0.5), Tone(659, 0.08, 0.5), Tone(784, 0.08, 0.5), Tone(1047, 0.22, 0.55))) },
            };
        }

        static AudioClip Clip(string name, float[] s)
        {
            var c = AudioClip.Create("ui_" + name, s.Length, 1, Rate, false);
            c.SetData(s, 0);
            return c;
        }

        /// <summary>A sine with a quick attack and an exponential decay.</summary>
        static float[] Tone(double hz, double seconds, double amp)
        {
            int n = (int)(seconds * Rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double env = System.Math.Min(1.0, t / 0.003) * System.Math.Exp(-t / (seconds * 0.35));
                s[i] = (float)(amp * env * (System.Math.Sin(2 * System.Math.PI * hz * t) + 0.25 * System.Math.Sin(4 * System.Math.PI * hz * t)));
            }
            return s;
        }

        static float[] Noise(double seconds, double amp)
        {
            int n = (int)(seconds * Rate);
            var s = new float[n];
            uint r = 12345u; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                r = r * 1664525u + 1013904223u;
                float w = (r >> 8) / 16777216f * 2f - 1f;
                lp += (w - lp) * 0.25f;                                      // a soft, filtered swish
                double t = (double)i / Rate;
                s[i] = (float)(amp * lp * System.Math.Sin(System.Math.PI * t / seconds));
            }
            return s;
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
    }
}
