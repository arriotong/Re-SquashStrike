// ===========================================================
// Assets/SquashGame/Runtime/Progression/VersusRecord.cs
// 2026-11-25 (the user: "work on the thin ones"): Local Versus keeps
// score between the two of you.
//
//   head to head   every match, kept: matches, games and points won each,
//                  the longest rally, series won each, the longest run of
//                  match wins in a row (Profile: Progress.H2H)
//   series         one match, best of 3 or best of 5 (MenuChoices.VsSeries):
//                  Rematch plays the next match of it; the post-match
//                  screen says where it stands
// ===========================================================

using System;
using SquashGame.Profile;

namespace SquashGame.Progression
{
    [Serializable]
    public sealed class VersusH2H
    {
        public int Matches, Wins0, Wins1, Games0, Games1, Points0, Points1, Longest, Series0, Series1;
        public int RunBy = -1, Run, BestRun0, BestRun1;               // the current run of wins (whose) and each one's best
    }

    public static class VersusRecord
    {
        public static readonly string[] SeriesLabels = { "One match", "Best of 3", "Best of 5" };
        public static readonly int[] SeriesNeed = { 1, 2, 3 };

        static VersusH2H R { get { var p = ProfileStore.Current.Progress; if (p.H2H == null) p.H2H = new VersusH2H(); return p.H2H; } }
        public static VersusH2H Record { get { return R; } }

        // ---- the series being played (this session) ----
        public static int Need { get; private set; } = 1;
        public static readonly int[] SeriesWins = new int[2];
        /// <summary>A series is under way (more than one match and not yet won).</summary>
        public static bool InSeries { get { return Need > 1 && SeriesWins[0] < Need && SeriesWins[1] < Need; } }
        public static int SeriesWinner { get { return SeriesWins[0] >= Need ? 0 : SeriesWins[1] >= Need ? 1 : -1; } }

        /// <summary>A new series from the setup's choice (0 one match, 1 best of 3, 2 best of 5).</summary>
        public static void NewSeries(int choice)
        {
            Need = SeriesNeed[Math.Max(0, Math.Min(SeriesNeed.Length - 1, choice))];
            SeriesWins[0] = SeriesWins[1] = 0;
        }

        /// <summary>A Local Versus match is over (winner 0 / 1, games and points by player, its longest rally). Saves.</summary>
        public static void RecordMatch(int winner, int games0, int games1, int points0, int points1, int longest)
        {
            if (winner < 0 || winner > 1) return;
            var r = R;
            r.Matches++;
            if (winner == 0) r.Wins0++; else r.Wins1++;
            r.Games0 += games0; r.Games1 += games1; r.Points0 += points0; r.Points1 += points1;
            r.Longest = Math.Max(r.Longest, longest);
            if (r.RunBy == winner) r.Run++; else { r.RunBy = winner; r.Run = 1; }
            if (winner == 0) r.BestRun0 = Math.Max(r.BestRun0, r.Run); else r.BestRun1 = Math.Max(r.BestRun1, r.Run);
            if (Need > 1 && SeriesWinner < 0)
            {
                SeriesWins[winner]++;
                if (SeriesWins[winner] >= Need) { if (winner == 0) r.Series0++; else r.Series1++; }
            }
            ProfileStore.Save();
        }

        public static void Reset() { ProfileStore.Current.Progress.H2H = new VersusH2H(); ProfileStore.Save(); }

        /// <summary>"Player 1  7 – 5  Player 2".</summary>
        public static string Line(string n0 = "Player 1", string n1 = "Player 2") { var r = R; return n0 + "  " + r.Wins0 + " – " + r.Wins1 + "  " + n1; }
    }
}
