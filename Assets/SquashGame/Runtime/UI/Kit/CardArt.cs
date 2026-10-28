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
//   clock, chevrons, menu, wall          the arcade's cabinets
//   corner, drop, angle, spots, nick     a court from above, one zone lit (drills, serves)
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class CardArt : VisualElement
    {
        readonly string _kind;
        readonly Color _a, _b;
        /// <summary>The highlight (lime; a Gauntlet build's own colour).</summary>
        readonly Color Lime;
        static readonly Color DefaultLime = new Color32(200, 255, 61, 255);

        public CardArt(string kind, Color a, Color b, Color? hi = null)
        {
            _kind = kind; _a = a; _b = b; Lime = hi ?? DefaultLime;
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
            // the drawing's size and centre: a tall card centres it, a wide one sets it right of the text
            float u = Mathf.Min(r.width * 0.9f, r.height * 0.62f), cx0 = r.width < r.height ? r.width * 0.5f : r.width * 0.6f;
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
                case "clock":                                         // Time Attack: a stopwatch
                {
                    var c = new Vector2(cx0, r.height * 0.06f + u * 0.5f);
                    float rad = u * 0.38f;
                    p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.strokeColor = _b; p.lineWidth = 6f; p.Stroke();
                    p.BeginPath(); p.Arc(c, rad - 10f, Angle.Degrees(-90), Angle.Degrees(150)); p.strokeColor = Lime; p.lineWidth = 8f; p.Stroke();
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(rad * 0.55f, rad * 0.32f)); p.strokeColor = _b; p.lineWidth = 5f; p.Stroke();
                    Box(p, new Rect(c.x - 9f, c.y - rad - 18f, 18f, 12f), _b);
                    break;
                }
                case "chevrons":                                      // Streak: chevrons getting smaller
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float w = u * (0.7f - i * 0.12f), cx = cx0, y = r.height * 0.06f + u * (0.1f + i * 0.2f);
                        p.BeginPath(); p.MoveTo(new Vector2(cx - w * 0.5f, y)); p.LineTo(new Vector2(cx, y + w * 0.32f)); p.LineTo(new Vector2(cx + w * 0.5f, y));
                        p.strokeColor = i == 0 ? Lime : new Color(_b.r, _b.g, _b.b, 0.75f - i * 0.15f); p.lineWidth = 9f - i; p.Stroke();
                    }
                    break;
                }
                case "menu":                                          // Shot Menu: called shots, one ticked
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var row = new Rect(r.width * 0.32f, r.height * (0.1f + i * 0.13f), r.width * 0.56f, r.height * 0.08f);
                        Box(p, row, i == 0 ? Lime : new Color(_b.r, _b.g, _b.b, 0.22f));
                    }
                    p.BeginPath(); p.MoveTo(new Vector2(r.width * 0.36f, r.height * 0.14f)); p.LineTo(new Vector2(r.width * 0.39f, r.height * 0.165f)); p.LineTo(new Vector2(r.width * 0.44f, r.height * 0.115f));
                    p.strokeColor = _a; p.lineWidth = 4f; p.Stroke();
                    break;
                }
                case "wall":                                          // Wall Rally: the front wall, the tin, a ring
                {
                    var wall = new Rect(r.width * 0.2f, r.height * 0.06f, r.width * 0.72f, r.height * 0.5f);
                    p.BeginPath(); p.MoveTo(wall.min); p.LineTo(new Vector2(wall.xMax, wall.yMin)); p.LineTo(wall.max); p.LineTo(new Vector2(wall.xMin, wall.yMax)); p.ClosePath();
                    p.strokeColor = new Color(1, 1, 1, 0.3f); p.lineWidth = 2f; p.Stroke();
                    Box(p, new Rect(wall.xMin, wall.yMax - wall.height * 0.1f, wall.width, wall.height * 0.1f), new Color(0.86f, 0.22f, 0.2f, 0.85f));
                    p.BeginPath(); p.MoveTo(new Vector2(wall.xMin, wall.yMin + wall.height * 0.35f)); p.LineTo(new Vector2(wall.xMax, wall.yMin + wall.height * 0.35f)); p.strokeColor = new Color(0.86f, 0.22f, 0.2f, 0.85f); p.Stroke();
                    p.BeginPath(); p.Arc(new Vector2(wall.center.x + wall.width * 0.15f, wall.yMin + wall.height * 0.55f), wall.height * 0.16f, Angle.Degrees(0), Angle.Degrees(360)); p.strokeColor = Lime; p.lineWidth = 5f; p.Stroke();
                    break;
                }
                case "corner": case "drop": case "angle": case "spots": case "nick":     // a court from above, one thing lit
                {
                    var ct = Court(p, r, cx0);
                    if (_kind == "corner") Box(p, new Rect(ct.xMin + 3, ct.yMax - ct.height * 0.22f, ct.width * 0.32f, ct.height * 0.22f - 3), new Color(Lime.r, Lime.g, Lime.b, 0.7f));
                    if (_kind == "drop") Box(p, new Rect(ct.xMax - ct.width * 0.32f - 3, ct.yMin + 3, ct.width * 0.32f, ct.height * 0.2f), new Color(Lime.r, Lime.g, Lime.b, 0.7f));
                    if (_kind == "nick") { p.BeginPath(); p.Arc(new Vector2(ct.xMin + 4, ct.yMin + ct.height * 0.12f), 9f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = Lime; p.Fill(); }
                    if (_kind == "angle")
                    {
                        p.BeginPath(); p.MoveTo(new Vector2(ct.center.x, ct.yMin + ct.height * 0.62f)); p.LineTo(new Vector2(ct.xMin, ct.yMin + ct.height * 0.3f));
                        p.LineTo(new Vector2(ct.center.x + ct.width * 0.2f, ct.yMin)); p.LineTo(new Vector2(ct.xMax - 6, ct.yMax - 6));
                        p.strokeColor = Lime; p.lineWidth = 4f; p.Stroke();
                    }
                    if (_kind == "spots")
                    {
                        var t = new Vector2(ct.center.x, ct.yMin + ct.height * 0.56f);
                        foreach (var d in new[] { new Vector2(-0.36f, -0.4f), new Vector2(0.36f, -0.4f), new Vector2(-0.38f, 0f), new Vector2(0.38f, 0f), new Vector2(-0.36f, 0.36f), new Vector2(0.36f, 0.36f) })
                        {
                            var at = t + new Vector2(d.x * ct.width, d.y * ct.height);
                            p.BeginPath(); p.MoveTo(t); p.LineTo(at); p.strokeColor = new Color(1, 1, 1, 0.18f); p.lineWidth = 2f; p.Stroke();
                            p.BeginPath(); p.Arc(at, 7f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = Lime; p.Fill();
                        }
                        p.BeginPath(); p.Arc(t, 9f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = _b; p.Fill();
                    }
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

        /// <summary>A court seen from above, front wall at the top: the outline, the short line, the half-court line, the boxes.</summary>
        Rect Court(Painter2D p, Rect r, float cx)
        {
            float h = Mathf.Min(r.height * 0.6f, r.width * 0.84f / 0.66f), w = h * 0.66f;
            var c = new Rect(cx - w * 0.5f, r.height * 0.05f, w, h);
            p.strokeColor = new Color(1, 1, 1, 0.35f); p.lineWidth = 2f;
            p.BeginPath(); p.MoveTo(c.min); p.LineTo(new Vector2(c.xMax, c.yMin)); p.LineTo(c.max); p.LineTo(new Vector2(c.xMin, c.yMax)); p.ClosePath(); p.Stroke();
            float sl = c.yMin + c.height * 0.56f, box = c.width * 0.25f;
            p.strokeColor = new Color(0.86f, 0.22f, 0.2f, 0.8f);
            p.BeginPath(); p.MoveTo(new Vector2(c.xMin, sl)); p.LineTo(new Vector2(c.xMax, sl)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(c.center.x, sl)); p.LineTo(new Vector2(c.center.x, c.yMax)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(c.xMin, sl + box)); p.LineTo(new Vector2(c.xMin + box, sl + box)); p.LineTo(new Vector2(c.xMin + box, sl)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(c.xMax, sl + box)); p.LineTo(new Vector2(c.xMax - box, sl + box)); p.LineTo(new Vector2(c.xMax - box, sl)); p.Stroke();
            return c;
        }

        static void Box(Painter2D p, Rect b, Color c)
        {
            p.BeginPath(); p.MoveTo(b.min); p.LineTo(new Vector2(b.xMax, b.yMin)); p.LineTo(b.max); p.LineTo(new Vector2(b.xMin, b.yMax)); p.ClosePath();
            p.fillColor = c; p.Fill();
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
