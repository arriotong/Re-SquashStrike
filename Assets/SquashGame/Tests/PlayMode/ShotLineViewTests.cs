// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ShotLineViewTests.cs
// The shot-line dot on the practice court: standing on the left, the
// ring anywhere on the left half puts the dot on the straight line (even
// near the middle); on the right half, the cross. From the front right,
// the ring on the left half is the cross, whose line meets the front wall
// on your own half (2026-10-28: the aim's half is the corner). Writes
// captures.
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

            r.Input.SetReticle(-2.4f, 1.8f);                                         // by your wall: straight
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] straight: dot at x " + aim.LineX.ToString("0.00") + " (ring " + aim.WorldPoint.x.ToString("0.00") + ")");
            Assert.That(aim.LineShown, Is.True);
            Assert.That(aim.LineX, Is.LessThan(-1.6), "the straight line, by the left wall");
            yield return FrontEndTests.CaptureUI(fe, "Lines_Straight.png");

            r.Input.SetReticle(-0.4f, 1.8f);                                         // the left half, near the middle: still straight
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] straight from the middle of the wall: dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(aim.LineX, Is.LessThan(-1.6), "the left corner's line");

            r.Input.SetReticle(0.6f, 1.8f);                                          // the right half: cross-court
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] cross: dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(aim.LineX, Is.GreaterThan(0.0), "the cross line, on the right half");
            yield return FrontEndTests.CaptureUI(fe, "Lines_Cross.png");

            // from the front right: the cross point is on your own half - still a cross
            r.Sim.Player.Player.Pos.X = 2.0; r.Sim.Player.Player.Pos.Z = 4.0;
            float crossPoint = (float)SquashSim.ArcadeStroke.ShotLine(2.0, 4.0, "drive", -3.0);
            r.Input.SetReticle(-1.0f, 1.8f);
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] cross from the front: textbook " + crossPoint.ToString("0.00") + ", dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(crossPoint, Is.GreaterThan(0f), "the cross point is on your own half");
            Assert.That(aim.LineX, Is.LessThan(1.6), "not the straight line by the wall");
            yield return FrontEndTests.CaptureUI(fe, "Lines_CrossFromFront.png");
        }
    }
}
