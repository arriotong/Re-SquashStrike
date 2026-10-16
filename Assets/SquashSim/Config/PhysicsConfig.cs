// ===========================================================
// Assets/SquashSim/Config/PhysicsConfig.cs
// Port of lab/config/PhysicsConfig.js.
//
// SINGLE SOURCE OF TRUTH for every tunable simulation number. Nothing
// else in SquashSim may hardcode one. Field names are the JS keys in
// PascalCase, so a config JSON exported from the lab (or written by a
// tuning panel) loads with a case-insensitive mapper.
//
// PhysicsConfig.Current is a process-wide mutable instance, exactly like
// the lab's module singleton. That keeps the port line-for-line; a
// per-simulation config can replace it once two differently tuned sims
// ever need to run in one process.
//
// These are STARTING values, not locked balance values.
// ===========================================================

using System;

namespace SquashSim
{
    public interface ICurvePoint { double Key { get; } double Mul { get; } }

    public sealed class SpeedPoint : ICurvePoint
    {
        public double Speed; public double Mul;
        public SpeedPoint() { }
        public SpeedPoint(double speed, double mul) { Speed = speed; Mul = mul; }
        double ICurvePoint.Key { get { return Speed; } }
        double ICurvePoint.Mul { get { return Mul; } }
    }

    public sealed class TempPoint : ICurvePoint
    {
        public double Temp; public double Mul;
        public TempPoint() { }
        public TempPoint(double temp, double mul) { Temp = temp; Mul = mul; }
        double ICurvePoint.Key { get { return Temp; } }
        double ICurvePoint.Mul { get { return Mul; } }
    }

    public sealed class TempState
    {
        public string Name; public double Below;
        public TempState() { }
        public TempState(string name, double below) { Name = name; Below = below; }
    }

    public sealed class SimSection { public double FixedDt; public int MaxStepsPerFrame; public int MaxCollisionIterations; }
    public sealed class WorldSection { public double Gravity; public double DragK; public double MagnusK; }
    public sealed class RenderSection { public double BallVisualScale; }

    public sealed class BallSection
    {
        public double Radius, Mass, InertiaFactor, SpinDampingPerSecond, SleepSpeed;
    }

    public sealed class RestitutionSection
    {
        public double MinRestitution, MaxRestitution;
        public SpeedPoint[] SpeedResponse;
        public double RestingNormalSpeed;
    }

    public sealed class TemperatureSection
    {
        public double Initial, Min, Max, Ambient;
        public double HeatingMultiplier, HeatPerJoule, RacketHeatScale, WallHeatScale, FloorHeatScale, MinHeatEnergy;
        public double CoolingRate;
        public TempPoint[] Response;
        public TempState[] States;
    }

    public sealed class SurfaceProps
    {
        public double Restitution, Friction, SpinGrip, RollingResistance;
        public SurfaceProps() { }
        public SurfaceProps(double restitution, double friction, double spinGrip, double rollingResistance)
        {
            Restitution = restitution; Friction = friction; SpinGrip = spinGrip; RollingResistance = rollingResistance;
        }
    }

    public sealed class SurfacesSection
    {
        public SurfaceProps Floor, FrontWall, SideWall, BackWall, Tin, Ceiling, OutBackstop;

        /// <summary>Lookup by the lab's surfaceClass string. Unknown classes fall back to floor, as in JS.</summary>
        public SurfaceProps Get(string surfaceClass)
        {
            switch (surfaceClass)
            {
                case "floor": return Floor;
                case "frontWall": return FrontWall;
                case "sideWall": return SideWall;
                case "backWall": return BackWall;
                case "tin": return Tin;
                case "ceiling": return Ceiling;
                case "outBackstop": return OutBackstop;
                default: return Floor;
            }
        }
    }

    public sealed class PlayerSection
    {
        public double Radius, Height, MaxSpeed, Acceleration, Braking, AdjustSpeed, AdjustRange, DirectionChangeCost, TurnRate;
    }

    public sealed class SwingProfile
    {
        public double HeadSpeed, Duration, FacePitch, PathRise;
        public SwingProfile() { }
        public SwingProfile(double headSpeed, double duration, double facePitch, double pathRise)
        {
            HeadSpeed = headSpeed; Duration = duration; FacePitch = facePitch; PathRise = pathRise;
        }
    }

