// ===========================================================
// Assets/SquashGame/Runtime/UI/ControlsScreen.cs
// 2026-11-23 Local Versus, the controls step (the user: "let user choose
// different input devices ... controllers or keyboards ... a list of all of
// their device's names ... they can use anything or even both using
// controllers"). Remade the same day (the user: "the things are not lined
// up ... i want it to be like a drop down one ... keep the left control and
// right control as a drop down of the full device ... if i click it senses
// as a selection input so find another way"):
//
//   left    each player, three dropdowns: the device (every controller and
//           keyboard by name), the keys (a keyboard whole, its left half or
//           its right half), the mouse (with a keyboard). Picking what the
//           other player has swaps them
//   right   every device once, in lined-up columns - a keyboard played in
//           halves shows its halves under it - each lit while it's being
//           shown: Shift on a keyboard, moving a mouse, a controller's right
//           stick (inputs the menus don't use)
//
// The list follows devices plugged in or out. The match reads the choices
// when it starts (MatchRunner.ChosenControls).
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

        static readonly KeyRegion[] Regions = { KeyRegion.Whole, KeyRegion.Left, KeyRegion.Right };
        static readonly string[] RegionNames = { "Whole keyboard", "Left half", "Right half" };

        List<InputSetup.Option> _devices, _mice;                     // controllers and whole keyboards; mice (the first: none)
        readonly List<(VisualElement row, InputSetup.Option o, VisualElement halves)> _rows = new List<(VisualElement, InputSetup.Option, VisualElement)>();
        readonly Label[] _keys = new Label[2];
        readonly CarouselItem[] _device = new CarouselItem[2], _region = new CarouselItem[2], _mouse = new CarouselItem[2];
        int _version, _pads;

        /// <summary>Tests: the rows (one a device), the ones lit, their names.</summary>
        public int Rows { get { return _rows.Count; } }
        public int RowsLit { get { int n = 0; foreach (var r in _rows) if (r.row.ClassListContains("k-dev-row--on")) n++; return n; } }
        public string LitNames { get { var l = new List<string>(); foreach (var r in _rows) if (r.row.ClassListContains("k-dev-row--on")) l.Add(r.o.Label); return string.Join(", ", l); } }
        public CarouselItem DeviceDropdown(int p) { return _device[p]; }
        public CarouselItem KeysDropdown(int p) { return _region[p]; }

        protected override void Compose()
        {
            var prim = InputSetup.Primaries();
            _devices = prim.FindAll(o => o.Kind == "pad" || o.Region == KeyRegion.Whole);
            if (_devices.Count == 0) _devices.Add(new InputSetup.Option { Id = "", Label = "Nothing plugged in", Kind = "none" });   // (every device unplugged)
            _mice = InputSetup.Mice();
            _version = RawInput.Version; _pads = Gamepad.all.Count;
            // a fresh start, or a choice that's gone: the usual pair
            if (InputSetup.Find(prim, C.VsControls1) == null || InputSetup.Find(prim, C.VsControls2) == null || InputSetup.Clash(C.VsControls1, C.VsControls2))
            {
                InputSetup.Defaults(out string a, out string am, out string b, out string bm);
                C.VsControls1 = a; C.VsMouse1 = am; C.VsControls2 = b; C.VsMouse2 = bm;
            }
            var wrap = Kit.Box(Content, "k-dev");
            var left = Kit.Box(wrap, "k-dev-left");
            string[] devNames = _devices.ConvertAll(o => o.Label).ToArray(), mouseNames = _mice.ConvertAll(o => o.Label).ToArray();
            for (int p = 0; p < 2; p++)
            {
                int me = p;
                Kit.L(left, "PLAYER " + (p + 1), "k-dev-player");
                _device[p] = AddTo(left, new CarouselItem("Device", devNames, () => Index(_devices, DeviceOf(Get(me))), v => SetDevice(me, _devices[v])));
                _region[p] = AddTo(left, new CarouselItem("Keys", RegionNames, () => System.Array.IndexOf(Regions, RegionOf(Get(me))), v => SetRegion(me, Regions[v])));
                _mouse[p] = AddTo(left, new CarouselItem("Mouse", mouseNames, () => Index(_mice, GetMouse(me)), v => SetMouse(me, _mice[v].Id)));
                foreach (var dd in new[] { _device[p], _region[p], _mouse[p] }) dd.Dropdown = true;
                _region[p].Enabled = () => IsKeyboard(Get(me));
                _mouse[p].Enabled = () => IsKeyboard(Get(me)) && _mice.Count > 1;
                _keys[p] = Kit.L(left, "", "k-dev-keys");
            }
            var right = Kit.Box(wrap, "k-dev-right");
            Kit.L(right, "YOUR DEVICES", "k-dev-head");
            Kit.L(right, InputSetup.IdentifyHint + (InputSetup.Separate ? "" : "  ·  every keyboard here works as one - split it in halves for two players"), "k-dev-hint");
            var list = new ScrollView(ScrollViewMode.Vertical); list.AddToClassList("k-dev-list"); right.Add(list);
            foreach (var o in _devices) if (o.Kind != "none") AddRow(list, o, o.Kind == "pad" ? "CONTROLLER" : "KEYBOARD");
            for (int i = 1; i < _mice.Count; i++) AddRow(list, _mice[i], "MOUSE");
            AddTo(Root, new PrimaryItem("Next: the court", () => SetupFlow.Next(Fe, this, true)));
            ShowKeys();
        }

        void AddRow(VisualElement into, InputSetup.Option o, string kind)
        {
            var row = Kit.Box(into, "k-dev-row");
            Kit.Box(row, "k-dev-dot");
            Kit.L(row, kind, "k-dev-kind");
            Kit.L(row, o.Label, "k-dev-name");
            var who = Kit.L(row, "", "k-dev-who"); who.name = "who";
            var halves = Kit.Box(into, "k-dev-halves");                   // (a keyboard played in halves: who has which)
            _rows.Add((row, o, halves));
        }

        static string Get(int p) { return p == 0 ? C.VsControls1 : C.VsControls2; }
        static string GetMouse(int p) { return p == 0 ? C.VsMouse1 : C.VsMouse2; }
        static void Put(int p, string id) { if (p == 0) C.VsControls1 = id; else C.VsControls2 = id; }
        static void PutMouse(int p, string id) { if (p == 0) C.VsMouse1 = id; else C.VsMouse2 = id; }
        static bool IsKeyboard(string id) { return id != null && id.StartsWith("kb:"); }
        /// <summary>"kb:&lt;path&gt;:left" -> the keyboard's whole id; a controller's own.</summary>
        static string DeviceOf(string id) { return IsKeyboard(id) ? id.Substring(0, id.LastIndexOf(':')) + ":whole" : id; }
        static KeyRegion RegionOf(string id)
        {
            if (!IsKeyboard(id)) return KeyRegion.Whole;
            return id.EndsWith(":left") ? KeyRegion.Left : id.EndsWith(":right") ? KeyRegion.Right : KeyRegion.Whole;
        }
        static string WithRegion(string deviceId, KeyRegion r) { return deviceId.Substring(0, deviceId.LastIndexOf(':')) + ":" + r.ToString().ToLowerInvariant(); }
        static int Index(List<InputSetup.Option> l, string id) { var o = InputSetup.Find(l, id); return o != null ? Mathf.Max(0, l.IndexOf(o)) : 0; }

        void SetDevice(int p, InputSetup.Option o)
        {
            if (o.Kind == "none") return;
            string id = o.Kind == "kb" ? WithRegion(o.Id, IsKeyboard(Get(p)) && DeviceOf(Get(p)) == o.Id ? RegionOf(Get(p)) : KeyRegion.Whole) : o.Id;
            // the same keyboard as the other player: split it rather than take it - they keep their half (on it whole: the left),
            // you get the other
            string theirs = Get(1 - p);
            if (o.Kind == "kb" && IsKeyboard(theirs) && DeviceOf(theirs) == o.Id)
            {
                var keep = RegionOf(theirs) == KeyRegion.Whole ? KeyRegion.Left : RegionOf(theirs);
                Put(1 - p, WithRegion(o.Id, keep));
                id = WithRegion(o.Id, keep == KeyRegion.Left ? KeyRegion.Right : KeyRegion.Left);
            }
            else if (InputSetup.Clash(id, theirs)) Put(1 - p, Get(p));                     // (their controller: they take yours)
            Put(p, id);
            Tidy(); Changed();
        }

        void SetRegion(int p, KeyRegion r)
        {
            string mine = Get(p);
            if (!IsKeyboard(mine)) return;
            string id = WithRegion(mine, r), theirs = Get(1 - p);
            // the other player on the same keyboard takes the other half (or, going whole, moves off it)
            if (InputSetup.Clash(id, theirs))
            {
                if (r == KeyRegion.Whole) { var other = FirstFree(id); if (other == null) return; Put(1 - p, other); }
                else Put(1 - p, WithRegion(mine, r == KeyRegion.Left ? KeyRegion.Right : KeyRegion.Left));
            }
            Put(p, id);
            Tidy(); Changed();
        }

        /// <summary>A device for the other player that doesn't clash with this choice (a controller first).</summary>
        string FirstFree(string mine)
        {
            foreach (var o in _devices) if (o.Kind == "pad" && !InputSetup.Clash(o.Id, mine)) return o.Id;
            foreach (var o in _devices) if (o.Kind == "kb" && !InputSetup.Clash(o.Id, mine)) return o.Id;
            return null;
        }

        void SetMouse(int p, string id)
        {
            if (!string.IsNullOrEmpty(id) && id == GetMouse(1 - p)) PutMouse(1 - p, "");        // (one mouse, one player)
            PutMouse(p, id);
            Changed();
        }

        static void Tidy()
        {
            for (int p = 0; p < 2; p++) if (!IsKeyboard(Get(p))) PutMouse(p, "");               // (a controller needs no mouse)
        }

        void Changed()
        {
            ProfileStore.Save();
            for (int p = 0; p < 2; p++) { _device[p].Refresh(); _region[p].Refresh(); _mouse[p].Refresh(); }
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
                r.row.EnableInClassList("k-dev-row--on", InputSetup.Active(r.o));
                // who has it: the whole device, a half, the mouse
                string who = "";
                string halves = "";
                for (int p = 0; p < 2; p++)
                {
                    string id = Get(p);
                    if (r.o.Kind == "ms") { if (GetMouse(p) == r.o.Id) who = "P" + (p + 1); continue; }
                    if (DeviceOf(id) != r.o.Id) continue;
                    if (RegionOf(id) == KeyRegion.Whole) who = "P" + (p + 1);
                    else halves += (halves.Length > 0 ? "     " : "") + (RegionOf(id) == KeyRegion.Left ? "left half" : "right half") + "  ·  P" + (p + 1);
                }
                var whoLabel = r.row.Q<Label>("who");
                if (whoLabel.text != who) whoLabel.text = who;
                r.row.EnableInClassList("k-dev-row--p1", who == "P1");
                r.row.EnableInClassList("k-dev-row--p2", who == "P2");
                if (halves.Length > 0)
                {
                    if (r.halves.childCount == 0) Kit.L(r.halves, "", "k-dev-half");
                    var l = (Label)r.halves[0]; if (l.text != halves) l.text = halves;
                }
                else if (r.halves.childCount > 0) r.halves.Clear();
            }
        }

        public override void OnHide() { ProfileStore.Save(); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Open the list"), (PromptBar.Btn.B, "Back")); }
    }
}
