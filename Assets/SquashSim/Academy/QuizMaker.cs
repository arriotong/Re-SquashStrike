// ===========================================================
// Assets/SquashSim/Academy/QuizMaker.cs
// Playable challenges (2026-10-28): the daily RULES QUIZ - "the quizzes
// from the academy ... put them in the challenges" (the user). New calls
// every day from the day's seed: in or out, good serve or fault, down /
// out / good, who scores. Each shot is drawn at random and then flown
// and judged by RulesQuiz (the real ball physics, the referee's rules),
// so the answer is always right - and the mix of answers is chosen first
// (it is not always "In").
//
// A quiz is a lesson the Academy plays as it plays its own: its id is
// "quiz:<seed>:<count>" (Lessons.Find makes it), with its pass mark.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Academy
{
    public static class QuizMaker
    {
        public const string Prefix = "quiz:";
        public static string Id(uint seed, int count) { return Prefix + seed + ":" + count; }
        public static bool IsQuiz(string id) { return id != null && id.StartsWith(Prefix); }
        /// <summary>To pass: all but one (4 of 5, 9 of 10).</summary>
        public static int PassMark(int count) { return Math.Max(1, count - 1); }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        /// <summary>The lesson for a quiz id (null: not one).</summary>
        public static Lesson FromId(string id)
        {
            if (id == null || !id.StartsWith(Prefix)) return null;
            var parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length != 2 || !uint.TryParse(parts[0], out uint seed) || !int.TryParse(parts[1], out int n)) return null;
            var l = new Lesson { Id = id, Chapter = Chapter.Rules, Title = n >= 10 ? "Rules exam" : "Rules quiz", Blurb = "Make the call.", QuizPass = PassMark(n) };
            foreach (var q in Make(seed, n)) l.Steps.Add(new LessonStep { Goal = GoalKind.Quiz, Setup = StepSetup.Demo, Question = q, Text = q.Text });
            return l;
        }

        /// <summary>n questions from this seed: the kinds in turn, each with its answer chosen first.</summary>
        public static List<QuizQuestion> Make(uint seed, int n)
        {
            var list = new List<QuizQuestion>();
            uint h = Hash(seed * 2654435761u + 99u);
            double U() { h = Hash(h + 0x6D2B79F5u); return (h + 0.5) / 4294967296.0; }
            double R(double a, double b) { return a + (b - a) * U(); }
            QuizKind[] cycle = { QuizKind.Wall, QuizKind.Serve, QuizKind.RallyEnd, QuizKind.Scoring, QuizKind.Serve, QuizKind.Wall, QuizKind.RallyEnd };
            int start = (int)(U() * cycle.Length);
            for (int i = 0; i < n; i++)
            {
                var kind = cycle[(start + i) % cycle.Length];
                QuizQuestion q = null;
                switch (kind)
                {
                    case QuizKind.Scoring:
                    {
                        bool english = U() < 0.6, serverWins = U() < 0.5;
                        q = new QuizQuestion { Kind = kind, English = english, ServerWins = serverWins, Choices = new[] { "Server", "Receiver", "Nobody" },
                            Text = (english ? "English" : "PAR-11") + ": the " + (serverWins ? "server" : "receiver") + " wins the rally. Who scores?" };
                        break;
                    }
                    case QuizKind.Wall:
                    {
                        string want = U() < 0.5 ? "In" : "Out";
                        for (int t = 0; t < 40; t++)
                        {
                            var c = new QuizQuestion { Kind = kind, Text = "In or out?", Choices = new[] { "In", "Out" }, Px = R(-2.2, 2.2), Pz = R(5.0, 7.2), Wx = R(-2.6, 2.6),
                                Wy = want == "In" ? R(1.2, 4.45) : R(4.62, 5.3), Speed = R(18, 22) };
                            if (RulesQuiz.Answer(c) == want) { q = c; break; }
                        }
                        break;
                    }
                    case QuizKind.Serve:
                    {
                        string want = U() < 0.45 ? "Good serve" : "Fault";
                        int side = U() < 0.5 ? 1 : -1;
                        for (int t = 0; t < 60; t++)
                        {
                            var c = new QuizQuestion { Kind = kind, Text = "Good serve or fault?", Choices = new[] { "Good serve", "Fault" }, Px = side * 2.4, Pz = 5.0,
                                Wx = R(-2.4, 2.4), Wy = R(1.0, 3.6), Speed = R(17, 21) };
                            if (RulesQuiz.Answer(c) == want) { q = c; break; }
                        }
                        break;
                    }
                    default:
                    {
                        double u = U();
                        string want = u < 0.34 ? "Down" : u < 0.67 ? "Out" : "Good";
                        for (int t = 0; t < 60; t++)
                        {
                            var c = new QuizQuestion { Kind = QuizKind.RallyEnd, Text = "Down, out, or good?", Choices = new[] { "Down", "Out", "Good" }, Px = R(-2.0, 2.0), Pz = R(4.5, 7.0),
                                Wx = R(-2.6, 2.6), Wy = want == "Down" ? R(0.05, 0.5) : want == "Out" ? R(4.6, 5.3) : R(0.7, 4.3), Speed = R(17, 22) };
                            if (RulesQuiz.Answer(c) == want) { q = c; break; }
                        }
                        break;
                    }
                }
                if (q == null) { i--; start++; continue; }                  // (no shot found for that answer: the next kind)
                q.Intended = RulesQuiz.Answer(q);
                q.Explain = Explain(q, q.Intended);
                list.Add(q);
            }
            return list;
        }

        static string Explain(QuizQuestion q, string answer)
        {
            switch (q.Kind)
            {
                case QuizKind.Scoring:
                    return !q.English ? "In PAR-11 every rally scores - whoever wins it." : q.ServerWins ? "In English only the server scores." : "In English the receiver just wins the serve - no point.";
                case QuizKind.Wall:
                    return answer == "In" ? "Between the tin and the front-wall out line." : "Above the front-wall out line.";
                case QuizKind.Serve:
                    return answer == "Good serve" ? "Above the service line, and into the opposite back quarter." : "Under the service line, out, or it landed outside the opposite back quarter.";
                default:
                    return answer == "Down" ? "Into the tin (or the floor first)." : answer == "Out" ? "Over an out line." : "Above the tin and under the out line - a good shot.";
            }
        }
    }
}
