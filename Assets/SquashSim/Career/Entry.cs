// ===========================================================
// Assets/SquashSim/Career/Entry.cs
// P7 phase 3 (2026-10-26): getting into events, the PSA's way (Rule Book,
// August 2025, 4.2-4.3 and 5.4.4), and paying to get there.
//
//   - no points gate: the field is filled best-ranked first and you are
//     in when your ranking beats the last direct entrant's ("last in")
//   - wildcards: at your own country's events; 2 in any 52 weeks, 1 of
//     them at Bronze or above; the World Tour's only for your country's
//     top 5 (a top national player)
//   - qualifying: a Diamond's (your section of 8: three wins) and the
//     World Championship's regional qualifiers (16: four wins), for the
//     next ranked players below the cut-off
//   - the Tour Finals: the Road to the Tour Finals' top 8 (World Event
//     points this season)
//   - your home club's open: always there, in your home city
//
// A trip costs the flight from where you are (World.Flight) and the
// hotel nights (none at home); you must have the cash, except to go home
// (your federation flies you if you can't). Prize money, goals' and
// sponsors' money all go to your cash (Earn).
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    public sealed partial class CareerSession
    {
        /// <summary>How you get into an event.</summary>
        public enum Via { None, Direct, Wildcard, Qualifying, Home }

        /// <summary>One of this week's events, for you.</summary>
        public sealed class Option
        {
            public int Index;                    // in Tour.Week (Tour.HomeIndex: your home club's open)
            public Tier Tier;
            public string Name, City;
            public bool Home;
            public Via Via;                      // how you'd get in (None: you can't)
            public int LastIn;                   // the last direct entrant's ranking (int.MaxValue: the field isn't full)
            public int Cost;                     // the trip (flight + hotel), US$
            public string Why;                   // null: you may enter now; else why not
            public bool Open { get { return Why == null; } }
            public TierInfo Info { get { return Home ? Tour.Club : Tour.Info(Tier); } }
        }

        // ---------------- money ----------------

        /// <summary>Money in: your earnings and your cash.</summary>
        void Earn(int amount) { if (amount == 0) return; S.Earnings += amount; S.Cash += amount; }

        /// <summary>A trip from where you are to an event in this city (qualifying: two more nights).</summary>
        public int TripCost(string city, Tier t, bool qualifying = false)
        {
            int flight = World.Flight(S.City, city);
            if (city == World.HomeOf(S.Nat)) return flight;                 // at home: your own bed
            var c = World.CityOf(city);
            int hotel = c != null ? c.Hotel : 150;
            return flight + hotel * (Tour.Nights(t) + (qualifying ? Tour.QualifyingNights : 0));
        }

        // ---------------- the week's options ----------------

        /// <summary>This week's events for you, biggest first, then your home club's open.</summary>
        public List<Option> Options()
        {
            var list = new List<Option>();
            int n = Tour.Week(S.Slot).Count;
            for (int i = 0; i < n; i++) list.Add(OptionAt(i));
            list.Add(HomeOption());
            return list;
        }

        /// <summary>One of this week's options by index (Tour.HomeIndex: the home club's open); null if there is none.</summary>
        public Option OptionAt(int index)
        {
            if (index == Tour.HomeIndex) return HomeOption();
            var ev = Tour.EventAt(S.Slot, index);
            if (ev == null) return null;
            var info = Tour.Info(ev.Tier);
            var o = new Option { Index = index, Tier = ev.Tier, Name = ev.Name, City = ev.City };
            var field = Fields()[index];
            o.LastIn = field.Count < info.Direct ? int.MaxValue : RankOf(field[info.Direct - 1]);
            int mine = RankOf(You);
            if (ev.Tier == Tier.WorldTourFinals)
            {
                var road = RoadTo();
                int at = road.IndexOf(You);
                if (at >= 0 && at < info.Field) o.Via = Via.Direct;
                else o.Why = "the Road to the Tour Finals' top " + info.Field + " only";
            }
            else if (mine != Unranked && mine < o.LastIn || o.LastIn == int.MaxValue) o.Via = Via.Direct;
            else if (WildcardWhy(ev) == null) o.Via = Via.Wildcard;
            else if (Tour.QualifyingField(ev.Tier) > 0 && mine != Unranked && mine < QualifyingLastIn(index)) o.Via = Via.Qualifying;
            else o.Why = mine == Unranked ? "needs a world ranking" : "ranked too low: last in #" + o.LastIn;
            o.Cost = TripCost(ev.City, ev.Tier, o.Via == Via.Qualifying);
            if (o.Why == null && S.Current != null) o.Why = "you are in an event";
            if (o.Why == null && o.Cost > S.Cash) o.Why = "can't afford the trip ($" + o.Cost.ToString("N0") + ")";
            return o;
        }

        Option HomeOption()
        {
            string home = World.HomeOf(S.Nat);
            int cost = TripCost(home, Tier.Challenger3);
            if (cost > S.Cash) cost = 0;                                    // your federation flies you home
            return new Option
            {
                Index = Tour.HomeIndex, Tier = Tier.Challenger3, Name = HomeEventName, City = home, Home = true, Via = Via.Home,
                LastIn = int.MaxValue, Cost = cost, Why = S.Current != null ? "you are in an event" : null,
            };
        }

        /// <summary>The qualifying's last entrant's ranking (int.MaxValue: not full).</summary>
        int QualifyingLastIn(int index)
        {
            Fields();
            var q = _quals[index];
            var ev = Tour.EventAt(S.Slot, index);
            return q == null || q.Count < Tour.QualifyingField(ev.Tier) ? int.MaxValue : RankOf(q[q.Count - 1]);
        }

        /// <summary>Null when you'd get a wildcard into this event, else why not.</summary>
        string WildcardWhy(TourEvent ev)
        {
            var city = World.CityOf(ev.City);
            if (city == null || city.Country != S.Nat) return "wildcards go to the host country's players";
            if (ev.Tier == Tier.WorldTourFinals) return "no wildcards";
            int used = 0; bool big = false;
            foreach (var w in S.Wildcards)
                if (w.Slot > S.Slot - Tour.RankingWindow) { used++; if (w.Tier >= (int)Tier.Bronze) big = true; }
            if (used >= 2) return "no wildcards left (2 in 52 weeks)";
            if (ev.Tier >= Tier.Bronze && big) return "one Bronze-or-above wildcard in 52 weeks";
            if (Tour.Info(ev.Tier).WorldTour && NationalRank() > 5) return "World Tour wildcards go to your country's top 5";
            return null;
        }

        /// <summary>Your place among your country's players (1 = its best); 99 when unranked.</summary>
        public int NationalRank()
        {
            int mine = RankOf(You);
            if (mine == Unranked) return 99;
            int better = 0;
            foreach (var r in Ranking()) { if (r.Rank >= mine) break; if (r.Id != You && Pro(r.Id) != null && Pro(r.Id).Nat == S.Nat) better++; }
            return better + 1;
        }

        /// <summary>The wildcards you may still use in the next 52 weeks (of 2).</summary>
        public int WildcardsLeft
        {
            get { int used = 0; foreach (var w in S.Wildcards) if (w.Slot > S.Slot - Tour.RankingWindow) used++; return Math.Max(0, 2 - used); }
        }

        /// <summary>The Road to the Tour Finals: World Event points this season (not the Finals'), best first (you included).</summary>
        public List<int> RoadTo()
        {
            var pts = new Dictionary<int, int>();
            int from = S.Season * Tour.SlotsPerSeason;
            foreach (var r in S.Results)
            {
                if (r.Slot < from || r.Slot >= S.Slot || r.Tier == (int)Tier.WorldTourFinals || !Tour.Info((Tier)r.Tier).WorldTour) continue;
                if (r.Player != You) { var p = Pro(r.Player); if (p == null || p.Retired) continue; }
                pts.TryGetValue(r.Player, out int v);
                pts[r.Player] = v + r.Points;
            }
            var ids = new List<int>(pts.Keys);
            ids.Sort((a, b) => pts[a] != pts[b] ? pts[b].CompareTo(pts[a]) : RankOf(a) != RankOf(b) ? RankOf(a).CompareTo(RankOf(b)) : a.CompareTo(b));
            return ids;
        }

        /// <summary>Your Road to the Tour Finals points this season.</summary>
        public int RoadPoints()
        {
            int from = S.Season * Tour.SlotsPerSeason, t = 0;
            foreach (var r in S.Results)
                if (r.Player == You && r.Slot >= from && r.Tier != (int)Tier.WorldTourFinals && Tour.Info((Tier)r.Tier).WorldTour) t += r.Points;
            return t;
        }

        // ---------------- entering ----------------

        /// <summary>Null when you may enter the event at this index now, else why not.</summary>
        public string Entry(int index) { var o = OptionAt(index); return o == null ? "no such event" : o.Why; }

        /// <summary>Enter one of this week's events (Entry(index) must be null): pay the trip, travel, and the draw is made
        /// (or the qualifying's, for a Diamond or the Worlds below the cut-off).</summary>
        public EventState Enter(int index)
        {
            var o = OptionAt(index);
            if (o == null || !o.Open) throw new InvalidOperationException("can't enter " + index + ": " + (o == null ? "no such event" : o.Why));
            S.Cash -= o.Cost; S.Spent += o.Cost;
            S.City = o.City;
            EventState e;
            if (o.Home) e = BuildHome();
            else if (o.Via == Via.Qualifying) e = BuildQualifying(index);
            else
            {
                if (o.Via == Via.Wildcard) S.Wildcards.Add(new WildcardRec { Slot = S.Slot, Tier = (int)o.Tier });
                e = Build(index, WeekFields(index)[index], true);
            }
            e.Via = (int)o.Via; e.Cost = o.Cost;
            S.Current = e;
            return e;
        }

        // ---------------- by tier (the tests, the season runner) ----------------

        /// <summary>This week's tiers, biggest first, each once.</summary>
        public Tier[] EventsNow()
        {
            var list = new List<Tier>();
            foreach (var e in Tour.Week(S.Slot)) if (!list.Contains(e.Tier)) list.Add(e.Tier);
            return list.ToArray();
        }

        Option FirstOf(Tier t)
        {
            Option first = null;
            int n = Tour.Week(S.Slot).Count;
            for (int i = 0; i < n; i++)
            {
                if (Tour.EventAt(S.Slot, i).Tier != t) continue;
                var o = OptionAt(i);
                if (o.Open) return o;
                if (first == null) first = o;
            }
            return first;
        }

        public string Entry(Tier t) { var o = FirstOf(t); return o == null ? "not this week" : o.Why; }
        public bool CanEnter(Tier t) { return Entry(t) == null; }
        /// <summary>Enter this week's first open event of a tier.</summary>
        public EventState Enter(Tier t)
        {
            var o = FirstOf(t);
            if (o == null || !o.Open) throw new InvalidOperationException("can't enter " + t + ": " + Entry(t));
            return Enter(o.Index);
        }

        /// <summary>The week's events worth showing you: the best two you may enter, the next one up (a goal), and your
        /// home club's open.</summary>
        public List<Option> EventsForYou()
        {
            var open = new List<Option>();
            Option next = null;
            var all = Options();
            foreach (var o in all)
            {
                if (o.Home) continue;
                if (o.Open) { if (open.Count < 2) open.Add(o); }
                else if (open.Count == 0) next = o;                    // the smallest closed one above the open ones
            }
            var list = new List<Option>();
            if (next != null) list.Add(next);
            list.AddRange(open);
            list.Add(all[all.Count - 1]);
            return list;
        }
    }
}
