// ===========================================================
// Assets/SquashGame/Runtime/Replays/BroadcastDirector.cs
// Camera work (2026-10-28): "different view to the court when replaying
// ... kind of like the views in the real squash highlight videos" (the
// user; plan: RE_SQUASHSTRIKE_CAMERA_WORK_PLAN.md).
//
//   Shot        the angles of a PSA highlight: Wide (the TV camera),
//               the front-wall cam looking back at the players, the side
//               glass, a back corner, over the striker's shoulder, the
//               ball cam chasing the ball, the drone overhead
//   Cut         the director: a replay opens Wide, cuts to the glass
//               mid-rally and ends on the front-wall cam, slow; a big
//               moment ends over the shoulder or on the ball cam
//   ShotCamera  films a shot from the drawn frames: eases while the
//               shot holds, snaps on a cut, works out who struck last
//               and where the ball is heading
//
// Presentation only: it reads the frames being shown, nothing else.
// ===========================================================

using UnityEngine;

namespace SquashGame.Replays
{
    public enum Shot { Wide, FrontWall, SideGlass, Corner, Shoulder, BallCam, Overhead }

    public static class BroadcastDirector
    {
        /// <summary>The director's cut: the shot at this point of a replay (0..1). big: the rally ends on a big moment;
        /// variant: which of the pairs (so replays after each other are not cut the same).</summary>
        public static Shot Cut(float progress, bool big, int variant)
        {
            if (progress < 0.38f) return Shot.Wide;
            if (progress < 0.68f) return (variant & 1) == 0 ? Shot.SideGlass : Shot.Corner;
            if (!big || progress < 0.86f) return Shot.FrontWall;
            return (variant & 2) == 0 ? Shot.Shoulder : Shot.BallCam;
        }

        /// <summary>The two angles a reel shows a winning shot again from (slow).</summary>
        public static Shot EndingAngle(int pass, int variant) { return pass == 0 ? Shot.FrontWall : (variant & 1) == 0 ? Shot.Shoulder : Shot.Corner; }

        /// <summary>Where a shot puts the camera for this frame (world space).</summary>
        public static void Frame(Shot shot, RenderSnapshot v, Vector3 ballDir, int striker, float side, out Vector3 pos, out Vector3 look, out float fov)
        {
            Vector3 ball = v.BallPos;
            Vector3 mid = v.Count > 1 ? (v.A0.Pos + v.A1.Pos) * 0.5f : v.A0.Pos;
            Vector3 action = Vector3.Lerp(mid, ball, 0.45f); action.y = Mathf.Clamp(ball.y, 0.6f, 2.2f) * 0.5f + 0.5f;
            Vector3 frontWall = SimToWorld.Pos(0f, 1.2f, 0f), backWall = SimToWorld.Pos(0f, 1.2f, (float)SquashSim.CourtDimensions.LENGTH);
            switch (shot)
            {
                case Shot.FrontWall:                                // low in the front wall, looking back at the players' faces
                {
                    var f = SimToWorld.Pos(0f, 0.85f, 0.14f);
                    pos = new Vector3(f.x + (action.x - f.x) * 0.3f, f.y, f.z);
                    look = new Vector3(action.x, 1.15f, action.z); fov = 64f; break;
                }
                case Shot.SideGlass:                                // level with play, in the side glass
                {
                    var wall = SimToWorld.Pos(side * 3.1f, 2.0f, 0f);
                    pos = new Vector3(wall.x, 2.0f, action.z); look = action; fov = 62f; break;
                }
                case Shot.Corner:                                   // high in a back corner, down the diagonal
                    pos = SimToWorld.Pos(side * 2.9f, 4.4f, 9.55f); look = action; fov = 58f; break;
                case Shot.Shoulder:                                 // just behind the striker, the ball away to the front wall
                {
                    var a = striker == 1 && v.Count > 1 ? v.A1.Pos : v.A0.Pos;
                    var toFront = frontWall - a; toFront.y = 0;
                    toFront = toFront.sqrMagnitude > 0.01f ? toFront.normalized : (frontWall - backWall).normalized;
                    var right = Vector3.Cross(Vector3.up, toFront);
                    pos = a - toFront * 1.7f + Vector3.up * 1.8f + right * 0.45f;
                    look = Vector3.Lerp(ball, new Vector3(ball.x, 1.0f, frontWall.z), 0.35f); fov = 60f; break;
                }
                case Shot.BallCam:                                  // chasing the ball, low behind it
                {
                    var d = ballDir.sqrMagnitude > 0.01f ? ballDir.normalized : (frontWall - backWall).normalized;
                    pos = ball - d * 1.7f + Vector3.up * 0.45f;
                    pos.y = Mathf.Max(pos.y, 0.35f);
                    look = ball + d * 2.2f; fov = 68f; break;
                }
                case Shot.Overhead:                                 // the drone
                    pos = SimToWorld.Pos(0f, 9.5f, 5.6f); look = SimToWorld.Pos(0f, 0f, 4.9f); fov = 64f; break;
                default:                                            // Wide: the TV camera, high behind the glass, leaning with the play
                {
                    var b = SimToWorld.Pos(0f, 3.0f, 12.6f);
                    pos = new Vector3(b.x + action.x * 0.35f, b.y, b.z); look = Vector3.Lerp(SimToWorld.Pos(0f, 1.1f, 4.2f), action, 0.4f); fov = 44f; break;
                }
            }
        }
    }

    /// <summary>Films shots from the frames being drawn: eases while a shot holds, snaps on a cut.</summary>
    public sealed class ShotCamera
    {
        Shot _shot = (Shot)(-1);
        bool _have;
        Vector3 _pos, _look, _prevBall, _ballDir;
        float _side = 1f;
        public int Striker { get; private set; }
        public Shot Current { get { return _shot; } }

        /// <summary>Forget the last frame (a new replay).</summary>
        public void Reset() { _have = false; _shot = (Shot)(-1); _ballDir = Vector3.zero; }

        public void Film(Camera cam, Shot shot, RenderSnapshot v, float dt)
        {
            // who struck last (the one swinging), where the ball is heading
            if (v.A0.Swinging) Striker = 0; else if (v.Count > 1 && v.A1.Swinging) Striker = 1;
            if (_have)
            {
                var d = v.BallPos - _prevBall;
                if (d.sqrMagnitude > 1e-6f) _ballDir = Vector3.Lerp(_ballDir, d.normalized, 0.35f);
            }
            _prevBall = v.BallPos;
            bool cut = shot != _shot;
            if (cut)
            {
                _shot = shot;
                // the glass and the corner on the side the ball isn't (the play comes toward the lens)
                var l = SimToWorld.Pos(-3f, 0f, 0f); var r = SimToWorld.Pos(3f, 0f, 0f);
                _side = Mathf.Abs(l.x - v.BallPos.x) > Mathf.Abs(r.x - v.BallPos.x) ? -1f : 1f;
            }
            BroadcastDirector.Frame(shot, v, _ballDir, Striker, _side, out var pos, out var look, out float fov);
            float k = _have && !cut ? 1f - Mathf.Exp(-dt * (shot == Shot.BallCam ? 10f : 6f)) : 1f;
            _pos = Vector3.Lerp(_pos, pos, k); _look = Vector3.Lerp(_look, look, k);
            _have = true;
            cam.fieldOfView = cut ? fov : Mathf.Lerp(cam.fieldOfView, fov, k);
            cam.transform.position = _pos;
            cam.transform.LookAt(_look, Vector3.up);
        }
    }
}
