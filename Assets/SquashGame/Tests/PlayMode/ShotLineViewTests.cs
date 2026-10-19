// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ShotLineViewTests.cs
// The shot-line dot on the practice court: standing on the left, the
// ring on your half puts the dot on the straight line by the left wall;
// the ring across puts it on the cross line. Writes captures of both.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ShotLineViewTests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }

        [UnityTest]
        public IEnumerator TheDotShowsStraightOrCross()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            LabSimRunner r = court.Runner;
            var aim = GameObject.Find("AimTarget0").GetComponent<AimTargetView>();
            yield return WaitReal(0.3f);
            r.Sim.Player.Player.Pos.X = -2.0; r.Sim.Player.Player.Pos.Z = 7.5;      // back left, ball in hand

            r.Input.SetReticle(-0.6f, 1.8f);                                         // your half: straight
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] straight: dot at x " + aim.LineX.ToString("0.00") + " (ring " + aim.WorldPoint.x.ToString("0.00") + ")");
            Assert.That(aim.LineShown, Is.True);
            Assert.That(aim.LineX, Is.LessThan(-1.6), "the straight line, by the left wall");
            yield return FrontEndTests.CaptureUI(fe, "Lines_Straight.png");

            r.Input.SetReticle(0.6f, 1.8f);                                          // across: cross-court
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] cross: dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(aim.LineX, Is.GreaterThan(0.0), "the cross line");
            yield return FrontEndTests.CaptureUI(fe, "Lines_Cross.png");
        }
    }
}
