// ===========================================================
// Assets/SquashGame/Runtime/Athlete/AthleteFigure.cs
// The athlete on court: an AthleteRig posed every frame from the
// simulation (through the runner's render snapshot). Presentation only -
// it reads state and never writes it (Phase 1 pillar 6).
//
// Ownership, from the Gate B contract:
//   Root     gameplay position and facing (PlayerMovement / PlayerFacing)
//   Pelvis   crouch and gait (ready stance, reach rung, running)
//   Spine    twist to the commitment solver's BODY yaw - what an opponent
//            reads - plus lean toward the ball when stretched
//   Head     looks at the ball
//   Legs     two-bone IK to gait / lunge foot targets
//   Racket   placed EXACTLY on the simulated racket (sweet spot = sim head
//            position, string normal = sim face normal): the racket you
//            see is the racket that hits. The racket never animates.
//   Arm      two-bone IK so the hand holds that racket by the Gate A grip
//            socket; the elbow bends toward a natural pole.
//   Fatigue  stoop and breathing (stamina), never a bar.
//
// "Stroke poses" are therefore sampled by the swing itself: as the sim's
// racket travels its arc, the arm, torso twist and lunge follow it.
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

        // ---- presentation state (smoothed on render time) ----
        float _hip = GateA.Hip, _gait, _breath, _leanToBall;
        Vector3 _prevRacketAxis = Vector3.up;
        bool _hasPrevAxis;

        const float ReadyHip = 0.855f;                       // Gate A ready pose: shoulder on 1.32 m
        static readonly float[] ReachHip = { ReadyHip, 0.76f, 0.68f, 0.63f };
        static readonly float[] LungeLength = { 0f, 0.55f, 0.75f, 0.95f };
        const float SocketToSweet = GateA.SweetSpotFromButt; // butt -> sweet spot along the grip axis
        const float LowBallHip = 0.619f;                     // Gate A low-ball reach pose
        const float ComfortReach = 1.02f;                    // shoulder -> sweet spot the arm + racket cover easily
        const float MaxBodyShift = 0.75f;                    // how far hips + trunk may travel toward the ball

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
            float dt = Time.unscaledDeltaTime;
            Pose(v, Runner.View.BallPos, dt);
        }

        public void Pose(AthleteSnapshot v, Vector3 ball, float dt)
        {
            Posed = v;
            Rig.ResetPose();
            Vector3 up = Vector3.up;
            Vector3 fwd = Flat(v.Forward, Vector3.forward);
            Vector3 right = Vector3.Cross(up, fwd);           // athlete's right in world

            // ---------------- root (gameplay owns it) ----------------
            transform.position = v.Pos;
            transform.rotation = Quaternion.LookRotation(-fwd, up);   // athlete faces local -Z

            // ---------------- crouch, gait, fatigue ----------------
            float speed = new Vector3(v.Velocity.x, 0, v.Velocity.z).magnitude;
            float targetHip = Mathf.Lerp(ReachHip[Mathf.Clamp(v.Reach, 0, 3)], GateA.Hip - 0.04f, Mathf.Clamp01(speed / 4f) * (v.Reach == 0 ? 1f : 0f));
            if (v.RacketHead.y < 0.75f) targetHip = Mathf.Min(targetHip, Mathf.Max(LowBallHip, ReadyHip - (0.75f - v.RacketHead.y) * 0.45f));
            _hip += (targetHip - _hip) * (1f - Mathf.Exp(-12f * dt));
            float stride = 0.9f + 0.25f * Mathf.Clamp01(speed / 5f);
            _gait += speed * dt / stride;
            float bob = 0.022f * Mathf.Abs(Mathf.Sin(_gait * Mathf.PI * 2f)) * Mathf.Clamp01(speed / 2f);
            float tired = Mathf.Clamp01(1f - v.Stamina);
            _breath += dt * Mathf.Lerp(1.6f, 4.2f, tired);
            float breathe = Mathf.Sin(_breath) * Mathf.Lerp(0.002f, 0.012f, tired);

            // Toward the ball we are playing (the racket head), flat.
            Vector3 toRacket = Flat(v.RacketHead - v.Pos, fwd);
            float reachT = v.Reach / 3f;
            _leanToBall += (reachT - _leanToBall) * (1f - Mathf.Exp(-10f * dt));

            Transform pelvis = B("Pelvis");
            Vector3 hipShift = toRacket * (0.22f * _leanToBall);
            pelvis.position = v.Pos + up * (_hip + bob + breathe) + hipShift;

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
                Vector3 look = Vector3.RotateTowards(chestFwd, toBall.normalized, 75f * Mathf.Deg2Rad, 0f);
                head.rotation = Quaternion.LookRotation(-look, up);
            }

            // ---------------- reach: bring the body to the racket ----------------
            // The simulated swing radius (the reach ladder, up to 1.55 m from the shoulder pivot) is longer
            // than an arm plus a racket. A player closes that gap with the body: hips and trunk travel
            // toward the ball (the feet stay where the lunge put them, via the leg IK below).
            {
                Vector3 sh = B("UpperArm_R").position;
                Vector3 gap = v.RacketHead - sh;
                float over = gap.magnitude - ComfortReach;
                ReachShift = 0;
                if (over > 0)
                {
                    Vector3 shift = gap.normalized * Mathf.Min(over, MaxBodyShift);
                    shift.y = Mathf.Min(shift.y, 0f);                  // never jump up to it
                    pelvis.position += shift;
                    ReachShift = shift.magnitude;
                }
            }

            // ---------------- legs ----------------
            float width = Mathf.Lerp(0.11f, 0.19f, 1f - Mathf.InverseLerp(ReadyHip, GateA.Hip, _hip));
            Vector3 vdir = speed > 0.1f ? new Vector3(v.Velocity.x, 0, v.Velocity.z) / speed : fwd;
            float amp = Mathf.Min(0.34f, 0.075f * speed), lift = Mathf.Min(0.12f, 0.03f * speed);
            float ph = _gait * Mathf.PI * 2f;
            float lungeLen = LungeLength[Mathf.Clamp(v.Reach, 0, 3)] * Mathf.Clamp01(_leanToBall * 3f);
            for (int side = 0; side < 2; side++)
            {
                string s = side == 0 ? "R" : "L";
                float sgn = side == 0 ? 1f : -1f;
                float phase = ph + side * Mathf.PI;
                Vector3 foot = v.Pos + right * (sgn * width) + vdir * (amp * Mathf.Sin(phase));
                foot.y = GateA.Ankle + lift * Mathf.Max(0f, Mathf.Cos(phase));
                // Lunge: the racket-side (right) leg drives toward the ball, the other stays back.
                if (lungeLen > 0.01f)
                    foot += side == 0 ? toRacket * lungeLen : -toRacket * (lungeLen * 0.45f);
                Vector3 knee = foot + fwd * 0.5f + up * 0.45f + right * (sgn * 0.08f);
                TwoBoneIK.Solve(B("Thigh_" + s), B("Shin_" + s), B("Foot_" + s), foot, knee);
                B("Foot_" + s).rotation = baseRot;                // soles flat
            }

            // ---------------- racket on the simulated racket; hand on the racket ----------------
            Vector3 sweet = v.RacketHead;
            Vector3 n = v.RacketFaceNormal.sqrMagnitude > 1e-6f ? v.RacketFaceNormal.normalized : fwd;
            Transform shoulderR = B("UpperArm_R");
            Vector3 axis = ChooseRacketAxis(sweet, n, shoulderR.position, up);
            _prevRacketAxis = axis; _hasPrevAxis = true;

            // Racket: local -Y = grip axis (butt -> tip), local X = string normal; sweet spot on the sim head.
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
                if (_hasPrevAxis) { float a = Vector3.Angle(u, _prevRacketAxis) * Mathf.Deg2Rad; cost += 0.15f * a * a; }
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
