// ===========================================================
// Assets/SquashGame/Runtime/Views/GauntletIsland.cs
// 2026-11-20 the Gauntlet's own background (the user: "a special gauntlet
// background ... island feeling with a giant squash ball on it ... a
// stable background, not the ones with players moving"): a floating
// island in a sunset sky -
//   - the island: a grass top over a hanging rock, low-poly, toon shaded
//   - the giant squash ball (two yellow dots) half sunk into its top, a
//     gold ring round it and a soft halo behind
//   - waterfalls off the edge, a tiny glass court, trees, small islands,
//     rocks and clouds floating round, a sea of cloud far below
// One of the menu worlds (Dioramas); built with ToonKit. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashGame.ToonKit;

namespace SquashGame
{
    public static class GauntletIsland
    {
        /// <summary>Where it is (world), its camera (tests and the old callers: Dioramas has them all).</summary>
        public static Vector3 World { get { return Dioramas.Origin(MenuWorld.Gauntlet); } }
        public static readonly Vector3 CamLocal = new Vector3(6f, 14f, 92f), LookLocal = new Vector3(24f, 2f, 0f);   // (world +x is screen left from here)
        public const float Fov = 36f;
        public const float FarClip = Dioramas.FarClip;
        /// <summary>Shown now (tests).</summary>
        public static bool Shown { get { return Dioramas.Shown(MenuWorld.Gauntlet); } }

        static readonly Color Grass = new Color(0.42f, 0.80f, 0.36f), RockCol = new Color(0.62f, 0.44f, 0.40f), Leaf = new Color(0.25f, 0.62f, 0.35f),
                              Trunk = new Color(0.45f, 0.30f, 0.22f), CloudCol = new Color(1.0f, 0.86f, 0.90f), BallCol = new Color(0.22f, 0.22f, 0.27f);

        public static void Build(Transform t, DioramaLife life)
        {
            var _pal = Pal;
            var rng = new System.Random(2611);

            // the sky: sunset, stars up high
            var sky = _pal.UnlitColor(Color.white);
            sky.mainTexture = ProcTex.LegacySky("gauntlet2", new Color(0.96f, 0.64f, 0.58f), new Color(1.0f, 0.58f, 0.32f), new Color(0.82f, 0.36f, 0.58f), new Color(0.10f, 0.09f, 0.30f), 140, 2611);
            var dome = Greybox.MeshObject("Sky", t, VenueView.Dome(420f, 48, 24), sky);
            dome.transform.localPosition = new Vector3(0, 6f, 0);            // the horizon at eye level
            // the sun, low behind, and its glow
            var sunGlow = _pal.Clear(new Color(1f, 0.75f, 0.45f, 0.55f)); sunGlow.mainTexture = ProcTex.SoftSpot();
            var glow = Greybox.MeshObject("SunGlow", t, Greybox.Disc(1f, 32), sunGlow);
            glow.transform.localPosition = new Vector3(-95f, 14f, -330f); glow.transform.localScale = new Vector3(190f, 190f, 1f);
            glow.transform.LookAt(t.TransformPoint(CamLocal)); glow.transform.Rotate(0, 180f, 0);
            var sunDisc = Greybox.MeshObject("Sun", t, Greybox.Disc(1f, 40), Flat(new Color(2.0f, 1.15f, 0.5f)));
            sunDisc.transform.localPosition = new Vector3(-95f, 14f, -329f); sunDisc.transform.localScale = new Vector3(26f, 26f, 1f);
            sunDisc.transform.rotation = glow.transform.rotation;

            // the island
            Vector3 c = Vector3.zero;
            var top = Top(c, 17f, 22, 1.4f, rng, out var rim);
            Toon(t, "IslandTop", top, Grass, c + Vector3.down * 4f);
            Toon(t, "IslandRock", Rock(c + Vector3.down * 1.0f, 16.5f, 30f, 22, rng), RockCol, c + Vector3.down * 10f, true);
            // strata: a darker band of boulders round the rock
            var bould = new List<Vector3>();
            for (int k = 0; k < 14; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, y = -3f - (float)rng.NextDouble() * 9f, rr = 15.5f - (-y) * 0.55f;
                bould.AddRange(Sphere(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr), Vector3.one * (1.2f + (float)rng.NextDouble() * 1.4f), 6, 4, rng, 0.3f));
            }
            Toon(t, "Boulders", bould, RockCol * 0.85f, c + Vector3.down * 8f, true);

