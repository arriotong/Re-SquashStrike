// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteFigure.cs
// The athlete on court: an AthleteRig posed every frame from the
// simulation (through the runner's render snapshot). Presentation only -
// it reads state and never writes it (Phase 1 pillar 6).
//
// Ownership, from the Gate B contract:
//   Root     gameplay position; facing follows the sim's, but turns at a
//            human rate and favours the front wall (a squash player
//            pivots and watches the ball - he does not spin round) (M6)
//   Pelvis   crouch (ready stance, the COMMITTED reach of a swing)
//   Spine    twist to the commitment solver's BODY yaw - what an opponent
//            reads - plus lean toward the ball when stretched
//   Head     looks at the ball
//   Legs     planted feet that take real steps (M6): a foot stays where it
//            is until the body has moved off it, then steps; the lunge is
//            a step of the racket-side foot
//   Racket   between swings: a smoothed ready racket (forehand/backhand
//            changes are a visible switch). From swing start it locks onto
//            the simulated racket EXACTLY (sweet spot = sim head position,
//            string normal = sim face normal), so the racket you see at
//            contact is the racket that hits (M6)
//   Arm      two-bone IK so the hand holds that racket by the Gate A grip
//            socket; the elbow bends toward a natural pole
//   Fatigue  stoop and breathing (stamina), never a bar
//
// The pose keys on the swing state, never on the sim's per-step reach
// prediction (which flickers while the ball is in flight).
// ===========================================================

using UnityEngine;

namespace SquashGame.Athlete
{
    public sealed class AthleteFigure : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public int Index;
        public AthleteRig Rig { get; private set; }

        /// <summary>Distance between where the hand reached and where the racket grip is (diagnostic).</summary>
        public float GripGap { get; private set; }
        public float WorstGripGap { get; private set; }
        /// <summary>The snapshot the current pose was built from (diagnostics and tests).</summary>
        public AthleteSnapshot Posed { get; private set; }
        /// <summary>How far the body moved toward the ball this frame to let the arm reach it.</summary>
        public float ReachShift { get; private set; }
        /// <summary>0 = the smoothed ready racket, 1 = exactly on the simulated racket.</summary>
        public float RacketLock { get; private set; }
        /// <summary>The sweet spot as drawn this frame.</summary>
        public Vector3 DrawnRacketHead { get; private set; }
        /// <summary>The drawn facing (world, flat).</summary>
        public Vector3 DrawnForward { get; private set; }
        /// <summary>How fast the drawn body turned in the last pose, degrees per second.</summary>
        public float TurnRate { get; private set; }
        /// <summary>World positions of the soles this frame (R, L) and whether each is planted.</summary>
        public readonly Vector3[] Feet = new Vector3[2];
        public readonly bool[] FootPlanted = { true, true };

        // ---- presentation state (smoothed on render time) ----
        float _hip = GateA.Hip, _breath, _leanToBall, _yaw, _bob;
        bool _hasYaw, _hasRacket, _hasFeet;
        Vector3 _prevRacketAxis = Vector3.up;
        bool _hasPrevAxis;
        Vector3 _smHead, _smNormal, _shift;
        readonly Vector3[] _footFrom = new Vector3[2], _footTo = new Vector3[2];
        readonly float[] _stepT = { -1f, -1f }, _stepDur = { 0.2f, 0.2f };

        const float ReadyHip = 0.855f;                       // Gate A ready pose: shoulder on 1.32 m
        static readonly float[] ReachHip = { ReadyHip, 0.76f, 0.68f, 0.63f };
        static readonly float[] LungeLength = { 0f, 0.55f, 0.75f, 0.95f };
        const float SocketToSweet = GateA.SweetSpotFromButt; // butt -> sweet spot along the grip axis
        const float LowBallHip = 0.619f;                     // Gate A low-ball reach pose
        const float ComfortReach = 1.02f;                    // shoulder -> sweet spot the arm + racket cover easily
        const float MaxBodyShift = 0.75f;                    // how far hips + trunk may travel toward the ball

        // M6 facing / racket / steps
        const float MaxFacingFromFront = 115f;               // degrees either side of facing the front wall
        const float TurnRateIdle = 300f, TurnRateSwing = 540f; // degrees per second
        const float LockUpRate = 14f, LockDownRate = 4f;     // racket lock blend per second
        const float StepThreshold = 0.24f, StepThresholdFast = 0.14f;

