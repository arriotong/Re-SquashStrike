// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteFigure.cs
// The athlete on court: an AthleteRig posed every frame from the
// simulation (through the runner's render snapshot). Presentation only -
// it reads state and never writes it (Phase 1 pillar 6).
//
// M7: designed poses drive the body; physics only steers the racket
// through the contact window (the Mario Tennis approach):
//
//   Root      gameplay position; the facing turns at a human rate and
//             favours the front wall. During a stroke it turns so the ball
//             sits where the authored contact pose expects it.
//   Body      two layers of AUTHORED poses (PoseLibrary):
//               locomotion  ready / shuffle / back-pedal / run, blended by
//                           the velocity in the athlete's own frame
//               stroke      backswing -> contact -> follow-through, chosen
//                           when the swing commits (side, contact height,
//                           reach, touch) and played on the sim's own swing
//                           phase (contact at 0.5); held while preparing
//   Legs      planted feet that take real steps toward the pose's foot
//             positions (no sliding); leg IK reaches them, knees forward
//   Head      level; turns to the ball at most 45 deg sideways, 25 up/down
//   Racket    always in the hand, where the pose puts it - until the
//             simulated racket closes on the ball during a swing: within
//             0.75 m it blends onto the simulated racket, and within 0.3 m
//             (always at contact, when the ball is on the strings) its sweet
//             spot and strings are EXACTLY the simulated racket's, so the
//             racket you see at contact is the racket that hits
//   Fatigue   extra stoop and breathing (stamina), never a bar
// ===========================================================

using UnityEngine;

namespace SquashGame.Athlete
{
    public sealed class AthleteFigure : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public int Index;
        public AthleteRig Rig { get; private set; }

        /// <summary>How far the hand fell short of the racket target this frame (diagnostic).</summary>
        public float GripGap { get; private set; }
        public float WorstGripGap { get; private set; }
        /// <summary>The snapshot the current pose was built from (diagnostics and tests).</summary>
        public AthleteSnapshot Posed { get; private set; }
        /// <summary>How far the body moved toward the ball this frame to let the arm reach it.</summary>
        public float ReachShift { get; private set; }
        /// <summary>0 = the racket where the pose puts it, 1 = exactly on the simulated racket.</summary>
        public float RacketLock { get; private set; }
        /// <summary>The sweet spot as drawn this frame.</summary>
        public Vector3 DrawnRacketHead { get; private set; }
        /// <summary>The drawn facing (world, flat).</summary>
        public Vector3 DrawnForward { get; private set; }
        /// <summary>How fast the drawn body turned in the last pose, degrees per second.</summary>
        public float TurnRate { get; private set; }
        /// <summary>The stroke clip in use (its contact pose), or null between strokes.</summary>
        public string Clip { get; private set; }
        /// <summary>The head's yaw / pitch relative to the chest (degrees) and its roll.</summary>
        /// <summary>Diagnostics: shoulder-to-sweet distance before / after the body shift this frame.</summary>
        public float ShoulderGapBefore { get; private set; }
        public float ShoulderGapAfter { get; private set; }
        public float HeadYaw { get; private set; }
        public float HeadPitch { get; private set; }
        /// <summary>World positions of the soles this frame (R, L) and whether each is planted.</summary>
        public readonly Vector3[] Feet = new Vector3[2];
        public readonly bool[] FootPlanted = { true, true };

        // ---- poses ----
        AthletePose _ready, _shuffle, _runA, _runB, _backpedal;
        AthletePose _cBack, _cContact, _cFollow;
        float _contactBearing;
        readonly PoseBuffer _loco = new PoseBuffer(), _stroke = new PoseBuffer(), _tmp = new PoseBuffer(), _out = new PoseBuffer();

        // ---- presentation state (smoothed on render time) ----
        float _yaw, _gait, _breath, _strokeW, _headYaw, _headPitch;
        bool _hasYaw, _hasFeet, _latched;
        Vector3 _contact;                                    // the stroke's contact point, latched when the swing commits
        Vector3 _shift;
        readonly Vector3[] _footFrom = new Vector3[2], _footTo = new Vector3[2];
        readonly float[] _stepT = { -1f, -1f }, _stepDur = { 0.2f, 0.2f };

