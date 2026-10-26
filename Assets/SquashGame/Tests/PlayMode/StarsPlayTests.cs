// ===========================================================
// Assets/SquashGame/Tests/PlayMode/StarsPlayTests.cs
// P6 the stars in the game:
//   - Quick Match is three rows (Opponent, Match, Court); the opponent
//     carousel climbs to the world No. 1, whose card shows; the match
//     against them carries their rating, attributes, signature and name
//   - the name editor (from your profile) renames the No. 1 everywhere
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class StarsPlayTests : InputTestFixture
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

        static IEnumerator Boot(System.Action<FrontEnd> got)
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            got(Object.FindFirstObjectByType<FrontEnd>());
        }

        [UnityTest]
        public IEnumerator QuickMatchIsThreeRowsUpToTheWorldNo1()
        {
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            fe.Push(new QuickMatchScreen());
            yield return null; yield return null;
            var qm = (QuickMatchScreen)fe.Top;
            var rows = qm.Reachable().FindAll(i => !(i is PrimaryItem));
            Assert.That(rows.ConvertAll(i => i.Name), Is.EqualTo(new[] { "Opponent", "Match", "Abilities", "Court" }), "four rows, nothing else (batch 2: abilities, off by default)");
            Assert.That(ProfileStore.Current.Choices.Abilities, Is.False);
            Assert.That(ProfileStore.Current.Choices.OpponentId, Is.EqualTo(MatchChoices.Ladder[0]), "a new player starts at the bottom");

            var opp = (CarouselItem)rows[0];
            for (int k = 0; k < MatchChoices.Ladder.Length - 1; k++) opp.Change(1);
            Assert.That(ProfileStore.Current.Choices.OpponentId, Is.EqualTo("star:s01"), "the top of the ladder: the world No. 1");
            Assert.That(opp.Value, Does.StartWith(Stars.FullName(Stars.No1)).And.EndWith("99"));
            yield return CaptureUI(fe, "P6_QuickMatch_No1.png");

            qm.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return Wait(2f, () => fe.Top == null);
            var r = fe.Match;
            Assert.That(r.Settings.Opponent, Is.EqualTo("star:s01"));
            Assert.That(r.Settings.Difficulty, Is.EqualTo(1f), "the AI at its very best");
            Assert.That(r.Sim.Players[1].Player.Attributes.Power, Is.EqualTo(Stars.No1.Attributes.Power), "their own athlete");
            Assert.That(r.Sim.Players[1].Player.Mods, Is.Not.Null, "and their signature shots");
            Assert.That(r.Names[1], Is.EqualTo(Stars.No1.Last.ToUpperInvariant()), "the score bug");
            Assert.That(r.Settings.FocusCharges, Is.EqualTo(0), "abilities off: no Focus");
            Assert.That(r.Sim.Players[0].Player.Mods, Is.Null, "and no abilities for you");
        }

        [UnityTest]
        public IEnumerator QuickMatchAbilitiesAreOptional()
        {
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            fe.Push(new QuickMatchScreen());
            yield return null; yield return null;
            var qm = (QuickMatchScreen)fe.Top;
            qm.Reachable().Find(i => i.Name == "Abilities").Change(1);
            Assert.That(ProfileStore.Current.Choices.Abilities, Is.True);
            yield return CaptureUI(fe, "B2_QuickMatch_Abilities.png");
            qm.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return Wait(2f, () => fe.Top == null);
            var r = fe.Match;
            Assert.That(r.Settings.FocusCharges, Is.EqualTo(MatchChoices.AbilityFocus), "Focus, twice a game");
            Assert.That(r.Sim.Players[0].Player.Mods, Is.Not.Null, "your arcade shots");
            Assert.That(r.Settings.OppMods.Exists(m => m.Id == "ace"), Is.True, "aces count double for them too");
        }

        [UnityTest]
        public IEnumerator TheNameEditorRenamesTheNo1Everywhere()
        {
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            fe.Push(new ProfileScreen());
            yield return null; yield return null;
            fe.Top.Reachable().Find(i => i.Name == "Player names").Submit();
            yield return null; yield return null;
            var ed = (PlayerEditorScreen)fe.Top;
            Assert.That(ChoiceBudgetTests.Count(ed), Is.LessThanOrEqualTo(ChoiceBudgetTests.Budget));
            Assert.That(ed.Current, Is.SameAs(Stars.No1), "it opens on the No. 1");
            ed.First.Field.value = "Real"; ed.Last.Field.value = "Legend";
            yield return CaptureUI(fe, "P6_PlayerNames.png");
            ed.Back();
            yield return null;
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo("Real Legend"));
            Assert.That(File.Exists(Path.Combine(PlayerNames.Folder, PlayerNames.FileName)), Is.True, "kept on this PC");
            Assert.That(MatchChoices.Line("star:s01"), Does.StartWith("Real Legend"), "the PLAY card");
            PlayerNames.ResetOne("s01");
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo(Stars.No1.First + " " + Stars.No1.Last));
        }
    }
}
