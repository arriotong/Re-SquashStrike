// ===========================================================
// Assets/SquashGame/Tests/PlayMode/EquipmentPlayTests.cs
// P7 phase 5: the career's Equipment, on the keyboard.
//   - the PLAYER tab's Equipment button opens it: a tab a slot, a card
//     an item; every look is a real kit item
//   - focus shows your stats now and with the item ("5 -> 7")
//   - A on an item you can afford asks, then buys it: the cash pays, the
//     card reads ON; A on an owned one puts it on; too dear: nothing
// Writes Captures/P7_Equipment_*.png for review.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashGame.UI;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class EquipmentPlayTests : InputTestFixture
    {
        IEnumerator Tap(ButtonControl b) { Press(b); yield return null; Release(b); yield return null; }

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

        [UnityTest]
        public IEnumerator BuyARacketAndPutOnShoes()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();

            foreach (var g in Gear.All) Assert.That(Cosmetics.Find(g.Look), Is.Not.Null, g.Name + ": a real kit look (" + g.Look + ")");

            CareerStore.New("Kitted", "ENG", 2, false);
            var c = CareerStore.Current;
            c.S.Cash = 3000;
            c.S.GearOwned.Add("sh_grip");                                   // shoes bought earlier, not on
            CareerStore.Save();
            fe.Push(new CareerHubScreen());
            yield return null; yield return null;
            var hub = (CareerHubScreen)fe.Top;
            hub.Tabs.Select(3);                                             // PLAYER
            yield return null; yield return null;
            Assert.That(hub.Tabs.Current, Is.EqualTo("PLAYER"));
            var button = fe.Top.Find("Equipment  ·  rackets, shoes, kit");
            Assert.That(button, Is.Not.Null, "the PLAYER tab's way in");
            hub.Focus(button);
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<EquipmentScreen>());
            var eq = (EquipmentScreen)fe.Top;

            // the Power 130: focus shows the change, A asks, then buys
            var (power, _) = eq.Cards.Find(x => x.item.Id == "rk_power");
            eq.Focus(power);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P7_Equipment_Racket.png");
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.InstanceOf<ConfirmScreen>());
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(c.S.Cash, Is.EqualTo(1500), "the cash paid");
            Assert.That(c.Equipped(GearSlot.Racket).Id, Is.EqualTo("rk_power"), "and it's on");
            Assert.That(fe.Top, Is.InstanceOf<EquipmentScreen>());
            eq = (EquipmentScreen)fe.Top;

            // too dear: nothing
            var (dear, _) = eq.Cards.Find(x => x.item.Id == "rk_balance");
            Assert.That(dear.IsLocked, Is.True, "$6,500 with $1,500");
            eq.Focus(dear);
            yield return Tap(kb.enterKey);
            Assert.That(fe.Top, Is.SameAs(eq)); Assert.That(c.S.Cash, Is.EqualTo(1500));

            // shoes already owned: A puts them on, no money
            eq.Tabs.Select(1);
            yield return null; yield return null;
            var (grip, _) = eq.Cards.Find(x => x.item.Id == "sh_grip");
            eq.Focus(grip);
            yield return null;
            yield return Tap(kb.enterKey);
            yield return null; yield return null; yield return null;
            Assert.That(c.Equipped(GearSlot.Shoes).Id, Is.EqualTo("sh_grip"));
            Assert.That(c.S.Cash, Is.EqualTo(1500));
            eq = (EquipmentScreen)fe.Top;
            Assert.That(eq.Tabs.Current, Is.EqualTo("SHOES"), "back on the same tab");
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "P7_Equipment_Shoes.png");
            Assert.That(c.Effective.Stability, Is.EqualTo(7), "Grip Max: +2 stability");
            CareerStore.Delete();
        }

    }
}
