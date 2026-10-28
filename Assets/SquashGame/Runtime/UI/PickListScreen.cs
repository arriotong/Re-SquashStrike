// ===========================================================
// Assets/SquashGame/Runtime/UI/PickListScreen.cs
// UI redesign (2026-10-28): "make everything straight forward, least
// that the player has to click as possible ... if I want a specific court
// in quick play I would have to cycle through like 50 different courts,
// maybe make it a pop up vertical list so players can scroll through".
//
// Every carousel with more than a few values (CarouselItem.ListFrom):
// confirm (Enter / A, or a click) opens this - the whole list over the
// screen, the current value focused and ticked. Up / down (or the wheel)
// scrolls, confirm picks it and closes, back closes. Left / right on the
// carousel itself still steps one at a time.
// ===========================================================

using System;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class PickListScreen : MenuScreen
    {
        readonly string _title;
        readonly string[] _values;
        readonly int _current;
        readonly Action<int> _pick;
        ScrollView _scroll;
        VisualElement _back;
        MenuItem _shown;

        public PickListScreen(string title, string[] values, int current, Action<int> pick)
        {
            _title = title; _values = values; _current = current; _pick = pick;
        }

        protected override string Title { get { return _title; } }
        protected override string RootClass { get { return "k-pick"; } }
        protected override bool Sidebar { get { return false; } }
        protected override string Hint { get { return null; } }
        public override bool Overlay { get { return true; } }
        /// <summary>The rows, top to bottom (tests).</summary>
        public int Count { get { return _values.Length; } }

        protected override void Build()
        {
            // the screen below stays in view, dimmed; a click on it closes the list (it never reaches the screen's rows)
            _back = new VisualElement(); _back.AddToClassList("k-pick-backdrop");
            _back.RegisterCallback<ClickEvent>(e => { Back(); e.StopPropagation(); });
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("k-pick-scroll");
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Content.Add(_scroll);
            for (int i = 0; i < _values.Length; i++)
            {
                int k = i;
                var row = AddTo(_scroll, new ActionItem(_values[i], () => Choose(k), i == _current ? "✓" : null));
                if (i == _current) row.Root.AddToClassList("k-pick-row--now");
            }
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--confirm");
            prompts.Set((PromptBar.Btn.Move, "Scroll"), (PromptBar.Btn.A, "Pick"), (PromptBar.Btn.B, "Back"));
            Root.Add(prompts);
        }

        public override void OnHide() { base.OnHide(); _back.RemoveFromHierarchy(); }

        void Choose(int i)
        {
            Fe.Pop();
            _pick(i);
        }

        /// <summary>Pick the row with this text (tests).</summary>
        public void Pick(string value) { int i = Array.IndexOf(_values, value); if (i >= 0) Choose(i); }

        public override void OnShow()
        {
            base.OnShow();
            // the backdrop goes just under the panel (a child would be clipped to it)
            var layer = Root.parent;
            if (layer != null && _back.parent == null) layer.Insert(layer.IndexOf(Root), _back);
            var items = Reachable();
            if (_current >= 0 && _current < items.Count) Focus(items[_current]);
        }

        public override void Tick(float dt)
        {
            if (Focused == null || Focused == _shown) return;
            _shown = Focused;
            _scroll.ScrollTo(_shown.Root);
        }
    }
}
