// ===========================================================
// Assets/SquashGame/Runtime/Views/ProcTex.Legacy.cs
// 2026-11-20 the new legacy courts' textures (the made-up places, after
// City Rooftop and Desert Night): their skies - an aurora night, deep
// space, under the sea, a jungle dusk, a volcano's smoke - and the Earth
// for the station window. Drawn in code, seeded, cached for the session.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static partial class ProcTex
    {
        /// <summary>An equirectangular sky like Sky(): v = 0 straight down .. 1 straight up, the horizon at 0.5.
        /// below / horizon / mid / top colours, stars over the top half, optional aurora curtains.</summary>
        public static Texture2D LegacySky(string key, Color below, Color horizon, Color mid, Color top, int stars, int seed, bool aurora = false)
        {
            return Cached("legacy_sky_" + key, () =>
            {
                const int W = 1024, H = 512;
                var rng = new System.Random(seed);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)(H - 1), e = Mathf.Clamp01((v - 0.5f) * 2f);
                    Color c = v < 0.5f ? Color.Lerp(below, horizon, Mathf.Pow(v * 2f, 3f))
                            : e < 0.25f ? Color.Lerp(horizon, mid, e / 0.25f) : Color.Lerp(mid, top, Mathf.Pow((e - 0.25f) / 0.75f, 0.7f));
                    for (int x = 0; x < W; x++) px[y * W + x] = c;
                }
                if (aurora)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W;
                        float centre = 0.68f + 0.06f * Mathf.Sin(u * 31f) + 0.03f * Mathf.Sin(u * 77f + 1.3f);
                        float strength = Mathf.Clamp01(0.55f + 0.45f * Mathf.Sin(u * 13f + 0.4f));
                        for (int y = H / 2; y < H; y++)
                        {
                            float v = y / (float)(H - 1), d = v - centre;
                            float curtain = d < 0 ? Mathf.Exp(-d * d / 0.0008f) : Mathf.Exp(-d * d / 0.01f);
                            Color glow = Color.Lerp(new Color(0.15f, 1.0f, 0.55f), new Color(0.6f, 0.25f, 0.9f), Mathf.Clamp01(d * 6f));
                            px[y * W + x] += glow * curtain * strength * 0.7f;
                        }
                    }
                for (int i = 0; i < stars; i++)
                {
                    float v = 0.52f + (float)rng.NextDouble() * 0.48f;
                    int x = rng.Next(W), y = (int)(v * (H - 1));
                    float b = 0.4f + (float)rng.NextDouble() * 0.8f;
                    px[y * W + x] = Color.Lerp(px[y * W + x], new Color(b, b, b * 1.05f), 0.9f);
                }
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
                t.SetPixels(px); t.Apply(true);
                return t;
            });
        }

        /// <summary>The Earth seen from orbit (equirectangular): deep blue oceans, green and brown land, white cloud swirls.</summary>
        public static Texture2D Earth()
        {
            return Cached("legacy_earth", () =>
            {
                const int W = 1024, H = 512;
                var px = new Color[W * H];
                Color ocean = new Color(0.05f, 0.18f, 0.48f), shallow = new Color(0.1f, 0.38f, 0.66f), land = new Color(0.22f, 0.42f, 0.18f), dry = new Color(0.55f, 0.45f, 0.28f);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W * 8f, v = y / (float)H * 4f;
                        float n = Mathf.PerlinNoise(u, v) * 0.6f + Mathf.PerlinNoise(u * 2.3f + 7f, v * 2.3f) * 0.4f;
                        Color c = n < 0.48f ? Color.Lerp(ocean, shallow, Mathf.Clamp01((n - 0.38f) / 0.1f))
                                : Color.Lerp(land, dry, Mathf.Clamp01((n - 0.52f) / 0.12f));
                        float lat = Mathf.Abs(y / (float)H - 0.5f) * 2f;
                        if (lat > 0.82f) c = Color.Lerp(c, Color.white, Mathf.Clamp01((lat - 0.82f) / 0.08f));       // the ice caps
                        float cloud = Mathf.PerlinNoise(u * 1.7f + 40f, v * 3.1f + 3f);
                        c = Color.Lerp(c, Color.white, Mathf.Clamp01((cloud - 0.58f) * 3.2f) * 0.85f);
                        px[y * W + x] = c;
                    }
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
                t.SetPixels(px); t.Apply(true);
                return t;
            });
        }
    }
}
