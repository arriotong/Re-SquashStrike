// ===========================================================
// Assets/SquashGame/Tests/PlayMode/MachinePlayTests.cs
// 2026-11-24 the new machines in the game:
//   - your walk-on plays its effect and line on the match's walk-on card;
//     your Special Shot's look is on your cut-in (the plain one on theirs)
//   - your perks go on your attributes in Quick Match - and not in versus
//   - the perks screen wears and takes off; the pro cards screen shows tiers
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class MachinePlayTests : InputTestFixture
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

        static void Own(string id, int copies = 1)
        {
            var p = ProfileStore.Current.Progress;
            if (!p.Owned.Contains(id)) p.Owned.Add(id);
            p.Copies.RemoveAll(c => c.Id == id);
            p.Copies.Add(new ItemCount { Id = id, N = copies });
        }

        [UnityTest]
        public IEnumerator YourWalkOnSpecialAndPerksPlay()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var p = ProfileStore.Current.Progress;
            Own("wo_fireworks"); Own("sp_dragon"); Own("pk_gym", 6);
            p.Loadout.WalkOn = "wo_fireworks"; p.Loadout.Special = "sp_dragon";
            Assert.That(Machines.Toggle("pk_gym"), Is.True);
            ProfileStore.Current.Settings.Cutscenes = false;
            fe.StartMatch(new MatchSettings { Opponent = "star:s10", Difficulty = 0.5f, GamesToWin = 1, Scoring = "par", WarmUp = false, Perks = true }.Dressed());
            yield return Wait(4f, () => fe.Match != null && !fe.Match.Demo && fe.Hud.IntroShown);
            var r = fe.Match;
            if (r.ReplayActive) r.StopReplay();
            yield return Wait(3f, () => fe.Hud.IntroShown);
            Assert.That(fe.Hud.IntroShown, Is.True, "the walk-on card");
            Assert.That(fe.Hud.WalkOnFxPlaying, Is.EqualTo(5), "fireworks");
            yield return Wait(1.2f, () => false);
            yield return CaptureUI(fe, "MACHINE_WalkOn.png");
            // perks: Gym Session at level 3 is +0.5 power (2%)
            double power = r.Setup.Attr0.Power;
            // the cut-in: yours in your look
            GameplayCameraRig.CutIn(r.HumanIndex, 1.0f);
            yield return null; yield return null;
            Assert.That(fe.Hud.CutInShown, Is.True);
            Assert.That(fe.Hud.CutInLook, Is.EqualTo("sp_dragon"), "your Special Shot's look");
            yield return Wait(0.3f, () => false);
            yield return CaptureUI(fe, "MACHINE_CutIn.png");
            yield return Wait(1.2f, () => !GameplayCameraRig.CutInShowing);
            GameplayCameraRig.CutIn(1 - r.HumanIndex, 0.5f);
            yield return null; yield return null;
            Assert.That(fe.Hud.CutInLook, Is.Null, "theirs: the plain band");
            yield return Wait(1f, () => !GameplayCameraRig.CutInShowing);

            // without the perks (as versus and online are built): the same match, plain power
            fe.QuitToMenu();
            yield return null; yield return null;
            fe.StartMatch(new MatchSettings { Opponent = "star:s10", Difficulty = 0.5f, GamesToWin = 1, Scoring = "par", WarmUp = false }.Dressed());
            yield return Wait(4f, () => fe.Match != null && !fe.Match.Demo);
            Assert.That(fe.Match.Setup.Attr0.Power, Is.LessThan(power - 0.4), "no perks unless asked for");
            Debug.Log("[Machines] power with the gym perk " + power + ", without " + fe.Match.Setup.Attr0.Power);
        }

        [UnityTest]
        public IEnumerator PerksAndProCardsScreens()
        {
            yield return Scene();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            foreach (var id in new[] { "pk_routine", "pk_gym", "pk_tape", "pk_gel" }) Own(id, 3);
            var perks = new PerksScreen();
            fe.Push(perks);
            yield return null; yield return null;
            foreach (var id in new[] { "pk_routine", "pk_gym", "pk_tape", "pk_gel" })
            {
                var tile = perks.Reachable().Find(i => i.Name == Cosmetics.Find(id).Name);
                Assert.That(tile, Is.Not.Null, id);
                tile.Submit();
                yield return null;
            }
            Assert.That(Machines.Equipped.Count, Is.EqualTo(3), "three at most");
            Assert.That(perks.Refused, Is.True, "the fourth is refused");
            Assert.That(fe.Top, Is.SameAs(perks), "the screen stays");
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "MACHINE_Perks.png");
            fe.Pop();

            var cards = Machines.Find(Machines.ProCardsId).Items;
            for (int i = 0; i < 8; i++) Own(cards[i * 3].Id, 1 + i);
            fe.Push(new ProCardsScreen());
            yield return Wait(0.8f, () => false);
            yield return CaptureUI(fe, "MACHINE_ProCards.png");
            fe.Pop();
            fe.Push(new CareerShopScreen());
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "MACHINE_CareerShop.png");
            fe.Pop();

            // the new celebrations, tried on in the locker
            var locker = new LockerRoomScreen(LockerRoomScreen.ShowtimeTab);
            fe.Push(locker);
            yield return Wait(0.5f, () => false);
            var row = (LockerRow)locker.Reachable().Find(i => i is LockerRow lr && lr.Slot == Slot.Celebration);
            locker.Focus(row);
            foreach (var cel in new[] { "cel_guitar", "cel_flex", "cel_point", "cel_salute", "cel_shush", "cel_cool" })
            {
                row.Select(cel);
                yield return Wait(1.4f, () => false);
                Assert.That(KitDirector.PreviewPose, Is.EqualTo(cel));
                yield return CaptureUI(fe, "CEL_" + cel + ".png");
            }
            fe.Pop();                                                          // (the try-on ends with the locker)
            yield return null;
            KitDirector.PreviewPose = null;
        }
    }
}
