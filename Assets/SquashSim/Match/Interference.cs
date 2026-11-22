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
//   clearing  did the opponent get off the striker's corridor (metres)?
//
// P2-M7 (v3): only obstruction that MATTERS counts - a referee would not
// notice the rest. Access must hold (0.15 s), be on the stretch of path
// just ahead of a striker who is running that way; line and swing count
// only within 0.45 s of the strike. Numbers: GameTuning.Interference.
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
        /// <summary>2026-11-22 evening: how close the call was, 0 (clear-cut) .. 1 (on the line) - a referee can get a close one wrong,
        /// the video review never; and the call it could have gone the other way to.</summary>
        public double Doubt;
        public InterferenceCall Other;
    }

    /// <summary>Where everyone was when an interference call was made: the striker, where they were going, the obstructor
    /// (the review's floor drawing, in the match and in recorded rallies).</summary>
    public sealed class CallSnapshot
    {
        public RallyEnd Kind;
        public ObstructionKind Obstruction;
        public int Striker;
        public double StrikerX, StrikerZ, TargetX, TargetZ, ObstructorX, ObstructorZ;
    }

    public sealed class Interference
    {
        public double AccessClearance = GameTuning.Interference.AccessClearance;
        public double SwingClearance = GameTuning.Interference.SwingClearance;
        public double LineHalfWidth = GameTuning.Interference.LineHalfWidth;
        public double LineMaxDistance = GameTuning.Interference.LineMaxDistance;
        public double EffortMinSpeed = GameTuning.Interference.EffortMinSpeed;
        public double EffortCloseEnough = GameTuning.Interference.EffortCloseEnough;
        public double ClearMinDistance = GameTuning.Interference.ClearMinDistance;
        public double ClearGrace = GameTuning.Interference.ClearGrace;
        public double ReachSlack = GameTuning.Interference.ReachSlack;
        public double ReactionGrace = GameTuning.Interference.ReactionGrace;
        public double AccessPersist = GameTuning.Interference.AccessPersist;
        public double AccessAhead = GameTuning.Interference.AccessAhead;
        public double AccessMinApproach = GameTuning.Interference.AccessMinApproach;
        public double AccessTransitSpeed = GameTuning.Interference.AccessTransitSpeed;
        public double StrikeWindow = GameTuning.Interference.StrikeWindow;
        public double NearPersist = GameTuning.Interference.NearPersist;

        /// <summary>The player now going for the ball (-1 when nobody is).</summary>
        public int Striker { get; private set; } = -1;
        /// <summary>The obstruction that counts now (P2-M7: it matters - see Update).</summary>
        public ObstructionKind Current { get; private set; }
        /// <summary>Raw geometry this step, before it is weighed (debug, the overlay).</summary>
        public ObstructionKind Raw { get; private set; }
        /// <summary>The most serious obstruction counted this turn (latched until the next racket contact).</summary>
        public ObstructionKind ThisTurn { get; private set; }
        public double ObstructedSeconds { get; private set; }
        public double TargetX { get; private set; }
        public double TargetZ { get; private set; }
        /// <summary>Seconds until the ball is playable where the striker is going.</summary>
        public double TimeToBall { get; private set; }

        readonly BallReader _read = new BallReader { RefreshSteps = 4 };
        int _turnKey = -1;
        double _turnTime, _effortSum, _effortTime, _weighedFor;
        bool _reachableAtObstruction, _reachableNow;
        double _distAtObstruction, _cleared;

        /// <summary>Mean speed of the striker toward the ball this turn (after the reaction grace).</summary>
        public double StrikerEffort { get { return _effortTime > 0 ? _effortSum / _effortTime : 0; } }
        /// <summary>How far (m) the obstructor ran off the striker's corridor while in the way, under its own steam (being
        /// pushed by the striker's body does not count): clearing.</summary>
        public double ObstructorClearing { get { return _cleared; } }
        public bool Reachable { get { return _reachableAtObstruction || _reachableNow; } }
        double _lineMargin = double.MaxValue, _nearMiss = double.MaxValue;
        /// <summary>A call this close (m, or m/s, or s) is in doubt; at the line, fully.</summary>
        public const double DoubtSpan = 0.25, DoubtSeconds = 0.08, DoubtSpeed = 0.4;

        public void Reset()
        {
            Striker = -1;
            Current = ObstructionKind.None; Raw = ObstructionKind.None;
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
                _turnTime = 0; _effortSum = 0; _effortTime = 0; _weighedFor = 0;
                _reachableAtObstruction = false; _reachableNow = false; _distAtObstruction = 0; _cleared = 0;
                _lineMargin = double.MaxValue; _nearMiss = double.MaxValue;
            }
            _turnTime += dt;
            Striker = striker;
            Current = ObstructionKind.None; Raw = ObstructionKind.None;
            if (striker < 0 || r.Shots == 0) return;          // nobody going for it, or the serve

            if (!_read.Read(m.Ball, m.Surfaces, m.Players[striker].Player)) return;   // where the striker would hit
            TargetX = _read.X;
            TargetZ = _read.Z;
            TimeToBall = _read.Time;
            _reachableNow = _read.ShortBy <= ReachSlack;

            TestPlayer s = m.Players[striker].Player;
            TestPlayer o = m.Players[1 - striker].Player;
            Raw = Detect(s.Pos.X, s.Pos.Z, o.Pos.X, o.Pos.Z, TargetX, TargetZ);
            // 2026-11-22 evening: how close it is to a line call either way, near the strike (the referee's doubt)
            if (TimeToBall <= StrikeWindow && o.Pos.Z < TargetZ)
            {
                double fromT = DMath.Hypot(o.Pos.X - TargetX, o.Pos.Z - TargetZ);
                if (fromT < LineMaxDistance)
                {
                    double gap = System.Math.Abs(System.Math.Abs(o.Pos.X - TargetX) - LineWidthAt(fromT));
                    if (Raw == ObstructionKind.Line) _lineMargin = System.Math.Min(_lineMargin, gap);
                    else _nearMiss = System.Math.Min(_nearMiss, gap);
                }
            }

            // Effort: the striker's speed toward the hitting point.
            double tx = TargetX - s.Pos.X, tz = TargetZ - s.Pos.Z;
            double td = System.Math.Sqrt(tx * tx + tz * tz);
            double approach = td > 1e-6 ? (s.Vel.X * tx + s.Vel.Z * tz) / td : 0;
            if (_turnTime > ReactionGrace && td > 1e-6)
            {
                _effortSum += System.Math.Max(0, approach) * dt;
                _effortTime += dt;
            }

            // The obstructor's distance off the striker's corridor (feet -> hitting point).
            double u = td < 1e-6 ? 0 : System.Math.Max(0, System.Math.Min(1, ((o.Pos.X - s.Pos.X) * tx + (o.Pos.Z - s.Pos.Z) * tz) / (td * td)));
            double ax = o.Pos.X - (s.Pos.X + tx * u), az = o.Pos.Z - (s.Pos.Z + tz * u);
            double off = System.Math.Sqrt(ax * ax + az * az);

            // P2-M7: weigh it - does this obstruction matter? (a referee would not notice the rest)
            ObstructionKind weighed = ObstructionKind.None;
            double across = td < 1e-6 ? 0 : System.Math.Abs(o.Vel.X * -tz + o.Vel.Z * tx) / td;   // the obstructor's speed across the path
            if (Raw == ObstructionKind.Access)
            {
                bool ahead = u * td <= AccessAhead;                      // in the stretch of path just ahead
                bool running = approach >= AccessMinApproach || td <= EffortCloseEnough;
                bool transit = across >= AccessTransitSpeed;             // passing straight through: not in the way
                if (ahead && running && !transit) weighed = ObstructionKind.Access;
            }
            else if (Raw == ObstructionKind.Line || Raw == ObstructionKind.Swing)
            {
                if (TimeToBall <= StrikeWindow) weighed = Raw;           // only near the strike
                else if (u * td <= AccessAhead && approach >= AccessMinApproach && Detect(s.Pos.X, s.Pos.Z, o.Pos.X, o.Pos.Z, TargetX, TargetZ, true) == ObstructionKind.Access)
                    weighed = ObstructionKind.Access;                    // earlier, it can still be in the way to the ball
            }
            _weighedFor = weighed == ObstructionKind.None ? 0 : _weighedFor + dt;
            double persist = weighed == ObstructionKind.Access ? AccessPersist : NearPersist;
            if (weighed != ObstructionKind.None && _weighedFor >= persist) Current = weighed;

            if (Current != ObstructionKind.None)
            {
                if (ObstructedSeconds == 0) { _reachableAtObstruction = _reachableNow; _distAtObstruction = td; }
                ObstructedSeconds += dt;
                if (Severity(Current) > Severity(ThisTurn)) ThisTurn = Current;
            }
            if (Current != ObstructionKind.None || (ObstructedSeconds > 0 && Raw != ObstructionKind.None))
            {
                // clearing: the obstructor's own speed away from the corridor - what it is moving AND trying to move
                // (being bumped aside by the striker carries the body, and is not an effort to clear)
                if (off < 1e-6) { ax = -tz; az = tx; off = System.Math.Sqrt(ax * ax + az * az); }
                double away = off < 1e-9 ? 0 : (o.Vel.X * ax + o.Vel.Z * az) / off;
                m.SteeredMove(1 - striker, out double wx, out double wz);
                double wants = off < 1e-9 ? 0 : PhysicsConfig.Current.Player.MaxSpeed * (wx * ax + wz * az) / off;
                _cleared += System.Math.Max(0, System.Math.Min(away, wants)) * dt;
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

        /// <summary>2026-11-22 evening (the user: "its kind of inaccurate when opponent is far away"; their replay: a stroke given with
        /// the opponent 2.8 m ahead and 0.5 m off the line - the ball would have missed them): the corridor to the front wall is the
        /// body and the ball (LineBallMargin) plus a margin for the swing that fades to nothing by LineMaxDistance.</summary>
        public double LineWidthAt(double fromTarget)
        {
            double fade = 1 - fromTarget / LineMaxDistance;
            return PhysicsConfig.Current.Player.Radius + LineBallMargin + LineHalfWidth * (fade > 0 ? fade : 0);
        }
        public const double LineBallMargin = 0.03;

        /// <summary>Pure geometry, exposed for tests and for the AI's clearing logic.</summary>
        public ObstructionKind Detect(double sx, double sz, double ox, double oz, double tx, double tz) { return Detect(sx, sz, ox, oz, tx, tz, false); }

        /// <summary>Geometry; accessOnly: just the path test.</summary>
        public ObstructionKind Detect(double sx, double sz, double ox, double oz, double tx, double tz, bool accessOnly)
        {
            double R = PhysicsConfig.Current.Player.Radius;
            double dtx = ox - tx, dtz = oz - tz;
            double fromTarget = System.Math.Sqrt(dtx * dtx + dtz * dtz);
            if (!accessOnly)
            {
                // Line: opponent between the hitting point and the front wall, close to the straight line.
                if (oz < tz && System.Math.Abs(ox - tx) < LineWidthAt(fromTarget) && fromTarget < LineMaxDistance)
                    return ObstructionKind.Line;
                // Swing: opponent inside the racket arc around the hitting point.
                if (fromTarget < SwingClearance) return ObstructionKind.Swing;
            }

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

        /// <summary>The referee's decision on an appeal by the current striker.</summary>
        public InterferenceDecision Decide()
        {
            var d = Judge(ThisTurn, Reachable, StrikerEffort, _distAtObstruction, ObstructedSeconds, ObstructorClearing);
            // 2026-11-22 evening: how close it was (the referee may see a close one wrong; the video review sees it right) - each
            // measure against its own span: metres off the line, metres cleared, seconds in the way, the run's speed
            double doubt = 0; var other = d.Call;
            double Near(double margin, double span) { return margin >= span ? 0 : 1 - margin / span; }
            double clearDoubt = System.Math.Max(Near(System.Math.Abs(ObstructorClearing - ClearMinDistance), DoubtSpan), Near(System.Math.Abs(ObstructedSeconds - ClearGrace), DoubtSeconds));
            double effortDoubt = _distAtObstruction > EffortCloseEnough && StrikerEffort > 0 ? Near(System.Math.Abs(StrikerEffort - EffortMinSpeed), DoubtSpeed) : 0;
            switch (d.Call)
            {
                case InterferenceCall.Stroke:
                    if (ThisTurn == ObstructionKind.Line) { doubt = Near(_lineMargin, DoubtSpan); other = InterferenceCall.Let; }
                    else if (ThisTurn == ObstructionKind.Access) { doubt = clearDoubt; other = InterferenceCall.Let; }
                    break;
                case InterferenceCall.Let:
                    doubt = clearDoubt; other = InterferenceCall.Stroke;
                    if (effortDoubt > doubt) { doubt = effortDoubt; other = InterferenceCall.NoLet; }
                    break;
                case InterferenceCall.NoLet:
                    if (ThisTurn == ObstructionKind.None) { doubt = Near(_nearMiss, DoubtSpan); other = InterferenceCall.Let; }
                    else { doubt = effortDoubt; other = InterferenceCall.Let; }
                    break;
            }
            d.Doubt = doubt;
            d.Other = other;
            return d;
        }

        /// <summary>The referee's view of it: a close call (Doubt) can go the other way - deterministically, from <paramref name="roll"/>
        /// (0..1, the match's own dice). <paramref name="errorRate"/>: the chance at the very line.</summary>
        public static InterferenceDecision AsSeen(InterferenceDecision exact, double roll, double errorRate)
        {
            if (exact.Doubt <= 0 || exact.Other == exact.Call || roll >= exact.Doubt * errorRate) return exact;
            var seen = exact;
            seen.Call = exact.Other; seen.Other = exact.Call;
            seen.Reason = exact.Other == InterferenceCall.Stroke ? "Stroke - opponent did not clear" :
                          exact.Other == InterferenceCall.Let ? "Let - opponent was clearing" : "No let - opponent was clear";
            return seen;
        }

        /// <summary>The decision itself, from what was observed this turn (pure: unit-tested). clearing: metres the
        /// obstructor got off the striker's corridor while in the way.</summary>
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
            bool cleared = obstructedSeconds < ClearGrace || clearing >= ClearMinDistance;
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
