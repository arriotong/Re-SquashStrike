// ===========================================================
// Assets/SquashGame/Runtime/Views/LandingMarkerView.cs
// Where the ball will land (playtest 2026-10-19: "balls are hard to
// chase - I can't see where it lands"): a light square on the floor at
// the ball's next bounce, drawn while it is in the air. It is the real
// flight - a copy of the ball flown through the same physics - worked
// out again whenever the ball meets something (a wall, the racket), so
// it is exact and costs nothing in between. Fades in as it is found, a
// soft pulse as the bounce comes. Settings > Gameplay > Landing marker.
// Presentation only.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class LandingMarkerView : MonoBehaviour
    {
        /// <summary>Settings > Gameplay > Landing marker.</summary>
        public static bool Enabled = true;
        public static readonly Color Colour = new Color(0.62f, 0.95f, 0.05f, 0.85f);   // a strong lime: it must read on the light floor
        const float Size = 0.55f;

        public SimRunnerBase Runner;
        Transform _square, _inner;
        Material _mat, _innerMat;
        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        bool _dirty = true, _has;
        Vector3 _land;
        double _landAt;               // sim time of the bounce
        float _fade, _sinceSolve;

        /// <summary>Drawn now, and where (world) - tests.</summary>
        public bool Showing { get; private set; }
        public Vector3 Point { get { return _land; } }

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            Runner = runner;
            _mat = palette.Clear(Colour);
            _innerMat = palette.Clear(new Color(1f, 1f, 1f, 0.85f));
            _square = Square("Landing", _mat, Size);
            _inner = Square("LandingCentre", _innerMat, Size * 0.28f);
            Runner.Stepped += OnStepped;
            Hide();
        }

        Transform Square(string name, Material m, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);           // flat on the floor, facing up
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void OnDestroy() { if (Runner != null) Runner.Stepped -= OnStepped; }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i].Type == SimEventType.Impact || events[i].Type == SimEventType.RacketContact || events[i].Type == SimEventType.BodyHit) { _dirty = true; return; }
        }

        bool Wanted()
        {
            if (!Enabled || Runner == null || Runner.Clock == null || Runner.ReplayActive || Runner.FramesActive) return false;
            var ball = Runner.Ball;
            if (ball == null || ball.Asleep) return false;
            if (Runner is MatchRunner mr)
            {
                if (mr.Demo || mr.InSetup || mr.Sim == null) return false;
                if (mr.Sim.Phase != MatchPhase.Rally || mr.Sim.Referee.Outcome != null) return false;
            }
            return true;
        }

        /// <summary>Fly a copy of the ball to its next bounce.</summary>
        void Solve()
        {
            _dirty = false; _sinceSolve = 0f; _has = false;
            var b = Runner.Ball;
            _scratch.Reset(b.Pos, b.Vel, b.Omega, b.Temperature);
            _scratch.DipOffFrontWall = b.DipOffFrontWall;
            _scratch.FloorSinceFrontWall = b.FloorSinceFrontWall;
            const double dt = 1.0 / 240;
            for (int i = 0; i < 240 * 3; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, Runner.Surfaces, dt, _events);
                foreach (var e in _events)
                {
                    if (e.Type != SimEventType.Impact) continue;
                    if (RallyReferee.OutReason(e) != null) return;                    // going out: nothing to chase
                    if (e.SurfaceClass == "floor")
                    {
                        _land = SimToWorld.Pos(e.Point.X, 0.012, e.Point.Z);
                        _landAt = Runner.Clock.SimulatedTime + (i + 1) * dt;
                        _has = true;
                        return;
                    }
                }
                if (_scratch.Asleep) return;
            }
        }

        void Hide()
        {
            Showing = false;
            _square.gameObject.SetActive(false); _inner.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!Wanted()) { _fade = 0f; _dirty = true; if (Showing) Hide(); return; }
            _sinceSolve += Time.unscaledDeltaTime;
            if (_dirty || _sinceSolve > 0.5f) Solve();
            double left = _landAt - Runner.Clock.SimulatedTime;
            if (!_has || left < -0.05) { _fade = 0f; if (Showing) Hide(); return; }
            Showing = true;
            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / 0.12f);
            _square.gameObject.SetActive(true); _inner.gameObject.SetActive(true);
            _square.position = _land; _inner.position = _land + Vector3.up * 0.001f;
            float near = Mathf.Clamp01(1f - (float)left / 0.6f);                      // the last 0.6 s: a soft pulse
            float pulse = 1f + 0.12f * near * Mathf.Sin(Time.unscaledTime * 18f);
            _square.localScale = new Vector3(Size * pulse, Size * pulse, 1f);
            var c = Colour; c.a *= _fade * (0.85f + 0.15f * near);
            _mat.color = c;
            _innerMat.color = new Color(1f, 1f, 1f, 0.85f * _fade);
        }
    }
}
