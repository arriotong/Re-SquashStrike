// ===========================================================
// Assets/SquashSim/Career/CareerState.cs
// P2-M4: everything a career is, as plain serialisable data (the game
// saves it to career.json with Unity's JsonUtility; the tools and tests
// keep it in memory). The roster, every result that counts for the
// ranking, your matches, the event in progress and the season records.
//
// Slots are counted from the start: slot index = season * 52 + week
// (P7 phase 3: weeks, as the real tour is; 12 months before).
// Season 0 is the tour's year before you joined (played out at
// creation, so the ranking means something on day one; you start it
// unranked).
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    /// <summary>One player's finish in one event: what counts for the ranking.</summary>
    [Serializable]
    public sealed class ResultRec
    {
        public int Player;          // 0 = you, else a Pro.Id
        public int Slot;            // absolute slot index
        public int Tier;
        public int Reached;         // 0 = won, 1 = final, 2 = semi, 3 = quarter, 4 = R16, 5 = R32, 6 = R64
        public int Points;
        public int Prize;           // P7: your prize money from it (yours only: 0 for the pros)
    }

    /// <summary>P7: a wildcard you used (2 in any 52 weeks, 1 of them at Bronze or above).</summary>
    [Serializable]
    public sealed class WildcardRec { public int Slot, Tier; }
    [Serializable]
    public sealed class MatchRec
    {
        public int Slot, Tier, Left;    // Left: players left in that round (2 = the final)
        public string Event;
        public int Opp;
        public int OppRank;
        public bool Won, Simulated, Retired;
        public bool Qual;               // P7: a qualifying match
        public string Score;            // from your side
    }

    [Serializable]
    public sealed class RoundRec
    {
        public List<int> Players = new List<int>();     // in draw order: matches are (0,1), (2,3), ...
        public List<int> Winners = new List<int>();     // one per match, once played
        public List<string> Scores = new List<string>();
    }

    [Serializable]
    public sealed class EventState
    {
        public int Slot, Tier;
        public int Index;               // the event's place in its week (Tour.Week); Tour.HomeIndex: your home club's open
        public string Name;
        public string City = "";        // P7: where it is
        public bool Home;               // P7: your home club's open (Tour.Club)
        public bool Qualifying;         // P7: the qualifying event of a Diamond or the World Championship (no points: win it to play the main draw)
        public int Via;                 // P7: how you got in (CareerSession.Via)
        public int Cost;                // P7: what the trip cost you
        public List<RoundRec> Rounds = new List<RoundRec>();
        public bool YouIn, YouOut, Done;
        // your finish (set when the slot is finished)
        public int YouReached = -1, YouPoints, YouPrize;
    }

    [Serializable]
    public sealed class SeasonRec
    {
        public int Season, Rank, Titles, Earnings, Played, Won;
        public string BestWin;
    }

    [Serializable]
    public sealed class CareerState
    {
        public const int CurrentVersion = 5;     // 2: the PSA system (2026-10-24); 3: player growth; 4: P7 - levels; 5: P7 - the weekly world tour (older careers start afresh)
        public int Version = CurrentVersion;
        public uint Seed;
        public string Name = "", Nat = "ENG";
        public int GamesToWin = 2;          // best of 3
        public bool English;
        public int Season = 1, Week;        // Week: 0..51 within the season (P7 phase 3; was Month)
        public double Rating = 0.05;        // your strength, from your real rallies (RallyModel)
        public int Earnings, Titles;        // Earnings: everything you have earned (prize money, goals, sponsors)
        // P7 phase 3: money and travel - your cash (earnings less what the trips cost), where you are, your wildcards
        public int Cash, Spent;
        public string City = "";
        public List<WildcardRec> Wildcards = new List<WildcardRec>();
        // P7 phase 5: gear - what you own and have on (Gear.cs), what it cost you
        public List<string> GearOwned = new List<string>();
        public string Racket = "", Shoes = "", KitGear = "";
        public int GearSpent;
        public int NextId = Tour.RosterSize + 1;
        public List<Pro> Roster = new List<Pro>();
        public List<ResultRec> Results = new List<ResultRec>();
        public List<MatchRec> Matches = new List<MatchRec>();
        public List<SeasonRec> Seasons = new List<SeasonRec>();
        public EventState Current;          // the event you are in (null between events)
        public EventState Last;             // your last finished event (for the result screen)
        public bool SeasonJustEnded;        // the season summary is due
        // P3-R3: player growth - your athlete's attributes (5 = where everyone starts) and the points to spend on them
        public SquashSim.AthleteAttributes Attributes = new SquashSim.AthleteAttributes();
        public int TrainingPoints, PointsEarned, MonthsTrained;      // P7: TrainingPoints are the stat points your levels give
        public int Xp;                                              // P7 phase 2: career XP (CareerSession.LevelOf)
        public int LastTrainedSlot = -1;
        // P3-R4: the season's goals (and last season's, for the review), sponsors
        public List<GoalRec> Goals = new List<GoalRec>(), LastGoals = new List<GoalRec>();
        public int GoalsSeason = -1, GoalsDone;
        public List<SponsorDeal> Offers = new List<SponsorDeal>();
        public int OffersSeason = -1;
        public SponsorDeal Sponsor;
        public int SponsorPaid;
        public string SponsorNews;

        public int Slot { get { return Season * Tour.SlotsPerSeason + Week; } }
    }
}
