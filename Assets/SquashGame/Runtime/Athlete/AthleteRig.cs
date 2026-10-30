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
        readonly List<(SkinnedMeshRenderer smr, FormPart part)> _forms = new List<(SkinnedMeshRenderer, FormPart)>();
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
            // 2026-10-30: a faint glow of its own, so a face in the court's shade never goes black and its features read
            _region[(int)Region.Skin].EnableKeyword("_EMISSION");
            _region[(int)Region.Skin].SetColor("_EmissionColor", k.Skin * 0.12f);
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
            foreach (var (smr, f) in _forms)
            {
                string key = f.Name + (female ? "_f" : "_m");
                if (!_bodyMeshes.TryGetValue(key, out var mesh) || mesh == null)
                {
                    var data = MannequinForms.SlimmedForm(f, female);
                    _bodyMeshes[key] = mesh = data.ToUnity(key);
                    Skin(mesh, data, f, smr);
                }
                smr.sharedMesh = mesh;
            }
            foreach (var (mf, jb) in _blendForms)                               // (none since the skinning)
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
            // 2026-10-30: the finish of each - skin soft and matte (it had the mannequin's wooden sheen), cloth flat, shoes a little shine
            for (int i = 0; i < _region.Length; i++)
            {
                var r = (Region)i;
                _region[i].SetFloat("_Smoothness", r == Region.Skin ? 0.22f : r == Region.Shoes ? 0.38f : 0.1f);
                _region[i].SetFloat("_Metallic", 0f);
                if (r == Region.Skin && _region[i].HasProperty("_SpecColor")) _region[i].SetColor("_SpecColor", new Color(0.18f, 0.16f, 0.15f));
            }
            foreach (FormPart f in MannequinForms.Build())
            {
                var go = new GameObject(f.Name);
                go.transform.SetParent(Bones[f.Bone], false);
                go.transform.localPosition = ToUnity(f.Pivot - rigPos[f.Bone]);
                // 2026-10-30 (the players, step 4): each form is SKINNED - near a joint it bends with the next bone, like
                // skin, so elbows and knees no longer show as a mannequin's balls and seams (the joint blends are gone)
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                var chain = ChainOf(f.Name, f.Bone);
                var bones = new Transform[chain.Length];
                var bind = new Matrix4x4[chain.Length];
                for (int k = 0; k < chain.Length; k++) { bones[k] = Bones[chain[k].bone]; bind[k] = bones[k].worldToLocalMatrix * go.transform.localToWorldMatrix; }
                smr.bones = bones;
                smr.rootBone = Bones[f.Bone];
                smr.updateWhenOffscreen = true;
                smr.quality = SkinQuality.Bone4;
                var hems = HemsOf(f.Name);
                if (hems != null) { var mats = new Material[hems.Value.regions.Length]; for (int k = 0; k < mats.Length; k++) mats[k] = _region[(int)hems.Value.regions[k]]; smr.sharedMaterials = mats; }
                else smr.sharedMaterials = new[] { _region[(int)RegionOf(f.Name)] };
                _bind[f.Name] = bind;
                _forms.Add((smr, f));
                FormCount++;
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
            var whites = palette.LitColor(new Color(0.93f, 0.92f, 0.88f)); whites.SetFloat("_Smoothness", 0.5f);
            HeadPart("face_eyewhites", HeadDetail.EyeWhites(), whites, false);
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

        // ---------------- the skinning (2026-10-30) ----------------

        readonly Dictionary<string, Matrix4x4[]> _bind = new Dictionary<string, Matrix4x4[]>();

        /// <summary>A bone of a form's chain: the joint with the bone before it at this height (rig y), blended over +-band.</summary>
        public struct ChainBone { public string bone; public float joint, band; }

        static ChainBone B(string bone, float joint, float band) { return new ChainBone { bone = bone, joint = joint, band = band }; }

        /// <summary>The bones a form bends with, in order along it (rising for the torso, falling down an arm or a leg).
        /// The head, hands and feet keep to their own bone.</summary>
        public static ChainBone[] ChainOf(string form, string bone)
        {
            string n = form.ToLowerInvariant();
            string side = form.EndsWith("R") ? "R" : "L";
            if (n.Contains("pelvis") || n.Contains("abdomen") || n.Contains("chest") || n.Contains("neck"))
                return new[] { B("Pelvis", 0f, 0f), B("Spine_01", RigContract.Spine01Y, 0.04f), B("Spine_02", RigContract.Spine02Y, 0.05f),
                               B("Chest", RigContract.ChestY, 0.06f), B("Neck", GateA.NeckBase, 0.025f), B("Head", RigContract.HeadY, 0.02f) };
            if (n.Contains("arm") || n.Contains("hand") || n.Contains("thumb"))     // (the hand: a soft wrist)
                return new[] { B("Clavicle_" + side, 0f, 0f), B("UpperArm_" + side, GateA.Shoulder, 0.03f), B("Forearm_" + side, RigContract.ElbowY, 0.05f),
                               B("Hand_" + side, RigContract.WristY, 0.03f) };
            if (n.Contains("leg") || n.Contains("thigh") || n.Contains("shin"))
                return new[] { B("Pelvis", 0f, 0f), B("Thigh_" + side, GateA.Hip, 0.05f), B("Shin_" + side, GateA.Knee, 0.08f), B("Foot_" + side, GateA.Ankle, 0.03f) };
            return new[] { B(bone, 0f, 0f) };
        }

        /// <summary>A vertex's weights along a chain by its height: its own segment's bone, shared with the next bone over the
        /// joint's band (half and half on the joint line). The torso's chain rises; a limb's falls.</summary>
        public static void ChainWeights(ChainBone[] chain, float y, float[] w)
        {
            for (int i = 0; i < w.Length; i++) w[i] = 0f;
            if (chain.Length == 1) { w[0] = 1f; return; }
            bool down = chain[chain.Length - 1].joint < chain[1].joint;          // a limb: the joints fall
            float u = down ? -y : y;
            int k = 0;                                                         // the segment's bone: the last joint passed
            for (int i = 1; i < chain.Length; i++) if (u >= (down ? -chain[i].joint : chain[i].joint)) k = i;
            w[k] = 1f;
            if (k > 0)                                                         // the joint below (towards the chain's start)
            {
                float j = down ? -chain[k].joint : chain[k].joint, b = chain[k].band;
                float share = 0.5f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - j) / Mathf.Max(1e-4f, b))));
                w[k - 1] += share; w[k] -= share;
            }
            if (k + 1 < chain.Length)                                          // the joint above
            {
                float j = down ? -chain[k + 1].joint : chain[k + 1].joint, b = chain[k + 1].band;
                float share = 0.5f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((j - u) / Mathf.Max(1e-4f, b))));
                w[k + 1] += share; w[k] -= share;
            }
        }

        /// <summary>Kit that stops part-way down a limb (2026-10-30: a whole limb in one colour read as a mannequin's - or
        /// tights): short sleeves, shorts to mid-thigh, ankle socks, skin between. The hems' rig heights falling, and the
        /// region of each band between them (one more than the hems).</summary>
        public static (float[] ys, Region[] regions)? HemsOf(string form)
        {
            string n = form.ToLowerInvariant();
            if (n.StartsWith("form_arm")) return (new[] { GateA.Shoulder - 0.14f }, new[] { Region.ShirtSleeves, Region.Skin });
            if (n.StartsWith("form_leg")) return (new[] { 0.66f, 0.24f }, new[] { Region.Shorts, Region.Skin, Region.Socks });
            return null;
        }

        /// <summary>A form's mesh skinned to its chain: each vertex's weights by its rig height, the bind poses of its renderer.</summary>
        void Skin(Mesh mesh, MeshData data, FormPart f, SkinnedMeshRenderer smr)
        {
            var chain = ChainOf(f.Name, f.Bone);
            var weights = new BoneWeight[data.V.Count];
            var w = new float[chain.Length];
            for (int v = 0; v < data.V.Count; v++)
            {
                ChainWeights(chain, data.V[v].y + f.Pivot.y, w);
                // the four largest (a chain's vertex never has more than three)
                var bw = new BoneWeight();
                int[] idx = { -1, -1, -1, -1 }; float[] val = { 0, 0, 0, 0 };
                for (int i = 0; i < w.Length; i++)
                    for (int s2 = 0; s2 < 4; s2++)
                        if (w[i] > val[s2]) { for (int t = 3; t > s2; t--) { val[t] = val[t - 1]; idx[t] = idx[t - 1]; } val[s2] = w[i]; idx[s2] = i; break; }
                float sum = val[0] + val[1] + val[2] + val[3];
                if (sum <= 0f) { val[0] = 1f; idx[0] = 0; sum = 1f; }
                bw.boneIndex0 = Mathf.Max(0, idx[0]); bw.weight0 = val[0] / sum;
                bw.boneIndex1 = Mathf.Max(0, idx[1]); bw.weight1 = val[1] / sum;
                bw.boneIndex2 = Mathf.Max(0, idx[2]); bw.weight2 = val[2] / sum;
                bw.boneIndex3 = Mathf.Max(0, idx[3]); bw.weight3 = val[3] / sum;
                weights[v] = bw;
            }
            mesh.boneWeights = weights;
            mesh.bindposes = _bind[f.Name];
            // the kit's hems: the triangles of each band between them in its own submesh (a material each)
            var hems = HemsOf(f.Name);
            if (hems.HasValue)
            {
                var ys = hems.Value.ys;
                var bands = new List<int>[ys.Length + 1];
                for (int k = 0; k < bands.Length; k++) bands[k] = new List<int>();
                for (int t = 0; t + 2 < data.T.Count; t += 3)
                {
                    // by the triangle's top corner: both triangles of a strip go the same way (a straight hem, not a zigzag)
                    float cy = Mathf.Max(data.V[data.T[t]].y, Mathf.Max(data.V[data.T[t + 1]].y, data.V[data.T[t + 2]].y)) + f.Pivot.y;
                    int band = 0; while (band < ys.Length && cy < ys[band]) band++;
                    bands[band].Add(data.T[t]); bands[band].Add(data.T[t + 1]); bands[band].Add(data.T[t + 2]);
                }
                mesh.subMeshCount = bands.Length;
                for (int k = 0; k < bands.Length; k++) mesh.SetTriangles(bands[k], k);
            }
            mesh.RecalculateBounds();
        }

        /// <summary>(The joint blends are gone: the forms are skinned - 2026-10-30.)</summary>
        public void UpdateBlends() { }
    }
}
