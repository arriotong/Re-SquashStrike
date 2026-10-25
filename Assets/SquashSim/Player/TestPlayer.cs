// ===========================================================
// Assets/SquashSim/Player/TestPlayer.cs
// Port of lab/player/TestPlayer.js. Player STATE only - pure data.
// Movement, facing, aim, swing and racket systems each write into it.
// ===========================================================

namespace SquashSim
{
    public static class ReachStates
    {
        public const string Normal = "normal";
        public const string Lunge = "lunge";
        public const string DeepLunge = "deepLunge";
        public const string Stretch = "stretch";
        public const string Unreachable = "unreachable";
    }

    public sealed class TestPlayer
    {
        public readonly V3 Pos;
        public readonly V3 Vel = new V3(0, 0, 0);
        public double Facing;            // radians; 0 looks toward the front wall
        public double Speed;
        public bool Precision;
        public readonly V3 Spawn;

        /// <summary>+1 forehand side, -1 backhand. DERIVED from the ball. 0 = undetermined.</summary>
        public int SwingSide;

        // Derived athletic state, chosen FOR the player.
        public string MovementState = "set";
        public string ReachState = ReachStates.Normal;
        public bool Adjusting;
        public double Balance = 1.0;

        // Movement modifiers (match only; the lab never sets them, so its
        // arithmetic is untouched). Seconds remaining for each effect.
        public double RecoveryTimer;     // after a stroke: slower to push off again
        public double BoostTimer;        // well-timed split-step: quicker first step
        public double PlantTimer;        // mistimed split-step: briefly planted

        /// <summary>0..1, match only (Match/Fatigue.cs). Exactly 1.0 in the lab.</summary>
        public double Stamina = 1.0;
        /// <summary>Game only: sprint / dive-recovery speed scale (exactly 1 in the lab: no effect).</summary>
        public double SpeedScale = 1.0;
        /// <summary>Game only (local players on the simple hitting system): full speed always - no near-ball
        /// "adjust" cap, no post-stroke recovery, split-step plant or fatigue slowdown. False in the lab.</summary>
        public bool ArcadeMovement;
        /// <summary>Game only: the AI's hitting pace for its difficulty (exactly 1 in the lab: no effect).</summary>
        public double PaceScale = 1.0;
        /// <summary>P3-C1: the athlete's attributes (career growth, Gauntlet perks). Neutral (all 5) changes nothing.</summary>
        public readonly AthleteAttributes Attributes = new AthleteAttributes();
        /// <summary>P5: the Gauntlet run's abilities (null everywhere else: no effect).</summary>
        public RunMods Mods;
        /// <summary>P5: the opponent's Pressure on this athlete's stamina drain (1 = none).</summary>
        public double OpponentDrain = 1.0;

        public TestPlayer() : this(new V3(0, 0, 6.2)) { }

        public TestPlayer(V3 spawn)
        {
            Pos = new V3(spawn.X, spawn.Y, spawn.Z);
            Spawn = spawn.Clone();
        }

        public void Reset()
        {
            Pos.X = Spawn.X;
            Pos.Y = Spawn.Y;
            Pos.Z = Spawn.Z;
            Vel.X = 0; Vel.Y = 0; Vel.Z = 0;
            Speed = 0;
            Facing = 0;
            SwingSide = 0;
            Balance = 1.0;
            MovementState = "set";
            ReachState = ReachStates.Normal;
            Adjusting = false;
            RecoveryTimer = 0; BoostTimer = 0; PlantTimer = 0;
        }
    }
}
