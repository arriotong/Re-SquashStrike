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
        public IEnumerator QuickMatchInStepsUpToTheWorldNo1()
        {
            // 2026-11-22 (the user: "spread the clicking process into a few tabs"): the match, the opponent, the court, your class, ready
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            fe.Push(new QuickMatchScreen());
            yield return null; yield return null;
            var qm = (QuickMatchScreen)fe.Top;
            var rows = qm.Reachable().FindAll(i => !(i is PrimaryItem));
            Assert.That(rows.ConvertAll(i => i.Name), Is.EqualTo(new[] { "Match", "Scoring", "Abilities" }), "step 1: the match");
            Assert.That(ProfileStore.Current.Choices.OpponentId, Is.EqualTo(MatchChoices.Ladder[0]), "a new player starts at the bottom");
            yield return CaptureUI(fe, "SETUP_1_Format.png");
            qm.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return null; yield return null;

            // step 2: the opponent - the ladder in a grid, the pro on the right
            Assert.That(fe.Top, Is.InstanceOf<OpponentPickScreen>());
            var op = (OpponentPickScreen)fe.Top;
            var tiles = op.Reachable().FindAll(i => i is CardItem);
            Assert.That(tiles.Count, Is.EqualTo(MatchChoices.Ladder.Length), "everyone on the ladder");
            Assert.That(op.FocusedId, Is.EqualTo(MatchChoices.Ladder[0]), "on the one you had");
            var top = (CardItem)tiles[tiles.Count - 1];
            op.Focus(top);
            yield return Wait(0.6f, () => false);
            Assert.That(op.FocusedId, Is.EqualTo("star:s01"), "the top of the ladder: the world No. 1");
            yield return CaptureUI(fe, "SETUP_2_Opponent.png");
            top.Submit();
            yield return null; yield return null;
            Assert.That(ProfileStore.Current.Choices.OpponentId, Is.EqualTo("star:s01"));

            // step 3: the court
            Assert.That(fe.Top, Is.InstanceOf<CourtPickScreen>());
            var cp = (CourtPickScreen)fe.Top;
            Assert.That(cp.Reachable().FindAll(i => i is CardItem).Count, Is.EqualTo(Courts.Pickable.Length), "every court");
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "SETUP_3_Court.png");
            ((CardItem)cp.Focused).Submit();
            yield return null; yield return null;

            // step 4: your class
            Assert.That(fe.Top, Is.InstanceOf<ClassPickScreen>());
            var kp = (ClassPickScreen)fe.Top;
            var classes = kp.Reachable().FindAll(i => i is CardItem);
            Assert.That(classes.Count, Is.EqualTo(5), "five classes");
            var power = (CardItem)classes.Find(i => ((CardItem)i).Title == "Power");
            kp.Focus(power);
            yield return Wait(0.6f, () => false);
            yield return CaptureUI(fe, "SETUP_4_Class.png");
            power.Submit();
            yield return null; yield return null;
            Assert.That(ProfileStore.Current.Choices.Class, Is.EqualTo("power"));

            // ready: what you chose; Back goes one step
            Assert.That(fe.Top, Is.InstanceOf<QuickMatchReadyScreen>());
            yield return Wait(0.4f, () => false);
            yield return CaptureUI(fe, "SETUP_5_Ready.png");
            var ready = fe.Top;
            ready.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return Wait(2f, () => fe.Top == null);
            var r = fe.Match;
            Assert.That(r.Settings.Opponent, Is.EqualTo("star:s01"));
            Assert.That(r.Settings.Class, Is.EqualTo("power"), "your class");
            Assert.That(r.Sim.Players[0].Player.Attributes.Power, Is.EqualTo(8.0).Within(1e-9), "Power: +3 on the athlete");
            Assert.That(r.Sim.Players[0].Player.Mods.Level("heavyball"), Is.EqualTo(1), "and its perk");
            Assert.That(r.Settings.Difficulty, Is.EqualTo(1f), "the AI at its very best");
            Assert.That(r.Sim.Players[1].Player.Attributes.Power, Is.EqualTo(Stars.No1.Attributes.Power), "their own athlete");
            Assert.That(r.Sim.Players[1].Player.Mods, Is.Not.Null, "and their signature shots");
            Assert.That(r.Names[1], Is.EqualTo(Stars.No1.Last.ToUpperInvariant()), "the score bug");
            Assert.That(r.Settings.FocusCharges, Is.EqualTo(0), "abilities off: no Focus");
            Assert.That(r.Sim.Players[0].Player.Mods.HasUltimate, Is.False, "and no special for you");
        }

        [UnityTest]
        public IEnumerator QuickMatchAbilitiesAreOffUnlessTurnedOn()
        {
            // 2026-11-20: the Abilities row is back (Off / On), Off unless the player turns it on
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            Assert.That(ProfileStore.Current.Choices.Abilities, Is.False, "off for a new player");
            fe.Push(new QuickMatchScreen());
            yield return null; yield return null;
            var qm = (QuickMatchScreen)fe.Top;
            var row = (SelectorItem)qm.Reachable().Find(i => i.Name == "Abilities");
            Assert.That(row, Is.Not.Null, "an Abilities row");
            Assert.That(row.Index, Is.EqualTo(0), "showing Off");
            row.Change(1);
            Assert.That(ProfileStore.Current.Choices.Abilities, Is.True, "turned on");
            fe.Push(new QuickMatchReadyScreen());                                    // (2026-11-22: the last step)
            yield return null; yield return null;
            fe.Top.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return Wait(2f, () => fe.Top == null);
            var r = fe.Match;
            Assert.That(r.Settings.MeterFocus, Is.True, "Focus from the Special meter");
            Assert.That(r.Sim.Players[0].Player.Mods.HasUltimate, Is.True, "you have the Special Shot");
            Assert.That(r.Sim.Players[1].Player.Mods.Level("special"), Is.EqualTo(1), "they have the Special Shot too");
            Assert.That(r.Settings.Mods, Is.Not.Null.And.Not.Empty, "your abilities");
        }

        /// <summary>2026-11-20 the specials (after Mario Tennis): the meter top right, Focus spends some of it and dims the screen,
        /// a full meter fires the Special Shot - the computer's too.</summary>
        [UnityTest]
        public IEnumerator QuickMatchSpecialsMeterFocusAndSpecialShot()
        {
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            ProfileStore.Current.Choices.Abilities = true;
            ProfileStore.Current.Settings.WarmUp = false;                           // (straight to the rally)
            fe.Push(new QuickMatchReadyScreen());                                    // (2026-11-22: the last step)
            yield return null; yield return null;
            fe.Top.Reachable().Find(i => i is PrimaryItem).Submit();
            yield return Wait(2f, () => fe.Top == null);
            var r = fe.Match;
            r.SetAutopilot(new SquashSim.OpponentBrain(SquashSim.AiProfile.ByName("retriever").WithDifficulty(0.9), 11));
            yield return Wait(25f, () => r.Sim.Phase == SquashSim.MatchPhase.Rally);
            Assert.That(r.Sim.Phase, Is.EqualTo(SquashSim.MatchPhase.Rally), "a rally under way");
            Assert.That(fe.Hud.SpecialMeterShown, Is.True, "the Special meter, top right");

            var mine = r.Sim.Players[0].Player.Mods;

            // 2026-11-22 the Special Shot: a full meter, the ball coming - you (the stand-in) jump to it, time stops, the aiming view
            yield return Wait(3f, () => !r.Focusing);
            mine.Power = 1;
            yield return Wait(25f, () => r.Sim.ZoneBy == 0);
            Assert.That(r.Sim.ZoneBy, Is.EqualTo(0), "you're aiming a Special Shot");
            var rig = Camera.main.GetComponent<GameplayCameraRig>();
            yield return Wait(0.35f, () => false);
            Assert.That(rig.ZoneCam, Is.True, "the aiming view");
            Assert.That(fe.Hud.FocusDim, Is.GreaterThan(0.5f), "time stands still: the screen dims");
            var held = r.Sim.Ball.Pos.Clone();
            yield return null; yield return null;
            Assert.That(r.Sim.Ball.Pos.Z, Is.EqualTo(held.Z).Within(1e-9), "the ball hangs");
            Assert.That(r.Sim.InAir(0), Is.True, "2026-11-22: up in the air above the T");
            var fx = Object.FindFirstObjectByType<SpecialFx>();
            Assert.That(rig.ZoneCam, Is.True, "first person: through your eyes");
            Assert.That(fx.AuraShown, Is.False, "(your own aura isn't drawn across your eyes)");
            yield return FrontEndTests.CaptureUI(fe, "SPECIAL_Aim.png");
            // the wind-up: 0.5 s, the world moving, the spark and the target warning
            yield return Wait(4f, () => r.Sim.WindupBy == 0);
            Assert.That(r.Sim.WindupBy, Is.EqualTo(0), "the wind-up");
            yield return null; yield return null;
            Assert.That(fx.SparkShown && fx.TargetShown, Is.True, "the spark and the target on the front wall");
            yield return FrontEndTests.CaptureUI(fe, "SPECIAL_Windup.png");
            yield return Wait(4f, () => r.Sim.SpecialBy == 0);
            Assert.That(r.Sim.SpecialBy, Is.EqualTo(0), "fired");
            Assert.That(mine.Power, Is.LessThan(0.1), "the meter spent (a shot since may have added a sliver)");
            yield return Wait(0.05f, () => false);
            Assert.That(fx.TrailShown, Is.True, "the lightning trail");
            yield return FrontEndTests.CaptureUI(fe, "SPECIAL_Shot.png");
            yield return Wait(1f, () => fx.WallFlashes > 0);
            Assert.That(fx.WallFlashes, Is.GreaterThan(0), "the front wall's flash");
            yield return Wait(0.6f, () => !r.Sim.InAir(0));
            Assert.That(r.Sim.InAir(0), Is.False, "back down on the floor");

            // the computer's: it jumps to the ball and fires too
            yield return Wait(8f, () => r.Sim.Phase == SquashSim.MatchPhase.Rally && r.Sim.SpecialBy < 0);
            var theirs = r.Sim.Players[1].Player.Mods;
            theirs.Power = 1;
            yield return Wait(25f, () => r.Sim.ZoneBy == 1);
            Assert.That(r.Sim.ZoneBy, Is.EqualTo(1), "their Special Shot");
            yield return null; yield return null;
            Assert.That(fe.Hud.PopText, Is.EqualTo("THEIR SPECIAL SHOT!"));
            yield return FrontEndTests.CaptureUI(fe, "SPECIAL_Theirs.png");

            // 2026-11-22 Focus, held, against it: the rally slows, the meter drains (not a chunk), the Special Shot slowed
            mine.Power = 0.6;
            yield return Wait(6f, () => r.Focusing);
            Assert.That(r.Focusing, Is.True, "you (the stand-in) focus against it");
            Assert.That(r.Sim.SpecialDefended, Is.True, "their Special Shot slowed");
            double p0 = mine.Power;
            yield return Wait(0.3f, () => false);
            Assert.That(mine.Power, Is.LessThan(p0).And.GreaterThan(p0 - 0.3), "the meter drains slowly while you focus");
            Assert.That(fe.Hud.FocusDim, Is.GreaterThan(0.3f), "the screen dims");
            yield return FrontEndTests.CaptureUI(fe, "SPECIAL_Focus.png");
        }

        [UnityTest]
        public IEnumerator TheCourtIsPickedFromAGrid()
        {
            // 2026-11-22: the courts in a grid (left), the court itself on the right and behind the menu
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd fe = null;
            yield return Boot(f => fe = f);
            fe.Push(new CourtPickScreen(false));
            yield return null; yield return null;
            var cp = (CourtPickScreen)fe.Top;
            var tiles = cp.Reachable().FindAll(i => i is CardItem);
            Assert.That(tiles.Count, Is.EqualTo(Courts.Pickable.Length), "every court in it");
            var last = (CardItem)tiles[tiles.Count - 1];
            cp.Focus(last);
            yield return null; yield return null;
            Assert.That(Courts.CurrentId, Is.EqualTo(Courts.Pickable[Courts.Pickable.Length - 1].Id), "the court behind follows the tile");
            last.Submit();
            yield return null;
            Assert.That(ProfileStore.Current.Choices.Court, Is.EqualTo(Courts.Pickable.Length - 1), "picked in one go");
            Assert.That(fe.Top, Is.InstanceOf<ClassPickScreen>(), "on to your class");
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