    public sealed class SwingProfiles
    {
        public SwingProfile Neutral, Touch, Attack, Lift;

        public SwingProfile Get(string name)
        {
            switch (name)
            {
                case "neutral": return Neutral;
                case "touch": return Touch;
                case "attack": return Attack;
                case "lift": return Lift;
                default: return null;
            }
        }
    }

    public sealed class RacketSection
    {
        public double HeadRadius, SweetSpotRadius, OffCentrePenalty, Reach, MinReach, MaxReach;
        public double ShoulderHeight, RestHeight, Restitution, Friction, SpinGrip;
        public double MinContactHeight, MaxContactHeight, FacePitchMin, FacePitchMax;
        public SwingProfiles Swing;
        public double TapMaxSeconds, PrepMinFraction, PrepFullSeconds, CooldownSeconds, ContactWindow;
    }

    public sealed class MovementAssistSection
    {
        public bool Enabled;
        public double Range, MaxSpeed, MaxTravel, Strength, Responsiveness, PreferredSpacing, MinSpacing;
    }

    public sealed class BalanceCost { public double Normal, Lunge, DeepLunge, Stretch; }

    public sealed class ReachSection
    {
        public double Normal, Lunge, DeepLunge, Stretch;
        public BalanceCost BalanceCost;
    }

    public sealed class PredictionSection { public double HorizonSeconds, StepSeconds; public int RefreshEverySteps; }

    public sealed class AimSection
    {
        public double MouseSensitivity, StickSensitivity, Deadzone, HorizontalResponse, VerticalResponse;
        public double RecenterSpeed, MaxBias, MaxYaw, RallyHeight, LowHeight, HighHeight;
        public bool ShowDebugIntent, ShowDebugLabel;
    }

    public sealed class QuickStroke { public double PaceScale, DurationScale, PrepQuality; }
    public sealed class PreparedStroke { public double PaceScale, DurationScale, MinPrepQuality, MaxPrepQuality; }

    public sealed class StrokesSection
    {
        public double TapThresholdSeconds, PrepFullSeconds;
        public QuickStroke Quick;
        public PreparedStroke Prepared;
    }

    public sealed class SwingPlanSection
    {
        public double HighBias, LowBias, MinQualityPaceScale, RushedPaceScale, VolleyPaceScale, VolleyDurationScale;
    }

    public sealed class CommitmentSection
    {
        public double SetWindowSeconds, BodyRatePrep, BodyRateSet, FaceRatePrep, FaceRateSet, FaceRateAccel;
        public double MaxFaceOffset, MaxLateFaceCorrection, MaxBodyYaw, RangeQualityFloor;
    }

    public sealed class InputTimingSection
    {
        public double BufferSeconds, MaxLeadSeconds, LateToleranceSeconds, PreparedTimingAssist;
    }

    public sealed class AssistSection
    {
        public bool Enabled;
        public double Radius, MaxCorrection, Strength, TimingWindow;
    }

    public sealed class CameraSection
    {
        public double Height, Distance, Fov, LookAtHeight, LookAtDepth, LateralFollow, FollowSmoothing;
    }

    public sealed class NickSection { public double DetectHeight, MinNormalSpeed; }

    public sealed class SimConfig
    {
        public SimSection Sim;
        public WorldSection World;
        public RenderSection Render;
        public BallSection Ball;
        public RestitutionSection Restitution;
        public TemperatureSection Temperature;
        public SurfacesSection Surfaces;
        public PlayerSection Player;
        public RacketSection Racket;
        public MovementAssistSection MovementAssist;
        public ReachSection Reach;
        public PredictionSection Prediction;
        public AimSection Aim;
        public StrokesSection Strokes;
        public SwingPlanSection SwingPlan;
        public CommitmentSection Commitment;
        public InputTimingSection InputTiming;
        public AssistSection Assist;
        public CameraSection Camera;
        public NickSection Nick;

