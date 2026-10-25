// ===========================================================
// Assets/SquashSim.Testing/Editor/CareerTests.cs
// P2-M4: the Career, headless (the PSA system since 2026-10-24).
//   - the world: 480 pros, strength falling with the place, no repeats
//   - draws: seeds 1 and 2 at the ends, every round halves, the same
//     seed gives the same draw
//   - the rally model: even at equal strength, legal scorelines (PAR
//     and English), the calibration it was fitted to
//   - rankings: you start unranked; the PSA average (12 months, divided
//     by at least 10); only the Challenger 3s are open to the unranked;
//     nobody plays two events in a month
//   - seasons: every event finishes; a strong player climbs onto the
//     World Tour inside two seasons, a weak one stays in the Challengers;
//     the save round-trips mid-event and plays out identically
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

        [Test] public void TheWorldFallsAwayFromTheTop()
        {
            var pros = Roster.Generate(7);
            Assert.That(pros.Count, Is.EqualTo(Tour.RosterSize));
            var names = new HashSet<string>();
            foreach (var p in pros) Assert.That(names.Add(p.Last), Is.True, "surnames are not repeated: " + p.Last);
            double Mean(int top, int bottom) { double s = 0; for (int i = top - 1; i < bottom; i++) s += pros[i].Strength; return s / (bottom - top + 1); }
            Debug.Log("[Career] strengths by place: 1-32 " + Mean(1, 32).ToString("0.00") + ", 33-140 " + Mean(33, 140).ToString("0.00") +
                      ", 141-300 " + Mean(141, 300).ToString("0.00") + ", 301-480 " + Mean(301, 480).ToString("0.00"));
            Assert.That(Mean(1, 32), Is.GreaterThan(0.5));
            Assert.That(Mean(401, 480), Is.LessThan(0.03));
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
            // the human-style model (r ~ 0.12) against the levels (MatchBench --balance, 2026-10-20): 59% / 31%
            Assert.That(RallyModel.YouRally(0.12, 0.045), Is.EqualTo(0.59).Within(0.02), "human-style model vs Easy");
            Assert.That(RallyModel.YouRally(0.12, 0.29), Is.EqualTo(0.31).Within(0.02), "human-style model vs Hard");
        }

        [Test] public void DrawsAreSeededAndRepeatable()
        {
            var a = CareerSession.New("Tester", "ENG", 42);
            var b = CareerSession.New("Tester", "ENG", 42);
            var e = a.Enter(Tier.Challenger3);
            var f = b.Enter(Tier.Challenger3);
            Assert.That(e.Rounds[0].Players, Is.EqualTo(f.Rounds[0].Players), "same seed, same draw");
            Assert.That(e.Rounds[0].Players.Count, Is.EqualTo(32));
            Assert.That(e.Rounds[0].Players, Does.Contain(CareerSession.You));
            var field = new List<int>(e.Rounds[0].Players);
            field.Sort((x, y) => a.RankOf(x).CompareTo(a.RankOf(y)));
            Assert.That(e.Rounds[0].Players[0], Is.EqualTo(field[0]), "the top seed at the top");
            Assert.That(e.Rounds[0].Players[31], Is.EqualTo(field[1]), "the second seed at the bottom");
            Assert.That(new HashSet<int>(e.Rounds[0].Players).Count, Is.EqualTo(32), "nobody twice");
            while (a.NextOpponent() >= 0) a.SimulateYours();
            a.FinishSlot();
            var done = a.S.Last;
            Assert.That(done.Done, Is.True);
            for (int i = 1; i < done.Rounds.Count; i++) Assert.That(done.Rounds[i].Players.Count, Is.EqualTo(done.Rounds[i - 1].Players.Count / 2));
            Assert.That(done.YouReached, Is.InRange(0, 5));
            Assert.That(done.YouPoints, Is.EqualTo(Tour.Info(Tier.Challenger3).Points[done.YouReached]));
            Assert.That(a.IsRanked(CareerSession.You), Is.True, "one event and you are on the list");
        }

        [Test] public void YouStartUnrankedAndOnlyTheChallenger3sAreOpen()
        {
            var c = CareerSession.New("Tester", "ENG", 5);
            Assert.That(c.RankOf(CareerSession.You), Is.EqualTo(CareerSession.Unranked), "you start unranked");
            Assert.That(c.Ranking().Count, Is.InRange(300, Tour.RosterSize), "a year of the tour: most of the world is ranked");
            Assert.That(c.CanEnter(Tier.Challenger3), Is.True);
            foreach (var t in c.EventsNow())
                if (t != Tier.Challenger3) Assert.That(c.CanEnter(t), Is.False, t + " is closed to the unranked");
            Assert.That(c.EventsForYou(), Does.Contain(Tier.Challenger3));
        }

        [Test] public void TheRankingIsThePSAAverage()
        {
            var c = CareerSession.New("Tester", "ENG", 5);
            // three results: the total divided by 10 (the minimum divisor)
            for (int i = 0; i < 3; i++) c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1 - i, Tier = 0, Reached = 0, Points = 100 });
            Assert.That(c.AverageOf(CareerSession.You), Is.EqualTo(30.0).Within(1e-9));
            Assert.That(c.IsRanked(CareerSession.You), Is.True);
            // twelve in the window: divided by 12; an older one does not count
            for (int i = 3; i < 12; i++) c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1 - i, Tier = 0, Reached = 0, Points = 100 });
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 13, Tier = 0, Reached = 0, Points = 5000 });   // too old
            Assert.That(c.AverageOf(CareerSession.You), Is.EqualTo(100.0).Within(1e-9));
            Assert.That(c.EventsOf(CareerSession.You), Is.EqualTo(12));
            var list = c.Ranking();
            for (int i = 1; i < list.Count; i++) Assert.That(list[i].Average, Is.LessThanOrEqualTo(list[i - 1].Average));
            Debug.Log("[Career] the world after a year: #1 " + Pts(list[0].Average) + ", #32 " + Pts(list[31].Average) + ", #100 " + Pts(list[99].Average) +
                      ", #250 " + Pts(list[249].Average) + ", #" + list.Count + " " + Pts(list[list.Count - 1].Average) + "; you (100.0) #" + c.RankOf(0));
        }

        static string Pts(double v) { return v.ToString("0.0"); }

        [Test] public void NobodyPlaysTwoEventsInAMonth()
        {
            var c = CareerSession.New("Tester", "ENG", 8);
            int slot = c.S.Slot;
            c.Skip();
            var seen = new HashSet<int>();
            int events = 0;
            foreach (var r in c.S.Results) if (r.Slot == slot) { Assert.That(seen.Add(r.Player), Is.True, "player " + r.Player + " twice"); if (r.Reached == 0) events++; }
            Assert.That(events, Is.EqualTo(Tour.EventsIn(slot).Length), "every event finished, one winner each");
            Debug.Log("[Career] a month: " + events + " events, " + seen.Count + " players of " + Tour.RosterSize);
        }

        // ---------------- P3-R3: player growth ----------------

        [Test] public void WinsTitlesSeasonsAndTrainingEarnPoints()
        {
            var c = CareerSession.New("Grower", "ENG", 31);
            Assert.That(c.S.TrainingPoints, Is.EqualTo(0));
            Assert.That(c.S.Attributes.IsNeutral, Is.True, "everyone starts at 5");
            int slot = c.S.Slot;
            c.Train();
            Assert.That(c.S.Slot, Is.EqualTo(slot + 1), "the month goes by");
            Assert.That(c.S.TrainingPoints, Is.EqualTo(CareerSession.TrainingMonthPoints));
            Assert.That(c.S.LastTrainedSlot, Is.EqualTo(slot));
            int before = c.S.TrainingPoints;
            SeasonRunner.Months(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            int wins = c.S.Matches.FindAll(m => m.Won).Count;
            int titles = 0;
            foreach (var r in c.S.Results) if (r.Player == CareerSession.You && r.Reached == 0) titles += CareerSession.TitlePoints(r.Tier);
            int goals = 0;
            foreach (var g in c.S.LastGoals) if (g.Done) goals += g.Points;
            foreach (var g in c.Goals) if (g.Done) goals += g.Points;
            Assert.That(c.S.TrainingPoints - before, Is.EqualTo(wins * CareerSession.PointsPerWin + titles + CareerSession.PointsPerSeason + goals),
                "a point a win, the titles' points, two at the season's end, and the season goals'");
            Assert.That(c.S.PointsEarned, Is.EqualTo(c.S.TrainingPoints));
        }

        [Test] public void SpendingRaisesAnAttributeToTen()
        {
            var c = CareerSession.New("Spender", "ENG", 32);
            c.S.TrainingPoints = 8;
            for (int i = 0; i < 5; i++) Assert.That(c.Spend("Speed"), Is.True);
            Assert.That(c.S.Attributes.Speed, Is.EqualTo(10));
            Assert.That(c.Spend("Speed"), Is.False, "10 is the top");
            Assert.That(c.Spend("Nonsense"), Is.False);
            Assert.That(c.S.TrainingPoints, Is.EqualTo(3));
            Assert.That(c.Spend("Power"), Is.True);
            Assert.That(c.AttributeEdge, Is.EqualTo(0.015 * (c.S.Attributes.Mean - 5)).Within(1e-4), "simulated matches feel it too");
            c.S.TrainingPoints = 0;
            Assert.That(c.Spend("Control"), Is.False, "no points, no spending");
        }

        [Test] public void AVersion2CareerUpgradesInPlace()
        {
            var c = CareerSession.New("Older", "ENG", 33);
            c.S.Version = 2;
            string json = JsonUtility.ToJson(c.S).Replace("\"Attributes\":", "\"Unused\":");     // as a v2 save: no attributes
            var back = new CareerSession(JsonUtility.FromJson<CareerState>(json));
            Assert.That(back.S.Version, Is.EqualTo(CareerState.CurrentVersion));
            Assert.That(back.S.Attributes, Is.Not.Null);
            Assert.That(back.S.Attributes.IsNeutral, Is.True);
            Assert.That(back.S.Roster.Count, Is.EqualTo(c.S.Roster.Count), "the career is kept");
        }

        // ---------------- P3-R4: goals and sponsors ----------------

        [Test] public void EachSeasonHasThreeGoalsFromWhereYouStand()
        {
            var c = CareerSession.New("Goals", "ENG", 41);
            Assert.That(c.Goals.Count, Is.EqualTo(3));
            Assert.That(c.Goals[0].Kind, Is.EqualTo("rank"), "the rank goal always");
            Assert.That(c.Goals[0].Target, Is.EqualTo(400), "unranked: get into the top 400");
            Assert.That(c.Goals.TrueForAll(g => g.Points > 0 && g.Money > 0 && !string.IsNullOrEmpty(g.Text)), Is.True);
            var again = CareerSession.New("Goals", "ENG", 41);
            Assert.That(again.Goals.ConvertAll(g => g.Text), Is.EqualTo(c.Goals.ConvertAll(g => g.Text)), "the same seed, the same goals");
            SeasonRunner.Months(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.S.LastGoals.Count, Is.EqualTo(3), "last season's, settled for the review");
            Assert.That(c.S.LastGoals.TrueForAll(g => g.Done || g.Failed), Is.True, "every goal settled");
            Assert.That(c.S.LastGoals[0].Done, Is.True, "a strong player gets into the top 400: " + c.RankOf(0));
            Assert.That(c.Goals.Count, Is.EqualTo(3)); Assert.That(c.Goals[0].Target, Is.LessThan(400), "new goals from the new ranking");
            Assert.That(c.S.GoalsDone, Is.EqualTo(c.S.LastGoals.FindAll(g => g.Done).Count));
        }

        [Test] public void SponsorsPayWhileYouKeepTheClause()
        {
            var c = CareerSession.New("Sponsored", "ENG", 42);
            Assert.That(c.OffersWaiting, Is.False, "nobody sponsors the unranked");
            SeasonRunner.Months(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.OffersWaiting, Is.True, "ranked now: two offers");
            Assert.That(c.S.Offers.Count, Is.EqualTo(2));
            Assert.That(c.S.Offers[1].Retainer, Is.GreaterThan(c.S.Offers[0].Retainer), "the bolder deal pays more");
            Assert.That(c.S.Offers[1].Clause, Is.LessThanOrEqualTo(c.S.Offers[0].Clause), "and asks more");
            var d = c.Sign(0);
            Assert.That(c.S.Sponsor, Is.SameAs(d)); Assert.That(c.OffersWaiting, Is.False);
            int earnings = c.S.Earnings, titles = c.S.Titles;
            d.Clause = 480;                                            // a clause it will keep
            SeasonRunner.Months(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            int bonus = (c.S.Titles - titles) * d.TitleBonus;
            Assert.That(c.S.SponsorPaid, Is.EqualTo(d.Retainer + bonus), "the retainer and a bonus a title");
            Assert.That(c.S.SponsorNews, Does.Contain("pays"));
            Assert.That(c.S.Sponsor, Is.Not.Null, "kept on");
            c.S.Sponsor.Clause = 1;                                   // one it can't
            SeasonRunner.Months(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.S.Sponsor, Is.Null, "dropped outside the clause");
            Assert.That(c.S.SponsorNews, Does.Contain("drops"));
            var json = JsonUtility.ToJson(c.S);
            Assert.That(new CareerSession(JsonUtility.FromJson<CareerState>(json)).S.Sponsor, Is.Null, "an empty sponsor reads back as none");
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
            Assert.That(top, Is.GreaterThanOrEqualTo((int)Tier.Copper), "a strong player reaches the World Tour inside two seasons");
            Assert.That(strong.S.Rating, Is.InRange(0.30, 0.60), "the rating finds the true strength");

            var mid = CareerSession.New("Mid", "ENG", 12);
            int midTop = HighestTier(mid, 2, 0.09);
            Debug.Log("[Career] human-style (0.09): rank " + mid.RankOf(0) + " after 2 seasons, highest tier " + (Tier)midTop + ", rating " + mid.S.Rating.ToString("0.00"));

            var weak = CareerSession.New("Weak", "ENG", 13);
            int weakTop = HighestTier(weak, 2, -0.15);
            Debug.Log("[Career] weak (-0.15): rank " + weak.RankOf(0) + " after 2 seasons, highest tier " + (Tier)weakTop);
            Assert.That(weakTop, Is.LessThanOrEqualTo((int)Tier.Challenger9), "a weak player stays in the small Challengers");
            Assert.That(strong.S.Seasons.Count, Is.EqualTo(2), "two season summaries");
        }

        [Test] public void ASaveMidEventPlaysOutTheSame()
        {
            var c = CareerSession.New("Saver", "SCO", 21);
            c.Enter(Tier.Challenger3);
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
