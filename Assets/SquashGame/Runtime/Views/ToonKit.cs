// ===========================================================
// Assets/SquashGame/Runtime/Views/ToonKit.cs
// 2026-11-20 the menu worlds' toolkit (out of the Gauntlet island, for
// every mode's own world - Dioramas): flat-toned toon meshes from
// triangle soups (each face one of a few tones by how it turns to a fixed
// sun, so they look the same on every court), the shapes (spheres,
// floating rocks, grass tops, cones, boxes, cylinders), waterfalls, and
// DioramaLife - the gentle motion (bobbing, drifting, spinning, a
// breathing halo, blinking lights). Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public static class ToonKit
    {
        /// <summary>Start a world: its palette (the court's), a fresh material cache.</summary>
        public static void Begin(GreyboxPalette pal) { _pal = pal; _mats.Clear(); }
        public static GreyboxPalette Pal { get { return _pal; } }

        // ---- the tones ----
        static readonly Vector3 Sun = new Vector3(-0.45f, 0.75f, 0.5f).normalized;   // the key light: up, left, toward the camera
        static GreyboxPalette _pal;
        static readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();

        public static Material Flat(Color c)
        {
            if (_mats.TryGetValue(c, out var m) && m != null) return m;
            m = _pal.UnlitColor(c);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);                  // both faces: the windings needn't be minded
            _mats[c] = m;
            return m;
        }

        /// <summary>A triangle soup as a toon-shaded mesh: Bands flat tones from shadow to light by each face's turn to the sun.
        /// The normals point away from <paramref name="centre"/> (flat: from its axis, at each face's own height).</summary>
        public static GameObject Toon(Transform parent, string name, List<Vector3> tris, Color baseCol, Vector3 centre, bool axis = false, int bands = 4)
        {
            var subs = new List<int>[bands];
            for (int b = 0; b < bands; b++) subs[b] = new List<int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = tris[i], b1 = tris[i + 1], c = tris[i + 2];
                Vector3 n = Vector3.Cross(b1 - a, c - a).normalized;
                Vector3 mid = (a + b1 + c) / 3f, from = axis ? new Vector3(centre.x, mid.y, centre.z) : centre;
                Vector3 outward = mid - from;
                if (outward.sqrMagnitude < 1e-6f) outward = Vector3.up;
                if (Vector3.Dot(n, outward) < 0) n = -n;
                float t = Mathf.Clamp01(Vector3.Dot(n, Sun) * 0.5f + 0.5f);
                int band = Mathf.Clamp((int)(t * bands), 0, bands - 1);
                subs[band].Add(i); subs[band].Add(i + 1); subs[band].Add(i + 2);
            }
            var mesh = new Mesh { name = name };
            if (tris.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(tris);
            var used = new List<Material>();
            int sm = 0;
            for (int b = 0; b < bands; b++) if (subs[b].Count > 0) sm++;
            mesh.subMeshCount = sm;
            sm = 0;
            Color shadow = new Color(baseCol.r * 0.32f + 0.05f, baseCol.g * 0.28f + 0.03f, baseCol.b * 0.38f + 0.10f);   // purple-blue shade
            Color light = baseCol * 1.12f; light.a = 1f;
            for (int b = 0; b < bands; b++)
            {
                if (subs[b].Count == 0) continue;
                mesh.SetTriangles(subs[b], sm++);
                Color tone = Color.Lerp(shadow, light, (b + 0.5f) / bands); tone.a = 1f;
                used.Add(Flat(Q(tone)));
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = used.ToArray();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        public static Color Q(Color c) { return new Color(Mathf.Round(c.r * 64f) / 64f, Mathf.Round(c.g * 64f) / 64f, Mathf.Round(c.b * 64f) / 64f, 1f); }

        // ---- shapes (triangle soups) ----

        public static void Tri(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c) { t.Add(a); t.Add(b); t.Add(c); }

        /// <summary>A low-poly sphere (latitude rings).</summary>
        public static List<Vector3> Sphere(Vector3 c, Vector3 r, int lon, int lat, System.Random jitter = null, float rough = 0f)
        {
            var t = new List<Vector3>();
            var p = new Vector3[lat + 1, lon];
            for (int i = 0; i <= lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    float th = Mathf.PI * i / lat, ph = 2 * Mathf.PI * j / lon;
                    Vector3 d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float k = jitter != null && i > 0 && i < lat ? 1f + ((float)jitter.NextDouble() - 0.5f) * rough : 1f;
                    p[i, j] = c + Vector3.Scale(d, r) * k;
                }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int j1 = (j + 1) % lon;
                    if (i > 0) Tri(t, p[i, j], p[i + 1, j], p[i, j1]);
                    if (i < lat - 1) Tri(t, p[i, j1], p[i + 1, j], p[i + 1, j1]);
                }
            return t;
        }

        /// <summary>A floating island's hanging rock: jagged rings tapering to a point under the top.</summary>
        public static List<Vector3> Rock(Vector3 c, float r, float depth, int sides, System.Random rng)
        {
            float[] ys = { 0f, -0.12f, -0.3f, -0.52f, -0.75f, -1f };
            float[] rs = { 1f, 0.92f, 0.72f, 0.48f, 0.24f, 0.04f };
            var ring = new Vector3[ys.Length, sides];
            float twist = 0f;
            for (int i = 0; i < ys.Length; i++)
            {
                twist += 0.35f;
                for (int j = 0; j < sides; j++)
                {
                    float a = 2 * Mathf.PI * j / sides + twist * 0.2f;
                    float rr = r * rs[i] * (i == 0 ? 1f : 0.82f + 0.36f * (float)rng.NextDouble());
                    float yy = depth * ys[i] + (i > 0 && i < ys.Length - 1 ? ((float)rng.NextDouble() - 0.5f) * depth * 0.08f : 0f);
                    ring[i, j] = c + new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr);
                }
            }
            var t = new List<Vector3>();
            for (int i = 0; i < ys.Length - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    Tri(t, ring[i, j], ring[i + 1, j], ring[i, j1]);
                    Tri(t, ring[i, j1], ring[i + 1, j], ring[i + 1, j1]);
                }
            Vector3 tip = c + Vector3.down * depth * 1.12f;
            for (int j = 0; j < sides; j++) Tri(t, ring[ys.Length - 1, j], tip, ring[ys.Length - 1, (j + 1) % sides]);
            return t;
        }

        /// <summary>The grass top: a jagged disc, a little domed, with a lip over the rock.</summary>
        public static List<Vector3> Top(Vector3 c, float r, int sides, float dome, System.Random rng, out Vector3[] rim)
        {
            var t = new List<Vector3>();
            rim = new Vector3[sides];
            var inner = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = 2 * Mathf.PI * j / sides;
                float rr = r * (0.93f + 0.12f * (float)rng.NextDouble());
                rim[j] = c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                inner[j] = c + new Vector3(Mathf.Cos(a) * rr * 0.55f, dome * 0.7f, Mathf.Sin(a) * rr * 0.55f);
            }
            Vector3 peak = c + Vector3.up * dome;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                Tri(t, peak, inner[j], inner[j1]);
                Tri(t, inner[j], rim[j], inner[j1]);
                Tri(t, inner[j1], rim[j], rim[j1]);
                // the lip: a band of grass down over the rock's edge
                Vector3 d0 = rim[j] + Vector3.down * 1.1f + (rim[j] - c).normalized * 0.3f, d1 = rim[j1] + Vector3.down * 1.1f + (rim[j1] - c).normalized * 0.3f;
                Tri(t, rim[j], d0, rim[j1]);
                Tri(t, rim[j1], d0, d1);
            }
            return t;
        }

        public static List<Vector3> Cone(Vector3 b, float r, float h, int sides)
        {
            var t = new List<Vector3>();
            Vector3 tip = b + Vector3.up * h;
            for (int j = 0; j < sides; j++)
            {
                float a0 = 2 * Mathf.PI * j / sides, a1 = 2 * Mathf.PI * (j + 1) / sides;
                Vector3 p0 = b + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), p1 = b + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Tri(t, p0, tip, p1);
                Tri(t, p0, p1, b);
            }
            return t;
        }


        /// <summary>Many separate parts as one toon mesh - each part's faces shaded out from that part's own centre (towers, segments).</summary>
        public static GameObject ToonParts(Transform parent, string name, List<(List<Vector3> tris, Vector3 centre)> parts, Color baseCol, int bands = 4)
        {
            var all = new List<Vector3>(); var flip = new List<bool>();
            var subs = new List<int>[bands];
            for (int b = 0; b < bands; b++) subs[b] = new List<int>();
            foreach (var (tris, centre) in parts)
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    Vector3 a = tris[i], b1 = tris[i + 1], c = tris[i + 2];
                    Vector3 n = Vector3.Cross(b1 - a, c - a).normalized, mid = (a + b1 + c) / 3f;
                    if (Vector3.Dot(n, mid - centre) < 0) n = -n;
                    int band = Mathf.Clamp((int)(Mathf.Clamp01(Vector3.Dot(n, Sun) * 0.5f + 0.5f) * bands), 0, bands - 1);
                    int k = all.Count; all.Add(a); all.Add(b1); all.Add(c);
                    subs[band].Add(k); subs[band].Add(k + 1); subs[band].Add(k + 2);
                }
            return Banded(parent, name, all, subs, baseCol);
        }

        static GameObject Banded(Transform parent, string name, List<Vector3> verts, List<int>[] subs, Color baseCol)
        {
            int bands = subs.Length;
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            int sm = 0; foreach (var l in subs) if (l.Count > 0) sm++;
            mesh.subMeshCount = sm; sm = 0;
            var used = new List<Material>();
            Color shadow = new Color(baseCol.r * 0.32f + 0.05f, baseCol.g * 0.28f + 0.03f, baseCol.b * 0.38f + 0.10f);
            Color light = baseCol * 1.12f; light.a = 1f;
            for (int b = 0; b < bands; b++)
            {
                if (subs[b].Count == 0) continue;
                mesh.SetTriangles(subs[b], sm++);
                Color tone = Color.Lerp(shadow, light, (b + 0.5f) / bands); tone.a = 1f;
                used.Add(Flat(Q(tone)));
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = used.ToArray();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        // ---- more shapes ----

        /// <summary>An axis-aligned box (its six faces) - turn it with <paramref name="rot"/> about its centre.</summary>
        public static List<Vector3> BoxTris(Vector3 c, Vector3 size, Quaternion rot)
        {
            var t = new List<Vector3>();
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) { return c + rot * new Vector3(x * h.x, y * h.y, z * h.z); }
            var v = new[] { P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1) };
            int[][] f = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 } };
            foreach (var q in f) { Tri(t, v[q[0]], v[q[1]], v[q[2]]); Tri(t, v[q[0]], v[q[2]], v[q[3]]); }
            return t;
        }
        public static List<Vector3> BoxTris(Vector3 c, Vector3 size) { return BoxTris(c, size, Quaternion.identity); }

        /// <summary>A cylinder from <paramref name="a"/> to <paramref name="b"/> (capped).</summary>
        public static List<Vector3> Cylinder(Vector3 a, Vector3 b, float r, int sides)
        {
            var t = new List<Vector3>();
            Vector3 ax = (b - a).normalized;
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(ax, u);
            for (int j = 0; j < sides; j++)
            {
                float a0 = 2 * Mathf.PI * j / sides, a1 = 2 * Mathf.PI * (j + 1) / sides;
                Vector3 o0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r, o1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r;
                Tri(t, a + o0, b + o0, a + o1); Tri(t, a + o1, b + o0, b + o1);
                Tri(t, a, a + o0, a + o1); Tri(t, b, b + o1, b + o0);
            }
            return t;
        }

        /// <summary>A flat ring (a torus' worth of a target) lying in the plane facing <paramref name="normal"/>.</summary>
        public static List<Vector3> Ring(Vector3 c, Vector3 normal, float r0, float r1, int sides)
        {
            var t = new List<Vector3>();
            Vector3 n = normal.normalized, u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(n, u);
            for (int j = 0; j < sides; j++)
            {
                float a0 = 2 * Mathf.PI * j / sides, a1 = 2 * Mathf.PI * (j + 1) / sides;
                Vector3 d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                Tri(t, c + d0 * r0, c + d0 * r1, c + d1 * r0); Tri(t, c + d1 * r0, c + d0 * r1, c + d1 * r1);
            }
            return t;
        }

        /// <summary>A flat-coloured mesh (no shading bands): lights, signs, glowing things.</summary>
        public static GameObject Glow(Transform parent, string name, List<Vector3> tris, Color c)
        {
            var mesh = new Mesh { name = name };
            if (tris.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(tris);
            var idx = new int[tris.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetTriangles(idx, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Flat(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        /// <summary>A sky: a dome round the world, its horizon at <paramref name="eye"/> height.</summary>
        public static void Sky(Transform t, string key, Color below, Color horizon, Color mid, Color top, int stars, int seed, float eye = 6f)
        {
            var sky = _pal.UnlitColor(Color.white);
            sky.mainTexture = ProcTex.LegacySky(key, below, horizon, mid, top, stars, seed);
            var dome = Greybox.MeshObject("Sky", t, VenueView.Dome(420f, 48, 24), sky);
            dome.transform.localPosition = new Vector3(0, eye, 0);
        }

        /// <summary>A soft glowing disc turned to the camera (a sun, a moon's halo, a lamp's bloom).</summary>
        public static GameObject Billboard(Transform t, string name, Vector3 at, float size, Color c, Vector3 camLocal, bool soft = true)
        {
            Material m;
            if (soft) { m = _pal.Clear(c); m.mainTexture = ProcTex.SoftSpot(); } else m = Flat(c);
            var g = Greybox.MeshObject(name, t, Greybox.Disc(1f, 40), m);
            g.transform.localPosition = at; g.transform.localScale = new Vector3(size, size, 1f);
            g.transform.LookAt(t.TransformPoint(camLocal)); g.transform.Rotate(0, 180f, 0);
            return g;
        }

        /// <summary>Puffy clouds: a few spheres side by side, toon-shaded.</summary>
        public static GameObject Cloud(Transform t, Vector3 at, float s, Color c, System.Random rng, int bands = 3)
        {
            var g = new GameObject("Cloud").transform; g.SetParent(t, false); g.localPosition = at;
            var puffs = new List<Vector3>();
            int n = 3 + rng.Next(4);
            for (int p = 0; p < n; p++)
            {
                var pc = new Vector3((p - n / 2f) * s * 0.7f, (float)rng.NextDouble() * s * 0.25f, ((float)rng.NextDouble() - 0.5f) * s * 0.4f);
                puffs.AddRange(Sphere(pc, new Vector3(1f, 0.7f, 0.9f) * s * (0.55f + 0.35f * (float)rng.NextDouble()), 9, 6));
            }
            Toon(g, "Puffs", puffs, c, Vector3.down * s * 0.4f, false, bands);
            return g.gameObject;
        }

        /// <summary>A squash ball: black (or a colour), its two yellow dots turned toward <paramref name="dotsToward"/>.</summary>
        public static void SquashBall(Transform t, Vector3 c, float r, Vector3 dotsToward, Color? body = null)
        {
            Toon(t, "SquashBall", Sphere(c, Vector3.one * r, 24, 16), body ?? new Color(0.22f, 0.22f, 0.27f), c, false, 5);
            Vector3 f = dotsToward.normalized, side = Vector3.Cross(Vector3.up, f).normalized;
            var dot = Flat(new Color(1.6f, 1.35f, 0.15f));
            foreach (var d in new[] { (f + side * 0.42f + Vector3.up * 0.22f).normalized, (f + side * 0.28f + Vector3.up * 0.45f).normalized })
            {
                var g = Greybox.MeshObject("YellowDot", t, Greybox.Disc(1f, 20), dot);
                g.transform.localPosition = c + d * (r * 1.006f);
                g.transform.localRotation = Quaternion.LookRotation(-d);
                g.transform.localScale = new Vector3(r * 0.086f, r * 0.086f, 1f);
            }
        }

        /// <summary>A squash racket: its head (a ring and strings), the throat, the grip - lying in the plane of <paramref name="face"/>.</summary>
        public static void Racket(Transform t, Vector3 butt, Vector3 up, Vector3 face, float length, Color frame, Color grip)
        {
            Vector3 u = up.normalized, n = face.normalized, s = Vector3.Cross(n, u).normalized;
            float headH = length * 0.40f, headW = length * 0.30f, shaft = length * 0.60f;
            Vector3 hc = butt + u * (shaft + headH * 0.45f);
            var fr = new List<Vector3>();
            const int N = 24;
            for (int j = 0; j < N; j++)
            {
                float a0 = 2 * Mathf.PI * j / N, a1 = 2 * Mathf.PI * (j + 1) / N;
                Vector3 p0 = hc + s * Mathf.Cos(a0) * headW * 0.5f + u * Mathf.Sin(a0) * headH * 0.5f, p1 = hc + s * Mathf.Cos(a1) * headW * 0.5f + u * Mathf.Sin(a1) * headH * 0.5f;
                fr.AddRange(Cylinder(p0, p1, length * 0.018f, 5));
            }
            Vector3 throat = hc - u * headH * 0.5f;
            fr.AddRange(Cylinder(butt + u * length * 0.22f, throat, length * 0.016f, 5));
            fr.AddRange(Cylinder(throat, throat + s * headW * 0.22f + u * headH * 0.06f, length * 0.012f, 4));
            fr.AddRange(Cylinder(throat, throat - s * headW * 0.22f + u * headH * 0.06f, length * 0.012f, 4));
            Toon(t, "RacketFrame", fr, frame, hc - n * length, false, 4);
            Toon(t, "RacketGrip", Cylinder(butt, butt + u * length * 0.22f, length * 0.024f, 6), grip, butt - n * length, false, 3);
            var str = new List<Vector3>();
            for (int k = -4; k <= 4; k++)
            {
                float x = k * headW * 0.1f, hh = headH * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (headW * 0.5f)) * (x / (headW * 0.5f))));
                str.AddRange(Cylinder(hc + s * x - u * hh, hc + s * x + u * hh, length * 0.004f, 3));
            }
            for (int k = -6; k <= 6; k++)
            {
                float y = k * headH * 0.075f, ww = headW * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (y / (headH * 0.5f)) * (y / (headH * 0.5f))));
                str.AddRange(Cylinder(hc + u * y - s * ww, hc + u * y + s * ww, length * 0.004f, 3));
            }
            Glow(t, "RacketStrings", str, new Color(0.95f, 0.95f, 0.9f));
        }

        /// <summary>A small glass court (a floor, a front wall with its lines, glass sides and back), <paramref name="scale"/> of the real one.</summary>
        public static Transform GlassCourt(Transform t, Vector3 at, float yaw, float scale, Color floor)
        {
            var g = new GameObject("GlassCourt").transform; g.SetParent(t, false);
            g.localPosition = at; g.localRotation = Quaternion.Euler(0, yaw, 0);
            float cw = 6.4f * scale, cl = 9.75f * scale, ch = 4.57f * scale;
            Greybox.Box("Floor", g, Flat(floor), new Vector3(0, 0.05f * scale, 0), new Vector3(cw, 0.1f * scale, cl), false);
            Greybox.Box("FrontWall", g, Flat(new Color(0.93f, 0.93f, 0.96f)), new Vector3(0, ch / 2, -cl / 2), new Vector3(cw, ch, 0.12f * scale), false);
            var red = Flat(new Color(0.9f, 0.2f, 0.2f));
            Greybox.Box("Tin", g, red, new Vector3(0, 0.48f * scale, -cl / 2 + 0.07f * scale), new Vector3(cw, 0.08f * scale, 0.02f), false);
            Greybox.Box("Service", g, red, new Vector3(0, 1.78f * scale, -cl / 2 + 0.07f * scale), new Vector3(cw, 0.08f * scale, 0.02f), false);
            Greybox.Box("Out", g, red, new Vector3(0, ch - 0.1f * scale, -cl / 2 + 0.07f * scale), new Vector3(cw, 0.08f * scale, 0.02f), false);
            Greybox.Box("ShortLine", g, red, new Vector3(0, 0.11f * scale, -cl / 2 + 5.44f * scale), new Vector3(cw, 0.01f, 0.08f * scale), false);
            Greybox.Box("HalfLine", g, red, new Vector3(0, 0.11f * scale, cl / 4 - 0.2f * scale), new Vector3(0.08f * scale, 0.01f, cl / 2 - 0.8f * scale), false);
            var glass = _pal.Clear(new Color(0.7f, 0.85f, 1f, 0.24f));
            foreach (int sd in new[] { -1, 1 }) Greybox.Box("Glass", g, glass, new Vector3(sd * cw / 2, ch * 0.45f, 0), new Vector3(0.05f * scale, ch * 0.9f, cl), false);
            Greybox.Box("GlassBack", g, glass, new Vector3(0, ch * 0.24f, cl / 2), new Vector3(cw, ch * 0.48f, 0.05f * scale), false);
            return g;
        }

        /// <summary>A waterfall's sheet: a tall strip, a little curved out over the edge at the top.</summary>
        public static Mesh FallMesh(float w, float h)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            const int N = 12;
            for (int i = 0; i <= N; i++)
            {
                float k = i / (float)N, y = -h * k, out_ = Mathf.Sqrt(k) * 2.2f;
                float ww = w * (1f + k * 0.8f);
                v.Add(new Vector3(-ww / 2, y, out_)); v.Add(new Vector3(ww / 2, y, out_));
                uv.Add(new Vector2(0, k)); uv.Add(new Vector2(1, k));
                if (i > 0) { int b = i * 2; tr.AddRange(new[] { b - 2, b, b - 1, b - 1, b, b + 1 }); }
            }
            var m = new Mesh { name = "Waterfall" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Texture2D _fall;
        /// <summary>White streaks down a sheet, fading out toward the bottom (scrolled by GauntletIslandLife).</summary>
        public static Texture2D FallTexture()
        {
            if (_fall != null) return _fall;
            const int W = 64, H = 256;
            _fall = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "gauntlet_fall" };
            var rng = new System.Random(7);
            var col = new float[W];
            for (int x = 0; x < W; x++) col[x] = 0.55f + 0.45f * (float)rng.NextDouble();
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(x, W - 1 - x) / 10f);
                    float streak = col[x] * (0.75f + 0.25f * Mathf.Sin((y * 0.11f + x * 1.7f)));
                    px[y * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(streak * edge));
                }
            _fall.SetPixels(px); _fall.Apply();
            return _fall;
        }
    }

    /// <summary>The island's gentle life: drifting clouds, bobbing islands, running falls, a breathing halo.</summary>
    public sealed class DioramaLife : MonoBehaviour
    {
        public Transform Halo; public Material HaloMat, Falls;
        readonly List<(Transform t, Vector3 at, float speed, float phase, float amp)> _bob = new List<(Transform, Vector3, float, float, float)>();
        readonly List<(Transform t, float speed, float wrap)> _drift = new List<(Transform, float, float)>();
        Vector3 _haloScale;
        float _time;

        public void Bob(Transform t, float speed, float phase, float amp) { _bob.Add((t, t.localPosition, speed, phase, amp)); }
        public void Drift(Transform t, float speed, float wrap) { _drift.Add((t, speed, wrap)); }
        readonly List<(Transform t, Vector3 axis, float speed)> _spin = new List<(Transform, Vector3, float)>();
        readonly List<(Material m, Color on, float speed, float phase)> _blink = new List<(Material, Color, float, float)>();
        /// <summary>Turn slowly about an axis (deg/s): a windmill, a blimp's prop, a globe.</summary>
        public void Spin(Transform t, Vector3 axis, float speed) { _spin.Add((t, axis, speed)); }
        /// <summary>A light that pulses (a beacon, a star, a target).</summary>
        public void Blink(Material m, float speed, float phase) { _blink.Add((m, m.color, speed, phase)); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime; _time += dt;
            foreach (var b in _bob) b.t.localPosition = b.at + Vector3.up * Mathf.Sin(_time * b.speed + b.phase) * b.amp;
            foreach (var d in _drift)
            {
                var p = d.t.localPosition; p.x += d.speed * dt;
                if (p.x > d.wrap) p.x -= 2 * d.wrap;
                d.t.localPosition = p;
            }
            if (Falls != null) Falls.mainTextureOffset = new Vector2(0f, _time * 0.9f);
            foreach (var s in _spin) s.t.Rotate(s.axis, s.speed * dt, Space.Self);
            foreach (var b in _blink) { float k = 0.55f + 0.45f * Mathf.Sin(_time * b.speed + b.phase); b.m.color = new Color(b.on.r * k, b.on.g * k, b.on.b * k, b.on.a); }
            if (Halo != null)
            {
                if (_haloScale == Vector3.zero) _haloScale = Halo.localScale;
                float k = 1f + 0.05f * Mathf.Sin(_time * 1.3f);
                Halo.localScale = _haloScale * k;
                if (HaloMat != null) { var c = HaloMat.color; c.a = 0.28f + 0.1f * (0.5f + 0.5f * Mathf.Sin(_time * 1.3f)); HaloMat.color = c; }
            }
        }
    }
}
