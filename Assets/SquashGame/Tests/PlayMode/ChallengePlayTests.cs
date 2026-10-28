// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ChallengePlayTests.cs
// Playable challenges (2026-10-28) in the real scenes, on the keyboard:
//   - the SHOT PUZZLE from the hub: the AIs' rally plays and freezes at
//     its moment, you pick a shot, it is played, the answer shows (what
//     each shot wins); solved: the daily is done; back to the challenges
//   - the RULES QUIZ: the Academy plays today's five calls; all right:
//     passed, the daily done, back to the challenges
// Writes Captures/CH_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Progression;
using SquashGame.UI;
using SquashSim.Academy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ChallengePlayTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false;
            ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            ps.targetTexture = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        static IEnumerator Menu()
        {
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            yield return null; yield return null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheShotPuzzleFreezesYouChooseAndItShowsTheAnswer()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "CHALLENGES", "SHOT PUZZLE");
            Assert.That(fe.Top, Is.InstanceOf<PuzzleScreen>());
            var m = fe.Match;
            Assert.That(m.Puzzle, Is.Not.Null, "today's puzzle from the bank");
            var p = m.Puzzle;
            yield return Wait(1.5f, () => false);
            yield return CaptureUI(fe, "CH_Puzzle_Watch.png");

            yield return Wait(40f, () => m.PuzzleStage == MatchRunner.PuzzleStep.Choosing);
            Assert.That(m.PuzzleStage, Is.EqualTo(MatchRunner.PuzzleStep.Choosing), "the rally froze");
            Assert.That(m.Sim.StepCount, Is.EqualTo(p.Freeze), "at its moment, to the step");
            Assert.That(SquashSim.Modes.ShotPuzzles.IsFreezeMoment(m.Sim), Is.True, "the same moment the puzzle was measured at");
            yield return null; yield return null;
            Assert.That(((PuzzleScreen)fe.Top).Stage, Is.EqualTo(MatchRunner.PuzzleStep.Choosing));
            Assert.That(fe.Top.Reachable().FindAll(i => i is CardItem).Count, Is.EqualTo(p.Options.Count), "the shots to choose from");
            yield return CaptureUI(fe, "CH_Puzzle_Choose.png");

            // the best one (it is what the test checks: solved)
            for (int i = 0; i < 6 && !(fe.Top.Focused is CardItem c && c.Title == SquashSim.Modes.ShotPuzzles.Name(p.Options[p.Best]).ToUpperInvariant()); i++) yield return Tap(kb.rightArrowKey);
            for (int i = 0; i < 6 && !(fe.Top.Focused is CardItem c && c.Title == SquashSim.Modes.ShotPuzzles.Name(p.Options[p.Best]).ToUpperInvariant()); i++) yield return Tap(kb.leftArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(m.PuzzleChoice, Is.EqualTo(p.Best));
            Assert.That(m.PuzzleStage, Is.EqualTo(MatchRunner.PuzzleStep.Playing));
            yield return Wait(1.0f, () => false);
            yield return CaptureUI(fe, "CH_Puzzle_Playing.png");
            yield return Wait(40f, () => fe.Top is PuzzleScreen ps && ps.Stage == MatchRunner.PuzzleStep.Over);
            Assert.That(((PuzzleScreen)fe.Top).Stage, Is.EqualTo(MatchRunner.PuzzleStep.Over));
            Assert.That(Challenges.Today(Challenges.Puzzle).Done, Is.True, "solved: today's puzzle done");
            yield return Wait(0.8f, () => false);
            yield return CaptureUI(fe, "CH_Puzzle_Result.png");

            yield return Tap(kb.escapeKey);
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<ChallengesScreen>(), "back to the challenges");
            Assert.That(fe.Match.Puzzle, Is.Null, "the menus' rally again");
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "CH_Challenges.png");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheRulesQuizIsTodaysFiveCalls()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Menu();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            string id = Challenges.QuizLessonId(false);
            yield return HubNav.Open(fe, kb, Tap, "CHALLENGES", "RULES QUIZ");
            yield return Wait(8f, () => { var f = Object.FindFirstObjectByType<FrontEnd>(); return f != null && f.Academy != null; });
            fe = Object.FindFirstObjectByType<FrontEnd>();
            var s = fe.Academy.Session;
            Assert.That(fe.Academy.Lesson.Id, Is.EqualTo(id), "today's quiz");
            Assert.That(fe.Academy.Lesson.Steps.Count, Is.EqualTo(Challenges.QuizSize));
            for (int q = 0; q < Challenges.QuizSize; q++)
            {
                yield return Wait(10f, () => fe.Top is QuizScreen);
                Assert.That(fe.Top, Is.InstanceOf<QuizScreen>(), "call " + (q + 1));
                if (q == 0) yield return CaptureUI(fe, "CH_Quiz.png");
                var question = s.Current.Question;
                int right = System.Array.IndexOf(question.Choices, RulesQuiz.Answer(question));
                var screen = fe.Top;
                for (int i = 0; i < right; i++) yield return Tap(kb.rightArrowKey);
                yield return Tap(kb.enterKey);
                Assert.That(fe.Top, Is.InstanceOf<QuizRevealScreen>());
                Assert.That(((QuizRevealScreen)fe.Top).Right, Is.True);
                yield return Tap(kb.enterKey);
            }
            yield return Wait(4f, () => fe.Top is ChallengeQuizDoneScreen);
            Assert.That(fe.Top, Is.InstanceOf<ChallengeQuizDoneScreen>(), "5 of 5: passed");
            Assert.That(Challenges.Today(Challenges.Quiz).Done, Is.True);
            yield return CaptureUI(fe, "CH_Quiz_Passed.png");
            yield return Tap(kb.enterKey);                                            // back to the challenges
            yield return Wait(8f, () => { var f = Object.FindFirstObjectByType<FrontEnd>(); return f != null && f.Top is ChallengesScreen; });
            Assert.That(Object.FindFirstObjectByType<FrontEnd>().Top, Is.InstanceOf<ChallengesScreen>());
        }
    }
}
