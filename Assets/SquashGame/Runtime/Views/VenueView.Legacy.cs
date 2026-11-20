// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Legacy.cs
// 2026-11-20 more legacy courts (the user: "those courts we made at the
// start, like City Rooftop, the ones that are not real locations - I
// will call them legacy courts - make a few more of them"): a snowy
// mountain peak under the northern lights, a beach at sunset, a space
// station over the Earth, a neon city in the rain, a dome on the sea
// floor, a jungle temple, and a volcano. Each a glass court with stands,
// built in code like the rest. Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static partial class VenueView
    {
        /// <summary>The legacy courts' builders, by court id (VenueView.Build's switch falls through to these).</summary>
        static bool Legacy(Ctx c, string id)
        {
            switch (id)
            {
                case "peak": Peak(c); return true;
                case "beach": Beach(c); return true;
                case "orbit": Orbit(c); return true;
                case "neon": Neon(c); return true;
                case "reef": Reef(c); return true;
                case "temple": Temple(c); return true;
                case "volcano": Volcano(c); return true;
            }
            return false;
        }

        // ---------------- shared bits ----------------

        /// <summary>Behind the back wall, where the cameras stand (the TV view, the match's opening shot): nothing tall goes here.</summary>
        static bool InCameraLane(float simX, float simZ) { return simZ > L && Mathf.Abs(simX) < 22f; }

        static void SkyOf(Ctx c, Texture2D tex, float radius)
        {
            var mat = c.Palette.UnlitColor(Color.white);
            mat.mainTexture = tex;
            var go = Greybox.MeshObject("Sky", c.Venue, SkyDome(radius, 48, 24), mat);
            go.transform.position = W(0, 0, L / 2);
        }

        /// <summary>An upright cone (a mountain, a pine, a volcano), base centred on 'foot'.</summary>
        static GameObject Cone(Ctx c, string name, Material mat, Vector3 foot, float radius, float height, int sides = 16, float topRadius = 0f)
        {
            var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>(); var uv = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * radius); v.Add(d * topRadius + Vector3.up * height);
                uv.Add(new Vector2(i / (float)sides, 0)); uv.Add(new Vector2(i / (float)sides, 1));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 1); t.Add(a + 3); t.Add(a + 2);
            }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = Greybox.MeshObject(name, c.Venue, m, mat, true);
            go.transform.position = foot;
            return go;
        }

        /// <summary>Stands down both sides and behind the camera (the end beyond the front wall left open for the view).</summary>
        static void LegacyStands(Ctx c, int rows, int seed, bool flat = false)
        {
            System.Func<int, float> tread = flat ? FlatTread : (System.Func<int, float>)null;
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "StandLeft" : "StandRight", rows, i => W(side * (HW + 1.6 + i * ROW_DEPTH), 0, L / 2 + 1.5),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 22f, side + seed, tread);
            Stand(c, "StandBack", rows, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 4f, seed + 2, tread);
        }

        /// <summary>The deck the court sits on.</summary>
        static void Deck(Ctx c, Material mat, float halfX = 16f, float front = -10f, float back = 26f)
        {
            Greybox.Box("Deck", c.Venue, mat, W(0, -0.3, (front + back) / 2), new Vector3(2 * halfX, 0.5f, back - front));
        }

        // ---------------- Mountain Peak: a summit at night, snow, pines, the northern lights ----------------

        static void Peak(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("peak", new Color(0.05f, 0.07f, 0.12f), new Color(0.12f, 0.16f, 0.28f), new Color(0.04f, 0.07f, 0.16f), new Color(0.01f, 0.015f, 0.04f), 2400, 101, true), 650f);
            var snow = Glow(c, new Color(0.42f, 0.48f, 0.60f));                       // moonlit: it holds its own light at night
            Greybox.Box("Snowfield", c.Venue, snow, W(0, -0.6, 0), new Vector3(900f, 0.2f, 900f), false);
            Deck(c, Mat(c, Greybox.Hex(0x2b2f38), 0.3f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x1c2a3a), Greybox.Hex(0x7fd8ff));

            // The range all round, highest beyond the front wall: rock below, snow on the tops.
            var rock = Glow(c, new Color(0.10f, 0.13f, 0.20f));
            var cap = Glow(c, new Color(0.62f, 0.72f, 0.86f));
            var rng = new System.Random(41);
            for (int i = 0; i < 22; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 140f + (float)rng.NextDouble() * 300f;
                // the range beyond the front wall and down the sides - never behind the back wall, where the cameras are
                float h = 60f + (float)rng.NextDouble() * 150f, rad = h * (0.9f + (float)rng.NextDouble() * 0.5f);
                float x = Mathf.Cos(a) * (r + rad) * 1.3f, z = -50f - rad - Mathf.Abs(Mathf.Sin(a)) * r;   // (a whole mountain's width clear of the court)
                Cone(c, "Mountain", rock, W(x, -0.6, z), rad, h, 7);
                Cone(c, "SnowCap", cap, W(x, -0.6 + h * 0.62, z), rad * 0.39f, h * 0.39f, 7);
            }
            // Pines round the deck.
            var pine = Glow(c, new Color(0.03f, 0.09f, 0.07f));
            var trunk = Mat(c, new Color(0.18f, 0.12f, 0.08f), 0.05f);
            for (int i = 0; i < 60; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 26f + (float)rng.NextDouble() * 50f;
                if (InCameraLane(Mathf.Cos(a) * r, L / 2 + Mathf.Sin(a) * r)) continue;
                Vector3 foot = W(Mathf.Cos(a) * r, -0.5, L / 2 + Mathf.Sin(a) * r);
                float h = 6f + (float)rng.NextDouble() * 7f;
                Greybox.Box("PineTrunk", c.Venue, trunk, foot + Vector3.up * 0.8f, new Vector3(0.4f, 1.6f, 0.4f), false);
                Cone(c, "Pine", pine, foot + Vector3.up * 1.2f, h * 0.3f, h, 8);
            }
            LegacyStands(c, 7, 51);
            FloodTowers(c, 13.5f, -6f, L + 14f, 18f);
            Ambient(new Color(0.20f, 0.30f, 0.36f), new Color(0.18f, 0.22f, 0.30f), new Color(0.22f, 0.24f, 0.30f));
            RenderSettings.fog = false;
            if (c.High) Point(c, "AuroraGlow", W(0, 30, -30), new Color(0.4f, 1f, 0.7f), 90f, 1.4f);
        }

        // ---------------- Beach Sunset: sand, the sea, palms, string lights ----------------

        static void Beach(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("beach", new Color(0.30f, 0.16f, 0.12f), new Color(1.0f, 0.55f, 0.25f), new Color(0.85f, 0.38f, 0.42f), new Color(0.16f, 0.14f, 0.34f), 120, 102), 650f);
            var sand = Mat(c, new Color(0.86f, 0.74f, 0.56f), 0.05f); sand.mainTexture = ProcTex.Sand();
            Greybox.TexturedBox("Sand", c.Venue, sand, W(0, -0.55, 20), new Vector3(400f, 0.2f, 120f), new Vector2(6f, 6f), false);
            // The sea beyond the front wall, out to the horizon, and the low sun on it.
            var sea = Mat(c, new Color(0.10f, 0.22f, 0.36f), 0.92f);
            Greybox.Box("Sea", c.Venue, sea, W(0, -0.7, -260), new Vector3(1400f, 0.2f, 520f), false);
            var foam = Glow(c, new Color(1.2f, 1.1f, 1.0f));
            for (int k = 0; k < 4; k++) Greybox.Box("Surf", c.Venue, foam, W(0, -0.55, -38 - k * 9), new Vector3(300f, 0.03f, 0.5f), false);
            var sun = Greybox.MeshObject("Sun", c.Venue, Greybox.Disc(45f, 48), Glow(c, new Color(3.2f, 1.8f, 0.7f)));
            sun.transform.position = W(30, 25, -600);
            sun.transform.rotation = Quaternion.LookRotation(W(0, 0, 0) - sun.transform.position, Vector3.up);
            Greybox.Box("SunPath", c.Venue, Glow(c, new Color(1.6f, 0.8f, 0.3f)), W(30, -0.58, -330), new Vector3(16f, 0.02f, 540f), false);
            Deck(c, Mat(c, Greybox.Hex(0x7a5636), 0.3f), 14f, -6f, 24f);
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x14424a), Greybox.Hex(0xffc35a));

            // Palms leaning in along the sand.
            var bark = Mat(c, new Color(0.40f, 0.28f, 0.18f), 0.1f);
            var frond = Mat(c, new Color(0.12f, 0.36f, 0.16f), 0.15f);
            var rng = new System.Random(43);
            for (int i = 0; i < 26; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float x = side * (18f + (float)rng.NextDouble() * 22f), z = -12f + (float)rng.NextDouble() * 50f;
                float h = 7f + (float)rng.NextDouble() * 5f, lean = side * (8f + (float)rng.NextDouble() * 12f);
                var tr = Greybox.Box("PalmTrunk", c.Venue, bark, W(x, h / 2 - 0.5, z), new Vector3(0.45f, h, 0.45f));
                tr.transform.rotation = Quaternion.Euler(0, 0, lean);
                Vector3 top = W(x, -0.5, z) + tr.transform.rotation * Vector3.up * h;
                for (int f = 0; f < 7; f++)
                {
                    var leaf = Greybox.Box("PalmFrond", c.Venue, frond, top, new Vector3(0.9f, 0.08f, 4.2f));
                    leaf.transform.rotation = Quaternion.Euler(28f, f * 360f / 7f + (float)rng.NextDouble() * 20f, 0) * Quaternion.Euler(0, 0, 0);
                    leaf.transform.position = top + leaf.transform.rotation * Vector3.forward * 1.8f;
                }
            }
            // String lights between posts, and a small crowd on deckchairs.
            var bulb = Glow(c, new Color(2.6f, 1.7f, 0.8f));
            var bulbs = new MeshMerge();
            foreach (int side in new[] { -1, 1 })
                for (float z = -4f; z < 22f; z += 6.5f)
                {
                    Greybox.Box("LightPost", c.Venue, bark, W(side * 12.5f, 2.7f, z), new Vector3(0.14f, 5.4f, 0.14f), false);
                    Vector3 a0 = W(side * 12.5f, 5.4f, z), a1 = W(side * 12.5f, 5.4f, z + 6.5f);
                    for (int k = 0; k <= 10; k++)
                    {
                        float t = k / 10f;
                        bulbs.Box(Vector3.Lerp(a0, a1, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.7f, new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity);
                    }
                }
            bulbs.Build("StringLights", c.Venue, bulb);
            LegacyStands(c, 4, 53, true);
            FloodTowers(c, 13f, -5f, L + 13f, 14f);
            Ambient(new Color(0.62f, 0.44f, 0.46f), new Color(0.80f, 0.50f, 0.34f), new Color(0.40f, 0.30f, 0.22f));
            RenderSettings.fog = false;
            if (c.High) Point(c, "SunsetWarm", W(0, 8, -12), new Color(1f, 0.6f, 0.35f), 40f, 1.8f);
        }

        // ---------------- Space Station: in orbit, the Earth below the front wall's window ----------------

        static void Orbit(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("space", new Color(0, 0, 0), new Color(0.01f, 0.01f, 0.02f), new Color(0.005f, 0.005f, 0.015f), new Color(0, 0, 0.005f), 5200, 103), 650f);
            // The Earth, huge, below and beyond the front wall, its atmosphere a blue rim.
            var earthMat = Glow(c, new Color(1f, 1f, 1f)); earthMat.mainTexture = ProcTex.Earth();
            var earth = Greybox.Primitive(PrimitiveType.Sphere, "Earth", c.Venue, earthMat, false);
            earth.transform.position = W(0, -250, -760); earth.transform.localScale = Vector3.one * 640f;
            earth.transform.rotation = Quaternion.Euler(20f, 30f, 10f);
            var rim = c.Palette.Clear(new Color(0.35f, 0.6f, 1.6f, 0.25f));
            var atmo = Greybox.Primitive(PrimitiveType.Sphere, "Atmosphere", c.Venue, rim, false);
            atmo.transform.position = earth.transform.position; atmo.transform.localScale = Vector3.one * 668f;

            // The module: a floor of panels, the ribs of the hull arching over, rows of lights.
            var panel = Mat(c, Greybox.Hex(0x3a3f4a), 0.55f);
            var hull = Mat(c, Greybox.Hex(0xc9ced8), 0.5f);
            Deck(c, panel, 15f, -4f, 26f);
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x1a2030), Greybox.Hex(0x47c6ff));
            var strip = Glow(c, new Color(0.6f, 1.6f, 2.6f));
            const float R = 15f;
            for (float z = -3f; z <= 26f; z += 4.5f)
            {
                for (int s = 0; s < 14; s++)
                {
                    float a0 = Mathf.PI * s / 14f, a1 = Mathf.PI * (s + 1) / 14f;
                    Vector3 p0 = W(-Mathf.Cos(a0) * R, Mathf.Sin(a0) * R * 0.8f, z), p1 = W(-Mathf.Cos(a1) * R, Mathf.Sin(a1) * R * 0.8f, z);
                    var seg = Greybox.Box("HullRib", c.Venue, hull, (p0 + p1) / 2, new Vector3(0.5f, 0.5f, (p1 - p0).magnitude + 0.1f), false);
                    seg.transform.rotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
                }
                Greybox.Box("LightStrip", c.Venue, strip, W(0, R * 0.8f - 0.3f, z), new Vector3(3f, 0.06f, 0.3f), false);
            }
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("HullSpine", c.Venue, hull, W(side * (R - 0.3f), 0.5, 11.5), new Vector3(0.6f, 1f, 29f), false);
                Greybox.Box("FloorStrip", c.Venue, strip, W(side * (R - 1.2f), 0.02, 11.5), new Vector3(0.2f, 0.02f, 29f), false);
            }
            // Blinking-light consoles along the walls.
            var red = Glow(c, new Color(2.4f, 0.3f, 0.3f)); var green = Glow(c, new Color(0.3f, 2.4f, 0.6f));
            var rng = new System.Random(45);
            foreach (int side in new[] { -1, 1 })
                for (float z = 0f; z < 24f; z += 3f)
                {
                    Greybox.Box("Console", c.Venue, panel, W(side * (R - 2f), 0.6, z), new Vector3(1.2f, 1.2f, 2.2f));
                    for (int k = 0; k < 4; k++)
                        Greybox.Box("ConsoleLight", c.Venue, rng.NextDouble() < 0.5 ? red : green, W(side * (R - 2.62f), 0.9 + (k % 2) * 0.25, z - 0.6 + k * 0.4), new Vector3(0.02f, 0.08f, 0.08f), false);
                }
            LegacyStands(c, 6, 55);
            Gantry(c);
            Ambient(new Color(0.30f, 0.38f, 0.55f), new Color(0.24f, 0.28f, 0.36f), new Color(0.10f, 0.12f, 0.16f));
            RenderSettings.fog = false;
            if (c.High) Point(c, "EarthShine", W(0, 2, -14), new Color(0.5f, 0.75f, 1.2f), 40f, 2.2f);
        }

        // ---------------- Neon City: a rainy street at night, signs and towers lit ----------------

        static void Neon(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("neon", new Color(0.04f, 0.02f, 0.06f), new Color(0.32f, 0.10f, 0.30f), new Color(0.08f, 0.04f, 0.16f), new Color(0.02f, 0.01f, 0.05f), 300, 104), 650f);
            var wet = Mat(c, new Color(0.06f, 0.06f, 0.08f), 0.95f);
            Greybox.Box("WetStreet", c.Venue, wet, W(0, -0.55, 10), new Vector3(120f, 0.2f, 200f), false);
            Deck(c, Mat(c, Greybox.Hex(0x15151c), 0.85f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x1a0e24), Greybox.Hex(0xff3fbf));
            var neon = new[] { new Color(3f, 0.3f, 1.8f), new Color(0.3f, 2.6f, 3f), new Color(3f, 1.6f, 0.2f), new Color(1.2f, 0.4f, 3f) };
            Skyline(c, 140, 61, 60f, 480f, 0f, 360f, 30f, 260f, neon, -2f, true);
            // Shop fronts and signs down both sides of the street.
            var front = Mat(c, Greybox.Hex(0x1a1a22), 0.4f);
            var rng = new System.Random(47);
            foreach (int side in new[] { -1, 1 })
                for (float z = -30f; z < 50f; z += 7f)
                {
                    float h = 10f + (float)rng.NextDouble() * 20f;
                    Greybox.Box("Building", c.Venue, front, W(side * 26f, h / 2, z), new Vector3(8f, h, 6.5f));
                    var col = Glow(c, neon[rng.Next(neon.Length)]);
                    Greybox.Box("NeonSign", c.Venue, col, W(side * 21.9f, 3f + (float)rng.NextDouble() * (h - 5f), z), new Vector3(0.15f, 1.0f + (float)rng.NextDouble() * 2f, 3.5f), false);
                    Greybox.Box("NeonStrip", c.Venue, col, W(side * 21.95f, 0.6, z), new Vector3(0.05f, 0.08f, 6f), false);
                }
            // Rain: thin bright streaks hanging in the air.
            var rain = c.Palette.Clear(new Color(0.75f, 0.85f, 1f, 0.22f));
            var drops = new MeshMerge();
            for (int i = 0; i < 900; i++)
            {
                float x = -40f + (float)rng.NextDouble() * 80f, z = -30f + (float)rng.NextDouble() * 70f, y = (float)rng.NextDouble() * 20f;
                if (Mathf.Abs(x) < HW + 0.5f && z > -0.5f && z < L + 0.5f) continue;            // (not inside the court)
                drops.Box(W(x, y, z), new Vector3(0.015f, 0.9f, 0.015f), Quaternion.identity);
            }
            drops.Build("Rain", c.Venue, rain);
            LegacyStands(c, 6, 57);
            Gantry(c);
            SkyBeams(c, new Color(1.2f, 0.3f, 1.4f, 0.12f), 6, 63, -20f, 20f, -60f, -20f);
            Ambient(new Color(0.30f, 0.16f, 0.38f), new Color(0.22f, 0.14f, 0.30f), new Color(0.08f, 0.06f, 0.10f));
            Fog(new Color(0.12f, 0.06f, 0.16f), 40f, 420f);
            if (c.High)
            {
                Point(c, "NeonPink", W(-14, 4, 6), new Color(1f, 0.25f, 0.8f), 26f, 2.4f);
                Point(c, "NeonCyan", W(14, 4, 6), new Color(0.2f, 0.9f, 1f), 26f, 2.4f);
            }
        }

        // ---------------- Underwater Dome: on the sea floor under glass, fish and coral all round ----------------

        static void Reef(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("reef", new Color(0.01f, 0.05f, 0.09f), new Color(0.03f, 0.22f, 0.34f), new Color(0.06f, 0.42f, 0.58f), new Color(0.35f, 0.75f, 0.85f), 0, 105), 400f);
            var sand = Mat(c, new Color(0.30f, 0.52f, 0.56f), 0.05f); sand.mainTexture = ProcTex.Sand();
            Greybox.TexturedBox("SeaFloor", c.Venue, sand, W(0, -0.55, 0), new Vector3(700f, 0.2f, 700f), new Vector2(6f, 6f), false);
            Deck(c, Mat(c, Greybox.Hex(0x22303a), 0.6f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0e2d3c), Greybox.Hex(0x4ef2e0));
            // The dome over everything: a glass bubble ringed in steel.
            var glass = c.Palette.Clear(new Color(0.45f, 0.85f, 1f, 0.2f));
            var dome = Greybox.MeshObject("Dome", c.Venue, SkyDome(34f, 40, 20), glass);
            dome.transform.position = W(0, -0.5, L / 2);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI * 2f / 12f;
                for (int s = 0; s < 8; s++)
                {
                    float e0 = Mathf.PI / 2f * s / 8f, e1 = Mathf.PI / 2f * (s + 1) / 8f;
                    Vector3 p0 = W(Mathf.Cos(a) * Mathf.Cos(e0) * 34, Mathf.Sin(e0) * 34 - 0.5, L / 2 + Mathf.Sin(a) * Mathf.Cos(e0) * 34);
                    Vector3 p1 = W(Mathf.Cos(a) * Mathf.Cos(e1) * 34, Mathf.Sin(e1) * 34 - 0.5, L / 2 + Mathf.Sin(a) * Mathf.Cos(e1) * 34);
                    var rib = Greybox.Box("DomeRib", c.Venue, c.Steel, (p0 + p1) / 2, new Vector3(0.35f, 0.35f, (p1 - p0).magnitude + 0.1f), false);
                    rib.transform.rotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
                }
            }
            // Coral in bright clumps, kelp swaying tall, and shoals of fish outside the glass.
            var rng = new System.Random(49);
            var corals = new[] { new Color(1f, 0.38f, 0.42f), new Color(1f, 0.62f, 0.2f), new Color(0.7f, 0.35f, 0.95f), new Color(0.98f, 0.85f, 0.3f) };
            var coralMats = System.Array.ConvertAll(corals, col => Mat(c, col, 0.2f));
            var kelp = Mat(c, new Color(0.15f, 0.42f, 0.18f), 0.1f);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 37f + (float)rng.NextDouble() * 60f;
                Vector3 foot = W(Mathf.Cos(a) * r, -0.5, L / 2 + Mathf.Sin(a) * r);
                if (i % 3 == 0)
                {
                    float h = 6f + (float)rng.NextDouble() * 12f;
                    var k = Greybox.Box("Kelp", c.Venue, kelp, foot + Vector3.up * h / 2, new Vector3(0.4f, h, 0.15f), false);
                    k.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 180f, (float)rng.NextDouble() * 10f - 5f);
                }
                else
                {
                    var blob = Greybox.Primitive(PrimitiveType.Sphere, "Coral", c.Venue, coralMats[rng.Next(coralMats.Length)], false);
                    blob.transform.position = foot + Vector3.up * 0.6f;
                    blob.transform.localScale = new Vector3(1.5f + (float)rng.NextDouble() * 3f, 1f + (float)rng.NextDouble() * 2.5f, 1.5f + (float)rng.NextDouble() * 3f);
                }
            }
            var fishCols = new[] { new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.7f, 1f), new Color(1f, 0.95f, 0.3f), new Color(0.95f, 0.95f, 1f) };
            for (int s = 0; s < fishCols.Length; s++)
            {
                var shoal = new MeshMerge();
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 42f + (float)rng.NextDouble() * 30f, y = 4f + (float)rng.NextDouble() * 16f;
                Vector3 centre = W(Mathf.Cos(a) * r, y, L / 2 + Mathf.Sin(a) * r);
                Quaternion heading = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                for (int f = 0; f < 40; f++)
                {
                    Vector3 off = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.5f, (float)rng.NextDouble() - 0.5f) * 9f;
                    shoal.Box(centre + off, new Vector3(0.18f, 0.32f, 0.7f), heading);
                }
                shoal.Build("Fish", c.Venue, Mat(c, fishCols[s], 0.6f));
            }
            SkyBeams(c, new Color(0.6f, 0.95f, 1.2f, 0.08f), 9, 65, -30f, 30f, -40f, 30f);
            Gantry(c);
            LegacyStands(c, 6, 59);
            Ambient(new Color(0.20f, 0.48f, 0.58f), new Color(0.12f, 0.32f, 0.42f), new Color(0.06f, 0.14f, 0.18f));
            Fog(new Color(0.05f, 0.30f, 0.42f), 20f, 110f);
            if (c.High) Point(c, "SeaLight", W(0, 18, L / 2), new Color(0.5f, 0.9f, 1f), 50f, 2f);
        }

        // ---------------- Jungle Temple: stone ruins in the rainforest, torches and mist ----------------

        static void Temple(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("jungle", new Color(0.04f, 0.06f, 0.04f), new Color(0.55f, 0.48f, 0.30f), new Color(0.28f, 0.40f, 0.36f), new Color(0.08f, 0.14f, 0.18f), 60, 106), 600f);
            var ground = Mat(c, new Color(0.16f, 0.22f, 0.10f), 0.05f);
            Greybox.Box("JungleFloor", c.Venue, ground, W(0, -0.6, 0), new Vector3(800f, 0.2f, 800f), false);
            var stone = Mat(c, new Color(0.52f, 0.50f, 0.42f), 0.1f); stone.mainTexture = ProcTex.Stone();
            Deck(c, stone);
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x2a2a1a), Greybox.Hex(0xe0b24a));

            // The stepped temple beyond the front wall, a shrine on top, stairs up the middle.
            float baseY = -0.5f;
            for (int k = 0; k < 7; k++)
            {
                float w = 52f - k * 6.5f, h = 4.2f;
                Greybox.TexturedBox("TempleTier", c.Venue, stone, W(0, baseY + h * k + h / 2, -55), new Vector3(w, h, w * 0.8f), new Vector2(4f, 4f));
            }
            Greybox.TexturedBox("Shrine", c.Venue, stone, W(0, baseY + 29.4 + 3, -55), new Vector3(9f, 6f, 7f), new Vector2(3f, 3f));
            var dark = Mat(c, new Color(0.05f, 0.05f, 0.04f), 0f);
            Greybox.Box("ShrineDoor", c.Venue, dark, W(0, baseY + 29.4 + 2.2, -51.4), new Vector3(2.4f, 4f, 0.2f), false);
            Greybox.Box("Stairs", c.Venue, Mat(c, new Color(0.44f, 0.42f, 0.36f), 0.1f), W(0, baseY + 14, -32), new Vector3(7f, 0.6f, 38f), false).transform.rotation = Quaternion.Euler(-37f, 0, 0);
            // Broken pillars round the deck.
            var rng = new System.Random(51);
            foreach (int side in new[] { -1, 1 })
                for (float z = -6f; z < 28f; z += 5.5f)
                {
                    float h = 3f + (float)rng.NextDouble() * 5f;
                    Greybox.TexturedBox("Pillar", c.Venue, stone, W(side * (15f + (float)rng.NextDouble() * 2f), h / 2 - 0.5, z), new Vector3(1.1f, h, 1.1f), new Vector2(1f, 2f));
                }
            // The rainforest: tall trunks under round canopies, all round.
            var bark = Mat(c, new Color(0.22f, 0.17f, 0.11f), 0.05f);
            var leaves = new[] { Mat(c, new Color(0.10f, 0.30f, 0.10f), 0.1f), Mat(c, new Color(0.14f, 0.38f, 0.14f), 0.1f), Mat(c, new Color(0.08f, 0.24f, 0.12f), 0.1f) };
            for (int i = 0; i < 90; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 28f + (float)rng.NextDouble() * 90f;
                if (InCameraLane(Mathf.Cos(a) * r, L / 2 + Mathf.Sin(a) * r)) continue;
                Vector3 foot = W(Mathf.Cos(a) * r, -0.5, L / 2 + Mathf.Sin(a) * r);
                if ((foot - W(0, 0, -55)).magnitude < 34f) continue;                     // (the temple stays clear)
                float h = 12f + (float)rng.NextDouble() * 16f;
                Greybox.Box("Trunk", c.Venue, bark, foot + Vector3.up * h / 2, new Vector3(0.9f, h, 0.9f), false);
                var crown = Greybox.Primitive(PrimitiveType.Sphere, "Canopy", c.Venue, leaves[rng.Next(leaves.Length)], false);
                crown.transform.position = foot + Vector3.up * h;
                float s = 7f + (float)rng.NextDouble() * 8f;
                crown.transform.localScale = new Vector3(s, s * 0.55f, s);
            }
            // Torches on posts, their fires glowing.
            var fire = Glow(c, new Color(3.2f, 1.4f, 0.3f));
            foreach (int side in new[] { -1, 1 })
                for (float z = -4f; z < 26f; z += 7.5f)
                {
                    Greybox.Box("TorchPost", c.Venue, bark, W(side * 12.5f, 1.2, z), new Vector3(0.18f, 2.4f, 0.18f), false);
                    Greybox.Box("TorchFire", c.Venue, fire, W(side * 12.5f, 2.6, z), new Vector3(0.4f, 0.6f, 0.4f), false);
                    if (c.High && z < 10f) Point(c, "Torch", W(side * 12.5f, 2.8, z), new Color(1f, 0.6f, 0.25f), 9f, 1.6f);
                }
            LegacyStands(c, 6, 61);
            FloodTowers(c, 13.5f, -6f, L + 14f, 16f);
            Ambient(new Color(0.34f, 0.42f, 0.32f), new Color(0.30f, 0.32f, 0.22f), new Color(0.14f, 0.16f, 0.10f));
            Fog(new Color(0.30f, 0.36f, 0.28f), 30f, 220f);
        }

        // ---------------- Volcano: black rock by an erupting peak, lava glowing ----------------

        static void Volcano(Ctx c)
        {
            SkyOf(c, ProcTex.LegacySky("volcano", new Color(0.06f, 0.02f, 0.01f), new Color(0.60f, 0.18f, 0.06f), new Color(0.22f, 0.08f, 0.06f), new Color(0.04f, 0.02f, 0.03f), 200, 107), 650f);
            var basalt = Mat(c, new Color(0.10f, 0.09f, 0.09f), 0.25f);
            Greybox.Box("Basalt", c.Venue, basalt, W(0, -0.6, 0), new Vector3(900f, 0.2f, 900f), false);
            Deck(c, Mat(c, Greybox.Hex(0x22201f), 0.35f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x2a1410), Greybox.Hex(0xff6a1a));

            // The volcano beyond the front wall: a broad dark cone, the crater glowing, lava down its side.
            var cone = Glow(c, new Color(0.20f, 0.08f, 0.05f));                       // lit from below by its own lava
            Cone(c, "Volcano", cone, W(0, -0.6, -230), 190f, 150f, 24, 30f);
            var lava = Glow(c, new Color(2.2f, 0.5f, 0.06f));
            var crater = Greybox.MeshObject("Crater", c.Venue, Greybox.Disc(27f, 32), lava);
            crater.transform.position = W(0, 149.2, -230); crater.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var rng = new System.Random(53);
            for (int k = 0; k < 5; k++)
            {
                float a = -0.6f + k * 0.3f;
                Vector3 top = W(Mathf.Sin(a) * 28f, 148, -230 + Mathf.Cos(a) * 28f), bottom = W(Mathf.Sin(a) * 185f, 0, -230 + Mathf.Cos(a) * 185f);
                var flow = Greybox.Box("LavaFlow", c.Venue, lava, (top + bottom) / 2, new Vector3(3f + k % 2 * 2f, 0.6f, (bottom - top).magnitude), false);
                flow.transform.rotation = Quaternion.LookRotation(bottom - top, Vector3.up);
            }
            // Lava rivers across the plain, cracks glowing between the rocks, embers in the air.
            foreach (int side in new[] { -1, 1 })
            {
                var river = Greybox.Box("LavaRiver", c.Venue, lava, W(side * 30f, -0.48, 0), new Vector3(4f, 0.05f, 260f), false);
                river.transform.rotation = Quaternion.Euler(0, side * 12f, 0);
            }
            var rock = Mat(c, new Color(0.08f, 0.07f, 0.07f), 0.2f);
            for (int i = 0; i < 50; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 24f + (float)rng.NextDouble() * 70f;
                var b = Greybox.Primitive(PrimitiveType.Sphere, "Boulder", c.Venue, rock, false);
                b.transform.position = W(Mathf.Cos(a) * r, -0.4, L / 2 + Mathf.Sin(a) * r);
                b.transform.localScale = new Vector3(2f + (float)rng.NextDouble() * 5f, 1.5f + (float)rng.NextDouble() * 3f, 2f + (float)rng.NextDouble() * 5f);
            }
            var ember = Glow(c, new Color(3f, 1.1f, 0.2f));
            var embers = new MeshMerge();
            for (int i = 0; i < 500; i++)
            {
                float x = -60f + (float)rng.NextDouble() * 120f, z = -120f + (float)rng.NextDouble() * 140f, y = (float)rng.NextDouble() * 45f;
                if (Mathf.Abs(x) < HW + 1f && z > -1f) continue;
                embers.Box(W(x, y, z), Vector3.one * 0.12f, Quaternion.identity);
            }
            embers.Build("Embers", c.Venue, ember);
            LegacyStands(c, 7, 63);
            FloodTowers(c, 13.5f, -6f, L + 14f, 18f);
            Ambient(new Color(0.40f, 0.16f, 0.10f), new Color(0.32f, 0.12f, 0.08f), new Color(0.20f, 0.08f, 0.05f));
            Fog(new Color(0.30f, 0.10f, 0.05f), 80f, 700f);
            if (c.High)
            {
                Point(c, "LavaGlowL", W(-30, 2, 0), new Color(1f, 0.4f, 0.1f), 30f, 2.2f);
                Point(c, "LavaGlowR", W(30, 2, 0), new Color(1f, 0.4f, 0.1f), 30f, 2.2f);
            }
        }
    }
}
