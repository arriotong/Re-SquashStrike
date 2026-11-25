// ===========================================================
// Assets/SquashGame/Runtime/Progression/QuickLadder.cs
// 2026-11-25 (the user: "work on the thin ones aim for everything being
// deep to deeper medium"): Quick Match gets something to aim for - the
// ladder of all 100 opponents.
//
//   first win   beating someone for the first time pays coins (more the
//               better they are) and, for a pro, a free copy of their card
//   milestones  10 / 25 / 50 / 100 beaten: coins and a title each
//   streak      wins in a row: a bonus that grows (to +100 a match)
//
// Quick Match only (not versus, online, career, the Gauntlet, scenarios
// or the Academy's drills).
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using UnityEngine;

namespace SquashGame.Progression
{
    public static class QuickLadder
    {
        public sealed class Milestone { public int Beaten, Coins; public string Title, Name; }

        public static readonly Milestone[] Milestones =
        {
            new Milestone { Beaten = 10, Coins = 500, Title = "title_ladder10", Name = "Ladder Climber" },
            new Milestone { Beaten = 25, Coins = 1500, Title = "title_ladder25", Name = "Giant Killer" },
            new Milestone { Beaten = 50, Coins = 4000, Title = "title_ladder50", Name = "Half the World" },
            new Milestone { Beaten = 100, Coins = 12000, Title = "title_ladder100", Name = "Beat Them All" },
        };

        public const int StreakStep = 10, StreakMax = 100;

        static Progress P { get { return ProfileStore.Current.Progress; } }
        static List<string> Beat { get { if (P.Beaten == null) P.Beaten = new List<string>(); return P.Beaten; } }

        public static bool Beaten(string opponentId) { return opponentId != null && Beat.Contains(opponentId); }
        public static int Count { get { return Beat.Count; } }
        public static int Total { get { return SquashSim.Career.Stars.AllOpponents.Length; } }
        public static int Streak { get { return P.QuickStreak; } }
        public static int BestStreak { get { return P.BestQuickStreak; } }

        /// <summary>A first win's coins: 50 for the weakest club player up to ~250 for the world No. 1.</summary>
        public static int FirstWinCoins(string opponentId)
        {
            int rating = SquashSim.Career.Stars.Describe(opponentId).rating;
            return Mathf.Clamp(50 + (rating - 52) * 4, 50, 300);
        }

        /// <summary>The next milestone (null: all reached).</summary>
        public static Milestone Next { get { foreach (var m in Milestones) if (Count < m.Beaten) return m; return null; } }

        /// <summary>What a Quick Match result did.</summary>
        public sealed class Result
        {
            public bool Won, First;
            public int FirstCoins, StreakCoins, Streak;
            public Item Card; public string CardUp;
            public Milestone Reached;
            public int Coins { get { return FirstCoins + StreakCoins + (Reached != null ? Reached.Coins : 0); } }
        }

        /// <summary>A Quick Match is over: the ladder, the streak, the rewards (coins paid here). Saves.</summary>
        public static Result Record(string opponentId, bool won)
        {
            var r = new Result { Won = won };
            if (!won) { P.QuickStreak = 0; ProfileStore.Save(); return r; }
            P.QuickStreak++;
            P.BestQuickStreak = Math.Max(P.BestQuickStreak, P.QuickStreak);
            r.Streak = P.QuickStreak;
            r.StreakCoins = Math.Min(StreakMax, StreakStep * (P.QuickStreak - 1));
            if (!Beaten(opponentId) && Array.IndexOf(SquashSim.Career.Stars.AllOpponents, opponentId) >= 0)
            {
                Beat.Add(opponentId);
                r.First = true;
                r.FirstCoins = FirstWinCoins(opponentId);
                var card = Machines.CardOf(opponentId);
                if (card != null && opponentId.StartsWith("star:"))
                {
                    r.Card = card;
                    r.CardUp = Machines.Copy(card, (Rarity)Mathf.Clamp(card.Rarity, 0, 4), out int dupe);
                    r.FirstCoins += dupe;
                }
                foreach (var m in Milestones)
                    if (Count == m.Beaten) { r.Reached = m; if (!P.Owned.Contains(m.Title)) P.Owned.Add(m.Title); }
            }
            GearDrops.Wallet.Coins += r.Coins;
            ProfileStore.Save();
            return r;
        }

        /// <summary>Called by Machines.AddTo: the milestones' titles.</summary>
        internal static void AddTitles(Func<Slot, string, string, Color, Color, UnlockKind, Item> add)
        {
            foreach (var m in Milestones) { var t = add(Slot.Title, m.Title, m.Name, Color.white, Color.white, UnlockKind.Challenge); t.Achievement = "Quick Match: beat " + m.Beaten + " different opponents"; }
        }
    }
}
