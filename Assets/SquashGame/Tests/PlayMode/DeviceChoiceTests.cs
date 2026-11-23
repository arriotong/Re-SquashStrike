// ===========================================================
// Assets/SquashGame/Tests/PlayMode/DeviceChoiceTests.cs
// 2026-11-23 Local Versus with any devices (InputSetup, ControlsScreen):
//   - one keyboard split: the left half moves and hits player 1 (WASD,
//     Space), the right half player 2 (arrows, Enter) - neither the other
//   - two controllers either way round: player 1 on pad 2, player 2 on pad 1
//   - the controls step: every device listed, a row lit while its device is
//     used, picking the other's choice swaps them
// (Separate physical keyboards and mice are told apart by Windows' raw input
// in the built game only - RawInput - so not here.)
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class DeviceChoiceTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        IEnumerator Start2P(System.Action<FrontEnd> got)
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return null;
            ProfileStore.Current.Settings.WarmUp = false;
            var s = MatchSettings.QuickGame(); s.TwoPlayer = true;
            fe.StartMatch(s);
            yield return Wait(0.4f);
            if (fe.Match.ReplayActive) fe.Match.StopReplay();
            got(fe);
        }

        [UnityTest]
        public IEnumerator OneKeyboardSplitInTwo()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            var c = ProfileStore.Current.Choices;
            c.VsControls1 = "kb:merged:left"; c.VsControls2 = "kb:merged:right"; c.VsMouse1 = ""; c.VsMouse2 = "";
            FrontEnd fe = null;
            yield return Start2P(f => fe = f);
            var r = fe.Match;
            Assert.That(r.Settings.TwoPlayer, Is.True, "two players with one keyboard");
            Assert.That(r.Input.Controls.Layout, Is.SameAs(KeyLayout.LeftHalf));
            Assert.That(r.Input2.Controls.Layout, Is.SameAs(KeyLayout.RightHalf));

            Vector3 a0 = r.View.A0.Pos, a1 = r.View.A1.Pos;
            Press(kb.dKey);
            yield return Wait(0.5f);
            Release(kb.dKey);
            yield return null;
            float m0 = Vector3.Distance(r.View.A0.Pos, a0), m1 = Vector3.Distance(r.View.A1.Pos, a1);
            Assert.That(m0, Is.GreaterThan(0.3f), "D moves player 1");
            Assert.That(m1, Is.LessThan(0.05f), "and not player 2");

            a0 = r.View.A0.Pos; a1 = r.View.A1.Pos;
            Press(kb.leftArrowKey);
            yield return Wait(0.5f);
            Release(kb.leftArrowKey);
            yield return null;
            m0 = Vector3.Distance(r.View.A0.Pos, a0); m1 = Vector3.Distance(r.View.A1.Pos, a1);
            Assert.That(m1, Is.GreaterThan(0.3f), "the left arrow moves player 2");
            Assert.That(m0, Is.LessThan(0.05f), "and not player 1");

            // aim: TFGH for player 1, IJKL for player 2
            float r1 = r.Input.ReticleX, r2 = r2X(r);
            Press(kb.hKey); yield return Wait(0.3f); Release(kb.hKey); yield return null;
            Assert.That(r.Input.ReticleX, Is.GreaterThan(r1 + 0.1f), "H aims player 1 right");
            Assert.That(r2X(r), Is.EqualTo(r2).Within(1e-4f), "not player 2's aim");
            // the serve: the server's own hit key tosses (Space for player 1, Enter would be player 2's)
            var m = r.Sim;
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            int server = m.Score.Server;
            ButtonControl other = server == 0 ? kb.enterKey : kb.spaceKey, mine = server == 0 ? kb.spaceKey : kb.enterKey;
            Press(other); yield return null; Release(other); yield return Wait(0.2f);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "the receiver's key does nothing");
            Press(mine); yield return null; Release(mine); yield return Wait(0.25f);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Rally), "the server's key serves");
        }
        static float r2X(MatchRunner r) { return r.Input2.ReticleX; }

        [UnityTest]
        public IEnumerator TwoControllersEitherWayRound()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            var pad1 = InputSystem.AddDevice<Gamepad>(); var pad2 = InputSystem.AddDevice<Gamepad>();
            var prim = InputSetup.Primaries();
            var o1 = prim.Find(o => o.Unity == pad1); var o2 = prim.Find(o => o.Unity == pad2);
            var c = ProfileStore.Current.Choices;
            c.VsControls1 = o2.Id; c.VsControls2 = o1.Id;                                  // player 1 on pad 2
            FrontEnd fe = null;
            yield return Start2P(f => fe = f);
            var r = fe.Match;
            Assert.That(r.Input.Devices, Is.EquivalentTo(new InputDevice[] { pad2 }), "player 1: pad 2 only");
            Assert.That(r.Input2.Devices, Is.EquivalentTo(new InputDevice[] { pad1 }), "player 2: pad 1 only");
            Vector3 a0 = r.View.A0.Pos, a1 = r.View.A1.Pos;
            Set(pad2.leftStick, new Vector2(1f, 0f));
            yield return Wait(0.5f);
            Set(pad2.leftStick, Vector2.zero);
            yield return null;
            Assert.That(Vector3.Distance(r.View.A0.Pos, a0), Is.GreaterThan(0.3f), "pad 2 moves player 1");
            Assert.That(Vector3.Distance(r.View.A1.Pos, a1), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator TheControlsStepListsLightsAndSwaps()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            var pad = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return null;
            fe.Push(new LocalVersusScreen());
            yield return null;
            fe.Push(new ControlsScreen());
            yield return null; yield return null;
            var cs = (ControlsScreen)fe.Top;
            var c = ProfileStore.Current.Choices;
            Assert.That(cs.Rows, Is.EqualTo(InputSetup.Primaries().Count + InputSetup.Mice().Count - 1), "every controller, keyboard (whole and halves) and mouse");
            Assert.That(c.VsControls1, Does.StartWith("kb:"), "one pad: the keyboard and mouse ...");
            Assert.That(c.VsControls2, Does.StartWith("pad:"), "... against the pad");
            Assert.That(cs.RowsLit, Is.EqualTo(0), "nothing lit: " + cs.LitNames);
            Set(pad.rightStick, new Vector2(1f, 0f)); yield return null; yield return null;       // (B would be Back)
            Assert.That(cs.RowsLit, Is.GreaterThanOrEqualTo(1), "pressing the pad lights its row");
            yield return FrontEndTests.CaptureUI(fe, "CONTROLS_Step.png");
            Set(pad.rightStick, Vector2.zero); yield return null; yield return null;
            Assert.That(cs.RowsLit, Is.EqualTo(0), "released: " + cs.LitNames);
            // player 1 takes the pad: player 2 gets the keyboard
            var p1 = (CarouselItem)cs.Reachable().Find(i => i is CarouselItem);
            int padAt = System.Array.FindIndex(p1.Values, v => v.StartsWith("Controller"));
            p1.Pick(padAt);
            Assert.That(c.VsControls1, Does.StartWith("pad:"), "player 1 on the pad");
            Assert.That(c.VsControls2, Does.StartWith("kb:"), "the keyboard went to player 2");
            Assert.That(c.VsMouse1, Is.Empty, "a pad needs no mouse");
        }
    }
}
