// ===========================================================
// Assets/SquashSim.Testing/Editor/FinalsTests.cs
// P7 phase 6: the Tour Finals' round robin and the trophy cabinet.
//   - the Road's top 8 in two snake-seeded groups of four; three match
//     days; a lost group match isn't out; the top two of each group go
//     through (A1 v B2, B1 v A2); the final is one game longer
//   - points: 150 a group win, 150 more unbeaten, then 200 / 550 / 1,000
//   - out in the group: no more matches, the week can end
//   - the table: wins, then head to head
//   - every title goes in the cabinet: the event, the final's opponent
//     and score; the pros' Finals play out without you too
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Career;

namespace SquashSim.Tests
{
    public class FinalsTests
    {
        const int You = CareerSession.You;

        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }

        /// <summary>A career at the Finals' week, on the Road's top 8.</summary>
        static CareerSession AtTheFinals(uint seed)
        {
            var c = CareerSession.New("Finalist", "ENG", seed);
            while (Tour.HeadlineOf(c.S.Slot) != Tier.WorldTourFinals) c.Skip();
            c.S.Results.Add(new ResultRec { Player = You, Slot = c.S.Slot - 1, Tier = (int)Tier.Diamond, Reached = 0, Points = 40000 });   // surely on the Road's top 8
            c.S.Cash = 100000;
            return c;
        }

        [Test] public void TwoGroupsThreeDaysThenSemisAndALongerFinal()
        {
            var c = AtTheFinals(71);
            Assert.That(c.RoadTo().IndexOf(You), Is.InRange(0, 7), "on the Road's top 8");
            var e = c.Enter(Tier.WorldTourFinals);
            Assert.That(e.RoundRobin, Is.True);
            Assert.That(e.Seeds.Count, Is.EqualTo(8));
            var groups = CareerSession.Groups(e);
            Assert.That(groups[0].Count, Is.EqualTo(4)); Assert.That(groups[1].Count, Is.EqualTo(4));
            Assert.That(groups[0][0], Is.EqualTo(e.Seeds[0])); Assert.That(groups[1][0], Is.EqualTo(e.Seeds[1]));
            Assert.That(groups[0], Does.Contain(e.Seeds[3])); Assert.That(groups[0], Does.Contain(e.Seeds[4])); Assert.That(groups[0], Does.Contain(e.Seeds[7]));
            var mine = groups[0].Contains(You) ? groups[0] : groups[1];
            var met = new HashSet<int>();
            for (int day = 1; day <= 3; day++)
            {
                Assert.That(c.PlayersLeft, Is.EqualTo(-day));
                Assert.That(Tour.RoundName(c.PlayersLeft), Is.EqualTo("Group match " + day));
                int opp = c.NextOpponent();
                Assert.That(mine, Does.Contain(opp), "a group opponent");
                Assert.That(met.Add(opp), Is.True, "a different one each day");
                Assert.That(c.GamesToWinNow, Is.EqualTo(c.S.GamesToWin));
                c.RecordYours(true, "11-5 11-6");
            }
            Assert.That(met.Count, Is.EqualTo(3), "everyone in the group");
            Assert.That(e.Rounds.Count, Is.EqualTo(4), "the semis are drawn");
            var semis = e.Rounds[3].Players;
            var a = CareerSession.Table(e, 0); var b = CareerSession.Table(e, 1);
            Assert.That(semis, Is.EqualTo(new List<int> { a[0].Id, b[1].Id, b[0].Id, a[1].Id }), "A1 v B2, B1 v A2");
            Assert.That(semis[0] == You || semis[2] == You, Is.True, "unbeaten: top of the group");
            Assert.That(c.PlayersLeft, Is.EqualTo(4));
            c.RecordYours(true, "11-7 11-9");
            Assert.That(c.PlayersLeft, Is.EqualTo(2), "the final");
            Assert.That(c.GamesToWinNow, Is.EqualTo(System.Math.Min(3, c.S.GamesToWin + 1)), "one game longer");
            c.RecordYours(true, "11-7 9-11 11-9 11-4");
            Assert.That(e.Done, Is.True);
            int cash = c.S.Cash;
            c.FinishSlot();
            Assert.That(c.S.Last.YouReached, Is.EqualTo(0));
            Assert.That(c.S.Last.YouPoints, Is.EqualTo(1000 + 3 * CareerSession.GroupWinPoints + CareerSession.UnbeatenPoints), "the title, three group wins, unbeaten");
            Assert.That(c.S.Cash, Is.GreaterThanOrEqualTo(cash + Tour.Info(Tier.WorldTourFinals).Prize[0]));
            var t = c.Trophies[c.Trophies.Count - 1];
            Assert.That(t.Event, Is.EqualTo("Tour Finals")); Assert.That(t.Tier, Is.EqualTo((int)Tier.WorldTourFinals));
            Assert.That(t.Score, Is.EqualTo("11-7 9-11 11-9 11-4"), "the final's score, your side first");
            Assert.That(t.Opp, Is.Not.EqualTo(You)); Assert.That(t.OppName, Is.EqualTo(c.NameOf(t.Opp)));
            Assert.That(CareerSession.ReachedLong(c.S.Last, 3), Is.EqualTo("Group stage"));
        }

