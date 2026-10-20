// ===========================================================
// Assets/SquashSim/Config/GameTuning.cs
// Every on-court number of the game, locked in one place.
//
// The lab keeps its own defaults (SimConfig.Defaults, held bit-identical
// to the JS lab by SimParity). The game profile (PhysicsConfig.UseGame
// Profile) is the lab plus these - nothing on court is tuned anywhere
// else, and nothing here changes at run time. Each value carries the
// playtest or bench that set it.
//
//   Ball         the match ball, the walls, pace
//   Movement     how athletes move and reach
//   Aim          the reticle on the front wall
//   Hit          the simple hitting system (both sides)
//   Shots        each shot type's pace and shape
//   Serve        tap = lob serve, hold = hard serve
//   Assist       Assisted controls
//   Ai           the opponents' difficulty and shot choice
//   Speed        game speed, slow motion
// ===========================================================

namespace SquashSim
{
    public static class GameTuning
    {
        /// <summary>The match ball, the court's walls and the pace of play.</summary>
        public static class Ball
        {
            public const double Pace = 1.05;                       // every shot, human and AI (playtest 2026-10-18: +5%)
            public const double SwingPaceScale = 1.15;             // the physical swing ~15% over PSA pace, for depth off the deader wall
            public const double NeutralHeadSpeed = 24.0, AttackHeadSpeed = 37.0;   // m/s at the racket head, before the scales
            public const double LiftHeadSpeed = 12.0, LiftScale = 1.25;            // lobs / serves of the physical swing
            public const double FrontWallRestitution = 0.50;       // playtest 2026-10-18: 0.62 still sent drives onto the back wall (lab 0.74)
            /// <summary>Drives, kills and boasts keep this much of their rise off the front wall (playtest 2026-10-18: they
            /// popped up into slow volleys). 0.2: 2.2 -> 1.3 m high at the short line, first bounce 8.1 -> 7.2 m.</summary>
            public const double FrontWallRiseKeep = 0.2;
            public const double HeatPerJoule = 0.22;               // runs at ~42-45 C in a rally
            /// <summary>Restitution against impact speed (m/s, multiplier): a double yellow is dead at pace.</summary>
            public static readonly double[,] SpeedResponse = { { 0.0, 1.10 }, { 2.0, 1.00 }, { 10.0, 0.88 }, { 25.0, 0.70 }, { 50.0, 0.58 } };
            /// <summary>How much of the warm-ball bounce survives at speed: warming buys height, not pace off the walls.</summary>
            public static readonly double[,] HotSpeedFade = { { 0.0, 1.0 }, { 6.0, 1.0 }, { 15.0, 0.55 }, { 30.0, 0.25 }, { 50.0, 0.15 } };
            public const double VisualScale = 2.2;                 // presentation only: the drawn ball (lab 1.45)
        }

        /// <summary>How athletes move and reach.</summary>
        public static class Movement
        {
            public const double AccelerationScale = 1.12, BrakingScale = 1.12;   // playtest 2026-10-17 (top speed stays the lab's 5.2 m/s)
            public const double Stretch = 1.45;                    // reach from the body at prediction time (lab 1.55)
            public const double LungeArmReach = 0.95, LungeMaxStep = 0.55;
            public const double AssistRadius = 0.25, AssistMaxCorrection = 0.12;   // the contact assist's pull (lab 0.42 / 0.30)
        }

        /// <summary>The reticle on the front wall.</summary>
        public static class Aim
        {
            public const double HalfWidth = 2.75, NeutralHeight = 1.7, MinHeight = 0.85, MaxHeight = 4.0;
            public const int RefreshSteps = 4, Iterations = 3;
            public const double BoastZone = 0.15, BoastHeightLow = 1.1, BoastHeightHigh = 2.4;
        }

