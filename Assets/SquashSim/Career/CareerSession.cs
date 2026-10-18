// ===========================================================
// Assets/SquashSim/Career/CareerSession.cs
// P2-M4: the Career's rules, on a CareerState.
//
//   New(...)            a new career: the roster, then the tour's year
//                       before you joined, played out in the background
//   Ranking()           everyone, best first (rolling best 8 of 12)
//   Entry(tier)         may you enter this event now?
//   Enter(tier)         build this slot's draw with you in it
//   NextOpponent        your next opponent, or -1
//   RecordYours(...)    your match is done (played, simulated, retired):
//                       the rest of the round is played out
//   SimulateYours()     let the rally model play your match
//   FinishSlot()        play out your event and the slot's others, award
//                       points and prize money, move to the next month
//                       (and at the end of the year, the next season)
//
// Everything random comes from the career seed and the slot, event,
// round and match, so the same save always plays out the same way.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    public sealed class CareerSession
    {
        public readonly CareerState S;
        public CareerSession(CareerState s) { S = s; Normalise(); }

        public const int You = 0;

        /// <summary>A new career: the roster, then a year of the tour without you (so the ranking is real).</summary>
        public static CareerSession New(string name, string nat, uint seed, int gamesToWin = 2, bool english = false)
        {
            var s = new CareerState { Name = name, Nat = nat, Seed = seed, GamesToWin = gamesToWin, English = english, Season = 0, Month = 0 };
            s.Roster = Roster.Generate(seed);
            var c = new CareerSession(s);
            for (int m = 0; m < Tour.SlotsPerSeason; m++) c.FinishSlot();
            s.Seasons.Clear();
            s.SeasonJustEnded = false;
            s.Last = null;
            return c;
        }

        /// <summary>JsonUtility writes empty objects for nulls: read them back as nulls.</summary>
        void Normalise()
        {
            if (S.Current != null && (S.Current.Rounds == null || S.Current.Rounds.Count == 0)) S.Current = null;
            if (S.Last != null && string.IsNullOrEmpty(S.Last.Name)) S.Last = null;
            if (S.Roster == null) S.Roster = new List<Pro>();
            if (S.Results == null) S.Results = new List<ResultRec>();
            if (S.Matches == null) S.Matches = new List<MatchRec>();
            if (S.Seasons == null) S.Seasons = new List<SeasonRec>();
        }

        // ---------------- players ----------------

        Dictionary<int, Pro> _byId;
        public Pro Pro(int id)
        {
            if (_byId == null || _byId.Count != S.Roster.Count) { _byId = new Dictionary<int, Pro>(); foreach (var p in S.Roster) _byId[p.Id] = p; }
            return _byId.TryGetValue(id, out var x) ? x : null;
        }

        public string NameOf(int id) { return id == You ? S.Name : (Pro(id) != null ? Pro(id).Name : "?"); }
        public string BugOf(int id) { return id == You ? (S.Name ?? "YOU").ToUpperInvariant() : (Pro(id) != null ? Pro(id).Bug : "?"); }
        public string NatOf(int id) { return id == You ? S.Nat : (Pro(id) != null ? Pro(id).Nat : ""); }

        // ---------------- the ranking ----------------

        public sealed class Row { public int Id, Rank, Points; public int LastSlot = -1; }

        List<Row> _ranking;
        int _rankingAt = int.MinValue, _rankingResults = -1;

        /// <summary>Everyone (you included), best first: points from the best 8 results of the last 12 slots.</summary>
        public List<Row> Ranking()
        {
            if (_ranking != null && _rankingAt == S.Slot && _rankingResults == S.Results.Count) return _ranking;
            _ranking = RankingAt(S.Slot); _rankingAt = S.Slot; _rankingResults = S.Results.Count;
            return _ranking;
        }

        /// <summary>The ranking as it stood at the start of a slot (an earlier one: how far a player has moved).</summary>
        public List<Row> RankingAt(int slot)
        {
            var rows = new Dictionary<int, Row>();
            var per = new Dictionary<int, List<int>>();
            foreach (var p in S.Roster) if (!p.Retired) { rows[p.Id] = new Row { Id = p.Id }; per[p.Id] = new List<int>(); }
            rows[You] = new Row { Id = You }; per[You] = new List<int>();
            int from = slot - Tour.RankingWindow;
            foreach (var r in S.Results)
            {
                if (r.Slot < from || r.Slot >= slot || !rows.ContainsKey(r.Player)) continue;
                per[r.Player].Add(r.Points);
                if (r.Points > 0 && r.Slot > rows[r.Player].LastSlot) rows[r.Player].LastSlot = r.Slot;
            }
            foreach (var kv in per)
            {
                kv.Value.Sort((a, b) => b.CompareTo(a));
                int sum = 0;
                for (int i = 0; i < kv.Value.Count && i < Tour.CountedResults; i++) sum += kv.Value[i];
                rows[kv.Key].Points = sum;
            }
            var list = new List<Row>(rows.Values);
            list.Sort((a, b) =>
            {
                if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
                if ((a.Points == 0) && (a.Id == You) != (b.Id == You)) return a.Id == You ? 1 : -1;   // unranked: you start at the bottom
                if (a.LastSlot != b.LastSlot) return b.LastSlot.CompareTo(a.LastSlot);
                double sa = a.Id == You ? -1 : Pro(a.Id).Strength, sb = b.Id == You ? -1 : Pro(b.Id).Strength;
                if (sa != sb) return sb.CompareTo(sa);
                return a.Id.CompareTo(b.Id);
            });
            for (int i = 0; i < list.Count; i++) list[i].Rank = i + 1;
            return list;
        }

        List<Row> _before;
        int _beforeAt = int.MinValue, _beforeResults = -1;

        /// <summary>Places moved since the start of last month: positive = up. 0 when new to the list.</summary>
        public int Movement(int id)
        {
            if (_before == null || _beforeAt != S.Slot || _beforeResults != S.Results.Count)
            { _before = RankingAt(S.Slot - 1); _beforeAt = S.Slot; _beforeResults = S.Results.Count; }
            int was = 0;
            foreach (var r in _before) if (r.Id == id) { was = r.Rank; break; }
            return was == 0 ? 0 : was - RankOf(id);
        }

        public int RankOf(int id) { foreach (var r in Ranking()) if (r.Id == id) return r.Rank; return 999; }
        public int PointsOf(int id) { foreach (var r in Ranking()) if (r.Id == id) return r.Points; return 0; }

        /// <summary>The pros only, best first (the tier bands are places in this list).</summary>
        List<int> ProOrder()
        {
            var ids = new List<int>();
            foreach (var r in Ranking()) if (r.Id != You) ids.Add(r.Id);
            return ids;
        }

        // ---------------- entering ----------------

        public Tier[] EventsNow() { return Tour.EventsIn(S.Month); }

        /// <summary>Null when you may enter this event now, else why not.</summary>
        public string Entry(Tier t)
        {
            if (S.Current != null) return "you are in an event";
            if (Array.IndexOf(EventsNow(), t) < 0) return "not this month";
            var info = Tour.Info(t);
            int rank = RankOf(You);
            if (info.UnlockRank > 0 && rank > info.UnlockRank)
                return t == Tier.WorldFinals ? "the season's top 8 only" : "opens at rank " + info.UnlockRank;
            return null;
        }

        public bool CanEnter(Tier t) { return Entry(t) == null; }

        /// <summary>The field for one of this slot's events: ids, best-ranked first.</summary>
        List<int> Field(Tier t, bool withYou)
        {
            var info = Tour.Info(t);
            var field = new List<int>();
            if (t == Tier.WorldFinals)
            {
                foreach (var r in Ranking()) { if (field.Count >= info.Draw) break; if (r.Id != You || withYou) field.Add(r.Id); }
                return field;
            }
            var order = ProOrder();
            for (int place = info.BandTop; place <= info.BandBottom && place - 1 < order.Count; place++) field.Add(order[place - 1]);
            if (withYou)
            {
                field.RemoveAt(field.Count - 1);                      // you take the lowest entrant's place
                int at = 0, mine = RankOf(You);
                while (at < field.Count && RankOf(field[at]) < mine) at++;
                field.Insert(at, You);
            }
            return field;
        }

        /// <summary>A draw: seeds 1 and 2 at the ends, 3 and 4 in the other half-ends, the rest by lot.</summary>
        RoundRec Draw(List<int> field, CareerRng rng)
        {
            int n = field.Count;
            var slots = new int[n];
            for (int i = 0; i < n; i++) slots[i] = int.MinValue;
            slots[0] = field[0];
            if (n > 1) slots[n - 1] = field[1];
            if (n > 3)
            {
                bool flip = rng.Next() < 0.5;
                slots[n / 2 - 1] = field[flip ? 3 : 2];
                slots[n / 2] = field[flip ? 2 : 3];
            }
            var rest = new List<int>();
            for (int i = Math.Min(4, n); i < n; i++) rest.Add(field[i]);
            for (int i = rest.Count - 1; i > 0; i--) { int j = rng.Range(i + 1); int tmp = rest[i]; rest[i] = rest[j]; rest[j] = tmp; }
            int k = 0;
            for (int i = 0; i < n; i++) if (slots[i] == int.MinValue) slots[i] = rest[k++];
            var round = new RoundRec();
            round.Players.AddRange(slots);
            return round;
        }

        EventState Build(Tier t, bool withYou)
        {
            var e = new EventState { Slot = S.Slot, Tier = (int)t, Name = Tour.EventName(S.Month, t), YouIn = withYou };
            e.Rounds.Add(Draw(Field(t, withYou), new CareerRng(S.Seed, S.Slot * 16 + (int)t, 999)));
            return e;
        }

        /// <summary>Enter one of this month's events (Entry(t) must be null).</summary>
        public EventState Enter(Tier t)
        {
            if (!CanEnter(t)) throw new InvalidOperationException("can't enter " + t + ": " + Entry(t));
            S.Current = Build(t, true);
            return S.Current;
        }

        // ---------------- your matches ----------------

        RoundRec Open(EventState e) { return e.Rounds[e.Rounds.Count - 1]; }

        /// <summary>Your next opponent in the current event, or -1.</summary>
        public int NextOpponent()
        {
            var e = S.Current;
            if (e == null || !e.YouIn || e.YouOut || e.Done) return -1;
            var r = Open(e);
            int i = r.Players.IndexOf(You);
            return i < 0 ? -1 : r.Players[i ^ 1];
        }

        /// <summary>The round you are about to play: players left ("Quarter-final" = 8).</summary>
        public int PlayersLeft { get { return S.Current == null ? 0 : Open(S.Current).Players.Count; } }

        /// <summary>Your match is over. Played matches move your rating; then the rest of the round is played out.</summary>
        public void RecordYours(bool won, string score, int ralliesWon = 0, int ralliesTotal = 0, bool simulated = false, bool retired = false)
        {
            int opp = NextOpponent();
            if (opp < 0) throw new InvalidOperationException("no match to record");
            var e = S.Current;
            if (!simulated && !retired && ralliesTotal > 0) S.Rating = RallyModel.Rate(S.Rating, Pro(opp).Strength, ralliesWon, ralliesTotal);
            S.Matches.Add(new MatchRec
            {
                Slot = S.Slot, Tier = e.Tier, Left = Open(e).Players.Count, Event = e.Name, Opp = opp, OppRank = RankOf(opp),
                Won = won, Simulated = simulated, Retired = retired, Score = retired ? "retired" : score
            });
            PlayRound(e, won ? You : opp, score);
            if (!won) e.YouOut = true;
        }

        /// <summary>The rally model plays your match (at your rating).</summary>
        public RallyModel.Result SimulateYours()
        {
            int opp = NextOpponent();
            if (opp < 0) throw new InvalidOperationException("no match to simulate");
            var rng = new CareerRng(S.Seed, S.Slot * 16 + S.Current.Tier, 5000 + PlayersLeft);
            var res = RallyModel.Play(RallyModel.YouRally(S.Rating, Pro(opp).Strength), S.GamesToWin, S.English, rng);
            RecordYours(res.AWon, res.Score, res.RalliesA, res.RalliesA + res.RalliesB, simulated: true);
            return res;
        }

        /// <summary>Play the open round (your match, if any, already decided as yourWinner), then open the next.</summary>
        void PlayRound(EventState e, int yourWinner, string yourScore)
        {
            var r = Open(e);
            int round = e.Rounds.Count - 1;
            for (int m = 0; m * 2 + 1 < r.Players.Count; m++)
            {
                int a = r.Players[m * 2], b = r.Players[m * 2 + 1];
                if (a == You || b == You)
                {
                    r.Winners.Add(yourWinner);
                    r.Scores.Add(a == You ? yourScore : Flip(yourScore));
                    continue;
                }
                var rng = new CareerRng(S.Seed, S.Slot * 16 + e.Tier, round * 64 + m);
                var res = RallyModel.Play(RallyModel.ProRally(Pro(a).Strength, Pro(b).Strength), S.GamesToWin, S.English, rng);
                r.Winners.Add(res.AWon ? a : b);
                r.Scores.Add(res.Score);
            }
            if (r.Winners.Count == 1) { e.Done = true; return; }
            var next = new RoundRec();
            next.Players.AddRange(r.Winners);
            e.Rounds.Add(next);
        }

        static string Flip(string score)
        {
            if (string.IsNullOrEmpty(score)) return score;
            var games = score.Split(' ');
            for (int i = 0; i < games.Length; i++) { var p = games[i].Split('-'); if (p.Length == 2) games[i] = p[1] + "-" + p[0]; }
            return string.Join(" ", games);
        }

        /// <summary>Play out an event to the end (your matches must already be decided).</summary>
        void PlayOut(EventState e)
        {
            int guard = 0;
            while (!e.Done && guard++ < 10)
            {
                if (e.YouIn && !e.YouOut && Open(e).Players.Contains(You)) throw new InvalidOperationException("your match first");
                PlayRound(e, -1, null);
            }
        }

        /// <summary>The event's winner (once done).</summary>
        public static int Winner(EventState e) { return e.Done ? e.Rounds[e.Rounds.Count - 1].Winners[0] : -1; }

        /// <summary>How far a player got: Reached index (0 = won), or -1 if not in it.</summary>
        public static int Reached(EventState e, int id)
        {
            if (!e.Rounds[0].Players.Contains(id)) return -1;
            for (int i = 0; i < e.Rounds.Count; i++)
            {
                var r = e.Rounds[i];
                int at = r.Players.IndexOf(id);
                if (at < 0) continue;
                if (r.Winners.Count > at / 2 && r.Winners[at / 2] != id) return Tour.ReachedIndex(r.Players.Count);
            }
            return Winner(e) == id ? 0 : Tour.ReachedIndex(e.Rounds[e.Rounds.Count - 1].Players.Count);
        }

        /// <summary>True when the month can end (you are not in an event, or your part in it is over).</summary>
        public bool CanFinishSlot { get { return S.Current == null || S.Current.Done || S.Current.YouOut; } }

        /// <summary>Not entering anything this month.</summary>
        public void Skip() { if (S.Current == null) FinishSlot(); }

        /// <summary>End the month: every event in it played out, points and prize money awarded; on to the next.</summary>
        public void FinishSlot()
        {
            if (!CanFinishSlot) throw new InvalidOperationException("your match first");
            var events = new List<EventState>();
            Tier yours = S.Current != null ? (Tier)S.Current.Tier : (Tier)(-1);
            foreach (var t in EventsNow())
            {
                if (S.Current != null && t == yours) { PlayOut(S.Current); events.Add(S.Current); continue; }
                var e = Build(t, false);
                PlayOut(e);
                events.Add(e);
            }
            foreach (var e in events)
            {
                var info = Tour.Info((Tier)e.Tier);
                foreach (int id in e.Rounds[0].Players)
                {
                    int reached = Reached(e, id);
                    S.Results.Add(new ResultRec { Player = id, Slot = S.Slot, Tier = e.Tier, Reached = reached, Points = info.Points[reached] });
                    if (id == You)
                    {
                        e.YouReached = reached; e.YouPoints = info.Points[reached]; e.YouPrize = info.Prize[reached];
                        S.Earnings += info.Prize[reached];
                        if (reached == 0) S.Titles++;
                    }
                }
            }
            S.Last = S.Current;
            S.Current = null;
            Prune();
            S.Month++;
            if (S.Month >= Tour.SlotsPerSeason) EndSeason();
        }

        void Prune()
        {
            int keep = S.Slot - 2 * Tour.RankingWindow;
            S.Results.RemoveAll(r => r.Slot < keep && r.Player != You);
        }

        void EndSeason()
        {
            S.Month = 0;
            int season = S.Season;
            S.Season++;                                        // the ranking now looks at the year just played
            var rec = new SeasonRec { Season = season, Rank = RankOf(You) };
            int best = int.MaxValue;
            foreach (var m in S.Matches)
            {
                if (m.Slot / Tour.SlotsPerSeason != season) continue;
                rec.Played++;
                if (!m.Won) continue;
                rec.Won++;
                if (m.OppRank < best) { best = m.OppRank; rec.BestWin = NameOf(m.Opp) + " (#" + m.OppRank + ")"; }
            }
            foreach (var r in S.Results)
                if (r.Player == You && r.Slot / Tour.SlotsPerSeason == season) { if (r.Reached == 0) rec.Titles++; rec.Earnings += Tour.Info((Tier)r.Tier).Prize[r.Reached]; }
            S.Seasons.Add(rec);
            S.SeasonJustEnded = true;
            int next = S.NextId;
            Roster.NewSeason(S.Roster, S.Seed, season, ref next);
            S.NextId = next;
            _byId = null; _ranking = null;
        }

        // ---------------- scouting ----------------

        /// <summary>A player's last results, newest first: "W", "F", "SF", "QF", "R16", "R32".</summary>
        public List<string> Form(int id, int count = 5)
        {
            var mine = S.Results.FindAll(r => r.Player == id && r.Slot < S.Slot);
            mine.Sort((a, b) => b.Slot.CompareTo(a.Slot));
            var list = new List<string>();
            for (int i = 0; i < mine.Count && i < count; i++) list.Add(ReachedShort(mine[i].Reached));
            return list;
        }

        public static string ReachedShort(int reached)
        {
            switch (reached) { case 0: return "W"; case 1: return "F"; case 2: return "SF"; case 3: return "QF"; case 4: return "R16"; default: return "R32"; }
        }

        public static string ReachedLong(int reached)
        {
            switch (reached) { case 0: return "Champion"; case 1: return "Runner-up"; case 2: return "Semi-finals"; case 3: return "Quarter-finals"; case 4: return "Round of 16"; default: return "Round of 32"; }
        }

        /// <summary>Your record against this pro: wins, losses.</summary>
        public void HeadToHead(int opp, out int wins, out int losses)
        {
            wins = 0; losses = 0;
            foreach (var m in S.Matches) if (m.Opp == opp) { if (m.Won) wins++; else losses++; }
        }

        /// <summary>A rival: beaten you twice, or met you in two finals.</summary>
        public bool IsRival(int opp)
        {
            int beat = 0, finals = 0;
            foreach (var m in S.Matches) if (m.Opp == opp) { if (!m.Won) beat++; if (m.Left == 2) finals++; }
            return beat >= 2 || finals >= 2;
        }

        public List<int> Rivals()
        {
            var seen = new List<int>();
            foreach (var m in S.Matches) if (!seen.Contains(m.Opp) && IsRival(m.Opp) && Pro(m.Opp) != null && !Pro(m.Opp).Retired) seen.Add(m.Opp);
            return seen;
        }
    }
}
