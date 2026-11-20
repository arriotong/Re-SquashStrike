// ===========================================================
// Assets/SquashGame/Runtime/Views/Dioramas.Worlds.cs
// 2026-11-20 the four new menu worlds, in the Gauntlet island's toon style,
// each with its own time of day and its squash in it (the user: "every
// background should have squash elements in it"):
//   Home        sunny afternoon - a park island: an outdoor glass court,
//               a giant racket sculpture and ball, the clubhouse, a squash-
//               ball hot-air balloon
//   Career      dusk - a city plateau: towers with lit windows, a stadium
//               with a glass court in its bowl and floodlights, a giant gold
//               trophy, a squash-ball blimp, planes
//   Challenges  night - a stone platform: a giant front wall with glowing
//               target rings, a ball flying at it on its trail, a racket,
//               the clock tower, the moon, fireflies
//   Training    morning - a practice camp: the court, cones, a ladder drill,
//               the ball machine, baskets of balls, the racket rack,
//               mountains behind
// Local coordinates: the camera sits at +z looking back (world +x shows on
// the screen's left), so each world's subject is at the origin and the
// look point to its +x puts it right of the menu. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashGame.ToonKit;

namespace SquashGame
{
    public static partial class Dioramas
    {
        static readonly Vector3 HomeCam = new Vector3(6f, 13f, 74f), HomeLook = new Vector3(22f, 3f, 0f);
        static readonly Vector3 CareerCam = new Vector3(6f, 18f, 104f), CareerLook = new Vector3(27f, 6f, 0f);
        static readonly Vector3 ChallengeCam = new Vector3(6f, 14f, 92f), ChallengeLook = new Vector3(24f, 5f, 0f);
        static readonly Vector3 TrainingCam = new Vector3(6f, 12f, 70f), TrainingLook = new Vector3(21f, 2f, 0f);

        static readonly Color Grass = new Color(0.46f, 0.82f, 0.38f), RockCol = new Color(0.68f, 0.52f, 0.43f), Leaf = new Color(0.26f, 0.64f, 0.34f),
                              Trunk = new Color(0.46f, 0.31f, 0.22f), Wood = new Color(0.86f, 0.62f, 0.38f), Cloud = new Color(1f, 1f, 1f);

        // ---- shared bits ----

        /// <summary>The main island: a grass top over a hanging rock. Returns its rim.</summary>
        static Vector3[] Island(Transform t, float r, Color top, Color rock, System.Random rng, int sides = 24, float depth = 34f)
        {
            Toon(t, "IslandTop", Top(Vector3.zero, r, sides, 1.2f, rng, out var rim), top, Vector3.down * 4f);
            Toon(t, "IslandRock", Rock(Vector3.down * 1.0f, r * 0.97f, depth, sides, rng), rock, Vector3.down * 10f, true);
            return rim;
        }

        static void Trees(Transform t, System.Random rng, int n, float rMin, float rMax, float aFrom, float aTo, System.Func<Vector3, bool> keepOut, Color leaf, float scale = 1f)
        {
            var leaves = new List<Vector3>(); var pines = new List<Vector3>(); var trunks = new List<Vector3>();
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.Lerp(aFrom, aTo, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                var p = new Vector3(Mathf.Cos(a) * rr, 0.6f, Mathf.Sin(a) * rr);
                if (keepOut != null && keepOut(p)) continue;
                float s = (0.8f + (float)rng.NextDouble() * 0.7f) * scale;
                trunks.AddRange(Cone(p, 0.35f * s, 2.0f * s, 5));
                if (k % 3 == 0) leaves.AddRange(Sphere(p + Vector3.up * 2.6f * s, new Vector3(1.7f, 1.5f, 1.7f) * s, 7, 5, rng, 0.25f));
                else { pines.AddRange(Cone(p + Vector3.up * 1.0f * s, 1.6f * s, 3.2f * s, 7)); pines.AddRange(Cone(p + Vector3.up * 2.6f * s, 1.15f * s, 2.6f * s, 7)); }
            }
            Toon(t, "Trunks", trunks, Trunk, Vector3.zero, true, 3);
            Toon(t, "Leaves", leaves, leaf * 1.15f, Vector3.zero, true);
            Toon(t, "Pines", pines, leaf, Vector3.zero, true);
        }

        static void SmallIslands(Transform t, DioramaLife life, System.Random rng, Vector3[] at, float[] size, Color top, Color rock, Color leaf)
        {
            for (int i = 0; i < at.Length; i++)
            {
                var g = new GameObject("SmallIsland").transform; g.SetParent(t, false); g.localPosition = at[i];
                float s = 17f * size[i];
                Toon(g, "Top", Top(Vector3.zero, s, 12, s * 0.12f, rng, out _), top, Vector3.down * 4f * size[i]);
                Toon(g, "Rock", Rock(Vector3.down * 0.9f * size[i], s * 0.97f, 30f * size[i], 12, rng), rock, Vector3.down * 10f * size[i], true);
                var tr = new List<Vector3>();
                int trees = 1 + rng.Next(3);
                for (int k = 0; k < trees; k++)
                {
                    var p = new Vector3(((float)rng.NextDouble() - 0.5f) * s, 0.4f * size[i], ((float)rng.NextDouble() - 0.5f) * s);
                    tr.AddRange(Cone(p + Vector3.up * size[i], 3.2f * size[i], 7.2f * size[i], 6));
                }
                Toon(g, "Pines", tr, leaf, Vector3.zero, true);
                life.Bob(g, 0.6f + (float)rng.NextDouble() * 0.8f, (float)rng.NextDouble() * 6f, 0.35f);
            }
        }

