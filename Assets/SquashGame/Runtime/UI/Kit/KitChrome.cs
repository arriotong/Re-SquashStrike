// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/KitChrome.cs
// UI redesign (U1): what frames a screen.
//
//   TabBar      LB / RB (Q / E) tabs with a lime underline
//   PromptBar   bottom left: the actions and their glyphs for the
//               device in use (pad buttons or keys), swapped live
//   PlayerChip  top right: banner colour, name, title, a level ring
//   Ring        the drawn level / progress ring (Painter2D)
//   Kit.Panel   a charcoal panel with a lime edge; Kit.Label helpers
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class Kit
    {
        public static Label L(VisualElement parent, string text, string cls)
        {
            var l = new Label(text); l.AddToClassList(cls); l.pickingMode = PickingMode.Ignore;
            if (parent != null) parent.Add(l);
            return l;
        }

        public static VisualElement Box(VisualElement parent, string cls)
        {
            var v = new VisualElement(); v.AddToClassList(cls); v.pickingMode = PickingMode.Ignore;
            if (parent != null) parent.Add(v);
            return v;
        }

        /// <summary>A charcoal panel with a title label (lime, spaced), for widgets.</summary>
        public static VisualElement Panel(VisualElement parent, string title = null)
        {
            var p = Box(parent, "k-panel");
            if (title != null) L(p, title, "k-panel-title");
            return p;
        }
    }

    public sealed class TabBar : VisualElement
    {
        readonly List<Label> _tabs = new List<Label>();
        readonly Label _l = new Label("LB"), _r = new Label("RB");
        public int Index { get; private set; }
        public event Action<int> Changed;
        public int Count { get { return _tabs.Count; } }

        public TabBar(params string[] names)
        {
            AddToClassList("k-tabs");
            _l.AddToClassList("k-tab-glyph"); Add(_l);
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                var t = new Label(names[i]); t.AddToClassList("k-tab");
                t.RegisterCallback<ClickEvent>(e => Select(k));
                Add(t); _tabs.Add(t);
            }
            _r.AddToClassList("k-tab-glyph"); Add(_r);
            Select(0, false);
            schedule.Execute(() => { bool pad = MenuInput.UsingPad; _l.text = pad ? "LB" : "Q"; _r.text = pad ? "RB" : "E"; }).Every(200);
        }

        public void Step(int d) { Select(((Index + d) % _tabs.Count + _tabs.Count) % _tabs.Count); }

        public void Select(int i, bool notify = true)
        {
            Index = Mathf.Clamp(i, 0, _tabs.Count - 1);
            for (int k = 0; k < _tabs.Count; k++) _tabs[k].EnableInClassList("k-tab--on", k == Index);
            if (notify) Changed?.Invoke(Index);
        }

        public string Current { get { return _tabs[Index].text; } }
    }

    /// <summary>The action legend: "A Select · B Back · LB/RB Tabs" with the glyphs of the device in use.</summary>
    public sealed class PromptBar : VisualElement
    {
        public enum Btn { A, B, X, Y, Tabs, Start, Move }
        readonly List<(Btn btn, string text)> _items = new List<(Btn, string)>();
        bool _pad;

        public PromptBar()
        {
            AddToClassList("k-prompts");
            pickingMode = PickingMode.Ignore;
            schedule.Execute(() => { if (_pad != MenuInput.UsingPad) Rebuild(); }).Every(150);
        }

        public PromptBar Item(Btn b, string text) { _items.Add((b, text)); Rebuild(); return this; }
        public void Set(params (Btn, string)[] items) { _items.Clear(); _items.AddRange(items); Rebuild(); }

        /// <summary>The glyph for a button on the device in use (tests read it).</summary>
        public static string Glyph(Btn b, bool pad)
        {
            switch (b)
            {
                case Btn.A: return pad ? "A" : "Enter";
                case Btn.B: return pad ? "B" : "Esc";
                case Btn.X: return pad ? "X" : "F";
                case Btn.Y: return pad ? "Y" : "R";
                case Btn.Tabs: return pad ? "LB  RB" : "Q  E";
                case Btn.Start: return pad ? "☰" : "M";
                default: return pad ? "◀▶" : "Arrows";
            }
        }

        /// <summary>What the bar shows now (tests).</summary>
        public string Text { get { var s = ""; foreach (var c in Children()) if (c is VisualElement v) foreach (var l in v.Query<Label>().ToList()) s += l.text + " "; return s.Trim(); } }

        void Rebuild()
        {
            _pad = MenuInput.UsingPad;
            Clear();
            foreach (var (b, text) in _items)
            {
                var item = Kit.Box(this, "k-prompt");
                var g = Kit.L(item, Glyph(b, _pad), "k-glyph");
                if (_pad && (b == Btn.A || b == Btn.B || b == Btn.X || b == Btn.Y)) g.AddToClassList("k-glyph--" + b.ToString().ToLowerInvariant());
                else g.AddToClassList("k-glyph--key");
                Kit.L(item, text, "k-prompt-text");
            }
        }
    }

    /// <summary>A drawn ring: a track and a lime arc for a fraction (level progress, a timer).</summary>
    public sealed class Ring : VisualElement
    {
        float _value;
        public Color Track = new Color(1f, 1f, 1f, 0.12f), Fill = new Color32(200, 255, 61, 255);
        public float Width = 4f;
        public float Value { get { return _value; } set { _value = Mathf.Clamp01(value); MarkDirtyRepaint(); } }

        public Ring()
        {
            AddToClassList("k-ring");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - Width;
            if (rad <= 1) return;
            var c = r.center;
            var p = mgc.painter2D;
            p.lineWidth = Width; p.lineCap = LineCap.Round;
            p.strokeColor = Track; p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (_value > 0.001f)
            {
                p.strokeColor = Fill; p.BeginPath(); p.Arc(c, rad, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * _value)); p.Stroke();
            }
        }
    }

    /// <summary>Top right on the hubs: the banner colour, name, title, level ring.</summary>
    public sealed class PlayerChip : VisualElement
    {
        readonly Ring _ring = new Ring();
        readonly Label _level, _name, _title, _next;

        public PlayerChip()
        {
            AddToClassList("k-chip");
            pickingMode = PickingMode.Ignore;
            var ringBox = Kit.Box(this, "k-chip-ringbox");
            ringBox.Add(_ring);
            _level = Kit.L(ringBox, "", "k-chip-level");
            var text = Kit.Box(this, "k-chip-text");
            _title = Kit.L(text, "", "k-chip-title");
            _name = Kit.L(text, "", "k-chip-name");
            _next = Kit.L(text, "", "k-chip-next");          // P3-R1: what the next level brings
            Refresh();
        }

        public string NameShown { get { return _name.text; } }

        public void Refresh()
        {
            var p = ProfileStore.Current.Progress;
            _ring.Value = Rewards.Into(p.Xp);
            _level.text = Rewards.LevelOf(p.Xp).ToString();
            var t = Cosmetics.Find(p.Loadout.Title);
            var on = ProfileStore.Current.Online;                                   // P2-M8: the ranked rating, once you have one
            _title.text = (t != null ? t.Name : "Rookie").ToUpperInvariant() + (on != null && on.Games > 0 ? "  ·  " + on.Rating : "");
            _name.text = CareerStore.Current != null ? CareerStore.Current.S.Name : "Player";
            int next = Rewards.NextUnlock(p.Xp, out var items, out int toGo);
            _next.text = next > 0 && items.Count > 0 ? "NEXT  " + items[0].Name.ToUpperInvariant() + (items.Count > 1 ? " +" + (items.Count - 1) : "") + "  ·  " + toGo.ToString("N0") + " XP" : "";
            _next.style.display = next > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var banner = Cosmetics.Find(p.Loadout.Banner);
            if (banner != null) style.borderLeftColor = banner.B;
        }
    }
}
