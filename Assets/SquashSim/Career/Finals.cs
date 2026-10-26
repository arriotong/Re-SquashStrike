// ===========================================================
// Assets/SquashSim/Career/Finals.cs
// P7 phase 6 (2026-10-27): the Tour Finals as the PSA plays them (Rule
// Book 4.8): the Road's top 8 in two groups of four (snake-seeded: 1, 4,
// 5, 8 and 2, 3, 6, 7), a round robin of three match days, the top two
// of each group into the semi-finals (a group's winner against the
// other's runner-up), then the final - one game longer than the career's
// matches (best of 5 for a best-of-3 career).
//
//   points (Appendix IX)   1,000 the winner, 550 the final, 200 the
//                          semis, + 150 a group win, + 150 unbeaten
//   prize money            by the stage reached (Tour.Info: the purse)
//   standings              wins, then the head-to-head (two level),
//                          then games won less lost, then the seed
//
// The event uses the knockout's records: Rounds[0..2] are the match days
// (each its four matches in pairs), Rounds[3] the semis, Rounds[4] the
// final. PlayersLeft is -1..-3 on a match day ("Group match 1").
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Career
{
    public sealed partial class CareerSession
    {
        public const int GroupDays = 3;
        public const int GroupWinPoints = 150, UnbeatenPoints = 150;
        // a round robin of four (seeds a b c d): 1v4 2v3, 1v3 4v2, 1v2 3v4
        static readonly int[][] Days = { new[] { 0, 3, 1, 2 }, new[] { 0, 2, 3, 1 }, new[] { 0, 1, 2, 3 } };

        static bool IsRoundRobin(EventState e) { return e != null && e.RoundRobin; }

        /// <summary>The two groups (seeded best first: the snake).</summary>
        public static List<int>[] Groups(EventState e)
        {
            var g = new[] { new List<int>(), new List<int>() };
            var seeds = e.Seeds;
            int[] snake = { 0, 1, 1, 0, 0, 1, 1, 0 };
            for (int i = 0; i < seeds.Count && i < 8; i++) g[snake[i]].Add(seeds[i]);
            return g;
        }

        /// <summary>The round robin's first match day: field best first (8).</summary>
        EventState BuildRoundRobin(int index, List<int> field, bool withYou)
        {
            var ev = Tour.EventAt(S.Slot, index);
            var e = new EventState { Slot = S.Slot, Tier = (int)ev.Tier, Index = index, Name = ev.Name, City = ev.City, YouIn = withYou, RoundRobin = true };
            e.Seeds.AddRange(field);
            e.Rounds.Add(MatchDay(e, 0));
            return e;
        }

        static RoundRec MatchDay(EventState e, int day)
        {
            var r = new RoundRec();
            foreach (var g in Groups(e))
                if (g.Count == 4) foreach (int k in Days[day]) r.Players.Add(g[k]);
            return r;
        }

        public sealed class Standing { public int Id, Wins, Played, GamesFor, GamesAgainst, Seed; }

        /// <summary>A group's table after the match days played so far, top first.</summary>
        public static List<Standing> Table(EventState e, int group)
        {
            var g = Groups(e)[group];
            var rows = new Dictionary<int, Standing>();
            for (int i = 0; i < g.Count; i++) rows[g[i]] = new Standing { Id = g[i], Seed = e.Seeds.IndexOf(g[i]) + 1 };
            var beat = new HashSet<(int, int)>();
            for (int d = 0; d < GroupDays && d < e.Rounds.Count; d++)
            {
                var r = e.Rounds[d];
                for (int m = 0; m < r.Winners.Count; m++)
                {
                    int a = r.Players[2 * m], b = r.Players[2 * m + 1];
                    if (!rows.ContainsKey(a)) continue;
                    int w = r.Winners[m], l = w == a ? b : a;
                    rows[w].Wins++; rows[w].Played++; rows[l].Played++;
                    beat.Add((w, l));
                    Games(r.Scores[m], out int ga, out int gb);                 // the score from a's side
                    rows[a].GamesFor += ga; rows[a].GamesAgainst += gb; rows[b].GamesFor += gb; rows[b].GamesAgainst += ga;
                }
            }
            var list = new List<Standing>(rows.Values);
            list.Sort((x, y) =>
            {
                if (x.Wins != y.Wins) return y.Wins.CompareTo(x.Wins);
                if (beat.Contains((x.Id, y.Id))) return -1;
                if (beat.Contains((y.Id, x.Id))) return 1;
                int dx = x.GamesFor - x.GamesAgainst, dy = y.GamesFor - y.GamesAgainst;
                if (dx != dy) return dy.CompareTo(dx);
                return x.Seed.CompareTo(y.Seed);
            });
            return list;
        }

        static void Games(string score, out int a, out int b)
        {
            a = 0; b = 0;
            if (string.IsNullOrEmpty(score)) return;
            foreach (var g in score.Split(' '))
            {
                var p = g.Split('-');
                if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y)) { if (x > y) a++; else b++; }
            }
        }

        /// <summary>After a match day: the next one, or (after the third) the semis - A1 v B2, B1 v A2; after the semis the final.</summary>
        void NextRoundRobin(EventState e)
        {
            int played = e.Rounds.Count;
            var last = e.Rounds[played - 1];
            if (played < GroupDays) { e.Rounds.Add(MatchDay(e, played)); return; }
            if (played == GroupDays)
            {
                var a = Table(e, 0); var b = Table(e, 1);
                var semis = new RoundRec();
                semis.Players.Add(a[0].Id); semis.Players.Add(b[1].Id);
                semis.Players.Add(b[0].Id); semis.Players.Add(a[1].Id);
                e.Rounds.Add(semis);
                if (e.YouIn && !semis.Players.Contains(You)) e.YouOut = true;      // out in the group
                return;
            }
            if (last.Winners.Count == 1) { e.Done = true; return; }
            var final = new RoundRec();
            final.Players.AddRange(last.Winners);
            e.Rounds.Add(final);
        }

        static int ReachedRoundRobin(EventState e, int id)
        {
            if (!e.Seeds.Contains(id)) return -1;
            if (e.Rounds.Count > GroupDays + 1 && e.Rounds[GroupDays + 1].Players.Contains(id))
                return e.Done && Winner(e) == id ? 0 : 1;
            if (e.Rounds.Count > GroupDays && e.Rounds[GroupDays].Players.Contains(id)) return 2;
            return 3;
        }

        /// <summary>A Finals player's points: the stage, and 150 a group win (150 more unbeaten).</summary>
        int RoundRobinPoints(EventState e, int id, int reached)
        {
            int[] stage = { 1000, 550, 200, 0 };
            int wins = 0;
            foreach (var t in new[] { Table(e, 0), Table(e, 1) }) foreach (var s in t) if (s.Id == id) wins = s.Wins;
            return stage[reached] + GroupWinPoints * wins + (wins == GroupDays ? UnbeatenPoints : 0);
        }

        /// <summary>The games to win in your next match: the Finals' final is one longer (at most best of 5).</summary>
        public int GamesToWinNow
        {
            get
            {
                var e = S.Current;
                return e != null && e.RoundRobin && e.Rounds.Count == GroupDays + 2 ? System.Math.Min(3, S.GamesToWin + 1) : S.GamesToWin;
            }
        }

        // ---------------- the trophy cabinet ----------------

        /// <summary>Your titles, oldest first.</summary>
        public List<TrophyRec> Trophies { get { return S.Trophies; } }

        void AddTrophy(EventState e)
        {
            var final = e.Rounds[e.Rounds.Count - 1];
            int at = final.Players.IndexOf(You);
            int opp = at >= 0 && final.Players.Count > (at ^ 1) ? final.Players[at ^ 1] : -1;
            string score = final.Scores.Count > 0 ? (at == 0 ? final.Scores[0] : Flip(final.Scores[0])) : "";
            S.Trophies.Add(new TrophyRec
            {
                Slot = S.Slot, Season = S.Season, Tier = e.Tier, Home = e.Home, Event = e.Name, City = e.City,
                Opp = opp, OppName = opp >= 0 ? NameOf(opp) : "", Score = score,
            });
        }
    }
}
