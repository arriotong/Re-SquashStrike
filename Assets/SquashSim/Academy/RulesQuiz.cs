// ===========================================================
// Assets/SquashSim/Academy/RulesQuiz.cs
// P2-M3: "make the call" - the right answer to a rules question comes
// from the sim itself: the shot is flown through the real ball physics
// and judged with the referee's own rules (out lines, tin, service
// line, the receiver's back quarter); scoring questions run the real
// MatchScore. So a question can never be marked wrongly.
// ===========================================================

using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim.Academy
{
    public static class RulesQuiz
    {
        /// <summary>Set the launcher up for this question's shot (the Academy's demo flies it for real).</summary>
        public static void Aim(Lab.BallLauncher launcher, QuizQuestion q)
        {
            Arcade.Feeder.Front(launcher, q.Px, q.Pz, q.Wx, q.Wy, q.Speed, 40);
        }

        /// <summary>The right call for this question (one of its Choices).</summary>
        public static string Answer(QuizQuestion q)
        {
            if (q.Kind == QuizKind.Scoring) return Scorer(q);
            if (q.Kind == QuizKind.Interference) return q.Stage != null ? q.Stage.Call() : null;   // P2-M7: the referee's call
            var surfaces = CourtSurfaces.Create();
            var ball = new BallState();
            var launcher = new Lab.BallLauncher(ball);
            Aim(launcher, q);
            var events = new List<SimEvent>();
            bool front = false;
            int side = q.Px >= 0 ? 1 : -1;               // a serve's box
            for (int i = 0; i < 240 * 4; i++)
            {
                events.Clear();
                BallPhysics.Step(ball, surfaces, 1.0 / 240, events);
                foreach (var e in events)
                {
                    if (e.Type != SimEventType.Impact) continue;
                    string call = Judge(q, e, ref front, side);
                    if (call != null) return call;
                }
            }
            return q.Kind == QuizKind.Serve ? "Fault" : "Good";
        }

        /// <summary>Judge one impact of a question's shot; null while undecided.</summary>
        public static string Judge(QuizQuestion q, SimEvent e, ref bool front, int side)
        {
            bool isOut = RallyReferee.OutReason(e) != null;
            string cls = e.SurfaceClass;
            switch (q.Kind)
            {
                case QuizKind.Wall:
                    if (isOut) return "Out";
                    if (cls == "frontWall") return "In";
                    return null;
                case QuizKind.RallyEnd:
                    if (!front && (cls == "tin" || cls == "floor")) return "Down";
                    if (isOut) return "Out";
                    if (cls == "frontWall") { front = true; return "Good"; }
                    return null;
                case QuizKind.Serve:
                    if (isOut) return "Fault";
                    if (!front)
                    {
                        if (cls == "frontWall") { front = true; return e.Point.Y <= SERVICE_LINE_H ? "Fault" : null; }
                        if (cls == "floor" || cls == "tin") return "Fault";
                        return null;
                    }
                    if (cls == "floor") return e.Point.X * side < 0 && e.Point.Z > SHORT_LINE_Z ? "Good serve" : "Fault";
                    return null;
            }
            return null;
        }

        static string Scorer(QuizQuestion q)
        {
            var score = new MatchScore(q.English ? ScoringSystem.English : ScoringSystem.Par, 0, 1, false);
            int winner = q.ServerWins ? 0 : 1;
            score.Apply(new RallyOutcome { Kind = RallyEnd.NotUp, Winner = winner, Loser = 1 - winner, Reason = "quiz" });
            if (score.Points[0] > 0) return "Server";
            if (score.Points[1] > 0) return "Receiver";
            return "Nobody";
        }
    }
}