        static void Clouds(Transform t, DioramaLife life, System.Random rng, int high, int low, Color c, float drift = 1f)
        {
            for (int k = 0; k < high + low; k++)
            {
                bool lo = k >= high;
                float x = -220f + (float)rng.NextDouble() * 440f, z = lo ? -20f - (float)rng.NextDouble() * 200f : -60f - (float)rng.NextDouble() * 240f;
                float y = lo ? -40f - (float)rng.NextDouble() * 18f : 22f + (float)rng.NextDouble() * 70f;
                float s = lo ? 14f + (float)rng.NextDouble() * 16f : 5f + (float)rng.NextDouble() * 9f;
                var g = ToonKit.Cloud(t, new Vector3(x, y, z), s, lo ? c * 0.92f : c, rng);
                life.Drift(g.transform, (lo ? 0.5f : 1.1f + (float)rng.NextDouble()) * drift, 230f);
            }
        }

        static void Waterfall(Transform t, DioramaLife life, Vector3 edge)
        {
            var fallMat = Pal.Clear(new Color(0.85f, 0.95f, 1f, 0.75f)); fallMat.mainTexture = FallTexture(); fallMat.mainTextureScale = new Vector2(1f, 3f);
            life.Falls = fallMat;
            Vector3 outDir = new Vector3(edge.x, 0, edge.z).normalized;
            var f = Greybox.MeshObject("Waterfall", t, FallMesh(2.4f, 50f), fallMat);
            f.transform.localPosition = edge + outDir * 0.6f;
            f.transform.localRotation = Quaternion.LookRotation(outDir);
        }

        static void Pyramid(List<Vector3> into, Vector3 baseCentre, float w, float d, float h)
        {
            Vector3 a = baseCentre + new Vector3(-w / 2, 0, -d / 2), b = baseCentre + new Vector3(w / 2, 0, -d / 2), c = baseCentre + new Vector3(w / 2, 0, d / 2), e = baseCentre + new Vector3(-w / 2, 0, d / 2), top = baseCentre + Vector3.up * h;
            Tri(into, a, top, b); Tri(into, b, top, c); Tri(into, c, top, e); Tri(into, e, top, a);
        }

        // =============================== HOME ===============================

        static void BuildHome(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3101);
            Sky(t, "home", new Color(0.78f, 0.90f, 0.98f), new Color(0.88f, 0.95f, 1.0f), new Color(0.52f, 0.77f, 0.98f), new Color(0.24f, 0.50f, 0.93f), 0, 3101);
            Billboard(t, "SunGlow", new Vector3(80f, 95f, -320f), 120f, new Color(1f, 0.97f, 0.85f, 0.6f), HomeCam);
            Billboard(t, "Sun", new Vector3(80f, 95f, -319f), 22f, new Color(1.8f, 1.7f, 1.3f), HomeCam, false);
            var rim = Island(t, 26f, Grass, RockCol, rng);

