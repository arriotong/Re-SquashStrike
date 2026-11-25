// ===========================================================
// Assets/SquashGame/Tests/PlayMode/DrillAndDevPlayTests.cs
// 2026-11-25:
//   - the Academy's MATCH SKILLS: each drill starts a real match with the
//     coach on the HUD and a full meter; passing it shows the lesson passed
//     and counts it; the Academy shows a third chapter
//   - Profile > Enter a code: the wrong code is refused, the right one opens
//     the dev tools; the full-meter tool fills your meter in Quick Match
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim.Academy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class DrillAndDevPlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> until)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !until()) yield return null;
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
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator MatchSkillsDrillsPlayAndPass()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            ProfileStore.Current.Settings.Cutscenes = false;
            fe.Push(new AcademyScreen());
            yield return Wait(0.8f, () => false);
            yield return CaptureUI(fe, "ACADEMY_Chapters.png");
            foreach (var l in AcademyScreen.InChapter(Chapter.Match))
            {
                if (fe.Match != null && !fe.Match.Demo) { fe.QuitToMenu(); yield return null; yield return null; }
                MatchDrills.Launch(fe, l);
                yield return Wait(4f, () => fe.Match != null && !fe.Match.Demo && fe.Match.Sim != null);
                var r = fe.Match;
                if (r.ReplayActive) r.StopReplay();
                yield return Wait(2.5f, () => fe.Hud.DrillText != null);
                Assert.That(fe.Hud.DrillText, Does.Contain(l.Title.ToUpperInvariant()), l.Id + ": the coach on the HUD");
                Assert.That(r.Sim.Players[r.HumanIndex].Player.Mods.Power, Is.EqualTo(1.0).Within(1e-9), l.Id + ": a full meter");
                if (l.Id == "duel") Assert.That(r.Sim.Players[1 - r.HumanIndex].Player.Mods.Power, Is.EqualTo(1.0).Within(1e-9), "theirs full too");
                yield return Wait(0.6f, () => false);
                yield return CaptureUI(fe, "DRILL_" + l.Id + ".png");
            }
            // passing one: the lesson passed over the match, counted
            var last = AcademyScreen.InChapter(Chapter.Match)[0];
            fe.QuitToMenu(); yield return null; yield return null;
            MatchDrills.Launch(fe, last);
            yield return Wait(4f, () => fe.Match != null && !fe.Match.Demo && fe.Match.Sim != null);
            if (fe.Match.ReplayActive) fe.Match.StopReplay();
            var t = MatchDrills.Current;
            var o = new MatchDrills.Observation { Me = 0, Z = 5 };
            t.See(o);
            for (int i = 0; i < t.Step.Count; i++) { o.Focusing = true; t.See(o); o.Focusing = false; t.See(o); }
            Assert.That(t.Passed, Is.True);
            yield return Wait(3f, () => fe.Top is LessonCompleteScreen);
            Assert.That(fe.Top, Is.InstanceOf<LessonCompleteScreen>(), "the lesson passed");
            Assert.That(ProfileStore.LessonDone(last.Id), Is.True, "counted");
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "DRILL_Passed.png");
        }

        [UnityTest]
        public IEnumerator TheCodeOpensTheDevTools()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.Push(new ProfileScreen());
            yield return null; yield return null;
            Assert.That(fe.Top.Find("Enter a code"), Is.Not.Null);
            fe.Top.Find("Enter a code").Submit();
            yield return null; yield return null;
            var code = (CodeScreen)fe.Top;
            code.Field.Field.value = "1234";
            code.Reachable().Find(i => i.Name == "OK").Submit();
            yield return null;
            Assert.That(fe.Top, Is.SameAs(code), "the wrong code: still here");
            Assert.That(DevMode.On, Is.False);
            yield return CaptureUI(fe, "DEV_Code.png");
            code.Field.Field.value = "0908";
            code.Reachable().Find(i => i.Name == "OK").Submit();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<DevToolsScreen>());
            Assert.That(DevMode.On, Is.True);
            Assert.That(ChoiceBudgetTests.Count(fe.Top), Is.LessThanOrEqualTo(ChoiceBudgetTests.Budget));
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "DEV_Tools.png");
            // the full meter in Quick Match
            ProfileStore.Current.Dev.FullMeter = true;
            ProfileStore.Current.Settings.Cutscenes = false;
            fe.StartMatch(new MatchSettings { Opponent = "star:s30", Difficulty = 0.5f, GamesToWin = 1, Scoring = "par", WarmUp = false }.Dressed());
            yield return Wait(4f, () => fe.Match != null && !fe.Match.Demo && fe.Match.Sim != null);
            Assert.That(fe.Match.Sim.Players[fe.Match.HumanIndex].Player.Mods, Is.Not.Null);
            Assert.That(fe.Match.Sim.Players[fe.Match.HumanIndex].Player.Mods.Power, Is.EqualTo(1.0).Within(1e-9), "your meter full");
            DevMode.TurnOff();
        }
    }
}
