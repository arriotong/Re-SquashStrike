// ===========================================================
// Assets/SquashSim/Career/Tour.cs
// P2-M4: the tour as data - the tiers, what they pay, who plays them
// and the season's calendar.
//
// The roster is ranked 1..80. Each tier draws its field from a band of
// ranking places, filled from the bottom of the band up, so a Club
// event is the 16 lowest-ranked pros and a Tour event the top 32:
//
//   Club 65-80   Regional 49-64   Challenger 33-48   Tour / Major 1-32
//   World Finals: the season's top 8 (you included, if you made it)
//
// You may enter any tier your ranking has opened; you take the place
// of that event's lowest-ranked entrant.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim.Career
{
    public enum Tier { Club, Regional, Challenger, Tour, Major, WorldFinals }

    public sealed class TierInfo
    {
        public Tier Tier;
        public string Name;
        public int Draw;
        public int BandTop, BandBottom;          // ranking places the field comes from
        public int UnlockRank;                   // you may enter at this ranking or better (0 = always)
        public int[] Points;                     // by round reached: W, F, SF, QF, R16, R32
        public int[] Prize;                      // earnings, same order
    }

    public static class Tour
    {
        public const int RosterSize = 80;
        public const int SlotsPerSeason = 12;
        public const int CountedResults = 8;     // ranking: the best 8 results...
        public const int RankingWindow = 12;     // ...of the last 12 slots (a rolling year)

        public static readonly TierInfo[] Tiers =
        {
            new TierInfo { Tier = Tier.Club,       Name = "Club",         Draw = 16, BandTop = 65, BandBottom = 80, UnlockRank = 0,
                           Points = new[] { 100, 60, 35, 20, 10, 0 },        Prize = new[] { 500, 300, 150, 80, 40, 0 } },
            new TierInfo { Tier = Tier.Regional,   Name = "Regional",     Draw = 16, BandTop = 49, BandBottom = 64, UnlockRank = 48,
                           Points = new[] { 250, 150, 90, 50, 25, 0 },       Prize = new[] { 1500, 900, 450, 250, 120, 0 } },
            new TierInfo { Tier = Tier.Challenger, Name = "Challenger",   Draw = 16, BandTop = 33, BandBottom = 48, UnlockRank = 32,
                           Points = new[] { 500, 300, 180, 100, 50, 0 },     Prize = new[] { 5000, 3000, 1500, 800, 400, 0 } },
            new TierInfo { Tier = Tier.Tour,       Name = "Tour",         Draw = 32, BandTop = 1,  BandBottom = 32, UnlockRank = 20,
                           Points = new[] { 1000, 600, 360, 200, 100, 50 },  Prize = new[] { 15000, 9000, 5000, 2500, 1200, 600 } },
            new TierInfo { Tier = Tier.Major,      Name = "Major",        Draw = 32, BandTop = 1,  BandBottom = 32, UnlockRank = 12,
                           Points = new[] { 2000, 1200, 720, 400, 200, 100 }, Prize = new[] { 40000, 24000, 12000, 6000, 3000, 1500 } },
            new TierInfo { Tier = Tier.WorldFinals, Name = "World Finals", Draw = 8, BandTop = 1,  BandBottom = 8,  UnlockRank = 8,
                           Points = new[] { 3000, 1800, 1000, 500, 0, 0 },   Prize = new[] { 60000, 36000, 18000, 9000, 0, 0 } },
        };

        public static TierInfo Info(Tier t) { return Tiers[(int)t]; }

        /// <summary>The calendar: each slot's events, top tier first. The same every season.</summary>
        static readonly Tier[][] Calendar =
        {
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Tour, Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Tour, Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Major, Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Tour, Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.Major, Tier.Challenger, Tier.Regional, Tier.Club },
            new[] { Tier.WorldFinals },
        };

        public static Tier[] EventsIn(int slot) { return Calendar[((slot % SlotsPerSeason) + SlotsPerSeason) % SlotsPerSeason]; }

        static readonly string[] Cities =
        {
            "Harbourside", "Northgate", "Lakeview", "Westmoor", "Kingsbridge", "Redcliffe", "Eastvale", "Silverton",
            "Marlow Bay", "Ashford", "Stonehaven", "Riverton", "Fairhaven", "Brightwater", "Oakmont", "Highcastle"
        };

        /// <summary>A name for this event ("Harbourside Open").</summary>
        public static string EventName(int slot, Tier t)
        {
            if (t == Tier.WorldFinals) return "World Finals";
            string city = Cities[(slot * 5 + (int)t * 3) % Cities.Length];
            switch (t)
            {
                case Tier.Major: return city + " Masters";
                case Tier.Tour: return city + " Open";
                case Tier.Challenger: return city + " Challenger";
                case Tier.Regional: return city + " Regional";
                default: return city + " Club Classic";
            }
        }

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
                case 16: return "Round of 16";
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
