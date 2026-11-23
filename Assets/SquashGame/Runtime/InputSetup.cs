// ===========================================================
// Assets/SquashGame/Runtime/InputSetup.cs
// 2026-11-23 (the user: "external device connection on the local versus ...
// let user choose different input devices ... controllers or keyboards ...
// a list of all of their device's names ... dont make it like player 1 needs
// to use keyboard or player 2 needs to use controller, they can use anything
// or even both using controllers"; answers: separate keyboards by name AND
// one keyboard split in halves; keyboard-only players get a full key
// layout; mice told apart too; a list where pressing a device lights it):
//
//   a player's controls  a controller, or a keyboard - whole, its left half
//                        or its right half - with a mouse or without
//   the keyboards        each physical one by name where Windows tells them
//                        apart (RawInput, the built game); else the one
//                        merged keyboard, still splittable in two
//   the layouts          keyboard + mouse: the usual keys, the mouse aims and
//                        hits; a whole keyboard alone: WASD, arrows aim,
//                        Space hits; the halves: WASD + TFGH / arrows + IJKL
//
// Choices are strings in the profile ("pad:<n>:<name>", "kb:<path>:whole",
// "ms:<path>", "" none) and resolved when a match starts.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SquashGame
{
    public enum KeyRegion { Whole, Left, Right }

    /// <summary>Which keys do what, for a keyboard player read key by key (not through the Input System's actions).</summary>
    public sealed class KeyLayout
    {
        public string Name;
        public Key Up, Down, Left, Right, AimUp, AimDown, AimLeft, AimRight;
        public Key Hit, Focus, Special, Boast, Sprint, Appeal, Challenge, Pause, Ready;
        /// <summary>The mouse aims and hits (left button), asks for a let (right).</summary>
        public bool MouseAims;
        public string Summary;

        public static readonly KeyLayout KeyboardMouse = new KeyLayout
        {
            Name = "Keyboard + mouse", MouseAims = true, Up = Key.W, Down = Key.S, Left = Key.A, Right = Key.D,
            Focus = Key.Space, Special = Key.Q, Boast = Key.E, Sprint = Key.LeftShift, Appeal = Key.R, Challenge = Key.F, Pause = Key.Escape, Ready = Key.Enter,
            Summary = "WASD move  ·  mouse aims, left button hits  ·  Space focus  ·  Q special  ·  E boast  ·  Shift sprint",
        };
        public static readonly KeyLayout KeysOnly = new KeyLayout
        {
            Name = "Keyboard", Up = Key.W, Down = Key.S, Left = Key.A, Right = Key.D, AimUp = Key.UpArrow, AimDown = Key.DownArrow, AimLeft = Key.LeftArrow, AimRight = Key.RightArrow,
            Hit = Key.Space, Focus = Key.LeftShift, Special = Key.Q, Boast = Key.E, Sprint = Key.LeftCtrl, Appeal = Key.R, Challenge = Key.F, Pause = Key.Escape, Ready = Key.Enter,
            Summary = "WASD move  ·  arrows aim  ·  Space hits  ·  Shift focus  ·  Q special  ·  E boast  ·  Ctrl sprint",
        };
        public static readonly KeyLayout LeftHalf = new KeyLayout
        {
            Name = "Left half", Up = Key.W, Down = Key.S, Left = Key.A, Right = Key.D, AimUp = Key.T, AimDown = Key.G, AimLeft = Key.F, AimRight = Key.H,
            Hit = Key.Space, Focus = Key.LeftShift, Special = Key.Q, Boast = Key.E, Sprint = Key.LeftCtrl, Appeal = Key.R, Challenge = Key.Z, Pause = Key.Escape, Ready = Key.R,
            Summary = "WASD move  ·  TFGH aim  ·  Space hits  ·  L-Shift focus  ·  Q special  ·  E boast  ·  R let",
        };
        public static readonly KeyLayout RightHalf = new KeyLayout
        {
            Name = "Right half", Up = Key.UpArrow, Down = Key.DownArrow, Left = Key.LeftArrow, Right = Key.RightArrow, AimUp = Key.I, AimDown = Key.K, AimLeft = Key.J, AimRight = Key.L,
            Hit = Key.Enter, Focus = Key.RightShift, Special = Key.U, Boast = Key.O, Sprint = Key.RightCtrl, Appeal = Key.P, Challenge = Key.M, Pause = Key.Backspace, Ready = Key.P,
            Summary = "Arrows move  ·  IJKL aim  ·  Enter hits  ·  R-Shift focus  ·  U special  ·  O boast  ·  P let",
        };

        /// <summary>A half's keys with the mouse doing the aiming and hitting.</summary>
        public KeyLayout WithMouse()
        {
            if (MouseAims) return this;
            if (this == KeysOnly) return KeyboardMouse;
            var l = (KeyLayout)MemberwiseClone();
            l.MouseAims = true; l.AimUp = l.AimDown = l.AimLeft = l.AimRight = Key.None;
            l.Name = Name + " + mouse";
            l.Summary = Summary.Replace(this == LeftHalf ? "TFGH aim  ·  Space hits" : "IJKL aim  ·  Enter hits", "mouse aims and hits");
            return l;
        }

        public static KeyLayout For(KeyRegion r, bool mouse)
        {
            var l = r == KeyRegion.Left ? LeftHalf : r == KeyRegion.Right ? RightHalf : KeysOnly;
            return mouse ? l.WithMouse() : l;
        }

        public IEnumerable<Key> All()
        {
            foreach (var k in new[] { Up, Down, Left, Right, AimUp, AimDown, AimLeft, AimRight, Hit, Focus, Special, Boast, Sprint, Appeal, Challenge, Pause, Ready })
                if (k != Key.None) yield return k;
        }
    }

    /// <summary>Reads a keyboard key by key: the merged one (the Input System), or one physical keyboard (RawInput).</summary>
    public interface IKeyReader { bool Down(Key k); }
    /// <summary>Reads a mouse: the movement since the last read (+y up) and its buttons.</summary>
    public interface IMouseReader { Vector2 TakeDelta(); bool Left { get; } bool Right { get; } }

    public sealed class MergedKeys : IKeyReader
    {
        public bool Down(Key k) { var kb = Keyboard.current; return kb != null && k != Key.None && kb[k].isPressed; }
    }
    public sealed class RawKeys : IKeyReader
    {
        public readonly RawInput.Device Device;
        public RawKeys(RawInput.Device d) { Device = d; }
        public bool Down(Key k) { return RawInput.KeyDown(Device, InputSetup.VirtualKey(k)); }
    }
    public sealed class MergedMouse : IMouseReader
    {
        public Vector2 TakeDelta() { var m = Mouse.current; return m != null ? m.delta.ReadValue() : Vector2.zero; }
        public bool Left { get { var m = Mouse.current; return m != null && m.leftButton.isPressed; } }
        public bool Right { get { var m = Mouse.current; return m != null && m.rightButton.isPressed; } }
    }
    public sealed class RawMouse : IMouseReader
    {
        public readonly RawInput.Device Device;
        public RawMouse(RawInput.Device d) { Device = d; }
        public Vector2 TakeDelta() { return RawInput.TakeDelta(Device); }
        public bool Left { get { return (RawInput.Buttons(Device) & 1) != 0; } }
        public bool Right { get { return (RawInput.Buttons(Device) & 2) != 0; } }
    }

    /// <summary>One player's controls, resolved: the Input System devices its actions listen to (a controller), and/or the
    /// keys and mouse read directly with their layout.</summary>
    public sealed class PlayerControls
    {
        public InputDevice[] Devices = new InputDevice[0];
        public IKeyReader Keys;
        public IMouseReader Mouse;
        public KeyLayout Layout;
        public string Label = "";
        public bool Lost;
    }

    public static class InputSetup
    {
        public sealed class Option
        {
            public string Id, Label, Kind;     // Kind: "pad", "kb", "ms", "none"
            public InputDevice Unity;           // a pad (or the merged keyboard / mouse)
            public RawInput.Device Raw;         // a physical keyboard / mouse (the built Windows game)
            public KeyRegion Region;
        }

        /// <summary>Physical keyboards and mice are told apart (else: the merged keyboard, split in two if need be).</summary>
        public static bool Separate { get { return RawInput.Available && RawInput.Keyboards.Count > 0; } }

        /// <summary>What a player can play with: every controller, every keyboard whole and in halves.</summary>
        public static List<Option> Primaries()
        {
            var l = new List<Option>();
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                l.Add(new Option { Id = "pad:" + i + ":" + pads[i].displayName, Label = "Controller " + (i + 1) + "  ·  " + pads[i].displayName, Kind = "pad", Unity = pads[i] });
            if (Separate)
            {
                foreach (var d in ByUse(RawInput.Keyboards))
                    foreach (KeyRegion r in new[] { KeyRegion.Whole, KeyRegion.Left, KeyRegion.Right })
                        l.Add(new Option { Id = "kb:" + d.Path + ":" + r.ToString().ToLowerInvariant(), Label = d.Name + RegionWords(r), Kind = "kb", Raw = d, Region = r });
            }
            else if (Keyboard.current != null)
                foreach (KeyRegion r in new[] { KeyRegion.Whole, KeyRegion.Left, KeyRegion.Right })
                    l.Add(new Option { Id = "kb:merged:" + r.ToString().ToLowerInvariant(), Label = "Keyboard" + RegionWords(r), Kind = "kb", Unity = Keyboard.current, Region = r });
            return l;
        }

        /// <summary>The mice to go with a keyboard (the first: none).</summary>
        public static List<Option> Mice()
        {
            var l = new List<Option> { new Option { Id = "", Label = "No mouse", Kind = "none" } };
            if (RawInput.Available && RawInput.Mice.Count > 0)
                foreach (var d in ByUse(RawInput.Mice)) l.Add(new Option { Id = "ms:" + d.Path, Label = d.Name, Kind = "ms", Raw = d });
            else if (UnityEngine.InputSystem.Mouse.current != null)
                l.Add(new Option { Id = "ms:merged", Label = "Mouse", Kind = "ms", Unity = UnityEngine.InputSystem.Mouse.current });
            return l;
        }

        /// <summary>The ones used this session first (press a key on yours and it comes to the top), then the rest as listed.</summary>
        static List<RawInput.Device> ByUse(List<RawInput.Device> l)
        {
            var used = l.FindAll(d => d.LastActive > 0f); var rest = l.FindAll(d => d.LastActive <= 0f);
            used.Sort((a, b) => b.LastActive.CompareTo(a.LastActive));
            used.AddRange(rest);
            return used;
        }

        static string RegionWords(KeyRegion r) { return r == KeyRegion.Left ? "  ·  left half" : r == KeyRegion.Right ? "  ·  right half" : ""; }

        public static Option Find(List<Option> l, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var o in l) if (o.Id == id) return o;
            // a controller by its place in the list may have moved: the same name will do
            if (id.StartsWith("pad:"))
            {
                string name = id.Substring(id.IndexOf(':', 4) + 1);
                foreach (var o in l) if (o.Kind == "pad" && o.Id.EndsWith(":" + name)) return o;
            }
            return null;
        }

        /// <summary>Two choices that can't both be had (the same controller; the same keyboard whole and in part; the same mouse).</summary>
        public static bool Clash(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            if (a.StartsWith("kb:") && b.StartsWith("kb:"))
            {
                string da = a.Substring(0, a.LastIndexOf(':')), db = b.Substring(0, b.LastIndexOf(':'));
                return da == db && (a.EndsWith(":whole") || b.EndsWith(":whole"));
            }
            return false;
        }

        /// <summary>The usual pair: two controllers - one each; one - the keyboard and mouse against it; none - the keyboard split.</summary>
        public static void Defaults(out string p1, out string m1, out string p2, out string m2)
        {
            var prim = Primaries(); var mice = Mice();
            var pads = prim.FindAll(o => o.Kind == "pad"); var kbs = prim.FindAll(o => o.Kind == "kb");
            string mouse = mice.Count > 1 ? mice[1].Id : "";
            m1 = ""; m2 = "";
            if (pads.Count >= 2) { p1 = pads[0].Id; p2 = pads[1].Id; }
            else if (pads.Count == 1 && kbs.Count > 0) { p1 = kbs[0].Id; m1 = mouse; p2 = pads[0].Id; }
            else if (kbs.Count >= 3 && Separate && RawInput.Keyboards.Count >= 2) { p1 = kbs[0].Id; m1 = mouse; p2 = kbs[3].Id; }
            else if (kbs.Count >= 3) { p1 = kbs[1].Id; p2 = kbs[2].Id; }          // one keyboard: its two halves
            else { p1 = pads.Count > 0 ? pads[0].Id : ""; p2 = ""; }
        }

        /// <summary>Two players can play now (two choices that don't clash - with any keyboard, its halves).</summary>
        public static bool CanPlayTwo { get { var p = Primaries(); for (int i = 0; i < p.Count; i++) for (int j = i + 1; j < p.Count; j++) if (!Clash(p[i].Id, p[j].Id)) return true; return false; } }

        /// <summary>A choice made live.</summary>
        public static PlayerControls Resolve(string primary, string mouse)
        {
            var c = new PlayerControls();
            var o = Find(Primaries(), primary);
            if (o == null) { c.Lost = !string.IsNullOrEmpty(primary); c.Label = "nothing"; return c; }
            c.Label = o.Label;
            if (o.Kind == "pad") { c.Devices = new[] { o.Unity }; return c; }
            var m = Find(Mice(), mouse);
            bool hasMouse = m != null && m.Kind == "ms";
            c.Keys = o.Raw != null ? (IKeyReader)new RawKeys(o.Raw) : new MergedKeys();
            if (hasMouse) { c.Mouse = m.Raw != null ? (IMouseReader)new RawMouse(m.Raw) : new MergedMouse(); c.Label += "  +  " + m.Label; }
            c.Layout = KeyLayout.For(o.Region, hasMouse);
            if (!string.IsNullOrEmpty(mouse) && m == null) c.Lost = true;
            return c;
        }

        /// <summary>2026-11-23 (the user: "if i click it senses as a selection input so find another way"): the device behind a choice is
        /// being shown - inputs the menus don't use: Shift on a keyboard, moving a mouse, a controller's right stick or a trigger.</summary>
        public static bool Active(Option o)
        {
            if (o == null) return false;
            if (o.Raw != null)
            {
                if (o.Raw.IsKeyboard) { var k = new RawKeys(o.Raw); return k.Down(Key.LeftShift) || k.Down(Key.RightShift); }
                return Time.realtimeSinceStartup - o.Raw.LastMoved < 0.35f;
            }
            if (o.Unity is Gamepad g)
                return g.rightStick.ReadValue().sqrMagnitude > 0.25f || g.leftTrigger.ReadValue() > 0.5f || g.rightTrigger.ReadValue() > 0.5f;
            if (o.Unity is Keyboard kb) return kb.shiftKey.isPressed;
            if (o.Unity is Mouse ms) return ms.delta.ReadValue().sqrMagnitude > 4f;
            return false;
        }
        public const string IdentifyHint = "To see which is which: tap Shift on a keyboard, move a mouse, or move a controller's right stick";

        static bool AnyInRegion(IKeyReader keys, KeyRegion r)
        {
            foreach (var k in (r == KeyRegion.Left ? KeyLayout.LeftHalf : KeyLayout.RightHalf).All()) if (keys.Down(k)) return true;
            return false;
        }

        /// <summary>Windows' virtual-key code for a key (the ones the layouts use).</summary>
        public static int VirtualKey(Key k)
        {
            if (k >= Key.A && k <= Key.Z) return 0x41 + (k - Key.A);
            if (k >= Key.Digit1 && k <= Key.Digit9) return 0x31 + (k - Key.Digit1);
            switch (k)
            {
                case Key.Digit0: return 0x30;
                case Key.Space: return 0x20; case Key.Enter: case Key.NumpadEnter: return 0x0D; case Key.Escape: return 0x1B; case Key.Tab: return 0x09; case Key.Backspace: return 0x08;
                case Key.LeftArrow: return 0x25; case Key.UpArrow: return 0x26; case Key.RightArrow: return 0x27; case Key.DownArrow: return 0x28;
                case Key.LeftShift: return 0xA0; case Key.RightShift: return 0xA1; case Key.LeftCtrl: return 0xA2; case Key.RightCtrl: return 0xA3;
                case Key.LeftAlt: return 0xA4; case Key.RightAlt: return 0xA5;
                default: return 0;
            }
        }
    }
}
