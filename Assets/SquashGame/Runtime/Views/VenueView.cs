// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.cs
// Everything around the court, for the court being played on
// (Courts.Current, 2026-10-24):
//
//   show      a wood-panelled wintergarden under an arched glass roof (the
//             Canary Wharf photo): close, dark stands, warm lamps, the big
//             screen with the match live
//   club      a club court: plaster walls to a white ceiling of strip
//             lights; a little gallery behind the glass
//   desert    glass on a stage in the dunes at night, floodlight towers,
//             the pyramids lit gold beyond the front wall, a starry sky
//   station   a great concourse: marble, stone walls, tall arched windows,
//             a turquoise vault of gold stars, chandeliers, the clock
//   rooftop   a roof deck at dusk, string lights, the city all round
//   arena     steep stands all round, LED ribbons, a cube of screens
//
// Built from CourtDimensions through SimToWorld like CourtView, so it
// always fits the court. Renders only: nothing here is read by the sim.
// Settings > Video > Venue: Off (the old greybox), Low (no extra lights,
// painted crowds, no grade), High (everything).
// ===========================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static partial class VenueView
    {
        /// <summary>Settings > Video > Venue: 0 off, 1 low, 2 high.</summary>
        public static int Detail = 2;
        /// <summary>Batch 1: the picture grade (post-processing) - High only.</summary>
        public static bool Post = true;

        const float ROW_DEPTH = 0.85f, ROW_RISE = 0.42f, CROWD_H = 0.95f;
        /// <summary>The first rows sit on the floor right by the glass; the rake starts behind them.</summary>
        const int FlatRows = 3;
        static float Tread(int i) { return i < FlatRows ? 0.04f : 0.5f + (i - FlatRows) * ROW_RISE; }
        static float FlatTread(int i) { return 0.04f; }
        static float SteepTread(int i) { return 0.4f + i * 0.55f; }
        const float HallX = 18.5f, HallFront = -24f, HallBackFromLength = 22f, HallWallH = 10f;
        static readonly Color Steel = Greybox.Hex(0x2a2e36);
        static readonly Color Dark = Greybox.Hex(0x12151b);
        static float L { get { return (float)LENGTH; } }
        static float HW { get { return (float)HALF_W; } }
        static float CH { get { return (float)CEILING_H; } }

        static Vector3 W(double x, double y, double z) { return SimToWorld.Pos(x, y, z); }

        /// <summary>What every court's builder shares.</summary>
        sealed class Ctx
        {
            public Transform Venue;
            public GreyboxPalette Palette;
            public bool High;
            public Material Steel, Blue, Dark, Seat;
            public Material[] Crowd;
            public CrowdBuilder.Palette People;
        }

        public static void Build(Transform root, GreyboxPalette palette)
        {
            if (Detail <= 0) return;
            var venue = new GameObject("Venue").transform;
            venue.SetParent(root, false);
            bool high = Detail >= 2;
            LiveCamera = null;

            var c = new Ctx { Venue = venue, Palette = palette, High = high };
            c.Steel = palette.LitColor(Steel); c.Steel.SetFloat("_Smoothness", 0.45f);
            c.Blue = palette.LitColor(Greybox.Hex(0x1d5fb8)); c.Blue.SetFloat("_Smoothness", 0.6f);
            c.Dark = palette.LitColor(Dark);
            c.Seat = palette.LitColor(Greybox.Hex(0x1c2029));
            // The people: 3D at High (CrowdBuilder), painted cards at Low.
            CrowdBuilder.ResetCount();
            c.People = high ? new CrowdBuilder.Palette(palette) : null;
            Motion = venue.gameObject.AddComponent<CrowdMotion>();
            c.Crowd = new Material[4];
            for (int i = 0; i < c.Crowd.Length; i++)
            {
                c.Crowd[i] = palette.LitColor(new Color(0.62f, 0.62f, 0.64f));
                c.Crowd[i].mainTexture = ProcTex.Crowd(i);
                c.Crowd[i].SetFloat("_Smoothness", 0f);
            }

            switch (Courts.Current.Id)
            {
                case "club": Club(c); break;
                case "warehouse": Warehouse(c); break;                          // batch 5: the clubs (VenueView.Clubs.cs)
                case "leisure": Leisure(c); break;
                case "county": County(c); break;
                case "desert": Desert(c); break;
                case "station": Station(c); break;
                case "rooftop": Rooftop(c); break;
                case "arena": Arena(c); break;
                default:
                    var v = Courts.Current.Id.StartsWith(Courts.VenuePrefix) ? ClubVenues.Find(Courts.Current.Id.Substring(Courts.VenuePrefix.Length)) : null;
                    if (v != null) Venue(c, v); else ShowCourt(c);                // courts per tournament (VenueView.Venues.cs)
                    break;
            }
            if (high && Post) Grade(venue);
            NightLook(c, root);
        }

        // =====================================================================================
        // batch 1 (2026-10-26): the pro venues' night look - after the user's photos (the Giza pyramids, the
        // Australian Open in Brisbane, the British Open in Birmingham): the hall near black, the glass court the
        // brightest thing in it, lit from the rig above; the crowd in silhouette in the spill; colour from the
        // screens, washes on the floor and searchlights. The court's key and fill light only the court (rendering
        // layer 1); the venue (layer 2) gets a dim, coloured spill. Cheap: no extra shadowed lights - the washes and
        // beams are see-through glowing cards.
        // =====================================================================================

        public const uint CourtLayer = 1u, VenueLayer = 2u;

        /// <summary>A court's night: spill on the venue (colour, intensity), the floor washes' colour (alpha 0: none),
        /// searchlights, and how far the ambient is turned down.</summary>
        struct Night { public Color Spill; public float SpillI; public Color Wash; public bool Beams; public float AmbientK; }

        static bool NightFor(string id, out Night n)
        {
            n = default;
            if (id.StartsWith(Courts.VenuePrefix)) return false;                // the Challengers' clubs and centres: bright and even
            switch (id)
            {
                case "club": case "warehouse": case "leisure": case "county": return false;     // a club stays a club: bright and even
                case "desert": n = new Night { Spill = new Color(0.55f, 0.62f, 0.9f), SpillI = 0.21f, Wash = new Color(0.2f, 0.45f, 1.4f, 0.22f), Beams = true, AmbientK = 0.7f }; break;
                case "arena": n = new Night { Spill = new Color(0.62f, 0.55f, 0.95f), SpillI = 0.19f, Wash = new Color(1.1f, 0.3f, 1.4f, 0.22f), AmbientK = 0.65f }; break;
                case "station": n = new Night { Spill = new Color(0.95f, 0.78f, 0.55f), SpillI = 0.23f, Wash = new Color(1.4f, 0.85f, 0.35f, 0.18f), AmbientK = 0.6f }; break;
                case "rooftop": n = new Night { Spill = new Color(0.6f, 0.58f, 0.9f), SpillI = 0.21f, Wash = new Color(0.4f, 0.5f, 1.4f, 0.2f), AmbientK = 0.75f }; break;
                default: n = new Night { Spill = new Color(0.6f, 0.68f, 1f), SpillI = 0.19f, Wash = new Color(0.2f, 0.5f, 1.4f, 0.2f), AmbientK = 0.6f }; break;   // the show court
            }
            return true;
        }

        static void NightLook(Ctx c, Transform root)
        {
            if (!NightFor(Courts.Current.Id, out var n)) return;
            // The court's own lights: the court only.
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional || l.name == "CourtSpot" || l.name == "TowerSpot") Layers(l, CourtLayer);
                else Layers(l, VenueLayer);                         // the hall's lamps and washes: the venue only
            }
            var key = root.Find("KeyLight");
            if (key != null) key.GetComponent<Light>().intensity *= 1.25f;             // the rig over the court: brighter than before
            // Everything away from the court is the venue.
            Vector3 a = W(-HW - 1.3, 0, -1.2), b = W(HW + 1.3, 0, L + 5.2);
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            foreach (var r in c.Venue.GetComponentsInChildren<Renderer>(true))
            {
                var p = r.bounds.center;
                bool court = p.x > x0 && p.x < x1 && p.z > z0 && p.z < z1 && p.y < 8f && r.bounds.size.x < 14f && r.bounds.size.z < 16f;   // (not a deck or a hall floor that runs under it)
                r.renderingLayerMask = court ? CourtLayer : VenueLayer;
            }
            var spill = new GameObject("VenueSpill").AddComponent<Light>();
            spill.transform.SetParent(c.Venue, false);
            spill.type = LightType.Directional;
            spill.color = n.Spill; spill.intensity = n.SpillI; spill.shadows = LightShadows.None;
            Layers(spill, VenueLayer);
            spill.transform.rotation = Quaternion.Euler(55f, 160f, 0f);
            // The court's glow on the nearest rows: they catch the light off the glass, the back rows fall into the dark.
            var glow = new GameObject("CourtGlow").AddComponent<Light>();
            glow.transform.SetParent(c.Venue, false);
            glow.type = LightType.Point; glow.shadows = LightShadows.None;
            glow.color = Color.Lerp(Color.white, n.Spill, 0.35f); glow.range = 26f; glow.intensity = 16f;
            Layers(glow, VenueLayer);
            glow.transform.localPosition = W(0, 4.5, L / 2);
            RenderSettings.ambientSkyColor *= n.AmbientK;
            RenderSettings.ambientEquatorColor *= n.AmbientK;
            RenderSettings.ambientGroundColor *= n.AmbientK;
            if (n.Wash.a > 0) Washes(c, n.Wash);
            if (n.Beams) Searchlights(c);
        }

        /// <summary>The rendering layers a light lights (URP reads its own light data, not Light.renderingLayerMask).</summary>
        static void Layers(Light l, uint mask)
        {
            l.renderingLayerMask = (int)mask;
            l.GetUniversalAdditionalLightData().renderingLayers = mask;
        }

        /// <summary>Coloured pools of light on the floor round the stage (Birmingham's purple wash).</summary>
        static void Washes(Ctx c, Color colour)
        {
            var mat = c.Palette.Clear(colour); mat.mainTexture = ProcTex.SoftSpot();
            var rng = new System.Random(17);
            foreach (int side in new[] { -1, 1 })
                for (int k = 0; k < 4; k++)
                {
                    float z = -0.5f + k * (float)(L + 3.5) / 3f, s = 2.4f + (float)rng.NextDouble() * 0.8f;
                    var q = Greybox.Primitive(PrimitiveType.Quad, "FloorWash", c.Venue, mat, false);
                    q.transform.localPosition = W(side * (HW + 1.35 + rng.NextDouble() * 0.3), 0.012, z);
                    q.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    q.transform.localScale = new Vector3(s, s * 1.4f, 1f);
                }
        }

        /// <summary>The Giza night: searchlights from behind the stands, crossing high over the court.</summary>
        static void Searchlights(Ctx c)
        {
            var mat = c.Palette.Clear(new Color(0.35f, 0.65f, 1.8f, 0.32f)); mat.mainTexture = ProcTex.Beam();
            var rng = new System.Random(29);
            for (int i = 0; i < 8; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                Vector3 foot = W(side * (12 + rng.NextDouble() * 6), 0.5, -14 + i * 6.5);
                Vector3 dir = (W(-side * (20 + rng.NextDouble() * 40), 160, -120 - rng.NextDouble() * 120) - foot).normalized;
                const float len = 190f, wide = 2.2f;
                for (int k = 0; k < 2; k++)                                           // two crossed cards: a beam from any side
                {
                    var q = Greybox.Primitive(PrimitiveType.Quad, "Searchlight", c.Venue, mat, false);
                    q.transform.localPosition = foot + dir * (len / 2);                 // (a quad's v runs up it: the foot at the foot)
                    q.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(dir, k == 0 ? Vector3.right : Vector3.forward).normalized, dir);
                    q.transform.localScale = new Vector3(wide, len, 1f);
                }
            }
        }

        // =====================================================================================
        // the courts
        // =====================================================================================

        /// <summary>The wintergarden (the Canary Wharf photo).</summary>
        static void ShowCourt(Ctx c)
        {
            var floor = c.Palette.LitColor(Greybox.Hex(0x1a1e26)); floor.SetFloat("_Smoothness", 0.55f);
            Greybox.Box("ArenaFloor", c.Venue, floor, W(0, -0.07, L / 2), new Vector3(46f, 0.1f, 52f));
            GlassShell(c);

            // Stands: the sides from the front to well behind the camera; behind the front wall; behind the camera.
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "StandLeft" : "StandRight", 14, i => W(side * (HW + 1.5 + i * ROW_DEPTH), 0, L / 2 + 2.5),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 34f, side + 5);
            Stand(c, "StandFront", 22, i => W(0, 0, -2.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 2.6f, 2);
            Stand(c, "StandBack", 14, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 2.6f, 3);
            GalleryMat(c, Greybox.Hex(0x0f1d36), Greybox.Hex(0x1d5fb8));

            var screen = BigScreen(c, 7.35f);
            Hall(c, screen);
            Gantry(c);

            Ambient(new Color(0.30f, 0.29f, 0.31f), new Color(0.27f, 0.24f, 0.22f), new Color(0.12f, 0.11f, 0.11f));
            Fog(Greybox.Hex(0x0f0b09), 26f, 80f);
            if (c.High)
                foreach (int side in new[] { -1, 1 })
                    for (int k = 0; k < 3; k++)
                        Point(c, "HallWarm", W(side * (HallX - 3f), 9.5, -12 + k * 16), new Color(1f, 0.68f, 0.38f), 16f, 2.2f);
        }

        /// <summary>A club court: plaster to a white ceiling of strip lights, the gallery behind the glass.</summary>
        static void Club(Ctx c)
        {
            var paint = c.Palette.LitColor(Greybox.Hex(0xe9e6df)); paint.SetFloat("_Smoothness", 0.1f);
            var cream = c.Palette.LitColor(Greybox.Hex(0xd8cdb8));
            var carpet = c.Palette.LitColor(Greybox.Hex(0x5a2228));
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.35f);
            const float ceiling = 6.1f, galleryEnd = 19f, galleryCeil = 4.6f;

            // Above the court's walls up to the ceiling; the ceiling with its strip lights.
            Greybox.Box("FrontWallUpper", c.Venue, paint, W(0, (CH + ceiling) / 2, -0.05), new Vector3(2 * HW + 0.2f, ceiling - CH, 0.1f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("SideWallUpper", c.Venue, paint, W(side * (HW + 0.05), (CH + ceiling) / 2, L / 2), new Vector3(0.1f, ceiling - CH, L + 0.2f), false);
            // (none of the room casts a shadow: the key light stands for the ceiling's strip lights)
            Greybox.Box("Ceiling", c.Venue, paint, W(0, ceiling + 0.05, L / 2), new Vector3(2 * HW + 0.4f, 0.1f, L + 0.2f), false);
            var strip = c.Palette.UnlitColor(c.High ? new Color(2.2f, 2.2f, 2.1f) : new Color(1.3f, 1.3f, 1.25f));
            for (int k = 0; k < 3; k++)
                foreach (int side in new[] { -1, 1 })
                {
                    float z = 1.6f + k * 3.2f;
                    Greybox.Box("StripHousing", c.Venue, c.Steel, W(side * 1.4, ceiling - 0.04, z), new Vector3(0.32f, 0.06f, 1.6f), false);
                    Greybox.Box("Strip", c.Venue, strip, W(side * 1.4, ceiling - 0.075, z), new Vector3(0.24f, 0.02f, 1.5f), false);
                    if (c.High) Spot(c, W(side * 1.4, ceiling - 0.12, z), 9f, 100f, 9f);
                }

            // The gallery: a carpeted room behind the glass, wood up to the rail, cream above; a low ceiling.
            float gl = galleryEnd - L, gz = L + gl / 2;
            Greybox.Box("GalleryFloor", c.Venue, carpet, W(0, -0.05, gz), new Vector3(9f, 0.1f, gl));
            // The corridors either side of the court, up to the room's walls: floor, walls and ceiling.
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("CorridorFloor", c.Venue, carpet, W(side * (HW + 0.7), -0.05, L / 2), new Vector3(1.4f, 0.1f, L + 0.6f), false);
                Greybox.Box("RoomWall", c.Venue, cream, W(side * 4.45, ceiling / 2, L / 2), new Vector3(0.1f, ceiling, L + 0.6f), false);
                Greybox.Box("CorridorCeiling", c.Venue, paint, W(side * (HW + 0.7), ceiling + 0.05, L / 2), new Vector3(1.4f, 0.1f, L + 0.6f), false);
            }
            Greybox.Box("RoomFront", c.Venue, cream, W(0, ceiling / 2, -0.35), new Vector3(9f, ceiling, 0.1f), false);
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.TexturedBox("GalleryPanel", c.Venue, wood, W(side * 4.45, 0.55, gz), new Vector3(0.1f, 1.1f, gl), new Vector2(2.4f, 2.4f), false);
                Greybox.Box("GalleryWall", c.Venue, cream, W(side * 4.45, (1.1 + galleryCeil) / 2, gz), new Vector3(0.1f, galleryCeil - 1.1f, gl), false);
            }
            Greybox.Box("GalleryEnd", c.Venue, cream, W(0, galleryCeil / 2, galleryEnd), new Vector3(9f, galleryCeil, 0.1f), false);
            Greybox.Box("GalleryCeiling", c.Venue, paint, W(0, galleryCeil + 0.05, gz), new Vector3(9f, 0.1f, gl), false);
            Greybox.Box("GalleryOverCourt", c.Venue, paint, W(0, (galleryCeil + ceiling) / 2, L + 0.05), new Vector3(9f, ceiling - galleryCeil, 0.1f), false);
            // A rail along the glass, and a few people watching from either side of the camera.
            Greybox.Box("GalleryRail", c.Venue, c.Steel, W(0, 1.0, L + 0.5), new Vector3(8.6f, 0.05f, 0.05f), false);
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "GalleryLeft" : "GalleryRight", 3, i => W(side * 3.05, 0, L + 1.0 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2.6f, 7 + side, FlatTread);
            // Honours boards in the gallery (the club's history).
            var board = c.Palette.LitColor(Greybox.Hex(0x3a2514)); board.SetFloat("_Smoothness", 0.5f);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("HonoursBoard", c.Venue, board, W(side * 4.38, 2.6, L + 6.0), new Vector3(0.04f, 1.4f, 2.2f), false);

            Ambient(new Color(0.62f, 0.62f, 0.60f), new Color(0.55f, 0.54f, 0.52f), new Color(0.35f, 0.33f, 0.30f));
            RenderSettings.fog = false;
        }

        /// <summary>Glass on a stage in the dunes at night, the pyramids floodlit beyond the front wall.</summary>
        static void Desert(Ctx c)
        {
            Sky(c, false, 600f);
            var sand = c.Palette.LitColor(new Color(0.55f, 0.48f, 0.42f)); sand.mainTexture = ProcTex.Sand(); sand.SetFloat("_Smoothness", 0.05f);
            Greybox.TexturedBox("Sand", c.Venue, sand, W(0, -0.6, 0), new Vector3(1200f, 0.2f, 1200f), new Vector2(8f, 8f), false);
            var deck = c.Palette.LitColor(Greybox.Hex(0x2a2018)); deck.SetFloat("_Smoothness", 0.3f);
            Greybox.Box("Stage", c.Venue, deck, W(0, -0.3, L / 2 + 3), new Vector3(30f, 0.5f, 36f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x3a2614), Greybox.Hex(0xc8a03c));

            // Dunes rolling away, and the three pyramids lit gold behind the front wall.
            var dune = c.Palette.LitColor(new Color(0.42f, 0.33f, 0.24f)); dune.SetFloat("_Smoothness", 0f);
            var rng = new System.Random(3);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 70f + (float)rng.NextDouble() * 260f;
                var d = Greybox.Primitive(PrimitiveType.Sphere, "Dune", c.Venue, dune, false);
                d.transform.localPosition = new Vector3(Mathf.Cos(a) * r, -0.6f, Mathf.Sin(a) * r + L / 2);
                d.transform.localScale = new Vector3(60f + (float)rng.NextDouble() * 80f, 6f + (float)rng.NextDouble() * 14f, 40f + (float)rng.NextDouble() * 60f);
            }
            Pyramid(c, W(-40, -0.6, -330), 150f, 96f);
            Pyramid(c, W(150, -0.6, -470), 140f, 90f);
            Pyramid(c, W(280, -0.6, -560), 70f, 44f);

            // Temporary grandstands on the sides and behind the camera (the front kept clear for the view).
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "StandLeft" : "StandRight", 9, i => W(side * (HW + 1.6 + i * ROW_DEPTH), 0, L / 2 + 1.5),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 24f, side + 11);
            Stand(c, "StandBack", 9, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 4f, 13);

            // Floodlight towers at the corners.
            FloodTowers(c, 13.5f, -6f, L + 14f, 22f);
            Ambient(new Color(0.16f, 0.18f, 0.26f), new Color(0.14f, 0.13f, 0.15f), new Color(0.08f, 0.06f, 0.05f));
            RenderSettings.fog = false;
        }

        /// <summary>The station concourse.</summary>
        static void Station(Ctx c)
        {
            const float hx = 24f, front = -42f, back = 40f, wallH = 22f, rise = 10f;
            var marble = c.Palette.LitColor(Color.white); marble.mainTexture = ProcTex.Marble(); marble.SetFloat("_Smoothness", 0.65f);
            Greybox.TexturedBox("Concourse", c.Venue, marble, W(0, -0.07, (front + back) / 2), new Vector3(2 * hx, 0.1f, back - front), new Vector2(2.4f, 2.4f));
            GlassShell(c);
            var stone = c.Palette.LitColor(Color.white); stone.mainTexture = ProcTex.Stone(); stone.SetFloat("_Smoothness", 0.15f);
            foreach (int side in new[] { -1, 1 })
                Greybox.TexturedBox("StationWall", c.Venue, stone, W(side * hx, wallH / 2, (front + back) / 2), new Vector3(0.6f, wallH, back - front), new Vector2(3f, 3f));
            Greybox.TexturedBox("StationEnd", c.Venue, stone, W(0, (wallH + rise) / 2, front), new Vector3(2 * hx, wallH + rise, 0.6f), new Vector2(3f, 3f));
            Greybox.TexturedBox("StationEnd", c.Venue, stone, W(0, (wallH + rise) / 2, back), new Vector3(2 * hx, wallH + rise, 0.6f), new Vector2(3f, 3f));

            // Windows: three great arches in the end wall beyond the front wall, a row high in each side wall.
            var win = c.Palette.Clear(new Color(1.35f, 1.35f, 1.35f, 1f)); win.mainTexture = ProcTex.ArchWindow();
            for (int k = -1; k <= 1; k++)
                Greybox.TexturedBox("ArchWindow", c.Venue, win, W(k * 13f, 13f, front + 0.4f), new Vector3(9f, 18f, 0.05f), new Vector2(9f, 18f), false);
            foreach (int side in new[] { -1, 1 })
                for (float z = front + 9f; z < back - 6f; z += 11f)
                    Greybox.TexturedBox("SideWindow", c.Venue, win, W(side * (hx - 0.35f), 15f, z), new Vector3(0.05f, 11f, 5.5f), new Vector2(5.5f, 11f), false);

            // The vault: turquoise with gold stars, from the walls' tops.
            var vault = c.Palette.UnlitColor(new Color(0.85f, 0.85f, 0.85f)); vault.mainTexture = ProcTex.StarCeiling();
            var sk = new MeshMerge();
            const int SEG = 20;
            for (int i = 0; i < SEG; i++)
            {
                float a0 = Mathf.PI * i / SEG, a1 = Mathf.PI * (i + 1) / SEG;
                Vector3 p0 = new Vector3(-Mathf.Cos(a0) * hx, wallH + Mathf.Sin(a0) * rise, 0), p1 = new Vector3(-Mathf.Cos(a1) * hx, wallH + Mathf.Sin(a1) * rise, 0);
                sk.Quad(p0 + new Vector3(0, 0, front), p1 + new Vector3(0, 0, front), p1 + new Vector3(0, 0, back), p0 + new Vector3(0, 0, back));
            }
            var vaultGo = sk.Build("StarVault", c.Venue, vault);
            StretchUV(vaultGo, 2 * hx * Mathf.PI / 2f / 24f, (back - front) / 24f);

            // Chandeliers, and the clock on its kiosk beyond the front wall.
            var warm = c.Palette.UnlitColor(new Color(2.6f, 1.9f, 1.1f));
            var brass = c.Palette.LitColor(Greybox.Hex(0xb08a3c)); brass.SetFloat("_Smoothness", 0.75f); brass.SetFloat("_Metallic", 0.8f);
            var bulbs = new MeshMerge();
            foreach (float z in new[] { -18f, 0f, 22f })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 hub = W(side * 11f, 15f, z);
                    Greybox.Box("ChandelierCable", c.Venue, c.Steel, hub + Vector3.up * 8f, new Vector3(0.05f, 16f, 0.05f), false);
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * Mathf.PI * 2f / 10f;
                        bulbs.Box(hub + new Vector3(Mathf.Cos(a) * 1.2f, -0.4f * (k % 2), Mathf.Sin(a) * 1.2f), new Vector3(0.28f, 0.28f, 0.28f), Quaternion.identity);
                    }
                    if (c.High) Point(c, "Chandelier", hub, new Color(1f, 0.82f, 0.6f), 22f, 2.6f);
                }
            bulbs.Build("ChandelierBulbs", c.Venue, warm);
            var kiosk = Greybox.Primitive(PrimitiveType.Cylinder, "ClockKiosk", c.Venue, brass);
            kiosk.transform.localPosition = W(0, 1.6, -15f); kiosk.transform.localScale = new Vector3(3.6f, 1.6f, 3.6f);
            var ball = Greybox.Primitive(PrimitiveType.Sphere, "Clock", c.Venue, brass);
            ball.transform.localPosition = W(0, 4.2, -15f); ball.transform.localScale = Vector3.one * 1.6f;
            var face = c.Palette.UnlitColor(new Color(1.7f, 1.62f, 1.4f));
            if (c.High) Point(c, "ClockLight", W(0, 6.5, -12.5), new Color(1f, 0.85f, 0.6f), 12f, 3.2f);
            for (int k = 0; k < 4; k++)
            {
                Quaternion r = Quaternion.Euler(0, k * 90f, 0);
                var f = Greybox.MeshObject("ClockFace", c.Venue, Greybox.Disc(0.55f, 28), face);
                f.transform.position = W(0, 4.2, -15f) + r * Vector3.forward * 0.81f;
                f.transform.rotation = Quaternion.LookRotation(r * Vector3.forward, Vector3.up);
            }

            // Bleachers along the sides and behind the camera: the end beyond the front wall left open for the view.
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "StandLeft" : "StandRight", 10, i => W(side * (HW + 1.6 + i * ROW_DEPTH), 0, L / 2 + 2f),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 26f, side + 21);
            Stand(c, "StandBack", 10, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 4f, 23);

            Gantry(c);
            Ambient(new Color(0.55f, 0.55f, 0.52f), new Color(0.50f, 0.45f, 0.38f), new Color(0.30f, 0.26f, 0.22f));
            Fog(Greybox.Hex(0x6e6252), 30f, 150f);
        }

        /// <summary>A roof deck at dusk, the city all round.</summary>
        static void Rooftop(Ctx c)
        {
            Sky(c, true, 650f);
            var concrete = c.Palette.LitColor(Color.white); concrete.mainTexture = ProcTex.Concrete(); concrete.SetFloat("_Smoothness", 0.2f);
            const float dx = 16f, dz0 = -12f, dz1 = 30f;
            Greybox.TexturedBox("Deck", c.Venue, concrete, W(0, -0.3, (dz0 + dz1) / 2), new Vector3(2 * dx, 0.5f, dz1 - dz0), new Vector2(4f, 4f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x1d1d24), Greybox.Hex(0xff9a3c));
            // The parapet and its rail.
            var parapet = c.Palette.LitColor(Greybox.Hex(0x5b5a58));
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("Parapet", c.Venue, parapet, W(side * dx, 0.5, (dz0 + dz1) / 2), new Vector3(0.3f, 1.1f, dz1 - dz0));
                Greybox.Box("Rail", c.Venue, c.Steel, W(side * dx, 1.15, (dz0 + dz1) / 2), new Vector3(0.06f, 0.06f, dz1 - dz0), false);
            }
            foreach (float z in new[] { dz0, dz1 })
            {
                Greybox.Box("Parapet", c.Venue, parapet, W(0, 0.5, z), new Vector3(2 * dx, 1.1f, 0.3f));
                Greybox.Box("Rail", c.Venue, c.Steel, W(0, 1.15, z), new Vector3(2 * dx, 0.06f, 0.06f), false);
            }
            // The city: towers all round, their windows lit, the nearer ones below the deck.
            var rng = new System.Random(9);
            var mats = new Material[4];
            for (int i = 0; i < mats.Length; i++) { mats[i] = c.Palette.UnlitColor(new Color(1f, 1f, 1f)); mats[i].mainTexture = ProcTex.CityWindows(i); }
            var red = c.Palette.UnlitColor(new Color(3f, 0.2f, 0.15f));
            int built = 0;
            for (int i = 0; i < 400 && built < 160; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = 95f + (float)(rng.NextDouble() * rng.NextDouble()) * 460f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r + L / 2;
                if (Mathf.Abs(x) < 30f && z > -30f && z < 50f) continue;
                float w = 14f + (float)rng.NextDouble() * 26f, d = 14f + (float)rng.NextDouble() * 26f;
                float top = -45f + (float)(rng.NextDouble() * rng.NextDouble() * rng.NextDouble()) * 230f + r * 0.04f;   // most below the deck, a few towering
                float bottom = -120f, h = top - bottom;
                Greybox.TexturedBox("Tower", c.Venue, mats[built % mats.Length], new Vector3(x, bottom + h / 2, z), new Vector3(w, h, d), new Vector2(12.8f, 25.6f), false);
                if (top > 40f && rng.NextDouble() < 0.5)
                    Greybox.Box("AirLight", c.Venue, red, new Vector3(x, top + 2f, z), new Vector3(0.8f, 0.8f, 0.8f), false);
                built++;
            }
            // String lights over the deck, zigzagging between posts.
            var bulb = c.Palette.UnlitColor(new Color(2.4f, 1.6f, 0.8f));
            var bulbs = new MeshMerge();
            foreach (int side in new[] { -1, 1 })
                for (float z = dz0 + 1f; z < dz1; z += 6f)
                {
                    Greybox.Box("LightPost", c.Venue, c.Steel, W(side * (dx - 1f), 4.2f, z), new Vector3(0.1f, 8.4f, 0.1f), false);
                    Vector3 a0 = W(side * (dx - 1f), 8.4f, z), a1 = W(-side * (dx - 1f), 8.4f, z + 3f);
                    for (int k = 0; k <= 16; k++)
                    {
                        float t = k / 16f;
                        Vector3 p = Vector3.Lerp(a0, a1, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.2f;
                        bulbs.Box(p, new Vector3(0.05f, 0.07f, 0.05f), Quaternion.identity);
                    }
                }
            bulbs.Build("StringLights", c.Venue, bulb);
            // A small crowd on chairs at deck level.
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "ChairsLeft" : "ChairsRight", 4, i => W(side * (HW + 1.6 + i * ROW_DEPTH), 0, L / 2 + 2f),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 16f, side + 31, FlatTread);
            Stand(c, "ChairsBack", 4, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 4f, 33, FlatTread);

            FloodTowers(c, 12f, -8f, L + 12f, 14f);
            Ambient(new Color(0.34f, 0.30f, 0.42f), new Color(0.40f, 0.28f, 0.26f), new Color(0.12f, 0.10f, 0.10f));
            RenderSettings.fog = false;
            if (c.High)
                foreach (int side in new[] { -1, 1 })
                    Point(c, "DeckWarm", W(side * 9f, 5f, L / 2), new Color(1f, 0.7f, 0.4f), 18f, 1.6f);
        }

        /// <summary>A modern arena: steep stands all round, LED ribbons, the cube of screens overhead.</summary>
        static void Arena(Ctx c)
        {
            var floor = c.Palette.LitColor(Greybox.Hex(0x14161c)); floor.SetFloat("_Smoothness", 0.6f);
            Greybox.Box("ArenaFloor", c.Venue, floor, W(0, -0.07, L / 2), new Vector3(70f, 0.1f, 80f));
            GlassShell(c);
            GalleryMat(c, Greybox.Hex(0x14141c), Greybox.Hex(0xb942f5));
            foreach (int side in new[] { -1, 1 })
                Stand(c, side < 0 ? "StandLeft" : "StandRight", 22, i => W(side * (HW + 2.2 + i * ROW_DEPTH), 0, L / 2 + 2.5),
                    new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 40f, side + 41, SteepTread);
            Stand(c, "StandFront", 22, i => W(0, 0, -3.2 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 4.4f, 43, SteepTread);
            Stand(c, "StandBack", 18, i => W(0, 0, L + 8f + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 4.4f, 45, SteepTread);

            // LED ribbons along the fascia of the side stands.
            var ribbonA = LedMat(c.Palette, "lime", "RE:SQUASHSTRIKE  •  WORLD TOUR  •", Greybox.Hex(0xb8ee26), new Color(0.02f, 0.025f, 0.03f), 1.3f);
            var ribbonB = LedMat(c.Palette, "white", "GLASS COURT SERIES  •  PLAY ON  •", new Color(0.95f, 0.96f, 1f), new Color(0.02f, 0.03f, 0.06f), 1.1f);
            foreach (int side in new[] { -1, 1 })
                Greybox.TexturedBox("Ribbon", c.Venue, side < 0 ? ribbonA : ribbonB, W(side * (HW + 2.2 + 6 * ROW_DEPTH - 0.05), SteepTread(6) - 0.5, L / 2 + 2.5),
                    new Vector3(0.06f, 0.6f, 40f), new Vector2(9.6f, 0.6f), false);

            // The cube of screens overhead, the match live on every face.
            var screen = c.Palette.UnlitColor(new Color(1.1f, 1.1f, 1.1f));
            screen.mainTexture = c.High && Post ? LiveFeed(c.Venue) : (Texture)ProcTex.Screen();
            Vector3 cube = W(0, 14f, L / 2);
            Greybox.Box("CubeFrame", c.Venue, c.Steel, cube, new Vector3(6.6f, 3.9f, 6.6f), false);
            Greybox.TexturedBox("CubeScreens", c.Venue, screen, cube, new Vector3(6.7f, 3.6f, 6.7f), new Vector2(6.7f, 3.6f), false);
            Greybox.Box("CubeCable", c.Venue, c.Steel, cube + Vector3.up * 6f, new Vector3(0.08f, 8f, 0.08f), false);
            // Rigging, and the roof lost in the dark.
            var rig = new MeshMerge();
            for (float x = -24f; x <= 24f; x += 6f) rig.Beam(new Vector3(x, 22f, -30f), new Vector3(x, 22f, 40f), 0.3f, 0.5f);
            for (float z = -30f; z <= 40f; z += 7f) rig.Beam(new Vector3(-26f, 22.4f, z), new Vector3(26f, 22.4f, z), 0.2f, 0.3f);
            rig.Build("Rigging", c.Venue, c.Steel);
            var black = c.Palette.UnlitColor(Greybox.Hex(0x050608));
            Greybox.Box("Roof", c.Venue, black, W(0, 25, L / 2), new Vector3(80f, 0.2f, 90f), false);

            Gantry(c);
            Ambient(new Color(0.20f, 0.20f, 0.26f), new Color(0.16f, 0.15f, 0.20f), new Color(0.08f, 0.08f, 0.10f));
            Fog(Greybox.Hex(0x08080e), 28f, 90f);
            if (c.High)
                foreach (int side in new[] { -1, 1 })
                {
                    Point(c, "StandWash", W(side * 14f, 10f, 0f), new Color(0.55f, 0.35f, 1f), 22f, 2.4f);
                    Point(c, "StandWash", W(side * 14f, 10f, 18f), new Color(0.3f, 0.55f, 1f), 22f, 2.4f);
                }
        }

        // =====================================================================================
        // shared pieces
        // =====================================================================================

        /// <summary>A glass court's frame: blue coping along the tops, a blue plinth along the foot, fins at the back corners.</summary>
        static void GlassShell(Ctx c)
        {
            if (Courts.Current.Walls == WallStyle.Plaster) return;
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("SideCoping", c.Venue, c.Blue, W(side * (HW + 0.03), CH + 0.05, L / 2), new Vector3(0.12f, 0.1f, L + 0.1f));
                Greybox.Box("SidePlinth", c.Venue, c.Blue, W(side * (HW + 0.2), 0.08, L / 2), new Vector3(0.36f, 0.18f, L + 0.4f));
                Greybox.Box("CornerPost", c.Venue, c.Blue, W(side * (HW + 0.08), BACK_WALL_OUT_H / 2 + 0.02, L + 0.02), new Vector3(0.14f, (float)BACK_WALL_OUT_H + 0.04f, 0.1f));
            }
            Greybox.Box("FrontCoping", c.Venue, c.Blue, W(0, CH + 0.05, -0.06), new Vector3(2 * HW + 0.5f, 0.1f, 0.36f));
        }

        /// <summary>The mat behind the glass, edged.</summary>
        static void GalleryMat(Ctx c, Color matColour, Color edgeColour)
        {
            var mat = c.Palette.LitColor(matColour); mat.SetFloat("_Smoothness", 0.35f);
            var edge = c.Palette.LitColor(edgeColour);
            float mw = 2 * HW + 1.6f, md = 4.6f, mz = L + 0.12f + md / 2;
            Greybox.Box("GalleryMat", c.Venue, mat, W(0, -0.005, mz), new Vector3(mw, 0.02f, md), false);
            Greybox.Box("GalleryEdge", c.Venue, edge, W(0, 0.006, mz + md / 2 - 0.04), new Vector3(mw, 0.004f, 0.06f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GalleryEdge", c.Venue, edge, W(side * (mw / 2 - 0.03), 0.006, mz), new Vector3(0.06f, 0.004f, md), false);
        }

        /// <summary>The big screen over the front wall: the match live (High) or the event's mark. Returns its material.</summary>
        static Material BigScreen(Ctx c, float y)
        {
            var screenMat = c.Palette.UnlitColor(new Color(1.1f, 1.1f, 1.1f));
            screenMat.mainTexture = c.High && Post ? LiveFeed(c.Venue) : (Texture)ProcTex.Screen();
            Greybox.Box("ScreenFrame", c.Venue, c.Steel, W(0, y, -1.35), new Vector3(4.3f, 2.5f, 0.2f), false);
            Greybox.TexturedBox("Screen", c.Venue, screenMat, W(0, y, -1.22), new Vector3(4.0f, 2.25f, 0.04f), new Vector2(4.0f, 2.25f), false);
            Greybox.Box("ScreenCable", c.Venue, c.Steel, W(-1.8, y + 2.55, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);
            Greybox.Box("ScreenCable", c.Venue, c.Steel, W(1.8, y + 2.55, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);
            return screenMat;
        }

        /// <summary>The lighting gantry over the court and the spots under its lamps.</summary>
        static void Gantry(Ctx c)
        {
            var lamp = c.Palette.UnlitColor(c.High ? new Color(3.2f, 3.1f, 2.9f) : new Color(1.4f, 1.4f, 1.35f));
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GantryRail", c.Venue, c.Steel, W(side * 2.2, 7.25, L / 2 + 0.7), new Vector3(0.18f, 0.22f, L - 2.0f), false);
            for (int k = 0; k < 4; k++)
            {
                float z = 2.0f + k * (L - 2.6f) / 3f;                          // clear of the big screen
                Greybox.Box("GantryCross", c.Venue, c.Steel, W(0, 7.4, z), new Vector3(4.8f, 0.14f, 0.14f), false);
                foreach (int side in new[] { -1, 1 })
                {
                    Greybox.Box("LampHousing", c.Venue, c.Dark, W(side * 2.2, 7.05, z), new Vector3(1.0f, 0.18f, 0.55f), false);
                    Greybox.Box("Lamp", c.Venue, lamp, W(side * 2.2, 6.95, z), new Vector3(0.86f, 0.03f, 0.42f), false);
                }
            }
            if (!c.High) return;
            for (int k = 0; k < 4; k++)
            {
                float z = 0.6f + k * (L - 1.2f) / 3f;
                foreach (int side in new[] { -1, 1 }) Spot(c, W(side * 1.7, 6.9, z), 17f, 92f, 13f);
            }
        }

        /// <summary>Four floodlight towers round the court (outdoors), their banks aimed at it.</summary>
        static void FloodTowers(Ctx c, float x, float zFront, float zBack, float height)
        {
            var lamp = c.Palette.UnlitColor(new Color(3.4f, 3.3f, 3.0f));
            foreach (float z in new[] { zFront, zBack })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 foot = W(side * x, 0, z), top = foot + Vector3.up * height;
                    Greybox.Box("TowerMast", c.Venue, c.Steel, foot + Vector3.up * height / 2, new Vector3(0.6f, height, 0.6f), false);
                    Vector3 aim = (W(0, 0.5, L / 2) - top).normalized;
                    var head = Greybox.Box("TowerHead", c.Venue, c.Dark, top, new Vector3(3.2f, 2.0f, 0.4f), false);
                    head.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
                    var face = Greybox.Box("TowerLamps", c.Venue, lamp, top + aim * 0.22f, new Vector3(3.0f, 1.8f, 0.05f), false);
                    face.transform.rotation = head.transform.rotation;
                    if (c.High)
                    {
                        var s = new GameObject("TowerSpot").AddComponent<Light>();
                        s.transform.SetParent(c.Venue, false);
                        s.type = LightType.Spot; s.color = new Color(1f, 0.97f, 0.92f);
                        s.spotAngle = 40f; s.innerSpotAngle = 20f; s.range = 60f; s.intensity = 90f; s.shadows = LightShadows.None;
                        s.transform.position = top + aim * 0.5f;
                        s.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
                    }
                }
        }

        /// <summary>A pyramid: four faces, the one toward the court floodlit gold, the others falling into the night.</summary>
        static void Pyramid(Ctx c, Vector3 baseCentre, float baseSize, float height)
        {
            float h = baseSize / 2;
            Vector3 apex = baseCentre + Vector3.up * height;
            Vector3[] corners = { baseCentre + new Vector3(-h, 0, -h), baseCentre + new Vector3(h, 0, -h), baseCentre + new Vector3(h, 0, h), baseCentre + new Vector3(-h, 0, h) };
            Color[] tones = { new Color(0.16f, 0.11f, 0.08f), new Color(0.40f, 0.26f, 0.13f), new Color(1.0f, 0.66f, 0.32f), new Color(0.44f, 0.29f, 0.14f) };
            var tex = ProcTex.PyramidFace();
            for (int f = 0; f < 4; f++)
            {
                // One face, both sides, stepped courses lit from the base (graphics pass 2).
                Vector3 a = corners[f], b = corners[(f + 1) % 4];
                var mesh = new Mesh { name = "PyramidFace" };
                mesh.vertices = new[] { a, b, apex, a, b, apex };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1) };
                mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var mat = c.Palette.UnlitColor(tones[f]); mat.mainTexture = tex;
                Greybox.MeshObject("PyramidFace", c.Venue, mesh, mat);
            }
        }

        /// <summary>The sky: a dome round everything, the night (stars, moon) or dusk.</summary>
        static void Sky(Ctx c, bool dusk, float radius)
        {
            var mat = c.Palette.UnlitColor(Color.white);
            mat.mainTexture = ProcTex.Sky(dusk);
            var go = Greybox.MeshObject("Sky", c.Venue, SkyDome(radius, 48, 24), mat);
            go.transform.position = W(0, 0, L / 2);
        }

        static Mesh SkyDome(float r, int lon, int lat)
        {
            var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= lat; i++)
                for (int j = 0; j <= lon; j++)
                {
                    float a = Mathf.PI * i / lat - Mathf.PI / 2f, b = Mathf.PI * 2f * j / lon;
                    v.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b)) * r);
                    uv.Add(new Vector2(j / (float)lon, i / (float)lat));
                }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int q = i * (lon + 1) + j, w = q + lon + 1;
                    t.Add(q); t.Add(q + 1); t.Add(w);                      // wound to face inward
                    t.Add(q + 1); t.Add(w + 1); t.Add(w);
                }
            var m = new Mesh { name = "SkyDome" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void StretchUV(GameObject go, float u, float v)
        {
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            var verts = m.vertices; var uv = new Vector2[verts.Length];
            // The merged quads come in fours along the arch: u across the arch, v along the hall.
            for (int i = 0; i < verts.Length; i += 4)
            {
                int quad = (i / 8);
                float u0 = quad / 20f * u, u1 = (quad + 1) / 20f * u;
                uv[i] = new Vector2(u0, 0); uv[i + 1] = new Vector2(u1, 0); uv[i + 2] = new Vector2(u1, v); uv[i + 3] = new Vector2(u0, v);
            }
            m.uv = uv;
        }

        static void Spot(Ctx c, Vector3 at, float intensity, float angle, float range)
        {
            var spot = new GameObject("CourtSpot").AddComponent<Light>();
            spot.transform.SetParent(c.Venue, false);
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.97f, 0.92f);
            spot.spotAngle = angle;
            spot.innerSpotAngle = angle * 0.45f;
            spot.range = range;
            spot.intensity = intensity;
            spot.shadows = LightShadows.None;
            spot.transform.position = at;
            spot.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }

        static void Point(Ctx c, string name, Vector3 at, Color colour, float range, float intensity)
        {
            var p = new GameObject(name).AddComponent<Light>();
            p.transform.SetParent(c.Venue, false);
            p.type = LightType.Point;
            p.color = colour;
            p.range = range;
            p.intensity = intensity;
            p.transform.position = at;
        }

        static void Ambient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.reflectionIntensity = 0.3f;
        }

        static void Fog(Color colour, float start, float end)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = colour;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
        }

        /// <summary>Raked rows of seats with people on them. rowPos(i) is the row's front edge at floor level
        /// (centre along its length); along is the row's length axis, facing points at the court. At High the
        /// people are 3D (CrowdBuilder) on benches; at Low they are painted cards.</summary>
        static void Stand(Ctx c, string name, int rows, System.Func<int, Vector3> rowPos, Vector3 along, Vector3 facing, float length, int seed,
            System.Func<int, float> tread = null)
        {
            tread = tread ?? Tread;
            var crowd = c.High ? new[] { BenchMat(c.Palette) } : c.Crowd;
            var stand = new GameObject(name).transform;
            stand.SetParent(c.Venue, false);
            Vector3 back = -facing;
            bool alongX = Mathf.Abs(along.x) > 0.5f;
            for (int i = 0; i < rows; i++)
            {
                Vector3 p = rowPos(i);
                float top = tread(i);
                // The tier: one block from the floor to this row's tread (rows behind stack higher).
                Vector3 size = alongX ? new Vector3(length, top, ROW_DEPTH) : new Vector3(ROW_DEPTH, top, length);
                if (top > 0.1f) Greybox.Box("Tier", stand, c.Seat, p + back * (ROW_DEPTH / 2) + Vector3.up * (top / 2), size, false);
                if (c.People != null)
                {
                    // The bench they sit on.
                    Vector3 bsize = alongX ? new Vector3(length, 0.42f, 0.36f) : new Vector3(0.36f, 0.42f, length);
                    Greybox.Box("Bench", stand, crowd[0], p + back * 0.64f + Vector3.up * (top + 0.21f), bsize, false);
                    continue;
                }
                // The people: a thin card on the tread, a little back from the edge.
                var mat = crowd[(i + seed) % crowd.Length];
                Vector3 csize = alongX ? new Vector3(length, CROWD_H, 0.04f) : new Vector3(0.04f, CROWD_H, length);
                var row = Greybox.TexturedBox("Crowd", stand, mat, p + back * 0.45f + Vector3.up * (top + CROWD_H / 2), csize, new Vector2(20f, CROWD_H), false);
                // Shift each row's pattern so neighbouring rows don't line up.
                var mesh = row.GetComponent<MeshFilter>().sharedMesh;
                var uv = mesh.uv; float off = ((i * 7 + seed * 3) % 11) / 11f;
                for (int k = 0; k < uv.Length; k++) uv[k].x += off;
                mesh.uv = uv;
            }
            if (c.People != null)
                Motion.Add(CrowdBuilder.Stand(stand, c.People, name, rows, rowPos, tread, along, facing, length, seed));
        }

        /// <summary>The show court's hall: wood-panelled walls, dark pillars (a TV on every other one), a line of warm
        /// lamps along a balcony rail, and a vault of steel ladder trusses with amber lamps.</summary>
        static void Hall(Ctx c, Material screen)
        {
            float back = L + HallBackFromLength, len = back - HallFront, midZ = (back + HallFront) / 2;
            var wood = c.Palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.35f);
            var pillar = c.Palette.LitColor(Greybox.Hex(0x1b1d22)); pillar.SetFloat("_Smoothness", 0.5f);
            var warmLamp = c.Palette.UnlitColor(new Color(2.8f, 1.7f, 0.8f));
            var roofSkin = c.Palette.UnlitColor(Greybox.Hex(0x05070b));
            var hall = new GameObject("Hall").transform; hall.SetParent(c.Venue, false);

            foreach (int side in new[] { -1, 1 })
                Greybox.TexturedBox("HallWall", hall, wood, W(side * HallX, HallWallH / 2, midZ), new Vector3(0.3f, HallWallH, len), new Vector2(2.4f, 2.4f));
            Greybox.TexturedBox("HallWall", hall, wood, W(0, HallWallH / 2, HallFront), new Vector3(2 * HallX, HallWallH, 0.3f), new Vector2(2.4f, 2.4f));
            Greybox.TexturedBox("HallWall", hall, wood, W(0, HallWallH / 2, back), new Vector3(2 * HallX, HallWallH, 0.3f), new Vector2(2.4f, 2.4f));
            Greybox.Box("HallEndFront", hall, roofSkin, W(0, HallWallH + 6, HallFront - 0.2), new Vector3(2 * HallX, 12f, 0.2f), false);
            Greybox.Box("HallEndBack", hall, roofSkin, W(0, HallWallH + 6, back + 0.2), new Vector3(2 * HallX, 12f, 0.2f), false);

            var lamps = new MeshMerge();
            int k = 0;
            for (float z = HallFront + 3f; z < back - 2f; z += 6f, k++)
                foreach (int side in new[] { -1, 1 })
                {
                    float x = side * (HallX - 0.35f);
                    Greybox.Box("Pillar", hall, pillar, W(x, HallWallH / 2, z), new Vector3(0.5f, HallWallH, 0.5f), false);
                    if (k % 2 == 0 && c.High)
                    {
                        Greybox.Box("TvFrame", hall, pillar, W(x - side * 0.3, 3.6, z), new Vector3(0.08f, 0.62f, 1.02f), false);
                        Greybox.TexturedBox("Tv", hall, screen, W(x - side * 0.35, 3.6, z), new Vector3(0.02f, 0.54f, 0.94f), new Vector2(0.94f, 0.54f), false);
                    }
                }
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("BalconyRail", hall, c.Steel, W(side * (HallX - 0.6), 6.2, midZ), new Vector3(0.15f, 0.15f, len), false);
                for (float z = HallFront + 1f; z < back; z += 1.6f)
                    lamps.Box(W(side * (HallX - 0.6), 6.38, z), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity);
            }

            const float spring = HallWallH, rise = 11f;
            const int SEG = 22;
            var truss = new MeshMerge();
            var skin = new MeshMerge();
            System.Func<float, float, Vector3> arch = (t, scale) =>
            {
                float a = Mathf.PI * t;
                return new Vector3(-Mathf.Cos(a) * HallX * scale, spring + Mathf.Sin(a) * rise * scale, 0);
            };
            for (float z = HallFront + 2f; z <= back - 1f; z += 4f)
            {
                Vector3 o = new Vector3(0, 0, z);
                for (int i = 0; i < SEG; i++)
                {
                    float t0 = i / (float)SEG, t1 = (i + 1) / (float)SEG;
                    Vector3 a0 = arch(t0, 1f) + o, a1 = arch(t1, 1f) + o, b0 = arch(t0, 0.93f) + o, b1 = arch(t1, 0.93f) + o;
                    b0.y += 0.5f; b1.y += 0.5f;
                    truss.Beam(a0, a1, 0.16f, 0.16f);
                    truss.Beam(b0, b1, 0.16f, 0.16f);
                    truss.Beam(a0, b0, 0.08f, 0.08f);
                    if (i % 4 == 2) lamps.Box((a0 + b0) * 0.5f + Vector3.down * 0.35f, new Vector3(0.22f, 0.22f, 0.22f), Quaternion.identity);
                }
            }
            for (int i = 1; i < SEG; i += 3)
            {
                float t = i / (float)SEG;
                truss.Beam(arch(t, 1f) + new Vector3(0, 0, HallFront), arch(t, 1f) + new Vector3(0, 0, back), 0.12f, 0.12f);
            }
            for (int i = 0; i < SEG; i++)
            {
                Vector3 p0 = arch(i / (float)SEG, 1f) + Vector3.up * 0.4f, p1 = arch((i + 1) / (float)SEG, 1f) + Vector3.up * 0.4f;
                skin.Quad(p0 + new Vector3(0, 0, HallFront), p1 + new Vector3(0, 0, HallFront), p1 + new Vector3(0, 0, back), p0 + new Vector3(0, 0, back));
            }
            truss.Build("RoofTrusses", hall, c.Steel);
            skin.Build("RoofGlass", hall, roofSkin);
            lamps.Build("WarmLamps", hall, warmLamp);
        }

        /// <summary>The broadcast's high camera behind the back stand, rendered small into the screens.</summary>
        static RenderTexture LiveFeed(Transform venue)
        {
            var rt = new RenderTexture(640, 360, 16) { name = "LiveFeed" };
            var cam = new GameObject("LiveFeedCamera").AddComponent<Camera>();
            cam.transform.SetParent(venue, false);
            cam.targetTexture = rt;
            // Batch 1 (2026-10-26): it drew the whole hall again every frame - half of all the drawing. Now a dozen frames a
            // second, without the crowd (the screens are small; nobody counts the heads in them).
            cam.enabled = false;
            cam.cullingMask &= ~(1 << CrowdBuilder.Layer);
            cam.gameObject.AddComponent<TimedCamera>().Fps = 12f;
            cam.fieldOfView = 46f;
            cam.depth = -5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Greybox.Hex(0x07080b);
            cam.farClipPlane = Courts.Current.FarClip;
            cam.transform.position = W(0, 6.8, LENGTH + 10.5);
            cam.transform.LookAt(W(0, 0.8, LENGTH * 0.42));
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.SetRenderer(GraphicsTier.LiteRenderer);
            LiveCamera = cam;
            return rt;
        }

        /// <summary>The live feed's camera (High only, courts with screens).</summary>
        public static Camera LiveCamera { get; private set; }

        static Material _bench;
        static Material BenchMat(GreyboxPalette palette)
        {
            if (_bench == null) { _bench = palette.LitColor(Greybox.Hex(0x12203a)); _bench.SetFloat("_Smoothness", 0.4f); }   // dark navy seats
            return _bench;
        }

        /// <summary>The crowd's motion (null with the venue off).</summary>
        public static CrowdMotion Motion { get; private set; }

        /// <summary>The crowd reacts: 0..1 (CrowdAudio's applause, cheers, oohs).</summary>
        public static void Excite(float level) { if (Motion != null) Motion.Excite(level); }

        static Material LedMat(GreyboxPalette palette, string id, string text, Color ink, Color back, float glow)
        {
            var m = palette.UnlitColor(new Color(glow, glow, glow));
            m.mainTexture = ProcTex.Led(id, text, ink, back);
            return m;
        }

        /// <summary>The broadcast grade: neutral tonemapping, a soft bloom on the lamps and boards, a light vignette.</summary>
        static void Grade(Transform venue)
        {
            var go = new GameObject("BroadcastGrade");
            go.transform.SetParent(venue, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "BroadcastGrade";
            bool night = NightFor(Courts.Current.Id, out _);                      // batch 1: punchier at night - the glow of the rig and screens
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(night ? 0.9f : 1.0f); bloom.intensity.Override(night ? 0.8f : 0.55f); bloom.scatter.Override(0.66f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(night ? 0.34f : 0.24f); vig.smoothness.Override(0.45f);
            var col = profile.Add<ColorAdjustments>(true);
            col.contrast.Override(night ? 18f : 10f); col.saturation.Override(night ? 12f : 8f); col.postExposure.Override(0.15f);
            vol.sharedProfile = profile;
        }

        /// <summary>Post-processing, anti-aliasing and how far the camera sees, for the court in use.</summary>
        public static void SetUpCamera(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            bool high = Detail >= 2;
            data.renderPostProcessing = high && Post;
            data.SetRenderer(high && Post ? 0 : GraphicsTier.LiteRenderer);   // batch 1: ambient occlusion (an extra pass of everything) on High only
            data.antialiasing = high && Post ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            if (Detail > 0) cam.backgroundColor = Greybox.Hex(0x07080b);
            cam.farClipPlane = Detail > 0 ? Mathf.Max(120f, Courts.Current.FarClip) : 120f;
        }
    }
}
