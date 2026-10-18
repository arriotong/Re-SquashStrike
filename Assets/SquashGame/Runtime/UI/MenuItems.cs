// ===========================================================
// Assets/SquashGame/Runtime/UI/MenuItems.cs
// P2-M1: the rows a menu screen is made of.
//
//   ButtonItem   a label (and an optional note); confirm runs it
//   OptionItem   label  < value >; left/right (or a click on an
//                arrow) changes it; confirm / click steps forward
//
// Rows are focused by the screen (pad / keyboard), or by the mouse
// pointer entering them. Disabled rows are skipped and greyed out.
// ===========================================================

using System;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public abstract class MenuItem
    {
        public readonly VisualElement Root = new VisualElement();
        protected readonly Label LabelEl = new Label();
        public string Name { get; private set; }
        public Func<bool> Enabled = () => true;
        public bool IsEnabled { get { return Enabled == null || Enabled(); } }

        /// <summary>Set by the screen: the pointer entered / clicked this row.</summary>
        internal Action<MenuItem> OnHover, OnClick;

        protected MenuItem(string label)
        {
            Name = label;
            LabelEl.text = label;
            LabelEl.AddToClassList("fe-item__label");
            Root.AddToClassList("fe-item");
            Root.Add(LabelEl);
            Root.RegisterCallback<PointerEnterEvent>(e => { if (IsEnabled && OnHover != null) OnHover(this); });
            Root.RegisterCallback<ClickEvent>(e => { if (IsEnabled && OnClick != null) OnClick(this); });
        }

        public void SetLabel(string text) { LabelEl.text = text; }
        public virtual void Submit() { }
        public virtual void Change(int d) { }
        public virtual void Refresh() { Root.EnableInClassList("fe-item--disabled", !IsEnabled); }
        public void SetFocused(bool on) { Root.EnableInClassList("fe-item--focused", on); }
    }

    public sealed class ButtonItem : MenuItem
    {
        readonly Action _run;
        readonly Label _note;
        readonly Func<string> _noteText;

        public ButtonItem(string label, Action run, bool primary = false, Func<string> note = null) : base(label)
        {
            _run = run;
            _noteText = note;
            if (primary) Root.AddToClassList("fe-item--primary");
            if (note != null)
            {
                _note = new Label();
                _note.AddToClassList("fe-item__note");
                Root.Add(_note);
            }
        }

        public override void Submit() { if (IsEnabled && _run != null) _run(); }

        public override void Refresh()
        {
            base.Refresh();
            if (_note != null) _note.text = _noteText();
        }
    }

    public sealed class OptionItem : MenuItem
    {
        readonly Func<string> _value;
        readonly Action<int> _change;
        readonly Label _valueEl = new Label();

        public OptionItem(string label, Func<string> value, Action<int> change) : base(label)
        {
            _value = value;
            _change = change;
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            var left = new Label("‹"); left.AddToClassList("fe-item__arrow");
            var right = new Label("›"); right.AddToClassList("fe-item__arrow");
            _valueEl.AddToClassList("fe-item__value");
            left.RegisterCallback<ClickEvent>(e => { if (IsEnabled) { Change(-1); e.StopPropagation(); } });
            right.RegisterCallback<ClickEvent>(e => { if (IsEnabled) { Change(1); e.StopPropagation(); } });
            box.Add(left); box.Add(_valueEl); box.Add(right);
            Root.Add(box);
        }

        public string Value { get { return _value(); } }
        public override void Submit() { Change(1); }
        public override void Change(int d) { if (!IsEnabled || _change == null) return; _change(d); Refresh(); }

        public override void Refresh()
        {
            base.Refresh();
            _valueEl.text = _value();
        }

        /// <summary>Step an index through n values (wrapping).</summary>
        public static int Step(int i, int d, int n) { return n <= 0 ? 0 : ((i + d) % n + n) % n; }
    }
}
