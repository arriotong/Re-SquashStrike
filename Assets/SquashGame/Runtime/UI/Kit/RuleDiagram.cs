// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/RuleDiagram.cs
// 2026-11-01: a picture for each of the visual book's 12 rules, drawn
// as the book draws them - the court from above, front wall at the
// top, blue = you, orange = the opponent, green = where the ball
// should go (a red line: the shot not to play).
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class RuleDiagram : VisualElement
    {
        // court metres: x across (0 the middle, +-3.2 the side walls), z from the front wall (0) to the back (9.75)
        const float W = 3.2f, L = 9.75f, ShortLine = 5.44f, Box = 1.6f;

        static readonly Color Floor = new Color32(34, 30, 26, 255), Line = new Color32(220, 60, 52, 220), Wall = new Color(1, 1, 1, 0.55f);
        public static readonly Color You = new Color32(64, 140, 255, 255), Them = new Color32(255, 140, 40, 255);
        public static readonly Color Go = new Color32(120, 230, 90, 255), NoGo = new Color32(240, 70, 60, 255);
        static readonly Color BallC = new Color32(250, 250, 240, 255);

        /// <summary>One rule's picture: you, them, the ball, the good shot (to, dashed when high), a green area, a bad shot,
        /// a path the ball has taken (dotted), a run (dotted, yours).</summary>
        sealed class Pic
        {
            public Vector2 Me, Opp, Ball;
            public Vector2? To, Bad, Zone0, Zone1, From, Run;
            public bool High, GreyBack, Stop;
        }

        readonly Pic _pic;

        public RuleDiagram(int rule)
        {
            _pic = Of(rule);
            AddToClassList("k-rule-pic");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        static Pic Of(int n)
        {
            switch (n)
            {
                case 1: return new Pic { Me = V(1.2f, 6.6f), Opp = V(0.9f, 5.3f), Ball = V(1.6f, 6.2f), Stop = true };
                case 2: return new Pic { Me = V(2.3f, 8.6f), Opp = V(0, 5f), Ball = V(2.8f, 9.2f), To = V(2.7f, 9.3f), High = true, Zone0 = V(1.8f, 8.3f), Zone1 = V(3.2f, 9.75f) };
                case 3: return new Pic { Me = V(0.7f, 6.2f), Opp = V(-0.6f, 3.6f), Ball = V(1.2f, 5.8f), To = V(2.6f, 9.1f), Zone0 = V(1.6f, 8f), Zone1 = V(3.2f, 9.75f) };
                case 4: return new Pic { Me = V(0.4f, 3.8f), Opp = V(-0.6f, 7.9f), Ball = V(1.1f, 3.4f), To = V(2.7f, 0.8f), Zone0 = V(1.8f, 0f), Zone1 = V(3.2f, 1.8f) };
                case 5: return new Pic { Me = V(0.4f, 5.6f), Opp = V(-2.0f, 5.6f), Ball = V(0.9f, 5.2f), To = V(2.6f, 8.8f), Zone0 = V(0.8f, 6.5f), Zone1 = V(3.2f, 9.75f) };
                case 6: return new Pic { Me = V(1.7f, 7.4f), Opp = V(0, 5f), Ball = V(2.3f, 7.0f), To = V(2.9f, 9.4f), Zone0 = V(2.4f, 7.6f), Zone1 = V(3.2f, 9.75f) };
                case 7: return new Pic { Me = V(0.9f, 3.9f), Opp = V(-0.2f, 7.4f), Ball = V(1.5f, 3.5f), To = V(2.7f, 0.8f), Zone0 = V(-3.2f, 0f), Zone1 = V(3.2f, 5.0f), GreyBack = true };
                case 8: return new Pic { Me = V(0.3f, 5.0f), Opp = V(-1.0f, 7.4f), Ball = V(0.9f, 4.6f), From = V(-1.4f, 0.1f), To = V(-2.5f, 1.2f), Zone0 = V(-3.2f, 0f), Zone1 = V(-1.6f, 1.8f) };
                case 9: return new Pic { Me = V(2.2f, 8.2f), Opp = V(-1.6f, 3.2f), Ball = V(-1.9f, 2.4f), Run = V(0, 5.2f), Zone0 = V(-0.7f, 4.5f), Zone1 = V(0.7f, 5.9f) };
                case 10: return new Pic { Me = V(1.9f, 7.5f), Opp = V(-0.4f, 5.2f), Ball = V(2.4f, 8.5f), From = V(2.7f, 9.75f), To = V(2.9f, 0.4f) };
                case 11: return new Pic { Me = V(2.3f, 7.6f), Opp = V(0, 5.1f), Ball = V(3.05f, 7.2f), To = V(3.0f, 0.4f), Bad = V(-2.6f, 8.6f) };
                case 12: return new Pic { Me = V(0.9f, 4.2f), Opp = V(-2.5f, 8.8f), Ball = V(2.4f, 1.6f), From = V(-3.2f, 7.6f), To = V(2.8f, 0.5f), Zone0 = V(1.8f, 0f), Zone1 = V(3.2f, 1.6f), Run = V(1.6f, 2.6f) };
                default: return new Pic { Me = V(0.5f, 5.5f), Opp = V(-0.5f, 5.5f), Ball = V(0, 4f) };
            }
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 8 || r.height < 8) return;
            var p = mgc.painter2D;
            // the court, as big as fits, kept to its shape
            float s = Mathf.Min(r.width / (2 * W), r.height / L) * 0.94f;
            float ox = r.width * 0.5f, oy = (r.height - L * s) * 0.5f;
            System.Func<Vector2, Vector2> at = c => new Vector2(ox + c.x * s, oy + c.y * s);

            Fill(p, at(V(-W, 0)), at(V(W, L)), Floor);
            if (_pic.Zone0.HasValue)
            {
                if (_pic.GreyBack) Fill(p, at(V(-W, 5.0f)), at(V(W, L)), new Color(0.6f, 0.6f, 0.6f, 0.22f));
                Fill(p, at(_pic.Zone0.Value), at(_pic.Zone1.Value), new Color(Go.r, Go.g, Go.b, 0.28f));
            }
            // the lines: short line, half-court line, service boxes; the walls
            p.lineWidth = 2f; p.strokeColor = Line;
            Seg(p, at(V(-W, ShortLine)), at(V(W, ShortLine)));
            Seg(p, at(V(0, ShortLine)), at(V(0, L)));
            Seg(p, at(V(-W + Box, ShortLine)), at(V(-W + Box, ShortLine + Box))); Seg(p, at(V(-W, ShortLine + Box)), at(V(-W + Box, ShortLine + Box)));
            Seg(p, at(V(W - Box, ShortLine)), at(V(W - Box, ShortLine + Box))); Seg(p, at(V(W, ShortLine + Box)), at(V(W - Box, ShortLine + Box)));
            p.strokeColor = Wall; p.lineWidth = 3f;
            p.BeginPath(); p.MoveTo(at(V(-W, 0))); p.LineTo(at(V(W, 0))); p.LineTo(at(V(W, L))); p.LineTo(at(V(-W, L))); p.ClosePath(); p.Stroke();
            p.strokeColor = new Color(1, 1, 1, 0.9f); p.lineWidth = 6f; Seg(p, at(V(-W, 0)), at(V(W, 0)));          // the front wall

            // where the ball came from (dotted), the run (dotted, blue)
            if (_pic.From.HasValue) Dotted(p, at(_pic.From.Value), at(_pic.Ball), new Color(1, 1, 1, 0.6f), 6f);
            if (_pic.Run.HasValue) Dotted(p, at(_pic.Me), at(_pic.Run.Value), You, 7f);
            // the shot not to play, the shot to play
            if (_pic.Bad.HasValue) { Arrow(p, at(_pic.Ball), at(_pic.Bad.Value), NoGo, false, s); Cross(p, Vector2.Lerp(at(_pic.Ball), at(_pic.Bad.Value), 0.5f), 0.35f * s, NoGo); }
            if (_pic.To.HasValue) Arrow(p, at(_pic.Ball), at(_pic.To.Value), Go, _pic.High, s);
            if (_pic.Stop) Cross(p, Vector2.Lerp(at(_pic.Ball), at(_pic.Opp), 0.5f), 0.4f * s, NoGo);

            Dot(p, at(_pic.Opp), 0.32f * s, Them);
            Dot(p, at(_pic.Me), 0.32f * s, You);
            Dot(p, at(_pic.Ball), 0.13f * s, BallC);
        }

        static void Fill(Painter2D p, Vector2 a, Vector2 b, Color c)
        {
            var mn = Vector2.Min(a, b); var mx = Vector2.Max(a, b);
            p.BeginPath(); p.MoveTo(mn); p.LineTo(new Vector2(mx.x, mn.y)); p.LineTo(mx); p.LineTo(new Vector2(mn.x, mx.y)); p.ClosePath();
            p.fillColor = c; p.Fill();
        }

        static void Seg(Painter2D p, Vector2 a, Vector2 b) { p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }

        static void Dot(Painter2D p, Vector2 c, float rad, Color col)
        {
            p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = col; p.Fill();
        }

        static void Dotted(Painter2D p, Vector2 a, Vector2 b, Color col, float gap)
        {
            float len = Vector2.Distance(a, b); int n = Mathf.Max(1, (int)(len / gap));
            for (int i = 0; i <= n; i += 2) { var c = Vector2.Lerp(a, b, i / (float)n); p.BeginPath(); p.Arc(c, 2f, Angle.Degrees(0), Angle.Degrees(360)); p.fillColor = col; p.Fill(); }
        }

        /// <summary>A shot's line with its head; dashed when it's a high one.</summary>
        static void Arrow(Painter2D p, Vector2 a, Vector2 b, Color col, bool dashed, float s)
        {
            p.strokeColor = col; p.lineWidth = Mathf.Max(3f, 0.1f * s);
            var d = (b - a).normalized; float head = Mathf.Max(10f, 0.45f * s);
            var end = b - d * head * 0.6f;
            if (dashed)
            {
                float len = Vector2.Distance(a, end); int n = Mathf.Max(2, (int)(len / 12f));
                for (int i = 0; i < n; i += 2) Seg(p, Vector2.Lerp(a, end, i / (float)n), Vector2.Lerp(a, end, (i + 1) / (float)n));
            }
            else Seg(p, a, end);
            var side = new Vector2(-d.y, d.x);
            p.BeginPath(); p.MoveTo(b); p.LineTo(b - d * head + side * head * 0.5f); p.LineTo(b - d * head - side * head * 0.5f); p.ClosePath();
            p.fillColor = col; p.Fill();
        }

        static void Cross(Painter2D p, Vector2 c, float size, Color col)
        {
            p.strokeColor = col; p.lineWidth = Mathf.Max(3f, size * 0.25f);
            Seg(p, c + new Vector2(-size, -size), c + new Vector2(size, size));
            Seg(p, c + new Vector2(-size, size), c + new Vector2(size, -size));
        }
    }
}
