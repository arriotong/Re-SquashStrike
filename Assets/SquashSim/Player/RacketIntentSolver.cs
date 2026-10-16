// ===========================================================
// Assets/SquashSim/Player/RacketIntentSolver.cs
// Port of lab/player/RacketIntentSolver.js. The commitment curve, and
// therefore deception.
//
//   PREPARATION  body and face follow aim quickly (and visibly)
//   SET          last moments before the swing: slower
//   ACCELERATION path locked to what the body showed; only the face
//                (wrist/forearm) can still turn, slowly and a little
//   CONTACT      whatever the racket is doing is what hits the ball
//
// No random penalty anywhere: a late change becomes a face turned away
// from the head's travel, and ordinary contact physics does the rest.
// ===========================================================

namespace SquashSim
{
    public static class CommitPhase
    {
        public const string Ready = "ready";
        public const string Preparation = "preparation";
        public const string Set = "set";
        public const string Acceleration = "acceleration";
        public const string Contact = "contact";
    }

    public sealed class RacketIntent
    {
        public string Phase = CommitPhase.Ready;

        public double BodyYaw;          // shoulders/torso: what an opponent reads
        public double FaceYaw;          // actual racket face
        public double FacePitch = 0.1;
        public double PathYaw;          // swing path, locked at swing start
        public double FaceYawAtSwingStart;

        public double RangeScale = 1;
        public bool FaceLimited;

        // Deception bookkeeping (debug only).
        public bool StrokeActive;
        public double InitialYaw;
        public double SettledYaw;
        public double LastChangeAt;
        public double Clock;
        public DeceptionRecord LastRecord;

        public void Reset()
        {
            Phase = CommitPhase.Ready;
            BodyYaw = 0; FaceYaw = 0; FacePitch = 0.1; PathYaw = 0; FaceYawAtSwingStart = 0;
            RangeScale = 1; FaceLimited = false;
            StrokeActive = false; InitialYaw = 0; SettledYaw = 0; LastChangeAt = 0; Clock = 0;
            LastRecord = null;
        }
    }

    public static class RacketIntentSolver
    {
        static double Approach(double current, double target, double maxStep)
        {
            double d = target - current;
            if (System.Math.Abs(d) <= maxStep) return target;
            return current + DMath.Sign(d) * maxStep;
        }

        static double ClampAround(double value, double centre, double halfRange)
        {
            return DMath.Max(centre - halfRange, DMath.Min(centre + halfRange, value));
        }

        /// <param name="quality">0..1 balance/reach quality</param>
        public static RacketIntent Step(RacketIntent ri, AimState aim, SwingController swing, double quality, double dt)
        {
            var c = PhysicsConfig.Current.Commitment;
            ri.Clock += dt;

            // ---------- which phase of commitment are we in? ----------
            string s = swing.State;
            string phase;
            if (s == SwingStates.Swinging)
            {
                phase = (!swing.HasContacted && swing.Phase < 0.5)
                    ? CommitPhase.Acceleration : CommitPhase.Contact;
            }
            else if (s == SwingStates.Preparing || s == SwingStates.Scheduled)
            {
                double? until = swing.ExpectedStartIn;
                phase = (until.HasValue && until.Value <= c.SetWindowSeconds)
                    ? CommitPhase.Set : CommitPhase.Preparation;
            }
            else
            {
                phase = CommitPhase.Ready;
            }

            // Swing just started: lock the path to what the body was showing.
            if (phase == CommitPhase.Acceleration && ri.Phase != CommitPhase.Acceleration)
            {
                ri.PathYaw = ri.BodyYaw;
                ri.FaceYawAtSwingStart = ri.FaceYaw;
            }

            bool strokeLive = s == SwingStates.Preparing || s == SwingStates.Scheduled || s == SwingStates.Swinging;
            if (strokeLive && !ri.StrokeActive)
            {
                ri.StrokeActive = true;
                ri.InitialYaw = aim.DesiredYaw;
                ri.SettledYaw = aim.DesiredYaw;
                ri.LastChangeAt = ri.Clock;
            }
            else if (!strokeLive)
            {
                ri.StrokeActive = false;
            }
            if (ri.StrokeActive && System.Math.Abs(aim.DesiredYaw - ri.SettledYaw) > 0.03)
            {
                ri.SettledYaw = aim.DesiredYaw;
                ri.LastChangeAt = ri.Clock;
            }

            ri.Phase = phase;

            // ---------- physical range available ----------
            ri.RangeScale = c.RangeQualityFloor + (1 - c.RangeQualityFloor) * DMath.Max(0, DMath.Min(1, quality));
            double maxBody = c.MaxBodyYaw * ri.RangeScale;
            double maxFaceOff = c.MaxFaceOffset * ri.RangeScale;

            // ---------- follow rates for this phase ----------
            double bodyRate, faceRate;
            switch (phase)
            {
                case CommitPhase.Set: bodyRate = c.BodyRateSet; faceRate = c.FaceRateSet; break;
                case CommitPhase.Acceleration: bodyRate = 0; faceRate = c.FaceRateAccel; break;
                case CommitPhase.Contact: bodyRate = 0; faceRate = 0; break;
                default: bodyRate = c.BodyRatePrep; faceRate = c.FaceRatePrep; break;
            }

            // ---------- body ----------
            double bodyTarget = DMath.Max(-maxBody, DMath.Min(maxBody, aim.DesiredYaw));
            ri.BodyYaw = Approach(ri.BodyYaw, bodyTarget, bodyRate * dt);

            // ---------- racket face ----------
            double faceCentre = phase == CommitPhase.Acceleration || phase == CommitPhase.Contact
                ? ri.PathYaw : ri.BodyYaw;
            double faceTarget = ClampAround(aim.DesiredYaw, faceCentre, maxFaceOff);
            if (phase == CommitPhase.Acceleration)
            {
                faceTarget = ClampAround(faceTarget, ri.FaceYawAtSwingStart, c.MaxLateFaceCorrection);
            }
            ri.FaceLimited = System.Math.Abs(faceTarget - aim.DesiredYaw) > 0.01;
            ri.FaceYaw = Approach(ri.FaceYaw, faceTarget, faceRate * dt);

            ri.FacePitch = Approach(ri.FacePitch, aim.DesiredPitch, faceRate * dt);

            if (phase == CommitPhase.Ready || phase == CommitPhase.Preparation || phase == CommitPhase.Set)
            {
                ri.PathYaw = ri.BodyYaw;
            }

            return ri;
        }

        /// <summary>Snapshot the deception figures at contact. Debug only.</summary>
        public static DeceptionRecord RecordDeception(RacketIntent ri, AimState aim)
        {
            ri.LastRecord = new DeceptionRecord
            {
                InitialYawDeg = ri.InitialYaw * 180 / DMath.PI,
                BodyAtSwingDeg = ri.PathYaw * 180 / DMath.PI,
                DesiredAtContactDeg = aim.DesiredYaw * 180 / DMath.PI,
                FaceAtContactDeg = ri.FaceYaw * 180 / DMath.PI,
                AimChangeDeg = (aim.DesiredYaw - ri.InitialYaw) * 180 / DMath.PI,
                FaceOffPathDeg = (ri.FaceYaw - ri.PathYaw) * 180 / DMath.PI,
                LateBySeconds = ri.Clock - ri.LastChangeAt,
                ShortfallDeg = (aim.DesiredYaw - ri.FaceYaw) * 180 / DMath.PI,
                Achieved = System.Math.Abs(aim.DesiredYaw - ri.FaceYaw) < 0.035,
                RangeScale = ri.RangeScale
            };
            return ri.LastRecord;
        }
    }
}
