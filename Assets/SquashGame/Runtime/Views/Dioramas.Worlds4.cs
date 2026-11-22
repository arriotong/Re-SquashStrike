// ===========================================================
// Assets/SquashGame/Runtime/Views/Dioramas.Worlds4.cs
// 2026-11-22 the PLAY world (the user: "the landing page world and quick
// match world is the same, make a new world for quick match and local
// vs"): Versus Night - a neon night, its own (no other world is neon):
//   - an arena island split down the middle, red half and blue half
//   - the glass show court on the seam, lit from both sides
//   - the giant glowing VS over it, two rackets crossed behind it
//   - searchlight towers sweeping, red and blue banners, spark showers
//   - a starry violet-teal sky, little islands, low clouds
// Same toolkit and layout rules as the other worlds. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashGame.ToonKit;

namespace SquashGame
{
    public static partial class Dioramas
    {
        static void BuildPlay(Transform t, DioramaLife life)
        {
            var rng = new System.Random(5150);
            Color red = new Color(0.86f, 0.24f, 0.30f), blue = new Color(0.22f, 0.42f, 0.92f), rock = new Color(0.26f, 0.24f, 0.36f), dark = new Color(0.12f, 0.12f, 0.18f);
            Sky(t, "play", new Color(0.05f, 0.04f, 0.10f), new Color(0.42f, 0.16f, 0.48f), new Color(0.12f, 0.20f, 0.42f), new Color(0.03f, 0.04f, 0.12f), 260, 5150);
            Billboard(t, "MoonGlow", new Vector3(90f, 80f, -320f), 120f, new Color(0.6f, 0.8f, 1f, 0.35f), StdCam);
            Billboard(t, "Moon", new Vector3(90f, 80f, -319f), 18f, new Color(1.5f, 1.6f, 1.9f), StdCam, false);

            // the island: one side red, one blue (two halves of top over one rock)
            Island(t, 26f, dark, rock, rng);
            var half = new List<Vector3>(); var half2 = new List<Vector3>();
            half.AddRange(BoxTris(new Vector3(9f, 1.08f, 0f), new Vector3(16f, 0.12f, 30f)));
            half2.AddRange(BoxTris(new Vector3(-9f, 1.08f, 0f), new Vector3(16f, 0.12f, 30f)));
            Toon(t, "RedHalf", half, red * 0.75f, new Vector3(9f, 1f, 0f), false, 3);
            Toon(t, "BlueHalf", half2, blue * 0.75f, new Vector3(-9f, 1f, 0f), false, 3);
            Glow(t, "Seam", BoxTris(new Vector3(0f, 1.16f, 0f), new Vector3(0.35f, 0.05f, 30f)), new Color(1.6f, 1.6f, 1.8f));

            // the glass court on the seam, lit from both sides
            GlassCourt(t, new Vector3(0f, 1.15f, 2f), 0f, 0.9f, Wood);
            Glow(t, "RedWash", BoxTris(new Vector3(5.5f, 1.2f, 2f), new Vector3(0.25f, 0.06f, 12f)), new Color(2.0f, 0.4f, 0.5f));
            Glow(t, "BlueWash", BoxTris(new Vector3(-5.5f, 1.2f, 2f), new Vector3(0.25f, 0.06f, 12f)), new Color(0.4f, 0.8f, 2.2f));

            // the giant VS, glowing, the crossed rackets behind it
            var vs = new GameObject("VS").transform; vs.SetParent(t, false); vs.localPosition = new Vector3(0f, 15f, -8f);
            // (the camera looks back from +z: the screen's left is world +x - so V sits at +x, S at -x, S drawn mirror-wise)
            var v = new List<Vector3>();
            v.AddRange(Cylinder(new Vector3(4.2f, 2.6f, 0f), new Vector3(2.6f, -2.6f, 0f), 0.45f, 6));
            v.AddRange(Cylinder(new Vector3(1.0f, 2.6f, 0f), new Vector3(2.6f, -2.6f, 0f), 0.45f, 6));
            Glow(vs, "V", v, new Color(2.2f, 0.5f, 0.6f));
            var s = new List<Vector3>();
            float sx = -2.6f;
            s.AddRange(BoxTris(new Vector3(sx, 2.2f, 0f), new Vector3(3.2f, 0.8f, 0.8f)));
            s.AddRange(BoxTris(new Vector3(sx + 1.2f, 1.1f, 0f), new Vector3(0.8f, 2.4f, 0.8f)));     // the top's upright: on the screen's left
            s.AddRange(BoxTris(new Vector3(sx, 0f, 0f), new Vector3(3.2f, 0.8f, 0.8f)));
            s.AddRange(BoxTris(new Vector3(sx - 1.2f, -1.1f, 0f), new Vector3(0.8f, 2.4f, 0.8f)));    // the bottom's: on the right
            s.AddRange(BoxTris(new Vector3(sx, -2.2f, 0f), new Vector3(3.2f, 0.8f, 0.8f)));
            Glow(vs, "S", s, new Color(0.5f, 0.9f, 2.4f));
            Billboard(vs, "VSGlow", new Vector3(0f, 0f, -0.6f), 16f, new Color(0.9f, 0.6f, 1f, 0.35f), StdCam - vs.localPosition);
            life.Bob(vs, 0.6f, 0f, 0.4f);
            Racket(t, new Vector3(6f, 2.0f, -13f), new Vector3(-0.75f, 1f, 0f), Vector3.forward, 10f, new Color(0.95f, 0.32f, 0.36f), new Color(0.12f, 0.12f, 0.14f));
            Racket(t, new Vector3(-6f, 2.0f, -13f), new Vector3(0.75f, 1f, 0f), Vector3.forward, 10f, new Color(0.3f, 0.6f, 1.0f), new Color(0.12f, 0.12f, 0.14f));
            SquashBall(t, new Vector3(0f, 22f, -9f), 1.1f, new Vector3(0.2f, -0.3f, 1f));

            // the searchlight towers and their beams, sweeping
            Vector3[] towers = { new Vector3(-17f, 1f, -12f), new Vector3(17f, 1f, -12f), new Vector3(-20f, 1f, 6f), new Vector3(20f, 1f, 6f) };
            for (int k = 0; k < towers.Length; k++)
            {
                var tw = towers[k];
                Toon(t, "Tower", Cylinder(tw, tw + Vector3.up * 9f, 0.45f, 8), new Color(0.3f, 0.3f, 0.36f), tw + Vector3.up * 4f, true, 3);
                var head = new GameObject("Searchlight").transform; head.SetParent(t, false); head.localPosition = tw + Vector3.up * 9.2f;
                Glow(head, "Lamp", Sphere(Vector3.zero, Vector3.one * 0.7f, 8, 5), k % 2 == 0 ? new Color(2.2f, 0.6f, 0.7f) : new Color(0.6f, 1.0f, 2.4f));
                var beam = new List<Vector3>(); beam.AddRange(Cone(Vector3.zero, 0.9f, 26f, 8));
                var b = Glow(head, "Beam", beam, k % 2 == 0 ? new Color(0.45f, 0.14f, 0.2f) : new Color(0.14f, 0.26f, 0.55f));
                b.transform.localRotation = Quaternion.Euler(0f, 0f, k % 2 == 0 ? 24f : -24f);
                life.Spin(head, Vector3.up, k % 2 == 0 ? 14f : -14f);
            }

            // banners: red on the left of the court, blue on the right
            for (int k = 0; k < 3; k++)
            {
                foreach (int side in new[] { 1, -1 })
                {
                    var bp = new Vector3(side * (9f + k * 4f), 1f, -4f - k * 2f);
                    Toon(t, "Pole", Cylinder(bp, bp + Vector3.up * 7f, 0.15f, 6), new Color(0.75f, 0.75f, 0.8f), bp + Vector3.up * 3f, true, 3);
                    var flag = new GameObject("Banner").transform; flag.SetParent(t, false); flag.localPosition = bp + new Vector3(side * 1.2f, 5.6f, 0f);
                    Toon(flag, "Cloth", BoxTris(Vector3.zero, new Vector3(2.2f, 2.6f, 0.08f)), side > 0 ? red : blue, Vector3.back, false, 3);
                    life.Bob(flag, 1.4f + k * 0.2f, k + side, 0.12f);
                }
            }

            // spark showers over both halves
            for (int k = 0; k < 36; k++)
            {
                int side = k % 2 == 0 ? 1 : -1;
                var sp = new GameObject("Spark").transform; sp.SetParent(t, false);
                sp.localPosition = new Vector3(side * (4f + (float)rng.NextDouble() * 16f), 4f + (float)rng.NextDouble() * 16f, -12f + (float)rng.NextDouble() * 18f);
                Glow(sp, "Bit", Sphere(Vector3.zero, Vector3.one * 0.18f, 5, 3), side > 0 ? new Color(2.4f, 1.4f, 0.6f) : new Color(0.8f, 1.6f, 2.6f));
                life.Bob(sp, 0.8f + (float)rng.NextDouble(), (float)rng.NextDouble() * 6f, 1.6f);
            }

            SmallIslands(t, life, rng, new[] { new Vector3(-74f, 10f, -86f), new Vector3(60f, -4f, -40f), new Vector3(96f, 20f, -150f) }, new[] { 0.4f, 0.3f, 0.5f }, dark, rock, new Color(0.4f, 0.2f, 0.5f));
            Clouds(t, life, rng, 6, 12, new Color(0.36f, 0.30f, 0.52f), 0.5f);
        }
    }
}
