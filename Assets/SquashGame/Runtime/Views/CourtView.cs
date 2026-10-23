// ===========================================================
// Assets/SquashGame/Runtime/Views/CourtView.cs
// The court AS GEOMETRY (port of lab/court/CourtView.js). Renders only.
// Every dimension comes from CourtDimensions - the same numbers the
// physics planes use - through SimToWorld, so the drawn court and the
// collision court cannot drift apart.
// ===========================================================

using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public sealed class CourtView : MonoBehaviour
    {
        const float LINE_W = 0.05f;
        const float FLOOR_LINE_Y = 0.004f;

        GreyboxPalette _palette;
        int _builtDetail = -1;

        public void Build(GreyboxPalette palette)
        {
            var total = System.Diagnostics.Stopwatch.StartNew();
            _palette = palette;
            _builtDetail = VenueView.Detail;
            Transform root = transform;
            bool dressed = VenueView.Detail > 0;      // PR1: the real court's surfaces and the arena around it
            var wall = palette.LitColor(Greybox.Hex(0xd9d9d4));
            var floor = palette.LitColor(Greybox.Hex(0xc2b79a));
            var tin = palette.LitColor(Greybox.Hex(0x4a4a52));
            Material frontWall = wall, sideWall = wall;
            if (dressed)
            {
                floor = palette.LitColor(new Color(0.84f, 0.80f, 0.76f)); floor.mainTexture = ProcTex.Floor(); floor.SetFloat("_Smoothness", 0.46f);
                frontWall = palette.LitColor(Color.white); frontWall.mainTexture = ProcTex.Wall(true); frontWall.SetFloat("_Smoothness", 0.12f);
                // PSA show court: blue-tinted glass side and back walls, a frosted front wall (2026-10-23)
                // The front wall: a show court's deep blue, branded along the top, lines in white (the Canary Wharf look,
                // 2026-10-23). The ball's light rim keeps it readable against the blue.
                frontWall = palette.LitColor(Color.white); frontWall.mainTexture = ProcTex.FrontWall(); frontWall.SetFloat("_Smoothness", 0.6f);
                sideWall = palette.Clear(new Color(0.42f, 0.66f, 0.92f, 0.26f));
                tin = palette.LitColor(Color.white); tin.mainTexture = ProcTex.Tin(); tin.SetFloat("_Smoothness", 0.5f);
            }
            var line = palette.UnlitColor(dressed ? Greybox.Hex(0xeef3ff) : Greybox.Hex(0xd8382f));
            var glass = dressed ? palette.Clear(new Color(0.42f, 0.66f, 0.92f, 0.16f)) : palette.Clear(Greybox.Hex(0x9fd4e8, 0.14f));
            var glassEdge = palette.UnlitColor(dressed ? Greybox.Hex(0x3d8be0) : Greybox.Hex(0x5fa8c4));

            float W = (float)(HALF_W * 2), L = (float)LENGTH, H = (float)CEILING_H;

            // ---- floor (top surface at y = 0) ----
            if (dressed) Greybox.TexturedBox("Floor", root, floor, SimToWorld.Pos(0, -0.05, LENGTH / 2), new Vector3(W, 0.1f, L), new Vector2(1.4f, 2.8f));
            else Greybox.Box("Floor", root, floor, SimToWorld.Pos(0, -0.05, LENGTH / 2), new Vector3(W, 0.1f, L));

            // Short line, half-court line, service boxes.
            Greybox.Box("ShortLine", root, line, SimToWorld.Pos(0, FLOOR_LINE_Y, SHORT_LINE_Z), new Vector3(W, 0.004f, LINE_W), false);
            float back = (float)(LENGTH - SHORT_LINE_Z);
            Greybox.Box("HalfCourtLine", root, line, SimToWorld.Pos(0, FLOOR_LINE_Y, SHORT_LINE_Z + back / 2),
                new Vector3(LINE_W, 0.004f, back), false);
            foreach (int side in new[] { -1, 1 })
            {
                double innerX = side * (HALF_W - SERVICE_BOX);
                Greybox.Box("ServiceBoxInner", root, line, SimToWorld.Pos(innerX, FLOOR_LINE_Y, SHORT_LINE_Z + SERVICE_BOX / 2),
                    new Vector3(LINE_W, 0.004f, (float)SERVICE_BOX), false);
                Greybox.Box("ServiceBoxBack", root, line, SimToWorld.Pos(side * (HALF_W - SERVICE_BOX / 2), FLOOR_LINE_Y, SHORT_LINE_Z + SERVICE_BOX),
                    new Vector3((float)SERVICE_BOX, 0.004f, LINE_W), false);
            }

            // ---- front wall (playing face at z = 0) ----
            if (dressed) Greybox.TexturedBox("FrontWall", root, frontWall, SimToWorld.Pos(0, CEILING_H / 2, -0.05), new Vector3(W, H, 0.1f), new Vector2(W, H));
            else Greybox.Box("FrontWall", root, wall, SimToWorld.Pos(0, CEILING_H / 2, -0.05), new Vector3(W, H, 0.1f));
            // Tin: a board standing proud of the wall so the 0.48 m surface-class change is visible.
            if (dressed) Greybox.TexturedBox("Tin", root, tin, SimToWorld.Pos(0, TIN_H / 2, 0.02), new Vector3(W, (float)TIN_H, 0.04f), new Vector2(W, (float)TIN_H));
            else Greybox.Box("Tin", root, tin, SimToWorld.Pos(0, TIN_H / 2, 0.02), new Vector3(W, (float)TIN_H, 0.04f));
            Greybox.Box("TinLine", root, line, SimToWorld.Pos(0, TIN_H, 0.022), new Vector3(W, 0.05f, 0.045f), false);
            Greybox.Box("ServiceLine", root, line, SimToWorld.Pos(0, SERVICE_LINE_H, 0.006), new Vector3(W, 0.05f, 0.01f), false);
            Greybox.Box("FrontOutLine", root, line, SimToWorld.Pos(0, FRONT_WALL_OUT_H, 0.006), new Vector3(W, 0.05f, 0.01f), false);

            // ---- side walls (faces at x = +-HALF_W) with the sloping out line ----
            foreach (int side in new[] { -1, 1 })
            {
                if (dressed)
                {
                    Greybox.Box(side < 0 ? "SideWallLeft" : "SideWallRight", root, sideWall,
                        SimToWorld.Pos(side * (HALF_W + 0.012), CEILING_H / 2, LENGTH / 2), new Vector3(0.024f, H, L), false);
                    // The glass panels' joints: slim blue fittings, so the wall reads as a wall at any angle.
                    for (int k = 1; k < 6; k++)
                        Greybox.Box("GlassJoint", root, glassEdge, SimToWorld.Pos(side * (HALF_W + 0.02), CEILING_H / 2, LENGTH * k / 6.0),
                            new Vector3(0.03f, H, 0.025f), false);
                    Greybox.Box("GlassFrameFront", root, glassEdge, SimToWorld.Pos(side * (HALF_W + 0.02), CEILING_H / 2, 0.02), new Vector3(0.04f, H, 0.04f), false);
                }
                else
                    Greybox.Box(side < 0 ? "SideWallLeft" : "SideWallRight", root, wall,
                        SimToWorld.Pos(side * (HALF_W + 0.05), CEILING_H / 2, LENGTH / 2), new Vector3(0.1f, H, L));

                var slope = Greybox.Primitive(PrimitiveType.Cube, "SideOutLine", root, line, false);
                double x = side * (HALF_W - 0.008);
                Greybox.PlaceBar(slope.transform,
                    SimToWorld.Pos(x, FRONT_WALL_OUT_H, 0), SimToWorld.Pos(x, BACK_WALL_OUT_H, LENGTH), 0.012f, 0.05f);
            }

            // ---- back wall (glass, only up to the out line) ----
            Greybox.Box("BackGlass", root, glass, SimToWorld.Pos(0, BACK_WALL_OUT_H / 2, LENGTH + 0.01),
                new Vector3(W, (float)BACK_WALL_OUT_H, 0.02f), false);
            Greybox.Box("BackOutLine", root, line, SimToWorld.Pos(0, BACK_WALL_OUT_H, LENGTH - 0.01), new Vector3(W, 0.05f, 0.012f), false);
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("GlassEdge", root, glassEdge, SimToWorld.Pos(side * (HALF_W - 0.01), BACK_WALL_OUT_H / 2, LENGTH),
                    new Vector3(0.02f, (float)BACK_WALL_OUT_H, 0.03f), false);

            // ---- lighting (lab: key + fill + ambient; the venue adds its own and takes over the ambient) ----
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.fog = false;
            bool lit = VenueView.Detail >= 2;

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.type = LightType.Directional;
            key.intensity = lit ? 0.85f : 1.1f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.LookRotation(SimToWorld.Pos(0, 0, 4) - SimToWorld.Pos(1.5, 7.5, 3.0));

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Directional;
            fill.intensity = lit ? 0.22f : 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(SimToWorld.Pos(0, 0, 4) - SimToWorld.Pos(-2.0, 5.0, 8.0));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            VenueView.Build(root, palette);
            Debug.Log("[venue] detail " + VenueView.Detail + " built in " + sw.ElapsedMilliseconds + " ms, " + CrowdBuilder.PeopleBuilt + " people");
            VenueView.SetUpCamera(Camera.main);
            Debug.Log("[venue] court and venue in " + total.ElapsedMilliseconds + " ms");
        }

        /// <summary>Settings > Video > Venue changed: rebuild the court (and the venue) in place.</summary>
        void Update()
        {
            if (_palette == null || _builtDetail == VenueView.Detail) return;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            Build(_palette);
        }
    }
}
