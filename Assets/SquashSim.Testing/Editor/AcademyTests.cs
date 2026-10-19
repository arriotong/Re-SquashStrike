// ===========================================================
// Assets/SquashSim.Testing/Editor/AcademyTests.cs
// P2-M3: the Academy, headless.
//   - every lesson (controls and rules) can be passed: the lesson bot
//     walks, sprints, dives, aims, plays the asked shots, serves, reads
//     the cards and makes the calls
//   - every quiz question's right answer, from the sim, is the call it
//     was written for (so the quiz can never mark you wrongly)
//   - a quiz with fewer than 2 right fails and can be retried
//   - the steps' checks: a wrong shot type does not count
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Academy;

namespace SquashSim.Tests
{
    public class AcademyTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static AcademySession Run(Lesson lesson, double seconds = 240)
        {
            var s = new AcademySession(lesson, 11);
            var bot = new LessonBot();
            var it = new InputIntent();
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps && !s.Done; i++) { bot.Fill(it, s, Dt); s.Step(it, Dt); }
            return s;
        }

        [Test] public void EveryLessonCanBePassed()
        {
            foreach (var lesson in Lessons.All())
            {
                var s = Run(lesson);
                UnityEngine.Debug.Log("[Academy] " + lesson.Id + ": " + (s.Done ? "passed" : "STUCK on step " + (s.StepIndex + 1) + " (" + s.Current.Goal + ", " + s.Progress + "/" + s.Current.Count + ")") +
                                      " in " + s.Time.ToString("0") + " s");
                Assert.That(s.Done, Is.True, lesson.Id + " should be passable");
            }
        }

        [Test] public void EveryQuizAnswerIsTheIntendedCall()
        {
            int n = 0;
            foreach (var lesson in Lessons.All())
                foreach (var st in lesson.Steps)
                {
                    if (st.Question == null) continue;
                    n++;
                    string a = RulesQuiz.Answer(st.Question);
                    Assert.That(a, Is.EqualTo(st.Question.Intended), lesson.Id + ": " + st.Question.Text + " (" + st.Question.Explain + ")");
                    Assert.That(st.Question.Choices, Does.Contain(a), "the right call is one of the choices");
                }
            Assert.That(n, Is.EqualTo(15), "P2-M7: three staged interference calls");
        }

        [Test] public void TheLetsLessonStagesTwoAthletes()
        {
            // P2-M7: the cards play a real staged moment (the opponent shown, the ball flying), the quiz stops as you would ask
            var s = new AcademySession(Lessons.Find("lets"), 1);
            var it = new InputIntent();
            Assert.That(s.Staged, Is.True, "the first card stages the moment");
            double z0 = s.Ball.Pos.Z, ox = s.Opponent.Player.Pos.X;
            for (int i = 0; i < 30; i++) s.Step(it, Dt);
            Assert.That(s.Opponent.Player.Pos.X, Is.EqualTo(ox).Within(0.05), "the opponent stands in the way (until bumped aside)");
            for (int i = 0; i < 90; i++) s.Step(it, Dt);
            Assert.That(s.Ball.Pos.Z, Is.Not.EqualTo(z0).Within(0.5), "the ball is in play");
            Assert.That(s.Player.Player.Pos.X, Is.LessThan(s.Stage.TargetX + 2.9), "the striker runs for the ball");
            for (int k = 0; k < 4; k++) s.NextCard();
            Assert.That(s.Current.Goal, Is.EqualTo(GoalKind.Quiz));
            for (int i = 0; i < 240 * 3 && !s.AwaitingAnswer; i++) s.Step(it, Dt);
            Assert.That(s.AwaitingAnswer, Is.True, "stopped where you would ask");
            Assert.That(s.Stage.Sim.Referee.Outcome, Is.Null, "before any call");
            Assert.That(s.Answer(System.Array.IndexOf(s.Current.Question.Choices, "Let")), Is.True, "they were clearing: a let");
        }

        [Test] public void AFailedQuizCanBeRetried()
        {
            var s = new AcademySession(Lessons.Find("ends"), 1);
            var it = new InputIntent();
            int guard = 0;
            while (!s.Done && !s.QuizFailed && guard++ < 240 * 60)
            {
                if (s.Current.Goal == GoalKind.Card) s.NextCard();
                else if (s.AwaitingAnswer)
                {
                    var q = s.Current.Question;
                    int wrong = (System.Array.IndexOf(q.Choices, RulesQuiz.Answer(q)) + 1) % q.Choices.Length;
                    s.Answer(wrong);
                }
                s.Step(it, Dt);
            }
            Assert.That(s.QuizFailed, Is.True, "all wrong: the quiz is failed");
            Assert.That(s.Done, Is.False);
            s.RetryQuiz();
            Assert.That(s.Current.Goal, Is.EqualTo(GoalKind.Quiz), "back to the first question");
            Assert.That(s.QuizRight, Is.EqualTo(0));
        }

        [Test] public void AWrongShotTypeDoesNotCount()
        {
            // The "height" lesson's first step wants a drive; a bot told to play lobs never passes it.
            var lesson = Lessons.Find("height");
            var s = new AcademySession(lesson, 3);
            var bot = new LessonBot();
            var it = new InputIntent();
            var lob = lesson.Steps[1];
            lesson.Steps[0].Shot = "lob";                              // the bot plays what the step says...
            try
            {
                var check = new AcademySession(lesson, 3);
                lesson.Steps[0].Shot = "drive";                         // ...but the session wants a drive
                for (int i = 0; i < (int)(40 / Dt); i++) { bot.Fill(it, check, Dt); if (check.Current.Shot == "drive") it.AimPointY = 3.5; check.Step(it, Dt); }
                Assert.That(check.StepIndex, Is.EqualTo(0), "lobs do not pass a drive step");
            }
            finally { lesson.Steps[0].Shot = "drive"; }
        }
    }
}