        const float MaxFacingFromFront = 115f;               // degrees either side of facing the front wall (between strokes)
        const float TurnRateIdle = 300f, TurnRateSwing = 540f; // degrees per second
        const float StepThreshold = 0.22f, StepThresholdFast = 0.14f;
        /// <summary>The athlete is drawn at this fraction of life size (the court is not): smaller figures read
        /// better from the broadcast camera. Gameplay distances (reach, hitting) are unchanged.</summary>
        public const float VisualScale = 0.88f;
        static float Ankle { get { return GateA.Ankle * VisualScale; } }
        const float ComfortReach = 0.90f * VisualScale;                    // shoulder -> sweet spot an arm + racket (strings set) cover
        const float MaxBodyShift = 0.60f, MaxBodyShiftLocked = 1.10f;   // locked: the drawn lunge may lead the sim's
        const float LockNear = 0.30f, LockFar = 0.75f;       // sim racket head to ball: fully locked .. free

        public void Build(SimRunnerBase runner, int index, GreyboxPalette palette, Color bodyColor)
        {
            Runner = runner;
            Index = index;
            Rig = gameObject.AddComponent<AthleteRig>();
            Rig.Build(palette, bodyColor);
            Rig.HoldRacketInHand();
            transform.localScale = Vector3.one * VisualScale;
            var lib = PoseLibrary.Active;
            _ready = lib.Find("ready"); _shuffle = lib.Find("shuffle");
            _runA = lib.Find("run_a"); _runB = lib.Find("run_b"); _backpedal = lib.Find("backpedal");
            _out.Set(_ready); _loco.Set(_ready); _stroke.Set(_ready);
        }

        Transform B(string name) { return Rig.Bones[name]; }

        void LateUpdate()
        {
            if (Runner == null || Runner.Clock == null || Rig == null) return;
            if (Index >= Runner.View.Count) return;
            AthleteSnapshot v = Runner.View.Athlete(Index);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Pose(v, Runner.View.BallPos, dt);
        }

        static bool Committed(AthleteSnapshot v) { return v.Swing == SwingPhase.Scheduled || v.Swing == SwingPhase.Swinging; }
        static bool InStroke(AthleteSnapshot v) { return v.Swing != SwingPhase.Idle; }

        /// <summary>The authored bearing of a pose's racket, degrees to the athlete's right of straight ahead.</summary>
        static float Bearing(AthletePose p) { return Mathf.Atan2(-p.Sweet.x, -p.Sweet.z) * Mathf.Rad2Deg; }

