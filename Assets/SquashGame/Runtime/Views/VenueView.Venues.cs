// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Venues.cs
// Courts per tournament, part 1 (2026-10-27): the Challenger venues
// (ClubVenues) - three club layouts dressed per venue. Built from the
// batch 5 club pieces (the court row, battens, benches and bags, the
// club light), every hall 14 m back from the glass and 9 m up so each
// match camera is inside it (TV: 12.5 m back).
//
//   Shed / Corridor   a row of plaster courts in a hall or a members'
//                     club; the venue's name and motto on the back wall,
//                     its country's flags, windows on a sunny city
//   Centre            the all-glass show court in a sports hall: stands
//                     down both sides, bleachers behind the glass (the
//                     camera's lane between them), the scoreboard
// ===========================================================

using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static partial class VenueView
    {
        static void Venue(Ctx c, ClubVenue v)
        {
            if (v.Kind == ClubKind.Centre) Centre(c, v);
            else ClubHall(c, v);
        }

        static Material Lit(Ctx c, Color col, float smooth) { var m = c.Palette.LitColor(col); m.SetFloat("_Smoothness", smooth); return m; }

        /// <summary>A row of courts in a hall (Shed) or a members' club (Corridor).</summary>
        static void ClubHall(Ctx c, ClubVenue v)
        {
            bool shed = v.Kind == ClubKind.Shed;
            var plaster = Lit(c, v.Plaster, 0.08f);
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.4f);
            var hallWall = Lit(c, v.Hall, 0.2f);
            var timber = Lit(c, Greybox.Hex(0xb88a5a), 0.3f);
            var beams = v.Timber ? Lit(c, Greybox.Hex(0x7a5232), 0.25f) : c.Steel;
            var lamp = c.Palette.UnlitColor(c.High ? new Color(2.4f, 2.4f, 2.45f) : new Color(1.4f, 1.4f, 1.42f));
            var line = LineMat(c);
            int each = Mathf.Clamp(v.Neighbours, 0, 2);
            const float roofY = 9.0f, walk = 4.2f;
            float half = HW + each * Pitch + (shed ? 3.2f : 0.6f), back = L + 14f;

            CourtRow(c, each, plaster, wood, line);
            // the floor behind the glass: a walkway (shed) or the club's carpet
            var floorBehind = shed ? Lit(c, Greybox.Hex(0x9a9c9e), 0.2f) : Lit(c, Color.Lerp(v.Accent, Color.black, 0.45f), 0.05f);
            Greybox.Box("HallFloor", c.Venue, floorBehind, W(0, -0.08, L / 2 + 2), new Vector3(2 * half, 0.1f, L + 30f));
            if (shed) Greybox.Box("Walkway", c.Venue, Lit(c, v.Accent, 0.25f), W(0, -0.02, L + walk / 2), new Vector3(2 * half, 0.04f, walk), false);
            else Greybox.Box("Line", c.Venue, c.Palette.UnlitColor(v.Accent2), W(0, 0.005, L + 0.75), new Vector3(2 * half, 0.004f, 0.06f), false);
            // the hall
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("HallWall", c.Venue, hallWall, W(s * half, roofY / 2, L / 2 + 2), new Vector3(0.2f, roofY, L + 30f), false);
            Greybox.Box("HallBack", c.Venue, hallWall, W(0, roofY / 2, back + 0.1), new Vector3(2 * half, roofY, 0.2f), false);
            Greybox.Box("HallFront", c.Venue, hallWall, W(0, roofY / 2, -1.2), new Vector3(2 * half, roofY, 0.2f), false);
            var rig = new MeshMerge();
            if (shed)
            {
                var roof = c.Palette.LitColor(Color.white); roof.mainTexture = ProcTex.Corrugated(); roof.SetFloat("_Smoothness", 0.35f);
                Greybox.TexturedBox("Roof", c.Venue, roof, W(0, roofY + 0.6, L / 2 + 2), new Vector3(2 * half, 0.08f, L + 30f), new Vector2(2 * half / 2.5f, 1f), false);
                for (float z = -1f; z <= back; z += 3.2f)
                {
                    rig.Beam(W(-half, roofY, z), W(half, roofY, z), 0.16f, 0.3f);
                    rig.Beam(W(-half, roofY, z), W(0, roofY + 0.55, z), 0.12f, 0.2f);
                    rig.Beam(W(half, roofY, z), W(0, roofY + 0.55, z), 0.12f, 0.2f);
                }
            }
            else
            {
                Greybox.Box("Ceiling", c.Venue, Lit(c, Greybox.Hex(0xe2e3e5), 0.1f), W(0, roofY + 0.4, L / 2 + 2), new Vector3(2 * half, 0.1f, L + 30f), false);
                for (float z = -0.5f; z <= back; z += 2.8f) rig.Beam(W(-half, roofY - 0.1, z), W(half, roofY - 0.1, z), 0.18f, 0.5f);
            }
            rig.Build("Beams", c.Venue, beams);
            for (int k = -each; k <= each; k++)
                foreach (float off in new[] { -1.2f, 1.2f })
                    Batten(c, lamp, W(k * Pitch + off, roofY - 0.2, 0.3), W(k * Pitch + off, roofY - 0.2, L - 0.3));
            Batten(c, lamp, W(-half + 1, roofY - 0.2, L + walk / 2), W(half - 1, roofY - 0.2, L + walk / 2));
            Batten(c, lamp, W(-half + 1, roofY - 0.2, L + 9.5), W(half - 1, roofY - 0.2, L + 9.5));
            if (v.Windows) Windows(c, v, half, back, roofY);

            // behind the glass: bleachers on event day, else benches and bags (a shed) or chairs (a club)
            if (v.Bleachers)
            {
                foreach (int s in new[] { -1, 1 })
                    Stand(c, s < 0 ? "BleachersLeft" : "BleachersRight", 5, i => W(s * 3.5, 0, L + 1.0 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.8f, 51 + s, SteepTread);
            }
            else if (shed)
            {
                for (int k = -each; k <= each; k++)
                {
                    Vector3 bench = W(k * Pitch, 0.42, L + 1.25);
                    Greybox.Box("Bench", c.Venue, timber, bench, new Vector3(2 * HW - 0.6f, 0.06f, 0.42f), false);
                    foreach (int s in new[] { -1, 1 }) Greybox.Box("BenchLeg", c.Venue, c.Steel, bench + new Vector3(s * (HW - 0.6f), -0.21f, 0), new Vector3(0.06f, 0.42f, 0.36f), false);
                    Clutter(c, bench, 2 * HW - 1.2f, v.Key.GetHashCode() + k);
                }
                Stand(c, "Watching", 1, i => W(-Pitch * Mathf.Max(1, each) + 1.2, 0, L + 2.4), new Vector3(1, 0, 0), Vector3.back, 1.6f, 21, FlatTread);
            }
            else
            {
                var chair = Lit(c, Color.Lerp(v.Accent, Color.white, 0.5f), 0.2f);
                var rng = new System.Random(v.Key.GetHashCode());
                for (int i = 0; i < 9; i++)
                {
                    Vector3 seat = W(-half + 1.2 + i * (2 * half - 2.4) / 8f + rng.NextDouble() * 0.3, 0.45, L + 3.6 - rng.NextDouble() * 1.4);
                    Greybox.Box("Chair", c.Venue, chair, seat, new Vector3(0.46f, 0.06f, 0.46f), false);
                    Greybox.Box("ChairBack", c.Venue, chair, seat + new Vector3(0, 0.28f, 0.2f), new Vector3(0.46f, 0.5f, 0.05f), false);
                }
                Stand(c, "Watching", 2, i => W(-Pitch * Mathf.Max(1, each) + 1.0, 0, L + 1.4 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.0f, 43, FlatTread);
            }
            Dress(c, v, half, back, roofY, v.Timber || !shed);
            ClubLight(c, roofY);
        }

        /// <summary>A national or event centre: the glass show court in a sports hall, stands down both sides, bleachers behind.</summary>
        static void Centre(Ctx c, ClubVenue v)
        {
            const float roofY = 12f;
            float half = HW + 2.2f + 7 * ROW_DEPTH + 1.2f, back = L + 16f, front = -8f;
            var hallWall = Lit(c, v.Hall, 0.2f);
            var court = Lit(c, Color.Lerp(v.Accent, Greybox.Hex(0x3a3d44), 0.55f), 0.45f);
            c.Blue = Lit(c, v.Accent, 0.6f);                                                // the glass court's frame in the venue's colour
            Greybox.Box("HallFloor", c.Venue, court, W(0, -0.08, (front + back) / 2), new Vector3(2 * half, 0.1f, back - front));
            GlassShell(c);
            GalleryMat(c, Color.Lerp(v.Accent, Color.black, 0.6f), v.Accent2);
            foreach (int s in new[] { -1, 1 })
                Greybox.Box("HallWall", c.Venue, hallWall, W(s * half, roofY / 2, (front + back) / 2), new Vector3(0.3f, roofY, back - front), false);
            Greybox.Box("HallBack", c.Venue, hallWall, W(0, roofY / 2, back), new Vector3(2 * half, roofY, 0.3f), false);
            Greybox.Box("HallFront", c.Venue, hallWall, W(0, roofY / 2, front), new Vector3(2 * half, roofY, 0.3f), false);
            Greybox.Box("Roof", c.Venue, Lit(c, Greybox.Hex(0xd8dadc), 0.1f), W(0, roofY + 0.8, (front + back) / 2), new Vector3(2 * half, 0.1f, back - front), false);
            var rig = new MeshMerge();
            for (float z = front + 1f; z <= back; z += 4f) rig.Beam(W(-half, roofY, z), W(half, roofY, z), 0.3f, 0.6f);
            rig.Build("Trusses", c.Venue, c.Steel);
            var lamp = c.Palette.UnlitColor(c.High ? new Color(2.6f, 2.6f, 2.6f) : new Color(1.5f, 1.5f, 1.5f));
            for (float z = front + 3f; z < back; z += 4f)
                foreach (float x in new[] { -half * 0.6f, 0f, half * 0.6f })
                    Greybox.Box("Lamp", c.Venue, lamp, W(x, roofY - 0.3, z), new Vector3(1.4f, 0.06f, 0.5f), false);
            if (v.Windows) Windows(c, v, half, back, roofY);

            // the stands: down both sides, bleachers behind the glass (the camera's lane between them), the front-wall end
            int rows = v.Bleachers ? 7 : 4;
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "StandLeft" : "StandRight", rows, i => W(s * (HW + 2.2 + i * ROW_DEPTH), 0, L / 2 + 1f),
                    new Vector3(0, 0, 1), s < 0 ? Vector3.left : Vector3.right, L + 2f, s + 61, SteepTread);
            foreach (int s in new[] { -1, 1 })
                Stand(c, s < 0 ? "BleachersLeft" : "BleachersRight", v.Bleachers ? 5 : 3, i => W(s * 3.5, 0, L + 1.2 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.8f, 71 + s, SteepTread);
            // the event's boards round the court, the scoreboard over the front wall
            var ev = LedMat(c.Palette, "v_" + v.Key + "_ev", ClubVenues.Sign(v.Events[0] + "  -  " + v.City + "  -  "), Color.white, v.Accent, 1.1f);
            foreach (int s in new[] { -1, 1 })
                Greybox.TexturedBox("EventBoard", c.Venue, ev, W(s * (HW + 0.9), 0.45, L / 2), new Vector3(0.04f, 0.6f, L - 1f), new Vector2(L - 1f, 0.6f), false);
            BigScreen(c, CH + 1.6f);
            Dress(c, v, half, back, roofY, false);
            ClubLight(c, roofY);
        }

        /// <summary>A band of windows high on the side walls and the back: the city's daylight.</summary>
        static void Windows(Ctx c, ClubVenue v, float half, float back, float roofY)
        {
            var sky = c.Palette.UnlitColor(v.Sky * (c.High ? 1.3f : 1f));
            float y = roofY - 2.4f, h = 1.6f;
            foreach (int s in new[] { -1, 1 })
                for (float z = 1f; z < back - 1f; z += 3.2f)
                {
                    Greybox.Box("Window", c.Venue, sky, W(s * (half - 0.12), y, z), new Vector3(0.04f, h, 2.6f), false);
                    Greybox.Box("Mullion", c.Venue, c.Steel, W(s * (half - 0.14), y, z), new Vector3(0.05f, h + 0.1f, 0.08f), false);
                }
            for (float x = -half + 1.5f; x < half - 1f; x += 3f)
                Greybox.Box("Window", c.Venue, sky, W(x, y, back - 0.12), new Vector3(2.4f, h, 0.04f), false);
        }

        /// <summary>The venue's own: its name and motto on the back wall, its country's flags, honours boards in a club.</summary>
        static void Dress(Ctx c, ClubVenue v, float half, float back, float roofY, bool honours)
        {
            // over the front wall, in the TV camera's view: the venue's board, its flags either side
            var top = LedMat(c.Palette, "v_" + v.Key + "_top", ClubVenues.Sign(v.Venue + "  -  "), Color.white, Color.Lerp(v.Accent, Color.black, 0.3f), 1.0f);
            float tz = v.Kind == ClubKind.Centre ? -7.8f : -1.08f, ty = v.Kind == ClubKind.Centre ? CH + 4.6f : CH + 1.5f;
            Greybox.TexturedBox("TopBoard", c.Venue, top, W(0, ty, tz), new Vector3(6.4f, 0.9f, 0.04f), new Vector2(6.4f, 0.9f), false);
            var fl = ClubVenues.Flag(v.Nat);
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 3; k++)
                    Greybox.Box("TopFlag", c.Venue, c.Palette.LitColor(fl[k]), W(s * 4.2 + (k - 1) * 0.5 * s, ty, tz + 0.02), new Vector3(0.5f, 0.9f, 0.03f), false);
            var a = LedMat(c.Palette, "v_" + v.Key + "_a", ClubVenues.Sign(v.Venue + "  -  " + v.City + "  -  "), Color.white, v.Accent, 0.95f);
            var ink = v.Accent2.grayscale < 0.25f ? Color.white : v.Accent2;                // a dark second colour: white letters
            var b = LedMat(c.Palette, "v_" + v.Key + "_b", ClubVenues.Sign(v.Motto + "  -  " + v.Events[0] + "  -  "), ink, Greybox.Hex(0x16181c), 0.9f);
            float w = Mathf.Min(half * 0.8f, 7f);
            Greybox.TexturedBox("Banner", c.Venue, a, W(-half / 2, 5.0, back - 0.05), new Vector3(w, 1.1f, 0.04f), new Vector2(w, 1.1f), false);
            Greybox.TexturedBox("Banner", c.Venue, b, W(half / 2, 5.0, back - 0.05), new Vector3(w, 1.1f, 0.04f), new Vector2(w, 1.1f), false);
            // the flags: three in the country's colours (a stripe each), hung on the right-hand wall
            var stripes = ClubVenues.Flag(v.Nat);
            for (int f = 0; f < 3; f++)
                for (int k = 0; k < 3; k++)
                    Greybox.Box("Flag", c.Venue, c.Palette.LitColor(stripes[k]), W(half - 0.14, 5.6 - k * 0.6, L + 1.0 + f * 1.6), new Vector3(0.03f, 0.6f, 1.2f), false);
            if (honours)
            {
                var board = Lit(c, Greybox.Hex(0x3a2514), 0.5f);
                var gold = c.Palette.UnlitColor(Greybox.Hex(0xd8b05a));
                foreach (float z in new[] { L + 5.0f, L + 8.0f })
                {
                    Greybox.Box("HonoursBoard", c.Venue, board, W(-half + 0.14, 2.8, z), new Vector3(0.04f, 1.6f, 2.2f), false);
                    for (int r = 0; r < 6; r++)
                        Greybox.Box("HonoursLine", c.Venue, gold, W(-half + 0.17, 3.4 - r * 0.22, z), new Vector3(0.01f, 0.03f, 1.6f), false);
                }
            }
        }
    }
}
