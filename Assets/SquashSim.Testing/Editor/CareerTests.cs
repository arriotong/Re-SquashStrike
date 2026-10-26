// ===========================================================
// Assets/SquashSim.Testing/Editor/CareerTests.cs
// P2-M4: the Career, headless (the PSA system since 2026-10-24).
//   - the world: 480 pros, strength falling with the place, no repeats
//   - draws: seeds 1 and 2 at the ends, every round halves, the same
//     seed gives the same draw
//   - the rally model: even at equal strength, legal scorelines (PAR
//     and English), the calibration it was fitted to
//   - rankings: you start unranked; the PSA average (52 weeks, the best
//     counting events, divided by at least 10); the unranked start at
//     their home club's open; nobody plays two events in a week
//   - P7 phase 3: the real calendar; byes for the top seeds; entry by
//     ranking, wildcards and qualifying; trips cost money; the Tour Finals
//     are the Road's top 8; nationality shapes the world
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
            // P6: the top 50 are the stars (strength from their rating: the No. 1 plays at 1.0); the rest fall away below them
            Assert.That(Mean(1, 32), Is.GreaterThan(Mean(33, 140)));
            Assert.That(Mean(33, 140), Is.GreaterThan(Mean(141, 300)));
            Assert.That(Mean(141, 300), Is.GreaterThan(Mean(301, 480)));
            Assert.That(Mean(401, 480), Is.LessThan(0.03));
            Assert.That(pros[0].Strength, Is.EqualTo(1.0));
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
            var e = a.Enter(Tour.HomeIndex);
            var f = b.Enter(Tour.HomeIndex);
            Assert.That(e.Rounds[0].Players, Is.EqualTo(f.Rounds[0].Players), "same seed, same draw");
            Assert.That(e.Rounds[0].Players.Count, Is.EqualTo(16));
            Assert.That(e.Rounds[0].Players, Does.Contain(CareerSession.You));
            var field = new List<int>(e.Rounds[0].Players);
            field.Sort((x, y) => a.RankOf(x).CompareTo(a.RankOf(y)));
            Assert.That(e.Rounds[0].Players[0], Is.EqualTo(field[0]), "the top seed at the top");
            Assert.That(e.Rounds[0].Players[15], Is.EqualTo(field[1]), "the second seed at the bottom");
            Assert.That(new HashSet<int>(e.Rounds[0].Players).Count, Is.EqualTo(16), "nobody twice");
            while (a.NextOpponent() >= 0) a.SimulateYours();
            a.FinishSlot();
            var done = a.S.Last;
            Assert.That(done.Done, Is.True);
            for (int i = 1; i < done.Rounds.Count; i++) Assert.That(done.Rounds[i].Players.Count, Is.EqualTo(done.Rounds[i - 1].Players.Count / 2));
            Assert.That(done.YouReached, Is.InRange(0, 4));
            Assert.That(done.YouPoints, Is.EqualTo(Tour.Club.Points[done.YouReached]));
            Assert.That(a.IsRanked(CareerSession.You), Is.True, "one event and you are on the list");
        }

        [Test] public void YouStartUnrankedAtHomeAndTheClubOpenIsTheWayIn()
        {
            var c = CareerSession.New("Tester", "ENG", 5);
            Assert.That(c.RankOf(CareerSession.You), Is.EqualTo(CareerSession.Unranked), "you start unranked");
            Assert.That(c.Ranking().Count, Is.InRange(300, Tour.RosterSize), "a year of the tour: most of the world is ranked");
            Assert.That(c.S.City, Is.EqualTo("London")); Assert.That(c.S.Cash, Is.EqualTo(Tour.StartCash));
            for (int w = 0; w < 12; w++)
            {
                foreach (var o in c.Options())
                {
                    if (o.Home) { Assert.That(o.Open, Is.True, "the club's open is always on"); Assert.That(o.Cost, Is.EqualTo(0), "at home: free"); continue; }
                    if (o.Open) Assert.That(o.Via, Is.EqualTo(CareerSession.Via.Wildcard), o.Name + ": only a wildcard gets the unranked in");
                    if (World.CityOf(o.City).Country != "ENG") Assert.That(o.Open, Is.False, o.Name + " is closed to the unranked");
                }
                c.Skip();
            }
            var list = c.EventsForYou();
            Assert.That(list[list.Count - 1].Home, Is.True, "the home club's open is always offered");
            Assert.That(list[list.Count - 1].Name, Is.EqualTo("London Club Open"));
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
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1 - Tour.RankingWindow, Tier = 0, Reached = 0, Points = 5000 });   // too old
            Assert.That(c.AverageOf(CareerSession.You), Is.EqualTo(100.0).Within(1e-9));
            Assert.That(c.EventsOf(CareerSession.You), Is.EqualTo(12));
            // 20 played: the best 14 count (7.4), averaged over 14 - eight poor ones more don't pull you down
            for (int i = 12; i < 20; i++) c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1 - i, Tier = 0, Reached = 5, Points = 6 });
            Assert.That(Tour.Counting(20), Is.EqualTo(14)); Assert.That(Tour.Counting(15), Is.EqualTo(11)); Assert.That(Tour.Counting(26), Is.EqualTo(22));
            Assert.That(c.EventsOf(CareerSession.You), Is.EqualTo(20));
            Assert.That(c.AverageOf(CareerSession.You), Is.EqualTo((12 * 100 + 2 * 6) / 14.0).Within(1e-9));
            // the Tour Finals: on top, not an event played
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1, Tier = (int)Tier.WorldTourFinals, Reached = 0, Points = 1000 });
            Assert.That(c.EventsOf(CareerSession.You), Is.EqualTo(20));
            Assert.That(c.AverageOf(CareerSession.You), Is.EqualTo((12 * 100 + 2 * 6 + 1000) / 14.0).Within(1e-9));
            var list = c.Ranking();
            for (int i = 1; i < list.Count; i++) Assert.That(list[i].Average, Is.LessThanOrEqualTo(list[i - 1].Average));
            Debug.Log("[Career] the world after a year: #1 " + Pts(list[0].Average) + ", #32 " + Pts(list[31].Average) + ", #100 " + Pts(list[99].Average) +
                      ", #250 " + Pts(list[249].Average) + ", #" + list.Count + " " + Pts(list[list.Count - 1].Average) + "; you (100.0) #" + c.RankOf(0));
        }

        static string Pts(double v) { return v.ToString("0.0"); }

        [Test] public void NobodyPlaysTwoEventsInAWeek()
        {
            var c = CareerSession.New("Tester", "ENG", 8);
            while (Tour.Week(c.S.Slot).Count < 4) c.Skip();
            int slot = c.S.Slot;
            c.Skip();
            var seen = new HashSet<int>();
            int events = 0;
            foreach (var r in c.S.Results) if (r.Slot == slot) { Assert.That(seen.Add(r.Player), Is.True, "player " + r.Player + " twice"); if (r.Reached == 0) events++; }
            Assert.That(events, Is.EqualTo(Tour.Week(slot).Count), "every event finished, one winner each");
            Debug.Log("[Career] a week: " + events + " events, " + seen.Count + " players of " + Tour.RosterSize);
        }

        // ---------------- P3-R3: player growth ----------------

        // P7 phase 2: levels from career XP, a stat point a level, four stats from 1 to 10

        [Test] public void MatchesMakeLevelsAndEachLevelIsAPoint()
        {
            var c = CareerSession.New("Grower", "ENG", 31);
            Assert.That(c.S.TrainingPoints, Is.EqualTo(0));
            Assert.That(c.S.Xp, Is.EqualTo(0)); Assert.That(c.Level, Is.EqualTo(1));
            Assert.That(c.S.Attributes.Speed, Is.EqualTo(CareerSession.StartStat), "you start weaker than a Quick Match player");
            Assert.That(c.S.Attributes.Mean, Is.EqualTo(CareerSession.StartStat));
            int slot = c.S.Slot;
            c.Train();
            Assert.That(c.S.Slot, Is.EqualTo(slot + 1), "the week goes by");
            Assert.That(c.S.Xp, Is.EqualTo(CareerSession.TrainingWeekXp));
            Assert.That(c.S.LastTrainedSlot, Is.EqualTo(slot));
            SeasonRunner.Weeks(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.Level, Is.GreaterThan(5), "a season of matches: levels");
            Assert.That(c.S.PointsEarned, Is.EqualTo(System.Math.Min(c.Level - 1, CareerSession.MaxPoints)), "a point a level");
            Assert.That(CareerSession.MatchXp((int)Tier.Diamond, 40, 25, true, false), Is.GreaterThan(CareerSession.MatchXp((int)Tier.Challenger3, 40, 25, true, false)), "the bigger stage teaches more");
            Assert.That(CareerSession.MatchXp((int)Tier.Copper, 40, 25, true, true), Is.LessThan(CareerSession.MatchXp((int)Tier.Copper, 40, 25, true, false)), "simulated: less");
            Assert.That(CareerSession.XpNeed(10), Is.GreaterThan(CareerSession.XpNeed(2)), "each level a little further");
        }

        [Test] public void APointRaisesAStatAndTheLevelsStopAtTheTop()
        {
            var c = CareerSession.New("Spender", "ENG", 32);
            c.S.TrainingPoints = 9;
            for (int i = 0; i < 9; i++) Assert.That(c.Spend("Speed"), Is.True, "a point a step: " + i);
            Assert.That(c.S.Attributes.Speed, Is.EqualTo(10));
            Assert.That(c.S.TrainingPoints, Is.EqualTo(0));
            Assert.That(c.Spend("Speed"), Is.False, "10 is the top");
            Assert.That(c.Spend("Nonsense"), Is.False);
            Assert.That(CareerSession.CostToRaise(1), Is.EqualTo(1)); Assert.That(CareerSession.CostToRaise(9), Is.EqualTo(1));
            Assert.That(c.AttributeEdge, Is.EqualTo(0.025 * (c.S.Attributes.Mean - 5)).Within(1e-4), "simulated matches feel it too");
            Assert.That(CareerSession.DisplayName("Control"), Is.EqualTo("Accuracy"));
            // past the points that take every stat to the top, levels give no more
            c.S.PointsEarned = CareerSession.MaxPoints - 1;
            c.GainXp(CareerSession.XpFor(c.Level + 5) - c.S.Xp);
            Assert.That(c.S.PointsEarned, Is.EqualTo(CareerSession.MaxPoints));
            Assert.That(CareerSession.MaxPoints, Is.EqualTo(36), "1 to 10 in four stats");
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
            SeasonRunner.Weeks(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
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
            SeasonRunner.Weeks(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.OffersWaiting, Is.True, "ranked now: two offers");
            Assert.That(c.S.Offers.Count, Is.EqualTo(2));
            Assert.That(c.S.Offers[1].Retainer, Is.GreaterThan(c.S.Offers[0].Retainer), "the bolder deal pays more");
            Assert.That(c.S.Offers[1].Clause, Is.LessThanOrEqualTo(c.S.Offers[0].Clause), "and asks more");
            var d = c.Sign(0);
            Assert.That(c.S.Sponsor, Is.SameAs(d)); Assert.That(c.OffersWaiting, Is.False);
            int earnings = c.S.Earnings, titles = c.S.Titles;
            d.Clause = 480;                                            // a clause it will keep
            SeasonRunner.Weeks(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            int bonus = (c.S.Titles - titles) * d.TitleBonus;
            Assert.That(c.S.SponsorPaid, Is.EqualTo(d.Retainer + bonus), "the retainer and a bonus a title");
            Assert.That(c.S.SponsorNews, Does.Contain("pays"));
            Assert.That(c.S.Sponsor, Is.Not.Null, "kept on");
            c.S.Sponsor.Clause = 1;                                   // one it can't
            SeasonRunner.Weeks(c, Tour.SlotsPerSeason, SeasonRunner.Model(0.45));
            Assert.That(c.S.Sponsor, Is.Null, "dropped outside the clause");
            Assert.That(c.S.SponsorNews, Does.Contain("drops"));
            var json = JsonUtility.ToJson(c.S);
            Assert.That(new CareerSession(JsonUtility.FromJson<CareerState>(json)).S.Sponsor, Is.Null, "an empty sponsor reads back as none");
        }

        /// <summary>The biggest tier you played your way into (not by a wildcard, not your club's open).</summary>
        static int HighestTier(CareerSession c, int seasons, double strength)
        {
            int best = 0;
            SeasonRunner.Weeks(c, seasons * Tour.SlotsPerSeason, SeasonRunner.Model(strength), (cc, e) =>
            {
                if (e != null && e.YouIn && !e.Home && e.Via != (int)CareerSession.Via.Wildcard) best = System.Math.Max(best, e.Tier);
            });
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
            // P7: a Challenger fills from its own region, so its cut-off can be deep (#250+, as the real ones are): the weak
            // play some - but never the World Tour, and stay far down the list
            Assert.That(weakTop, Is.LessThan((int)Tier.Copper), "a weak player never reaches the World Tour");
            Assert.That(weak.RankOf(0), Is.GreaterThan(150), "and stays far down");
            Assert.That(strong.S.Seasons.Count, Is.EqualTo(2), "two season summaries");
        }

        [Test] public void ASaveMidEventPlaysOutTheSame()
        {
            var c = CareerSession.New("Saver", "SCO", 21);
            c.Enter(Tour.HomeIndex);
            c.SimulateYours();
            string json = JsonUtility.ToJson(c.S);
            var copy = new CareerSession(JsonUtility.FromJson<CareerState>(json));
            Assert.That(copy.NextOpponent(), Is.EqualTo(c.NextOpponent()));
            SeasonRunner.Weeks(c, 0, null);
            while (c.NextOpponent() >= 0) c.SimulateYours();
            while (copy.NextOpponent() >= 0) copy.SimulateYours();
            c.FinishSlot(); copy.FinishSlot();
            Assert.That(JsonUtility.ToJson(copy.S), Is.EqualTo(JsonUtility.ToJson(c.S)), "identical after the event");
            var empty = new CareerSession(JsonUtility.FromJson<CareerState>(JsonUtility.ToJson(c.S)));
            Assert.That(empty.S.Current, Is.Null, "no event in progress after a load between events");
        }

        // ---------------- P7 phase 3: the world tour ----------------

        [Test] public void TheCalendarIsTheRealTour()
        {
            Assert.That(Tour.Calendar.Length, Is.EqualTo(116), "39 graded events and 77 Challengers");
            int graded = 0;
            foreach (var e in Tour.Calendar) if (Tour.Info(e.Tier).WorldTour) graded++;
            Assert.That(graded, Is.EqualTo(39));
            Assert.That(Tour.CountOf(Tier.WorldChampionship), Is.EqualTo(1));
            Assert.That(Tour.CountOf(Tier.WorldTourFinals), Is.EqualTo(1));
            Assert.That(System.Array.Exists(Tour.Calendar, e => e.Name == "British Open" && e.Tier == Tier.Diamond), Is.True, "the British Open is a Diamond");
            foreach (var e in Tour.Calendar)
            {
                Assert.That(World.CityOf(e.City), Is.Not.Null, e.Name + ": a city on the map (" + e.City + ")");
                Assert.That(e.Week, Is.InRange(0, Tour.OffSeasonFrom - 1), e.Name + ": before the off-season");
            }
            Assert.That(Tour.Week(Tour.SlotsPerSeason - 1).Count, Is.EqualTo(0), "the off-season");
            Assert.That(Tour.HeadlineOf(46), Is.EqualTo(Tier.WorldTourFinals));
            foreach (var n in Roster.Nations) Assert.That(World.CityOf(World.HomeOf(n)), Is.Not.Null, n + ": a home city");
        }

        [Test] public void TheTopSeedsGetByes()
        {
            var c = CareerSession.New("Seed", "ENG", 51);
            while (!System.Array.Exists(c.EventsNow(), t => t == Tier.Challenger3)) c.Skip();
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1, Tier = (int)Tier.Diamond, Reached = 0, Points = 40000 });   // the world No. 1
            Assert.That(c.RankOf(CareerSession.You), Is.EqualTo(1));
            var e = c.Enter(Tier.Challenger3);
            var first = e.Rounds[0].Players;
            Assert.That(first.Count, Is.EqualTo(32), "a 32-draw");
            Assert.That(first.FindAll(id => id == CareerSession.Bye).Count, Is.EqualTo(8), "24 players: 8 byes");
            Assert.That(first[0], Is.EqualTo(CareerSession.You), "the top seed at the top");
            Assert.That(first[1], Is.EqualTo(CareerSession.Bye), "with a bye");
            Assert.That(c.PlayersLeft, Is.EqualTo(16), "straight into the round of 16");
            for (int m = 0; m < 16; m++) Assert.That(first[2 * m] == CareerSession.Bye && first[2 * m + 1] == CareerSession.Bye, Is.False, "never two byes");
            while (c.NextOpponent() >= 0) c.SimulateYours();
            c.FinishSlot();
            Assert.That(c.S.Last.YouReached, Is.InRange(0, 4), "a seed can't go out in the round of 32");
            foreach (var r in c.S.Results) Assert.That(r.Player, Is.Not.EqualTo(CareerSession.Bye));
        }

        [Test] public void WildcardsAreForHomeEventsTwoAYear()
        {
            var c = CareerSession.New("Local", "ENG", 52);
            int used = 0;
            for (int w = 0; w < Tour.SlotsPerSeason && used < 3; w++)
            {
                foreach (var o in c.Options())
                {
                    if (o.Via != CareerSession.Via.Wildcard || !o.Open) continue;
                    Assert.That(World.CityOf(o.City).Country, Is.EqualTo("ENG"), "a wildcard at home only");
                    Assert.That(o.Tier, Is.LessThan(Tier.Copper), "the unranked aren't their country's top 5");
                    c.Enter(o.Index);
                    Assert.That(c.S.Current.Via, Is.EqualTo((int)CareerSession.Via.Wildcard));
                    used++;
                    while (c.NextOpponent() >= 0) c.RecordYours(false, "0-11 0-11");
                    break;
                }
                c.FinishSlot();
                if (used == 2) { Assert.That(c.WildcardsLeft, Is.EqualTo(0)); break; }
            }
            Assert.That(used, Is.EqualTo(2), "two English Challengers in a season, a wildcard each");
            for (int w = 0; w < 10; w++)
            {
                foreach (var o in c.Options()) Assert.That(o.Via, Is.Not.EqualTo(CareerSession.Via.Wildcard), "no more wildcards this year");
                c.Skip();
            }
        }

        [Test] public void BelowADiamondsCutOffYouQualify()
        {
            var c = CareerSession.New("Qualifier", "ENG", 53);
            while (!System.Array.Exists(c.EventsNow(), t => t == Tier.Diamond)) c.Skip();
            int index = Tour.IndexOf(c.S.Slot, Tier.Diamond);
            int lastIn = c.OptionAt(index).LastIn;
            // just below the cut-off: a few places under the last direct entrant
            var target = c.Ranking()[lastIn + 6];
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1, Tier = (int)Tier.Gold, Reached = 3, Points = (int)System.Math.Ceiling(target.Average * Tour.MinDivisor) + 1 });
            var o = c.OptionAt(index);
            Assert.That(c.RankOf(CareerSession.You), Is.GreaterThan(o.LastIn), "below the cut-off");
            Assert.That(o.Via, Is.EqualTo(CareerSession.Via.Qualifying), o.Why);
            var q = c.Enter(index);
            Assert.That(q.Qualifying, Is.True);
            Assert.That(q.Rounds[0].Players.Count, Is.EqualTo(8), "your section of the 32");
            int wins = 0;
            while (c.S.Current.Qualifying) { c.RecordYours(true, "11-0 11-0"); wins++; }
            Assert.That(wins, Is.EqualTo(3), "three wins to qualify");
            var main = c.S.Current;
            Assert.That(main.Qualifying, Is.False); Assert.That(main.YouIn, Is.True);
            Assert.That(main.Rounds[0].Players.Count, Is.EqualTo(64), "the main draw");
            Assert.That(main.Rounds[0].Players, Does.Contain(CareerSession.You));
            Assert.That(main.Via, Is.EqualTo((int)CareerSession.Via.Qualifying));
            Assert.That(c.S.Matches.FindAll(m => m.Qual).Count, Is.EqualTo(3));
            while (c.NextOpponent() >= 0) c.SimulateYours();
            c.FinishSlot();
            Assert.That(c.S.Last.YouPoints, Is.GreaterThan(0), "the main draw's points");
        }

        [Test] public void TripsCostMoneyAndPrizesPay()
        {
            var c = CareerSession.New("Traveller", "ENG", 54);
            c.S.Results.Add(new ResultRec { Player = 0, Slot = c.S.Slot - 1, Tier = (int)Tier.Silver, Reached = 0, Points = 1350 });
            CareerSession.Option far = null;
            for (int w = 0; w < 20 && far == null; w++)
            {
                foreach (var o in c.Options()) if (o.Open && !o.Home && World.CityOf(o.City).Country != "ENG") { far = o; break; }
                if (far == null) c.Skip();
            }
            Assert.That(far, Is.Not.Null, "somewhere abroad is open to a ranked player");
            Assert.That(far.Cost, Is.EqualTo(World.Flight("London", far.City) + World.CityOf(far.City).Hotel * Tour.Nights(far.Tier)));
            int cash = c.S.Cash, earned = c.S.Earnings;
            c.Enter(far.Index);
            Assert.That(c.S.Cash, Is.EqualTo(cash - far.Cost)); Assert.That(c.S.City, Is.EqualTo(far.City));
            Assert.That(c.S.Spent, Is.EqualTo(far.Cost));
            while (c.NextOpponent() >= 0) c.SimulateYours();
            c.FinishSlot();
            Assert.That(c.S.Last.YouPrize, Is.GreaterThan(0), "every round pays");
            Assert.That(c.S.Cash, Is.EqualTo(cash - far.Cost + (c.S.Earnings - earned)), "prize money (and any goal's) to your cash");
            // broke and abroad: the federation flies you home to the club's open
            c.S.Cash = 0;
            var home = c.OptionAt(Tour.HomeIndex);
            Assert.That(home.Open, Is.True); Assert.That(home.Cost, Is.EqualTo(0));
            foreach (var o in c.Options()) if (!o.Home) Assert.That(o.Open, Is.False, o.Name + ": no money, no trip");
            c.Enter(Tour.HomeIndex);
            Assert.That(c.S.City, Is.EqualTo("London")); Assert.That(c.S.Cash, Is.EqualTo(0));
        }

        [Test] public void TheTourFinalsAreTheRoadsTopEight()
        {
            var c = CareerSession.New("Watcher", "ENG", 55);
            while (Tour.HeadlineOf(c.S.Slot) != Tier.WorldTourFinals) c.Skip();
            var road = c.RoadTo().GetRange(0, 8);
            int index = Tour.IndexOf(c.S.Slot, Tier.WorldTourFinals);
            Assert.That(c.OptionAt(index).Open, Is.False, "you're not on the Road's top 8");
            int slot = c.S.Slot;
            c.Skip();
            var played = new List<int>();
            foreach (var r in c.S.Results) if (r.Slot == slot && r.Tier == (int)Tier.WorldTourFinals) played.Add(r.Player);
            played.Sort(); road.Sort();
            Assert.That(played, Is.EqualTo(road), "the Road to the Tour Finals' top 8");
        }

        [Test] public void NationalityShapesTheWorld()
        {
            var pros = Roster.Generate(9);
            var count = new Dictionary<string, int>();
            var sum = new Dictionary<string, double>();
            for (int i = Stars.All().Count; i < pros.Count; i++)
            {
                var p = pros[i];
                count.TryGetValue(p.Nat, out int n); count[p.Nat] = n + 1;
                sum.TryGetValue(p.Nat, out double t); sum[p.Nat] = t + p.Strength;
                Assert.That(World.CountryOf(p.Nat), Is.Not.Null, p.Nat);
            }
            string most = null;
            foreach (var kv in count) if (most == null || kv.Value > count[most]) most = kv.Key;
            Assert.That(most, Is.EqualTo("EGY"), "Egypt has the most pros");
            double Mean(params string[] nats) { double s2 = 0; int n2 = 0; foreach (var k in nats) if (count.ContainsKey(k)) { s2 += sum[k]; n2 += count[k]; } return s2 / n2; }
            Assert.That(Mean("EGY"), Is.GreaterThan(Mean("AUT", "ITA", "POL", "KEN", "NGR")), "and the better ones");
            for (int i = 1; i < pros.Count; i++) if (i != Stars.All().Count) Assert.That(pros[i].Strength, Is.LessThanOrEqualTo(pros[i - 1].Strength), "still best first");
            for (int i = 0; i < pros.Count; i++) Assert.That(pros[i].Id, Is.EqualTo(i + 1));
            Assert.That(CareerSession.New("Pharaoh", "EGY", 3).S.City, Is.EqualTo("Cairo"), "you start at home");
        }
    }
}
