// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Clubs.cs
// Batch 5 (2026-10-26): club and mid-tier courts, after the user's photos
// of real clubs - a row of courts side by side, white plaster, wooden
// floors, glass backs in steel frames; a walkway behind the glass with
// benches and kit bags; an open steel roof with long LED battens; banners
// and flags. Bright and even (the clubs are not the pro night look).
//
//   warehouse   Warehouse Club: a steel shed, a row of four, a green
//               walkway, blue lines
//   leisure     Leisure Centre: a row of three, red lines, a grey
//               corridor with a yellow line, chairs, trusses overhead
// Every hall runs 14 m back from the glass and 9 m up, so each match
// camera (TV: 12.5 m back) is inside it.
//   county      County Open: the warehouse on event day - pull-out
//               bleachers behind the glass, the event's banners, a
//               scoreboard over the front wall
// ===========================================================

using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static partial class VenueView
    {
        const float Pitch = 2f * (float)HALF_W + 0.36f;                     // court to court across the row

        /// <summary>A club's lines (and its tin) in the theme's colour.</summary>
        static Material LineMat(Ctx c) { return c.Palette.UnlitColor(Courts.Current.Lines); }

        /// <summary>A neighbouring court in the row, centred at x = cx: floor, plaster walls, its lines, the glass back.</summary>
        static void NeighbourCourt(Ctx c, float cx, Material plaster, Material floor, Material glass, Material line)
        {
            var root = new GameObject("Neighbour").transform; root.SetParent(c.Venue, false);
            Greybox.TexturedBox("NFloor", root, floor, W(cx, -0.05, L / 2), new Vector3(2 * HW, 0.1f, L), new Vector2(2 * HW, L), false);
            Greybox.Box("NFront", root, plaster, W(cx, CH / 2, -0.06), new Vector3(2 * HW, CH, 0.12f), false);
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("NSide", root, plaster, W(cx + s * (HW + 0.06), CH / 2, L / 2), new Vector3(0.12f, CH, L), false);
            // the front wall's lines and the floor's
            Greybox.Box("NOut", root, line, W(cx, FRONT_WALL_OUT_H, 0.005), new Vector3(2 * HW, 0.05f, 0.01f), false);
            Greybox.Box("NService", root, line, W(cx, SERVICE_LINE_H, 0.005), new Vector3(2 * HW, 0.05f, 0.01f), false);
            Greybox.Box("NTin", root, line, W(cx, TIN_H / 2, 0.02), new Vector3(2 * HW, (float)TIN_H, 0.04f), false);
            float shortZ = L - (float)SHORT_LINE_Z;
            Greybox.Box("NShort", root, line, W(cx, 0.003, shortZ), new Vector3(2 * HW, 0.006f, 0.05f), false);
            Greybox.Box("NHalf", root, line, W(cx, 0.003, (shortZ + L) / 2), new Vector3(0.05f, 0.006f, L - shortZ), false);
            foreach (int s in new[] { -1, 1 })
            {
                Greybox.Box("NBoxSide", root, line, W(cx + s * (HW - SERVICE_BOX), 0.003, shortZ + SERVICE_BOX / 2), new Vector3(0.05f, 0.006f, (float)SERVICE_BOX), false);
                Greybox.Box("NBoxBack", root, line, W(cx + s * (HW - SERVICE_BOX / 2), 0.003, shortZ + SERVICE_BOX), new Vector3((float)SERVICE_BOX, 0.006f, 0.05f), false);
            }
            // the glass back in its frame
            Greybox.Box("NGlass", root, glass, W(cx, BACK_WALL_OUT_H / 2, L + 0.01), new Vector3(2 * HW, (float)BACK_WALL_OUT_H, 0.02f), false);
            for (int k = -2; k <= 2; k++)
                Greybox.Box("NPost", root, c.Steel, W(cx + k * HW / 2, BACK_WALL_OUT_H / 2 + 0.2, L + 0.04), new Vector3(0.06f, (float)BACK_WALL_OUT_H + 0.4f, 0.06f), false);
            Greybox.Box("NRail", root, c.Steel, W(cx, BACK_WALL_OUT_H + 0.4, L + 0.04), new Vector3(2 * HW, 0.06f, 0.06f), false);
        }

        /// <summary>The row: n neighbours each side of the court in play, the caps on the walls between.</summary>
        static void CourtRow(Ctx c, int each, Material plaster, Material floor, Material line)
        {
            var glass = c.Palette.Clear(new Color(0.75f, 0.85f, 0.9f, 0.18f));
            for (int k = 1; k <= each; k++)
                foreach (int s in new[] { -1, 1 }) NeighbourCourt(c, s * k * Pitch, plaster, floor, glass, line);
            for (int k = 0; k <= each; k++)
                foreach (int s in new[] { -1, 1 })
                    Greybox.Box("WallCap", c.Venue, plaster, W(s * (HW + 0.18 + k * Pitch), CH + 0.02, L / 2), new Vector3(0.36f, 0.04f, L + 0.1f), false);
            // our own court's glass back: the frame round it, like its neighbours'
            for (int k = -2; k <= 2; k++)
                Greybox.Box("Post", c.Venue, c.Steel, W(k * HW / 2, BACK_WALL_OUT_H / 2 + 0.2, L + 0.04), new Vector3(0.06f, (float)BACK_WALL_OUT_H + 0.4f, 0.06f), false);
            Greybox.Box("Rail", c.Venue, c.Steel, W(0, BACK_WALL_OUT_H + 0.4, L + 0.04), new Vector3(2 * HW, 0.06f, 0.06f), false);
        }

        /// <summary>A long LED batten (the roof's lights): unlit, bright.</summary>
        static void Batten(Ctx c, Material lamp, Vector3 from, Vector3 to)
        {
            Vector3 mid = (from + to) / 2, d = to - from;
            var g = Greybox.Box("Batten", c.Venue, lamp, mid, new Vector3(Mathf.Max(0.12f, Mathf.Abs(d.x)), 0.06f, Mathf.Max(0.12f, Mathf.Abs(d.z))), false);
        }

        /// <summary>Kit bags, water bottles and rackets along a bench (the clubs' clutter).</summary>
        static void Clutter(Ctx c, Vector3 benchCentre, float length, int seed)
        {
            var rng = new System.Random(seed);
            Color[] bags = { Greybox.Hex(0x1d1f26), Greybox.Hex(0x2b4a8a), Greybox.Hex(0x8a2b2b), Greybox.Hex(0x3a3d44), Greybox.Hex(0xd8d8d0) };
            int n = 3 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                var m = c.Palette.LitColor(bags[rng.Next(bags.Length)]);
                float x = (float)(rng.NextDouble() - 0.5) * length;
                bool onFloor = rng.NextDouble() < 0.4;
                Vector3 at = benchCentre + new Vector3(x, onFloor ? -0.27f : 0.12f, onFloor ? 0.45f : 0f);
                Greybox.Box("Bag", c.Venue, m, at, new Vector3(0.7f, 0.28f, 0.3f), false);
            }
            var bottle = c.Palette.LitColor(Greybox.Hex(0xd84a7a));
            Greybox.Box("Bottle", c.Venue, bottle, benchCentre + new Vector3((float)(rng.NextDouble() - 0.5) * length, 0.25f, 0.05f), new Vector3(0.07f, 0.22f, 0.07f), false);
        }

        static void ClubLight(Ctx c, float roofY)
        {
            // bright, even and cool - the battens; the key light stands for them on the court
            Ambient(new Color(0.66f, 0.68f, 0.70f), new Color(0.58f, 0.58f, 0.58f), new Color(0.36f, 0.35f, 0.33f));
            RenderSettings.fog = false;
            if (c.High)
                for (int k = 0; k < 3; k++)
                    foreach (int s in new[] { -1, 1 }) Spot(c, W(s * 1.6, roofY - 0.3, 1.6 + k * 3.2), 7f, 110f, roofY + 3f);
        }

        // =====================================================================================

        /// <summary>The Warehouse Club: a steel shed, a row of four courts, the green walkway behind the glass.</summary>
        static void Warehouse(Ctx c) { Shed(c, 1, false); }

        /// <summary>The County Open: the shed on event day.</summary>
        static void County(Ctx c) { Shed(c, 1, true); }

        static void Shed(Ctx c, int each, bool eventDay)
        {
            var plaster = c.Palette.LitColor(Greybox.Hex(0xf1f0ec)); plaster.SetFloat("_Smoothness", 0.08f);
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.4f);
            var green = c.Palette.LitColor(Greybox.Hex(0x2f8a4a)); green.SetFloat("_Smoothness", 0.25f);
            var shedWall = c.Palette.LitColor(Greybox.Hex(0xd9dadc)); shedWall.SetFloat("_Smoothness", 0.2f);
            var roof = c.Palette.LitColor(Color.white); roof.mainTexture = ProcTex.Corrugated(); roof.SetFloat("_Smoothness", 0.35f);
            var timber = c.Palette.LitColor(Greybox.Hex(0xb88a5a)); timber.SetFloat("_Smoothness", 0.3f);
            var lamp = c.Palette.UnlitColor(c.High ? new Color(2.4f, 2.4f, 2.45f) : new Color(1.4f, 1.4f, 1.42f));
            var line = LineMat(c);
            const float roofY = 9.2f, walk = 4.2f;
            float halfShed = HW + each * Pitch + 3.2f, back = L + 14f;                  // deep enough for every match camera (TV: 12.5 m back)

            CourtRow(c, each, plaster, wood, line);
            // the shed: floor, walls, the corrugated roof on its trusses, the battens
            Greybox.Box("ShedFloor", c.Venue, c.Palette.LitColor(Greybox.Hex(0x9a9c9e)), W(0, -0.08, L / 2 + 2), new Vector3(2 * halfShed, 0.1f, L + 30f));
            Greybox.Box("Walkway", c.Venue, green, W(0, -0.02, L + walk / 2), new Vector3(2 * halfShed, 0.04f, walk), false);
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("ShedWall", c.Venue, shedWall, W(s * halfShed, roofY / 2, L / 2 + 2), new Vector3(0.2f, roofY, L + 30f), false);
            Greybox.Box("ShedBack", c.Venue, shedWall, W(0, roofY / 2, back + 0.1), new Vector3(2 * halfShed, roofY, 0.2f), false);
            Greybox.Box("ShedFront", c.Venue, shedWall, W(0, roofY / 2, -1.2), new Vector3(2 * halfShed, roofY, 0.2f), false);
            Greybox.TexturedBox("Roof", c.Venue, roof, W(0, roofY + 0.6, L / 2 + 2), new Vector3(2 * halfShed, 0.08f, L + 30f), new Vector2(2 * halfShed / 2.5f, 1f), false);
            var rig = new MeshMerge();
            for (float z = -1f; z <= back; z += 3.2f)
            {
                rig.Beam(W(-halfShed, roofY, z), W(halfShed, roofY, z), 0.16f, 0.3f);                          // the tie
                rig.Beam(W(-halfShed, roofY, z), W(0, roofY + 0.55, z), 0.12f, 0.2f);                         // the rafters
                rig.Beam(W(halfShed, roofY, z), W(0, roofY + 0.55, z), 0.12f, 0.2f);
            }
            for (float x = -halfShed + 2f; x < halfShed; x += 4f) rig.Beam(W(x, roofY - 0.1, -1f), W(x, roofY - 0.1, back), 0.1f, 0.14f);   // purlins
            rig.Build("Trusses", c.Venue, c.Steel);
            for (int k = -each; k <= each; k++)
                foreach (float off in new[] { -1.2f, 1.2f })
                    Batten(c, lamp, W(k * Pitch + off, roofY - 0.2, 0.3), W(k * Pitch + off, roofY - 0.2, L - 0.3));
            Batten(c, lamp, W(-halfShed + 1, roofY - 0.2, L + walk / 2), W(halfShed - 1, roofY - 0.2, L + walk / 2));

            // the walkway: benches along the glass, kit bags, a few people sitting out (watching, waiting for a court)
            for (int k = -each; k <= each; k++)
            {
                if (eventDay && k == 0) continue;                                         // the bleachers take our bench's place
                Vector3 bench = W(k * Pitch, 0.42, L + 1.25);
                Greybox.Box("Bench", c.Venue, timber, bench, new Vector3(2 * HW - 0.6f, 0.06f, 0.42f), false);
                foreach (int s in new[] { -1, 1 }) Greybox.Box("BenchLeg", c.Venue, c.Steel, bench + new Vector3(s * (HW - 0.6f), -0.21f, 0), new Vector3(0.06f, 0.42f, 0.36f), false);
                Clutter(c, bench, 2 * HW - 1.2f, 31 + k);
            }
            if (!eventDay)
                foreach (int s in new[] { -1, 1 })
                    Stand(c, s < 0 ? "SittingOutLeft" : "SittingOutRight", 1, i => W(s * (Pitch - 1.2), 0, L + 1.5), new Vector3(1, 0, 0), Vector3.back, 1.6f, 21 + s, FlatTread);

            // the club: banners on the back wall, its flags, a roller door
            var bannerA = LedMat(c.Palette, "clubA", "RE:SQUASHSTRIKE CLUB  •  MEMBERS' LADDER  •", Greybox.Hex(0x1d1f26), Greybox.Hex(0xf2d23a), 0.9f);
            var bannerB = LedMat(c.Palette, "clubB", "THE STRONGEST AREN'T ALWAYS THE ONES WHO SURVIVE  •", Color.white, Greybox.Hex(0x2f8a4a), 0.9f);
            Greybox.TexturedBox("Banner", c.Venue, bannerA, W(-halfShed / 2, 5.2, back - 0.02), new Vector3(halfShed * 0.8f, 1.1f, 0.04f), new Vector2(halfShed * 0.8f, 1.1f), false);
            Greybox.TexturedBox("Banner", c.Venue, bannerB, W(halfShed / 2, 5.2, back - 0.02), new Vector3(halfShed * 0.8f, 1.1f, 0.04f), new Vector2(halfShed * 0.8f, 1.1f), false);
            Color[] flagCols = { Greybox.Hex(0x2f8a4a), Greybox.Hex(0xf2d23a), Greybox.Hex(0xc8352e), Greybox.Hex(0x2b4a8a) };
            for (int i = 0; i < 4; i++)
                Greybox.Box("Flag", c.Venue, c.Palette.LitColor(flagCols[i]), W(halfShed - 0.12, 4.8 - (i % 2) * 0.3, L + 0.5 + i * 1.3), new Vector3(0.03f, 2.2f, 1.1f), false);
            Greybox.Box("RollerDoor", c.Venue, c.Palette.LitColor(Greybox.Hex(0x6e7076)), W(-halfShed + 0.12, 2.2, L + walk + 3.2), new Vector3(0.05f, 4.4f, 3.2f), false);
            Batten(c, lamp, W(-halfShed + 1, roofY - 0.2, L + 9.5), W(halfShed - 1, roofY - 0.2, L + 9.5));

            if (eventDay)
            {
                // pull-out bleachers behind our glass, the event's banners along the walkway, the scoreboard over the front wall
                // (two blocks either side of the camera's line: the match is filmed between them)
                foreach (int s in new[] { -1, 1 })
                    Stand(c, s < 0 ? "BleachersLeft" : "BleachersRight", 5, i => W(s * 3.5, 0, L + 1.0 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.8f, 51 + s, SteepTread);
                var ev = LedMat(c.Palette, "county", "COUNTY OPEN  •  RE:SQUASHSTRIKE TOUR  •", Color.white, Greybox.Hex(0x1d5fb8), 1.1f);
                foreach (int s in new[] { -1, 1 })
                    Greybox.TexturedBox("EventBoard", c.Venue, ev, W(s * (HW + 0.6), 0.45, L + 0.6), new Vector3(0.04f, 0.6f, 2.4f), new Vector2(2.4f, 0.6f), false);
                BigScreen(c, CH + 1.6f);
            }
            ClubLight(c, roofY);
        }

        /// <summary>The Leisure Centre: a row of three red-lined courts, the grey corridor behind with its yellow line, chairs.</summary>
        static void Leisure(Ctx c)
        {
            var plaster = c.Palette.LitColor(Greybox.Hex(0xf4f3f0)); plaster.SetFloat("_Smoothness", 0.1f);
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.45f);
            var carpet = c.Palette.LitColor(Greybox.Hex(0x8c9196));
            var yellow = c.Palette.UnlitColor(Greybox.Hex(0xe8c93a));
            var ceilingMat = c.Palette.LitColor(Greybox.Hex(0xe2e3e5));
            var lamp = c.Palette.UnlitColor(c.High ? new Color(2.3f, 2.3f, 2.3f) : new Color(1.35f, 1.35f, 1.35f));
            var chairMat = c.Palette.LitColor(Greybox.Hex(0xcfd2d6));
            var line = LineMat(c);
            const int each = 1;
            const float roofY = 9.0f, corridor = 5.0f;
            float half = HW + each * Pitch + 0.4f, back = L + 14f;                     // the corridor, then the viewing area (the cameras sit in it)

            CourtRow(c, each, plaster, wood, line);
            Greybox.Box("Corridor", c.Venue, carpet, W(0, -0.03, (L + back) / 2), new Vector3(2 * half, 0.06f, back - L));
            Greybox.Box("YellowLine", c.Venue, yellow, W(0, 0.005, L + 0.75), new Vector3(2 * half, 0.004f, 0.06f), false);
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("HallWall", c.Venue, plaster, W(s * half, roofY / 2, L / 2 + 2), new Vector3(0.2f, roofY, L + 8f), false);
            Greybox.Box("HallBack", c.Venue, plaster, W(0, roofY / 2, back), new Vector3(2 * half, roofY, 0.2f), false);
            Greybox.Box("HallFront", c.Venue, plaster, W(0, roofY / 2, -0.9), new Vector3(2 * half, roofY, 0.2f), false);
            Greybox.Box("Ceiling", c.Venue, ceilingMat, W(0, roofY + 0.4, L / 2 + 2), new Vector3(2 * half, 0.1f, L + 8f), false);
            var rig = new MeshMerge();
            for (float z = -0.5f; z <= back; z += 2.8f) rig.Beam(W(-half, roofY - 0.1, z), W(half, roofY - 0.1, z), 0.18f, 0.5f);
            rig.Build("Trusses", c.Venue, c.Steel);
            for (int k = -each; k <= each; k++)
                for (int r = 0; r < 3; r++)
                    Greybox.Box("Panel", c.Venue, lamp, W(k * Pitch, roofY - 0.36, 1.4 + r * 3.4), new Vector3(1.2f, 0.04f, 0.6f), false);
            for (int r = 0; r < 4; r++)
                for (float z = L + corridor / 2; z < back - 1; z += 4f)
                    Greybox.Box("Panel", c.Venue, lamp, W(-half + 2 + r * (2 * half - 4) / 3f, roofY - 0.36, z), new Vector3(1.2f, 0.04f, 0.6f), false);

            // chairs along the corridor, a table, a few people waiting; a TV on the back wall; numbers over the doors
            var rng = new System.Random(41);
            for (int i = 0; i < 9; i++)
            {
                Vector3 seat = W(-half + 1.2 + i * (2 * half - 2.4) / 8f + rng.NextDouble() * 0.3, 0.45, L + corridor - 1.0 - rng.NextDouble() * 1.4);
                Greybox.Box("Chair", c.Venue, chairMat, seat, new Vector3(0.46f, 0.06f, 0.46f), false);
                Greybox.Box("ChairBack", c.Venue, chairMat, seat + new Vector3(0, 0.28f, 0.2f), new Vector3(0.46f, 0.5f, 0.05f), false);
            }
            Greybox.Box("Table", c.Venue, c.Palette.LitColor(Greybox.Hex(0x2a2c31)), W(half - 2.2, 0.38, L + corridor - 1.4), new Vector3(1.2f, 0.06f, 0.7f), false);
            Stand(c, "Waiting", 1, i => W(-Pitch, 0, L + 2.6), new Vector3(1, 0, 0), Vector3.back, 2.4f, 43, FlatTread);
            var tv = c.Palette.UnlitColor(new Color(1.05f, 1.05f, 1.05f)); tv.mainTexture = ProcTex.Screen();
            Greybox.TexturedBox("TV", c.Venue, tv, W(0, 3.4, back - 0.12), new Vector3(1.8f, 1.0f, 0.04f), new Vector2(1.8f, 1.0f), false);
            ClubLight(c, roofY);
        }
    }
}
