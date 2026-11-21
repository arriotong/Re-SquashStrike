// ===========================================================
// Assets/SquashGame/Runtime/Views/Dioramas.Worlds3.cs
// 2026-11-21 the Settings world (the user: "giving a world for the
// settings"): a snowy island on a bright winter afternoon - its own season,
// none of the other worlds have snow - with the workshop where the game is
// tuned:
//   - the wooden workshop, warm windows, a chimney smoking
//   - a big cog sculpture turning slowly (the settings sign)
//   - squash: the racket-stringing machine with a racket on it, a pegboard
//     of rackets, a crate of balls, a snowman with a squash-ball head and
//     racket arms
//   - snow pines, snowflakes, small snowy islands
// Same toolkit and layout rules as the other worlds. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashGame.ToonKit;

namespace SquashGame
{
    public static partial class Dioramas
    {
        static void BuildSettings(Transform t, DioramaLife life)
        {
            var rng = new System.Random(4111);
            Color snow = new Color(0.93f, 0.96f, 1.0f), rock = new Color(0.56f, 0.60f, 0.70f), wood = new Color(0.66f, 0.45f, 0.30f), pine = new Color(0.22f, 0.45f, 0.38f);
            Sky(t, "settings", new Color(0.86f, 0.90f, 0.97f), new Color(0.97f, 0.93f, 0.95f), new Color(0.70f, 0.80f, 0.96f), new Color(0.38f, 0.54f, 0.86f), 0, 4111);
            Billboard(t, "SunGlow", new Vector3(-100f, 35f, -320f), 150f, new Color(1f, 0.95f, 0.85f, 0.55f), NearCam);
            Billboard(t, "Sun", new Vector3(-100f, 35f, -319f), 20f, new Color(1.8f, 1.7f, 1.5f), NearCam, false);
            Island(t, 26f, snow, rock, rng);

            // the workshop: log walls, a snowy roof, warm windows, the chimney and its smoke
            Vector3 ws = new Vector3(-7f, 1f, -9f);
            ToonParts(t, "Workshop", Part(BoxTris(ws + Vector3.up * 2.8f, new Vector3(12f, 5.6f, 7f)), ws + Vector3.up * 2.8f), wood);
            var roof = new List<Vector3>(); Pyramid(roof, ws + Vector3.up * 5.6f, 13.6f, 8.6f, 3.6f);
            Toon(t, "SnowRoof", roof, snow, ws + Vector3.up * 6.6f);
            var win = new List<Vector3>();
            for (int k = 0; k < 3; k++) win.AddRange(BoxTris(ws + new Vector3(-3.6f + k * 3.6f, 3f, 3.53f), new Vector3(1.6f, 1.5f, 0.1f)));
            Glow(t, "WarmWindows", win, new Color(2.0f, 1.45f, 0.7f));
            Toon(t, "Door", BoxTris(ws + new Vector3(4.6f, 1.6f, 3.55f), new Vector3(1.6f, 3.2f, 0.12f)), new Color(0.40f, 0.26f, 0.18f), ws + new Vector3(4.6f, 1.6f, 0f));
            Vector3 ch = ws + new Vector3(3.5f, 7.4f, -1.5f);
            ToonParts(t, "Chimney", Part(BoxTris(ch, new Vector3(1.3f, 3.0f, 1.3f)), ch), new Color(0.62f, 0.40f, 0.36f), 3);
            for (int k = 0; k < 5; k++)
            {
                var puff = new GameObject("Smoke").transform; puff.SetParent(t, false); puff.localPosition = ch + new Vector3(k * 0.5f, 2.2f + k * 1.4f, -k * 0.2f);
                Toon(puff, "Puff", Sphere(Vector3.zero, Vector3.one * (0.6f + k * 0.25f), 8, 5), new Color(0.88f, 0.90f, 0.95f), Vector3.zero, false, 3);
                life.Bob(puff, 0.5f + k * 0.1f, k, 0.4f);
            }

            // the cog sculpture, turning
            var cog = new GameObject("Cog").transform; cog.SetParent(t, false); cog.localPosition = new Vector3(10f, 8.5f, -10f);
            var teeth = new List<Vector3>(); teeth.AddRange(Ring(Vector3.zero, Vector3.forward, 2.2f, 4.2f, 32)); teeth.AddRange(Ring(new Vector3(0, 0, 0.8f), Vector3.forward, 2.2f, 4.2f, 32));
            for (int k = 0; k < 12; k++) { float a = k * 30f; teeth.AddRange(BoxTris(Quaternion.Euler(0, 0, a) * new Vector3(4.7f, 0, 0.4f), new Vector3(1.3f, 1.2f, 0.9f), Quaternion.Euler(0, 0, a))); }
            Toon(cog, "Teeth", teeth, new Color(0.72f, 0.78f, 0.88f), new Vector3(0, 0, -2f), false, 4);
            Glow(cog, "Hub", Cylinder(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.9f), 1.0f, 16), new Color(0.78f, 1.0f, 0.24f));
            life.Spin(cog, Vector3.forward, 12f);
            Toon(t, "CogPost", Cylinder(new Vector3(10f, 1f, -10.4f), new Vector3(10f, 8.5f, -10.4f), 0.5f, 8), rock, new Vector3(10f, 4f, -10.4f), true, 3);

