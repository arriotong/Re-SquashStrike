// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Tour.cs
// Courts per tournament, part 2 (2026-10-27): the World Tour's stages,
// one builder each, from the user's photos (RE_SQUASHSTRIKE_COURT_PHOTO_
// NOTES.md) and "as epic as possible" (the user): rigs, beams, screens,
// packed stands, colour. Shared pieces first, then the venues.
//
// Every venue keeps the match cameras inside it (the TV camera sits
// 12.5 m behind the back glass, 6 m up): the back stands start 7.8 m
// behind the glass as the existing courts' do; low rooms stay above 7 m.
// ===========================================================

using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static partial class VenueView
    {
        static void Tour(Ctx c, TourVenue v)
        {
            c.Blue = Mat(c, v.Frame, 0.6f);                                     // the glass court's coping in the venue's colour
            switch (v.Key)
            {
                case "pyramids": Pyramids(c, v); break;
                case "grandcentral": GrandCentral(c, v); break;
                case "unionstation": UnionStation(c, v); break;
                case "mallofarabia": MallOfArabia(c, v); break;
                case "library": Library(c, v); break;
                case "cirque": Cirque(c, v); break;
                case "khalifa": Khalifa(c, v); break;
                case "harbour": Harbour(c, v); break;
                case "usopen": UsOpen(c, v); break;
                case "marina": Marina(c, v); break;
                case "squashzone": SquashZone(c, v); break;
                case "ocbc": Ocbc(c, v); break;
                case "texas": Texas(c, v); break;
                case "brisbane": Brisbane(c, v); break;
                case "rep": Rep(c, v); break;
                case "allypally": AllyPally(c, v); break;
                case "zurich": Zurich(c, v); break;
                case "pool": Pool(c, v); break;
                default: ShowCourt(c); break;
            }
        }

        // =====================================================================================
        // shared pieces
        // =====================================================================================

        static Material Mat(Ctx c, Color col, float smooth = 0.2f) { var m = c.Palette.LitColor(col); m.SetFloat("_Smoothness", smooth); return m; }
        static Material Glow(Ctx c, Color col) { return c.Palette.UnlitColor(col); }
        static Material Tex(Ctx c, Color tint, Texture tex, float smooth = 0.2f) { var m = Mat(c, tint, smooth); m.mainTexture = tex; return m; }

        /// <summary>The live screens' material: one feed camera per venue, however many screens show it.</summary>
        static Material FeedMat(Ctx c)
        {
            if (c.Feed != null) return c.Feed;
            c.Feed = c.Palette.UnlitColor(new Color(1.1f, 1.1f, 1.1f));
            c.Feed.mainTexture = c.High && Post ? LiveFeed(c.Venue) : (Texture)ProcTex.Screen();
            return c.Feed;
        }

        /// <summary>A big screen showing the match, in a black frame, facing yaw (0: toward the back glass and the cameras).</summary>
        static void Screen(Ctx c, Vector3 at, float w, float h, float yaw = 0f, Material frame = null)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var f = Greybox.Box("ScreenFrame", c.Venue, frame ?? c.Dark, at, new Vector3(w + 0.3f, h + 0.3f, 0.14f), false); f.transform.rotation = rot;
            var s = Greybox.TexturedBox("Screen", c.Venue, FeedMat(c), at + rot * new Vector3(0, 0, 0.09f), new Vector3(w, h, 0.03f), new Vector2(w, h), false);
            s.transform.rotation = rot;
        }

        /// <summary>A square box truss from a to b (four chords, zigzag lacing).</summary>
        static void TrussBar(MeshMerge m, Vector3 a, Vector3 b, float s)
        {
            Vector3 d = b - a, u = d.normalized;
            Vector3 n1 = Vector3.Cross(u, Mathf.Abs(u.y) > 0.9f ? Vector3.right : Vector3.up).normalized * s / 2, n2 = Vector3.Cross(u, n1).normalized * s / 2;
            foreach (var o in new[] { n1 + n2, n1 - n2, -n1 + n2, -n1 - n2 }) m.Beam(a + o, b + o, 0.05f, 0.05f);
            int k = Mathf.Max(1, (int)(d.magnitude / s));
            for (int i = 0; i < k; i++)
            {
                Vector3 p0 = a + d * (i / (float)k), p1 = a + d * ((i + 1) / (float)k);
                m.Beam(p0 + n1 + n2, p1 + n1 - n2, 0.03f, 0.03f);
                m.Beam(p0 - n1 + n2, p1 - n1 - n2, 0.03f, 0.03f);
            }
        }

        /// <summary>A lighting rig over the court: a truss rectangle (on towers to the floor, or hung), lamp heads along it,
        /// coloured beams angled down at the court; the court's spots under it.</summary>
        static void Rig(Ctx c, float hx, float z0, float z1, float y, Color lamp, Color beam, bool towers, int beams, int seed)
        {
            var m = new MeshMerge();
            Vector3 a = W(-hx, y, z0), b = W(hx, y, z0), cc = W(hx, y, z1), d = W(-hx, y, z1);
            TrussBar(m, a, b, 0.4f); TrussBar(m, b, cc, 0.4f); TrussBar(m, cc, d, 0.4f); TrussBar(m, d, a, 0.4f);
            TrussBar(m, W(-hx, y, (z0 + z1) / 2), W(hx, y, (z0 + z1) / 2), 0.4f);
            if (towers) foreach (var p in new[] { a, b, cc, d }) TrussBar(m, new Vector3(p.x, 0, p.z), p, 0.45f);
            m.Build("Rig", c.Venue, c.Steel);
            var heads = new MeshMerge(); var lenses = new MeshMerge();
            for (float z = z0; z <= z1 + 0.01f; z += (z1 - z0) / 4f)
                foreach (float x in new[] { -hx, -hx * 0.33f, hx * 0.33f, hx })
                {
                    heads.Box(W(x, y - 0.38, z), new Vector3(0.34f, 0.4f, 0.34f), Quaternion.identity);       // the housing
                    lenses.Box(W(x, y - 0.6, z), new Vector3(0.2f, 0.04f, 0.2f), Quaternion.identity);       // its lens, facing down
                }
            heads.Build("RigHeads", c.Venue, c.Dark);
            lenses.Build("RigLenses", c.Venue, Glow(c, lamp));
            if (beams > 0) RigBeams(c, beam, hx, z0, z1, y - 0.4f, beams, seed);
            if (c.High) for (int k = 0; k < 3; k++) foreach (int s in new[] { -1, 1 }) Spot(c, W(s * 1.6, y - 0.5, 1.5 + k * 3.3), 14f, 70f, y + 4f);
        }

        /// <summary>Moving heads' beams from a rig, angled down at the court and the crowd (see-through glowing cards).</summary>
        static void RigBeams(Ctx c, Color col, float hx, float z0, float z1, float y, int n, int seed)
        {
            var mat = c.Palette.Clear(col); mat.mainTexture = ProcTex.Beam();
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                Vector3 foot = W((rng.NextDouble() * 2 - 1) * hx, y, z0 + rng.NextDouble() * (z1 - z0));
                Vector3 target = W((rng.NextDouble() * 2 - 1) * 12, 0, -4 + rng.NextDouble() * (L + 14));
                Vector3 dir = (target - foot).normalized;
                float len = (target - foot).magnitude * 1.1f;
                for (int k = 0; k < 2; k++)
                {
                    var q = Greybox.Primitive(PrimitiveType.Quad, "RigBeam", c.Venue, mat, false);
                    q.transform.localPosition = foot + dir * (len / 2);
                    q.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(dir, k == 0 ? Vector3.right : Vector3.forward).normalized, dir);
                    q.transform.localScale = new Vector3(0.9f, len, 1f);
                }
            }
        }

        /// <summary>Searchlights into the sky from round the venue (any colour), crossing high.</summary>
        static void SkyBeams(Ctx c, Color col, int n, int seed, float x0, float x1, float zNear, float zFar)
        {
            var mat = c.Palette.Clear(col); mat.mainTexture = ProcTex.Beam();
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                Vector3 foot = W(side * (x0 + rng.NextDouble() * (x1 - x0)), 0.5, zNear + (zFar - zNear) * i / Mathf.Max(1, n - 1));
                Vector3 dir = (W(-side * (20 + rng.NextDouble() * 50), 160, -120 - rng.NextDouble() * 140) - foot).normalized;
                const float len = 200f, wide = 2.4f;
                for (int k = 0; k < 2; k++)
                {
                    var q = Greybox.Primitive(PrimitiveType.Quad, "Searchlight", c.Venue, mat, false);
                    q.transform.localPosition = foot + dir * (len / 2);
                    q.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(dir, k == 0 ? Vector3.right : Vector3.forward).normalized, dir);
                    q.transform.localScale = new Vector3(wide, len, 1f);
                }
            }
        }

        /// <summary>A scaffold tower with a crossbar of lamps on top (the Giza stage's corners).</summary>
        static void LampTower(Ctx c, float x, float z, float h, Color lamp)
        {
            var m = new MeshMerge();
            TrussBar(m, W(x, 0, z), W(x, h, z), 0.6f);
            TrussBar(m, W(x - 2.2, h, z), W(x + 2.2, h, z), 0.4f);
            m.Build("LampTower", c.Venue, c.Steel);
            var lamps = new MeshMerge();
            for (int k = 0; k < 7; k++) lamps.Box(W(x - 1.8 + k * 0.6, h - 0.35, z + 0.2), new Vector3(0.32f, 0.32f, 0.2f), Quaternion.identity);
            lamps.Build("TowerLamps", c.Venue, Glow(c, lamp));
            if (c.High) Point(c, "TowerGlow", W(x, h - 1, z + 1), new Color(1f, 0.95f, 0.85f), 14f, 1.4f);
        }

        /// <summary>A row of coloured uplights along a line (a stand's front, a stage edge), and their pools on the floor.</summary>
        static void EdgeGlow(Ctx c, Color col, float x, float z0, float z1)
        {
            var dots = new MeshMerge();
            for (float z = z0; z <= z1; z += 1.6f) dots.Box(W(x, 0.12, z), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity);
            dots.Build("EdgeLights", c.Venue, Glow(c, col * 2.4f));
            var pool = c.Palette.Clear(new Color(col.r, col.g, col.b, 0.22f)); pool.mainTexture = ProcTex.SoftSpot();
            for (float z = z0; z <= z1; z += 3.2f)
            {
                var q = Greybox.Primitive(PrimitiveType.Quad, "EdgePool", c.Venue, pool, false);
                q.transform.localPosition = W(x, 0.015, z);
                q.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                q.transform.localScale = new Vector3(2.2f, 2.6f, 1f);
            }
        }

        /// <summary>LED boards round the foot of the court (both sides and behind the back glass), the venue's words scrolling.</summary>
        static void CourtBoards(Ctx c, TourVenue v, string text, Color ink, Color back)
        {
            var led = LedMat(c.Palette, "t_" + v.Key + "_boards", ClubVenues.Sign(text + "  -  "), ink, back, 1.15f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("CourtBoard", c.Venue, led, W(s * (HW + 0.75), 0.42, L / 2), new Vector3(0.05f, 0.62f, L + 0.6f), new Vector2(L + 0.6f, 0.62f), false);
        }

        /// <summary>Seats in the venue's colour for the stands built next.</summary>
        static void Seats(Ctx c, Color col) { c.Seat = Mat(c, col, 0.3f); }

        /// <summary>The usual stands: down both sides from the front wall to past the glass, and behind the cameras.</summary>
        static void SideAndBack(Ctx c, int side, int back, float sideLen = 26f, float x0 = 1.6f, int seed = 0, float backWidth = 0f)
        {
            foreach (int s in new[] { -1, 1 })
                if (side > 0)
                    Stand(c, s < 0 ? "StandLeft" : "StandRight", side, i => W(s * (HW + x0 + i * ROW_DEPTH), 0, L / 2 + 2f),
                        new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, sideLen, s + 31 + seed, SteepTread);
            if (back > 0)
                Stand(c, "StandBack", back, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, backWidth > 0 ? backWidth : 2 * HW + 6f, 33 + seed, SteepTread);
        }

        /// <summary>Towers across the water or round the edge of town: lit windows, a few with neon bands and aircraft lights.</summary>
        static void Skyline(Ctx c, int count, int seed, float rMin, float rMax, float aFrom, float aTo, float hMin, float hMax, Color[] neon, float baseY = -2f, bool reflect = false)
        {
            var rng = new System.Random(seed);
            var mats = new Material[4];
            for (int i = 0; i < mats.Length; i++) { mats[i] = c.Palette.UnlitColor(new Color(0.9f, 0.9f, 0.95f)); mats[i].mainTexture = ProcTex.CityWindows(i); }
            var red = Glow(c, new Color(3f, 0.2f, 0.15f));
            var neonMats = neon != null ? System.Array.ConvertAll(neon, n => Glow(c, n)) : null;
            Material[] reflMats = null;
            if (reflect && neon != null)
                reflMats = System.Array.ConvertAll(neon, n => { var m = c.Palette.Clear(new Color(n.r, n.g, n.b, 0.35f)); m.mainTexture = ProcTex.SoftSpot(); return m; });
            Vector3 centre = W(0, 0, L / 2);
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(aFrom, aTo, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                Vector3 p = centre + new Vector3(Mathf.Sin(a) * r, 0, -Mathf.Cos(a) * r);
                float w = 12f + (float)rng.NextDouble() * 22f, d = 12f + (float)rng.NextDouble() * 22f;
                float h = Mathf.Lerp(hMin, hMax, (float)(rng.NextDouble() * rng.NextDouble()));
                Greybox.TexturedBox("Tower", c.Venue, mats[i % mats.Length], new Vector3(p.x, baseY + h / 2, p.z), new Vector3(w, h, d), new Vector2(12.8f, 25.6f), false);
                if (h > hMax * 0.45f && rng.NextDouble() < 0.6) Greybox.Box("AirLight", c.Venue, red, new Vector3(p.x, baseY + h + 1.5f, p.z), new Vector3(0.9f, 0.9f, 0.9f), false);
                if (neonMats != null && rng.NextDouble() < 0.55)
                {
                    int k = rng.Next(neonMats.Length);
                    for (int band = 0; band < 3; band++)
                        Greybox.Box("Neon", c.Venue, neonMats[k], new Vector3(p.x, baseY + h * (0.35f + band * 0.25f), p.z), new Vector3(w + 0.4f, 1.2f, d + 0.4f), false);
                    if (reflMats != null)
                    {
                        var q = Greybox.Primitive(PrimitiveType.Quad, "NeonReflection", c.Venue, reflMats[k], false);
                        q.transform.localPosition = new Vector3(p.x, -1.45f, p.z + 60f);
                        q.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                        q.transform.localScale = new Vector3(w * 0.8f, 140f, 1f);
                    }
                }
            }
        }

        /// <summary>A ring of far lights round the horizon (a city at night).</summary>
        static void CityGlow(Ctx c, float r, int n, int seed, float aFrom, float aTo)
        {
            var rng = new System.Random(seed);
            var m = new MeshMerge();
            Vector3 centre = W(0, 0, L / 2);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(aFrom, aTo, (float)rng.NextDouble()) * Mathf.Deg2Rad, rr = r * (0.85f + (float)rng.NextDouble() * 0.3f);
                m.Box(centre + new Vector3(Mathf.Sin(a) * rr, 1f + (float)rng.NextDouble() * 6f, -Mathf.Cos(a) * rr), Vector3.one * (1.2f + (float)rng.NextDouble() * 2f), Quaternion.identity);
            }
            m.Build("CityLights", c.Venue, Glow(c, new Color(2.2f, 1.6f, 0.9f)));
        }

        static void Palm(Ctx c, Vector3 foot, float h, Material trunk, Material leaf)
        {
            var t = Greybox.Primitive(PrimitiveType.Cylinder, "PalmTrunk", c.Venue, trunk, false);
            t.transform.localPosition = foot + Vector3.up * h / 2; t.transform.localScale = new Vector3(0.35f, h / 2, 0.35f);
            for (int k = 0; k < 7; k++)
            {
                var f = Greybox.Box("PalmLeaf", c.Venue, leaf, foot + Vector3.up * h + Quaternion.Euler(0, k * 51f, 0) * Vector3.forward * 1.4f, new Vector3(0.5f, 0.08f, 3.0f), false);
                f.transform.localRotation = Quaternion.Euler(-25f, k * 51f, 0);
            }
        }

        /// <summary>A chandelier: a brass hub and rings of warm bulbs (and its light on High).</summary>
        static void Chandelier(Ctx c, Vector3 hub, float r, Material bulbs, Material brass, bool light)
        {
            Greybox.Box("ChandelierCable", c.Venue, c.Steel, hub + Vector3.up * 5f, new Vector3(0.05f, 10f, 0.05f), false);
            var hubGo = Greybox.Primitive(PrimitiveType.Sphere, "ChandelierHub", c.Venue, brass, false);
            hubGo.transform.localPosition = hub; hubGo.transform.localScale = Vector3.one * r * 0.6f;
            var m = new MeshMerge();
            for (int ring = 0; ring < 2; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f + ring * 0.26f, rr = r * (1f - ring * 0.4f);
                    m.Box(hub + new Vector3(Mathf.Cos(a) * rr, -0.35f - ring * 0.5f, Mathf.Sin(a) * rr), Vector3.one * 0.24f, Quaternion.identity);
                }
            m.Build("ChandelierBulbs", c.Venue, bulbs);
            if (light && c.High) Point(c, "Chandelier", hub + Vector3.down, new Color(1f, 0.82f, 0.6f), 20f, 2.2f);
        }

        /// <summary>A stretch of flags: three-stripe flags in a row.</summary>
        static void FlagRow(Ctx c, Color[] stripes, Vector3 start, Vector3 step, int n, Vector3 size)
        {
            var mats = System.Array.ConvertAll(stripes, s => Mat(c, s, 0.1f));
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 3; k++)
                    Greybox.Box("Flag", c.Venue, mats[k], start + step * i + Vector3.down * (k * size.y), size, false);
        }

        /// <summary>A grand hall's shell: floor, walls, ends (the stone and the hall's size given).</summary>
        static void HallShell(Ctx c, Material floor, Material walls, float hx, float front, float back, float wallH, Vector2 tile)
        {
            Greybox.TexturedBox("HallFloor", c.Venue, floor, W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front), new Vector2(2.4f, 2.4f));
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("HallWall", c.Venue, walls, W(s * hx, wallH / 2, (front + back) / 2), new Vector3(0.6f, wallH, back - front), tile);
            Greybox.TexturedBox("HallEnd", c.Venue, walls, W(0, wallH / 2, front), new Vector3(2 * hx, wallH, 0.6f), tile);
            Greybox.TexturedBox("HallEnd", c.Venue, walls, W(0, wallH / 2, back), new Vector3(2 * hx, wallH, 0.6f), tile);
        }

        /// <summary>A barrel vault over a hall: an arch of quads (from the walls' tops), and ribs across it.</summary>
        static void Vault(Ctx c, float hx, float front, float back, float wallH, float rise, Material skin, Material ribs, float ribStep)
        {
            var sk = new MeshMerge();
            const int SEG = 20;
            for (int i = 0; i < SEG; i++)
            {
                float a0 = Mathf.PI * i / SEG, a1 = Mathf.PI * (i + 1) / SEG;
                Vector3 p0 = new Vector3(-Mathf.Cos(a0) * hx, wallH + Mathf.Sin(a0) * rise, 0), p1 = new Vector3(-Mathf.Cos(a1) * hx, wallH + Mathf.Sin(a1) * rise, 0);
                sk.Quad(p0 + new Vector3(0, 0, front), p1 + new Vector3(0, 0, front), p1 + new Vector3(0, 0, back), p0 + new Vector3(0, 0, back));
            }
            sk.Build("Vault", c.Venue, skin);
            var rb = new MeshMerge();
            for (float z = front; z <= back; z += ribStep)
                for (int i = 0; i < SEG; i++)
                {
                    float a0 = Mathf.PI * i / SEG, a1 = Mathf.PI * (i + 1) / SEG;
                    rb.Beam(new Vector3(-Mathf.Cos(a0) * hx * 0.99f, wallH + Mathf.Sin(a0) * rise * 0.99f, z), new Vector3(-Mathf.Cos(a1) * hx * 0.99f, wallH + Mathf.Sin(a1) * rise * 0.99f, z), 0.3f, 0.4f);
                }
            for (int i = 1; i < SEG; i += 2)
            {
                float a = Mathf.PI * i / SEG;
                rb.Beam(new Vector3(-Mathf.Cos(a) * hx * 0.98f, wallH + Mathf.Sin(a) * rise * 0.98f, front), new Vector3(-Mathf.Cos(a) * hx * 0.98f, wallH + Mathf.Sin(a) * rise * 0.98f, back), 0.15f, 0.15f);
            }
            rb.Build("VaultRibs", c.Venue, ribs);
        }

        /// <summary>A peaked white tent canopy over the court on four posts, a lit fascia across its front.</summary>
        static void Canopy(Ctx c, TourVenue v, float eaves, float ridge, string fascia)
        {
            var white = Mat(c, Greybox.Hex(0xf2f2f4), 0.3f);
            float hx = HW + 2.6f, z0 = -2f, z1 = L + 3f;
            foreach (int s in new[] { -1, 1 })
            {
                var roof = Greybox.Box("CanopyRoof", c.Venue, white, W(s * hx / 2, (eaves + ridge) / 2, (z0 + z1) / 2), new Vector3(hx * 1.06f, 0.12f, z1 - z0), false);
                roof.transform.localRotation = Quaternion.Euler(0, 0, s * Mathf.Atan2(ridge - eaves, hx) * Mathf.Rad2Deg);
            }
            var m = new MeshMerge();
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { z0, z1 }) TrussBar(m, W(s * hx, 0, z), W(s * hx, eaves, z), 0.35f);
            foreach (float z in new[] { z0, z1 }) TrussBar(m, W(-hx, eaves, z), W(hx, eaves, z), 0.35f);
            m.Build("CanopyFrame", c.Venue, c.Steel);
            var led = LedMat(c.Palette, "t_" + v.Key + "_fascia", ClubVenues.Sign(fascia + "  -  "), Greybox.Hex(0x1d3f9a), new Color(1.6f, 1.6f, 1.6f), 1.2f);
            Greybox.TexturedBox("Fascia", c.Venue, led, W(0, eaves - 0.5, z1 + 0.05), new Vector3(2 * hx, 0.9f, 0.08f), new Vector2(2 * hx, 0.9f), false);
            var lamps = new MeshMerge();
            for (int k = 0; k < 6; k++) lamps.Box(W(-hx + 0.6 + k * (2 * hx - 1.2) / 5f, eaves - 0.2, z1 - 0.4), new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity);
            lamps.Build("CanopyLamps", c.Venue, Glow(c, new Color(2.6f, 2.5f, 2.3f)));
        }

        /// <summary>A building's facade (a box): plaster in a colour, rows of lit arched windows on the face toward the court.</summary>
        static void Facade(Ctx c, Vector3 centre, Vector3 size, Material wall, Material window, int floors, float facing)
        {
            Greybox.Box("Building", c.Venue, wall, centre, size, false);
            var m = new MeshMerge();
            var face = Quaternion.Euler(0, facing, 0);
            float along = Mathf.Abs(facing) % 180f < 1f ? size.x : size.z, depth = Mathf.Abs(facing) % 180f < 1f ? size.z : size.x;
            int cols = Mathf.Max(2, (int)(along / 3.2f));
            for (int f = 0; f < floors; f++)
                for (int k = 0; k < cols; k++)
                {
                    Vector3 local = new Vector3(-along / 2 + (k + 0.5f) * along / cols, -size.y / 2 + (f + 0.6f) * size.y / (floors + 0.3f), depth / 2 + 0.06f);
                    m.Box(centre + face * local, new Vector3(1.1f, 1.7f, 0.1f), face);
                }
            m.Build("Windows", c.Venue, window);
        }

        // =====================================================================================
        // the venues
        // =====================================================================================

        /// <summary>#3 Egyptian Open: the glass court on a black stage before the Giza pyramids, searchlights fanning over it.</summary>
        static void Pyramids(Ctx c, TourVenue v)
        {
            Sky(c, false, 800f);
            var sand = Tex(c, new Color(0.34f, 0.29f, 0.24f), ProcTex.Sand(), 0.05f);
            Greybox.TexturedBox("Sand", c.Venue, sand, W(0, -0.7, 0), new Vector3(1600f, 0.2f, 1600f), new Vector2(10f, 10f), false);
            var stage = Mat(c, Greybox.Hex(0x0c0d10), 0.5f);
            Greybox.Box("Stage", c.Venue, stage, W(0, -0.3, L / 2 + 4), new Vector3(36f, 0.5f, 44f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a0b10), Greybox.Hex(0x8a3ae0));
            // the two together on the left (Khafre in front), Menkaure alone on the right; Cairo's lights along the horizon
            Pyramid(c, W(-70, -0.7, -310), 170f, 110f);
            Pyramid(c, W(-205, -0.7, -420), 160f, 104f);
            Pyramid(c, W(175, -0.7, -400), 120f, 76f);
            CityGlow(c, 900f, 320, 7, -110f, 110f);
            Seats(c, Greybox.Hex(0x15171c));
            SideAndBack(c, 9, 10, 26f);
            // the stepped black blocks round the stands, their boards
            var block = Mat(c, Greybox.Hex(0x101114), 0.3f);
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("StandWall", c.Venue, block, W(s * (HW + 1.6 + 9 * ROW_DEPTH + 0.4), 2.6, L / 2 + 2), new Vector3(0.4f, 5.2f, 26f), false);
            var zone = LedMat(c.Palette, "t_pyr_zone", "EGYPTIAN OPEN  -  GIZA  -  PYRAMIDS  -  ", Color.white, Greybox.Hex(0x0c1a4a), 1.1f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("StandBoard", c.Venue, zone, W(s * (HW + 1.6 + 9 * ROW_DEPTH + 0.15), 1.2, L / 2 + 2), new Vector3(0.05f, 1.0f, 20f), new Vector2(20f, 1.0f), false);
            // the screens either side of the front wall, the lamp towers at the stage's corners, its purple edge
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.6), 5.0, -2.0), 6.4f, 3.6f);
            foreach (int s in new[] { -1, 1 }) LampTower(c, s * 12.5f, -6f, 10f, new Color(3f, 2.9f, 2.6f));
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.6f, 0.25f, 1f), s * (HW + 1.3f), -1f, L + 4f);
            CourtBoards(c, v, "EGYPTIAN OPEN - GIZA", Color.white, Greybox.Hex(0x0c1a4a));
            Rig(c, HW + 0.6f, -0.6f, L + 0.6f, CH + 2.4f, new Color(3f, 3f, 2.8f), new Color(0.6f, 0.75f, 1.6f, 0.18f), false, 0, 3);
            SkyBeams(c, new Color(0.4f, 0.65f, 1.9f, 0.34f), 14, 29, 10f, 22f, -16f, L + 18f);
            Ambient(new Color(0.16f, 0.18f, 0.26f), new Color(0.14f, 0.13f, 0.16f), new Color(0.08f, 0.06f, 0.05f));
            RenderSettings.fog = false;
        }

        /// <summary>#9 Tournament of Champions: the glass court in Vanderbilt Hall - honey stone, a coffered gold ceiling,
        /// chandeliers, the great arched windows pouring daylight, steep black stands.</summary>
        static void GrandCentral(Ctx c, TourVenue v)
        {
            const float hx = 22f, front = -34f, back = 38f, wallH = 24f;
            var marble = Tex(c, new Color(1f, 0.94f, 0.84f), ProcTex.Marble(), 0.65f);
            var stone = Tex(c, new Color(1f, 0.88f, 0.7f), ProcTex.Stone(), 0.15f);
            HallShell(c, marble, stone, hx, front, back, wallH, new Vector2(3f, 3f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x16181e), Greybox.Hex(0xb8902a));
            // the coffered ceiling: a gold grid, warm panels
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0xd8c49a), 0.2f), W(0, wallH + 0.2, (front + back) / 2), new Vector3(2 * hx, 0.3f, back - front), false);
            var gold = Mat(c, Greybox.Hex(0xc8a050), 0.7f); gold.SetFloat("_Metallic", 0.6f);
            var grid = new MeshMerge();
            for (float x = -hx; x <= hx; x += 4f) grid.Beam(W(x, wallH - 0.3, front), W(x, wallH - 0.3, back), 0.35f, 0.6f);
            for (float z = front; z <= back; z += 4f) grid.Beam(W(-hx, wallH - 0.3, z), W(hx, wallH - 0.3, z), 0.35f, 0.6f);
            grid.Build("Coffers", c.Venue, gold);
            // the windows down the left wall and over the far end: daylight, and its shafts across the hall
            var win = c.Palette.Clear(new Color(1.6f, 1.55f, 1.45f, 1f)); win.mainTexture = ProcTex.ArchWindow();
            foreach (float z in new[] { -22f, -4f, 14f, 30f })
                Greybox.TexturedBox("ArchWindow", c.Venue, win, W(-hx + 0.35, 12.5, z), new Vector3(0.05f, 17f, 11f), new Vector2(11f, 17f), false);
            Greybox.TexturedBox("ArchWindow", c.Venue, win, W(0, 13, front + 0.35), new Vector3(16f, 18f, 0.05f), new Vector2(16f, 18f), false);
            var shaft = c.Palette.Clear(new Color(1.5f, 1.35f, 1.0f, 0.10f)); shaft.mainTexture = ProcTex.Beam();
            foreach (float z in new[] { -22f, -4f, 14f })
            {
                var q = Greybox.Primitive(PrimitiveType.Quad, "SunShaft", c.Venue, shaft, false);
                Vector3 from = W(-hx + 1, 16, z), to = W(4, 0, z + 6);
                Vector3 d = (to - from);
                q.transform.localPosition = from + d / 2;
                q.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(d.normalized, Vector3.forward).normalized, d.normalized);
                q.transform.localScale = new Vector3(7f, d.magnitude * 1.1f, 1f);
            }
            // the chandeliers in two rows, the flags, the red carpet at the stands' feet
            var bulbs = Glow(c, new Color(2.8f, 2.1f, 1.2f));
            var brass = Mat(c, Greybox.Hex(0xb08a3c), 0.75f); brass.SetFloat("_Metallic", 0.8f);
            int lit = 0;
            foreach (float z in new[] { -20f, -6f, 8f, 22f })
                foreach (int s in new[] { -1, 1 }) Chandelier(c, W(s * 10f, 15f, z), 1.6f, bulbs, brass, lit++ < 4);
            FlagRow(c, new[] { Greybox.Hex(0xb22234), Greybox.Hex(0xf2f2f2), Greybox.Hex(0x3c3b6e) }, W(-hx + 0.5, 20, -12), new Vector3(0, 0, 8f), 4, new Vector3(0.05f, 1.6f, 4.2f));
            var carpet = Mat(c, Greybox.Hex(0x9a1a22), 0.1f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Carpet", c.Venue, carpet, W(s * (HW + 1.0), -0.01, L / 2 + 2), new Vector3(1.2f, 0.02f, 26f), false);
            Seats(c, Greybox.Hex(0x16181c));
            SideAndBack(c, 12, 11, 28f);
            // the rig over the court with its speakers, the big screen on the right-hand wall
            Rig(c, HW + 0.8f, -1.5f, L + 1.5f, 9.2f, new Color(3f, 3f, 2.8f), new Color(1.6f, 1.5f, 1.2f, 0.1f), false, 0, 5);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Speaker", c.Venue, c.Dark, W(s * (HW + 0.8), 7.6, -1.0), new Vector3(0.8f, 1.8f, 0.8f), false);
            Screen(c, W(hx - 0.6, 9.5, 4), 9f, 5f, 90f);
            CourtBoards(c, v, "TOURNAMENT OF CHAMPIONS - GRAND CENTRAL", Color.white, Greybox.Hex(0x14306a));
            Ambient(new Color(0.78f, 0.72f, 0.62f), new Color(0.66f, 0.58f, 0.48f), new Color(0.36f, 0.30f, 0.24f));
            Fog(Greybox.Hex(0xd8c8a6), 46f, 200f);
        }

        /// <summary>#2 World Championship: the Great Hall - columns, the barrel skylight, amber uplight, a square truss on four
        /// towers with its screens, teal stands on three sides.</summary>
        static void UnionStation(Ctx c, TourVenue v)
        {
            const float hx = 21f, front = -32f, back = 36f, wallH = 16f, rise = 9f;
            var marble = Tex(c, new Color(0.98f, 0.92f, 0.82f), ProcTex.Marble(), 0.65f);
            var stone = Tex(c, new Color(1f, 0.86f, 0.64f), ProcTex.Stone(), 0.15f);
            HallShell(c, marble, stone, hx, front, back, wallH + rise, new Vector2(3f, 3f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0c1020), Greybox.Hex(0xd8302a));
            // the skylight: night blue glass in steel ribs
            Vault(c, hx, front, back, wallH, rise, Glow(c, new Color(0.06f, 0.10f, 0.22f)), Mat(c, Greybox.Hex(0x2a2a30), 0.4f), 3f);
            // the columns down both walls, gold capitals
            var col = Mat(c, Greybox.Hex(0xe2d2b0), 0.3f);
            var gold = Mat(c, Greybox.Hex(0xc8a050), 0.7f); gold.SetFloat("_Metallic", 0.6f);
            foreach (int s in new[] { -1, 1 })
                for (float z = front + 4; z < back - 2; z += 6f)
                {
                    var p = Greybox.Primitive(PrimitiveType.Cylinder, "Column", c.Venue, col, false);
                    p.transform.localPosition = W(s * (hx - 1.4), wallH / 2, z); p.transform.localScale = new Vector3(1.3f, wallH / 2, 1.3f);
                    Greybox.Box("Capital", c.Venue, gold, W(s * (hx - 1.4), wallH - 0.4, z), new Vector3(1.8f, 0.8f, 1.8f), false);
                    Greybox.Box("Base", c.Venue, col, W(s * (hx - 1.4), 0.4, z), new Vector3(1.8f, 0.8f, 1.8f), false);
                }
            // the lunettes over the far end, lit amber; the amber uplight up the walls
            var lun = c.Palette.Clear(new Color(1.9f, 1.25f, 0.6f, 1f)); lun.mainTexture = ProcTex.ArchWindow();
            for (int k = -1; k <= 1; k++)
                Greybox.TexturedBox("Lunette", c.Venue, lun, W(k * 13f, wallH + 2.5, front + 0.35), new Vector3(10f, 8f, 0.05f), new Vector2(10f, 8f), false);
            var up = c.Palette.Clear(new Color(1.6f, 0.9f, 0.35f, 0.35f)); up.mainTexture = ProcTex.SoftSpot();
            foreach (int s in new[] { -1, 1 })
                for (float z = front + 4; z < back - 2; z += 6f)
                {
                    var q = Greybox.Primitive(PrimitiveType.Quad, "Uplight", c.Venue, up, false);
                    q.transform.localPosition = W(s * (hx - 0.4), 6, z + 3);
                    q.transform.localRotation = Quaternion.Euler(0, s < 0 ? -90f : 90f, 0);
                    q.transform.localScale = new Vector3(5f, 12f, 1f);
                }
            Seats(c, Greybox.Hex(0x1f8a7a));
            SideAndBack(c, 11, 11, 28f);
            Stand(c, "StandFront", 8, i => W(0, 0, -6.5 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 10f, 37, SteepTread);
            // the square truss on its four towers, speakers and the two screens hung from it
            Rig(c, HW + 3.4f, -3f, L + 2f, 11f, new Color(3f, 3f, 2.8f), new Color(1.2f, 0.9f, 1.6f, 0.12f), true, 10, 7);
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("SpeakerStack", c.Venue, c.Dark, W(s * (HW + 3.4), 8.4, L / 2), new Vector3(0.9f, 3.2f, 0.9f), false);
                Screen(c, W(s * (HW + 5.8), 7.6, -2.6), 6.6f, 3.7f);
            }
            CourtBoards(c, v, "PSA WORLD CHAMPIONSHIP - CHICAGO", Color.white, Greybox.Hex(0x0e1430));
            if (c.High) foreach (int s in new[] { -1, 1 }) Point(c, "HallAmber", W(s * (hx - 3), 6, 0), new Color(1f, 0.7f, 0.35f), 26f, 2.4f);
            Ambient(new Color(0.42f, 0.36f, 0.30f), new Color(0.40f, 0.32f, 0.22f), new Color(0.20f, 0.16f, 0.12f));
            Fog(Greybox.Hex(0x2a2014), 40f, 180f);
        }

        /// <summary>The Tour Finals in Cairo: the plaza at the Mall of Arabia at dusk - a red stage, green stands, floodlights,
        /// gold lamps, the pyramid-topped tower behind.</summary>
        static void MallOfArabia(Ctx c, TourVenue v)
        {
            Sky(c, true, 700f);
            var pavers = Tex(c, new Color(0.62f, 0.58f, 0.52f), ProcTex.Concrete(), 0.2f);
            Greybox.TexturedBox("Plaza", c.Venue, pavers, W(0, -0.75, L / 2), new Vector3(400f, 0.2f, 400f), new Vector2(40f, 40f), false);
            var red = Mat(c, Greybox.Hex(0xb81e24), 0.25f);
            Greybox.Box("Stage", c.Venue, red, W(0, -0.38, L / 2 + 2), new Vector3(18f, 0.7f, 26f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x7a1018), Greybox.Hex(0xe8c040));
            // the mall: a glass block on the left, cream stone round the plaza, the tower with its lit pyramid top
            var glassBlock = c.Palette.UnlitColor(new Color(0.75f, 0.85f, 1f)); glassBlock.mainTexture = ProcTex.CityWindows(2);
            Greybox.TexturedBox("GlassBlock", c.Venue, glassBlock, W(-42, 12, 0), new Vector3(26f, 26f, 46f), new Vector2(12.8f, 25.6f), false);
            var cream = Tex(c, new Color(0.92f, 0.86f, 0.74f), ProcTex.Stone(), 0.15f);
            var warmWin = Glow(c, new Color(2.0f, 1.5f, 0.8f));
            Facade(c, W(40, 9, 4), new Vector3(24f, 18f, 46f), cream, warmWin, 4, 90f);
            Facade(c, W(0, 10, -60), new Vector3(70f, 20f, 16f), cream, warmWin, 4, 0f);
            Greybox.Box("Tower", c.Venue, cream, W(12, 18, -54), new Vector3(10f, 36f, 10f), false);
            Pyramid(c, W(12, 36, -54), 11f, 8f);
            CityGlow(c, 500f, 160, 11, -120f, 120f);
            Seats(c, Greybox.Hex(0x1f7a3a));
            SideAndBack(c, 9, 12, 26f);
            Stand(c, "StandFront", 7, i => W(0, -0.6, -9.5 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 14f, 41, SteepTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.2), 5.4, -2.2), 6.2f, 3.5f);
            FloodTowers(c, 17f, -12f, L + 16f, 18f);
            SkyBeams(c, new Color(0.7f, 0.8f, 1.6f, 0.26f), 8, 41, 14f, 24f, -14f, L + 14f);
            // gold canopy lamps on posts round the walkways, string lights over the crowd
            var lamp = Glow(c, new Color(2.8f, 1.9f, 0.7f));
            foreach (int s in new[] { -1, 1 })
                for (float z = -8f; z < L + 18f; z += 7f)
                {
                    Greybox.Box("LampPost", c.Venue, c.Steel, W(s * 16f, 1.6, z), new Vector3(0.12f, 3.2f, 0.12f), false);
                    Greybox.Box("CanopyLamp", c.Venue, lamp, W(s * 16f, 3.4, z), new Vector3(1.6f, 0.4f, 1.6f), false);
                }
            CourtBoards(c, v, "PSA WORLD TOUR FINALS - CAIRO", Color.white, Greybox.Hex(0x0a2a6a));
            Ambient(new Color(0.30f, 0.30f, 0.40f), new Color(0.34f, 0.28f, 0.26f), new Color(0.12f, 0.10f, 0.10f));
            RenderSettings.fog = false;
        }

        /// <summary>#50 Alexandria Open: the plaza at the Bibliotheca Alexandrina at night - the great dark globe ringed in blue
        /// light, palms, a domed canopy over the court, an orange stage with white sofas, green stands, flags.</summary>
        static void Library(Ctx c, TourVenue v)
        {
            Sky(c, false, 600f);
            var pavers = Tex(c, new Color(0.5f, 0.48f, 0.46f), ProcTex.Concrete(), 0.2f);
            Greybox.TexturedBox("Plaza", c.Venue, pavers, W(0, -0.75, L / 2), new Vector3(400f, 0.2f, 400f), new Vector2(40f, 40f), false);
            var orange = Mat(c, Greybox.Hex(0xd8541e), 0.2f);
            Greybox.Box("Stage", c.Venue, orange, W(0, -0.38, L / 2 + 2), new Vector3(20f, 0.7f, 26f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x7a2a10), Greybox.Hex(0xf2f2f2));
            // the globe and its rings of blue light
            var globe = Greybox.Primitive(PrimitiveType.Sphere, "Planetarium", c.Venue, Mat(c, Greybox.Hex(0x1a1c22), 0.5f), false);
            globe.transform.localPosition = W(-34, 6, -4); globe.transform.localScale = Vector3.one * 26f;
            var blue = Glow(c, new Color(0.3f, 0.7f, 2.4f));
            foreach (float y in new[] { 0f, 4.5f, 9f, 13f })
            {
                float r = Mathf.Sqrt(Mathf.Max(1f, 13f * 13f - (y - 6f) * (y - 6f))) + 0.2f;
                var ring = Greybox.MeshObject("GlobeRing", c.Venue, Greybox.Torus(r, 0.25f, 48, 6), blue);
                ring.transform.localPosition = W(-34, y, -4); ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);       // (the torus is made upright)
            }
            var trunk = Mat(c, Greybox.Hex(0x5a4632), 0.1f); var leaf = Mat(c, Greybox.Hex(0x1f4a2a), 0.1f);
            for (float z = -14f; z < L + 20f; z += 6f) foreach (int s in new[] { -1, 1 }) Palm(c, W(s * 19f, -0.75, z), 8f, trunk, leaf);
            // the canopy roof on the right
            var canopyRoof = Mat(c, Greybox.Hex(0xd8d8d8), 0.3f);
            Greybox.Box("Canopy", c.Venue, canopyRoof, W(28, 7, 8), new Vector3(14f, 0.3f, 50f), false);
            for (float z = -14f; z < 32f; z += 8f) Greybox.Box("CanopyPost", c.Venue, c.Steel, W(22, 3.2, z), new Vector3(0.3f, 7f, 0.3f), false);
            // the dome over the court on its posts, a band of lights
            var dome = Greybox.Primitive(PrimitiveType.Sphere, "Dome", c.Venue, Mat(c, Greybox.Hex(0xd8a868), 0.3f), false);
            dome.transform.localPosition = W(0, CH + 2.6, L / 2); dome.transform.localScale = new Vector3(2 * HW + 3.2f, 3.6f, L + 3.2f);
            Greybox.Box("DomeBand", c.Venue, Glow(c, new Color(2.6f, 2.2f, 1.6f)), W(0, CH + 1.0, L / 2), new Vector3(2 * HW + 3.0f, 0.2f, L + 3.0f), false);
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { -1.4f, L + 1.4f })
                Greybox.Box("DomePost", c.Venue, c.Steel, W(s * (HW + 1.4), (CH + 1) / 2, z), new Vector3(0.2f, CH + 1f, 0.2f), false);
            // white sofas down both sides, green stands behind them, white flags round the edge
            var sofa = Mat(c, Greybox.Hex(0xf2f2f0), 0.2f);
            foreach (int s in new[] { -1, 1 })
                for (float z = 0.8f; z < L; z += 2.4f)
                {
                    Greybox.Box("Sofa", c.Venue, sofa, W(s * (HW + 1.5), 0.25, z), new Vector3(0.9f, 0.5f, 1.9f), false);
                    Greybox.Box("SofaBack", c.Venue, sofa, W(s * (HW + 1.9), 0.55, z), new Vector3(0.2f, 0.8f, 1.9f), false);
                }
            Seats(c, Greybox.Hex(0x2a8a3a));
            SideAndBack(c, 8, 11, 24f, 3.0f);
            var flag = Mat(c, Greybox.Hex(0xf2f2f2), 0.1f);
            for (float z = -4f; z < L + 10f; z += 3f) foreach (int s in new[] { -1, 1 }) Greybox.Box("Flag", c.Venue, flag, W(s * (HW + 9.8), 2.0, z), new Vector3(0.05f, 3.2f, 0.9f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.0), 4.8, -2.0), 5.6f, 3.2f);
            SkyBeams(c, new Color(0.35f, 0.6f, 1.9f, 0.26f), 8, 51, 12f, 22f, -12f, L + 14f);
            CourtBoards(c, v, "ALEXANDRIA OPEN - BIBLIOTHECA ALEXANDRINA", Color.white, Greybox.Hex(0x0c2a74));
            Ambient(new Color(0.20f, 0.22f, 0.32f), new Color(0.22f, 0.18f, 0.18f), new Color(0.08f, 0.07f, 0.07f));
            RenderSettings.fog = false;
        }

        /// <summary>#5 Paris Squash (and the Paris Finals): the Cirque d'Hiver - the court in the ring, red velvet seats all round,
        /// the blue big-top dome, chandeliers, the red and gold ring entrance.</summary>
        static void Cirque(Ctx c, TourVenue v)
        {
            Vector3 centre = W(0, 0, L / 2);
            const float R = 25f;
            var dark = Mat(c, Greybox.Hex(0x3a0a14), 0.2f);
            Greybox.Box("Floor", c.Venue, dark, centre + Vector3.down * 0.07f, new Vector3(2 * R, 0.1f, 2 * R));
            var ring = Greybox.MeshObject("Ring", c.Venue, Greybox.Disc(9.2f, 48), Glow(c, new Color(0.12f, 0.36f, 0.62f)));
            ring.transform.localPosition = centre + Vector3.up * 0.005f; ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            GlassShell(c);
            // the seats: a full circle (the camera's line kept open behind the back glass), red velvet
            Seats(c, Greybox.Hex(0x9a1424));
            for (int k = 0; k < 24; k++)
            {
                float a = k * 15f;
                if (a < 20f || a > 340f) continue;                                      // behind the back glass: the cameras
                string name = "RingStand" + k;
                Stand(c, name, 13, i => centre + new Vector3(0, 0, 10.2f + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.75f, 60 + k, SteepTread);
                var st = c.Venue.Find(name);
                if (st != null) st.RotateAround(centre, Vector3.up, a);
            }
            // the back: a low curved block behind the cameras' lane
            Stand(c, "StandBack", 6, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 2f, 59, SteepTread);
            var gold = Mat(c, Greybox.Hex(0xc8a040), 0.7f); gold.SetFloat("_Metallic", 0.7f);
            var rail = Greybox.MeshObject("Balustrade", c.Venue, Greybox.Torus(9.8f, 0.08f, 64, 6), gold);
            rail.transform.localPosition = centre + Vector3.up * 1.0f; rail.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            // the outer wall, the dome and its ribs, the rim of gold lamps
            for (int k = 0; k < 32; k++)
            {
                var w = Greybox.Box("CircusWall", c.Venue, dark, centre + Quaternion.Euler(0, k * 11.25f, 0) * new Vector3(0, 8f, R), new Vector3(5.2f, 16f, 0.5f), false);
                w.transform.localRotation = Quaternion.Euler(0, k * 11.25f, 0);
            }
            var domeMat = Glow(c, new Color(0.10f, 0.20f, 0.52f));
            var domeGo = Greybox.MeshObject("Dome", c.Venue, SkyDome(R + 1f, 40, 20), domeMat);
            domeGo.transform.localPosition = centre + Vector3.up * 13f; domeGo.transform.localScale = new Vector3(1f, 0.55f, 1f);
            var ribs = new MeshMerge();
            Vector3 top = centre + Vector3.up * 27f;
            for (int k = 0; k < 24; k++) ribs.Beam(top, centre + Quaternion.Euler(0, k * 15f, 0) * new Vector3(0, 16f, R - 0.5f), 0.25f, 0.25f);
            ribs.Build("DomeRibs", c.Venue, gold);
            var rimLamps = new MeshMerge();
            for (int k = 0; k < 64; k++) rimLamps.Box(centre + Quaternion.Euler(0, k * 5.625f, 0) * new Vector3(0, 15.6f, R - 0.6f), Vector3.one * 0.3f, Quaternion.identity);
            rimLamps.Build("RimLamps", c.Venue, Glow(c, new Color(2.8f, 2.0f, 1.0f)));
            var bulbs = Glow(c, new Color(2.6f, 2.4f, 2.1f));
            for (int k = 0; k < 8; k++) Chandelier(c, centre + Quaternion.Euler(0, 22.5f + k * 45f, 0) * new Vector3(0, 13.5f, R - 5f), 1.3f, bulbs, gold, false);
            Chandelier(c, centre + Vector3.up * 18f, 3.0f, bulbs, gold, true);
            // the ring entrance beyond the front wall: red and gold, curtains, the band's balcony, the red stairs
            var redVelvet = Mat(c, Greybox.Hex(0x8a0c1c), 0.15f);
            Vector3 gate = centre + new Vector3(0, 0, -(R - 2f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("GatePillar", c.Venue, gold, gate + new Vector3(s * 3.2f, 3.5f, 0), new Vector3(0.8f, 7f, 0.8f), false);
            Greybox.Box("GateHeader", c.Venue, gold, gate + new Vector3(0, 7.4f, 0), new Vector3(7.6f, 0.9f, 0.9f), false);
            Greybox.Box("Curtain", c.Venue, redVelvet, gate + new Vector3(0, 3.5f, -0.4f), new Vector3(5.6f, 7f, 0.2f), false);
            Greybox.Box("BandBalcony", c.Venue, redVelvet, gate + new Vector3(0, 9.4f, 0.6f), new Vector3(10f, 1.2f, 2f), false);
            Greybox.Box("BandRail", c.Venue, gold, gate + new Vector3(0, 10.2f, 1.6f), new Vector3(10f, 0.1f, 0.1f), false);
            for (int k = 0; k < 6; k++) Greybox.Box("Stair", c.Venue, redVelvet, gate + new Vector3(4.8f, 0.2f + k * 0.35f, 2.8f - k * 0.45f), new Vector3(2f, 0.35f, 0.45f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, centre + new Vector3(s * 11f, 11f, -6f), 6f, 3.4f, s * -25f);
            CourtBoards(c, v, "PARIS SQUASH - CIRQUE D HIVER", Color.white, Greybox.Hex(0x0a3a5a));
            Rig(c, HW + 0.6f, -0.8f, L + 0.8f, 10f, new Color(3f, 3f, 2.8f), new Color(0.6f, 0.7f, 1.6f, 0.14f), false, 8, 13);
            Ambient(new Color(0.22f, 0.18f, 0.24f), new Color(0.28f, 0.14f, 0.14f), new Color(0.10f, 0.06f, 0.06f));
            Fog(Greybox.Hex(0x0c0814), 30f, 90f);
        }

        /// <summary>#6 Qatar Classic (and Doha's other events and Finals): the Khalifa complex - Doha's skyline mural on the far
        /// wall, a blue court lettered QATAR, the black board over it, steep green stands with blue LED fronts.</summary>
        static void Khalifa(Ctx c, TourVenue v)
        {
            const float hx = 22f, front = -16f, back = 34f, roof = 14f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x3a3c42), 0.3f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x14306a), Greybox.Hex(0xe8e8f0));
            var wall = Mat(c, Greybox.Hex(0x1c1e26), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, wall, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("BackWall", c.Venue, wall, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("Ceiling", c.Venue, Glow(c, Greybox.Hex(0x16181e)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var vents = new MeshMerge();
            for (float x = -hx + 4; x < hx; x += 8f) for (float z = front + 4; z < back; z += 8f) vents.Box(W(x, roof - 0.15, z), new Vector3(1.4f, 0.1f, 1.4f), Quaternion.identity);
            vents.Build("Vents", c.Venue, Glow(c, new Color(1.6f, 1.6f, 1.7f)));
            // Doha at night, the length of the far wall
            var mural = c.Palette.UnlitColor(new Color(1.05f, 1.05f, 1.05f)); mural.mainTexture = ProcTex.SkylineMural();
            Greybox.TexturedBox("Mural", c.Venue, mural, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.2f), new Vector2(2 * hx, roof), false);
            // the black board over the court, its curved light bars, the scoreboard under it
            var board = LedMat(c.Palette, "t_khalifa_board", "QATAR CLASSIC  -  DOHA  -  ", Greybox.Hex(0x9a9ca4), Greybox.Hex(0x0c0d10), 1.0f);
            Greybox.TexturedBox("HangingBoard", c.Venue, board, W(0, 10.4, -0.6), new Vector3(13f, 2.6f, 0.4f), new Vector2(13f, 2.6f), false);
            var lamp = Glow(c, new Color(3f, 3f, 2.9f));
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 22f * Mathf.Deg2Rad;
                    Greybox.Box("SpotBar", c.Venue, lamp, W(s * (7.4 + Mathf.Sin(a) * 1.2), 9.4 + Mathf.Cos(a) * 1.4, -0.6), new Vector3(0.45f, 0.45f, 0.45f), false);
                }
            Greybox.TexturedBox("Scoreboard", c.Venue, FeedMat(c), W(0, 8.5, -0.5), new Vector3(4.2f, 1.2f, 0.1f), new Vector2(4.2f, 1.2f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 14f, 9f, front + 0.6f), 6f, 3.4f);
            // steep green stands with blue LED fronts, stairs, the blue back stand
            Seats(c, Greybox.Hex(0x1f8a3a));
            SideAndBack(c, 14, 11, 30f, 2.0f);
            var ledFront = LedMat(c.Palette, "t_khalifa_front", "QATAR  -  QATARSQUASH.COM  -  QATAR CLASSIC  -  ", Color.white, Greybox.Hex(0x1a3ad0), 1.25f);
            foreach (int s in new[] { -1, 1 })
            {
                var b = Greybox.TexturedBox("StandLed", c.Venue, ledFront, W(s * (HW + 1.6), 0.55, L / 2 + 2), new Vector3(0.05f, 0.9f, 26f), new Vector2(26f, 0.9f), false);
                b.transform.localRotation = Quaternion.Euler(0, 0, s * -20f);
            }
            CourtBoards(c, v, "QATAR - QATAR CLASSIC - DOHA", Color.white, Greybox.Hex(0x14306a));
            Rig(c, HW + 0.4f, -0.4f, L + 0.4f, 9.2f, new Color(3f, 3f, 2.9f), Color.clear, false, 0, 19);
            Ambient(new Color(0.48f, 0.50f, 0.58f), new Color(0.42f, 0.42f, 0.46f), new Color(0.20f, 0.20f, 0.22f));
            RenderSettings.fog = false;
        }

        /// <summary>#8 Hong Kong Open (and the Hong Kong Finals): under a white canopy on the harbour's edge at night, the
        /// neon skyline across Victoria Harbour and in the water.</summary>
        static void Harbour(Ctx c, TourVenue v)
        {
            Sky(c, false, 900f);
            var water = Glow(c, Greybox.Hex(0x060a1c));
            Greybox.Box("Harbour", c.Venue, water, W(0, -1.6, -200), new Vector3(1400f, 0.2f, 900f), false);
            var deck = Tex(c, new Color(0.6f, 0.58f, 0.56f), ProcTex.Concrete(), 0.25f);
            Greybox.TexturedBox("Promenade", c.Venue, deck, W(4, -0.3, L / 2 + 10), new Vector3(42f, 0.5f, 52f), new Vector2(6f, 6f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0c1a4a), Greybox.Hex(0xe8e8f0));
            Color[] neon = { new Color(2.6f, 0.3f, 1.6f), new Color(0.3f, 0.9f, 2.6f), new Color(0.4f, 2.4f, 1.0f), new Color(2.6f, 1.8f, 0.4f) };
            Skyline(c, 90, 61, 260f, 620f, -150f, 40f, 40f, 300f, neon, -1.6f, true);
            // the needle tower and the bank's tower, lit
            var needle = Glow(c, new Color(1.6f, 1.7f, 2.2f));
            Greybox.Box("Needle", c.Venue, needle, W(-60, 140, -420), new Vector3(16f, 280f, 16f), false);
            Greybox.Box("NeedleSpire", c.Venue, needle, W(-60, 300, -420), new Vector3(2f, 40f, 2f), false);
            var bank = Greybox.Box("BankTower", c.Venue, Glow(c, new Color(1.2f, 1.5f, 2.4f)), W(80, 120, -360), new Vector3(30f, 240f, 30f), false);
            bank.transform.localRotation = Quaternion.Euler(0, 45f, 0);
            var trunk = Mat(c, Greybox.Hex(0x5a4632), 0.1f); var leaf = Mat(c, Greybox.Hex(0x1f4a2a), 0.1f);
            for (float z = -6f; z < L + 30f; z += 7f) Palm(c, W(-16.5, -0.05, z), 7f, trunk, leaf);
            var rail = new MeshMerge();
            rail.Beam(W(-17, 1.0, -15), W(-17, 1.0, L + 35), 0.06f, 0.06f); rail.Beam(W(-17, 0.5, -15), W(-17, 0.5, L + 35), 0.04f, 0.04f);
            rail.Beam(W(-17, 1.0, -15), W(25, 1.0, -15), 0.06f, 0.06f);
            rail.Build("HarbourRail", c.Venue, c.Steel);
            Canopy(c, v, 8.6f, 11f, "HONG KONG SQUASH OPEN");
            Seats(c, Greybox.Hex(0x1c2236));
            SideAndBack(c, 9, 11, 26f);
            var tower = new MeshMerge(); TrussBar(tower, W(-12, 0, L + 5), W(-12, 16, L + 5), 0.6f); tower.Build("LightTower", c.Venue, c.Steel);
            Greybox.Box("TowerLamps", c.Venue, Glow(c, new Color(3f, 3f, 2.8f)), W(-12, 16.4, L + 5), new Vector3(2.4f, 0.6f, 0.6f), false);
            Screen(c, W(13.5, 4.6, 2.0), 6.4f, 3.6f, -30f);
            CourtBoards(c, v, "HONG KONG SQUASH OPEN - VICTORIA HARBOUR", Color.white, Greybox.Hex(0x0a1e6a));
            SkyBeams(c, new Color(1.2f, 0.5f, 1.8f, 0.22f), 6, 63, 16f, 26f, -10f, L + 14f);
            Ambient(new Color(0.20f, 0.18f, 0.30f), new Color(0.22f, 0.16f, 0.24f), new Color(0.08f, 0.07f, 0.09f));
            RenderSettings.fog = false;
        }

        /// <summary>#7 U.S. Open: the US Squash Center - the arched white truss roof, brick, mezzanines either side, the blue court
        /// under a rig, the red glow at the far end, blue stands.</summary>
        static void UsOpen(Ctx c, TourVenue v)
        {
            const float hx = 17f, front = -16f, back = 34f, wallH = 10f, rise = 7f;
            var floor = Mat(c, Greybox.Hex(0x2a2c32), 0.4f);
            Greybox.Box("Floor", c.Venue, floor, W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0e1838), Greybox.Hex(0xf0f2f6));
            var white = Mat(c, Greybox.Hex(0xe8e8ea), 0.2f);
            var brick = Tex(c, new Color(0.62f, 0.34f, 0.26f), ProcTex.Concrete(), 0.1f);
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("WallLower", c.Venue, white, W(s * hx, 2.5, (front + back) / 2), new Vector3(0.4f, 5f, back - front), false);
                Greybox.TexturedBox("WallBrick", c.Venue, brick, W(s * hx, 7.5, (front + back) / 2), new Vector3(0.4f, 5f, back - front), new Vector2(3f, 3f), false);
            }
            Greybox.TexturedBox("EndWall", c.Venue, brick, W(0, (wallH + rise) / 2, front), new Vector3(2 * hx, wallH + rise, 0.4f), new Vector2(3f, 3f), false);
            Greybox.Box("BackWall", c.Venue, white, W(0, (wallH + rise) / 2, back), new Vector3(2 * hx, wallH + rise, 0.4f), false);
            var win = c.Palette.Clear(new Color(0.45f, 0.55f, 1.0f, 1f)); win.mainTexture = ProcTex.ArchWindow();
            Greybox.TexturedBox("ArchWindow", c.Venue, win, W(0, wallH + 1, front + 0.3), new Vector3(14f, 10f, 0.05f), new Vector2(14f, 10f), false);
            Vault(c, hx, front, back, wallH, rise, Glow(c, new Color(0.07f, 0.08f, 0.12f)), Mat(c, Greybox.Hex(0xf2f2f4), 0.4f), 4f);
            // the mezzanines down both sides: glass balustrades, people on the rails
            var slab = Mat(c, Greybox.Hex(0xd8dadc), 0.2f);
            var glassRail = c.Palette.Clear(new Color(0.7f, 0.85f, 0.95f, 0.18f));
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("Mezzanine", c.Venue, slab, W(s * (hx - 3.2), 4.4, (front + 4 + back) / 2), new Vector3(6.4f, 0.4f, back - front - 4), false);
                Greybox.Box("Balustrade", c.Venue, glassRail, W(s * (hx - 6.4), 5.1, (front + 4 + back) / 2), new Vector3(0.04f, 1.0f, back - front - 4), false);
                Greybox.Box("RailTop", c.Venue, c.Steel, W(s * (hx - 6.4), 5.62, (front + 4 + back) / 2), new Vector3(0.08f, 0.06f, back - front - 4), false);
                Stand(c, s < 0 ? "MezzLeft" : "MezzRight", 2, i => W(s * (hx - 5.8 + i * ROW_DEPTH), 4.6, L / 2 + 2f), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 28f, s + 71, FlatTread);
            }
            // the far end's red glow, the two screens hung high, the rig with its beams
            var red = Glow(c, new Color(2.2f, 0.25f, 0.35f));
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 4; k++) Greybox.Box("RedPanel", c.Venue, red, W(s * (4.5 + k * 2.4), 6.5, front + 0.5), new Vector3(2.0f, 5f, 0.1f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 6.5, 10f, -4f), 6f, 3.4f);
            Rig(c, HW + 1.4f, -1.5f, L + 1.5f, 9f, new Color(3f, 2.9f, 2.8f), new Color(1.6f, 0.3f, 0.5f, 0.14f), false, 10, 23);
            Seats(c, Greybox.Hex(0x1a3a9a));
            SideAndBack(c, 4, 12, 24f);
            CourtBoards(c, v, "U.S. OPEN - US SQUASH - PHILADELPHIA", Color.white, Greybox.Hex(0x0a1438));
            Ambient(new Color(0.30f, 0.28f, 0.34f), new Color(0.30f, 0.24f, 0.26f), new Color(0.12f, 0.10f, 0.10f));
            Fog(Greybox.Hex(0x0e0a10), 30f, 110f);
        }

        /// <summary>#10 El Gouna International: the marina's square at night - white Mediterranean buildings uplit lavender and
        /// purple, Egyptian flags, string lights, the crowd packed all round the glowing court.</summary>
        static void Marina(Ctx c, TourVenue v)
        {
            Sky(c, false, 600f);
            var pavers = Tex(c, new Color(0.62f, 0.56f, 0.5f), ProcTex.Concrete(), 0.2f);
            Greybox.TexturedBox("Square", c.Venue, pavers, W(0, -0.75, L / 2), new Vector3(300f, 0.2f, 300f), new Vector2(30f, 30f), false);
            Greybox.Box("Stage", c.Venue, Mat(c, Greybox.Hex(0x101218), 0.3f), W(0, -0.38, L / 2 + 2), new Vector3(16f, 0.7f, 24f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0c1430), Greybox.Hex(0xb070ff));
            // the square's buildings, uplit: lavender below, paler above; arched windows warm
            var lower = Glow(c, new Color(0.78f, 0.62f, 1.0f)); var upper = Glow(c, new Color(0.86f, 0.80f, 0.98f));
            var windows = Glow(c, new Color(1.8f, 1.25f, 0.7f));
            void Building(Vector3 at, Vector3 size, float facing)
            {
                Facade(c, at + Vector3.down * size.y * 0.25f, new Vector3(size.x, size.y * 0.5f, size.z), lower, windows, 2, facing);
                Facade(c, at + Vector3.up * size.y * 0.25f, new Vector3(size.x, size.y * 0.5f, size.z), upper, windows, 2, facing);
                var dome = Greybox.Primitive(PrimitiveType.Sphere, "Dome", c.Venue, upper, false);
                dome.transform.localPosition = at + Vector3.up * (size.y / 2); dome.transform.localScale = Vector3.one * Mathf.Min(size.x, size.z) * 0.3f;
            }
            for (float z = -18f; z < L + 30f; z += 12f) { Building(W(-24, 6, z), new Vector3(10f, 13f, 11f), 90f); Building(W(24, 6, z), new Vector3(10f, 13f, 11f), -90f); }
            for (float x = -20f; x <= 20f; x += 13f) Building(W(x, 7, -26), new Vector3(12f, 15f, 10f), 0f);
            FlagRow(c, ClubVenues.Flag("EGY"), W(-18.5, 9, -14), new Vector3(0, 0, 9f), 5, new Vector3(0.05f, 0.5f, 1.4f));
            FlagRow(c, ClubVenues.Flag("EGY"), W(18.5, 9, -10), new Vector3(0, 0, 9f), 5, new Vector3(0.05f, 0.5f, 1.4f));
            // string lights across the square
            var bulb = Glow(c, new Color(2.6f, 1.8f, 0.8f));
            var bulbs = new MeshMerge();
            for (float z = -12f; z < L + 26f; z += 5f)
                for (int k = 0; k <= 30; k++)
                {
                    float t = k / 30f;
                    bulbs.Box(Vector3.Lerp(W(-19, 10, z), W(19, 10, z + 3), t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.8f, Vector3.one * 0.12f, Quaternion.identity);
                }
            bulbs.Build("StringLights", c.Venue, bulb);
            Seats(c, Greybox.Hex(0x1a1c24));
            SideAndBack(c, 8, 12, 24f);
            Stand(c, "StandFront", 6, i => W(0, -0.6, -8 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 12f, 81, SteepTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 4.8), 5f, -2f), 5.6f, 3.2f);
            if (c.High) foreach (int s in new[] { -1, 1 }) Point(c, "PurpleUplight", W(s * 18, 2, L / 2), new Color(0.7f, 0.4f, 1f), 30f, 3f);
            var ledFront = LedMat(c.Palette, "t_marina_front", "EL GOUNA INTERNATIONAL SQUASH OPEN  -  ", Color.white, Greybox.Hex(0x0c0d12), 1.15f);
            Greybox.TexturedBox("StageBoard", c.Venue, ledFront, W(0, -0.2, L + 3.6), new Vector3(16f, 0.6f, 0.06f), new Vector2(16f, 0.6f), false);
            CourtBoards(c, v, "EL GOUNA - RED SEA", Color.white, Greybox.Hex(0x0c2a86));
            SkyBeams(c, new Color(0.8f, 0.5f, 1.9f, 0.24f), 8, 83, 14f, 22f, -12f, L + 14f);
            Ambient(new Color(0.26f, 0.22f, 0.34f), new Color(0.28f, 0.22f, 0.30f), new Color(0.10f, 0.08f, 0.10f));
            RenderSettings.fog = false;
        }

        /// <summary>#11 Silicon Valley Open: Squash Zone made into an event - the low white ceiling and its ducts, the blue court,
        /// stands right up to the glass, a rig of moving heads in a blue wash, the green-lit sponsor corner.</summary>
        static void SquashZone(Ctx c, TourVenue v)
        {
            const float hx = 14f, front = -10f, back = 26f, ceil = 7.6f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x33363c), 0.3f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0e1430), Greybox.Hex(0xf2c81e));
            var white = Mat(c, Greybox.Hex(0xeaeaea), 0.15f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, white, W(s * hx, ceil / 2, (front + back) / 2), new Vector3(0.3f, ceil, back - front), false);
            Greybox.Box("EndWall", c.Venue, white, W(0, ceil / 2, front), new Vector3(2 * hx, ceil, 0.3f), false);
            Greybox.Box("BackWall", c.Venue, white, W(0, ceil / 2, back), new Vector3(2 * hx, ceil, 0.3f), false);
            Greybox.Box("Ceiling", c.Venue, white, W(0, ceil + 0.1, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var duct = Mat(c, Greybox.Hex(0xb8bcc2), 0.6f);
            foreach (int s in new[] { -1, 1 })
            {
                var d = Greybox.Primitive(PrimitiveType.Cylinder, "Duct", c.Venue, duct, false);
                d.transform.localPosition = W(s * 7.5, ceil - 0.5, (front + back) / 2); d.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                d.transform.localScale = new Vector3(0.7f, (back - front) / 2, 0.7f);
            }
            for (float z = front + 4; z < back; z += 9f) foreach (int s in new[] { -1, 1 })
            {
                var col = Greybox.Primitive(PrimitiveType.Cylinder, "Column", c.Venue, white, false);
                col.transform.localPosition = W(s * 9.6, ceil / 2, z); col.transform.localScale = new Vector3(0.6f, ceil / 2, 0.6f);
            }
            Seats(c, Greybox.Hex(0x1c1e28));
            SideAndBack(c, 6, 7, 22f, 1.4f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, ceil - 0.6f, new Color(2.6f, 2.8f, 3.2f), new Color(0.3f, 0.5f, 1.8f, 0.16f), false, 12, 29);
            var tower = new MeshMerge(); TrussBar(tower, W(-11, 0, -7), W(-11, ceil - 0.4, -7), 0.5f); tower.Build("LightTower", c.Venue, c.Steel);
            Screen(c, W(hx - 0.4, 4, 3), 5f, 2.8f, 90f);
            Greybox.Box("SponsorGreen", c.Venue, Glow(c, new Color(0.3f, 1.8f, 0.6f)), W(hx - 0.3, 2.2, 20), new Vector3(0.1f, 4.4f, 8f), false);
            CourtBoards(c, v, "SILICON VALLEY OPEN - SQUASH ZONE", Color.white, Greybox.Hex(0x0a1230));
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.3f, 0.5f, 1.2f), s * (HW + 1.2f), -0.5f, L + 3f);
            Ambient(new Color(0.40f, 0.42f, 0.50f), new Color(0.36f, 0.36f, 0.42f), new Color(0.16f, 0.16f, 0.18f));
            RenderSettings.fog = false;
        }

        /// <summary>#12 Singapore Open: OCBC Arena - a dark arena, black roof, silver ducts, the court glowing cyan, a sea of blue
        /// seats on all sides, purple light along every stand, the event's mark on the floor.</summary>
        static void Ocbc(Ctx c, TourVenue v)
        {
            const float hx = 30f, front = -26f, back = 42f, roof = 18f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x0e0f12), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a30), Greybox.Hex(0xff4a8a));
            var wall = Mat(c, Greybox.Hex(0xc8cacc), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, wall, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("EndWall", c.Venue, wall, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("BackWall", c.Venue, wall, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("Roof", c.Venue, Glow(c, Greybox.Hex(0x08090c)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var duct = Mat(c, Greybox.Hex(0xc0c4ca), 0.7f);
            for (float x = -20f; x <= 20f; x += 10f)
            {
                var d = Greybox.Primitive(PrimitiveType.Cylinder, "Duct", c.Venue, duct, false);
                d.transform.localPosition = W(x, roof - 2.5, (front + back) / 2); d.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                d.transform.localScale = new Vector3(1.2f, (back - front) / 2, 1.2f);
            }
            var rig = new MeshMerge();
            for (float z = front + 2; z < back; z += 6f) TrussBar(rig, W(-hx, roof - 1, z), W(hx, roof - 1, z), 0.8f);
            rig.Build("RoofTrusses", c.Venue, c.Steel);
            // billboards and section signs high on the walls
            var bb = LedMat(c.Palette, "t_ocbc_bb", "SINGAPORE SQUASH OPEN  -  OCBC ARENA  -  ", Color.white, Greybox.Hex(0xc8102e), 1.0f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("Billboard", c.Venue, bb, W(s * (hx - 0.3), 12, (front + back) / 2), new Vector3(0.05f, 2.2f, 30f), new Vector2(30f, 2.2f), false);
            Seats(c, Greybox.Hex(0x1a4ad8));
            SideAndBack(c, 14, 12, 34f, 2.0f);
            Stand(c, "StandFront", 10, i => W(0, 0, -4.2 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 10f, 91, SteepTread);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.7f, 0.3f, 1.4f), s * (HW + 1.8f), -2f, L + 6f);
            // the event's mark on the black floor behind the glass
            var mark = LedMat(c.Palette, "t_ocbc_floor", "SINGAPORE SQUASH OPEN", new Color(1.4f, 0.4f, 0.8f), Greybox.Hex(0x0a0b10), 1.0f);
            var fm = Greybox.TexturedBox("FloorMark", c.Venue, mark, W(0, -0.01, L + 4.8), new Vector3(7f, 0.02f, 2.2f), new Vector2(7f, 2.2f), false);
            Screen(c, W(0, 9.5, -3.5), 7f, 4f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 12f, 10.5, L / 2), 6f, 3.4f, s * -60f);
            // the court's cyan glow, its light bar
            if (c.High) Point(c, "CourtCyan", W(0, 3, L / 2), new Color(0.3f, 0.9f, 1f), 14f, 2.2f);
            Rig(c, HW + 0.6f, -0.6f, L + 0.6f, 10f, new Color(3f, 3f, 3f), new Color(0.6f, 0.35f, 1.6f, 0.14f), false, 10, 31);
            CourtBoards(c, v, "SINGAPORE - SQUASH OPEN", Color.white, Greybox.Hex(0x0a3a78));
            Ambient(new Color(0.20f, 0.22f, 0.30f), new Color(0.18f, 0.18f, 0.24f), new Color(0.06f, 0.06f, 0.08f));
            Fog(Greybox.Hex(0x06070a), 34f, 140f);
        }

        /// <summary>#13 Texas Open: a dark warehouse - the royal-blue court with yellow lines, a truss of stage lights and beams,
        /// cream sofas and bleachers behind the glass, the nations' flags.</summary>
        static void Texas(Ctx c, TourVenue v)
        {
            const float hx = 16f, front = -12f, back = 38f, roof = 16f;          // (the high opening shot is inside it)
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x18191c), 0.3f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1440), Greybox.Hex(0xf6c81a));
            var dark = Mat(c, Greybox.Hex(0x16171a), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, dark, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.3f, roof, back - front), false);
            Greybox.Box("EndWall", c.Venue, dark, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.3f), false);
            Greybox.Box("BackWall", c.Venue, dark, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.3f), false);
            Greybox.Box("Roof", c.Venue, Glow(c, Greybox.Hex(0x0c0d10)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var grid = new MeshMerge();
            for (float z = front + 2; z < back; z += 4f) TrussBar(grid, W(-hx, roof - 0.8, z), W(hx, roof - 0.8, z), 0.45f);
            grid.Build("RoofTruss", c.Venue, c.Steel);
            Rig(c, HW + 1.0f, -1f, L + 1f, 8.6f, new Color(2.8f, 2.8f, 3.2f), new Color(0.4f, 0.55f, 1.8f, 0.16f), false, 14, 37);
            // cream sofas behind the back glass, bleachers behind them
            var cream = Mat(c, Greybox.Hex(0xe8dcc4), 0.2f);
            for (int k = -2; k <= 2; k++)
            {
                Greybox.Box("Sofa", c.Venue, cream, W(k * 1.5, 0.25, L + 2.0), new Vector3(1.3f, 0.5f, 0.9f), false);
                Greybox.Box("SofaBack", c.Venue, cream, W(k * 1.5, 0.6, L + 2.4), new Vector3(1.3f, 0.8f, 0.2f), false);
            }
            Stand(c, "SofaCrowd", 1, i => W(0, 0, L + 2.1), new Vector3(1, 0, 0), Vector3.back, 7f, 95, FlatTread);
            Seats(c, Greybox.Hex(0x22242c));
            SideAndBack(c, 7, 9, 22f);
            // the nations' flags down the left wall, the lit sign, the screen
            string[] nats = { "USA", "MEX", "EGY", "ENG", "FRA", "COL", "PER", "NZL", "IND", "CAN" };
            for (int k = 0; k < nats.Length; k++)
                FlagRow(c, ClubVenues.Flag(nats[k]), W(-hx + 0.25, 7.2, -8 + k * 2.6), Vector3.zero, 1, new Vector3(0.05f, 0.5f, 2.2f));
            var sign = LedMat(c.Palette, "t_texas_sign", "TEXAS SQUASH OPEN  -  HOUSTON  -  ", new Color(2.4f, 1.2f, 0.4f), Greybox.Hex(0x0c0c0e), 1.1f);
            Greybox.TexturedBox("WallSign", c.Venue, sign, W(hx - 0.2, 5, 6), new Vector3(0.05f, 1.2f, 12f), new Vector2(12f, 1.2f), false);
            Screen(c, W(-hx + 0.5, 4.2, 10), 5.6f, 3.2f, -90f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.3f, 0.4f, 1.4f), s * (HW + 1.3f), -0.5f, L + 3f);
            CourtBoards(c, v, "TEXAS SQUASH OPEN - HOUSTON", new Color(2f, 1.7f, 0.4f), Greybox.Hex(0x0a1440));
            Ambient(new Color(0.24f, 0.25f, 0.32f), new Color(0.22f, 0.22f, 0.26f), new Color(0.08f, 0.08f, 0.10f));
            Fog(Greybox.Hex(0x08090c), 26f, 90f);
        }

        /// <summary>#14 Australian Open: an arena bowl - packed steep stands all round, the black roof with its green rim, the
        /// court on a black stage with green glowing lines, two screens hung over it.</summary>
        static void Brisbane(Ctx c, TourVenue v)
        {
            Greybox.Box("ArenaFloor", c.Venue, Mat(c, Greybox.Hex(0x0e0f12), 0.5f), W(0, -0.07, L / 2), new Vector3(70f, 0.1f, 80f));
            Greybox.Box("Stage", c.Venue, Mat(c, Greybox.Hex(0x16171a), 0.4f), W(0, -0.04, L / 2 + 1.5), new Vector3(2 * HW + 6f, 0.06f, L + 7f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a2a), Greybox.Hex(0x40e090));
            Seats(c, Greybox.Hex(0x1c1e26));
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "StandLeft" : "StandRight", 20, i => W(s * (HW + 3.0 + i * ROW_DEPTH), 0, L / 2 + 2.5),
                    new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 40f, s + 101, SteepTread);
            Stand(c, "StandFront", 20, i => W(0, 0, -4.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 6.4f, 103, SteepTread);
            Stand(c, "StandBack", 16, i => W(0, 0, L + 8f + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 6.4f, 105, SteepTread);
            // the VIP rows at the court's side
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "VipLeft" : "VipRight", 2, i => W(s * (HW + 1.4 + i * ROW_DEPTH), 0, L / 2), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 8f, s + 107, FlatTread);
            // the roof and its green rim
            Greybox.Box("Roof", c.Venue, Glow(c, Greybox.Hex(0x050608)), W(0, 24, L / 2), new Vector3(80f, 0.2f, 90f), false);
            var green = Glow(c, new Color(0.3f, 2.2f, 0.9f));
            float rimY = SteepTread(19) + 1.4f, rx = HW + 3.0f + 20 * ROW_DEPTH;
            foreach (int s in new[] { -1, 1 }) Greybox.Box("GreenRim", c.Venue, green, W(s * rx, rimY, L / 2 + 2.5), new Vector3(0.12f, 0.12f, 40f), false);
            Greybox.Box("GreenRim", c.Venue, green, W(0, rimY, -4.4f - 20 * ROW_DEPTH), new Vector3(2 * rx, 0.12f, 0.12f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 5.5, 10.5, L / 2), 6f, 3.4f, s * -35f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 11f, new Color(3f, 3f, 3f), new Color(0.3f, 1.6f, 0.8f, 0.12f), false, 10, 41);
            CourtBoards(c, v, "AUSTRALIAN OPEN - BRISBANE", new Color(0.4f, 2f, 1f), Greybox.Hex(0x0a1a2a));
            Ambient(new Color(0.18f, 0.20f, 0.22f), new Color(0.16f, 0.16f, 0.18f), new Color(0.06f, 0.06f, 0.07f));
            Fog(Greybox.Hex(0x050607), 28f, 100f);
        }

        /// <summary>#4 British Open: the Birmingham Rep - the court on the stage under the proscenium, the red-seated auditorium
        /// rising behind the cameras, magenta uplight on black, purple screens hung high, BIRMINGHAM along the stage's edge.</summary>
        static void Rep(Ctx c, TourVenue v)
        {
            float stageFront = L + 2.6f;
            var black = Mat(c, Greybox.Hex(0x0a0a0c), 0.4f);
            Greybox.Box("Stage", c.Venue, black, W(0, -0.07, (stageFront - 8f) / 2), new Vector3(26f, 0.1f, stageFront + 8f));
            Greybox.Box("StallsFloor", c.Venue, Mat(c, Greybox.Hex(0x2a0a10), 0.1f), W(0, -0.6, stageFront + 14), new Vector3(30f, 0.1f, 28f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x16060c), Greybox.Hex(0xff3ab4));
            // the auditorium: red seats rising steeply behind the cameras, red walls with gallery rails
            Seats(c, Greybox.Hex(0x8a1424));
            Stand(c, "Stalls", 18, i => W(0, -0.6, stageFront + 1.4 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 26f, 111, SteepTread);
            var redWall = Mat(c, Greybox.Hex(0x7a1220), 0.2f);
            var gold = Mat(c, Greybox.Hex(0xc8a040), 0.6f);
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("AuditoriumWall", c.Venue, redWall, W(s * 14, 7, stageFront + 12), new Vector3(0.4f, 16f, 24f), false);
                foreach (float y in new[] { 5.5f, 9f })
                {
                    Greybox.Box("Gallery", c.Venue, redWall, W(s * 12.8, y, stageFront + 12), new Vector3(2f, 0.4f, 22f), false);
                    Greybox.Box("GalleryRail", c.Venue, gold, W(s * 11.8, y + 0.6, stageFront + 12), new Vector3(0.08f, 0.08f, 22f), false);
                }
            }
            // the proscenium arch the cameras look through
            var frame = Mat(c, Greybox.Hex(0x1a0a0e), 0.3f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Proscenium", c.Venue, frame, W(s * 11.2, 6.5, stageFront + 0.4), new Vector3(2.2f, 13f, 1.0f), false);
            Greybox.Box("ProsceniumHead", c.Venue, frame, W(0, 13.2, stageFront + 0.4), new Vector3(24.6f, 2.4f, 1.0f), false);
            Greybox.Box("ProsceniumTrim", c.Venue, gold, W(0, 12.0, stageFront + 0.95), new Vector3(20f, 0.15f, 0.1f), false);
            // the stage: wings, the back wall, fly bars of lamps, the BIRMINGHAM sign along its edge
            var curtain = Mat(c, Greybox.Hex(0x1a0408), 0.1f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wing", c.Venue, curtain, W(s * 11.5, 6, (stageFront - 8) / 2), new Vector3(0.3f, 12f, stageFront + 6f), false);
            Greybox.Box("StageBack", c.Venue, black, W(0, 8, -8), new Vector3(26f, 16f, 0.3f), false);
            var dots = new MeshMerge();
            foreach (float y in new[] { 10.5f, 11.6f })
                foreach (float z in new[] { -3f, 2f, 7f })
                    for (float x = -10f; x <= 10f; x += 0.9f) dots.Box(W(x, y, z), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity);
            dots.Build("FlyLamps", c.Venue, Glow(c, new Color(2.6f, 2.3f, 2.0f)));
            var sign = LedMat(c.Palette, "t_rep_sign", "BIRMINGHAM  -  BRITISH OPEN  -  ", Color.white, Greybox.Hex(0x0a0a0c), 1.3f);
            Greybox.TexturedBox("StageSign", c.Venue, sign, W(0, 0.3, stageFront - 0.1), new Vector3(16f, 0.6f, 0.06f), new Vector2(16f, 0.6f), false);
            // the purple screens hung high, the monitors on stands, the on-stage rows
            var purpleFrame = Mat(c, Greybox.Hex(0x3a0a5a), 0.3f);
            foreach (int s in new[] { -1, 1 })
            {
                Screen(c, W(s * 7.6, 8.4, -0.5), 6f, 3.4f, 0f, purpleFrame);
                Screen(c, W(s * (HW + 2.2), 1.7, L + 1.0), 1.6f, 0.9f, s * -30f);
            }
            Seats(c, Greybox.Hex(0x1a0a10));
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "StageLeft" : "StageRight", 3, i => W(s * (HW + 1.8 + i * ROW_DEPTH), 0, L / 2), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 8f, s + 113, FlatTread);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(1.2f, 0.2f, 1.0f), s * (HW + 4.2f), -4f, L + 2f);
            Rig(c, HW + 1.0f, -1f, L + 1f, 10f, new Color(3f, 2.6f, 3f), new Color(1.4f, 0.3f, 1.3f, 0.16f), false, 12, 43);
            CourtBoards(c, v, "BRITISH OPEN - BIRMINGHAM", Color.white, Greybox.Hex(0x3a0818));
            Ambient(new Color(0.16f, 0.12f, 0.18f), new Color(0.20f, 0.10f, 0.16f), new Color(0.06f, 0.04f, 0.05f));
            Fog(Greybox.Hex(0x080406), 26f, 80f);
        }

        /// <summary>#15 London Squash Classic (and the London Finals): the Alexandra Palace Theatre - peeling Victorian walls and
        /// gilded boxes, the crimson court on red carpet in the stalls, a truss of blue and purple beams, the stage behind.</summary>
        static void AllyPally(Ctx c, TourVenue v)
        {
            const float hx = 18f, front = -16f, back = 34f, roof = 16f;
            var wood = Tex(c, new Color(0.75f, 0.6f, 0.45f), ProcTex.Wood(), 0.3f);
            Greybox.TexturedBox("Floor", c.Venue, wood, W(0, -0.08, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front), new Vector2(2.4f, 2.4f));
            Greybox.Box("Carpet", c.Venue, Mat(c, Greybox.Hex(0x9a1424), 0.1f), W(0, -0.03, L / 2 + 1), new Vector3(2 * HW + 4.6f, 0.04f, L + 5f));
            GlassShell(c);
            // the walls: weathered salmon plaster, brick showing through, gilded boxes
            var plaster = Tex(c, new Color(0.84f, 0.62f, 0.54f), ProcTex.Stone(), 0.1f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("Wall", c.Venue, plaster, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("BackWall", c.Venue, plaster, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), new Vector2(3f, 3f), false);
            var rng = new System.Random(47);
            var brickMat = Mat(c, Greybox.Hex(0x7a3a2a), 0.1f); var pale = Mat(c, Greybox.Hex(0xd8c0a8), 0.1f);
            for (int k = 0; k < 40; k++)
            {
                int s = k % 2 == 0 ? -1 : 1;
                Greybox.Box("Patch", c.Venue, rng.NextDouble() < 0.5 ? brickMat : pale, W(s * (hx - 0.22), 2 + rng.NextDouble() * 12, front + rng.NextDouble() * (back - front)),
                    new Vector3(0.05f, 1f + (float)rng.NextDouble() * 2.5f, 1.5f + (float)rng.NextDouble() * 4f), false);
            }
            var gold = Mat(c, Greybox.Hex(0xc8a040), 0.6f); gold.SetFloat("_Metallic", 0.5f);
            var boxMat = Mat(c, Greybox.Hex(0x2a1418), 0.2f);
            foreach (int s in new[] { -1, 1 })
                foreach (float y in new[] { 4.5f, 8f })
                    for (float z = 2f; z < 26f; z += 6f)
                    {
                        Greybox.Box("TheatreBox", c.Venue, boxMat, W(s * (hx - 1.2), y, z), new Vector3(2f, 1.2f, 4.4f), false);
                        Greybox.Box("BoxGilt", c.Venue, gold, W(s * (hx - 2.25), y + 0.55, z), new Vector3(0.12f, 0.2f, 4.4f), false);
                    }
            // the dark ceiling and its grid, the stage behind the front wall
            Greybox.Box("Ceiling", c.Venue, Glow(c, Greybox.Hex(0x0a0a0c)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var grid = new MeshMerge();
            for (float x = -hx; x <= hx; x += 4f) grid.Beam(W(x, roof - 0.6, front), W(x, roof - 0.6, back), 0.12f, 0.12f);
            for (float z = front; z <= back; z += 4f) grid.Beam(W(-hx, roof - 0.6, z), W(hx, roof - 0.6, z), 0.12f, 0.12f);
            grid.Build("CeilingGrid", c.Venue, c.Steel);
            Greybox.Box("Stage", c.Venue, Mat(c, Greybox.Hex(0x1a1214), 0.3f), W(0, 0.6, -9), new Vector3(24f, 1.2f, 12f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Proscenium", c.Venue, gold, W(s * 9, 6, -3.2), new Vector3(1.4f, 12f, 0.8f), false);
            Greybox.Box("ProsceniumHead", c.Venue, gold, W(0, 12.4, -3.2), new Vector3(19.4f, 1.2f, 0.8f), false);
            Greybox.Box("StageCurtain", c.Venue, Mat(c, Greybox.Hex(0x14060a), 0.1f), W(0, 6.5, -14), new Vector3(18f, 11f, 0.2f), false);
            // the stalls: red seats rising behind the cameras, low rows down the sides; purple boards round the carpet
            Seats(c, Greybox.Hex(0x9a1828));
            SideAndBack(c, 4, 16, 22f, 3.0f);
            var boards = LedMat(c.Palette, "t_ally_side", "LONDON SQUASH CLASSIC  -  ", Color.white, Greybox.Hex(0x4a1a7a), 1.1f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("CarpetBoard", c.Venue, boards, W(s * (HW + 2.4), 0.45, L / 2 + 1), new Vector3(0.05f, 0.7f, L + 4f), new Vector2(L + 4f, 0.7f), false);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 7.8, 7.6, -1.2), 5.4f, 3f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (hx - 0.6), 6, L + 8), 4f, 2.2f, s * 90f);
            Rig(c, HW + 1.2f, -1.4f, L + 1.4f, 10f, new Color(2.8f, 2.6f, 3.2f), new Color(0.6f, 0.4f, 1.8f, 0.18f), false, 14, 53);
            CourtBoards(c, v, "LONDON SQUASH CLASSIC - LONDON", Color.white, Greybox.Hex(0x3a0812));
            Ambient(new Color(0.24f, 0.18f, 0.20f), new Color(0.28f, 0.18f, 0.18f), new Color(0.08f, 0.06f, 0.06f));
            Fog(Greybox.Hex(0x0a0608), 28f, 90f);
        }

        /// <summary>#16 Grasshopper Cup: Zurich's station hall - stone and great round-arched windows, a dark steel roof, and a
        /// cloud of glowing white and cyan orbs over the blue court.</summary>
        static void Zurich(Ctx c, TourVenue v)
        {
            const float hx = 24f, front = -36f, back = 40f, wallH = 18f, rise = 10f;
            var stone = Tex(c, new Color(0.92f, 0.82f, 0.68f), ProcTex.Stone(), 0.15f);
            var floor = Tex(c, new Color(0.7f, 0.66f, 0.6f), ProcTex.Marble(), 0.5f);
            HallShell(c, floor, stone, hx, front, back, wallH, new Vector2(3f, 3f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0c1430), Greybox.Hex(0x40c8ff));
            Vault(c, hx, front, back, wallH, rise, Glow(c, new Color(0.05f, 0.06f, 0.09f)), Mat(c, Greybox.Hex(0x22252c), 0.4f), 4f);
            var win = c.Palette.Clear(new Color(0.5f, 0.62f, 1.0f, 1f)); win.mainTexture = ProcTex.ArchWindow();
            foreach (int s in new[] { -1, 1 })
                for (float z = front + 8; z < back - 4; z += 12f)
                    Greybox.TexturedBox("ArchWindow", c.Venue, win, W(s * (hx - 0.35), 10, z), new Vector3(0.05f, 12f, 8f), new Vector2(8f, 12f), false);
            Greybox.TexturedBox("ArchWindow", c.Venue, win, W(0, 12, front + 0.35), new Vector3(20f, 16f, 0.05f), new Vector2(20f, 16f), false);
            var lantern = Glow(c, new Color(2.6f, 1.8f, 0.8f));
            foreach (int s in new[] { -1, 1 }) for (float z = front + 2; z < back; z += 12f) Greybox.Box("Lantern", c.Venue, lantern, W(s * (hx - 0.5), 5, z), new Vector3(0.4f, 0.7f, 0.4f), false);
            // the orbs: long garlands of glowing spheres across the hall, white and cyan
            var white = Glow(c, new Color(1.9f, 1.9f, 2.0f)); var cyan = Glow(c, new Color(0.5f, 1.6f, 2.4f));
            var rng = new System.Random(59);
            var cables = new MeshMerge();
            for (int g = 0; g < 7; g++)
            {
                float z = front + 8 + g * (back - front - 12) / 6f;
                for (int k = 0; k < 34; k++)
                {
                    float x = -hx + 3 + k * (2 * hx - 6) / 33f + (float)(rng.NextDouble() - 0.5) * 1.5f;
                    float y = 14f + Mathf.Sin(k * 0.6f + g) * 1.6f + (float)rng.NextDouble() * 2.2f;
                    float r = 0.2f + (float)(rng.NextDouble() * rng.NextDouble()) * 0.45f;
                    if (Mathf.Abs(z - 30f) < 7f && Mathf.Abs(x) < 8f) continue;              // clear of the high opening shot
                    var orb = Greybox.Primitive(PrimitiveType.Sphere, "Orb", c.Venue, rng.NextDouble() < 0.55 ? white : cyan, false);
                    orb.transform.localPosition = W(x, y, z + (float)(rng.NextDouble() - 0.5) * 3f);
                    orb.transform.localScale = Vector3.one * r * 2f;
                    if (k % 4 == 0) cables.Beam(W(x, y, z), W(x, wallH + rise * 0.6, z), 0.02f, 0.02f);
                }
            }
            cables.Build("OrbCables", c.Venue, c.Steel);
            // the dark structure behind the front wall: speaker towers, a screen
            Greybox.Box("Backdrop", c.Venue, Mat(c, Greybox.Hex(0x0c0d10), 0.3f), W(0, 4.6, -3.2), new Vector3(16f, 9.2f, 0.4f), false);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("SpeakerTower", c.Venue, c.Dark, W(s * 6.8, 5, -2.6), new Vector3(1.2f, 10f, 1.2f), false);
            Screen(c, W(0, 10.6, -2.8), 6.4f, 3.6f);
            Seats(c, Greybox.Hex(0x6a6e78));
            SideAndBack(c, 10, 12, 26f);
            Stand(c, "FloorCrowd", 5, i => W(0, 0, L + 3.0 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 6f, 117, FlatTread);
            Rig(c, HW + 0.8f, -1f, L + 1f, 9.5f, new Color(2.8f, 3f, 3.2f), new Color(0.4f, 0.8f, 1.8f, 0.12f), false, 8, 61);
            CourtBoards(c, v, "GRASSHOPPER CUP - ZURICH", Color.white, Greybox.Hex(0x0a2470));
            Ambient(new Color(0.30f, 0.30f, 0.36f), new Color(0.32f, 0.28f, 0.26f), new Color(0.12f, 0.10f, 0.10f));
            Fog(Greybox.Hex(0x14141c), 40f, 170f);
        }

        /// <summary>#17 China Open: on a deck over the Oriental Sports Center's great outdoor pool at sunset - the lanes beneath the
        /// water, purple truss towers with their screens, a white canopy, Shanghai on the skyline.</summary>
        static void Pool(Ctx c, TourVenue v)
        {
            Sky(c, true, 900f);
            var water = Glow(c, new Color(0.10f, 0.45f, 0.85f));
            Greybox.Box("Pool", c.Venue, water, W(0, -0.62, L / 2), new Vector3(90f, 0.1f, 140f), false);
            var lanes = new MeshMerge();
            for (float x = -44f; x <= 44f; x += 2.5f) if (Mathf.Abs(x) > 8f) lanes.Box(W(x, -0.56, L / 2), new Vector3(0.12f, 0.02f, 138f), Quaternion.identity);
            lanes.Build("LaneLines", c.Venue, Glow(c, new Color(0.04f, 0.12f, 0.35f)));
            var deckMat = Tex(c, new Color(0.86f, 0.86f, 0.84f), ProcTex.Concrete(), 0.3f);
            Greybox.TexturedBox("PoolDeck", c.Venue, deckMat, W(0, -0.62, L / 2), new Vector3(110f, 0.08f, 170f), new Vector2(8f, 8f), false);
            Greybox.TexturedBox("CourtDeck", c.Venue, deckMat, W(0, -0.3, L / 2 + 8), new Vector3(16f, 0.6f, 38f), new Vector2(4f, 4f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0c2a8a), Greybox.Hex(0xff3c9a));
            Canopy(c, v, 8.2f, 10.4f, "CHINA OPEN SQUASH");
            // the purple towers and their screens, the screens' glow on the water
            var purple = Mat(c, Greybox.Hex(0x6a2ab8), 0.4f);
            var pixels = Glow(c, new Color(1.4f, 0.5f, 2.6f));
            foreach (int s in new[] { -1, 1 })
            {
                var m = new MeshMerge();
                TrussBar(m, W(s * 10, -0.6, -2.5), W(s * 10, 12, -2.5), 0.7f);
                TrussBar(m, W(s * 13.4, -0.6, -2.5), W(s * 13.4, 12, -2.5), 0.7f);
                TrussBar(m, W(s * 10, 12, -2.5), W(s * 13.4, 12, -2.5), 0.6f);
                m.Build("PurpleTower", c.Venue, purple);
                var px = new MeshMerge();
                for (float y = 0; y < 12; y += 0.8f) foreach (float x in new[] { 10f, 13.4f }) px.Box(W(s * x, y, -2.1), Vector3.one * 0.16f, Quaternion.identity);
                px.Build("TowerPixels", c.Venue, pixels);
                Screen(c, W(s * 11.7, 8.6, -2.2), 5.8f, 3.4f);
                var glow = c.Palette.Clear(new Color(0.8f, 0.4f, 1.6f, 0.3f)); glow.mainTexture = ProcTex.SoftSpot();
                var q = Greybox.Primitive(PrimitiveType.Quad, "ScreenReflection", c.Venue, glow, false);
                q.transform.localPosition = W(s * 11.7, -0.55, 6); q.transform.localRotation = Quaternion.Euler(90f, 0, 0); q.transform.localScale = new Vector3(6f, 18f, 1f);
            }
            // the floodlight masts, Shanghai beyond, the sign on the deck
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("Mast", c.Venue, c.Steel, W(s * 34, 18, -30), new Vector3(0.8f, 38f, 0.8f), false);
                Greybox.Box("MastLamps", c.Venue, Glow(c, new Color(3f, 3f, 2.8f)), W(s * 34, 37, -30), new Vector3(4f, 2.4f, 0.6f), false);
            }
            Skyline(c, 60, 67, 380f, 700f, -60f, 60f, 40f, 260f, new[] { new Color(1.6f, 0.5f, 2.2f), new Color(0.4f, 1.2f, 2.4f) });
            var sign = LedMat(c.Palette, "t_pool_sign", "CHINA OPEN  ", Color.white, new Color(0.06f, 0.08f, 0.2f), 1.3f);
            Greybox.TexturedBox("DeckSign", c.Venue, sign, W(9.5, 0.6, L + 6), new Vector3(5f, 1.2f, 0.1f), new Vector2(5f, 1.2f), false);
            Seats(c, Greybox.Hex(0x1c2236));
            Stand(c, "StandBack", 12, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 8f, 121, SteepTread);
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "DeckLeft" : "DeckRight", 3, i => W(s * (HW + 1.6 + i * ROW_DEPTH), 0, L + 2), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 10f, s + 123, FlatTread);
            if (c.High) Point(c, "CourtTeal", W(0, 3, L / 2), new Color(0.3f, 1f, 0.85f), 12f, 1.8f);
            CourtBoards(c, v, "CHINA OPEN - SHANGHAI", Color.white, Greybox.Hex(0x0c2a8a));
            SkyBeams(c, new Color(1.0f, 0.5f, 1.8f, 0.2f), 6, 71, 16f, 30f, -10f, L + 10f);
            Ambient(new Color(0.40f, 0.30f, 0.38f), new Color(0.46f, 0.32f, 0.30f), new Color(0.14f, 0.12f, 0.14f));
            RenderSettings.fog = false;
        }
    }
}
