// ===========================================================
// Assets/SquashGame/Runtime/Views/AimTargetView.cs
// M6: the always-on aim target. A ring on the front wall where the
// local player's current aim would send the ball (ShotPreview: the swing's
// own launch maths, flown through the real ball physics). The ring's size
// is the expected spread: tight on a set, prepared stroke; wide when quick,
// stretched or volleyed. Dim when no contact is predicted yet (the ball
// is on its way to the opponent): then it is the aim's straight line.
// Red when the shot would go down (tin / floor). For a boast (aim pushed
// past the edge of the front wall) the ring sits on that side wall, at the
// point the shot is solved to strike first, tagged BOAST.
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
        /// <summary>M9: the ring is not drawn any more (drag-to-aim); the preview is still computed (tests, notes).</summary>
        public bool DrawRing = false;

        /// <summary>The preview drawn this frame (tests and diagnostics).</summary>
        public readonly ShotPreview Preview = new ShotPreview();
        public bool Shown { get; private set; }
        /// <summary>The smoothed world position of the target centre.</summary>
        public Vector3 WorldPoint { get; private set; }
        /// <summary>0 = aiming at the front wall; -1 / +1 = a boast off the left / right side wall.</summary>
        public int Wall { get; private set; }
        GUIStyle _tag;

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
            _ring.gameObject.SetActive(Shown && DrawRing);
            _dot.gameObject.SetActive(Shown && DrawRing);
            if (!Shown) { _hasPos = false; return; }

            var athletes = Runner.Athletes;
            Preview.Update(athletes[Athlete], src.Intent, Runner.Ball, Runner.Surfaces);
            if (!Preview.Valid) return;

            // Just off the wall, toward the court, so it never z-fights the plaster.
            int wall = Preview.TargetWall;
            Vector3 target = wall == 0
                ? SimToWorld.Pos(Preview.FrontPoint.X, System.Math.Max(0.03, Preview.FrontPoint.Y), 0.012)
                : SimToWorld.Pos(Preview.Target.X - wall * 0.012, System.Math.Max(0.03, Preview.Target.Y), Preview.Target.Z);
            if (wall != Wall) _hasPos = false;                // jump to the other wall, don't glide across the court
            Wall = wall;
            float radius = Mathf.Clamp((float)Preview.Spread, 0.2f, 0.9f);
            float dt = Time.unscaledDeltaTime;
            // The prediction refreshes every few steps: ease toward it so the target glides, not jitters.
            float k = 1f - Mathf.Exp(-18f * dt);
            _pos = _hasPos ? Vector3.Lerp(_pos, target, k) : target;
            _radius = _hasPos ? Mathf.Lerp(_radius, radius, k) : radius;
            _hasPos = true;
            WorldPoint = _pos;

            _ring.position = _pos; _dot.position = _pos;
            // The front wall is the world XY plane; a side wall is the YZ plane.
            Quaternion face = wall == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            _ring.rotation = face; _dot.rotation = face;
            _ring.localScale = new Vector3(_radius, _radius, 1f);

            Color c = Preview.Down ? new Color(0.95f, 0.25f, 0.2f) : Colour;
            c.a = Preview.FromContact ? 0.9f : 0.45f;
            // Fully charged: the special shot (kill / drop) is ready - the ring turns gold and pulses.
            var ctrl = athletes[Athlete];
            if (ctrl.ArcadeHitEnabled && (ctrl.Arcade.Charging || ctrl.Arcade.Waiting) && ctrl.Arcade.ChargeRatio >= 0.999)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
                c = Color.Lerp(new Color(1f, 0.8f, 0.1f), Color.white, 0.3f * pulse);
                c.a = 1f;
                _ring.localScale = new Vector3(_radius * (1.05f + 0.1f * pulse), _radius * (1.05f + 0.1f * pulse), 1f);
            }
            _mat.color = c;
        }

        void OnGUI()
        {
            if (!Shown || Wall == 0 || !DrawRing) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(WorldPoint + Vector3.up * (_radius + 0.25f));
            if (sp.z <= 0) return;
            if (_tag == null) _tag = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var r = new Rect(sp.x - 60, Screen.height - sp.y - 12, 120, 24);
            _tag.normal.textColor = new Color(0, 0, 0, 0.7f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), "BOAST", _tag);
            _tag.normal.textColor = Colour;
            GUI.Label(r, "BOAST", _tag);
        }
    }
}
