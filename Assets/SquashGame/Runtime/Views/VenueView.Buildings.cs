// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueView.Buildings.cs
// 2026-11-20 real 3D city buildings (the user: "in some of the court
// backgrounds it's just a flat drawing of buildings - I want actual 3D
// buildings, make them look good"): every skyline (City Rooftop, Neon
// City, the World Tour's cities) builds its towers here instead of one
// window-papered box each:
//   - a tower in one to three tiers, each set back from the one below,
//     a ledge at every step
//   - the faces turned to the court lit, the others in shade, a dark roof
//     - so they read as solid from every camera
//   - on the roof: an antenna with its red light, a water tower, plant
//     boxes; on a neon court a glowing crown round the top
// One mesh per tower (three materials), its roof bits merged. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public static partial class VenueView
    {
        /// <summary>A city's materials (one set per skyline) and its tower builder.</summary>
        sealed class Buildings
        {
            readonly Ctx _c;
            readonly Material[] _lit = new Material[4], _shade = new Material[4];
            readonly Material _roof, _ledge, _red;
            readonly Material[] _neon;
            readonly MeshMerge _roofBits = new MeshMerge(), _redBits = new MeshMerge();
            public readonly List<Material> Crowns = new List<Material>();
            readonly Dictionary<Material, MeshMerge> _crown = new Dictionary<Material, MeshMerge>();

            public Buildings(Ctx c, Color[] neon, float brightness = 1f)
            {
                _c = c;
                for (int i = 0; i < 4; i++)
                {
                    _lit[i] = c.Palette.UnlitColor(new Color(0.92f, 0.92f, 0.97f) * brightness); _lit[i].mainTexture = ProcTex.CityWindows(i);
                    _shade[i] = c.Palette.UnlitColor(new Color(0.40f, 0.42f, 0.50f) * brightness); _shade[i].mainTexture = ProcTex.CityWindows(i);
                }
                _roof = c.Palette.UnlitColor(new Color(0.05f, 0.055f, 0.07f));
                _ledge = c.Palette.UnlitColor(new Color(0.11f, 0.12f, 0.15f));
                _red = c.Palette.UnlitColor(new Color(3f, 0.2f, 0.15f));
                _neon = neon != null ? System.Array.ConvertAll(neon, n => c.Palette.UnlitColor(n)) : null;
            }

            /// <summary>A tower standing at foot (world, its base), w x d wide, h tall; tex picks the window pattern.</summary>
            public void Tower(Vector3 foot, float w, float d, float h, int tex, System.Random rng, bool aligned = false)
            {
                Vector3 toCourtW = W(0, 0, L / 2) - foot; toCourtW.y = 0; toCourtW.Normalize();
                // turned partway to the court, so two faces show (one lit, one in shade) - never flat-on
                float yaw = Mathf.Atan2(toCourtW.x, toCourtW.z) * Mathf.Rad2Deg + (rng.NextDouble() < 0.5 ? -1f : 1f) * (22f + (float)rng.NextDouble() * 18f);
                var rot = aligned ? Quaternion.identity : Quaternion.Euler(0, yaw, 0);   // aligned: square to the street (a block beside the hall)
                Vector3 toCourt = Quaternion.Inverse(rot) * toCourtW;          // in the tower's own frame
                System.Func<Vector3, Vector3> P = local => foot + rot * local;
                var lit = new List<int>(); var shade = new List<int>(); var top = new List<int>();
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>();
                // the tiers: base, then set back
                int tiers = h > 120f ? 3 : h > 55f ? 2 : 1;
                float[] share = tiers == 3 ? new[] { 0.55f, 0.30f, 0.15f } : tiers == 2 ? new[] { 0.7f, 0.3f } : new[] { 1f };
                float[] inset = { 1f, 0.76f, 0.52f };
                float y = 0f;
                for (int t = 0; t < tiers; t++)
                {
                    float tw = w * inset[t], td = d * inset[t], th = h * share[t];
                    Box(new Vector3(0, y + th / 2, 0), new Vector3(tw, th, td), toCourt, v, uv, n, lit, shade, top);
                    // the ledge at the step (a slab a little wider than the tier)
                    _roofBits.Box(P(new Vector3(0, y + th + 0.3f, 0)), new Vector3(tw + 1.0f, 0.6f, td + 1.0f), rot);
                    // the corner piers, standing proud of the glass
                    for (int k = 0; k < 4; k++)
                        _roofBits.Box(P(new Vector3(((k & 1) * 2 - 1) * tw / 2, y + th / 2, ((k >> 1) * 2 - 1) * td / 2)), new Vector3(1.4f, th, 1.4f), rot);
                    // a band every few floors
                    for (float by = 12.8f; by < th - 4f; by += 25.6f)
                        _roofBits.Box(P(new Vector3(0, y + by, 0)), new Vector3(tw + 0.5f, 0.45f, td + 0.5f), rot);
                    y += th + 0.6f;
                }
                var mesh = new Mesh { name = "Tower" };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
                mesh.subMeshCount = 3;
                mesh.SetTriangles(lit, 0); mesh.SetTriangles(shade, 1); mesh.SetTriangles(top, 2);
                mesh.RecalculateBounds();
                var go = new GameObject("Tower");
                go.transform.SetParent(_c.Venue, false);
                go.transform.localPosition = foot; go.transform.localRotation = rot;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { _lit[tex & 3], _shade[tex & 3], _roof };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;

                // the roof: what stands on it
                float topW = w * inset[tiers - 1], topD = d * inset[tiers - 1];
                Vector3 roof = P(new Vector3(0, y, 0));
                double pick = rng.NextDouble();
                if (h > 70f && pick < 0.45)
                {
                    float ah = 8f + h * 0.08f;                                       // an antenna and its warning light
                    _roofBits.Box(roof + Vector3.up * ah / 2, new Vector3(0.6f, ah, 0.6f), Quaternion.identity);
                    _redBits.Box(roof + Vector3.up * (ah + 0.6f), new Vector3(1.1f, 1.1f, 1.1f), Quaternion.identity);
                }
                else if (pick < 0.75)
                {
                    // a water tower on legs, off one corner
                    Vector3 wt = P(new Vector3(topW * 0.2f, y, -topD * 0.15f));
                    for (int k = 0; k < 4; k++) _roofBits.Box(wt + rot * new Vector3((k & 1) * 2.4f - 1.2f, 1.5f, (k >> 1) * 2.4f - 1.2f), new Vector3(0.3f, 3f, 0.3f), rot);
                    _roofBits.Box(wt + Vector3.up * 4.6f, new Vector3(3.6f, 3.2f, 3.6f), Quaternion.Euler(0, 45f, 0));
                    _roofBits.Box(wt + Vector3.up * 6.6f, new Vector3(2.6f, 0.8f, 2.6f), Quaternion.Euler(0, 45f, 0));
                }
                else
                {
                    int boxes = 2 + rng.Next(3);                                     // plant boxes
                    for (int k = 0; k < boxes; k++)
                    {
                        float bx = ((float)rng.NextDouble() - 0.5f) * topW * 0.6f, bz = ((float)rng.NextDouble() - 0.5f) * topD * 0.6f;
                        _roofBits.Box(P(new Vector3(bx, y + 1.2f, bz)), new Vector3(3f + (float)rng.NextDouble() * 3f, 2.4f, 2.5f + (float)rng.NextDouble() * 2f), rot);
                    }
                }
                // a neon crown round the top (a neon city)
                if (_neon != null && rng.NextDouble() < 0.5)
                {
                    var m = _neon[rng.Next(_neon.Length)];
                    if (!_crown.TryGetValue(m, out var mm)) { mm = new MeshMerge(); _crown[m] = mm; }
                    float cy = y - 0.9f;
                    mm.Box(P(new Vector3(0, cy, topD / 2 + 0.15f)), new Vector3(topW + 0.3f, 0.7f, 0.3f), rot);
                    mm.Box(P(new Vector3(0, cy, -topD / 2 - 0.15f)), new Vector3(topW + 0.3f, 0.7f, 0.3f), rot);
                    mm.Box(P(new Vector3(topW / 2 + 0.15f, cy, 0)), new Vector3(0.3f, 0.7f, topD + 0.3f), rot);
                    mm.Box(P(new Vector3(-topW / 2 - 0.15f, cy, 0)), new Vector3(0.3f, 0.7f, topD + 0.3f), rot);
                    // and a lit stripe up the corner that faces the court
                    if (rng.NextDouble() < 0.5)
                    {
                        float sh = h * share[0];
                        mm.Box(P(new Vector3(Mathf.Sign(toCourt.x) * (w / 2 + 0.75f), sh / 2, Mathf.Sign(toCourt.z) * (d / 2 + 0.75f))), new Vector3(0.5f, sh, 0.5f), rot);
                    }
                }
            }

            /// <summary>The roof bits, warning lights and crowns, merged (call once, after the towers).</summary>
            public void Finish()
            {
                _roofBits.Build("RoofBits", _c.Venue, _ledge);
                _redBits.Build("AirLights", _c.Venue, _red);
                foreach (var kv in _crown) kv.Value.Build("NeonCrown", _c.Venue, kv.Key);
            }

            /// <summary>A box's six faces into the tower: windows on the sides (lit toward the court, shaded away), the roof on top.</summary>
            static void Box(Vector3 centre, Vector3 s, Vector3 toCourt, List<Vector3> v, List<Vector2> uv, List<Vector3> nn, List<int> lit, List<int> shade, List<int> top)
            {
                Vector3 h = s * 0.5f;
                Vector3[,] faces =
                {
                    { Vector3.up, Vector3.right, Vector3.forward },
                    { Vector3.forward, Vector3.left, Vector3.up },
                    { Vector3.back, Vector3.right, Vector3.up },
                    { Vector3.right, Vector3.forward, Vector3.up },
                    { Vector3.left, Vector3.back, Vector3.up },
                };
                for (int f = 0; f < faces.GetLength(0); f++)
                {
                    Vector3 n = faces[f, 0], r = faces[f, 1], u = faces[f, 2];
                    float hr = Mathf.Abs(Vector3.Dot(h, r)), hu = Mathf.Abs(Vector3.Dot(h, u)), hn = Mathf.Abs(Vector3.Dot(h, n));
                    int b = v.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (k == 1 || k == 2) ? 1 : -1, cc = k >= 2 ? 1 : -1;
                        v.Add(centre + n * hn + r * (a * hr) + u * (cc * hu));
                        nn.Add(n);
                        uv.Add(new Vector2((a * hr + hr) / 12.8f, (cc * hu + hu) / 25.6f));
                    }
                    var list = f == 0 ? top : Vector3.Dot(n, toCourt) > 0.7f ? lit : shade;   // only the face most square to the court is lit
                    list.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                }
            }
        }
    }
}
