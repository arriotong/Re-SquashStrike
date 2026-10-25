// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteRig.cs
// Builds the athlete at runtime: the Gate B skeleton (74 bones, from
// RigContract) as a Transform hierarchy, the Gate A body forms on its
// bones, the joint blends, and the Gate A racket.
//
// P2-M5: the forms are coloured by kit region (shirt, sleeves, shorts,
// socks, shoes, skin), with a headband and a wristband that can be
// shown, and the racket's frame / grip / strings - ApplyKit.
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
        // The slim build (2026-10-24): each form's mesh, made per body (male / female) once a session.
        readonly List<(MeshFilter mf, FormPart part)> _forms = new List<(MeshFilter, FormPart)>();
        readonly List<(MeshFilter mf, JointBlend blend)> _blendForms = new List<(MeshFilter, JointBlend)>();
        static readonly Dictionary<string, Mesh> _bodyMeshes = new Dictionary<string, Mesh>();
        public bool Female { get; private set; }
        bool _bodyBuilt;

        public Transform Racket { get; private set; }
        MeshRenderer _frameRenderer;
        Material _frameGlow;
        /// <summary>P4: the frame is drawn glowing (tests).</summary>
        public bool FrameGlowing { get; private set; }
        /// <summary>The racket's pose in the hand (HoldRacketInHand): hand-local position and rotation.</summary>
        public Vector3 GripLocalPos { get; private set; }
        public Quaternion GripLocalRot { get; private set; } = Quaternion.identity;
        public bool RacketInHand { get; private set; }
        public int FormCount { get; private set; }
        public const float BlendThresholdDeg = 13f;      // lab: 0.22 rad

        static Vector3 ToUnity(Vector3 rig) { return new Vector3(-rig.x, rig.y, rig.z); }

        public enum Region { ShirtBody, ShirtSleeves, Shorts, Socks, Shoes, Skin }
        readonly Material[] _region = new Material[6];
        Material _frame, _grip, _strings, _headband, _wristband, _hair, _features;
        GameObject _headbandGo, _wristbandGo;
        readonly GameObject[] _hairGo = new GameObject[HeadDetail.StyleCount];
        /// <summary>PR3: the hair style drawn (tests).</summary>
        public int HairStyleShown { get; private set; } = 2;

        /// <summary>The kit region a body form (or a joint blend, by its bone) belongs to.</summary>
        public static Region RegionOf(string name)
        {
            if (name.Contains("chest") || name.Contains("abdomen") || name.Contains("Chest") || name.Contains("Spine")) return Region.ShirtBody;
            if (name.Contains("upperArm") || name.Contains("UpperArm")) return Region.ShirtSleeves;
            if (name.Contains("pelvis") || name.Contains("thigh") || name.Contains("Pelvis") || name.Contains("Thigh")) return Region.Shorts;
            if (name.Contains("shin") || name.Contains("blend_Foot")) return Region.Socks;
            if (name.Contains("foot")) return Region.Shoes;
            return Region.Skin;                                    // head, neck, forearms, hands; elbow, wrist and knee blends
        }

        /// <summary>The material drawing a kit region (tests, captures).</summary>
        public Material RegionMaterial(Region r) { return _region[(int)r]; }
        public bool HeadbandShown { get { return _headbandGo != null && _headbandGo.activeSelf; } }
        public bool WristbandShown { get { return _wristbandGo != null && _wristbandGo.activeSelf; } }
        /// <summary>The bands' materials (the body worn over the rig wears its own bands in them).</summary>
        public Material HeadbandMaterial { get { return _headband; } }
        public Material WristbandMaterial { get { return _wristband; } }

        /// <summary>Dress the athlete: the kit's colours on its regions, the bands, the racket. Presentation only.</summary>
        public void ApplyKit(SquashGame.Progression.Kit k)
        {
            if (_region[0] == null) return;
            _region[(int)Region.ShirtBody].color = k.ShirtBody;
            _region[(int)Region.ShirtSleeves].color = k.ShirtSleeves;
            _region[(int)Region.Shorts].color = k.Shorts;
            _region[(int)Region.Socks].color = k.Socks;
            _region[(int)Region.Shoes].color = k.Shoes;
            _region[(int)Region.Skin].color = k.Skin;
            _headbandGo.SetActive(k.HasHeadband); _headband.color = k.Headband;
            _wristbandGo.SetActive(k.HasWristband); _wristband.color = k.Wristband;
            _frame.color = k.Frame; _grip.color = k.Grip;
            // P4: an Epic / Legendary / gold frame glows (an HDR unlit copy: the bloom picks it up)
            if (_frameRenderer != null)
            {
                if (k.FrameGlow) { _frameGlow.color = new Color(k.Frame.r * 1.8f, k.Frame.g * 1.8f, k.Frame.b * 1.8f, 1f); _frameRenderer.sharedMaterial = _frameGlow; }
                else _frameRenderer.sharedMaterial = _frame;
            }
            FrameGlowing = k.FrameGlow;
            _strings.color = new Color(Mathf.Min(1f, k.Strings.r * 1.15f), Mathf.Min(1f, k.Strings.g * 1.15f), Mathf.Min(1f, k.Strings.b * 1.15f), 0.6f);   // readable at TV zoom
            ShowHair(k.HairStyle);
            _hair.color = k.Hair;
            SetBody(k.Female);
        }

        /// <summary>The slim build, male or female: every form's mesh narrowed by MannequinForms.Slim.</summary>
        public void SetBody(bool female)
        {
            if (_bodyBuilt && female == Female) return;
            Female = female; _bodyBuilt = true;
            foreach (var (mf, f) in _forms)
            {
                string key = f.Name + (female ? "_f" : "_m");
                if (!_bodyMeshes.TryGetValue(key, out var mesh) || mesh == null)
                    _bodyMeshes[key] = mesh = MannequinForms.Slimmed(f.Mesh, MannequinForms.Slim(f.Name, female)).ToUnity(key);
                mf.sharedMesh = mesh;
            }
            foreach (var (mf, jb) in _blendForms)
            {
                string key = "blend_" + jb.Bone + (female ? "_f" : "_m");
                if (!_bodyMeshes.TryGetValue(key, out var mesh) || mesh == null)
                {
                    var s = MannequinForms.Slim(jb.Bone, female);
                    _bodyMeshes[key] = mesh = Loft.Ellipsoid(jb.W * s.x * 0.86f, jb.D * s.y * 0.86f, jb.H * 0.9f).ToUnity(key);   // never bulging past the limbs
                }
                mf.sharedMesh = mesh;
            }
            // Hair, face and headband follow the head's narrowing.
            var hs = MannequinForms.Slim("head", female);
            foreach (var hp in _headParts) if (hp != null) hp.transform.localScale = new Vector3(hs.x, 1f, hs.y);
            if (_headbandGo != null) _headbandGo.transform.localScale = new Vector3(0.094f * hs.x, 0.094f * hs.y, 0.14f);
        }

        void ShowHair(int style)
        {
            HairStyleShown = Mathf.Clamp(style, 0, HeadDetail.StyleCount - 1);
            for (int i = 0; i < _hairGo.Length; i++) if (_hairGo[i] != null) _hairGo[i].SetActive(i == HairStyleShown);
        }

        readonly List<GameObject> _headParts = new List<GameObject>();

        GameObject HeadPart(string name, MeshData m, Material mat, bool shadows)
        {
            if (m == null) return null;
            var go = Greybox.MeshObject(name, Bones["Head"], m.ToUnity(name), mat, shadows);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            go.transform.localPosition = ToUnity(HeadDetail.Pivot - RigBoneRest("Head"));
            _headParts.Add(go);
            return go;
        }

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

            // ---- body forms (a material per kit region; all bodyColor until a kit is applied) ----
            for (int i = 0; i < _region.Length; i++) _region[i] = palette.LitColor(bodyColor);
            foreach (FormPart f in MannequinForms.Build())
            {
                var go = new GameObject(f.Name);
                go.transform.SetParent(Bones[f.Bone], false);
                go.transform.localPosition = ToUnity(f.Pivot - rigPos[f.Bone]);
                var mf = go.AddComponent<MeshFilter>();
                _forms.Add((mf, f));
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _region[(int)RegionOf(f.Name)];
                FormCount++;
            }

            // ---- joint blends: shown only when the joint is bent ----
            foreach (JointBlend jb in MannequinForms.Blends())
            {
                var go = new GameObject("blend_" + jb.Bone);
                go.transform.SetParent(Bones[jb.Bone], false);
                _blendForms.Add((go.AddComponent<MeshFilter>(), jb));
                go.AddComponent<MeshRenderer>().sharedMaterial = _region[(int)RegionOf("blend_" + jb.Bone)];
                go.SetActive(false);
                _blends.Add((Bones[jb.Bone], go));
            }

            SetBody(false);

            // ---- graphics pass 2: trainers - a white midsole under each foot ----
            var sole = palette.LitColor(new Color(0.94f, 0.94f, 0.92f)); sole.SetFloat("_Smoothness", 0.3f);
            var soleMesh = Loft.Ellipsoid(0.098f, 0.29f, 0.026f).ToUnity("sole");
            foreach (var side in new[] { "R", "L" })
            {
                var sg = new GameObject("sole_" + side);
                sg.transform.SetParent(Bones["Foot_" + side], false);
                sg.AddComponent<MeshFilter>().sharedMesh = soleMesh;
                sg.AddComponent<MeshRenderer>().sharedMaterial = sole;
                sg.transform.localPosition = ToUnity(new Vector3(0f, -GateA.Ankle + 0.012f, -0.06f));
            }

            // ---- racket (placed in world from the simulated racket, not parented to the hand) ----
            var parts = RacketMesh.Build();
            Racket = new GameObject("Racket").transform;
            Racket.SetParent(transform, false);
            _frame = palette.LitColor(Greybox.Hex(0x5d656f));
            _grip = palette.LitColor(Greybox.Hex(0x23272c));
            _strings = palette.Clear(Greybox.Hex(0xdde2e8, 0.30f));
            AddPart(Racket, "racket_frame", parts.Frame, _frame, true);
            _frameRenderer = Racket.Find("racket_frame").GetComponent<MeshRenderer>();
            _frameGlow = palette.UnlitColor(Color.white);
            AddPart(Racket, "racket_handle", parts.Handle, _grip, true);
            AddPart(Racket, "racket_stringBed", parts.Bed, _strings, false);

            // ---- P2-M5: the bands (hidden until a kit has them) ----
            _headband = palette.LitColor(Color.white);
            _wristband = palette.LitColor(Color.white);
            var band = Greybox.Torus(1f, 0.16f, 32, 8);
            _headbandGo = Greybox.MeshObject("headband", Bones["Head"], band, _headband, true);
            _headbandGo.transform.localPosition = ToUnity(new Vector3(0, 0.45f * RigBoneRest("Head").y + 0.55f * GateA.Crown, 0.006f) - RigBoneRest("Head"));
            _headbandGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _headbandGo.transform.localScale = new Vector3(0.094f, 0.094f, 0.14f);
            _headbandGo.SetActive(false);
            _wristbandGo = Greybox.MeshObject("wristband", Bones["Forearm_R"], band, _wristband, true);
            _wristbandGo.transform.localPosition = ToUnity(new Vector3(RigBoneRest("Hand_R").x, RigContract.WristY + 0.035f, 0) - RigBoneRest("Forearm_R"));
            _wristbandGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _wristbandGo.transform.localScale = new Vector3(0.046f, 0.046f, 0.09f);
            _wristbandGo.SetActive(false);

            // ---- PR3: a face and hair ----
            _hair = palette.LitColor(Greybox.Hex(0x352418)); _hair.SetFloat("_Smoothness", 0.35f);
            _features = palette.LitColor(Greybox.Hex(0x1c1411)); _features.SetFloat("_Smoothness", 0.6f);
            for (int i = 0; i < _hairGo.Length; i++) _hairGo[i] = HeadPart("hair_" + i, HeadDetail.Hair(i), _hair, true);
            ShowHair(HairStyleShown);
            HeadPart("face_skin", HeadDetail.SkinFeatures(), _region[(int)Region.Skin], false);
            HeadPart("face_dark", HeadDetail.DarkFeatures(), _features, false);
            HeadPart("face_brows", HeadDetail.Brows(), _hair, false);
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
