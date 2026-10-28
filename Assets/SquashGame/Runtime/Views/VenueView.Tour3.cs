// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Tour3.cs
// Courts per tournament, part 3 (2026-10-28): the Silver, Bronze and
// Copper events, #18 to #39 - five from the user's photos, the rest from
// photos Claude looked up (RE_SQUASHSTRIKE_COURT_PHOTO_NOTES.md) - and
// every one "as epic as possible" (the user). The plaster club courts
// (Motor City, NASH Cup, Brno, Bermuda) share ClubEvent: the club on
// event day, a steep packed gallery behind the glass either side of the
// cameras' lane.
//
// As in part 2: the back stands start 7.8 m behind the glass (the TV
// camera sits 12.5 m back, 6 m up); a venue with the high opening shot
// is 36 m+ deep and 15 m+ high, the low clubs go without it.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static partial class VenueView
    {
        // =====================================================================================
        // shared pieces (part 3)
        // =====================================================================================

        /// <summary>A flat silhouette standing at depth z (a mountain range, a headland): the outline's points (x, height),
        /// filled down to the ground.</summary>
        static void Ridge(Ctx c, string name, float z, Vector2[] pts, Material mat, float baseY = -2f)
        {
            var m = new MeshMerge();
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                m.Quad(W(a.x, baseY, z), W(b.x, baseY, z), W(b.x, b.y, z), W(a.x, a.y, z));
            }
            m.Build(name, c.Venue, mat);
        }

        /// <summary>A mountain range across the horizon at depth z, the peaks capped with snow.</summary>
        static void Mountains(Ctx c, float z, float x0, float x1, float hMin, float hMax, int peaks, int seed, Color rock, Color snow)
        {
            var rng = new System.Random(seed);
            var pts = new List<Vector2> { new Vector2(x0, hMin * 0.3f) };
            float step = (x1 - x0) / peaks;
            for (int i = 0; i < peaks; i++)
            {
                float x = x0 + (i + 0.5f) * step + (float)(rng.NextDouble() - 0.5) * step * 0.4f;
                float h = Mathf.Lerp(hMin, hMax, (float)rng.NextDouble());
                pts.Add(new Vector2(x, h));
                pts.Add(new Vector2(x + step * 0.5f, h * (0.4f + (float)rng.NextDouble() * 0.25f)));
            }
            pts.Add(new Vector2(x1, hMin * 0.3f));
            Ridge(c, "Mountains", z, pts.ToArray(), Glow(c, rock));
            var caps = new MeshMerge();
            for (int k = 1; k + 1 < pts.Count; k += 2)
            {
                Vector2 p = pts[k], l = Vector2.Lerp(p, pts[k - 1], 0.22f), r = Vector2.Lerp(p, pts[k + 1], 0.22f);
                caps.Quad(W(l.x, l.y, z + 1), W(p.x, p.y, z + 1), W(p.x, p.y, z + 1), W(r.x, r.y, z + 1));
            }
            caps.Build("Snow", c.Venue, Glow(c, snow));
        }

        /// <summary>Open water: a dark glowing sheet (a harbour, a river, the sea).</summary>
        static void Water(Ctx c, Color col, Vector3 centre, Vector2 size) { Greybox.Box("Water", c.Venue, Glow(c, col), centre, new Vector3(size.x, 0.2f, size.y), false); }

        /// <summary>Cloth banners hung in a row (colours in turn), turned to face yaw.</summary>
        static void Banners(Ctx c, Vector3 start, Vector3 step, int n, Vector2 size, Color[] cols, float yaw)
        {
            var mats = System.Array.ConvertAll(cols, k => Mat(c, k, 0.1f));
            for (int i = 0; i < n; i++)
            {
                var b = Greybox.Box("Banner", c.Venue, mats[i % mats.Length], start + step * i, new Vector3(size.x, size.y, 0.04f), false);
                b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        /// <summary>Strings of bulbs swagged across a space (x0..x1 across, every step along z0..z1).</summary>
        static void StringLights(Ctx c, float x0, float x1, float z0, float z1, float step, float y, Color col)
        {
            var bulbs = new MeshMerge();
            for (float z = z0; z <= z1; z += step)
                for (int k = 0; k <= 30; k++)
                {
                    float t = k / 30f;
                    bulbs.Box(Vector3.Lerp(W(x0, y, z), W(x1, y, z + step * 0.5f), t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.6f, Vector3.one * 0.13f, Quaternion.identity);
                }
            bulbs.Build("StringLights", c.Venue, Glow(c, col));
        }

        /// <summary>A white peaked marquee: walls and a roof of two pitched panels.</summary>
        static void Tent(Ctx c, Vector3 at, float w, float d, float wallH, float peak, Material cloth)
        {
            Greybox.Box("TentWalls", c.Venue, cloth, at + Vector3.up * wallH / 2, new Vector3(w, wallH, d), false);
            foreach (int s in new[] { -1, 1 })
            {
                var r = Greybox.Box("TentRoof", c.Venue, cloth, at + new Vector3(s * w / 4, wallH + peak / 2, 0), new Vector3(w * 0.56f, 0.08f, d + 0.3f), false);
                r.transform.localRotation = Quaternion.Euler(0, 0, -s * Mathf.Atan2(peak, w / 2) * Mathf.Rad2Deg);
            }
        }

        /// <summary>A balcony round a hall at height y: the slab, a glass balustrade and its rail, people at the rail
        /// (along z on a side, along x at an end).</summary>
        static void Balcony(Ctx c, string name, float y, Vector3 centre, bool alongZ, float length, float depth, int facingSign, Material slab, int seed)
        {
            Vector3 size = alongZ ? new Vector3(depth, 0.4f, length) : new Vector3(length, 0.4f, depth);
            Greybox.Box(name + "Slab", c.Venue, slab, centre + Vector3.up * y, size, false);
            Vector3 edge = alongZ ? new Vector3(facingSign * depth / 2, 0, 0) : new Vector3(0, 0, facingSign * depth / 2);
            var glass = c.Palette.Clear(new Color(0.7f, 0.85f, 0.95f, 0.16f));
            Vector3 rail = alongZ ? new Vector3(0.04f, 1.0f, length) : new Vector3(length, 1.0f, 0.04f);
            Greybox.Box(name + "Balustrade", c.Venue, glass, centre + edge + Vector3.up * (y + 0.7f), rail, false);
            Greybox.Box(name + "Rail", c.Venue, c.Steel, centre + edge + Vector3.up * (y + 1.22f), alongZ ? new Vector3(0.08f, 0.06f, length) : new Vector3(length, 0.06f, 0.08f), false);
            Vector3 facing = alongZ ? new Vector3(facingSign, 0, 0) : new Vector3(0, 0, facingSign);
            Vector3 along = alongZ ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
            Stand(c, name, 2, i => centre + edge - facing * (0.5f + i * ROW_DEPTH) + Vector3.up * (y + 0.2f), along, facing, length - 1f, seed, FlatTread);
        }

        /// <summary>A dome's upper half (seen from both sides), radius r, rising h.</summary>
        static void Cupola(Ctx c, Vector3 centre, float r, float h, Material mat)
        {
            var m = new MeshMerge();
            const int LON = 32, LAT = 8;
            Vector3 P(int i, int j) { float a = Mathf.PI / 2 * i / LAT, b = Mathf.PI * 2 * j / LON; return centre + new Vector3(Mathf.Cos(a) * Mathf.Cos(b) * r, Mathf.Sin(a) * h, Mathf.Cos(a) * Mathf.Sin(b) * r); }
            for (int i = 0; i < LAT; i++) for (int j = 0; j < LON; j++) m.Quad(P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j));
            m.Build("Cupola", c.Venue, mat);
        }

        /// <summary>A plaster club on event day: the court in a row of each courts either side, the hall round it, strip lights,
        /// a steep packed gallery behind the glass either side of the cameras' lane. glassLeft: the left wall is windows above
        /// the dado (the view outside). Returns the hall's half width and its back.</summary>
        static void ClubEvent(Ctx c, TourVenue v, int each, float roofY, Color hall, Color seat, int rows, bool glassLeft, out float half, out float back)
        {
            var plaster = Lit(c, Greybox.Hex(0xf2f1ed), 0.08f);
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.4f);
            var line = LineMat(c);
            var hallWall = Lit(c, hall, 0.2f);
            var lamp = c.Palette.UnlitColor(c.High ? new Color(2.4f, 2.4f, 2.45f) : new Color(1.4f, 1.4f, 1.42f));
            half = HW + each * Pitch + (each == 0 ? 4.2f : 1.4f); back = L + 14f;
            CourtRow(c, each, plaster, wood, line);
            Greybox.Box("HallFloor", c.Venue, Lit(c, Color.Lerp(v.TinEdge, Color.black, 0.6f), 0.05f), W(0, -0.08, L / 2 + 2), new Vector3(2 * half, 0.1f, L + 30f));
            foreach (int s in new[] { -1, 1 })
            {
                if (s < 0 && glassLeft)
                {
                    Greybox.Box("Dado", c.Venue, hallWall, W(-half, 0.6, L / 2 + 2), new Vector3(0.2f, 1.2f, L + 30f), false);
                    Greybox.Box("Windows", c.Venue, c.Palette.Clear(new Color(0.8f, 0.9f, 1f, 0.06f)), W(-half, (roofY + 1.2f) / 2, L / 2 + 2), new Vector3(0.04f, roofY - 1.2f, L + 30f), false);
                    var mull = new MeshMerge();
                    for (float z = -1f; z <= back; z += 2.4f) mull.Box(W(-half, (roofY + 1.2f) / 2, z), new Vector3(0.1f, roofY - 1.2f, 0.1f), Quaternion.identity);
                    mull.Build("Mullions", c.Venue, Lit(c, Greybox.Hex(0xf2f2f2), 0.3f));
                    continue;
                }
                Greybox.Box("HallWall", c.Venue, hallWall, W(s * half, roofY / 2, L / 2 + 2), new Vector3(0.2f, roofY, L + 30f), false);
            }
            Greybox.Box("HallBack", c.Venue, hallWall, W(0, roofY / 2, back + 0.1), new Vector3(2 * half, roofY, 0.2f), false);
            Greybox.Box("HallFront", c.Venue, hallWall, W(0, roofY / 2, -1.2), new Vector3(2 * half, roofY, 0.2f), false);
            Greybox.Box("Ceiling", c.Venue, Lit(c, Greybox.Hex(0xe8e8ea), 0.1f), W(0, roofY + 0.4, L / 2 + 2), new Vector3(2 * half, 0.1f, L + 30f), false);
            for (int k = -each; k <= each; k++)
                foreach (float off in new[] { -1.2f, 1.2f })
                    Batten(c, lamp, W(k * Pitch + off, roofY - 0.2, 0.3), W(k * Pitch + off, roofY - 0.2, L - 0.3));
            for (float z = L + 3f; z < back; z += 3.5f) Batten(c, lamp, W(-half + 1, roofY - 0.2, z), W(half - 1, roofY - 0.2, z));
            // the gallery: steep packed rows behind the glass, the cameras' lane down the middle
            Seats(c, seat);
            float bw = half - 2.6f;
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "GalleryLeft" : "GalleryRight", rows, i => W(s * (2.3 + bw / 2), 0, L + 1.0 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, bw, 121 + s, SteepTread);
            // the event's board over the front wall
            var board = LedMat(c.Palette, "t_" + v.Key + "_top", ClubVenues.Sign(v.Name + "  -  " + v.Mark + "  -  "), Color.white, Color.Lerp(v.TinEdge, Color.black, 0.5f), 1.1f);
            Greybox.TexturedBox("TopBoard", c.Venue, board, W(0, CH + 1.4, -1.08), new Vector3(6.4f, 0.9f, 0.04f), new Vector2(6.4f, 0.9f), false);
            ClubLight(c, roofY);
        }

        // =====================================================================================
        // the venues (part 3)
        // =====================================================================================

        /// <summary>#18 Squash In The Land: the Cleveland atrium by day - white walls and window bands, the timber soffit, the
        /// blue glass court under a tall steel frame of floodlights, black risers down both sides with orange edges.</summary>
        static void Cleveland(Ctx c, TourVenue v)
        {
            const float hx = 18f, front = -12f, back = 38f, roof = 16f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x9a9ca0), 0.3f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x14161c), Greybox.Hex(0xf07a1a));
            var white = Mat(c, Greybox.Hex(0xeeeeec), 0.15f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, white, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.3f, roof, back - front), false);
            Greybox.Box("BackWall", c.Venue, white, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.3f), false);
            Greybox.Box("RedTrim", c.Venue, Mat(c, Greybox.Hex(0xc8352e), 0.3f), W(hx - 0.2, 0.4, (front + back) / 2), new Vector3(0.1f, 0.8f, back - front), false);
            // the window bands, daylight
            var day = Glow(c, new Color(1.5f, 1.6f, 1.75f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("WindowBand", c.Venue, day, W(s * (hx - 0.2), 11.5, (front + back) / 2), new Vector3(0.05f, 2.2f, back - front - 4), false);
            // the two-storey glass block at the far end, its floors lit
            var block = c.Palette.UnlitColor(new Color(1.1f, 1.15f, 1.2f)); block.mainTexture = ProcTex.CityWindows(1);
            Greybox.TexturedBox("OfficeBlock", c.Venue, block, W(0, 5, front + 0.3), new Vector3(2 * hx, 10f, 0.2f), new Vector2(12.8f, 12.8f), false);
            Greybox.Box("UpperWall", c.Venue, white, W(0, 13, front), new Vector3(2 * hx, 6f, 0.3f), false);
            // the curved timber soffit over the far end, the dark open ceiling, the nets
            var timber = Mat(c, Greybox.Hex(0xb8875a), 0.3f);
            var slats = new MeshMerge();
            for (float x = -hx + 0.4f; x < hx; x += 0.5f)
                for (int k = 0; k < 8; k++)
                {
                    float t0 = k / 8f, t1 = (k + 1) / 8f;
                    slats.Beam(W(x, roof - 1 - Mathf.Sin(t0 * Mathf.PI / 2) * 3, front + t0 * 12), W(x, roof - 1 - Mathf.Sin(t1 * Mathf.PI / 2) * 3, front + t1 * 12), 0.12f, 0.08f);
                }
            slats.Build("TimberSoffit", c.Venue, timber);
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0x1c1e22), 0.2f), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var recessed = new MeshMerge();
            for (float x = -hx + 3; x < hx; x += 6f) for (float z = front + 14; z < back; z += 6f) recessed.Box(W(x, roof - 0.12, z), new Vector3(0.6f, 0.05f, 0.6f), Quaternion.identity);
            recessed.Build("Downlights", c.Venue, Glow(c, new Color(2.4f, 2.4f, 2.3f)));
            Greybox.Box("Nets", c.Venue, c.Palette.Clear(new Color(0.05f, 0.05f, 0.06f, 0.35f)), W(0, roof - 2.5, (front + back) / 2 + 6), new Vector3(2 * hx - 2, 0.02f, 20f), false);
            // the tall steel frame over the court and its floodlight bars
            var frame = new MeshMerge();
            const float fy = 9.5f;
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { -1.5f, L + 1.5f }) frame.Beam(W(s * (HW + 2.2), 0, z), W(s * (HW + 2.2), fy, z), 0.3f, 0.3f);
            foreach (int s in new[] { -1, 1 }) frame.Beam(W(s * (HW + 2.2), fy, -1.5), W(s * (HW + 2.2), fy, L + 1.5), 0.35f, 0.5f);
            for (float z = -1.5f; z <= L + 1.6f; z += (L + 3f) / 4f) frame.Beam(W(-HW - 2.2, fy, z), W(HW + 2.2, fy, z), 0.25f, 0.4f);
            frame.Build("SteelFrame", c.Venue, Mat(c, Greybox.Hex(0x1a1c22), 0.4f));
            var flood = new MeshMerge();
            for (float z = -1.5f; z <= L + 1.6f; z += (L + 3f) / 4f) flood.Box(W(0, fy - 0.35, z), new Vector3(2 * HW + 2f, 0.12f, 0.3f), Quaternion.identity);
            flood.Build("FloodBars", c.Venue, Glow(c, new Color(3.2f, 3.2f, 3.1f)));
            if (c.High) for (int k = 0; k < 3; k++) foreach (int s in new[] { -1, 1 }) Spot(c, W(s * 1.6, fy - 0.5, 1.5 + k * 3.3), 16f, 80f, fy + 4f);
            // black risers down both sides and behind, orange tape at their edges, a blue wash on them
            Seats(c, Greybox.Hex(0x16171a));
            SideAndBack(c, 7, 10, 24f, 1.8f);
            var tape = Glow(c, new Color(2.2f, 0.9f, 0.2f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Tape", c.Venue, tape, W(s * (HW + 1.75), 0.02, L / 2 + 2f), new Vector3(0.06f, 0.01f, 24f), false);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.3f, 0.5f, 1.4f), s * (HW + 1.4f), -0.5f, L + 3f);
            // TVs on stands at the front corners
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("TvStand", c.Venue, c.Steel, W(s * (HW + 2.0), 1.0, -0.8), new Vector3(0.1f, 2f, 0.1f), false);
                Screen(c, W(s * (HW + 2.0), 2.4, -0.8), 1.8f, 1.0f, s * -25f);
            }
            Screen(c, W(hx - 0.4, 6, 4), 6f, 3.4f, 90f);
            CourtBoards(c, v, "SQUASH IN THE LAND - CLEVELAND", Color.white, Greybox.Hex(0x14306a));
            Ambient(new Color(0.78f, 0.80f, 0.84f), new Color(0.66f, 0.66f, 0.66f), new Color(0.36f, 0.34f, 0.32f));
            RenderSettings.fog = false;
        }

        /// <summary>#19 Motor City Open: the Birmingham Athletic Club - white plaster and red lines, a packed wooden bleacher
        /// right up to the glass, banners and club crests hung all round, blue curtains, the scoreboard by the glass.</summary>
        static void Bac(Ctx c, TourVenue v)
        {
            ClubEvent(c, v, 1, 7.6f, Greybox.Hex(0xe6e4de), Greybox.Hex(0x8a6a44), 8, false, out float half, out float back);
            // banners and pennants up both walls and over the gallery: blue, white, burgundy
            Color[] cols = { Greybox.Hex(0x1d3f8a), Greybox.Hex(0xf2f1ed), Greybox.Hex(0x7a1a2a) };
            foreach (int s in new[] { -1, 1 }) Banners(c, W(s * (half - 0.15), 5.6, 0.6), new Vector3(0, 0, 1.6f), 14, new Vector2(1.2f, 1.8f), cols, 90f);
            Banners(c, W(-half + 1.2, 6.2, back - 0.15), new Vector3(1.6f, 0, 0), (int)((2 * half - 2) / 1.6f), new Vector2(1.2f, 1.6f), cols, 0f);
            // the club's crests: blue shields
            var shield = Mat(c, Greybox.Hex(0x1d3f8a), 0.3f); var gold = Glow(c, Greybox.Hex(0xd8b05a));
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("Crest", c.Venue, shield, W(s * 4.6, CH + 1.4, -1.06), new Vector3(1.0f, 1.2f, 0.05f), false);
                Greybox.Box("CrestBand", c.Venue, gold, W(s * 4.6, CH + 1.5, -1.03), new Vector3(0.8f, 0.12f, 0.02f), false);
            }
            // the honours banner, the club's name over the court, the blue curtains, the scoreboard by the glass
            var honours = LedMat(c.Palette, "t_bac_honours", "CHAMPIONS 1963 - 71  -  MOTOR CITY OPEN  -  ", Greybox.Hex(0xd8b05a), Greybox.Hex(0x7a1a2a), 1.0f);
            Greybox.TexturedBox("Honours", c.Venue, honours, W(half - 0.12, 4.2, L / 2), new Vector3(0.04f, 0.9f, 6f), new Vector2(6f, 0.9f), false);
            var curtain = Mat(c, Greybox.Hex(0x1d3a8a), 0.1f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Curtain", c.Venue, curtain, W(s * (half - 0.3), 3.6, L + 5.5), new Vector3(0.2f, 7.2f, 3f), false);
            Screen(c, W(-HW - 1.4, 3.6, L + 0.6), 1.8f, 1.0f, 20f);
            Greybox.Box("WhiteColumn", c.Venue, Lit(c, Greybox.Hex(0xf2f1ed), 0.1f), W(HW + 1.0, 3.8, L + 0.5), new Vector3(0.5f, 7.6f, 0.5f), false);
        }

        /// <summary>#20 Pittsburgh Open: Steel City - a dark industrial hall of rusted girders and riveted trusses, a yellow bridge
        /// truss over the far end, sodium glow, strip lights hung long, dark stepped stands packed on three sides.</summary>
        static void SteelCity(Ctx c, TourVenue v)
        {
            const float hx = 19f, front = -14f, back = 38f, roof = 17f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x1a1a1c), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x101114), Greybox.Hex(0xf2b81e));
            var brick = Tex(c, new Color(0.36f, 0.20f, 0.15f), ProcTex.Concrete(), 0.1f);
            foreach (int s in new[] { -1, 1 }) Greybox.TexturedBox("Wall", c.Venue, brick, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("EndWall", c.Venue, brick, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.4f), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("BackWall", c.Venue, brick, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), new Vector2(3f, 3f), false);
            Greybox.Box("Roof", c.Venue, Glow(c, Greybox.Hex(0x0a0a0c)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            // the girders: columns down both walls, trusses across the roof
            var rust = Mat(c, Greybox.Hex(0x5a3020), 0.35f);
            var girders = new MeshMerge();
            for (float z = front + 3; z < back; z += 6f)
            {
                foreach (int s in new[] { -1, 1 }) girders.Beam(W(s * (hx - 0.6), 0, z), W(s * (hx - 0.6), roof - 1, z), 0.5f, 0.7f);
                TrussBar(girders, W(-hx, roof - 1.4, z), W(hx, roof - 1.4, z), 1.2f);
            }
            girders.Build("Girders", c.Venue, rust);
            // the bridge over the far end: a yellow through-truss
            var yellow = Mat(c, Greybox.Hex(0xf2b81e), 0.4f);
            var bridge = new MeshMerge();
            const float by0 = 9.5f, by1 = 14f, bz = -9f;
            bridge.Beam(W(-hx + 1, by0, bz), W(hx - 1, by0, bz), 0.4f, 0.5f);
            bridge.Beam(W(-hx + 4, by1, bz), W(hx - 4, by1, bz), 0.4f, 0.5f);
            bridge.Beam(W(-hx + 1, by0, bz), W(-hx + 4, by1, bz), 0.35f, 0.4f);
            bridge.Beam(W(hx - 1, by0, bz), W(hx - 4, by1, bz), 0.35f, 0.4f);
            for (float x = -hx + 4; x < hx - 4; x += 3f)
            {
                bridge.Beam(W(x, by0, bz), W(x, by1, bz), 0.2f, 0.2f);
                bridge.Beam(W(x, by0, bz), W(x + 3, by1, bz), 0.15f, 0.15f);
            }
            bridge.Build("BridgeTruss", c.Venue, yellow);
            var bulbs = new MeshMerge();
            for (float x = -hx + 2; x < hx - 1; x += 1.2f) bulbs.Box(W(x, by0 - 0.35, bz + 0.3), Vector3.one * 0.18f, Quaternion.identity);
            bulbs.Build("BridgeLights", c.Venue, Glow(c, new Color(2.8f, 1.8f, 0.7f)));
            // long strip lights hung over the court and stands, the sodium glow
            var strip = Glow(c, new Color(2.6f, 2.5f, 2.3f));
            for (float x = -12f; x <= 12f; x += 4f) Greybox.Box("StripLight", c.Venue, strip, W(x, 11.5, L / 2 + 2), new Vector3(0.14f, 0.08f, 26f), false);
            if (c.High) foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { -6f, 20f }) Point(c, "Sodium", W(s * 14, 8, z), new Color(1f, 0.62f, 0.3f), 24f, 2.2f);
            // the flag and black-and-gold banners on the far wall
            FlagRow(c, ClubVenues.Flag("USA"), W(-3, 12.5, front + 0.3), Vector3.zero, 1, new Vector3(6f, 1.0f, 0.05f));
            foreach (int s in new[] { -1, 1 }) Banners(c, W(s * 9, 9, front + 0.3), new Vector3(s * 2.4f, 0, 0), 3, new Vector2(1.6f, 4f), new[] { Greybox.Hex(0x111214), Greybox.Hex(0xf2b81e) }, 0f);
            Seats(c, Greybox.Hex(0x1e1f24));
            SideAndBack(c, 9, 12, 26f);
            Stand(c, "StandFront", 7, i => W(0, 0, -4.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 10f, 131, SteepTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 9.5, 7.5, -5.6), 5.6f, 3.2f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9f, new Color(3f, 2.8f, 2.4f), new Color(1.6f, 1.0f, 0.4f, 0.14f), false, 10, 133);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(1.4f, 0.8f, 0.2f), s * (HW + 1.3f), -0.5f, L + 3f);
            CourtBoards(c, v, "PITTSBURGH OPEN - STEEL CITY SQUASH", new Color(2f, 1.6f, 0.4f), Greybox.Hex(0x101114));
            Ambient(new Color(0.24f, 0.20f, 0.18f), new Color(0.26f, 0.18f, 0.12f), new Color(0.08f, 0.06f, 0.05f));
            Fog(Greybox.Hex(0x140c06), 26f, 110f);
        }

        /// <summary>#21 New Zealand Open: the Isaac Theatre Royal - the court on the stage, the house beyond the front wall in a
        /// horseshoe of four tiers, cream-and-gold balcony fronts, red velvet, the painted dome and its chandelier, the
        /// proscenium behind the cameras, footlights and follow-spots.</summary>
        static void TheatreRoyal(Ctx c, TourVenue v)
        {
            var black = Mat(c, Greybox.Hex(0x0c0c0e), 0.4f);
            Greybox.Box("Stage", c.Venue, black, W(0, -0.07, (L + 9.4f) / 2), new Vector3(24f, 0.1f, L + 14.6f));
            Greybox.Box("Stalls", c.Venue, Mat(c, Greybox.Hex(0x3a0c14), 0.1f), W(0, -1.3, -14), new Vector3(34f, 0.1f, 24f));
            Greybox.Box("StageEdge", c.Venue, black, W(0, -0.65, -2.6), new Vector3(24f, 1.2f, 0.3f), false);
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0e0f14), Greybox.Hex(0x2a6ad8));
            var cream = Mat(c, Greybox.Hex(0xe8dcc0), 0.3f);
            var gold = Mat(c, Greybox.Hex(0xc8a040), 0.7f); gold.SetFloat("_Metallic", 0.6f);
            var redWall = Mat(c, Greybox.Hex(0x5a0c18), 0.2f);
            // the house: stalls, then dress circle, upper circle and gallery, each wrapping down the sides as a horseshoe
            Seats(c, Greybox.Hex(0x9a1424));
            Stand(c, "StallsSeats", 9, i => W(0, -1.25, -3.6 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 22f, 141, SteepTread);
            float[] ys = { 5.2f, 9.2f, 13.0f }; float[] zs = { -10.5f, -13f, -15.5f };
            for (int t = 0; t < 3; t++)
            {
                float y = ys[t], z = zs[t], w = 26f + t * 3f;
                Greybox.Box("CircleSlab", c.Venue, redWall, W(0, y - 0.3, z - 2.2), new Vector3(w, 0.6f, 5f), false);
                Stand(c, "Circle" + t, 4, i => W(0, y, z - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, w - 2f, 143 + t, SteepTread);
                Greybox.Box("CircleFront", c.Venue, cream, W(0, y + 0.3, z + 0.3), new Vector3(w, 1.2f, 0.3f), false);
                Greybox.Box("CircleTrim", c.Venue, gold, W(0, y + 0.9, z + 0.46), new Vector3(w, 0.12f, 0.06f), false);
                Greybox.Box("CircleTrim", c.Venue, gold, W(0, y - 0.25, z + 0.46), new Vector3(w, 0.1f, 0.06f), false);
                foreach (int s in new[] { -1, 1 })
                {
                    float sx = s * (w / 2 - 1.2f);
                    Greybox.Box("SideSlab", c.Venue, redWall, W(sx + s * 1.2, y - 0.3, z / 2 - 1), new Vector3(3f, 0.6f, -z + 2f), false);
                    Stand(c, "Side" + t + (s < 0 ? "L" : "R"), 2, i => W(sx + s * i * ROW_DEPTH, y, z / 2 - 1), new Vector3(0, 0, 1), new Vector3(-s, 0, 0), -z, 147 + t * 2 + s, FlatTread);
                    Greybox.Box("SideFront", c.Venue, cream, W(sx - s * 0.4, y + 0.3, z / 2 - 1), new Vector3(0.3f, 1.2f, -z), false);
                    Greybox.Box("SideTrim", c.Venue, gold, W(sx - s * 0.56, y + 0.9, z / 2 - 1), new Vector3(0.06f, 0.12f, -z), false);
                }
            }
            foreach (int s in new[] { -1, 1 }) Greybox.Box("HouseWall", c.Venue, redWall, W(s * 17.5, 11, -8), new Vector3(0.4f, 26f, 30f), false);
            Greybox.Box("HouseBack", c.Venue, redWall, W(0, 11, -23), new Vector3(35f, 26f, 0.4f), false);
            // the boxes beside the stage
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 2; k++)
                {
                    Greybox.Box("BoxFront", c.Venue, cream, W(s * 12.5, 4.6 + k * 3.6, 0.5), new Vector3(2.4f, 1.0f, 0.3f), false);
                    Greybox.Box("BoxTrim", c.Venue, gold, W(s * 12.5, 5.15 + k * 3.6, 0.66), new Vector3(2.4f, 0.1f, 0.05f), false);
                    Stand(c, "Box" + k + (s < 0 ? "L" : "R"), 1, i => W(s * 12.5, 4.1 + k * 3.6, 0.0), new Vector3(1, 0, 0), Vector3.forward, 2f, 151 + k + s, FlatTread);
                }
            // the dome: painted, gold-ribbed, the chandelier at its heart; the plasterwork round it
            Vector3 domeC = W(0, 18f, -8f);
            Cupola(c, domeC, 16f, 6f, Glow(c, new Color(0.30f, 0.34f, 0.40f)));
            var ribs = new MeshMerge();
            for (int k = 0; k < 16; k++)
            {
                Vector3 prevRib = domeC + Quaternion.Euler(0, k * 22.5f, 0) * new Vector3(0, 0, 15.8f);
                for (int i = 1; i <= 6; i++)
                {
                    float a = Mathf.PI / 2 * i / 6f;
                    Vector3 p = domeC + Quaternion.Euler(0, k * 22.5f, 0) * new Vector3(0, Mathf.Sin(a) * 5.9f, Mathf.Cos(a) * 15.8f);
                    ribs.Beam(prevRib, p, 0.18f, 0.18f); prevRib = p;
                }
            }
            ribs.Build("DomeRibs", c.Venue, gold);
            var ring = Greybox.MeshObject("DomeRing", c.Venue, Greybox.Torus(16f, 0.3f, 64, 6), gold);
            ring.transform.localPosition = domeC; ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0x1a0a0c), 0.2f), W(0, 24.2, -8), new Vector3(35f, 0.2f, 30f), false);
            Chandelier(c, domeC + Vector3.down * 1.5f, 2.2f, Glow(c, new Color(2.8f, 2.3f, 1.5f)), gold, true);
            // the green scoreboard hung from the gallery
            var score = LedMat(c.Palette, "t_theatre_score", "NEW ZEALAND OPEN  -  ", new Color(0.4f, 2.2f, 0.8f), Greybox.Hex(0x061008), 1.1f);
            Greybox.TexturedBox("Scoreboard", c.Venue, score, W(0, 11.6, -12.2), new Vector3(4.4f, 1.2f, 0.1f), new Vector2(4.4f, 1.2f), false);
            // the proscenium behind the cameras, the fly bars, the footlights along the stage's front
            var frame = Mat(c, Greybox.Hex(0x1a0a0e), 0.3f);
            float pz = L + 3.2f;
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Proscenium", c.Venue, frame, W(s * 11.5, 6.5, pz), new Vector3(1.8f, 13f, 0.8f), false);
            Greybox.Box("ProsceniumHead", c.Venue, frame, W(0, 13.0, pz), new Vector3(24.8f, 2f, 0.8f), false);
            Greybox.Box("ProsceniumGold", c.Venue, gold, W(0, 12.0, pz - 0.45), new Vector3(21f, 0.15f, 0.08f), false);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wing", c.Venue, Mat(c, Greybox.Hex(0x14060a), 0.1f), W(s * 12.2, 7, L / 2 + 3), new Vector3(0.3f, 14f, L + 12f), false);
            var dots = new MeshMerge();
            foreach (float z in new[] { 1f, 5f, 9f })
                for (float x = -10f; x <= 10f; x += 0.9f) dots.Box(W(x, 11.4, z), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.identity);
            for (float x = -11f; x <= 11f; x += 0.7f) dots.Box(W(x, 0.1, -2.4), new Vector3(0.12f, 0.08f, 0.12f), Quaternion.identity);
            dots.Build("StageLamps", c.Venue, Glow(c, new Color(2.8f, 2.6f, 2.2f)));
            Seats(c, Greybox.Hex(0x16060c));
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "OnStageLeft" : "OnStageRight", 3, i => W(s * (HW + 1.8 + i * ROW_DEPTH), 0, L / 2), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 8f, s + 155, FlatTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 9.4, 8.2, -1.0), 4.6f, 2.6f, s * -15f);
            Rig(c, HW + 0.6f, -0.6f, L + 0.6f, 10.4f, new Color(3f, 3f, 3f), new Color(1.6f, 1.5f, 1.3f, 0.10f), false, 8, 157);
            CourtBoards(c, v, "NEW ZEALAND OPEN - CHRISTCHURCH", Color.white, Greybox.Hex(0x0e1a3a));
            if (c.High) Point(c, "HouseLights", W(0, 12, -10), new Color(1f, 0.8f, 0.55f), 30f, 2.2f);
            Ambient(new Color(0.34f, 0.28f, 0.24f), new Color(0.36f, 0.22f, 0.18f), new Color(0.12f, 0.08f, 0.07f));
            Fog(Greybox.Hex(0x120806), 34f, 120f);
        }

        /// <summary>#22 Open Squash Classic: Open Squash in the Financial District at night - the electric-blue court on its maple
        /// plinth, a navy ceiling of LED panels, glass walls on both sides and lower Manhattan's towers lit all round.</summary>
        static void OpenSquash(Ctx c, TourVenue v)
        {
            const float hx = 15f, front = -10f, back = 38f, roof = 15f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x3a3e48), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            var maple = Tex(c, new Color(1f, 0.92f, 0.78f), ProcTex.Wood(), 0.45f);
            Greybox.TexturedBox("Plinth", c.Venue, maple, W(0, -0.02, L / 2 + 1.2), new Vector3(2 * HW + 3f, 0.06f, L + 5f), new Vector2(2f, 2f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1440), Greybox.Hex(0x3a7aff));
            Greybox.Box("Turf", c.Venue, Mat(c, Greybox.Hex(0x2a7a2e), 0.05f), W(hx - 3.5, -0.01, front + 5), new Vector3(6f, 0.03f, 8f), false);
            // glass walls on both sides and at the far end, the city beyond
            var glass = c.Palette.Clear(new Color(0.5f, 0.6f, 0.9f, 0.08f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("GlassWall", c.Venue, glass, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.05f, roof, back - front), false);
            Greybox.Box("GlassEnd", c.Venue, glass, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.05f), false);
            Greybox.Box("BackWall", c.Venue, Mat(c, Greybox.Hex(0x6a6e78), 0.2f), W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.3f), false);
            var mull = new MeshMerge();
            foreach (int s in new[] { -1, 1 }) for (float z = front; z <= back; z += 3f) mull.Box(W(s * hx, roof / 2, z), new Vector3(0.12f, roof, 0.12f), Quaternion.identity);
            for (float x = -hx; x <= hx; x += 3f) mull.Box(W(x, roof / 2, front), new Vector3(0.12f, roof, 0.12f), Quaternion.identity);
            mull.Build("Mullions", c.Venue, c.Steel);
            Sky(c, false, 500f);
            Color[] neon = { new Color(0.3f, 0.6f, 2.6f), new Color(2.2f, 2.2f, 2.4f), new Color(0.3f, 1.8f, 2.4f) };
            Skyline(c, 110, 161, 70f, 320f, -180f, 180f, 50f, 300f, neon, -2f);
            CityGlow(c, 380f, 220, 163, -180f, 180f);
            // the navy ceiling and its rows of square LED panels
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0x0c1a3a), 0.2f), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var panels = new MeshMerge();
            for (float x = -hx + 2.5f; x < hx; x += 2.5f) for (float z = front + 2; z < back; z += 2.5f) panels.Box(W(x, roof - 0.12, z), new Vector3(0.8f, 0.05f, 0.8f), Quaternion.identity);
            panels.Build("LedPanels", c.Venue, Glow(c, new Color(2.4f, 2.5f, 2.7f)));
            Seats(c, Greybox.Hex(0x101830));
            SideAndBack(c, 5, 10, 22f, 2.4f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.25f, 0.45f, 1.8f), s * (HW + 1.7f), -0.5f, L + 3.5f);
            Screen(c, W(0, 8.6, -3.5), 6f, 3.4f);
            Rig(c, HW + 0.6f, -0.6f, L + 0.6f, 9.4f, new Color(2.8f, 2.9f, 3.2f), new Color(0.3f, 0.5f, 1.8f, 0.14f), false, 10, 165);
            if (c.High) Point(c, "CourtBlue", W(0, 3, L / 2), new Color(0.3f, 0.5f, 1f), 14f, 2.2f);
            CourtBoards(c, v, "OPEN SQUASH CLASSIC - NEW YORK", Color.white, Greybox.Hex(0x0a1a5a));
            Ambient(new Color(0.22f, 0.26f, 0.40f), new Color(0.20f, 0.22f, 0.32f), new Color(0.08f, 0.08f, 0.12f));
            RenderSettings.fog = false;
        }

        /// <summary>#23 Malaysia Squash Cup: The Curve's atrium - the court on a hot-pink plinth, its glass edged in pink light,
        /// three floors of shop balconies packed with watchers, banners hung from them, twin screens, the skylight at dusk.</summary>
        static void TheCurve(Ctx c, TourVenue v)
        {
            const float hx = 22f, front = -14f, back = 38f, roof = 20f;
            Greybox.Box("Floor", c.Venue, Tex(c, new Color(0.92f, 0.9f, 0.86f), ProcTex.Marble(), 0.6f), W(0, -0.08, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            Greybox.Box("PinkPlinth", c.Venue, Mat(c, Greybox.Hex(0xe8207a), 0.15f), W(0, -0.03, L / 2 + 2), new Vector3(2 * HW + 8f, 0.06f, L + 10f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x5a0a3a), Greybox.Hex(0xff8ac8));
            // the mall's walls: shop fronts glowing on each floor
            var wall = Mat(c, Greybox.Hex(0xe6e2dc), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, wall, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("EndWall", c.Venue, wall, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("BackWall", c.Venue, wall, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            Color[] shops = { new Color(2.2f, 1.8f, 1.2f), new Color(1.6f, 1.9f, 2.2f), new Color(2.2f, 1.4f, 1.8f), new Color(1.9f, 2.1f, 1.6f) };
            var shopMats = System.Array.ConvertAll(shops, k => Glow(c, k));
            var slab = Mat(c, Greybox.Hex(0xf2f0ec), 0.3f);
            int seed = 171;
            for (int f = 0; f < 4; f++)
            {
                float y = f * 5f;
                for (float z = front + 3; z < back - 2; z += 5f)
                    foreach (int s in new[] { -1, 1 })
                        Greybox.Box("Shop", c.Venue, shopMats[((f + (int)z + s) % 4 + 4) % 4], W(s * (hx - 0.25), y + 1.8, z), new Vector3(0.1f, 3f, 4f), false);
                for (float x = -hx + 3; x < hx - 2; x += 5f)
                    Greybox.Box("Shop", c.Venue, shopMats[((f + (int)x) % 4 + 4) % 4], W(x, y + 1.8, front + 0.25), new Vector3(4f, 3f, 0.1f), false);
                if (f == 0) continue;
                foreach (int s in new[] { -1, 1 })
                    Balcony(c, "Balcony" + f + (s < 0 ? "L" : "R"), y, W(s * (hx - 3f), 0, (front + back) / 2), true, back - front - 2, 6f, -s, slab, seed++);
                Balcony(c, "BalconyEnd" + f, y, W(0, 0, front + 3f), false, 2 * hx - 12f, 6f, 1, slab, seed++);
            }
            // the skylight at dusk
            Greybox.Box("Skylight", c.Venue, Glow(c, new Color(0.55f, 0.42f, 0.7f)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var ribs = new MeshMerge();
            for (float z = front; z <= back; z += 4f) ribs.Beam(W(-hx, roof - 0.2, z), W(hx, roof - 0.2, z), 0.2f, 0.3f);
            ribs.Build("SkylightRibs", c.Venue, Mat(c, Greybox.Hex(0xf2f2f2), 0.3f));
            // banners hung from the balconies, the mall's sign on the end balcony
            Banners(c, W(-hx + 6.3, 12, -4), new Vector3(0, 0, 6f), 6, new Vector2(1.6f, 4.5f), new[] { Greybox.Hex(0xe8207a), Greybox.Hex(0xf6f2f6) }, 90f);
            Banners(c, W(hx - 6.3, 12, -1), new Vector3(0, 0, 6f), 6, new Vector2(1.6f, 4.5f), new[] { Greybox.Hex(0xf6f2f6), Greybox.Hex(0xe8207a) }, 90f);
            var sign = LedMat(c.Palette, "t_curve_sign", "THE CURVE  -  MALAYSIA SQUASH CUP  -  ", Color.white, Greybox.Hex(0x5a0a3a), 1.2f);
            Greybox.TexturedBox("MallSign", c.Venue, sign, W(0, 9.5, -7.95), new Vector3(14f, 1.0f, 0.06f), new Vector2(14f, 1.0f), false);
            Seats(c, Greybox.Hex(0x2a0a1e));
            SideAndBack(c, 4, 7, 22f, 2.6f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 6.2), 3.4, -1.0), 5.2f, 3.0f, s * -20f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(1.6f, 0.3f, 1.0f), s * (HW + 0.6f), -0.5f, L + 1f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("PinkEdge", c.Venue, Glow(c, new Color(2.4f, 0.5f, 1.4f)), W(s * (HW + 0.05), CH + 0.14, L / 2), new Vector3(0.06f, 0.06f, L), false);
            Greybox.Box("PinkEdge", c.Venue, Glow(c, new Color(2.4f, 0.5f, 1.4f)), W(0, CH + 0.14, -0.05), new Vector3(2 * HW, 0.06f, 0.06f), false);
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { -0.6f, L + 0.6f })
                Greybox.Box("GreenSpot", c.Venue, Glow(c, new Color(0.6f, 2.4f, 0.6f)), W(s * (HW + 0.6), 0.2, z), new Vector3(0.3f, 0.3f, 0.3f), false);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9.6f, new Color(3f, 2.9f, 3f), new Color(1.6f, 0.4f, 1.2f, 0.12f), false, 8, 175);
            CourtBoards(c, v, "MALAYSIA SQUASH CUP - THE CURVE", Color.white, Greybox.Hex(0x5a0a3a));
            Ambient(new Color(0.70f, 0.66f, 0.70f), new Color(0.62f, 0.56f, 0.56f), new Color(0.30f, 0.26f, 0.26f));
            RenderSettings.fog = false;
        }

        /// <summary>#26 Canadian Men's Open: Bow Valley Square's atrium at night - a glass curtain wall at the far end onto
        /// Calgary's towers and the Rockies, rings of downlights, a mezzanine of watchers, the raked stand packed behind.</summary>
        static void BowValley(Ctx c, TourVenue v)
        {
            const float hx = 18f, front = -12f, back = 38f, roof = 16f;
            Greybox.Box("Floor", c.Venue, Tex(c, new Color(0.7f, 0.7f, 0.72f), ProcTex.Marble(), 0.55f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a3a), Greybox.Hex(0xc8352e));
            var stone = Mat(c, Greybox.Hex(0x8a8c92), 0.3f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, stone, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("BackWall", c.Venue, stone, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            // the curtain wall and what it looks onto
            Greybox.Box("CurtainWall", c.Venue, c.Palette.Clear(new Color(0.5f, 0.6f, 0.9f, 0.07f)), W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.05f), false);
            var mull = new MeshMerge();
            for (float x = -hx; x <= hx; x += 3f) mull.Box(W(x, roof / 2, front), new Vector3(0.15f, roof, 0.15f), Quaternion.identity);
            for (float y = 4f; y < roof; y += 4f) mull.Box(W(0, y, front), new Vector3(2 * hx, 0.12f, 0.15f), Quaternion.identity);
            mull.Build("CurtainMullions", c.Venue, c.Steel);
            Sky(c, false, 1000f);
            Color[] neon = { new Color(2.4f, 0.4f, 0.4f), new Color(2.2f, 2.2f, 2.4f), new Color(0.4f, 1.2f, 2.4f) };
            Skyline(c, 60, 181, 60f, 300f, -70f, 70f, 40f, 240f, neon, -2f);
            Mountains(c, -720f, -620f, 620f, 70f, 170f, 7, 183, new Color(0.10f, 0.12f, 0.20f), new Color(0.55f, 0.6f, 0.75f));
            CityGlow(c, 420f, 140, 185, -80f, 80f);
            // the ceiling and its rings of downlights, the mezzanine down both sides
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0x1e2026), 0.2f), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var ringMat = Glow(c, new Color(2.6f, 2.5f, 2.3f));
            foreach (float z in new[] { 0f, 12f, 24f })
                foreach (float x in new[] { -9f, 9f })
                {
                    var r = Greybox.MeshObject("LightRing", c.Venue, Greybox.Torus(2.2f, 0.1f, 32, 6), ringMat);
                    r.transform.localPosition = W(x, roof - 0.3, z); r.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                }
            var slab = Mat(c, Greybox.Hex(0xc8cacc), 0.3f);
            foreach (int s in new[] { -1, 1 }) Balcony(c, s < 0 ? "MezzL" : "MezzR", 6f, W(s * (hx - 2.5f), 0, (front + back) / 2), true, back - front - 4, 5f, -s, slab, 187 + s);
            Banners(c, W(-hx + 5.2, 11, -6), new Vector3(0, 0, 5f), 8, new Vector2(1.4f, 3.6f), new[] { Greybox.Hex(0xc8352e), Greybox.Hex(0xf2f1ed) }, 90f);
            Banners(c, W(hx - 5.2, 11, -3.5), new Vector3(0, 0, 5f), 8, new Vector2(1.4f, 3.6f), new[] { Greybox.Hex(0xf2f1ed), Greybox.Hex(0xc8352e) }, 90f);
            Seats(c, Greybox.Hex(0x1a1e2a));
            SideAndBack(c, 6, 12, 24f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 9.5, 8, -4.5), 5.6f, 3.2f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9.4f, new Color(3f, 3f, 3f), new Color(0.6f, 0.7f, 1.6f, 0.12f), false, 8, 189);
            CourtBoards(c, v, "CANADIAN OPEN - CALGARY", Color.white, Greybox.Hex(0x0a1a3a));
            Ambient(new Color(0.28f, 0.30f, 0.40f), new Color(0.26f, 0.26f, 0.32f), new Color(0.10f, 0.10f, 0.12f));
            RenderSettings.fog = false;
        }

        /// <summary>#27 German Open: Sportwerk Hamburg - a red-brick hall with a gallery round three sides, the crowd at its rails
        /// and packed below, green club banners, harbour cranes through the high windows.</summary>
        static void Sportwerk(Ctx c, TourVenue v)
        {
            const float hx = 18f, front = -12f, back = 38f, roof = 15f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x2a2c30), 0.3f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1840), Greybox.Hex(0x2a7a44));
            var brick = Tex(c, new Color(0.55f, 0.24f, 0.17f), ProcTex.Concrete(), 0.1f);
            foreach (int s in new[] { -1, 1 }) Greybox.TexturedBox("Wall", c.Venue, brick, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("BackWall", c.Venue, brick, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("EndWall", c.Venue, brick, W(0, 4, front), new Vector3(2 * hx, 8f, 0.4f), new Vector2(3f, 3f), false);
            Greybox.Box("HighWindows", c.Venue, c.Palette.Clear(new Color(0.5f, 0.6f, 0.9f, 0.06f)), W(0, 11.5, front), new Vector3(2 * hx, 7f, 0.05f), false);
            var mull = new MeshMerge();
            for (float x = -hx; x <= hx; x += 2.4f) mull.Box(W(x, 11.5, front), new Vector3(0.14f, 7f, 0.14f), Quaternion.identity);
            mull.Build("Mullions", c.Venue, Mat(c, Greybox.Hex(0x1a1c20), 0.3f));
            Greybox.Box("Ceiling", c.Venue, Glow(c, Greybox.Hex(0x0e0f12)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var spots = new MeshMerge();
            for (float x = -12f; x <= 12f; x += 4f) for (float z = -6f; z < back; z += 4f) spots.Box(W(x, roof - 0.2, z), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity);
            spots.Build("SpotRows", c.Venue, Glow(c, new Color(2.6f, 2.5f, 2.3f)));
            // the harbour beyond the windows: water, the cranes, the Speicherstadt's lights
            Sky(c, false, 500f);
            Water(c, Greybox.Hex(0x0a1020), W(0, -2, -200), new Vector2(800f, 300f));
            var craneMat = Mat(c, Greybox.Hex(0x30343c), 0.3f);
            var red = Glow(c, new Color(3f, 0.25f, 0.2f));
            for (int k = 0; k < 6; k++)
            {
                float x = -90f + k * 36f, z = -110f - (k % 2) * 40f;
                var cr = new MeshMerge();
                TrussBar(cr, W(x, -2, z), W(x, 52, z), 3f);
                TrussBar(cr, W(x - 10, 52, z), W(x + 46, 52, z), 2f);
                cr.Build("Crane", c.Venue, craneMat);
                Greybox.Box("CraneLight", c.Venue, red, W(x, 55, z), Vector3.one * 1.4f, false);
                Greybox.Box("CraneLight", c.Venue, red, W(x + 46, 53, z), Vector3.one * 1.2f, false);
            }
            CityGlow(c, 300f, 160, 191, -100f, 100f);
            // the gallery round three sides, the crowd at its rails; green banners hung from it
            var slab = Mat(c, Greybox.Hex(0x3a3c42), 0.3f);
            foreach (int s in new[] { -1, 1 }) Balcony(c, s < 0 ? "GalleryL" : "GalleryR", 4.8f, W(s * (hx - 2.2f), 0, (front + back) / 2 + 2), true, back - front - 6, 4.4f, -s, slab, 193 + s);
            Balcony(c, "GalleryEnd", 4.8f, W(0, 0, front + 2.2f), false, 2 * hx - 9f, 4.4f, 1, slab, 195);
            Banners(c, W(-hx + 4.45, 3.4, -6), new Vector3(0, 0, 4.5f), 9, new Vector2(1.4f, 2.4f), new[] { Greybox.Hex(0x2a7a44), Greybox.Hex(0xf2f1ed) }, 90f);
            Banners(c, W(hx - 4.45, 3.4, -4), new Vector3(0, 0, 4.5f), 9, new Vector2(1.4f, 2.4f), new[] { Greybox.Hex(0xf2f1ed), Greybox.Hex(0x2a7a44) }, 90f);
            FlagRow(c, new[] { Greybox.Hex(0x16171a), Greybox.Hex(0xd8302a), Greybox.Hex(0xf2c81e) }, W(-6, 7.6, front + 4.5), new Vector3(4f, 0, 0), 4, new Vector3(2.2f, 0.45f, 0.05f));
            Seats(c, Greybox.Hex(0x1c1e24));
            SideAndBack(c, 7, 11, 24f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 9.5, 9.6, -3.5), 5.2f, 3.0f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9.4f, new Color(3f, 3f, 3f), new Color(0.6f, 0.8f, 1.6f, 0.12f), false, 8, 197);
            CourtBoards(c, v, "GERMAN OPEN - HAMBURG", Color.white, Greybox.Hex(0x0a1840));
            Ambient(new Color(0.30f, 0.30f, 0.36f), new Color(0.30f, 0.24f, 0.22f), new Color(0.10f, 0.08f, 0.08f));
            Fog(Greybox.Hex(0x0c0a0a), 30f, 120f);
        }

        /// <summary>#28 Manchester Open: the National Squash Centre made an arena - the white curved truss roof, steep stands
        /// packed on all four sides, the court glowing blue at its heart, screens and red-and-blue banners.</summary>
        static void Sportcity(Ctx c, TourVenue v)
        {
            const float hx = 28f, front = -24f, back = 40f, wallH = 11f, rise = 9f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x101114), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a40), Greybox.Hex(0xd8302a));
            var wall = Mat(c, Greybox.Hex(0x22242a), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, wall, W(s * hx, (wallH + rise) / 2, (front + back) / 2), new Vector3(0.4f, wallH + rise, back - front), false);
            Greybox.Box("EndWall", c.Venue, wall, W(0, (wallH + rise) / 2, front), new Vector3(2 * hx, wallH + rise, 0.4f), false);
            Greybox.Box("BackWall", c.Venue, wall, W(0, (wallH + rise) / 2, back), new Vector3(2 * hx, wallH + rise, 0.4f), false);
            Vault(c, hx, front, back, wallH, rise, Glow(c, new Color(0.06f, 0.07f, 0.10f)), Mat(c, Greybox.Hex(0xf0f0f2), 0.4f), 4f);
            Seats(c, Greybox.Hex(0x1a2a6a));
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "StandLeft" : "StandRight", 15, i => W(s * (HW + 2.4 + i * ROW_DEPTH), 0, L / 2 + 2f), new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, 34f, s + 201, SteepTread);
            Stand(c, "StandFront", 14, i => W(0, 0, -4.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 8f, 203, SteepTread);
            Stand(c, "StandBack", 13, i => W(0, 0, L + 7.8f + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 8f, 205, SteepTread);
            Banners(c, W(-hx + 0.3, 12.5, -16), new Vector3(0, 0, 5f), 11, new Vector2(2.2f, 3.6f), new[] { Greybox.Hex(0xd8302a), Greybox.Hex(0x1d3f9a) }, 90f);
            Banners(c, W(hx - 0.3, 12.5, -13.5), new Vector3(0, 0, 5f), 11, new Vector2(2.2f, 3.6f), new[] { Greybox.Hex(0x1d3f9a), Greybox.Hex(0xd8302a) }, 90f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 7.5, 11.5, L / 2), 6.4f, 3.6f, s * -40f);
            Screen(c, W(0, 13, -14), 8f, 4.5f);
            Rig(c, HW + 1.2f, -1.2f, L + 1.2f, 12f, new Color(3f, 3f, 3f), new Color(0.4f, 0.6f, 1.8f, 0.14f), false, 14, 207);
            if (c.High) Point(c, "CourtBlue", W(0, 3, L / 2), new Color(0.3f, 0.55f, 1f), 16f, 2.6f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.3f, 0.5f, 1.6f), s * (HW + 2.2f), -2f, L + 6f);
            CourtBoards(c, v, "MANCHESTER OPEN - SPORTCITY", Color.white, Greybox.Hex(0x0a2a6a));
            Ambient(new Color(0.18f, 0.20f, 0.28f), new Color(0.16f, 0.16f, 0.22f), new Color(0.06f, 0.06f, 0.08f));
            Fog(Greybox.Hex(0x06070a), 30f, 120f);
        }

        /// <summary>#29 Squash on Fire Open: the downtown club at night - the sky-blue court with yellow lines and its flame
        /// stripe, fire-orange glow, and through the glass wall the Capitol's dome and the Monument lit white.</summary>
        static void SquashOnFire(Ctx c, TourVenue v)
        {
            const float hx = 15f, front = -10f, back = 38f, roof = 15f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x2e3036), 0.35f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x101216), Greybox.Hex(0xff6a1a));
            var concrete = Tex(c, new Color(0.62f, 0.62f, 0.64f), ProcTex.Concrete(), 0.2f);
            Greybox.TexturedBox("Wall", c.Venue, concrete, W(hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), new Vector2(3f, 3f), false);
            Greybox.TexturedBox("BackWall", c.Venue, concrete, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), new Vector2(3f, 3f), false);
            // the glass wall on the left and at the far end, the city beyond
            var glass = c.Palette.Clear(new Color(0.5f, 0.6f, 0.9f, 0.07f));
            Greybox.Box("GlassWall", c.Venue, glass, W(-hx, roof / 2, (front + back) / 2), new Vector3(0.05f, roof, back - front), false);
            Greybox.Box("GlassEnd", c.Venue, glass, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.05f), false);
            var mull = new MeshMerge();
            for (float z = front; z <= back; z += 3f) mull.Box(W(-hx, roof / 2, z), new Vector3(0.12f, roof, 0.12f), Quaternion.identity);
            for (float x = -hx; x <= hx; x += 3f) mull.Box(W(x, roof / 2, front), new Vector3(0.12f, roof, 0.12f), Quaternion.identity);
            mull.Build("Mullions", c.Venue, c.Steel);
            Sky(c, false, 700f);
            Skyline(c, 70, 211, 60f, 260f, -150f, 30f, 18f, 50f, null, -2f);
            var marble = Glow(c, new Color(1.6f, 1.6f, 1.55f));
            // the Capitol: wings, the drum and the dome, its lantern
            Vector3 cap = W(-60, 0, -420);
            Greybox.Box("CapitolWings", c.Venue, marble, cap + Vector3.up * 14f, new Vector3(220f, 28f, 40f), false);
            var drum = Greybox.Primitive(PrimitiveType.Cylinder, "CapitolDrum", c.Venue, marble, false);
            drum.transform.localPosition = cap + Vector3.up * 44f; drum.transform.localScale = new Vector3(44f, 16f, 44f);
            var domeGo = Greybox.Primitive(PrimitiveType.Sphere, "CapitolDome", c.Venue, marble, false);
            domeGo.transform.localPosition = cap + Vector3.up * 62f; domeGo.transform.localScale = new Vector3(40f, 44f, 40f);
            Greybox.Box("Lantern", c.Venue, marble, cap + Vector3.up * 88f, new Vector3(6f, 10f, 6f), false);
            // the Monument: the white obelisk, its red lights
            Vector3 mon = W(90, 0, -360);
            Greybox.Box("Monument", c.Venue, marble, mon + Vector3.up * 85f, new Vector3(9f, 170f, 9f), false);
            var tip = Greybox.Box("MonumentTip", c.Venue, marble, mon + Vector3.up * 172f, new Vector3(6.4f, 6.4f, 6.4f), false);
            tip.transform.localRotation = Quaternion.Euler(45f, 0, 45f);
            Greybox.Box("AirLight", c.Venue, Glow(c, new Color(3f, 0.2f, 0.15f)), mon + Vector3.up * 165f, new Vector3(10f, 1f, 10f), false);
            CityGlow(c, 420f, 160, 213, -150f, 40f);
            // the ceiling, the flame banners, the fire-orange glow
            Greybox.Box("Ceiling", c.Venue, Mat(c, Greybox.Hex(0x16171a), 0.2f), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var fire = LedMat(c.Palette, "t_fire_banner", "SQUASH ON FIRE  -  WASHINGTON DC  -  ", new Color(2.6f, 1.4f, 0.3f), Greybox.Hex(0x1a0a04), 1.2f);
            Greybox.TexturedBox("FireBanner", c.Venue, fire, W(hx - 0.25, 6.5, (front + back) / 2), new Vector3(0.05f, 1.4f, 30f), new Vector2(30f, 1.4f), false);
            Seats(c, Greybox.Hex(0x1c1a1a));
            SideAndBack(c, 6, 10, 24f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(1.8f, 0.6f, 0.1f), s * (HW + 1.3f), -0.5f, L + 3f);
            Screen(c, W(hx - 0.5, 9.5, 2), 5.6f, 3.2f, 90f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9.4f, new Color(3f, 2.9f, 2.6f), new Color(1.8f, 0.7f, 0.2f, 0.14f), false, 10, 215);
            CourtBoards(c, v, "SQUASH ON FIRE OPEN - WASHINGTON DC", new Color(2.4f, 1.4f, 0.3f), Greybox.Hex(0x101216));
            Ambient(new Color(0.24f, 0.24f, 0.32f), new Color(0.28f, 0.20f, 0.16f), new Color(0.08f, 0.07f, 0.07f));
            RenderSettings.fog = false;
        }

        /// <summary>#30 NASH Cup: the club in London, Ontario on event day - white plaster and red lines, a packed gallery,
        /// maple-leaf banners, a fall-colours mural over the gallery.</summary>
        static void Nash(Ctx c, TourVenue v)
        {
            ClubEvent(c, v, 1, 7.6f, Greybox.Hex(0xe8e4dc), Greybox.Hex(0x9a7a50), 8, false, out float half, out float back);
            Color[] canada = { Greybox.Hex(0xc8352e), Greybox.Hex(0xf2f1ed) };
            foreach (int s in new[] { -1, 1 }) Banners(c, W(s * (half - 0.15), 5.6, 0.8), new Vector3(0, 0, 2.0f), 11, new Vector2(1.4f, 2.0f), canada, 90f);
            // the maple leaves: red diamonds on the white banners
            var leaf = Mat(c, Greybox.Hex(0xc8352e), 0.1f);
            foreach (int s in new[] { -1, 1 })
                for (int k = 1; k < 11; k += 2)
                {
                    var l = Greybox.Box("Maple", c.Venue, leaf, W(s * (half - 0.2), 5.6, 0.8 + k * 2.0), new Vector3(0.5f, 0.5f, 0.02f), false);
                    l.transform.localRotation = Quaternion.Euler(0, 90f, 45f);
                }
            // the mural on the back wall: autumn trees, red and orange and gold
            var rng = new System.Random(221);
            Color[] fall = { new Color(1.5f, 0.35f, 0.15f), new Color(1.6f, 0.75f, 0.15f), new Color(1.3f, 1.0f, 0.25f), new Color(0.9f, 0.2f, 0.12f) };
            var fallMats = System.Array.ConvertAll(fall, k => Glow(c, k));
            Greybox.Box("MuralSky", c.Venue, Glow(c, new Color(0.55f, 0.7f, 0.95f)), W(0, 5.6, back - 0.05), new Vector3(2 * half - 1, 3.6f, 0.04f), false);
            for (int i = 0; i < 40; i++)
            {
                float x = -half + 1 + (float)rng.NextDouble() * (2 * half - 2), y = 4.2f + (float)rng.NextDouble() * 2.6f, s2 = 0.6f + (float)rng.NextDouble() * 0.9f;
                Greybox.Box("Tree", c.Venue, fallMats[rng.Next(fallMats.Length)], W(x, y, back - 0.08), new Vector3(s2, s2, 0.04f), false);
            }
            FlagRow(c, ClubVenues.Flag("CAN"), W(-1.5, CH + 2.6, -1.06), new Vector3(1.0f, 0, 0), 3, new Vector3(0.9f, 0.3f, 0.03f));
            Screen(c, W(HW + 1.4, 3.6, L + 0.6), 1.8f, 1.0f, -20f);
        }

        /// <summary>#31 Budapest Open: City Squash at night - the teal show court on a dark floor, orange glow, and through the
        /// windows the Danube with the Parliament and the Chain Bridge lit gold.</summary>
        static void CitySquash(Ctx c, TourVenue v)
        {
            const float hx = 16f, front = -12f, back = 38f, roof = 15f;
            Greybox.Box("Floor", c.Venue, Mat(c, Greybox.Hex(0x141618), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a2a2a), Greybox.Hex(0xf08a24));
            var dark = Mat(c, Greybox.Hex(0x1a1e20), 0.2f);
            Greybox.Box("Wall", c.Venue, dark, W(hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("BackWall", c.Venue, dark, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("EndWall", c.Venue, dark, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("Dado", c.Venue, dark, W(-hx, 2, (front + back) / 2), new Vector3(0.4f, 4f, back - front), false);
            Greybox.Box("Windows", c.Venue, c.Palette.Clear(new Color(0.5f, 0.6f, 0.9f, 0.06f)), W(-hx, 9.5, (front + back) / 2), new Vector3(0.05f, 11f, back - front), false);
            var mull = new MeshMerge();
            for (float z = front; z <= back; z += 3f) mull.Box(W(-hx, 9.5, z), new Vector3(0.14f, 11f, 0.14f), Quaternion.identity);
            mull.Build("Mullions", c.Venue, c.Steel);
            Greybox.Box("Ceiling", c.Venue, Glow(c, Greybox.Hex(0x0c0e10)), W(0, roof, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            // the river and the far bank: the Parliament's long gothic front, its dome and spires, all gold
            Sky(c, false, 600f);
            Water(c, Greybox.Hex(0x081420), W(-160, -3, L / 2), new Vector2(260f, 600f));
            var goldLit = Glow(c, new Color(2.0f, 1.45f, 0.6f));
            var goldDim = Glow(c, new Color(1.2f, 0.8f, 0.35f));
            Vector3 parl = W(-300, 0, 20);
            Greybox.Box("Parliament", c.Venue, goldDim, parl + Vector3.up * 18f, new Vector3(30f, 36f, 260f), false);
            var dome = Greybox.Primitive(PrimitiveType.Sphere, "ParliamentDome", c.Venue, goldLit, false);
            dome.transform.localPosition = parl + Vector3.up * 50f; dome.transform.localScale = new Vector3(34f, 46f, 34f);
            Greybox.Box("DomeSpire", c.Venue, goldLit, parl + Vector3.up * 80f, new Vector3(3f, 24f, 3f), false);
            for (float z = -120f; z <= 120f; z += 20f) Greybox.Box("Spire", c.Venue, goldLit, parl + new Vector3(10f, 42f, z), new Vector3(2f, 14f, 2f), false);
            var winRow = new MeshMerge();
            for (float y = 8f; y < 34f; y += 6f) for (float z = -125f; z <= 125f; z += 4f) winRow.Box(parl + new Vector3(15.2f, y, z), new Vector3(0.2f, 2.6f, 1.4f), Quaternion.identity);
            winRow.Build("ParliamentWindows", c.Venue, Glow(c, new Color(2.6f, 2.0f, 1.0f)));
            // the Chain Bridge: two lit stone towers, the chains swagged between, bulbs along them
            var stone = Glow(c, new Color(1.3f, 1.05f, 0.7f));
            float bz = -140f;
            foreach (float x in new[] { -70f, -230f })
            {
                Greybox.Box("BridgeTower", c.Venue, stone, W(x, 16, bz), new Vector3(12f, 36f, 18f), false);
                Greybox.Box("BridgeArch", c.Venue, Glow(c, Greybox.Hex(0x081420)), W(x, 10, bz), new Vector3(12.2f, 14f, 8f), false);
            }
            var chain = new MeshMerge();
            Vector3 prev = W(-20, 6, bz);
            float[] xs = { -20f, -70f, -230f, -280f };
            for (int seg = 0; seg < 3; seg++)
            {
                float xa = xs[seg], xb = xs[seg + 1];
                for (int k = 0; k <= 20; k++)
                {
                    float t = k / 20f;
                    float ya = seg == 0 ? Mathf.Lerp(6f, 33f, t) : seg == 2 ? Mathf.Lerp(33f, 6f, t) : 33f - Mathf.Sin(t * Mathf.PI) * 20f;
                    Vector3 p = W(Mathf.Lerp(xa, xb, t), ya, bz);
                    if (k > 0) chain.Beam(prev, p, 0.6f, 0.6f);
                    chain.Box(p + Vector3.down * 0.6f, Vector3.one * 0.9f, Quaternion.identity);
                    prev = p;
                }
            }
            chain.Build("Chains", c.Venue, goldLit);
            Greybox.Box("Deck", c.Venue, goldDim, W(-150, 5, bz), new Vector3(260f, 1.2f, 12f), false);
            CityGlow(c, 420f, 140, 231, -170f, -20f);
            // the teal and orange: banners, edge glow
            Banners(c, W(hx - 0.3, 8, -6), new Vector3(0, 0, 4f), 10, new Vector2(1.4f, 4f), new[] { Greybox.Hex(0x1a7a72), Greybox.Hex(0xf08a24) }, 90f);
            FlagRow(c, new[] { Greybox.Hex(0xc8352e), Greybox.Hex(0xf2f1ed), Greybox.Hex(0x2a7a44) }, W(-6, 11, front + 0.3), new Vector3(4f, 0, 0), 4, new Vector3(2.4f, 0.5f, 0.05f));
            Seats(c, Greybox.Hex(0x14201e));
            SideAndBack(c, 6, 10, 24f);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(1.6f, 0.7f, 0.2f), s * (HW + 1.3f), -0.5f, L + 3f);
            Screen(c, W(hx - 0.5, 9, 4), 5.6f, 3.2f, 90f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 9.4f, new Color(3f, 2.9f, 2.6f), new Color(0.3f, 1.4f, 1.2f, 0.12f), false, 10, 233);
            CourtBoards(c, v, "BUDAPEST OPEN - CITY SQUASH", new Color(2.2f, 1.2f, 0.4f), Greybox.Hex(0x0a2a2a));
            Ambient(new Color(0.22f, 0.24f, 0.28f), new Color(0.26f, 0.22f, 0.18f), new Color(0.08f, 0.07f, 0.06f));
            RenderSettings.fog = false;
        }

        /// <summary>#32 Charlottesville Open: the McArthur Squash Center - a bright hall of cream walls and white roof trusses,
        /// clerestory daylight, navy bleachers with cable rails on both sides, orange-and-navy pennants.</summary>
        static void McArthur(Ctx c, TourVenue v)
        {
            const float hx = 20f, front = -12f, back = 38f, roof = 15f;
            Greybox.Box("Floor", c.Venue, Tex(c, new Color(0.95f, 0.88f, 0.76f), ProcTex.Wood(), 0.45f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a4a), Greybox.Hex(0xf08a24));
            var cream = Mat(c, Greybox.Hex(0xe8d8b8), 0.2f);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("Wall", c.Venue, cream, W(s * hx, roof / 2, (front + back) / 2), new Vector3(0.4f, roof, back - front), false);
            Greybox.Box("EndWall", c.Venue, cream, W(0, roof / 2, front), new Vector3(2 * hx, roof, 0.4f), false);
            Greybox.Box("BackWall", c.Venue, cream, W(0, roof / 2, back), new Vector3(2 * hx, roof, 0.4f), false);
            var day = Glow(c, new Color(1.6f, 1.7f, 1.8f));
            foreach (int s in new[] { -1, 1 })
                for (float z = front + 2; z < back - 1; z += 3.2f) Greybox.Box("Clerestory", c.Venue, day, W(s * (hx - 0.22), roof - 2.2, z), new Vector3(0.05f, 2.4f, 2.6f), false);
            for (float x = -hx + 2; x < hx - 1; x += 3.2f) Greybox.Box("Clerestory", c.Venue, day, W(x, roof - 2.2, front + 0.22), new Vector3(2.6f, 2.4f, 0.05f), false);
            Greybox.Box("Roof", c.Venue, Mat(c, Greybox.Hex(0xe2e2e2), 0.2f), W(0, roof + 1.2, (front + back) / 2), new Vector3(2 * hx, 0.2f, back - front), false);
            var white = Mat(c, Greybox.Hex(0xf4f4f4), 0.4f);
            var trusses = new MeshMerge();
            for (float z = front + 3; z < back; z += 5f) TrussBar(trusses, W(-hx, roof + 0.3, z), W(hx, roof + 0.3, z), 1.1f);
            trusses.Build("RoofTrusses", c.Venue, white);
            var lamps = new MeshMerge();
            for (float x = -12f; x <= 12f; x += 6f) for (float z = front + 3; z < back; z += 5f) lamps.Box(W(x, roof - 0.5, z + 2.5f), new Vector3(1.2f, 0.1f, 0.5f), Quaternion.identity);
            lamps.Build("Lamps", c.Venue, Glow(c, new Color(2.6f, 2.6f, 2.5f)));
            // navy bleachers on both sides and behind, their cable rails
            Seats(c, Greybox.Hex(0x1a2a5a));
            SideAndBack(c, 10, 10, 26f, 2.0f);
            var cable = new MeshMerge();
            foreach (int s in new[] { -1, 1 })
                for (int i = 2; i < 10; i += 3)
                    cable.Beam(W(s * (HW + 2.0 + i * ROW_DEPTH), SteepTread(i) + 1.0, L / 2 + 2 - 13), W(s * (HW + 2.0 + i * ROW_DEPTH), SteepTread(i) + 1.0, L / 2 + 2 + 13), 0.03f, 0.03f);
            cable.Build("CableRails", c.Venue, c.Steel);
            // the pennants, orange and navy, strung over the stands; the TVs on the walls
            var pen = new MeshMerge(); var pen2 = new MeshMerge();
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 24; k++)
                {
                    var target = k % 2 == 0 ? pen : pen2;
                    Vector3 p = W(s * (HW + 6), 10.5 - Mathf.Sin(k / 23f * Mathf.PI) * 1.2f, -8 + k * 1.4f);
                    target.Box(p, new Vector3(0.03f, 0.7f, 0.5f), Quaternion.Euler(0, 0, 0));
                }
            pen.Build("Pennants", c.Venue, Mat(c, Greybox.Hex(0xf08a24), 0.1f));
            pen2.Build("Pennants", c.Venue, Mat(c, Greybox.Hex(0x1a2a5a), 0.1f));
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (hx - 0.4), 7, 4), 4.4f, 2.5f, s * 90f);
            Screen(c, W(0, 9.5, front + 0.6), 6f, 3.4f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 10f, new Color(3f, 3f, 3f), Color.clear, false, 0, 241);
            CourtBoards(c, v, "CHARLOTTESVILLE OPEN - VIRGINIA", new Color(2.2f, 1.2f, 0.4f), Greybox.Hex(0x0a1a4a));
            Ambient(new Color(0.80f, 0.78f, 0.74f), new Color(0.66f, 0.62f, 0.56f), new Color(0.36f, 0.32f, 0.26f));
            RenderSettings.fog = false;
        }

        /// <summary>#33 Vancouver Open: the glass court out on the waterfront under a open sky at dusk - Coal Harbour, the
        /// North Shore mountains with snow on their tops, a seaplane coming in, the city's lights on the water.</summary>
        static void Waterfront(Ctx c, TourVenue v)
        {
            Sky(c, true, 1100f);
            Water(c, Greybox.Hex(0x1a2a4a), W(0, -1.6, -300), new Vector2(1800f, 700f));
            var deck = Tex(c, new Color(0.62f, 0.6f, 0.58f), ProcTex.Concrete(), 0.25f);
            Greybox.TexturedBox("Plaza", c.Venue, deck, W(0, -0.3, L / 2 + 14), new Vector3(70f, 0.5f, 64f), new Vector2(8f, 8f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x14161c), Greybox.Hex(0xe8e8ec));
            Mountains(c, -600f, -650f, 650f, 45f, 95f, 9, 251, new Color(0.30f, 0.26f, 0.40f), new Color(1.4f, 1.15f, 1.2f));
            Color[] neon = { new Color(2.4f, 1.6f, 0.8f), new Color(1.8f, 2.0f, 2.4f) };
            Skyline(c, 50, 253, 120f, 320f, 60f, 160f, 40f, 180f, neon, -1.6f, true);
            Skyline(c, 40, 255, 120f, 320f, -160f, -60f, 40f, 180f, neon, -1.6f, true);
            CityGlow(c, 520f, 120, 257, -60f, 60f);
            // the seaplane coming in over the water
            var plane = Mat(c, Greybox.Hex(0xf2f2f4), 0.4f);
            Vector3 sp = W(-80, 46, -260);
            Greybox.Box("PlaneBody", c.Venue, plane, sp, new Vector3(3f, 2.6f, 12f), false);
            Greybox.Box("PlaneWing", c.Venue, plane, sp + Vector3.up * 1.4f, new Vector3(18f, 0.3f, 2.4f), false);
            foreach (int s in new[] { -1, 1 }) Greybox.Box("PlaneFloat", c.Venue, plane, sp + new Vector3(s * 2.2f, -2.6f, 0), new Vector3(0.8f, 0.8f, 10f), false);
            Greybox.Box("PlaneLight", c.Venue, Glow(c, new Color(3f, 0.3f, 0.2f)), sp + new Vector3(-9f, 1.4f, 0), Vector3.one * 0.6f, false);
            // open to the sky (no canopy: the mountains are the backdrop) - the rig on its four towers
            Rig(c, HW + 1.2f, -1.2f, L + 1.2f, 9.4f, new Color(3f, 2.9f, 2.7f), Color.clear, true, 0, 259);
            var rail = new MeshMerge();
            rail.Beam(W(-30, 1.0, -8), W(30, 1.0, -8), 0.06f, 0.06f); rail.Beam(W(-30, 0.5, -8), W(30, 0.5, -8), 0.04f, 0.04f);
            rail.Build("SeaRail", c.Venue, c.Steel);
            Seats(c, Greybox.Hex(0x1c2236));
            SideAndBack(c, 7, 11, 24f);
            // deck chairs at the water's edge
            var chairCols = new[] { Mat(c, Greybox.Hex(0xd8302a), 0.1f), Mat(c, Greybox.Hex(0x1d4fb0), 0.1f), Mat(c, Greybox.Hex(0xf2c81e), 0.1f) };
            for (int k = 0; k < 10; k++)
            {
                float x = -22f + k * 4.6f;
                if (Mathf.Abs(x) < 8f) continue;
                var ch = Greybox.Box("DeckChair", c.Venue, chairCols[k % 3], W(x, 0.35, -5.5), new Vector3(0.7f, 0.05f, 1.4f), false);
                ch.transform.localRotation = Quaternion.Euler(-30f, 0, 0);
            }
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.4), 4.6, -1.6), 5.2f, 3.0f, s * -20f);
            CourtBoards(c, v, "VANCOUVER OPEN - COAL HARBOUR", Color.white, Greybox.Hex(0x1a2a4a));
            Ambient(new Color(0.46f, 0.38f, 0.48f), new Color(0.50f, 0.36f, 0.34f), new Color(0.16f, 0.12f, 0.12f));
            RenderSettings.fog = false;
        }

        /// <summary>#34 Toronto Classic: Brookfield Place's Galleria - the court on the plaza floor under the white steel forest
        /// of branching arches and the glass vault, towers either side, the CN Tower lit through the roof.</summary>
        static void Galleria(Ctx c, TourVenue v)
        {
            const float hx = 14f, front = -26f, back = 40f, wallH = 11f, rise = 13f;
            Greybox.Box("Floor", c.Venue, Tex(c, new Color(0.8f, 0.76f, 0.72f), ProcTex.Marble(), 0.6f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a5a), Greybox.Hex(0xe8e8ec));
            // the glass vault (see-through: the night and the towers beyond)
            Vault(c, hx, front, back, wallH, rise, c.Palette.Clear(new Color(0.4f, 0.5f, 0.8f, 0.06f)), Mat(c, Greybox.Hex(0xf2f2f4), 0.4f), 6f);
            // the forest: a column each side every 6 m, branching up into the arch
            var white = Mat(c, Greybox.Hex(0xf2f2f4), 0.45f);
            var tree = new MeshMerge();
            for (float z = front + 3; z < back; z += 6f)
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 foot = W(s * (hx - 1), 0, z), fork = W(s * (hx - 1), wallH * 0.55f, z);
                    tree.Beam(foot, fork, 0.6f, 0.6f);
                    for (int b = 0; b < 4; b++)
                    {
                        float a = Mathf.PI * (0.12f + b * 0.1f);                    // up the arch toward its crown
                        Vector3 tip = W(s * Mathf.Cos(a) * hx * 0.98f * (b < 3 ? 1f : -0.4f), wallH + Mathf.Sin(a) * rise * 0.96f, z + (b - 1.5f) * 1.6f);
                        tree.Beam(fork, tip, 0.28f, 0.28f);
                        tree.Beam(tip, tip + (W(0, wallH + rise, z) - tip) * 0.35f, 0.2f, 0.2f);
                    }
                }
            tree.Build("SteelForest", c.Venue, white);
            // the towers either side, through the glass; the CN Tower up the far end
            Sky(c, false, 700f);
            var win = Glow(c, new Color(1.8f, 1.6f, 1.2f));
            foreach (int s in new[] { -1, 1 })
            {
                var tw = c.Palette.UnlitColor(new Color(0.9f, 0.9f, 0.95f)); tw.mainTexture = ProcTex.CityWindows(s < 0 ? 0 : 3);
                Greybox.TexturedBox("Tower", c.Venue, tw, W(s * (hx + 16), 60, (front + back) / 2), new Vector3(30f, 120f, back - front), new Vector2(12.8f, 25.6f), false);
            }
            Vector3 cn = W(30, 0, -340);
            var concrete = Glow(c, new Color(0.55f, 0.55f, 0.62f));
            Greybox.Box("CnShaft", c.Venue, concrete, cn + Vector3.up * 170f, new Vector3(16f, 340f, 16f), false);
            Greybox.Box("CnBase", c.Venue, concrete, cn + Vector3.up * 60f, new Vector3(30f, 120f, 30f), false);
            var pod = Greybox.Primitive(PrimitiveType.Cylinder, "CnPod", c.Venue, Glow(c, new Color(1.2f, 0.6f, 1.8f)), false);
            pod.transform.localPosition = cn + Vector3.up * 340f; pod.transform.localScale = new Vector3(44f, 8f, 44f);
            Greybox.Box("CnSpire", c.Venue, Glow(c, new Color(1.6f, 1.6f, 1.9f)), cn + Vector3.up * 440f, new Vector3(4f, 190f, 4f), false);
            Greybox.Box("AirLight", c.Venue, Glow(c, new Color(3f, 0.2f, 0.15f)), cn + Vector3.up * 538f, Vector3.one * 3f, false);
            Skyline(c, 50, 261, 160f, 420f, -60f, 60f, 60f, 240f, new[] { new Color(1.8f, 2.0f, 2.4f) }, -2f);
            // crowds both sides on the plaza, the screens
            Seats(c, Greybox.Hex(0x1a1e30));
            SideAndBack(c, 8, 11, 26f);
            Stand(c, "StandFront", 6, i => W(0, 0, -4.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 8f, 263, SteepTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 8.5, 9.5, -6.5), 5.2f, 3.0f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 10f, new Color(3f, 3f, 3f), new Color(0.6f, 0.7f, 1.6f, 0.12f), true, 10, 265);
            if (c.High) foreach (int s in new[] { -1, 1 }) Point(c, "ForestUplight", W(s * (hx - 2), 2, 6), new Color(0.75f, 0.8f, 1f), 30f, 2.4f);
            CourtBoards(c, v, "TORONTO CLASSIC - BROOKFIELD PLACE", Color.white, Greybox.Hex(0x0a1a5a));
            Ambient(new Color(0.34f, 0.36f, 0.46f), new Color(0.32f, 0.32f, 0.38f), new Color(0.12f, 0.12f, 0.14f));
            RenderSettings.fog = false;
        }

        /// <summary>#35 Czech Open: Centrum Viktoria in Brno - the plaster court, the orange mark, a packed gallery and a balcony
        /// over the next courts, Czech flags, the window onto the floodlit clay of the tennis stadium.</summary>
        static void Viktoria(Ctx c, TourVenue v)
        {
            ClubEvent(c, v, 1, 8.4f, Greybox.Hex(0xe2e2de), Greybox.Hex(0x2a2c32), 8, true, out float half, out float back);
            // the orange mark over the court, the Czech flags along the right
            var orange = Glow(c, new Color(2.2f, 0.9f, 0.2f));
            var disc = Greybox.Primitive(PrimitiveType.Cylinder, "ViktoriaMark", c.Venue, orange, false);
            disc.transform.localPosition = W(-4.6, CH + 1.4, -1.06); disc.transform.localRotation = Quaternion.Euler(90f, 0, 0); disc.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            for (int f = 0; f < 4; f++)
                FlagRow(c, ClubVenues.Flag("CZE"), W(half - 0.14, 6.6, L + 1.0 + f * 2.2), Vector3.zero, 1, new Vector3(0.03f, 0.5f, 1.6f));
            // a balcony over the next court on the right, the crowd along it
            Balcony(c, "Balcony", 4.6f, W(half - 1.6f, 0, L / 2 + 1), true, L + 4f, 3f, -1, Lit(c, Greybox.Hex(0x3a3c42), 0.2f), 271);
            // outside: the tennis stadium's clay under floodlights, its stand
            Sky(c, false, 500f);
            Greybox.Box("Grounds", c.Venue, Mat(c, Greybox.Hex(0x1a1e1a), 0.1f), W(-80, -0.4, L / 2), new Vector3(140f, 0.2f, 200f), false);
            Greybox.Box("Clay", c.Venue, Mat(c, Greybox.Hex(0xc8642e), 0.1f), W(-60, -0.25, L / 2), new Vector3(24f, 0.1f, 40f), false);
            var chalk = Glow(c, new Color(1.6f, 1.6f, 1.6f));
            foreach (float dx in new[] { -5.5f, 5.5f }) Greybox.Box("ClayLine", c.Venue, chalk, W(-60 + dx, -0.18, L / 2), new Vector3(0.1f, 0.02f, 24f), false);
            foreach (float dz in new[] { -12f, 12f }) Greybox.Box("ClayLine", c.Venue, chalk, W(-60, -0.18, L / 2 + dz), new Vector3(11f, 0.02f, 0.1f), false);
            Greybox.Box("Net", c.Venue, Mat(c, Greybox.Hex(0x16171a), 0.1f), W(-60, 0.4, L / 2), new Vector3(13f, 0.9f, 0.05f), false);
            foreach (float z in new[] { -20f, L + 20f }) LampTower(c, -44f, z, 18f, new Color(3f, 3f, 2.8f));
            Greybox.Box("StadiumStand", c.Venue, Mat(c, Greybox.Hex(0x3a6aa8), 0.2f), W(-82, 4, L / 2), new Vector3(10f, 8f, 50f), false);
            if (c.High) Point(c, "ClayFlood", W(-60, 14, L / 2), new Color(1f, 0.95f, 0.85f), 40f, 3f);
        }

        /// <summary>#36 London Open: Canary Wharf's East Wintergarden - the court under the glass vault, palms down both sides,
        /// a raked crowd on three sides, the Wharf's towers lit above the glass.</summary>
        static void Wintergarden(Ctx c, TourVenue v)
        {
            const float hx = 20f, front = -20f, back = 40f, wallH = 10f, rise = 12f;
            Greybox.Box("Floor", c.Venue, Tex(c, new Color(0.62f, 0.62f, 0.66f), ProcTex.Stone(), 0.4f), W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a1a4a), Greybox.Hex(0xd8302a));
            var glass = c.Palette.Clear(new Color(0.45f, 0.55f, 0.85f, 0.07f));
            foreach (int s in new[] { -1, 1 }) Greybox.Box("GlassWall", c.Venue, glass, W(s * hx, wallH / 2, (front + back) / 2), new Vector3(0.05f, wallH, back - front), false);
            Greybox.Box("GlassEnd", c.Venue, glass, W(0, (wallH + rise) / 2, front), new Vector3(2 * hx, wallH + rise, 0.05f), false);
            Greybox.Box("BackWall", c.Venue, Mat(c, Greybox.Hex(0x2a2c32), 0.2f), W(0, (wallH + rise) / 2, back), new Vector3(2 * hx, wallH + rise, 0.3f), false);
            var whiteSteel = Mat(c, Greybox.Hex(0xf0f0f2), 0.45f);
            Vault(c, hx, front, back, wallH, rise, c.Palette.Clear(new Color(0.4f, 0.5f, 0.8f, 0.06f)), whiteSteel, 3f);
            var cols = new MeshMerge();
            foreach (int s in new[] { -1, 1 }) for (float z = front; z <= back; z += 3f) cols.Beam(W(s * hx, 0, z), W(s * hx, wallH, z), 0.25f, 0.25f);
            cols.Build("Columns", c.Venue, whiteSteel);
            // the Wharf: the towers round about, the tallest with its pyramid top and beacon
            Sky(c, false, 700f);
            Color[] neon = { new Color(1.8f, 2.0f, 2.4f), new Color(0.4f, 1.2f, 2.4f), new Color(2.4f, 0.5f, 0.6f) };
            Skyline(c, 60, 281, 80f, 280f, -180f, 180f, 80f, 220f, neon, -2f);
            var tw = c.Palette.UnlitColor(new Color(0.95f, 0.95f, 1f)); tw.mainTexture = ProcTex.CityWindows(2);
            Vector3 one = W(-20, 0, -130);
            Greybox.TexturedBox("OneCanadaSquare", c.Venue, tw, one + Vector3.up * 117f, new Vector3(50f, 234f, 50f), new Vector2(12.8f, 25.6f), false);
            var cap = Greybox.Box("PyramidTop", c.Venue, Glow(c, new Color(1.4f, 1.5f, 1.7f)), one + Vector3.up * 242f, new Vector3(25f, 25f, 25f), false);
            cap.transform.localRotation = Quaternion.Euler(45f, 0, 45f);
            Greybox.Box("Beacon", c.Venue, Glow(c, new Color(3f, 3f, 3f)), one + Vector3.up * 258f, Vector3.one * 2.4f, false);
            // palms down both sides inside the glass
            var trunk = Mat(c, Greybox.Hex(0x5a4632), 0.1f); var leaf = Mat(c, Greybox.Hex(0x1f5a2a), 0.1f);
            foreach (int s in new[] { -1, 1 }) for (float z = front + 4; z < back - 2; z += 7f) Palm(c, W(s * (hx - 2), 0, z), 8f, trunk, leaf);
            Seats(c, Greybox.Hex(0x1a1c2a));
            SideAndBack(c, 10, 11, 26f);
            Stand(c, "StandFront", 8, i => W(0, 0, -4.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 8f, 283, SteepTread);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * 9, 10, -9), 5.6f, 3.2f);
            Rig(c, HW + 0.8f, -0.8f, L + 0.8f, 10f, new Color(3f, 3f, 3f), new Color(0.5f, 0.6f, 1.6f, 0.14f), true, 12, 285);
            foreach (int s in new[] { -1, 1 }) EdgeGlow(c, new Color(0.4f, 0.5f, 1.4f), s * (HW + 1.5f), -0.5f, L + 3f);
            CourtBoards(c, v, "LONDON OPEN - CANARY WHARF", Color.white, Greybox.Hex(0x0a1a4a));
            Ambient(new Color(0.28f, 0.30f, 0.40f), new Color(0.26f, 0.26f, 0.32f), new Color(0.10f, 0.10f, 0.12f));
            RenderSettings.fog = false;
        }

        /// <summary>#37 Cape Town Open: the court on the Western Province Cricket Club's lawn at golden hour, open to the sky,
        /// Table Mountain's flat top filling the sky behind with its tablecloth of cloud, palms and the pavilion.</summary>
        static void Wpcc(Ctx c, TourVenue v)
        {
            Sky(c, true, 1400f);
            Greybox.Box("Lawn", c.Venue, Mat(c, Greybox.Hex(0x3a6a2a), 0.05f), W(0, -0.3, L / 2), new Vector3(400f, 0.2f, 400f), false);
            Greybox.Box("Deck", c.Venue, Mat(c, Greybox.Hex(0x2a2c32), 0.3f), W(0, -0.1, L / 2 + 4), new Vector3(30f, 0.2f, 34f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a3a7a), Greybox.Hex(0xf2f2f6));
            // Table Mountain: Devil's Peak on the left, the table, Lion's Head on the right
            var rock = Glow(c, new Color(0.26f, 0.16f, 0.20f));
            Ridge(c, "TableMountain", -700f, new[]
            {
                new Vector2(-720, 14), new Vector2(-540, 41), new Vector2(-456, 74), new Vector2(-396, 58), new Vector2(-336, 64),
                new Vector2(-288, 80), new Vector2(228, 80), new Vector2(264, 76), new Vector2(336, 41), new Vector2(408, 44),
                new Vector2(456, 64), new Vector2(504, 44), new Vector2(600, 21), new Vector2(720, 12),
            }, rock);
            var cloud = c.Palette.Clear(new Color(2.0f, 1.9f, 1.85f, 0.8f)); cloud.mainTexture = ProcTex.SoftSpot();
            for (int k = 0; k < 9; k++)
            {
                var q = Greybox.Primitive(PrimitiveType.Quad, "Tablecloth", c.Venue, cloud, false);
                q.transform.localPosition = W(-276 + k * 63, 84, -695); q.transform.localScale = new Vector3(115f, 14f, 1f);
            }
            Ridge(c, "Foothills", -400f, new[] { new Vector2(-600, 6), new Vector2(-300, 12), new Vector2(0, 8), new Vector2(300, 13), new Vector2(600, 5) }, Glow(c, new Color(0.22f, 0.26f, 0.16f)));
            // the pavilion, palms round the lawn, the oaks' dark line
            var whiteWall = Mat(c, Greybox.Hex(0xf2f0ea), 0.2f);
            Facade(c, W(-34, 5, 6), new Vector3(12f, 10f, 30f), whiteWall, Glow(c, new Color(2f, 1.6f, 0.9f)), 2, 90f);
            Greybox.Box("PavilionRoof", c.Venue, Mat(c, Greybox.Hex(0x1f5a32), 0.2f), W(-34, 10.6, 6), new Vector3(14f, 0.5f, 32f), false);
            var trunk = Mat(c, Greybox.Hex(0x5a4632), 0.1f); var leaf = Mat(c, Greybox.Hex(0x1f4a2a), 0.1f);
            for (float z = -14f; z < L + 24f; z += 7f) Palm(c, W(24, -0.3, z), 9f, trunk, leaf);
            // open to the sky (no canopy: Table Mountain is the backdrop) - the rig on its four towers
            Rig(c, HW + 1.2f, -1.2f, L + 1.2f, 9.4f, new Color(3f, 2.9f, 2.7f), Color.clear, true, 0, 299);
            Seats(c, Greybox.Hex(0x1f3a6a));
            SideAndBack(c, 7, 11, 24f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.4), 4.6, -1.6), 5.2f, 3.0f, s * -20f);
            FlagRow(c, ClubVenues.Flag("RSA"), W(-14, 9, -6), new Vector3(4f, 0, 0), 8, new Vector3(1.4f, 0.4f, 0.05f));
            CourtBoards(c, v, "CAPE TOWN OPEN - WPCC", Color.white, Greybox.Hex(0x0a3a7a));
            Ambient(new Color(0.62f, 0.50f, 0.46f), new Color(0.62f, 0.46f, 0.34f), new Color(0.20f, 0.16f, 0.12f));
            RenderSettings.fog = false;
        }

        /// <summary>#38 Indian Open: the Bombay Gymkhana's lawn at night - the glass court on its plinth, teal stands on three
        /// sides, white marquees, string lights and marigold garlands, the old pavilion, Mumbai's towers beyond.</summary>
        static void Gymkhana(Ctx c, TourVenue v)
        {
            Sky(c, false, 800f);
            Greybox.Box("Lawn", c.Venue, Mat(c, Greybox.Hex(0x1f3a1c), 0.05f), W(0, -0.6, L / 2), new Vector3(400f, 0.2f, 400f), false);
            Greybox.Box("Plinth", c.Venue, Mat(c, Greybox.Hex(0x16181e), 0.3f), W(0, -0.3, L / 2 + 4), new Vector3(2 * HW + 26f, 0.6f, L + 34f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x0a2a6a), Greybox.Hex(0xf2a21e));
            // the marquees down both sides, behind the stands
            var cloth = Mat(c, Greybox.Hex(0xf4f2ec), 0.2f);
            foreach (int s in new[] { -1, 1 }) for (float z = -10f; z < L + 26f; z += 9f) Tent(c, W(s * 22, -0.5, z), 8f, 8f, 3.2f, 3.4f, cloth);
            // the pavilion: cream arches, lit
            var cream = Mat(c, Greybox.Hex(0xe8dcc0), 0.2f);
            Facade(c, W(0, 7, -40), new Vector3(60f, 14f, 12f), cream, Glow(c, new Color(2.2f, 1.6f, 0.8f)), 3, 0f);
            Greybox.Box("PavilionRoof", c.Venue, Mat(c, Greybox.Hex(0x8a3a2a), 0.2f), W(0, 14.6, -40), new Vector3(62f, 1.2f, 14f), false);
            Color[] neon = { new Color(2.4f, 1.6f, 0.6f), new Color(1.8f, 2.0f, 2.4f), new Color(2.4f, 0.6f, 1.4f) };
            Skyline(c, 80, 291, 220f, 600f, -180f, 180f, 40f, 220f, neon, -2f);
            CityGlow(c, 640f, 200, 293, -180f, 180f);
            Seats(c, Greybox.Hex(0x1f9a9a));
            SideAndBack(c, 8, 11, 26f, 2.4f);
            Stand(c, "StandFront", 6, i => W(0, 0, -6.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 10f, 295, SteepTread);
            // marigold garlands along the stand fronts, string lights over the lawn
            var marigold = Glow(c, new Color(2.6f, 1.2f, 0.15f));
            var garland = new MeshMerge();
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 60; k++)
                {
                    float t = (k % 6) / 6f;
                    garland.Box(W(s * (HW + 2.3), 0.9 - Mathf.Sin(t * Mathf.PI) * 0.35f, -4 + k * 0.45f), Vector3.one * 0.14f, Quaternion.identity);
                }
            garland.Build("Marigolds", c.Venue, marigold);
            StringLights(c, -20f, 20f, -10f, L + 20f, 5f, 9.5f, new Color(2.6f, 1.9f, 0.9f));
            FloodTowers(c, 17f, -12f, L + 16f, 18f);
            foreach (int s in new[] { -1, 1 }) Screen(c, W(s * (HW + 5.6), 5.2, -2.4), 5.6f, 3.2f);
            FlagRow(c, ClubVenues.Flag("IND"), W(-12, 8.5, -9), new Vector3(4f, 0, 0), 7, new Vector3(1.4f, 0.4f, 0.05f));
            SkyBeams(c, new Color(1.6f, 0.9f, 0.3f, 0.22f), 8, 297, 14f, 22f, -12f, L + 14f);
            CourtBoards(c, v, "INDIAN OPEN - BOMBAY GYMKHANA", Color.white, Greybox.Hex(0x0a2a6a));
            Ambient(new Color(0.26f, 0.24f, 0.30f), new Color(0.30f, 0.22f, 0.18f), new Color(0.10f, 0.08f, 0.06f));
            RenderSettings.fog = false;
        }

        /// <summary>#39 Bermuda Open: the island club - the white court with blue lines, the gallery packed, and through the
        /// glass wall the turquoise sea at sunset, pastel cottages with white stepped roofs, palms.</summary>
        static void Bermuda(Ctx c, TourVenue v)
        {
            ClubEvent(c, v, 0, 8.4f, Greybox.Hex(0xf2f0ea), Greybox.Hex(0x1d4f8a), 8, true, out float half, out float back);
            Sky(c, true, 700f);
            Water(c, new Color(0.10f, 0.45f, 0.50f), W(-260, -2, L / 2), new Vector2(400f, 900f));
            Greybox.Box("Shore", c.Venue, Mat(c, Greybox.Hex(0xe8dcc4), 0.1f), W(-30, -0.5, L / 2), new Vector3(40f, 0.4f, 200f), false);
            var sun = Greybox.Primitive(PrimitiveType.Sphere, "Sun", c.Venue, Glow(c, new Color(3f, 1.6f, 0.6f)), false);
            sun.transform.localPosition = W(-600, 30, -80); sun.transform.localScale = Vector3.one * 60f;
            var path = c.Palette.Clear(new Color(2f, 1.2f, 0.5f, 0.4f)); path.mainTexture = ProcTex.SoftSpot();
            var q = Greybox.Primitive(PrimitiveType.Quad, "SunPath", c.Venue, path, false);
            q.transform.localPosition = W(-340, -1.8, -40); q.transform.localRotation = Quaternion.Euler(90f, 90f, 0); q.transform.localScale = new Vector3(600f, 40f, 1f);
            // the cottages: pastel walls, white stepped roofs
            Color[] pastel = { Greybox.Hex(0xf2b8c8), Greybox.Hex(0xf6e08a), Greybox.Hex(0x9ad0e8), Greybox.Hex(0xb8e0b0), Greybox.Hex(0xf2c49a) };
            var roofWhite = Mat(c, Greybox.Hex(0xf6f6f4), 0.3f);
            var rng = new System.Random(301);
            for (int k = 0; k < 9; k++)
            {
                Vector3 at = W(-26 - rng.NextDouble() * 18, 0, -30 + k * 9 + rng.NextDouble() * 3);
                float w = 6f + (float)rng.NextDouble() * 3f, h = 3.6f + (float)rng.NextDouble() * 1.4f;
                Greybox.Box("Cottage", c.Venue, Mat(c, pastel[k % pastel.Length], 0.2f), at + Vector3.up * h / 2, new Vector3(w, h, 6f), false);
                for (int st = 0; st < 4; st++)
                    Greybox.Box("SteppedRoof", c.Venue, roofWhite, at + Vector3.up * (h + 0.25f + st * 0.5f), new Vector3(w + 0.4f - st * w * 0.22f, 0.5f, 6.4f - st * 1.4f), false);
            }
            var trunk = Mat(c, Greybox.Hex(0x5a4632), 0.1f); var leaf = Mat(c, Greybox.Hex(0x1f5a2a), 0.1f);
            for (float z = -16f; z < L + 22f; z += 6f) Palm(c, W(-half - 4 - (z % 3), -0.3, z), 7.5f, trunk, leaf);
            FlagRow(c, new[] { Greybox.Hex(0xc8352e), Greybox.Hex(0xc8352e), Greybox.Hex(0x1d2f6a) }, W(half - 0.14, 6.4, L + 1.5), new Vector3(0, 0, 2.2f), 4, new Vector3(0.03f, 0.5f, 1.6f));
            Banners(c, W(half - 0.15, 4.2, 0.6), new Vector3(0, 0, 2.2f), 8, new Vector2(1.4f, 1.8f), new[] { Greybox.Hex(0x3ad8d0), Greybox.Hex(0xf2b8c8) }, 90f);
            if (c.High) Point(c, "Sunset", W(-half - 2, 4, L / 2), new Color(1f, 0.6f, 0.35f), 30f, 2.4f);
        }
    }
}