        [Test] public void OutInTheGroupEndsYourWeek()
        {
            var c = AtTheFinals(72);
            var e = c.Enter(Tier.WorldTourFinals);
            for (int day = 0; day < 3; day++)
            {
                c.RecordYours(false, "3-11 4-11");
                if (day < 2) Assert.That(e.YouOut, Is.False, "a lost group match isn't out");
            }
            Assert.That(e.YouOut, Is.True, "0-3: out");
            Assert.That(c.NextOpponent(), Is.EqualTo(-1));
            Assert.That(c.CanFinishSlot, Is.True);
            c.FinishSlot();
            Assert.That(c.S.Last.YouReached, Is.EqualTo(3));
            Assert.That(c.S.Last.YouPoints, Is.EqualTo(0), "no group wins: no points");
            Assert.That(c.S.Last.YouPrize, Is.GreaterThan(0), "the Finals pay everyone");
            Assert.That(c.S.Last.Done, Is.True, "played out without you");
        }

        [Test] public void TheFinalsGoRoundTheWorld()
        {
            int week = System.Array.Find(Tour.Calendar, e => e.Tier == Tier.WorldTourFinals).Week;
            var seen = new List<string>();
            for (int season = 1; season <= Tour.FinalsCities.Length + 1; season++)
            {
                var ev = Tour.Week(season * Tour.SlotsPerSeason + week).Find(e => e.Tier == Tier.WorldTourFinals);
                Assert.That(ev.City, Is.EqualTo(Tour.FinalsCity(season)));
                Assert.That(World.CityOf(ev.City), Is.Not.Null, ev.City + " is on the map");
                Assert.That(Tour.Week(season * Tour.SlotsPerSeason + week), Is.SameAs(Tour.Week(season * Tour.SlotsPerSeason + week)), "the same week each time");
                seen.Add(ev.City);
            }
            Assert.That(seen[0], Is.EqualTo("Cairo")); Assert.That(seen[1], Is.EqualTo("Paris"));
            Assert.That(seen[Tour.FinalsCities.Length], Is.EqualTo("Cairo"), "round again");
            Assert.That(new HashSet<string>(seen).Count, Is.EqualTo(Tour.FinalsCities.Length), "a different host each season");
            // a new career's first Finals (season 1): Cairo
            var c = AtTheFinals(74);
            Assert.That(c.Enter(Tier.WorldTourFinals).City, Is.EqualTo("Cairo"));
        }

