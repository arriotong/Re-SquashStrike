// ===========================================================
// Assets/SquashGame/Runtime/Views/BallView.cs
// Port of lab/ball/BallView.js. Reads the ball, never writes it.
//
// PHYSICS radius vs VISUAL radius are separate on purpose: the mesh is
// drawn render.ballVisualScale x larger so it can be tracked from the
// gameplay camera, centred exactly on the physics position. Colour
// shows temperature (presentation only). Spin is integrated from the
// real angular velocity, so what you see spinning really is spinning.
// A short fading trail follows the drawn ball (presentation only) so it is
// easy to pick up in flight.
// PR2: never smaller than MinPixels on screen (far up the court the real
// size is a few pixels), and a thin light rim so it reads against the
// dark stands as well as the white walls.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class BallView : MonoBehaviour
    {
        /// <summary>Accessibility setting: the drawn ball's size x this (render only; the physics ball is unchanged).</summary>
        public static float UserScale = 1f;
        /// <summary>Practice: a long "where that went" trail (else the short one).</summary>
        public static bool LongTrail;
        /// <summary>PR2: the drawn ball's smallest diameter on screen, in pixels at 1080p.</summary>
        public const float MinPixels = 9f;
        /// <summary>PR2: the last drawn diameter in metres (for tests).</summary>
        public float DrawnDiameter { get; private set; }

        static readonly Color Cold = new Color(0.20f, 0.30f, 0.55f);
        static readonly Color Hot = new Color(0.95f, 0.35f, 0.15f);

        SimRunnerBase _runner;
        Transform _ball, _shadow, _rim;
        Material _ballMat, _shadowMat;
        TrailRenderer _trail;
        Vector3 _lastPos;

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner;
            _ballMat = palette.LitColor(Cold);
            _ball = Greybox.Primitive(PrimitiveType.Sphere, "Ball", transform, _ballMat).transform;
            // The rim: a slightly larger shell drawn inside-out (front faces culled), so only its edge shows.
            var rimMat = palette.UnlitColor(new Color(0.96f, 0.95f, 0.88f));
            rimMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
            _rim = Greybox.Primitive(PrimitiveType.Sphere, "BallRim", _ball, rimMat, false).transform;
            _rim.localScale = Vector3.one * 1.22f;

            _shadowMat = palette.Clear(new Color(0, 0, 0, 0.4f));
            var shadow = Greybox.MeshObject("BallShadow", transform, Greybox.Disc(0.5f, 20), _shadowMat);
            _shadow = shadow.transform;
            _shadow.rotation = Quaternion.Euler(-90f, 0f, 0f);   // disc (+Z facing) turned to face up

            _trail = _ball.gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = palette.Clear(new Color(1f, 1f, 1f, 1f));
            _trail.time = 0.16f;
            _trail.minVertexDistance = 0.03f;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.8f), 1f) },
                      new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;
        }

        void LateUpdate()
        {
            if (_runner == null || _runner.Clock == null) return;
            BallState ball = _runner.Ball;
            SimConfig cfg = PhysicsConfig.Current;

            float visualRadius = (float)(cfg.Ball.Radius * cfg.Render.BallVisualScale) * UserScale;
            Vector3 p = _runner.View.BallPos;
            visualRadius = Mathf.Max(visualRadius, MinRadiusAt(p) * UserScale);
            DrawnDiameter = visualRadius * 2f;
            _ball.position = p;
            _ball.localScale = Vector3.one * (visualRadius * 2f);   // Unity sphere primitive has radius 0.5
            if ((p - _lastPos).sqrMagnitude > 4f) _trail.Clear();     // a reset / new feed: no streak across the court
            _lastPos = p;
            // Faster ball, longer and brighter streak (presentation only).
            float speed = (float)V3.Length(ball.Vel);
            float fast = Mathf.Clamp01((speed - 8f) / 22f);
            _trail.startWidth = visualRadius * 1.5f;
            _trail.time = LongTrail ? 1.4f : Mathf.Lerp(0.14f, 0.24f, fast);
            _trail.endWidth = 0f;
            var tc = new Color(1f, 0.95f, 0.8f);
            _trail.colorGradient = TrailGradient(tc, Mathf.Lerp(0.4f, 0.65f, fast));

            // Visual spin, only while the sim is actually running.
            double w = V3.Length(ball.Omega);
            float simDt = _runner.Clock.Paused ? 0f : Time.unscaledDeltaTime * (float)_runner.Clock.TimeScale;
            if (w > 1e-4 && simDt > 0f)
                _ball.rotation = SimToWorld.Rotation(ball.Omega, w * simDt) * _ball.rotation;

            float t = (float)BallTemperature.Normalised(ball.Temperature);
            _ballMat.color = Color.Lerp(Cold, Hot, t);

            // Shadow fades and spreads with height: a depth cue for judging the ball.
            float h = Mathf.Max(0f, p.y);
            float spread = 1f + h * 0.35f;
            _shadow.position = new Vector3(p.x, 0.006f, p.z);
            float d = visualRadius * 5f * spread * 2f;
            _shadow.localScale = new Vector3(d, d, 1f);
            _shadowMat.color = new Color(0, 0, 0, Mathf.Max(0.05f, 0.4f / spread));
        }

        /// <summary>The radius that makes the ball MinPixels across (scaled to the window's height) at this point.</summary>
        static float MinRadiusAt(Vector3 p)
        {
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            float d = Vector3.Dot(p - cam.transform.position, cam.transform.forward);
            if (d <= 0.1f) return 0f;
            float viewHeight = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);   // metres seen top to bottom at that depth
            float px = MinPixels * Mathf.Max(1, cam.pixelHeight) / 1080f;
            return 0.5f * px * viewHeight / Mathf.Max(1, cam.pixelHeight);
        }

        readonly Gradient _gradient = new Gradient();
        float _gradientAlpha = -1f;
        Gradient TrailGradient(Color c, float alpha)
        {
            if (Mathf.Abs(alpha - _gradientAlpha) < 0.02f) return _gradient;
            _gradientAlpha = alpha;
            _gradient.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                              new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return _gradient;
        }
    }
}
