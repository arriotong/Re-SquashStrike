// ===========================================================
// Assets/SquashGame/Runtime/Replays/Cutscenes.cs
// Camera work (2026-10-28): "tournament finals can have the camera flying
// around before the match starts so it makes you feel prestiged, also add
// in player walking from the player path to the court" (the user; plan:
// RE_SQUASHSTRIKE_CAMERA_WORK_PLAN.md).
//
//   the walk-on   every match (Settings > Camera > Cutscenes): the players
//                 come down the path behind the back glass, through the
//                 court's door and to their places - first from behind
//                 them, then from the front wall as they walk in
//   prestige      a final, the Tour Finals, the World Championship, a
//                 Gauntlet boss, the world No. 1: first a slow turn round
//                 the venue, high and coming down, with the title
//
// Made of frames worked out here (positions, facing, a walking velocity
// so the gait animates) and played through the runner's replay path: the
// sim waits, and nothing in it changes. Any key skips.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame.Replays
{
    public static class Cutscenes
    {
        /// <summary>Frames a second (the runner plays them at this rate).</summary>
        public const float Rate = 30f;
        public const float OrbitSeconds = 7f, WalkSpeed = 1.45f, Hold = 0.7f, Behind = 0.9f;
        public const string Intro = "intro";

        /// <summary>The frames: the orbit (when prestige), then the walk-on, ending exactly on <paramref name="end"/> (the match
        /// as it is now). orbitShare: the share of the frames the orbit takes (the camera turns over it).</summary>
        public static List<RenderSnapshot> Build(RenderSnapshot end, bool orbit, out float orbitShare)
        {
            var frames = new List<RenderSnapshot>();
            double L = CourtDimensions.LENGTH;
            // the path: down the lane behind the back glass, through the door (the middle of the back wall), to the place
            var paths = new List<Vector3>[2];
            for (int i = 0; i < end.Count && i < 2; i++)
            {
                var at = end.Athlete(i).Pos;
                float side = i == 0 ? -0.35f : 0.35f;
                paths[i] = new List<Vector3>
                {
                    SimToWorld.Pos(side * 1.5f, 0f, L + 4.2f),
                    SimToWorld.Pos(side, 0f, L + 1.2f),
                    SimToWorld.Pos(side * 0.5f, 0f, L - 0.6f),
                    new Vector3(at.x, 0f, at.z),
                };
            }
            float walk = 0;
            for (int i = 0; i < end.Count && i < 2; i++) walk = Mathf.Max(walk, Length(paths[i]) / WalkSpeed + (i == 1 ? Behind : 0));
            float orbitT = orbit ? OrbitSeconds : 0f, total = orbitT + walk + Hold;
            int n = Mathf.CeilToInt(total * Rate) + 1;
            var hidden = new Vector3(0f, -20f, 0f);
            for (int f = 0; f < n; f++)
            {
                float t = f / Rate;
                var s = new RenderSnapshot { Count = end.Count, BallPos = t >= total - 0.05f ? end.BallPos : hidden };
                for (int i = 0; i < end.Count && i < 2; i++)
                {
                    float wt = Mathf.Max(0f, t - orbitT - (i == 1 ? Behind : 0f));
                    var a = Walker(end.Athlete(i), paths[i], wt * WalkSpeed);
                    if (i == 0) s.A0 = a; else s.A1 = a;
                }
                frames.Add(s);
            }
            orbitShare = total > 0 ? orbitT / total : 0;
            return frames;
        }

        static float Length(List<Vector3> p) { float d = 0; for (int i = 1; i < p.Count; i++) d += Vector3.Distance(p[i - 1], p[i]); return d; }

        /// <summary>An athlete this far down its path: walking (facing the way, a walking velocity), or at the end in its own pose.</summary>
        static AthleteSnapshot Walker(AthleteSnapshot end, List<Vector3> path, float dist)
        {
            Vector3 pos = path[path.Count - 1], dir = end.Forward;
            bool moving = false;
            for (int k = 1; k < path.Count; k++)
            {
                float seg = Vector3.Distance(path[k - 1], path[k]);
                if (dist <= seg)
                {
                    dir = (path[k] - path[k - 1]).normalized;
                    pos = Vector3.Lerp(path[k - 1], path[k], seg > 1e-4f ? dist / seg : 1f);
                    moving = true;
                    break;
                }
                dist -= seg;
            }
            if (!moving) return end;
            pos.y = end.Pos.y;
            var shift = pos - end.Pos;
            var a = end;
            a.Pos = pos;
            a.Forward = dir; a.ShoulderForward = dir;
            a.Velocity = dir * WalkSpeed;
            a.RacketAnchor = end.RacketAnchor + shift; a.RacketHead = end.RacketHead + shift;
            a.Swinging = false; a.Swing = SwingPhase.Idle; a.SwingT = 0; a.HasPred = false; a.Diving = false; a.GetUp = 0; a.Reach = 0; a.Stamina = 1f;
            return a;
        }

        // ---------------- the cameras ----------------

        /// <summary>Point the camera for this point of a cutscene (u: 0..1 through it; orbitShare: the orbit's part).</summary>
        public static void Film(Camera cam, RenderSnapshot v, float u, float orbitShare, bool high, ref Vector3 pos, ref Vector3 look, float dt)
        {
            Vector3 centre = SimToWorld.Pos(0f, 1.2f, (float)CourtDimensions.LENGTH / 2f);
            Vector3 p, l; float fov;
            if (u < orbitShare)
            {
                // the turn round the venue: one full turn, coming down, ending behind the back glass
                float o = u / orbitShare;
                float e = o * o * (3f - 2f * o);
                float ang = Mathf.Lerp(-180f, 180f, e) * Mathf.Deg2Rad;
                Vector3 back = SimToWorld.Pos(0f, 0f, (float)CourtDimensions.LENGTH + 10f) - SimToWorld.Pos(0f, 0f, (float)CourtDimensions.LENGTH / 2f);
                back.y = 0; back.Normalize();
                Vector3 radial = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, Vector3.up) * back;
                float r = high ? Mathf.Lerp(16f, 12f, e) : 5.2f, h = high ? Mathf.Lerp(12f, 6.5f, e) : Mathf.Lerp(5.2f, 3.8f, e);
                p = centre + radial * r + Vector3.up * h; l = centre; fov = high ? 56f : 70f;
                Apply(cam, p, l, fov, ref pos, ref look, dt, o < 0.02f);
                return;
            }
            float w = orbitShare < 1f ? (u - orbitShare) / (1f - orbitShare) : 1f;
            Vector3 walkers = v.Count > 1 ? (v.A0.Pos + v.A1.Pos) * 0.5f : v.A0.Pos;
            if (w < 0.4f)
            {
                // behind them, low, as they come down the path to the door
                // (close and high: the lane behind the glass, never back in the stands)
                p = walkers + (SimToWorld.Pos(0f, 0f, (float)CourtDimensions.LENGTH + 10f) - SimToWorld.Pos(0f, 0f, (float)CourtDimensions.LENGTH)).normalized * 1.5f + Vector3.up * 2.5f;
                l = walkers + Vector3.up * 0.9f + (centre - walkers).normalized * 2.5f;
                fov = 62f;
                Apply(cam, p, l, fov, ref pos, ref look, dt, w < 0.01f);
                return;
            }
            // the front-wall cam: they walk in toward it, to their places
            var f = SimToWorld.Pos(0f, 1.45f, 0.3f);
            p = new Vector3(f.x + (walkers.x - f.x) * 0.25f, f.y, f.z);
            l = new Vector3(walkers.x, 1.25f, walkers.z);
            fov = Mathf.Lerp(46f, 58f, (w - 0.4f) / 0.6f);
            Apply(cam, p, l, fov, ref pos, ref look, dt, w < 0.42f && Vector3.Distance(pos, p) > 3f);
        }

        static void Apply(Camera cam, Vector3 p, Vector3 l, float fov, ref Vector3 pos, ref Vector3 look, float dt, bool cut)
        {
            float k = cut ? 1f : 1f - Mathf.Exp(-dt * 5f);
            pos = Vector3.Lerp(pos, p, k); look = Vector3.Lerp(look, l, k);
            cam.fieldOfView = cut ? fov : Mathf.Lerp(cam.fieldOfView, fov, k);
            cam.transform.position = pos;
            cam.transform.LookAt(look, Vector3.up);
        }
    }
}
