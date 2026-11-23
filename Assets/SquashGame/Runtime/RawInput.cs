// ===========================================================
// Assets/SquashGame/Runtime/RawInput.cs
// 2026-11-23 (the user: "if the user has 3 different keyboards connected,
// they should be able to see a list of all of their device's names and then
// they can choose from that"): Unity's Input System merges every keyboard
// (and every mouse) on Windows into one. Windows itself tells them apart -
// its Raw Input: each WM_INPUT names the device it came from.
//
// Windows lets one window per program receive raw input for a kind of
// device, so this doesn't register a window of its own (that would take the
// mouse from Unity): it listens in on the window that already gets it - the
// game's own (subclassing its window procedure) - and registers the keyboard
// there only if nobody has. Each keyboard's keys and each mouse's movement
// and buttons are kept per device; names come from the device itself
// (HidD_GetProductString), else its vendor and product ids.
//
// The built Windows game only (not the editor, not other platforms):
// elsewhere Available is false and the game uses the merged keyboard and
// mouse (split into halves for two players). Presentation input only - the
// match still reads intents.
// ===========================================================

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace SquashGame
{
    public static class RawInput
    {
        public sealed class Device
        {
            public IntPtr Handle;
            public bool IsKeyboard;
            public string Path = "", Name = "";
            /// <summary>The physical device (its USB parent): a keyboard's extra-keys part and a receiver's parts are one device.</summary>
            public string Group = "";
            internal readonly List<IntPtr> Handles = new List<IntPtr>();
            /// <summary>Real time of its last key, button or movement.</summary>
            public float LastActive = -100f;
            /// <summary>A mouse: real time of its last movement (moving it shows which it is - a click would pick).</summary>
            public float LastMoved = -100f;
            internal readonly bool[] Keys = new bool[256];
            internal float Dx, Dy;
            internal int Buttons;
        }

        /// <summary>Raw input is running (the built Windows game, hooked).</summary>
        public static bool Available { get; private set; }
        public static readonly List<Device> Keyboards = new List<Device>(), Mice = new List<Device>();
        /// <summary>The device list changed (a keyboard or mouse plugged in or out).</summary>
        public static int Version { get; private set; }

        public static Device Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var d in Keyboards) if (d.Path == path) return d;
            foreach (var d in Mice) if (d.Path == path) return d;
            return null;
        }

        /// <summary>A key (Windows virtual-key code; left / right Shift and Ctrl apart) is down on this keyboard.</summary>
        public static bool KeyDown(Device d, int vk) { return d != null && vk > 0 && vk < 256 && d.Keys[vk]; }
        /// <summary>The mouse's movement since the last call (pixels; +y up), and its buttons (1 left, 2 right, 4 middle).</summary>
        public static Vector2 TakeDelta(Device d) { if (d == null) return Vector2.zero; var v = new Vector2(d.Dx, -d.Dy); d.Dx = 0; d.Dy = 0; return v; }
        public static int Buttons(Device d) { return d != null ? d.Buttons : 0; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // ---------------- Win32 ----------------
        const int GWLP_WNDPROC = -4;
        const uint WM_INPUT = 0x00FF, WM_INPUT_DEVICE_CHANGE = 0x00FE, WM_KILLFOCUS = 0x0008;
        const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007;
        const uint RIM_TYPEMOUSE = 0, RIM_TYPEKEYBOARD = 1;
        const uint RIDEV_DEVNOTIFY = 0x00002000;
        const ushort RI_KEY_BREAK = 1, RI_KEY_E0 = 2;

        [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }
        [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }
        [StructLayout(LayoutKind.Sequential)] struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] list, ref uint count, uint size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint command, StringBuilder data, ref uint size);
        [DllImport("user32.dll", SetLastError = true)] static extern uint GetRegisteredRawInputDevices([Out] RAWINPUTDEVICE[] devices, ref uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc fn, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(IntPtr h, StringBuilder buffer, int length);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint devInst, string id, uint flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint devInst, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint devInst, StringBuilder buffer, int length, uint flags);
        static readonly Dictionary<IntPtr, Device> _byHandle = new Dictionary<IntPtr, Device>();

        /// <summary>The physical device a raw-input path belongs to: up the device tree to the USB device (not one of its
        /// interfaces); a built-in one: its own parent. Two of the same keyboard stay two (their USB devices differ).</summary>
        static string PhysicalId(string path)
        {
            try
            {
                string id = path.StartsWith(@"\\?\") ? path.Substring(4) : path;
                int g = id.LastIndexOf("#{"); if (g > 0) id = id.Substring(0, g);
                id = id.Replace('#', '\\');
                if (CM_Locate_DevNodeW(out uint node, id, 0) != 0) return path;
                string last = id;
                for (int up = 0; up < 5; up++)
                {
                    if (CM_Get_Parent(out uint parent, node, 0) != 0) break;
                    var sb = new StringBuilder(512);
                    if (CM_Get_Device_IDW(parent, sb, 512, 0) != 0) break;
                    string pid = sb.ToString().ToUpperInvariant();
                    if (pid.StartsWith("USB\\") && !pid.Contains("&MI_")) return pid;
                    if (up == 0 && pid.StartsWith("ROOT\\") && !pid.StartsWith("ROOT\\ACPI_HAL")) return pid;   // (a software device's own parent)
                    if (pid.StartsWith("HTREE") || pid.Length == 0) break;
                    last = pid; node = parent;
                }
                return id.ToUpperInvariant();                          // (built in, not USB: the device itself)
            }
            catch { return path; }
        }

        static WndProc _proc;                                     // (kept alive: Windows calls it)
        static readonly Dictionary<IntPtr, IntPtr> _hooked = new Dictionary<IntPtr, IntPtr>();
        static IntPtr _buffer = IntPtr.Zero; static uint _bufferSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            try
            {
                IntPtr main = MainWindow();
                if (main == IntPtr.Zero) { Debug.Log("[rawinput] no game window - merged input only"); return; }
                _proc = Proc;
                var targets = new HashSet<IntPtr>();
                // who gets keyboards (usage 6) and mice (usage 2) now? listen where they go; register the keyboard if nobody does
                uint n = 0, sz = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE));
                GetRegisteredRawInputDevices(null, ref n, sz);
                var regs = new RAWINPUTDEVICE[Math.Max(1, (int)n)];
                if (n > 0) GetRegisteredRawInputDevices(regs, ref n, sz);
                bool kb = false, ms = false;
                for (int i = 0; i < n; i++)
                {
                    if (regs[i].usUsagePage != 1) continue;
                    if (regs[i].usUsage == 6) { kb = true; targets.Add(regs[i].hwndTarget == IntPtr.Zero ? main : regs[i].hwndTarget); }
                    if (regs[i].usUsage == 2) { ms = true; targets.Add(regs[i].hwndTarget == IntPtr.Zero ? main : regs[i].hwndTarget); }
                }
                var mine = new List<RAWINPUTDEVICE>();
                if (!kb) mine.Add(new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 6, dwFlags = RIDEV_DEVNOTIFY, hwndTarget = main });
                if (!ms) mine.Add(new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_DEVNOTIFY, hwndTarget = main });
                if (mine.Count > 0) { RegisterRawInputDevices(mine.ToArray(), (uint)mine.Count, sz); targets.Add(main); }
                foreach (var h in targets)
                {
                    if (h == IntPtr.Zero || _hooked.ContainsKey(h)) continue;
                    IntPtr prev = SetWindowLongPtr(h, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_proc));
                    if (prev != IntPtr.Zero) _hooked[h] = prev;
                }
                Refresh();
                Available = _hooked.Count > 0;
                Debug.Log("[rawinput] " + (Available ? "on" : "off") + ": " + Keyboards.Count + " keyboards, " + Mice.Count + " mice");
                foreach (var d in Keyboards) Debug.Log("[rawinput] keyboard: " + d.Name + "  |  " + d.Path);
                foreach (var d in Mice) Debug.Log("[rawinput] mouse: " + d.Name + "  |  " + d.Path);
            }
            catch (Exception e) { Available = false; Debug.LogWarning("[rawinput] off: " + e.Message); }
        }

        static IntPtr MainWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), (h, p) =>
            {
                var sb = new StringBuilder(64); GetClassName(h, sb, 64);
                if (sb.ToString() == "UnityWndClass") { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        static IntPtr Proc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT) { try { Read(lParam); } catch { } }
            else if (msg == WM_INPUT_DEVICE_CHANGE) { try { Refresh(); } catch { } }
            else if (msg == WM_KILLFOCUS) { foreach (var d in Keyboards) Array.Clear(d.Keys, 0, 256); foreach (var d in Mice) d.Buttons = 0; }
            return _hooked.TryGetValue(hWnd, out IntPtr prev) ? CallWindowProc(prev, hWnd, msg, wParam, lParam) : IntPtr.Zero;
        }

        static void Read(IntPtr lParam)
        {
            uint size = 0, header = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, header);
            if (size == 0) return;
            if (size > _bufferSize) { if (_buffer != IntPtr.Zero) Marshal.FreeHGlobal(_buffer); _buffer = Marshal.AllocHGlobal((int)size); _bufferSize = size; }
            if (GetRawInputData(lParam, RID_INPUT, _buffer, ref size, header) != size) return;
            var h = (RAWINPUTHEADER)Marshal.PtrToStructure(_buffer, typeof(RAWINPUTHEADER));
            IntPtr body = IntPtr.Add(_buffer, (int)header);
            float now = Time.realtimeSinceStartup;
            if (h.dwType == RIM_TYPEKEYBOARD)
            {
                var d = ById(Keyboards, h.hDevice);
                if (d == null) return;
                ushort make = (ushort)Marshal.ReadInt16(body, 0), flags = (ushort)Marshal.ReadInt16(body, 2), vk = (ushort)Marshal.ReadInt16(body, 6);
                if (vk == 0x10) vk = (ushort)(make == 0x36 ? 0xA1 : 0xA0);                    // Shift: left or right
                else if (vk == 0x11) vk = (ushort)((flags & RI_KEY_E0) != 0 ? 0xA3 : 0xA2);    // Ctrl
                else if (vk == 0x12) vk = (ushort)((flags & RI_KEY_E0) != 0 ? 0xA5 : 0xA4);    // Alt
                if (vk < 256) d.Keys[vk] = (flags & RI_KEY_BREAK) == 0;
                d.LastActive = now;
            }
            else if (h.dwType == RIM_TYPEMOUSE)
            {
                var d = ById(Mice, h.hDevice);
                if (d == null) return;
                ushort usFlags = (ushort)Marshal.ReadInt16(body, 0);
                ushort buttons = (ushort)Marshal.ReadInt16(body, 4);
                int dx = Marshal.ReadInt32(body, 12), dy = Marshal.ReadInt32(body, 16);
                if ((usFlags & 1) == 0) { d.Dx += dx; d.Dy += dy; }                         // (relative movement only)
                if ((buttons & 0x01) != 0) d.Buttons |= 1; if ((buttons & 0x02) != 0) d.Buttons &= ~1;
                if ((buttons & 0x04) != 0) d.Buttons |= 2; if ((buttons & 0x08) != 0) d.Buttons &= ~2;
                if ((buttons & 0x10) != 0) d.Buttons |= 4; if ((buttons & 0x20) != 0) d.Buttons &= ~4;
                if (dx != 0 || dy != 0 || buttons != 0) d.LastActive = now;
                if (dx != 0 || dy != 0) d.LastMoved = now;
            }
        }

        static Device ById(List<Device> l, IntPtr h)
        {
            if (_byHandle.TryGetValue(h, out var d) && l.Contains(d)) return d;
            Refresh();                                                // (a device we hadn't listed yet)
            return _byHandle.TryGetValue(h, out d) && l.Contains(d) ? d : null;
        }

        /// <summary>List the keyboards and mice again (their names from the devices).</summary>
        public static void Refresh()
        {
            uint n = 0, sz = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
            GetRawInputDeviceList(null, ref n, sz);
            var list = new RAWINPUTDEVICELIST[Math.Max(1, (int)n)];
            if (n > 0) GetRawInputDeviceList(list, ref n, sz);
            var kbs = new List<Device>(); var mice = new List<Device>();
            var oldGroups = new Dictionary<string, Device>();
            foreach (var d in Keyboards) oldGroups["k" + d.Group] = d;
            foreach (var d in Mice) oldGroups["m" + d.Group] = d;
            _byHandle.Clear();
            for (int i = 0; i < n; i++)
            {
                if (list[i].dwType != RIM_TYPEKEYBOARD && list[i].dwType != RIM_TYPEMOUSE) continue;
                string path = DeviceName(list[i].hDevice);
                if (path.IndexOf("RDP_", StringComparison.OrdinalIgnoreCase) >= 0) continue;    // (remote-desktop stand-ins)
                if (path.IndexOf("VIRTUALDEVICE", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // (software-made devices)
                bool isKb = list[i].dwType == RIM_TYPEKEYBOARD;
                string group = PhysicalId(path);
                if (group.StartsWith("ROOT\\")) continue;                      // (made by software, not plugged in)
                if (!group.StartsWith("USB\\") && group.Contains("VID_")) continue;   // (a USB maker's id with no USB device under it: virtual)
                var into = isKb ? kbs : mice;
                // one physical device's parts are one device (the keys of each go to it)
                var d = into.Find(x => x.Group == group);
                if (d == null)
                {
                    oldGroups.TryGetValue((isKb ? "k" : "m") + group, out d);
                    if (d == null) d = new Device { Path = group, Group = group, IsKeyboard = isKb };
                    d.Handles.Clear(); d.Name = "";
                    into.Add(d);
                }
                d.Handles.Add(list[i].hDevice);
                if (d.Handle == IntPtr.Zero || path.Contains("MI_00") || !path.Contains("&COL")) d.Handle = list[i].hDevice;
                _byHandle[list[i].hDevice] = d;
                // its name: the first part that has one (the main interface's preferred)
                string name = Friendly(path, isKb);
                if (string.IsNullOrEmpty(d.Name) || (d.Name.StartsWith(isKb ? "Keyboard" : "Mouse") && !name.StartsWith(isKb ? "Keyboard" : "Mouse"))) d.Name = name;
            }
            // the same product twice: number them
            foreach (var group in new[] { kbs, mice })
            {
                var seen = new Dictionary<string, int>();
                foreach (var d in group) seen[d.Name] = seen.TryGetValue(d.Name, out int c) ? c + 1 : 1;
                var at = new Dictionary<string, int>();
                foreach (var d in group)
                    if (seen[d.Name] > 1 && !d.Name.EndsWith(")")) { at[d.Name] = at.TryGetValue(d.Name, out int k) ? k + 1 : 1; d.Name = d.Name + " (" + at[d.Name] + ")"; }
            }
            bool changed = kbs.Count != Keyboards.Count || mice.Count != Mice.Count;
            Keyboards.Clear(); Keyboards.AddRange(kbs); Mice.Clear(); Mice.AddRange(mice);
            if (changed) Version++;
        }

        static string DeviceName(IntPtr h)
        {
            uint size = 0;
            GetRawInputDeviceInfo(h, RIDI_DEVICENAME, null, ref size);
            if (size == 0) return "";
            var sb = new StringBuilder((int)size + 1);
            GetRawInputDeviceInfo(h, RIDI_DEVICENAME, sb, ref size);
            return sb.ToString();
        }

        static string Friendly(string path, bool keyboard)
        {
            string kind = keyboard ? "Keyboard" : "Mouse";
            try
            {
                IntPtr f = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (f != IntPtr.Zero && f != new IntPtr(-1))
                {
                    var sb = new StringBuilder(128);
                    bool ok = HidD_GetProductString(f, sb, 256);
                    CloseHandle(f);
                    string s = sb.ToString().Trim();
                    if (ok && s.Length > 0) return s;
                }
            }
            catch { }
            string up = path.ToUpperInvariant();
            int v = up.IndexOf("VID_"), p = up.IndexOf("PID_");
            if (v >= 0 && p >= 0 && v + 8 <= up.Length && p + 8 <= up.Length) return kind + " " + up.Substring(v + 4, 4) + ":" + up.Substring(p + 4, 4);
            if (up.Contains("ACPI") || up.Contains("PNP")) return "Built-in " + kind.ToLowerInvariant();
            return kind;
        }
#else
        /// <summary>(Not the built Windows game: no raw input - the merged keyboard and mouse.)</summary>
        public static void Refresh() { }
#endif
    }
}