            // the giant squash ball, half sunk, the dots toward the camera
            Vector3 ballC = new Vector3(-2f, 4.2f, -2f); const float R = 7.2f;
            Toon(t, "GiantBall", Sphere(ballC, Vector3.one * R, 28, 18), BallCol, ballC, false, 5);
            var dot = Flat(new Color(1.6f, 1.35f, 0.15f));
            foreach (var dir in new[] { new Vector3(0.56f, 0.30f, 1f).normalized, new Vector3(0.30f, 0.62f, 1f).normalized })   // small, off to one side (+x: screen left) - a ball, not a face
            {
                var d = Greybox.MeshObject("YellowDot", t, Greybox.Disc(1f, 24), dot);
                d.transform.localPosition = ballC + dir * (R + 0.04f);
                d.transform.localRotation = Quaternion.LookRotation(-dir);
                d.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            }
            // its highlight (a soft white sheen upper left)
            var sheen = _pal.Clear(new Color(1f, 1f, 1f, 0.22f)); sheen.mainTexture = ProcTex.SoftSpot();
            var sh = Greybox.MeshObject("Sheen", t, Greybox.Disc(1f, 24), sheen);
            Vector3 sd = new Vector3(0.5f, 0.62f, 0.62f).normalized;
            sh.transform.localPosition = ballC + sd * (R + 0.06f); sh.transform.localRotation = Quaternion.LookRotation(-sd); sh.transform.localScale = new Vector3(3.2f, 2.2f, 1f);
            // the gold ring at its foot and the halo behind it
            var ring = Greybox.MeshObject("GoldRing", t, Greybox.Torus(1f, 0.035f, 64, 6), Flat(new Color(2.0f, 1.4f, 0.35f)));
            ring.transform.localPosition = new Vector3(ballC.x, 1.25f, ballC.z); ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            ring.transform.localScale = Vector3.one * (Mathf.Sqrt(R * R - (ballC.y - 1.25f) * (ballC.y - 1.25f)) + 0.9f);
            var haloMat = _pal.Clear(new Color(1f, 0.82f, 0.4f, 0.35f)); haloMat.mainTexture = ProcTex.SoftSpot();
            var halo = Greybox.MeshObject("Halo", t, Greybox.Disc(1f, 32), haloMat);
            halo.transform.localPosition = ballC + new Vector3(0, 0.5f, -3f); halo.transform.localScale = new Vector3(24f, 24f, 1f);
            halo.transform.LookAt(t.TransformPoint(CamLocal)); halo.transform.Rotate(0, 180f, 0);
            life.Halo = halo.transform; life.HaloMat = haloMat;

