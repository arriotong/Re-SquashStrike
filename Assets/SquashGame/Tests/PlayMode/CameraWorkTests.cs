// ===========================================================
// Assets/SquashGame/Tests/PlayMode/CameraWorkTests.cs
// Camera work (2026-10-28) in the real scene:
//   - a prestige match opens with the fly-around (its title), then the
//     players walk on from the path behind the glass to their places;
//     the sim waits; then the match plays
//   - a replay between rallies cuts like a broadcast (wide, the glass or
//     a corner, the front-wall cam) under "REPLAY"
// Writes Captures/CK_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class CameraWorkTests : InputTestFixture
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

        static IEnumerator Scene()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator AFinalOpensWithTheFlyAroundAndTheWalkOn()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            ProfileStore.Current.Settings.Cutscenes = true;
            fe.StartMatch(new MatchSettings { Opponent = "star:s01", Difficulty = 0.6f, GamesToWin = 1, Scoring = "par", WarmUp = false,
                                              Court = "tour:rep", CrowdFill = 1f, Prestige = "THE FINAL  ·  BRITISH OPEN" }.Dressed());
            yield return Wait(3f, () => fe.Match != null && !fe.Match.Demo && fe.Match.CinematicKind != null);
            var r = fe.Match;
            Assert.That(r.CinematicKind, Is.EqualTo(Replays.Cutscenes.Intro), "the match opens on its cutscene");
            Assert.That(r.CinematicOrbit, Is.GreaterThan(0.3f), "a final: the fly-around first");
            long steps = r.Sim.StepCount;

            yield return Wait(6f, () => r.ReplayProgress > r.CinematicOrbit * 0.45f);
            Assert.That(fe.Hud.CineText, Is.EqualTo("THE FINAL  ·  BRITISH OPEN"));
            yield return CaptureUI(fe, "CK_Orbit.png");
            yield return Wait(8f, () => r.ReplayProgress > r.CinematicOrbit + (1 - r.CinematicOrbit) * 0.2f);
            Assert.That(fe.Hud.CineText, Does.Contain("   v   "), "the names as they walk on");
            yield return CaptureUI(fe, "CK_WalkPath.png");
            yield return Wait(8f, () => r.ReplayProgress > r.CinematicOrbit + (1 - r.CinematicOrbit) * 0.7f);
            yield return CaptureUI(fe, "CK_WalkIn.png");
            Assert.That(r.Sim.StepCount, Is.EqualTo(steps), "the sim waits for the cutscene");

            yield return Wait(10f, () => !r.ReplayActive);
            Assert.That(r.ReplayActive, Is.False, "the cutscene ends");
            Assert.That(r.CinematicKind, Is.Null);
            yield return Wait(1f, () => r.Sim.StepCount > steps);
            Assert.That(r.Sim.StepCount, Is.GreaterThan(steps), "and the match plays");
        }

        [UnityTest]
        public IEnumerator AReplayBetweenRalliesCutsLikeABroadcast()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            ProfileStore.Current.Settings.Replays = 2;                                           // every point
            fe.StartMatch(new MatchSettings { Opponent = "retriever", Difficulty = 0.6f, GamesToWin = 1, Scoring = "par", WarmUp = false, Court = "tour:pyramids", CrowdFill = 1f });
            yield return Wait(3f, () => fe.Match != null && !fe.Match.Demo);
            var r = fe.Match;
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), 3));
            yield return Wait(90f, () => r.ReplayActive);
            Assert.That(r.ReplayActive, Is.True, "a rally ended: its replay");
            Assert.That(r.CinematicKind, Is.Null);
            yield return null;
            Assert.That(fe.Hud.BannerText, Is.EqualTo("REPLAY"), "a replay, not a referee review");
            var rig = Camera.main.GetComponent<GameplayCameraRig>();
            var shots = new System.Collections.Generic.List<Replays.Shot>();
            foreach (var (at, name) in new[] { (0.2f, "CK_Replay_1.png"), (0.55f, "CK_Replay_2.png"), (0.8f, "CK_Replay_3.png") })
            {
                yield return Wait(15f, () => !r.ReplayActive || r.ReplayProgress > at);
                if (!r.ReplayActive) break;
                shots.Add(rig.ReplayShot);
                yield return CaptureUI(fe, name);
            }
            Assert.That(shots.Count, Is.EqualTo(3));
            Assert.That(shots[0], Is.EqualTo(Replays.Shot.Wide));
            Assert.That(shots[1], Is.Not.EqualTo(Replays.Shot.Wide), "cut to the glass or a corner");
            Assert.That(shots[2], Is.EqualTo(Replays.Shot.FrontWall).Or.EqualTo(Replays.Shot.Shoulder).Or.EqualTo(Replays.Shot.BallCam), "the ending close");
        }
    }
}
