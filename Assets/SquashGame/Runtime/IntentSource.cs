// ===========================================================
// Assets/SquashGame/Runtime/IntentSource.cs
// Unity Input System -> SquashSim.InputIntent. Port of the device half
// of lab/core/InputIntent.js. The only class that touches devices.
//
//   Move   WASD / arrows (always full speed) or left stick (analog)
//   Aim    the original SquashStrike RETICLE: a point on the front wall
//          that stays where you leave it. The mouse moves it (no button
//          needed); the right stick drives it. It glides right up to the
//          front-wall corners and stops there. Its height picks the shot:
//          high = lob, low = drop (or a kill when charged).
//   Boast  hold E / pad RB while you hit: the ball goes off the side wall
//          and lands on the reticle.
//   Hit    LMB / pad South: hold to charge, let go as the ball arrives
//   Dive   Space / pad East     Sprint  Shift / pad LB
//   Let    RMB / Q / pad Y: "Let, please"   Touch  pad West
//   Focus  pad LT: split-step, timed to the opponent's contact
//   Pause  Esc / Start
//   Ready  Enter / pad Y outside a rally: skip the knock-up or break
//
// Local two-player: each player owns an IntentSource restricted to
// their devices (InputActionMap.devices), e.g. keyboard + mouse vs a
// pad, or one pad each. With no restriction it listens to everything.
//
// Stroke edges are ACCUMULATED until a fixed step consumes them
// (LabSimulation clears them). A press during a frame that runs zero
// sim steps is therefore never lost.
// ===========================================================

