// ===========================================================
// Assets/SquashGame/Runtime/UI/MenuScreen.cs
// P2-M1: one screen of the front end - a heading and a column of
// rows (MenuItems), navigated by MenuInput or the mouse. Screens live
// on FrontEnd's stack; only the top one shows and takes input.
// ===========================================================

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public abstract class MenuScreen
    {
        public FrontEnd Fe { get; private set; }
        public readonly VisualElement Root = new VisualElement();
        protected readonly VisualElement Content = new VisualElement();
        protected readonly List<MenuItem> Items = new List<MenuItem>();
        readonly Label _hint = new Label();
        int _focus = -1;

        /// <summary>Centred panel (pause, post-match) instead of the left column.</summary>
        protected virtual bool Centred { get { return false; } }
        /// <summary>An extra style class for the screen's root (a layout of its own: the title, the main menu).</summary>
        protected virtual string RootClass { get { return null; } }
        /// <summary>The dark sidebar behind the column (off for the title).</summary>
        protected virtual bool Sidebar { get { return !Centred; } }
        /// <summary>Dim the court behind this screen.</summary>
        public virtual bool Dims { get { return false; } }
        /// <summary>UI redesign: focus moves in 2D to the nearest choice in the pressed direction (cards, grids).</summary>
        public virtual bool Spatial { get { return false; } }
        /// <summary>LB / RB (Q / E): change tab. Screens with tabs override.</summary>
        public virtual void OnTab(int d) { }
        /// <summary>Pad X / F, pad Y / R: a screen's extra actions.</summary>
        public virtual void OnActionX() { }
        public virtual void OnActionY() { }
        /// <summary>Esc belongs to the game here (it un-pauses), so the menu ignores it.</summary>
        public virtual bool EscapeIsGame { get { return false; } }
        protected virtual string Kicker { get { return null; } }
        protected abstract string Title { get; }
        protected virtual string Hint { get { return "↑↓ choose    ←→ change    Enter / A  select    Esc / B  back"; } }

        public void Create(FrontEnd fe)
        {
            Fe = fe;
            Root.AddToClassList("fe-screen");
            if (Centred) Root.AddToClassList("fe-screen--centre");
            if (Sidebar) Root.style.backgroundImage = new StyleBackground(Palette.SidebarFade());   // the old game's sidebar: dark, fading out
            if (RootClass != null) Root.AddToClassList(RootClass);
            if (Kicker != null) { var k = new Label(Kicker); k.AddToClassList("fe-kicker"); Root.Add(k); }
            var t = new Label(Title); t.AddToClassList("fe-title"); Root.Add(t);
            Root.Add(Content);
            Build();
            _hint.AddToClassList("fe-hint");
            _hint.text = Hint;
            if (!Centred && Hint != null) Root.Add(_hint);
        }

        protected abstract void Build();

        protected T Add<T>(T item) where T : MenuItem
        {
            item.OnHover = it => Focus(it);
            item.OnClick = it => { Focus(it); it.Submit(); };
            if (item.Name != null && item.Name.StartsWith("Quit")) item.Root.AddToClassList("fe-item--danger");
            if (!Centred && item.WantsSlant) item.UseSlant();
            Items.Add(item);
            Content.Add(item.Root);
            return item;
        }

        protected void Section(string text)
        {
            var l = new Label(text.ToUpperInvariant());
            l.AddToClassList("fe-section");
            Content.Add(l);
        }

        protected Label Text(string text, string cls = "fe-subtitle")
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            Content.Add(l);
            return l;
        }

        public MenuItem Focused { get { return _focus >= 0 && _focus < Items.Count ? Items[_focus] : null; } }

        /// <summary>The row with this label (tests).</summary>
        public MenuItem Find(string label)
        {
            foreach (var i in Items) if (i.Name == label) return i;
            return null;
        }

        public void Focus(MenuItem item)
        {
            int idx = Items.IndexOf(item);
            if (idx < 0 || !item.IsEnabled) return;
            if (Focused != null) Focused.SetFocused(false);
            _focus = idx;
            item.SetFocused(true);
        }

        void FocusFirst()
        {
            if (Focused != null && Focused.IsEnabled) { Focused.SetFocused(true); return; }
            _focus = -1;
            for (int i = 0; i < Items.Count; i++) if (Items[i].IsEnabled) { Focus(Items[i]); return; }
        }

        public void Navigate(int d)
        {
            if (Items.Count == 0 || d == 0) return;
            int i = _focus < 0 ? (d > 0 ? -1 : Items.Count) : _focus;
            for (int k = 0; k < Items.Count; k++)
            {
                i = ((i + d) % Items.Count + Items.Count) % Items.Count;
                if (Items[i].IsEnabled) { Focus(Items[i]); return; }
            }
        }

        public void RefreshAll()
        {
            foreach (var i in Items) i.Refresh();
            FocusFirst();
        }

        public virtual void OnShow() { RefreshAll(); }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }
        public virtual void Back() { Fe.Pop(); }

        /// <summary>A text field has the keyboard (the menu's keys are left alone).</summary>
        public virtual bool Typing { get { foreach (var i in Items) if (i is TextItem t && t.Typing) return true; return false; } }

        /// <summary>Add a choice somewhere other than the content column (a card row, a grid, a corner).</summary>
        protected T AddTo<T>(VisualElement parent, T item) where T : MenuItem
        {
            Add(item);
            item.Root.RemoveFromHierarchy();
            parent.Add(item.Root);
            return item;
        }

        /// <summary>Shown on screen: in a panel, and no hidden ancestor (an inactive tab page).</summary>
        static bool Shown(VisualElement e)
        {
            if (e.panel == null) return false;
            for (var p = e; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>The choices a player can reach now (enabled and shown).</summary>
        public List<MenuItem> Reachable()
        {
            var l = new List<MenuItem>();
            foreach (var i in Items) if (i.IsEnabled && Shown(i.Root)) l.Add(i);
            return l;
        }

        /// <summary>Spatial navigation: the nearest reachable choice in direction (dx, dy) from the focused one.</summary>
        public void Move(int dx, int dy)
        {
            var cur = Focused;
            if (cur == null || !Shown(cur.Root)) { FocusFirstShown(); return; }
            var a = cur.Root.worldBound.center;
            MenuItem best = null; float bestScore = float.MaxValue;
            foreach (var it in Reachable())
            {
                if (it == cur) continue;
                var b = it.Root.worldBound.center;
                float along = dx != 0 ? (b.x - a.x) * dx : (b.y - a.y) * dy;
                float across = dx != 0 ? System.Math.Abs(b.y - a.y) : System.Math.Abs(b.x - a.x);
                if (along <= 4f) continue;
                float score = along + 2.2f * across;
                if (score < bestScore) { bestScore = score; best = it; }
            }
            if (best != null) { Focus(best); Sounds.Play("focus"); }
        }

        /// <summary>Up / down on a spatial screen, before the focus moves: true if the screen took it (the main menu's rail).</summary>
        public virtual bool OnVertical(int dy) { return false; }


        public void FocusFirstShown()
        {
            foreach (var it in Reachable()) { Focus(it); return; }
        }

        public void HandleInput(MenuInput input)
        {
            if (input.Tab != 0) { OnTab(input.Tab); Sounds.Play("tab"); }
            if (input.ActionX) OnActionX();
            if (input.ActionY) OnActionY();
            if (Spatial && !Typing)
            {
                if (input.Vertical != 0 && !OnVertical(input.Vertical)) Move(0, input.Vertical);
                if (input.Horizontal != 0)
                {
                    if (Focused != null && Focused.TakesHorizontal) { Focused.Change(input.Horizontal); Sounds.Play("tick"); }
                    else Move(input.Horizontal, 0);
                }
                if (input.Submit && Focused != null) { Sounds.Play("select"); Focused.Submit(); }
                if (input.Cancel && !(input.CancelFromEscape && EscapeIsGame)) { Sounds.Play("back"); Back(); }
                return;
            }
            if (Typing)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                bool done = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
                foreach (var pad in UnityEngine.InputSystem.Gamepad.all) done |= pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
                if (done)
                    foreach (var i in Items) if (i is TextItem t) t.StopTyping();
                return;
            }
            if (input.Vertical != 0) Navigate(input.Vertical);
            if (input.Horizontal != 0 && Focused != null) Focused.Change(input.Horizontal);
            if (input.Submit && Focused != null) Focused.Submit();
            if (input.Cancel && !(input.CancelFromEscape && EscapeIsGame)) Back();
        }
    }
}