        /// <summary>The simple hitting system: reach, timing, spread, the dive and sprint.</summary>
        public static class Hit
        {
            // Shot lines (2026-10-19, "the game should know I'm going for a drive"): for a person, the half of the
            // front wall aimed at picks straight or cross; the line itself comes from the shot. Per shot: how far off
            // the side wall it lands and how deep (m) - straight, then cross.
            // how far the shot is pulled from the aim onto its line (1 = the line exactly, 0 = the aim). 2026-10-20: was
            // 1.0 / 0.8 - Assisted played the perfect shot whatever the aim; now it helps, and the aim still matters
            public const double LineSnapAssisted = 0.6, LineSnapNonAssisted = 0.4;
            public const double AssistSpreadScale = 0.75;          // Assisted: a contact's spread, a little tighter
            public const double LineMidBall = 0.35;                // a ball this near the middle: the aim's side is the corner
            public const double DriveStraightOff = 0.40, DriveStraightDepth = 7.4, DriveCrossOff = 1.0, DriveCrossDepth = 6.6;
            public const double KillStraightOff = 0.30, KillStraightDepth = 3.0, KillCrossOff = 1.1, KillCrossDepth = 3.2;
            public const double LobStraightOff = 0.50, LobStraightDepth = 8.6, LobCrossOff = 0.9, LobCrossDepth = 8.4;
            public const double DropStraightOff = 0.35, DropStraightDepth = 1.0, DropCrossOff = 0.45, DropCrossDepth = 1.1;
            public const double Reach = 1.5, SweetSpot = 0.85, MinHeight = 0.04, MaxHeight = 2.6;
            public const double Grace = 0.22;                      // released this early, it still connects as the ball comes in
            public const double ChargeFull = 0.9, ChargeBonus = 0.6;
            public const double MaxSpread = 0.7, GrossMissChance = 0.12;
            public const double LungeMax = 0.7, LungeSpeed = 4.0;
            public const double BackWallSafeHeight = 1.4;
            // set early: up to 75% less spread, 85% fewer mishits
            public const double EarlyFull = 0.45, EarlySpreadCut = 0.75, EarlyMishitCut = 0.85, EarlyNear = 0.35;
            // a scrambled contact: how much worse it counts
            public const double DiveMiss = 0.35, DiveRecoveryMiss = 0.15;
            public const double FullChargeSpreadCut = 0.6;         // above FullChargeAt, a committed swing: tighter
            public const double FullChargeAt = 0.8;
            public const double MissPaceLoss = 0.15;               // a stretched contact loses up to 15% pace
            public const double GrossMissTinDepth = 0.15, GrossMissOff = 0.4, GrossMissOffSpan = 0.6;
            public const double SoftenStep = 0.93; public const int SoftenTries = 14;   // floor before the back wall: a touch softer each try
            // the great hit (the ring's flash)
            public const double GreatEarliness = 0.75, GreatReleaseWait = 0.06, GreatDistance = 0.15;
            // dive and sprint (the original SquashStrike)
            public const double DiveSeconds = 0.42, DiveSpeedScale = 1.85, DiveRecovery = 1.1, DiveRecoverySpeed = 0.65, DiveReachScale = 1.5;
            public const double DiveStamina = 0.12, DiveMinStamina = 0.2;
            public const double SprintScale = 1.3, SprintDrain = 0.05, WindedStamina = 0.3;
        }

        /// <summary>Each shot type: the aim height picks it (the original SquashStrike), the charge adds pace.</summary>
        public static class Shots
        {
            public const double DriveSpeed = 28.0;
            public const double TouchSpeed = 11.0;
            public const double LowHeight = 1.15, LobHeight = 3.0; // under: a drop (a kill if charged), over: a lob
            public const double KillCharge = 0.25, KillHeightAboveTin = 0.10;
            public const double DropHeightAboveTin = 0.17, DropSpeed = 9.0, DropFlightTime = 0.45, DropSpeedMin = 10.0, DropSpeedMax = 17.0;
            public const double LobSpeed = 14.0, LobRise = 1.1, LobMaxSpeed = 28.0, LobDepth = 8.7, LobWallMax = 3.8;
            public const double BoastPaceScale = 0.9, BoastMaxHeight = 1.8, BoastMinHeight = 0.8, BoastCross = 0.3;
        }

        /// <summary>The serve: a tap is a lob serve (high and deep); held past HardCharge, a hard serve.</summary>
        public static class Serve
        {
            public const double Speed = 24.0, HardCharge = 0.3, ChargeBonus = 0.6, Depth = 8.4;
            public const double AiPace = 0.85;                     // the physical swing's serve: 0.8-0.9 of the lift (before Ball.Pace)
        }

        /// <summary>Assisted controls.</summary>
        public static class Assist
        {
            // the straight drive that would clip its side wall: aimed in 0.1 m at a time, at most 0.3 m (2026-10-20: was
            // 1.2 m - a safety now, not the perfect line)
            public const double StraightStep = 0.1, StraightMinSide = 0.6, StraightInner = 0.5, StraightMaxShift = 0.3;
            // a bad contact (playtest 2026-10-18): jammed / stretched past 0.25 m (fully bad 0.35 m further), set less than
            // 0.3 s, or diving - a 25% (just bad) to 70% (fully bad) chance to lose the clean line, then 0.6-1.2 m off the aim
            public const double BadDistance = 0.25, BadSpan = 0.35, BadSetTime = 0.3;
            public const double MissMin = 0.25, MissMax = 0.70, MissOffset = 0.6;
            // the serve: every line across the wall, 0.2 m apart
            public const double ServeLineStep = 0.2;
        }

