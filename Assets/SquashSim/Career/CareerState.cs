// ===========================================================
// Assets/SquashSim/Career/CareerState.cs
// P2-M4: everything a career is, as plain serialisable data (the game
// saves it to career.json with Unity's JsonUtility; the tools and tests
// keep it in memory). The roster, every result that counts for the
// ranking, your matches, the event in progress and the season records.
//
// Slots are counted from the start: slot index = season * 12 + month.
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
    }

    /// <summary>One of your matches.</summary>
    [Serializable]
    public sealed class MatchRec
    {
        public int Slot, Tier, Left;    // Left: players left in that round (2 = the final)
        public string Event;
        public int Opp;
        public int OppRank;
        public bool Won, Simulated, Retired;
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
        public int Index;               // the event's place in its month (Tour.EventsIn): a month has several Challenger 3s
        public string Name;
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
        public const int CurrentVersion = 3;     // 2: the PSA system (2026-10-24); 3: player growth (v2 careers upgrade in place)
        public int Version = CurrentVersion;
        public uint Seed;
        public string Name = "", Nat = "ENG";
        public int GamesToWin = 2;          // best of 3
        public bool English;
        public int Season = 1, Month;       // Month: 0..11 within the season
        public double Rating = 0.05;        // your strength, from your real rallies (RallyModel)
        public int Earnings, Titles;
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
        public int TrainingPoints, PointsEarned, MonthsTrained;
        public int LastTrainedSlot = -1;

        public int Slot { get { return Season * Tour.SlotsPerSeason + Month; } }
    }
}
