// ===========================================================
// Assets/SquashSim/Ball/BallState.cs
// Port of lab/ball/BallState.js. The complete physical state of one
// squash ball - pure data, snapshot/serialise/replay friendly.
// Nothing here knows what a "drop" or a "boast" is.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class NickRecord
    {
        public string Surface; public V3 Point; public double Time;
    }

    public sealed class RacketTouch
    {
        public string By; public double Time; public double Speed; public string Stroke; public string Technique;
    }

    public sealed class BallState
    {
        const int IMPACT_HISTORY_MAX = 32;

        // --- kinematics ---
        public readonly V3 Pos = new V3(0, 1.0, 5.0);
        public readonly V3 Vel = new V3(0, 0, 0);
        public readonly V3 Omega = new V3(0, 0, 0);        // rad/s

        // --- thermal ---
        public double Temperature = PhysicsConfig.Current.Temperature.Initial;

        // --- contact bookkeeping (observational, never physical) ---
        public string LastCollisionSurface;
        public string LastCollisionClass;
        public double LastImpactSpeed;
        public double LastImpactNormalSpeed;
        public readonly V3 LastImpactPoint = new V3(0, 0, 0);
        public double LastImpactEnergyLost;
        public double LastImpactRestitution;
        public RacketTouch LastRacketContact;
        /// <summary>Game profile: this shot (a drive, kill or boast on the simple hitting system) comes off the front
        /// wall with little of its rise left (ArcadeHitSection.FrontWallRiseKeep). Cleared at that impact.</summary>
        public bool DipOffFrontWall;
        public NickRecord LastNick;

        // --- counters ---
        public int FloorBounces;
        /// <summary>Floor bounces since the front wall or a racket (volley detection only).</summary>
        public int FloorSinceFrontWall;
        public int WallImpacts;
        public int TotalImpacts;

        // --- simulation flags ---
        public bool Resting;
        public bool Asleep;
        public double SimTime;

        public readonly List<SimEvent> ImpactHistory = new List<SimEvent>();

        /// <summary>Reset to a launch state without reallocating (lab resetBallState).</summary>
        public void Reset(V3 pos, V3 vel, V3 omega, double? temperature)
        {
            if (pos != null) Pos.CopyFrom(pos); else Pos.Set(0, 1.0, 5.0);
            if (vel != null) Vel.CopyFrom(vel); else Vel.Set(0, 0, 0);
            if (omega != null) Omega.CopyFrom(omega); else Omega.Set(0, 0, 0);

            Temperature = temperature.HasValue ? temperature.Value : PhysicsConfig.Current.Temperature.Initial;

            LastCollisionSurface = null;
            LastCollisionClass = null;
            LastImpactSpeed = 0;
            LastImpactNormalSpeed = 0;
            LastImpactEnergyLost = 0;
            LastImpactRestitution = 0;
            LastRacketContact = null;
            DipOffFrontWall = false;
            LastNick = null;

            FloorBounces = 0;
            FloorSinceFrontWall = 0;
            WallImpacts = 0;
            TotalImpacts = 0;

            Resting = false;
            Asleep = false;
            SimTime = 0;
            ImpactHistory.Clear();
        }

        public void RecordImpact(SimEvent record)
        {
            ImpactHistory.Add(record);
            if (ImpactHistory.Count > IMPACT_HISTORY_MAX) ImpactHistory.RemoveAt(0);
        }

        public double Speed() { return System.Math.Sqrt(Vel.X * Vel.X + Vel.Y * Vel.Y + Vel.Z * Vel.Z); }

        public double SpinRate() { return System.Math.Sqrt(Omega.X * Omega.X + Omega.Y * Omega.Y + Omega.Z * Omega.Z); }

        /// <summary>Kinetic energy (linear + rotational), joules.</summary>
        public double KineticEnergy()
        {
            var cfg = PhysicsConfig.Current.Ball;
            double m = cfg.Mass;
            double r = cfg.Radius;
            double I = cfg.InertiaFactor * m * r * r;
            double v = Speed();
            double w = SpinRate();
            return 0.5 * m * v * v + 0.5 * I * w * w;
        }
    }
}
