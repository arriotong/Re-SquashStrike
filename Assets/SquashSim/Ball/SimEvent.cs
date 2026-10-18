// ===========================================================
// Assets/SquashSim/Ball/SimEvent.cs
// The events a fixed step can emit: a ball impact on a court surface,
// or a racket contact. One class with a Type tag, mirroring the lab's
// plain event objects, so observers (referee, audio, stats, debug) can
// consume one stream.
// ===========================================================

namespace SquashSim
{
    public enum SimEventType { Impact, RacketContact, BodyHit, PlayerContact }

    public sealed class DeceptionRecord
    {
        public double InitialYawDeg, BodyAtSwingDeg, DesiredAtContactDeg, FaceAtContactDeg;
        public double AimChangeDeg, FaceOffPathDeg, LateBySeconds, ShortfallDeg;
        public bool Achieved;
        public double RangeScale;
    }

    public sealed class SimEvent
    {
        public SimEventType Type;
        public V3 Point;
        public double Time;
        public double Restitution;
        public double EnergyLost;
        public double Temperature;
        /// <summary>Racket contact: who struck. BodyHit: whose body. PlayerContact: the first player.</summary>
        public int PlayerIndex = -1;
        /// <summary>PlayerContact: the other player.</summary>
        public int OtherIndex = -1;
        /// <summary>BodyHit: ball centre and velocity just before the body deflected it.</summary>
        public V3 InPos, InVel;

        // ---- impact ----
        public string SurfaceId;
        public string SurfaceClass;
        public double ImpactSpeed;
        public double NormalSpeed;
        public bool Resting;
        public bool Nick;

        // ---- racket contact ----
        public string Stroke;
        public string Mode;
        public string Technique;
        public int Side;
        public bool Rushed;
        public double HeadSpeed;
        public V3 FaceNormal;
        public double OffCentre;
        public double OffCentreNorm;
        public double AssistApplied;
        public double BallSpeedOut;
        public V3 OutVel;
        // Filled by TestPlayerController after contact:
        public string Label;
        public string ReachState;
        public double Balance;
        public DeceptionRecord Deception;
        public double TimingCorrection;
        /// <summary>Racket contact: how far through the swing (0..1) the ball was met; 0.5 = the ideal moment. -1 = n/a.</summary>
        public double SwingPhaseAtContact = -1;
        /// <summary>Simple hitting system: how clean the hit was, 1 = in the sweet spot .. 0 = at full stretch. -1 = n/a.</summary>
        public double ArcadeQuality = -1;
        /// <summary>The simple hitting system: 0..1 how early the striker was set for the ball (-1: not that system).</summary>
        public double Earliness = -1;
        /// <summary>The simple hitting system: seconds from letting go of Hit to the contact (-1: not that system).
        /// About zero = let go just as the ball arrived.</summary>
        public double ReleaseWait = -1;
        /// <summary>The simple hitting system: how far the ball was from the athlete at contact (m; the sweet spot is
        /// ArcadeHit.SweetSpot - nearer = held on too long, further = let go early). -1: not that system.</summary>
        public double ArcadeDistance = -1;
    }
}
