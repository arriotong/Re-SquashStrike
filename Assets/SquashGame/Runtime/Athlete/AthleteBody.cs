// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteBody.cs
// 2026-11-01 the real players: a MakeHuman body (PlayerModels) drawn in
// place of the code-built figure. The figure (AthleteFigure + its rig)
// stays the brain - the sim's racket, the reach, the split step, the
// dive, planted feet - and is no longer drawn (its racket still is).
// Each frame, after it has posed its rig:
//   1. its pose is carried onto the body bone by bone (each bone's turn
//      from its rest, applied to the body's matching bone);
//   2. running, the motion capture's legs, hips and arms take over
//      (walk / jog / sprint blended by speed, played at the pace that
//      keeps the feet from sliding);
//   3. IK: the racket hand to the figure's hand (so the racket meets the
//      ball where the sim says), the free hand, the feet to the floor.
// Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SquashGame.Athlete
{
    [DefaultExecutionOrder(100)]                 // after AthleteFigure has posed its rig
    public sealed class AthleteBody : MonoBehaviour
    {
        public AthleteFigure Figure { get; private set; }
        public Animator Anim { get; private set; }
        public string ModelId { get; private set; }
        /// <summary>The body's height before its scale to the figure's (tests).</summary>
        public float Stature { get; private set; }
        public readonly List<PlayerModels.Part> Parts = new List<PlayerModels.Part>();
        GameObject _model;

        // ---- the bone pairs: the figure's rig bone, the body's Humanoid bone ----
        static readonly List<(string rig, HumanBodyBones body, string child)> Pairs = new List<(string, HumanBodyBones, string)>
        {
            ("Pelvis", HumanBodyBones.Hips, "Spine_01"),
            ("Spine_01", HumanBodyBones.Spine, "Spine_02"),
            ("Spine_02", HumanBodyBones.Chest, "Chest"),
            ("Chest", HumanBodyBones.UpperChest, "Neck"),
            ("Neck", HumanBodyBones.Neck, "Head"),
            ("Head", HumanBodyBones.Head, "Head_End"),
            ("Clavicle_L", HumanBodyBones.LeftShoulder, "UpperArm_L"), ("Clavicle_R", HumanBodyBones.RightShoulder, "UpperArm_R"),
            ("UpperArm_L", HumanBodyBones.LeftUpperArm, "Forearm_L"), ("UpperArm_R", HumanBodyBones.RightUpperArm, "Forearm_R"),
            ("Forearm_L", HumanBodyBones.LeftLowerArm, "Hand_L"), ("Forearm_R", HumanBodyBones.RightLowerArm, "Hand_R"),
            ("Hand_L", HumanBodyBones.LeftHand, "Middle_01_L"), ("Hand_R", HumanBodyBones.RightHand, "Middle_01_R"),
            ("Thigh_L", HumanBodyBones.LeftUpperLeg, "Shin_L"), ("Thigh_R", HumanBodyBones.RightUpperLeg, "Shin_R"),
            ("Shin_L", HumanBodyBones.LeftLowerLeg, "Foot_L"), ("Shin_R", HumanBodyBones.RightLowerLeg, "Foot_R"),
            ("Foot_L", HumanBodyBones.LeftFoot, "Toe_L"), ("Foot_R", HumanBodyBones.RightFoot, "Toe_R"),
            ("Toe_L", HumanBodyBones.LeftToes, "Toe_End_L"), ("Toe_R", HumanBodyBones.RightToes, "Toe_End_R"),
        };

        // the fingers too: the racket hand closes round the grip as the figure's does
        static AthleteBody()
        {
            string[] rigF = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
            HumanBodyBones[] l = { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal };
            HumanBodyBones[] r = { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
            for (int s = 0; s < 2; s++)
                for (int f = 0; f < rigF.Length; f++)
                    for (int k = 0; k < 3; k++)
                    {
                        string side = s == 0 ? "L" : "R";
                        string bone = rigF[f] + "_0" + (k + 1) + "_" + side, child = rigF[f] + (k < 2 ? "_0" + (k + 2) : "_End") + "_" + side;
                        Pairs.Add((bone, (HumanBodyBones)((int)(s == 0 ? l[f] : r[f]) + k), child));
                    }
        }

        sealed class Pair
        {
            public Transform Rig, Body, RigChild;
            public Quaternion RigRest, BodyRestAdj;            // root-local at rest; the body bone turned to the rig bone's rest direction
            public bool Legs, Arm, Spine;
        }
        readonly List<Pair> _pairs = new List<Pair>();
        Vector3 _hipsOffset;                                   // body hips minus rig pelvis at rest (root-local)
        Transform _bodyHips, _rigPelvis;

        // ---- motion capture ----
        PlayableGraph _graph;
        AnimationMixerPlayable _loco;
        readonly AnimationClipPlayable[] _locoClips = new AnimationClipPlayable[4];   // idle, walk, jog, sprint
        static readonly string[] LocoNames = { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop" };
        public static readonly float[] LocoSpeeds = { 0f, 1.5f, 3.2f, 6.0f };           // m/s each clip reads as
        readonly Dictionary<HumanBodyBones, Quaternion> _anim = new Dictionary<HumanBodyBones, Quaternion>();
        /// <summary>How much of the motion capture is shown now, 0..1 (tests).</summary>
        public float MocapWeight { get; private set; }
        float _mocapW;
        Vector3 _vel;

        /// <summary>The real players are drawn (Settings: off = the classic figure). Set before a court is built.</summary>
        public static bool Enabled = true;
        Material _lit;


        /// <summary>Give a figure its real body, when they're on and the art is there (else it stays the classic figure).</summary>
        public static AthleteBody Attach(AthleteFigure figure, Material lit)
        {
            if (!Enabled) return null;
            var b = figure.gameObject.AddComponent<AthleteBody>();
            if (!b.Build(figure, PlayerModels.BodyFor(b.IsYou(figure), false, 2), lit)) { Destroy(b); return null; }
            return b;
        }

        /// <summary>This figure is the person playing (else an opponent).</summary>
        bool IsYou(AthleteFigure f) { return f.Runner == null || f.Index == f.Runner.HumanIndex; }

        /// <summary>Put a body on this figure; false (and nothing changed) if the art isn't there.</summary>
        public bool Build(AthleteFigure figure, string modelId, Material litTemplate)
        {
            Figure = figure;
            ModelId = modelId;
            _lit = litTemplate;
            figure.Dressed -= OnDressed; figure.Dressed += OnDressed;
            _model = PlayerModels.Spawn(modelId, figure.transform, litTemplate, Parts);
            if (_model == null) return false;
            Anim = _model.GetComponent<Animator>();
            if (Anim == null || Anim.avatar == null || !Anim.avatar.isHuman) { Destroy(_model); return false; }
            Anim.applyRootMotion = false;
            Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // the rig at rest; the body at its bind pose (T), turned to face where the rig faces
            var rig = figure.Rig;
            rig.ResetPose();
            _model.transform.localPosition = Vector3.zero;
            Stature = PlayerModels.Statures.TryGetValue(modelId, out var st) ? st : 0f;    // (Spawn drew it at the figure's height)
            Vector3 rigFwd = Flat(rig.Bones["Toe_End_R"].position - rig.Bones["Foot_R"].position);
            Vector3 bodyFwd = Flat(Anim.GetBoneTransform(HumanBodyBones.RightToes).position - Anim.GetBoneTransform(HumanBodyBones.RightFoot).position);
            _model.transform.localRotation = Quaternion.FromToRotation(bodyFwd, rigFwd);
            var root = figure.transform;
            foreach (var (rb, hb, child) in Pairs)
            {
                if (!rig.Bones.TryGetValue(rb, out var rt)) continue;
                var bt = Anim.GetBoneTransform(hb);
                if (bt == null) continue;
                rig.Bones.TryGetValue(child, out var rc);
                var p = new Pair { Rig = rt, Body = bt, RigChild = rc };
                p.RigRest = Quaternion.Inverse(root.rotation) * rt.rotation;
                Vector3 rigDir = rc != null ? (rc.position - rt.position).normalized : rt.up;
                Vector3 bodyDir = BodyDir(hb, bt);
                p.BodyRestAdj = Quaternion.Inverse(root.rotation) * (Quaternion.FromToRotation(bodyDir, rigDir) * bt.rotation);
                string n = rb;
                p.Legs = n.StartsWith("Thigh") || n.StartsWith("Shin") || n.StartsWith("Foot") || n.StartsWith("Toe") || n == "Pelvis";
                p.Arm = n.StartsWith("Clavicle") || n.StartsWith("UpperArm") || n.StartsWith("Forearm") || n.StartsWith("Hand") ||
                        n.StartsWith("Thumb") || n.StartsWith("Index") || n.StartsWith("Middle") || n.StartsWith("Ring") || n.StartsWith("Pinky");
                p.Spine = n.StartsWith("Spine") || n == "Chest";
                _pairs.Add(p);
            }
            _bodyHips = Anim.GetBoneTransform(HumanBodyBones.Hips);
            _rigPelvis = rig.Bones["Pelvis"];
            _hipsOffset = root.InverseTransformPoint(_bodyHips.position) - root.InverseTransformPoint(_rigPelvis.position);

            BuildGrip();
            FindBands();
            BuildGraph();
            Show(Wanted);                                        // the figure's own body is no longer drawn - its racket is
            return true;
        }

        /// <summary>The direction a body bone points at rest: to its child along the chain.</summary>
        Vector3 BodyDir(HumanBodyBones hb, Transform bt)
        {
            HumanBodyBones next = hb switch
            {
                HumanBodyBones.Hips => HumanBodyBones.Spine, HumanBodyBones.Spine => HumanBodyBones.Chest, HumanBodyBones.Chest => HumanBodyBones.UpperChest,
                HumanBodyBones.UpperChest => HumanBodyBones.Neck, HumanBodyBones.Neck => HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder => HumanBodyBones.LeftUpperArm, HumanBodyBones.RightShoulder => HumanBodyBones.RightUpperArm,
                HumanBodyBones.LeftUpperArm => HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm => HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftLowerArm => HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm => HumanBodyBones.RightHand,
                HumanBodyBones.LeftHand => HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightHand => HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.LeftUpperLeg => HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg => HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftLowerLeg => HumanBodyBones.LeftFoot, HumanBodyBones.RightLowerLeg => HumanBodyBones.RightFoot,
                HumanBodyBones.LeftFoot => HumanBodyBones.LeftToes, HumanBodyBones.RightFoot => HumanBodyBones.RightToes,
                _ => HumanBodyBones.LastBone
            };
            var c = next != HumanBodyBones.LastBone ? Anim.GetBoneTransform(next) : null;
            if (c == null && bt.childCount > 0) c = bt.GetChild(0);
            if (hb == HumanBodyBones.Head) return Vector3.up;
            if (hb == HumanBodyBones.LeftToes || hb == HumanBodyBones.RightToes) return Flat(Figure.transform.forward) == Vector3.zero ? Vector3.forward : (bt.childCount > 0 ? (bt.GetChild(0).position - bt.position).normalized : bt.forward);
            return c != null ? (c.position - bt.position).normalized : bt.up;
        }

        // ---- 2026-11-02 the squash grip, on the body's own hand ----
        /// <summary>The racket hand's turn, relative to the racket (a handshake grip: the handle across the palm from the
        /// heel to the index knuckle, the strings facing where the palm does); the palm's middle, in the hand's turn.</summary>
        Quaternion _gripRot = Quaternion.identity;
        Vector3 _palmOffset;
        bool _grip;
        /// <summary>Where along the handle the palm holds it, metres up from the butt (the hand high, its heel at the cap).</summary>
        public const float GripUp = 0.065f;
        /// <summary>Each finger joint's turn in its parent, closed round the handle (racket hand) or loosely curled (free hand).</summary>
        readonly List<(Transform bone, Quaternion local)> _fist = new List<(Transform, Quaternion)>(), _relaxed = new List<(Transform, Quaternion)>();
        /// <summary>How far the racket is drawn from where the sim has it (the body's arm couldn't reach it), metres (tests).</summary>
        public float GripGap { get; private set; }
        /// <summary>How far the chest leant toward an out-of-reach racket, degrees.</summary>
        public const float MaxLean = 30f;
        /// <summary>How far the body may be moved toward a racket still out of reach after the lean, metres; how far it was now.</summary>
        public const float MaxBodyShift = 0.5f;
        public float BodyShift { get; private set; }

        /// <summary>The hand's frame from its bones: along the fingers, across the knuckles (little finger to index), the palm's normal.</summary>
        bool HandFrame(bool right, out Vector3 along, out Vector3 across, out Vector3 palm, out float len)
        {
            along = across = palm = Vector3.zero; len = 0f;
            var hand = Anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var mid = Anim.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var idx = Anim.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var lit = Anim.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || mid == null || idx == null || lit == null) return false;
            len = Vector3.Distance(hand.position, mid.position);
            along = (mid.position - hand.position).normalized;
            across = Vector3.ProjectOnPlane(idx.position - lit.position, along).normalized;
            palm = Vector3.Cross(along, across);                 // a right hand palm-down, fingers +X, thumb +Z: palm -Y
            if (!right) palm = -palm;
            return true;
        }

        /// <summary>The grip, measured once at the body's bind pose: the hand turned onto the racket, the fingers closed.</summary>
        void BuildGrip()
        {
            var racket = Figure.Rig.Racket;
            var hand = Anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (racket == null || hand == null || !HandFrame(true, out var along, out var across, out var palm, out float len)) return;
            // the handle runs diagonally across the palm: from the heel (little-finger side) to the index knuckle
            Vector3 handle = (across + 0.8f * along).normalized;
            Quaternion handFrame = Quaternion.LookRotation(handle, palm);
            Quaternion racketFrame = Quaternion.LookRotation(-racket.up, racket.right);      // the shaft to the tip; the strings (+X) face the palm's way
            Quaternion turn = racketFrame * Quaternion.Inverse(handFrame);
            _gripRot = Quaternion.Inverse(racket.rotation) * (turn * hand.rotation);
            // the handle's middle sits in the palm, a little in from the bones
            Vector3 palmMid = hand.position + along * (0.55f * len) + palm * (0.33f * len);
            _palmOffset = Quaternion.Inverse(hand.rotation) * (palmMid - hand.position);

            // the fingers closed round it, the index looser (a trigger); the free hand loosely curled
            Curl(true, new[] { 30f, 40f, 30f }, new[] { 55f, 70f, 40f }, new[] { 75f, 95f, 55f }, new[] { 80f, 95f, 55f }, new[] { 85f, 95f, 55f }, _fist);
            Curl(false, new[] { 10f, 15f, 10f }, new[] { 20f, 25f, 15f }, new[] { 25f, 30f, 18f }, new[] { 28f, 32f, 20f }, new[] { 32f, 35f, 22f }, _relaxed);
            _grip = true;
        }

        /// <summary>Each finger joint bent toward the palm by these degrees (thumb, index, middle, ring, little; base to tip),
        /// kept as its turn in its parent; the hand is put back as it was.</summary>
        void Curl(bool right, float[] thumb, float[] index, float[] middle, float[] ring, float[] little, List<(Transform, Quaternion)> into)
        {
            if (!HandFrame(right, out var along, out var across, out var palm, out _)) return;
            HumanBodyBones first = right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
            float[][] deg = { thumb, index, middle, ring, little };
            var saved = new List<(Transform, Quaternion)>();
            for (int f = 0; f < 5; f++)
                for (int k = 0; k < 3; k++)
                {
                    var b = Anim.GetBoneTransform((HumanBodyBones)((int)first + f * 3 + k));
                    if (b == null) continue;
                    saved.Add((b, b.localRotation));
                    Vector3 dir = b.childCount > 0 ? (b.GetChild(0).position - b.position).normalized : along;
                    Vector3 axis = Vector3.Cross(dir, palm);                       // turns the bone's tip toward the palm
                    if (axis.sqrMagnitude < 1e-6f) continue;
                    b.rotation = Quaternion.AngleAxis(deg[f][k], axis.normalized) * b.rotation;
                }
            foreach (var (b, _) in saved) into.Add((b, b.localRotation));
            foreach (var (b, q) in saved) b.localRotation = q;
        }

        /// <summary>Each frame, after the pose: the racket hand turned and placed so the handle lies in its palm (the arm reaching
        /// there, its elbow toward the figure's), the forearm taking half the hand's roll, the fingers closed.</summary>
        void Grip()
        {
            var racket = Figure.Rig.Racket;
            var ta = Anim.GetBoneTransform(HumanBodyBones.RightUpperArm); var tb = Anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var tc = Anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (racket == null || ta == null || tb == null || tc == null) return;
            // where the sim has the racket (last frame it was drawn in the body's hand: back in the figure's first)
            if (Figure.Rig.RacketInHand) { racket.localPosition = Figure.Rig.GripLocalPos; racket.localRotation = Figure.Rig.GripLocalRot; }
            Quaternion handRot = racket.rotation * _gripRot;
            Vector3 simAt = racket.position;
            Vector3 onHandle = racket.position - racket.up * GripUp;
            Vector3 wrist = onHandle - handRot * _palmOffset;

            // out of the arm's reach: the chest leans toward it (up to MaxLean), as a player stretches for the ball
            float arm = Vector3.Distance(ta.position, tb.position) + Vector3.Distance(tb.position, tc.position);
            var chest = Anim.GetBoneTransform(HumanBodyBones.Chest) ?? Anim.GetBoneTransform(HumanBodyBones.Spine);
            float over = Vector3.Distance(ta.position, wrist) - arm * 0.97f;
            if (chest != null && over > 0f)
            {
                Vector3 from = ta.position - chest.position, to = wrist - chest.position;
                Quaternion lean = Quaternion.FromToRotation(from, to);
                lean.ToAngleAxis(out float ang, out Vector3 ax);
                if (ang > 180f) ang -= 360f;
                float want = Mathf.Clamp(ang, -MaxLean, MaxLean) * Mathf.Clamp01(over / 0.15f);
                chest.rotation = Quaternion.AngleAxis(want, ax) * chest.rotation;
            }
            // still out of reach: the body goes to it (the hips moved by the shortfall, as the figure's pelvis is), the feet kept
            // where they were planted (the knees bend for it)
            over = Vector3.Distance(ta.position, wrist) - arm * 0.98f;
            if (over > 0f)
            {
                Vector3 shift = Vector3.ClampMagnitude((wrist - ta.position).normalized * over, MaxBodyShift);
                var fl = Anim.GetBoneTransform(HumanBodyBones.LeftFoot); var fr = Anim.GetBoneTransform(HumanBodyBones.RightFoot);
                Vector3 pl = fl.position, pr = fr.position; Quaternion ql = fl.rotation, qr = fr.rotation;
                _bodyHips.position += shift;
                Vector3 ahead = Flat(Figure.DrawnForward) * 0.4f;
                var kl = Anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var kr = Anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                TwoBoneIK.Solve(Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), kl, fl, pl, kl.position + ahead);
                TwoBoneIK.Solve(Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), kr, fr, pr, kr.position + ahead);
                fl.rotation = ql; fr.rotation = qr;
                BodyShift = shift.magnitude;
            }
            else BodyShift = 0f;
            var mid = Figure.Rig.Bones["Forearm_R"]; var end = Figure.Rig.Bones["Hand_R"];
            TwoBoneIK.Solve(ta, tb, tc, wrist, mid.position + (mid.position - end.position) * 0.2f);
            // the forearm rolls with the hand (pronation / supination) - half of it, as the radius does - so the wrist doesn't wring
            Vector3 axis = (tc.position - tb.position).normalized;
            Quaternion rel = handRot * Quaternion.Inverse(tc.rotation);
            Vector3 p = axis * Vector3.Dot(new Vector3(rel.x, rel.y, rel.z), axis);
            var twist = new Quaternion(p.x, p.y, p.z, rel.w);
            float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m > 1e-5f)
            {
                twist = new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
                tb.rotation = Quaternion.Slerp(Quaternion.identity, twist, 0.5f) * tb.rotation;
            }
            tc.rotation = handRot;
            foreach (var (b, q) in _fist) b.localRotation = q;
            foreach (var (b, q) in _relaxed) b.localRotation = q;
            // the racket drawn in the body's hand, wherever the arm got to
            racket.rotation = handRot * Quaternion.Inverse(_gripRot);
            racket.position = tc.position + handRot * _palmOffset + racket.up * GripUp;
            GripGap = Vector3.Distance(racket.position, simAt);
        }

        // ---- the kit's bands on the body: the wristband on its racket wrist, the headband round its head ----
        Transform _wristband, _headband;
        Vector3 _wbPos, _hbPos; Quaternion _wbRot, _hbRot;

        void FindBands()
        {
            foreach (var t in Figure.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "wristband") { _wristband = t; _wbPos = t.localPosition; _wbRot = t.localRotation; }
                else if (t.name == "headband") { _headband = t; _hbPos = t.localPosition; _hbRot = t.localRotation; }
            }
        }

        /// <summary>Back on the figure's bones (where they ride when the classic figure is drawn).</summary>
        void BandsHome()
        {
            if (_wristband != null) { _wristband.localPosition = _wbPos; _wristband.localRotation = _wbRot; }
            if (_headband != null) { _headband.localPosition = _hbPos; _headband.localRotation = _hbRot; }
        }

        /// <summary>Each frame: each band turned from the figure's limb onto the body's (its ring round the forearm just above
        /// the wrist; round the head, above the brow).</summary>
        void Bands()
        {
            BandsHome();
            var rig = Figure.Rig.Bones;
            if (_wristband != null && _wristband.gameObject.activeInHierarchy)
            {
                var fa = Anim.GetBoneTransform(HumanBodyBones.RightLowerArm); var hd = Anim.GetBoneTransform(HumanBodyBones.RightHand);
                Vector3 figAxis = rig["Hand_R"].position - rig["Forearm_R"].position, bodyAxis = hd.position - fa.position;
                _wristband.rotation = Quaternion.FromToRotation(figAxis, bodyAxis) * _wristband.rotation;
                _wristband.position = hd.position - bodyAxis.normalized * 0.04f;
            }
            if (_headband != null && _headband.gameObject.activeInHierarchy)
            {
                var head = Anim.GetBoneTransform(HumanBodyBones.Head);
                Transform top = head.childCount > 0 ? head.GetChild(0) : null;
                foreach (Transform c in head) if (c.name.Contains("HeadTop")) top = c;
                if (top != null)
                {
                    Vector3 up = (top.position - head.position).normalized;
                    _headband.rotation = Quaternion.FromToRotation(Figure.Rig.Bones["Head"].rotation * Vector3.up, up) * _headband.rotation;
                    _headband.position = Vector3.Lerp(head.position, top.position, 0.55f) + head.rotation * Vector3.forward * 0.005f;
                }
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.zero; }

        // ---- 2026-11-01 the user's Mixamo clips: every direction, the goalkeeper dive ----
        AnimationLayerMixerPlayable _layers;
        AnimationMixerPlayable _base, _dir, _dive, _prep, _stroke;
        AnimationClipPlayable _back, _left, _right, _diveL, _diveR, _ready, _low;
        readonly AnimationClipPlayable[] _prepClips = new AnimationClipPlayable[MoveBook.All.Length], _strokeClips = new AnimationClipPlayable[MoveBook.All.Length];
        /// <summary>The dive's launch, landing and rise, in seconds (measured in Blender, 2026-11-01).</summary>
        public const float DiveLaunch = 1.0f, DiveLandL = 1.6f, DiveRiseL = 2.4f, DiveEndL = 3.33f, DiveLandR = 1.47f, DiveRiseR = 2.2f, DiveEndR = 3.2f;
        public const float StrafeSpeed = 4.0f, BackSpeed = 2.2f;

        /// <summary>Between shots: the junior's ready on the T, swaying between these two moments; the split step lands in the
        /// low stance's moment.</summary>
        public const float ReadyA = 4.10f, ReadyB = 4.45f, SplitLow = 0.6f;
        /// <summary>Our motion capture is in (else the figure's pose is the body's when standing, as before).</summary>
        public bool HasMoves { get; private set; }
        /// <summary>The dive and the swing shown now, 0..1 (tests).</summary>
        public float DiveShown { get; private set; }
        public float SwingShown { get; private set; }
        /// <summary>The move playing now and its clip time (tests).</summary>
        public string MoveShown { get; private set; } = "";
        public float MoveTime { get; private set; }
        float _diveW, _prepW, _strokeW, _diveClock, _readyClock, _recoverClock, _splitW;
        bool _diving, _diveRight, _locked;
        int _pick = MoveBook.FhDrive;
        /// <summary>The stroke's move is settled (it stays until the stroke is over) (tests).</summary>
        public bool MoveLocked { get { return _locked; } }

        AnimationClipPlayable Clip(Dictionary<string, AnimationClip> clips, string name, bool timed)
        {
            if (!clips.TryGetValue(name, out var c)) return default(AnimationClipPlayable);
            var p = AnimationClipPlayable.Create(_graph, c);
            if (timed) p.SetSpeed(0);                                  // the game sets its time
            return p;
        }

        static AvatarMask UpperBody()
        {
            var m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                m.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm ||
                    part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers ||
                    part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK);
            }
            return m;
        }

        void Connect(Playable to, int port, Playable from, float w)
        {
            if (!from.IsValid()) return;
            _graph.Connect(from, 0, to, port);
            to.SetInputWeight(port, w);
        }

        void BuildGraph()
        {
            var clips = PlayerModels.Clips();
            _graph = PlayableGraph.Create("AthleteBody_" + name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _loco = AnimationMixerPlayable.Create(_graph, LocoNames.Length);
            for (int i = 0; i < LocoNames.Length; i++)
            {
                clips.TryGetValue(LocoNames[i], out var c);
                if (c == null) continue;
                _locoClips[i] = AnimationClipPlayable.Create(_graph, c);
                _graph.Connect(_locoClips[i], 0, _loco, i);
                _loco.SetInputWeight(i, i == 0 ? 1f : 0f);
            }
            // movement: forward (walk / jog / sprint), back, to the left, to the right
            _dir = AnimationMixerPlayable.Create(_graph, 4);
            _graph.Connect(_loco, 0, _dir, 0); _dir.SetInputWeight(0, 1f);
            _back = Clip(clips, "Jog_Back_Loop", false); _left = Clip(clips, "Strafe_Left_Loop", false); _right = Clip(clips, "Strafe_Right_Loop", false);
            Connect(_dir, 1, _back, 0f); Connect(_dir, 2, _left, 0f); Connect(_dir, 3, _right, 0f);
            // the base: the ready on the T, the split step's low landing, moving
            _base = AnimationMixerPlayable.Create(_graph, 3);
            _ready = Clip(clips, "Mocap_EliteRally", true); _low = Clip(clips, "Mocap_LowStance", true);
            HasMoves = _ready.IsValid() && _low.IsValid();
            Connect(_base, 0, _ready, 1f); Connect(_base, 1, _low, 0f);
            _graph.Connect(_dir, 0, _base, 2); _base.SetInputWeight(2, HasMoves ? 0f : 1f);
            // the strokes (MoveBook): getting ready is the upper body's, over the feet still moving; committed, it's the whole
            // body's, the same every time
            _prep = AnimationMixerPlayable.Create(_graph, MoveBook.All.Length);
            _stroke = AnimationMixerPlayable.Create(_graph, MoveBook.All.Length);
            for (int i = 0; i < MoveBook.All.Length; i++)
            {
                _prepClips[i] = Clip(clips, MoveBook.All[i].Clip, true); _strokeClips[i] = Clip(clips, MoveBook.All[i].Clip, true);
                Connect(_prep, i, _prepClips[i], 0f); Connect(_stroke, i, _strokeClips[i], 0f);
            }
            // the dive, left or right (over everything)
            _dive = AnimationMixerPlayable.Create(_graph, 2);
            _diveL = Clip(clips, "Dive_Left", true); _diveR = Clip(clips, "Dive_Right", true);
            Connect(_dive, 0, _diveL, 1f); Connect(_dive, 1, _diveR, 0f);

            _layers = AnimationLayerMixerPlayable.Create(_graph, 4);
            _graph.Connect(_base, 0, _layers, 0); _layers.SetInputWeight(0, 1f);
            _graph.Connect(_prep, 0, _layers, 1); _layers.SetInputWeight(1, 0f);
            _layers.SetLayerMaskFromAvatarMask(1, UpperBody());
            _graph.Connect(_stroke, 0, _layers, 2); _layers.SetInputWeight(2, 0f);
            _graph.Connect(_dive, 0, _layers, 3); _layers.SetInputWeight(3, 0f);
            var output = AnimationPlayableOutput.Create(_graph, "body", Anim);
            output.SetSourcePlayable(_layers);
            _graph.Play();
        }

        /// <summary>The dive (whole body) and the swing (upper body) from the sim's state, their clips' time set by the game:
        /// the dive from its launch, landing and rise; the swing so its contact lands on the sim's (SwingT 0.5).</summary>
        void Actions(AthleteSnapshot v, float dt)
        {
            // ---- the dive ----
            bool diving = v.Diving || v.GetUp > 0f;
            if (diving && !_diving)
            {
                _diveClock = 0f;
                Vector3 right = Vector3.Cross(Vector3.up, Flat(Figure.DrawnForward));
                _diveRight = Vector3.Dot(_vel, right) > 0f || (_vel.sqrMagnitude < 0.01f && v.SwingSide > 0);
            }
            _diving = diving;
            _diveW = Mathf.MoveTowards(_diveW, diving ? 1f : 0f, dt * (diving ? 8f : 3f));
            if (_diveW > 0.001f)
            {
                _diveClock += dt;
                float land = _diveRight ? DiveLandR : DiveLandL, rise = _diveRight ? DiveRiseR : DiveRiseL, end = _diveRight ? DiveEndR : DiveEndL;
                float t = v.Diving ? Mathf.Min(DiveLaunch + _diveClock, rise) : rise + (1f - Mathf.Clamp01(v.GetUp)) * (end - rise);
                if (!diving) t = end;
                var clip = _diveRight ? _diveR : _diveL;
                if (clip.IsValid()) clip.SetTime(t);
                _dive.SetInputWeight(0, _diveRight ? 0f : 1f); _dive.SetInputWeight(1, _diveRight ? 1f : 0f);
            }
            _layers.SetInputWeight(3, _diveW);
            DiveShown = _diveW;

            // ---- the base: the ready (a slow sway through the junior's moment on the T), the split step's low landing ----
            _readyClock += dt;
            if (_ready.IsValid()) _ready.SetTime(ReadyA + (ReadyB - ReadyA) * 0.5f * (1f - Mathf.Cos(_readyClock * Mathf.PI * 2f / 1.8f)));
            if (_low.IsValid()) _low.SetTime(SplitLow);
            float split = Figure.SplitT >= 0f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(Figure.SplitT)) * 0.7f : 0f;
            _splitW = Mathf.MoveTowards(_splitW, split, dt * 6f);
            if (HasMoves)
            {
                float moving = _mocapW, low = Mathf.Min(_splitW, 1f - moving);
                _base.SetInputWeight(0, 1f - moving - low); _base.SetInputWeight(1, low); _base.SetInputWeight(2, moving);
            }

            // ---- the stroke: one situation, one move (MoveBook), its ball on the sim's (SwingT 0.5). The move is read while the
            // stroke is being set up and locked as it commits: it never changes half way through ----
            bool committed = v.Swing == SwingPhase.Scheduled || v.Swing == SwingPhase.Swinging || v.Swing == SwingPhase.Recovering;
            if (v.Swing == SwingPhase.Idle) _locked = false;
            else if (!_locked) _pick = MoveBook.Pick(v.SwingSide > 0, v.Reach, v.HasPred ? v.PredContact.y : v.RacketHead.y, v.Touch, Figure.Runner != null && Figure.Runner.IsServing(Figure.Index));
            if (committed) _locked = true;
            var move = MoveBook.All[_pick];
            bool going = v.Swing != SwingPhase.Idle && v.Swing != SwingPhase.Recovering;
            _recoverClock = going ? 0f : _recoverClock + dt;
            // the follow-through ends; away early if the athlete is already moving off
            bool done = v.Swing == SwingPhase.Recovering && (_vel.magnitude > 1.2f || _recoverClock >= move.Recover - move.End);
            float wantStroke = HasMoves && !diving && committed && !done ? 1f : 0f;
            float wantPrep = HasMoves && !diving && !committed && v.Swing != SwingPhase.Idle ? 1f : 0f;
            _strokeW = Mathf.MoveTowards(_strokeW, wantStroke, dt * (wantStroke > _strokeW ? 9f : 3f));
            _prepW = Mathf.MoveTowards(_prepW, wantPrep, dt * (wantPrep > _prepW ? 7f : 5f));
            float swingT = Mathf.Clamp01(v.SwingT), at = move.At(v.Swing, swingT, _recoverClock);
            for (int i = 0; i < MoveBook.All.Length; i++)
            {
                if (!_strokeClips[i].IsValid()) continue;
                _prep.SetInputWeight(i, i == _pick ? 1f : 0f); _stroke.SetInputWeight(i, i == _pick ? 1f : 0f);
            }
            if (_prepClips[_pick].IsValid()) _prepClips[_pick].SetTime(at);
            if (_strokeClips[_pick].IsValid()) _strokeClips[_pick].SetTime(at);
            _layers.SetInputWeight(1, _prepW);
            _layers.SetInputWeight(2, _strokeW);
            SwingShown = Mathf.Max(_prepW, _strokeW);
            MoveShown = SwingShown < 0.01f ? "" : move.Name;
            MoveTime = SwingShown < 0.01f ? 0f : at;
        }

        void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); if (Figure != null) Figure.Dressed -= OnDressed; }

        /// <summary>The kit's colours on the body; the other build's body when the kit's build changes.</summary>
        void OnDressed(Progression.Kit k)
        {
            string want = PlayerModels.BodyFor(IsYou(Figure), k.Female, PlayerModels.ToneOf(k.Skin));
            if (want != ModelId && Resources.Load<TextAsset>(PlayerModels.Folder + want) != null)
            {
                if (_graph.IsValid()) _graph.Destroy();
                Destroy(_model); _model = null; Parts.Clear(); _pairs.Clear(); _keys.Clear(); _fist.Clear(); _relaxed.Clear(); _grip = false;
                if (!Build(Figure, want, _lit)) { enabled = false; return; }
            }
            PlayerModels.Dress(Parts, k.ShirtBody, k.Shorts, k.Socks, k.Shoes, k.Hair, k.HairStyle, k.Skin);
        }

        // ---------------- each frame ----------------

        /// <summary>Drawn now (Settings > Video > Players: Real); Classic shows the code-built figure again.</summary>
        public bool Showing { get; private set; } = true;
        static bool Wanted { get { return Profile.ProfileStore.Current == null || !Profile.ProfileStore.Current.Settings.ClassicPlayers; } }

        /// <summary>Show the real body or the classic figure (the figure's renderers on/off, the body's off/on).</summary>
        void Show(bool real)
        {
            Showing = real;
            if (_model != null) _model.SetActive(real);
            foreach (var r in Figure.GetComponentsInChildren<Renderer>(true))
            {
                if (_model != null && r.transform.IsChildOf(_model.transform)) continue;
                if (Figure.Rig.Racket != null && r.transform.IsChildOf(Figure.Rig.Racket)) continue;
                if (r.name == "ContactPatch" || r.name == "headband" || r.name == "wristband") continue;      // worn over the body too
                r.forceRenderingOff = real;
            }
            if (_graph.IsValid()) { if (real) _graph.Play(); else _graph.Stop(); }
            if (!real) BandsHome();
            if (!real && Figure.Rig.RacketInHand && Figure.Rig.Racket != null) { Figure.Rig.Racket.localPosition = Figure.Rig.GripLocalPos; Figure.Rig.Racket.localRotation = Figure.Rig.GripLocalRot; }
        }

        void LateUpdate()
        {
            if (Figure == null || Anim == null || Figure.Rig == null) return;
            if (Wanted != Showing) Show(Wanted);
            if (!Showing) return;
            var v = Figure.Posed;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _vel = Vector3.Lerp(_vel, new Vector3(v.Velocity.x, 0, v.Velocity.z), 1f - Mathf.Exp(-dt * 10f));
            Locomotion(v, dt);
            Actions(v, dt);

            // what the motion capture drew this frame (the Animator ran before LateUpdate)
            _anim.Clear();
            var root = Figure.transform;
            Quaternion rootInv = Quaternion.Inverse(root.rotation);
            foreach (var p in _pairs) _anim[Key(p)] = rootInv * p.Body.rotation;
            Vector3 animHips = root.InverseTransformPoint(_bodyHips.position);

            // the capture's hips face where the figure's do: the real player turned all over the court, the game turns the
            // athlete itself (their twist above the hips, and every limb, stays the capture's) - not in the dive, which rolls
            if (HasMoves)
            {
                Vector3 animRight = root.InverseTransformDirection(Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position);
                Vector3 figRight = root.InverseTransformDirection(Figure.Rig.Bones["Thigh_R"].position - Figure.Rig.Bones["Thigh_L"].position);
                animRight.y = 0; figRight.y = 0;
                float yaw = Vector3.SignedAngle(animRight, figRight, Vector3.up) * (1f - _diveW);
                if (animRight.sqrMagnitude > 1e-6f && figRight.sqrMagnitude > 1e-6f && Mathf.Abs(yaw) > 0.01f)
                {
                    Quaternion fix = Quaternion.AngleAxis(yaw, Vector3.up);
                    foreach (var p in _pairs) { var k = Key(p); _anim[k] = fix * _anim[k]; }
                }
            }

            // 1 + 2: the figure's pose on the body, the motion capture blended in by part. With our squash capture the body
            // is the capture's throughout (the figure only steers it: its racket hand, its facing, its hips over the floor);
            // without, the legs are the capture's moving or diving, the arms the dive's or swinging free when running
            float wLegs = HasMoves ? 1f : Mathf.Max(_mocapW, _diveW);
            float wArms = HasMoves ? 1f : Mathf.Max(_diveW, _mocapW * 0.85f);
            float wSpine = HasMoves ? 1f : Mathf.Max(_diveW, _mocapW * 0.6f);
            foreach (var p in _pairs)
            {
                Quaternion fig = rootInv * p.Rig.rotation;
                Quaternion turned = fig * Quaternion.Inverse(p.RigRest) * p.BodyRestAdj;
                float w = p.Legs ? wLegs : p.Arm ? wArms : p.Spine ? wSpine : HasMoves ? 1f : Mathf.Max(_diveW, _mocapW * 0.3f);
                Quaternion q = w > 0.001f ? Quaternion.Slerp(turned, _anim[Key(p)], w) : turned;
                p.Body.rotation = root.rotation * q;
            }
            Vector3 figHips = root.InverseTransformPoint(_rigPelvis.position) + _hipsOffset;
            _bodyHips.position = root.TransformPoint(Vector3.Lerp(figHips, new Vector3(figHips.x, Mathf.Lerp(figHips.y, animHips.y, wLegs), figHips.z), 1f));

            // 3: IK - the racket in the body's hand (the squash grip; before it, the hand where the figure's is), the free hand, the feet
            if (_grip) Grip();
            else Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "Hand_R", "Forearm_R", 1f);
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "Hand_L", "Forearm_L", 1f - wArms);
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "L" : "R";
                Reach(s == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, s == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                      s == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, "Foot_" + side, "Shin_" + side, 1f - wLegs);
            }
            Bands();
        }

        HumanBodyBones Key(Pair p) { return _keys.TryGetValue(p.Body, out var k) ? k : Cache(p); }
        readonly Dictionary<Transform, HumanBodyBones> _keys = new Dictionary<Transform, HumanBodyBones>();
        HumanBodyBones Cache(Pair p)
        {
            foreach (var (rb, hb, _) in Pairs) if (Anim.GetBoneTransform(hb) == p.Body) { _keys[p.Body] = hb; return hb; }
            return HumanBodyBones.LastBone;
        }

        static bool InStroke(AthleteSnapshot v) { return v.Swing != SwingPhase.Idle; }

        /// <summary>Moving: the motion capture's share, its directions' blend (forward, back, left, right) and pace. Strokes'
        /// lunges, the split step and a near-standstill stay the figure's.</summary>
        void Locomotion(AthleteSnapshot v, float dt)
        {
            float speed = _vel.magnitude;
            Vector3 fwd = Flat(Figure.DrawnForward);
            if (fwd == Vector3.zero) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float f = Vector3.Dot(_vel, fwd), r = Vector3.Dot(_vel, right);
            // (with our capture the feet keep moving into a shot: the ready and the lunge take over as the athlete stops)
            bool busy = !HasMoves && (InStroke(v) && v.Swing >= SwingPhase.Scheduled || Figure.SplitT >= 0f);
            float want = busy ? 0f : Mathf.Clamp01((speed - 0.6f) / 1.0f);
            _mocapW = Mathf.MoveTowards(_mocapW, want, dt * (want > _mocapW ? 4f : 6f));
            MocapWeight = _mocapW;

            // which way: forward, back, left, right (by how much of the speed goes each way)
            float wf = Mathf.Max(f, 0f), wb = Mathf.Max(-f, 0f), wr = Mathf.Max(r, 0f), wl = Mathf.Max(-r, 0f), sum = wf + wb + wr + wl;
            if (sum < 1e-3f) { wf = 1f; sum = 1f; }
            _dir.SetInputWeight(0, wf / sum);
            if (_back.IsValid()) { _dir.SetInputWeight(1, wb / sum); _back.SetSpeed(Mathf.Clamp(speed / BackSpeed, 0.5f, 1.5f)); }
            if (_left.IsValid()) { _dir.SetInputWeight(2, wl / sum); _left.SetSpeed(Mathf.Clamp(speed / StrafeSpeed, 0.5f, 1.5f)); }
            if (_right.IsValid()) { _dir.SetInputWeight(3, wr / sum); _right.SetSpeed(Mathf.Clamp(speed / StrafeSpeed, 0.5f, 1.5f)); }

            // forward: walk, jog, sprint by speed, each played at the pace that matches it
            float[] w = new float[4];
            if (speed <= LocoSpeeds[1]) { float t = speed / LocoSpeeds[1]; w[0] = 1 - t; w[1] = t; }
            else if (speed <= LocoSpeeds[2]) { float t = (speed - LocoSpeeds[1]) / (LocoSpeeds[2] - LocoSpeeds[1]); w[1] = 1 - t; w[2] = t; }
            else { float t = Mathf.Clamp01((speed - LocoSpeeds[2]) / (LocoSpeeds[3] - LocoSpeeds[2])); w[2] = 1 - t; w[3] = t; }
            for (int i = 0; i < 4; i++)
            {
                if (!_locoClips[i].IsValid()) continue;
                _loco.SetInputWeight(i, w[i]);
                float rate = i == 0 ? 1f : Mathf.Clamp(speed / LocoSpeeds[i], 0.6f, 1.6f);
                _locoClips[i].SetSpeed(rate);
            }
        }

        /// <summary>Two-bone IK of a body limb onto the figure's end bone (bending toward the figure's middle joint).</summary>
        void Reach(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, string rigEnd, string rigMid, float weight)
        {
            if (weight < 0.01f) return;
            var ta = Anim.GetBoneTransform(a); var tb = Anim.GetBoneTransform(b); var tc = Anim.GetBoneTransform(c);
            if (ta == null || tb == null || tc == null) return;
            var end = Figure.Rig.Bones[rigEnd]; var mid = Figure.Rig.Bones[rigMid];
            Vector3 target = Vector3.Lerp(tc.position, end.position, weight);
            Quaternion endRot = tc.rotation;
            TwoBoneIK.Solve(ta, tb, tc, target, mid.position + (mid.position - end.position) * 0.2f);
            tc.rotation = endRot;                                    // the hand / foot keeps the pose's turn
        }
    }
}
