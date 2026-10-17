// ===========================================================
// Assets/SquashGame/Runtime/Views/AimStickWidget.cs
// M9: the Joy-Con stick. While a local player holds Hit / Touch, a small
// thumbstick appears in the middle of the screen: a base circle and a
// knob that follows the drag (or the pad's right stick). The knob goes
// gold when the charge is full (a kill / drop is ready); the base's rim
// lights on one side, tagged BOAST, when the aim is pushed into the boast
// zone. It fades out once the stroke is over (the aim is back in the
// middle). In local 2P, P1's sits left of centre and P2's right.
// Presentation only.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class AimStickWidget : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public int Athlete;
        public Color Colour = new Color(1f, 0.78f, 0.22f);

        /// <summary>Is the widget on screen this frame (tests)?</summary>
        public bool Visible { get; private set; }

        float _alpha;
        static Texture2D _disc, _ring;
        GUIStyle _tag;

        public void Build(SimRunnerBase runner, int athlete, Color colour)
        {
            Runner = runner; Athlete = athlete; Colour = colour;
            if (_disc == null) _disc = Circle(64, false);
            if (_ring == null) _ring = Circle(128, true);
        }

        static Texture2D Circle(int size, bool ring)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);                                        // anti-aliased edge
                    if (ring) a *= Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 3f) + 0.18f * Mathf.Clamp01(r - 6f - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        void Update()
        {
            IntentSource src = Runner != null ? Runner.LocalSource(Athlete) : null;
            bool want = src != null && (src.StrokeHeld || src.AwaitingHit);
            var mr = Runner as MatchRunner;
            if (mr != null && mr.InSetup) want = false;
            _alpha = Mathf.MoveTowards(_alpha, want ? 1f : 0f, Time.unscaledDeltaTime / (want ? 0.06f : 0.15f));
            Visible = _alpha > 0.01f;
        }

        void OnGUI()
        {
            if (!Visible || Runner == null) return;
            IntentSource src = Runner.LocalSource(Athlete);
            if (src == null) return;
            float unit = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.6f);
            float baseR = 64f * unit, knobR = 24f * unit;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.62f;
            var mr = Runner as MatchRunner;
            if (mr != null && mr.Settings.TwoPlayer) cx += (Athlete == 0 ? -1f : 1f) * baseR * 1.8f;

            Color old = GUI.color;
            // Base
            GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.45f * _alpha);
            GUI.DrawTexture(new Rect(cx - baseR, cy - baseR, baseR * 2, baseR * 2), _disc);
            GUI.color = new Color(Colour.r, Colour.g, Colour.b, 0.85f * _alpha);
            GUI.DrawTexture(new Rect(cx - baseR, cy - baseR, baseR * 2, baseR * 2), _ring);

            // Boast zone: the rim lights on that side.
            var ctrl = Runner.Athletes[Athlete];
            int boast = ctrl.WallAimEnabled ? WallAim.TargetWall(ctrl.Aim.BiasX) : 0;
            if (boast != 0)
            {
                float bx = cx + boast * (baseR - 4f * unit);
                GUI.color = new Color(1f, 0.95f, 0.6f, _alpha);
                GUI.DrawTexture(new Rect(bx - 9f * unit, cy - 9f * unit, 18f * unit, 18f * unit), _disc);
                if (_tag == null) _tag = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _tag.fontSize = Mathf.RoundToInt(13 * unit);
                _tag.normal.textColor = new Color(1f, 0.95f, 0.6f, _alpha);
                GUI.Label(new Rect(cx + boast * baseR * 1.45f - 40f, cy - 10f * unit, 80f, 20f * unit), "BOAST", _tag);
            }

            // Knob: the stick as aimed (+y up = up on screen); gold when the charge is full.
            Vector2 k = new Vector2(src.StickX, src.StickY);
            if (k.sqrMagnitude > 1f) k.Normalize();
            float kx = cx + k.x * (baseR - knobR * 0.6f), ky = cy - k.y * (baseR - knobR * 0.6f);
            bool charged = ctrl.ArcadeHitEnabled && ctrl.Arcade.ChargeRatio >= 0.999 && (ctrl.Arcade.Charging || ctrl.Arcade.Waiting);
            Color knob = charged ? new Color(1f, 0.8f, 0.1f) : Colour;
            GUI.color = new Color(0, 0, 0, 0.35f * _alpha);
            GUI.DrawTexture(new Rect(kx - knobR + 2, ky - knobR + 3, knobR * 2, knobR * 2), _disc);
            GUI.color = new Color(knob.r, knob.g, knob.b, _alpha);
            GUI.DrawTexture(new Rect(kx - knobR, ky - knobR, knobR * 2, knobR * 2), _disc);
            GUI.color = old;
        }
    }
}