        /// <summary>Pick the stroke clip from what the ball will do.</summary>
        void ChooseClip(AthleteSnapshot v)
        {
            var lib = PoseLibrary.Active;
            bool fore = v.SwingSide > 0;
            float h = v.HasPred ? v.PredContact.y : v.RacketHead.y;
            string back = fore ? "fh_back" : "bh_back", contact, follow = fore ? "fh_follow" : "bh_follow";
            if (v.Reach >= 1) { contact = fore ? "lunge_fh" : "lunge_bh"; follow = contact; }
            else if (h > 1.9f) { contact = "overhead"; back = "fh_back"; follow = "fh_follow"; }
            else if (v.Touch) { contact = fore ? "touch" : "dig_bh"; follow = contact; }
            else if (h < 0.45f) contact = fore ? "dig_fh" : "dig_bh";
            else contact = fore ? "fh_contact" : "bh_contact";
            _cBack = lib.Find(back); _cContact = lib.Find(contact); _cFollow = lib.Find(follow);
            _contactBearing = Bearing(_cContact);
            Clip = contact;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public void Pose(AthleteSnapshot v, Vector3 ball, float dt)
        {
            Posed = v;
            Rig.ResetPose();
            Vector3 up = Vector3.up;
            bool committed = Committed(v), inStroke = InStroke(v);

            // ---------------- stroke clip: provisional while preparing, latched when the swing commits ----------------
            if (inStroke)
            {
                if (!_latched)
                {
                    ChooseClip(v);
                    _contact = v.HasPred ? v.PredContact : v.RacketHead;
                }
                else if (v.HasPred && v.Swing != SwingPhase.Swinging) _contact = v.PredContact;
                if (committed) _latched = true;
            }
            else if (_strokeW <= 0.001f) { _latched = false; Clip = null; }
            _strokeW = Mathf.MoveTowards(_strokeW, inStroke ? 1f : 0f, (inStroke ? 9f : 3.5f) * dt);

            // ---------------- root: facing ----------------
            Vector3 front = SimToWorld.YawForward(0);
            float want;
            Vector3 toContact = (_latched ? _contact : (v.HasPred ? v.PredContact : v.RacketHead)) - v.Pos;
            toContact.y = 0;
            if (inStroke && _cContact != null && toContact.sqrMagnitude > 0.04f)
                want = Vector3.SignedAngle(front, toContact, up) - _contactBearing;          // the ball where the contact pose expects it
            else
                want = Mathf.Clamp(Vector3.SignedAngle(front, Flat(v.Forward, front), up), -MaxFacingFromFront, MaxFacingFromFront);
            if (!_hasYaw) { _yaw = want; _hasYaw = true; }
            float rate = (inStroke ? TurnRateSwing : TurnRateIdle) * dt;
            float turn = Mathf.Clamp(Mathf.DeltaAngle(_yaw, want) * (1f - Mathf.Exp(-10f * dt)) * 1.6f, -rate, rate);
            _yaw += turn;
            TurnRate = dt > 1e-5f ? Mathf.Abs(turn) / dt : 0f;
            Vector3 fwd = Quaternion.AngleAxis(_yaw, up) * front;
            DrawnForward = fwd;
            transform.position = v.Pos;
            transform.rotation = Quaternion.LookRotation(-fwd, up);   // athlete faces local -Z

            // ---------------- locomotion layer ----------------
            Vector3 vflat = new Vector3(v.Velocity.x, 0, v.Velocity.z);
            float speed = vflat.magnitude;
            Vector3 vl = transform.InverseTransformDirection(vflat);   // +X left, -Z forward
            float fwdSpeed = -vl.z, sideSpeed = vl.x;
            _loco.Set(_ready);
            float moveW = Mathf.Clamp01((speed - 0.35f) / 2.2f);
            if (moveW > 0f)
            {
                _gait += speed * dt / 1.6f;
                if (Mathf.Abs(sideSpeed) > Mathf.Abs(fwdSpeed) * 0.8f) _tmp.Set(_shuffle);
                else if (fwdSpeed > 0f)
                {
                    _tmp.Set(_runA);
                    _tmp.Blend(_runB, 0.5f + 0.5f * Mathf.Sin(_gait * Mathf.PI * 2f));
                }
                else _tmp.Set(_backpedal);
                _loco.Blend(_tmp, moveW);
            }

            // ---------------- stroke layer ----------------
            if (_cContact != null && _strokeW > 0f)
            {
                if (v.Swing == SwingPhase.Swinging)
                {
                    float t = Mathf.Clamp01(v.SwingT);
                    if (t < 0.5f) { _stroke.Set(_cBack); _stroke.Blend(_cContact, Ease(t / 0.5f)); }
                    else { _stroke.Set(_cContact); _stroke.Blend(_cFollow, Ease((t - 0.5f) / 0.5f)); }
                }
                else if (v.Swing == SwingPhase.Recovering || (!inStroke && _latched)) _stroke.Set(_cFollow);
                else _stroke.Set(_cBack);                                           // armed / preparing / scheduled: the backswing
                _out.CopyFrom(_loco);
                _out.Blend(_stroke, Ease(_strokeW));
            }
            else _out.CopyFrom(_loco);

            // ---------------- body ----------------
            _out.ApplyBody(Rig);
            float tired = Mathf.Clamp01(1f - v.Stamina);
            _breath += dt * Mathf.Lerp(1.6f, 4.2f, tired);
            B("Chest").localRotation = B("Chest").localRotation * Quaternion.Euler(-12f * tired, 0, 0);
            B("Spine_02").localRotation = B("Spine_02").localRotation * Quaternion.Euler(-6f * tired, 0, 0);
            Transform pelvis = B("Pelvis");
            pelvis.localPosition += Vector3.up * (Mathf.Sin(_breath) * Mathf.Lerp(0.002f, 0.012f, tired));

            // ---------------- the racket target ----------------
            float w = 0f;
            if (v.Swing == SwingPhase.Swinging)
                w = 1f - Mathf.Clamp01((Vector3.Distance(v.RacketHead, ball) - LockNear) / (LockFar - LockNear));
            RacketLock = w;
            Vector3 simN = v.RacketFaceNormal.sqrMagnitude > 1e-6f ? v.RacketFaceNormal.normalized : fwd;
            Vector3 sweet = Vector3.Lerp(transform.TransformPoint(_out.Sweet), v.RacketHead, w);
            Vector3 normal = Vector3.Slerp(transform.TransformDirection(_out.RacketNormal), simN, w).normalized;
            Vector3 dir = transform.TransformDirection(_out.RacketDir);
            // Locked onto the sim racket: the shaft points from the shoulder out to the ball, so the hand sits
            // between them where the arm can reach (the sim has no shaft, only a head and a face).
            if (w > 0f)
            {
                Vector3 fromShoulder = (sweet - B("UpperArm_R").position).normalized;
                dir = Vector3.Slerp(dir.normalized, fromShoulder, w);
            }
            // The strings are the truth (exactly the sim's when locked): the shaft bends to lie in their plane.
            dir = dir - Vector3.Dot(dir, normal) * normal;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.Cross(normal, right0());
            dir.Normalize();

            // ---------------- reach: bring the body to the racket (eased; immediate when locked) ----------------
            {
                Vector3 sh = B("UpperArm_R").position;
                Vector3 gap = sweet - sh;
                ShoulderGapBefore = gap.magnitude;
                float over = gap.magnitude - ComfortReach;
                Vector3 target = Vector3.zero;
                if (over > 0) { target = gap.normalized * Mathf.Min(over, Mathf.Lerp(MaxBodyShift, MaxBodyShiftLocked, w)); target.y = Mathf.Min(target.y, 0f); }
                _shift = w >= 1f ? target : Vector3.Lerp(_shift, target, 1f - Mathf.Exp(-14f * dt));
                pelvis.position += _shift;
                ReachShift = _shift.magnitude;
                ShoulderGapAfter = (sweet - B("UpperArm_R").position).magnitude;
            }

            // ---------------- racket arm (before the legs: the body may still have to move to it) ----------------
            Vector3 elbowR = transform.TransformDirection(_out.ElbowR);
            GripGap = PoseMath.SolveRacketArm(Rig, sweet, dir, normal, elbowR, out Vector3 wristTarget);
            if (w > 0f)
            {
                // Still short while locked: move the body by exactly the shortfall, and solve again.
                for (int it = 0; it < 2 && GripGap > 0.003f; it++)
                {
                    Vector3 miss = (wristTarget - B("Hand_R").position) * w;
                    if (_shift.magnitude > MaxBodyShiftLocked + 0.4f) break;
                    pelvis.position += miss;
                    _shift += miss;
                    GripGap = PoseMath.SolveRacketArm(Rig, sweet, dir, normal, elbowR, out wristTarget);
                }
                ReachShift = _shift.magnitude;
            }
            if (GripGap > WorstGripGap) WorstGripGap = GripGap;
            DrawnRacketHead = Rig.Racket.TransformPoint(new Vector3(0, -GateA.SweetSpotFromButt, 0));

            // ---------------- legs: planted feet stepping toward the pose's feet ----------------
            Vector3 right = Vector3.Cross(up, fwd);
            Vector3 shiftFlat = new Vector3(_shift.x, 0, _shift.z);
            float threshold = speed > 2.5f ? StepThresholdFast : StepThreshold;
            for (int side = 0; side < 2; side++)
            {
                Vector3 local = side == 0 ? _out.FootR : _out.FootL;
                Vector3 target = transform.TransformPoint(new Vector3(local.x, 0f, local.z)) + shiftFlat * 0.6f;
                float lift = Mathf.Max(0f, local.y - GateA.Ankle) * VisualScale;   // pose units are life-size
                target.y = Ankle;
                if (!_hasFeet) Feet[side] = target;

                if (_stepT[side] < 0)
                {
                    float offDist = new Vector3(Feet[side].x - target.x, 0, Feet[side].z - target.z).magnitude;
                    bool otherStepping = _stepT[1 - side] >= 0 && _stepT[1 - side] < 0.5f;
                    if (offDist > 1.6f) Feet[side] = target;                               // teleport (reset, new rally)
                    else if (offDist > threshold && (!otherStepping || offDist > 0.6f))
                    {
                        _stepT[side] = 0f;
                        _stepDur[side] = Mathf.Clamp(0.24f - 0.025f * speed, 0.12f, 0.24f);
                        _footFrom[side] = Feet[side];
                    }
                    else Feet[side] = new Vector3(Feet[side].x, Ankle + lift, Feet[side].z);
                }
                if (_stepT[side] >= 0)
                {
                    _footTo[side] = target + vflat * (_stepDur[side] * 0.5f);
                    _footTo[side].y = Ankle;
                    _stepT[side] += dt / _stepDur[side];
                    float u = Mathf.Clamp01(_stepT[side]);
                    Vector3 p = Vector3.Lerp(_footFrom[side], _footTo[side], Ease(u));
                    p.y = Ankle + Mathf.Sin(u * Mathf.PI) * Mathf.Min(0.12f, 0.05f + 0.02f * speed);
                    Feet[side] = p;
                    if (u >= 1f) { _stepT[side] = -1f; Feet[side] = _footTo[side]; }
                }
                FootPlanted[side] = _stepT[side] < 0;

                string sn = side == 0 ? "R" : "L";
                float outward = side == 0 ? -1f : 1f;
                Vector3 knee = Feet[side] + fwd * 0.55f + up * 0.45f - right * (outward * 0.12f);
                TwoBoneIK.Solve(B("Thigh_" + sn), B("Shin_" + sn), B("Foot_" + sn), Feet[side], knee);
                B("Foot_" + sn).rotation = transform.rotation * Quaternion.Euler(0f, side == 0 ? _out.FootYawR : _out.FootYawL, 0f);
            }
            _hasFeet = true;

            // ---------------- head: level, eyes on the ball ----------------
            {
                Transform chest = B("Chest"), head = B("Head");
                Vector3 cf = chest.TransformDirection(Vector3.back); cf.y = 0;
                if (cf.sqrMagnitude < 1e-4f) cf = fwd;
                cf.Normalize();
                Vector3 toBall = ball - head.position;
                Vector3 tbFlat = new Vector3(toBall.x, 0, toBall.z);
                float yawWant = tbFlat.sqrMagnitude > 1e-4f ? Mathf.Clamp(Vector3.SignedAngle(cf, tbFlat, up), -45f, 45f) : 0f;
                float pitchWant = Mathf.Clamp(Mathf.Atan2(toBall.y, Mathf.Max(0.2f, tbFlat.magnitude)) * Mathf.Rad2Deg, -25f, 25f);
                float k = 1f - Mathf.Exp(-8f * dt);
                _headYaw = Mathf.Lerp(_headYaw, yawWant, k);
                _headPitch = Mathf.Lerp(_headPitch, pitchWant, k);
                head.rotation = Quaternion.LookRotation(-cf, up) * Quaternion.Euler(_headPitch, _headYaw, 0f);
                HeadYaw = _headYaw; HeadPitch = _headPitch;
            }

            // ---------------- free arm ----------------
            PoseMath.SolveFreeArm(Rig, transform.TransformPoint(_out.HandL), transform.TransformDirection(_out.ElbowL));

            Rig.UpdateBlends();
        }

        Vector3 right0() { return Vector3.Cross(Vector3.up, DrawnForward).normalized; }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }
    }
}
