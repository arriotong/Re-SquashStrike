// ===========================================================
// Assets/SquashGame/Tests/PlayMode/DragAimTests.cs
// M9: drag-to-aim with a virtual mouse in the practice court. Press Hit
// and the aim starts in the middle - the circle on the front wall sits in
// the middle; drag left and the circle goes left; let go and, once the
// stroke is over, the circle is back in the middle. Moving the mouse
// without holding does nothing to the aim.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class DragAimTests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        [UnityTest]
        public IEnumerator PressDragReleaseAimsThenRecentres()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            LabSimRunner r = court.Runner;
            var ring = GameObject.Find("AimTarget0").GetComponent<AimTargetView>();
            yield return WaitReal(0.3f);

            // Moving the mouse without holding: the aim stays in the middle.
            for (int i = 0; i < 10; i++) { Set(mouse.delta, new Vector2(-30f, 0f)); yield return null; }
            Set(mouse.delta, Vector2.zero);
            yield return null;
            Assert.That(Mathf.Abs(r.Input.StickX), Is.LessThan(0.01f), "the mouse alone does not aim");
            Assert.That(ring.Shown, Is.True, "the circle is on the wall");
            Assert.That(Mathf.Abs(ring.WorldPoint.x), Is.LessThan(0.2f), "resting in the middle of the front wall");

            // Press: the stick starts in the middle and the widget shows; drag left: the aim goes left.
            Press(mouse.leftButton);
            yield return null;
            Assert.That(r.Input.StrokeHeld, Is.True);
            for (int i = 0; i < 8; i++) { Set(mouse.delta, new Vector2(-15f, 0f)); yield return null; }
            Set(mouse.delta, Vector2.zero);
            yield return null;
            yield return WaitReal(0.3f);                                   // the circle glides to the aim
            Debug.Log("[M9] after a 120 px drag left: stick x " + r.Input.StickX.ToString("0.00") + ", circle x " + ring.WorldPoint.x.ToString("0.00"));
            Assert.That(r.Input.StickX, Is.LessThan(-0.6f), "dragging left aims left");
            Assert.That(Mathf.Abs(ring.WorldPoint.x), Is.GreaterThan(1.0f), "the circle follows the drag across the wall");

            // Let go: once the stroke is over (or the 1.2 s fallback), the aim is back in the middle.
            Release(mouse.leftButton);
            yield return WaitReal(1.6f);
            Assert.That(Mathf.Abs(r.Input.StickX), Is.LessThan(0.01f), "back to the middle after the stroke");
            yield return WaitReal(0.3f);
            Assert.That(Mathf.Abs(ring.WorldPoint.x), Is.LessThan(0.2f), "the circle is back in the middle");
        }
    }
}