            // a tiny glass court on the grass, right of the ball
            var courtRoot = new GameObject("TinyCourt").transform; courtRoot.SetParent(t, false);
            courtRoot.localPosition = new Vector3(9.5f, 1.05f, 5.5f); courtRoot.localRotation = Quaternion.Euler(0, -28f, 0);
            const float cw = 2.9f, cl = 4.4f, ch = 2.1f;
            Greybox.Box("Floor", courtRoot, Flat(new Color(0.86f, 0.62f, 0.36f)), new Vector3(0, 0.05f, 0), new Vector3(cw, 0.1f, cl), false);
            Greybox.Box("FrontWall", courtRoot, Flat(new Color(0.93f, 0.93f, 0.96f)), new Vector3(0, ch / 2, -cl / 2), new Vector3(cw, ch, 0.08f), false);
            Greybox.Box("Tin", courtRoot, Flat(new Color(0.9f, 0.2f, 0.2f)), new Vector3(0, 0.18f, -cl / 2 + 0.05f), new Vector3(cw, 0.06f, 0.02f), false);
            Greybox.Box("Line", courtRoot, Flat(new Color(0.9f, 0.2f, 0.2f)), new Vector3(0, 1.75f, -cl / 2 + 0.05f), new Vector3(cw, 0.05f, 0.02f), false);
            var glass = _pal.Clear(new Color(0.7f, 0.85f, 1f, 0.28f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Glass", courtRoot, glass, new Vector3(s * cw / 2, ch * 0.45f, 0), new Vector3(0.04f, ch * 0.9f, cl), false);
            Greybox.Box("GlassBack", courtRoot, glass, new Vector3(0, ch * 0.3f, cl / 2), new Vector3(cw, ch * 0.6f, 0.04f), false);

            // trees round the back and the sides
            var leaves = new List<Vector3>(); var pines = new List<Vector3>(); var trunks = new List<Vector3>();
            for (int k = 0; k < 16; k++)
            {
                float a = Mathf.Lerp(-200f, 20f, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = 10.5f + (float)rng.NextDouble() * 5f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0.6f, Mathf.Sin(a) * rr);
                if ((p - new Vector3(9.5f, 0, 5.5f)).magnitude < 4f || (p - ballC).magnitude < R + 1.5f) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.7f;
                trunks.AddRange(Cone(p, 0.35f * s, 2.0f * s, 5));
                if (k % 3 == 0) leaves.AddRange(Sphere(p + Vector3.up * 2.6f * s, new Vector3(1.7f, 1.5f, 1.7f) * s, 7, 5, rng, 0.25f));
                else { pines.AddRange(Cone(p + Vector3.up * 1.0f * s, 1.6f * s, 3.2f * s, 7)); pines.AddRange(Cone(p + Vector3.up * 2.6f * s, 1.15f * s, 2.6f * s, 7)); }
            }
            Toon(t, "Trunks", trunks, Trunk, new Vector3(0, 0, 0), true, 3);
            Toon(t, "Leaves", leaves, Leaf * 1.15f, Vector3.zero, true);
            Toon(t, "Pines", pines, Leaf, Vector3.zero, true);

            // waterfalls off the edge (front left, right)
            var fallMat = _pal.Clear(new Color(0.85f, 0.95f, 1f, 0.75f)); fallMat.mainTexture = FallTexture();
            fallMat.mainTextureScale = new Vector2(1f, 3f);
            life.Falls = fallMat;
            foreach (int j in new[] { 15, 19, 4 })
            {
                Vector3 e = rim[j % rim.Length];
                Vector3 outDir = new Vector3(e.x, 0, e.z).normalized;
                var f = Greybox.MeshObject("Waterfall", t, Greybox.Disc(1f, 4), fallMat);
                Object.Destroy(f.GetComponent<MeshFilter>().sharedMesh);
                f.GetComponent<MeshFilter>().sharedMesh = FallMesh(2.2f, 46f);
                f.transform.localPosition = e + outDir * 0.6f;
                f.transform.localRotation = Quaternion.LookRotation(outDir);
                // the mist where it leaves the edge
                var mist = Greybox.MeshObject("Mist", t, Greybox.Disc(1f, 16), _pal.Clear(new Color(1f, 1f, 1f, 0.35f)));
                mist.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = ProcTex.SoftSpot();
                mist.transform.localPosition = e + outDir * 1.2f + Vector3.down * 0.6f; mist.transform.localScale = new Vector3(4f, 3f, 1f);
                mist.transform.LookAt(t.TransformPoint(CamLocal)); mist.transform.Rotate(0, 180f, 0);
            }

            // small islands about, a tree or two on each
            Vector3[] isl = { new Vector3(-62f, 12f, -70f), new Vector3(48f, -6f, -30f), new Vector3(-38f, -16f, 14f), new Vector3(85f, 22f, -130f), new Vector3(-120f, 28f, -170f), new Vector3(30f, 30f, -190f) };
            float[] isz = { 0.42f, 0.32f, 0.24f, 0.5f, 0.6f, 0.38f };
            for (int i = 0; i < isl.Length; i++)
            {
                var g = new GameObject("SmallIsland").transform; g.SetParent(t, false); g.localPosition = isl[i];
                float s = 17f * isz[i];
                Toon(g, "Top", Top(Vector3.zero, s, 12, s * 0.12f, rng, out _), Grass, Vector3.down * 4f * isz[i]);
                Toon(g, "Rock", Rock(Vector3.down * 0.9f * isz[i], s * 0.97f, 30f * isz[i], 12, rng), RockCol, Vector3.down * 10f * isz[i], true);
                var tr = new List<Vector3>();
                int trees = 1 + rng.Next(3);
                for (int k = 0; k < trees; k++)
                {
                    var p = new Vector3(((float)rng.NextDouble() - 0.5f) * s, 0.4f * isz[i], ((float)rng.NextDouble() - 0.5f) * s);
                    tr.AddRange(Cone(p + Vector3.up * 0.5f * isz[i] * 2f, 1.6f * isz[i] * 2f, 3.6f * isz[i] * 2f, 6));
                }
                Toon(g, "Pines", tr, Leaf, Vector3.zero, true);
                life.Bob(g, 0.6f + (float)rng.NextDouble() * 0.8f, (float)rng.NextDouble() * 6f, 0.35f);
            }
            // rocks drifting close round the island
            for (int k = 0; k < 9; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = 22f + (float)rng.NextDouble() * 10f;
                var g = new GameObject("FloatingRock").transform; g.SetParent(t, false);
                g.localPosition = new Vector3(Mathf.Cos(a) * rr, -6f + (float)rng.NextDouble() * 14f, Mathf.Sin(a) * rr * 0.8f - 6f);
                float s = 0.6f + (float)rng.NextDouble() * 1.4f;
                Toon(g, "Rock", Rock(Vector3.zero, s, s * 1.6f, 6, rng), RockCol, Vector3.down * s * 0.6f, true, 3);
                life.Bob(g, 0.4f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 6f, 0.5f);
            }

            // clouds: puffs drifting by, and a sea of them far below
            for (int k = 0; k < 14; k++)
            {
                var g = new GameObject("Cloud").transform; g.SetParent(t, false);
                bool low = k < 6;
                float x = -200f + (float)rng.NextDouble() * 400f, z = low ? -20f - (float)rng.NextDouble() * 160f : -60f - (float)rng.NextDouble() * 220f;
                float y = low ? -24f - (float)rng.NextDouble() * 10f : 18f + (float)rng.NextDouble() * 60f;
                g.localPosition = new Vector3(x, y, z);
                float s = low ? 9f + (float)rng.NextDouble() * 8f : 5f + (float)rng.NextDouble() * 9f;
                var puffs = new List<Vector3>();
                int n = 3 + rng.Next(4);
                for (int p = 0; p < n; p++)
                {
                    var pc = new Vector3((p - n / 2f) * s * 0.7f, (float)rng.NextDouble() * s * 0.25f, ((float)rng.NextDouble() - 0.5f) * s * 0.4f);
                    puffs.AddRange(Sphere(pc, new Vector3(1f, 0.7f, 0.9f) * s * (0.55f + 0.35f * (float)rng.NextDouble()), 9, 6));
                }
                Toon(g, "Puffs", puffs, low ? CloudCol * 0.9f : CloudCol, Vector3.down * s * 0.4f, false, 3);
                life.Drift(g, (low ? 0.6f : 1.2f) + (float)rng.NextDouble(), 220f);
            }
            // the sea of cloud far below: big low banks over the warm haze (the sky's lower half)
            for (int k = 0; k < 22; k++)
            {
                var g = new GameObject("CloudSea").transform; g.SetParent(t, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = 60f + (float)rng.NextDouble() * 300f;
                g.localPosition = new Vector3(Mathf.Cos(a) * rr, -46f - (float)rng.NextDouble() * 16f, Mathf.Sin(a) * rr * 0.8f - 40f);
                float s = 16f + (float)rng.NextDouble() * 18f;
                var puffs = new List<Vector3>();
                for (int p = 0; p < 4; p++)
                    puffs.AddRange(Sphere(new Vector3((p - 1.5f) * s * 0.8f, (float)rng.NextDouble() * s * 0.2f, ((float)rng.NextDouble() - 0.5f) * s * 0.6f), new Vector3(1f, 0.55f, 0.9f) * s * (0.6f + 0.3f * (float)rng.NextDouble()), 9, 6));
                Toon(g, "Bank", puffs, new Color(1.0f, 0.78f, 0.80f), Vector3.down * s * 0.3f, false, 3);
            }
        }
    }
}
