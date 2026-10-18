// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/CardArt.cs
// UI redesign (U1): drawn card pictures (no image assets) - for cards
// that are not a live 3D vignette.
//
//   rings     a target: concentric rings, a lime bull (Target Arcade)
//   stripes   diagonal stripes in a colour (practice, locked things)
//   split     two colours split on a diagonal (versus)
//   trophy    a cup on a plinth (career, titles)
//   trail     dots on a winding line (the Academy)
//   bracket   a small draw, one path lit (the career draw)
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class CardArt : VisualElement
    {
        readonly string _kind;
        readonly Color _a, _b;
        static readonly Color Lime = new Color32(200, 255, 61, 255);

        public CardArt(string kind, Color a, Color b)
        {
            _kind = kind; _a = a; _b = b;
            AddToClassList("k-card-drawn");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 4) return;
            var p = mgc.painter2D;
            Rect(p, r, _a);
            switch (_kind)
            {
                case "rings":
                {
                    var c = new Vector2(r.width * 0.62f, r.height * 0.38f);
                    float max = r.height * 0.55f;
                    for (int i = 6; i >= 1; i--)
                    {
                        p.BeginPath(); p.Arc(c, max * i / 6f, Angle.Degrees(0), Angle.Degrees(360));
                        p.strokeColor = new Color(Lime.r, Lime.g, Lime.b, 0.10f + 0.06f * (6 - i)); p.lineWidth = 3f; p.Stroke();
                    }
                    p.BeginPath(); p.Arc(c, max / 7f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = Lime; p.Fill();
                    break;
                }
                case "stripes":
                {
                    p.strokeColor = _b; p.lineWidth = 14f;
                    for (float x = -r.height; x < r.width; x += 34f)
                    { p.BeginPath(); p.MoveTo(new Vector2(x, r.height)); p.LineTo(new Vector2(x + r.height, 0)); p.Stroke(); }
                    break;
                }
                case "split":
                {
                    p.BeginPath(); p.MoveTo(new Vector2(r.width * 0.58f, 0)); p.LineTo(new Vector2(r.width, 0)); p.LineTo(new Vector2(r.width, r.height));
                    p.LineTo(new Vector2(r.width * 0.42f, r.height)); p.ClosePath(); p.fillColor = _b; p.Fill();
                    p.BeginPath(); p.MoveTo(new Vector2(r.width * 0.58f, 0)); p.LineTo(new Vector2(r.width * 0.42f, r.height));
                    p.strokeColor = Color.white; p.lineWidth = 3f; p.Stroke();
                    break;
                }
                case "trophy":
                {
                    float cx = r.width * 0.6f, top = r.height * 0.12f, w = r.height * 0.36f;
                    p.fillColor = _b;
                    p.BeginPath(); p.MoveTo(new Vector2(cx - w * 0.5f, top)); p.LineTo(new Vector2(cx + w * 0.5f, top));
                    p.LineTo(new Vector2(cx + w * 0.32f, top + w * 0.7f)); p.LineTo(new Vector2(cx - w * 0.32f, top + w * 0.7f)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(new Vector2(cx - w * 0.08f, top + w * 0.7f)); p.LineTo(new Vector2(cx + w * 0.08f, top + w * 0.7f));
                    p.LineTo(new Vector2(cx + w * 0.08f, top + w * 0.95f)); p.LineTo(new Vector2(cx - w * 0.08f, top + w * 0.95f)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(new Vector2(cx - w * 0.35f, top + w * 0.95f)); p.LineTo(new Vector2(cx + w * 0.35f, top + w * 0.95f));
                    p.LineTo(new Vector2(cx + w * 0.35f, top + w * 1.08f)); p.LineTo(new Vector2(cx - w * 0.35f, top + w * 1.08f)); p.ClosePath(); p.Fill();
                    p.strokeColor = _b; p.lineWidth = 5f;
                    p.BeginPath(); p.Arc(new Vector2(cx - w * 0.5f, top + w * 0.22f), w * 0.16f, Angle.Degrees(90), Angle.Degrees(270)); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(cx + w * 0.5f, top + w * 0.22f), w * 0.16f, Angle.Degrees(-90), Angle.Degrees(90)); p.Stroke();
                    break;
                }
                case "bracket":
                {
                    // a little draw: 4 first-round lines feeding 2, then the final, lit lime on one path
                    float x0 = r.width * 0.18f, w = r.width * 0.2f, top = r.height * 0.08f, h = r.height * 0.5f;
                    p.lineWidth = 3f;
                    for (int round = 0; round < 3; round++)
                    {
                        int n = 4 >> round;
                        for (int i = 0; i < n; i++)
                        {
                            float y = top + h * (i + 0.5f) / n, x = x0 + round * w;
                            bool lit = i == 0;
                            p.strokeColor = lit ? Lime : new Color(1, 1, 1, 0.25f);
                            p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w * 0.7f, y));
                            if (round < 2) { float y2 = top + h * ((i ^ 1) + 0.5f) / n; p.LineTo(new Vector2(x + w * 0.7f, (y + y2) * 0.5f)); p.LineTo(new Vector2(x + w, (y + y2) * 0.5f)); }
                            p.Stroke();
                        }
                    }
                    p.BeginPath(); p.Arc(new Vector2(x0 + 3 * w - w * 0.1f, top + h * 0.5f), 10f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = _b; p.Fill();
                    break;
                }
                case "trail":
                {
                    p.strokeColor = new Color(1, 1, 1, 0.2f); p.lineWidth = 4f;
                    p.BeginPath(); p.MoveTo(new Vector2(r.width * 0.1f, r.height * 0.55f));
                    p.BezierCurveTo(new Vector2(r.width * 0.35f, r.height * 0.05f), new Vector2(r.width * 0.6f, r.height * 0.75f), new Vector2(r.width * 0.92f, r.height * 0.18f));
                    p.Stroke();
                    for (int i = 0; i < 6; i++)
                    {
                        float t = i / 5f;
                        var pt = Bezier(new Vector2(r.width * 0.1f, r.height * 0.55f), new Vector2(r.width * 0.35f, r.height * 0.05f), new Vector2(r.width * 0.6f, r.height * 0.75f), new Vector2(r.width * 0.92f, r.height * 0.18f), t);
                        p.BeginPath(); p.Arc(pt, i < 3 ? 9f : 7f, Angle.Degrees(0), Angle.Degrees(360));
                        p.fillColor = i < 3 ? Lime : new Color(1, 1, 1, 0.25f); p.Fill();
                    }
                    break;
                }
            }
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        static void Rect(Painter2D p, Rect r, Color c)
        {
            p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(r.width, 0)); p.LineTo(new Vector2(r.width, r.height)); p.LineTo(new Vector2(0, r.height)); p.ClosePath();
            p.fillColor = c; p.Fill();
        }
    }
}
