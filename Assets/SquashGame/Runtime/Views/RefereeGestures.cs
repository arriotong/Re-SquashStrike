// ===========================================================
// Assets/SquashGame/Runtime/Views/RefereeGestures.cs
// 2026-11-23 (the user: "Referee pointing: he doesn't point when making a
// call yet - fix"): the referee at the desk behind the back wall gives his
// decisions with his arms, as a referee does:
//
//   stroke, no let   an arm raised, pointing to the player who gets the point
//   let              both arms up, palms out: play it again
//   a video review   both arms up (the box drawn in the air) while it runs
//
// Then back down on the table. Reads the match (MatchRunner.Sim) each frame;
// presentation only, real time.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class RefereeGestures : MonoBehaviour
    {
        public Transform Left, Right;
        /// <summary>The way the court lies from the desk (world), and a sim x's side in the world (+ / -).</summary>
        public Vector3 Court = Vector3.forward;
        public System.Func<double, float> SideOf;
        public const float HoldSeconds = 2.2f, RaiseSeconds = 0.25f;

        Quaternion _restL, _restR;
        bool _init;
        int _seen = -1, _reviews;
        float _left;
        Quaternion _wantL, _wantR;
        MatchRunner _runner;
        /// <summary>The last gesture (tests): "point:0", "point:1", "let", "review" - and how many so far.</summary>
        public string LastGesture { get; private set; }
        public int Gestures { get; private set; }
        public static RefereeGestures Active { get; private set; }

        void OnEnable() { Active = this; }
        void OnDisable() { if (Active == this) Active = null; }

        void Update()
        {
            if (Left == null || Right == null) return;
            if (!_init) { _init = true; _restL = Left.rotation; _restR = Right.rotation; }
            if (_runner == null) _runner = FindFirstObjectByType<MatchRunner>();
            var m = _runner != null ? _runner.Sim : null;
            if (m != null)
            {
                if (_seen < 0 || _seen > m.History.Count) _seen = m.History.Count;
                if (m.History.Count > _seen)
                {
                    _seen = m.History.Count;
                    Gesture(m, m.History[m.History.Count - 1]);
                }
                if (m.Reviews != _reviews) { _reviews = m.Reviews; if (m.ReviewBy >= 0) BothUp("review"); }
                if (m.ReviewBy >= 0) _left = Mathf.Max(_left, 0.3f);          // (up for as long as the review runs)
            }
            _left -= Time.unscaledDeltaTime;
            float k = _left > 0 ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / RaiseSeconds * 4f) : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f);
            Left.rotation = Quaternion.Slerp(Left.rotation, _left > 0 ? _wantL : _restL, k);
            Right.rotation = Quaternion.Slerp(Right.rotation, _left > 0 ? _wantR : _restR, k);
        }

        void Gesture(MatchSimulation m, RallyOutcome o)
        {
            if (o == null) return;
            if (o.Kind == RallyEnd.Let) { BothUp("let"); return; }
            if (o.Kind != RallyEnd.Stroke && o.Kind != RallyEnd.NoLet) return;   // (a winner, an error: no decision to give)
            if (o.Winner < 0) return;
            // point to the winner: the arm on their side, out towards where they stand, a little up
            var p = m.Players[o.Winner].Player.Pos;
            float side = SideOf != null ? Mathf.Sign(SideOf(p.X)) : 1f;
            if (side == 0) side = 1f;
            Vector3 across = Vector3.Cross(Vector3.up, Court).normalized * side;
            Vector3 dir = (Court * 0.55f + across * 0.8f + Vector3.up * 0.35f).normalized;
            bool rightArm = Vector3.Dot(across, Right.position - Left.position) > 0;
            _wantL = rightArm ? _restL : Quaternion.LookRotation(dir, Vector3.up);
            _wantR = rightArm ? Quaternion.LookRotation(dir, Vector3.up) : _restR;
            _left = HoldSeconds;
            LastGesture = "point:" + o.Winner; Gestures++;
        }

        void BothUp(string what)
        {
            Vector3 across = Vector3.Cross(Vector3.up, Court).normalized;
            float l = Vector3.Dot(across, Left.position - transform.position) >= 0 ? 1f : -1f;
            _wantL = Quaternion.LookRotation((Court * 0.35f + Vector3.up * 0.9f + across * 0.25f * l).normalized, -Court);
            _wantR = Quaternion.LookRotation((Court * 0.35f + Vector3.up * 0.9f - across * 0.25f * l).normalized, -Court);
            _left = HoldSeconds;
            LastGesture = what; Gestures++;
        }
    }
}
