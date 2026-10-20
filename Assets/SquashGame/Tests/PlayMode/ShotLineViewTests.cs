// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ShotLineViewTests.cs
// The shot-line dot on the practice court: standing on the left, the
// ring by your wall puts the dot on the straight line; the ring nearer the
// cross line pulls the dot toward the cross. From the front, the ring on
// the true cross point (on your own half) stays a cross - it used to be
// read as a drive (2026-10-20). Writes captures.
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

            r.Input.SetReticle(-0.6f, 1.8f);                                         // nearer the cross line: cross-court
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] cross: dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(aim.LineX, Is.GreaterThan(-0.6 + 0.1), "pulled toward the cross, not the wall");
            yield return FrontEndTests.CaptureUI(fe, "Lines_Cross.png");

            // from the front right: the cross point is on your own half - still a cross
            r.Sim.Player.Player.Pos.X = 2.0; r.Sim.Player.Player.Pos.Z = 4.0;
            float crossPoint = (float)SquashSim.ArcadeStroke.ShotLine(2.0, 4.0, "drive", -3.0);
            r.Input.SetReticle(crossPoint, 1.8f);
            yield return WaitReal(0.3f);
            Debug.Log("[Lines] cross from the front: ring " + crossPoint.ToString("0.00") + ", dot at x " + aim.LineX.ToString("0.00"));
            Assert.That(crossPoint, Is.GreaterThan(0f), "the cross point is on your own half");
            Assert.That(aim.LineX, Is.LessThan(crossPoint + 0.4), "not pulled out to the straight line by the wall");
            yield return FrontEndTests.CaptureUI(fe, "Lines_CrossFromFront.png");
        }
    }
}
