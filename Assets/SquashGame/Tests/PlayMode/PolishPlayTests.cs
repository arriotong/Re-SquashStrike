// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PolishPlayTests.cs
// 2026-11-21 the polish pass, in the real scene with the motion on
// (batch runs keep it off: FrontEnd.AnimateScreens):
//   - a match won: the last ball in slow motion, the camera swings in
//     front of the winner, who plays their celebration (it moves), the
//     title slams in, confetti; then full time, XP and coins counting
//   - Reduce motion: no close-up, full time as before
// Writes Captures/J_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Athlete;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PolishPlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false;
            ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            ps.targetTexture = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        static IEnumerator Short(FrontEnd fe, MatchRunner r)
        {
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.0f, GamesToWin = 1, Scoring = "par", ParPoints = 4, WarmUp = false });
            r.Sim.Intents[0] = new InputIntent();
            r.Sim.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.9), 77);
            r.Sim.Players[0].WallAimEnabled = true; r.Sim.Players[0].ArcadeHitEnabled = true;
            r.SetTimeScale(10);
            yield return Wait(150f, () => r.Sim.Phase == MatchPhase.MatchOver);
            r.SetTimeScale(SimRunnerBase.GameSpeed);
        }

        [UnityTest]
        public IEnumerator AMatchWonCelebratesThenFullTimeCounts()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            FrontEnd.AnimateScreens = true;
            Juice.ReduceMotion = false;
            try
            {
                var r = Object.FindFirstObjectByType<MatchCourt>().Runner;
                var fe = Object.FindFirstObjectByType<FrontEnd>();
                yield return Short(fe, r);
                Assert.That(r.Sim.Phase, Is.EqualTo(MatchPhase.MatchOver), "the short match finished");
                Assert.That(r.CelebrationTime, Is.GreaterThanOrEqualTo(0f), "the celebration's clock runs");
                Debug.Log("[polish] hitstops " + r.HitStops + ", bursts " + Object.FindFirstObjectByType<ImpactBurst>().Bursts + ", last callout " + fe.Hud.LastCallout);
                var rig = Camera.main.GetComponent<GameplayCameraRig>();
                yield return Wait(2.4f, () => false);
                Assert.That(fe.Top, Is.Null, "the celebration plays before full time");
                Assert.That(rig.WinnerCam, Is.True, "the camera is on the winner");
                Assert.That(fe.Hud.WinTitle, Is.Not.Null, "the title");
                int w = r.Sim.Score.MatchWinner;
                AthleteFigure winner = null;
                foreach (var a in Object.FindObjectsByType<AthleteFigure>(FindObjectsSortMode.None)) if (a.MomentShown != null && a.MomentShown.StartsWith("cel_")) winner = a;
                Assert.That(winner, Is.Not.Null, "the winner celebrates");
                Assert.That(winner.CelebrationPlayed, Is.GreaterThan(1f), "and it plays (it moves)");
                Debug.Log("[polish] winner " + w + " celebrates " + winner.MomentShown + ", title " + fe.Hud.WinTitle);
                yield return CaptureUI(fe, "J_MatchWin.png");
                yield return Wait(0.8f, () => false);
                yield return CaptureUI(fe, "J_MatchWin2.png");
                yield return Wait(FrontEnd.CelebrateSeconds, () => fe.Top is PostMatchScreen);
                Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>(), "then full time");
                yield return Wait(1.0f, () => false);
                yield return CaptureUI(fe, "J_PostMatch_Counting.png");
                yield return Wait(2.2f, () => false);
                yield return CaptureUI(fe, "J_PostMatch.png");

                // Reduce motion: straight to full time, no close-up
                Juice.ReduceMotion = true;
                fe.Restart();
                yield return Short(fe, r);
                yield return Wait(0.8f, () => false);
                Assert.That(rig.WinnerCam, Is.False, "no close-up with Reduce motion");
                yield return Wait(2.5f, () => fe.Top is PostMatchScreen);
                Assert.That(fe.Top, Is.InstanceOf<PostMatchScreen>(), "full time as before");
                fe.QuitToMenu();
                yield return Wait(0.6f, () => false);
                yield return CaptureUI(fe, "J_Menu.png");
            }
            finally
            {
                FrontEnd.AnimateScreens = false;
                Juice.ReduceMotion = false;
            }
        }
    }
}
