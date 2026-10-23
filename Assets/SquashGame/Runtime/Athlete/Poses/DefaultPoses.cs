// ===========================================================
// Assets/SquashGame/Runtime/Athlete/Poses/DefaultPoses.cs
// M7: the authored pose set (right-handed), Mario Tennis-like: a chunky,
// wide, low base; clear anticipation (big backswing) and follow-through;
// readable silhouettes. Built with a small builder:
//
//   Crouch(h)   pelvis down h metres          Lean(deg)   bow forward
//   Twist(deg)  turn the trunk (+ = to the athlete's right)
//   Tilt(deg)   lean the trunk sideways (+ = toward the right)
//   Feet(...)   ankle targets: x (+ left), z (+ behind), lift; toe yaw
//   Racket(...) where the racket's SWEET SPOT is, which way the shaft points
//               (butt -> tip) and which way the strings face; the arm is
//               solved to hold it there (athlete local: +X left, +Y up,
//               +Z behind; so the athlete's right is -X and forward is -Z)
//   Hand(...)   where the free (left) hand is
//   Look(...)   head look on top of the level head (+ pitch = up)
//
// Rig axes (see AthletePose): +X = left, +Y = up, +Z = behind. For a bone
// hanging down, +X rotation swings it forward and +Z swings it toward the
// athlete's left; for a bone pointing up, +X tips it back.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public static class DefaultPoses
    {
        sealed class B
        {
            public readonly AthletePose P;
            public B(string name, string group) { P = new AthletePose { Name = name, Group = group }; }

            B Add(string bone, float x, float y, float z) { P.Set(bone, P.Get(bone) + new Vector3(x, y, z)); return this; }

            public B Crouch(float h) { P.Pelvis.y -= h; return this; }
            public B PelvisShift(float x, float z) { P.Pelvis.x += x; P.Pelvis.z += z; return this; }
            public B Lean(float deg) { return Add("Spine_01", -deg * 0.4f, 0, 0).Add("Spine_02", -deg * 0.3f, 0, 0).Add("Chest", -deg * 0.3f, 0, 0); }
            public B Tilt(float deg) { return Add("Spine_01", 0, 0, -deg * 0.4f).Add("Spine_02", 0, 0, -deg * 0.3f).Add("Chest", 0, 0, -deg * 0.3f); }
            public B Twist(float deg) { return Add("Pelvis", 0, deg * 0.35f, 0).Add("Spine_01", 0, deg * 0.2f, 0).Add("Spine_02", 0, deg * 0.2f, 0).Add("Chest", 0, deg * 0.25f, 0); }

            /// <summary>Ankle targets: right foot (xr, zr), left (xl, zl); lifts; toe yaws (+ = turned right).</summary>
            public B Feet(float xr, float zr, float xl, float zl, float yawR = 10, float yawL = -10, float liftR = 0, float liftL = 0)
            {
                P.FootR = new Vector3(xr, GateA.Ankle + liftR, zr);
                P.FootL = new Vector3(xl, GateA.Ankle + liftL, zl);
                P.FootYawR = yawR; P.FootYawL = yawL;
                return this;
            }

            /// <summary>The racket by target: sweet spot, shaft direction (butt to tip), string normal; optional elbow pole.</summary>
            public B Racket(float sx, float sy, float sz, float dx, float dy, float dz, float nx, float ny, float nz,
                            float ex = -0.35f, float ey = -0.55f, float ez = 0.25f)
            {
                P.RacketIK = true;
                P.Sweet = new Vector3(sx, sy, sz);
                P.RacketDir = new Vector3(dx, dy, dz);
                P.RacketNormal = new Vector3(nx, ny, nz);
                P.ElbowR = new Vector3(ex, ey, ez);
                return this;
            }

            /// <summary>The free (left) hand by target; optional elbow pole.</summary>
            public B Hand(float x, float y, float z, float ex = 0.35f, float ey = -0.55f, float ez = 0.2f)
            {
                P.HandIK = true;
                P.HandL = new Vector3(x, y, z);
                P.ElbowL = new Vector3(ex, ey, ez);
                return this;
            }

            public B Look(float pitchUp, float yawRight = 0) { return Add("Head", pitchUp, yawRight, 0); }
            public B HeadFree() { P.LevelHead = false; return this; }
        }

        static B Pose(string name, string group) { return new B(name, group); }

        public static List<AthletePose> Build()
        {
            var poses = new List<B>
            {
                // ---------------- stance: racket up in front, free hand by the throat ----------------
                // ---------------- stance: low and wide, weight forward, racket head up and angled out (anim rework 2026-10-23) ----------------
                Pose("ready", "stance").Crouch(0.23f).Lean(30).Feet(-0.31f, -0.02f, 0.31f, 0.04f, 20, -16)
                    .Racket(-0.36f, 1.36f, -0.42f, -0.35f, 0.80f, -0.40f, 0.25f, 0.15f, -0.95f).Hand(0.18f, 1.05f, -0.36f),
                Pose("split_hop", "stance").Crouch(0.10f).Lean(24).Feet(-0.33f, 0, 0.33f, 0, 20, -16, 0.06f, 0.06f)
                    .Racket(-0.38f, 1.46f, -0.40f, -0.35f, 0.85f, -0.35f, 0.25f, 0.15f, -0.95f).Hand(0.26f, 1.12f, -0.32f),
                Pose("shuffle", "move").Crouch(0.25f).Lean(28).Feet(-0.40f, 0, 0.40f, 0, 22, -22)
                    .Racket(-0.36f, 1.30f, -0.42f, -0.35f, 0.80f, -0.40f, 0.25f, 0.15f, -0.95f).Hand(0.22f, 1.00f, -0.38f),
                Pose("run_a", "move").Crouch(0.12f).Lean(28).Twist(10).Feet(-0.12f, -0.46f, 0.12f, 0.36f, 5, -5, 0.04f, 0.22f)
                    .Racket(-0.34f, 1.10f, 0.05f, -0.1f, 0.75f, 0.55f, 0.4f, 0.2f, -0.9f).Hand(0.24f, 1.05f, -0.45f),
                Pose("run_b", "move").Crouch(0.12f).Lean(28).Twist(-10).Feet(-0.12f, 0.36f, 0.12f, -0.46f, 5, -5, 0.22f, 0.04f)
                    .Racket(-0.34f, 1.25f, -0.55f, -0.1f, 0.75f, -0.6f, 0.4f, 0.2f, -0.9f).Hand(0.24f, 0.80f, 0.32f),
                Pose("backpedal", "move").Crouch(0.18f).Lean(16).Feet(-0.20f, 0.22f, 0.20f, -0.16f, 12, -12, 0, 0.10f)
                    .Racket(-0.36f, 1.40f, -0.38f, -0.3f, 0.85f, -0.35f, 0.25f, 0.15f, -0.95f).Hand(0.22f, 1.08f, -0.36f),

                // ---------------- forehand drive: racket up and back, out in front at contact, over the left shoulder after ----------------
                Pose("fh_back", "forehand").Crouch(0.18f).Lean(24).Twist(50).Feet(-0.30f, 0.15f, 0.20f, -0.30f, 60, 30)
                    .Racket(-0.70f, 1.75f, 0.40f, -0.25f, 0.75f, 0.60f, -0.9f, 0.0f, 0.4f).Hand(-0.05f, 1.30f, -0.60f),
                Pose("fh_contact", "forehand").Crouch(0.23f).Lean(30).Twist(10).Feet(-0.30f, 0.15f, 0.20f, -0.30f, 60, 30)
                    .Racket(-0.85f, 0.80f, -0.60f, -0.85f, 0.15f, -0.50f, 0.0f, 0.15f, -1.0f).Hand(0.55f, 1.10f, 0.15f),
                Pose("fh_follow", "forehand").Crouch(0.17f).Lean(14).Twist(-55).Feet(-0.30f, 0.15f, 0.20f, -0.30f, 60, 30)
                    .Racket(0.35f, 1.80f, 0.10f, 0.6f, 0.5f, 0.6f, 0.5f, 0.0f, -0.8f, 0.2f, -0.5f, -0.3f).Hand(0.25f, 1.15f, -0.30f),

                // ---------------- backhand drive: racket behind the left shoulder, out to the LEFT in front at contact ----------------
                Pose("bh_back", "backhand").Crouch(0.18f).Lean(24).Twist(-60).Feet(-0.05f, -0.32f, 0.32f, 0.15f, -30, -60)
                    .Racket(0.45f, 1.70f, 0.35f, 0.3f, 0.75f, 0.55f, 0.9f, 0.0f, 0.3f, 0.15f, -0.45f, -0.3f).Hand(0.25f, 1.25f, -0.15f),
                Pose("bh_contact", "backhand").Crouch(0.23f).Lean(30).Twist(-15).Feet(-0.05f, -0.32f, 0.32f, 0.15f, -30, -60)
                    .Racket(0.75f, 0.80f, -0.60f, 0.85f, 0.10f, -0.45f, 0.0f, 0.15f, -1.0f, 0.15f, -0.45f, -0.35f).Hand(-0.45f, 1.10f, 0.25f),
                Pose("bh_follow", "backhand").Crouch(0.17f).Lean(12).Twist(25).Feet(-0.05f, -0.32f, 0.32f, 0.15f, -30, -60)
                    .Racket(-0.40f, 1.95f, -0.15f, -0.5f, 0.75f, 0.3f, -0.3f, 0.2f, -0.9f).Hand(0.55f, 1.10f, 0.35f),

                // ---------------- specials ----------------
                Pose("dig_fh", "special").Crouch(0.30f).Lean(32).Twist(20).Feet(-0.45f, -0.55f, 0.25f, 0.35f, 40, 0)
                    .Racket(-0.85f, 0.18f, -0.80f, -0.7f, -0.15f, -0.6f, 0.0f, 0.6f, -0.8f).Hand(0.60f, 0.90f, 0.30f),
                Pose("dig_bh", "special").Crouch(0.30f).Lean(32).Twist(-35).Feet(0.15f, -0.60f, 0.35f, 0.35f, -30, -20)
                    .Racket(0.80f, 0.18f, -0.85f, 0.7f, -0.15f, -0.6f, 0.0f, 0.6f, -0.8f, 0.15f, -0.45f, -0.35f).Hand(-0.50f, 0.95f, 0.35f),
                Pose("overhead", "special").Crouch(0.04f).Lean(-5).Twist(30).Feet(-0.25f, 0.20f, 0.20f, -0.20f, 40, 10)
                    .Racket(-0.35f, 2.50f, -0.30f, -0.2f, 0.9f, -0.3f, 0.0f, 0.2f, -1.0f, -0.4f, -0.1f, 0.3f).Hand(0.20f, 2.00f, -0.40f).Look(20),
                Pose("touch", "special").Crouch(0.20f).Lean(26).Twist(5).Feet(-0.30f, -0.50f, 0.20f, 0.25f, 20, -5)
                    .Racket(-0.55f, 0.40f, -0.95f, -0.4f, -0.1f, -0.9f, 0.0f, 0.7f, -0.7f).Hand(0.45f, 0.95f, 0.30f),
                Pose("serve_toss", "special").Crouch(0.06f).Lean(6).Twist(45).Feet(-0.25f, 0.15f, 0.15f, -0.25f, 50, 20)
                    .Racket(-0.70f, 1.60f, 0.35f, -0.25f, 0.75f, 0.6f, -0.9f, 0.0f, 0.4f).Hand(0.15f, 2.10f, -0.35f).Look(20),
                Pose("serve_hit", "special").Crouch(0.08f).Lean(12).Twist(0).Feet(-0.25f, 0.15f, 0.15f, -0.25f, 50, 20)
                    .Racket(-0.75f, 1.35f, -0.60f, -0.6f, 0.3f, -0.7f, 0.0f, 0.0f, -1.0f).Hand(0.35f, 1.20f, -0.20f),

                // ---------------- far balls: lunge, then dive ----------------
                Pose("lunge_fh", "reach").Crouch(0.30f).PelvisShift(-0.18f, -0.28f).Lean(30).Twist(25).Feet(-0.55f, -0.85f, 0.22f, 0.40f, 45, 20)
                    .Racket(-1.15f, 0.38f, -1.20f, -0.6f, -0.1f, -0.8f, 0.1f, 0.5f, -0.85f).Hand(0.60f, 1.00f, 0.55f),
                Pose("lunge_bh", "reach").Crouch(0.30f).PelvisShift(0.12f, -0.28f).Lean(30).Twist(-50).Feet(0.25f, -0.85f, 0.30f, 0.40f, -40, -10)
                    .Racket(0.95f, 0.38f, -1.25f, 0.5f, -0.1f, -0.85f, -0.1f, 0.5f, -0.85f, 0.15f, -0.45f, -0.35f).Hand(-0.40f, 1.00f, 0.55f),
                Pose("dive_fh", "reach").Crouch(0.40f).PelvisShift(-0.55f, -0.15f).Lean(45).Tilt(30).Twist(30)
                    .Feet(-0.95f, -0.20f, 0.15f, 0.35f, 80, 10, 0.25f, 0)
                    .Racket(-1.70f, 0.32f, -0.55f, -0.95f, -0.1f, -0.2f, 0.0f, 0.4f, -0.9f).Hand(0.40f, 1.15f, 0.40f),
                Pose("dive_bh", "reach").Crouch(0.40f).PelvisShift(0.45f, -0.15f).Lean(45).Tilt(-30).Twist(-45)
                    .Feet(0.75f, -0.30f, 0.0f, 0.35f, -70, -20, 0.25f, 0)
                    .Racket(1.50f, 0.32f, -0.65f, 0.95f, -0.1f, -0.3f, 0.0f, 0.4f, -0.9f, 0.15f, -0.45f, -0.35f).Hand(-0.30f, 1.00f, 0.50f),
                Pose("get_up", "reach").Crouch(0.42f).Lean(22).Feet(-0.20f, -0.35f, 0.20f, 0.45f, 10, -10)
                    .Racket(-0.55f, 0.55f, -0.40f, -0.3f, 0.6f, -0.7f, -0.9f, 0.0f, 0.0f).Hand(0.25f, 0.12f, -0.35f),

                // ---------------- match moments ----------------
                Pose("celebrate", "moment").Crouch(0.02f).Lean(-5).Feet(-0.22f, 0, 0.22f, 0, 14, -14)
                    .Racket(-0.55f, 1.15f, -0.20f, -0.3f, 0.9f, 0.1f, -0.9f, 0.0f, 0.3f).Hand(0.25f, 2.10f, -0.10f, 0.3f, -0.1f, 0.3f).Look(10),
                Pose("dejected", "moment").Crouch(0.0f).Lean(30).Feet(-0.16f, 0, 0.16f, 0, 10, -10)
                    .Racket(-0.25f, 0.35f, -0.25f, 0.0f, -0.7f, -0.7f, -0.9f, 0.0f, 0.0f).Hand(0.20f, 0.80f, 0.0f).Look(-25),

                // ---------------- P2-M5: the Locker Room's celebrations (on a won match point) ----------------
                Pose("cel_fist", "celebration").Crouch(0.08f).Lean(8).Feet(-0.26f, 0, 0.26f, 0, 14, -14)
                    .Racket(-0.32f, 0.70f, -0.12f, 0.0f, -0.6f, -0.6f, -0.9f, 0.0f, 0.0f).Hand(0.16f, 1.30f, -0.30f, 0.35f, -0.25f, 0.25f).Look(5),
                Pose("cel_raise", "celebration").Crouch(0.0f).Lean(-6).Feet(-0.22f, 0, 0.22f, 0, 14, -14)
                    .Racket(-0.22f, 2.05f, -0.05f, 0.0f, 1.0f, 0.05f, 0.0f, 0.0f, -1.0f, -0.25f, 0.3f, 0.2f).Hand(0.28f, 0.95f, -0.18f).Look(18),
                Pose("cel_wide", "celebration").Crouch(0.02f).Lean(-10).Feet(-0.30f, 0, 0.30f, 0, 18, -18)
                    .Racket(-0.95f, 1.45f, -0.05f, -1.0f, 0.3f, 0.0f, 0.0f, 0.0f, -1.0f, -0.3f, -0.2f, 0.3f).Hand(0.90f, 1.45f, -0.05f, 0.3f, -0.2f, 0.3f).Look(12),
                Pose("cel_sky", "celebration").Crouch(0.02f).Lean(-8).Feet(-0.22f, 0, 0.22f, 0, 14, -14)
                    .Racket(-0.40f, 0.85f, -0.20f, -0.2f, -0.5f, -0.6f, -0.9f, 0.0f, 0.0f).Hand(0.22f, 2.15f, -0.18f, 0.3f, -0.05f, 0.3f).Look(32),
                Pose("cel_bow", "celebration").Crouch(0.04f).Lean(50).Feet(-0.18f, 0, 0.18f, 0, 10, -10)
                    .Racket(-0.30f, 0.60f, -0.45f, 0.0f, -0.6f, -0.7f, -0.9f, 0.0f, 0.0f).Hand(0.05f, 0.95f, -0.40f).Look(-20),
                Pose("cel_knee", "celebration").Crouch(0.48f).Lean(-10).Feet(-0.22f, -0.42f, 0.22f, 0.48f, 10, -10)
                    .Racket(-0.55f, 1.55f, -0.20f, -0.4f, 0.9f, 0.0f, 0.0f, 0.0f, -1.0f, -0.3f, 0.0f, 0.3f).Hand(0.32f, 1.35f, -0.25f, 0.35f, -0.2f, 0.25f).Look(24)
            };
            var list = new List<AthletePose>();
            foreach (var b in poses) list.Add(b.P);
            return list;
        }
    }
}