            // the outdoor glass court, the path to the clubhouse
            GlassCourt(t, new Vector3(-1f, 1.05f, 3f), 12f, 0.95f, Wood);
            var path = new List<(List<Vector3>, Vector3)>();
            for (int k = 0; k < 7; k++) { var p = new Vector3(-9f - k * 1.1f, 1.15f, -2f - k * 1.0f); path.Add((BoxTris(p, new Vector3(1.3f, 0.12f, 0.9f), Quaternion.Euler(0, 30f + k * 7f, 0)), p)); }
            ToonParts(t, "Path", path, new Color(0.85f, 0.82f, 0.74f), 3);
            // the clubhouse: cream walls, a red roof, lit windows, a sign
            var house = new List<(List<Vector3>, Vector3)> { (BoxTris(new Vector3(-17f, 3.2f, -10f), new Vector3(10f, 4.4f, 7f)), new Vector3(-17f, 3.2f, -10f)) };
            ToonParts(t, "Clubhouse", house, new Color(0.96f, 0.91f, 0.80f));
            var roof = new List<Vector3>(); Pyramid(roof, new Vector3(-17f, 5.4f, -10f), 11.4f, 8.4f, 3.4f);
            Toon(t, "Roof", roof, new Color(0.85f, 0.32f, 0.26f), new Vector3(-17f, 6f, -10f));
            var win = new List<Vector3>();
            for (int k = 0; k < 3; k++) win.AddRange(BoxTris(new Vector3(-20.5f + k * 3.5f, 3.6f, -6.45f), new Vector3(1.4f, 1.3f, 0.1f)));
            Glow(t, "Windows", win, new Color(0.65f, 0.85f, 1.1f));
            Glow(t, "Sign", BoxTris(new Vector3(-17f, 5.0f, -6.4f), new Vector3(5f, 0.8f, 0.12f)), new Color(0.78f, 1.0f, 0.24f));
            // the giant racket sculpture and its ball
            Racket(t, new Vector3(12f, 1.2f, -9f), new Vector3(-0.28f, 1f, 0.05f), new Vector3(0.15f, 0.1f, 1f), 21f, new Color(0.95f, 0.32f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            Toon(t, "Plinth", Cylinder(new Vector3(12f, 0.6f, -9f), new Vector3(12f, 1.4f, -9f), 2.2f, 10), new Color(0.82f, 0.80f, 0.76f), new Vector3(12f, 1f, -9f));
            SquashBall(t, new Vector3(5.5f, 3.6f, -12f), 3.1f, new Vector3(0.3f, 0.2f, 1f));
            // a flag on a pole by the court
            Toon(t, "Pole", Cylinder(new Vector3(-8f, 1f, 9f), new Vector3(-8f, 10f, 9f), 0.12f, 6), new Color(0.85f, 0.85f, 0.88f), new Vector3(-8f, 5f, 9f));
            var flag = new List<Vector3>(); Tri(flag, new Vector3(-8f, 10f, 9f), new Vector3(-8f, 8f, 9f), new Vector3(-11.5f, 9f, 9f));
            Glow(t, "Pennant", flag, new Color(0.78f, 1.0f, 0.24f));

            Trees(t, rng, 26, 14f, 24f, -220f, 40f, p => (p - new Vector3(-1f, 0, 3f)).magnitude < 9f || (p - new Vector3(-17f, 0, -10f)).magnitude < 7.5f || (p - new Vector3(12f, 0, -9f)).magnitude < 4f, Leaf);
            Waterfall(t, life, rim[19]); Waterfall(t, life, rim[3]);

            // the hot-air balloon: a squash ball, its basket and ropes
            var bal = new GameObject("Balloon").transform; bal.SetParent(t, false); bal.localPosition = new Vector3(-36f, 30f, -26f);
            SquashBall(bal, Vector3.zero, 6.5f, new Vector3(0.4f, 0f, 1f));
            ToonParts(bal, "Basket", new List<(List<Vector3>, Vector3)> { (BoxTris(new Vector3(0, -10.5f, 0), new Vector3(2.4f, 1.6f, 2.4f)), new Vector3(0, -10.5f, 0)) }, new Color(0.72f, 0.5f, 0.3f));
            var ropes = new List<Vector3>();
            foreach (var c in new[] { new Vector3(1.1f, 0, 1.1f), new Vector3(-1.1f, 0, 1.1f), new Vector3(1.1f, 0, -1.1f), new Vector3(-1.1f, 0, -1.1f) })
                ropes.AddRange(Cylinder(c + Vector3.down * 9.7f, c * 2.6f + Vector3.down * 5.6f, 0.06f, 4));
            Glow(bal, "Ropes", ropes, new Color(0.35f, 0.3f, 0.25f));
            life.Bob(bal, 0.35f, 0f, 1.6f);

            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 14f, -80f), new Vector3(52f, -4f, -36f), new Vector3(92f, 24f, -140f), new Vector3(-130f, 30f, -180f), new Vector3(30f, 34f, -200f) },
                         new[] { 0.42f, 0.3f, 0.5f, 0.6f, 0.38f }, Grass, RockCol, Leaf);
            Clouds(t, life, rng, 12, 18, Cloud);
        }

        // =============================== CAREER ===============================

        static void BuildCareer(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3202);
            Sky(t, "career", new Color(0.42f, 0.30f, 0.52f), new Color(1.0f, 0.55f, 0.34f), new Color(0.48f, 0.30f, 0.62f), new Color(0.07f, 0.07f, 0.24f), 90, 3202);
            Billboard(t, "SunsetGlow", new Vector3(-110f, -2f, -330f), 240f, new Color(1f, 0.55f, 0.3f, 0.6f), CareerCam);
            Island(t, 30f, new Color(0.52f, 0.54f, 0.58f), new Color(0.56f, 0.45f, 0.48f), rng, 28, 38f);

            // the towers round the back, their windows coming on
            var towers = new List<(List<Vector3>, Vector3)>(); var towers2 = new List<(List<Vector3>, Vector3)>();
            var windows = new List<Vector3>(); var beacons = new List<Vector3>();
            for (int k = 0; k < 26; k++)
            {
                float a = Mathf.Lerp(-200f, 20f, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = 13f + (float)rng.NextDouble() * 14f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                if ((p - new Vector3(-1f, 0, 7f)).magnitude < 12.5f || (p - new Vector3(15f, 0, 12f)).magnitude < 4f) continue;
                float w = 3.5f + (float)rng.NextDouble() * 3.5f, d = 3.5f + (float)rng.NextDouble() * 3.5f, h = 8f + (float)(rng.NextDouble() * rng.NextDouble()) * 34f;
                var c = new Vector3(p.x, 1f + h / 2, p.z);
                (k % 2 == 0 ? towers : towers2).Add((BoxTris(c, new Vector3(w, h, d)), c));
                if (h > 22f)
                {
                    var c2 = new Vector3(p.x, 1f + h + h * 0.12f, p.z);
                    (k % 2 == 0 ? towers : towers2).Add((BoxTris(c2, new Vector3(w * 0.6f, h * 0.24f, d * 0.6f)), c2));
                    beacons.AddRange(Sphere(new Vector3(p.x, 1f + h * 1.28f, p.z), Vector3.one * 0.45f, 6, 4));
                }
                for (float y = 2.6f; y < h - 0.5f; y += 1.6f)
                    for (float x = -w / 2 + 0.7f; x < w / 2 - 0.4f; x += 1.1f)
                        if (rng.NextDouble() < 0.42) windows.AddRange(BoxTris(new Vector3(p.x + x, 1f + y, p.z + d / 2 + 0.03f), new Vector3(0.55f, 0.7f, 0.05f)));
            }
            ToonParts(t, "Towers", towers, new Color(0.40f, 0.46f, 0.64f));
            ToonParts(t, "Towers2", towers2, new Color(0.56f, 0.48f, 0.64f));
            Glow(t, "Windows", windows, new Color(1.7f, 1.3f, 0.6f));
            var beacon = Glow(t, "Beacons", beacons, new Color(2.6f, 0.25f, 0.2f));
            life.Blink(beacon.GetComponent<MeshRenderer>().sharedMaterial, 2.4f, 0f);

            // the stadium: a bowl of stands, the glass court in it, four floodlights
            Vector3 sc = new Vector3(-1f, 1f, 7f);
            var bowl = new List<(List<Vector3>, Vector3)>();
            for (int k = 0; k < 28; k++)
            {
                float a = k / 28f * 360f;
                if (a > 60f && a < 120f) continue;                                              // open toward the camera
                var rot = Quaternion.Euler(0, -a, 0);
                var c = sc + rot * new Vector3(0, 2.2f, 10.5f);
                bowl.Add((BoxTris(c, new Vector3(2.5f, 4.4f, 3.2f), rot * Quaternion.Euler(-24f, 0, 0)), c));
            }
            ToonParts(t, "Stands", bowl, new Color(0.92f, 0.92f, 0.96f));
            var crowd = new List<Vector3>();
            for (int k = 0; k < 240; k++)
            {
                float a = (float)rng.NextDouble() * 360f;
                if (a > 60f && a < 120f) continue;
                var rot = Quaternion.Euler(0, -a, 0);
                float back = (float)rng.NextDouble();
                crowd.AddRange(BoxTris(sc + rot * new Vector3(0, 1.6f + back * 2.6f, 9.4f + back * 2.2f), new Vector3(0.35f, 0.35f, 0.35f)));
            }
            Glow(t, "Crowd", crowd, new Color(1.2f, 0.75f, 0.6f));
            GlassCourt(t, sc + Vector3.up * 0.1f, 90f, 0.82f, Wood);
            var lamps = new List<Vector3>(); var masts = new List<Vector3>();
            foreach (var m in new[] { new Vector3(-10f, 0, -3f), new Vector3(8f, 0, -3f), new Vector3(-10f, 0, 17f), new Vector3(8f, 0, 17f) })
            {
                masts.AddRange(Cylinder(sc + m, sc + m + Vector3.up * 13f, 0.25f, 6));
                lamps.AddRange(BoxTris(sc + m + Vector3.up * 13.4f, new Vector3(2.4f, 1.4f, 0.6f), Quaternion.LookRotation(-m.normalized)));
                Billboard(t, "FloodBloom", sc + m + Vector3.up * 13.5f, 9f, new Color(1f, 0.95f, 0.8f, 0.55f), CareerCam);
            }
            Toon(t, "Masts", masts, new Color(0.62f, 0.64f, 0.7f), sc, true, 3);
            Glow(t, "Floodlights", lamps, new Color(2.6f, 2.5f, 2.1f));

            // the giant gold trophy
            Vector3 tp = new Vector3(16f, 1f, 13f); Color gold = new Color(1.0f, 0.74f, 0.24f);
            Toon(t, "TrophyBase", BoxTris(tp + Vector3.up * 1.2f, new Vector3(3.6f, 2.4f, 3.6f)), new Color(0.25f, 0.2f, 0.2f), tp + Vector3.up * 1.2f);
            Toon(t, "TrophyStem", Cylinder(tp + Vector3.up * 2.4f, tp + Vector3.up * 5.4f, 0.55f, 10), gold, tp + Vector3.up * 4f, true);
            Toon(t, "TrophyCup", Sphere(tp + Vector3.up * 7.6f, new Vector3(2.4f, 2.6f, 2.4f), 14, 10), gold, tp + Vector3.up * 7.6f, false, 5);
            Toon(t, "TrophyRim", Ring(tp + Vector3.up * 9.8f, Vector3.up, 1.7f, 2.4f, 18), gold * 1.1f, tp + Vector3.up * 9f);
            var handles = new List<Vector3>();
            foreach (int sd in new[] { -1, 1 })
                for (int k = 0; k < 10; k++)
                {
                    float a0 = Mathf.PI * (k / 10f - 0.5f), a1 = Mathf.PI * ((k + 1) / 10f - 0.5f);
                    handles.AddRange(Cylinder(tp + new Vector3(sd * (2.3f + Mathf.Cos(a0) * 1.1f), 7.8f + Mathf.Sin(a0) * 1.4f, 0), tp + new Vector3(sd * (2.3f + Mathf.Cos(a1) * 1.1f), 7.8f + Mathf.Sin(a1) * 1.4f, 0), 0.22f, 5));
                }
            Toon(t, "TrophyHandles", handles, gold, tp + Vector3.up * 7.8f);
            Billboard(t, "TrophyShine", tp + Vector3.up * 8f, 9f, new Color(1f, 0.85f, 0.4f, 0.4f), CareerCam);

            // the squash-ball blimp and two little planes
            var blimp = new GameObject("Blimp").transform; blimp.SetParent(t, false); blimp.localPosition = new Vector3(-30f, 34f, -40f);
            Toon(blimp, "Body", Sphere(Vector3.zero, new Vector3(9f, 3.6f, 3.6f), 18, 10), new Color(0.22f, 0.22f, 0.27f), Vector3.zero, false, 5);
            var dots = new List<Vector3>(); dots.AddRange(Sphere(new Vector3(-2.2f, 1.6f, 3.0f), Vector3.one * 0.5f, 8, 5)); dots.AddRange(Sphere(new Vector3(-0.8f, 2.4f, 2.6f), Vector3.one * 0.5f, 8, 5));
            Glow(blimp, "Dots", dots, new Color(1.6f, 1.35f, 0.15f));
            var fins = new List<Vector3>(); fins.AddRange(BoxTris(new Vector3(9f, 0, 0), new Vector3(2.6f, 0.2f, 4.4f))); fins.AddRange(BoxTris(new Vector3(9f, 0, 0), new Vector3(2.6f, 4.4f, 0.2f)));
            Toon(blimp, "Fins", fins, new Color(0.78f, 1.0f, 0.24f), new Vector3(9f, 0, 0), false, 3);
            Glow(blimp, "Gondola", BoxTris(new Vector3(0, -3.9f, 0), new Vector3(3f, 0.9f, 1.2f)), new Color(1.5f, 1.2f, 0.6f));
            life.Drift(blimp, -1.2f, 120f); life.Bob(blimp, 0.3f, 1f, 0.8f);
            for (int k = 0; k < 2; k++)
            {
                var pl = new GameObject("Plane").transform; pl.SetParent(t, false); pl.localPosition = new Vector3(-150f + k * 170f, 55f + k * 18f, -150f - k * 60f);
                var body = new List<Vector3>(); body.AddRange(BoxTris(Vector3.zero, new Vector3(5f, 0.7f, 0.7f))); body.AddRange(BoxTris(Vector3.zero, new Vector3(1.0f, 0.15f, 6f))); body.AddRange(BoxTris(new Vector3(2.3f, 0.6f, 0), new Vector3(0.6f, 1.2f, 0.15f)));
                Toon(pl, "Body", body, new Color(0.9f, 0.92f, 0.96f), Vector3.zero, false, 3);
                var light = Glow(pl, "Light", Sphere(new Vector3(0, 0, 3.1f), Vector3.one * 0.3f, 6, 4), new Color(2.6f, 0.3f, 0.2f));
                life.Blink(light.GetComponent<MeshRenderer>().sharedMaterial, 3.2f, k);
                life.Drift(pl, 3.5f + k, 230f);
            }
            SmallIslands(t, life, rng, new[] { new Vector3(-74f, 10f, -80f), new Vector3(56f, -6f, -40f), new Vector3(96f, 22f, -150f), new Vector3(-126f, 26f, -170f) },
                         new[] { 0.4f, 0.3f, 0.5f, 0.55f }, new Color(0.42f, 0.62f, 0.40f), new Color(0.56f, 0.45f, 0.48f), new Color(0.24f, 0.46f, 0.36f));
            Clouds(t, life, rng, 8, 16, new Color(1.0f, 0.72f, 0.70f), 0.7f);
        }

        // =============================== CHALLENGES ===============================

        static void BuildChallenges(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3303);
            Sky(t, "challenges", new Color(0.10f, 0.10f, 0.24f), new Color(0.26f, 0.20f, 0.50f), new Color(0.09f, 0.09f, 0.28f), new Color(0.02f, 0.02f, 0.09f), 420, 3303);
            Billboard(t, "MoonHalo", new Vector3(90f, 85f, -320f), 110f, new Color(0.7f, 0.8f, 1f, 0.45f), ChallengeCam);
            Billboard(t, "Moon", new Vector3(90f, 85f, -319f), 26f, new Color(1.5f, 1.5f, 1.35f), ChallengeCam, false);
            Island(t, 22f, new Color(0.46f, 0.44f, 0.64f), new Color(0.36f, 0.32f, 0.50f), rng, 6, 30f);
            // stone tiles on the platform
            var tiles = new List<Vector3>();
            for (float x = -14f; x <= 14f; x += 3.2f) for (float z = -12f; z <= 14f; z += 3.2f) if (x * x + z * z < 260f && rng.NextDouble() < 0.5) tiles.AddRange(BoxTris(new Vector3(x, 1.25f, z), new Vector3(2.9f, 0.08f, 2.9f)));
            Toon(t, "Tiles", tiles, new Color(0.56f, 0.54f, 0.74f), new Vector3(0, -5f, 0), false, 3);

            // the giant front wall, its tin and lines, glowing targets on it
            Vector3 wc = new Vector3(0f, 7f, -9f);
            ToonParts(t, "FrontWall", new List<(List<Vector3>, Vector3)> { (BoxTris(wc, new Vector3(20f, 12f, 1.2f)), wc) }, new Color(0.90f, 0.90f, 0.98f));
            var red = new Color(2.2f, 0.35f, 0.3f);
            Glow(t, "Tin", BoxTris(new Vector3(0, 2.0f, -8.35f), new Vector3(20f, 0.35f, 0.1f)), red);
            Glow(t, "ServiceLine", BoxTris(new Vector3(0, 5.6f, -8.35f), new Vector3(20f, 0.25f, 0.1f)), red);
            Glow(t, "OutLine", BoxTris(new Vector3(0, 12.6f, -8.35f), new Vector3(20f, 0.25f, 0.1f)), red);
            Color[] ring = { new Color(0.8f, 2.2f, 0.4f), new Color(0.4f, 1.6f, 2.6f), new Color(2.2f, 0.6f, 2.0f) };
            Vector3[] at = { new Vector3(6f, 9f, -8.3f), new Vector3(-5f, 3.6f, -8.3f), new Vector3(-6.5f, 9.5f, -8.3f) };
            for (int k = 0; k < 3; k++)
            {
                var g = new List<Vector3>(); g.AddRange(Ring(at[k], Vector3.forward, 1.6f, 2.0f, 28)); g.AddRange(Ring(at[k], Vector3.forward, 0.6f, 0.95f, 20));
                var r = Glow(t, "Target", g, ring[k]);
                life.Blink(r.GetComponent<MeshRenderer>().sharedMaterial, 1.6f + k * 0.4f, k * 2f);
            }
            // a ball flying at it, its trail from the racket
            Vector3 ball = new Vector3(2.5f, 8.0f, -1f);
            SquashBall(t, ball, 1.5f, new Vector3(-0.2f, 0.2f, 1f));
            Vector3 from = new Vector3(-9f, 3.5f, 11f);
            var trail = new List<Vector3>();
            for (int k = 1; k < 14; k++)
            {
                float s = k / 14f; Vector3 p = Vector3.Lerp(from, ball, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 2.6f;
                trail.AddRange(Sphere(p, Vector3.one * (0.12f + 0.3f * s), 6, 4));
            }
            var tr = Glow(t, "Trail", trail, new Color(1.6f, 1.4f, 0.4f));
            life.Blink(tr.GetComponent<MeshRenderer>().sharedMaterial, 2.2f, 0.5f);
            Racket(t, new Vector3(-12f, 1.6f, 12.5f), new Vector3(0.55f, 0.75f, -0.3f), new Vector3(0.5f, 0f, 1f), 9f, new Color(0.3f, 0.75f, 1.0f), new Color(0.12f, 0.12f, 0.14f));

            // the clock tower
            Vector3 ct = new Vector3(15f, 1f, -4f);
            ToonParts(t, "ClockTower", new List<(List<Vector3>, Vector3)> { (BoxTris(ct + Vector3.up * 8f, new Vector3(4.2f, 16f, 4.2f)), ct + Vector3.up * 8f) }, new Color(0.58f, 0.54f, 0.72f));
            var roof = new List<Vector3>(); Pyramid(roof, ct + Vector3.up * 16f, 5.2f, 5.2f, 5f);
            Toon(t, "ClockRoof", roof, new Color(0.38f, 0.30f, 0.56f), ct + Vector3.up * 17f);
            var face = Greybox.MeshObject("ClockFace", t, Greybox.Disc(1f, 32), Flat(new Color(1.6f, 1.45f, 1.0f)));
            face.transform.localPosition = ct + new Vector3(0, 12.5f, 2.13f); face.transform.localRotation = Quaternion.LookRotation(Vector3.back); face.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            var hand = new GameObject("ClockHand").transform; hand.SetParent(t, false); hand.localPosition = ct + new Vector3(0, 12.5f, 2.2f);
            Glow(hand, "Hand", BoxTris(new Vector3(0, 0.6f, 0), new Vector3(0.12f, 1.25f, 0.05f)), new Color(0.1f, 0.1f, 0.15f));
            life.Spin(hand, Vector3.forward, -24f);

            // glowing stones and fireflies
            var crystals = new List<Vector3>();
            for (int k = 0; k < 9; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = 15f + (float)rng.NextDouble() * 5f;
                var p = new Vector3(Mathf.Cos(a) * rr, 1f, Mathf.Sin(a) * rr);
                if (p.z > 8f && Mathf.Abs(p.x) < 8f) continue;
                crystals.AddRange(Cone(p, 0.5f + (float)rng.NextDouble() * 0.4f, 1.8f + (float)rng.NextDouble() * 1.6f, 5));
            }
            var cr = Glow(t, "Crystals", crystals, new Color(0.5f, 1.5f, 2.4f));
            life.Blink(cr.GetComponent<MeshRenderer>().sharedMaterial, 0.9f, 1f);
            for (int k = 0; k < 18; k++)
            {
                var ff = new GameObject("Firefly").transform; ff.SetParent(t, false);
                ff.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 40f, 2f + (float)rng.NextDouble() * 10f, ((float)rng.NextDouble() - 0.5f) * 30f);
                Glow(ff, "Glow", Sphere(Vector3.zero, Vector3.one * 0.14f, 5, 3), new Color(2.2f, 2.0f, 0.6f));
                life.Bob(ff, 0.8f + (float)rng.NextDouble(), (float)rng.NextDouble() * 6f, 0.8f);
            }
            SmallIslands(t, life, rng, new[] { new Vector3(-66f, 12f, -70f), new Vector3(52f, -6f, -30f), new Vector3(90f, 26f, -140f), new Vector3(-120f, 30f, -170f) },
                         new[] { 0.36f, 0.28f, 0.48f, 0.55f }, new Color(0.40f, 0.42f, 0.62f), new Color(0.32f, 0.30f, 0.48f), new Color(0.22f, 0.36f, 0.42f));
            Clouds(t, life, rng, 6, 16, new Color(0.42f, 0.44f, 0.72f), 0.6f);
        }

        // =============================== TRAINING ===============================

        static void BuildTraining(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3404);
            Sky(t, "training", new Color(0.86f, 0.92f, 0.96f), new Color(1.0f, 0.93f, 0.80f), new Color(0.62f, 0.83f, 0.98f), new Color(0.32f, 0.60f, 0.95f), 0, 3404);
            Billboard(t, "SunGlow", new Vector3(-120f, 40f, -320f), 150f, new Color(1f, 0.92f, 0.7f, 0.6f), TrainingCam);
            Billboard(t, "Sun", new Vector3(-120f, 40f, -319f), 24f, new Color(1.9f, 1.7f, 1.2f), TrainingCam, false);
            // mountains behind, snow on top
            var mts = new List<(List<Vector3>, Vector3)>(); var snow = new List<(List<Vector3>, Vector3)>();
            for (int k = 0; k < 9; k++)
            {
                var b = new Vector3(-220f + k * 55f + (float)rng.NextDouble() * 20f, -50f, -220f - (float)rng.NextDouble() * 60f);
                float r = 50f + (float)rng.NextDouble() * 30f, h = 110f + (float)rng.NextDouble() * 70f;
                mts.Add((Cone(b, r, h, 7), b + Vector3.up * h * 0.3f));
                float sh = h * 0.28f, sr = r * 0.28f;
                snow.Add((Cone(b + Vector3.up * (h - sh), sr, sh + 0.5f, 7), b + Vector3.up * (h - sh * 0.6f)));
            }
            ToonParts(t, "Mountains", mts, new Color(0.52f, 0.60f, 0.76f));
            ToonParts(t, "Snow", snow, new Color(0.98f, 0.98f, 1.0f));
            var rim = Island(t, 27f, new Color(0.50f, 0.85f, 0.40f), RockCol, rng);

            // the practice court with targets on its front wall
            var court = GlassCourt(t, new Vector3(-2f, 1.05f, 2f), 8f, 1.0f, Wood);
            var tg = new List<Vector3>();
            foreach (var p in new[] { new Vector3(-2f, 2.2f, -4.8f), new Vector3(2f, 3.2f, -4.8f), new Vector3(0f, 1.1f, -4.8f) })
            { tg.AddRange(Ring(p, Vector3.forward, 0.55f, 0.72f, 24)); tg.AddRange(Ring(p, Vector3.forward, 0.18f, 0.3f, 16)); }
            var tgo = Glow(court, "Targets", tg, new Color(0.78f, 1.0f, 0.24f));
            // cones in a zigzag and a ladder drill on the grass
            var cones = new List<(List<Vector3>, Vector3)>();
            for (int k = 0; k < 8; k++) { var p = new Vector3(12f + (k % 2) * 2.2f, 1.1f, 12f - k * 2.4f); cones.Add((Cone(p, 0.45f, 1.0f, 8), p + Vector3.up * 0.3f)); }
            ToonParts(t, "Cones", cones, new Color(1.0f, 0.55f, 0.15f), 3);
            var ladder = new List<Vector3>();
            for (int k = 0; k < 9; k++) ladder.AddRange(BoxTris(new Vector3(-14f, 1.18f, 14f - k * 1.1f), new Vector3(2.2f, 0.06f, 0.12f)));
            ladder.AddRange(BoxTris(new Vector3(-15.1f, 1.18f, 9.6f), new Vector3(0.12f, 0.06f, 9.8f))); ladder.AddRange(BoxTris(new Vector3(-12.9f, 1.18f, 9.6f), new Vector3(0.12f, 0.06f, 9.8f)));
            Glow(t, "Ladder", ladder, new Color(1.6f, 1.4f, 0.2f));
            // the ball machine, baskets of balls, the racket rack
            Vector3 bm = new Vector3(6f, 1.1f, 13f);
            ToonParts(t, "BallMachine", new List<(List<Vector3>, Vector3)> { (BoxTris(bm + Vector3.up * 0.7f, new Vector3(1.6f, 1.4f, 1.3f)), bm + Vector3.up * 0.7f) }, new Color(0.25f, 0.55f, 0.95f));
            Toon(t, "Chute", Cylinder(bm + new Vector3(0, 1.3f, -0.3f), bm + new Vector3(0, 1.9f, -1.6f), 0.28f, 8), new Color(0.2f, 0.2f, 0.24f), bm + new Vector3(0, 1.6f, -1f), true, 3);
            Toon(t, "Hopper", Cylinder(bm + Vector3.up * 1.4f, bm + Vector3.up * 2.3f, 0.8f, 10), new Color(0.85f, 0.88f, 0.92f), bm + Vector3.up * 1.8f, true, 3);
            var balls = new List<Vector3>();
            for (int k = 0; k < 14; k++) balls.AddRange(Sphere(bm + new Vector3(((float)rng.NextDouble() - 0.5f) * 1.1f, 2.3f + (float)rng.NextDouble() * 0.3f, ((float)rng.NextDouble() - 0.5f) * 1.1f), Vector3.one * 0.22f, 6, 4));
            foreach (var bk in new[] { new Vector3(9.5f, 1.1f, 15.5f), new Vector3(-9f, 1.1f, 16f) })
            {
                Toon(t, "Basket", Cylinder(bk, bk + Vector3.up * 1.0f, 0.9f, 10), new Color(0.75f, 0.78f, 0.84f), bk + Vector3.up * 0.5f, true, 3);
                for (int k = 0; k < 12; k++) balls.AddRange(Sphere(bk + new Vector3(((float)rng.NextDouble() - 0.5f) * 1.2f, 1.0f + (float)rng.NextDouble() * 0.35f, ((float)rng.NextDouble() - 0.5f) * 1.2f), Vector3.one * 0.22f, 6, 4));
            }
            Toon(t, "Balls", balls, new Color(0.2f, 0.2f, 0.24f), bm, false, 3);
            var rack = new List<Vector3>(); rack.AddRange(Cylinder(new Vector3(-6f, 1.1f, 15f), new Vector3(-6f, 3.2f, 15f), 0.1f, 6)); rack.AddRange(Cylinder(new Vector3(-1f, 1.1f, 15f), new Vector3(-1f, 3.2f, 15f), 0.1f, 6)); rack.AddRange(Cylinder(new Vector3(-6f, 3.2f, 15f), new Vector3(-1f, 3.2f, 15f), 0.1f, 6));
            Toon(t, "Rack", rack, new Color(0.7f, 0.5f, 0.32f), new Vector3(-3.5f, 2f, 15f), false, 3);
            Color[] frames = { new Color(0.95f, 0.32f, 0.24f), new Color(0.3f, 0.75f, 1.0f), new Color(0.78f, 1.0f, 0.24f), new Color(1.0f, 0.75f, 0.25f) };
            for (int k = 0; k < 4; k++) Racket(t, new Vector3(-5.5f + k * 1.25f, 1.15f, 15.25f), new Vector3(0.05f, 1f, 0.1f), Vector3.forward, 2.6f, frames[k], new Color(0.12f, 0.12f, 0.14f));
            // a little gazebo, trees, a waterfall
            var gz = new List<Vector3>(); Pyramid(gz, new Vector3(16f, 4.4f, -6f), 6f, 6f, 2.6f);
            Toon(t, "GazeboRoof", gz, new Color(0.95f, 0.95f, 0.98f), new Vector3(16f, 5f, -6f));
            var posts = new List<Vector3>(); foreach (var c in new[] { new Vector3(13.4f, 0, -8.6f), new Vector3(18.6f, 0, -8.6f), new Vector3(13.4f, 0, -3.4f), new Vector3(18.6f, 0, -3.4f) }) posts.AddRange(Cylinder(c + Vector3.up * 1f, c + Vector3.up * 4.4f, 0.14f, 6));
            Toon(t, "GazeboPosts", posts, new Color(0.25f, 0.55f, 0.95f), new Vector3(16f, 2.5f, -6f), true, 3);
            Trees(t, rng, 24, 15f, 25f, -220f, 30f, p => (p - new Vector3(-2f, 0, 2f)).magnitude < 9.5f || (p - new Vector3(16f, 0, -6f)).magnitude < 5f, Leaf);
            Waterfall(t, life, rim[20]);
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) },
                         new[] { 0.4f, 0.3f, 0.5f }, new Color(0.50f, 0.85f, 0.40f), RockCol, Leaf);
            Clouds(t, life, rng, 10, 16, Cloud, 0.8f);
        }
    }
}
