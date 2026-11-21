// ===========================================================
// Assets/SquashSim.Testing/Editor/GauntletFloorTests.cs
// 2026-11-21 the Gauntlet's arcade floors (GauntletFloor):
//   - points by how a rally is won, times the multiplier; a lost rally
//     resets it; lets don't count
//   - the streak to 5x (twists: a lower cap, slower growth, winners only)
//   - at the cap the wheel charges; the streak ending spins it - seeded
//   - the target reached ends the floor at once, the rallies left pay;
//     out of rallies short of it: failed
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Modes;

namespace SquashSim.Tests
{
    public class GauntletFloorTests
    {
        static FloorRally Win(RallyEnd kind = RallyEnd.Stroke, bool nick = false, bool ace = false, int shots = 3) { return new FloorRally { Won = true, Kind = kind, Nick = nick, Ace = ace, Shots = shots, GamePoints = 1 }; }
        static FloorRally Loss() { return new FloorRally { Won = false, Kind = RallyEnd.NotUp, Shots = 3, GamePoints = 0 }; }

        [Test] public void PointsByHowTimesTheMultiplier()
        {
            var f = GauntletFloor.Begin(1, 100000, 20, 9);
            var a = GauntletFloor.Rally(f, Win(RallyEnd.Stroke));
            Assert.That(a.Points, Is.EqualTo(GauntletFloor.StrokePts), "1x");
            Assert.That(f.Mult, Is.EqualTo(1 + GauntletFloor.PerWin));
            var b = GauntletFloor.Rally(f, Win(RallyEnd.NotUp));
            Assert.That(b.Points, Is.EqualTo((int)System.Math.Round(GauntletFloor.WinnerPts * (1 + GauntletFloor.PerWin))));
            Assert.That(b.What, Is.EqualTo("WINNER"));
            Assert.That(f.Mult, Is.EqualTo(1 + 2 * GauntletFloor.PerWin + GauntletFloor.PerGreat), "a winner grows it more");
            Assert.That(GauntletFloor.Rally(f, Win(RallyEnd.NotUp, nick: true)).What, Is.EqualTo("NICK"));
            int played = f.Played;
            GauntletFloor.Rally(f, new FloorRally { Let = true });
            Assert.That(f.Played, Is.EqualTo(played), "a let doesn't count");
            var lost = GauntletFloor.Rally(f, Loss());
            Assert.That(lost.Points, Is.EqualTo(0)); Assert.That(f.Mult, Is.EqualTo(1.0), "lost: back to 1x");
            var longOne = GauntletFloor.Rally(f, Win(RallyEnd.Down, shots: 10));
            Assert.That(longOne.Points, Is.EqualTo(GauntletFloor.ErrorPts + 4 * GauntletFloor.LongPerShot), "a long rally pays more");
        }

        [Test] public void TheStreakCapsAndTheTwistsBendIt()
        {
            var f = GauntletFloor.Begin(1, 100000, 30, 9);
            for (int i = 0; i < 12; i++) GauntletFloor.Rally(f, Win());
            Assert.That(f.Mult, Is.EqualTo(GauntletFloor.MaxMult), "5x at most");
            var c = GauntletFloor.Begin(1, 100000, 30, 9); c.MaxMult = 3.0;
            for (int i = 0; i < 12; i++) GauntletFloor.Rally(c, Win());
            Assert.That(c.Mult, Is.EqualTo(3.0), "Low Ceiling");
            var cold = GauntletFloor.Begin(1, 100000, 30, 9); cold.MultStep = 0.5;
            GauntletFloor.Rally(cold, Win());
            Assert.That(cold.Mult, Is.EqualTo(1 + GauntletFloor.PerWin * 0.5), "Cold Start");
            var w = GauntletFloor.Begin(1, 100000, 30, 9); w.WinnersOnly = true;
            Assert.That(GauntletFloor.Rally(w, Win(RallyEnd.Down)).Points, Is.EqualTo(0), "Winners Only");
            Assert.That(GauntletFloor.Rally(w, Win(RallyEnd.NotUp)).Points, Is.GreaterThan(0));
        }

