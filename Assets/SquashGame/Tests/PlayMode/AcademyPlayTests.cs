// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AcademyPlayTests.cs
// P2-M3: the Academy in the real scenes, on the virtual keyboard.
//   - a new profile is pointed to the Academy once, and only once
//   - main menu -> Academy -> Continue: Move; walk, sprint and dive to
//     the spots with WASD / Shift / Space -> lesson passed -> saved,
//     ticked on the Academy menu
//   - a rules lesson: the cards (Enter), the lines lit; the quiz all
//     wrong -> failed -> try again -> all right -> passed
// Writes Captures/P2M3_UI_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim.Academy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class AcademyPlayTests : InputTestFixture
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

        static IEnumerator Loaded(string scene)
        {
            yield return Wait(5f, () => SceneManager.GetActiveScene().name == scene && Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator ANewPlayerIsPointedToTheAcademyOnce()
        {
            LocalMatchTests.FreshProfile(newPlayer: true);
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Loaded("Match");
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.InstanceOf<AcademySuggestScreen>(), "a new player is asked");
            yield return CaptureUI(fe, "P2M3_UI_1_Suggest.png");
            Assert.That(ProfileStore.Load().Academy.Suggested, Is.True, "saved: asked once");
            yield return Tap(kb.enterKey);                                         // Start the Academy
            Assert.That(fe.Top, Is.InstanceOf<AcademyScreen>());
            yield return CaptureUI(fe, "P2M3_UI_2_Academy.png");
            yield return Tap(kb.escapeKey);
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());

            SceneManager.LoadScene("Match");                                      // the next time: not again
            yield return Loaded("Match");
            fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>(), "only once");
            Assert.That(fe.Top.Find("ACADEMY"), Is.Not.Null, "the Academy card is on the hub");
        }

        /// <summary>Hold the WASD keys that walk the player towards (x, z) - D is +x, W is towards the front wall.</summary>
        void Steer(Keyboard kb, AcademyRunner r, double x, double z)
        {
            var p = r.Session.Player.Player.Pos;
            double dx = x - p.X, dz = z - p.Z;
            Set(kb.dKey, dx > 0.15); Set(kb.aKey, dx < -0.15);
            Set(kb.sKey, dz > 0.15); Set(kb.wKey, dz < -0.15);
        }

        void Set(KeyControl k, bool down) { if (down && !k.isPressed) Press(k); else if (!down && k.isPressed) Release(k); }

        void Let(Keyboard kb) { Set(kb.wKey, false); Set(kb.aKey, false); Set(kb.sKey, false); Set(kb.dKey, false); Set(kb.leftShiftKey, false); }

        [UnityTest]
        public IEnumerator TheMoveLessonOnTheKeyboard()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Loaded("Match");
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return HubNav.Open(fe, kb, Tap, "ACADEMY", "ALL LESSONS");
            Assert.That(fe.Top, Is.InstanceOf<AcademyScreen>());
            yield return null; yield return null;
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Move"), "the trail starts on the next lesson");
            yield return CaptureUI(fe, "U4_Academy_Trail.png");
            yield return Tap(kb.enterKey);

            yield return Loaded("WarmUpCourt");
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            var r = court.Academy;
            fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(r, Is.Not.Null, "the practice court runs the lesson");
            Assert.That(r.Lesson.Id, Is.EqualTo("move"));
            Assert.That(fe.Top, Is.Null, "straight into the lesson");
            Assert.That(fe.LessonHud.Instruction, Is.EqualTo("Walk to the lit spot."));
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "P2M3_UI_3_MoveLesson.png");
            var targets = Object.FindFirstObjectByType<TargetView>();
            Assert.That(targets.Showing, Is.EqualTo(1), "the spot is drawn");

            var s = r.Session;
            float t0 = Time.realtimeSinceStartup;
            int dives = 0; float diveWait = 0f;
            while (!s.Done && Time.realtimeSinceStartup - t0 < 60f)
            {
                var st = s.Current;
                if (st.Goal == GoalKind.ReachSpots || st.Goal == GoalKind.SprintTo)
                {
                    if (s.Targets.Count > 0) Steer(kb, r, s.Targets[0].X, s.Targets[0].Z0); else Let(kb);
                    Set(kb.leftShiftKey, st.Goal == GoalKind.SprintTo);
                }
                else if (st.Goal == GoalKind.Dive)
                {
                    Set(kb.leftShiftKey, false);
                    Steer(kb, r, s.Player.Player.Pos.X > 0 ? -2.0 : 2.0, 5.0);
                    diveWait += Time.unscaledDeltaTime;
                    if (diveWait > 1.6f) { diveWait = 0f; dives++; yield return Tap(kb.spaceKey); continue; }
                }
                yield return null;
            }
            Let(kb);
            Assert.That(s.Done, Is.True, "walked, sprinted and dived through the lesson (stuck on step " + (s.StepIndex + 1) + ")");
            Debug.Log("[P2M3] move lesson on the keyboard in " + s.Time.ToString("0.0") + " s of play, " + dives + " dive presses");

            yield return Wait(3f, () => fe.Top is LessonCompleteScreen);
            Assert.That(fe.Top, Is.InstanceOf<LessonCompleteScreen>());
            yield return CaptureUI(fe, "P2M3_UI_4_LessonPassed.png");
            Assert.That(ProfileStore.Load().Academy.Done, Does.Contain("move"), "saved");
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Next: Aim"));
            yield return HubNav.Seek(fe, "Academy", kb.leftArrowKey, kb.upArrowKey, Tap);
            yield return Tap(kb.enterKey);

            yield return Loaded("Match");
            fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(fe.Top, Is.InstanceOf<AcademyScreen>(), "back on the Academy menu");
            yield return null; yield return null;
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Aim"));
            Assert.That(((TrailNode)fe.Top.Find("Move")).Done, Is.True, "ticked on the trail");
            yield return CaptureUI(fe, "U4_Academy_TrailAfter.png");
        }

        [UnityTest]
        public IEnumerator TheLetsLessonStagesTheMoments()
        {
            // P2-M7: two athletes and a live ball on the cards; the quiz asks at the moment you would appeal
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchLesson("lets");
            yield return Loaded("WarmUpCourt");
            var r = Object.FindFirstObjectByType<WarmUpCourt>().Academy;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var s = r.Session;
            yield return Wait(1.2f, () => false);
            Assert.That(s.Staged, Is.True);
            Assert.That(r.View.Count, Is.EqualTo(2), "the opponent is drawn");
            yield return CaptureUI(fe, "P2M7_Academy_Card.png");
            for (int k = 0; k < 4; k++) yield return Tap(kb.enterKey);
            Assert.That(s.Current.Goal, Is.EqualTo(GoalKind.Quiz));
            yield return Wait(8f, () => fe.Top is QuizScreen);
            Assert.That(fe.Top, Is.InstanceOf<QuizScreen>(), "asked as the striker would appeal");
            yield return CaptureUI(fe, "P2M7_Academy_Quiz.png");
            var question = s.Current.Question;
            Assert.That(question.Kind, Is.EqualTo(QuizKind.Interference));
            yield return Choose(kb, fe, System.Array.IndexOf(question.Choices, RulesQuiz.Answer(question)));
            Assert.That(fe.Top, Is.InstanceOf<QuizRevealScreen>());
            Assert.That(((QuizRevealScreen)fe.Top).Right, Is.True);
        }

        [UnityTest]
        public IEnumerator ARulesLessonCardsAndQuiz()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchLesson("ends");
            yield return Loaded("WarmUpCourt");
            var r = Object.FindFirstObjectByType<WarmUpCourt>().Academy;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var lines = Object.FindFirstObjectByType<CourtHighlight>();
            var s = r.Session;
            Assert.That(r.Lesson.Id, Is.EqualTo("ends"));

            yield return null;
            Assert.That(lines.Showing, Is.EqualTo("tin"), "the first card lights the tin");
            yield return CaptureUI(fe, "P2M3_UI_5_RulesCard.png");
            yield return Tap(kb.enterKey);
            Assert.That(s.StepIndex, Is.EqualTo(1), "Enter reads on");
            yield return null;
            Assert.That(lines.Showing, Is.EqualTo("outlines"));
            yield return Tap(kb.enterKey);
            yield return Tap(kb.enterKey);
            Assert.That(s.Current.Goal, Is.EqualTo(GoalKind.Quiz));
            yield return null;
            Assert.That(lines.Showing, Is.Null);

            // ---- all wrong: failed ----
            for (int q = 0; q < 3; q++)
            {
                yield return Wait(8f, () => fe.Top is QuizScreen);
                Assert.That(fe.Top, Is.InstanceOf<QuizScreen>(), "question " + (q + 1) + " is asked once the shot is judged");
                if (q == 0) yield return CaptureUI(fe, "P2M3_UI_6_Quiz.png");
                yield return null;                                                    // drawn at the end of the frame
                Assert.That(lines.Marking, Is.True, "where the ball went is marked while you make the call");
                Assert.That(r.Clock.Paused, Is.True, "the game waits for the call");
                var question = s.Current.Question;
                int right = System.Array.IndexOf(question.Choices, RulesQuiz.Answer(question));
                yield return Choose(kb, fe, (right + 1) % question.Choices.Length);
                Assert.That(fe.Top, Is.InstanceOf<QuizRevealScreen>());
                Assert.That(((QuizRevealScreen)fe.Top).Right, Is.False);
                if (q == 0) { yield return Wait(0.6f, () => false); yield return CaptureUI(fe, "P2M3_UI_7_Reveal.png"); }
                yield return Tap(kb.enterKey);
            }
            Assert.That(fe.Top, Is.InstanceOf<QuizFailedScreen>(), "0 of 3: failed");
            yield return CaptureUI(fe, "P2M3_UI_8_QuizFailed.png");
            yield return Tap(kb.enterKey);                                            // try the questions again
            Assert.That(fe.Top, Is.Null);
            Assert.That(s.Current.Goal, Is.EqualTo(GoalKind.Quiz));

            // ---- all right: passed ----
            for (int q = 0; q < 3; q++)
            {
                yield return Wait(8f, () => fe.Top is QuizScreen);
                Assert.That(fe.Top, Is.InstanceOf<QuizScreen>());
                var question = s.Current.Question;
                yield return Choose(kb, fe, System.Array.IndexOf(question.Choices, RulesQuiz.Answer(question)));
                Assert.That(((QuizRevealScreen)fe.Top).Right, Is.True);
                yield return Tap(kb.enterKey);
            }
            yield return Wait(3f, () => fe.Top is LessonCompleteScreen);
            Assert.That(fe.Top, Is.InstanceOf<LessonCompleteScreen>(), "3 of 3: passed");
            Assert.That(ProfileStore.LessonDone("ends"), Is.True);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Next: Scoring"));
        }

        IEnumerator Choose(Keyboard kb, FrontEnd fe, int index)
        {
            var screen = fe.Top;
            for (int i = 0; i < index; i++) yield return Tap(kb.rightArrowKey);
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.Not.SameAs(screen), "answered");
        }
    }
}
