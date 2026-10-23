// ===========================================================
// Assets/SquashGame/Runtime/Views/CrowdBuilder.cs
// The crowd as people (playtest 2026-10-23: "they are cardboards"):
// every seat gets a low-poly seated spectator - legs, shoes, torso,
// arms (resting, clapping or one up), head, hair or a cap - in their own
// clothes and skin tone, a little bigger or smaller, turned a little.
//
// Thousands of people at a handful of draw calls: the parts are merged
// into one mesh per colour per stand section, and each section is split
// into four groups that the CrowdMotion component bobs when the crowd
// reacts (CrowdAudio -> VenueView.Excite). Seeded, so the same crowd is
// in the same seats every match. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public static class CrowdBuilder
    {
        public const float SeatPitch = 0.52f;          // seat to seat along a row
        public const int Groups = 4;
        public static int PeopleBuilt { get; private set; }

        // An evening crowd (2026-10-23, after the Canary Wharf photo): mostly dark and muted - navy, black, charcoal,
        // greys, white shirts - with the odd colour.
        static readonly Color[] Shirts =
        {
            Greybox.Hex(0x1c2233), Greybox.Hex(0x16171b), Greybox.Hex(0x2b2e35), Greybox.Hex(0x3d4048), Greybox.Hex(0x5b5f68),
            Greybox.Hex(0xd8d8d4), Greybox.Hex(0xb9bcc2), Greybox.Hex(0x23324f), Greybox.Hex(0x4a2228), Greybox.Hex(0x3a4230),
            Greybox.Hex(0x6b5a44), Greybox.Hex(0x2f5a8a), Greybox.Hex(0x8a2f2f), Greybox.Hex(0x1f2a3a),
        };
        static readonly Color[] Trousers = { Greybox.Hex(0x232d40), Greybox.Hex(0x141518), Greybox.Hex(0x5e5444), Greybox.Hex(0x3c3f45) };
        static readonly Color[] Skins = { Greybox.Hex(0xf1c9a5), Greybox.Hex(0xe0ac83), Greybox.Hex(0xc68b62), Greybox.Hex(0x8d5a3b), Greybox.Hex(0x5c3a26) };
        static readonly Color[] Hairs = { Greybox.Hex(0x1a1410), Greybox.Hex(0x3b2a1e), Greybox.Hex(0x6b4a2e), Greybox.Hex(0xb08a58), Greybox.Hex(0x9a9a9a) };
        static readonly Color Shoe = Greybox.Hex(0x2a2a2e);

        /// <summary>Mesh pieces for one material, gathered from many people.</summary>
        sealed class Acc
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> T = new List<int>();
        }

        /// <summary>The materials, one per colour, shared by every stand (made once per court build).</summary>
        public sealed class Palette
        {
            public readonly Material[] Shirt, Trouser, Skin, Hair;
            public readonly Material ShoeMat;
            public Palette(GreyboxPalette p)
            {
                // Darker than their clothes: the stands sit in the hall's shadow, the court takes the light.
                Material M(Color c) { var m = p.LitColor(c * 0.62f); m.SetFloat("_Smoothness", 0.15f); return m; }
                Shirt = System.Array.ConvertAll(Shirts, M);
                Trouser = System.Array.ConvertAll(Trousers, M);
                Skin = System.Array.ConvertAll(Skins, M);
                Hair = System.Array.ConvertAll(Hairs, M);
                ShoeMat = M(Shoe);
            }
        }

        public static void ResetCount() { PeopleBuilt = 0; }

        /// <summary>One stand: rows of people. rowStart(i) is row i's front edge at floor level (the centre of its length),
        /// along its length axis, facing the court; tread(i) its tread height. Returns the four groups' transforms.</summary>
        public static Transform[] Stand(Transform parent, Palette pal, string name, int rows, System.Func<int, Vector3> rowStart,
            System.Func<int, float> tread, Vector3 along, Vector3 facing, float length, int seed, float empty = 0.1f)
        {
            var groups = new Transform[Groups];
            var accs = new Dictionary<Material, Acc>[Groups];
            for (int g = 0; g < Groups; g++)
            {
                groups[g] = new GameObject(name + "_Crowd" + g).transform;
                groups[g].SetParent(parent, false);
                accs[g] = new Dictionary<Material, Acc>();
            }
            var rng = new System.Random(seed * 7919 + 13);
            int seats = Mathf.FloorToInt(length / SeatPitch);
            Quaternion face = Quaternion.LookRotation(facing, Vector3.up);
            for (int i = 0; i < rows; i++)
            {
                Vector3 front = rowStart(i);
                float y = tread(i);
                for (int s = 0; s < seats; s++)
                {
                    if (rng.NextDouble() < empty) continue;
                    float u = (s + 0.5f) * SeatPitch - length * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.06f;
                    Vector3 hip = front + along * u - facing * 0.55f + Vector3.up * y;
                    float scale = 0.92f + (float)rng.NextDouble() * 0.16f;
                    Quaternion turn = face * Quaternion.Euler(0, ((float)rng.NextDouble() - 0.5f) * 22f, 0);
                    Person(accs[rng.Next(Groups)], pal, Matrix4x4.TRS(hip, turn, Vector3.one * scale), rng);
                    PeopleBuilt++;
                }
            }
            for (int g = 0; g < Groups; g++)
                foreach (var kv in accs[g])
                {
                    var m = new Mesh { name = name + "_crowd", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    m.SetVertices(kv.Value.V); m.SetNormals(kv.Value.N); m.SetTriangles(kv.Value.T, 0);
                    m.RecalculateBounds();
                    Greybox.MeshObject(kv.Key.name, groups[g], m, kv.Key, false);
                }
            return groups;
        }

        /// <summary>A seated person in their own frame: hips at the origin on the seat, facing +Z, feet on the tread.</summary>
        static void Person(Dictionary<Material, Acc> accs, Palette pal, Matrix4x4 at, System.Random rng)
        {
            var shirt = pal.Shirt[rng.Next(pal.Shirt.Length)];
            var trouser = pal.Trouser[rng.Next(pal.Trouser.Length)];
            int skinI = rng.Next(pal.Skin.Length);
            var skin = pal.Skin[skinI];
            bool shortSleeves = rng.NextDouble() < 0.45;
            float lean = -4f - (float)rng.NextDouble() * 10f;            // sitting back
            float pose = (float)rng.NextDouble();                       // 0..0.75 resting, ..0.92 clapping, rest one arm up

            // Legs and shoes.
            foreach (int sd in new[] { -1, 1 })
            {
                float x = sd * 0.1f;
                Box(accs, trouser, at, new Vector3(x, 0.47f, 0.2f), new Vector3(0.15f, 0.14f, 0.44f), Quaternion.identity);
                Box(accs, trouser, at, new Vector3(x, 0.23f, 0.40f), new Vector3(0.12f, 0.46f, 0.12f), Quaternion.identity);
                Box(accs, pal.ShoeMat, at, new Vector3(x, 0.045f, 0.44f), new Vector3(0.11f, 0.09f, 0.22f), Quaternion.identity);
            }
            // Torso and head (tilted with the lean).
            Matrix4x4 body = at * Matrix4x4.TRS(new Vector3(0, 0.45f, -0.04f), Quaternion.Euler(lean, 0, 0), Vector3.one);
            Ell(accs, trouser, body, new Vector3(0, 0.04f, 0.02f), new Vector3(0.36f, 0.18f, 0.26f));      // seat of the trousers
            Ell(accs, shirt, body, new Vector3(0, 0.20f, 0), new Vector3(0.31f, 0.34f, 0.22f));             // waist
            Ell(accs, shirt, body, new Vector3(0, 0.45f, 0), new Vector3(0.41f, 0.36f, 0.24f));             // chest and shoulders
            Ell(accs, skin, body, new Vector3(0, 0.66f, 0.01f), new Vector3(0.10f, 0.10f, 0.10f));         // neck
            float headTilt = ((float)rng.NextDouble() - 0.5f) * 16f;
            Matrix4x4 head = body * Matrix4x4.TRS(new Vector3(0, 0.80f, 0.01f), Quaternion.Euler(-lean * 0.6f, headTilt, 0), Vector3.one);
            Ell(accs, skin, head, Vector3.zero, new Vector3(0.195f, 0.245f, 0.22f));
            double h = rng.NextDouble();
            var hair = pal.Hair[rng.Next(pal.Hair.Length)];
            if (h < 0.72) Ell(accs, hair, head, new Vector3(0, 0.06f, -0.03f), new Vector3(0.205f, 0.16f, 0.21f));      // hair: top and back, the face clear
            else if (h < 0.84)
            {
                Ell(accs, shirt, head, new Vector3(0, 0.06f, -0.005f), new Vector3(0.2f, 0.12f, 0.22f));                 // a cap
                Box(accs, shirt, head, new Vector3(0, 0.04f, 0.13f), new Vector3(0.16f, 0.02f, 0.1f), Quaternion.identity);
            }
            if (h > 0.9) Ell(accs, hair, head, new Vector3(0, -0.02f, -0.1f), new Vector3(0.16f, 0.2f, 0.08f));         // long at the back

            // Arms: upper arms down the sides; forearms on the lap, together for a clap, or one raised.
            var sleeve = shirt;
            var fore = shortSleeves ? skin : shirt;
            foreach (int sd in new[] { -1, 1 })
            {
                float x = sd * 0.215f;
                bool up = pose > 0.92f && sd > 0;
                if (up)
                {
                    Box(accs, sleeve, body, new Vector3(x + sd * 0.02f, 0.78f, 0.02f), new Vector3(0.1f, 0.34f, 0.1f), Quaternion.Euler(0, 0, -sd * 12f));
                    Box(accs, fore, body, new Vector3(x + sd * 0.05f, 1.08f, 0.04f), new Vector3(0.09f, 0.3f, 0.09f), Quaternion.Euler(0, 0, -sd * 8f));
                    Ell(accs, skin, body, new Vector3(x + sd * 0.06f, 1.25f, 0.05f), new Vector3(0.09f, 0.1f, 0.06f));
                    continue;
                }
                Box(accs, sleeve, body, new Vector3(x, 0.40f, 0.02f), new Vector3(0.1f, 0.32f, 0.1f), Quaternion.Euler(-10f, 0, sd * 4f));
                if (pose > 0.75f)                                        // clapping: hands together in front of the chest
                {
                    Box(accs, fore, body, new Vector3(sd * 0.11f, 0.36f, 0.16f), new Vector3(0.08f, 0.08f, 0.28f), Quaternion.Euler(-35f, -sd * 30f, 0));
                    Ell(accs, skin, body, new Vector3(sd * 0.03f, 0.45f, 0.27f), new Vector3(0.05f, 0.09f, 0.08f));
                }
                else                                                     // resting on the lap
                {
                    Box(accs, fore, body, new Vector3(sd * 0.17f, 0.2f, 0.16f), new Vector3(0.09f, 0.09f, 0.3f), Quaternion.Euler(10f, -sd * 10f, 0));
                    Ell(accs, skin, body, new Vector3(sd * 0.13f, 0.16f, 0.31f), new Vector3(0.08f, 0.05f, 0.1f));
                }
            }
        }

        // ---------------- geometry ----------------

        static Acc Get(Dictionary<Material, Acc> accs, Material m)
        {
            if (!accs.TryGetValue(m, out var a)) accs[m] = a = new Acc();
            return a;
        }

        static readonly Vector3[] CubeN = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        static void Box(Dictionary<Material, Acc> accs, Material mat, Matrix4x4 frame, Vector3 centre, Vector3 size, Quaternion rot)
        {
            Acc a = Get(accs, mat);
            Matrix4x4 m = frame * Matrix4x4.TRS(centre, rot, Vector3.one);
            Vector3 h = size * 0.5f;
            foreach (var n in CubeN)
            {
                // two axes across the face, ordered so the winding faces out (Unity: clockwise)
                Vector3 r = n == Vector3.up || n == Vector3.down ? Vector3.right : Vector3.Cross(Vector3.up, n);
                if (n == Vector3.down) r = Vector3.left;
                Vector3 u = Vector3.Cross(r, n);
                int b = a.V.Count;
                for (int k = 0; k < 4; k++)
                {
                    float sr = (k == 1 || k == 2) ? 1 : -1, su = k >= 2 ? 1 : -1;
                    Vector3 p = Vector3.Scale(n + r * sr + u * su, h);
                    a.V.Add(m.MultiplyPoint3x4(p));
                    a.N.Add(m.MultiplyVector(n).normalized);
                }
                a.T.Add(b); a.T.Add(b + 2); a.T.Add(b + 1);
                a.T.Add(b); a.T.Add(b + 3); a.T.Add(b + 2);
            }
        }

        const int LON = 8, LAT = 6;
        static Vector3[] _unit;

        static void Ell(Dictionary<Material, Acc> accs, Material mat, Matrix4x4 frame, Vector3 centre, Vector3 size)
        {
            if (_unit == null)
            {
                _unit = new Vector3[(LAT + 1) * (LON + 1)];
                for (int i = 0; i <= LAT; i++)
                    for (int j = 0; j <= LON; j++)
                    {
                        float v = i / (float)LAT * Mathf.PI, uu = j / (float)LON * Mathf.PI * 2f;
                        _unit[i * (LON + 1) + j] = new Vector3(Mathf.Sin(v) * Mathf.Cos(uu), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(uu));
                    }
            }
            Acc a = Get(accs, mat);
            Matrix4x4 m = frame * Matrix4x4.Translate(centre);
            Vector3 h = size * 0.5f;
            int b = a.V.Count;
            foreach (var p in _unit)
            {
                a.V.Add(m.MultiplyPoint3x4(Vector3.Scale(p, h)));
                a.N.Add(m.MultiplyVector(new Vector3(p.x / h.x, p.y / h.y, p.z / h.z)).normalized);
            }
            for (int i = 0; i < LAT; i++)
                for (int j = 0; j < LON; j++)
                {
                    int q = b + i * (LON + 1) + j, w = q + LON + 1;
                    a.T.Add(q); a.T.Add(q + 1); a.T.Add(w);
                    a.T.Add(q + 1); a.T.Add(w + 1); a.T.Add(w);
                }
        }
    }

    /// <summary>Bobs the crowd's groups when it reacts (applause, a cheer, an "ooh"), each group at its own beat.</summary>
    public sealed class CrowdMotion : MonoBehaviour
    {
        public readonly List<Transform> Groups = new List<Transform>();
        readonly List<Vector3> _rest = new List<Vector3>();
        public float Excitement { get; private set; }

        public void Add(Transform[] groups) { foreach (var g in groups) { Groups.Add(g); _rest.Add(g.localPosition); } }

        public void Excite(float level) { Excitement = Mathf.Max(Excitement, Mathf.Clamp01(level)); }

        void Update()
        {
            Excitement = Mathf.Max(0f, Excitement - Time.deltaTime * 0.35f);
            float t = Time.time;
            for (int i = 0; i < Groups.Count; i++)
            {
                float beat = Mathf.Abs(Mathf.Sin(t * (7.5f + (i % 4) * 0.9f) + i * 1.7f));
                Groups[i].localPosition = _rest[i] + Vector3.up * (beat * 0.045f * Excitement);
            }
        }
    }
}
