// ===========================================================
// Assets/SquashSim/Career/CareerSession.cs
// P2-M4: the Career's rules, on a CareerState.
//
//   New(...)            a new career: the roster, then the tour's year
//                       before you joined, played out in the background
//   Ranking()           the ranked players, best first: the PSA's rolling
//                       12 months, points averaged over the events played
//                       (dividing by at least 10). No results = unranked.
//   Entry(tier)         may you enter this event now?
//   Enter(tier)         build this slot's draw with you in it (the month's
//                       fields are drawn biggest event first: MonthFields)
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
    public sealed partial class CareerSession
    {
        public readonly CareerState S;
        public CareerSession(CareerState s) { S = s; Normalise(); }

        public const int You = 0;

        /// <summary>A new career: the world's pros, then a year of the tour without you (so the ranking is real). You start unranked.</summary>
        public static CareerSession New(string name, string nat, uint seed, int gamesToWin = 2, bool english = false)
        {
            var s = new CareerState { Name = name, Nat = nat, Seed = seed, GamesToWin = gamesToWin, English = english, Season = 0, Month = 0 };
            s.Roster = Roster.Generate(seed);
            var c = new CareerSession(s);
            for (int m = 0; m < Tour.SlotsPerSeason; m++) c.FinishSlot();
            s.Seasons.Clear();
            s.SeasonJustEnded = false;
            s.TrainingPoints = 0; s.PointsEarned = 0;          // the year before you joined earns you nothing
            s.Earnings = 0; s.GoalsDone = 0; s.SponsorPaid = 0; s.SponsorNews = null; s.Sponsor = null;
            s.LastGoals = new List<GoalRec>();
            c.SetGoals();                                      // season 1's goals, from where you start (unranked)
            c.MakeOffers();
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
            if (S.Attributes == null) S.Attributes = new AthleteAttributes();
            if (S.Goals == null) S.Goals = new List<GoalRec>();
            if (S.LastGoals == null) S.LastGoals = new List<GoalRec>();
            if (S.Offers == null) S.Offers = new List<SponsorDeal>();
            if (S.Sponsor != null && string.IsNullOrEmpty(S.Sponsor.Brand)) S.Sponsor = null;      // JsonUtility's empty object
            if (S.Version < CareerState.CurrentVersion) S.Version = CareerState.CurrentVersion;     // v2 -> v3: the defaults fill in
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

        public sealed class Row
        {
            public int Id, Rank, Total, Events;
            public double Average;                          // the ranking points: Total / max(Events, MinDivisor)
            public int LastSlot = -1;
            /// <summary>The ranking points, rounded (the tables show them like this).</summary>
            public int Points { get { return (int)System.Math.Round(Average); } }
        }

        /// <summary>RankOf for a player with no results in the last 12 months.</summary>
        public const int Unranked = 9999;

        List<Row> _ranking;
        Dictionary<int, Row> _rankById;
        int _rankingAt = int.MinValue, _rankingResults = -1, _rankingRoster = -1;

        /// <summary>The ranked players (you included, once you have a result), best first.</summary>
        public List<Row> Ranking()
        {
            if (_ranking != null && _rankingAt == S.Slot && _rankingResults == S.Results.Count && _rankingRoster == S.Roster.Count) return _ranking;
            _ranking = RankingAt(S.Slot); _rankingAt = S.Slot; _rankingResults = S.Results.Count; _rankingRoster = S.Roster.Count;
            _rankById = new Dictionary<int, Row>();
            foreach (var r in _ranking) _rankById[r.Id] = r;
            _order = null;
            return _ranking;
        }

        /// <summary>The ranking as it stood at the start of a slot (an earlier one: how far a player has moved).</summary>
        public List<Row> RankingAt(int slot)
        {
            var rows = new Dictionary<int, Row>();
            int from = slot - Tour.RankingWindow;
            foreach (var r in S.Results)
            {
                if (r.Slot < from || r.Slot >= slot) continue;
                if (r.Player != You) { var p = Pro(r.Player); if (p == null || p.Retired) continue; }
                if (!rows.TryGetValue(r.Player, out var row)) rows[r.Player] = row = new Row { Id = r.Player };
                row.Total += r.Points;
                row.Events++;
                if (r.Slot > row.LastSlot) row.LastSlot = r.Slot;
            }
            var list = new List<Row>();
            foreach (var row in rows.Values)
            {
                if (row.Total <= 0) continue;
                row.Average = (double)row.Total / System.Math.Max(row.Events, Tour.MinDivisor);
                list.Add(row);
            }
            list.Sort((a, b) =>
            {
                if (a.Average != b.Average) return b.Average.CompareTo(a.Average);
                if (a.Total != b.Total) return b.Total.CompareTo(a.Total);
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
            int now = RankOf(id);
            return was == 0 || now == Unranked ? 0 : was - now;
        }

        /// <summary>A player's world ranking, or Unranked.</summary>
        public int RankOf(int id) { Ranking(); return _rankById.TryGetValue(id, out var r) ? r.Rank : Unranked; }
        public bool IsRanked(int id) { return RankOf(id) != Unranked; }
        /// <summary>Ranking points (the average), rounded.</summary>
        public int PointsOf(int id) { Ranking(); return _rankById.TryGetValue(id, out var r) ? r.Points : 0; }
        public double AverageOf(int id) { Ranking(); return _rankById.TryGetValue(id, out var r) ? r.Average : 0; }
        /// <summary>Events counted in the ranking (the last 12 months).</summary>
        public int EventsOf(int id) { Ranking(); return _rankById.TryGetValue(id, out var r) ? r.Events : 0; }

        List<int> _order;

        /// <summary>The pros in entry order: the ranked best first, then the unranked by strength. A tier's band is places in this list.</summary>
        List<int> ProOrder()
        {
            Ranking();
            if (_order != null) return _order;
            var ids = new List<int>();
            foreach (var r in _ranking) if (r.Id != You) ids.Add(r.Id);
            var rest = new List<Pro>();
            foreach (var p in S.Roster) if (!p.Retired && !_rankById.ContainsKey(p.Id)) rest.Add(p);
            rest.Sort((a, b) => a.Strength != b.Strength ? b.Strength.CompareTo(a.Strength) : a.Id.CompareTo(b.Id));
            foreach (var p in rest) ids.Add(p.Id);
            return _order = ids;
        }

        // ---------------- entering ----------------

        /// <summary>This month's tiers, biggest first, each once (a month has several Challenger 3s: you play one).</summary>
        public Tier[] EventsNow()
        {
            var list = new List<Tier>();
            foreach (var t in Tour.EventsIn(S.Month)) if (!list.Contains(t)) list.Add(t);
            return list.ToArray();
        }

        /// <summary>The month's events worth showing you: the best two you may enter, and the next one up (a goal).</summary>
        public List<Tier> EventsForYou()
        {
            var open = new List<Tier>();
            Tier next = (Tier)(-1);
            foreach (var t in EventsNow())
            {
                if (CanEnter(t)) { if (open.Count < 2) open.Add(t); }
                else if (open.Count == 0) next = t;              // the smallest locked one above the open ones
            }
            var list = new List<Tier>();
            if ((int)next >= 0) list.Add(next);
            list.AddRange(open);
            return list;
        }

        /// <summary>Null when you may enter this event now, else why not.</summary>
        public string Entry(Tier t)
        {
            if (S.Current != null) return "you are in an event";
            if (Tour.IndexOf(S.Month, t) < 0) return "not this month";
            var info = Tour.Info(t);
            int rank = RankOf(You);
            if (t == Tier.WorldTourFinals) return rank <= info.UnlockRank ? null : "the world's top 8 only";
            if (info.UnlockRank > 0 && rank > info.UnlockRank)
                return rank == Unranked ? "needs a world ranking" : "opens at #" + info.UnlockRank;
            return null;
        }

        public bool CanEnter(Tier t) { return Entry(t) == null; }

        /// <summary>Does this pro enter an event this month? (Most do; the same answer all month.)</summary>
        bool Enters(int id) { return CareerRng.Mix(S.Seed ^ 0x5EA5u, S.Slot, id) % 1000u < (uint)(Tour.EntryChance * 1000); }

        /// <summary>Every event's field this month, by its index in Tour.EventsIn: filled biggest event first, each from its
        /// band down, the best-ranked entrants first; nobody plays two. yourEvent = the index you play (-1 = none):
        /// you take the place of its lowest entrant.</summary>
        List<int>[] MonthFields(int yourEvent)
        {
            var tiers = Tour.EventsIn(S.Month);
            var fields = new List<int>[tiers.Length];
            var order = ProOrder();
            var used = new HashSet<int>();
            var idx = new List<int>();
            for (int i = 0; i < tiers.Length; i++) idx.Add(i);
            idx.Sort((a, b) => tiers[a] != tiers[b] ? ((int)tiers[b]).CompareTo((int)tiers[a]) : a.CompareTo(b));
            foreach (int i in idx)
            {
                var info = Tour.Info(tiers[i]);
                var field = new List<int>();
                bool everyone = tiers[i] == Tier.WorldChampionship || tiers[i] == Tier.WorldTourFinals;   // nobody misses these
                for (int pass = 0; pass < 2 && field.Count < info.Draw; pass++)
                    for (int place = info.BandTop - 1; place < order.Count && field.Count < info.Draw; place++)
                    {
                        int id = order[place];
                        if (used.Contains(id) || field.Contains(id)) continue;
                        if (pass == 0 && !everyone && !Enters(id)) continue;
                        field.Add(id);
                    }
                if (i == yourEvent && field.Count > 0)
                {
                    field.RemoveAt(field.Count - 1);                      // you take the lowest entrant's place
                    int at = 0, mine = RankOf(You);
                    while (at < field.Count && RankOf(field[at]) < mine) at++;
                    field.Insert(at, You);
                }
                foreach (int id in field) used.Add(id);
                fields[i] = field;
            }
            return fields;
        }

        /// <summary>The draw's seeding: seed number at each slot (1 at the top, 2 at the bottom, 3 and 4 in the other halves...).</summary>
        static int[] SeedOrder(int n)
        {
            var o = new List<int> { 1, 2 };
            while (o.Count < n)
            {
                int m = o.Count * 2;
                var next = new List<int>();
                for (int i = 0; i < o.Count; i++)
                {
                    if (i % 2 == 0) { next.Add(o[i]); next.Add(m + 1 - o[i]); }
                    else { next.Add(m + 1 - o[i]); next.Add(o[i]); }
                }
                o = next;
            }
            return o.ToArray();
        }

        /// <summary>A draw: a quarter of the field seeded (1 and 2 at the ends, 3-4, 5-8, 9-16 drawn into their places), the rest by lot.</summary>
        RoundRec Draw(List<int> field, CareerRng rng)
        {
            int n = field.Count;
            var slots = new int[n];
            if (n < 2) { var single = new RoundRec(); single.Players.AddRange(field); return single; }
            var order = SeedOrder(n);
            int seeds = System.Math.Max(2, n / 4);
            // the seeds: groups 3-4, 5-8, 9-16 are each drawn into their group's places
            var bySeed = new int[seeds + 1];
            for (int k = 1; k <= seeds; k++) bySeed[k] = field[k - 1];
            for (int lo = 3, hi; lo <= seeds; lo = hi + 1)
            {
                hi = System.Math.Min(seeds, lo * 2 - 2);
                for (int k = hi; k > lo; k--) { int j = lo + rng.Range(k - lo + 1); int tmp = bySeed[k]; bySeed[k] = bySeed[j]; bySeed[j] = tmp; }
            }
            var rest = new List<int>();
            for (int i = seeds; i < n; i++) rest.Add(field[i]);
            for (int i = rest.Count - 1; i > 0; i--) { int j = rng.Range(i + 1); int tmp = rest[i]; rest[i] = rest[j]; rest[j] = tmp; }
            int r = 0;
            for (int i = 0; i < n; i++) slots[i] = order[i] <= seeds ? bySeed[order[i]] : rest[r++];
            var round = new RoundRec();
            round.Players.AddRange(slots);
            return round;
        }

        EventState Build(int index, List<int> field, bool withYou)
        {
            var t = Tour.EventsIn(S.Month)[index];
            var e = new EventState { Slot = S.Slot, Tier = (int)t, Index = index, Name = Tour.EventName(S.Month, index), YouIn = withYou };
            e.Rounds.Add(Draw(field, new CareerRng(S.Seed, S.Slot * 16 + index, 999)));
            return e;
        }

        /// <summary>Enter one of this month's events (Entry(t) must be null).</summary>
        public EventState Enter(Tier t)
        {
            if (!CanEnter(t)) throw new InvalidOperationException("can't enter " + t + ": " + Entry(t));
            int index = Tour.IndexOf(S.Month, t);
            S.Current = Build(index, MonthFields(index)[index], true);
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
            else { Award(PointsPerWin); GoalsAfterWin(S.Matches[S.Matches.Count - 1].OppRank); }
        }

        /// <summary>The rally model plays your match (at your rating).</summary>
        public RallyModel.Result SimulateYours()
        {
            int opp = NextOpponent();
            if (opp < 0) throw new InvalidOperationException("no match to simulate");
            var rng = new CareerRng(S.Seed, S.Slot * 16 + S.Current.Index, 5000 + PlayersLeft);
            var res = RallyModel.Play(RallyModel.YouRally(S.Rating + AttributeEdge, Pro(opp).Strength), S.GamesToWin, S.English, rng);
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
                var rng = new CareerRng(S.Seed, S.Slot * 16 + e.Index, round * 64 + m);
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
            while (!e.Done && guard++ < 12)
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

        // ---------------- P3-R3: player growth ----------------

        public const int PointsPerWin = 1, PointsPerSeason = 2, TrainingMonthPoints = 3;

        /// <summary>A title's points: 1 for a small Challenger, 2 up to Gold, 3 above.</summary>
        public static int TitlePoints(int tier) { int b = Tour.Band(tier); return b <= 1 ? 1 : b <= 3 ? 2 : 3; }

        void Award(int points) { if (points <= 0) return; S.TrainingPoints += points; S.PointsEarned += points; }

        /// <summary>Your attributes' edge in a simulated match (a rating bonus: 0.015 per point of the mean above 5).</summary>
        public double AttributeEdge { get { return S.Attributes == null ? 0 : System.Math.Round(0.015 * (S.Attributes.Mean - AthleteAttributes.Neutral), 4); } }

        /// <summary>A month on the training court instead of an event: the events go on without you, you gain points.</summary>
        public void Train()
        {
            if (S.Current != null) return;
            Award(TrainingMonthPoints);
            S.MonthsTrained++;
            S.LastTrainedSlot = S.Slot;
            FinishSlot();
        }

        /// <summary>Spend a point on an attribute ("Speed", "Stamina", "Power", "Control"). False when it can't.</summary>
        public bool Spend(string attribute)
        {
            if (S.TrainingPoints <= 0) return false;
            var a = S.Attributes;
            double v = Get(a, attribute);
            if (v < 0 || v >= AthleteAttributes.Max) return false;
            Set(a, attribute, v + 1);
            S.TrainingPoints--;
            return true;
        }

        public static readonly string[] AttributeNames = { "Speed", "Stamina", "Power", "Control" };

        public static double Get(AthleteAttributes a, string name)
        {
            switch (name) { case "Speed": return a.Speed; case "Stamina": return a.Stamina; case "Power": return a.Power; case "Control": return a.Control; default: return -1; }
        }

        static void Set(AthleteAttributes a, string name, double v)
        {
            switch (name) { case "Speed": a.Speed = v; break; case "Stamina": a.Stamina = v; break; case "Power": a.Power = v; break; case "Control": a.Control = v; break; }
        }

        /// <summary>End the month: every event in it played out, points and prize money awarded; on to the next.</summary>
        public void FinishSlot()
        {
            if (!CanFinishSlot) throw new InvalidOperationException("your match first");
            var events = new List<EventState>();
            int yours = S.Current != null ? S.Current.Index : -1;
            var fields = MonthFields(yours);
            for (int i = 0; i < fields.Length; i++)
            {
                if (i == yours) { PlayOut(S.Current); events.Add(S.Current); continue; }
                if (fields[i].Count < 2) continue;
                var e = Build(i, fields[i], false);
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
                        if (reached == 0) { S.Titles++; Award(TitlePoints(e.Tier)); SponsorTitle(); }
                        if (e.YouIn) GoalsAfterEvent(e, reached);
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
            Award(PointsPerSeason);
            SettleGoals(rec.Rank);
            SettleSponsor(rec.Rank);
            int next = S.NextId;
            Roster.NewSeason(S.Roster, S.Seed, season, ref next);
            S.NextId = next;
            _byId = null; _ranking = null; _order = null;
            SetGoals();                                        // the new season's goals and offers, from where you finished
            MakeOffers();
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
            switch (reached) { case 0: return "W"; case 1: return "F"; case 2: return "SF"; case 3: return "QF"; case 4: return "R16"; case 5: return "R32"; default: return "R64"; }
        }

        public static string ReachedLong(int reached)
        {
            switch (reached) { case 0: return "Champion"; case 1: return "Runner-up"; case 2: return "Semi-finals"; case 3: return "Quarter-finals"; case 4: return "Round of 16"; case 5: return "Round of 32"; default: return "Round of 64"; }
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
