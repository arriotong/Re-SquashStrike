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
        /// <summary>
        /// Game only (null in the lab = off): how much of a HOT ball's extra liveliness survives at a
        /// given normal impact speed. A warm double yellow bounces higher on soft impacts, but still
        /// dies when it is hit hard into a wall (and a cold one is no deader than a warm one at pace).
        /// Sampled by normal speed: scales the Response's departure from 1 (1 = the full effect).
        /// </summary>
        public SpeedPoint[] HotSpeedFade;
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
        /// <summary>Game only (false in the lab): a scheduled swing keeps re-timing to the latest prediction
        /// until it starts, so running on toward the ball does not leave the swing early.</summary>
        public bool RetimeScheduled;
        /// <summary>Game only (false in the lab): a held stroke released with no contact predicted waits
        /// for the ball (it is armed) instead of swinging at air.</summary>
        public bool NoAirSwings;
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

    // ---------------- match-only sections (M2) ----------------
    // The HTML lab has no equivalent of these; LabSimulation never reads them,
    // which is what keeps the lab parity traces bit-identical.

    /// <summary>Ball hitting a player's body (a vertical capsule of Player.Radius / Player.Height).</summary>
    public sealed class BodySection
    {
        public bool Enabled;
        public double Restitution;          // normal bounce off a body: clothing and flesh are dead
        public double TangentialRetain;     // fraction of sliding speed kept along the body
        public double SpinRetain;
        public int SweepSamples;            // sub-samples of the ball's path per step (thin grazes at 70 m/s)
    }

    /// <summary>After a stroke the body needs time to recover; stretched strokes cost more.</summary>
    public sealed class RecoverySection
    {
        public double Normal, Lunge, DeepLunge, Stretch;    // seconds of reduced movement after contact
        public double RateScale;                             // acceleration multiplier while recovering
        public double SpeedScale;                            // top-speed multiplier while recovering
    }

    /// <summary>Momentum Focus = split-step: a hop timed to the opponent's contact.</summary>
    public sealed class SplitStepSection
    {
        public double Window;               // +-seconds around the opponent's racket contact
        public double BoostSeconds, BoostRateScale;
        public double PlantSeconds, PlantRateScale;        // mistimed: briefly planted
    }

    /// <summary>Player bodies cannot pass through each other.</summary>
    public sealed class PlayerContactSection
    {
        public bool Enabled;
        public double Softness;             // 0..1 share of the closing speed removed per contact
    }

    /// <summary>Fatigue (match only). Tuned from MatchBench rally data; see Match/Fatigue.cs.</summary>
    public sealed class FatigueSection
    {
        public bool Enabled;
        public double DrainRate;              // per second at full speed (scales with (speed/max)^2)
        public double LungeCost, DeepLungeCost, StretchCost;   // per stroke
        public double RecoverStanding;        // per second, below StandingSpeed during a rally
        public double StandingSpeed;
        public double RecoverBetweenRallies;  // per second while the rally is over / before the serve
        public double Floor;                  // never below this
        public double SpeedLoss;              // top speed and acceleration lose this much at zero stamina
        public double BalanceCostGain;        // reach balance costs gain this much at zero stamina
    }

    /// <summary>Rules, serve and match flow.</summary>
    public sealed class MatchSection
    {
        public double LineWidth;            // painted lines count as OUT (walls) / FAULT (serve floor lines)
        public double FootFaultTolerance;   // server's centre may be this far outside the box at contact
        public double ServeTossSpeed, ServeTossHeight, ServeTossForward, ServeTossSide;
        public double BetweenRallySeconds;
        public string Scoring;              // "par" | "english"
        public int ParPoints;               // 11, win by 2
        public int EnglishPoints;           // 9; at 8-8 the receiver may "set two" (to 10)
        public bool EnglishSetTwo;
        public int GamesToWin;
        public double TX, TZ;               // where players recover to (the T)
        public double StartTemperature;     // ball temperature at the first serve when the warm-up is skipped
        public bool AppealsEnabled = true;  // "let, please" (players and AI); the game turns it off for now
        public bool WarmUpEnabled;          // knock-up before the first serve (the ball starts cold)
        public double WarmUpSecondsEach;    // each player's half of the knock-up (real squash: 150 s)
        public double WarmUpRefeedSeconds;  // a dead ball is fed again after this long
        public double BetweenGamesSeconds;  // break between games (real squash: 90 s); the ball cools
        public bool AutoNextGame;           // after the break, the next game starts by itself
    }

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

        // Match-only (M2). Never read by LabSimulation.
        public BodySection Body;
        public RecoverySection Recovery;
        public SplitStepSection SplitStep;
        public PlayerContactSection PlayerContact;
        public MatchSection Match;
        public FatigueSection Fatigue;
        /// <summary>Game only (null in the lab): the visible lunge step (LungeStep).</summary>
        public LungeSection Lunge;
        /// <summary>Game only (null in the lab): front-wall target aim for local players (WallAim).</summary>
        public WallAimSection WallAim;
        /// <summary>Game only (null in the lab): the simple hitting system for local players (ArcadeStroke).</summary>
        public ArcadeHitSection ArcadeHit;

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
                Nick = new NickSection { DetectHeight = 0.09, MinNormalSpeed = 2.5 },

                Body = new BodySection { Enabled = true, Restitution = 0.15, TangentialRetain = 0.45, SpinRetain = 0.5, SweepSamples = 4 },
                Recovery = new RecoverySection
                {
                    Normal = 0.0, Lunge = 0.12, DeepLunge = 0.22, Stretch = 0.35, RateScale = 0.35, SpeedScale = 0.6
                },
                SplitStep = new SplitStepSection
                {
                    Window = 0.12, BoostSeconds = 0.25, BoostRateScale = 1.3, PlantSeconds = 0.10, PlantRateScale = 0.2
                },
                PlayerContact = new PlayerContactSection { Enabled = true, Softness = 0.5 },
                Match = new MatchSection
                {
                    LineWidth = 0.05, FootFaultTolerance = 0.15,
                    ServeTossSpeed = 3.6, ServeTossHeight = 1.0, ServeTossForward = 0.35, ServeTossSide = 0.35,
                    BetweenRallySeconds = 2.0,
                    Scoring = "par", ParPoints = 11, EnglishPoints = 9, EnglishSetTwo = true, GamesToWin = 1,
                    TX = 0.0, TZ = 5.0,
                    StartTemperature = 30.0,
                    WarmUpEnabled = false, WarmUpSecondsEach = 45.0, WarmUpRefeedSeconds = 0.6,
                    BetweenGamesSeconds = 45.0, AutoNextGame = true
                },
                Fatigue = new FatigueSection
                {
                    Enabled = true, DrainRate = 0.075,
                    LungeCost = 0.012, DeepLungeCost = 0.024, StretchCost = 0.04,
                    RecoverStanding = 0.015, StandingSpeed = 1.0, RecoverBetweenRallies = 0.09,
                    Floor = 0.2, SpeedLoss = 0.15, BalanceCostGain = 0.6
                }
            };
        }
    }

    /// <summary>The live config every system reads through, plus the lab's shared helpers.</summary>
    public static class PhysicsConfig
    {
        public static SimConfig Current = SimConfig.Defaults();

        public static void ResetToDefaults() { Current = SimConfig.Defaults(); }

        /// <summary>
        /// The game's match ball on top of the lab defaults (the lab itself stays on Defaults(),
        /// which SimParity holds bit-identical to the JS lab).
        /// </summary>
        /// <summary>
        /// Everything the game changes on top of the lab defaults: the match ball and the M6
        /// controls/movement tuning. The lab itself stays on Defaults().
        /// </summary>
        public static void UseGameProfile()
        {
            UseMatchBall();
            UseGameReach();
            UseGameMovement();
            UseGameAim();
            UseGameSwing();
            UseArcadeHit();
            Current.Match.AppealsEnabled = false;                     // let calls: to be reworked
            // Presentation only: the drawn ball is bigger than the real one (physics radius unchanged).
            Current.Render.BallVisualScale = 2.2;                    // lab 1.45
        }

        /// <summary>
        /// The simple hitting system for local players, after the original SquashStrike: hold to charge,
        /// release to hit - if the ball is within reach it is hit, sent to your aim target; how close it was
        /// sets how clean. (The AI keeps the physical swing.)
        /// </summary>
        public static void UseArcadeHit()
        {
            Current.ArcadeHit = new ArcadeHitSection
            {
                Reach = 1.5, SweetSpot = 0.85, MinHeight = 0.04, MaxHeight = 2.6, Grace = 0.22, ChargeFull = 0.9,
                DriveSpeed = 28.0, ChargeBonus = 0.6, LobSpeed = 14.0, LobHeight = 3.0, TouchSpeed = 11.0,
                MaxSpread = 0.7, GrossMissChance = 0.12, LungeMax = 0.7, LungeSpeed = 4.0, BackWallSafeHeight = 1.4,
                // The original SquashStrike: shot type from aim height, dive, sprint.
                LowHeight = 1.15, KillCharge = 0.25, KillHeightAboveTin = 0.10, DropHeightAboveTin = 0.17, DropSpeed = 9.0,
                DiveSeconds = 0.42, DiveSpeedScale = 1.85, DiveRecovery = 1.1, DiveRecoverySpeed = 0.65, DiveReachScale = 1.5,
                DiveStamina = 0.12, DiveMinStamina = 0.2, SprintScale = 1.3, SprintDrain = 0.05, WindedStamina = 0.3,
                ServeSpeed = 24.0, LobRise = 1.5, LobMaxSpeed = 26.0, LobDepth = 8.0, LobWallMax = 3.3,
                BoastPaceScale = 0.9, BoastMaxHeight = 1.8, BoastMinHeight = 0.8, BoastCross = 0.3,
                // Set early (in position before the ball arrives): up to 75% less spread, 85% fewer mishits.
                EarlyFull = 0.45, EarlySpreadCut = 0.75, EarlyMishitCut = 0.85, EarlyNear = 0.35,
                // The drop: about 0.45 s to the front wall (firm from the back, gentle from the front).
                DropFlightTime = 0.45, DropSpeedMin = 10.0, DropSpeedMax = 17.0
            };
        }

        /// <summary>
        /// M8 swing registration (press audit, 2026-10-17): the swing follows the ball as you move, and a
        /// stroke never fires at thin air. Human-style presses that connect: 47% -> ~81%.
        /// </summary>
        public static void UseGameSwing()
        {
            Current.InputTiming.RetimeScheduled = true;
            Current.InputTiming.NoAirSwings = true;
        }

        /// <summary>
        /// Front-wall target aim for local players: the stick picks a point on the wall that stays put
        /// as you move, and the shot is solved to really go there. The lowest point is kept clear of
        /// the tin (0.48 m): playable first, realistic second.
        /// </summary>
        public static void UseGameAim()
        {
            Current.WallAim = new WallAimSection
            {
                HalfWidth = 2.75, NeutralHeight = 1.7, MinHeight = 0.85, MaxHeight = 4.0,
                RefreshSteps = 4, Iterations = 3,
                BoastZone = 0.15, BoastHeightLow = 1.1, BoastHeightHigh = 2.4
            };
        }

        /// <summary>
        /// M6 movement. The lab's acceleration / braking (34 / 46 m/s2) are kept: the "instant" look came
        /// from the animation (now planted steps and a rate-limited turn), and slower values cost the AI
        /// its accuracy with no published squash figure to justify them (MatchBench --feel, 2026-10-17).
        /// The movement changes are in how athletes move: the AI arrives (eases off inside braking
        /// distance) and reads low balls (BallReader), which brings mean rally speed near PSA's 1.88 m/s.
        /// </summary>
        public static void UseGameMovement()
        {
            // Quicker off the mark (playtest 2026-10-17): +12% acceleration and braking. Top speed stays the lab's
            // 5.2 m/s (playtest 2026-10-18: 5.8 felt too fast once nothing slows a local player down).
            var p = Current.Player;
            p.Acceleration *= 1.12;
            p.Braking *= 1.12;
        }

        /// <summary>
        /// M6 reach balance: the athlete visibly lunges to far balls instead of reaching them with an
        /// impossibly long arm, the total reach is a little shorter, and the contact assist's pull on
        /// the ball is small.
        /// </summary>
        public static void UseGameReach()
        {
            var c = Current;
            c.Reach.Stretch = 1.45;                                   // lab 1.55 (from the body, at prediction time)
            c.Lunge = new LungeSection { Enabled = true, ArmReach = 0.95, MaxStep = 0.55 };
            c.Assist.Radius = 0.25;                                   // lab 0.42
            c.Assist.MaxCorrection = 0.12;                            // lab 0.30
        }

        public const double GameFrontWallRestitution = 0.62;
        public const double GamePaceScale = 1.15;

        public static void UseMatchBall()
        {
            var c = Current;
            // PSA pace: a hard drive leaves the racket at ~40-50 m/s (the lab swings were club pace).
            // Hit ~15% harder than that (playtest 2026-10-18) to keep the depth off the deader front wall below.
            c.Racket.Swing.Neutral.HeadSpeed = 24.0 * GamePaceScale;
            c.Racket.Swing.Attack.HeadSpeed = 37.0 * GamePaceScale;
            // A deader front wall: the ball comes off it ~15% slower (playtest 2026-10-18; lab 0.74).
            c.Surfaces.FrontWall.Restitution = GameFrontWallRestitution;
            // (Lift - lobs and serves - keeps the lab's soft pace.)
            // A double yellow is dead at pace: restitution falls off further at high impact speed...
            c.Restitution.SpeedResponse = new[]
            {
                new SpeedPoint(0.0, 1.10), new SpeedPoint(2.0, 1.00), new SpeedPoint(10.0, 0.88),
                new SpeedPoint(25.0, 0.70), new SpeedPoint(50.0, 0.58)
            };
            // ...and warming it buys bounce height (margin), not pace off the walls.
            c.Temperature.HotSpeedFade = new[]
            {
                new SpeedPoint(0.0, 1.0), new SpeedPoint(6.0, 1.0), new SpeedPoint(15.0, 0.55),
                new SpeedPoint(30.0, 0.25), new SpeedPoint(50.0, 0.15)
            };
            // Runs at ~42-45 C in a rally (it sat on the 48 C cap).
            c.Temperature.HeatPerJoule = 0.22;
        }

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
