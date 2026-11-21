// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/Juice.cs
// 2026-11-21 the polish pass (the user: "pop out numbers, a zoom in
// celebration, little fades ... all those little things"): the small
// motions every screen shares, in one place so Reduce Motion (Settings,
// Accessibility) turns them down together.
//
//   On / Calm     motion at all (off in batch runs: captures stay still);
//                 the big moves (shake, zooms, flashes) - off with Reduce
//                 motion
//   Cascade       a screen's cards and rows drop in one after another
//   Bump          a number or chip bumps (a count landing, a coin in)
//   FlyCoins      coin icons fly from one element to another
//   Burst         a ring and sparks out of an element (a level up)
//   Dust          puffs under a stamp as it lands
//   Rumble        the gamepad's motors, for a moment (Settings: rumble)
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class Juice
    {
        /// <summary>Settings: fewer big moves (no shake, zooms, flashes, cascades), and rumble on or off.</summary>
        public static bool ReduceMotion, RumbleOn = true;

        /// <summary>Any motion (screens animate: not in batch runs).</summary>
        public static bool On { get { return FrontEnd.AnimateScreens; } }
        /// <summary>The big moves: shake, zooms, flashes, confetti.</summary>
        public static bool Big { get { return On && !ReduceMotion; } }

        // ---------------- cascades ----------------

        /// <summary>The screen's cards, then its rows, drop in one after another.</summary>
        public static void Cascade(VisualElement root)
        {
            if (!On || ReduceMotion || root == null) return;
            var items = new List<VisualElement>();
            root.Query(className: "k-card").ForEach(e => items.Add(e));
            if (items.Count == 0) root.Query(className: "fe-item").ForEach(e => { if (items.Count < 14) items.Add(e); });
            for (int i = 0; i < items.Count && i < 14; i++)
            {
                var e = items[i];
                e.AddToClassList("jc-pre");
                int at = 30 + i * 45;
                e.schedule.Execute(() => e.RemoveFromClassList("jc-pre")).StartingIn(at);
            }
        }

        /// <summary>A quick bump (scale up and back).</summary>
        public static void Bump(VisualElement e)
        {
            if (!On || e == null) return;
            e.AddToClassList("jc-bump");
            e.schedule.Execute(() => e.RemoveFromClassList("jc-bump")).StartingIn(120);
        }

        /// <summary>A tab's page fades in.</summary>
        public static void FadeIn(VisualElement e)
        {
            if (!On || e == null) return;
            e.AddToClassList("jc-fade-pre");
            e.schedule.Execute(() => e.RemoveFromClassList("jc-fade-pre")).StartingIn(16);
        }

        // ---------------- coins and bursts ----------------

        static VisualElement Layer(VisualElement any)
        {
            var p = any; while (p.parent != null) p = p.parent;
            return p;
        }

        /// <summary>Coin icons fly from one element into another; each one landing bumps it and ticks.</summary>
        public static void FlyCoins(VisualElement from, VisualElement to, int count, System.Action landed = null)
        {
            if (from == null || to == null) { landed?.Invoke(); return; }
            if (!On || from.panel == null) { landed?.Invoke(); return; }
            var layer = Layer(from);
            Vector2 a = layer.WorldToLocal(from.worldBound.center), b = layer.WorldToLocal(to.worldBound.center);
            int n = Mathf.Clamp(count, 1, 12);
            int left = n;
            for (int i = 0; i < n; i++)
            {
                var c = new VisualElement();
                c.AddToClassList("jc-coin");
                c.pickingMode = PickingMode.Ignore;
                c.style.position = Position.Absolute;
                layer.Add(c);
                float delay = i * 0.06f, t = -delay;
                Vector2 ctrl = (a + b) * 0.5f + new Vector2(Random.Range(-80f, 80f), -140f - Random.Range(0f, 80f));
                Vector2 jitter = new Vector2(Random.Range(-18f, 18f), Random.Range(-10f, 10f));
                c.style.left = a.x; c.style.top = a.y; c.style.opacity = 0;
                c.schedule.Execute(() =>
                {
                    t += 0.016f;
                    float u = Mathf.Clamp01(t / 0.62f);
                    if (t < 0) return;
                    float e = u * u * (3f - 2f * u);
                    Vector2 p = (1 - e) * (1 - e) * (a + jitter) + 2 * (1 - e) * e * ctrl + e * e * b;
                    c.style.left = p.x - 11; c.style.top = p.y - 11;
                    c.style.opacity = u < 0.9f ? 1f : (1f - u) * 10f;
                    if (u >= 1f && c.parent != null)
                    {
                        c.RemoveFromHierarchy();
                        Bump(to);
                        Sounds.Play("count");
                        if (--left == 0) landed?.Invoke();
                    }
                }).Every(16).Until(() => c.parent == null);
            }
        }

        /// <summary>A ring and sparks bursting out of an element (a level up, a star).</summary>
        public static void Burst(VisualElement at, Color colour, int sparks = 12)
        {
            if (!On || at == null || at.panel == null) return;
            var layer = Layer(at);
            Vector2 c = layer.WorldToLocal(at.worldBound.center);
            var ring = new VisualElement(); ring.AddToClassList("jc-ring"); ring.pickingMode = PickingMode.Ignore;
            ring.style.position = Position.Absolute; ring.style.borderTopColor = ring.style.borderBottomColor = ring.style.borderLeftColor = ring.style.borderRightColor = colour;
            layer.Add(ring);
            float t = 0;
            ring.schedule.Execute(() =>
            {
                t += 0.016f;
                float u = Mathf.Clamp01(t / 0.55f), r = Mathf.Lerp(10f, 120f, 1f - (1f - u) * (1f - u));
                ring.style.left = c.x - r; ring.style.top = c.y - r; ring.style.width = r * 2; ring.style.height = r * 2;
                ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = r;
                ring.style.opacity = 1f - u;
                if (u >= 1f) ring.RemoveFromHierarchy();
            }).Every(16).Until(() => ring.parent == null);
            if (ReduceMotion) return;
            for (int i = 0; i < sparks; i++)
            {
                var s = new VisualElement(); s.AddToClassList("jc-spark"); s.pickingMode = PickingMode.Ignore;
                s.style.position = Position.Absolute; s.style.backgroundColor = colour;
                layer.Add(s);
                float ang = (i + Random.value * 0.5f) / sparks * Mathf.PI * 2f, sp = Random.Range(140f, 260f), st = 0;
                s.schedule.Execute(() =>
                {
                    st += 0.016f;
                    float u = Mathf.Clamp01(st / 0.6f), d = sp * (st - 0.5f * st * st * 1.2f);
                    s.style.left = c.x + Mathf.Cos(ang) * d - 4; s.style.top = c.y + Mathf.Sin(ang) * d - 4 + 60f * st * st;
                    s.style.opacity = 1f - u;
                    if (u >= 1f) s.RemoveFromHierarchy();
                }).Every(16).Until(() => s.parent == null);
            }
        }

        /// <summary>Dust puffs out under an element (a stamp landing).</summary>
        public static void Dust(VisualElement at)
        {
            if (!Big || at == null || at.panel == null) return;
            var layer = Layer(at);
            var r = at.worldBound;
            for (int i = 0; i < 9; i++)
            {
                var d = new VisualElement(); d.AddToClassList("jc-dust"); d.pickingMode = PickingMode.Ignore;
                d.style.position = Position.Absolute;
                layer.Add(d);
                Vector2 o = layer.WorldToLocal(new Vector2(Mathf.Lerp(r.xMin, r.xMax, i / 8f), r.yMax - 6));
                float dir = (i / 8f - 0.5f) * 2f, t = 0;
                d.schedule.Execute(() =>
                {
                    t += 0.016f;
                    float u = Mathf.Clamp01(t / 0.5f), size = Mathf.Lerp(8f, 26f, u);
                    d.style.left = o.x + dir * 50f * u - size / 2; d.style.top = o.y - 10f * u - size / 2;
                    d.style.width = size; d.style.height = size;
                    d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = size / 2;
                    d.style.opacity = 0.55f * (1f - u);
                    if (u >= 1f) d.RemoveFromHierarchy();
                }).Every(16).Until(() => d.parent == null);
            }
        }

        /// <summary>A shake of an element (a stamp's thump).</summary>
        public static void Shake(VisualElement e, float px = 6f, float seconds = 0.25f)
        {
            if (!Big || e == null) return;
            float t = 0;
            e.schedule.Execute(() =>
            {
                t += 0.016f;
                float k = 1f - Mathf.Clamp01(t / seconds);
                e.style.translate = k > 0 ? new Translate(Mathf.Sin(t * 90f) * px * k, Mathf.Cos(t * 70f) * px * 0.5f * k) : new Translate(0, 0);
            }).Every(16).Until(() => t >= seconds);
        }

        // ---------------- rumble ----------------

        static float _rumbleLeft;

        /// <summary>The gamepad's motors for a moment (low: the heavy motor, high: the light one).</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            if (!RumbleOn || !Application.isPlaying || Application.isBatchMode) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            _rumbleLeft = Mathf.Max(_rumbleLeft, seconds);
        }

        /// <summary>Each frame (FrontEnd): the rumble stops when its time is up.</summary>
        public static void Tick(float dt)
        {
            if (_rumbleLeft <= 0) return;
            _rumbleLeft -= dt;
            if (_rumbleLeft <= 0) { var pad = Gamepad.current; if (pad != null) pad.SetMotorSpeeds(0, 0); }
        }

        public static void StopRumble() { _rumbleLeft = 0; var pad = Gamepad.current; if (pad != null) pad.SetMotorSpeeds(0, 0); }
    }
}
