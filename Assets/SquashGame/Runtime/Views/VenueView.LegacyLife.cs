// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.LegacyLife.cs
// 2026-11-22 evening (the user: "for the legacy courts i love all the ideas
// but some of them looks a bit boring and too flat like the beach side one
// maybe adding more different elements and 3D things like moving waves that
// gets high and low would be good, do this with every legacy court"): each
// made-up court gets more things, and things that move (VenueMotion):
//
//   Beach Sunset    swells rising and falling with their foam, sailboats
//                   rocking, gulls wheeling, umbrellas and a lifeguard tower
//   Mountain Peak   the aurora's ribbons swaying, snow falling, a chairlift
//   Space Station   the Earth turning, satellites going round, rocks tumbling
//   Neon City       the rain falling, flying cars, signs flickering
//   Underwater Dome shoals circling, kelp swaying, bubbles, jellyfish, a ray
//   Jungle Temple   torches flickering, birds circling, a waterfall, fireflies
//   Volcano         lava bursting from the crater, smoke rising, embers
//   Desert Night    hot-air balloons drifting, searchlights sweeping
//   City Rooftop    a helicopter circling, a crane swinging, blinking masts
//
// Nothing tall in the camera lane behind the back wall. Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static partial class VenueView
    {
        /// <summary>The legacy courts' extra life (after their build).</summary>
        static void LegacyLife(Ctx c, string id)
        {
            switch (id)
            {
                case "beach": BeachLife(c); break;
                case "peak": PeakLife(c); break;
                case "orbit": OrbitLife(c); break;
                case "neon": NeonLife(c); break;
                case "reef": ReefLife(c); break;
                case "temple": TempleLife(c); break;
                case "volcano": VolcanoLife(c); break;
                case "desert": DesertLife(c); break;
                case "rooftop": RooftopLife(c); break;
            }
        }

        static Vector3 Dir(double x, double y, double z) { return (W(x, y, z) - W(0, 0, 0)).normalized; }
        static GameObject Move(GameObject go, VenueMotion.Kind k, Vector3 axis, float amount, float speed, float phase = 0f, float span = 10f)
        { VenueMotion.Add(go, k, axis, amount, speed, phase, span); return go; }
        /// <summary>A box placed in the world, then hung on a moving parent.</summary>
        static GameObject Kid(Ctx c, string name, Transform parent, Material mat, Vector3 worldCentre, Vector3 size, bool shadows = false)
        { var go = Greybox.Box(name, c.Venue, mat, worldCentre, size, shadows); go.transform.SetParent(parent, true); return go; }
        static GameObject Pivot(Ctx c, string name, Vector3 at) { var p = new GameObject(name); p.transform.SetParent(c.Venue, false); p.transform.position = at; return p; }

        /// <summary>Two layers of a falling (or rising) field, half a span apart, so the wrap never shows.</summary>
        static void Field(Ctx c, string name, Material mat, int count, float x0, float x1, float z0, float z1, float y0, float span, Vector3 size, Vector3 dir, float speed, int seed)
        {
            var rng = new System.Random(seed);
            for (int layer = 0; layer < 2; layer++)
            {
                var m = new MeshMerge();
                for (int i = 0; i < count / 2; i++)
                {
                    float x = x0 + (float)rng.NextDouble() * (x1 - x0), z = z0 + (float)rng.NextDouble() * (z1 - z0), y = y0 + (float)rng.NextDouble() * span;
                    if (Mathf.Abs(x) < HW + 0.8f && z > -0.8f && z < L + 0.8f) continue;           // (never inside the court)
                    m.Box(W(x, y, z), size, Quaternion.identity);
                }
                var go = m.Build(name, c.Venue, mat);
                // the layer starts shifted back half a span along the way it goes, then travels a span and wraps
                go.transform.position -= dir * (span * (layer == 0 ? 0f : 0.5f));
                Move(go, VenueMotion.Kind.Drift, dir, speed, 1f, layer * span * 0.5f / Mathf.Max(0.01f, speed), span);
            }
        }

        // ---------------- Beach Sunset ----------------
        static void BeachLife(Ctx c)
        {
            var rng = new System.Random(201);
            var swell = Mat(c, new Color(0.12f, 0.27f, 0.42f), 0.95f);
            var foam = Glow(c, new Color(1.3f, 1.2f, 1.1f));
            // the swells: long ridges of water, each rising and falling on its own beat, foam on their crests
            for (int k = 0; k < 9; k++)
            {
                float z = -34f - k * 11f, h = 1.2f + k * 0.35f;
                // a rounded hump of water (a long flattened ball), its foam a thin bright line along the top
                var w = Greybox.Primitive(PrimitiveType.Sphere, "Swell", c.Venue, swell, false);
                w.transform.position = W(0, -1.2 + h * 0.15, z); w.transform.localScale = new Vector3(420f, h * 2f, 7f + k * 1.5f);
                var crest = Greybox.Box("Crest", c.Venue, foam, W(0, -1.2 + h * 1.12, z + 0.4f), new Vector3(400f, 0.16f, 0.6f), false);
                crest.transform.SetParent(w.transform, true);
                Move(w, VenueMotion.Kind.Bob, Vector3.up, 0.5f + k * 0.18f, 0.55f + 0.05f * k, k * 0.9f);
            }
            // breakers rolling in to the shore: foam lines sliding up the sand and back
            for (int k = 0; k < 3; k++)
            {
                var br = Greybox.Box("Breaker", c.Venue, foam, W(0, -0.5, -16 - k * 6), new Vector3(300f, 0.08f, 1.0f), false);
                Move(br, VenueMotion.Kind.Bob, Dir(0, 0, 1), 3f, 0.5f, k * 2.1f);
            }
            // sailboats out on the water, rocking
            var hull = Mat(c, new Color(0.92f, 0.9f, 0.86f), 0.4f);
            var sails = new[] { Mat(c, new Color(1f, 0.95f, 0.88f), 0.1f), Mat(c, new Color(0.95f, 0.35f, 0.3f), 0.1f), Mat(c, new Color(0.3f, 0.55f, 0.95f), 0.1f) };
            for (int k = 0; k < 6; k++)
            {
                float x = -140f + k * 55f + (float)rng.NextDouble() * 20f, z = -90f - (float)rng.NextDouble() * 120f;
                var boat = Pivot(c, "Sailboat", W(x, -0.6, z));
                var hb = Kid(c, "Hull", boat.transform, hull, boat.transform.position + Vector3.up * 0.6f, new Vector3(2.2f, 1.2f, 7f), false);
                var sail = Kid(c, "Sail", boat.transform, sails[k % sails.Length], boat.transform.position + Vector3.up * 6.5f, new Vector3(0.1f, 10f, 4.5f), false);
                Move(boat, VenueMotion.Kind.Sway, Vector3.forward, 5f, 0.9f + 0.1f * k, k);
                Move(hb, VenueMotion.Kind.Bob, Vector3.up, 0.35f, 1.1f, k * 0.7f);
            }
            // gulls wheeling over the water
            var gull = Mat(c, new Color(0.95f, 0.95f, 0.95f), 0.2f);
            for (int f = 0; f < 3; f++)
            {
                var p = Pivot(c, "Gulls", W(-20 + f * 25, 16 + f * 4, -40 - f * 15));
                for (int g = 0; g < 4; g++)
                {
                    float a = g * Mathf.PI / 2f, r = 8f + g * 2f;
                    var b = Kid(c, "Gull", p.transform, gull, p.transform.position + new Vector3(Mathf.Cos(a) * r, g * 0.8f, Mathf.Sin(a) * r), new Vector3(1.6f, 0.06f, 0.35f), false);
                    Move(b, VenueMotion.Kind.Sway, Vector3.forward, 25f, 4f + g, g);
                }
                Move(p, VenueMotion.Kind.Spin, Vector3.up, 18f + f * 6f, 1f);
            }
            // umbrellas and a lifeguard tower on the sand down the sides
            var cols = new[] { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.7f, 1f), new Color(0.3f, 0.9f, 0.5f) };
            var pole = Mat(c, new Color(0.9f, 0.9f, 0.9f), 0.2f);
            for (int k = 0; k < 12; k++)
            {
                int side = k % 2 == 0 ? -1 : 1;
                float x = side * (17f + (float)rng.NextDouble() * 16f), z = -10f + (float)rng.NextDouble() * 34f;
                Greybox.Box("UmbrellaPole", c.Venue, pole, W(x, 1.0, z), new Vector3(0.08f, 2.6f, 0.08f), false);
                Cone(c, "Umbrella", Mat(c, cols[k % cols.Length], 0.2f), W(x, 2.0, z), 1.8f, 0.8f, 10);
            }
            var wood = Mat(c, new Color(0.55f, 0.38f, 0.22f), 0.2f);
            foreach (var leg in new[] { -1f, 1f })
                foreach (var lz in new[] { -1f, 1f }) Greybox.Box("TowerLeg", c.Venue, wood, W(-24 + leg * 1.1, 2.0, 6 + lz * 1.1), new Vector3(0.2f, 4.6f, 0.2f), false);
            Greybox.Box("TowerHut", c.Venue, Mat(c, new Color(0.95f, 0.85f, 0.25f), 0.2f), W(-24, 5.2, 6), new Vector3(3.2f, 2f, 3.2f), false);
            Greybox.Box("TowerRoof", c.Venue, Mat(c, new Color(0.9f, 0.25f, 0.2f), 0.2f), W(-24, 6.45, 6), new Vector3(3.8f, 0.3f, 3.8f), false);
        }

        // ---------------- Mountain Peak ----------------
        static void PeakLife(Ctx c)
        {
            // the aurora: tall soft ribbons high beyond the front wall, swaying and lifting
            var greens = new[] { new Color(0.3f, 1.6f, 0.9f, 0.16f), new Color(0.4f, 1.2f, 1.6f, 0.13f), new Color(0.9f, 0.5f, 1.5f, 0.10f) };
            for (int k = 0; k < 7; k++)
            {
                var m = c.Palette.Clear(greens[k % greens.Length]); m.mainTexture = ProcTex.Beam();
                var q = Greybox.Primitive(PrimitiveType.Quad, "Aurora", c.Venue, m, false);
                q.transform.position = W(-160 + k * 55, 90 + (k % 3) * 18, -260 - k * 12);
                q.transform.rotation = Quaternion.Euler(0, 15f * (k - 3), 0);
                q.transform.localScale = new Vector3(70f, 110f, 1f);
                Move(q, VenueMotion.Kind.Sway, Vector3.up, 9f, 0.18f + k * 0.02f, k);
                Move(q, VenueMotion.Kind.Bob, Vector3.up, 6f, 0.12f, k * 1.3f);
            }
            // snow falling all round
            Field(c, "Snow", Glow(c, new Color(1.2f, 1.25f, 1.35f)), 1400, -60f, 60f, -50f, 60f, 0f, 26f, Vector3.one * 0.09f, Dir(0.15, -1, 0), 1.6f, 211);
            // a chairlift up the slope to the side: towers, the cable, chairs going up
            var steel = c.Steel; var seat = Mat(c, new Color(0.85f, 0.2f, 0.2f), 0.3f);
            Vector3 a = W(34, 0, 20), b = W(110, 70, -120);
            for (int k = 0; k <= 5; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / 5f);
                Greybox.Box("LiftTower", c.Venue, steel, p + Vector3.up * 5f, new Vector3(0.6f, 10f, 0.6f), false);
            }
            var cable = Greybox.Box("LiftCable", c.Venue, c.Dark, (a + b) / 2 + Vector3.up * 10f, new Vector3(0.08f, 0.08f, (b - a).magnitude), false);
            cable.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Vector3 dir = (b - a).normalized; float len = (b - a).magnitude;
            for (int k = 0; k < 12; k++)
            {
                var ch = Greybox.Box("LiftChair", c.Venue, seat, a + Vector3.up * 8.6f, new Vector3(1.4f, 0.8f, 0.9f), false);
                Move(ch, VenueMotion.Kind.Drift, dir, 3f, 1f, k * len / 12f / 3f, len);
            }
        }

        // ---------------- Space Station ----------------
        static void OrbitLife(Ctx c)
        {
            var earth = c.Venue.Find("Earth");
            if (earth != null) Move(earth.gameObject, VenueMotion.Kind.Spin, new Vector3(0.2f, 1f, 0f).normalized, 1.2f, 1f);
            var metal = Mat(c, new Color(0.8f, 0.82f, 0.86f), 0.6f);
            var panel = Glow(c, new Color(0.15f, 0.3f, 0.9f));
            // satellites going round the station, beyond the window
            for (int k = 0; k < 4; k++)
            {
                var p = Pivot(c, "SatOrbit", W(0, 10 + k * 6, -60 - k * 20));
                var sat = Pivot(c, "Satellite", p.transform.position + new Vector3(45f + k * 12f, 0, 0));
                sat.transform.SetParent(p.transform, true);
                Kid(c, "SatBody", sat.transform, metal, sat.transform.position, new Vector3(1.4f, 1.4f, 2f), false);
                Kid(c, "SatPanel", sat.transform, panel, sat.transform.position, new Vector3(9f, 0.05f, 1.6f), false);
                Move(sat, VenueMotion.Kind.Spin, Vector3.forward, 20f, 1f);
                Move(p, VenueMotion.Kind.Spin, Vector3.up, 6f + k * 2f, 1f, k * 40f);
            }
            // rocks tumbling past
            var rock = Mat(c, new Color(0.35f, 0.32f, 0.30f), 0.1f);
            var rng = new System.Random(213);
            for (int k = 0; k < 14; k++)
            {
                var r = Greybox.Primitive(PrimitiveType.Sphere, "Asteroid", c.Venue, rock, false);
                r.transform.position = W(-160 + (float)rng.NextDouble() * 60, 20 + (float)rng.NextDouble() * 60, -80 - (float)rng.NextDouble() * 160);
                r.transform.localScale = new Vector3(2f + (float)rng.NextDouble() * 6f, 1.5f + (float)rng.NextDouble() * 4f, 2f + (float)rng.NextDouble() * 5f);
                Move(r, VenueMotion.Kind.Spin, Random3(rng), 25f, 1f);
                Move(r, VenueMotion.Kind.Drift, Dir(1, 0.05, 0.1), 4f, 1f, (float)rng.NextDouble() * 60f, 320f);
            }
        }
        static Vector3 Random3(System.Random r) { return new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f).normalized; }

        // ---------------- Neon City ----------------
        static void NeonLife(Ctx c)
        {
            var rain = c.Venue.Find("Rain");
            if (rain != null) Object.Destroy(rain.gameObject);                        // (the old still rain: now it falls)
            Field(c, "Rain", c.Palette.Clear(new Color(0.75f, 0.85f, 1f, 0.24f)), 1400, -40f, 40f, -30f, 40f, 0f, 22f, new Vector3(0.015f, 0.9f, 0.015f), Dir(0.05, -1, 0), 14f, 221);
            // flying cars crossing the sky between the towers, their lights streaking
            var cols = new[] { new Color(3f, 0.4f, 1.6f), new Color(0.3f, 2.4f, 3f), new Color(3f, 2.2f, 0.6f) };
            var body = Mat(c, new Color(0.12f, 0.12f, 0.16f), 0.8f);
            for (int k = 0; k < 10; k++)
            {
                int dirSign = k % 2 == 0 ? 1 : -1;
                var car = Pivot(c, "FlyingCar", W(-dirSign * 120, 22 + (k % 5) * 7, -40 - (k % 4) * 25));
                Kid(c, "CarBody", car.transform, body, car.transform.position, new Vector3(3.2f, 0.9f, 1.6f), false);
                Kid(c, "CarLight", car.transform, Glow(c, cols[k % cols.Length]), car.transform.position + Vector3.down * 0.5f, new Vector3(3.4f, 0.1f, 1.7f), false);
                Move(car, VenueMotion.Kind.Drift, Dir(dirSign, 0, 0), 18f + k, 1f, k * 3f, 240f);
            }
            // the signs flicker
            int n = 0;
            foreach (Transform t in c.Venue)
                if (t.name == "NeonSign" && n++ % 3 == 0) Move(t.gameObject, VenueMotion.Kind.Pulse, Vector3.one, 0.08f, 7f + n % 5, n);
        }

        // ---------------- Underwater Dome ----------------
        static void ReefLife(Ctx c)
        {
            // the shoals circle the dome
            int s = 0;
            foreach (Transform t in c.Venue)
            {
                if (t.name != "Fish") continue;
                var p = Pivot(c, "ShoalPivot", W(0, 0, L / 2));
                t.SetParent(p.transform, true);
                Move(p.gameObject, VenueMotion.Kind.Spin, Vector3.up, (s % 2 == 0 ? 1 : -1) * (6f + s * 2f), 1f);
                s++;
                if (s > 8) break;
            }
            foreach (Transform t in c.Venue) if (t.name == "Fish") Move(t.gameObject, VenueMotion.Kind.Bob, Vector3.up, 1.2f, 0.6f, t.GetSiblingIndex());
            // kelp swaying (the old ones, and new tall fronds)
            var kelp = Mat(c, new Color(0.15f, 0.45f, 0.2f), 0.1f);
            var rng = new System.Random(231);
            for (int i = 0; i < 40; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 36f + (float)rng.NextDouble() * 40f;
                float h = 8f + (float)rng.NextDouble() * 14f;
                var root = Pivot(c, "KelpRoot", W(Mathf.Cos(a) * r, -0.5, L / 2 + Mathf.Sin(a) * r));
                Kid(c, "Kelp", root.transform, kelp, root.transform.position + Vector3.up * h / 2, new Vector3(0.5f, h, 0.15f), false);
                Move(root, VenueMotion.Kind.Sway, Vector3.forward, 7f, 0.5f + (float)rng.NextDouble() * 0.4f, i);
            }
            // bubbles rising, jellyfish drifting up and down, a ray gliding by
            Field(c, "Bubbles", c.Palette.Clear(new Color(0.8f, 1f, 1.1f, 0.35f)), 600, -50f, 50f, -40f, 60f, -0.5f, 30f, Vector3.one * 0.25f, Vector3.up, 2.4f, 233);
            var jelly = c.Palette.Clear(new Color(1.2f, 0.6f, 1.4f, 0.45f));
            for (int k = 0; k < 10; k++)
            {
                float a = k * 0.63f, r = 40f + (k % 3) * 8f;
                var j = Greybox.Primitive(PrimitiveType.Sphere, "Jellyfish", c.Venue, jelly, false);
                j.transform.position = W(Mathf.Cos(a) * r, 8 + k % 4 * 4, L / 2 + Mathf.Sin(a) * r);
                j.transform.localScale = new Vector3(2.2f, 1.4f, 2.2f);
                Move(j, VenueMotion.Kind.Bob, Vector3.up, 2.5f, 0.4f, k);
                Move(j, VenueMotion.Kind.Pulse, Vector3.one, 0.12f, 1.6f, k);
            }
            var ray = Pivot(c, "RayPivot", W(0, 16, L / 2));
            var rayBody = Kid(c, "Ray", ray.transform, Mat(c, new Color(0.25f, 0.3f, 0.36f), 0.4f), ray.transform.position + new Vector3(48f, 0, 0), new Vector3(7f, 0.4f, 5f), false);
            Move(rayBody, VenueMotion.Kind.Sway, Vector3.forward, 14f, 1.1f);
            Move(ray, VenueMotion.Kind.Spin, Vector3.up, 7f, 1f);
        }

        // ---------------- Jungle Temple ----------------
        static void TempleLife(Ctx c)
        {
            foreach (Transform t in c.Venue) if (t.name == "TorchFire") Move(t.gameObject, VenueMotion.Kind.Pulse, Vector3.one, 0.18f, 9f + t.GetSiblingIndex() % 4, t.GetSiblingIndex());
            // birds circling over the canopy
            var bird = Mat(c, new Color(0.9f, 0.2f, 0.15f), 0.2f);
            for (int f = 0; f < 3; f++)
            {
                var p = Pivot(c, "Birds", W(-30 + f * 30, 30 + f * 6, -30 - f * 20));
                for (int g = 0; g < 5; g++)
                {
                    float a = g * Mathf.PI * 2 / 5f, r = 10f + g * 2f;
                    var b = Kid(c, "Bird", p.transform, bird, p.transform.position + new Vector3(Mathf.Cos(a) * r, g * 0.6f, Mathf.Sin(a) * r), new Vector3(1.3f, 0.06f, 0.4f), false);
                    Move(b, VenueMotion.Kind.Sway, Vector3.forward, 28f, 5f + g, g);
                }
                Move(p, VenueMotion.Kind.Spin, Vector3.up, 14f + f * 5f, 1f);
            }
            // a waterfall down the cliff to the left of the temple, its pool, the spray
            var rock = Mat(c, new Color(0.30f, 0.30f, 0.26f), 0.1f);
            Greybox.Box("Cliff", c.Venue, rock, W(-60, 18, -70), new Vector3(24f, 37f, 14f));
            var water = c.Palette.Clear(new Color(0.75f, 0.9f, 1.1f, 0.5f));
            Greybox.Box("Falls", c.Venue, water, W(-60, 18, -62.8), new Vector3(7f, 36f, 0.3f), false);
            Field(c, "FallsFoam", Glow(c, new Color(1.3f, 1.4f, 1.5f)), 300, -63.4f, -56.6f, -62.6f, -62.2f, 0f, 36f, new Vector3(0.5f, 1.4f, 0.1f), Vector3.down, 12f, 241);
            Greybox.Box("Pool", c.Venue, Mat(c, new Color(0.12f, 0.32f, 0.36f), 0.95f), W(-60, -0.45, -58), new Vector3(18f, 0.1f, 10f), false);
            // fireflies over the deck's edge
            var fly = Glow(c, new Color(2.4f, 2.2f, 0.5f));
            var rng = new System.Random(243);
            for (int k = 0; k < 40; k++)
            {
                float x = (k % 2 == 0 ? -1 : 1) * (15f + (float)rng.NextDouble() * 14f), z = -8f + (float)rng.NextDouble() * 34f;
                var f2 = Greybox.Box("Firefly", c.Venue, fly, W(x, 1.5 + (float)rng.NextDouble() * 4, z), Vector3.one * 0.12f, false);
                Move(f2, VenueMotion.Kind.Bob, Random3(rng), 0.8f, 0.7f + (float)rng.NextDouble(), k);
            }
        }

        // ---------------- Volcano ----------------
        static void VolcanoLife(Ctx c)
        {
            // lava bursting from the crater: glowing bombs flung up and falling back (rise, then wrap)
            var lava = Glow(c, new Color(3f, 0.8f, 0.1f));
            var rng = new System.Random(251);
            for (int k = 0; k < 24; k++)
            {
                var bomb = Greybox.Primitive(PrimitiveType.Sphere, "LavaBomb", c.Venue, lava, false);
                bomb.transform.position = W((float)rng.NextDouble() * 30 - 15, 150, -230 + (float)rng.NextDouble() * 30 - 15);
                bomb.transform.localScale = Vector3.one * (3f + (float)rng.NextDouble() * 4f);
                Move(bomb, VenueMotion.Kind.Drift, Random3Up(rng), 14f + (float)rng.NextDouble() * 10f, 1f, (float)rng.NextDouble() * 5f, 70f);
            }
            // the plume: puffs of smoke rising and swelling
            var smoke = c.Palette.Clear(new Color(0.18f, 0.12f, 0.12f, 0.55f));
            for (int k = 0; k < 10; k++)
            {
                var puff = Greybox.Primitive(PrimitiveType.Sphere, "Smoke", c.Venue, smoke, false);
                puff.transform.position = W(k * 3 - 15, 160, -232);
                puff.transform.localScale = Vector3.one * (30f + k * 4f);
                Move(puff, VenueMotion.Kind.Drift, Dir(0.25, 1, 0), 6f, 1f, k * 4f, 150f);
                Move(puff, VenueMotion.Kind.Pulse, Vector3.one, 0.15f, 0.4f, k);
            }
            // embers rising round the deck, and the lava rivers breathing
            Field(c, "EmbersRising", Glow(c, new Color(3f, 1.1f, 0.2f)), 700, -60f, 60f, -80f, 40f, 0f, 40f, Vector3.one * 0.12f, Dir(0.1, 1, 0), 3.5f, 253);
            foreach (Transform t in c.Venue) if (t.name == "LavaRiver" || t.name == "Crater") Move(t.gameObject, VenueMotion.Kind.Pulse, Vector3.one, 0.04f, 1.3f, t.GetSiblingIndex());
        }
        static Vector3 Random3Up(System.Random r) { return new Vector3((float)r.NextDouble() - 0.5f, 1.6f, (float)r.NextDouble() - 0.5f).normalized; }

        // ---------------- Desert Night ----------------
        static void DesertLife(Ctx c)
        {
            var cols = new[] { new Color(1f, 0.35f, 0.25f), new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.6f, 1f), new Color(0.6f, 0.9f, 0.4f) };
            var basket = Mat(c, new Color(0.4f, 0.28f, 0.16f), 0.2f);
            var flame = Glow(c, new Color(3f, 1.6f, 0.4f));
            for (int k = 0; k < 6; k++)
            {
                var b = Pivot(c, "Balloon", W(-120 + k * 45, 40 + (k % 3) * 18, -140 - (k % 2) * 60));
                var env = Greybox.Primitive(PrimitiveType.Sphere, "Envelope", c.Venue, Mat(c, cols[k % cols.Length], 0.3f), false);
                env.transform.SetParent(b.transform, false); env.transform.localScale = new Vector3(14f, 17f, 14f);
                var bk = Kid(c, "Basket", b.transform, basket, b.transform.position + Vector3.down * 11f, new Vector3(2.4f, 1.6f, 2.4f), false);
                Kid(c, "Burner", b.transform, flame, b.transform.position + Vector3.down * 9.6f, Vector3.one * 0.6f, false);
                Move(b, VenueMotion.Kind.Bob, Vector3.up, 3f, 0.25f + k * 0.03f, k);
                Move(b, VenueMotion.Kind.Drift, Dir(1, 0, 0.1), 1.5f, 1f, k * 20f, 160f);
            }
            SkyBeams(c, new Color(1.4f, 1.2f, 0.8f, 0.10f), 6, 261, -40f, 40f, -120f, -60f);
            foreach (Transform t in c.Venue) if (t.name == "Searchlight") Move(t.gameObject, VenueMotion.Kind.Sway, Vector3.forward, 18f, 0.3f, t.GetSiblingIndex());
        }

        // ---------------- City Rooftop ----------------
        static void RooftopLife(Ctx c)
        {
            var heli = Pivot(c, "HeliPivot", W(0, 45, -40));
            var body = Pivot(c, "Helicopter", heli.transform.position + new Vector3(60f, 0, 0));
            body.transform.SetParent(heli.transform, true);
            Kid(c, "HeliBody", body.transform, Mat(c, new Color(0.15f, 0.16f, 0.2f), 0.6f), body.transform.position, new Vector3(2.4f, 2.2f, 6f), false);
            Kid(c, "HeliTail", body.transform, c.Dark, body.transform.position + new Vector3(0, 0.4f, 5f), new Vector3(0.5f, 0.5f, 5f), false);
            var rotor = Kid(c, "Rotor", body.transform, c.Dark, body.transform.position + Vector3.up * 1.5f, new Vector3(11f, 0.08f, 0.4f), false);
            Move(rotor, VenueMotion.Kind.Spin, Vector3.up, 900f, 1f);
            var beacon = Kid(c, "HeliLight", body.transform, Glow(c, new Color(3f, 0.3f, 0.3f)), body.transform.position + Vector3.down * 1.2f, Vector3.one * 0.4f, false);
            Move(beacon, VenueMotion.Kind.Pulse, Vector3.one, 0.6f, 8f);
            Move(heli, VenueMotion.Kind.Spin, Vector3.up, 9f, 1f);
            // a tower crane swinging slowly over the street
            var crane = Pivot(c, "Crane", W(-70, 0, -60));
            Greybox.Box("CraneMast", c.Venue, c.Steel, crane.transform.position + Vector3.up * 35f, new Vector3(2f, 70f, 2f), false);
            var jib = Pivot(c, "CraneJib", crane.transform.position + Vector3.up * 70f);
            Kid(c, "Jib", jib.transform, Mat(c, new Color(0.95f, 0.75f, 0.15f), 0.3f), jib.transform.position + new Vector3(18f, 0, 0), new Vector3(48f, 1.6f, 1.6f), false);
            Kid(c, "JibLight", jib.transform, Glow(c, new Color(3f, 0.3f, 0.2f)), jib.transform.position + new Vector3(41f, 1f, 0), Vector3.one * 0.7f, false);
            Move(jib, VenueMotion.Kind.Sway, Vector3.up, 40f, 0.08f);
            // blinking lights on the masts and the tallest roofs
            var red = Glow(c, new Color(3f, 0.2f, 0.2f));
            var rng = new System.Random(271);
            for (int k = 0; k < 14; k++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI) + Mathf.PI, r = 70f + (float)rng.NextDouble() * 200f;
                var l = Greybox.Box("MastLight", c.Venue, red, W(Mathf.Cos(a) * r, 50 + (float)rng.NextDouble() * 90, Mathf.Sin(a) * r * 0.6f - 40), Vector3.one * 1.2f, false);
                Move(l, VenueMotion.Kind.Pulse, Vector3.one, 0.7f, 3f + (float)rng.NextDouble() * 2f, k);
            }
        }
    
        // ---------------- 2026-11-22 evening (the user: "maybe considering adding a ref and high table and chair right behind the
        // court and behind the camera"; answer: off-camera, seen in the walk-on, replays and reviews): the referee's desk ----------------

        /// <summary>A raised platform behind the back wall, just behind the match camera (Classic sits 6.6 m back): the high table,
        /// two monitors, the referee on a tall chair and the marker beside, facing the court.</summary>
        static void RefereeDesk(Ctx c)
        {
            float z = L + 6.95f;
            var root = Pivot(c, "RefereeDesk", W(0, 0, z));
            var deck = Mat(c, new Color(0.12f, 0.13f, 0.16f), 0.3f);
            var wood = Mat(c, new Color(0.30f, 0.20f, 0.12f), 0.45f);
            var blazer = Mat(c, new Color(0.10f, 0.14f, 0.30f), 0.3f);
            var shirt = Mat(c, new Color(0.92f, 0.92f, 0.94f), 0.2f);
            var skin = Mat(c, new Color(0.78f, 0.58f, 0.44f), 0.3f);
            var hair = Mat(c, new Color(0.18f, 0.14f, 0.10f), 0.2f);
            var screen = Glow(c, new Color(0.5f, 0.9f, 1.4f));
            Kid(c, "RefPlatform", root.transform, deck, W(0, 0.45, z + 0.2), new Vector3(4.6f, 0.9f, 1.8f), true);
            Kid(c, "RefTable", root.transform, wood, W(0, 1.55, z - 0.45), new Vector3(4.2f, 1.3f, 0.5f), true);
            Kid(c, "RefTableTop", root.transform, wood, W(0, 2.22, z - 0.35), new Vector3(4.4f, 0.06f, 0.8f), false);
            foreach (float x in new[] { -0.7f, 0.9f })
            {
                // (low and small, laid back on the table: they mustn't hide the referee's arms)
                var mon = Kid(c, "RefMonitor", root.transform, c.Dark, W(x - 0.35f, 2.36, z - 0.2), new Vector3(0.42f, 0.26f, 0.04f), false);
                Kid(c, "RefMonitorGlow", root.transform, screen, W(x - 0.35f, 2.36, z - 0.23), new Vector3(0.36f, 0.2f, 0.01f), false);
            }
            // the referee (left of centre) and the marker: on tall chairs, blazers, looking at the court
            foreach (var (x, name) in new[] { (-0.7f, "Referee"), (0.9f, "Marker") })
            {
                Kid(c, name + "Chair", root.transform, c.Dark, W(x, 1.45, z + 0.35), new Vector3(0.55f, 1.1f, 0.55f), false);
                Kid(c, name + "Legs", root.transform, c.Dark, W(x, 1.95, z + 0.05), new Vector3(0.38f, 0.18f, 0.6f), false);
                Kid(c, name + "Body", root.transform, blazer, W(x, 2.45, z + 0.3), new Vector3(0.48f, 0.7f, 0.3f), false);
                Kid(c, name + "Shirt", root.transform, shirt, W(x, 2.62, z + 0.14), new Vector3(0.16f, 0.32f, 0.02f), false);
                var head = Greybox.Primitive(PrimitiveType.Sphere, name + "Head", c.Venue, skin, false);
                head.transform.position = W(x, 3.02, z + 0.3); head.transform.localScale = new Vector3(0.24f, 0.28f, 0.25f);
                head.transform.SetParent(root.transform, true);
                var top = Greybox.Primitive(PrimitiveType.Sphere, name + "Hair", c.Venue, hair, false);
                top.transform.position = W(x, 3.09, z + 0.34); top.transform.localScale = new Vector3(0.25f, 0.18f, 0.25f);
                top.transform.SetParent(root.transform, true);
                if (name != "Referee")
                {
                    foreach (int s in new[] { -1, 1 }) Kid(c, name + "Arm", root.transform, blazer, W(x + s * 0.3f, 2.38, z + 0.05), new Vector3(0.13f, 0.13f, 0.55f), false);
                    continue;
                }
                // 2026-11-23 (the user: "referee pointing"): the referee's arms hang from the shoulders, so they can point at a call
                var gesture = root.AddComponent<RefereeGestures>();
                for (int s = 0; s < 2; s++)
                {
                    int side = s == 0 ? -1 : 1;
                    var shoulder = Pivot(c, "RefereeShoulder", W(x + side * 0.3f, 2.68, z + 0.3));
                    shoulder.transform.SetParent(root.transform, true);
                    Vector3 rest = (W(x + side * 0.3f, 2.3, z - 0.3) - shoulder.transform.position).normalized;    // forearms on the table
                    shoulder.transform.rotation = Quaternion.LookRotation(rest, Vector3.up);
                    var arm = Greybox.Box("RefereeArm", c.Venue, blazer, Vector3.zero, new Vector3(0.13f, 0.13f, 0.62f), false);
                    arm.transform.SetParent(shoulder.transform, false);
                    arm.transform.localPosition = new Vector3(0, 0, 0.31f);
                    var cuff = Greybox.Box("RefereeCuff", c.Venue, shirt, Vector3.zero, new Vector3(0.15f, 0.15f, 0.08f), false);
                    cuff.transform.SetParent(shoulder.transform, false); cuff.transform.localPosition = new Vector3(0, 0, 0.6f);
                    var hand = Greybox.Primitive(PrimitiveType.Sphere, "RefereeHand", c.Venue, skin, false);
                    hand.transform.SetParent(shoulder.transform, false);
                    hand.transform.localPosition = new Vector3(0, 0, 0.7f); hand.transform.localScale = new Vector3(0.14f, 0.16f, 0.16f);
                    if (s == 0) gesture.Left = shoulder.transform; else gesture.Right = shoulder.transform;
                }
                gesture.Court = (W(0, 0, 0) - W(0, 0, 1)).normalized;              // (the way the court is from the desk)
                gesture.SideOf = simX => W(simX, 0, 0).x - W(0, 0, 0).x;
            }
        }
    }
}
