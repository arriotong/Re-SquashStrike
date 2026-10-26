// ===========================================================
// Assets/SquashSim/Career/Tour.cs
// The tour as data - the PSA's tiers, what they pay, who plays them and
// the season's calendar.
//
// P7 phase 3 (2026-10-26): the real thing, from the PSA Squash Tour Rule
// Book (August 2025) and the 2024-25 men's World Tour:
//   - the tiers: Challenger 3, 6, 9, 12, 15; Copper, Bronze, Silver,
//     Gold, Platinum, Diamond; the World Championship; the Tour Finals
//   - draws (4.3.1): 24 entries in a 32-draw (the top 8 seeds byes) from
//     Challenger to Gold; Platinum 32; Diamond 48 in a 64-draw (16 byes);
//     the World Championship 64; the Finals the season's top 8
//   - ranking points by round (Appendix IX), prize money the 2025-26
//     minimums (1.3.9), shared by round as the points are
//   - the calendar is weekly (TourCalendar.cs): a slot is a week, a
//     season 52 of them from late July; the Tour Finals in week 46, the
//     off-season after them
//
// The world is ~480 pros. Each week every event takes its field from
// the players who enter, best-ranked first, working down from the week's
// biggest event; a Challenger draws more of its region's players (they
// pay their own travel). You enter by ranking: the field fills above
// you or it doesn't (CareerSession.Entry).
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Career
{
    public enum Tier
    {
        Challenger3, Challenger6, Challenger9, Challenger12, Challenger15,
        Copper, Bronze, Silver, Gold, Platinum, Diamond,
        WorldChampionship, WorldTourFinals
    }

    public sealed class TierInfo
    {
        public Tier Tier;
        public string Name, Short;
        public bool WorldTour;                   // false = the Challenger Tour
        public int Draw;                         // the bracket: 8, 32 or 64
        public int Field;                        // its entries (the rest of the bracket are byes for the top seeds)
        public int Direct;                       // of those, by ranking (the rest: wildcards and qualifiers)
        public int BandTop;                      // the AI's field comes from this ranking place down (the stars skip the small ones)
        public int Purse;                        // prize money, US$ (the 2025-26 minimum)
        public int[] Points;                     // by round reached: W, F, SF, QF, R16, R32, R64
        public int[] Prize;                      // the purse shared in proportion to the points, same order
        /// <summary>How many players go out at each round (W, F, SF, QF, R16, R32, R64): the field, with byes.</summary>
        public int[] OutAt;
    }

    public static partial class Tour
    {
        public const int RosterSize = 480;
        public const int SlotsPerSeason = 52;    // weeks, from late July
        public const int RankingWindow = 52;     // the ranking: the last 52 weeks' events...
        public const int MinDivisor = 10;        // ...their points averaged, over at least 10
        public const double EntryChance = 0.85;  // a pro enters a World Event this often (rest, injury)
        public const int OffSeasonFrom = 47;     // weeks 47..51: no events

        static TierInfo T(Tier t, string name, string shortName, bool wt, int draw, int field, int direct, int bandTop, int purse, params int[] points)
        {
            var info = new TierInfo { Tier = t, Name = name, Short = shortName, WorldTour = wt, Draw = draw, Field = field, Direct = direct, BandTop = bandTop, Purse = purse, Points = new int[7] };
            for (int i = 0; i < points.Length && i < 7; i++) info.Points[i] = points[i];
            // who goes out where: the winner; 1 in the final, 2 in the semis, 4 ... ; the first round's losers are its
            // matches less the byes (a 24-player field in a 32-draw: 8 matches in the first round, 8 byes)
            info.OutAt = new int[7];
            info.OutAt[0] = 1;
            int rounds = 0; while ((1 << rounds) < draw) rounds++;
            for (int k = 1; k <= rounds && k < 7; k++) info.OutAt[k] = k < rounds ? 1 << (k - 1) : (1 << (k - 1)) - (draw - field);
            int total = 0;
            for (int i = 0; i < 7; i++) total += info.Points[i] * info.OutAt[i];
            info.Prize = new int[7];
            for (int i = 0; i < 7; i++) info.Prize[i] = total == 0 ? 0 : (int)System.Math.Round(purse * (double)info.Points[i] / total / 10.0) * 10;
            return info;
        }

        public static readonly TierInfo[] Tiers =
        {
            //                                                               draw field direct band  purse      W     F    SF   QF   R16  R32  R64
            T(Tier.Challenger3,  "Challenger 3",  "C3",  false, 32, 24, 20, 200,   3000,    60,   39,   24,  15,   9,   6),
            T(Tier.Challenger6,  "Challenger 6",  "C6",  false, 32, 24, 20, 150,   6000,   120,   78,   48,  30,  18,  11),
            T(Tier.Challenger9,  "Challenger 9",  "C9",  false, 32, 24, 21, 110,   9000,   180,  117,   72,  45,  27,  17),
            T(Tier.Challenger12, "Challenger 12", "C12", false, 32, 24, 21,  90,  12000,   240,  156,   96,  60,  36,  22),
            T(Tier.Challenger15, "Challenger 15", "C15", false, 32, 24, 21,  70,  15000,   300,  195,  120,  75,  45,  28),
            T(Tier.Copper,       "Copper",        "CU",  true,  32, 24, 22,  24,  27500,   500,  325,  200, 125,  75,  46),
            T(Tier.Bronze,       "Bronze",        "BR",  true,  32, 24, 22,  12,  55000,   900,  585,  360, 225, 135,  83),
            T(Tier.Silver,       "Silver",        "SV",  true,  32, 24, 22,   6,  82500,  1350,  878,  540, 338, 203, 124),
            T(Tier.Gold,         "Gold",          "GD",  true,  32, 24, 22,   1, 110000,  1800, 1170,  720, 450, 270, 165),
            T(Tier.Platinum,     "Platinum",      "PT",  true,  32, 32, 30,   1, 205000,  2800, 1820, 1120, 700, 420, 257),
            T(Tier.Diamond,      "Diamond",       "DM",  true,  64, 48, 42,   1, 325000,  3100, 2015, 1240, 775, 465, 284, 174),
            T(Tier.WorldChampionship, "World Championship", "WC", true, 64, 64, 57, 1, 650000, 3500, 2275, 1400, 875, 525, 321, 196),
            T(Tier.WorldTourFinals,   "Tour Finals",  "TF",  true,   8,  8,  8,   1, 325000,  1000,  550,  200, 150),
        };

        public static TierInfo Info(Tier t) { return Tiers[(int)t]; }

        // ---------------- P7: your home club's open, money, qualifying ----------------

        /// <summary>Your home club's open: there every week, in your home city - the way in for the unranked and the
        /// broke (no flight, no hotel). Challenger 3 points, a 16-draw, a small purse. Not a calendar event: only you
        /// (and the local pros you meet there) play it.</summary>
        public static readonly TierInfo Club = T(Tier.Challenger3, "Club Open", "CLUB", false, 16, 16, 16, 0, 2000, 60, 39, 24, 15, 9);
        /// <summary>The home club's open's index in a week (past the calendar's events).</summary>
        public const int HomeIndex = 15;

        public static TierInfo InfoOf(EventState e) { return e != null && e.Home ? Club : Info((Tier)e.Tier); }

        /// <summary>What a new career starts with, US$.</summary>
        public const int StartCash = 5000;

        /// <summary>Hotel nights a trip to an event takes (more for the bigger draws; 2 more to play qualifying).</summary>
        public static int Nights(Tier t) { return t <= Tier.Challenger15 ? 2 : t >= Tier.Diamond ? 5 : 4; }
        public const int QualifyingNights = 2;

        /// <summary>The qualifying event for the main draw's last places (Rule Book 4.3): a Diamond's 32 players for 4
        /// places (you play your section of 8: three wins), the World Championship's regional qualifiers (16, one place:
        /// four wins). 0: none.</summary>
        public static int QualifyingDraw(Tier t) { return t == Tier.Diamond ? 8 : t == Tier.WorldChampionship ? 16 : 0; }
        /// <summary>How many play the qualifying (all of its sections): its cut-off.</summary>
        public static int QualifyingField(Tier t) { return t == Tier.Diamond ? 32 : t == Tier.WorldChampionship ? 80 : 0; }

        /// <summary>About where a tier's direct entry closes in an ordinary week (the goals' and ladder's guide; the real
        /// cut-off is each week's own: CareerSession.Options).</summary>
        public static int TypicalLastIn(Tier t)
        {
            var i = Info(t);
            if (t == Tier.WorldTourFinals) return 8;
            return i.BandTop - 1 + (int)System.Math.Round(i.Direct / EntryChance);
        }

        /// <summary>The look of a tier, 0..5: the badge colour, the stars, the XP, the achievements
        /// (Challenger 3/6, Challenger 9-15, Copper/Bronze, Silver/Gold, Platinum/Diamond, the World Championship and Finals).</summary>
        public static int Band(Tier t)
        {
            switch (t)
            {
                case Tier.Challenger3: case Tier.Challenger6: return 0;
                case Tier.Challenger9: case Tier.Challenger12: case Tier.Challenger15: return 1;
                case Tier.Copper: case Tier.Bronze: return 2;
                case Tier.Silver: case Tier.Gold: return 3;
                case Tier.Platinum: case Tier.Diamond: return 4;
                default: return 5;
            }
        }
        public static int Band(int t) { return Band((Tier)t); }

        // ---------------- the calendar (TourCalendar.cs: the events themselves) ----------------

        static List<TourEvent>[] _byWeek;
        static List<TourEvent>[] ByWeek
        {
            get
            {
                if (_byWeek != null) return _byWeek;
                var w = new List<TourEvent>[SlotsPerSeason];
                for (int i = 0; i < w.Length; i++) w[i] = new List<TourEvent>();
                foreach (var e in Calendar) if (e.Week >= 0 && e.Week < SlotsPerSeason) w[e.Week].Add(e);
                foreach (var l in w) l.Sort((a, b) => a.Tier != b.Tier ? ((int)b.Tier).CompareTo((int)a.Tier) : string.CompareOrdinal(a.Name, b.Name));
                return _byWeek = w;
            }
        }

        public static int WeekOf(int slot) { return ((slot % SlotsPerSeason) + SlotsPerSeason) % SlotsPerSeason; }

        /// <summary>This slot's (week's) events, biggest first.</summary>
        public static List<TourEvent> Week(int slot) { return ByWeek[WeekOf(slot)]; }

        /// <summary>The week's tiers, biggest first (an event is (slot, index)).</summary>
        public static Tier[] EventsIn(int slot) { var w = Week(slot); var t = new Tier[w.Count]; for (int i = 0; i < t.Length; i++) t[i] = w[i].Tier; return t; }

        public static TourEvent EventAt(int slot, int index) { var w = Week(slot); return index >= 0 && index < w.Count ? w[index] : null; }

        /// <summary>The week's headline (for the calendar strip); Challenger3 in an empty week.</summary>
        public static Tier HeadlineOf(int slot) { var w = Week(slot); return w.Count > 0 ? w[0].Tier : Tier.Challenger3; }

        public static string EventName(int slot, int index) { var e = EventAt(slot, index); return e != null ? e.Name : "?"; }
        public static string EventCity(int slot, int index) { var e = EventAt(slot, index); return e != null ? e.City : ""; }

        /// <summary>The first index of a tier in this week's events, or -1.</summary>
        public static int IndexOf(int slot, Tier t) { return System.Array.IndexOf(EventsIn(slot), t); }

        /// <summary>The season's events of a tier (the calendar's count: 39 graded, 77 Challengers).</summary>
        public static int CountOf(Tier t) { int n = 0; foreach (var e in Calendar) if (e.Tier == t) n++; return n; }

        static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        /// <summary>The month a week falls in ("Sep"): the season starts in the last days of July.</summary>
        public static string Month(int slot) { return Months[MonthIndex(slot)]; }
        public static int MonthIndex(int slot)
        {
            int d = 28 + WeekOf(slot) * 7 + 3;                                  // days from 1 July to the week's middle (week 0: 29 Jul)
            int[] len = { 31, 31, 30, 31, 30, 31, 31, 28, 31, 30, 31, 30 };     // Jul .. Jun
            int m = 0;
            while (m < 11 && d >= len[m]) { d -= len[m]; m++; }
            return (6 + m) % 12;
        }
        /// <summary>"Wk 9 · Sep".</summary>
        public static string WeekLabel(int slot) { return "Wk " + (WeekOf(slot) + 1) + " · " + Month(slot); }

        /// <summary>"Final", "Semi-final", ... for a round with this many players left.</summary>
        public static string RoundName(int playersLeft)
        {
            if (playersLeft < 0) return "Group match " + (-playersLeft);      // the Tour Finals' round robin (P7 phase 6)
            switch (playersLeft)
            {
                case 2: return "Final";
                case 4: return "Semi-final";
                case 8: return "Quarter-final";
                default: return "Round of " + playersLeft;
            }
        }

        /// <summary>Index into Points / Prize for a player who went out with this many left (1 = won it).</summary>
        public static int ReachedIndex(int playersLeftWhenOut)
        {
            int i = 0, n = 1;
            while (n < playersLeftWhenOut) { n *= 2; i++; }
            return i;
        }

        /// <summary>The ranking's counting events (Rule Book 7.4): of 15 played, 11 count ... of 24, 20; one more for each above;
        /// below 15, all of them.</summary>
        public static int Counting(int played)
        {
            if (played < 15) return played;
            int[] table = { 11, 12, 12, 13, 13, 14, 15, 16, 17, 20 };          // 15..24
            return played <= 24 ? table[played - 15] : 20 + (played - 24);
        }
    }
}
