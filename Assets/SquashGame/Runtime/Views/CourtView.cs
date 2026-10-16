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

        public void Build(GreyboxPalette palette)
        {
            Transform root = transform;
            var wall = palette.LitColor(Greybox.Hex(0xd9d9d4));
            var floor = palette.LitColor(Greybox.Hex(0xc2b79a));
            var tin = palette.LitColor(Greybox.Hex(0x4a4a52));
            var line = palette.UnlitColor(Greybox.Hex(0xd8382f));
            var glass = palette.Clear(Greybox.Hex(0x9fd4e8, 0.14f));
            var glassEdge = palette.UnlitColor(Greybox.Hex(0x5fa8c4));

            float W = (float)(HALF_W * 2), L = (float)LENGTH, H = (float)CEILING_H;

            // ---- floor (top surface at y = 0) ----
            Greybox.Box("Floor", root, floor, SimToWorld.Pos(0, -0.05, LENGTH / 2), new Vector3(W, 0.1f, L));

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
            Greybox.Box("FrontWall", root, wall, SimToWorld.Pos(0, CEILING_H / 2, -0.05), new Vector3(W, H, 0.1f));
            // Tin: a board standing proud of the wall so the 0.48 m surface-class change is visible.
            Greybox.Box("Tin", root, tin, SimToWorld.Pos(0, TIN_H / 2, 0.02), new Vector3(W, (float)TIN_H, 0.04f));
            Greybox.Box("TinLine", root, line, SimToWorld.Pos(0, TIN_H, 0.022), new Vector3(W, 0.05f, 0.045f), false);
            Greybox.Box("ServiceLine", root, line, SimToWorld.Pos(0, SERVICE_LINE_H, 0.006), new Vector3(W, 0.05f, 0.01f), false);
            Greybox.Box("FrontOutLine", root, line, SimToWorld.Pos(0, FRONT_WALL_OUT_H, 0.006), new Vector3(W, 0.05f, 0.01f), false);

            // ---- side walls (faces at x = +-HALF_W) with the sloping out line ----
            foreach (int side in new[] { -1, 1 })
            {
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

            // ---- lighting (lab: key + fill + ambient) ----
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.LookRotation(SimToWorld.Pos(0, 0, 4) - SimToWorld.Pos(1.5, 7.5, 3.0));

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(SimToWorld.Pos(0, 0, 4) - SimToWorld.Pos(-2.0, 5.0, 8.0));
        }
    }
}
