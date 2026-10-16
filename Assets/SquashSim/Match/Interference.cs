// ===========================================================
// Assets/SquashSim/Match/Interference.cs
// Obstruction DETECTION (interference v1, Phase 1 section 5.3).
//
// While a striker is going for the ball, is the other player in the
// way? Three geometric tests, the ones a referee looks at:
//
//   Access   the opponent's body is on (or right beside) the striker's
//            path from where they stand to where the ball will be
//            playable (BallReader).
//   Swing    the opponent is inside the striker's swing space around
//            that hitting point.
//   Line     the opponent stands in the straight line from the hitting
//            point to the front wall - the classic stroke situation.
//
// It only DETECTS, every step, and remembers whether the striker was
// obstructed at any point in this turn. What it means is decided by an
// appeal: obstructed -> Let, clear -> No let (v1, Phase 1 decision 6).
// Effort and clearing judgement (Stroke) arrive with interference v2.
// ===========================================================

namespace SquashSim
{
    public enum ObstructionKind { None, Access, Swing, Line }

    public sealed class Interference
    {
        public double AccessClearance = 0.10;    // extra metres beside the two bodies on the path
        public double SwingClearance = 0.95;     // racket arc around the hitting point
        public double LineHalfWidth = 0.45;      // corridor to the front wall
        public double LineMaxDistance = 3.5;     // the line test only applies near the hitting point

        /// <summary>The player now going for the ball (-1 when nobody is).</summary>
        public int Striker { get; private set; } = -1;
        public ObstructionKind Current { get; private set; }
        /// <summary>The most serious obstruction seen this turn (latched until the next racket contact).</summary>
        public ObstructionKind ThisTurn { get; private set; }
        public double ObstructedSeconds { get; private set; }
        public double TargetX { get; private set; }
        public double TargetZ { get; private set; }

        readonly BallReader _read = new BallReader { RefreshSteps = 4 };
        int _turnKey = -1;

        public void Reset()
        {
            Striker = -1;
            Current = ObstructionKind.None;
            ThisTurn = ObstructionKind.None;
            ObstructedSeconds = 0;
            _turnKey = -1;
            _read.Invalidate();
        }

        public bool ObstructedThisTurn(int striker) { return striker == Striker && ThisTurn != ObstructionKind.None; }

        /// <summary>Call once per step during a live rally.</summary>
        public void Update(MatchSimulation m, double dt)
        {
            RallyReferee r = m.Referee;
            int striker = r.Outcome != null ? -1 : r.PendingStriker;
            int turnKey = r.Shots * 4 + (striker + 1);
            if (turnKey != _turnKey)
            {
                // A new turn: forget the last one.
                _turnKey = turnKey;
                ThisTurn = ObstructionKind.None;
                ObstructedSeconds = 0;
                _read.Invalidate();
            }
            Striker = striker;
            Current = ObstructionKind.None;
            if (striker < 0 || r.Shots == 0) return;          // nobody going for it, or the serve

            if (!_read.Read(m.Ball, m.Surfaces)) return;
            TargetX = _read.X;
            TargetZ = _read.Z;

            TestPlayer s = m.Players[striker].Player;
            TestPlayer o = m.Players[1 - striker].Player;
            Current = Detect(s.Pos.X, s.Pos.Z, o.Pos.X, o.Pos.Z, TargetX, TargetZ);

            if (Current != ObstructionKind.None)
            {
                ObstructedSeconds += dt;
                if (Severity(Current) > Severity(ThisTurn)) ThisTurn = Current;
            }
        }

        static int Severity(ObstructionKind k)
        {
            switch (k)
            {
                case ObstructionKind.Line: return 3;
                case ObstructionKind.Swing: return 2;
                case ObstructionKind.Access: return 1;
                default: return 0;
            }
        }

        /// <summary>Pure geometry, exposed for tests and for the AI's clearing logic.</summary>
        public ObstructionKind Detect(double sx, double sz, double ox, double oz, double tx, double tz)
        {
            double R = PhysicsConfig.Current.Player.Radius;

            // Line: opponent between the hitting point and the front wall, close to the straight line.
            double dtx = ox - tx, dtz = oz - tz;
            double fromTarget = System.Math.Sqrt(dtx * dtx + dtz * dtz);
            if (oz < tz && System.Math.Abs(ox - tx) < LineHalfWidth + R && fromTarget < LineMaxDistance)
                return ObstructionKind.Line;

            // Swing: opponent inside the racket arc around the hitting point.
            if (fromTarget < SwingClearance) return ObstructionKind.Swing;

            // Access: opponent's body on the striker's path to the ball.
            double px = tx - sx, pz = tz - sz;
            double len2 = px * px + pz * pz;
            double u = len2 < 1e-9 ? 0 : ((ox - sx) * px + (oz - sz) * pz) / len2;
            if (u > 0 && u < 1)
            {
                double cx = sx + px * u - ox, cz = sz + pz * u - oz;
                if (System.Math.Sqrt(cx * cx + cz * cz) < 2 * R + AccessClearance) return ObstructionKind.Access;
            }
            return ObstructionKind.None;
        }

        public static string Describe(ObstructionKind k)
        {
            switch (k)
            {
                case ObstructionKind.Line: return "in the line to the front wall";
                case ObstructionKind.Swing: return "in the swing";
                case ObstructionKind.Access: return "blocking the way to the ball";
                default: return "clear";
            }
        }
    }
}
