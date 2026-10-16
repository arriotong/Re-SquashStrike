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
// It DETECTS every step and keeps what a referee weighs for the turn:
//   reach     could the striker have got to the ball? (an intercept read)
//   effort    was the striker moving to the ball?
//   clearing  was the opponent moving out of the way while obstructing?
// An appeal is then decided (interference v2):
//   no obstruction, couldn't reach, or no effort        -> No let
//   in the swing, or in the line to the front wall      -> Stroke
//   in the way to the ball, but made every effort to clear -> Let
//   in the way to the ball and did not clear            -> Stroke
// ===========================================================

namespace SquashSim
{
    public enum ObstructionKind { None, Access, Swing, Line }

    public enum InterferenceCall { NoLet, Let, Stroke }

    public struct InterferenceDecision
    {
        public InterferenceCall Call;
        public string Reason;
    }

    public sealed class Interference
    {
        public double AccessClearance = 0.10;    // extra metres beside the two bodies on the path
        public double SwingClearance = 0.95;     // racket arc around the hitting point
        public double LineHalfWidth = 0.20;      // corridor to the front wall (plus the body radius): the ball's path, not a lane
        public double LineMaxDistance = 3.5;     // the line test only applies near the hitting point
        public double EffortMinSpeed = 1.0;      // mean speed toward the ball that counts as "every effort" (m/s)
        public double EffortCloseEnough = 1.2;   // already this close to the ball: no running needed
        public double ClearMinSpeed = 1.0;       // obstructor's mean speed out of the way that counts as clearing
        public double ClearGrace = 0.20;         // an obstruction shorter than this was cleared in time
        public double ReachSlack = 0.15;         // seconds short of the intercept a referee still calls reachable
        public double ReactionGrace = 0.15;      // effort is judged after the striker has had time to react

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
        double _turnTime, _effortSum, _effortTime, _clearSum, _clearTime;
        bool _reachableAtObstruction, _reachableNow;
        double _distAtObstruction;

        /// <summary>Mean speed of the striker toward the ball this turn (after the reaction grace).</summary>
        public double StrikerEffort { get { return _effortTime > 0 ? _effortSum / _effortTime : 0; } }
        /// <summary>Mean speed of the obstructor out of the way while obstructing.</summary>
        public double ObstructorClearing { get { return _clearTime > 0 ? _clearSum / _clearTime : 0; } }
        public bool Reachable { get { return _reachableAtObstruction || _reachableNow; } }

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
                _turnTime = 0; _effortSum = 0; _effortTime = 0; _clearSum = 0; _clearTime = 0;
                _reachableAtObstruction = false; _reachableNow = false; _distAtObstruction = 0;
            }
            _turnTime += dt;
            Striker = striker;
            Current = ObstructionKind.None;
            if (striker < 0 || r.Shots == 0) return;          // nobody going for it, or the serve

            if (!_read.Read(m.Ball, m.Surfaces, m.Players[striker].Player)) return;   // where the striker would hit
            TargetX = _read.X;
            TargetZ = _read.Z;
            _reachableNow = _read.ShortBy <= ReachSlack;

            TestPlayer s = m.Players[striker].Player;
            TestPlayer o = m.Players[1 - striker].Player;
            Current = Detect(s.Pos.X, s.Pos.Z, o.Pos.X, o.Pos.Z, TargetX, TargetZ);

            // Effort: the striker's speed toward the hitting point.
            double tx = TargetX - s.Pos.X, tz = TargetZ - s.Pos.Z;
            double td = System.Math.Sqrt(tx * tx + tz * tz);
            if (_turnTime > ReactionGrace && td > 1e-6)
            {
                _effortSum += System.Math.Max(0, (s.Vel.X * tx + s.Vel.Z * tz) / td) * dt;
                _effortTime += dt;
            }

            if (Current != ObstructionKind.None)
            {
                if (ObstructedSeconds == 0) { _reachableAtObstruction = _reachableNow; _distAtObstruction = td; }
                ObstructedSeconds += dt;
                if (Severity(Current) > Severity(ThisTurn)) ThisTurn = Current;

                // Clearing: the obstructor's speed away from the striker's corridor / hitting point.
                double px = TargetX - s.Pos.X, pz = TargetZ - s.Pos.Z, len2 = px * px + pz * pz;
                double u = len2 < 1e-9 ? 0 : System.Math.Max(0, System.Math.Min(1, ((o.Pos.X - s.Pos.X) * px + (o.Pos.Z - s.Pos.Z) * pz) / len2));
                double ax = o.Pos.X - (s.Pos.X + px * u), az = o.Pos.Z - (s.Pos.Z + pz * u);
                double ad = System.Math.Sqrt(ax * ax + az * az);
                if (ad < 1e-6) { ax = -pz; az = px; ad = System.Math.Sqrt(ax * ax + az * az); }
                double away = ad < 1e-9 ? 0 : (o.Vel.X * ax + o.Vel.Z * az) / ad;
                _clearSum += System.Math.Max(0, away) * dt;
                _clearTime += dt;
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

        /// <summary>The referee's decision on an appeal by the current striker (interference v2).</summary>
        public InterferenceDecision Decide()
        {
            return Judge(ThisTurn, Reachable, StrikerEffort, _distAtObstruction, ObstructedSeconds, ObstructorClearing);
        }

        /// <summary>The decision itself, from what was observed this turn (pure: unit-tested).</summary>
        public InterferenceDecision Judge(ObstructionKind worst, bool reachable, double effort, double distAtObstruction,
                                          double obstructedSeconds, double clearing)
        {
            if (worst == ObstructionKind.None)
                return new InterferenceDecision { Call = InterferenceCall.NoLet, Reason = "No let - opponent was clear" };
            if (!reachable)
                return new InterferenceDecision { Call = InterferenceCall.NoLet, Reason = "No let - could not have reached the ball" };
            if (distAtObstruction > EffortCloseEnough && effort < EffortMinSpeed)
                return new InterferenceDecision { Call = InterferenceCall.NoLet, Reason = "No let - not making every effort" };
            if (worst == ObstructionKind.Line)
                return new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = "Stroke - opponent in the line to the front wall" };
            if (worst == ObstructionKind.Swing)
                return new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = "Stroke - opponent in the swing" };
            bool cleared = obstructedSeconds < ClearGrace || clearing >= ClearMinSpeed;
            return cleared
                ? new InterferenceDecision { Call = InterferenceCall.Let, Reason = "Let - opponent was clearing" }
                : new InterferenceDecision { Call = InterferenceCall.Stroke, Reason = "Stroke - opponent did not clear" };
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
