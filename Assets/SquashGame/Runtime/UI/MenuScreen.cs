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
            if (!Centred) item.UseSlant();
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

        public void HandleInput(MenuInput input)
        {
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
