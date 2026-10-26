// ===========================================================
// Assets/SquashSim.Testing/Editor/GearTests.cs
// P7 phase 5: the career's gear.
//   - every item trades: an upside and a downside (the starters: none);
//     prices $500..$8,000; three slots
//   - with gear a stat moves at most +-2 (all items together) and stays
//     within 0..10; Stability starts neutral
//   - buying: your cash pays, it's yours and on; no money, no gear;
//     only what you own goes on; your simulated matches feel it
//   - Stability: x0.6 at 10; it rides in a replay (format 6)
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Career;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class GearTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void EveryItemTradesSomething()
        {
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                Assert.That(Gear.Starter(slot), Is.Not.Null, slot + ": a starter");
                Assert.That(Gear.In(slot).Count, Is.InRange(4, 6), slot + ": a few to choose from (the 6-choice budget)");
            }
            var ids = new HashSet<string>();
            foreach (var g in Gear.All)
            {
                Assert.That(ids.Add(g.Id), Is.True, g.Id);
                int[] v = { g.Speed, g.Power, g.Accuracy, g.Stamina, g.Stability };
                bool up = System.Array.Exists(v, x => x > 0), down = System.Array.Exists(v, x => x < 0);
                foreach (int x in v) Assert.That(x, Is.InRange(-Gear.MaxShift, Gear.MaxShift), g.Name);
                if (g.Price == 0) { Assert.That(up || down, Is.False, g.Name + ": the starter is neutral"); continue; }
                Assert.That(g.Price, Is.InRange(500, 8000), g.Name);
                Assert.That(up, Is.True, g.Name + " gives something");
                // Dry-Fit is the one plain step (the plan's table); everything dearer trades
                if (g.Id != "kt_dryfit") Assert.That(down, Is.True, g.Name + " costs something");
                Assert.That(g.Line, Is.Not.EqualTo("NEUTRAL"));
            }
        }

        [Test] public void GearShiftsAtMostTwoAndStaysOnTheScale()
        {
            var stats = new AthleteAttributes { Speed = 9, Power = 1, Control = 5, Stamina = 0 };
            var none = Gear.Apply(stats, new GearItem[0]);
            Assert.That(none.Speed, Is.EqualTo(9)); Assert.That(none.Stability, Is.EqualTo(AthleteAttributes.Neutral));
            // three speed items: +1 +2 +1 = +4, held at +2; and 9 + 2 stays at 10
            var fast = Gear.Apply(stats, new[] { Gear.Find("rk_feather"), Gear.Find("sh_speed"), Gear.Find("kt_feather") });
            Assert.That(fast.Speed, Is.EqualTo(10), "9 + 2, kept at 10");
            Assert.That(fast.Stamina, Is.EqualTo(0), "0 - 2, kept at 0");
            Assert.That(fast.Power, Is.EqualTo(0), "1 - 1");
            Assert.That(fast.Stability, Is.EqualTo(4), "the feather twists");
            var steady = Gear.Apply(new AthleteAttributes(), new[] { Gear.Find("rk_balance"), Gear.Find("sh_grip"), Gear.Find("kt_compress") });
            Assert.That(steady.Stability, Is.EqualTo(7), "1 + 2 + 1, held at +2");
            Assert.That(steady.Speed, Is.EqualTo(3), "-1 -1 -1, held at -2");
            Assert.That(stats.Speed, Is.EqualTo(9), "your levels aren't touched");
        }

        [Test] public void YouBuyWithYourCashAndWearWhatYouOwn()
        {
            var c = CareerSession.New("Shopper", "ENG", 61);
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot))) Assert.That(c.Equipped(slot), Is.SameAs(Gear.Starter(slot)));
            Assert.That(c.Effective.Speed, Is.EqualTo(c.S.Attributes.Speed), "starters: your levels as they are");
            Assert.That(c.Equip("rk_power"), Is.False, "not yours yet");
            int cash = c.S.Cash;
            Assert.That(c.Buy("rk_power"), Is.True);
            Assert.That(c.S.Cash, Is.EqualTo(cash - 1500)); Assert.That(c.S.GearSpent, Is.EqualTo(1500));
            Assert.That(c.Owns("rk_power"), Is.True); Assert.That(c.Equipped(GearSlot.Racket).Id, Is.EqualTo("rk_power"), "on at once");
            Assert.That(c.Effective.Power, Is.EqualTo(c.S.Attributes.Power + 2));
            Assert.That(c.Effective.Speed, Is.EqualTo(c.S.Attributes.Speed - 1));
            Assert.That(c.CantBuy("rk_power"), Is.EqualTo("yours already"));
            Assert.That(c.Buy("rk_power"), Is.False);
            Assert.That(c.Equip("rk_club"), Is.True, "the starter is always yours");
            Assert.That(c.Equipped(GearSlot.Racket).Id, Is.EqualTo("rk_club"));
            c.S.Cash = 100;
            Assert.That(c.CantBuy("rk_balance"), Is.EqualTo("not enough cash"));
            Assert.That(c.Buy("rk_balance"), Is.False);
            Assert.That(c.S.Cash, Is.EqualTo(100));
            // the edge in a simulated match comes from your levels with your gear
            c.Equip("rk_power"); c.S.Cash = 10000; c.Buy("sh_cushion"); c.Buy("kt_dryfit");
            Assert.That(c.AttributeEdge, Is.EqualTo(System.Math.Round(0.025 * (c.Effective.Mean - 5), 4)));
            // it saves
            var back = new CareerSession(UnityEngine.JsonUtility.FromJson<CareerState>(UnityEngine.JsonUtility.ToJson(c.S)));
            Assert.That(back.Equipped(GearSlot.Racket).Id, Is.EqualTo("rk_power"));
            Assert.That(back.Owns("sh_cushion"), Is.True);
        }

        [Test] public void StabilityScalesAndRidesInAReplay()
        {
            Assert.That(new AthleteAttributes().StabilityScale, Is.EqualTo(1.0));
            Assert.That(new AthleteAttributes { Stability = 10 }.StabilityScale, Is.EqualTo(0.6).Within(1e-12));
            Assert.That(new AthleteAttributes { Stability = 0 }.StabilityScale, Is.EqualTo(1.4).Within(1e-12));
            Assert.That(new AthleteAttributes { Stability = 6 }.IsNeutral, Is.False);
            var f = new ReplayFile();
            f.Setup.Attr0 = new AthleteAttributes { Speed = 4, Stability = 7 };
            f.Setup.Attr1 = new AthleteAttributes { Power = 8, Stability = 3 };
            var back = ReplayFile.FromBytes(f.ToBytes());
            Assert.That(back.Setup.Attr0.Stability, Is.EqualTo(7)); Assert.That(back.Setup.Attr0.Speed, Is.EqualTo(4));
            Assert.That(back.Setup.Attr1.Stability, Is.EqualTo(3)); Assert.That(back.Setup.Attr1.Power, Is.EqualTo(8));
            var copy = new AthleteAttributes(); copy.CopyFrom(f.Setup.Attr0);
            Assert.That(copy.Stability, Is.EqualTo(7));
        }
    }
}
