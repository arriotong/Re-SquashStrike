// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/KitItems.cs
// UI redesign (U1): the kit's choices - what a player focuses and picks.
//
//   CardItem      a picture (a live 3D vignette, drawn art or a big
//                 glyph), a kicker, a title, a line, a stat badge;
//                 grows and glows when focused; locked / NEW states
//   SelectorItem  one option as a pill strip or pips (left / right)
//   CarouselItem  one value of a long list, big, between arrows
//   PrimaryItem   the screen's one big lime action (bottom right)
//
// They are MenuItems, so a screen's focus, mouse, sounds and tests
// work on them as on any row.
// ===========================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public enum CardSize { S, M, L, Hero }

    public sealed class CardItem : MenuItem
    {
        public readonly VisualElement Art = new VisualElement();
        readonly VisualElement _shade = new VisualElement(), _body = new VisualElement();
        readonly Label _kicker = new Label(), _title = new Label(), _line = new Label(), _badge = new Label(), _lock = new Label(), _new = new Label("NEW");
        readonly Action _run;
        Func<string> _badgeText, _lineText;
        public Func<bool> Locked = () => false;
        public Func<string> LockLine;
        public Func<bool> IsNew = () => false;
        /// <summary>Shown at all (a card that only makes sense sometimes: Continue career / Start a career).</summary>
        public Func<bool> Visible;

        public override bool WantsSlant { get { return false; } }

        public CardItem(string title, string kicker, Action run, CardSize size = CardSize.M) : base(title)
        {
            _run = run;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-card");
            Root.AddToClassList("k-card--" + size.ToString().ToLowerInvariant());
            LabelEl.style.display = DisplayStyle.None;
            Art.AddToClassList("k-card-art"); Art.pickingMode = PickingMode.Ignore;
            _shade.AddToClassList("k-card-shade"); _shade.pickingMode = PickingMode.Ignore;
            _body.AddToClassList("k-card-body"); _body.pickingMode = PickingMode.Ignore;
            _kicker.AddToClassList("k-card-kicker"); _kicker.text = kicker ?? "";
            _title.AddToClassList("k-card-title"); _title.text = title;
            _line.AddToClassList("k-card-line");
            _badge.AddToClassList("k-badge");
            _lock.AddToClassList("k-card-lock");
            _new.AddToClassList("k-new");
            _body.Add(_kicker); _body.Add(_title); _body.Add(_line); _body.Add(_lock);
            Root.Insert(0, Art); Root.Add(_shade); Root.Add(_body); Root.Add(_badge); Root.Add(_new);
            foreach (var l in new[] { _kicker, _title, _line, _badge, _lock, _new }) l.pickingMode = PickingMode.Ignore;
        }

        public CardItem Line(Func<string> line) { _lineText = line; return this; }
        public CardItem Line(string line) { _lineText = () => line; return this; }
        public CardItem Badge(Func<string> badge, bool lime = false) { _badgeText = badge; _badge.EnableInClassList("k-badge--lime", lime); return this; }
        /// <summary>Drawn art for the picture area (CardArt: "rings", "stripes", "split", "trophy", "trail").</summary>
        public CardItem Drawn(string kind, Color a, Color b, Color? highlight = null) { Art.Add(new CardArt(kind, a, b, highlight)); return this; }
        /// <summary>A live 3D picture of the court in the picture area (Vignettes: "you", "court", "front", "racket", "versus").</summary>
        public CardItem Vignette(string shot) { Vignettes.Attach(Art, shot); Root.AddToClassList("k-card--live"); return this; }
        /// <summary>A big glyph in the picture area (when there is no art).</summary>
        public CardItem Glyph(string glyph) { var g = new Label(glyph); g.AddToClassList("k-card-glyph"); g.pickingMode = PickingMode.Ignore; Art.Add(g); return this; }

        public bool IsLocked { get { return Locked != null && Locked(); } }
        public string Title { get { return _title.text; } }

        public override void Submit() { if (IsEnabled && !IsLocked && _run != null) _run(); }

        public override void Refresh()
        {
            base.Refresh();
            bool locked = IsLocked;
            if (Visible != null) Root.style.display = Visible() ? DisplayStyle.Flex : DisplayStyle.None;
            Root.EnableInClassList("k-card--locked", locked);
            _line.text = _lineText != null ? _lineText() : "";
            _line.style.display = string.IsNullOrEmpty(_line.text) ? DisplayStyle.None : DisplayStyle.Flex;
            string b = _badgeText != null ? _badgeText() : null;
            _badge.text = b ?? "";
            _badge.style.display = string.IsNullOrEmpty(b) ? DisplayStyle.None : DisplayStyle.Flex;
            _lock.text = locked && LockLine != null ? "LOCKED  ·  " + LockLine() : "";
            _lock.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
            _new.style.display = !locked && IsNew != null && IsNew() ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>One option as a strip of pills (or pips): left / right changes it, confirm steps it on.</summary>
    public sealed class SelectorItem : MenuItem
    {
        readonly string[] _values;
        readonly Func<int> _get;
        readonly Action<int> _set;
        readonly Label[] _pills;
        readonly Label _caption = new Label();
        readonly Func<string> _captionText;

        public override bool WantsSlant { get { return false; } }
        public override bool TakesHorizontal { get { return true; } }

        public SelectorItem(string label, string[] values, Func<int> get, Action<int> set, bool pips = false, Func<string> caption = null) : base(label)
        {
            _values = values; _get = get; _set = set; _captionText = caption;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-sel");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("k-sel-label");
            var strip = new VisualElement(); strip.AddToClassList("k-sel-strip");
            _pills = new Label[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                int k = i;
                var p = new Label(pips ? "" : values[i]); p.AddToClassList(pips ? "k-pip" : "k-pill");
                p.RegisterCallback<ClickEvent>(e => { if (IsEnabled) { _set(k); Refresh(); e.StopPropagation(); } });
                strip.Add(p);
                _pills[i] = p;
            }
            Root.Add(strip);
            _caption.AddToClassList("k-sel-caption");
            Root.Add(_caption);
        }

        public int Index { get { return _get(); } }
        public string Value { get { return _values[Mathf.Clamp(_get(), 0, _values.Length - 1)]; } }
        public override void Submit() { Change(1); }
        public override void Change(int d) { if (!IsEnabled) return; _set(OptionItem.Step(_get(), d, _values.Length)); Refresh(); }

        public override void Refresh()
        {
            base.Refresh();
            int cur = _get();
            for (int i = 0; i < _pills.Length; i++) { _pills[i].EnableInClassList("k-on", i == cur); _pills[i].EnableInClassList("k-past", i < cur); }
            string cap = _captionText != null ? _captionText() : (_pills.Length > 0 && _pills[0].ClassListContains("k-pip") ? Value : null);
            _caption.text = cap ?? "";
            _caption.style.display = string.IsNullOrEmpty(cap) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>One value from a long list, shown big between arrows (a nationality): left / right steps it.</summary>
    public sealed class CarouselItem : MenuItem
    {
        readonly string[] _values;
        readonly Func<int> _get;
        readonly Action<int> _set;
        readonly Label _value = new Label(), _count = new Label();

        public override bool WantsSlant { get { return false; } }
        public override bool TakesHorizontal { get { return true; } }

        public CarouselItem(string label, string[] values, Func<int> get, Action<int> set) : base(label)
        {
            _values = values; _get = get; _set = set;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-carousel");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("k-sel-label");
            var strip = new VisualElement(); strip.AddToClassList("k-car-strip");
            var left = new Label("◀"); left.AddToClassList("k-car-arrow");
            var right = new Label("▶"); right.AddToClassList("k-car-arrow");
            left.RegisterCallback<ClickEvent>(e => { Change(-1); e.StopPropagation(); });
            right.RegisterCallback<ClickEvent>(e => { Change(1); e.StopPropagation(); });
            _value.AddToClassList("k-car-value"); _count.AddToClassList("k-car-count");
            strip.Add(left); strip.Add(_value); strip.Add(right);
            Root.Add(strip); Root.Add(_count);
        }

        public string Value { get { return _values[Mathf.Clamp(_get(), 0, _values.Length - 1)]; } }
        public string[] Values { get { return _values; } }
        public int Index { get { return Mathf.Clamp(_get(), 0, _values.Length - 1); } }
        /// <summary>UI redesign (2026-10-28): more than this many values, and confirm opens the whole list (PickListScreen)
        /// instead of stepping one - no cycling through 50 courts.</summary>
        public const int ListFrom = 5;
        /// <summary>Opens the list (set by the FrontEnd; none: confirm steps, as before).</summary>
        public static Action<CarouselItem> ListOpener;
        public void Pick(int i) { if (!IsEnabled) return; _set(Mathf.Clamp(i, 0, _values.Length - 1)); Refresh(); }
        public override void Submit()
        {
            if (!IsEnabled) return;
            if (ListOpener != null && _values.Length > ListFrom) ListOpener(this);
            else Change(1);
        }
        public override void Change(int d) { if (!IsEnabled) return; _set(OptionItem.Step(_get(), d, _values.Length)); Refresh(); }
        public override void Refresh() { base.Refresh(); _value.text = Value; _count.text = (_get() + 1) + " / " + _values.Length; }
    }

    /// <summary>A plain action as a wide row (pause, full time, confirm): a label, an optional glyph; lime when focused.</summary>
    public sealed class ActionItem : MenuItem
    {
        readonly Action _run;
        public override bool WantsSlant { get { return false; } }
        public ActionItem(string label, Action run, string glyph = null) : base(label)
        {
            _run = run;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-action");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("k-action-label");
            if (glyph != null) { var g = new Label(glyph); g.AddToClassList("k-action-glyph"); g.pickingMode = PickingMode.Ignore; Root.Add(g); }
            if (label.StartsWith("Quit")) Root.AddToClassList("k-action--danger");
        }
        /// <summary>A chip with a button glyph that follows the device in use (Y on a pad, R on the keyboard).</summary>
        public ActionItem(string label, Action run, PromptBar.Btn button) : this(label, run, PromptBar.Glyph(button, MenuInput.UsingPad))
        {
            var g = Root.Q<Label>(className: "k-action-glyph");
            g.schedule.Execute(() => g.text = PromptBar.Glyph(button, MenuInput.UsingPad)).Every(150);
        }
        public override void Submit() { if (IsEnabled && _run != null) _run(); }
    }

    /// <summary>The screen's one primary action: the big lime button at the bottom right.</summary>
    public sealed class PrimaryItem : MenuItem
    {
        readonly Action _run;
        public override bool WantsSlant { get { return false; } }
        public PrimaryItem(string label, Action run) : base(label)
        {
            _run = run;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-primary");
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("k-primary-label");
            LabelEl.text = label.ToUpperInvariant() + "  ▸";
        }
        public override void Submit() { if (IsEnabled && _run != null) _run(); }
    }
}
