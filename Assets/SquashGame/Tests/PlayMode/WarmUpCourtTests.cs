// ===========================================================
// Assets/SquashGame/Tests/PlayMode/WarmUpCourtTests.cs
// Boots the real WarmUpCourt scene and checks the M1 loop end to end:
// the clock advances on real time, views follow the simulation, input
// from a (virtual) gamepad reaches the athlete in court-relative
// directions, a stroke starts, and the gameplay camera sees the court.
// Also writes Captures/M1_WarmUpCourt.png (git-ignored) for a visual check.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class WarmUpCourtTests : InputTestFixture
    {
        Gamepad _pad;

        IEnumerator LoadCourt()
        {
            SceneManager.LoadScene("WarmUpCourt");
            yield return null;
            yield return null;
        }

        static LabSimRunner Runner()
        {
            var court = Object.FindFirstObjectByType<WarmUpCourt>();
            Assert.That(court, Is.Not.Null, "WarmUpCourt not in scene");
            return court.Runner;
        }

        [UnityTest]
        public IEnumerator SimulationAdvancesAndViewsFollow()
        {
            yield return LoadCourt();
            LabSimRunner runner = Runner();
            if (runner.Free != null) { runner.SetFreePlay(false); runner.FeedSingle(); }   // a fed ball (free play keeps it in hand)
            long start = runner.Clock.TotalSteps;
            Vector3 ballStart = SimToWorld.Pos(runner.Sim.Ball.Pos);

            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.0f) yield return null;

            long steps = runner.Clock.TotalSteps - start;
            // ~240 steps per real second (allow for slow batch-mode frames and the per-frame cap).
            Assert.That(steps, Is.GreaterThan(100), "clock did not advance on real time");
            Assert.That(SimToWorld.Pos(runner.Sim.Ball.Pos), Is.Not.EqualTo(ballStart), "ball never moved");

            // Coroutines resume after Update and before LateUpdate (views draw in LateUpdate).
            // Pause so the render state is stable, then let one full frame run.
            runner.Clock.Pause();
            yield return null;
            yield return null;
            var ballView = GameObject.Find("Ball");
            Assert.That(ballView, Is.Not.Null);
            Assert.That(Vector3.Distance(ballView.transform.position, runner.View.BallPos), Is.LessThan(1e-4f),
                "ball view is not drawn at the simulation's (interpolated) position");

            CaptureScreenshot("M1_WarmUpCourt.png");

            // Close-up from the side wall, to check athlete, shoulder line and racket.
            Camera cam = Camera.main;
            var rig = cam.GetComponent<GameplayCameraRig>();
            rig.enabled = false;
            var pp = runner.Sim.Player.Player.Pos;
            cam.transform.position = SimToWorld.Pos(pp.X + 2.6, 1.7, pp.Z - 0.4);
            cam.transform.LookAt(SimToWorld.Pos(pp.X, 1.0, pp.Z));
            CaptureScreenshot("M1_Athlete_Side.png");
            rig.enabled = true;
        }

        [UnityTest]
        public IEnumerator GamepadMovesAthleteCourtRelative()
        {
            _pad = InputSystem.AddDevice<Gamepad>();
            yield return LoadCourt();
            LabSimRunner runner = Runner();
            runner.Sim.Launcher.SetRepeat(false);

            double x0 = runner.Sim.Player.Player.Pos.X;
            Set(_pad.leftStick, new Vector2(1f, 0f));       // stick right = toward the right side wall
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.4f) yield return null;
            Set(_pad.leftStick, Vector2.zero);
            double x1 = runner.Sim.Player.Player.Pos.X;
            Assert.That(x1, Is.GreaterThan(x0 + 0.2), "stick right should move the athlete toward sim +x");

            // ...which the camera behind the glass must show on the RIGHT of the screen.
            Camera cam = Camera.main;
            yield return null;
            float sx = cam.WorldToViewportPoint(SimToWorld.Pos(runner.Sim.Player.Player.Pos)).x;
            float sx0 = cam.WorldToViewportPoint(SimToWorld.Pos(x0, 0, runner.Sim.Player.Player.Pos.Z)).x;
            Assert.That(sx, Is.GreaterThan(sx0), "sim +x must appear to the right from the back-wall camera");

            double z0 = runner.Sim.Player.Player.Pos.Z;
            Set(_pad.leftStick, new Vector2(0f, 1f));       // stick up = toward the front wall
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.3f) yield return null;
            Set(_pad.leftStick, Vector2.zero);
            Assert.That(runner.Sim.Player.Player.Pos.Z, Is.LessThan(z0 - 0.2), "stick up should move toward the front wall (sim -z)");
        }

        [UnityTest]
        public IEnumerator GamepadStrokeStartsASwing()
        {
            _pad = InputSystem.AddDevice<Gamepad>();
            yield return LoadCourt();
            LabSimRunner runner = Runner();
            runner.ResetFull();
            runner.Sim.Launcher.Fire(fresh: true);

            Press(_pad.buttonSouth);
            yield return null;
            yield return null;
            Release(_pad.buttonSouth);

            bool left = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f)
            {
                if (runner.Sim.Player.Swing.State != SquashSim.SwingStates.Idle) { left = true; break; }
                yield return null;
            }
            Assert.That(left, Is.True, "a Hit press never reached the swing controller");
        }

        static void CaptureScreenshot(string file)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(1280, 720, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[M1] screenshot: " + path);
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
