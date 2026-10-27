// ===========================================================
// Assets/SquashGame/Runtime/Views/ProcTex.Courts.cs
// The courts' textures (2026-10-24), drawn in code like the rest: two
// skies, sand, marble, a station's star ceiling, arched windows, lit city
// windows, stone and concrete. Seeded and cached for the session.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static partial class ProcTex
    {
        /// <summary>An equirectangular sky (u = around, v = 0 straight down .. 1 straight up; the horizon at 0.5).
        /// night: deep navy, a warm glow low on the horizon, stars and a moon. dusk: navy over violet over orange.</summary>
        public static Texture2D Sky(bool dusk)
        {
            return Cached(dusk ? "sky_dusk" : "sky_night", () =>
            {
                const int W = 2048, H = 1024;
                var rng = new System.Random(dusk ? 77 : 78);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)(H - 1), e = Mathf.Clamp01((v - 0.5f) * 2f);       // 0 at the horizon, 1 at the top
                    Color c;
                    if (dusk)
                    {
                        Color top = new Color(0.04f, 0.06f, 0.18f), mid = new Color(0.30f, 0.17f, 0.40f), low = new Color(1.0f, 0.52f, 0.26f);
                        c = e < 0.18f ? Color.Lerp(low, mid, e / 0.18f) : Color.Lerp(mid, top, Mathf.Pow((e - 0.18f) / 0.82f, 0.6f));
                        if (v < 0.5f) c = Color.Lerp(new Color(0.22f, 0.12f, 0.14f), low, Mathf.Clamp01(v * 2f) * Mathf.Clamp01(v * 2f));
                    }
                    else
                    {
                        Color top = new Color(0.008f, 0.014f, 0.045f), low = new Color(0.07f, 0.075f, 0.14f), glow = new Color(0.28f, 0.16f, 0.09f);
                        c = Color.Lerp(low, top, Mathf.Pow(e, 0.45f));
                        c = Color.Lerp(c, glow, Mathf.Clamp01(1f - e / 0.06f) * 0.6f);
                        if (v < 0.5f) c = Color.Lerp(new Color(0.03f, 0.025f, 0.03f), glow * 0.6f, Mathf.Clamp01(v * 2f));
                    }
                    for (int x = 0; x < W; x++) px[y * W + x] = c;
                }
                // Stars: many at night, a few at dusk, more toward the top.
                int stars = dusk ? 260 : 2600;
                for (int i = 0; i < stars; i++)
                {
                    float v = 0.52f + (float)rng.NextDouble() * 0.48f;
                    if (rng.NextDouble() > (v - 0.45f) * 1.8f) continue;
                    int x = rng.Next(W), y = (int)(v * (H - 1));
                    float b = 0.4f + (float)rng.NextDouble() * (dusk ? 0.5f : 1.2f);
                    var sc = new Color(b, b, b * (0.9f + (float)rng.NextDouble() * 0.2f));
                    px[y * W + x] = sc;
                    if (b > 1.1f) { Set(px, W, H, x + 1, y, sc * 0.5f); Set(px, W, H, x, y + 1, sc * 0.5f); }
                }
                if (!dusk)
                {
                    // The moon, high over the left of the court's back.
                    int mx = (int)(W * 0.62f), my = (int)(H * 0.80f), r = 14;
                    for (int y = -r - 20; y <= r + 20; y++)
                        for (int x = -r - 20; x <= r + 20; x++)
                        {
                            float d = Mathf.Sqrt(x * x + y * y);
                            int X = mx + x, Y = my + y;
                            if (X < 0 || X >= W || Y < 0 || Y >= H) continue;
                            if (d <= r) px[Y * W + X] = new Color(1.6f, 1.55f, 1.4f) * (0.9f + Hash(X, Y) * 0.1f);
                            else px[Y * W + X] += new Color(0.12f, 0.12f, 0.14f) * Mathf.Exp(-(d - r) / 7f);
                        }
                }
                var t = Make(W, H, px, false);
                t.wrapModeU = TextureWrapMode.Repeat;
                return t;
            });
        }

        /// <summary>A frosted glass front wall (graphics pass 2): denser low down where the ball meets it, clearing toward the
        /// top so what is behind shows through, a dark band of names along the top. UV 0..1 = the whole wall; alpha carries the frost.</summary>
        public static Texture2D FrostedGlass() { return FrostedGlass("frostedglass", "RE:SQUASHSTRIKE  WORLD TOUR  GLASS COURT"); }

        /// <summary>The frosted front wall with its own row of names along the top (a venue's).</summary>
        public static Texture2D FrostedGlass(string key, string names)
        {
            return Cached(key, () =>
            {
                const int W = 512, H = 452;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float f = y / (float)(H - 1);                         // 0 floor .. 1 top of the wall
                    float a = f < 0.36f ? 0.46f : Mathf.Lerp(0.46f, 0.16f, Mathf.Clamp01((f - 0.36f) / 0.40f));
                    for (int x = 0; x < W; x++)
                    {
                        float n = ValueNoise(x / 30f, y / 30f, 21) * 0.05f;
                        px[y * W + x] = new Color(0.88f, 0.92f, 0.97f, a + n);
                    }
                }
                int b0 = (int)(H * 0.86f), b1 = (int)(H * 0.95f);
                for (int y = b0; y < b1; y++) for (int x = 0; x < W; x++) px[y * W + x] = new Color(0.08f, 0.14f, 0.30f, 0.82f);
                int tw = TextWidth(names, 2);
                DotText(px, W, H, names, (W - tw) / 2, b0 + (b1 - b0 - 14) / 2, 2, new Color(0.95f, 0.97f, 1f, 1f), 1, W);
                return Make(W, H, px, false);
            });
        }

        /// <summary>Batch 5: corrugated steel sheeting (the club shed's roof): light and dark ribs across u.</summary>
        public static Texture2D Corrugated()
        {
            return Cached("corrugated", () =>
            {
                const int W = 64, H = 8;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float rib = 0.5f + 0.5f * Mathf.Sin(x / (float)W * Mathf.PI * 2f * 4f);
                        float v = 0.62f + 0.22f * rib;
                        px[y * W + x] = new Color(v, v * 1.01f, v * 1.03f);
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>Batch 1: a searchlight's beam - soft across (u), strongest at its foot and fading out along it (v 0 -> 1).
        /// Alpha only.</summary>
        public static Texture2D Beam()
        {
            return Cached("beam", () =>
            {
                const int W = 32, H = 256;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / W * 2f - 1f), v = (y + 0.5f) / H;
                        float across = Mathf.Clamp01(1f - u); across *= across;
                        float along = Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v * 40f);
                        px[y * W + x] = new Color(1f, 1f, 1f, across * along);
                    }
                return Make(W, H, px, false);
            });
        }

        /// <summary>A soft round spot: opaque at the middle, nothing at the edge (contact patches). Alpha only.</summary>
        public static Texture2D SoftSpot()
        {
            return Cached("softspot", () =>
            {
                const int N = 128;
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                        px[y * N + x] = new Color(1f, 1f, 1f, a);
                    }
                return Make(N, N, px, false);
            });
        }

        /// <summary>A pyramid's face at night: stepped courses, lit warm at the base and falling into the dark toward the apex.
        /// UV v = 0 base .. 1 apex.</summary>
        public static Texture2D PyramidFace()
        {
            return Cached("pyramidface", () =>
            {
                const int W = 256, H = 512;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)(H - 1);
                    float light = Mathf.Lerp(1.0f, 0.38f, Mathf.Pow(v, 0.8f));
                    bool course = y % 8 < 2;
                    for (int x = 0; x < W; x++)
                    {
                        float t = light * (0.9f + Hash(x / 6, y / 8) * 0.12f) * (course ? 0.72f : 1f);
                        px[y * W + x] = new Color(t, t * 0.92f, t * 0.82f);
                    }
                }
                return Make(W, H, px, false);
            });
        }

        /// <summary>Sand: warm, rippled, mottled. One tile = 8 m.</summary>
        public static Texture2D Sand()
        {
            return Cached("sand", () =>
            {
                const int W = 512, H = 512;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float n = ValueNoise(x / 60f, y / 60f, 9);
                        float ripple = Mathf.Sin((y * 0.18f + Mathf.Sin(x * 0.02f) * 6f + n * 8f)) * 0.5f + 0.5f;
                        float t = 0.82f + n * 0.18f + ripple * 0.06f - Hash(x, y) * 0.05f;
                        px[y * W + x] = new Color(0.78f * t, 0.62f * t, 0.42f * t);
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>Cream marble with grey veins, laid in 1.2 m slabs. One tile = 2.4 m.</summary>
        public static Texture2D Marble()
        {
            return Cached("marble", () =>
            {
                const int W = 512, H = 512;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float n = ValueNoise(x / 40f, y / 40f, 4);
                        float vein = Mathf.Abs(Mathf.Sin((x * 0.012f + y * 0.007f + n * 4.5f) * 3.1f));
                        float t = 0.92f - Mathf.Pow(1f - vein, 18f) * 0.35f - Hash(x, y) * 0.02f;
                        var c = new Color(0.93f * t, 0.89f * t, 0.82f * t);
                        if (x % 256 < 2 || y % 256 < 2) c *= 0.82f;                       // the joints between slabs
                        px[y * W + x] = c;
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>The station's ceiling: a turquoise sky with gold stars, the brighter ones joined into constellations.
        /// One tile = 24 m.</summary>
        public static Texture2D StarCeiling()
        {
            return Cached("starceiling", () =>
            {
                const int W = 1024, H = 1024;
                var rng = new System.Random(31);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float n = ValueNoise(x / 90f, y / 90f, 2);
                        px[y * W + x] = new Color(0.20f, 0.46f, 0.47f) * (0.88f + n * 0.2f);
                    }
                var gold = new Color(0.98f, 0.80f, 0.40f);
                var pts = new System.Collections.Generic.List<Vector2Int>();
                for (int i = 0; i < 260; i++)
                {
                    int x = rng.Next(W), y = rng.Next(H);
                    int r = rng.NextDouble() < 0.12 ? 4 : rng.NextDouble() < 0.4 ? 2 : 1;
                    Disc(px, W, H, x, y, r, gold);
                    if (r >= 2) pts.Add(new Vector2Int(x, y));
                }
                // Constellations: thin gold lines between near neighbours.
                for (int i = 0; i < pts.Count; i++)
                    for (int j = i + 1; j < pts.Count; j++)
                    {
                        float d = Vector2Int.Distance(pts[i], pts[j]);
                        if (d < 40 || d > 95 || (i * 7 + j) % 3 != 0) continue;
                        for (float t = 0; t <= 1; t += 1f / d)
                        {
                            int x = (int)Mathf.Lerp(pts[i].x, pts[j].x, t), y = (int)Mathf.Lerp(pts[i].y, pts[j].y, t);
                            if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = Color.Lerp(px[y * W + x], gold, 0.55f);
                        }
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>A great arched window lit from outside: leaded panes glowing pale, a dark frame; transparent outside the arch.
        /// UV 0..1 = the window (an aspect of about 1:2).</summary>
        public static Texture2D ArchWindow()
        {
            return Cached("archwindow", () =>
            {
                const int W = 256, H = 512;
                var px = new Color[W * H];
                float r = W * 0.5f, springY = H - r;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        bool inside = y < springY || (x - r) * (x - r) + (y - springY) * (y - springY) <= r * r;
                        if (!inside) { px[y * W + x] = new Color(0, 0, 0, 0); continue; }
                        bool frame = x < 6 || x > W - 7 || y < 6 || x % 32 < 3 || y % 48 < 3;
                        float glow = 0.75f + 0.25f * (y / (float)H) + Hash(x / 32, y / 48) * 0.12f;
                        px[y * W + x] = frame ? new Color(0.10f, 0.10f, 0.12f, 1f) : new Color(0.80f * glow, 0.88f * glow, 0.95f * glow, 1f);
                    }
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true, false);
                t.SetPixels(px);
                t.wrapMode = TextureWrapMode.Clamp;
                t.Apply(true, true);
                return t;
            });
        }

        /// <summary>A building's face at night: dark glass and concrete, windows lit warm or cool at random.
        /// One tile = 8 windows across x 8 floors (12.8 m x 25.6 m).</summary>
        public static Texture2D CityWindows(int seed)
        {
            return Cached("city" + seed, () =>
            {
                const int W = 256, H = 256;
                var rng = new System.Random(500 + seed);
                var px = Fill(W, H, new Color(0.05f, 0.055f, 0.07f));
                for (int fy = 0; fy < 8; fy++)
                    for (int fx = 0; fx < 8; fx++)
                    {
                        double roll = rng.NextDouble();
                        Color c = roll < 0.42 ? new Color(1.0f, 0.78f, 0.45f) : roll < 0.55 ? new Color(0.7f, 0.85f, 1.0f) : new Color(0.09f, 0.10f, 0.13f);
                        c *= 0.7f + (float)rng.NextDouble() * 0.5f;
                        for (int y = fy * 32 + 8; y < fy * 32 + 28; y++)
                            for (int x = fx * 32 + 6; x < fx * 32 + 26; x++)
                                px[y * W + x] = c;
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>Pale stone in courses (the station's walls). One tile = 3 m.</summary>
        public static Texture2D Stone()
        {
            return Cached("stone", () =>
            {
                const int W = 256, H = 256;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int course = y / 32, off = course % 2 == 0 ? 0 : 32;
                        bool joint = y % 32 < 2 || (x + off) % 64 < 2;
                        float t = 0.86f + ValueNoise(x / 20f, y / 20f, 6) * 0.1f + Hash(x / 64, course) * 0.06f;
                        px[y * W + x] = joint ? new Color(0.55f, 0.50f, 0.44f) : new Color(0.86f * t, 0.80f * t, 0.70f * t);
                    }
                return Make(W, H, px, true);
            });
        }

        /// <summary>Weathered concrete (the rooftop deck). One tile = 4 m.</summary>
        public static Texture2D Concrete()
        {
            return Cached("concrete", () =>
            {
                const int W = 256, H = 256;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float t = 0.55f + ValueNoise(x / 25f, y / 25f, 12) * 0.15f - Hash(x, y) * 0.05f;
                        var c = new Color(t, t * 0.98f, t * 0.95f);
                        if (x % 128 < 2 || y % 128 < 2) c *= 0.7f;
                        px[y * W + x] = c;
                    }
                return Make(W, H, px, true);
            });
        }
    }
}