using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame
{
    public sealed class IntentSource : System.IDisposable
    {
        public readonly InputIntent Intent = new InputIntent();

        /// <summary>"keyboard" or "gamepad": which device last steered the aim.</summary>
        public string Device { get; private set; } = "keyboard";
        public bool HasGamepad { get; private set; }
        /// <summary>The devices this source listens to (null = all).</summary>
        public InputDevice[] Devices { get; private set; }
        /// <summary>Short label for HUD prompts: "keyboard", "pad 1", ...</summary>
        public string DeviceLabel { get; private set; } = "keyboard";
        /// <summary>The reticle (aim space: x across the front wall, beyond +-HALF_W on a side wall; y = height).</summary>
        public float ReticleX { get { return _rx; } }
        public float ReticleY { get { return _ry; } }

        // ---- player settings (setup panel; saved per machine) ----
        /// <summary>Aim speed: 1 = the original 0.015 m of wall per pixel of mouse movement.</summary>
        public static float MouseSensitivityScale = 1f;
        public const float MetresPerPixel = 0.015f;
        /// <summary>Right-stick reticle speed (m/s at full deflection).</summary>
        public const float StickReticleSpeed = 9f;
        public static bool InvertMouseY;
        /// <summary>How close to a front-wall corner the reticle may go: the ring (AimTargetView.CircleRadius) stays fully on the wall.</summary>
        public const float EdgeMargin = 0.15f;
        public static readonly float[] SensitivitySteps = { 0.5f, 0.7f, 1f, 1.4f, 2f };

        public static void LoadSettings()
        {
            try
            {
                MouseSensitivityScale = PlayerPrefs.GetFloat("aim.mouseSensitivity", 1f);
                InvertMouseY = PlayerPrefs.GetInt("aim.invertY", 0) == 1;
            }
            catch (System.Exception) { }
        }

        public static void SaveSettings()
        {
            try
            {
                PlayerPrefs.SetFloat("aim.mouseSensitivity", MouseSensitivityScale);
                PlayerPrefs.SetInt("aim.invertY", InvertMouseY ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }

        /// <summary>When false, mouse buttons and mouse aim are ignored (e.g. cursor free for a debug panel).</summary>
        public bool MouseGameplayEnabled = true;

        const float MOVE_DEADZONE = 0.18f;

        readonly InputActionMap _map;
        readonly InputAction _moveKeys, _moveStick, _aimStick, _mouseDelta, _hit, _touch, _focus, _pause, _appeal, _dive, _sprint, _boast;

        public const float ReticleStartX = 0f, ReticleStartY = 1.8f;
        float _rx = ReticleStartX, _ry = ReticleStartY;
        bool _ready;

        /// <summary>Hit is held right now (charging).</summary>
        public bool StrokeHeld { get { return _hit.IsPressed() || _touch.IsPressed(); } }

        public IntentSource(InputDevice[] devices = null)
        {
            _map = new InputActionMap("Gameplay");

            _moveKeys = _map.AddAction("MoveKeys", InputActionType.Value);
            _moveKeys.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _moveKeys.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _moveStick = _map.AddAction("MoveStick", InputActionType.Value, "<Gamepad>/leftStick");
            _aimStick = _map.AddAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick");
            _mouseDelta = _map.AddAction("MouseAim", InputActionType.Value, "<Mouse>/delta");

            _hit = _map.AddAction("Hit", InputActionType.Button);
            _hit.AddBinding("<Mouse>/leftButton").WithGroup("Mouse");
            _hit.AddBinding("<Gamepad>/buttonSouth");

            _touch = _map.AddAction("Touch", InputActionType.Button);
            _touch.AddBinding("<Gamepad>/buttonWest");

            _focus = _map.AddAction("MomentumFocus", InputActionType.Button);
            _focus.AddBinding("<Gamepad>/leftTrigger");

            _dive = _map.AddAction("Dive", InputActionType.Button);
            _dive.AddBinding("<Keyboard>/space");
            _dive.AddBinding("<Gamepad>/buttonEast");

            _sprint = _map.AddAction("Sprint", InputActionType.Button);
            _sprint.AddBinding("<Keyboard>/leftShift");
            _sprint.AddBinding("<Keyboard>/rightShift");
            _sprint.AddBinding("<Gamepad>/leftShoulder");

            _boast = _map.AddAction("Boast", InputActionType.Button);
            _boast.AddBinding("<Keyboard>/e");
            _boast.AddBinding("<Gamepad>/rightShoulder");

            _appeal = _map.AddAction("Appeal", InputActionType.Button);
            _appeal.AddBinding("<Keyboard>/q");
            _appeal.AddBinding("<Mouse>/rightButton").WithGroup("Mouse");
            _appeal.AddBinding("<Gamepad>/buttonNorth");

            _pause = _map.AddAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");

            UseDevices(devices);
            _map.Enable();
        }

        /// <summary>Listen only to these devices (null = every device). Safe to call at any time.</summary>
        public void UseDevices(InputDevice[] devices)
        {
            Devices = devices;
            // A null InputDevice[] would convert to an EMPTY restriction (no devices at all).
            // Re-resolve the bindings cleanly: swap devices on a disabled map.
            bool wasEnabled = _map.enabled;
            if (wasEnabled) _map.Disable();
            if (devices == null) _map.devices = null; else _map.devices = devices;
            if (wasEnabled) _map.Enable();
            bool keyboard = devices == null || System.Array.Exists(devices, d => d is Keyboard);
            Gamepad pad = OwnPad();
            if (!keyboard && pad != null) Device = "gamepad";
            else if (keyboard && devices != null && pad == null) Device = "keyboard";
            DeviceLabel = keyboard ? (pad != null && devices != null ? "keyboard + pad" : "keyboard") : PadLabel(pad);
        }

        static string PadLabel(Gamepad pad)
        {
            int i = -1;
            for (int k = 0; pad != null && k < Gamepad.all.Count; k++) if (Gamepad.all[k] == pad) i = k;
            return i >= 0 ? "pad " + (i + 1) : "pad";
        }

        Gamepad OwnPad()
        {
            if (Devices == null) return Gamepad.current;
            foreach (var d in Devices) if (d is Gamepad g && g.added) return g;
            return null;
        }

        public void Dispose() { _map.Disable(); _map.Dispose(); }

        /// <summary>After a stroke. The original reticle stays where you left it, so nothing to do.</summary>
        public void BeginRecenter() { }

        /// <summary>Put the reticle somewhere (tests, a new match).</summary>
        public void SetReticle(float x, float y) { _rx = x; _ry = y; ClampReticle(); }

        static float MoveDeadzone(float v)
        {
            float a = Mathf.Abs(v);
            if (a < MOVE_DEADZONE) return 0f;
            return Mathf.Sign(v) * (a - MOVE_DEADZONE) / (1f - MOVE_DEADZONE);
        }

        void ClampReticle()
        {
            // Right up to the front-wall corners (boasts have their own button), from just over the tin to near the out line.
            float maxX = (float)CourtDimensions.HALF_W - EdgeMargin;
            _rx = Mathf.Clamp(_rx, -maxX, maxX);
            _ry = Mathf.Clamp(_ry, 0.65f, (float)CourtDimensions.FRONT_WALL_OUT_H - 0.5f);
        }

        static bool FromMouse(InputAction a)
        {
            var c = a.activeControl;
            return c != null && c.device is Mouse;
        }

        /// <summary>Sample devices once per rendered frame.</summary>
        public void Sample(float dt)
        {
            HasGamepad = OwnPad() != null;

            // ---- movement: court-relative, never camera-relative ----
            // Sim axes: +x = right side wall (seen from the back), -z = toward the front wall.
            Vector2 keys = _moveKeys.ReadValue<Vector2>();
            float mx = keys.x, mz = -keys.y;
            bool keyboardMoving = mx != 0f || mz != 0f;

            float padMag = 0f;
            Vector2 stick = _moveStick.ReadValue<Vector2>();
            float lx = MoveDeadzone(stick.x), lz = MoveDeadzone(-stick.y);
            if (lx != 0f || lz != 0f)
            {
                mx += lx; mz += lz;
                padMag = Mathf.Min(1f, Mathf.Sqrt(lx * lx + lz * lz));
                Device = "gamepad";
            }
            float mag = Mathf.Sqrt(mx * mx + mz * mz);
            if (mag > 1f) { mx /= mag; mz /= mag; }
            Intent.Move.X = mx;
            Intent.Move.Z = mz;
            // A keyboard player is never slowed for lacking an analog stick.
            Intent.MoveMagnitude = keyboardMoving ? 1.0 : Mathf.Min(1f, padMag);

            // ---- aim: the reticle, left where you put it ----
            if (MouseGameplayEnabled)
            {
                Vector2 d = _mouseDelta.ReadValue<Vector2>();
                if (d.x != 0f || d.y != 0f)
                {
                    float k = MetresPerPixel * MouseSensitivityScale;
                    _rx += d.x * k;
                    _ry += (InvertMouseY ? -d.y : d.y) * k;   // Input System delta is +y up: mouse up = aim up
                    Device = "keyboard";
                }
            }
            Vector2 aimStick = _aimStick.ReadValue<Vector2>();
            if (aimStick.sqrMagnitude > 0.15f * 0.15f)
            {
                _rx += aimStick.x * StickReticleSpeed * dt;
                _ry += aimStick.y * StickReticleSpeed * dt;
                Device = "gamepad";
            }
            ClampReticle();
            Intent.HasAimPoint = true;
            Intent.AimPointX = _rx;
            Intent.AimPointY = _ry;
            Intent.AimRawX = 0;
            Intent.AimRawY = 0;

            // ---- strokes: edges accumulate until a fixed step consumes them ----
            SampleStroke(_hit, Intent.Hit, dt);
            SampleStroke(_touch, Intent.Touch, dt);
            if (_hit.WasPressedThisFrame() || _touch.WasPressedThisFrame())
            {
                if (!(FromMouse(_hit) || FromMouse(_touch))) Device = "gamepad";
            }

            Intent.Sprint = _sprint.IsPressed();
            Intent.Boast = _boast.IsPressed();
            if (_dive.WasPressedThisFrame()) Intent.DivePressed = true;          // consumed by the next fixed step
            Intent.MomentumFocus = _focus.IsPressed();
            if (_focus.WasPressedThisFrame()) Intent.SplitStepPressed = true;   // consumed by the next fixed step
            Intent.PausePressed |= _pause.WasPressedThisFrame();
            if (_appeal.WasPressedThisFrame() && (MouseGameplayEnabled || !FromMouse(_appeal))) Intent.AppealPressed = true;   // consumed by the next fixed step
            if ((_appeal.WasPressedThisFrame() && !FromMouse(_appeal)) || ReadyKey()) _ready = true;
        }

        bool ReadyKey()
        {
            bool keyboard = Devices == null || System.Array.Exists(Devices, d => d is Keyboard);
            var kb = Keyboard.current;
            return keyboard && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }

        /// <summary>Did this player press Ready (Enter / pad Y) since the last call? Consumes it.</summary>
        public bool ConsumeReady()
        {
            bool r = _ready;
            _ready = false;
            return r;
        }

        void SampleStroke(InputAction a, StrokeButton b, float dt)
        {
            bool mouse = FromMouse(a);
            bool allowed = MouseGameplayEnabled || !mouse;
            if (a.WasPressedThisFrame() && allowed) { b.Pressed = true; b.HeldSeconds = 0; }
            if (a.WasReleasedThisFrame()) b.Released = true;
            b.Held = a.IsPressed() && allowed;
            if (b.Held) b.HeldSeconds += dt; else b.HeldSeconds = 0;
        }

        /// <summary>Did the pause binding fire? Consumes it.</summary>
        public bool ConsumePause()
        {
            bool p = Intent.PausePressed;
            Intent.PausePressed = false;
            return p;
        }
    }
}
