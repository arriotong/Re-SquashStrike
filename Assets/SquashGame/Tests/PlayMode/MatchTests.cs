// ===========================================================
// Assets/SquashGame/Tests/PlayMode/MatchTests.cs
// Boots the M2 Match scene and plays the opening of a game with a
// virtual gamepad: serve setup, toss, struck serve, a rally that ends
// with a referee call, and the score/serve updating to match.
// Writes Captures/M2_Match.png.
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
    public class MatchPlayTests : InputTestFixture
    {
        static MatchRunner Runner()
        {
            var court = Object.FindFirstObjectByType<MatchCourt>();
            Assert.That(court, Is.Not.Null, "MatchCourt not in scene");
            return court.Runner;
        }

        [UnityTest]
        public IEnumerator ServeSetupPlacesPlayersAndHoldsTheBall()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Runner();
            MatchSimulation m = r.Sim;
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            Assert.That(m.Score.Server, Is.EqualTo(0), "the player serves first");
            Assert.That(RallyReferee.InServiceBox(m.Score.ServeSide, m.Players[0].Player.Pos, 0), Is.True, "server not in the box");
            Assert.That(m.Ball.Asleep, Is.True, "ball should be held until the toss");
            Assert.That(m.Players[1].Player.Pos.Z, Is.GreaterThan(CourtDimensions.SHORT_LINE_Z), "receiver should wait deep");
        }

        [UnityTest]
        public IEnumerator GamepadServeLeadsToARefereeCall()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Runner();
            MatchSimulation m = r.Sim;

            // Aim up and across, as the prompt says.
            Set(pad.rightStick, new Vector2(-0.55f, 0.85f));
            yield return null;

            int rallies = m.History.Count;
            float t0 = Time.realtimeSinceStartup;
            bool struck = false;
            while (Time.realtimeSinceStartup - t0 < 12f && m.History.Count == rallies)
            {
                if (m.Phase == MatchPhase.AwaitToss || (m.Phase == MatchPhase.Rally && m.Referee.Shots == 0))
                {
                    // Toss, then a Quick stroke: a tap is auto-timed to the falling ball.
                    Press(pad.buttonSouth); yield return null;
                    Release(pad.buttonSouth); yield return null;
                }
                if (m.Referee.Shots > 0) struck = true;
                yield return null;
            }

            Assert.That(struck, Is.True, "the serve was never struck");
            Assert.That(m.History.Count, Is.GreaterThan(rallies), "no referee call within 12 s");
            RallyOutcome o = m.LastOutcome;
            Debug.Log("[M2] first rally: " + o + " after " + m.ShotsPerRally[m.ShotsPerRally.Count - 1] + " shots");
            if (o.Kind != RallyEnd.Let)
            {
                Assert.That(m.Score.Points[0] + m.Score.Points[1], Is.EqualTo(1), "PAR: every rally scores");
                Assert.That(m.Score.Server, Is.EqualTo(o.Winner), "the rally winner serves");
            }
            Capture();
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "M2_Match.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
