// ===========================================================
// Assets/SquashGame/Runtime/UI/MenuInput.cs
// P2-M1: menu navigation from any pad or the keyboard (the mouse
// talks to the UI elements directly). Up/down/left/right with
// hold-to-repeat, confirm, back.
//
//   move     arrows / WASD / D-pad / left stick
//   confirm  Enter / Space / pad A (South)
//   back     Esc / Backspace / pad B (East)
// ===========================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame.UI
{
    public sealed class MenuInput : System.IDisposable
    {
        public const float RepeatDelay = 0.38f, RepeatEvery = 0.11f;

        readonly InputActionMap _map;
        readonly InputAction _up, _down, _left, _right, _submit, _cancel;
        readonly float[] _held = new float[4];
        readonly float[] _next = new float[4];

        /// <summary>This frame: -1 up / +1 down (with repeat).</summary>
        public int Vertical { get; private set; }
        /// <summary>This frame: -1 left / +1 right (with repeat).</summary>
        public int Horizontal { get; private set; }
        public bool Submit { get; private set; }
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
        }
    }
}
