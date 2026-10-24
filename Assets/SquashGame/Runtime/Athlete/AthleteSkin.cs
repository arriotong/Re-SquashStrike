// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteSkin.cs
// The athlete's body (2026-10-24): a skinned human from the Quaternius
// Universal Base Characters (CC0; Resources/Characters, see its LICENSE)
// worn over the game's own rig. The rig still does all of the posing
// (AthleteFigure: poses, stepping feet, the racket arm); this copies it
// onto the body every frame and then pins the body's hands and feet to
// the rig's, so the racket at contact is still exactly the simulated one.
//
//   bind      with both at rest, each body bone is turned to point the way
//             its rig bone does (bone to child; the hands, the chest and
//             the hips also roll to match), and both rests are kept
//   follow    body bone = rig bone's rotation since its rest, applied to
//             the body bone's rest; hips follow the rig's hips; the arms
//             are lengthened to the rig's (the body's are a little
//             shorter) and solved onto the rig's hands, the legs onto its
//             feet
//   kit       the body is split once (per body) into regions - shirt,
//             sleeves, shorts, socks, shoes, skin - by where each triangle
//             sits on the T-posed model; the kit colours them. Skin keeps
//             the model's painted skin, tinted to the chosen tone; hair
//             (the kit's hairstyles) and brows take the hair colour.
//
// Presentation only: it reads the rig and never the sim.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public sealed class AthleteSkin : MonoBehaviour
    {
        /// <summary>Off: the old lofted mannequin (tests, low-end fallback).</summary>
        public static bool Enabled = true;

        enum Region { Skin, Shirt, Sleeves, Shorts, Socks, Shoes }
        const int Regions = 6;

        sealed class Pair
        {
            public Transform Ours, Theirs, OursChild, TheirsChild;
            public Transform OursAcrossA, OursAcrossB, TheirsAcrossA, TheirsAcrossB;     // optional roll reference
            public Quaternion OursRest, TheirsRest;
        }

        AthleteRig _rig;
        Transform _model, _theirPelvis;
        readonly List<Pair> _pairs = new List<Pair>();
        readonly Dictionary<string, Pair> _byOurs = new Dictionary<string, Pair>();
        Vector3 _pelvisOffsetLocal;
        SkinnedMeshRenderer _body, _brows;
        readonly Material[] _mats = new Material[Regions];
        Material _skin, _hair;
        readonly Dictionary<int, GameObject> _hairs = new Dictionary<int, GameObject>();
        readonly HashSet<Renderer> _hidden = new HashSet<Renderer>();
        public bool Female { get; private set; }
        public int HairShown { get; private set; } = -1;
        /// <summary>How far the body's right hand ended from the rig's this frame (m).</summary>
        public float HandGap { get; private set; }

        public static readonly Color SkinReference = new Color(165f / 255f, 119f / 255f, 87f / 255f);   // the painted skin's average

        // ---------------- build ----------------

        /// <summary>Put the body on. The rig must be at rest (ResetPose) and the athlete unrotated.</summary>
        public void Build(AthleteRig rig, GreyboxPalette palette, bool female)
        {
            _rig = rig;
            Female = female;
            var prefab = Resources.Load<GameObject>(female ? "Characters/Superhero_Female_FullBody" : "Characters/Superhero_Male_FullBody");
            if (prefab == null) { Debug.LogWarning("AthleteSkin: no body model in Resources/Characters"); enabled = false; return; }
            _model = Instantiate(prefab, rig.transform, false).transform;
            _model.name = "Body";
            _model.localPosition = Vector3.zero; _model.localRotation = Quaternion.identity; _model.localScale = Vector3.one;

            foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;                      // bones move far from the bind pose (lunges, dives)
                if (smr.name.StartsWith("SuperHero")) _body = smr;
                else if (smr.name == "Eyebrows") _brows = smr;
            }

            // Materials: a lit colour per kit region; the skin with its painted texture; eyes, brows, hair.
            for (int i = 0; i < Regions; i++) { _mats[i] = palette.LitColor(Color.white); _mats[i].SetFloat("_Smoothness", 0.25f); }
            _skin = palette.LitColor(Color.white);
            _skin.mainTexture = Resources.Load<Texture2D>(female ? "Characters/Textures/T_Superhero_Female_Light_BaseColor" : "Characters/Textures/T_Superhero_Male_Ligh");
            _skin.SetFloat("_Smoothness", 0.3f);
            _mats[(int)Region.Skin] = _skin;
            _hair = palette.LitColor(Greybox.Hex(0x352418));
            _hair.mainTexture = Resources.Load<Texture2D>("Characters/Textures/T_Hair_1_BaseColor");
            _hair.SetFloat("_Smoothness", 0.35f);
            foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == _body) continue;
                if (smr == _brows) { smr.sharedMaterial = _hair; continue; }
                var eye = palette.LitColor(Color.white);
                eye.mainTexture = Resources.Load<Texture2D>("Characters/Textures/T_Eye_Brown");
                eye.SetFloat("_Smoothness", 0.8f);
                smr.sharedMaterial = eye;
            }
            if (_body != null)
            {
                _body.sharedMesh = SplitBody(_body, _model, female);
                _body.sharedMaterials = _mats;
            }

            // Hairstyles: "origin at 0" meshes, turned to face the way the body does (they are exported the other way
            // round) and fitted to this skull - their crown just over its crown - then carried by the head.
            Transform head = Find(_model, "Head");
            Vector3 fit = Vector3.zero;
            bool haveFit = false;
            if (_body != null)
            {
                var refHair = Resources.Load<GameObject>("Characters/Hair/" + (female ? "Hair_BuzzedFemale" : "Hair_Buzzed"));
                if (refHair != null)
                {
                    var probe = Instantiate(refHair, _model, false);
                    TurnRound(probe.transform);
                    Bounds hb = WorldBounds(probe);
                    Vector3 crown = SkullCrown(_body, _model);              // model space
                    Vector3 hairCrown = _model.InverseTransformPoint(new Vector3(hb.center.x, hb.max.y, hb.center.z));
                    fit = new Vector3(0f, crown.y + 0.012f - hairCrown.y, crown.z - hairCrown.z);
                    haveFit = true;
                    DestroyImmediate(probe);
                }
            }
            for (int style = 1; style < HeadDetail.StyleCount; style++)
                foreach (string piece in HairPieces(style, female))
                {
                    var hp = Resources.Load<GameObject>("Characters/Hair/" + piece);
                    if (hp == null) continue;
                    var h = Instantiate(hp, _model, false);
                    h.name = "Hair_" + style + "_" + piece;
                    TurnRound(h.transform);
                    if (haveFit) h.transform.localPosition += fit;
                    foreach (var r in h.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = _hair; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                    if (head != null) h.transform.SetParent(head, true);
                    if (!_hairs.ContainsKey(style)) _hairs[style] = new GameObject("HairStyle_" + style);
                    var holder = _hairs[style];
                    if (holder.transform.parent == null) holder.transform.SetParent(head != null ? head : _model, false);
                    h.transform.SetParent(holder.transform, true);
                }
            ShowHair(2);

            Map(female);
            Bind();

            Bands(head);

            // The mannequin stays (it is what gets posed) but is not drawn; the racket is.
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(_model)) continue;
                if (rig.Racket != null && r.transform.IsChildOf(rig.Racket)) continue;
                r.enabled = false;
                _hidden.Add(r);
            }
        }

        GameObject _headband, _wristband;

        /// <summary>The kit's headband and wristband, fitted to this body: round the forehead just under the crown, and on the
        /// racket wrist. They take the rig's band materials and follow its bands on and off.</summary>
        void Bands(Transform head)
        {
            if (_body == null) return;
            var band = Greybox.Torus(1f, 0.16f, 32, 8);
            Vector3 crown = SkullCrown(_body, _model);
            float y = crown.y - 0.07f;
            Vector2 r = SkullRadius(_body, _model, y);
            _headband = Greybox.MeshObject("Headband", _model, band, _rig.HeadbandMaterial, true);
            _headband.transform.localPosition = new Vector3(0f, y, crown.z);
            _headband.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _headband.transform.localScale = new Vector3(r.x + 0.006f, r.y + 0.006f, 0.16f);
            if (head != null) _headband.transform.SetParent(head, true);
            Transform hand = Find(_model, "hand_r"), fore = Find(_model, "lowerarm_r");
            if (hand != null && fore != null)
            {
                _wristband = Greybox.MeshObject("Wristband", fore, band, _rig.WristbandMaterial, true);
                Vector3 along = (hand.position - fore.position).normalized;
                _wristband.transform.position = hand.position - along * 0.045f * _model.lossyScale.x;
                _wristband.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
                _wristband.transform.localScale = Vector3.one;
                float s = 0.042f / Mathf.Max(1e-4f, _wristband.transform.lossyScale.x) * _model.lossyScale.x;
                _wristband.transform.localScale = new Vector3(s, s, s * 1.6f);
            }
        }

        /// <summary>Half-width and half-depth of the head at a height (model space).</summary>
        static Vector2 SkullRadius(SkinnedMeshRenderer smr, Transform model, float y)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            float rx = 0.07f, rz = 0.09f, zMin = float.MaxValue, zMax = float.MinValue, xMax = 0f;
            foreach (var v0 in baked.vertices)
            {
                Vector3 p = model.InverseTransformPoint(smr.transform.TransformPoint(v0));
                if (Mathf.Abs(p.y - y) > 0.012f || Mathf.Abs(p.x) > 0.13f) continue;
                xMax = Mathf.Max(xMax, Mathf.Abs(p.x)); zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z);
            }
            if (xMax > 0.03f) rx = xMax;
            if (zMax > zMin) rz = (zMax - zMin) * 0.5f;
            Destroy(baked);
            return new Vector2(rx, rz);
        }

        /// <summary>Is this one of the mannequin's renderers the body replaces (AthleteFigure keeps them hidden)?</summary>
        public bool Hides(Renderer r) { return _hidden.Contains(r); }

        /// <summary>The kit's hairstyles (Cosmetics.HairStyles: shaved, buzzed, parted, long, buns, buzzed + beard, parted + beard).</summary>
        static string[] HairPieces(int style, bool female)
        {
            switch (style)
            {
                case 1: return new[] { female ? "Hair_BuzzedFemale" : "Hair_Buzzed" };
                case 2: return new[] { "Hair_SimpleParted" };
                case 3: return new[] { "Hair_Long" };
                case 4: return new[] { "Hair_Buns" };
                case 5: return female ? new[] { "Hair_BuzzedFemale" } : new[] { "Hair_Buzzed", "Hair_Beard" };
                case 6: return female ? new[] { "Hair_Long" } : new[] { "Hair_SimpleParted", "Hair_Beard" };
                default: return new string[0];
            }
        }

        /// <summary>Half a turn about the model's vertical axis (the hair pieces face +Z; the body faces -Z).</summary>
        static void TurnRound(Transform t)
        {
            t.localRotation = Quaternion.Euler(0f, 180f, 0f) * t.localRotation;
            t.localPosition = Quaternion.Euler(0f, 180f, 0f) * t.localPosition;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>The top of the skull in model space: the highest point near the midline, at the head's middle depth.</summary>
        static Vector3 SkullCrown(SkinnedMeshRenderer smr, Transform model)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var v = baked.vertices;
            float top = float.MinValue, zSum = 0; int n = 0;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = model.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                if (Mathf.Abs(p.x) < 0.1f && p.y > top) top = p.y;
            }
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = model.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
                if (Mathf.Abs(p.x) < 0.1f && p.y > top - 0.06f) { zSum += p.z; n++; }
            }
            Destroy(baked);
            return new Vector3(0f, top, n > 0 ? zSum / n : 0f);
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }

        void Add(string ours, string theirs, string oursChild, string theirsChild, string oa = null, string ob = null, string ta = null, string tb = null)
        {
            var b = _rig.Bones;
            Transform o = b.TryGetValue(ours, out var x) ? x : null, t = Find(_model, theirs);
            if (o == null || t == null) return;
            var p = new Pair
            {
                Ours = o, Theirs = t,
                OursChild = oursChild != null && b.TryGetValue(oursChild, out var oc) ? oc : null,
                TheirsChild = theirsChild != null ? Find(_model, theirsChild) : null,
                OursAcrossA = oa != null && b.TryGetValue(oa, out var a1) ? a1 : null, OursAcrossB = ob != null && b.TryGetValue(ob, out var a2) ? a2 : null,
                TheirsAcrossA = ta != null ? Find(_model, ta) : null, TheirsAcrossB = tb != null ? Find(_model, tb) : null,
            };
            _pairs.Add(p);
            _byOurs[ours] = p;
        }

        /// <summary>The rig's bones and the body's, parents first.</summary>
        void Map(bool female)
        {
            Add("Pelvis", "pelvis", "Spine_01", "spine_01", "Thigh_L", "Thigh_R", "thigh_l", "thigh_r");
            Add("Spine_01", "spine_01", "Spine_02", "spine_02");
            Add("Spine_02", "spine_02", "Chest", "spine_03");
            Add("Chest", "spine_03", "Neck", "neck_01", "UpperArm_L", "UpperArm_R", "upperarm_l", "upperarm_r");
            Add("Neck", "neck_01", "Head", "Head");
            Add("Head", "Head", null, null);
            foreach (var s in new[] { "R", "L" })
            {
                string t = s.ToLowerInvariant();
                Add("Clavicle_" + s, "clavicle_" + t, "UpperArm_" + s, "upperarm_" + t);
                Add("UpperArm_" + s, "upperarm_" + t, "Forearm_" + s, "lowerarm_" + t);
                Add("Forearm_" + s, "lowerarm_" + t, "Hand_" + s, "hand_" + t);
                Add("Hand_" + s, "hand_" + t, "Middle_01_" + s, "middle_01_" + t, "Index_01_" + s, "Pinky_01_" + s, "index_01_" + t, "pinky_01_" + t);
                foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky", "Thumb" })
                {
                    string g = f.ToLowerInvariant();
                    Add(f + "_01_" + s, g + "_01_" + t, f + "_02_" + s, g + "_02_" + t);
                    Add(f + "_02_" + s, g + "_02_" + t, f + "_03_" + s, g + "_03_" + t);
                    Add(f + "_03_" + s, g + "_03_" + t, f + "_End_" + s, g + "_04_leaf_" + t);
                }
                Add("Thigh_" + s, "thigh_" + t, "Shin_" + s, "calf_" + t);
                Add("Shin_" + s, "calf_" + t, "Foot_" + s, "foot_" + t);
                Add("Foot_" + s, "foot_" + t, "Toe_" + s, "ball_" + t);
                Add("Toe_" + s, "ball_" + t, "Toe_End_" + s, "ball_leaf_" + t);
            }
            _theirPelvis = Find(_model, "pelvis");
        }

        /// <summary>Turn each body bone to lie along its rig bone (and roll to match where given), then keep both rests.</summary>
        void Bind()
        {
            foreach (var p in _pairs)
            {
                if (p.OursChild != null && p.TheirsChild != null)
                {
                    Vector3 od = (p.OursChild.position - p.Ours.position).normalized, td = (p.TheirsChild.position - p.Theirs.position).normalized;
                    if (od.sqrMagnitude > 0.5f && td.sqrMagnitude > 0.5f) p.Theirs.rotation = Quaternion.FromToRotation(td, od) * p.Theirs.rotation;
                    if (p.OursAcrossA != null && p.OursAcrossB != null && p.TheirsAcrossA != null && p.TheirsAcrossB != null)
                    {
                        Vector3 axis = od;
                        Vector3 oa = Vector3.ProjectOnPlane(p.OursAcrossB.position - p.OursAcrossA.position, axis);
                        Vector3 ta = Vector3.ProjectOnPlane(p.TheirsAcrossB.position - p.TheirsAcrossA.position, axis);
                        if (oa.sqrMagnitude > 1e-6f && ta.sqrMagnitude > 1e-6f)
                            p.Theirs.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(ta, oa, axis), axis) * p.Theirs.rotation;
                    }
                }
            }
            foreach (var p in _pairs) { p.OursRest = p.Ours.rotation; p.TheirsRest = p.Theirs.rotation; }
            if (_theirPelvis != null) _pelvisOffsetLocal = _rig.transform.InverseTransformVector(_theirPelvis.position - _rig.Bones["Pelvis"].position);
            // The arms take the rig's lengths (the body's are a few centimetres shorter): the forearm and hand bones move out.
            foreach (var s in new[] { "R", "L" })
            {
                Stretch("UpperArm_" + s, "Forearm_" + s);
                Stretch("Forearm_" + s, "Hand_" + s);
            }
        }

        void Stretch(string oursBone, string oursChild)
        {
            if (!_byOurs.TryGetValue(oursBone, out var p) || !_byOurs.TryGetValue(oursChild, out var c)) return;
            float want = Vector3.Distance(p.Ours.position, c.Ours.position), have = Vector3.Distance(p.Theirs.position, c.Theirs.position);
            if (have < 1e-4f || Mathf.Abs(want - have) < 0.005f) return;
            c.Theirs.localPosition *= want / have;
        }

        // ---------------- per frame ----------------

        /// <summary>Pose the body from the rig (call after the rig is posed).</summary>
        public void Follow()
        {
            if (_model == null) return;
            if (_headband != null && _headband.activeSelf != _rig.HeadbandShown) _headband.SetActive(_rig.HeadbandShown);
            if (_wristband != null && _wristband.activeSelf != _rig.WristbandShown) _wristband.SetActive(_rig.WristbandShown);
            foreach (var p in _pairs) p.Theirs.rotation = p.Ours.rotation * Quaternion.Inverse(p.OursRest) * p.TheirsRest;
            if (_theirPelvis != null) _theirPelvis.position = _rig.Bones["Pelvis"].position + _rig.transform.TransformVector(_pelvisOffsetLocal);
            // Hands and feet exactly where the rig has them: the racket and the planted feet are the truth.
            HandGap = Pin("UpperArm_R", "Forearm_R", "Hand_R");
            Pin("UpperArm_L", "Forearm_L", "Hand_L");
            Pin("Thigh_R", "Shin_R", "Foot_R");
            Pin("Thigh_L", "Shin_L", "Foot_L");
        }

        float Pin(string a, string b, string c)
        {
            if (!_byOurs.TryGetValue(a, out var pa) || !_byOurs.TryGetValue(b, out var pb) || !_byOurs.TryGetValue(c, out var pc)) return 0f;
            float gap = TwoBoneIK.Solve(pa.Theirs, pb.Theirs, pc.Theirs, pc.Ours.position, pb.Ours.position);
            pc.Theirs.rotation = pc.Ours.rotation * Quaternion.Inverse(pc.OursRest) * pc.TheirsRest;
            // Fingers / toes ride on the end bone again after the solve.
            foreach (var q in _pairs)
                if (q.Ours.parent != null && q.Ours.IsChildOf(pc.Ours) && q != pc) q.Theirs.rotation = q.Ours.rotation * Quaternion.Inverse(q.OursRest) * q.TheirsRest;
            return gap;
        }

        // ---------------- kit ----------------

        public void ApplyKit(SquashGame.Progression.Kit k)
        {
            if (_model == null) return;
            _mats[(int)Region.Shirt].color = k.ShirtBody;
            _mats[(int)Region.Sleeves].color = k.ShirtSleeves;
            _mats[(int)Region.Shorts].color = k.Shorts;
            _mats[(int)Region.Socks].color = k.Socks;
            _mats[(int)Region.Shoes].color = k.Shoes;
            Color tint = new Color(k.Skin.r / SkinReference.r, k.Skin.g / SkinReference.g, k.Skin.b / SkinReference.b);
            _skin.color = new Color(Mathf.Min(tint.r, 1.6f), Mathf.Min(tint.g, 1.6f), Mathf.Min(tint.b, 1.6f));
            _hair.color = k.Hair * 1.6f;                              // the hair texture is a mid grey
            ShowHair(k.HairStyle);
        }

        void ShowHair(int style)
        {
            HairShown = style;
            foreach (var kv in _hairs) kv.Value.SetActive(kv.Key == style);
        }

        // ---------------- the regions (cached per body) ----------------

        static readonly Dictionary<string, Mesh> _split = new Dictionary<string, Mesh>();

        /// <summary>The body mesh with one submesh per kit region, chosen by where each triangle sits on the T-posed model
        /// (the model's own space: metres, y up, facing -z).</summary>
        static Mesh SplitBody(SkinnedMeshRenderer smr, Transform model, bool female)
        {
            string key = smr.sharedMesh.name + (female ? "_f" : "_m");
            if (_split.TryGetValue(key, out var cached) && cached != null) return cached;
            var src = smr.sharedMesh;
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var v = baked.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = model.InverseTransformPoint(smr.transform.TransformPoint(v[i]));
            // Shoulder line and upper-arm length from the skeleton at rest.
            Transform ua = Find(model, "upperarm_r"), la = Find(model, "lowerarm_r");
            float shoulder = ua != null ? Mathf.Abs(model.InverseTransformPoint(ua.position).x) : 0.2f;
            float upper = ua != null && la != null ? Vector3.Distance(ua.position, la.position) / Mathf.Max(1e-4f, model.lossyScale.x) : 0.25f;
            float sleeveEnd = shoulder + upper * 0.5f;

            var tris = src.triangles;
            var lists = new List<int>[Regions];
            for (int r = 0; r < Regions; r++) lists[r] = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 c = (v[tris[t]] + v[tris[t + 1]] + v[tris[t + 2]]) / 3f;
                float ax = Mathf.Abs(c.x);
                Region reg;
                if (c.y < 0.115f) reg = Region.Shoes;
                else if (c.y < 0.32f) reg = Region.Socks;
                else if (c.y >= 0.60f && c.y < 0.99f && ax < 0.33f) reg = Region.Shorts;
                else if (c.y >= 0.99f && c.y < 1.50f && ax < shoulder) reg = Region.Shirt;
                else if (c.y >= 1.28f && c.y < 1.62f && ax >= shoulder - 0.02f && ax < sleeveEnd) reg = Region.Sleeves;
                else reg = Region.Skin;
                var l = lists[(int)reg];
                l.Add(tris[t]); l.Add(tris[t + 1]); l.Add(tris[t + 2]);
            }
            var m = Instantiate(src);
            m.name = src.name + "_kit";
            m.subMeshCount = Regions;
            for (int r = 0; r < Regions; r++) m.SetTriangles(lists[r], r);
            Destroy(baked);
            _split[key] = m;
            return m;
        }
    }
}
