// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.cs
// The arena around the court (presentation pass PR1): a dark arena floor,
// raked stands full of people on three sides and behind the camera, LED
// ad boards, a big screen over the front wall, a lighting gantry with
// spots over the court, fog that lets the stands fall away into the dark,
// and the broadcast grade (tonemapping, bloom, vignette).
//
// Built from CourtDimensions through SimToWorld like CourtView, so it
// always fits the court. Renders only: nothing here is read by the sim.
// Settings > Video > Venue: Off (the old greybox), Low (venue, no extra
// lights or grade), High (everything).
// 2026-10-23, after a Canary Wharf Classic photo: a dark, warm hall - wood
// panelled walls, pillars with TVs, a string of warm lights, an arched steel
// roof with amber lamps; the crowd close and low (flat rows by the glass,
// then the rake) in darker clothes; the big screen shows the match live.
// ===========================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public static class VenueView
    {
        /// <summary>Settings > Video > Venue: 0 off, 1 low, 2 high.</summary>
        public static int Detail = 2;

        const float ROW_DEPTH = 0.85f, ROW_RISE = 0.42f, CROWD_H = 0.95f;
        /// <summary>The first rows sit on the floor right by the glass; the rake starts behind them.</summary>
        const int FlatRows = 3;
        static float Tread(int i) { return i < FlatRows ? 0.04f : 0.5f + (i - FlatRows) * ROW_RISE; }
        const float HallX = 18.5f, HallFront = -24f, HallBackFromLength = 22f, HallWallH = 10f;
        static readonly Color Steel = Greybox.Hex(0x2a2e36);
        static readonly Color Dark = Greybox.Hex(0x12151b);

        static Vector3 W(double x, double y, double z) { return SimToWorld.Pos(x, y, z); }

        public static void Build(Transform root, GreyboxPalette palette)
        {
            if (Detail <= 0) return;
            var venue = new GameObject("Venue").transform;
            venue.SetParent(root, false);
            bool high = Detail >= 2;

            var floor = palette.LitColor(Greybox.Hex(0x1a1e26)); floor.SetFloat("_Smoothness", 0.55f);
            var steel = palette.LitColor(Steel); steel.SetFloat("_Smoothness", 0.45f);
            var blue = palette.LitColor(Greybox.Hex(0x1d5fb8)); blue.SetFloat("_Smoothness", 0.6f);
            var dark = palette.LitColor(Dark);
            var seat = palette.LitColor(Greybox.Hex(0x1c2029));
            // The people: 3D at High (CrowdBuilder), painted cards at Low.
            CrowdBuilder.ResetCount();
            var people = high ? new CrowdBuilder.Palette(palette) : null;
            Motion = venue.gameObject.AddComponent<CrowdMotion>();
            var crowd = new Material[4];
            for (int i = 0; i < crowd.Length; i++)
            {
                crowd[i] = palette.LitColor(new Color(0.62f, 0.62f, 0.64f));
                crowd[i].mainTexture = ProcTex.Crowd(i);
                crowd[i].SetFloat("_Smoothness", 0f);
            }

            float L = (float)LENGTH, HW = (float)HALF_W, H = (float)CEILING_H;

            // ---- arena floor (just below the court's floor) ----
            Greybox.Box("ArenaFloor", venue, floor, W(0, -0.07, L / 2), new Vector3(46f, 0.1f, 52f));

            // ---- the court's shell: dark cladding outside the walls, a steel coping along the top ----
            foreach (int side in new[] { -1, 1 })
            {
                // Glass side walls now (PSA court): no cladding, a blue frame along the top and a blue plinth along the foot.
                Greybox.Box("SideCoping", venue, blue, W(side * (HW + 0.03), H + 0.05, L / 2), new Vector3(0.12f, 0.1f, L + 0.1f));
                Greybox.Box("SidePlinth", venue, blue, W(side * (HW + 0.2), 0.08, L / 2), new Vector3(0.36f, 0.18f, L + 0.4f));
                // Glass fins at the back corners, full height of the glass.
                Greybox.Box("CornerPost", venue, blue, W(side * (HW + 0.08), BACK_WALL_OUT_H / 2 + 0.02, L + 0.02), new Vector3(0.14f, (float)BACK_WALL_OUT_H + 0.04f, 0.1f));
            }
            // (no front cladding: the front wall is glass, the crowd behind it shows through)
            Greybox.Box("FrontCoping", venue, blue, W(0, H + 0.05, -0.06), new Vector3(2 * HW + 0.5f, 0.1f, 0.36f));

            // ---- stands ----
            // Sides: rows rising away from the court, front to well behind the camera.
            foreach (int side in new[] { -1, 1 })
                Stand(venue, seat, high ? new[] { BenchMat(palette) } : crowd, people, Motion, side < 0 ? "StandLeft" : "StandRight", 14,
                    i => W(side * (HW + 1.5 + i * ROW_DEPTH), 0, L / 2 + 2.5), new Vector3(0, 0, 1), side < 0 ? Vector3.left : Vector3.right, 34f, side + 5);
            // Behind the front wall: only the upper rows show over the wall from the TV camera.
            Stand(venue, seat, high ? new[] { BenchMat(palette) } : crowd, people, Motion, "StandFront", 22, i => W(0, 0, -2.4 - i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.forward, 2 * HW + 2.6f, 2);
            // Behind the camera (seen in the menus' reverse angles).
            Stand(venue, seat, high ? new[] { BenchMat(palette) } : crowd, people, Motion, "StandBack", 14, i => W(0, 0, L + 7.8 + i * ROW_DEPTH), new Vector3(1, 0, 0), Vector3.back, 2 * HW + 2.6f, 3);

            // (no LED boards: the show-court look keeps the court the only bright thing at floor level)

            // ---- the gallery mat behind the glass, edged in blue ----
            var mat = palette.LitColor(Greybox.Hex(0x0f1d36)); mat.SetFloat("_Smoothness", 0.35f);
            var edge = palette.LitColor(Greybox.Hex(0x1d5fb8));
            float mw = 2 * HW + 1.6f, md = 4.6f, mz = L + 0.12f + md / 2;
            Greybox.Box("GalleryMat", venue, mat, W(0, -0.005, mz), new Vector3(mw, 0.02f, md), false);
            Greybox.Box("GalleryEdge", venue, edge, W(0, 0.006, mz + md / 2 - 0.04), new Vector3(mw, 0.004f, 0.06f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GalleryEdge", venue, edge, W(side * (mw / 2 - 0.03), 0.006, mz), new Vector3(0.06f, 0.004f, md), false);

            // ---- the big screen over the front wall: the match, live (High), or the event's mark ----
            var screenMat = palette.UnlitColor(new Color(1.1f, 1.1f, 1.1f));
            screenMat.mainTexture = high ? LiveFeed(venue) : (Texture)ProcTex.Screen();
            Greybox.Box("ScreenFrame", venue, steel, W(0, 7.35, -1.35), new Vector3(4.3f, 2.5f, 0.2f), false);
            Greybox.TexturedBox("Screen", venue, screenMat, W(0, 7.35, -1.22), new Vector3(4.0f, 2.25f, 0.04f), new Vector2(4.0f, 2.25f), false);
            Greybox.Box("ScreenCable", venue, steel, W(-1.8, 9.9, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);
            Greybox.Box("ScreenCable", venue, steel, W(1.8, 9.9, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);

            // ---- the hall: wood-panelled walls, pillars with TVs, warm lights, an arched steel roof ----
            Hall(venue, palette, screenMat, steel, high);

            // ---- the lighting gantry ----
            var lamp = palette.UnlitColor(high ? new Color(3.2f, 3.1f, 2.9f) : new Color(1.4f, 1.4f, 1.35f));
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GantryRail", venue, steel, W(side * 2.2, 7.25, L / 2 + 0.7), new Vector3(0.18f, 0.22f, L - 2.0f), false);
            for (int k = 0; k < 4; k++)
            {
                float z = 2.0f + k * (L - 2.6f) / 3f;                          // clear of the big screen
                Greybox.Box("GantryCross", venue, steel, W(0, 7.4, z), new Vector3(4.8f, 0.14f, 0.14f), false);
                foreach (int side in new[] { -1, 1 })
                {
                    Greybox.Box("LampHousing", venue, dark, W(side * 2.2, 7.05, z), new Vector3(1.0f, 0.18f, 0.55f), false);
                    Greybox.Box("Lamp", venue, lamp, W(side * 2.2, 6.95, z), new Vector3(0.86f, 0.03f, 0.42f), false);
                }
            }

            // ---- light ----
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.29f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.24f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.11f);
            RenderSettings.reflectionIntensity = 0.3f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Greybox.Hex(0x0f0b09);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 80f;

            if (high)
            {
                // Spots under the gantry's lamps: the court gets the light, the stands don't.
                for (int k = 0; k < 4; k++)
                {
                    float z = 0.6f + k * (L - 1.2f) / 3f;
                    foreach (int side in new[] { -1, 1 })
                    {
                        var spot = new GameObject("CourtSpot").AddComponent<Light>();
                        spot.transform.SetParent(venue, false);
                        spot.type = LightType.Spot;
                        spot.color = new Color(1f, 0.97f, 0.92f);
                        spot.spotAngle = 92f;
                        spot.innerSpotAngle = 40f;
                        spot.range = 13f;
                        spot.intensity = 17f;
                        spot.shadows = LightShadows.None;
                        spot.transform.position = W(side * 1.7, 6.9, z);
                        spot.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    }
                }
                // The hall's warm light: amber pools on the wood and the roof, the stands left dim.
                foreach (int side in new[] { -1, 1 })
                    for (int k = 0; k < 3; k++)
                    {
                        var warm = new GameObject("HallWarm").AddComponent<Light>();
                        warm.transform.SetParent(venue, false);
                        warm.type = LightType.Point;
                        warm.color = new Color(1f, 0.68f, 0.38f);
                        warm.range = 16f;
                        warm.intensity = 2.2f;
                        warm.transform.position = W(side * (HallX - 3f), 9.5, -12 + k * 16);
                    }
                Grade(venue);
            }
        }

        /// <summary>Raked rows of seats with people on them. rowPos(i) is the row's front edge at floor level
        /// (centre along its length); along is the row's length axis, facing points at the court. At High the
        /// people are 3D (CrowdBuilder) on benches; at Low they are painted cards.</summary>
        static void Stand(Transform venue, Material seat, Material[] crowd, CrowdBuilder.Palette people, CrowdMotion motion, string name, int rows,
            System.Func<int, Vector3> rowPos, Vector3 along, Vector3 facing, float length, int seed)
        {
            var stand = new GameObject(name).transform;
            stand.SetParent(venue, false);
            Vector3 back = -facing;
            bool alongX = Mathf.Abs(along.x) > 0.5f;
            for (int i = 0; i < rows; i++)
            {
                Vector3 p = rowPos(i);
                float top = Tread(i);
                // The tier: one block from the floor to this row's tread (rows behind stack higher).
                Vector3 size = alongX ? new Vector3(length, top, ROW_DEPTH) : new Vector3(ROW_DEPTH, top, length);
                if (top > 0.1f) Greybox.Box("Tier", stand, seat, p + back * (ROW_DEPTH / 2) + Vector3.up * (top / 2), size, false);
                if (people != null)
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
            if (people != null)
                motion.Add(CrowdBuilder.Stand(stand, people, name, rows, rowPos, Tread, along, facing, length, seed));
        }

        /// <summary>The hall around the stands. Walls in warm wood with dark pillars (a TV on every other one), a line
        /// of warm lamps along a balcony rail, and over it all a vault of steel ladder trusses with amber lamps.</summary>
        static void Hall(Transform venue, GreyboxPalette palette, Material screen, Material steel, bool high)
        {
            float L = (float)LENGTH, back = L + HallBackFromLength, len = back - HallFront, midZ = (back + HallFront) / 2;
            var wood = palette.LitColor(Color.white); wood.mainTexture = ProcTex.Wood(); wood.SetFloat("_Smoothness", 0.35f);
            var pillar = palette.LitColor(Greybox.Hex(0x1b1d22)); pillar.SetFloat("_Smoothness", 0.5f);
            var warmLamp = palette.UnlitColor(new Color(2.8f, 1.7f, 0.8f));
            var roofSkin = palette.UnlitColor(Greybox.Hex(0x05070b));
            var hall = new GameObject("Hall").transform; hall.SetParent(venue, false);

            // Walls (wood), then the dark above the end walls up to the roof.
            foreach (int side in new[] { -1, 1 })
                Greybox.TexturedBox("HallWall", hall, wood, W(side * HallX, HallWallH / 2, midZ), new Vector3(0.3f, HallWallH, len), new Vector2(2.4f, 2.4f));
            Greybox.TexturedBox("HallWall", hall, wood, W(0, HallWallH / 2, HallFront), new Vector3(2 * HallX, HallWallH, 0.3f), new Vector2(2.4f, 2.4f));
            Greybox.TexturedBox("HallWall", hall, wood, W(0, HallWallH / 2, back), new Vector3(2 * HallX, HallWallH, 0.3f), new Vector2(2.4f, 2.4f));
            Greybox.Box("HallEndFront", hall, roofSkin, W(0, HallWallH + 6, HallFront - 0.2), new Vector3(2 * HallX, 12f, 0.2f), false);
            Greybox.Box("HallEndBack", hall, roofSkin, W(0, HallWallH + 6, back + 0.2), new Vector3(2 * HallX, 12f, 0.2f), false);

            // Pillars along the side walls, a small TV on every other one; the balcony rail and its lamps.
            var lamps = new MeshMerge();
            int k = 0;
            for (float z = HallFront + 3f; z < back - 2f; z += 6f, k++)
                foreach (int side in new[] { -1, 1 })
                {
                    float x = side * (HallX - 0.35f);
                    Greybox.Box("Pillar", hall, pillar, W(x, HallWallH / 2, z), new Vector3(0.5f, HallWallH, 0.5f), false);
                    if (k % 2 == 0 && high)
                    {
                        Greybox.Box("TvFrame", hall, pillar, W(x - side * 0.3, 3.6, z), new Vector3(0.08f, 0.62f, 1.02f), false);
                        Greybox.TexturedBox("Tv", hall, screen, W(x - side * 0.35, 3.6, z), new Vector3(0.02f, 0.54f, 0.94f), new Vector2(0.94f, 0.54f), false);
                    }
                }
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("BalconyRail", hall, steel, W(side * (HallX - 0.6), 6.2, midZ), new Vector3(0.15f, 0.15f, len), false);
                for (float z = HallFront + 1f; z < back; z += 1.6f)
                    lamps.Box(W(side * (HallX - 0.6), 6.38, z), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity);
            }

            // The vault: elliptical ladder trusses across the hall every 4 m, purlins along it, dark glass over it.
            const float spring = HallWallH, rise = 11f;
            const int SEG = 22;
            var truss = new MeshMerge();
            var skin = new MeshMerge();
            System.Func<float, float, Vector3> arch = (t, scale) =>                               // t: 0..1 across the hall
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
                    truss.Beam(a0, b0, 0.08f, 0.08f);                                                // the ladder's rungs
                    if (i % 4 == 2) lamps.Box((a0 + b0) * 0.5f + Vector3.down * 0.35f, new Vector3(0.22f, 0.22f, 0.22f), Quaternion.identity);
                }
            }
            for (int i = 1; i < SEG; i += 3)                                                          // purlins along the hall
            {
                float t = i / (float)SEG;
                truss.Beam(arch(t, 1f) + new Vector3(0, 0, HallFront), arch(t, 1f) + new Vector3(0, 0, back), 0.12f, 0.12f);
            }
            for (int i = 0; i < SEG; i++)                                                             // the dark glass over it
            {
                Vector3 p0 = arch(i / (float)SEG, 1f) + Vector3.up * 0.4f, p1 = arch((i + 1) / (float)SEG, 1f) + Vector3.up * 0.4f;
                skin.Quad(p0 + new Vector3(0, 0, HallFront), p1 + new Vector3(0, 0, HallFront), p1 + new Vector3(0, 0, back), p0 + new Vector3(0, 0, back));
            }
            truss.Build("RoofTrusses", hall, steel);
            skin.Build("RoofGlass", hall, roofSkin);
            lamps.Build("WarmLamps", hall, warmLamp);
        }

        /// <summary>The broadcast's high camera behind the back stand, rendered small into the big screen and the TVs.</summary>
        static RenderTexture LiveFeed(Transform venue)
        {
            var rt = new RenderTexture(640, 360, 16) { name = "LiveFeed" };
            var cam = new GameObject("LiveFeedCamera").AddComponent<Camera>();
            cam.transform.SetParent(venue, false);
            cam.targetTexture = rt;
            cam.fieldOfView = 46f;
            cam.depth = -5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Greybox.Hex(0x07080b);
            cam.farClipPlane = 90f;
            cam.transform.position = W(0, 6.8, LENGTH + 10.5);
            cam.transform.LookAt(W(0, 0.8, LENGTH * 0.42));
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            LiveCamera = cam;
            return rt;
        }

        /// <summary>The live feed's camera (High only).</summary>
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
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.62f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f); vig.smoothness.Override(0.45f);
            var col = profile.Add<ColorAdjustments>(true);
            col.contrast.Override(10f); col.saturation.Override(8f); col.postExposure.Override(0.15f);
            vol.sharedProfile = profile;
        }

        /// <summary>Post-processing and anti-aliasing on the game's camera when the grade is in use.</summary>
        public static void SetUpCamera(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            bool high = Detail >= 2;
            data.renderPostProcessing = high;
            data.antialiasing = high ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            if (Detail > 0) cam.backgroundColor = Greybox.Hex(0x07080b);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 120f);
        }
    }
}
