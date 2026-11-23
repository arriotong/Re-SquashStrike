// ===========================================================
// Assets/SquashGame/Runtime/UI/ControlsScreen.cs
// 2026-11-23 Local Versus, the controls step (the user: "let user choose
// different input devices ... controllers or keyboards ... a list of all of
// their device's names ... they can use anything or even both using
// controllers"; answers: a list, and pressing any button on a device lights
// its row):
//
//   left    each player: their controls (any controller, any keyboard -
//           whole or a half) and a mouse to go with a keyboard; picking one
//           the other player has swaps them
//   right   every device by name, each lit while it's being used ("press
//           any button to see which is which"), and each player's keys
//
// The list follows devices plugged in or out. Presentation only - the match
// reads the choices when it starts (MatchRunner.ChosenControls).
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class ControlsScreen : GameScreen
    {
        protected override string Kicker { get { return "LOCAL VERSUS  ·  STEP 2  ·  CONTROLS"; } }
        protected override string Title { get { return "Who plays with what"; } }
        protected override bool ShowChip { get { return false; } }
        public override MenuWorld Backdrop { get { return MenuWorld.Play; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }

        List<InputSetup.Option> _prim, _mice;
        readonly List<(VisualElement row, VisualElement dot, InputSetup.Option o)> _rows = new List<(VisualElement, VisualElement, InputSetup.Option)>();
        readonly Label[] _keys = new Label[2];
        readonly CarouselItem[] _pick = new CarouselItem[2], _mouse = new CarouselItem[2];
        int _version, _pads;

        /// <summary>The rows lit now (tests) and the choices.</summary>
        public int RowsLit { get { int n = 0; foreach (var r in _rows) if (r.row.ClassListContains("k-dev-row--on")) n++; return n; } }
        public int Rows { get { return _rows.Count; } }
        public string LitNames { get { var l = new List<string>(); foreach (var r in _rows) if (r.row.ClassListContains("k-dev-row--on")) l.Add(r.o.Label); return string.Join(", ", l); } }

        protected override void Compose()
        {
            _prim = InputSetup.Primaries(); _mice = InputSetup.Mice();
            if (_prim.Count == 0) _prim.Add(new InputSetup.Option { Id = "", Label = "Nothing plugged in", Kind = "none" });   // (every device unplugged)
            _version = RawInput.Version; _pads = Gamepad.all.Count;
            // a fresh start, or a choice that's gone: the usual pair
            if (InputSetup.Find(_prim, C.VsControls1) == null || InputSetup.Find(_prim, C.VsControls2) == null || InputSetup.Clash(C.VsControls1, C.VsControls2))
            {
                InputSetup.Defaults(out string a, out string am, out string b, out string bm);
                C.VsControls1 = a; C.VsMouse1 = am; C.VsControls2 = b; C.VsMouse2 = bm;
            }
            var wrap = Kit.Box(Content, "k-dev");
            var left = Kit.Box(wrap, "k-dev-left");
            string[] primNames = _prim.ConvertAll(o => o.Label).ToArray(), mouseNames = _mice.ConvertAll(o => o.Label).ToArray();
            for (int p = 0; p < 2; p++)
            {
                int me = p;
                Kit.L(left, "PLAYER " + (p + 1), "k-dev-player");
                _pick[p] = AddTo(left, new CarouselItem("Controls", primNames, () => Index(_prim, Get(me)), v => SetPrimary(me, _prim[v].Id)));
                _mouse[p] = AddTo(left, new CarouselItem("Mouse", mouseNames, () => Index(_mice, GetMouse(me)), v => SetMouse(me, _mice[v].Id)));
                _mouse[p].Enabled = () => IsKeyboard(Get(me)) && _mice.Count > 1;
                _keys[p] = Kit.L(left, "", "k-dev-keys");
            }
            var right = Kit.Box(wrap, "k-dev-right");
            Kit.L(right, "YOUR DEVICES", "k-dev-head");
            Kit.L(right, InputSetup.Separate ? "Press any button on a device to see which it is" :
                "Press any button to see which it is  ·  every keyboard here works as one - split it in two for two players", "k-dev-hint");
            foreach (var o in _prim) AddRow(right, o, o.Kind == "pad" ? "CONTROLLER" : o.Region == KeyRegion.Whole ? "KEYBOARD" : "HALF");
            for (int i = 1; i < _mice.Count; i++) AddRow(right, _mice[i], "MOUSE");
            AddTo(Root, new PrimaryItem("Next: the court", () => SetupFlow.Next(Fe, this, true)));
            ShowKeys();
        }

        void AddRow(VisualElement into, InputSetup.Option o, string kind)
        {
            var row = Kit.Box(into, "k-dev-row");
            var dot = Kit.Box(row, "k-dev-dot");
            Kit.L(row, kind, "k-dev-kind");
            Kit.L(row, o.Label, "k-dev-name");
            _rows.Add((row, dot, o));
        }

        static string Get(int p) { return p == 0 ? C.VsControls1 : C.VsControls2; }
        static string GetMouse(int p) { return p == 0 ? C.VsMouse1 : C.VsMouse2; }
        static int Index(List<InputSetup.Option> l, string id) { var o = InputSetup.Find(l, id); return o != null ? Mathf.Max(0, l.IndexOf(o)) : 0; }
        static bool IsKeyboard(string id) { return id != null && id.StartsWith("kb:"); }

        void SetPrimary(int p, string id)
        {
            string mine = Get(p), theirs = Get(1 - p);
            // the other player has it (or the same keyboard whole and in part): they take yours
            if (InputSetup.Clash(id, theirs)) { if (p == 0) C.VsControls2 = mine; else C.VsControls1 = mine; }
            if (p == 0) C.VsControls1 = id; else C.VsControls2 = id;
            if (!IsKeyboard(id)) { if (p == 0) C.VsMouse1 = ""; else C.VsMouse2 = ""; }
            if (!IsKeyboard(Get(1 - p))) { if (p == 0) C.VsMouse2 = ""; else C.VsMouse1 = ""; }
            Changed();
        }

        void SetMouse(int p, string id)
        {
            if (!string.IsNullOrEmpty(id) && id == GetMouse(1 - p)) { if (p == 0) C.VsMouse2 = ""; else C.VsMouse1 = ""; }   // (one mouse, one player)
            if (p == 0) C.VsMouse1 = id; else C.VsMouse2 = id;
            Changed();
        }

        void Changed()
        {
            ProfileStore.Save();
            for (int p = 0; p < 2; p++) { _pick[p].Refresh(); _mouse[p].Refresh(); }
            ShowKeys();
        }

        void ShowKeys()
        {
            for (int p = 0; p < 2; p++)
            {
                var c = InputSetup.Resolve(Get(p), GetMouse(p));
                _keys[p].text = c.Layout != null ? c.Layout.Summary : c.Devices.Length > 0 ? "Left stick moves  ·  right stick aims  ·  A hits  ·  RT focus  ·  D-pad up special" : "";
            }
        }

        public override void Tick(float dt)
        {
            // devices plugged in or out: the lists again
            if (RawInput.Version != _version || Gamepad.all.Count != _pads)
            {
                var fe = Fe;
                fe.Pop(); fe.Push(new ControlsScreen());
                return;
            }
            foreach (var r in _rows)
            {
                bool on = InputSetup.Active(r.o);
                r.row.EnableInClassList("k-dev-row--on", on);
                // a row taken by a player says who
                string who = r.o.Id == C.VsControls1 || (r.o.Id != "" && r.o.Id == C.VsMouse1) ? "P1" : r.o.Id == C.VsControls2 || (r.o.Id != "" && r.o.Id == C.VsMouse2) ? "P2" : "";
                r.row.EnableInClassList("k-dev-row--p1", who == "P1");
                r.row.EnableInClassList("k-dev-row--p2", who == "P2");
            }
        }

        public override void OnHide() { ProfileStore.Save(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Pick from the list"), (PromptBar.Btn.B, "Back")); }
    }
}
