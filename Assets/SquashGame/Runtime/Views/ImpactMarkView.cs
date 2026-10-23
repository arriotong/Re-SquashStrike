// ===========================================================
// Assets/SquashGame/Runtime/Views/ImpactMarkView.cs
// PR2: where the ball hits a wall or the floor, a quick mark: a dark
// smudge that fades (like the real ball's skid) inside a light ring that
// opens and vanishes, so the eye catches every bounce. The harder the hit,
// the bigger the ring. Reads the sim's impact events (and a replay's, as
// it plays) and never writes anything. Presentation only.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class ImpactMarkView : MonoBehaviour
    {
        public static bool Enabled = true;
        const int POOL = 10;
        const float LIFE = 0.7f;

        sealed class Mark { public Transform Smudge, Ring; public Material SmudgeMat, RingMat; public float Age = 99f, Size; }

        SimRunnerBase _runner;
        readonly List<Mark> _marks = new List<Mark>();
        int _next;
        public int Shown { get; private set; }

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner;
            Mesh quad = Quad();
            Texture2D soft = SoftDisc(), ring = RingTex();
            for (int i = 0; i < POOL; i++)
            {
                var m = new Mark();
                m.SmudgeMat = palette.Clear(new Color(0.12f, 0.12f, 0.14f, 0f)); m.SmudgeMat.mainTexture = soft;
                m.RingMat = palette.Clear(new Color(1f, 1f, 0.95f, 0f)); m.RingMat.mainTexture = ring;
                m.Smudge = Greybox.MeshObject("ImpactSmudge", transform, quad, m.SmudgeMat).transform;
                m.Ring = Greybox.MeshObject("ImpactRing", transform, quad, m.RingMat).transform;
                m.Smudge.gameObject.SetActive(false); m.Ring.gameObject.SetActive(false);
                _marks.Add(m);
            }
            runner.Stepped += OnEvents;
            runner.FrameSounds += OnFrameEvents;
        }

        void OnDestroy()
        {
            if (_runner != null) { _runner.Stepped -= OnEvents; _runner.FrameSounds -= OnFrameEvents; }
        }

        void OnFrameEvents(IReadOnlyList<SimEvent> events, float speed) { OnEvents(events); }

        void OnEvents(IReadOnlyList<SimEvent> events)
        {
            if (!Enabled) return;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Type != SimEventType.Impact || e.Resting || e.NormalSpeed < 1.5) continue;
                V3 n;
                switch (e.SurfaceClass)
                {
                    case "frontWall": case "tin": n = new V3(0, 0, 1); break;
                    case "backWall": n = new V3(0, 0, -1); break;
                    case "sideWall": n = new V3(e.Point.X > 0 ? -1 : 1, 0, 0); break;
                    case "floor": n = new V3(0, 1, 0); break;
                    default: continue;
                }
                Show(SimToWorld.Pos(e.Point), SimToWorld.Dir(n), (float)e.NormalSpeed);
            }
        }

        void Show(Vector3 at, Vector3 normal, float normalSpeed)
        {
            Mark m = _marks[_next];
            _next = (_next + 1) % _marks.Count;
            m.Age = 0f;
            m.Size = Mathf.Lerp(0.16f, 0.42f, Mathf.Clamp01(normalSpeed / 20f));
            // The ball's centre is a radius off the surface: put the mark on it, a hair proud.
            Vector3 p = at - normal * (float)(PhysicsConfig.Current.Ball.Radius - 0.004);
            Quaternion r = Quaternion.LookRotation(-normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up);
            m.Smudge.SetPositionAndRotation(p, r);
            m.Ring.SetPositionAndRotation(p + normal * 0.002f, r);
            m.Smudge.gameObject.SetActive(true); m.Ring.gameObject.SetActive(true);
            Shown++;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _marks.Count; i++)
            {
                Mark m = _marks[i];
                if (m.Age > LIFE) continue;
                m.Age += dt;
                float t = Mathf.Clamp01(m.Age / LIFE);
                if (t >= 1f) { m.Smudge.gameObject.SetActive(false); m.Ring.gameObject.SetActive(false); continue; }
                float smudge = 0.06f + 0.02f * t;
                m.Smudge.localScale = new Vector3(smudge, smudge, 1f);
                m.SmudgeMat.color = new Color(0.12f, 0.12f, 0.14f, 0.55f * (1f - t));
                float rt = Mathf.Clamp01(t * 2.2f);                                     // the ring is quicker
                float ring = Mathf.Lerp(0.05f, m.Size, 1f - (1f - rt) * (1f - rt));
                m.Ring.localScale = new Vector3(ring, ring, 1f);
                m.RingMat.color = new Color(1f, 1f, 0.95f, 0.7f * (1f - rt));
            }
        }

        // ---------------- meshes and textures ----------------

        /// <summary>A unit quad in the local XY plane facing -Z (so LookRotation(-normal) shows it from the court).</summary>
        static Mesh Quad()
        {
            var m = new Mesh { name = "ImpactQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Texture2D _soft, _ring;

        static Texture2D SoftDisc()
        {
            if (_soft != null) return _soft;
            _soft = Radial(64, d => Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            return _soft;
        }

        static Texture2D RingTex()
        {
            if (_ring != null) return _ring;
            _ring = Radial(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.1f));
            return _ring;
        }

        static Texture2D Radial(int size, System.Func<float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "ImpactTex" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    px[y * size + x] = new Color(1, 1, 1, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }
    }
}
