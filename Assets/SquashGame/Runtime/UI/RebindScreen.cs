// ===========================================================
// Assets/SquashGame/Runtime/UI/RebindScreen.cs
// Settings > Controls > Change keys: the actions you may rebind, for the
// keyboard and mouse and for the pad (two tabs). Pick one, press the new
// key or button (Esc cancels); saved in the profile at once. Moving and
// aiming stay on WASD / mouse and the sticks.
// ===========================================================

using SquashGame.Profile;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class RebindScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-tray"; } }
        public override bool Spatial { get { return true; } }
        protected override string Kicker { get { return "SETTINGS"; } }
        protected override string Title { get { return "Change keys"; } }
        protected override string Hint { get { return null; } }
        public override bool Typing { get { return IntentSource.Rebinding || base.Typing; } }

        readonly VisualElement[] _pages = new VisualElement[2];
        TabBar _tabs;
        Label _note;
        int _page;
        /// <summary>The action waiting for a key (tests), or null.</summary>
        public string Listening { get; private set; }

        public RebindScreen(int page = 0) { _page = page; }

        protected override void Build()
        {
            _tabs = new TabBar("KEYBOARD", "PAD");
            _tabs.Changed += Page;
            Content.Add(_tabs);
            for (int p = 0; p < 2; p++)
            {
                bool pad = p == 1;
                _pages[p] = Kit.Box(Content, "k-tray-page");
                for (int k = 0; k < IntentSource.Rebindable.Length; k++)
                {
                    string action = IntentSource.Rebindable[k], name = IntentSource.RebindableNames[k];
                    AddTo(_pages[p], new ActionItem(name + "   ·   " + IntentSource.BindingLabel(action, pad), () => Listen(action, name, pad), "✎"));
                }
                AddTo(_pages[p], new ActionItem("Back to the defaults", Reset, "↺"));
            }
            _note = Kit.L(Content, "Pick an action, then press the new key or button.  Esc cancels.", "k-set-hint");
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Change"), (PromptBar.Btn.Tabs, "Keyboard / pad"), (PromptBar.Btn.B, "Back"));
            Root.Add(prompts);
            _tabs.Select(_page);
            Page(_page);
        }

        void Page(int i)
        {
            _page = i;
            for (int k = 0; k < 2; k++) _pages[k].style.display = k == i ? DisplayStyle.Flex : DisplayStyle.None;
            Root.schedule.Execute(FocusFirstShown);
        }

        public override void OnTab(int d) { if (!IntentSource.Rebinding) _tabs.Step(d); }

        void Listen(string action, string name, bool pad)
        {
            Listening = action;
            _note.text = "Press the new " + (pad ? "button" : "key") + " for " + name + "...   Esc cancels";
            // start on the next frame, so the press that chose it is not taken as the new key
            Root.schedule.Execute(() => IntentSource.Rebind(action, pad, changed =>
            {
                Listening = null;
                if (changed) { ProfileStore.Current.Settings.Bindings = IntentSource.Overrides; ProfileStore.Save(); }
                int page = _page;
                Fe.Pop(); Fe.Push(new RebindScreen(page));                 // the labels, fresh
            })).StartingIn(120);
        }

        void Reset()
        {
            IntentSource.ResetBindings();
            ProfileStore.Current.Settings.Bindings = "";
            ProfileStore.Save();
            int page = _page;
            Fe.Pop(); Fe.Push(new RebindScreen(page));
        }

        public override void Back() { if (!IntentSource.Rebinding) Fe.Pop(); }
    }
}