        /// <summary>The shipped defaults. Must equal lab/config/PhysicsConfig.js defaults()
        /// (SimParity checks this against every trace's config).</summary>
        public static SimConfig Defaults()
        {
            return new SimConfig
            {
                Sim = new SimSection { FixedDt = 1.0 / 240, MaxStepsPerFrame = 12, MaxCollisionIterations = 8 },
                World = new WorldSection { Gravity = 9.81, DragK = 0.0157, MagnusK = 0.0 },
                Render = new RenderSection { BallVisualScale = 1.45 },
                Ball = new BallSection
                {
                    Radius = 0.0203, Mass = 0.024, InertiaFactor = 0.4, SpinDampingPerSecond = 0.35, SleepSpeed = 0.05
                },
                Restitution = new RestitutionSection
                {
                    MinRestitution = 0.02,
                    MaxRestitution = 0.95,
                    SpeedResponse = new[]
                    {
                        new SpeedPoint(0.0, 1.10), new SpeedPoint(2.0, 1.00), new SpeedPoint(10.0, 0.90),
                        new SpeedPoint(25.0, 0.80), new SpeedPoint(50.0, 0.72)
                    },
                    RestingNormalSpeed = 0.22
                },
                Temperature = new TemperatureSection
                {
                    Initial = 16.0, Min = 8.0, Max = 48.0, Ambient = 18.0,
                    HeatingMultiplier = 1.0, HeatPerJoule = 1.6, RacketHeatScale = 1.25, WallHeatScale = 1.0,
                    FloorHeatScale = 0.85, MinHeatEnergy = 0.02,
                    CoolingRate = 0.035,
                    Response = new[]
                    {
                        new TempPoint(8.0, 0.55), new TempPoint(16.0, 0.72), new TempPoint(24.0, 0.88),
                        new TempPoint(32.0, 1.00), new TempPoint(40.0, 1.08), new TempPoint(48.0, 1.13)
                    },
                    States = new[]
                    {
                        new TempState("Cold", 19.0), new TempState("Warming", 27.0),
                        new TempState("Match Ready", 37.0), new TempState("Hot", double.PositiveInfinity)
                    }
                },
                Surfaces = new SurfacesSection
                {
                    Floor = new SurfaceProps(0.50, 0.42, 0.55, 0.55),
                    FrontWall = new SurfaceProps(0.74, 0.28, 0.45, 0),
                    SideWall = new SurfaceProps(0.72, 0.28, 0.45, 0),
                    BackWall = new SurfaceProps(0.64, 0.18, 0.30, 0),
                    Tin = new SurfaceProps(0.35, 0.30, 0.35, 0),
                    Ceiling = new SurfaceProps(0.55, 0.30, 0.35, 0),
                    OutBackstop = new SurfaceProps(0.10, 0.80, 0.60, 2.0)
                },
                Player = new PlayerSection
                {
                    Radius = 0.30, Height = 1.80, MaxSpeed = 5.2, Acceleration = 34.0, Braking = 46.0,
                    AdjustSpeed = 2.2, AdjustRange = 0.85, DirectionChangeCost = 0.55, TurnRate = 14.0
                },
                Racket = new RacketSection
                {
                    HeadRadius = 0.085, SweetSpotRadius = 0.050, OffCentrePenalty = 0.38,
                    Reach = 0.76, MinReach = 0.40, MaxReach = 1.00,
                    ShoulderHeight = 1.32, RestHeight = 0.95, Restitution = 0.74, Friction = 0.40, SpinGrip = 0.85,
                    MinContactHeight = 0.05, MaxContactHeight = 2.25, FacePitchMin = -0.45, FacePitchMax = 0.95,
                    Swing = new SwingProfiles
                    {
                        Neutral = new SwingProfile(15.0, 0.200, 0.02, 0.10),
                        Touch = new SwingProfile(6.0, 0.270, 0.34, -0.06),
                        Attack = new SwingProfile(22.0, 0.165, 0.0, -0.10),
                        Lift = new SwingProfile(12.0, 0.240, 0.18, 0.42)
                    },
                    TapMaxSeconds = 0.18, PrepMinFraction = 0.55, PrepFullSeconds = 0.45,
                    CooldownSeconds = 0.10, ContactWindow = 0.55
                },
                MovementAssist = new MovementAssistSection
                {
                    Enabled = true, Range = 0.90, MaxSpeed = 1.30, MaxTravel = 0.45, Strength = 0.85,
                    Responsiveness = 6.0, PreferredSpacing = 0.72, MinSpacing = 0.45
                },
                Reach = new ReachSection
                {
                    Normal = 0.80, Lunge = 1.05, DeepLunge = 1.30, Stretch = 1.55,
                    BalanceCost = new BalanceCost { Normal = 0.0, Lunge = 0.22, DeepLunge = 0.45, Stretch = 0.70 }
                },
                Prediction = new PredictionSection { HorizonSeconds = 1.60, StepSeconds = 1.0 / 120, RefreshEverySteps = 4 },
                Aim = new AimSection
                {
                    MouseSensitivity = 0.0042, StickSensitivity = 1.0, Deadzone = 0.08,
                    HorizontalResponse = 1.35, VerticalResponse = 1.15, RecenterSpeed = 3.0, MaxBias = 1.0,
                    MaxYaw = 1.05, RallyHeight = 1.55, LowHeight = 0.90, HighHeight = 4.40,
                    ShowDebugIntent = true, ShowDebugLabel = true
                },
                Strokes = new StrokesSection
                {
                    TapThresholdSeconds = 0.17, PrepFullSeconds = 0.40,
                    Quick = new QuickStroke { PaceScale = 0.86, DurationScale = 0.82, PrepQuality = 0.45 },
                    Prepared = new PreparedStroke { PaceScale = 1.00, DurationScale = 1.00, MinPrepQuality = 0.55, MaxPrepQuality = 1.00 }
                },
                SwingPlan = new SwingPlanSection
                {
                    HighBias = 0.45, LowBias = -0.35, MinQualityPaceScale = 0.45, RushedPaceScale = 0.70,
                    VolleyPaceScale = 0.82, VolleyDurationScale = 0.80
                },
                Commitment = new CommitmentSection
                {
                    SetWindowSeconds = 0.14, BodyRatePrep = 7.0, BodyRateSet = 2.2, FaceRatePrep = 12.0,
                    FaceRateSet = 6.0, FaceRateAccel = 3.2, MaxFaceOffset = 0.40, MaxLateFaceCorrection = 0.22,
                    MaxBodyYaw = 0.70, RangeQualityFloor = 0.35
                },
                InputTiming = new InputTimingSection
                {
                    BufferSeconds = 0.55, MaxLeadSeconds = 1.20, LateToleranceSeconds = 0.18, PreparedTimingAssist = 0.16
                },
                Assist = new AssistSection { Enabled = true, Radius = 0.42, MaxCorrection = 0.30, Strength = 0.85, TimingWindow = 0.10 },
                Camera = new CameraSection
                {
                    Height = 3.95, Distance = 6.60, Fov = 52, LookAtHeight = 1.55, LookAtDepth = 3.20,
                    LateralFollow = 0.22, FollowSmoothing = 2.4
                },
                Nick = new NickSection { DetectHeight = 0.09, MinNormalSpeed = 2.5 }
            };
        }
    }

    /// <summary>The live config every system reads through, plus the lab's shared helpers.</summary>
    public static class PhysicsConfig
    {
        public static SimConfig Current = SimConfig.Defaults();

        public static void ResetToDefaults() { Current = SimConfig.Defaults(); }

        /// <summary>Piecewise-linear curve read (lab sampleCurve).</summary>
        public static double SampleCurve(ICurvePoint[] points, double x)
        {
            if (points == null || points.Length == 0) return 1;
            if (x <= points[0].Key) return points[0].Mul;
            ICurvePoint last = points[points.Length - 1];
            if (x >= last.Key) return last.Mul;
            for (int i = 0; i < points.Length - 1; i++)
            {
                ICurvePoint a = points[i];
                ICurvePoint b = points[i + 1];
                if (x >= a.Key && x <= b.Key)
                {
                    double span = b.Key - a.Key;
                    double t = span <= 0 ? 0 : (x - a.Key) / span;
                    return a.Mul + (b.Mul - a.Mul) * t;
                }
            }
            return last.Mul;
        }

        /// <summary>Lab clamp(): v &lt; lo ? lo : (v &gt; hi ? hi : v).</summary>
        public static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
