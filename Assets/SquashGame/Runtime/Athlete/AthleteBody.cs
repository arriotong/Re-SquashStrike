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

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.zero; }

        // ---- 2026-11-01 the user's Mixamo clips: every direction, the goalkeeper dive, the swings ----
        AnimationLayerMixerPlayable _layers;
        AnimationMixerPlayable _dir, _dive, _swing;
        AnimationClipPlayable _back, _left, _right, _diveL, _diveR, _fh, _bh, _oh;
        /// <summary>Each clip's moment that matters, in seconds (MakeHuman-free: measured in Blender, 2026-11-01): the dive's
        /// launch, landing and rise; the swings' contact (the hand at its fastest).</summary>
        public const float DiveLaunch = 1.0f, DiveLandL = 1.6f, DiveRiseL = 2.4f, DiveEndL = 3.33f, DiveLandR = 1.47f, DiveRiseR = 2.2f, DiveEndR = 3.2f;
        public const float FhContact = 19f / 30f, BhContact = 26f / 30f, OhContact = 28f / 30f;
        public const float StrafeSpeed = 4.0f, BackSpeed = 2.2f;
        /// <summary>The dive and the swing shown now, 0..1 (tests).</summary>
        public float DiveShown { get; private set; }
        public float SwingShown { get; private set; }
        float _diveW, _swingW, _diveClock;
        bool _diving, _diveRight;

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
            // the dive, left or right
            _dive = AnimationMixerPlayable.Create(_graph, 2);
            _diveL = Clip(clips, "Dive_Left", true); _diveR = Clip(clips, "Dive_Right", true);
            Connect(_dive, 0, _diveL, 1f); Connect(_dive, 1, _diveR, 0f);
            // the swings
            _swing = AnimationMixerPlayable.Create(_graph, 3);
            _fh = Clip(clips, "Swing_Forehand", true); _bh = Clip(clips, "Swing_Backhand", true); _oh = Clip(clips, "Swing_Overhead", true);
            Connect(_swing, 0, _fh, 1f); Connect(_swing, 1, _bh, 0f); Connect(_swing, 2, _oh, 0f);

            _layers = AnimationLayerMixerPlayable.Create(_graph, 3);
            _graph.Connect(_dir, 0, _layers, 0); _layers.SetInputWeight(0, 1f);
            _graph.Connect(_dive, 0, _layers, 1); _layers.SetInputWeight(1, 0f);
            _graph.Connect(_swing, 0, _layers, 2); _layers.SetInputWeight(2, 0f);
            _layers.SetLayerMaskFromAvatarMask(2, UpperBody());
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
            _layers.SetInputWeight(1, _diveW);
            DiveShown = _diveW;

            // ---- the swing (upper body): forehand, backhand, or overhead (the serve, a ball above the head) ----
            float want = v.Swing == SwingPhase.Idle ? 0f : v.Swing == SwingPhase.Armed ? 0.35f : 0.85f;
            if (diving) want = 0f;
            _swingW = Mathf.MoveTowards(_swingW, want, dt * (want > _swingW ? 6f : 3f));
            if (_swingW > 0.001f)
            {
                bool over = Figure.Clip == "overhead" || (v.HasPred && v.PredContact.y > 1.9f);
                int which = over ? 2 : v.SwingSide > 0 ? 0 : 1;
                var clip = which == 0 ? _fh : which == 1 ? _bh : _oh;
                float contact = which == 0 ? FhContact : which == 1 ? BhContact : OhContact;
                if (clip.IsValid())
                {
                    float len = (float)clip.GetAnimationClip().length;
                    float t;
                    if (v.Swing == SwingPhase.Swinging)
                        t = v.SwingT < 0.5f ? Mathf.Lerp(contact * 0.6f, contact, v.SwingT / 0.5f) : Mathf.Lerp(contact, len * 0.92f, (v.SwingT - 0.5f) / 0.5f);
                    else if (v.Swing == SwingPhase.Recovering) t = len * 0.92f;
                    else t = contact * 0.6f;                               // the backswing, held until the swing goes
                    clip.SetTime(t);
                }
                for (int i = 0; i < 3; i++) _swing.SetInputWeight(i, i == which ? 1f : 0f);
            }
            _layers.SetInputWeight(2, _swingW);
            SwingShown = _swingW;
        }

        void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); if (Figure != null) Figure.Dressed -= OnDressed; }

        /// <summary>The kit's colours on the body; the other build's body when the kit's build changes.</summary>
        void OnDressed(Progression.Kit k)
        {
            string want = PlayerModels.BodyFor(IsYou(Figure), k.Female, PlayerModels.ToneOf(k.Skin));
            if (want != ModelId && Resources.Load<TextAsset>(PlayerModels.Folder + want) != null)
            {
                if (_graph.IsValid()) _graph.Destroy();
                Destroy(_model); _model = null; Parts.Clear(); _pairs.Clear(); _keys.Clear();
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

            // 1 + 2: the figure's pose on the body, the motion capture blended in by part
            // the legs: moving or diving; the arms and back: the swing, the dive, or swinging free when running
            float wLegs = Mathf.Max(_mocapW, _diveW);
            float wArms = Mathf.Max(_diveW, InStroke(v) ? _swingW : _mocapW * 0.85f);
            float wSpine = Mathf.Max(_diveW, Mathf.Max(_swingW * 0.8f, _mocapW * 0.6f));
            foreach (var p in _pairs)
            {
                Quaternion fig = rootInv * p.Rig.rotation;
                Quaternion turned = fig * Quaternion.Inverse(p.RigRest) * p.BodyRestAdj;
                float w = p.Legs ? wLegs : p.Arm ? wArms : p.Spine ? wSpine : Mathf.Max(_diveW, _mocapW * 0.3f);
                Quaternion q = w > 0.001f ? Quaternion.Slerp(turned, _anim[Key(p)], w) : turned;
                p.Body.rotation = root.rotation * q;
            }
            Vector3 figHips = root.InverseTransformPoint(_rigPelvis.position) + _hipsOffset;
            _bodyHips.position = root.TransformPoint(Vector3.Lerp(figHips, new Vector3(figHips.x, Mathf.Lerp(figHips.y, animHips.y, wLegs), figHips.z), 1f));

            // 3: IK - the racket hand exactly where the figure's is (the racket rides that hand), the free hand, the feet
            Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "Hand_R", "Forearm_R", 1f);
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "Hand_L", "Forearm_L", 1f - wArms);
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "L" : "R";
                Reach(s == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, s == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                      s == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, "Foot_" + side, "Shin_" + side, 1f - wLegs);
            }
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
            bool busy = InStroke(v) && v.Swing >= SwingPhase.Scheduled || Figure.SplitT >= 0f;
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
