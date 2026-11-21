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

            // 2026-11-20 behind PLAY's hub now: its toon world (Dioramas), the camera held still on it - no more cuts round the
            // court (the slow-motion rally still plays: the title, the match setup and the studio still show it)
            var cam = Camera.main.transform;
            yield return null; yield return null;
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.Home), "the park island behind PLAY");
            Vector3 a = cam.position;
            yield return Wait(MenuCinematics.CutSeconds + 0.5f);
            Assert.That(Vector3.Distance(a, cam.position), Is.LessThan(0.01f), "held still: a background, not a film");

            yield return Tap(kb.rightArrowKey);                                   // Local Versus
            Assert.That(menu.Focused.Name, Is.EqualTo("LOCAL VERSUS"));
            Assert.That(MenuCinematics.Scene, Is.EqualTo(1), "its own set of angles (for when the court shows)");
            foreach (var tab in new[] { "CAREER", "CHALLENGES", "MODES", "ONLINE", "TRAINING" })      // PLAY's hub (2026-10-29)
            {
                yield return Tap(kb.eKey);
                Assert.That(menu.Tabs.Current, Is.EqualTo(tab));
                Assert.That(menu.CardsInView, Is.InRange(1, 4), tab + ": at most 4 cards in view");
            }
            // 2026-10-30: the Q / E strip in the top bar (no more rail) - E round to the first, Q back
            yield return Tap(kb.eKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("PLAY"), "E from the last section: round to the first");
            yield return Tap(kb.qKey);
            Assert.That(menu.Tabs.Current, Is.EqualTo("TRAINING"), "Q: the one before");
            Assert.That(menu.Tabs.Style, Is.EqualTo(TabStyle.Strip));
            Assert.That(menu.Tabs.worldBound.yMax, Is.LessThan(menu.Coins.worldBound.yMax + 30f), "up in the top bar");
            Assert.That(menu.Tabs.worldBound.xMax, Is.LessThanOrEqualTo(menu.Coins.worldBound.xMin), "on the right, before the coins");
            yield return FrontEndTests.CaptureUI(fe, "H2_TopTabs.png");

            fe.StartMatch(MatchSettings.QuickGame());
            yield return null; yield return null;
            Assert.That(fe.Match.Clock.TimeScale, Is.EqualTo(SimRunnerBase.GameSpeed).Within(1e-9), "full speed in a match");
            Assert.That(cam.position.z, Is.GreaterThan((float)SquashSim.CourtDimensions.LENGTH), "the gameplay camera, behind the glass");
        }
    }
}
