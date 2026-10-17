// ===========================================================
// Assets/SquashGame/Tests/PlayMode/LocalMatchTests.cs
// M5 in the real Match scene, with virtual devices only:
//   - the setup panel, driven by a pad, starts a Local 2P match;
//     each pad steers only its own athlete; the knock-up starts with a
//     cold ball and player 2's Y skips it; only the server can toss
//   - the audio director plays the event sounds (no errors)
//   - vs AI with the keyboard: knock-up, Enter skips, serve setup
// Writes Captures/M5_*.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Audio;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class LocalMatchTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        IEnumerator Tap(ButtonControl b)
        {
            Press(b); yield return null;
            Release(b); yield return null;
        }

        [UnityTest]
        public IEnumerator TwoPadsPlayLocalTwoPlayerFromTheSetupPanel()
        {
            var pad1 = InputSystem.AddDevice<Gamepad>();
            var pad2 = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var panel = Object.FindFirstObjectByType<MatchSetupPanel>();
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            Assert.That(r.InSetup, Is.True, "the match opens on the setup panel");
            Assert.That(panel.Selected, Is.EqualTo(MatchSetupPanel.RowStart));

            // D-pad up to the opponent row, left once: wraps to "Player 2 (local)".
            double simTimeAtSetup = r.Sim.Time;
            while (panel.Selected != MatchSetupPanel.RowOpponent) yield return Tap(pad1.dpad.up);
            Assert.That(panel.Selected, Is.EqualTo(MatchSetupPanel.RowOpponent));
            yield return Tap(pad1.dpad.left);
            Assert.That(panel.TwoPlayer, Is.True);
            Assert.That(r.Sim.Time, Is.EqualTo(simTimeAtSetup), "nothing moves behind the panel");
            Capture("M5_Setup.png");
            yield return Tap(pad1.buttonSouth);                     // start

            Assert.That(r.InSetup, Is.False);
            MatchSimulation m = r.Sim;
            Assert.That(r.Settings.TwoPlayer, Is.True);
            Assert.That(m.Bots[1], Is.Null, "no AI in a two-player match");
            Assert.That(m.Score.GamesToWin, Is.EqualTo(3), "best of 5 by default");
            Assert.That(r.Input2, Is.Not.Null);
            Assert.That(r.Input2.Devices, Is.EquivalentTo(new InputDevice[] { pad2 }), "player 2 has pad 2 only");
            Assert.That(System.Array.IndexOf(r.Input.Devices, pad1), Is.GreaterThanOrEqualTo(0), "player 1 has pad 1");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.WarmUp), "the knock-up comes first");
            Assert.That(m.Ball.Temperature, Is.LessThan(PhysicsConfig.Current.Match.StartTemperature), "with a cold ball");

            yield return Tap(pad2.buttonNorth);                     // player 2: ready - skip the knock-up
            yield return Wait(0.1f, () => m.Phase != MatchPhase.WarmUp);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            Assert.That(m.Score.Server, Is.EqualTo(0));

            Vector3 a0 = r.View.A0.Pos, a1 = r.View.A1.Pos;
            Set(pad2.leftStick, new Vector2(-1f, 0f));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.6f) yield return null;
            Set(pad2.leftStick, Vector2.zero);
            yield return null;
            float moved1 = Vector3.Distance(r.View.A1.Pos, a1), moved0 = Vector3.Distance(r.View.A0.Pos, a0);
            Debug.Log("[M5] pad 2 stick: athlete 1 moved " + moved1.ToString("0.00") + " m, athlete 0 " + moved0.ToString("0.00") + " m");
            Assert.That(moved1, Is.GreaterThan(0.3f), "pad 2 moves player 2");
            Assert.That(moved0, Is.LessThan(0.05f), "pad 2 must not move player 1");

            // Only the server tosses: pad 2's Hit does nothing, pad 1's tosses.
            yield return Tap(pad2.buttonSouth);
            yield return Wait(0.25f, () => false);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "the receiver cannot toss");
            Set(pad1.rightStick, new Vector2(-0.55f, 0.85f));
            yield return Tap(pad1.buttonSouth);
            yield return Wait(0.25f, () => m.Phase != MatchPhase.AwaitToss);   // frames here are faster than the 240 Hz step
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Rally), "player 1 tossed");

            // Strike, and let the rally make some noise.
            int heard = audio.PlayedTotal;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 4f && m.Referee.Shots == 0)
            {
                yield return Tap(pad1.buttonSouth);
                yield return null;
            }
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
            Debug.Log("[M5] sounds: " + string.Join(", ", System.Linq.Enumerable.Select(audio.Played, kv => kv.Key + " " + kv.Value)));
            Assert.That(audio.PlayedTotal, Is.GreaterThan(heard), "the rally should be heard");
            Assert.That(audio.Played.ContainsKey("floor") || audio.Played.ContainsKey("front") || audio.Played.ContainsKey("ping"), Is.True);
            Capture("M5_TwoPlayer.png");
            Set(pad1.rightStick, Vector2.zero);
        }

        [UnityTest]
        public IEnumerator KeyboardStartsAKnockUpAgainstTheAiAndEnterSkipsIt()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var panel = Object.FindFirstObjectByType<MatchSetupPanel>();
            Assert.That(r.InSetup, Is.True);
            Assert.That(MatchRunner.CanPlayTwoPlayer, Is.False);
            // With no pad, the opponent row skips "Player 2".
            while (panel.Selected != MatchSetupPanel.RowOpponent) yield return Tap(kb.upArrowKey);
            yield return Tap(kb.leftArrowKey);
            Assert.That(panel.TwoPlayer, Is.False, "Local 2P needs a pad");
            yield return Tap(kb.enterKey);

            MatchSimulation m = r.Sim;
            Assert.That(r.InSetup, Is.False);
            Assert.That(m.Bots[1], Is.Not.Null, "the AI plays");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.WarmUp));
            Assert.That(m.WarmUpOwner, Is.EqualTo(0), "you knock up first");

            // Let the knock-up run a moment: the feed comes and the ball is in play.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.WarmUp));
            Assert.That(m.Ball.Asleep, Is.False, "fed ball in play");

            yield return Tap(kb.enterKey);
            yield return Wait(0.1f, () => m.Phase != MatchPhase.WarmUp);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "Enter skips the knock-up");
            Assert.That(m.Score.Points[0] + m.Score.Points[1], Is.EqualTo(0));
        }

        static void Capture(string file)
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
