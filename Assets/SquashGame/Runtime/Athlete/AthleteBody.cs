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

            _footRest = root.InverseTransformPoint(Anim.GetBoneTransform(HumanBodyBones.LeftFoot).position).y;
            _toeRest = root.InverseTransformPoint(Anim.GetBoneTransform(HumanBodyBones.LeftToes).position).y;
            _toesRest = Flat(root.InverseTransformDirection(Anim.GetBoneTransform(HumanBodyBones.LeftToes).position - Anim.GetBoneTransform(HumanBodyBones.LeftFoot).position));
            _footRotL = Quaternion.Inverse(root.rotation) * Anim.GetBoneTransform(HumanBodyBones.LeftFoot).rotation;
            _footRotR = Quaternion.Inverse(root.rotation) * Anim.GetBoneTransform(HumanBodyBones.RightFoot).rotation;
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
        public static bool DebugGrip;
        float _dbgWant, _dbgFull;
        /// <summary>How far the handle runs along the fingers against across them: the shaft's angle off the forearm, with the wrist straight.</summary>
        public const float GripAlong = 1.5f;
        /// <summary>Each finger joint's turn in its parent, closed round the handle (racket hand) or loosely curled (free hand).</summary>
        readonly List<(Transform bone, Quaternion local)> _fist = new List<(Transform, Quaternion)>(), _relaxed = new List<(Transform, Quaternion)>();
        /// <summary>How far the racket is drawn from where the sim has it (the body's arm couldn't reach it), metres (tests).</summary>
        public float GripGap { get; private set; }
        /// <summary>How far the chest leant toward an out-of-reach racket, degrees.</summary>
        public const float MaxLean = 15f;
        /// <summary>How far a stroke's chest may lean toward the ball and sideways, and turn from the hips, degrees.</summary>
        public const float MaxForwardLean = 38f, MaxSideLean = 10f, MaxWaistTurn = 25f;
        /// <summary>The hips never go lower than this, metres (a deep lunge, not the splits).</summary>
        public const float LowestHips = 0.62f;
        /// <summary>The ball this near its predicted contact (metres), the athlete sets for it.</summary>
        public const float BallNear = 3.0f;
        /// <summary>Past this SwingT the ball has gone (0.5) and the arm may finish on its own.</summary>
        public const float FollowFrom = 0.56f;
        /// <summary>How straight the arm is at the ball (of its full length), and how far the body may back off for it, metres.</summary>
        public const float ContactArm = 0.93f, MaxBackOff = 1.15f;
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
            Vector3 handle = (across + GripAlong * along).normalized;
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
            float arm = Vector3.Distance(ta.position, tb.position) + Vector3.Distance(tb.position, tc.position);
            // 2026-11-07 the wrist in line with the forearm (the user: the hand was bending inward). The sim fixes the sweet spot
            // and which way the strings face; the racket can still turn about the sweet spot in the plane of the strings
            float inLine = Mathf.Max(Mathf.Max(_strokeW, _prepW), _stanceW) * (1f - _followW);
            if (inLine <= 0.001f) _faceChosen = false;
            if (inLine > 0.001f)
            {
                Vector3 sweet = racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0));
                // 2026-11-08 the strings have two faces: the palm meets the ball on a forehand, the back of the hand on a backhand
                // (the user's PSA photos - the racket carries on from the forearm wherever the arm reaches, not held head-up).
                // Settled once a stroke; rolling about the shaft leaves the strings where they are
                if (!_faceChosen)
                {
                    Vector3 hit = Figure.Posed.RacketFaceNormal;
                    bool fore = _pick % 2 == 0;
                    _faceFlip = hit.sqrMagnitude > 1e-6f && (Vector3.Dot(racket.right, hit) > 0f) != fore;
                    _faceChosen = true;
                }
                if (_faceFlip) racket.rotation = Quaternion.AngleAxis(180f, racket.up) * racket.rotation;
                // 2026-11-08 the arm, wrist straight, from the shoulder to the sweet spot in the plane of the strings: upper arm,
                // then the forearm and the racket as one stiff piece (the shaft at the grip's angle to the forearm). Where those two
                // meet is the elbow - two circles crossing. A close ball folds the elbow, not the wrist (the user chose this)
                Vector3 n = racket.right, shaft = -racket.up;
                float l1 = Vector3.Distance(ta.position, tb.position), l2 = Vector3.Distance(tb.position, tc.position) + 0.055f;
                float L = GateA.SweetSpotFromButt - GripUp, gam = Mathf.Atan2(1f, GripAlong);
                float R = Mathf.Sqrt(l2 * l2 + L * L + 2f * l2 * L * Mathf.Cos(gam)), del = Mathf.Atan2(L * Mathf.Sin(gam), l2 + L * Mathf.Cos(gam));
                Vector3 e = Vector3.ProjectOnPlane(ta.position - sweet, n);
                float d = e.magnitude;
                _elbowSet = d > 1e-3f;
                if (_elbowSet)
                {
                    e /= d;
                    float dc = Mathf.Clamp(d, Mathf.Abs(R - l1) + 1e-3f, R + l1 - 1e-3f);
                    float x = (R * R - l1 * l1 + dc * dc) / (2f * dc), h = Mathf.Sqrt(Mathf.Max(0f, R * R - x * x));
                    Vector3 across = Vector3.Cross(n, e);
                    Vector3 e1 = sweet + e * x + across * h, e2 = sweet + e * x - across * h;
                    _elbowAim = e1.y < e2.y ? e1 : e2;                  // the elbow below the line, as players carry it
                    Vector3 f = Quaternion.AngleAxis(-del * Mathf.Rad2Deg, n) * (sweet - _elbowAim).normalized;
                    shaft = Quaternion.AngleAxis(gam * Mathf.Rad2Deg, n) * f;
                }
                Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(-racket.up, shaft, n) * inLine, n);
                racket.rotation = turn * racket.rotation;
                racket.position = sweet + turn * (racket.position - sweet);
            }
            Vector3 simAt = racket.position;
            // 2026-11-07 the ball gone, the arm finishes straight out through the swing (the user: it was folding back toward
            // the body, following the figure's racket): in front on the forehand, out to the right on the backhand, the racket
            // head up above the line of the arm
            // 2026-11-07 and straight at the ball: the body backs off a racket it's standing too close to, so the arm is out at
            // contact, not cramped (smoothed: the body doesn't jump)
            Vector3 back = Vector3.zero;
            float setW = Mathf.Max(_strokeW, _stanceW);
            if (setW > 0.01f && _followW < 0.5f && _planted)
            {
                // shoulder to sweet spot with the arm straight and the wrist in line: the arm, then the racket at the grip's angle
                Vector3 sw = racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0));
                float l = GateA.SweetSpotFromButt - GripUp, cos = GripAlong / Mathf.Sqrt(GripAlong * GripAlong + 1f), rise = sw.y - ta.position.y;
                float full = Mathf.Sqrt(arm * arm * ContactArm * ContactArm + l * l + 2f * arm * ContactArm * l * cos);
                // 2026-11-08 and side-on to it (the user's PSA photos): the shoulder in the plane the strings swing through, so the
                // arm and the racket are one line, not the arm across the strings with the racket hanging off it
                Vector3 s0 = ta.position - _backOff;                 // (where the shoulder would be without it)
                Vector3 nf = Flat(racket.right);
                Vector3 s1 = nf == Vector3.zero ? s0 : s0 - nf * Vector3.Dot(s0 - sw, nf);
                Vector3 away = s1 - sw; away.y = 0;
                if (away.sqrMagnitude < 1e-4f) away = -_strideDir;
                Vector3 s2 = sw + away.normalized * Mathf.Sqrt(Mathf.Max(0f, full * full - rise * rise));
                Vector3 move = s2 - s0; move.y = 0;
                back = Vector3.ClampMagnitude(move, MaxBackOff) * setW;
                _dbgWant = move.magnitude; _dbgFull = full;
            }
            _backOff = Vector3.Lerp(_backOff, back, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 60f));
            Quaternion held = racket.rotation;
            Vector3 straight = Vector3.zero;
            if (_followW > 0.001f)
            {
                Vector3 side = Flat(ta.position - Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
                Vector3 ahead = Vector3.Cross(side, Vector3.up);
                bool fore = _pick % 2 == 0;
                Vector3 dir = (ahead + side * (fore ? -0.15f : 0.7f) + Vector3.up * (fore ? 0.25f : 0.3f)).normalized;
                // the racket head up over the arm, the wrist straight (the shaft at the grip's own angle to the arm)
                Vector3 above = Vector3.ProjectOnPlane(Vector3.up, dir).normalized;
                Vector3 shaft = (GripAlong * dir + above).normalized;
                Quaternion finish = Quaternion.LookRotation(shaft, Vector3.Cross(dir, above)) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.down, Vector3.right));
                held = Quaternion.Slerp(held, finish, _followW);
                straight = ta.position + dir * arm * 0.999f;
            }
            Quaternion handRot = held * _gripRot;
            Vector3 onHandle = racket.position - held * Vector3.up * GripUp;
            Vector3 wrist = onHandle - handRot * _palmOffset;
            if (_followW > 0.001f) wrist = Vector3.Lerp(wrist, straight, _followW);

            // out of the arm's reach: the chest leans toward it (up to MaxLean), as a player stretches for the ball
            var chest = Anim.GetBoneTransform(HumanBodyBones.Chest) ?? Anim.GetBoneTransform(HumanBodyBones.Spine);
            float over = Vector3.Distance(ta.position, wrist) - arm * 0.97f;
            if (chest != null && over > 0f && _followW < 0.001f)
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
            BodyShift = 0f;
            for (int i = 0; i < 4 && _followW < 0.001f; i++)          // (the legs may drop the body again: until the arm gets there)
            {
                over = Vector3.Distance(ta.position, wrist) - arm * 0.98f;
                if (over <= 0.002f) break;
                Vector3 shift = Vector3.ClampMagnitude((wrist - ta.position).normalized * over, MaxBodyShift);
                MoveHips(shift, false);                                // (the racket comes first: a foot drags sooner than the arm falls short)
                BodyShift += shift.magnitude;
            }
            var mid = Figure.Rig.Bones["Forearm_R"]; var end = Figure.Rig.Bones["Hand_R"];
            Vector3 pole = mid.position + (mid.position - end.position) * 0.2f;
            if (_elbowSet && inLine > 0.001f && _followW < 0.001f) pole = Vector3.Lerp(pole, _elbowAim, inLine);
            TwoBoneIK.Solve(ta, tb, tc, wrist, pole);
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
            GripGap = _followW > 0.001f ? 0f : Vector3.Distance(racket.position, simAt);
            if (DebugGrip && Figure.RacketLock >= 1f)
            {
                Vector3 fa = (tc.position - tb.position).normalized, pm = (Anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position - tc.position).normalized;
                float bend = Vector3.Angle(fa, pm);
                if (bend > 50f)
                    Debug.Log("[GripDbg] " + MoveShown + " bend " + bend.ToString("0") + " inLine " + inLine.ToString("0.00") + " stance " + _stanceW.ToString("0.00") + " stroke " + _strokeW.ToString("0.00") +
                              " | shaft " + (-racket.up).ToString("0.00") + " n " + racket.right.ToString("0.00") + " forearm " + fa.ToString("0.00") + " upper " + (tb.position - ta.position).normalized.ToString("0.00") +
                              " | shoulder->sweet " + (racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0)) - ta.position).ToString("0.00") + " reach " + Vector3.Distance(ta.position, tc.position).ToString("0.00") + "/" + arm.ToString("0.00") + " back " + _backOff.magnitude.ToString("0.00") + " want " + _dbgWant.ToString("0.00") + " shift " + BodyShift.ToString("0.00") + " full " + _dbgFull.ToString("0.00"));
            }
            FootGap = Lowest();
        }

        // ---- 2026-11-07 the feet on the floor (the user: a player was floating) ----
        float _footRest, _toeRest;
        /// <summary>The lower foot's height off the floor now, metres (tests).</summary>
        public float FootGap { get; private set; }

        float Lowest()
        {
            var root = Figure.transform; float low = float.MaxValue;
            foreach (var (foot, toe) in new[] { (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes) })
            {
                low = Mathf.Min(low, root.InverseTransformPoint(Anim.GetBoneTransform(foot).position).y - _footRest);
                low = Mathf.Min(low, root.InverseTransformPoint(Anim.GetBoneTransform(toe).position).y - _toeRest);
            }
            return low;
        }

        /// <summary>The body lowered (or raised) so its lower foot is on the floor: a capture's hop or drift doesn't lift it.
        /// Not in the dive, nor running, whose stride has its own flight.</summary>
        void Ground()
        {
            if (!HasMoves) return;
            float w = (1f - _diveW) * Mathf.Max(Mathf.Max(_stanceW, Mathf.Clamp01(_strokeW * 10f)), 1f - _mocapW);
            _bodyHips.position -= Figure.transform.up * (Lowest() * w);
            Stance();
        }

        /// <summary>2026-11-07 the stroke's stance (the user's PSA photos): set as the stroke commits, along the line to the ball, and
        /// held: the front foot out and pointing there, the back leg long, its heel up on the toes, the hips as low as that needs.</summary>
        Vector3 _plantFront, _plantBack, _strideDir;
        float _stanceW;
        Vector3 _elbowAim; bool _elbowSet;
        bool _faceChosen, _faceFlip;
        bool _planted, _rightFront;
        Vector3 _toesRest;                                      // the foot's toes direction at rest, root-local
        Quaternion _footRotL, _footRotR;                        // the feet's turn at rest, root-local

        void PlantStance(AthleteSnapshot v)
        {
            var root = Figure.transform;
            Vector3 at = v.HasPred ? v.PredContact : v.RacketHead;
            _strideDir = Flat(at - root.position);
            if (_strideDir == Vector3.zero) _strideDir = Flat(Figure.DrawnForward);
            Vector3 side = Vector3.Cross(Vector3.up, _strideDir);
            float len = MoveBook.Stride(_pick);
            _rightFront = MoveBook.RightFootFront(_pick);
            float s = _rightFront ? 1f : -1f;
            Vector3 floor = Vector3.up * _footRest;              // (held from the athlete: the sim may still be sliding them)
            _plantFront = floor + _strideDir * (len * 0.45f) + side * (0.1f * s);
            _plantBack = floor - _strideDir * (len * 0.55f) - side * (0.12f * s) + Vector3.up * 0.06f;
            _planted = true;
        }

        void Stance()
        {
            _bodyHips.position += _backOff;                     // (the body stands off the ball first: the legs then fit where it is)
            if (!_planted || _stanceW < 0.01f) return;
            float k = _stanceW * (1f - _diveW);
            var root = Figure.transform;
            var uf = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg);
            var kf = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg);
            var ff = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
            var ub = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var kb = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var fb = Anim.GetBoneTransform(_rightFront ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            Vector3 from = root.position + new Vector3(_backOff.x, 0f, _backOff.z);      // (the stance goes with the body as it stands off the ball)
            Vector3 front = Vector3.Lerp(ff.position, from + _plantFront, k), back = Vector3.Lerp(fb.position, from + _plantBack, k);
            float lenF = Vector3.Distance(uf.position, kf.position) + Vector3.Distance(kf.position, ff.position);
            float lenB = Vector3.Distance(ub.position, kb.position) + Vector3.Distance(kb.position, fb.position);
            for (int i = 0; i < 8; i++)
            {
                float excess = Mathf.Max(Vector3.Distance(uf.position, front) - lenF * 0.995f, Vector3.Distance(ub.position, back) - lenB * 0.995f);
                float room = root.InverseTransformPoint(_bodyHips.position).y - LowestHips;
                if (excess <= 0.001f || room <= 0f) break;
                _bodyHips.position -= root.up * Mathf.Min(excess, room);
            }
            TwoBoneIK.Solve(uf, kf, ff, Within(uf.position, front, lenF), kf.position + _strideDir * 0.5f);
            TwoBoneIK.Solve(ub, kb, fb, Within(ub.position, back, lenB), kb.position - _strideDir * 0.1f + Vector3.down * 0.4f);
            Quaternion bindF = root.rotation * (_rightFront ? _footRotR : _footRotL), bindB = root.rotation * (_rightFront ? _footRotL : _footRotR);
            Vector3 toes = root.TransformDirection(_toesRest);
            Vector3 backToes = (Quaternion.AngleAxis(_rightFront ? -40f : 40f, Vector3.up) * _strideDir).normalized;
            Quaternion aimF = Quaternion.FromToRotation(toes, _strideDir) * bindF;
            Quaternion aimB = Quaternion.AngleAxis(35f, Vector3.Cross(Vector3.up, backToes)) * Quaternion.FromToRotation(toes, backToes) * bindB;
            ff.rotation = Quaternion.Slerp(ff.rotation, aimF, k);
            fb.rotation = Quaternion.Slerp(fb.rotation, aimB, k);
        }

        /// <summary>2026-11-07 a stroke's body held up over its feet (the user: picture 23 looked like falling, 15 twisted too much at
        /// the waist). The chest may lean toward the ball only so far, hardly at all sideways, and turn only so far from the hips.</summary>
        void Posture()
        {
            if (!_planted || _stanceW < 0.01f) return;
            var spine = Anim.GetBoneTransform(HumanBodyBones.Spine); var neck = Anim.GetBoneTransform(HumanBodyBones.Neck);
            if (spine == null || neck == null) return;
            float k = _stanceW * (1f - _diveW);
            Vector3 side = Vector3.Cross(Vector3.up, _strideDir);
            Vector3 torso = (neck.position - spine.position).normalized;
            float along = Mathf.Atan2(Vector3.Dot(torso, _strideDir), torso.y) * Mathf.Rad2Deg, across = Mathf.Atan2(Vector3.Dot(torso, side), torso.y) * Mathf.Rad2Deg;
            float a = Mathf.Clamp(along, -5f, MaxForwardLean) * Mathf.Deg2Rad, c = Mathf.Clamp(across, -MaxSideLean, MaxSideLean) * Mathf.Deg2Rad;
            Vector3 upright = (Vector3.up + _strideDir * Mathf.Tan(a) + side * Mathf.Tan(c)).normalized;
            spine.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(torso, upright), k) * spine.rotation;

            Vector3 hips = Flat(Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position);
            Vector3 shoulders = Flat(Anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position - Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
            float turn = Vector3.SignedAngle(hips, shoulders, Vector3.up), over = turn - Mathf.Clamp(turn, -MaxWaistTurn, MaxWaistTurn);
            if (Mathf.Abs(over) < 0.01f) return;
            var bones = new[] { spine, Anim.GetBoneTransform(HumanBodyBones.Chest), Anim.GetBoneTransform(HumanBodyBones.UpperChest) };
            int count = 0; foreach (var b in bones) if (b != null) count++;
            foreach (var b in bones) if (b != null) b.rotation = Quaternion.AngleAxis(-over * k / count, Vector3.up) * b.rotation;
        }

        /// <summary>The free arm out behind for balance, the look in every lunge photo.</summary>
        void FreeArm()
        {
            if (!_planted || _stanceW < 0.01f) return;
            var ua = Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); var la = Anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); var hd = Anim.GetBoneTransform(HumanBodyBones.LeftHand);
            float len = Vector3.Distance(ua.position, la.position) + Vector3.Distance(la.position, hd.position);
            Vector3 side = Vector3.Cross(Vector3.up, _strideDir);
            Vector3 dir = (-_strideDir - side * 0.55f + Vector3.down * 0.25f).normalized;
            Vector3 target = Vector3.Lerp(hd.position, ua.position + dir * len * 0.96f, _stanceW * (1f - _diveW));
            Quaternion q = hd.rotation;
            TwoBoneIK.Solve(ua, la, hd, target, la.position + Vector3.down * 0.3f);
            hd.rotation = q;
        }

        /// <summary>The hips moved, the feet staying planted where they are: if the legs can't reach them from there the hips
        /// drop until they can (a lunge going lower, not feet leaving the floor).</summary>
        void MoveHips(Vector3 shift, bool drop = true)
        {
            var fl = Anim.GetBoneTransform(HumanBodyBones.LeftFoot); var fr = Anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var ul = Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var ur = Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var kl = Anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var kr = Anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Vector3 pl = fl.position, pr = fr.position; Quaternion ql = fl.rotation, qr = fr.rotation;
            float lenL = Vector3.Distance(ul.position, kl.position) + Vector3.Distance(kl.position, fl.position);
            float lenR = Vector3.Distance(ur.position, kr.position) + Vector3.Distance(kr.position, fr.position);
            _bodyHips.position += shift;
            for (int i = 0; i < 3 && drop; i++)
            {
                float excess = Mathf.Max(Vector3.Distance(ul.position, pl) - lenL * 0.995f, Vector3.Distance(ur.position, pr) - lenR * 0.995f);
                if (excess <= 0f) break;
                _bodyHips.position -= Figure.transform.up * excess;
            }
            Vector3 ahead = Flat(Figure.DrawnForward) * 0.4f;
            TwoBoneIK.Solve(ul, kl, fl, Within(ul.position, pl, lenL), kl.position + ahead);
            TwoBoneIK.Solve(ur, kr, fr, Within(ur.position, pr, lenR), kr.position + ahead);
            fl.rotation = ql; fr.rotation = qr;
        }

        /// <summary>A foot's spot the leg can reach from its hip: at the same height, drawn in along the floor if it's too far
        /// (a stretching player's foot drags; it doesn't lift).</summary>
        Vector3 Within(Vector3 hip, Vector3 foot, float len)
        {
            float reach = len * 0.995f, drop = hip.y - foot.y;
            Vector3 flat = foot - hip; flat.y = 0;
            float most = Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop));
            return flat.magnitude <= most ? foot : new Vector3(hip.x, foot.y, hip.z) + flat.normalized * most;
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
        Vector3 _backOff;
        float _followW, _diveW, _prepW, _strokeW, _diveClock, _readyClock, _recoverClock, _splitW;
        bool _diving, _diveRight, _locked;
        int _pick = MoveBook.FhDrive;
        /// <summary>The arm's straight finish shown now, 0..1 (tests).</summary>
        public float FollowShown { get { return _followW; } }
        /// <summary>The stance shown now, 0..1 (tests).</summary>
        public float StanceShown { get { return _stanceW; } }
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
            // the ball nearly here (the hit itself can start right at contact): the feet and the racket set themselves in time
            bool coming = v.HasPred && Figure.Runner != null && Vector3.Distance(Figure.Runner.View.BallPos, v.PredContact) < BallNear;
            if (v.Swing == SwingPhase.Idle) _locked = false;
            if ((v.Swing != SwingPhase.Idle || coming) && !_locked)
            {
                _pick = MoveBook.Pick(v.SwingSide > 0, v.Reach, v.HasPred ? v.PredContact.y : v.RacketHead.y, v.Touch, Figure.Runner != null && Figure.Runner.IsServing(Figure.Index));
                PlantStance(v);
            }
            if (committed) _locked = true;
            var move = MoveBook.All[_pick];
            bool going = v.Swing != SwingPhase.Idle && v.Swing != SwingPhase.Recovering;
            _recoverClock = going ? 0f : _recoverClock + dt;
            // the follow-through ends; away early if the athlete is already moving off
            bool done = v.Swing == SwingPhase.Recovering && (_vel.magnitude > 1.2f || _recoverClock >= move.Recover - move.End);
            float wantStroke = HasMoves && !diving && committed && !done ? 1f : 0f;
            float wantPrep = HasMoves && !diving && !committed && (v.Swing != SwingPhase.Idle || coming) ? 1f : 0f;
            bool set = HasMoves && !diving && _planted && !done && (v.Swing != SwingPhase.Idle || coming);
            _stanceW = Mathf.MoveTowards(_stanceW, set ? 1f : 0f, dt * (set ? 10f : 3f));
            if (_stanceW <= 0f && !set) _planted = false;
            _strokeW = Mathf.MoveTowards(_strokeW, wantStroke, dt * (wantStroke > _strokeW ? 20f : 3f));
            _prepW = Mathf.MoveTowards(_prepW, wantPrep, dt * (wantPrep > _prepW ? 7f : 5f));
            float swingT = Mathf.Clamp01(v.SwingT), at = move.At(v.Swing == SwingPhase.Idle && coming ? SwingPhase.Armed : v.Swing, swingT, _recoverClock);
            // (never while the racket is on the ball)
            bool finishing = wantStroke > 0f && Figure.RacketLock < 1f && (v.Swing == SwingPhase.Recovering || (v.Swing == SwingPhase.Swinging && swingT >= FollowFrom));
            _followW = Figure.RacketLock >= 1f ? 0f : Mathf.MoveTowards(_followW, finishing ? 1f : 0f, dt * (finishing ? 25f : 4f));
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
                if (_stanceW > 0.001f) figRight = Vector3.Slerp(figRight.normalized, root.InverseTransformDirection(Vector3.Cross(Vector3.up, _strideDir)), _stanceW);
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

            Ground();
            Posture();

            // 3: IK - the racket in the body's hand (the squash grip; before it, the hand where the figure's is), the free hand, the feet
            if (_grip) Grip();
            else Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "Hand_R", "Forearm_R", 1f);
            FreeArm();
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
