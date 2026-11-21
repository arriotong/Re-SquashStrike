// ===========================================================
// Assets/SquashGame/Runtime/Views/Dioramas.Worlds2.cs
// 2026-11-21 a world for every hub tab (the user: "a few of them were
// repeated - I want separate ones for every single mode", and the Others
// hub too), each its own place, time and squash:
//   Modes    golden-pink afternoon - a fairground: a ferris wheel of
//            squash-ball cabins, a big top, a carousel on racket poles
//   Online   blue hour - four little islands with glass courts, joined by
//            glowing bridges, a radio tower, glowing balls in flight
//   Packs    tropical midday - a beach island with a lagoon and palms,
//            giant wrapped packs, a chest of coins and squash balls
//   Store    pastel dawn - a floating market: striped stalls, the racket
//            shop and its giant sign, racket stands, ball balloons
//   You      autumn - orange trees, the cabin locker room, lockers, a medal
//            podium, a giant squash shoe, a racket bag, falling leaves
//   Replays  magenta twilight - an open-air cinema: a giant screen with a
//            court on it, the projector's beam, reels, seats, popcorn
// Same toolkit and layout rules as Dioramas.Worlds.cs. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashGame.ToonKit;

namespace SquashGame
{
    public static partial class Dioramas
    {
        static readonly Vector3 StdCam = new Vector3(6f, 13f, 76f), StdLook = new Vector3(22f, 3f, 0f);
        static readonly Vector3 NearCam = new Vector3(6f, 12f, 64f), NearLook = new Vector3(19f, 3f, 0f);   // (the smaller set pieces)

        static List<(List<Vector3>, Vector3)> Part(List<Vector3> tris, Vector3 centre) { return new List<(List<Vector3>, Vector3)> { (tris, centre) }; }

        // =============================== MODES: the fairground ===============================

        static void BuildModes(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3505);
            Sky(t, "modes", new Color(1.0f, 0.78f, 0.70f), new Color(1.0f, 0.70f, 0.45f), new Color(0.95f, 0.55f, 0.70f), new Color(0.40f, 0.30f, 0.70f), 20, 3505);
            Billboard(t, "SunGlow", new Vector3(-100f, 30f, -320f), 180f, new Color(1f, 0.7f, 0.45f, 0.6f), StdCam);
            Billboard(t, "Sun", new Vector3(-100f, 30f, -319f), 24f, new Color(2.0f, 1.3f, 0.6f), StdCam, false);
            Island(t, 26f, new Color(0.52f, 0.80f, 0.40f), RockCol, rng);

            // the ferris wheel: a rim, spokes, squash-ball cabins - turning slowly
            Vector3 wc = new Vector3(6f, 15f, -8f);
            var legs = new List<Vector3>();
            foreach (int sd in new[] { -1, 1 }) { legs.AddRange(Cylinder(new Vector3(wc.x + sd * 6f, 1f, wc.z - 1.5f), wc, 0.35f, 6)); legs.AddRange(Cylinder(new Vector3(wc.x + sd * 6f, 1f, wc.z + 1.5f), wc, 0.35f, 6)); }
            Toon(t, "WheelLegs", legs, new Color(0.95f, 0.95f, 0.98f), wc + Vector3.down * 6f, true, 3);
            var wheel = new GameObject("Wheel").transform; wheel.SetParent(t, false); wheel.localPosition = wc;
            var rim = new List<Vector3>(); rim.AddRange(Ring(Vector3.zero, Vector3.forward, 11.4f, 12f, 40)); rim.AddRange(Ring(new Vector3(0, 0, 0.6f), Vector3.forward, 11.4f, 12f, 40));
            for (int k = 0; k < 12; k++) { float a = k * 30f * Mathf.Deg2Rad; rim.AddRange(Cylinder(Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 11.7f, 0.12f, 4)); }
            Toon(wheel, "Rim", rim, new Color(1.0f, 0.35f, 0.45f), new Vector3(0, 0, -3f), false, 3);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                SquashBall(wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 12f + new Vector3(0, 0, 1.2f), 1.1f, Vector3.forward);
            }
            var bulbs = new List<Vector3>();
            for (int k = 0; k < 36; k++) { float a = k * 10f * Mathf.Deg2Rad; bulbs.AddRange(Sphere(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 12.2f + new Vector3(0, 0, 0.7f), Vector3.one * 0.22f, 5, 3)); }
            var bl = Glow(wheel, "Bulbs", bulbs, new Color(2.2f, 1.9f, 0.9f));
            life.Blink(bl.GetComponent<MeshRenderer>().sharedMaterial, 3f, 0f);
            life.Spin(wheel, Vector3.forward, 6f);

            // the big top: striped walls, a red-and-white cone roof, its flag
            Vector3 tc = new Vector3(-12f, 1f, -6f);
            var stripesA = new List<Vector3>(); var stripesB = new List<Vector3>();
            for (int k = 0; k < 16; k++)
            {
                float a0 = k / 16f * Mathf.PI * 2, a1 = (k + 1) / 16f * Mathf.PI * 2;
                var wall = new List<Vector3>();
                Vector3 p0 = tc + new Vector3(Mathf.Cos(a0) * 6f, 0, Mathf.Sin(a0) * 6f), p1 = tc + new Vector3(Mathf.Cos(a1) * 6f, 0, Mathf.Sin(a1) * 6f);
                Tri(wall, p0, p0 + Vector3.up * 4f, p1); Tri(wall, p1, p0 + Vector3.up * 4f, p1 + Vector3.up * 4f);
                Tri(wall, p0 + Vector3.up * 4f, tc + Vector3.up * 9f, p1 + Vector3.up * 4f);
                (k % 2 == 0 ? stripesA : stripesB).AddRange(wall);
            }
            Toon(t, "TentRed", stripesA, new Color(0.95f, 0.25f, 0.30f), tc + Vector3.up * 3f, true);
            Toon(t, "TentWhite", stripesB, new Color(0.98f, 0.96f, 0.92f), tc + Vector3.up * 3f, true);
            var flag = new List<Vector3>(); Tri(flag, tc + Vector3.up * 11f, tc + Vector3.up * 9.6f, tc + new Vector3(-2.4f, 10.3f, 0));
            Glow(t, "TentFlag", flag, new Color(0.78f, 1.0f, 0.24f));
            Toon(t, "TentPole", Cylinder(tc + Vector3.up * 8.5f, tc + Vector3.up * 11f, 0.1f, 5), new Color(0.9f, 0.9f, 0.9f), tc + Vector3.up * 10f, true, 3);

