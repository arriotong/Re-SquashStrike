// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ReticleAimTests.cs
// The original SquashStrike controls, with a virtual mouse + keyboard in
// the practice court: the mouse moves the reticle without any button and
// it stays where you leave it (also after a stroke); past the edge of the
// front wall it is on the side wall (a boast); its height picks the shot
// (lob / drive / drop); Space dives (a brief slow-mo, the dive pose).
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Athlete;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ReticleAimTests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        [UnityTest]
        public IEnumerator MouseMovesAPersistentReticle()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var kb = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            LabSimRunner r = court.Runner;
            var ring = GameObject.Find("AimTarget0").GetComponent<AimTargetView>();
            yield return WaitReal(0.3f);
            Assert.That(ring.Fade, Is.GreaterThan(0.9f), "the reticle is always on");
            Assert.That(Mathf.Abs(r.Input.ReticleX), Is.LessThan(0.01f), "it starts in the middle");

            // Move the mouse left, no button: 100 px = 1.5 m of wall.
            for (int i = 0; i < 10; i++) { Set(mouse.delta, new Vector2(-10f, 0f)); yield return null; }
            Set(mouse.delta, Vector2.zero);
            yield return WaitReal(0.2f);
            Debug.Log("[Reticle] after 100 px left: reticle x " + r.Input.ReticleX.ToString("0.00") + ", ring x " + ring.WorldPoint.x.ToString("0.00"));
            Assert.That(r.Input.ReticleX, Is.LessThan(-1.2f), "the mouse alone moves the reticle");
            Assert.That(ring.Wall, Is.EqualTo(0), "still on the front wall");
            Assert.That(Mathf.Abs(Mathf.Abs(ring.WorldPoint.x) - Mathf.Abs(r.Input.ReticleX)), Is.LessThan(0.1f), "the ring is drawn on the reticle");

            // A stroke does not move it back.
            float before = r.Input.ReticleX;
            Press(mouse.leftButton); yield return WaitReal(0.2f);
            Release(mouse.leftButton); yield return WaitReal(1.5f);
            Assert.That(r.Input.ReticleX, Is.EqualTo(before).Within(0.01f), "the reticle stays where you left it");

            // Past the edge of the front wall: a boast off the side wall.
            r.Input.SetReticle(-(float)CourtDimensions.HALF_W - 1.2f, 1.8f);
            yield return WaitReal(0.1f);
            Assert.That(ring.Wall, Is.EqualTo(-1), "past the left edge: the left side wall");

            // Height picks the shot.
            r.Input.SetReticle(0f, 3.6f); yield return WaitReal(0.05f);
            Assert.That(ring.ShotType, Is.EqualTo("lob"));
            r.Input.SetReticle(0f, 1.9f); yield return WaitReal(0.05f);
            Assert.That(ring.ShotType, Is.EqualTo("drive"));
            r.Input.SetReticle(0f, 0.9f); yield return WaitReal(0.05f);
            Assert.That(ring.ShotType, Is.EqualTo("drop"), "low and not charged: a drop");

            // Space dives: in the move direction, a short slow-mo, the dive pose.
            var arcade = r.Sim.Player.Arcade;
            int dives = arcade.DiveCount;
            double x0 = r.Sim.Player.Player.Pos.X;
            Press(kb.dKey);
            yield return WaitReal(0.1f);
            Press(kb.spaceKey); yield return null; yield return null;
            Release(kb.spaceKey);
            bool slowMo = false; float drawn = 0f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.4f)
            {
                if (r.Clock.TimeScale < SimRunnerBase.GameSpeed - 0.1) slowMo = true;
                foreach (var f in Object.FindObjectsByType<AthleteFigure>(FindObjectsSortMode.None))
                    if (f.Index == 0) drawn = Mathf.Max(drawn, f.DiveWeight);
                yield return null;
            }
            Release(kb.dKey);
            yield return WaitReal(0.6f);
            Debug.Log("[Dive] dives " + (arcade.DiveCount - dives) + ", moved " + (r.Sim.Player.Player.Pos.X - x0).ToString("0.00") +
                      " m, slow-mo " + slowMo + ", dive pose " + drawn.ToString("0.00") + ", time scale now " + r.Clock.TimeScale.ToString("0.00"));
            Assert.That(arcade.DiveCount, Is.EqualTo(dives + 1), "Space dives");
            Assert.That(slowMo, Is.True, "a dive slows the game for a moment");
            Assert.That(r.Clock.TimeScale, Is.EqualTo(SimRunnerBase.GameSpeed).Within(1e-6), "and back to normal speed after");
            Assert.That(drawn, Is.GreaterThan(0.5f), "the figure dives");
            Assert.That(r.Sim.Player.Player.Pos.X - x0, Is.GreaterThan(0.5), "the dive carries the athlete toward the move direction");
        }
    }
}
