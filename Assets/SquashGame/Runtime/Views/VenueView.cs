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

            // ---- LED ad boards along the side stands' front ----
            var ledLime = LedMat(palette, "lime", "RE:SQUASHSTRIKE  •  WORLD TOUR  •", Greybox.Hex(0xb8ee26), new Color(0.02f, 0.025f, 0.03f), high ? 1.9f : 1.2f);
            var ledWhite = LedMat(palette, "white", "GLASS COURT SERIES  •  PLAY ON  •", new Color(0.95f, 0.96f, 1f), new Color(0.02f, 0.03f, 0.06f), high ? 1.5f : 1.0f);
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.TexturedBox("LedSide", venue, side < 0 ? ledLime : ledWhite, W(side * (HW + 1.15), 0.45, L / 2 - 1.0), new Vector3(0.08f, 0.9f, L + 3f), new Vector2(14.4f, 0.9f), false);
            }

            // ---- the gallery mat behind the glass, edged in lime ----
            var mat = palette.LitColor(Greybox.Hex(0x143a74)); mat.SetFloat("_Smoothness", 0.35f);
            var edge = palette.UnlitColor(Greybox.Hex(0xb8ee26));
            float mw = 2 * HW + 1.6f, md = 4.6f, mz = L + 0.12f + md / 2;
            Greybox.Box("GalleryMat", venue, mat, W(0, -0.005, mz), new Vector3(mw, 0.02f, md), false);
            Greybox.Box("GalleryEdge", venue, edge, W(0, 0.006, mz + md / 2 - 0.04), new Vector3(mw, 0.004f, 0.06f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GalleryEdge", venue, edge, W(side * (mw / 2 - 0.03), 0.006, mz), new Vector3(0.06f, 0.004f, md), false);

            // ---- the big screen over the front wall ----
            var screenMat = palette.UnlitColor(new Color(1.15f, 1.15f, 1.15f));
            screenMat.mainTexture = ProcTex.Screen();
            Greybox.Box("ScreenFrame", venue, steel, W(0, 7.25, -1.35), new Vector3(7.6f, 2.1f, 0.2f), false);
            Greybox.TexturedBox("Screen", venue, screenMat, W(0, 7.25, -1.22), new Vector3(7.2f, 1.8f, 0.04f), new Vector2(7.2f, 1.8f), false);
            Greybox.Box("ScreenCable", venue, steel, W(-3.0, 9.6, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);
            Greybox.Box("ScreenCable", venue, steel, W(3.0, 9.6, -1.35), new Vector3(0.03f, 2.6f, 0.03f), false);

            // ---- the hall: roof and end walls, lost in the dark ----
            var hall = palette.UnlitColor(Greybox.Hex(0x07080b));
            Greybox.Box("Roof", venue, hall, W(0, 17, L / 2), new Vector3(60f, 0.2f, 70f), false);
            Greybox.Box("HallFront", venue, hall, W(0, 8, -27), new Vector3(60f, 18f, 0.2f), false);
            Greybox.Box("HallBack", venue, hall, W(0, 8, L + 27), new Vector3(60f, 18f, 0.2f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("HallSide", venue, hall, W(side * 25, 8, L / 2), new Vector3(0.2f, 18f, 70f), false);

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
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.37f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.22f);
            RenderSettings.reflectionIntensity = 0.3f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Greybox.Hex(0x0b0d12);
            RenderSettings.fogStartDistance = 19f;
            RenderSettings.fogEndDistance = 52f;

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
                        spot.intensity = 14f;
                        spot.shadows = LightShadows.None;
                        spot.transform.position = W(side * 1.7, 6.9, z);
                        spot.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    }
                }
                // Accent: a cool wash on the side stands and a lime glow off the boards.
                foreach (int side in new[] { -1, 1 })
                {
                    var wash = new GameObject("StandWash").AddComponent<Light>();
                    wash.transform.SetParent(venue, false);
                    wash.type = LightType.Point;
                    wash.color = new Color(0.45f, 0.6f, 1f);
                    wash.range = 12f;
                    wash.intensity = 1.6f;
                    wash.transform.position = W(side * (HW + 6.5), 6.5, L / 2);
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
                float top = 0.5f + i * ROW_RISE;
                // The tier: one block from the floor to this row's tread (rows behind stack higher).
                Vector3 size = alongX ? new Vector3(length, top, ROW_DEPTH) : new Vector3(ROW_DEPTH, top, length);
                Greybox.Box("Tier", stand, seat, p + back * (ROW_DEPTH / 2) + Vector3.up * (top / 2), size, false);
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
                motion.Add(CrowdBuilder.Stand(stand, people, name, rows, rowPos, i => 0.5f + i * ROW_RISE, along, facing, length, seed));
        }

        static Material _bench;
        static Material BenchMat(GreyboxPalette palette)
        {
            if (_bench == null) { _bench = palette.LitColor(Greybox.Hex(0x1f4f9a)); _bench.SetFloat("_Smoothness", 0.4f); }   // blue seats
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
