// ===========================================================
// Assets/SquashGame/Runtime/UI/WalkOnFx.cs
// 2026-11-24 the WALK-ON machine's effects, over the screen while your
// walk-on card shows: confetti, sparks, smoke, spotlights, fireworks
// (Machines.WalkOnFx), in the walk-on's two colours. UI only - a few dozen
// small boxes moved each frame; nothing in the sim.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class WalkOnFx : VisualElement
    {
        sealed class Bit { public VisualElement E; public Vector2 P, V; public float Life, Age, Size, Spin, Rot; }

        readonly List<Bit> _bits = new List<Bit>();
        int _style;
        Color _a, _b;
        float _t, _next;
        uint _seed = 12345;

        /// <summary>The effect playing (0: none) - tests.</summary>
        public int Playing { get { return _style; } }

        public WalkOnFx()
        {
            AddToClassList("hud-wofx");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.overflow = Overflow.Hidden;
        }

        float R() { _seed = _seed * 1664525u + 1013904223u; return (_seed >> 8) / 16777216f; }
        float W { get { float w = resolvedStyle.width; return float.IsNaN(w) || w < 10 ? 1920 : w; } }
        float Hh { get { float h = resolvedStyle.height; return float.IsNaN(h) || h < 10 ? 1080 : h; } }

        public void Play(int fx, Color a, Color b)
        {
            if (fx == _style) return;
            Stop();
            _style = fx; _a = a; _b = b; _t = 0; _next = 0;
            if (fx == 4) for (int i = 0; i < 3; i++) Spot(i);
        }

        public void Stop()
        {
            foreach (var x in _bits) x.E.RemoveFromHierarchy();
            _bits.Clear();
            _style = 0;
        }

        Bit Add(float size, Color c, bool round)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.width = size; e.style.height = size;
            e.style.backgroundColor = c;
            if (round) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = size * 0.5f; }
            Add(e);
            var bit = new Bit { E = e, Size = size };
            _bits.Add(bit);
            return bit;
        }

        Color Pick() { float u = R(); return u < 0.42f ? _a : u < 0.84f ? _b : Color.white; }

        void Spot(int i)
        {
            var bit = Add(1, new Color(_a.r, _a.g, _a.b, 0.16f), false);
            bit.E.style.width = W * 0.09f; bit.E.style.height = Hh * 1.6f;
            bit.P = new Vector2(W * (0.2f + 0.3f * i), -Hh * 0.2f);
            bit.Spin = 0.6f + 0.25f * i; bit.Rot = i * 2f; bit.Life = 999;
            bit.E.style.transformOrigin = new TransformOrigin(Length.Percent(50), 0);
        }

        public void Step(float dt)
        {
            if (_style == 0) return;
            _t += dt;
            float w = W, h = Hh;
            // spawn
            switch (_style)
            {
                case 1:                                                                     // confetti: down from the top
                    while (_next <= _t && _bits.Count < 70) { var b = Add(8 + R() * 8, Pick(), false); b.P = new Vector2(R() * w, -20); b.V = new Vector2((R() - 0.5f) * 120, 160 + R() * 160); b.Life = 6; b.Spin = (R() - 0.5f) * 720; _next += 0.035f; }
                    break;
                case 2:                                                                     // sparks: up from the floor
                    while (_next <= _t && _bits.Count < 60) { var b = Add(4 + R() * 4, Pick(), true); b.P = new Vector2(w * (0.15f + R() * 0.7f), h + 4); b.V = new Vector2((R() - 0.5f) * 240, -500 - R() * 500); b.Life = 1.4f; _next += 0.03f; }
                    break;
                case 3:                                                                     // smoke: big soft puffs, drifting up
                    while (_next <= _t && _bits.Count < 16) { var g = 0.55f + R() * 0.3f; var b = Add(160 + R() * 160, new Color(g, g, g, 0.22f), true); b.P = new Vector2(R() * w, h - 40); b.V = new Vector2((R() - 0.5f) * 40, -40 - R() * 50); b.Life = 4.5f; _next += 0.22f; }
                    break;
                case 5:                                                                     // fireworks: a burst every so often
                    if (_next <= _t)
                    {
                        var at = new Vector2(w * (0.15f + R() * 0.7f), h * (0.12f + R() * 0.3f));
                        var c = Pick();
                        for (int k = 0; k < 26; k++) { float ang = k / 26f * Mathf.PI * 2; var b = Add(6, c, true); b.P = at; b.V = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (220 + R() * 90); b.Life = 1.3f; }
                        _next = _t + 0.55f + R() * 0.3f;
                    }
                    break;
            }
            // move
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                b.Age += dt;
                if (b.Age >= b.Life) { b.E.RemoveFromHierarchy(); _bits.RemoveAt(i); continue; }
                float fade = 1f;
                switch (_style)
                {
                    case 1: b.V.x += Mathf.Sin(b.Age * 3 + i) * 60 * dt; b.Rot += b.Spin * dt; break;
                    case 2: b.V.y += 900 * dt; fade = 1 - b.Age / b.Life; break;
                    case 3: b.Size += 40 * dt; b.E.style.width = b.Size; b.E.style.height = b.Size; fade = Mathf.Sin(b.Age / b.Life * Mathf.PI); break;
                    case 4: b.Rot = Mathf.Sin(_t * b.Spin + b.Rot * 0.0f + i) * 28f; break;
                    case 5: b.V *= 1 - 1.6f * dt; b.V.y += 140 * dt; fade = 1 - b.Age / b.Life; break;
                }
                b.P += b.V * dt;
                b.E.style.left = b.P.x - b.Size * 0.5f; b.E.style.top = b.P.y - b.Size * 0.5f;
                if (_style == 4) { b.E.style.left = b.P.x; b.E.style.top = b.P.y; }
                b.E.style.rotate = new Rotate(b.Rot);
                b.E.style.opacity = fade;
            }
        }
    }
}
