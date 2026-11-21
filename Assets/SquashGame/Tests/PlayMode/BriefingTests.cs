// ===========================================================
// Assets/SquashGame/Tests/PlayMode/BriefingTests.cs
// 2026-11-21 nothing starts the moment it is clicked: the hub's cards
// that used to launch play (the shot puzzle, the rules quiz, the skill
// test, learn to play), the practice court, the arcade's levels and a Big
// Moment all open a get-ready screen first, on their own world - and its
// START does launch it. Writes Captures/BRIEF_*.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class BriefingTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t = 0;
            while (t < seconds && !done()) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static IEnumerator CaptureUI(FrontEnd fe, string file)
        {
            var ps = fe.Doc.panelSettings;
            var rt = new RenderTexture(1920, 1080, 24);
            Camera cam = Camera.main;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            ps.clearColor = false; ps.targetTexture = rt;
            yield return null; yield return null; yield return null;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = null; ps.targetTexture = null;
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file)), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release();
        }

        /// <summary>Submit the hub card <paramref name="title"/> on tab <paramref name="tab"/>; the get-ready screen on <paramref name="world"/> must come up.</summary>
        static IEnumerator Brief(FrontEnd fe, string tab, string title, MenuWorld world, string shot)
        {
            var hub = fe.Top as MainMenuScreen;
            for (int i = 0; i < hub.MySections.Length && hub.Tabs.Current != tab; i++) { hub.OnTab(1); yield return null; }
            var card = hub.Reachable().Find(it => it is CardItem c && c.Title == title);
            Assert.That(card, Is.Not.Null, title + " on " + tab);
            card.Submit();
            yield return Wait(0.6f, () => false);
            Assert.That(fe.Top, Is.InstanceOf<BriefingScreen>(), title + ": a get-ready screen, not straight into play");
            Assert.That(Dioramas.Current, Is.EqualTo(world), title + ": on its world");
            Assert.That(fe.Match != null && fe.Match.Demo, Is.True, title + ": nothing started yet");
            yield return CaptureUI(fe, "BRIEF_" + shot + ".png");
            fe.Top.Back();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.SameAs(hub), "Back: the hub again");
        }

        [UnityTest]
        public IEnumerator NothingStartsTheMomentItIsClicked()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top is MainMenuScreen);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return Wait(0.5f, () => false);

            yield return Brief(fe, "CHALLENGES", "SHOT PUZZLE", MenuWorld.Challenges, "Puzzle");
            yield return Brief(fe, "CHALLENGES", "RULES QUIZ", MenuWorld.Challenges, "Quiz");
            yield return Brief(fe, "CHALLENGES", "SKILL TEST", MenuWorld.Challenges, "Skill");
            yield return Brief(fe, "TRAINING", "LEARN TO PLAY", MenuWorld.Training, "Lesson");

            // the practice court's free practice, an arcade level, a Big Moment: from their own screens
            fe.Push(new PracticeScreen());
            yield return Wait(0.4f, () => false);
            var free = fe.Top.Reachable().Find(it => it is CardItem c && c.Title == "FREE PRACTICE");
            free.Submit();
            yield return Wait(0.4f, () => false);
            Assert.That(fe.Top, Is.InstanceOf<BriefingScreen>(), "free practice: get ready first");
            yield return CaptureUI(fe, "BRIEF_Practice.png");
            fe.Pop();
            var drill = fe.Top.Reachable().Find(it => it is CardItem c && c.Title != "FREE PRACTICE");
            if (drill != null)
            {
                drill.Submit();
                yield return Wait(0.4f, () => false);
                Assert.That(fe.Top, Is.InstanceOf<BriefingScreen>(), "a drill: get ready first");
                yield return CaptureUI(fe, "BRIEF_Drill.png");
                fe.Pop();
            }
            fe.Pop();

            fe.Push(new ScenarioScreen());
            yield return Wait(0.4f, () => false);
            var moment = fe.Top.Reachable().Find(it => it is CardItem c && !c.IsLocked);
            Assert.That(moment, Is.Not.Null);
            moment.Submit();
            yield return Wait(0.5f, () => false);
            Assert.That(fe.Top, Is.InstanceOf<BriefingScreen>(), "a Big Moment: get ready first");
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.Modes));
            yield return CaptureUI(fe, "BRIEF_BigMoment.png");
            // and its START does start it
            ((BriefingScreen)fe.Top).StartCard.Submit();
            yield return Wait(2f, () => fe.Match != null && !fe.Match.Demo);
            Assert.That(fe.Match != null && !fe.Match.Demo, Is.True, "START: the match begins");
            Assert.That(Dioramas.Current, Is.EqualTo(MenuWorld.None), "and the world gives way to the court");
        }
    }
}
