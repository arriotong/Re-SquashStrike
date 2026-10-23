// ===========================================================
// Assets/SquashGame/Runtime/Views/Greybox.cs
// Greybox building blocks shared by every view: the material palette
// (real assets, so URP shaders are never stripped from a build) and
// small primitive/mesh helpers. Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static class Greybox
    {
        public static Color Hex(uint rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);
        }

        /// <summary>A primitive with its collider removed: views never take part in Unity physics.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return go;
        }

        /// <summary>Axis-aligned box from a world-space centre and size.</summary>
        public static GameObject Box(string name, Transform parent, Material mat, Vector3 centre, Vector3 size, bool shadows = true)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, mat, shadows);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>A thin bar from a to b (painted lines, racket shaft).</summary>
        public static void PlaceBar(Transform t, Vector3 a, Vector3 b, float width, float depth)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(1e-4f, d.magnitude);
            t.position = (a + b) * 0.5f;
            t.rotation = Quaternion.FromToRotation(Vector3.forward, d / len);
            t.localScale = new Vector3(width, depth, len);
        }

        /// <summary>Flat disc in the local XY plane, facing +Z (racket strings, shadows after rotation).</summary>
        public static Mesh Disc(float radius, int segments)
        {
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % segments; tris[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = "Disc", vertices = verts, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Torus in the local XY plane (racket rim): major radius R, tube radius r.</summary>
        public static Mesh Torus(float R, float r, int major, int minor)
        {
            var verts = new Vector3[(major + 1) * (minor + 1)];
            var tris = new int[major * minor * 6];
            int v = 0;
            for (int i = 0; i <= major; i++)
            {
                float u = i * Mathf.PI * 2f / major;
                Vector3 centre = new Vector3(Mathf.Cos(u) * R, Mathf.Sin(u) * R, 0f);
                Vector3 outward = centre.normalized;
                for (int j = 0; j <= minor; j++)
                {
                    float w = j * Mathf.PI * 2f / minor;
                    verts[v++] = centre + outward * (Mathf.Cos(w) * r) + Vector3.forward * (Mathf.Sin(w) * r);
                }
            }
            int t = 0;
            for (int i = 0; i < major; i++)
            {
                for (int j = 0; j < minor; j++)
                {
                    int a = i * (minor + 1) + j, b = (i + 1) * (minor + 1) + j;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = b; tris[t++] = b + 1; tris[t++] = a + 1;
                }
            }
            var m = new Mesh { name = "Torus", vertices = verts, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A box whose UVs are in metres / tile on every face (a texture keeps its real size
        /// whatever the box's size), each face's u reading left to right from outside it.
        /// On the top face u = x, v = z.</summary>
        public static GameObject TexturedBox(string name, Transform parent, Material mat, Vector3 centre, Vector3 size, Vector2 tile, bool shadows = true)
        {
            var go = MeshObject(name, parent, BoxMesh(size, tile), mat, shadows);
            go.GetComponent<MeshRenderer>().receiveShadows = shadows;
            go.transform.localPosition = centre;
            return go;
        }

        static Mesh BoxMesh(Vector3 s, Vector2 tile)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            Vector3 h = s * 0.5f;
            // normal, right (u), up (v) per face, as seen from outside
            Vector3[,] faces =
            {
                { Vector3.up, Vector3.right, Vector3.forward },
                { Vector3.down, Vector3.right, Vector3.back },
                { Vector3.forward, Vector3.left, Vector3.up },
                { Vector3.back, Vector3.right, Vector3.up },
                { Vector3.right, Vector3.forward, Vector3.up },
                { Vector3.left, Vector3.back, Vector3.up },
            };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faces[f, 0], r = faces[f, 1], u = faces[f, 2];
                float hr = Mathf.Abs(Vector3.Dot(h, r)), hu = Mathf.Abs(Vector3.Dot(h, u)), hn = Mathf.Abs(Vector3.Dot(h, n));
                int b = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    float a = (k == 1 || k == 2) ? 1 : -1, c = k >= 2 ? 1 : -1;
                    verts.Add(n * hn + r * (a * hr) + u * (c * hu));
                    norms.Add(n);
                    uvs.Add(new Vector2((a * hr + hr) / tile.x, (c * hu + hu) / tile.y));
                }
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = "TexturedBox" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
