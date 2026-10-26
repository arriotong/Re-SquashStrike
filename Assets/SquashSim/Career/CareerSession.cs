// ===========================================================
// Assets/SquashSim/Career/CareerSession.cs
// P2-M4: the Career's rules, on a CareerState.
//
//   New(...)            a new career: the roster, then the tour's year
//                       before you joined, played out in the background
//   Ranking()           the ranked players, best first: the PSA's rolling
//                       12 months, points averaged over the events played
//                       (dividing by at least 10). No results = unranked.
//   Options()           this week's events for you: how you'd get in (by
//                       ranking, a wildcard, qualifying), the trip's cost
//                       (Entry.cs); your home club's open is always there
//   Enter(index)        build this week's draw with you in it (the week's
//                       fields are filled biggest event first: WeekFields)
//   NextOpponent        your next opponent, or -1
//   RecordYours(...)    your match is done (played, simulated, retired):
//                       the rest of the round is played out
//   SimulateYours()     let the rally model play your match
//   FinishSlot()        play out your event and the week's others, award
//                       points and prize money, move to the next week
//                       (and at the end of the year, the next season)
//
// P7 phase 3: a slot is a week of the real calendar (Tour.Calendar);
// draws have byes (a 24-player field in a 32-draw); the ranking counts
// your best events as the PSA does (Tour.Counting).
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
            var s = new CareerState { Name = name, Nat = nat, Seed = seed, GamesToWin = gamesToWin, English = english, Season = 0, Week = 0 };
            s.Attributes = new AthleteAttributes { Speed = StartStat, Stamina = StartStat, Power = StartStat, Control = StartStat };   // P7: you start weaker
            s.Roster = Roster.Generate(seed);
            var c = new CareerSession(s);
            for (int m = 0; m < Tour.SlotsPerSeason; m++) c.FinishSlot();
            s.Seasons.Clear();
            s.SeasonJustEnded = false;
            s.TrainingPoints = 0; s.PointsEarned = 0; s.Xp = 0; // the year before you joined earns you nothing
            s.Earnings = 0; s.GoalsDone = 0; s.SponsorPaid = 0; s.SponsorNews = null; s.Sponsor = null;
            s.Cash = Tour.StartCash; s.Spent = 0; s.City = World.HomeOf(nat); s.Wildcards = new List<WildcardRec>();   // P7: you start at home
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
            if (S.Wildcards == null) S.Wildcards = new List<WildcardRec>();
            if (S.GearOwned == null) S.GearOwned = new List<string>();
            if (S.Trophies == null) S.Trophies = new List<TrophyRec>();
            if (string.IsNullOrEmpty(S.City)) S.City = World.HomeOf(S.Nat);
            if (S.Version < CareerState.CurrentVersion) S.Version = CareerState.CurrentVersion;     // v2 -> v3: the defaults fill in
        }

        // ---------------- players ----------------

        Dictionary<int, Pro> _byId;
        public Pro Pro(int id)
        {
            if (_byId == null || _byId.Count != S.Roster.Count) { _byId = new Dictionary<int, Pro>(); foreach (var p in S.Roster) _byId[p.Id] = p; }
            return _byId.TryGetValue(id, out var x) ? x : null;
        }

        /// <summary>A bye in a draw (a top seed's empty first round).</summary>
        public const int Bye = -1;

        public string NameOf(int id) { return id == You ? S.Name : id == Bye ? "BYE" : (Pro(id) != null ? Pro(id).Name : "?"); }
        public string BugOf(int id) { return id == You ? (S.Name ?? "YOU").ToUpperInvariant() : id == Bye ? "BYE" : (Pro(id) != null ? Pro(id).Bug : "?"); }
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
            var played = new Dictionary<int, List<int>>();
            int from = slot - Tour.RankingWindow;
            foreach (var r in S.Results)
            {
                if (r.Slot < from || r.Slot >= slot) continue;
                if (r.Player != You) { var p = Pro(r.Player); if (p == null || p.Retired) continue; }
                if (!rows.TryGetValue(r.Player, out var row)) { rows[r.Player] = row = new Row { Id = r.Player }; played[r.Player] = new List<int>(); }
                if (r.Tier == (int)Tier.WorldTourFinals) row.Total += r.Points;   // the Finals: on top, not an event played (7.3)
                else { played[r.Player].Add(r.Points); row.Events++; }
                if (r.Slot > row.LastSlot) row.LastSlot = r.Slot;
            }
            var list = new List<Row>();
            foreach (var row in rows.Values)
            {
                // P7: the counting events (7.4) - the best of them; the average over at least MinDivisor
                var pts = played[row.Id];
                pts.Sort((a, b) => b.CompareTo(a));
                int counting = Tour.Counting(pts.Count);
                for (int i = 0; i < counting; i++) row.Total += pts[i];
                if (row.Total <= 0) continue;
                row.Average = (double)row.Total / System.Math.Max(counting, Tour.MinDivisor);
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

        /// <summary>Places moved since the start of last week: positive = up. 0 when new to the list.</summary>
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

        // ---------------- the week's fields ----------------

        /// <summary>Does this pro enter an event this week? (Most do; the same answer all week.)</summary>
        bool Enters(int id) { return CareerRng.Mix(S.Seed ^ 0x5EA5u, S.Slot, id) % 1000u < (uint)(Tour.EntryChance * 1000); }

        string RegionOfPro(int id) { var p = Pro(id); var k = p != null ? World.CountryOf(p.Nat) : null; return k != null ? k.Region : ""; }

        List<int>[] _fields, _quals;
        Dictionary<int, int> _pos;
        int _fieldsAt = int.MinValue, _fieldsResults = -1, _fieldsRoster = -1;

        /// <summary>Every calendar event's field this week without you, by its index in Tour.Week: filled biggest event
        /// first, each from its band down, best-ranked entrants first; nobody plays two. A Challenger fills from its own
        /// region first (they pay their own travel). The Tour Finals: the Road's top 8. Sorted best first.</summary>
        List<int>[] Fields()
        {
            if (_fields != null && _fieldsAt == S.Slot && _fieldsResults == S.Results.Count && _fieldsRoster == S.Roster.Count) return _fields;
            var week = Tour.Week(S.Slot);
            var fields = new List<int>[week.Count];
            var quals = new List<int>[week.Count];
            var order = ProOrder();
            var pos = new Dictionary<int, int>();
            for (int i = 0; i < order.Count; i++) pos[order[i]] = i;
            var used = new HashSet<int>();
            for (int i = 0; i < week.Count; i++)                          // Tour.Week: biggest first
            {
                var ev = week[i];
                var info = Tour.Info(ev.Tier);
                var field = new List<int>();
                var inField = new HashSet<int>();
                if (ev.Tier == Tier.WorldTourFinals)
                {
                    foreach (int id in RoadTo()) if (id != You && field.Count < info.Field) { field.Add(id); inField.Add(id); }
                }
                else
                {
                    bool everyone = ev.Tier == Tier.WorldChampionship;           // nobody misses the Worlds
                    string region = World.RegionOf(World.CityOf(ev.City));
                    int passes = info.WorldTour ? 2 : 3;                         // a Challenger: its region, then the rest, then anyone
                    for (int pass = 0; pass < passes && field.Count < info.Field; pass++)
                        for (int place = info.BandTop - 1; place < order.Count && field.Count < info.Field; place++)
                        {
                            int id = order[place];
                            if (used.Contains(id) || inField.Contains(id)) continue;
                            if (pass < passes - 1 && !everyone && !Enters(id)) continue;
                            if (!info.WorldTour && pass == 0 && RegionOfPro(id) != region) continue;
                            field.Add(id); inField.Add(id);
                        }
                }
                field.Sort((a, b) => pos[a].CompareTo(pos[b]));
                foreach (int id in field) used.Add(id);
                fields[i] = field;
                int qn = Tour.QualifyingField(ev.Tier);
                if (qn > 0)
                {
                    // the qualifying: the next entrants down (it is played before the week's other events: its players may play them too)
                    var q = new List<int>();
                    int start = field.Count > 0 ? pos[field[field.Count - 1]] + 1 : 0;
                    for (int place = start; place < order.Count && q.Count < qn; place++)
                    {
                        int id = order[place];
                        if (used.Contains(id) || !Enters(id)) continue;
                        q.Add(id);
                    }
                    quals[i] = q;
                }
            }
            _fields = fields; _quals = quals; _pos = pos;
            _fieldsAt = S.Slot; _fieldsResults = S.Results.Count; _fieldsRoster = S.Roster.Count;
            return fields;
        }

        /// <summary>This week's fields, with you in the event at <paramref name="yours"/> (-1: none) in place of its lowest entrant.</summary>
        List<int>[] WeekFields(int yours)
        {
            var f = Fields();
            var copy = new List<int>[f.Length];
            for (int i = 0; i < f.Length; i++) copy[i] = new List<int>(f[i]);
            if (yours >= 0 && yours < copy.Length) InsertYou(copy[yours], Tour.Info(Tour.EventAt(S.Slot, yours).Tier).Field);
            return copy;
        }

        /// <summary>You into a field (best first), in place of its lowest entrant when it is full.</summary>
        void InsertYou(List<int> field, int size)
        {
            if (field.Count >= size && field.Count > 0) field.RemoveAt(field.Count - 1);
            int at = 0, mine = RankOf(You);
            while (at < field.Count && RankOf(field[at]) < mine) at++;
            field.Insert(at, You);
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

        /// <summary>A draw (the field best first) in the smallest bracket that holds it: a quarter of the bracket seeded
        /// (1 and 2 at the ends, 3-4, 5-8, 9-16 drawn into their places), the rest by lot; the empty places are byes for
        /// the top seeds (4.3.1: a 24-player field in a 32-draw, seeds 1-8 straight into the second round).</summary>
        RoundRec Draw(List<int> field, CareerRng rng)
        {
            int n = field.Count;
            if (n < 2) { var single = new RoundRec(); single.Players.AddRange(field); return single; }
            int size = 2;
            while (size < n) size *= 2;
            var order = SeedOrder(size);
            int seeds = System.Math.Min(n, System.Math.Max(2, size / 4));
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
            // seed k meets size + 1 - k in the first round: the places numbered past the field are the top seeds' byes
            var slots = new int[size];
            int r = 0;
            for (int i = 0; i < size; i++) slots[i] = order[i] > n ? Bye : order[i] <= seeds ? bySeed[order[i]] : rest[r++];
            var round = new RoundRec();
            round.Players.AddRange(slots);
            return round;
        }

        EventState Build(int index, List<int> field, bool withYou)
        {
            var ev = Tour.EventAt(S.Slot, index);
            if (ev.Tier == Tier.WorldTourFinals && field.Count == 8) return BuildRoundRobin(index, field, withYou);   // P7 phase 6
            var e = new EventState { Slot = S.Slot, Tier = (int)ev.Tier, Index = index, Name = ev.Name, City = ev.City, YouIn = withYou };
            e.Rounds.Add(Draw(field, new CareerRng(S.Seed, S.Slot * 16 + index, 999)));
            if (withYou) ByeThrough(e);
            return e;
        }

        /// <summary>A bye for you (a top seed): straight through to the next round (the round's other matches are played).</summary>
        void ByeThrough(EventState e)
        {
            var r = Open(e);
            int i = r.Players.IndexOf(You);
            if (i >= 0 && r.Players[i ^ 1] == Bye) PlayRound(e, You, "");
        }

        /// <summary>Your home club's open: 15 local pros (your country's first, then your region's) from the lower part of
        /// the list, not playing elsewhere this week, and you.</summary>
        EventState BuildHome()
        {
            var order = ProOrder();
            var used = new HashSet<int>();
            foreach (var f in Fields()) foreach (int id in f) used.Add(id);
            var homeRegion = World.CountryOf(S.Nat) != null ? World.CountryOf(S.Nat).Region : "";
            var field = new List<int>();
            int from = (int)(order.Count * 0.6);
            for (int pass = 0; pass < 3 && field.Count < Tour.Club.Field - 1; pass++)
                for (int place = from; place < order.Count && field.Count < Tour.Club.Field - 1; place++)
                {
                    int id = order[place];
                    if (used.Contains(id) || field.Contains(id)) continue;
                    var p = Pro(id);
                    if (pass == 0 && p.Nat != S.Nat) continue;
                    if (pass == 1 && RegionOfPro(id) != homeRegion) continue;
                    field.Add(id);
                }
            var pos = _pos;
            field.Sort((a, b) => pos[a].CompareTo(pos[b]));
            InsertYou(field, Tour.Club.Field);
            var e = new EventState { Slot = S.Slot, Tier = (int)Tier.Challenger3, Index = Tour.HomeIndex, Name = HomeEventName, City = World.HomeOf(S.Nat), Home = true, YouIn = true };
            e.Rounds.Add(Draw(field, new CareerRng(S.Seed, S.Slot * 16 + Tour.HomeIndex, 999)));
            ByeThrough(e);
            return e;
        }

        /// <summary>"Cairo Club Open".</summary>
        public string HomeEventName { get { return World.HomeOf(S.Nat) + " Club Open"; } }

        /// <summary>The qualifying for the event at index: your section (8 for a Diamond, 16 for the Worlds - your region's
        /// players first), drawn from the qualifying's entrants. Win it and you are in the main draw.</summary>
        EventState BuildQualifying(int index)
        {
            Fields();
            var ev = Tour.EventAt(S.Slot, index);
            var pool = new List<int>(_quals[index] ?? new List<int>());
            var rng = new CareerRng(S.Seed, S.Slot * 16 + index, 777);
            for (int i = pool.Count - 1; i > 0; i--) { int j = rng.Range(i + 1); int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp; }
            if (ev.Tier == Tier.WorldChampionship)
            {
                var mine = World.CountryOf(S.Nat) != null ? World.CountryOf(S.Nat).Region : "";
                var local = pool.FindAll(id => RegionOfPro(id) == mine);
                pool.RemoveAll(id => RegionOfPro(id) == mine);
                local.AddRange(pool);
                pool = local;
            }
            int size = Tour.QualifyingDraw(ev.Tier);
            var section = pool.GetRange(0, System.Math.Min(size - 1, pool.Count));
            var pos = _pos;
            section.Sort((a, b) => pos[a].CompareTo(pos[b]));
            InsertYou(section, size);
            var e = new EventState { Slot = S.Slot, Tier = (int)ev.Tier, Index = index, Name = ev.Name + " Qualifying", City = ev.City, Qualifying = true, YouIn = true };
            e.Rounds.Add(Draw(section, new CareerRng(S.Seed, S.Slot * 16 + index, 777)));
            return e;
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
        public int PlayersLeft
        {
            get
            {
                if (S.Current == null) return 0;
                if (S.Current.RoundRobin && S.Current.Rounds.Count <= GroupDays) return -S.Current.Rounds.Count;   // "Group match 2"
                return Open(S.Current).Players.Count;
            }
        }

        /// <summary>Your match is over. Played matches move your rating; then the rest of the round is played out.</summary>
        public void RecordYours(bool won, string score, int ralliesWon = 0, int ralliesTotal = 0, bool simulated = false, bool retired = false)
        {
            int opp = NextOpponent();
            if (opp < 0) throw new InvalidOperationException("no match to record");
            var e = S.Current;
            if (!simulated && !retired && ralliesTotal > 0) S.Rating = RallyModel.Rate(S.Rating, Pro(opp).Strength, ralliesWon, ralliesTotal);
            S.Matches.Add(new MatchRec
            {
                Slot = S.Slot, Tier = e.Tier, Left = PlayersLeft, Event = e.Name, Opp = opp, OppRank = RankOf(opp),
                Won = won, Simulated = simulated, Retired = retired, Qual = e.Qualifying, Score = retired ? "retired" : score
            });
            bool group = e.RoundRobin && e.Rounds.Count <= GroupDays;          // a group match lost isn't out (NextRoundRobin decides)
            PlayRound(e, won ? You : opp, score);
            if (!won && !group) e.YouOut = true;
            else GoalsAfterWin(S.Matches[S.Matches.Count - 1].OppRank);
            if (!retired) GainXp(MatchXp(e.Tier, ralliesTotal, ralliesWon, won, simulated));   // P7 phase 2: every match grows you
            if (e.Qualifying && e.Done && Winner(e) == You)
            {
                // P7: through qualifying - into the main draw, in place of its lowest entrant
                var main = Build(e.Index, WeekFields(e.Index)[e.Index], true);
                main.Via = e.Via; main.Cost = e.Cost;
                S.Current = main;
            }
        }

        /// <summary>The rally model plays your match (at your rating).</summary>
        public RallyModel.Result SimulateYours()
        {
            int opp = NextOpponent();
            if (opp < 0) throw new InvalidOperationException("no match to simulate");
            var rng = new CareerRng(S.Seed, S.Slot * 16 + S.Current.Index, 5000 + PlayersLeft + (S.Current.Qualifying ? 100 : 0));
            var res = RallyModel.Play(RallyModel.YouRally(S.Rating + AttributeEdge, Pro(opp).Strength), GamesToWinNow, S.English, rng);
            RecordYours(res.AWon, res.Score, res.RalliesA, res.RalliesA + res.RalliesB, simulated: true);
            return res;
        }

        /// <summary>Play the open round (your match, if any, already decided as yourWinner), then open the next.</summary>
        void PlayRound(EventState e, int yourWinner, string yourScore)
        {
            var r = Open(e);
            int round = e.Rounds.Count - 1;
            int stream = e.Qualifying ? 1000 : 0;
            for (int m = 0; m * 2 + 1 < r.Players.Count; m++)
            {
                int a = r.Players[m * 2], b = r.Players[m * 2 + 1];
                if (a == Bye || b == Bye) { r.Winners.Add(a == Bye ? b : a); r.Scores.Add(""); continue; }
                if (a == You || b == You)
                {
                    r.Winners.Add(yourWinner);
                    r.Scores.Add(a == You ? yourScore : Flip(yourScore));
                    continue;
                }
                var rng = new CareerRng(S.Seed, S.Slot * 16 + e.Index, stream + round * 64 + m);
                int toWin = e.RoundRobin && e.Rounds.Count == GroupDays + 2 ? System.Math.Min(3, S.GamesToWin + 1) : S.GamesToWin;   // the Finals' final
                var res = RallyModel.Play(RallyModel.ProRally(Pro(a).Strength, Pro(b).Strength), toWin, S.English, rng);
                r.Winners.Add(res.AWon ? a : b);
                r.Scores.Add(res.Score);
            }
            if (e.RoundRobin) { NextRoundRobin(e); return; }
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
            if (e.RoundRobin) return ReachedRoundRobin(e, id);
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

        /// <summary>True when the week can end (you are not in an event, or your part in it is over).</summary>
        public bool CanFinishSlot { get { return S.Current == null || S.Current.Done || S.Current.YouOut; } }

        /// <summary>Not entering anything this week.</summary>
        public void Skip() { if (S.Current == null) FinishSlot(); }

        // ---------------- P7 phase 2: levels and four stats ----------------
        // The user (2026-10-26): "start is everything -20% of the quick play character and maxed out is +20%", "each level up
        // gives you one stat point". Stats run 0..10, 5 being the Quick Match player (AthleteAttributes: 4% a step); a new
        // career starts at 1 in each; career XP from every match (more at the bigger events) makes levels; each level is a
        // point; a point raises a stat by one. 36 points take you from the start to everything at 10.

        public const int StartStat = 1;
        public const int TrainingWeekXp = 300, SeasonXp = 300, TitleXpBase = 150, GoalXpPerPoint = 120;
        public const int RallyXp = 2, RallyWonXp = 3, WinXp = 60;
        /// <summary>XP by the event's band (Challenger 3/6 .. the majors): the bigger the stage, the more you learn.</summary>
        public static readonly double[] BandXp = { 1.0, 1.15, 1.35, 1.6, 2.0, 2.5 };
        /// <summary>The points that take every stat from StartStat to the top: no more points past them.</summary>
        public static int MaxPoints { get { return 4 * (int)(AthleteAttributes.Max - StartStat); } }

        /// <summary>XP from level to level + 1.</summary>
        public static int XpNeed(int level) { return 1000 + 230 * (level - 1); }     // P7 phase 3: a season is ~130 matches now (weekly)
        public static int XpFor(int level) { int t = 0; for (int l = 1; l < level; l++) t += XpNeed(l); return t; }
        public static int LevelOf(int xp) { int l = 1; while (xp >= XpFor(l + 1)) l++; return l; }
        public int Level { get { return LevelOf(S.Xp); } }
        /// <summary>0..1 into the current level.</summary>
        public float LevelProgress { get { int l = Level; return (float)(S.Xp - XpFor(l)) / XpNeed(l); } }

        /// <summary>A match's XP: its rallies (played, won) and the win, by the event's band; a simulated one 60%.</summary>
        public static int MatchXp(int tier, int rallies, int ralliesWon, bool won, bool simulated)
        {
            double xp = (RallyXp * rallies + RallyWonXp * ralliesWon + (won ? WinXp : 0)) * BandXp[System.Math.Max(0, System.Math.Min(BandXp.Length - 1, Tour.Band(tier)))];
            return (int)System.Math.Round(simulated ? xp * 0.6 : xp);
        }

        /// <summary>XP in: each level reached gives a stat point (until every stat could be at the top).</summary>
        public void GainXp(int xp)
        {
            if (xp <= 0) return;
            int before = Level;
            S.Xp += xp;
            int levels = Level - before;
            int room = MaxPoints - S.PointsEarned;
            int points = System.Math.Max(0, System.Math.Min(levels, room));
            S.TrainingPoints += points; S.PointsEarned += points;
        }

        /// <summary>A title's XP: TitleXpBase by the band.</summary>
        public static int TitleXp(int tier) { return (int)System.Math.Round(TitleXpBase * BandXp[System.Math.Max(0, System.Math.Min(BandXp.Length - 1, Tour.Band(tier)))]); }

        /// <summary>Your stats' edge in a simulated match (a rating bonus: 0.025 per point of the mean away from 5).</summary>
        public double AttributeEdge { get { return S.Attributes == null ? 0 : System.Math.Round(0.025 * (Effective.Mean - AthleteAttributes.Neutral), 4); } }   // P7: with your gear

        /// <summary>A week on the training court instead of an event: the events go on without you, you gain XP.</summary>
        public void Train()
        {
            if (S.Current != null) return;
            GainXp(TrainingWeekXp);
            S.MonthsTrained++;
            S.LastTrainedSlot = S.Slot;
            FinishSlot();
        }

        /// <summary>P7 phase 2: a point raises a stat by one, at every level (levels are the pace now).</summary>
        public static int CostToRaise(double v) { return 1; }

        /// <summary>Can this attribute be raised now (points enough, not at the top)?</summary>
        public bool CanRaise(string attribute)
        {
            double v = Get(S.Attributes, attribute);
            return v >= 0 && v < AthleteAttributes.Max && S.TrainingPoints >= CostToRaise(v);
        }

        /// <summary>Raise an attribute ("Speed", "Stamina", "Power", "Control") by one, for its cost. False when it can't.</summary>
        public bool Spend(string attribute)
        {
            if (!CanRaise(attribute)) return false;
            var a = S.Attributes;
            double v = Get(a, attribute);
            S.TrainingPoints -= CostToRaise(v);
            Set(a, attribute, v + 1);
            return true;
        }

        public static readonly string[] AttributeNames = { "Speed", "Power", "Control", "Stamina" };
        /// <summary>What the game calls them ("Control" is Accuracy on screen).</summary>
        public static string DisplayName(string attribute) { return attribute == "Control" ? "Accuracy" : attribute; }

        public static double Get(AthleteAttributes a, string name)
        {
            switch (name) { case "Speed": return a.Speed; case "Stamina": return a.Stamina; case "Power": return a.Power; case "Control": return a.Control; default: return -1; }
        }

        static void Set(AthleteAttributes a, string name, double v)
        {
            switch (name) { case "Speed": a.Speed = v; break; case "Stamina": a.Stamina = v; break; case "Power": a.Power = v; break; case "Control": a.Control = v; break; }
        }

        /// <summary>End the week: every event in it played out, points and prize money awarded; on to the next.</summary>
        public void FinishSlot()
        {
            if (!CanFinishSlot) throw new InvalidOperationException("your match first");
            var events = new List<EventState>();
            var cur = S.Current;
            int yours = cur != null && !cur.Home && !cur.Qualifying ? cur.Index : -1;
            var fields = WeekFields(yours);
            for (int i = 0; i < fields.Length; i++)
            {
                if (i == yours) { PlayOut(cur); events.Add(cur); continue; }
                if (fields[i].Count < 2) continue;
                var e = Build(i, fields[i], false);
                PlayOut(e);
                events.Add(e);
            }
            if (cur != null && (cur.Home || cur.Qualifying))
            {
                PlayOut(cur);
                if (cur.Home) events.Add(cur);                         // the qualifying: no points, no prize
            }
            foreach (var e in events)
            {
                var info = Tour.InfoOf(e);
                foreach (int id in e.Rounds[0].Players)
                {
                    if (id == Bye) continue;
                    int reached = Reached(e, id);
                    int prize = id == You ? info.Prize[reached] : 0;
                    int points = e.RoundRobin ? RoundRobinPoints(e, id, reached) : info.Points[reached];
                    S.Results.Add(new ResultRec { Player = id, Slot = S.Slot, Tier = e.Tier, Reached = reached, Points = points, Prize = prize });
                    if (id == You)
                    {
                        e.YouReached = reached; e.YouPoints = points; e.YouPrize = prize;
                        Earn(prize);
                        if (reached == 0) { S.Titles++; GainXp(TitleXp(e.Tier)); SponsorTitle(); AddTrophy(e); }
                        if (e.YouIn) GoalsAfterEvent(e, reached);
                    }
                }
            }
            S.Last = cur;
            S.Current = null;
            Prune();
            S.Week++;
            if (S.Week >= Tour.SlotsPerSeason) EndSeason();
        }

        void Prune()
        {
            int keep = S.Slot - 2 * Tour.RankingWindow;
            S.Results.RemoveAll(r => r.Slot < keep && r.Player != You);
        }

        void EndSeason()
        {
            S.Week = 0;
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
                if (r.Player == You && r.Slot / Tour.SlotsPerSeason == season) { if (r.Reached == 0) rec.Titles++; rec.Earnings += r.Prize; }
            S.Seasons.Add(rec);
            S.SeasonJustEnded = true;
            GainXp(SeasonXp);
            SettleGoals(rec.Rank);
            SettleSponsor(rec.Rank);
            int next = S.NextId;
            Roster.NewSeason(S.Roster, S.Seed, season, ref next);
            S.NextId = next;
            _byId = null; _ranking = null; _order = null; _fields = null;
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

        /// <summary>The finish in words, for this event (the Tour Finals' group: "Group stage").</summary>
        public static string ReachedLong(EventState e, int reached) { return e != null && e.RoundRobin && reached == 3 ? "Group stage" : ReachedLong(reached); }
        public static string ReachedShort(int tier, int reached) { return tier == (int)Tier.WorldTourFinals && reached == 3 ? "GRP" : ReachedShort(reached); }

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
            foreach (var m in S.Matches) if (m.Opp == opp) { if (!m.Won) beat++; if (m.Left == 2 && !m.Qual) finals++; }
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