            // the carousel: a disc, racket poles round it, a striped canopy, turning
            var car = new GameObject("Carousel").transform; car.SetParent(t, false); car.localPosition = new Vector3(-6f, 1.1f, 10f);
            Toon(car, "Base", Cylinder(Vector3.zero, Vector3.up * 0.5f, 4.2f, 18), new Color(0.98f, 0.85f, 0.40f), Vector3.up * 0.25f, true, 3);
            var can = new List<Vector3>(); can.AddRange(Cone(Vector3.up * 4.4f, 4.6f, 2.2f, 18));
            Toon(car, "Canopy", can, new Color(0.35f, 0.65f, 1.0f), Vector3.up * 4.8f, true);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad; Vector3 p = new Vector3(Mathf.Cos(a) * 3.2f, 0.5f, Mathf.Sin(a) * 3.2f);
                Racket(car, p, Vector3.up, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), 3.9f, k % 2 == 0 ? new Color(1f, 0.4f, 0.5f) : new Color(0.78f, 1f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            }
            life.Spin(car, Vector3.up, 14f);

            // bunting between poles, lanterns
            var poles = new List<Vector3>(); var flags = new List<Vector3>(); var flags2 = new List<Vector3>();
            Vector3[] pp = { new Vector3(16f, 1f, 6f), new Vector3(8f, 1f, 14f), new Vector3(-2f, 1f, 18f), new Vector3(-16f, 1f, 12f) };
            foreach (var p in pp) poles.AddRange(Cylinder(p, p + Vector3.up * 6f, 0.15f, 5));
            for (int i = 0; i < pp.Length - 1; i++)
                for (int k = 0; k < 7; k++)
                {
                    float s = (k + 0.5f) / 7f; Vector3 a = Vector3.Lerp(pp[i], pp[i + 1], s) + Vector3.up * (6f - Mathf.Sin(s * Mathf.PI) * 1.2f);
                    var f = new List<Vector3>(); Tri(f, a + new Vector3(-0.4f, 0, 0), a + new Vector3(0.4f, 0, 0), a + Vector3.down * 0.9f);
                    (k % 2 == 0 ? flags : flags2).AddRange(f);
                }
            Toon(t, "BuntingPoles", poles, new Color(0.95f, 0.95f, 0.98f), Vector3.up * 3f, true, 3);
            Glow(t, "Bunting", flags, new Color(1.6f, 0.45f, 0.55f)); Glow(t, "Bunting2", flags2, new Color(0.78f, 1.0f, 0.24f));