        public void Build(SimRunnerBase runner, int index, GreyboxPalette palette, Color bodyColor)
        {
            Runner = runner;
            Index = index;
            Rig = gameObject.AddComponent<AthleteRig>();
            Rig.Build(palette, bodyColor);
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

        /// <summary>Is the athlete committed to a stroke (scheduled or swinging)?</summary>
        static bool Committed(AthleteSnapshot v) { return v.Swing == SwingPhase.Scheduled || v.Swing == SwingPhase.Swinging; }

        public void Pose(AthleteSnapshot v, Vector3 ball, float dt)
        {
            Posed = v;
            Rig.ResetPose();
            Vector3 up = Vector3.up;
            bool committed = Committed(v);
            bool inStroke = committed || v.Swing == SwingPhase.Recovering;
            int reach = inStroke ? Mathf.Clamp(v.Reach, 0, 3) : 0;   // the committed reach, not the flickering prediction

            // ---------------- root: facing at a human rate, favouring the front wall ----------------
            Vector3 front = SimToWorld.YawForward(0);
            Vector3 simFwd = Flat(v.Forward, front);
            float want = Mathf.Clamp(Vector3.SignedAngle(front, simFwd, up), -MaxFacingFromFront, MaxFacingFromFront);
            if (!_hasYaw) { _yaw = want; _hasYaw = true; }
            float rate = (inStroke ? TurnRateSwing : TurnRateIdle) * dt;
            float diff = Mathf.DeltaAngle(_yaw, want);
            float turn = Mathf.Clamp(diff * (1f - Mathf.Exp(-10f * dt)) * 1.6f, -rate, rate);
            _yaw += turn;
            TurnRate = dt > 1e-5f ? Mathf.Abs(turn) / dt : 0f;
            Vector3 fwd = Quaternion.AngleAxis(_yaw, up) * front;
            Vector3 right = Vector3.Cross(up, fwd);           // athlete's right in world
            DrawnForward = fwd;

            transform.position = v.Pos;
            transform.rotation = Quaternion.LookRotation(-fwd, up);   // athlete faces local -Z

            // ---------------- the racket as drawn ----------------
            Vector3 simN = v.RacketFaceNormal.sqrMagnitude > 1e-6f ? v.RacketFaceNormal.normalized : fwd;
            if (!_hasRacket || (_smHead - v.RacketHead).sqrMagnitude > 9f) { _smHead = v.RacketHead; _smNormal = simN; _hasRacket = true; }
            float follow = 1f - Mathf.Exp(-12f * dt);
            _smHead = Vector3.Lerp(_smHead, v.RacketHead, follow);
            _smNormal = Vector3.Slerp(_smNormal, simN, follow).normalized;
            RacketLock = committed ? Mathf.Min(1f, RacketLock + LockUpRate * dt) : Mathf.Max(0f, RacketLock - LockDownRate * dt);
            Vector3 sweet = RacketLock >= 1f ? v.RacketHead : Vector3.Lerp(_smHead, v.RacketHead, RacketLock);
            Vector3 n = RacketLock >= 1f ? simN : Vector3.Slerp(_smNormal, simN, RacketLock).normalized;
            if (RacketLock >= 1f) { _smHead = v.RacketHead; _smNormal = simN; }   // nothing to catch up when the lock releases
            DrawnRacketHead = sweet;

            // ---------------- crouch, fatigue ----------------
            float speed = new Vector3(v.Velocity.x, 0, v.Velocity.z).magnitude;
            float targetHip = Mathf.Lerp(ReachHip[reach], GateA.Hip - 0.04f, Mathf.Clamp01(speed / 4f) * (reach == 0 ? 1f : 0f));
            if (inStroke && sweet.y < 0.75f) targetHip = Mathf.Min(targetHip, Mathf.Max(LowBallHip, ReadyHip - (0.75f - sweet.y) * 0.45f));
            _hip += (targetHip - _hip) * (1f - Mathf.Exp(-9f * dt));
            float tired = Mathf.Clamp01(1f - v.Stamina);
            _breath += dt * Mathf.Lerp(1.6f, 4.2f, tired);
            float breathe = Mathf.Sin(_breath) * Mathf.Lerp(0.002f, 0.012f, tired);

            // Toward the ball we are playing (the racket head), flat.
            Vector3 toRacket = Flat(sweet - v.Pos, fwd);
            _leanToBall += (reach / 3f - _leanToBall) * (1f - Mathf.Exp(-8f * dt));

            Transform pelvis = B("Pelvis");
            Vector3 hipShift = toRacket * (0.22f * _leanToBall);
            pelvis.position = v.Pos + up * (_hip + _bob + breathe) + hipShift;

            // ---------------- torso: twist to body yaw, lean ----------------
            Vector3 shoulders = Flat(v.ShoulderForward, fwd);
            Quaternion twist = Quaternion.FromToRotation(fwd, shoulders);
            float leanDeg = 8f + 10f * (1f - Mathf.InverseLerp(ReadyHip, GateA.Hip, _hip)) + 22f * _leanToBall + 12f * tired;
            Vector3 leanDir = Vector3.Slerp(fwd, toRacket, _leanToBall).normalized;
            Quaternion lean = Quaternion.AngleAxis(leanDeg, Vector3.Cross(up, leanDir));
            Quaternion baseRot = transform.rotation;
            B("Spine_01").rotation = Quaternion.Slerp(Quaternion.identity, lean, 0.45f) * Quaternion.Slerp(Quaternion.identity, twist, 0.3f) * baseRot;
            B("Spine_02").rotation = Quaternion.Slerp(Quaternion.identity, lean, 0.75f) * Quaternion.Slerp(Quaternion.identity, twist, 0.6f) * baseRot;
            B("Chest").rotation = lean * twist * baseRot;

            // ---------------- head: look at the ball ----------------
            Transform head = B("Head");
            Vector3 chestFwd = B("Chest").rotation * new Vector3(0, 0, -1);
            Vector3 toBall = (ball - head.position);
            if (toBall.sqrMagnitude > 1e-4f)
            {
                Vector3 look = Vector3.RotateTowards(chestFwd, toBall.normalized, 80f * Mathf.Deg2Rad, 0f);
                head.rotation = Quaternion.LookRotation(-look, up);
            }

            // ---------------- reach: bring the body to the racket (eased, never a jump) ----------------
            {
                Vector3 sh = B("UpperArm_R").position;
                Vector3 gap = sweet - sh;
                float over = gap.magnitude - ComfortReach;
                Vector3 target = Vector3.zero;
                if (over > 0)
                {
                    target = gap.normalized * Mathf.Min(over, MaxBodyShift);
                    target.y = Mathf.Min(target.y, 0f);              // never jump up to it
                }
                // Locked onto a swing the body must get there now; otherwise ease (no pops).
                _shift = RacketLock >= 1f ? target : Vector3.Lerp(_shift, target, 1f - Mathf.Exp(-14f * dt));
                pelvis.position += _shift;
                ReachShift = _shift.magnitude;
            }

            // ---------------- legs: planted feet, real steps ----------------
            float width = Mathf.Lerp(0.13f, 0.21f, 1f - Mathf.InverseLerp(ReadyHip, GateA.Hip, _hip));
            float lungeLen = LungeLength[reach] * Mathf.Clamp01(_leanToBall * 3f);
            Vector3 vflat = new Vector3(v.Velocity.x, 0, v.Velocity.z);
            Vector3 groundPelvis = new Vector3(pelvis.position.x, 0, pelvis.position.z);
            float threshold = speed > 2.5f ? StepThresholdFast : StepThreshold;
            for (int side = 0; side < 2; side++)
            {
                float sgn = side == 0 ? 1f : -1f;
                Vector3 target = groundPelvis + right * (sgn * width) + fwd * (side == 0 ? 0.06f : -0.06f);
                // Lunge: the racket-side (right) foot steps toward the ball, the other stays back.
                if (lungeLen > 0.01f) target += side == 0 ? toRacket * lungeLen : -toRacket * (lungeLen * 0.35f);
                target.y = GateA.Ankle;
                if (!_hasFeet) { Feet[0] = target; Feet[1] = target; }
                Feet[side].y = GateA.Ankle;

                if (_stepT[side] < 0)
                {
                    float off = new Vector3(Feet[side].x - target.x, 0, Feet[side].z - target.z).magnitude;
                    bool otherStepping = _stepT[1 - side] >= 0 && _stepT[1 - side] < 0.5f;
                    if (off > 1.6f) Feet[side] = target;                                   // teleport (reset, new rally)
                    else if (off > threshold && (!otherStepping || off > 0.6f))
                    {
                        _stepT[side] = 0f;
                        _stepDur[side] = Mathf.Clamp(0.26f - 0.025f * speed, 0.13f, 0.26f);
                        _footFrom[side] = Feet[side];
                    }
                }
                if (_stepT[side] >= 0)
                {
                    // Aim a little ahead of where the body will be when the foot lands.
                    _footTo[side] = target + vflat * (_stepDur[side] * 0.5f);
                    _footTo[side].y = GateA.Ankle;
                    _stepT[side] += dt / _stepDur[side];
                    float u = Mathf.Clamp01(_stepT[side]);
                    float s = u * u * (3f - 2f * u);
                    Vector3 p = Vector3.Lerp(_footFrom[side], _footTo[side], s);
                    p.y = GateA.Ankle + Mathf.Sin(u * Mathf.PI) * Mathf.Min(0.13f, 0.045f + 0.02f * speed);
                    Feet[side] = p;
                    if (u >= 1f) { _stepT[side] = -1f; Feet[side] = _footTo[side]; }
                }
                FootPlanted[side] = _stepT[side] < 0;

                string sname = side == 0 ? "R" : "L";
                Vector3 knee = Feet[side] + fwd * 0.5f + up * 0.45f + right * (sgn * 0.08f);
                TwoBoneIK.Solve(B("Thigh_" + sname), B("Shin_" + sname), B("Foot_" + sname), Feet[side], knee);
                B("Foot_" + sname).rotation = baseRot;                // soles flat
            }
            _hasFeet = true;
            // A small pelvis bob while a foot is in the air.
            float air = Mathf.Max(_stepT[0] >= 0 ? Mathf.Sin(Mathf.Clamp01(_stepT[0]) * Mathf.PI) : 0f,
                                  _stepT[1] >= 0 ? Mathf.Sin(Mathf.Clamp01(_stepT[1]) * Mathf.PI) : 0f);
            _bob = 0.018f * air;

            // ---------------- racket; hand on the racket ----------------
            Transform shoulderR = B("UpperArm_R");
            Vector3 axis = ChooseRacketAxis(sweet, n, shoulderR.position, up);
            _prevRacketAxis = axis; _hasPrevAxis = true;

            // Racket: local -Y = grip axis (butt -> tip), local X = string normal; sweet spot on the drawn head.
            Quaternion racketRot = Basis(n, -axis);
            Vector3 butt = sweet - axis * SocketToSweet;
            Rig.Racket.SetPositionAndRotation(butt, racketRot);

            // Hand: Gate A grip socket (fwd = grip axis, up = string normal) onto the racket butt.
            Quaternion handRot = Basis(-n, -axis);
            Vector3 socketLocal = new Vector3(-GateA.GripOffsetFromWrist.x, GateA.GripOffsetFromWrist.y, GateA.GripOffsetFromWrist.z);
            Vector3 wristTarget = butt - handRot * socketLocal;
            Vector3 elbowPole = shoulderR.position - up * 0.6f + right * 0.35f - fwd * 0.25f;
            TwoBoneIK.Solve(shoulderR, B("Forearm_R"), B("Hand_R"), wristTarget, elbowPole);
            B("Hand_R").rotation = handRot;
            GripGap = (B("Hand_R").position - wristTarget).magnitude;
            if (GripGap > WorstGripGap) WorstGripGap = GripGap;

            // ---------------- free arm: balance, out and forward ----------------
            Transform shoulderL = B("UpperArm_L");
            Vector3 freeHand = shoulderL.position + (-right) * 0.32f + fwd * 0.22f - up * (0.42f - 0.15f * _leanToBall);
            TwoBoneIK.Solve(shoulderL, B("Forearm_L"), B("Hand_L"), freeHand, shoulderL.position - up * 0.5f - right * 0.2f - fwd * 0.3f);

            Rig.UpdateBlends();
        }

        /// <summary>
        /// The grip-axis direction that lets a real arm hold this racket: in the string plane
        /// (perpendicular to the face normal), with the wrist about an arm's comfortable length
        /// from the shoulder, the tip not hanging down, and close to last frame's choice.
        /// </summary>
        Vector3 ChooseRacketAxis(Vector3 sweet, Vector3 n, Vector3 shoulder, Vector3 up)
        {
            Vector3 v = sweet - shoulder;
            Vector3 e1 = v - Vector3.Dot(v, n) * n;
            if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.Cross(n, Vector3.right);
            e1.Normalize();
            Vector3 e2 = Vector3.Cross(n, e1).normalized;
            const float comfortable = 0.48f;
            Vector3 best = e1;
            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < 48; i++)
            {
                float th = i / 48f * Mathf.PI * 2f;
                Vector3 u = Mathf.Cos(th) * e1 + Mathf.Sin(th) * e2;
                Vector3 wrist = sweet - u * (SocketToSweet + 0.06f);
                float d = (wrist - shoulder).magnitude;
                float cost = 10f * (d - comfortable) * (d - comfortable);
                cost += 0.6f * Mathf.Max(0f, -u.y - 0.15f);                 // tip should not hang down
                cost += 0.3f * Mathf.Max(0f, wrist.y - sweet.y);            // hand below the head
                // Strong preference for last frame's grip: the racket turns in the hand, it never flips.
                if (_hasPrevAxis) { float a = Vector3.Angle(u, _prevRacketAxis) * Mathf.Deg2Rad; cost += 0.6f * a * a; }
                if (cost < bestCost) { bestCost = cost; best = u; }
            }
            return best;
        }

        /// <summary>Rotation whose local +X is <paramref name="x"/> and local +Y is <paramref name="y"/>.</summary>
        static Quaternion Basis(Vector3 x, Vector3 y)
        {
            y = y.normalized;
            x = (x - Vector3.Dot(x, y) * y).normalized;
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }
    }
}
