// ===========================================================
// Assets/SquashSim/Match/WallBot.cs
// The M2 opponent: a WALL-BOT, not the real AI (that is M3). It returns
// the ball to the front wall at a safe rally height and goes back to
// the T. It plays only through InputIntent - move, aim stick, Hit
// presses - exactly like a human, so it obeys every physical rule and
// can miss, arrive stretched or be wrong-footed like anyone else.
//
// Deterministic: its only randomness is a seeded LCG, and it reads
// simulation state, never a clock.
// ===========================================================

namespace SquashSim
{
    public sealed class WallBot : IAthleteBrain
    {
        // Tunables (a difficulty preset can change these).
        public double TriggerLead = 0.42;     // press Hit when the predicted contact is this close (s)
        public double ArriveDistance = 0.12;  // stop steering this close to the target spot
        public double AimSlew = 0.06;         // stick travel per step
        public double TossDelay = 0.8;        // seconds in the box before tossing
        public double ServeAimX = 0.55;       // lob serve: across to the receiver's side
        public double ServeAimY = 0.85;
        public double RallyAimSpreadX = 0.45;
        public double RallyAimMinY = 0.10, RallyAimMaxY = 0.50;
        public double LowBallHeight = 0.6;    // below this contact height, open the face more
        public double LowBallExtraY = 0.25;

        uint _seed;
        double _aimX, _aimY, _aimTX, _aimTY;
        int _cooldownSteps;
        bool _releaseNext;
        long _strokeKey = -1;                 // (rally, shot) we last pressed for: one press per ball

        // The bot's own read of the ball: where it will be playable (not limited to reach).
        readonly BallReader _read = new BallReader();

        public WallBot(uint seed) { _seed = seed == 0 ? 1u : seed; }

        double Next()
        {
            _seed = unchecked(_seed * 1664525u + 1013904223u);
            return _seed / 4294967296.0;
        }

        /// <summary>Write this step's input for player <paramref name="me"/>.</summary>
        public void Fill(InputIntent intent, MatchSimulation m, int me, double dt)
        {
            var ctrl = m.Players[me];
            var p = ctrl.Player;
            var cfg = PhysicsConfig.Current.Match;
            bool myTurn = m.CanStrike(me);
            bool serving = m.Score.Server == me;

            // ---------------- movement ----------------
            double tx, tz;
            bool move = true;
            if (!m.BallLive || (m.Phase == MatchPhase.WarmUp && !m.GoingForBall(me)))
            {
                move = false; tx = p.Pos.X; tz = p.Pos.Z;           // serve setup or rally over: stay put
            }
            else if (myTurn && ctrl.Assist.HasIdealSpot)
            {
                tx = ctrl.Assist.IdealSpot.X; tz = ctrl.Assist.IdealSpot.Z;
            }
            else if (myTurn && _read.Read(m.Ball, m.Surfaces))
            {
                // Head for the read, standing beside it on the forehand-side spacing.
                tx = _read.X - PhysicsConfig.Current.MovementAssist.PreferredSpacing;
                tz = _read.Z + 0.15;
            }
            else
            {
                tx = cfg.TX; tz = cfg.TZ;                            // back to the T
            }

            intent.Move.X = 0; intent.Move.Z = 0; intent.MoveMagnitude = 0;
            if (move)
            {
                double dx = tx - p.Pos.X, dz = tz - p.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dz * dz);
                if (d > ArriveDistance)
                {
                    intent.Move.X = dx / d; intent.Move.Z = dz / d; intent.MoveMagnitude = 1;
                }
            }

            // ---------------- strokes ----------------
            intent.Hit.Pressed = false;
            if (_releaseNext)
            {
                intent.Hit.Released = true;
                intent.Hit.Held = false;
                _releaseNext = false;
            }
            if (_cooldownSteps > 0) _cooldownSteps--;

            if (m.Phase == MatchPhase.AwaitToss && serving && m.PhaseTime >= TossDelay)
            {
                // Toss, with the serve's aim already set: a lob across the court.
                _aimTX = -m.Score.ServeSide * ServeAimX;
                _aimTY = ServeAimY;
                Tap(intent);
            }
            else if (myTurn && _cooldownSteps == 0 && ctrl.Swing.State == SwingStates.Idle &&
                     ctrl.Prediction.Valid && !m.Ball.Asleep && ctrl.Prediction.Time < TriggerLead &&
                     _strokeKey != StrokeKey(m))
            {
                bool serveStroke = m.TurnShots == 0;
                if (!serveStroke)
                {
                    _aimTX = (Next() * 2 - 1) * RallyAimSpreadX;
                    _aimTY = RallyAimMinY + Next() * (RallyAimMaxY - RallyAimMinY);
                    // A low ball needs an open face to clear the tin - what a player does by feel.
                    if (ctrl.Prediction.Point.Y < LowBallHeight) _aimTY = System.Math.Min(1.0, _aimTY + LowBallExtraY);
                    // The stick is a thumb, not a teleport: start from where it is, and reach the target in time.
                    _aimX = _aimTX; _aimY = _aimTY;
                }
                _strokeKey = StrokeKey(m);
                Tap(intent);
            }

            // ---------------- aim stick ----------------
            _aimX += Clamp(_aimTX - _aimX, -AimSlew, AimSlew);
            _aimY += Clamp(_aimTY - _aimY, -AimSlew, AimSlew);
            intent.AimRawX = _aimX;
            intent.AimRawY = _aimY;

            intent.Touch.Pressed = false; intent.Touch.Held = false;
        }

        static long StrokeKey(MatchSimulation m) { return m.History.Count * 1000L + m.TurnShots; }

        void Tap(InputIntent intent)
        {
            intent.Hit.Pressed = true;
            intent.Hit.Held = true;
            _releaseNext = true;          // released next step: a Quick stroke
            _cooldownSteps = 60;
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
