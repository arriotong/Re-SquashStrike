// ===========================================================
// Assets/SquashSim.Testing/Editor/QuizMakerTests.cs
// Playable challenges (2026-10-28): the daily rules quiz.
//   - the same seed, the same calls; another seed, others
//   - every answer is what the referee says (RulesQuiz), and the calls
//     are mixed (not all "In")
//   - a quiz id makes its lesson, with all-but-one to pass
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Academy;

namespace SquashSim.Tests
{
    public class QuizMakerTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheSameDayTheSameCalls()
        {
            var a = QuizMaker.Make(1234, 5); var b = QuizMaker.Make(1234, 5); var c = QuizMaker.Make(1235, 5);
            Assert.That(a.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++) { Assert.That(b[i].Text, Is.EqualTo(a[i].Text)); Assert.That(b[i].Wy, Is.EqualTo(a[i].Wy)); }
            bool differs = false;
            for (int i = 0; i < 5; i++) if (c[i].Wy != a[i].Wy || c[i].Kind != a[i].Kind) differs = true;
            Assert.That(differs, Is.True, "another day, other calls");
        }

        [Test] public void EveryAnswerIsTheRefereesAndTheyAreMixed()
        {
            var answers = new HashSet<string>();
            for (uint seed = 1; seed <= 12; seed++)
                foreach (var q in QuizMaker.Make(seed, 10))
                {
                    Assert.That(RulesQuiz.Answer(q), Is.EqualTo(q.Intended), q.Text);
                    Assert.That(q.Choices, Does.Contain(q.Intended));
                    Assert.That(q.Explain, Is.Not.Empty);
                    answers.Add(q.Intended);
                }
            foreach (var want in new[] { "In", "Out", "Good serve", "Fault", "Down", "Good", "Server", "Receiver", "Nobody" })
                Assert.That(answers, Does.Contain(want), "a mix of calls: " + want);
        }

        [Test] public void AQuizIdIsALesson()
        {
            var l = Lessons.Find(QuizMaker.Id(77, 5));
            Assert.That(l, Is.Not.Null);
            Assert.That(l.IsChallengeQuiz, Is.True);
            Assert.That(l.Steps.Count, Is.EqualTo(5));
            Assert.That(l.QuizPass, Is.EqualTo(4));
            Assert.That(Lessons.Find(QuizMaker.Id(77, 10)).QuizPass, Is.EqualTo(9));
            Assert.That(Lessons.Find("move").QuizPass, Is.EqualTo(2), "the Academy's own: 2 of 3 as before");
        }
    }
}
