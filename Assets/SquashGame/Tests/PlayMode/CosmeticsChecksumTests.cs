// ===========================================================
// Assets/SquashGame/Tests/PlayMode/CosmeticsChecksumTests.cs
// P2-M5 exit test: cosmetics never change the simulation.
//
// The same match (the same bots, the same seeds) is played twice in the
// real match scene, one fixed step per frame, with completely different
// kits on both players - every slot changed, bands on and off. The sim
// state (ball, both players, score, phase) is hashed at every step; the
// two runs must agree at every one of them. The athletes are checked to
// be really dressed differently.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class CosmeticsChecksumTests
    {
        const int Steps = 1500;

        static ulong Mix(ulong h, double v) { unchecked { h ^= (ulong)System.BitConverter.DoubleToInt64Bits(v); h *= 1099511628211UL; return h; } }

        static ulong Hash(MatchSimulation m)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, m.Ball.Pos.X); h = Mix(h, m.Ball.Pos.Y); h = Mix(h, m.Ball.Pos.Z);
            h = Mix(h, m.Ball.Vel.X); h = Mix(h, m.Ball.Vel.Y); h = Mix(h, m.Ball.Vel.Z);
            for (int i = 0; i < 2; i++) { var p = m.Players[i].Player; h = Mix(h, p.Pos.X); h = Mix(h, p.Pos.Z); h = Mix(h, p.Vel.X); h = Mix(h, p.Vel.Z); }
            h = Mix(h, m.Score.Points[0]); h = Mix(h, m.Score.Points[1]); h = Mix(h, (int)m.Phase); h = Mix(h, m.History.Count);
            return h;
        }

        static IEnumerator Run(Loadout mine, uint kitSeed, List<ulong> hashes, List<Athlete.AthleteFigure> dressed)
        {
            ProfileStore.Current.Progress.Loadout = mine;
            SceneManager.LoadScene("Match");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f && Object.FindFirstObjectByType<FrontEnd>() == null) yield return null;
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var r = fe.Match;
            r.BotSeed = 4242;
            fe.StartMatch(new MatchSettings { Opponent = "attacker", Difficulty = 0.42f, GamesToWin = 1, KitSeed = kitSeed });
            var m = r.Sim;
            m.Bots[0] = new OpponentBrain(AiProfile.ByName("grinder").WithDifficulty(0.42), 777);   // "you": an AI, so both runs play the same
            r.StepOnce();                                                     // one fixed step per frame from here
            yield return null; yield return null;
            dressed.Clear();
            dressed.AddRange(Object.FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None));
            dressed.Sort((x, y) => x.Index.CompareTo(y.Index));                 // you first
            int guard = 0;
            while (hashes.Count < Steps && guard++ < Steps * 4)
            {
                r.StepOnce();
                yield return null;
                hashes.Add(Hash(m));
            }
        }

        [UnityTest]
        public IEnumerator CosmeticsNeverChangeTheSimulation()
        {
            LocalMatchTests.FreshProfile();
            FrontEnd.StartAtTitle = false;
            var a = new List<ulong>(); var b = new List<ulong>();
            var dressed = new List<Athlete.AthleteFigure>();

            yield return Run(new Loadout(), 0, a, dressed);
            Assert.That(dressed.Count, Is.EqualTo(2));
            string wornA0 = dressed[0].Worn.Shirt + dressed[1].Worn.Shirt;
            bool bandsA = dressed[0].Rig.HeadbandShown;

            var loud = new Loadout
            {
                Shirt = "shirt_blacklime", Shorts = "shorts_orange", Socks = "socks_pink", Shoes = "shoes_gold", Skin = "skin_6",
                Headband = "headband_red", Wristband = "wristband_lime", Frame = "frame_purple", Grip = "grip_orange", Strings = "strings_red",
                Celebration = "cel_knee", Banner = "banner_neon", Title = "title_world"
            };
            yield return Run(loud, 0xC0FFEEu, b, dressed);
            string wornB0 = dressed[0].Worn.Shirt + dressed[1].Worn.Shirt;
            Assert.That(wornB0, Is.Not.EqualTo(wornA0), "the athletes were dressed differently");
            Assert.That(dressed[0].Rig.HeadbandShown, Is.Not.EqualTo(bandsA), "and the headband came on");

            Assert.That(a.Count, Is.EqualTo(Steps));
            Assert.That(b.Count, Is.EqualTo(Steps));
            Assert.That(new HashSet<ulong>(a).Count, Is.GreaterThan(Steps / 2), "the match really moved (not a frozen state)");
            for (int i = 0; i < Steps; i++) Assert.That(b[i], Is.EqualTo(a[i]), "the sim differs at step " + i);
            Debug.Log("[P2M5] checksum: " + Steps + " steps identical with two different kits (final " + a[Steps - 1].ToString("x16") + ")");
        }
    }
}
