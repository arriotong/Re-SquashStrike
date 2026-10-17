// ===========================================================
// Assets/SquashGame/Runtime/Views/AimTargetView.cs
// M6: the always-on aim target. A ring on the front wall where the
// local player's current aim would send the ball (ShotPreview: the swing's
// own launch maths, flown through the real ball physics). The ring's size
// is the expected spread: tight on a set, prepared stroke; wide when quick,
// stretched or volleyed. Dim when no contact is predicted yet (the ball
// is on its way to the opponent): then it is the aim's straight line.
// Red when the shot would go down (tin / floor).
//
// One per local player (two colours in local 2P). Presentation only.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class AimTargetView : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public int Athlete;
        public Color Colour = new Color(1f, 0.78f, 0.22f);

        /// <summary>The preview drawn this frame (tests and diagnostics).</summary>
        public readonly ShotPreview Preview = new ShotPreview();
        public bool Shown { get; private set; }
        /// <summary>The smoothed world position of the target centre.</summary>
        public Vector3 WorldPoint { get; private set; }

        Transform _ring, _dot;
        Material _mat;
        Vector3 _pos;
        float _radius = 0.3f;
        bool _hasPos;

        public void Build(SimRunnerBase runner, int athlete, GreyboxPalette palette, Color colour)
        {
            Runner = runner; Athlete = athlete; Colour = colour;
            _mat = palette.Clear(colour);
            // Unit ring (radius 1) scaled to the spread; a fixed-size centre dot.
            _ring = Greybox.MeshObject("Ring", transform, Greybox.Torus(1f, 0.06f, 48, 6), _mat).transform;
            _dot = Greybox.MeshObject("Dot", transform, Greybox.Torus(0.045f, 0.045f, 12, 6), _mat).transform;
        }

        bool Visible()
        {
            if (Runner == null) return false;
            if (Runner.ReplayActive) return false;
            var mr = Runner as MatchRunner;
            if (mr == null) return true;                                  // the practice court: always
            if (mr.InSetup || mr.Sim == null) return false;
            var ph = mr.Sim.Phase;
            return ph == MatchPhase.WarmUp || ph == MatchPhase.AwaitToss || ph == MatchPhase.Rally;
        }

        void LateUpdate()
        {
            IntentSource src = Runner != null ? Runner.LocalSource(Athlete) : null;
            Shown = src != null && Visible();
            _ring.gameObject.SetActive(Shown);
            _dot.gameObject.SetActive(Shown);
            if (!Shown) { _hasPos = false; return; }

            var athletes = Runner.Athletes;
            Preview.Update(athletes[Athlete], src.Intent, Runner.Ball, Runner.Surfaces);
            if (!Preview.Valid) return;

            // Just off the wall, toward the court, so it never z-fights the plaster.
            Vector3 target = SimToWorld.Pos(Preview.FrontPoint.X, System.Math.Max(0.03, Preview.FrontPoint.Y), 0.012);
            float radius = Mathf.Clamp((float)Preview.Spread, 0.2f, 0.9f);
            float dt = Time.unscaledDeltaTime;
            // The prediction refreshes every few steps: ease toward it so the target glides, not jitters.
            float k = 1f - Mathf.Exp(-18f * dt);
            _pos = _hasPos ? Vector3.Lerp(_pos, target, k) : target;
            _radius = _hasPos ? Mathf.Lerp(_radius, radius, k) : radius;
            _hasPos = true;
            WorldPoint = _pos;

            _ring.position = _pos; _dot.position = _pos;
            _ring.rotation = Quaternion.identity; _dot.rotation = Quaternion.identity;   // the wall is the world XY plane
            _ring.localScale = new Vector3(_radius, _radius, 1f);

            Color c = Preview.Down ? new Color(0.95f, 0.25f, 0.2f) : Colour;
            c.a = Preview.FromContact ? 0.9f : 0.45f;
            _mat.color = c;
        }
    }
}
