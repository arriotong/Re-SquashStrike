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
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class BallView : MonoBehaviour
    {
        /// <summary>Accessibility setting: the drawn ball's size x this (render only; the physics ball is unchanged).</summary>
        public static float UserScale = 1f;

        static readonly Color Cold = new Color(0.20f, 0.30f, 0.55f);
        static readonly Color Hot = new Color(0.95f, 0.35f, 0.15f);

        SimRunnerBase _runner;
        Transform _ball, _shadow;
        Material _ballMat, _shadowMat;
        TrailRenderer _trail;
        Vector3 _lastPos;

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner;
            _ballMat = palette.LitColor(Cold);
            _ball = Greybox.Primitive(PrimitiveType.Sphere, "Ball", transform, _ballMat).transform;

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
            _ball.position = p;
            _ball.localScale = Vector3.one * (visualRadius * 2f);   // Unity sphere primitive has radius 0.5
            if ((p - _lastPos).sqrMagnitude > 4f) _trail.Clear();     // a reset / new feed: no streak across the court
            _lastPos = p;
            _trail.startWidth = visualRadius * 1.6f;
            _trail.endWidth = 0f;

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
    }
}
