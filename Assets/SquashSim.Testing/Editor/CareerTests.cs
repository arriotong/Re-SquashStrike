// ===========================================================
// Assets/SquashSim.Testing/Editor/CareerTests.cs
// P2-M4: the Career, headless.
//   - the roster: 80 pros, strengths in the tier bands, no repeats
//   - draws: seeds 1 and 2 at the ends, every round halves, the same
//     seed gives the same draw
//   - the rally model: even at equal strength, legal scorelines (PAR
//     and English), the calibration it was fitted to
//   - rankings: points by round, the best 8 of the last 12
//   - seasons: every event finishes; a strong player climbs from Club
//     to Tour inside two seasons, a weak one stays down; the save
//     round-trips mid-event and plays out identically
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Career;
using UnityEngine;

namespace SquashSim.Tests
{
    public class CareerTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }

        [Test] public void TheRosterFillsTheTierBands()
        {
            var pros = Roster.Generate(7);
            Assert.That(pros.Count, Is.EqualTo(Tour.RosterSize));
            var names = new HashSet<string>();
            foreach (var p in pros) Assert.That(names.Add(p.Last), Is.True, "surnames are not repeated: " + p.Last);
            double Mean(int top, int bottom) { double s = 0; for (int i = top - 1; i < bottom; i++) s += pros[i].Strength; return s / (bottom - top + 1); }
            Debug.Log("[Career] band strengths: Tour " + Mean(1, 32).ToString("0.00") + ", Challenger " + Mean(33, 48).ToString("0.00") +
                      ", Regional " + Mean(49, 64).ToString("0.00") + ", Club " + Mean(65, 80).ToString("0.00"));
            Assert.That(Mean(65, 80), Is.LessThan(0.05));
            Assert.That(Mean(49, 64), Is.InRange(0.04, 0.14));
            Assert.That(Mean(33, 48), Is.InRange(0.12, 0.27));
            Assert.That(pros[0].Strength, Is.InRange(0.55, Roster.MaxStrength));
        }

        [Test] public void TheRallyModelIsFairAndScoresLegally()
        {
            int aWins = 0;
            var rng = new CareerRng(3);
            for (int i = 0; i < 2000; i++) if (RallyModel.Play(0.5, 2, false, rng).AWon) aWins++;
            Assert.That(aWins / 2000.0, Is.InRange(0.47, 0.53), "equal strength: even");
            for (int i = 0; i < 300; i++)
            {
                bool english = i % 2 == 1;
                var r = RallyModel.Play(rng.Between(0.3, 0.7), 2, english, rng);
                int gA = 0, gB = 0;
                foreach (var g in r.Score.Split(' '))
                {
                    var p = g.Split('-'); int a = int.Parse(p[0]), b = int.Parse(p[1]);
                    int hi = System.Math.Max(a, b), lo = System.Math.Min(a, b);
                    if (english) Assert.That(hi == 9 || hi == 10, Is.True, "English game to 9 (or 10 when set): " + g);
                    else Assert.That(hi == 11 ? lo <= 9 : hi - lo == 2 && hi > 11, Is.True, "PAR to 11, win by 2: " + g);
                    if (a > b) gA++; else gB++;
                }
                Assert.That(System.Math.Max(gA, gB), Is.EqualTo(2), "best of 3: " + r.Score);
                Assert.That(r.AWon, Is.EqualTo(gA == 2));
            }
            // the bench numbers it was fitted to (MatchBench --career-calibrate, 2026-10-18)
            Assert.That(RallyModel.ProRally(0.30, 0.0), Is.EqualTo(0.616).Within(0.01));
            Assert.That(RallyModel.ProRally(0.60, 0.0), Is.EqualTo(0.710).Within(0.01));
            Assert.That(RallyModel.YouRally(0.09, 0.60), Is.EqualTo(0.18).Within(0.02), "human-style model vs 0.60");
        }

        [Test] public void DrawsAreSeededAndRepeatable()
        {
            var a = CareerSession.New("Tester", "ENG", 42);
            var b = CareerSession.New("Tester", "ENG", 42);
            var e = a.Enter(Tier.Club);
            var f = b.Enter(Tier.Club);
            Assert.That(e.Rounds[0].Players, Is.EqualTo(f.Rounds[0].Players), "same seed, same draw");
            Assert.That(e.Rounds[0].Players.Count, Is.EqualTo(16));
            Assert.That(e.Rounds[0].Players, Does.Contain(CareerSession.You));
            var field = new List<int>(e.Rounds[0].Players);
            field.Sort((x, y) => a.RankOf(x).CompareTo(a.RankOf(y)));
            Assert.That(e.Rounds[0].Players[0], Is.EqualTo(field[0]), "the top seed at the top");
            Assert.That(e.Rounds[0].Players[15], Is.EqualTo(field[1]), "the second seed at the bottom");
            while (a.NextOpponent() >= 0) a.SimulateYours();
            a.FinishSlot();
            var done = a.S.Last;
            Assert.That(done.Done, Is.True);
            for (int i = 1; i < done.Rounds.Count; i++) Assert.That(done.Rounds[i].Players.Count, Is.EqualTo(done.Rounds[i - 1].Players.Count / 2));
            Assert.That(done.YouReached, Is.InRange(0, 4));
            Assert.That(done.YouPoints, Is.EqualTo(Tour.Info(Tier.Club).Points[done.YouReached]));
        }

        [Test] public void TheRankingCountsTheBestEightOfTwelve()
        {
            var c = CareerSession.New("Tester", "ENG", 5);
            Assert.That(c.RankOf(CareerSession.You), Is.EqualTo(Tour.RosterSize + 1), "you start unranked, at the bottom");
            // ten results for you in the window: only the best 8 count
            for (int i = 0; i < 10; i++) c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1 - i, Tier = 0, Reached = 0, Points = 10 * (i + 1) });
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 13, Tier = 0, Reached = 0, Points = 5000 });   // too old
            Assert.That(c.PointsOf(CareerSession.You), Is.EqualTo(100 + 90 + 80 + 70 + 60 + 50 + 40 + 30));
        }

        static int HighestTier(CareerSession c, int seasons, double strength)
        {
            int best = 0;
            SeasonRunner.Months(c, seasons * Tour.SlotsPerSeason, SeasonRunner.Model(strength), (cc, e) => { if (e != null && e.YouIn) best = System.Math.Max(best, e.Tier); });
            return best;
        }

        [Test] public void AStrongPlayerClimbsToTourAndAWeakOneStaysDown()
        {
            var strong = CareerSession.New("Strong", "ENG", 11);
            int top = HighestTier(strong, 2, 0.45);
            Debug.Log("[Career] strong (0.45): rank " + strong.RankOf(0) + " after 2 seasons, highest tier " + (Tier)top + ", rating " + strong.S.Rating.ToString("0.00") +
                      ", titles " + strong.S.Titles + ", earnings " + strong.S.Earnings);
            Assert.That(top, Is.GreaterThanOrEqualTo((int)Tier.Tour), "a strong player reaches the Tour inside two seasons");
            Assert.That(strong.S.Rating, Is.InRange(0.30, 0.60), "the rating finds the true strength");

            var mid = CareerSession.New("Mid", "ENG", 12);
            int midTop = HighestTier(mid, 2, 0.09);
            Debug.Log("[Career] human-style (0.09): rank " + mid.RankOf(0) + " after 2 seasons, highest tier " + (Tier)midTop + ", rating " + mid.S.Rating.ToString("0.00"));

            var weak = CareerSession.New("Weak", "ENG", 13);
            int weakTop = HighestTier(weak, 2, -0.15);
            Debug.Log("[Career] weak (-0.15): rank " + weak.RankOf(0) + " after 2 seasons, highest tier " + (Tier)weakTop);
            Assert.That(weakTop, Is.LessThanOrEqualTo((int)Tier.Regional), "a weak player stays in the lower tiers");
            Assert.That(strong.S.Seasons.Count, Is.EqualTo(2), "two season summaries");
        }

        [Test] public void ASaveMidEventPlaysOutTheSame()
        {
            var c = CareerSession.New("Saver", "SCO", 21);
            c.Enter(Tier.Club);
            c.SimulateYours();
            string json = JsonUtility.ToJson(c.S);
            var copy = new CareerSession(JsonUtility.FromJson<CareerState>(json));
            Assert.That(copy.NextOpponent(), Is.EqualTo(c.NextOpponent()));
            SeasonRunner.Months(c, 0, null);
            while (c.NextOpponent() >= 0) c.SimulateYours();
            while (copy.NextOpponent() >= 0) copy.SimulateYours();
            c.FinishSlot(); copy.FinishSlot();
            Assert.That(JsonUtility.ToJson(copy.S), Is.EqualTo(JsonUtility.ToJson(c.S)), "identical after the event");
            var empty = new CareerSession(JsonUtility.FromJson<CareerState>(JsonUtility.ToJson(c.S)));
            Assert.That(empty.S.Current, Is.Null, "no event in progress after a load between events");
        }
    }
}
