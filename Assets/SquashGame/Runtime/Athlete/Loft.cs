// ===========================================================
// Assets/SquashGame/Runtime/Athlete/Loft.cs
// Port of lab/character/SurfaceSweep.js: smooth organic forms lofted
// from a stack of superellipse cross-sections, plus the closed-path
// tube used for the racket frame.
//
// Geometry is produced in the RIG frame (right-handed, +X right,
// -Z forward) exactly as the lab builds it; MeshData.ToUnity()
// mirrors X (and fixes winding and normals) once at the end.
// Normals come from the swept surface itself, and caps get their own
// vertices, so the joins between forms do not print shading seams.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public struct Ring
    {
        public float Y, Rx, RF, RB, N, Cx, Cz;

        /// <summary>w = full breadth, dFront = depth toward -Z (front), dBack toward +Z.</summary>
        public static Ring Make(float y, float w, float dFront, float dBack, float n = 2.2f, float cx = 0, float cz = 0)
        {
            return new Ring { Y = y, Rx = w * 0.5f, RF = dFront, RB = dBack, N = n, Cx = cx, Cz = cz };
        }

        /// <summary>Symmetric front/back from one full depth.</summary>
        public static Ring D(float y, float w, float d, float n = 2.2f, float cx = 0, float cz = 0)
        {
            return Make(y, w, d * 0.5f, d * 0.5f, n, cx, cz);
        }
    }

    /// <summary>Plain triangle mesh data in the rig frame.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<int> T = new List<int>();

        public void Append(MeshData o)
        {
            int b = V.Count;
            V.AddRange(o.V); N.AddRange(o.N);
            foreach (int i in o.T) T.Add(b + i);
        }

        public MeshData Transformed(Matrix4x4 m)
        {
            var r = new MeshData();
            foreach (var v in V) r.V.Add(m.MultiplyPoint3x4(v));
            foreach (var n in N) r.N.Add(m.MultiplyVector(n).normalized);
            r.T.AddRange(T);
            return r;
        }

        public MeshData Translated(Vector3 d) { return Transformed(Matrix4x4.Translate(d)); }

        /// <summary>Mirror across X (lab mirrored()): positions, normals, and winding.</summary>
        public MeshData MirroredX()
        {
            var r = new MeshData();
            foreach (var v in V) r.V.Add(new Vector3(-v.x, v.y, v.z));
            foreach (var n in N) r.N.Add(new Vector3(-n.x, n.y, n.z));
            for (int i = 0; i < T.Count; i += 3) { r.T.Add(T[i + 2]); r.T.Add(T[i + 1]); r.T.Add(T[i]); }
            return r;
        }

        /// <summary>
        /// Rig frame (right-handed) -> Unity (left-handed): the same mirror as SimToWorld.
        /// Winding is deliberately KEPT: a counter-clockwise front face (three.js / rig convention)
        /// becomes clockwise when reflected, which is exactly Unity's front-face convention.
        /// (MirroredX, by contrast, mirrors WITHIN one handedness and must swap winding.)
        /// </summary>
        public Mesh ToUnity(string name)
        {
            var verts = new List<Vector3>(V.Count);
            var norms = new List<Vector3>(N.Count);
            foreach (var v in V) verts.Add(new Vector3(-v.x, v.y, v.z));
            foreach (var n in N) norms.Add(new Vector3(-n.x, n.y, n.z));
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public static class Loft
    {
        static Vector2 SuperPoint(Ring r, float t)
        {
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            float e = 2f / r.N;
            float x = r.Cx + r.Rx * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
            float depth = s >= 0 ? r.RB : r.RF;          // -Z is the facing direction: negative lobe = front depth
            float z = r.Cz + depth * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
            return new Vector2(x, z);
        }

        /// <summary>Loft rings into a closed form with surface-derived normals and separate flat caps.</summary>
        public static MeshData Sweep(List<Ring> rings, int segs = 28)
        {
            int rows = rings.Count;
            var pos = new Vector3[rows * segs];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < segs; j++)
                {
                    Vector2 p = SuperPoint(rings[i], j / (float)segs * Mathf.PI * 2f);
                    pos[i * segs + j] = new Vector3(p.x, rings[i].Y, p.y);
                }

            System.Func<int, int, Vector3> get = (i, j) =>
                pos[Mathf.Clamp(i, 0, rows - 1) * segs + ((j % segs) + segs) % segs];

            var d = new MeshData();
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < segs; j++)
                {
                    Vector3 du = get(i, j + 1) - get(i, j - 1);
                    Vector3 dv = get(i + 1, j) - get(i - 1, j);
                    // Same cross order as the lab (right-handed rig frame).
                    Vector3 n = new Vector3(dv.y * du.z - dv.z * du.y, dv.z * du.x - dv.x * du.z, dv.x * du.y - dv.y * du.x);
                    d.V.Add(pos[i * segs + j]);
                    d.N.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
                }
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < segs; j++)
                {
                    int v0 = i * segs + j, v1 = i * segs + (j + 1) % segs;
                    int v2 = (i + 1) * segs + j, v3 = (i + 1) * segs + (j + 1) % segs;
                    d.T.Add(v0); d.T.Add(v2); d.T.Add(v1);
                    d.T.Add(v1); d.T.Add(v2); d.T.Add(v3);
                }

            Cap(d, pos, rings, segs, 0, -1);
            Cap(d, pos, rings, segs, rows - 1, 1);
            return d;
        }

        static void Cap(MeshData d, Vector3[] pos, List<Ring> rings, int segs, int row, int dir)
        {
            int baseIdx = d.V.Count;
            for (int j = 0; j < segs; j++) { d.V.Add(pos[row * segs + j]); d.N.Add(new Vector3(0, dir, 0)); }
            int c = d.V.Count;
            d.V.Add(new Vector3(rings[row].Cx, rings[row].Y, rings[row].Cz)); d.N.Add(new Vector3(0, dir, 0));
            for (int j = 0; j < segs; j++)
            {
                int u = baseIdx + j, v = baseIdx + (j + 1) % segs;
                if (dir > 0) { d.T.Add(c); d.T.Add(v); d.T.Add(u); }
                else { d.T.Add(c); d.T.Add(u); d.T.Add(v); }
            }
        }

        /// <summary>Catmull-Rom resample over every channel (lab smoothRings).</summary>
        public static List<Ring> Smooth(List<Ring> key, int steps = 5)
        {
            if (key.Count < 3) return key;
            System.Func<int, Ring> at = i => key[Mathf.Clamp(i, 0, key.Count - 1)];
            var outp = new List<Ring>();
            for (int i = 0; i < key.Count - 1; i++)
            {
                Ring p0 = at(i - 1), p1 = at(i), p2 = at(i + 1), p3 = at(i + 2);
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    System.Func<float, float, float, float, float> cr = (a, b, c, e) =>
                        0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - e) * t2 + (-a + 3 * b - 3 * c + e) * t3);
                    outp.Add(new Ring
                    {
                        Y = cr(p0.Y, p1.Y, p2.Y, p3.Y), Rx = cr(p0.Rx, p1.Rx, p2.Rx, p3.Rx),
                        RF = cr(p0.RF, p1.RF, p2.RF, p3.RF), RB = cr(p0.RB, p1.RB, p2.RB, p3.RB),
                        N = cr(p0.N, p1.N, p2.N, p3.N), Cx = cr(p0.Cx, p1.Cx, p2.Cx, p3.Cx), Cz = cr(p0.Cz, p1.Cz, p2.Cz, p3.Cz)
                    });
                }
            }
            outp.Add(key[key.Count - 1]);
            return outp;
        }

        public static MeshData Make(List<Ring> key, int steps = 5, int segs = 28)
        {
            return Sweep(Smooth(key, steps), segs);
        }

        /// <summary>Elliptical tube along a CLOSED path in the YZ plane (racket frame).</summary>
        public static MeshData TubeAlongPath(List<Vector2> path, float rFace, float[] rEdge, int seg = 10)
        {
            int n = path.Count;
            var nrm = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = path[(i - 1 + n) % n], b = path[(i + 1) % n];
                Vector2 t = (b - a).normalized;
                nrm[i] = new Vector2(-t.y, t.x);
            }
            var d = new MeshData();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float e = rEdge[i];
                    d.V.Add(new Vector3(rFace * s, path[i].x + nrm[i].x * e * c, path[i].y + nrm[i].y * e * c));
                    d.N.Add(Vector3.zero);
                }
            System.Func<int, int, int> at = (i, j) => (((i % n) + n) % n) * seg + (((j % seg) + seg) % seg);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < seg; j++)
                {
                    Vector3 du = d.V[at(i, j + 1)] - d.V[at(i, j - 1)];
                    Vector3 dv = d.V[at(i + 1, j)] - d.V[at(i - 1, j)];
                    Vector3 nn = new Vector3(du.y * dv.z - du.z * dv.y, du.z * dv.x - du.x * dv.z, du.x * dv.y - du.y * dv.x);
                    d.N[at(i, j)] = nn.sqrMagnitude > 1e-12f ? nn.normalized : Vector3.right;
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < seg; j++)
                {
                    int v0 = i * seg + j, v1 = i * seg + (j + 1) % seg;
                    int v2 = ((i + 1) % n) * seg + j, v3 = ((i + 1) % n) * seg + (j + 1) % seg;
                    d.T.Add(v0); d.T.Add(v1); d.T.Add(v2);
                    d.T.Add(v1); d.T.Add(v3); d.T.Add(v2);
                }
            return d;
        }

        /// <summary>Offset a closed YZ path inward (lab offsetPath).</summary>
        public static List<Vector2> OffsetPath(List<Vector2> path, float[] dist)
        {
            int n = path.Count;
            var r = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = path[(i - 1 + n) % n], b = path[(i + 1) % n];
                Vector2 t = (b - a).normalized;
                r.Add(new Vector2(path[i].x + t.y * dist[i], path[i].y - t.x * dist[i]));
            }
            return r;
        }

        /// <summary>Blend ellipsoid for a bent joint (lab joint()).</summary>
        public static MeshData Ellipsoid(float w, float d, float h, int lon = 20, int lat = 14)
        {
            var m = new MeshData();
            for (int i = 0; i <= lat; i++)
            {
                float v = i / (float)lat * Mathf.PI, sv = Mathf.Sin(v), cv = Mathf.Cos(v);
                for (int j = 0; j <= lon; j++)
                {
                    float u = j / (float)lon * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(sv * Mathf.Cos(u), cv, sv * Mathf.Sin(u));
                    m.V.Add(new Vector3(unit.x * w * 0.5f, unit.y * h * 0.5f, unit.z * d * 0.5f));
                    m.N.Add(new Vector3(unit.x / (w * w), unit.y / (h * h), unit.z / (d * d)).normalized);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b = a + lon + 1;
                    m.T.Add(a); m.T.Add(a + 1); m.T.Add(b);
                    m.T.Add(a + 1); m.T.Add(b + 1); m.T.Add(b);
                }
            return m;
        }
    }
}
