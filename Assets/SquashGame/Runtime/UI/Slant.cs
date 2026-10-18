// ===========================================================
// Assets/SquashGame/Runtime/UI/Slant.cs
// The old SquashStrike's slanted button: a parallelogram leaning
// right (skewX(-10deg) in its CSS), the text kept upright. UI Toolkit
// has no skew transform, so the shape is painted (Painter2D) behind the
// row's content. Colours are set by the row (focused / normal / ...).
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class Slant : VisualElement
    {
        /// <summary>tan of the lean: skewX(-10deg).</summary>
        public float Lean = 0.176f;
        public Color Fill = new Color(0.08f, 0.09f, 0.11f, 0.82f);
        public Color Stroke = new Color(1f, 1f, 1f, 0.14f);
        public float StrokeWidth = 1.5f;
        /// <summary>A hard offset shadow (the intro's buttons: 4px lime), none when alpha is 0.</summary>
        public Color Shadow = new Color(0, 0, 0, 0);
        public float ShadowOffset = 4f;

        public Slant()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            generateVisualContent += Paint;
        }

        public void Set(Color fill, Color stroke, float strokeWidth, Color shadow)
        {
            if (fill == Fill && stroke == Stroke && strokeWidth == StrokeWidth && shadow == Shadow) return;
            Fill = fill; Stroke = stroke; StrokeWidth = strokeWidth; Shadow = shadow;
            MarkDirtyRepaint();
        }

        void Shape(Painter2D p, float w, float h, float dx, float dy)
        {
            float k = h * Lean * 0.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(k + dx, dy));
            p.LineTo(new Vector2(w + k + dx, dy));
            p.LineTo(new Vector2(w - k + dx, h + dy));
            p.LineTo(new Vector2(-k + dx, h + dy));
            p.ClosePath();
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 2 || r.height < 2) return;
            var p = mgc.painter2D;
            if (Shadow.a > 0)
            {
                Shape(p, r.width, r.height, ShadowOffset, ShadowOffset);
                p.fillColor = Shadow; p.Fill();
            }
            Shape(p, r.width, r.height, 0, 0);
            p.fillColor = Fill; p.Fill();
            if (StrokeWidth > 0 && Stroke.a > 0)
            {
                Shape(p, r.width, r.height, 0, 0);
                p.strokeColor = Stroke; p.lineWidth = StrokeWidth; p.lineJoin = LineJoin.Miter; p.Stroke();
            }
        }
    }

    /// <summary>The old game's colours.</summary>
    public static class Palette
    {
        public static readonly Color Lime = new Color32(200, 255, 61, 255);
        public static readonly Color Charcoal = new Color32(14, 15, 18, 255);
        public static readonly Color Red = new Color32(255, 74, 64, 255);
        public static readonly Color White90 = new Color(1f, 1f, 1f, 0.9f);

        /// <summary>The menu sidebar's background: dark on the left, fading out on the right.</summary>
        public static Texture2D SidebarFade()
        {
            if (_fade != null) return _fade;
            const int n = 256;
            _fade = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);
                float a = u < 0.62f ? 0.9f : Mathf.Lerp(0.9f, 0f, Mathf.SmoothStep(0f, 1f, (u - 0.62f) / 0.38f));
                _fade.SetPixel(i, 0, new Color(Charcoal.r, Charcoal.g, Charcoal.b, a));
            }
            _fade.Apply();
            return _fade;
        }
        static Texture2D _fade;

        /// <summary>The kit screens' bottom fade: clear at the top, dark at the bottom (cards read over the court).</summary>
        public static Texture2D BottomFade()
        {
            if (_bottom != null) return _bottom;
            const int n = 128;
            _bottom = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);                                   // 0 = bottom row of the texture
                float a = Mathf.Lerp(0.92f, 0f, Mathf.SmoothStep(0f, 1f, u));
                _bottom.SetPixel(0, i, new Color(0.02f, 0.024f, 0.03f, a));
            }
            _bottom.Apply();
            return _bottom;
        }
        static Texture2D _bottom;
    }
}
