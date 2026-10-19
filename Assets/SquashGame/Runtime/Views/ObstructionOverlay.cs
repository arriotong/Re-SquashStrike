// ===========================================================
// Assets/SquashGame/Runtime/Views/ObstructionOverlay.cs
// P2-M7: the referee review's drawing on the floor - the striker's
// corridor to where they would play the ball, the obstructor's body
// circle, and (for a line call) the line to the front wall. Shown while
// a call is being reviewed (MatchRunner.ReviewingCall), and in the Replay
// Theatre over the last two seconds of a rally that ended on a call (the
// call is recorded with the rally: RallyRec.Call).
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class ObstructionOverlay : MonoBehaviour
    {
        MatchRunner _runner;
        LineRenderer _corridor, _body, _line;
        const int Segments = 40;
        const float Lift = 0.02f;

        /// <summary>Drawn now (tests).</summary>
        public bool Showing { get; private set; }

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner as MatchRunner;
            var lime = new Color(0.78f, 1f, 0.24f);
            var red = new Color(1f, 0.32f, 0.28f);
            _corridor = Line("Corridor", palette.UnlitColor(lime), 5, true);
            _body = Line("Obstructor", palette.UnlitColor(red), Segments, true);
            _line = Line("LineToFrontWall", palette.UnlitColor(new Color(lime.r, lime.g, lime.b, 0.8f)), 2, false);
            Hide();
        }

        LineRenderer Line(string name, Material m, int points, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m;
            lr.positionCount = points;
            lr.loop = loop;
            lr.widthMultiplier = 0.05f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        void Hide()
        {
            Showing = false;
            _corridor.enabled = _body.enabled = _line.enabled = false;
        }

        void LateUpdate()
        {
            var c = _runner != null && _runner.ReplayActive && _runner.ReviewingCall ? _runner.LastCall : TheatreCall();
            if (c == null || c.Obstruction == ObstructionKind.None) { if (Showing) Hide(); return; }
            Showing = true;
            double R = PhysicsConfig.Current.Player.Radius;
            double half = R + GameTuning.Interference.AccessClearance;

            // The corridor: a strip from the striker to the hitting point.
            double dx = c.TargetX - c.StrikerX, dz = c.TargetZ - c.StrikerZ, d = System.Math.Sqrt(dx * dx + dz * dz);
            double nx = d > 1e-6 ? -dz / d : 1, nz = d > 1e-6 ? dx / d : 0;
            _corridor.enabled = true;
            _corridor.SetPosition(0, Floor(c.StrikerX + nx * half, c.StrikerZ + nz * half));
            _corridor.SetPosition(1, Floor(c.TargetX + nx * half, c.TargetZ + nz * half));
            _corridor.SetPosition(2, Floor(c.TargetX - nx * half, c.TargetZ - nz * half));
            _corridor.SetPosition(3, Floor(c.StrikerX - nx * half, c.StrikerZ - nz * half));
            _corridor.SetPosition(4, Floor(c.StrikerX + nx * half, c.StrikerZ + nz * half));

            // The obstructor's body.
            _body.enabled = true;
            for (int i = 0; i < Segments; i++)
            {
                double a = i * 2 * System.Math.PI / Segments;
                _body.SetPosition(i, Floor(c.ObstructorX + System.Math.Cos(a) * R, c.ObstructorZ + System.Math.Sin(a) * R));
            }

            // The line to the front wall, for a line call.
            _line.enabled = c.Obstruction == ObstructionKind.Line;
            if (_line.enabled)
            {
                _line.SetPosition(0, Floor(c.TargetX, c.TargetZ));
                _line.SetPosition(1, Floor(c.TargetX, 0));
            }
        }

        /// <summary>The Replay Theatre: the playing rally's call, near its end (the last two seconds and after).</summary>
        CallSnapshot TheatreCall()
        {
            var t = Replays.TheatreSession.Active;
            if (t == null || _runner == null || !_runner.FramesActive) return null;
            var clip = t.Clip;
            if (clip == null || clip.Rec == null || clip.Rec.Call == null) return null;
            return t.Cursor >= clip.EndFrame - 2 * SimRunnerBase.FrameRate ? clip.Rec.Call : null;
        }

        static Vector3 Floor(double x, double z) { return SimToWorld.Pos(x, Lift, z); }
    }
}
