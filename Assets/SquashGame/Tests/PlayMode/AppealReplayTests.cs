// ===========================================================
// Assets/SquashGame/Tests/PlayMode/AppealReplayTests.cs
// M3 in the real Match scene against the AI: the player appeals with
// pad Y on their turn, the referee rules Let or No let, the referee
// replay plays from the overhead camera, then play carries on.
// Writes Captures/M3_RefereeReplay.png.
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
    public class AppealReplayTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator AppealOnOurTurnGetsACallAndAReplay()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            r.BeginMatch(MatchSettings.QuickGame());      // past the setup panel: one game vs the Retriever
            MatchSimulation m = r.Sim;
            Assert.That(r.OpponentName, Does.Contain("RETRIEVER"), "the AI opponent should be in the match");

            // Serve (toss + tap, aim up and across), then wait for our next turn and appeal.
            Set(pad.rightStick, new Vector2(-0.55f, 0.85f));
            float t0 = Time.realtimeSinceStartup;
            bool appealed = false;
            while (Time.realtimeSinceStartup - t0 < 30f && !appealed)
            {
                if (m.Phase == MatchPhase.AwaitToss && m.Score.Server == 0 || (m.Phase == MatchPhase.Rally && m.Referee.Shots == 0 && m.Score.Server == 0))
                {
                    Press(pad.buttonSouth); yield return null;
                    Release(pad.buttonSouth); yield return null;
                }
                if (m.Phase == MatchPhase.Rally && m.Referee.Outcome == null && m.Referee.Shots >= 1 && m.Referee.PendingStriker == 0)
                {
                    int before = m.History.Count;
                    Press(pad.buttonNorth); yield return null;
                    Release(pad.buttonNorth); yield return null;
                    yield return null;
                    if (m.History.Count > before) appealed = true;
                }
                yield return null;
            }
            Assert.That(appealed, Is.True, "never got a turn to appeal on within 30 s");
            RallyOutcome o = m.LastOutcome;
            Assert.That(o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet, o.ToString());
            Debug.Log("[M3] appeal ruled: " + o);
            var pops = Object.FindFirstObjectByType<ArcadePopups>();
            Debug.Log("[Pop] shown: " + string.Join(" ", pops.Shown));
            Assert.That(pops.Shown, Does.Contain("LET, PLEASE?"), "the appeal pops up");
            Assert.That(pops.Shown, Does.Contain(o.Kind == RallyEnd.Let ? "LET!" : "NO LET!"), "the decision pops up");

            // The call is reviewed automatically on the referee camera.
            yield return null; yield return null;
            Assert.That(r.ReplayActive, Is.True, "an interference call should start the referee replay");
            long stepsAtReplay = r.Clock.TotalSteps;
            float tr = Time.realtimeSinceStartup;
            while (r.ReplayActive && Time.realtimeSinceStartup - tr < 0.8f) yield return null;
            Capture();
            Assert.That(r.Clock.TotalSteps, Is.EqualTo(stepsAtReplay), "the simulation must wait during the replay");

            tr = Time.realtimeSinceStartup;
            while (r.ReplayActive && Time.realtimeSinceStartup - tr < 15f) yield return null;
            Assert.That(r.ReplayActive, Is.False, "the replay should finish");
            float tc = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tc < 0.3f) yield return null;
            Assert.That(r.Clock.TotalSteps, Is.GreaterThan(stepsAtReplay), "play should resume after the replay");
        }

        static void Capture()
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "M3_RefereeReplay.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
