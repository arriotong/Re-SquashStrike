// ===========================================================
// Assets/SquashSim/Career/Tour.cs
// The tour as data - the PSA's tiers, what they pay, who plays them and
// the season's calendar (2026-10-24: the real PSA system, fictional
// players; was a made-up 80-player, six-tier ladder).
//
// The tiers are the PSA's: the Challenger Tour (3, 6, 9, 15, 20) and the
// World Tour (Copper, Bronze, Silver, Gold, Platinum, Diamond), the World
// Championship and the World Tour Finals. Ranking points by round are
// the PSA's own table (halves rounded up); prize money is the event's
// purse shared in proportion to the points.
//
// The world is ~480 pros. Each month every event takes its field from
// the players who enter (most do, most months), best-ranked first,
// working down from the month's biggest event: the top players fill the
// World Tour events, the rest drop to the Challengers. A tier's BandTop
// keeps the stars out of the small events. You may enter any event your
// ranking opens (UnlockRank); you take the lowest entrant's place.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Career
{
    public enum Tier
    {
        Challenger3, Challenger6, Challenger9, Challenger15, Challenger20,
        Copper, Bronze, Silver, Gold, Platinum, Diamond,
        WorldChampionship, WorldTourFinals
    }

    public sealed class TierInfo
    {
        public Tier Tier;
        public string Name, Short;
        public bool WorldTour;                   // false = the Challenger Tour
        public int Draw;
        public int BandTop;                      // the field comes from this ranking place down
        public int UnlockRank;                   // you may enter at this ranking or better (0 = always, unranked too)
        public int Purse;                        // prize money, US$
        public int[] Points;                     // by round reached: W, F, SF, QF, R16, R32, R64
        public int[] Prize;                      // the purse shared in proportion to the points, same order
    }

    public static class Tour
    {
        public const int RosterSize = 480;
        public const int SlotsPerSeason = 12;
        public const int RankingWindow = 12;     // the ranking: the last 12 months' events...
        public const int MinDivisor = 10;        // ...their points averaged, dividing by at least 10 (the PSA's rule)
        public const double EntryChance = 0.85;  // a pro enters an event this often (rest, injury, travel)

        static TierInfo T(Tier t, string name, string shortName, bool wt, int draw, int bandTop, int unlock, int purse, params int[] points)
        {
            var info = new TierInfo { Tier = t, Name = name, Short = shortName, WorldTour = wt, Draw = draw, BandTop = bandTop, UnlockRank = unlock, Purse = purse, Points = new int[7] };
            for (int i = 0; i < points.Length && i < 7; i++) info.Points[i] = points[i];
            // the purse, shared as the points are: each round's players take points / (all points awarded)
            int total = 0;
            for (int i = 0; i < 7 && (1 << i) <= draw; i++) total += info.Points[i] * (i == 0 ? 1 : 1 << (i - 1));
            info.Prize = new int[7];
            for (int i = 0; i < 7; i++) info.Prize[i] = total == 0 ? 0 : (int)System.Math.Round(purse * (double)info.Points[i] / total / 10.0) * 10;
            return info;
        }

        public static readonly TierInfo[] Tiers =
        {
            //                                                                  draw band unlock  purse     W     F    SF   QF   R16  R32  R64
            T(Tier.Challenger3,  "Challenger 3",  "C3",  false, 32, 200,   0,   3000,   65,   40,   25,  15,   9,   6),
            T(Tier.Challenger6,  "Challenger 6",  "C6",  false, 32, 150, 360,   6000,  100,   65,   40,  25,  15,   9),
            T(Tier.Challenger9,  "Challenger 9",  "C9",  false, 32, 110, 290,   9000,  150,   98,   60,  38,  23,  14),
            T(Tier.Challenger15, "Challenger 15", "C15", false, 32,  80, 230,  15000,  250,  163,  100,  63,  38,  23),
            T(Tier.Challenger20, "Challenger 20", "C20", false, 32,  50, 180,  20000,  350,  230,  140,  85,  51,  32),
            T(Tier.Copper,       "Copper",        "CU",  true,  32,  24, 140,  25000,  500,  325,  200, 125,  75,  46),
            T(Tier.Bronze,       "Bronze",        "BR",  true,  32,  12, 100,  50000,  900,  585,  360, 225, 135,  83),
            T(Tier.Silver,       "Silver",        "SV",  true,  32,   6,  76,  75000, 1350,  878,  540, 338, 203, 124),
            T(Tier.Gold,         "Gold",          "GD",  true,  32,   1,  56, 100000, 1800, 1170,  720, 450, 270, 165),
            T(Tier.Platinum,     "Platinum",      "PT",  true,  32,   1,  40, 190000, 2800, 1820, 1120, 700, 420, 257),
            T(Tier.Diamond,      "Diamond",       "DM",  true,  32,   1,  32, 300000, 3100, 2015, 1240, 775, 465, 284),
            T(Tier.WorldChampionship, "World Championship", "WC", true, 64, 1, 64, 600000, 3500, 2275, 1400, 875, 525, 321, 196),
            T(Tier.WorldTourFinals,   "World Tour Finals",  "WTF", true, 8, 1,  8, 200000, 2000, 1300,  800, 500),
        };

        public static TierInfo Info(Tier t) { return Tiers[(int)t]; }

        /// <summary>The look of a tier, 0..5: the badge colour, the stars, the XP, the achievements
        /// (Challenger 3/6, Challenger 9-20, Copper/Bronze, Silver/Gold, Platinum/Diamond, the World Championship and Finals).</summary>
        public static int Band(Tier t)
        {
            switch (t)
            {
                case Tier.Challenger3: case Tier.Challenger6: return 0;
                case Tier.Challenger9: case Tier.Challenger15: case Tier.Challenger20: return 1;
                case Tier.Copper: case Tier.Bronze: return 2;
                case Tier.Silver: case Tier.Gold: return 3;
                case Tier.Platinum: case Tier.Diamond: return 4;
                default: return 5;
            }
        }
        public static int Band(int t) { return Band((Tier)t); }

        /// <summary>The Challenger Tour, every month: the bottom of the world plays these.</summary>
        static readonly Tier[] Challengers = { Tier.Challenger20, Tier.Challenger15, Tier.Challenger9, Tier.Challenger6, Tier.Challenger6, Tier.Challenger3, Tier.Challenger3, Tier.Challenger3 };

        /// <summary>Each month's World Tour events, biggest first (the Challengers and a Copper follow).</summary>
        static readonly Tier[][] Headline =
        {
            new[] { Tier.Gold, Tier.Bronze },                 // Jan
            new[] { Tier.Platinum, Tier.Silver },             // Feb
            new[] { Tier.Diamond, Tier.Bronze },              // Mar
            new[] { Tier.Gold, Tier.Silver },                 // Apr
            new[] { Tier.Platinum, Tier.Bronze },             // May
            new[] { Tier.Diamond, Tier.Silver },              // Jun
            new[] { Tier.Gold, Tier.Bronze },                 // Jul
            new[] { Tier.Platinum, Tier.Silver },             // Aug
            new[] { Tier.Diamond, Tier.Bronze },              // Sep
            new[] { Tier.Gold, Tier.Silver },                 // Oct
            new[] { Tier.WorldChampionship },                 // Nov
            new[] { Tier.WorldTourFinals, Tier.Silver },      // Dec: the season's top 8 meet
        };

        static readonly Tier[][] Calendar = BuildCalendar();
        static Tier[][] BuildCalendar()
        {
            var cal = new Tier[SlotsPerSeason][];
            for (int m = 0; m < SlotsPerSeason; m++)
            {
                var list = new List<Tier>(Headline[m]);
                list.Add(Tier.Copper);
                list.AddRange(Challengers);
                cal[m] = list.ToArray();
            }
            return cal;
        }

        /// <summary>The calendar: each slot's events, biggest first. The same every season. The same tier can appear more
        /// than once in a month (several Challenger 3s): an event is (month, index).</summary>
        public static Tier[] EventsIn(int slot) { return Calendar[((slot % SlotsPerSeason) + SlotsPerSeason) % SlotsPerSeason]; }

        /// <summary>The month's World Tour headline (for the calendar strip).</summary>
        public static Tier HeadlineOf(int slot) { return EventsIn(slot)[0]; }

        static readonly string[] Cities =
        {
            "Harbourside", "Northgate", "Lakeview", "Westmoor", "Kingsbridge", "Redcliffe", "Eastvale", "Silverton",
            "Marlow Bay", "Ashford", "Stonehaven", "Riverton", "Fairhaven", "Brightwater", "Oakmont", "Highcastle",
            "Port Alder", "Cedar Falls", "Saltmere", "Duneholm", "Glenrock", "Bayreach", "Thornbury", "Larkhill",
            "Kestrel Bay", "Vale Royal", "Mistral", "Sunhaven", "Ironbridge", "Westhaven", "Coral Point", "Elmstead"
        };

        static readonly string[] Sponsors = { "Open", "Classic", "International", "Cup", "Squash Open", "Championships" };

        /// <summary>A name for one of a month's events ("Harbourside Open"), by its index in EventsIn.</summary>
        public static string EventName(int slot, int index)
        {
            var t = EventsIn(slot)[index];
            if (t == Tier.WorldChampionship) return "PSA World Championship";
            if (t == Tier.WorldTourFinals) return "PSA World Tour Finals";
            int m = ((slot % SlotsPerSeason) + SlotsPerSeason) % SlotsPerSeason;
            string city = Cities[(m * 7 + index * 11) % Cities.Length];
            if (!Info(t).WorldTour) return city + " Challenger";
            return city + " " + Sponsors[(m + index * 3) % Sponsors.Length];
        }

        /// <summary>The first index of a tier in this slot's events, or -1.</summary>
        public static int IndexOf(int slot, Tier t) { return System.Array.IndexOf(EventsIn(slot), t); }

        static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        public static string Month(int slot) { return Months[((slot % 12) + 12) % 12]; }

        /// <summary>"Final", "Semi-final", ... for a round with this many players left.</summary>
        public static string RoundName(int playersLeft)
        {
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
    }
}