            // the stringing machine: a table, the clamp, a racket on it
            Vector3 sm = new Vector3(4f, 1f, 4f);
            ToonParts(t, "StringingTable", Part(BoxTris(sm + Vector3.up * 1.0f, new Vector3(3.2f, 0.3f, 1.8f)), sm + Vector3.up * 1.0f), wood, 3);
            Toon(t, "StringingPost", Cylinder(sm, sm + Vector3.up * 0.9f, 0.25f, 8), new Color(0.3f, 0.32f, 0.38f), sm + Vector3.up * 0.4f, true, 3);
            Toon(t, "Clamp", Cylinder(sm + new Vector3(0, 1.2f, 0), sm + new Vector3(0, 1.4f, 0), 1.1f, 16), new Color(0.25f, 0.55f, 0.95f), sm + Vector3.up * 1.3f, true, 3);
            Racket(t, sm + new Vector3(-1.6f, 1.55f, 0f), new Vector3(1f, 0.02f, 0f), Vector3.up, 3.0f, new Color(0.95f, 0.32f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            // the pegboard of rackets on the workshop's wall
            Vector3 pb = ws + new Vector3(-0.6f, 2.8f, 3.6f);
            Toon(t, "Pegboard", BoxTris(pb, new Vector3(3.8f, 2.4f, 0.1f)), new Color(0.86f, 0.74f, 0.56f), pb + Vector3.back, false, 3);
            Color[] fr = { new Color(0.78f, 1f, 0.24f), new Color(0.3f, 0.75f, 1.0f), new Color(1.0f, 0.75f, 0.25f) };
            for (int k = 0; k < 3; k++) Racket(t, pb + new Vector3(-1.2f + k * 1.2f, -1.0f, 0.12f), Vector3.up, Vector3.forward, 2.0f, fr[k], new Color(0.12f, 0.12f, 0.14f));
            // a crate of balls
            Vector3 cr = new Vector3(-12f, 1f, 6f);
            ToonParts(t, "Crate", Part(BoxTris(cr + Vector3.up * 0.6f, new Vector3(2.2f, 1.2f, 1.6f)), cr + Vector3.up * 0.6f), wood, 3);
            var balls = new List<Vector3>();
            for (int k = 0; k < 14; k++) balls.AddRange(Sphere(cr + new Vector3(((float)rng.NextDouble() - 0.5f) * 1.8f, 1.3f + (float)rng.NextDouble() * 0.3f, ((float)rng.NextDouble() - 0.5f) * 1.2f), Vector3.one * 0.22f, 6, 4));
            Toon(t, "Balls", balls, new Color(0.2f, 0.2f, 0.24f), cr + Vector3.up, false, 3);

            // the snowman: snow, a squash-ball head, racket arms
            Vector3 sn = new Vector3(-3f, 1f, 9f);
            Toon(t, "SnowBase", Sphere(sn + Vector3.up * 1.4f, Vector3.one * 1.7f, 14, 10), snow, sn + Vector3.up * 1.4f, false, 4);
            Toon(t, "SnowBody", Sphere(sn + Vector3.up * 3.6f, Vector3.one * 1.2f, 14, 10), snow, sn + Vector3.up * 3.6f, false, 4);
            SquashBall(t, sn + Vector3.up * 5.3f, 0.85f, new Vector3(0.2f, 0.1f, 1f));
            Racket(t, sn + new Vector3(1.0f, 3.7f, 0f), new Vector3(1f, 0.7f, 0f), Vector3.forward, 2.4f, new Color(0.95f, 0.32f, 0.24f), new Color(0.12f, 0.12f, 0.14f));
            Racket(t, sn + new Vector3(-1.0f, 3.7f, 0f), new Vector3(-1f, 0.7f, 0f), Vector3.forward, 2.4f, new Color(0.3f, 0.75f, 1.0f), new Color(0.12f, 0.12f, 0.14f));
            Glow(t, "Scarf", Cylinder(sn + new Vector3(0, 4.6f, -0.2f), sn + new Vector3(0, 4.75f, 0.2f), 0.85f, 14), new Color(1.6f, 0.4f, 0.45f));

            // snow pines and snowflakes
            var pines = new List<Vector3>(); var caps = new List<Vector3>();
            for (int k = 0; k < 20; k++)
            {
                float a = Mathf.Lerp(-220f, 30f, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = 15f + (float)rng.NextDouble() * 9f;
                var p = new Vector3(Mathf.Cos(a) * rr, 0.8f, Mathf.Sin(a) * rr);
                if ((p - ws).magnitude < 8f || (p - new Vector3(10f, 0, -10f)).magnitude < 5f) continue;
                float s = 0.9f + (float)rng.NextDouble() * 0.7f;
                pines.AddRange(Cone(p, 1.8f * s, 3.4f * s, 7)); pines.AddRange(Cone(p + Vector3.up * 1.8f * s, 1.3f * s, 2.8f * s, 7));
                caps.AddRange(Cone(p + Vector3.up * 3.2f * s, 0.75f * s, 1.5f * s, 7));
            }
            Toon(t, "Pines", pines, pine, Vector3.zero, true);
            Toon(t, "PineSnow", caps, snow, Vector3.zero, true, 3);
            for (int k = 0; k < 40; k++)
            {
                var f = new GameObject("Snowflake").transform; f.SetParent(t, false);
                f.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 60f, 2f + (float)rng.NextDouble() * 24f, ((float)rng.NextDouble() - 0.5f) * 40f);
                Glow(f, "Flake", Sphere(Vector3.zero, Vector3.one * 0.16f, 5, 3), new Color(1.2f, 1.2f, 1.25f));
                life.Bob(f, 0.4f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 6f, 1.2f);
                life.Drift(f, 0.4f + (float)rng.NextDouble() * 0.4f, 30f);
            }
            SmallIslands(t, life, rng, new[] { new Vector3(-70f, 12f, -80f), new Vector3(54f, -6f, -36f), new Vector3(94f, 22f, -140f) }, new[] { 0.4f, 0.3f, 0.5f }, snow, rock, pine);
            Clouds(t, life, rng, 10, 16, new Color(0.96f, 0.97f, 1.0f), 0.7f);
        }
    }
}
