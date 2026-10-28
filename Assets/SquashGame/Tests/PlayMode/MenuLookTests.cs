// ===========================================================
// Assets/SquashGame/Tests/PlayMode/MenuLookTests.cs
// The old SquashStrike look:
//   - behind the main menu the demo rally plays in slow motion and the
//     camera cuts between cinematic angles; focusing another row
//     changes the set of angles and the glowing preview
//   - starting a match gives the gameplay camera and full speed back
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class MenuLookTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

        static IEnumerator Wait(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        [UnityTest]
        public IEnumerator TheMenuBackgroundIsASlowMotionCinematic()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f && Object.FindFirstObjectByType<FrontEnd>() == null) yield return null;
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var menu = fe.Top as MainMenuScreen;
            Assert.That(menu, Is.Not.Null);
            Assert.That(fe.Match.Demo, Is.True);
            Assert.That(fe.Match.Clock.TimeScale, Is.EqualTo(MatchRunner.DemoSpeed).Within(1e-9), "the demo rally is in slow motion");
            Assert.That(menu.Focused.Name, Is.EqualTo("QUICK MATCH"));
            Assert.That(menu.CardsInView, Is.LessThanOrEqualTo(4), "at most 4 cards in view");

            var cam = Camera.main.transform;
            Vector3 a = cam.position;
            yield return Wait(MenuCinematics.CutSeconds + 0.5f);
            Vector3 b = cam.position;
            Assert.That(Vector3.Distance(a, b), Is.GreaterThan(1.0f), "the camera cut to another angle");

            yield return Tap(kb.rightArrowKey);                                   // Local Versus
            Assert.That(menu.Focused.Name, Is.EqualTo("LOCAL VERSUS"));
            Assert.That(MenuCinematics.Scene, Is.EqualTo(1), "its own set of angles");
            yield return null; yield return null;
            Assert.That(Vector3.Distance(cam.position, b), Is.GreaterThan(1.0f), "a new card, a new set of angles");
            foreach (var tab in new[] { "CAREER", "CHALLENGES", "MODES", "ONLINE", "TRAINING", "YOU", "REPLAYS" })
            {
                yield return Tap(kb.eKey);
                Assert.That(menu.Tabs.Current, Is.EqualTo(tab));
                Assert.That(menu.CardsInView, Is.InRange(1, 4), tab + ": at most 4 cards in view");
            }
            // UI remake: the rail - up / down step the sections (from the cards), each section has its live line
            yield return null; yield return null;
            yield return Tap(kb.downArrowKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("PLAY"), "down from the last section: round to the first");
            yield return null; yield return null;
            yield return Tap(kb.downArrowKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("CAREER"), "down: the next section");
            yield return null; yield return null;
            yield return Tap(kb.upArrowKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("PLAY"), "up: the section above");
            yield return null; yield return null;
            yield return Tap(kb.upArrowKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("PLAY"), "up from the first: the top bar, not round");
            Assert.That(menu.Focused, Is.Not.InstanceOf<CardItem>(), "the settings / profile chips up top");
            Assert.That(menu.Tabs.Style, Is.EqualTo(TabStyle.Rail));
            Assert.That(menu.Tabs.Sub(0), Does.StartWith("v "), "PLAY's line: who you play");
            Assert.That(menu.Tabs.Sub(3), Does.Contain("today"), "CHALLENGES' line");

            fe.StartMatch(MatchSettings.QuickGame());
            yield return null; yield return null;
            Assert.That(fe.Match.Clock.TimeScale, Is.EqualTo(SimRunnerBase.GameSpeed).Within(1e-9), "full speed in a match");
            Assert.That(cam.position.z, Is.GreaterThan((float)SquashSim.CourtDimensions.LENGTH), "the gameplay camera, behind the glass");
        }
    }
}