        [Test] public void TheTableIsWinsThenHeadToHead()
        {
            // group A: 1, 4, 5, 8 (seeds); 1 beats 4 and 5, loses to 8; 8 beats 1 and 4, loses to 5; ...
            var e = new EventState { RoundRobin = true };
            e.Seeds.AddRange(new[] { 101, 102, 103, 104, 105, 106, 107, 108 });
            var g = CareerSession.Groups(e)[0];
            Assert.That(g, Is.EqualTo(new List<int> { 101, 104, 105, 108 }));
            void Day(params (int a, int b, int w)[] ms)
            {
                var r = new RoundRec();
                foreach (var m in ms) { r.Players.Add(m.a); r.Players.Add(m.b); r.Winners.Add(m.w); r.Scores.Add(m.w == m.a ? "11-5 11-5" : "5-11 5-11"); }
                e.Rounds.Add(r);
            }
            Day((101, 108, 108), (104, 105, 105));
            Day((101, 105, 101), (108, 104, 108));
            Day((101, 104, 101), (105, 108, 105));
            var t = CareerSession.Table(e, 0);
            // 101: 2 wins, 108: 2 wins, 105: 2 wins, 104: 0 - a three-way tie on wins: games, then seed
            Assert.That(t[3].Id, Is.EqualTo(104));
            Assert.That(t[0].Wins, Is.EqualTo(2));
            // two level on wins: the head to head decides (108 beat 101; 104 beat 105)
            var e2 = new EventState { RoundRobin = true };
            e2.Seeds.AddRange(new[] { 101, 102, 103, 104, 105, 106, 107, 108 });
            e = e2;
            Day((101, 108, 108), (104, 105, 104));
            Day((101, 105, 101), (108, 104, 108));
            Day((101, 104, 101), (105, 108, 105));
            t = CareerSession.Table(e, 0);
            Assert.That(t.ConvertAll(x => x.Id), Is.EqualTo(new List<int> { 108, 101, 104, 105 }));
        }

        [Test] public void EveryTitleGoesInTheCabinetAndTheProsPlayTheFinals()
        {
            var c = CareerSession.New("Collector", "ENG", 73);
            c.Enter(Tour.HomeIndex);
            while (c.NextOpponent() >= 0) c.RecordYours(true, "11-2 11-3");
            c.FinishSlot();
            Assert.That(c.Trophies.Count, Is.EqualTo(1));
            var t = c.Trophies[0];
            Assert.That(t.Home, Is.True); Assert.That(t.Event, Is.EqualTo("London Club Open")); Assert.That(t.City, Is.EqualTo("London"));
            Assert.That(t.Score, Is.EqualTo("11-2 11-3")); Assert.That(t.Season, Is.EqualTo(1));
            Assert.That(c.Trophies.Count, Is.EqualTo(c.S.Titles));
            var back = new CareerSession(UnityEngine.JsonUtility.FromJson<CareerState>(UnityEngine.JsonUtility.ToJson(c.S)));
            Assert.That(back.Trophies.Count, Is.EqualTo(1), "it saves");

            // the pros' Finals: eight players, the stage and group-win points
            while (Tour.HeadlineOf(c.S.Slot) != Tier.WorldTourFinals) c.Skip();
            int slot = c.S.Slot;
            c.Skip();
            var finals = c.S.Results.FindAll(r => r.Slot == slot && r.Tier == (int)Tier.WorldTourFinals);
            Assert.That(finals.Count, Is.EqualTo(8));
            Assert.That(finals.FindAll(r => r.Reached == 0).Count, Is.EqualTo(1));
            Assert.That(finals.FindAll(r => r.Reached == 3).Count, Is.EqualTo(4), "four out in the groups");
            foreach (var r in finals)
            {
                Assert.That((r.Points - new[] { 1000, 550, 200, 0 }[r.Reached]) % 150, Is.EqualTo(0), "the stage plus 150s");
                if (r.Reached == 0) Assert.That(r.Points, Is.GreaterThanOrEqualTo(1000 + 150));
            }
        }
    }
}
