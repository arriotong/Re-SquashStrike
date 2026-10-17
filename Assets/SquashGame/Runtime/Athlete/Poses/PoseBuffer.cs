// ===========================================================
// Assets/SquashGame/Runtime/Athlete/Poses/PoseBuffer.cs
// M7: a pose being blended at runtime, without allocating: the spine /
// head rotations the authored poses use, the pelvis offset, both feet
// (position + toe yaw), the racket target (sweet spot, shaft direction,
// string normal, elbow pole) and the free hand. Everything in the
// athlete's local space, like AthletePose.
// ===========================================================

using UnityEngine;

namespace SquashGame.Athlete
{
    public sealed class PoseBuffer
    {
        public static readonly string[] Bones = { "Pelvis", "Spine_01", "Spine_02", "Chest", "Head" };
        public readonly Quaternion[] Rot = new Quaternion[Bones.Length];
        public Vector3 Pelvis, FootR, FootL;
        public float FootYawR, FootYawL;
        public Vector3 Sweet, RacketDir = Vector3.up, RacketNormal = Vector3.back, ElbowR, HandL, ElbowL;

        public PoseBuffer() { for (int i = 0; i < Rot.Length; i++) Rot[i] = Quaternion.identity; }

        public void Set(AthletePose p)
        {
            for (int i = 0; i < Bones.Length; i++) Rot[i] = Quaternion.Euler(p.Get(Bones[i]));
            Pelvis = p.Pelvis; FootR = p.FootR; FootL = p.FootL; FootYawR = p.FootYawR; FootYawL = p.FootYawL;
            Sweet = p.Sweet; RacketDir = p.RacketDir.normalized; RacketNormal = p.RacketNormal.normalized; ElbowR = p.ElbowR;
            HandL = p.HandL; ElbowL = p.ElbowL;
        }

        /// <summary>Move toward pose <paramref name="p"/> by w (0..1).</summary>
        public void Blend(AthletePose p, float w)
        {
            if (p == null || w <= 0f) return;
            if (w >= 1f) { Set(p); return; }
            for (int i = 0; i < Bones.Length; i++) Rot[i] = Quaternion.Slerp(Rot[i], Quaternion.Euler(p.Get(Bones[i])), w);
            Pelvis = Vector3.Lerp(Pelvis, p.Pelvis, w);
            FootR = Vector3.Lerp(FootR, p.FootR, w); FootL = Vector3.Lerp(FootL, p.FootL, w);
            FootYawR = Mathf.Lerp(FootYawR, p.FootYawR, w); FootYawL = Mathf.Lerp(FootYawL, p.FootYawL, w);
            Sweet = Vector3.Lerp(Sweet, p.Sweet, w);
            RacketDir = Vector3.Slerp(RacketDir, p.RacketDir.normalized, w);
            RacketNormal = Vector3.Slerp(RacketNormal, p.RacketNormal.normalized, w);
            ElbowR = Vector3.Lerp(ElbowR, p.ElbowR, w);
            HandL = Vector3.Lerp(HandL, p.HandL, w); ElbowL = Vector3.Lerp(ElbowL, p.ElbowL, w);
        }

        /// <summary>Copy another buffer.</summary>
        public void CopyFrom(PoseBuffer o)
        {
            for (int i = 0; i < Rot.Length; i++) Rot[i] = o.Rot[i];
            Pelvis = o.Pelvis; FootR = o.FootR; FootL = o.FootL; FootYawR = o.FootYawR; FootYawL = o.FootYawL;
            Sweet = o.Sweet; RacketDir = o.RacketDir; RacketNormal = o.RacketNormal; ElbowR = o.ElbowR; HandL = o.HandL; ElbowL = o.ElbowL;
        }

        /// <summary>Blend toward another buffer by w.</summary>
        public void Blend(PoseBuffer o, float w)
        {
            if (w <= 0f) return;
            if (w >= 1f) { CopyFrom(o); return; }
            for (int i = 0; i < Rot.Length; i++) Rot[i] = Quaternion.Slerp(Rot[i], o.Rot[i], w);
            Pelvis = Vector3.Lerp(Pelvis, o.Pelvis, w);
            FootR = Vector3.Lerp(FootR, o.FootR, w); FootL = Vector3.Lerp(FootL, o.FootL, w);
            FootYawR = Mathf.Lerp(FootYawR, o.FootYawR, w); FootYawL = Mathf.Lerp(FootYawL, o.FootYawL, w);
            Sweet = Vector3.Lerp(Sweet, o.Sweet, w);
            RacketDir = Vector3.Slerp(RacketDir, o.RacketDir, w);
            RacketNormal = Vector3.Slerp(RacketNormal, o.RacketNormal, w);
            ElbowR = Vector3.Lerp(ElbowR, o.ElbowR, w);
            HandL = Vector3.Lerp(HandL, o.HandL, w); ElbowL = Vector3.Lerp(ElbowL, o.ElbowL, w);
        }

        /// <summary>Spine / head rotations and pelvis offset onto the rig (from rest).</summary>
        public void ApplyBody(AthleteRig rig)
        {
            for (int i = 0; i < Bones.Length; i++)
                if (Bones[i] != "Head") rig.Bones[Bones[i]].localRotation = Rot[i];
            rig.Bones["Pelvis"].localPosition = rig.RestLocal("Pelvis") + Pelvis;
        }

        public Quaternion HeadLook { get { return Rot[4]; } }
    }
}
