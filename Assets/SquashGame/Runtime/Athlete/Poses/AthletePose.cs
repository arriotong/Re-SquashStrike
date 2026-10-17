// ===========================================================
// Assets/SquashGame/Runtime/Athlete/Poses/AthletePose.cs
// M7: authored key poses for the athlete (the Mario Tennis approach:
// designed poses drive the body; physics only nudges the hand at
// contact).
//
// A pose is a set of LOCAL bone rotations (Euler degrees, applied on the
// identity rest pose of the Gate B rig) plus a pelvis offset in the
// athlete's local space. Rig local axes at rest, for every bone:
//   +X = the athlete's LEFT, +Y = up, +Z = BEHIND (the athlete faces -Z).
// Bones not listed stay at rest. Poses blend by slerp.
//
// Grounded feet (default): each pose places both feet (athlete local,
// soles on the floor unless lifted) and the legs are solved to reach them
// with the knees forward - so every pose stands on the floor by
// construction, however low the pelvis goes. Level head (default): the
// head is kept upright (no roll, no pitch from the spine's lean); a pose's
// own Head rotation is then applied on top as a look.
//
// PoseLibrary holds them: DefaultPoses builds the authored set from code;
// a PoseLibrary asset saved by the Pose Lab window overrides it.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    [System.Serializable]
    public struct BoneRot
    {
        public string Bone;
        public Vector3 Euler;
        public BoneRot(string bone, float x, float y, float z) { Bone = bone; Euler = new Vector3(x, y, z); }
    }

    [System.Serializable]
    public sealed class AthletePose
    {
        public string Name;
        public string Group;
        /// <summary>Pelvis offset from its rest position, athlete local space (+X left, +Y up, +Z behind).</summary>
        public Vector3 Pelvis;
        public List<BoneRot> Rots = new List<BoneRot>();
        public bool FeetIK = true;
        /// <summary>Ankle targets, athlete local (+X left, +Y up, +Z behind). Right foot at -X.</summary>
        public Vector3 FootR = new Vector3(-0.12f, GateA.Ankle, 0f), FootL = new Vector3(0.12f, GateA.Ankle, 0f);
        /// <summary>Toe direction of each foot, degrees (+ = turned toward the athlete's right).</summary>
        public float FootYawR, FootYawL;
        public bool LevelHead = true;

        // ---- arms by target (athlete local: +X left, +Y up, +Z behind) ----
        /// <summary>Solve the racket arm so the racket's sweet spot sits at Sweet, its shaft (butt to tip) along
        /// RacketDir and its strings facing RacketNormal; the elbow bends toward ElbowR (offset from the shoulder).</summary>
        public bool RacketIK;
        public Vector3 Sweet, RacketDir = Vector3.up, RacketNormal = Vector3.back, ElbowR = new Vector3(-0.35f, -0.55f, 0.25f);
        /// <summary>Solve the free arm so the left hand reaches HandL, elbow toward ElbowL (offset from the shoulder).</summary>
        public bool HandIK;
        public Vector3 HandL, ElbowL = new Vector3(0.35f, -0.55f, 0.2f);

        public AthletePose() { }
        public AthletePose(string name, string group, Vector3 pelvis, params BoneRot[] rots)
        {
            Name = name; Group = group; Pelvis = pelvis; Rots = new List<BoneRot>(rots);
        }

        public Vector3 Get(string bone)
        {
            foreach (var r in Rots) if (r.Bone == bone) return r.Euler;
            return Vector3.zero;
        }

        public void Set(string bone, Vector3 euler)
        {
            for (int i = 0; i < Rots.Count; i++)
                if (Rots[i].Bone == bone) { Rots[i] = new BoneRot(bone, euler.x, euler.y, euler.z); return; }
            Rots.Add(new BoneRot(bone, euler.x, euler.y, euler.z));
        }

        public AthletePose Clone()
        {
            var p = new AthletePose
            {
                Name = Name, Group = Group, Pelvis = Pelvis, FeetIK = FeetIK, FootR = FootR, FootL = FootL,
                FootYawR = FootYawR, FootYawL = FootYawL, LevelHead = LevelHead,
                RacketIK = RacketIK, Sweet = Sweet, RacketDir = RacketDir, RacketNormal = RacketNormal, ElbowR = ElbowR,
                HandIK = HandIK, HandL = HandL, ElbowL = ElbowL
            };
            p.Rots.AddRange(Rots);
            return p;
        }
    }

    public static class PoseMath
    {
        /// <summary>Put the rig in this pose (from rest), feet grounded and head level as the pose asks.</summary>
        public static void Apply(AthleteRig rig, AthletePose pose)
        {
            rig.ResetPose();
            Layer(rig, pose, 1f);
            if (pose.FeetIK) SolveFeet(rig, pose.FootR, pose.FootL, pose.FootYawR, pose.FootYawL);
            if (pose.LevelHead) LevelHead(rig, pose.Get("Head"));
            Transform root = rig.transform;
            if (pose.RacketIK)
                SolveRacketArm(rig, root.TransformPoint(pose.Sweet), root.TransformDirection(pose.RacketDir),
                               root.TransformDirection(pose.RacketNormal), root.TransformDirection(pose.ElbowR));
            if (pose.HandIK)
                SolveFreeArm(rig, root.TransformPoint(pose.HandL), root.TransformDirection(pose.ElbowL));
        }

        /// <summary>
        /// The racket arm, by target (world space): the racket in the hand ends with its sweet spot at
        /// <paramref name="sweet"/>, shaft along <paramref name="dir"/>, strings facing <paramref name="normal"/>.
        /// Returns how far the hand fell short (0 when reachable).
        /// </summary>
        public static float SolveRacketArm(AthleteRig rig, Vector3 sweet, Vector3 dir, Vector3 normal, Vector3 elbowOffset)
        {
            dir = dir.normalized;
            normal = (normal - Vector3.Dot(normal, dir) * dir).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(dir, Vector3.up).normalized;
            // Racket mesh: local -Y = butt -> tip, local +X = string normal; origin at the butt.
            Quaternion racketRot = Basis(normal, -dir);
            Vector3 butt = sweet - dir * GateA.SweetSpotFromButt;
            Quaternion handRot = racketRot * Quaternion.Inverse(rig.GripLocalRot);
            Vector3 wrist = butt - handRot * rig.GripLocalPos;
            Transform shoulder = rig.Bones["UpperArm_R"];
            float err = TwoBoneIK.Solve(shoulder, rig.Bones["Forearm_R"], rig.Bones["Hand_R"], wrist, shoulder.position + elbowOffset);
            rig.Bones["Hand_R"].rotation = handRot;
            return err;
        }

        public static float SolveFreeArm(AthleteRig rig, Vector3 hand, Vector3 elbowOffset)
        {
            Transform shoulder = rig.Bones["UpperArm_L"];
            return TwoBoneIK.Solve(shoulder, rig.Bones["Forearm_L"], rig.Bones["Hand_L"], hand, shoulder.position + elbowOffset);
        }

        /// <summary>Rotation whose local +X is <paramref name="x"/> and local +Y is <paramref name="y"/>.</summary>
        public static Quaternion Basis(Vector3 x, Vector3 y)
        {
            y = y.normalized;
            x = (x - Vector3.Dot(x, y) * y).normalized;
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }

        /// <summary>Legs reach the given ankle targets (athlete local), knees forward and a little out.</summary>
        public static void SolveFeet(AthleteRig rig, Vector3 footR, Vector3 footL, float yawR, float yawL)
        {
            Transform root = rig.transform;
            Vector3 fwd = root.TransformDirection(Vector3.back);           // athlete faces local -Z
            Vector3 left = root.TransformDirection(Vector3.right);
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "R" : "L";
                Vector3 target = root.TransformPoint(side == 0 ? footR : footL);
                float outward = side == 0 ? -1f : 1f;                        // the right leg's outside is -X (local)
                Vector3 knee = target + fwd * 0.55f + Vector3.up * 0.45f + left * (outward * 0.12f);
                TwoBoneIK.Solve(rig.Bones["Thigh_" + s], rig.Bones["Shin_" + s], rig.Bones["Foot_" + s], target, knee);
                float yaw = side == 0 ? yawR : yawL;
                rig.Bones["Foot_" + s].rotation = root.rotation * Quaternion.Euler(0f, yaw, 0f);   // soles flat
            }
        }

        /// <summary>Head upright, facing the way the chest faces (flattened), then the pose's own head look.</summary>
        public static void LevelHead(AthleteRig rig, Vector3 look)
        {
            Transform chest = rig.Bones["Chest"], head = rig.Bones["Head"];
            Vector3 f = chest.TransformDirection(Vector3.back);
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = rig.transform.TransformDirection(Vector3.back);
            head.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up) * Quaternion.Euler(look);
        }

        /// <summary>Blend a pose on top of whatever the rig holds now (weight 0..1).</summary>
        public static void Layer(AthleteRig rig, AthletePose pose, float w)
        {
            if (pose == null || w <= 0f) return;
            foreach (var r in pose.Rots)
            {
                if (!rig.Bones.TryGetValue(r.Bone, out Transform t)) continue;
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(r.Euler), w);
            }
            Transform pelvis = rig.Bones["Pelvis"];
            pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, rig.RestLocal("Pelvis") + pose.Pelvis, w);
        }

        /// <summary>A pose between two poses (for authoring in-betweens and blending clips).</summary>
        public static AthletePose Lerp(AthletePose a, AthletePose b, float t, string name = null)
        {
            var o = new AthletePose
            {
                Name = name ?? (a.Name + "~" + b.Name), Group = a.Group, Pelvis = Vector3.Lerp(a.Pelvis, b.Pelvis, t),
                FeetIK = a.FeetIK, FootR = Vector3.Lerp(a.FootR, b.FootR, t), FootL = Vector3.Lerp(a.FootL, b.FootL, t),
                FootYawR = Mathf.Lerp(a.FootYawR, b.FootYawR, t), FootYawL = Mathf.Lerp(a.FootYawL, b.FootYawL, t),
                LevelHead = a.LevelHead,
                RacketIK = a.RacketIK, Sweet = Vector3.Lerp(a.Sweet, b.Sweet, t),
                RacketDir = Vector3.Slerp(a.RacketDir.normalized, b.RacketDir.normalized, t),
                RacketNormal = Vector3.Slerp(a.RacketNormal.normalized, b.RacketNormal.normalized, t),
                ElbowR = Vector3.Lerp(a.ElbowR, b.ElbowR, t),
                HandIK = a.HandIK, HandL = Vector3.Lerp(a.HandL, b.HandL, t), ElbowL = Vector3.Lerp(a.ElbowL, b.ElbowL, t)
            };
            var bones = new HashSet<string>();
            foreach (var r in a.Rots) bones.Add(r.Bone);
            foreach (var r in b.Rots) bones.Add(r.Bone);
            foreach (var bone in bones)
            {
                Quaternion q = Quaternion.Slerp(Quaternion.Euler(a.Get(bone)), Quaternion.Euler(b.Get(bone)), t);
                Vector3 e = q.eulerAngles;
                o.Rots.Add(new BoneRot(bone, Wrap(e.x), Wrap(e.y), Wrap(e.z)));
            }
            return o;
        }

        static float Wrap(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }
    }
}