        /// <summary>The opponents (MatchBench --balance 2026-10-18: the human-style model wins ~61 / 44 / 27% of rallies).</summary>
        /// <summary>The referee's view of interference (Interference). P2-M7: only obstruction that matters counts.</summary>
        public static class Interference
        {
            public const double AccessClearance = 0.10;    // m beside the two bodies on the striker's path
            public const double SwingClearance = 0.95;     // m: the racket arc around the hitting point
            public const double LineHalfWidth = 0.20;      // m (plus the body radius): the corridor to the front wall
            public const double LineMaxDistance = 3.5;     // m: the line test applies near the hitting point
            public const double EffortMinSpeed = 1.0;      // m/s toward the ball that counts as "every effort"
            public const double EffortCloseEnough = 1.2;   // m: already this close, no running needed
            public const double ClearMinDistance = 0.25;   // m moved off the striker's corridor while in the way = clearing
            public const double ClearGrace = 0.20;         // s: an obstruction shorter than this was cleared in time
            public const double ReachSlack = 0.15;         // s short of the intercept a referee still calls reachable
            public const double ReactionGrace = 0.15;      // s: effort is judged after the striker could react
            public const double AccessPersist = 0.15;      // s: in the way this long before it counts (a crossing is free)
            public const double AccessAhead = 1.8;         // m: only the stretch of path just ahead of the striker
            public const double AccessMinApproach = 0.8;   // m/s: the striker is actually running that way
            public const double AccessTransitSpeed = 2.5;  // m/s across the path: passing through, not standing in the way
            public const double StrikeWindow = 0.45;       // s before the ball is playable: line and swing count only then
            public const double NearPersist = 0.05;        // s: line / swing must hold this long (a few steps)
            // Getting out of the way (P2-M7 L2)
            public const double StrikerKeep = 0.85;        // share of the striker's closing speed kept on body contact (live rally)
            public const double AssistGiveWay = 0.6;       // Assisted, non-striker in the way: sideways steer blended into the move
            // AI appeals (P2-M7 L3): the personality's tendency, scaled by level - rare at Easy
            public const double AppealAtEasy = 0.2;        // x the tendency at 0, rising to 1 at Hard (0.3 before the 2026-10-20 levels)
            public const double AppealFullAt = 0.29;       // the level at which the full tendency applies: Hard (was 0.42, Hard's old value)
            public const double AppealFadeAbove = 2.5, AppealFloor = 0.2;   // above that, x (1 - fade * (d - full)), at least the floor:
                                                           // strong players play long rallies; asking at every chance made half of them end in calls
            public const double SwingStopDistance = 0.95;  // m: the referee stops play himself only with the opponent this near the ball at contact
            public const double AppealHampered = 0.25;     // s obstructed before it thinks of asking
            public const double AppealShare = 0.25;        // in the line / swing: of the shots it would be given, the share it stops for
            public const double AppealShareWay = 0.3;      // blocked on the way: asked more freely (people ask for those lets); 0.8 until the
                                                           // AI stopped gifting balls (2026-10-20) and rallies doubled - then calls at Career strengths went 3% -> 16%
            public const double AppealNearBall = 0.6;      // s: an Access obstruction this close to the ball is worth asking about
        }

        public static class Ai
        {
            public const double MinReleaseLead = 0.025;   // s: the AI lets go at least this long before the contact (> the prediction's 1/60 s refresh)
            // difficulty d (0..1): reaction, aim noise, run speed and pace
            public const double Reaction0 = 0.50, ReactionSpan = 0.36, Noise0 = 0.80, NoiseSpan = 0.55;
            public const double Move0 = 0.60, Pace0 = 0.30, PaceSpan = 0.40;
            // the simple hitting system's shot choice
            public const double AimNoiseScale = 1.0, GapWeight = 0.25, ChoiceNoise = 2.5;
            /// <summary>Quick Match: Easy, Medium, Hard.</summary>
            public static readonly double[] Levels = { 0.045, 0.15, 0.29 };   // 2026-10-20 (was 0, 0.12, 0.42 before the release fix)
        }

        /// <summary>The game's clock.</summary>
        public static class Speed
        {
            public const double Game = 0.85;                       // the whole game runs at 85% of real time
            public const double DiveSlowMoSeconds = 0.38, DiveSlowMo = 0.3;
            public const double MenuDemo = 0.4;                    // the menus' demo rally (the old home screen)
        }
    }
}
