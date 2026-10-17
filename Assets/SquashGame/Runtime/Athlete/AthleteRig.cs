// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteRig.cs
// Builds the athlete at runtime: the Gate B skeleton (74 bones, from
// RigContract) as a Transform hierarchy, the Gate A body forms on its
// bones, the joint blends, and the Gate A racket.
//
// Rig frame -> Unity: mirror X, once, here. In the athlete's local
// space it faces -Z with its RIGHT at local -X (the mirror of the rig's
// +X), which is what keeps a right-hander's racket on screen right from
// the back-wall camera. Rest rotations are identity, so a form's local
// offset is simply (pivot - bone) mirrored.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public sealed class AthleteRig : MonoBehaviour
    {
        public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> _restLocal = new Dictionary<string, Vector3>();
        readonly List<(Transform bone, GameObject blend)> _blends = new List<(Transform, GameObject)>();

        public Transform Racket { get; private set; }
        /// <summary>The racket's pose in the hand (HoldRacketInHand): hand-local position and rotation.</summary>
        public Vector3 GripLocalPos { get; private set; }
        public Quaternion GripLocalRot { get; private set; } = Quaternion.identity;
        public bool RacketInHand { get; private set; }
        public int FormCount { get; private set; }
        public const float BlendThresholdDeg = 13f;      // lab: 0.22 rad

        static Vector3 ToUnity(Vector3 rig) { return new Vector3(-rig.x, rig.y, rig.z); }

        public void Build(GreyboxPalette palette, Color bodyColor)
        {
            // ---- skeleton ----
            var rigPos = new Dictionary<string, Vector3>();
            foreach (RigBone b in RigContract.Bones)
            {
                var t = new GameObject(b.Name).transform;
                Transform parent = b.Parent == null ? transform : Bones[b.Parent];
                t.SetParent(parent, false);
                Vector3 parentRig = b.Parent == null ? Vector3.zero : rigPos[b.Parent];
                t.localPosition = ToUnity(b.Pos - parentRig);
                t.localRotation = Quaternion.identity;
                Bones[b.Name] = t;
                rigPos[b.Name] = b.Pos;
                _restLocal[b.Name] = t.localPosition;
            }

            // ---- body forms ----
            Material body = palette.LitColor(bodyColor);
            foreach (FormPart f in MannequinForms.Build())
            {
                var go = new GameObject(f.Name);
                go.transform.SetParent(Bones[f.Bone], false);
                go.transform.localPosition = ToUnity(f.Pivot - rigPos[f.Bone]);
                go.AddComponent<MeshFilter>().sharedMesh = f.Mesh.ToUnity(f.Name);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = body;
                FormCount++;
            }

            // ---- joint blends: shown only when the joint is bent ----
            foreach (JointBlend jb in MannequinForms.Blends())
            {
                var go = new GameObject("blend_" + jb.Bone);
                go.transform.SetParent(Bones[jb.Bone], false);
                go.AddComponent<MeshFilter>().sharedMesh = Loft.Ellipsoid(jb.W, jb.D, jb.H).ToUnity("blend");
                go.AddComponent<MeshRenderer>().sharedMaterial = body;
                go.SetActive(false);
                _blends.Add((Bones[jb.Bone], go));
            }

            // ---- racket (placed in world from the simulated racket, not parented to the hand) ----
            var parts = RacketMesh.Build();
            Racket = new GameObject("Racket").transform;
            Racket.SetParent(transform, false);
            AddPart(Racket, "racket_frame", parts.Frame, palette.LitColor(Greybox.Hex(0x5d656f)), true);
            AddPart(Racket, "racket_handle", parts.Handle, palette.LitColor(Greybox.Hex(0x23272c)), true);
            AddPart(Racket, "racket_stringBed", parts.Bed, palette.Clear(Greybox.Hex(0xdde2e8, 0.30f)), false);
        }

        static void AddPart(Transform parent, string name, MeshData m, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m.ToUnity(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// M7: put the racket in the right hand (parented to Hand_R, butt in the Gate A grip socket).
        /// The shaft points out of the fist, forward and a little up; the strings face the palm side.
        /// </summary>
        public void HoldRacketInHand(float cockDeg = 25f)
        {
            Transform hand = Bones["Hand_R"];
            Racket.SetParent(hand, false);
            Vector3 grip = ToUnity(RigContract.RacketGrip("R")) - ToUnity(RigBoneRest("Hand_R"));
            Racket.localPosition = grip;
            // Hand local at rest: fingers along -Y, the athlete faces -Z. Shaft forward (-Z), cocked up.
            Vector3 tip = Quaternion.AngleAxis(-cockDeg, Vector3.right) * new Vector3(0, 0, -1);
            Vector3 normal = Vector3.right;                       // strings face +X (the palm side of a right hand)
            Vector3 y = -tip.normalized;                          // racket local -Y = butt -> tip
            Vector3 x = (normal - Vector3.Dot(normal, y) * y).normalized;
            Racket.localRotation = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            GripLocalPos = Racket.localPosition;
            GripLocalRot = Racket.localRotation;
            RacketInHand = true;
        }

        static Vector3 RigBoneRest(string name)
        {
            foreach (RigBone b in RigContract.Bones) if (b.Name == name) return b.Pos;
            return Vector3.zero;
        }

        /// <summary>Back to the rest pose (identity rotations, contract positions).</summary>
        public void ResetPose()
        {
            foreach (var kv in Bones)
            {
                kv.Value.localRotation = Quaternion.identity;
                kv.Value.localPosition = _restLocal[kv.Key];
            }
        }

        public Vector3 RestLocal(string bone) { return _restLocal[bone]; }

        public void UpdateBlends()
        {
            foreach (var (bone, blend) in _blends)
            {
                float angle = Quaternion.Angle(Quaternion.identity, bone.localRotation);
                bool show = angle > BlendThresholdDeg;
                if (blend.activeSelf != show) blend.SetActive(show);
            }
        }
    }
}
