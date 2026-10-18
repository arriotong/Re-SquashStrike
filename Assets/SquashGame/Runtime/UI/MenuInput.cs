// ===========================================================
// Assets/SquashGame/Runtime/UI/MenuInput.cs
// P2-M1: menu navigation from any pad or the keyboard (the mouse
// talks to the UI elements directly). Up/down/left/right with
// hold-to-repeat, confirm, back.
//
//   move     arrows / WASD / D-pad / left stick
//   confirm  Enter / Space / pad A (South)
//   back     Esc / Backspace / pad B (East)
//   tabs     Q / E / pad LB / RB                   (UI redesign U1)
//   x, y     F / R / pad X (West) / Y (North)      a screen's extra actions
//
// UsingPad: whether the last thing touched was a pad (the prompt bar
// shows that device's glyphs).
// ===========================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame.UI
{
    public sealed class MenuInput : System.IDisposable
    {
        public const float RepeatDelay = 0.38f, RepeatEvery = 0.11f;

        readonly InputActionMap _map;
        readonly InputAction _up, _down, _left, _right, _submit, _cancel, _tabL, _tabR, _x, _y;
        readonly float[] _held = new float[4];
        readonly float[] _next = new float[4];

        /// <summary>This frame: -1 up / +1 down (with repeat).</summary>
        public int Vertical { get; private set; }
        /// <summary>This frame: -1 left / +1 right (with repeat).</summary>
        public int Horizontal { get; private set; }
        public bool Submit { get; private set; }
        /// <summary>This frame: -1 previous tab / +1 next tab.</summary>
        public int Tab { get; private set; }
        /// <summary>A screen's extra actions (pad X / F, pad Y / R).</summary>
        public bool ActionX { get; private set; }
        public bool ActionY { get; private set; }
        /// <summary>The last input came from a pad (else keyboard and mouse).</summary>
        public static bool UsingPad { get; private set; }
        public bool Cancel { get; private set; }
        /// <summary>The back press came from Esc (the game's pause key too).</summary>
        public bool CancelFromEscape { get; private set; }

        public MenuInput()
        {
            _map = new InputActionMap("Menus");
            _up = Button("Up", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            _down = Button("Down", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            _left = Button("Left", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _right = Button("Right", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _submit = Button("Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _cancel = Button("Cancel", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            _tabL = Button("TabLeft", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            _tabR = Button("TabRight", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            _x = Button("ActionX", "<Keyboard>/f", "<Gamepad>/buttonWest");
            _y = Button("ActionY", "<Keyboard>/r", "<Gamepad>/buttonNorth");
            _map.Enable();
        }

        InputAction Button(string name, params string[] paths)
        {
            var a = _map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        public void Dispose() { _map.Disable(); _map.Dispose(); }

        int Dir(int i, InputAction a, float dt)
        {
            if (a.WasPressedThisFrame()) { _held[i] = 0f; _next[i] = RepeatDelay; return 1; }
            if (!a.IsPressed()) { _held[i] = 0f; return 0; }
            _held[i] += dt;
            if (_held[i] >= _next[i]) { _next[i] += RepeatEvery; return 1; }
            return 0;
        }

        public void Sample(float dt)
        {
            Vertical = Dir(1, _down, dt) - Dir(0, _up, dt);
            Horizontal = Dir(3, _right, dt) - Dir(2, _left, dt);
            Submit = _submit.WasPressedThisFrame();
            Cancel = _cancel.WasPressedThisFrame();
            var kb = Keyboard.current;
            CancelFromEscape = Cancel && kb != null && kb.escapeKey.wasPressedThisFrame;
            Tab = (_tabR.WasPressedThisFrame() ? 1 : 0) - (_tabL.WasPressedThisFrame() ? 1 : 0);
            ActionX = _x.WasPressedThisFrame();
            ActionY = _y.WasPressedThisFrame();
            // which device: the last one that was used
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
                    pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)
                    UsingPad = true;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.delta.ReadValue().sqrMagnitude > 4f)))
                UsingPad = false;
        }
    }
}
