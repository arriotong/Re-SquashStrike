// ===========================================================
// Assets/SquashGame/Runtime/Views/MeshMerge.cs
// Gathers many boxes and quads into one mesh (the hall's roof trusses,
// its lamps): thousands of parts at one draw call. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public sealed class MeshMerge
    {
        readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        readonly List<int> _t = new List<int>();
        public int Parts { get; private set; }

        /// <summary>A box of the given size centred at <paramref name="centre"/>, its local +Z along <paramref name="rot"/>.</summary>
        public void Box(Vector3 centre, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (var n in normals)
            {
                Vector3 r = n == Vector3.up ? Vector3.right : n == Vector3.down ? Vector3.left : Vector3.Cross(Vector3.up, n);
                Vector3 u = Vector3.Cross(r, n);
                int b = _v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float sr = (k == 1 || k == 2) ? 1 : -1, su = k >= 2 ? 1 : -1;
                    _v.Add(centre + rot * Vector3.Scale(n + r * sr + u * su, h));
                    _n.Add(rot * n);
                }
                _t.Add(b); _t.Add(b + 2); _t.Add(b + 1);
                _t.Add(b); _t.Add(b + 3); _t.Add(b + 2);
            }
            Parts++;
        }

        /// <summary>A beam from a to b.</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float d)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) return;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Box((a + b) * 0.5f, new Vector3(w, d, dir.magnitude), Quaternion.LookRotation(dir, upHint));
        }

        /// <summary>A quad a-b-c-d, seen from both sides.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _n.Add(n); _n.Add(n); _n.Add(n); _n.Add(n);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2); _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
            int j = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _n.Add(-n); _n.Add(-n); _n.Add(-n); _n.Add(-n);
            _t.Add(j); _t.Add(j + 2); _t.Add(j + 1); _t.Add(j); _t.Add(j + 3); _t.Add(j + 2);
            Parts++;
        }

        public GameObject Build(string name, Transform parent, Material mat)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(_v); m.SetNormals(_n); m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return Greybox.MeshObject(name, parent, m, mat, false);
        }
    }
}