        [Test] public void TheWheelChargesAtTheCapAndSpinsSeeded()
        {
            FloorPlay Charged(uint seed)
            {
                var f = GauntletFloor.Begin(3, 100000, 30, seed);
                while (f.Mult < GauntletFloor.MaxMult) GauntletFloor.Rally(f, Win());
                Assert.That(f.Charged, Is.True, "reaching the cap charges it");
                GauntletFloor.Rally(f, Win()); GauntletFloor.Rally(f, Win());
                Assert.That(f.Power, Is.EqualTo(GauntletFloor.MaxPower), "more wins at the cap: more power");
                Assert.That(f.SpinDue, Is.False, "still on a streak");
                GauntletFloor.Rally(f, Loss());
                Assert.That(f.SpinDue, Is.True, "the streak ended charged: spin");
                return f;
            }
            var a = Charged(77); var b = Charged(77);
            var sa = GauntletFloor.Spin(a); var sb = GauntletFloor.Spin(b);
            Assert.That(sa.Count, Is.GreaterThan(0));
            Assert.That(sa[0].Slice, Is.EqualTo(sb[0].Slice), "the same floor plays the same wheel");
            Assert.That(sa[0].Words, Is.Not.Empty);
            Assert.That(a.Power, Is.EqualTo(0)); Assert.That(a.SpinDue, Is.False); Assert.That(a.Spins, Is.EqualTo(1));
            Assert.That(GauntletFloor.Spin(a), Is.Empty, "nothing to spin");
            if (sa[0].Golden) Assert.That(sa.Count, Is.EqualTo(2), "gold: the Golden Wheel too");
            // over many seeds the wheel lands on every slice, gold among them
            var seen = new System.Collections.Generic.HashSet<WheelPrize>();
            for (uint s = 1; s < 200; s++) { var f = GauntletFloor.Begin(1, 100000, 30, s); f.Power = 3; f.SpinDue = true; foreach (var x in GauntletFloor.Spin(f)) seen.Add(x.Prize); }
            Assert.That(seen, Does.Contain(WheelPrize.Golden));
            Assert.That(seen.Count, Is.GreaterThan(8), "both wheels' prizes come up");
            var nw = GauntletFloor.Begin(1, 100000, 30, 5); nw.NoWheel = true;
            for (int i = 0; i < 12; i++) GauntletFloor.Rally(nw, Win());
            Assert.That(nw.Charged, Is.False, "No Wheel");
        }

        [Test] public void TheTargetEndsTheFloorAndTheRalliesLeftPay()
        {
            var f = GauntletFloor.Begin(1, 150, 10, 3);
            GauntletFloor.Rally(f, Win(RallyEnd.NotUp));                       // 100
            Assert.That(f.Done, Is.False);
            GauntletFloor.Rally(f, Win(RallyEnd.NotUp));                       // 100 x2.5: cleared
            Assert.That(f.Done, Is.True); Assert.That(f.Cleared, Is.True);
            Assert.That(f.Score, Is.EqualTo(100 + 250 + 8 * GauntletFloor.LeftoverPts), "8 rallies left over");
            Assert.That(f.Coins, Is.EqualTo(8 * GauntletFloor.LeftoverCoins));
            int s = f.Score;
            GauntletFloor.Rally(f, Win());
            Assert.That(f.Score, Is.EqualTo(s), "done: nothing more");

            var g = GauntletFloor.Begin(1, 10000, 3, 3);
            for (int i = 0; i < 3; i++) GauntletFloor.Rally(g, Win());
            Assert.That(g.Done, Is.True); Assert.That(g.Cleared, Is.False, "out of rallies: failed");
            Assert.That(g.Left, Is.EqualTo(0));
        }

        [Test] public void AFloorFromTheRun()
        {
            var run = Gauntlet.NewRun(8);
            var p = Gauntlet.BeginFloor(run, null);
            Assert.That(p.Target, Is.EqualTo(Gauntlet.TargetFor(1))); Assert.That(p.Rallies, Is.EqualTo(Gauntlet.Rallies));
            run.Abilities.Add(new RunMod { Id = "head", Level = 2 });
            Assert.That(Gauntlet.BeginFloor(run, null).Score, Is.EqualTo(200), "Head Start: points to start with");
            run.Floor = 5;
            var boss = Gauntlet.BeginFloor(run, null);
            var tw = Gauntlet.Floor(run, 5).Twists[0];
            if (tw == "ceiling") Assert.That(boss.MaxMult, Is.EqualTo(3.0));
            if (tw == "short") Assert.That(boss.Rallies, Is.EqualTo(Gauntlet.BossRallies - 2));
            if (tw == "nowheel") Assert.That(boss.NoWheel, Is.True);
            Assert.That(Gauntlet.BeginFloor(run, null).Seed, Is.EqualTo(boss.Seed), "seeded");
        }
    }
}
