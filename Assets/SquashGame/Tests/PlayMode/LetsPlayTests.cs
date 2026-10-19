// ===========================================================
// Assets/SquashGame/Tests/PlayMode/LetsPlayTests.cs
// P2-M7 L3 in the real Match scene: lets are on by default; a staged
// obstruction (the opponent in the line to the front wall) shows the
// LET? chip over the player, and Q asks - the referee gives a stroke.
// The Lets setting off: the next match plays through.
// Writes Captures/P2M7_LetPrompt.png.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class LetsPlayTests : InputTestFixture
    {
        // The staged drive (RefereeScenarioTests): struck from the back left, playable about here.
        static readonly V3 From = new V3(-1.0, 1.0, 8.0), Drive = new V3(0.0, 7.0, -16.0);
        const double TX = -1.0, TZ = 3.89;

        [TearDown] public void Restore() { MatchRunner.Lets = true; }

        [UnityTest]
        public IEnumerator TheLetPromptShowsAndQAsks()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return null;
            fe.StartMatch(MatchSettings.QuickGame());           // menus cleared: the HUD is up
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.5f) yield return null;
            MatchRunner r = fe.Match;
            MatchSimulation m = r.Sim;
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.True, "lets are on by default");

            // The opponent stands still in the line to the front wall; we are close to where the ball will be.
            m.Bots[1] = null;
            m.Intents[1].Move.X = 0; m.Intents[1].Move.Z = 0; m.Intents[1].MoveMagnitude = 0;
            m.StageRally(From, Drive, TX + 0.6, TZ + 0.6, TX, TZ - 1.4);

            float t0 = Time.realtimeSinceStartup;
            while (fe.Hud.LetPromptFor != 0 && m.Referee.Outcome == null && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            Debug.Log("[P2M7] LET? over " + fe.Hud.LetPromptFor + ", interference " + m.Interference.ThisTurn + ", would " + m.Interference.Decide().Reason);
            Assert.That(fe.Hud.LetPromptFor, Is.EqualTo(0), "LET? over the player while asking would be granted");
            yield return FrontEndTests.CaptureUI(fe, "P2M7_LetPrompt.png");

            Press(mouse.rightButton); yield return null; Release(mouse.rightButton);   // right click asks
            t0 = Time.realtimeSinceStartup;
            while (m.Referee.Outcome == null && Time.realtimeSinceStartup - t0 < 2f) yield return null;
            RallyOutcome o = m.Referee.Outcome;
            Debug.Log("[P2M7] Q asked: " + o);
            Assert.That(o, Is.Not.Null);
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Stroke), o.ToString());
            Assert.That(o.Winner, Is.EqualTo(0));

            // L4: a stroke is reviewed at once, with the obstruction drawn on the floor; any key skips it.
            t0 = Time.realtimeSinceStartup;
            while (!r.ReplayActive && Time.realtimeSinceStartup - t0 < 1f) yield return null;
            Assert.That(r.ReplayActive && r.ReviewingCall, Is.True, "a stroke is reviewed automatically");
            Assert.That(r.LastCall, Is.Not.Null);
            Assert.That(r.LastCall.Obstruction, Is.EqualTo(ObstructionKind.Line));
            var overlay = Object.FindFirstObjectByType<ObstructionOverlay>();
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.6f) yield return null;
            Assert.That(overlay.Showing, Is.True, "the corridor and the obstructor drawn");
            Debug.Log("[P2M7] review: " + fe.Hud.BannerText + " / " + fe.Hud.BannerSub);
            Assert.That(fe.Hud.BannerSub, Does.Contain("in the line to the front wall"));
            yield return FrontEndTests.CaptureUI(fe, "P2M7_StrokeReview.png");
            Press(kb.spaceKey); yield return null; Release(kb.spaceKey); yield return null; yield return null;
            Assert.That(r.ReplayActive, Is.False, "any key skips the review");
            Assert.That(overlay.Showing, Is.False);

            // The call on the lower third: the decision big, why in plain words, the point.
            Debug.Log("[P2M7] call: " + fe.Hud.BannerText + " / " + fe.Hud.BannerSub);
            Assert.That(fe.Hud.BannerText, Is.EqualTo("STROKE"));
            Assert.That(fe.Hud.BannerSub, Does.StartWith("Retriever was in the line to the front wall"));
            Assert.That(fe.Hud.BannerSub, Does.Contain("POINT  YOU"));
            yield return FrontEndTests.CaptureUI(fe, "P2M7_StrokeCall.png");
        }

        [UnityTest]
        public IEnumerator WithLetsOffTheMatchPlaysThrough()
        {
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            MatchRunner.Lets = false;
            r.BeginMatch(MatchSettings.QuickGame());
            yield return null;
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.False, "Lets off: the match is built without appeals");
            Assert.That(r.Recorder == null || !r.Recorder.File.Setup.Lets, Is.True, "and recorded so");
        }
    }
}