            Trees(t, rng, 18, 17f, 25f, -220f, 30f, p => (p - new Vector3(6f, 0, -8f)).magnitude < 9f || (p - new Vector3(-12f, 0, -6f)).magnitude < 8f, Leaf);
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) }, new[] { 0.4f, 0.3f, 0.5f }, Grass, RockCol, Leaf);
            Clouds(t, life, rng, 10, 16, new Color(1f, 0.86f, 0.86f), 0.8f);
        }

        // =============================== ONLINE: the linked islands ===============================

        static void BuildOnline(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3606);
            Sky(t, "online", new Color(0.30f, 0.45f, 0.70f), new Color(0.55f, 0.72f, 0.95f), new Color(0.22f, 0.36f, 0.72f), new Color(0.05f, 0.08f, 0.25f), 160, 3606);
            Vector3[] isl = { new Vector3(0f, 0f, 0f), new Vector3(30f, 6f, -22f), new Vector3(-30f, -4f, -18f), new Vector3(8f, 10f, -48f) };
            float[] size = { 15f, 10f, 10f, 9f };
            for (int i = 0; i < isl.Length; i++)
            {
                var g = new GameObject("Isle").transform; g.SetParent(t, false); g.localPosition = isl[i];
                Toon(g, "Top", Top(Vector3.zero, size[i], 16, 0.8f, rng, out _), new Color(0.44f, 0.76f, 0.46f), Vector3.down * 3f);
                Toon(g, "Rock", Rock(Vector3.down * 0.9f, size[i] * 0.97f, size[i] * 1.5f, 16, rng), new Color(0.48f, 0.46f, 0.60f), Vector3.down * 8f, true);
                GlassCourt(g, new Vector3(0, 1f, 0), 15f * i, size[i] / 17f, Wood);
                if (i > 0) life.Bob(g, 0.4f + i * 0.15f, i, 0.5f);
            }
            // glowing bridges between them, and balls travelling the links
            var beams = new List<Vector3>();
            int[,] links = { { 0, 1 }, { 0, 2 }, { 1, 3 }, { 2, 3 } };
            for (int k = 0; k < links.GetLength(0); k++)
            {
                Vector3 a = isl[links[k, 0]] + Vector3.up * 2f, b = isl[links[k, 1]] + Vector3.up * 2f;
                for (int s = 0; s < 16; s++)
                {
                    float u0 = s / 16f, u1 = (s + 1) / 16f;
                    Vector3 p0 = Vector3.Lerp(a, b, u0) + Vector3.up * Mathf.Sin(u0 * Mathf.PI) * 6f, p1 = Vector3.Lerp(a, b, u1) + Vector3.up * Mathf.Sin(u1 * Mathf.PI) * 6f;
                    beams.AddRange(Cylinder(p0, p1, 0.18f, 5));
                }
                var orb = new GameObject("Orb").transform; orb.SetParent(t, false);
                orb.localPosition = Vector3.Lerp(a, b, 0.5f) + Vector3.up * 6.4f;
                Glow(orb, "Glow", Sphere(Vector3.zero, Vector3.one * 0.7f, 8, 6), new Color(1.6f, 1.6f, 0.4f));
                Billboard(orb, "Halo", Vector3.zero, 4f, new Color(1f, 1f, 0.5f, 0.5f), StdCam - orb.localPosition);
                life.Bob(orb, 1.2f + k * 0.3f, k, 1.0f);
            }
            var bm = Glow(t, "Bridges", beams, new Color(0.4f, 1.8f, 2.6f));
            life.Blink(bm.GetComponent<MeshRenderer>().sharedMaterial, 1.4f, 0f);
            // the radio tower on the big island, its beacon and a dish
            Vector3 rt = new Vector3(-9f, 1f, -6f);
            var lat = new List<Vector3>();
            for (int k = 0; k < 4; k++) { float a = k * 90f * Mathf.Deg2Rad + 0.78f; lat.AddRange(Cylinder(rt + new Vector3(Mathf.Cos(a) * 1.6f, 0, Mathf.Sin(a) * 1.6f), rt + Vector3.up * 18f, 0.15f, 4)); }
            for (float y = 3f; y < 17f; y += 3f) { float r = 1.6f * (1f - y / 18f); lat.AddRange(Ring(rt + Vector3.up * y, Vector3.up, r - 0.08f, r + 0.08f, 8)); }
            Toon(t, "Tower", lat, new Color(0.95f, 0.35f, 0.30f), rt + Vector3.up * 9f, true, 3);
            var bc = Glow(t, "Beacon", Sphere(rt + Vector3.up * 18.5f, Vector3.one * 0.6f, 8, 5), new Color(2.6f, 0.4f, 0.3f));
            life.Blink(bc.GetComponent<MeshRenderer>().sharedMaterial, 3f, 0f);
            var dish = new GameObject("Dish").transform; dish.SetParent(t, false); dish.localPosition = new Vector3(9f, 1f, -7f);
            Toon(dish, "Mast", Cylinder(Vector3.zero, Vector3.up * 3f, 0.25f, 6), new Color(0.8f, 0.82f, 0.88f), Vector3.up, true, 3);
            Toon(dish, "Bowl", Sphere(Vector3.up * 4f, new Vector3(2.4f, 0.8f, 2.4f), 14, 6), new Color(0.92f, 0.93f, 0.96f), Vector3.up * 4f, false, 4);
            life.Spin(dish, Vector3.up, 10f);
            Clouds(t, life, rng, 8, 16, new Color(0.75f, 0.82f, 1.0f), 0.7f);
        }

        // =============================== PACKS: the treasure cove ===============================

        static void BuildPacks(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3707);
            Sky(t, "packs", new Color(0.70f, 0.92f, 0.98f), new Color(0.80f, 0.96f, 1.0f), new Color(0.38f, 0.78f, 0.98f), new Color(0.15f, 0.48f, 0.92f), 0, 3707);
            Billboard(t, "Sun", new Vector3(60f, 120f, -320f), 26f, new Color(2f, 1.9f, 1.4f), StdCam, false);
            Billboard(t, "SunGlow", new Vector3(60f, 120f, -320f), 130f, new Color(1f, 1f, 0.85f, 0.5f), StdCam);
            Toon(t, "Sand", Top(Vector3.zero, 26f, 24, 1.0f, rng, out var rim), new Color(0.98f, 0.86f, 0.58f), Vector3.down * 4f);
            Toon(t, "IslandRock", Rock(Vector3.down * 1.0f, 25f, 32f, 24, rng), new Color(0.78f, 0.62f, 0.48f), Vector3.down * 10f, true);
            // the lagoon in the middle
            var lagoon = Greybox.MeshObject("Lagoon", t, Greybox.Disc(1f, 40), Flat(new Color(0.25f, 0.85f, 0.88f)));
            lagoon.transform.localPosition = new Vector3(-6f, 1.25f, 4f); lagoon.transform.localRotation = Quaternion.Euler(90f, 0, 0); lagoon.transform.localScale = new Vector3(8f, 6f, 1f);
            // palms
            var trunks = new List<Vector3>(); var fronds = new List<Vector3>();
            for (int k = 0; k < 9; k++)
            {
                float a = Mathf.Lerp(-200f, 20f, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = 14f + (float)rng.NextDouble() * 9f;
                Vector3 p = new Vector3(Mathf.Cos(a) * rr, 1f, Mathf.Sin(a) * rr), lean = new Vector3(((float)rng.NextDouble() - 0.5f) * 3f, 8f, ((float)rng.NextDouble() - 0.5f) * 3f);
                trunks.AddRange(Cylinder(p, p + lean, 0.35f, 6));
                Vector3 top = p + lean;
                for (int f = 0; f < 6; f++) { float fa = f * 60f * Mathf.Deg2Rad; var fr = new List<Vector3>(); Tri(fr, top, top + new Vector3(Mathf.Cos(fa) * 4f, -1.4f, Mathf.Sin(fa) * 4f), top + new Vector3(Mathf.Cos(fa + 0.35f) * 3.2f, -0.6f, Mathf.Sin(fa + 0.35f) * 3.2f)); fronds.AddRange(fr); }
            }
            Toon(t, "PalmTrunks", trunks, new Color(0.62f, 0.45f, 0.30f), Vector3.up * 4f, true, 3);
            Toon(t, "Fronds", fronds, new Color(0.30f, 0.72f, 0.35f), Vector3.up * 6f, true, 3);
            // giant wrapped packs with ribbons
            (Vector3 at, float s, Color c, Color rib)[] packs =
            {
                (new Vector3(9f, 1f, -6f), 5.5f, new Color(0.62f, 0.38f, 0.95f), new Color(0.78f, 1.0f, 0.24f)),
                (new Vector3(14f, 1f, 1f), 3.6f, new Color(0.95f, 0.35f, 0.45f), new Color(1.0f, 0.85f, 0.3f)),
                (new Vector3(4f, 1f, -11f), 3.0f, new Color(0.25f, 0.70f, 1.0f), new Color(1.0f, 1.0f, 1.0f)),
            };
            foreach (var (at, s, c, rib) in packs)
            {
                Vector3 cc = at + Vector3.up * s / 2;
                var rot = Quaternion.Euler(0, (float)rng.NextDouble() * 40f - 20f, 0);
                ToonParts(t, "Pack", Part(BoxTris(cc, Vector3.one * s, rot), cc), c);
                var r = new List<Vector3>(); r.AddRange(BoxTris(cc, new Vector3(s * 1.02f, s * 1.02f, s * 0.16f), rot)); r.AddRange(BoxTris(cc, new Vector3(s * 0.16f, s * 1.02f, s * 1.02f), rot));
                r.AddRange(Sphere(cc + Vector3.up * (s * 0.55f) + rot * new Vector3(s * 0.15f, 0, 0), new Vector3(s * 0.18f, s * 0.12f, s * 0.12f), 6, 4));
                r.AddRange(Sphere(cc + Vector3.up * (s * 0.55f) - rot * new Vector3(s * 0.15f, 0, 0), new Vector3(s * 0.18f, s * 0.12f, s * 0.12f), 6, 4));
                Toon(t, "Ribbon", r, rib, cc, false, 3);
            }
            // the treasure chest: gold coins and squash balls spilling out
            Vector3 ch = new Vector3(-12f, 1f, -4f);
            ToonParts(t, "Chest", Part(BoxTris(ch + Vector3.up * 1.4f, new Vector3(5f, 2.8f, 3.2f)), ch + Vector3.up * 1.4f), new Color(0.62f, 0.38f, 0.22f));
            ToonParts(t, "Lid", Part(BoxTris(ch + new Vector3(0, 3.6f, -1.9f), new Vector3(5f, 2.6f, 0.4f), Quaternion.Euler(-20f, 0, 0)), ch + new Vector3(0, 3.6f, -1.9f)), new Color(0.55f, 0.33f, 0.20f));
            var gold = new List<Vector3>();
            for (int k = 0; k < 40; k++)
            {
                bool spilt = k > 26;                                                        // a few on the sand in front
                Vector3 p = ch + (spilt ? new Vector3(((float)rng.NextDouble() - 0.5f) * 6f, 0.25f, 2.2f + (float)rng.NextDouble() * 2.5f)
                                        : new Vector3(((float)rng.NextDouble() - 0.5f) * 4.2f, 2.9f + (float)rng.NextDouble() * 0.5f, ((float)rng.NextDouble() - 0.5f) * 2.4f));
                gold.AddRange(Cylinder(p, p + Vector3.up * 0.14f, 0.45f, 10));
            }
            Toon(t, "Coins", gold, new Color(1.0f, 0.80f, 0.25f), ch + Vector3.up * 3f, false, 3);
            for (int k = 0; k < 4; k++) SquashBall(t, ch + new Vector3(-2.5f + k * 1.6f, 3.4f + (k % 2) * 0.4f, 1.2f), 0.55f, Vector3.forward);
            var shine = Billboard(t, "Shine", ch + Vector3.up * 4f, 10f, new Color(1f, 0.9f, 0.4f, 0.5f), StdCam);
            life.Halo = shine.transform; life.HaloMat = shine.GetComponent<MeshRenderer>().sharedMaterial;
            Waterfall(t, life, rim[19]);
            SmallIslands(t, life, rng, new[] { new Vector3(-66f, 8f, -70f), new Vector3(52f, -4f, -34f), new Vector3(92f, 20f, -140f) }, new[] { 0.36f, 0.3f, 0.48f }, new Color(0.98f, 0.86f, 0.58f), new Color(0.78f, 0.62f, 0.48f), new Color(0.30f, 0.72f, 0.35f));
            Clouds(t, life, rng, 12, 18, Cloud, 0.9f);
        }

        // =============================== STORE: the floating market ===============================

        static void BuildStore(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3808);
            Sky(t, "store", new Color(0.98f, 0.86f, 0.90f), new Color(1.0f, 0.82f, 0.78f), new Color(0.80f, 0.70f, 0.95f), new Color(0.48f, 0.52f, 0.90f), 0, 3808);
            Billboard(t, "SunGlow", new Vector3(110f, 18f, -320f), 170f, new Color(1f, 0.8f, 0.75f, 0.6f), StdCam);
            Billboard(t, "Sun", new Vector3(110f, 18f, -319f), 22f, new Color(1.9f, 1.5f, 1.2f), StdCam, false);
            Island(t, 26f, new Color(0.56f, 0.82f, 0.48f), new Color(0.72f, 0.58f, 0.56f), rng);
            // the square: paving round the middle
            var pave = new List<Vector3>();
            for (float x = -12f; x <= 12f; x += 2f) for (float z = -10f; z <= 12f; z += 2f) if (x * x + z * z < 150f) pave.AddRange(BoxTris(new Vector3(x, 1.18f, z), new Vector3(1.85f, 0.08f, 1.85f)));
            Toon(t, "Paving", pave, new Color(0.92f, 0.86f, 0.78f), Vector3.down * 3f, false, 3);
            // the racket shop: walls, a roof, a window full of rackets, its giant racket sign
            Vector3 sh = new Vector3(0f, 1f, -11f);
            ToonParts(t, "Shop", Part(BoxTris(sh + Vector3.up * 3f, new Vector3(12f, 6f, 6f)), sh + Vector3.up * 3f), new Color(0.98f, 0.94f, 0.86f));
            var roof = new List<Vector3>(); Pyramid(roof, sh + Vector3.up * 6f, 13f, 7f, 3f);
            Toon(t, "ShopRoof", roof, new Color(0.40f, 0.55f, 0.95f), sh + Vector3.up * 7f);
            Glow(t, "ShopWindow", BoxTris(sh + new Vector3(-2.5f, 2.6f, 3.03f), new Vector3(5f, 3f, 0.08f)), new Color(1.5f, 1.35f, 0.9f));
            Toon(t, "ShopDoor", BoxTris(sh + new Vector3(3.5f, 2f, 3.03f), new Vector3(2f, 3.6f, 0.1f)), new Color(0.40f, 0.55f, 0.95f), sh + new Vector3(3.5f, 2f, 0f));
            for (int k = 0; k < 3; k++) Racket(t, sh + new Vector3(-4.2f + k * 1.7f, 1.5f, 3.15f), Vector3.up, Vector3.forward, 2.6f, k == 1 ? new Color(0.78f, 1f, 0.24f) : new Color(0.95f, 0.32f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            Racket(t, sh + new Vector3(0, 9f, 0.5f), new Vector3(-1f, 0.15f, 0f), Vector3.forward, 11f, new Color(0.95f, 0.32f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            // market stalls with striped awnings, crates of balls
            Color[] aw = { new Color(0.95f, 0.35f, 0.45f), new Color(0.30f, 0.70f, 0.95f), new Color(1.0f, 0.75f, 0.25f), new Color(0.55f, 0.85f, 0.35f) };
            Vector3[] st = { new Vector3(13f, 1f, -3f), new Vector3(-13f, 1f, -3f), new Vector3(10f, 1f, 8f), new Vector3(-11f, 1f, 8f) };
            for (int k = 0; k < st.Length; k++)
            {
                Vector3 s = st[k];
                ToonParts(t, "Counter", Part(BoxTris(s + Vector3.up * 0.8f, new Vector3(4f, 1.6f, 2f)), s + Vector3.up * 0.8f), new Color(0.80f, 0.60f, 0.40f), 3);
                var posts = new List<Vector3>(); foreach (int sx in new[] { -1, 1 }) posts.AddRange(Cylinder(s + new Vector3(sx * 1.9f, 0, -0.9f), s + new Vector3(sx * 1.9f, 3.6f, -0.9f), 0.1f, 5));
                Toon(t, "Posts", posts, new Color(0.9f, 0.9f, 0.92f), s + Vector3.up * 2f, true, 3);
                for (int j = 0; j < 4; j++)
                {
                    var stripe = BoxTris(s + new Vector3(-1.5f + j * 1.0f, 3.5f, 0.1f), new Vector3(1.0f, 0.12f, 2.6f), Quaternion.Euler(18f, 0, 0));
                    ToonParts(t, "Awning", Part(stripe, s + new Vector3(-1.5f + j * 1.0f, 3f, 0.1f)), j % 2 == 0 ? aw[k] : new Color(0.98f, 0.97f, 0.94f), 3);
                }
                for (int j = 0; j < 5; j++) SquashBall(t, s + new Vector3(-1.4f + j * 0.7f, 1.85f, 0.4f), 0.28f, Vector3.forward);
            }
            // squash-ball balloons on strings
            for (int k = 0; k < 5; k++)
            {
                var b = new GameObject("Balloon").transform; b.SetParent(t, false);
                Vector3 anchor = st[k % st.Length] + new Vector3(1.8f, 3.6f, -0.9f);
                b.localPosition = anchor + new Vector3(((float)rng.NextDouble() - 0.5f) * 2f, 4f + (float)rng.NextDouble() * 2f, 0);
                SquashBall(b, Vector3.zero, 0.9f, Vector3.forward, k % 2 == 0 ? new Color(0.95f, 0.35f, 0.45f) : (Color?)null);
                Glow(b, "String", Cylinder(Vector3.down * 0.9f, anchor - b.localPosition, 0.03f, 3), new Color(0.3f, 0.3f, 0.3f));
                life.Bob(b, 0.8f + k * 0.2f, k, 0.3f);
            }
            Trees(t, rng, 16, 17f, 25f, -220f, 30f, p => p.z < -6f && Mathf.Abs(p.x) < 9f, Leaf);
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) }, new[] { 0.4f, 0.3f, 0.5f }, new Color(0.56f, 0.82f, 0.48f), new Color(0.72f, 0.58f, 0.56f), Leaf);
            Clouds(t, life, rng, 10, 16, new Color(1f, 0.92f, 0.95f), 0.8f);
        }

        // =============================== YOU: the autumn island ===============================

        static void BuildYou(Transform t, DioramaLife life)
        {
            var rng = new System.Random(3909);
            Sky(t, "you", new Color(0.98f, 0.88f, 0.70f), new Color(1.0f, 0.82f, 0.55f), new Color(0.80f, 0.74f, 0.72f), new Color(0.46f, 0.52f, 0.70f), 0, 3909);
            Billboard(t, "SunGlow", new Vector3(-90f, 45f, -320f), 160f, new Color(1f, 0.85f, 0.55f, 0.55f), StdCam);
            Island(t, 26f, new Color(0.70f, 0.72f, 0.36f), new Color(0.66f, 0.50f, 0.42f), rng);
            // autumn trees: orange, red, gold
            Color[] fall = { new Color(0.98f, 0.55f, 0.20f), new Color(0.90f, 0.30f, 0.20f), new Color(1.0f, 0.78f, 0.25f) };
            var trunks = new List<Vector3>(); var crowns = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            for (int k = 0; k < 22; k++)
            {
                float a = Mathf.Lerp(-220f, 30f, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = 15f + (float)rng.NextDouble() * 9f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0.6f, Mathf.Sin(a) * rr); float s = 0.9f + (float)rng.NextDouble() * 0.7f;
                trunks.AddRange(Cone(p, 0.4f * s, 2.4f * s, 5));
                crowns[k % 3].AddRange(Sphere(p + Vector3.up * 3.0f * s, new Vector3(2.0f, 1.8f, 2.0f) * s, 7, 5, rng, 0.25f));
            }
            Toon(t, "Trunks", trunks, Trunk, Vector3.zero, true, 3);
            for (int k = 0; k < 3; k++) Toon(t, "Crowns", crowns[k], fall[k], Vector3.zero, true);
            // the cabin locker room, its lockers along the front
            Vector3 cb = new Vector3(-6f, 1f, -9f);
            ToonParts(t, "Cabin", Part(BoxTris(cb + Vector3.up * 2.8f, new Vector3(12f, 5.6f, 7f)), cb + Vector3.up * 2.8f), new Color(0.72f, 0.48f, 0.32f));
            var roof = new List<Vector3>(); Pyramid(roof, cb + Vector3.up * 5.6f, 13.4f, 8.4f, 3.4f);
            Toon(t, "CabinRoof", roof, new Color(0.35f, 0.38f, 0.48f), cb + Vector3.up * 6.6f);
            var lockers = new List<(List<Vector3>, Vector3)>(); var vents = new List<Vector3>();
            for (int k = 0; k < 6; k++)
            {
                Vector3 l = cb + new Vector3(-4.6f + k * 1.6f, 1.9f, 3.9f);
                lockers.Add((BoxTris(l, new Vector3(1.45f, 3.8f, 0.8f)), l + Vector3.back * 0.5f));
                for (int v = 0; v < 3; v++) vents.AddRange(BoxTris(l + new Vector3(0, 1.3f - v * 0.22f, 0.41f), new Vector3(0.9f, 0.06f, 0.02f)));
            }
            ToonParts(t, "Lockers", lockers, new Color(0.30f, 0.55f, 0.95f));
            Glow(t, "LockerVents", vents, new Color(0.15f, 0.2f, 0.35f));
            Glow(t, "CabinSign", BoxTris(cb + new Vector3(0, 5.0f, 3.55f), new Vector3(5f, 0.9f, 0.1f)), new Color(0.78f, 1.0f, 0.24f));
            // the medal podium
            Vector3 pd = new Vector3(10f, 1f, 2f);
            var steps = new List<(List<Vector3>, Vector3)>
            {
                (BoxTris(pd + new Vector3(0, 1.5f, 0), new Vector3(2.6f, 3f, 2.6f)), pd + new Vector3(0, 1.5f, 0)),
                (BoxTris(pd + new Vector3(-2.6f, 1f, 0), new Vector3(2.6f, 2f, 2.6f)), pd + new Vector3(-2.6f, 1f, 0)),
                (BoxTris(pd + new Vector3(2.6f, 0.7f, 0), new Vector3(2.6f, 1.4f, 2.6f)), pd + new Vector3(2.6f, 0.7f, 0)),
            };
            ToonParts(t, "Podium", steps, new Color(0.96f, 0.96f, 0.98f));
            (Vector3 at, Color c)[] medals = { (pd + new Vector3(0, 4.2f, 0.6f), new Color(1.0f, 0.78f, 0.25f)), (pd + new Vector3(-2.6f, 3.2f, 0.6f), new Color(0.80f, 0.82f, 0.88f)), (pd + new Vector3(2.6f, 2.6f, 0.6f), new Color(0.85f, 0.52f, 0.30f)) };
            foreach (var (at, c) in medals)
            {
                Toon(t, "Medal", Cylinder(at, at + Vector3.forward * 0.25f, 0.7f, 16), c, at, false, 4);
                Glow(t, "MedalRibbon", BoxTris(at + Vector3.up * 0.9f, new Vector3(0.4f, 1.0f, 0.06f)), new Color(0.3f, 0.6f, 1.6f));
            }
            // a giant squash shoe and a racket bag
            Vector3 so = new Vector3(4f, 1f, 11f);
            var shoe = new List<(List<Vector3>, Vector3)>
            {
                (BoxTris(so + new Vector3(0, 0.4f, 0), new Vector3(5.6f, 0.8f, 2.4f)), so + new Vector3(0, 0.4f, 0)),
                (BoxTris(so + new Vector3(0.6f, 1.6f, 0), new Vector3(4.2f, 1.8f, 2.2f)), so + new Vector3(0.6f, 1.6f, 0)),
                (BoxTris(so + new Vector3(1.7f, 2.9f, 0), new Vector3(2.0f, 1.2f, 2.0f)), so + new Vector3(1.7f, 2.9f, 0)),
            };
            ToonParts(t, "Shoe", shoe, new Color(0.98f, 0.98f, 1.0f));
            Glow(t, "ShoeSole", BoxTris(so + new Vector3(0, 0.12f, 0), new Vector3(5.7f, 0.24f, 2.5f)), new Color(0.78f, 1.0f, 0.24f));
            Glow(t, "ShoeStripe", BoxTris(so + new Vector3(0.4f, 1.6f, 1.12f), new Vector3(3.2f, 0.35f, 0.05f), Quaternion.Euler(0, 0, 18f)), new Color(0.30f, 0.55f, 0.95f));
            Vector3 bg = new Vector3(-12f, 1f, 8f);
            Toon(t, "RacketBag", Cylinder(bg + new Vector3(-2.6f, 1f, 0), bg + new Vector3(2.6f, 1f, 0), 1.0f, 12), new Color(0.15f, 0.16f, 0.20f), bg + Vector3.up, true, 4);
            Glow(t, "BagStripe", Cylinder(bg + new Vector3(-0.3f, 1f, 0), bg + new Vector3(0.3f, 1f, 0), 1.03f, 12), new Color(0.95f, 0.35f, 0.3f));
            Racket(t, bg + new Vector3(1.2f, 1.6f, -0.4f), new Vector3(0.7f, 1f, 0f), Vector3.forward, 3.6f, new Color(0.78f, 1f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            // falling leaves
            for (int k = 0; k < 22; k++)
            {
                var lf = new GameObject("Leaf").transform; lf.SetParent(t, false);
                lf.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 44f, 3f + (float)rng.NextDouble() * 12f, ((float)rng.NextDouble() - 0.5f) * 30f);
                var tri = new List<Vector3>(); Tri(tri, new Vector3(-0.3f, 0, 0), new Vector3(0.3f, 0, 0.1f), new Vector3(0, 0.05f, 0.5f));
                Glow(lf, "L", tri, fall[k % 3]);
                life.Bob(lf, 0.6f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 6f, 0.8f);
                life.Spin(lf, new Vector3(0.3f, 1f, 0.2f), 40f + (float)rng.NextDouble() * 50f);
            }
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) }, new[] { 0.4f, 0.3f, 0.5f }, new Color(0.70f, 0.72f, 0.36f), new Color(0.66f, 0.50f, 0.42f), new Color(0.98f, 0.55f, 0.20f));
            Clouds(t, life, rng, 10, 16, new Color(1f, 0.93f, 0.85f), 0.7f);
        }

        // =============================== REPLAYS: the open-air cinema ===============================

        static void BuildReplays(Transform t, DioramaLife life)
        {
            var rng = new System.Random(4010);
            Sky(t, "replays", new Color(0.40f, 0.18f, 0.45f), new Color(0.95f, 0.40f, 0.62f), new Color(0.48f, 0.20f, 0.60f), new Color(0.10f, 0.05f, 0.22f), 220, 4010);
            Island(t, 26f, new Color(0.34f, 0.46f, 0.40f), new Color(0.44f, 0.34f, 0.46f), rng);
            // the giant screen, its frame and legs, a court on it
            Vector3 sc = new Vector3(2f, 9.5f, -11f);
            ToonParts(t, "ScreenFrame", Part(BoxTris(sc, new Vector3(22f, 12.6f, 0.8f)), sc + Vector3.back), new Color(0.18f, 0.16f, 0.22f));
            var legs = new List<Vector3>(); foreach (int sd in new[] { -1, 1 }) legs.AddRange(BoxTris(new Vector3(sc.x + sd * 9f, 1.8f, sc.z), new Vector3(1.2f, 3.6f, 1.2f)));
            Toon(t, "ScreenLegs", legs, new Color(0.18f, 0.16f, 0.22f), new Vector3(sc.x, 1.8f, sc.z - 1f));
            Vector3 face = sc + new Vector3(0, 0, 0.44f);
            Glow(t, "Picture", BoxTris(face, new Vector3(20.6f, 11.4f, 0.04f)), new Color(0.30f, 0.45f, 0.95f));
            // the court on the screen: the front wall, the floor in perspective, the lines, two players and the ball
            var floor = new List<Vector3>(); Tri(floor, face + new Vector3(-10.3f, -5.7f, 0.03f), face + new Vector3(10.3f, -5.7f, 0.03f), face + new Vector3(4.5f, -1.0f, 0.03f)); Tri(floor, face + new Vector3(-10.3f, -5.7f, 0.03f), face + new Vector3(4.5f, -1.0f, 0.03f), face + new Vector3(-4.5f, -1.0f, 0.03f));
            Glow(t, "PicFloor", floor, new Color(1.3f, 0.95f, 0.55f));
            Glow(t, "PicWall", BoxTris(face + new Vector3(0, 1.6f, 0.03f), new Vector3(9f, 5.2f, 0.02f)), new Color(1.5f, 1.5f, 1.6f));
            var lines = new List<Vector3>(); lines.AddRange(BoxTris(face + new Vector3(0, -0.75f, 0.06f), new Vector3(9f, 0.18f, 0.02f))); lines.AddRange(BoxTris(face + new Vector3(0, 0.8f, 0.06f), new Vector3(9f, 0.14f, 0.02f))); lines.AddRange(BoxTris(face + new Vector3(0, 4.0f, 0.06f), new Vector3(9f, 0.14f, 0.02f)));
            Glow(t, "PicLines", lines, new Color(2.0f, 0.35f, 0.3f));
            var players = new List<Vector3>();
            foreach (var pp in new[] { new Vector3(-2.6f, -3.2f, 0.07f), new Vector3(2.4f, -2.4f, 0.07f) })
            { players.AddRange(BoxTris(face + pp, new Vector3(0.8f, 2.2f, 0.02f))); players.AddRange(Sphere(face + pp + Vector3.up * 1.5f, new Vector3(0.45f, 0.45f, 0.02f), 8, 5)); }
            Glow(t, "PicPlayers", players, new Color(0.12f, 0.12f, 0.2f));
            Glow(t, "PicBall", Sphere(face + new Vector3(1.0f, 2.2f, 0.08f), new Vector3(0.3f, 0.3f, 0.02f), 8, 5), new Color(0.08f, 0.08f, 0.1f));
            // the projector booth and its beam to the screen
            Vector3 pj = new Vector3(-2f, 1f, 16f);
            ToonParts(t, "Booth", Part(BoxTris(pj + Vector3.up * 2.4f, new Vector3(4.4f, 4.8f, 3.6f)), pj + Vector3.up * 2.4f), new Color(0.88f, 0.30f, 0.36f));
            var lens = Glow(t, "Lens", Sphere(pj + new Vector3(0, 3.2f, -1.85f), Vector3.one * 0.5f, 8, 5), new Color(2.6f, 2.5f, 2.0f));
            var beamMat = Pal.Clear(new Color(0.9f, 0.9f, 1f, 0.12f));
            var beamGo = new GameObject("Beam"); beamGo.transform.SetParent(t, false);
            var bm = new Mesh(); var bv = new List<Vector3> { pj + new Vector3(0, 3.2f, -1.9f), face + new Vector3(-10f, -5.5f, 0.1f), face + new Vector3(10f, -5.5f, 0.1f), face + new Vector3(10f, 5.5f, 0.1f), face + new Vector3(-10f, 5.5f, 0.1f) };
            bm.SetVertices(bv); bm.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 }, 0); bm.RecalculateNormals(); bm.RecalculateBounds();
            beamGo.AddComponent<MeshFilter>().sharedMesh = bm; beamGo.AddComponent<MeshRenderer>().sharedMaterial = beamMat;
            if (beamMat.HasProperty("_Cull")) beamMat.SetFloat("_Cull", 0f);
            // film reels on the booth's roof, turning
            foreach (int sd in new[] { -1, 1 })
            {
                var reel = new GameObject("Reel").transform; reel.SetParent(t, false); reel.localPosition = pj + new Vector3(sd * 1.2f, 6.2f, 0);
                var r = new List<Vector3>(); r.AddRange(Ring(Vector3.zero, Vector3.forward, 0.3f, 1.3f, 20));
                for (int k = 0; k < 5; k++) { float a = k * 72f * Mathf.Deg2Rad; r.AddRange(Sphere(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.75f, new Vector3(0.28f, 0.28f, 0.1f), 6, 4)); }
                Toon(reel, "Reel", r, new Color(0.25f, 0.25f, 0.3f), new Vector3(0, 0, -1f), false, 3);
                life.Spin(reel, Vector3.forward, sd * 50f);
            }
            // rows of seats facing the screen
            var seats = new List<(List<Vector3>, Vector3)>();
            for (int row = 0; row < 4; row++)
                for (int k = 0; k < 9; k++)
                {
                    if (k == 4) continue;
                    Vector3 s = new Vector3(-8f + k * 2.1f, 1.6f + row * 0.35f, 3f + row * 2.6f);
                    seats.Add((BoxTris(s, new Vector3(1.6f, 1.2f, 1.4f)), s));
                }
            ToonParts(t, "Seats", seats, new Color(0.85f, 0.25f, 0.35f));
            // the popcorn stand, a neon sign, string lights
            Vector3 pc = new Vector3(14f, 1f, 8f);
            ToonParts(t, "PopcornStand", Part(BoxTris(pc + Vector3.up * 1.4f, new Vector3(3.6f, 2.8f, 2.4f)), pc + Vector3.up * 1.4f), new Color(0.95f, 0.92f, 0.88f));
            var corn = new List<Vector3>(); for (int k = 0; k < 18; k++) corn.AddRange(Sphere(pc + new Vector3(((float)rng.NextDouble() - 0.5f) * 2.6f, 3.0f + (float)rng.NextDouble() * 0.5f, ((float)rng.NextDouble() - 0.5f) * 1.6f), Vector3.one * 0.35f, 6, 4));
            Toon(t, "Popcorn", corn, new Color(1.0f, 0.92f, 0.62f), pc + Vector3.up * 2.6f, false, 3);
            var neon = Glow(t, "Neon", BoxTris(new Vector3(2f, 17.4f, -11f), new Vector3(10f, 1.4f, 0.2f)), new Color(2.4f, 0.5f, 1.8f));
            life.Blink(neon.GetComponent<MeshRenderer>().sharedMaterial, 2f, 0f);
            var bulbs = new List<Vector3>();
            for (int k = 0; k < 30; k++) { float s = k / 29f; bulbs.AddRange(Sphere(new Vector3(-16f + s * 32f, 8f - Mathf.Sin(s * Mathf.PI) * 2f, 10f), Vector3.one * 0.22f, 5, 3)); }
            var bl = Glow(t, "StringLights", bulbs, new Color(2.2f, 1.8f, 0.8f));
            life.Blink(bl.GetComponent<MeshRenderer>().sharedMaterial, 1.2f, 1f);
            Trees(t, rng, 14, 18f, 25f, -220f, 30f, p => p.z < -6f && Mathf.Abs(p.x - 2f) < 13f, new Color(0.24f, 0.46f, 0.40f));
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) }, new[] { 0.4f, 0.3f, 0.5f }, new Color(0.34f, 0.46f, 0.40f), new Color(0.44f, 0.34f, 0.46f), new Color(0.24f, 0.46f, 0.40f));
            Clouds(t, life, rng, 8, 16, new Color(0.80f, 0.55f, 0.85f), 0.6f);
        }
    }
}
