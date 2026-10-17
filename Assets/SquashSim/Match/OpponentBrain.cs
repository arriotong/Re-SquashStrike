// ===========================================================
// Assets/SquashSim/Match/OpponentBrain.cs
// AI opponent v1 (Phase 1 section 6).
//
//   Perceive  the ball (BallReader, ContactPrediction), the opponent,
//             its own balance - after a REACTION delay
//   Decide    shot choice by short ROLLOUTS of the real ball integrator:
//             ~50 candidate aims (and Touch drops), each flown from the
//             predicted contact with execution noise, scored for
//             pressure on the opponent, length, width, height and risk,
//             weighted by personality
//   Act       only through InputIntent - move, aim stick, Hit/Touch
//             press/hold/release, split-step, appeal - with human limits:
//             stick slew, timing jitter, aim error
//
// It never touches the ball or the racket, so every physical rule
// applies to it. Its errors come from execution (late, stretched,
// noisy aim), never from dice on the outcome. Deterministic: its only
// randomness is a seeded LCG, its maths goes through DMath.
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed class AiProfile
    {
        public string Name;

        // ---- execution (difficulty) ----
        public double ReactionTime;       // s after the opponent's contact before it starts moving to the new ball
        public double AimNoise;           // std dev of stick error at the chosen aim
        public double TriggerJitter;      // std dev of press timing (s)
        public double SplitStepError;     // std dev of split-step timing (s); <= 0: never split-steps
        public double AimSlew;            // stick travel per step
        public int NoiseSamples = 2;      // rollouts per candidate (noisy copies)

        // ---- decision (personality) ----
        public double RiskAppetite;       // 0 = only safe margins, 1 = goes for the nick
        public double WPressure, WLength, WWidth, WLow, WShort;
        public double TouchPreference;    // 0..1: considers Touch strokes (drops)
        public double PreparedPreference; // 0..1: holds for a Prepared stroke when time allows
        public double DeceptionChance;    // 0..1: shows one direction, plays another
        public double Discipline;         // 0..1: how fully it recovers to the T
        public double AppealTendency;     // 0..1: asks for a let when obstructed and it expects one
        public double Misjudge = 0.1;     // 0..1: how often it misreads whether it would get the decision

        public AiProfile Clone() { return (AiProfile)MemberwiseClone(); }

        /// <summary>Execution quality from 0 (beginner) to 1 (strong club player).</summary>
        public AiProfile WithDifficulty(double d)
        {
            d = d < 0 ? 0 : (d > 1 ? 1 : d);
            var p = Clone();
            p.ReactionTime = 0.32 - 0.20 * d;
            p.AimNoise = 0.22 - 0.17 * d;
            p.TriggerJitter = 0.06 - 0.045 * d;
            p.SplitStepError = 0.15 - 0.11 * d;
            p.AimSlew = 0.05 + 0.04 * d;
            p.Name = Name + " (" + (d < 0.34 ? "easy" : d < 0.67 ? "medium" : "hard") + ")";
            return p;
        }

        static AiProfile Base(string name)
        {
            return new AiProfile
            {
                Name = name, ReactionTime = 0.2, AimNoise = 0.1, TriggerJitter = 0.03, SplitStepError = 0.08, AimSlew = 0.07,
                PreparedPreference = 0.6, Discipline = 0.9, AppealTendency = 0.8
            };
        }

        /// <summary>Gets everything back; safe height, deep length.</summary>
        public static AiProfile Retriever()
        {
            var p = Base("Retriever");
            p.RiskAppetite = 0.2; p.WPressure = 1.0; p.WLength = 1.0; p.WWidth = 0.6; p.WLow = 0.1; p.WShort = 0.0;
            p.TouchPreference = 0.1; p.DeceptionChance = 0.05; p.Discipline = 0.95;
            return p;
        }

        /// <summary>Hits low and hard; takes risks to end rallies.</summary>
        public static AiProfile Attacker()
        {
            var p = Base("Attacker");
            p.RiskAppetite = 0.7; p.WPressure = 1.6; p.WLength = 0.3; p.WWidth = 0.4; p.WLow = 1.0; p.WShort = 0.2;
            p.TouchPreference = 0.2; p.DeceptionChance = 0.15; p.PreparedPreference = 0.8;
            return p;
        }

        /// <summary>Drops, boasts and disguise; moves the opponent front to back.</summary>
        public static AiProfile ShotMaker()
        {
            var p = Base("Shot-maker");
            p.RiskAppetite = 0.6; p.WPressure = 1.3; p.WLength = 0.3; p.WWidth = 0.4; p.WLow = 0.4; p.WShort = 0.9;
            p.TouchPreference = 0.8; p.DeceptionChance = 0.35;
            return p;
        }

        /// <summary>Tight length all day; never gives anything away.</summary>
        public static AiProfile Grinder()
        {
            var p = Base("Grinder");
            p.RiskAppetite = 0.3; p.WPressure = 0.8; p.WLength = 1.2; p.WWidth = 1.0; p.WLow = 0.2; p.WShort = 0.0;
            p.TouchPreference = 0.0; p.DeceptionChance = 0.1; p.Discipline = 1.0;
            return p;
        }

        public static AiProfile ByName(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "attacker": return Attacker();
                case "shotmaker": case "shot-maker": return ShotMaker();
                case "grinder": return Grinder();
                default: return Retriever();
            }
        }
    }

    public sealed class OpponentBrain : IAthleteBrain
    {
        public readonly AiProfile Profile;

        // Shot-selection constants.
        public double DecisionLead = 0.6;      // decide when the predicted contact is this close (s)
        public double TriggerLead = 0.42;      // press for a Quick stroke at this lead (s)
        public double DeceptionSwitchLead = 0.16;
        public int ReplanEverySteps = 12;      // keep re-planning on the latest prediction until committed
        public double TinMarginScale = 0.45, TinMarginVolley = 0.8, OutMarginScale = 0.4;
        public double QuickHeadFactor = 0.75;
        public double TossDelay = 0.9;
        public double ServeStrikeDelay = 0.26;  // after the toss: strike near its top
        public double ServeAimX = 0.55, ServeAimY = 0.85;
        static readonly double[] CandX = { -0.85, -0.55, -0.25, 0.0, 0.25, 0.55, 0.85 };
        static readonly double[] CandY = { -0.5, -0.2, 0.1, 0.4, 0.7 };
        static readonly double[] TouchY = { 0.0, 0.3, 0.6 };
        const double RolloutStep = 1.0 / 60;
        const double RolloutHorizon = 2.6;

        // ---- state ----
        uint _seed;
        int _lastShots = -1, _lastRally = -1;
        double _oppContactTime = -1e9;
        double _tgtX, _tgtZ;
        bool _decided, _pressed, _holding, _releaseNext, _appealed;
        double _choiceX, _choiceY, _decoyX;
        bool _choiceTouch, _choicePrepared, _useDecoy;
        double _pressLead;
        double _stickX, _stickY, _stickTX, _stickTY;
        double _splitAt = -1;
        double _obstructedFor;
        double _holdFor;
        bool _notedLanding;
        MatchPhase _lastPhase = MatchPhase.MatchOver;
        bool _serveAimSet;
        int _replanAge;
        double _noiseX, _noiseY;               // this shot's execution error, drawn once
        readonly double[] _oppLandX = new double[6];
        int _oppLandCount;

        readonly BallReader _read = new BallReader();
        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly AimState _aim = new AimState();
        readonly V3 _launch = new V3();
        bool _planPrepared;
        readonly InputIntent _probe = new InputIntent();

        /// <summary>For debug views: the last decision's best score and choice.</summary>
        public double LastBestScore { get; private set; }
        public string LastChoice { get; private set; } = "";
        /// <summary>Diagnostics: rally strokes pressed, and turns that ended with no press at all.</summary>
        public int RallyPresses, TurnsWithoutPress;
        /// <summary>Diagnostics: one-line state of the shot logic this step.</summary>
        public string DebugState { get; private set; } = "";
        /// <summary>Rollout's predicted front-wall height and launch speed for the last choice (calibration).</summary>
        public double LastPredFrontH { get; private set; }
        public double LastPredSpeed { get; private set; }
        double _rollFrontH, _rollSpeed;
        public readonly V3 LastPredIncoming = new V3(), LastPredLaunch = new V3(), LastPredPoint = new V3();

        public OpponentBrain(AiProfile profile, uint seed)
        {
            Profile = profile;
            _seed = seed == 0 ? 1u : seed;
        }

        // ---------------- randomness (deterministic) ----------------

        double Uniform()
        {
            _seed = unchecked(_seed * 1664525u + 1013904223u);
            return (_seed + 0.5) / 4294967296.0;
        }

        double Gauss()
        {
            double u1 = Uniform(), u2 = Uniform();
            return System.Math.Sqrt(-2.0 * DMath.Log(u1)) * DMath.Cos(2 * DMath.PI * u2);
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        // ---------------- the brain ----------------

        public void Fill(InputIntent intent, MatchSimulation m, int me, double dt)
        {
            var ctrl = m.Players[me];
            var p = ctrl.Player;
            var r = m.Referee;
            int opp = 1 - me;
            double t = m.Time;

            intent.Hit.Pressed = false; intent.Touch.Pressed = false;
            intent.SplitStepPressed = false; intent.AppealPressed = false;
            if (_releaseNext)
            {
                intent.Hit.Released = true; intent.Hit.Held = false;
                intent.Touch.Released = true; intent.Touch.Held = false;
                _releaseNext = false;
            }

            // ---- new rally / new shot bookkeeping ----
            if (m.History.Count != _lastRally)
            {
                _lastRally = m.History.Count;
                _lastShots = -1;
                ResetTurn();
                _tgtX = p.Pos.X; _tgtZ = p.Pos.Z;
            }
            if (m.Phase == MatchPhase.AwaitToss && _lastPhase != MatchPhase.AwaitToss) ResetTurn();   // new serve or re-toss
            _lastPhase = m.Phase;

            // Where did the opponent's last shot first land? (shades the T)
            if (!_notedLanding && m.TurnLastHitter == opp && m.TurnShots > 1 && m.Ball.FloorSinceFrontWall == 1)
            {
                _notedLanding = true;
                NoteOpponentLanding(m.Ball.LastImpactPoint.X);
            }

            if (m.TurnShots != _lastShots)
            {
                if (m.TurnShots > 0 && m.TurnLastHitter == opp)
                {
                    _oppContactTime = t;
                    if (Profile.SplitStepError > 0) _splitAt = t + System.Math.Abs(Gauss()) * Profile.SplitStepError;
                }
                if (m.TurnShots > 0 && m.TurnLastHitter == me)
                {
                    // Just played: decide where to recover to.
                    RecoveryTarget(m, p);
                }
                _lastShots = m.TurnShots;
                ResetTurn();
            }

            // Turn queries come from the match, so the same brain knocks up in the warm-up.
            bool live = m.BallLive;
            bool myTurn = m.GoingForBall(me);
            bool canStrike = m.CanStrike(me);
            bool warmUp = m.Phase == MatchPhase.WarmUp;

            // ---- split-step, timed to the opponent's contact ----
            if (_splitAt >= 0 && t >= _splitAt) { intent.SplitStepPressed = true; _splitAt = -1; }

            // ---- movement ----
            double tx = p.Pos.X, tz = p.Pos.Z;
            bool move = live;
            if (myTurn && m.TurnShots > 0)
            {
                if (t - _oppContactTime < Profile.ReactionTime) { tx = _tgtX; tz = _tgtZ; }   // still reacting
                else if (canStrike && ctrl.Prediction.Valid && ctrl.Prediction.Distance <= PhysicsConfig.Current.Reach.Normal)
                {
                    // Already within normal reach of the contact: plant and let the ball come. Chasing it
                    // makes "closest approach" recede with the athlete and the ball is never met.
                    tx = p.Pos.X; tz = p.Pos.Z;
                }
                else if (canStrike && ctrl.Assist.HasIdealSpot) { tx = ctrl.Assist.IdealSpot.X; tz = ctrl.Assist.IdealSpot.Z; }
                else if (_read.Read(m.Ball, m.Surfaces, p))
                {
                    tx = _read.X - PhysicsConfig.Current.MovementAssist.PreferredSpacing;
                    tz = _read.Z + 0.15;
                }
                else { tx = _tgtX; tz = _tgtZ; }
            }
            else if (myTurn && m.TurnShots == 0)
            {
                move = false;                                    // serving: stay in the box
            }
            else if (warmUp)
            {
                move = false;                                    // the other half of the knock-up: wait in the corner
            }
            else if (live)
            {
                tx = _tgtX; tz = _tgtZ;
                ShadowPosition(m, me, ref tx, ref tz);
                ClearTheWay(m, me, ref tx, ref tz);
            }

            intent.Move.X = 0; intent.Move.Z = 0; intent.MoveMagnitude = 0;
            if (move)
            {
                double dx = tx - p.Pos.X, dz = tz - p.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dz * dz);
                if (d > 0.10)
                {
                    // Arrive: ease off inside braking distance so the athlete stops ON the spot, not past it.
                    var pc = PhysicsConfig.Current.Player;
                    double arrive = System.Math.Sqrt(2 * pc.Braking * (d - 0.05)) / pc.MaxSpeed;
                    intent.Move.X = dx / d; intent.Move.Z = dz / d;
                    intent.MoveMagnitude = System.Math.Min(1.0, System.Math.Max(0.25, arrive));
                }
            }

            // ---- serve ----
            // Settle the stick on the serve aim while standing in the box, so the body has
            // turned to it before the toss; toss; strike near the top of the toss.
            if (m.Phase == MatchPhase.AwaitToss && m.Score.Server == me)
            {
                if (!_serveAimSet)
                {
                    double side = m.Score.ServeSide;
                    _choiceX = Clamp(-side * ServeAimX + Gauss() * Profile.AimNoise * 0.25, -1, 1);
                    _choiceY = Clamp(ServeAimY + Gauss() * Profile.AimNoise * 0.25, -1, 1);
                    _serveAimSet = true;
                }
                _stickTX = _choiceX; _stickTY = _choiceY;
                if (m.PhaseTime >= TossDelay && !_pressed)
                {
                    Tap(intent, false);
                    _pressed = true;                               // the toss
                }
            }
            if (canStrike && m.TurnShots == 0 && !_decided && ctrl.Prediction.Valid && !m.Ball.Asleep &&
                m.PhaseTime >= ServeStrikeDelay && ctrl.Prediction.Time < TriggerLead)
            {
                _decided = true;
                Tap(intent, false);                                // the serve itself
            }

            // ---- rally shot: decide, (maybe disguise), press ----
            if (canStrike && m.TurnShots > 0)
            {
                var pred = ctrl.Prediction;
                if (!_decided && pred.Valid && !m.Ball.Asleep && pred.Time < DecisionLead)
                {
                    _choicePrepared = pred.Time > 0.35 && Uniform() < Profile.PreparedPreference;
                    _planPrepared = _choicePrepared;
                    _noiseX = Gauss() * Profile.AimNoise;
                    _noiseY = Gauss() * Profile.AimNoise;
                    Decide(m, ctrl, me);
                    _decided = true;
                    _replanAge = 0;
                    _pressLead = TriggerLead + Gauss() * Profile.TriggerJitter;
                    _useDecoy = _choicePrepared && Uniform() < Profile.DeceptionChance;
                    _decoyX = Clamp(-_choiceX, -1, 1);
                }
                bool committed = ctrl.Swing.State == SwingStates.Swinging || ctrl.Swing.State == SwingStates.Scheduled ||
                                 ctrl.Swing.State == SwingStates.Recovering || (_pressed && !_choicePrepared);
                if (_decided && !committed && pred.Valid && ++_replanAge >= ReplanEverySteps)
                {
                    _replanAge = 0;
                    Decide(m, ctrl, me);
                }
                if (_decided)
                {
                    bool late = pred.Valid && pred.Time < DeceptionSwitchLead;
                    _stickTX = _useDecoy && !late ? _decoyX : _choiceX;
                    _stickTY = _choiceY;

                    if (!_pressed && pred.Valid && (_choicePrepared || pred.Time < _pressLead))
                    {
                        if (_choicePrepared) { Hold(intent, _choiceTouch); }
                        else Tap(intent, _choiceTouch);
                        _pressed = true;
                        RallyPresses++;
                    }
                }
            }

            // A Prepared stroke is held until the swing fires on its own, then released.
            if (_holding)
            {
                _holdFor += dt;
                bool swingGone = ctrl.Swing.State == SwingStates.Swinging || ctrl.Swing.State == SwingStates.Recovering;
                if (swingGone || !live || _holdFor > 1.5) { _holding = false; _holdFor = 0; _releaseNext = true; }
                else if (_choiceTouch) intent.Touch.Held = true; else intent.Hit.Held = true;
            }

            // ---- appeal: only when obstructed AND it expects the decision ----
            // A player knows the rules: they ask when they would get a let or a stroke, and
            // otherwise play on. Their read of the situation is imperfect (Misjudge).
            if (!warmUp && myTurn && m.TurnShots > 0 && !_appealed && m.Interference.Striker == me &&
                m.Interference.ThisTurn != ObstructionKind.None)
            {
                _obstructedFor += dt;
                var kind = m.Interference.Current;
                bool aboutToStrike = canStrike && ctrl.Prediction.Valid && ctrl.Prediction.Time < 0.45;
                bool cannotPlay = !ctrl.Prediction.Valid && _read.Valid && _read.Time < 0.6;
                bool hampered = _obstructedFor > 0.25 &&
                    ((kind != ObstructionKind.None && kind != ObstructionKind.Access && aboutToStrike) || cannotPlay);
                if (hampered)
                {
                    _appealed = true;
                    var verdict = m.Interference.Decide().Call;
                    bool expectsIt = verdict != InterferenceCall.NoLet;
                    if (Uniform() < Profile.Misjudge) expectsIt = !expectsIt;
                    if (expectsIt && Uniform() < Profile.AppealTendency) intent.AppealPressed = true;
                }
            }

            DebugState = "turn " + (myTurn ? "Y" : "n") + " strike " + (canStrike ? "Y" : "n") + " pred " +
                (ctrl.Prediction.Valid ? ctrl.Prediction.Time.ToString("0.00") + "@" + ctrl.Prediction.Distance.ToString("0.00") : "-") +
                " dec " + (_decided ? "Y" : "n") + " prep " + (_choicePrepared ? "Y" : "n") + " pressed " + (_pressed ? "Y" : "n") +
                " swing " + ctrl.Swing.State + " react " + (t - _oppContactTime < Profile.ReactionTime ? "Y" : "n") +
                " read " + (_read.Valid ? _read.Time.ToString("0.00") : "-") + " assist " + (ctrl.Assist.HasIdealSpot ? "Y" : "n");

            // ---- stick: a thumb, not a teleport ----
            _stickX += Clamp(_stickTX - _stickX, -Profile.AimSlew, Profile.AimSlew);
            _stickY += Clamp(_stickTY - _stickY, -Profile.AimSlew, Profile.AimSlew);
            intent.AimRawX = _stickX;
            intent.AimRawY = _stickY;
        }

        void ResetTurn()
        {
            _decided = false; _pressed = false; _appealed = false; _useDecoy = false;
            _obstructedFor = 0;
            _notedLanding = false;
            _serveAimSet = false;
            _read.Invalidate();
        }

        void Tap(InputIntent intent, bool touch)
        {
            var b = touch ? intent.Touch : intent.Hit;
            b.Pressed = true; b.Held = true;
            _releaseNext = true;
        }

        void Hold(InputIntent intent, bool touch)
        {
            var b = touch ? intent.Touch : intent.Hit;
            b.Pressed = true; b.Held = true;
            _holding = true;
            _holdFor = 0;
        }

        // ---------------- positioning ----------------

        void RecoveryTarget(MatchSimulation m, TestPlayer p)
        {
            var cfg = PhysicsConfig.Current.Match;
            // Shade the T toward the side the opponent has been hitting to.
            double mean = 0;
            for (int i = 0; i < _oppLandCount; i++) mean += _oppLandX[i];
            if (_oppLandCount > 0) mean /= _oppLandCount;
            double tx = cfg.TX + 0.25 * mean, tz = cfg.TZ;
            // Discipline: a lazy player stops short of the T.
            double k = Profile.Discipline;
            _tgtX = p.Pos.X + (tx - p.Pos.X) * k;
            _tgtZ = p.Pos.Z + (tz - p.Pos.Z) * k;
        }

        /// <summary>
        /// Where to wait while the opponent plays the ball: the T (shaded toward their usual side),
        /// pushed out of the three spaces the striker is entitled to - the corridor from where they
        /// are to the ball, the swing around the hitting point, and the straight line from the
        /// hitting point to the front wall.
        /// </summary>
        void ShadowPosition(MatchSimulation m, int me, ref double tx, ref double tz)
        {
            var it = m.Interference;
            if (it.Striker != 1 - me || it.TargetZ <= 0) return;
            var s = m.Players[1 - me].Player.Pos;
            double bx = it.TargetX, bz = it.TargetZ;
            double clear = 2 * PhysicsConfig.Current.Player.Radius + it.AccessClearance + 0.25;

            for (int iter = 0; iter < 3; iter++)
            {
                // 1. Corridor from the striker to the hitting point.
                double px = bx - s.X, pz = bz - s.Z;
                double len2 = px * px + pz * pz;
                double u = len2 < 1e-9 ? 0 : Clamp(((tx - s.X) * px + (tz - s.Z) * pz) / len2, 0, 1);
                double cx = s.X + px * u, cz = s.Z + pz * u;
                double dx = tx - cx, dz = tz - cz, d = System.Math.Sqrt(dx * dx + dz * dz);
                if (d < clear)
                {
                    if (d < 1e-6) { dx = -pz; dz = px; d = System.Math.Sqrt(dx * dx + dz * dz); if (d < 1e-9) { dx = 1; dz = 0; d = 1; } }
                    tx = cx + dx / d * clear; tz = cz + dz / d * clear;
                }
                // 2. Swing space around the hitting point.
                double sx = tx - bx, sz = tz - bz, sd = System.Math.Sqrt(sx * sx + sz * sz);
                double swing = it.SwingClearance + 0.2;
                if (sd < swing)
                {
                    if (sd < 1e-6) { sx = 0; sz = 1; sd = 1; }
                    tx = bx + sx / sd * swing; tz = bz + sz / sd * swing;
                }
                // 3. The line from the hitting point to the front wall.
                double lineHalf = it.LineHalfWidth + PhysicsConfig.Current.Player.Radius + 0.2;
                if (tz < bz && System.Math.Abs(tx - bx) < lineHalf)
                    tx = bx + (tx >= bx ? 1 : -1) * lineHalf;
                tx = Clamp(tx, -HALF_W + 0.35, HALF_W - 0.35);
                tz = Clamp(tz, 1.0, LENGTH - 0.35);
            }
        }

        /// <summary>If I am in the striker's way, step out of it (clearing - the opponent's right to the ball).</summary>
        void ClearTheWay(MatchSimulation m, int me, ref double tx, ref double tz)
        {
            var it = m.Interference;
            if (it.Striker != 1 - me) return;
            var p = m.Players[me].Player;
            var o = m.Players[1 - me].Player;
            // Look a quarter-second ahead: start clearing before actually being in the way.
            double lx = p.Pos.X + p.Vel.X * 0.25, lz = p.Pos.Z + p.Vel.Z * 0.25;
            if (it.Detect(o.Pos.X, o.Pos.Z, p.Pos.X, p.Pos.Z, it.TargetX, it.TargetZ) == ObstructionKind.None &&
                it.Detect(o.Pos.X, o.Pos.Z, lx, lz, it.TargetX, it.TargetZ) == ObstructionKind.None) return;

            // Move away from the hitting point, and sideways out of the line to the front wall.
            double ax = p.Pos.X - it.TargetX, az = p.Pos.Z - it.TargetZ;
            double d = System.Math.Sqrt(ax * ax + az * az);
            if (d < 1e-6) { ax = 1; az = 0; d = 1; }
            double side = p.Pos.X >= it.TargetX ? 1 : -1;
            tx = Clamp(p.Pos.X + ax / d * 1.0 + side * 0.8, -HALF_W + 0.35, HALF_W - 0.35);
            tz = Clamp(p.Pos.Z + az / d * 1.0, 0.5, LENGTH - 0.35);
        }

        // ---------------- shot choice by rollouts ----------------

        bool _knockUp;

        void Decide(MatchSimulation m, TestPlayerController ctrl, int me)
        {
            // Knock-up: the "opponent" to play for is yourself - a ball that comes back to you.
            _knockUp = m.Phase == MatchPhase.WarmUp;
            var o = m.Players[_knockUp ? me : 1 - me].Player;
            double best = double.NegativeInfinity;
            double bx = 0, by = 0.3;
            bool bt = false;

            for (int ix = 0; ix < CandX.Length; ix++)
                for (int iy = 0; iy < CandY.Length; iy++)
                {
                    double s = Score(m, ctrl, o, CandX[ix], CandY[iy], false);
                    if (s > best) { best = s; bx = CandX[ix]; by = CandY[iy]; bt = false; }
                }

            if (!_knockUp && Profile.TouchPreference > 0 && Uniform() < Profile.TouchPreference)
            {
                for (int ix = 0; ix < CandX.Length; ix++)
                    for (int iy = 0; iy < TouchY.Length; iy++)
                    {
                        double s = Score(m, ctrl, o, CandX[ix], TouchY[iy], true);
                        if (s > best) { best = s; bx = CandX[ix]; by = TouchY[iy]; bt = true; }
                    }
            }

            // Execution error: where the thumb actually ends up (drawn once per shot).
            _choiceX = Clamp(bx + _noiseX, -1, 1);
            _choiceY = Clamp(by + _noiseY, -1, 1);
            _choiceTouch = bt;
            LastBestScore = best;
            LastChoice = (bt ? "TOUCH " : "HIT ") + bx.ToString("0.00") + "," + by.ToString("0.00");
            Rollout(m, ctrl, o, _choiceX, _choiceY, bt);
            LastPredFrontH = _rollFrontH; LastPredSpeed = _rollSpeed;
            LastPredIncoming.CopyFrom(ctrl.Prediction.Vel); LastPredLaunch.CopyFrom(_launch); LastPredPoint.CopyFrom(ctrl.Prediction.Point);
        }

        double Score(MatchSimulation m, TestPlayerController ctrl, TestPlayer opp, double ax, double ay, bool touch)
        {
            int n = Profile.NoiseSamples < 1 ? 1 : Profile.NoiseSamples;
            double total = 0;
            for (int k = 0; k < n; k++)
            {
                // Deterministic spread: the nominal aim, then symmetric offsets by the aim error.
                double ox = 0, oy = 0;
                if (k > 0)
                {
                    double a = k * 2.399963;                    // golden-angle spread
                    ox = DMath.Cos(a) * Profile.AimNoise;
                    oy = DMath.Sin(a) * Profile.AimNoise;
                }
                total += Rollout(m, ctrl, opp, Clamp(ax + ox, -1, 1), Clamp(ay + oy, -1, 1), touch);
            }
            return total / n;
        }

        /// <summary>Fly the ball this aim would produce, from the predicted contact, and score it.</summary>
        double Rollout(MatchSimulation m, TestPlayerController ctrl, TestPlayer opp, double ax, double ay, bool touch)
        {
            const double FAIL = -3.0;
            var pred = ctrl.Prediction;
            V3 origin = pred.Point;

            // What the stick means at this contact (AimController), and what the athlete's
            // technique would do with it (SwingPlanner) - the same code the swing uses.
            _probe.AimRawX = ax; _probe.AimRawY = ay;
            AimController.Step(_aim, _probe, origin);
            var plan = SwingPlanner.Plan(touch ? "touch" : "hit", _aim, ctrl.Player.SwingSide >= 0 ? 1 : -1, new SwingContext
            {
                Mode = _planPrepared ? "prepared" : "quick", Quality = ctrl.Assist.Quality,
                ReachState = ctrl.Assist.ReachState, IsVolley = pred.IsVolley
            });
            if (!PredictLaunch(ctrl, pred.Vel, plan, _launch)) return FAIL;     // the face would not meet the ball
            _rollSpeed = V3.Length(_launch); _rollFrontH = -1;
            _scratch.Reset(origin, _launch, null, m.Ball.Temperature);

            bool toFront = true;
            double frontH = 0;
            int floorsAfter = 0;
            double x2 = origin.X, z2 = origin.Z;
            double bestReq = double.PositiveInfinity;    // the opponent's easiest required speed
            double tFront = 0;
            int steps = (int)(RolloutHorizon / RolloutStep);
            for (int i = 1; i <= steps; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, m.Surfaces, RolloutStep, _events);
                double t = i * RolloutStep;
                foreach (var e in _events)
                {
                    if (RallyReferee.OutReason(e) != null) return FAIL;
                    if (toFront)
                    {
                        if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") return FAIL;
                        if (e.SurfaceClass == "frontWall") { toFront = false; frontH = e.Point.Y; tFront = t; _rollFrontH = frontH; }
                    }
                    else if (e.SurfaceClass == "floor")
                    {
                        floorsAfter++;
                        if (floorsAfter == 2) { x2 = e.Point.X; z2 = e.Point.Z; }
                    }
                }
                if (floorsAfter >= 2) break;

                // Could the opponent take it here? Required speed = (distance - reach) / time available.
                if (!toFront && (i & 1) == 0 && _scratch.Pos.Y >= 0.2 && _scratch.Pos.Y <= 1.6)
                {
                    double dx = _scratch.Pos.X - opp.Pos.X, dz = _scratch.Pos.Z - opp.Pos.Z;
                    double gap = System.Math.Sqrt(dx * dx + dz * dz) - 0.9;
                    double req = gap <= 0 ? 0 : gap / t;
                    if (req < bestReq) bestReq = req;
                }
                x2 = _scratch.Pos.X; z2 = _scratch.Pos.Z;
            }
            if (toFront) return FAIL;                    // never reached the front wall in time

            double maxSpeed = PhysicsConfig.Current.Player.MaxSpeed;
            double pressure = double.IsPositiveInfinity(bestReq) ? 1.5 : Clamp(bestReq / maxSpeed, 0, 1.5);
            double length = Clamp(z2 / LENGTH, 0, 1);
            double width = Clamp(System.Math.Abs(x2) / HALF_W, 0, 1);
            double low = 1 - Clamp((frontH - TIN_H) / 3.0, 0, 1);
            double shortness = 1 - length;

            // Tin and out margins against the model's own accuracy (see below).
            double tinScale = pred.IsVolley ? TinMarginVolley : TinMarginScale;
            double tinMargin = frontH - TIN_H;
            double outMargin = FRONT_WALL_OUT_H - PhysicsConfig.Current.Match.LineWidth - frontH;
            double risk = DMath.Exp(-tinMargin / tinScale) + DMath.Exp(-outMargin / OutMarginScale);

            // Knock-up: easy to reach again (low "pressure" on yourself), a good length, safe.
            if (_knockUp)
            {
                double offLine = Clamp(System.Math.Abs(x2 - opp.Pos.X) / HALF_W, 0, 1);   // straight back down your own wall
                return -2.0 * pressure - 1.5 * offLine + 0.5 * length - risk;
            }

            double score = Profile.WPressure * pressure + Profile.WLength * length + Profile.WWidth * width +
                           Profile.WLow * low + Profile.WShort * shortness;

            // Margins: how close to the tin and the out line this aim flies, measured against
            // the model's own accuracy (MatchBench --calibrate: about +-0.5 m, worse on volleys,
            // where the real contact point drifts most from the predicted one).
            score -= (1 - Profile.RiskAppetite) * 2.0 * risk;
            return score;
        }

        /// <summary>The launch this plan would give (ShotPreview: shared with the aim target).</summary>
        bool PredictLaunch(TestPlayerController ctrl, V3 ballVel, SwingPlan plan, V3 output)
        {
            return ShotPreview.PredictLaunch(ctrl, ballVel, plan, _aim, _planPrepared, QuickHeadFactor, output);
        }

        /// <summary>Remember where the opponent's shots land (shades the T).</summary>
        void NoteOpponentLanding(double x)
        {
            for (int i = _oppLandX.Length - 1; i > 0; i--) _oppLandX[i] = _oppLandX[i - 1];
            _oppLandX[0] = x;
            if (_oppLandCount < _oppLandX.Length) _oppLandCount++;
        }
    }
}
