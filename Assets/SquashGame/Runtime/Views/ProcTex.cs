// ===========================================================
// Assets/SquashGame/Runtime/Views/ProcTex.cs
// The venue's textures, drawn in code (no image assets, like the rest of
// the game): maple floor boards, wall plaster with ball marks, the tin's
// board, LED ad boards, the crowd, and a 5x7 dot font for the lettering.
// Every texture is seeded, drawn once and cached for the session.
// Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public static partial class ProcTex
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        // ---------------- surfaces ----------------

        /// <summary>Maple boards running front to back. One tile = 1.4 m across x 2.8 m long (u = x, v = z).</summary>
        public static Texture2D Floor()
        {
            return Cached("floor", () =>
            {
                const int W = 512, H = 1024, BOARDS = 20;                        // 0.07 m boards
                var rng = new System.Random(11);
                var px = new Color[W * H];
                float bw = W / (float)BOARDS;
                for (int b = 0; b < BOARDS; b++)
                {
                    // Each board: its own tone, and end joints at staggered lengths.
                    int joints = 2;
                    float[] cut = new float[joints];
                    float[] tone = new float[joints + 1];
                    float start = (float)rng.NextDouble();
                    for (int j = 0; j < joints; j++) cut[j] = (start + j / (float)joints) % 1f;
                    System.Array.Sort(cut);
                    for (int j = 0; j <= joints; j++) tone[j] = 0.9f + (float)rng.NextDouble() * 0.16f;
                    float grainPhase = (float)rng.NextDouble() * 50f;
                    int x0 = Mathf.RoundToInt(b * bw), x1 = Mathf.RoundToInt((b + 1) * bw);
                    for (int x = x0; x < x1; x++)
                    {
                        float fx = (x - x0) / (float)(x1 - x0);
                        bool seam = x == x0;
                        for (int y = 0; y < H; y++)
                        {
                            float fy = y / (float)H;
                            int seg = 0; while (seg < joints && fy > cut[seg]) seg++;
                            bool joint = false;
                            for (int j = 0; j < joints; j++) if (Mathf.Abs(fy - cut[j]) * H < 1.0f) joint = true;
                            float grain = Mathf.Sin((fy * 40f + Mathf.Sin(fx * 6f + grainPhase) * 0.6f + grainPhase) * Mathf.PI * 2f) * 0.5f + 0.5f;
                            float fine = Hash(x * 7 + b, y) * 0.05f;
                            float t = tone[seg] * (0.95f + grain * 0.05f) - fine;
                            var c = new Color(0.86f * t, 0.72f * t, 0.52f * t);
                            if (seam || joint) c *= 0.80f;
                            px[y * W + x] = c;
                        }
                    }
                }
                return Make(W, H, px, true);
            });
        }

        /// <summary>Wall plaster: near-white with a faint mottle; the front wall also carries ball marks
        /// clustered between the tin and the service line. One tile = the whole wall (UV 0..1).</summary>
        public static Texture2D Wall(bool front)
        {
            return Cached(front ? "wall_front" : "wall_side", () =>
            {
                const int W = 512, H = 512;
                var rng = new System.Random(front ? 21 : 22);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float n = ValueNoise(x / 48f, y / 48f, front ? 3 : 4) * 0.035f + Hash(x, y) * 0.015f;
                        float v = 0.95f - n;
                        px[y * W + x] = new Color(v, v, v * 0.985f);
                    }
                // Ball marks: soft grey smudges, densest just above the tin.
                int marks = front ? 260 : 40;
                for (int i = 0; i < marks; i++)
                {
                    float u = (float)rng.NextDouble();
                    float hv = front ? Mathf.Pow((float)rng.NextDouble(), 2.2f) * 0.55f + 0.09f : (float)rng.NextDouble() * 0.45f;   // fraction of wall height
                    int cx = (int)(u * W), cy = (int)(hv * H);
                    float r = 0.8f + (float)rng.NextDouble() * 1.3f;
                    float a = 0.05f + (float)rng.NextDouble() * 0.12f;
                    Blob(px, W, H, cx, cy, r * 1.6f, r, new Color(0.25f, 0.25f, 0.27f), a);
                }
                return Make(W, H, px, true);
            });
        }

        /// <summary>The tin's board: dark, a lime top stripe, and the game's name repeated in dot letters.</summary>
        public static Texture2D Tin()
        {
            return Cached("tin", () =>
            {
                const int W = 2048, H = 160;
                // Navy board, white lettering, a light-blue top edge (the show-court look, 2026-10-23).
                var px = Fill(W, H, new Color(0.05f, 0.09f, 0.18f));
                for (int y = H - 12; y < H; y++) for (int x = 0; x < W; x++) px[y * W + x] = new Color(0.45f, 0.72f, 1f);
                DotText(px, W, H, "RE:SQUASHSTRIKE CLASSIC", 70, 44, 7, new Color(0.95f, 0.96f, 0.98f), 1, 1024);
                DotText(px, W, H, "RE:SQUASHSTRIKE CLASSIC", 70 + 1024, 44, 7, new Color(0.95f, 0.96f, 0.98f), 1, W);
                return Make(W, H, px, true);
            });
        }

        /// <summary>The front wall of a show court: deep blue, lighter at the centre, a row of names along the top above
        /// the out line and the event's mark above the service line; the playing area itself is left clean. UV 0..1 = the
        /// whole wall (6.40 x 5.64 m).</summary>
        public static Texture2D FrontWall()
        {
            return Cached("frontwall_blue", () =>
            {
                const int W = 1024, H = 904;                                     // ~ the wall's aspect
                var px = new Color[W * H];
                var deep = new Color(0.07f, 0.20f, 0.48f); var mid = new Color(0.13f, 0.34f, 0.70f);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (x - W * 0.5f) / (W * 0.5f), dy = (y - H * 0.55f) / (H * 0.6f);
                        float glow = Mathf.Clamp01(1f - (dx * dx * 0.8f + dy * dy));
                        px[y * W + x] = Color.Lerp(deep, mid, glow) * (0.97f + Hash(x, y) * 0.03f);
                    }
                var white = new Color(0.96f, 0.97f, 1f);
                // The names along the top (above the 4.57 m out line: from 81% of the height).
                int topY = (int)(H * 0.855f);
                string[] names = { "RE:SQUASHSTRIKE", "WORLD TOUR", "GLASS COURT", "PLAY ON" };
                int x0 = 30;
                foreach (var n in names)
                {
                    DotText(px, W, H, n, x0, topY, 4, white, 1, W);
                    x0 += TextWidth(n, 4) + 48;
                }
                // The event's mark, between the service line (32%) and the out line.
                string mark = "RE:SQUASHSTRIKE";
                int mw = TextWidth(mark, 8);
                DotText(px, W, H, mark, (W - mw) / 2, (int)(H * 0.62f), 8, white, 1, W);
                string sub = "CLASSIC  2026";
                DotText(px, W, H, sub, (W - TextWidth(sub, 4)) / 2, (int)(H * 0.56f), 4, new Color(0.62f, 0.82f, 1f), 1, W);
                return Make(W, H, px, false);
            });
        }

        /// <summary>Warm wood panelling (the hall's walls): vertical boards with grain, a dark shadow line at each joint.
        /// One tile = 2.4 m across x 2.4 m.</summary>
        public static Texture2D Wood()
        {
            return Cached("wood", () =>
            {
                const int W = 512, H = 512, BOARDS = 6;
                var px = new Color[W * H];
                var rng = new System.Random(5);
                float[] tone = new float[BOARDS];
                for (int b = 0; b < BOARDS; b++) tone[b] = 0.85f + (float)rng.NextDouble() * 0.25f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int b = x * BOARDS / W; float fx = (x * BOARDS / (float)W) - b;
                        float grain = Mathf.Sin((x * 0.09f + Mathf.Sin(y * 0.013f + b * 3f) * 2.5f) * 3f) * 0.5f + 0.5f;
                        float t = tone[b] * (0.88f + grain * 0.12f) - ValueNoise(x / 30f, y / 120f, b) * 0.06f;
                        var c = new Color(0.42f * t, 0.25f * t, 0.14f * t);
                        if (fx < 0.02f) c *= 0.45f;
                        px[y * W + x] = c;
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>An LED ad board: black with lit dot lettering. HDR is applied by the material.</summary>
        public static Texture2D Led(string id, string text, Color ink, Color back)
        {
            return Cached("led_" + id, () =>
            {
                const int H = 64;
                int W = 1024;
                var px = Fill(W, H, back);
                // LED grid: faint darker pixel gaps.
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if ((x % 4 == 3) || (y % 4 == 3)) px[y * W + x] *= 0.55f;
                int scale = 6, adv = TextWidth(text, scale) + 90;
                for (int x0 = 40; x0 < W; x0 += adv) DotText(px, W, H, text, x0, (H - 7 * scale) / 2, scale, ink, 1, W);
                return Make(W, H, px, true);
            });
        }

        /// <summary>A row of seated spectators, 40 seats across (0.5 m each), seen from the court.
        /// Different seeds give different rows.</summary>
        public static Texture2D Crowd(int seed)
        {
            return Cached("crowd" + seed, () =>
            {
                const int W = 1024, H = 64, SEATS = 40;
                var rng = new System.Random(100 + seed);
                var px = Fill(W, H, new Color(0.07f, 0.075f, 0.09f));
                Color[] shirts =
                {
                    Greybox.Hex(0x2b3a55), Greybox.Hex(0x7a1f2b), Greybox.Hex(0xe8e8e8), Greybox.Hex(0x1d1d22), Greybox.Hex(0x35643f),
                    Greybox.Hex(0x9aa3ad), Greybox.Hex(0xc8a03c), Greybox.Hex(0x3d6fa8), Greybox.Hex(0x5a3d6e), Greybox.Hex(0xb35a2a),
                    Greybox.Hex(0x223344), Greybox.Hex(0xaec43a),
                };
                Color[] skins = { Greybox.Hex(0xf1c9a5), Greybox.Hex(0xe0ac83), Greybox.Hex(0xc68b62), Greybox.Hex(0x8d5a3b), Greybox.Hex(0x5c3a26) };
                Color[] hairs = { Greybox.Hex(0x1a1410), Greybox.Hex(0x3b2a1e), Greybox.Hex(0x6b4a2e), Greybox.Hex(0xb08a58), Greybox.Hex(0x9a9a9a) };
                float sw = W / (float)SEATS;
                for (int s = 0; s < SEATS; s++)
                {
                    int cx = Mathf.RoundToInt((s + 0.5f) * sw + ((float)rng.NextDouble() - 0.5f) * 4f);
                    // Seat back.
                    Rect(px, W, H, cx - 10, 0, 21, 22, new Color(0.13f, 0.14f, 0.17f));
                    if (rng.NextDouble() < 0.12) continue;                                       // an empty seat
                    var shirt = shirts[rng.Next(shirts.Length)] * (0.8f + (float)rng.NextDouble() * 0.2f);
                    var skin = skins[rng.Next(skins.Length)];
                    int lean = rng.Next(-2, 3);
                    int shoulderW = 9 + rng.Next(0, 3);
                    // Torso (rounded at the shoulders), arms as darker edges.
                    for (int y = 0; y < 34; y++)
                    {
                        float k = y > 26 ? Mathf.Sqrt(1f - Mathf.Pow((y - 26) / 8f, 2f)) : 1f;
                        int half = Mathf.RoundToInt(shoulderW * k);
                        for (int x = -half; x <= half; x++)
                        {
                            var c = Mathf.Abs(x) > half - 2 ? shirt * 0.75f : shirt;
                            Set(px, W, H, cx + x + lean * y / 34, y, c);
                        }
                    }
                    // Head and hair.
                    int hx = cx + lean, hy = 42, hr = 6;
                    Disc(px, W, H, hx, hy, hr, skin);
                    if (rng.NextDouble() < 0.85)
                    {
                        var hair = hairs[rng.Next(hairs.Length)];
                        for (int y = hy; y <= hy + hr + 1; y++)
                            for (int x = -hr; x <= hr; x++)
                                if (x * x + (y - hy) * (y - hy) <= (hr + 0.5f) * (hr + 0.5f) && y >= hy + 1) Set(px, W, H, hx + x, y, hair);
                    }
                    if (rng.NextDouble() < 0.08) Rect(px, W, H, hx - 7, hy + 3, 15, 3, shirt);     // a cap
                }
                return Make(W, H, px, true);
            });
        }

        /// <summary>The big screen above the front wall: the game's mark on a dark gradient.</summary>
        public static Texture2D Screen()
        {
            return Cached("screen", () =>
            {
                const int W = 1024, H = 256;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float t = y / (float)H;
                        px[y * W + x] = Color.Lerp(new Color(0.03f, 0.05f, 0.09f), new Color(0.07f, 0.11f, 0.18f), t);
                    }
                const int s = 10;
                string all = "RE:SQUASHSTRIKE";
                int w = TextWidth(all, s), x0 = (W - w) / 2, y0 = 100;
                DotText(px, W, H, "RE:", x0, y0, s, new Color(0.72f, 0.93f, 0.15f), 1, W);   // the prefix in lime
                DotText(px, W, H, "SQUASHSTRIKE", x0 + 3 * 6 * s, y0, s, Color.white, 1, W);
                for (int x = x0; x < x0 + w; x++) for (int y = 72; y < 78; y++) px[y * W + x] = new Color(0.72f, 0.93f, 0.15f);
                return Make(W, H, px, true);
            });
        }

        // ---------------- 5x7 dot font ----------------

        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            // 7 rows top to bottom, 5 bits each ('#' lit)
            { 'A', ".###.#...##...#######...##...##...#" }, { 'B', "####.#...##...#####.#...##...#####." },
            { 'C', ".#####....#....#....#....#.....####" }, { 'D', "####.#...##...##...##...##...#####." },
            { 'E', "######....#....####.#....#....#####" }, { 'F', "######....#....####.#....#....#...." },
            { 'G', ".#####....#....#..###...##...#.###." }, { 'H', "#...##...##...#######...##...##...#" },
            { 'I', ".###...#....#....#....#....#...###." }, { 'J', "..###...#....#....#....##..#..##..." },
            { 'K', "#...##..#.#.#..##...#.#..#..#.#...#" }, { 'L', "#....#....#....#....#....#....#####" },
            { 'M', "#...###.###.#.##...##...##...##...#" }, { 'N', "#...###..##.#.##..###...##...##...#" },
            { 'O', ".###.#...##...##...##...##...#.###." }, { 'P', "####.#...##...#####.#....#....#...." },
            { 'Q', ".###.#...##...##...##.#.##..#..##.#" }, { 'R', "####.#...##...#####.#.#..#..#.#...#" },
            { 'S', ".#####....#.....###.....#....#####." }, { 'T', "#####..#....#....#....#....#....#.." },
            { 'U', "#...##...##...##...##...##...#.###." }, { 'V', "#...##...##...##...##...#.#.#...#.." },
            { 'W', "#...##...##...##.#.##.#.##.#.#.#.#." }, { 'X', "#...##...#.#.#...#...#.#.#...##...#" },
            { 'Y', "#...##...#.#.#...#....#....#....#.." }, { 'Z', "#####....#...#...#...#...#....#####" },
            { '0', ".###.#...##..###.#.###..##...#.###." }, { '1', "..#...##....#....#....#....#...###." },
            { '2', ".###.#...#....#...#...#...#...#####" }, { '3', "####.....#....#.###.....#....#####." },
            { '4', "...#...##..#.#.#..#.#####...#....#." }, { '5', "######....####.....#....##...#.###." },
            { '6', ".###.#....#....####.#...##...#.###." }, { '7', "#####....#...#...#...#....#....#..." },
            { '8', ".###.#...##...#.###.#...##...#.###." }, { '9', ".###.#...##...#.####....#....#.###." },
            { ':', "......##...##.......##...##........" }, { '.', "..............................##..." },
            { '-', "...............###................." }, { '\'', "..#....#...#......................." },
            { '•', "...........###..###..###..........." }, { ' ', "..................................." },
        };

        public static int TextWidth(string text, int scale) { return text.Length * 6 * scale - scale; }

        /// <summary>Draws text with its bottom-left at (x0, y0) (texture y up). dot: 1 = solid squares,
        /// 3+ = round dots of that size inside each cell.</summary>
        public static void DotText(Color[] px, int W, int H, string text, int x0, int y0, int scale, Color ink, int dot, int clipW)
        {
            int cx = x0;
            foreach (char ch0 in text)
            {
                char ch = char.ToUpperInvariant(ch0);
                if (!Glyphs.TryGetValue(ch, out string g)) g = Glyphs[' '];
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (g[row * 5 + col] != '#') continue;
                        int bx = cx + col * scale, by = y0 + (6 - row) * scale;
                        for (int y = 0; y < scale; y++)
                            for (int x = 0; x < scale; x++)
                            {
                                if (dot > 1)
                                {
                                    float dx = x - (scale - 1) * 0.5f, dy = y - (scale - 1) * 0.5f;
                                    if (dx * dx + dy * dy > scale * scale * 0.2f) continue;
                                }
                                int X = bx + x;
                                if (X >= clipW || X >= W) continue;
                                Set(px, W, H, X, by + y, ink);
                            }
                    }
                cx += 6 * scale;
            }
        }

        // ---------------- helpers ----------------

        static Texture2D Cached(string key, System.Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = "ProcTex_" + key;
            Cache[key] = t;
            return t;
        }

        static Texture2D Make(int w, int h, Color[] px, bool repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            t.SetPixels(px);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, true);                                                     // mips, then free the CPU copy
            return t;
        }

        static Color[] Fill(int w, int h, Color c) { var px = new Color[w * h]; for (int i = 0; i < px.Length; i++) px[i] = c; return px; }

        static void Set(Color[] px, int W, int H, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            px[y * W + x] = c;
        }

        static void Rect(Color[] px, int W, int H, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) Set(px, W, H, x, y, c);
        }

        static void Disc(Color[] px, int W, int H, int cx, int cy, int r, Color c)
        {
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r + r) Set(px, W, H, cx + x, cy + y, c);
        }

        /// <summary>A soft elliptical smudge blended over what is there.</summary>
        static void Blob(Color[] px, int W, int H, int cx, int cy, float rx, float ry, Color c, float alpha)
        {
            int ix = Mathf.CeilToInt(rx * 2), iy = Mathf.CeilToInt(ry * 2);
            for (int y = -iy; y <= iy; y++)
                for (int x = -ix; x <= ix; x++)
                {
                    float d = (x * x) / (rx * rx) + (y * y) / (ry * ry);
                    if (d > 4f) continue;
                    int X = ((cx + x) % W + W) % W, Y = cy + y;
                    if (Y < 0 || Y >= H) continue;
                    float a = alpha * Mathf.Exp(-d * 1.2f);
                    px[Y * W + X] = Color.Lerp(px[Y * W + X], c, a);
                }
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < 4; o++)
            {
                int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
                float fx = x - xi, fy = y - yi;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                float a = Hash(xi + seed * 131, yi), b = Hash(xi + 1 + seed * 131, yi);
                float c = Hash(xi + seed * 131, yi + 1), d = Hash(xi + 1 + seed * 131, yi + 1);
                sum += Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) * amp;
                norm += amp; amp *= 0.5f; x *= 2f; y *= 2f;
            }
            return sum / norm;
        }
    }
}
