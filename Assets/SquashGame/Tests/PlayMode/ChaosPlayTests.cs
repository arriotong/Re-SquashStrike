// ===========================================================
// Assets/SquashGame/Tests/PlayMode/ChaosPlayTests.cs
// The Gauntlet chaos remake (2026-10-28) in the real scene: a Gauntlet
// match with a chaos build (an autopilot plays you) - the POWER meter
// fills, ULTIMATE READY shows, the ultimate fires (TIME STOP!, the blue
// of a frozen opponent), the points pop. Writes Captures/C3_*.png.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class ChaosPlayTests : InputTestFixture
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

        static RunMod M(string id, int l) { return new RunMod { Id = id, Level = l }; }

        [UnityTest]
        public IEnumerator PacksDecksAndARunFromYourDeck()
        {
            // 2026-10-28: the Gauntlet as card decks - packs for coins, the deck screen, a run that draws from it
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null && Object.FindFirstObjectByType<FrontEnd>().Top != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var cards = GauntletFlow.Cards;
            Assert.That(cards.Cards.Count, Is.GreaterThanOrEqualTo(10), "a starter set");
            Progression.GearDrops.Wallet.Coins = 3 * SquashSim.Modes.GauntletCards.PackCost;
            fe.Push(new GauntletScreen());
            yield return null; yield return null;
            var pack = fe.Top.Reachable().Find(i => i.Name.StartsWith("Open a pack"));
            Assert.That(pack, Is.Not.Null);
            int before = 0; foreach (var c in cards.Cards) before += c.Copies;
            pack.Submit();
            yield return null; yield return null;
            Assert.That(fe.Top, Is.InstanceOf<PackScreen>());
            Assert.That(((PackScreen)fe.Top).Count, Is.EqualTo(SquashSim.Modes.GauntletCards.PackSize));
            int after = 0; foreach (var c in cards.Cards) after += c.Copies;
            Assert.That(after, Is.EqualTo(before + SquashSim.Modes.GauntletCards.PackSize), "five cards in");
            Assert.That(Progression.GearDrops.Wallet.Coins, Is.EqualTo(2 * SquashSim.Modes.GauntletCards.PackCost), "paid in coins");
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "G6_Pack.png");

            fe.Replace(new DeckScreen());
            yield return null; yield return null;
            fe.Top.OnActionY();                                                       // the best 20
            yield return null; yield return null;
            Assert.That(cards.Deck.Count, Is.EqualTo(System.Math.Min(SquashSim.Modes.GauntletCards.DeckSize, cards.Cards.Count)));
            int n = cards.Deck.Count;
            var first = fe.Top.Reachable()[0];
            first.Submit();                                                           // out of the deck
            yield return null; yield return null;
            Assert.That(cards.Deck.Count, Is.EqualTo(n - 1));
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "G6_Deck.png");

            GauntletFlow.NewRun();
            var run = GauntletFlow.Run;
            Assert.That(run.HasDeck, Is.True, "the run draws from your deck");
            Assert.That(run.Deck.Count, Is.EqualTo(cards.Deck.Count));
            Assert.That(run.Abilities, Is.Empty, "nothing active at first");
            SquashSim.Modes.Gauntlet.Won(run, 3, 0, 0, GauntletFlow.State.Talents);
            foreach (var id in run.Offer) Assert.That(cards.Deck, Does.Contain(id), "drawn from the deck");
            fe.Replace(new GauntletScreen());
            yield return Wait(0.5f, () => false);
            yield return CaptureUI(fe, "G6_Pick.png");
            GauntletFlow.Abandon();
        }

        [UnityTest]
        public IEnumerator AChaosBuildFillsPowerAndFiresItsUltimate()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            fe.StartMatch(new MatchSettings
            {
                Opponent = "retriever", Difficulty = 0.45f, GamesToWin = 1, Scoring = "par", WarmUp = false, Gauntlet = true, ParPoints = 7,
                Mods = new List<RunMod> { M("freeze", 3), M("inferno", 3), M("jackpot", 3), M("overclock", 5), M("combo", 3), M("curve", 5), M("ricochet", 3), M("wallride", 2) },
                OppMods = new List<RunMod> { M("deaddrop", 3) },
                Court = "tour:pyramids", CrowdFill = 1f,
            });
            yield return Wait(3f, () => fe.Match != null && fe.Match.Settings.Gauntlet && !fe.Match.Demo);
            var r = fe.Match;
            r.SetAutopilot(new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.95), 7));
            r.SetTimeScale(3);
            var mods = r.Sim.Players[0].Player.Mods;
            Assert.That(mods, Is.Not.Null);
            Assert.That(mods.HasUltimate, Is.True);

            // the meter shows and fills; ULTIMATE READY
            yield return Wait(4f, () => fe.Hud.PowerText != null);
            Assert.That(fe.Hud.PowerText, Does.StartWith("POWER"), "the meter, under the score");
            yield return Wait(90f, () => mods.Power >= 1 || r.Sim.Phase == MatchPhase.MatchOver);
            if (mods.Power >= 1)
            {
                r.SetTimeScale(1);
                yield return null; yield return null;
                Assert.That(fe.Hud.PowerText, Does.StartWith("ULTIMATE READY"));
                yield return CaptureUI(fe, "C3_Chaos_Ready.png");
                r.SetTimeScale(3);
            }

            // the ultimate: TIME STOP!, the opponent frozen, the blue
            bool Fired() { foreach (var n in r.Sim.Chaos) if (n.Kind == "freeze") return true; return false; }
            yield return Wait(120f, () => Fired() || r.Sim.Phase == MatchPhase.MatchOver);
            Assert.That(Fired(), Is.True, "the ultimate fired (a charged shot on a full meter)");
            r.SetTimeScale(0.5);
            yield return null; yield return null; yield return null;
            Assert.That(fe.Hud.PopText, Is.EqualTo("TIME STOP!"));
            yield return CaptureUI(fe, "C3_Chaos_TimeStop.png");

            // the points: a pop when the cards add to a won rally
            r.SetTimeScale(3);
            bool Bonus() { foreach (var n in r.Sim.Chaos) if (n.Kind == "bonus" && n.Player == 0) return true; return false; }
            yield return Wait(120f, () => Bonus() || r.Sim.Phase == MatchPhase.MatchOver);
            if (Bonus())
            {
                r.SetTimeScale(0.5);
                yield return null; yield return null; yield return null;
                yield return CaptureUI(fe, "C3_Chaos_Points.png");
            }
            r.SetTimeScale(SimRunnerBase.GameSpeed);
        }
    }
}
