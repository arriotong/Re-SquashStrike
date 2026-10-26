// ===========================================================
// Assets/SquashGame/Runtime/Progression/Rewards.cs
// P2-M5: XP, levels and unlocks - every rule in one place.
//
//   level N -> N+1 needs 200 + 60 (N - 1) XP; 50 is the top level, XP
//   past it counts as "prestige stars" (cosmetic only)
//
//   a rally played / won        2 / +3
//   a match won                 +60 (x1.5 against Hard or a Tour-tier pro)
//   a great hit                 +5
//   Target Arcade               20 a play, +100 a new star
//   a drill finished            30
//   an Academy lesson (first)   80
//   a career event              30 entered, +40 a round won, +200 the
//                               title, x the tier (1 .. 4)
//   an achievement              50 / 150 / 400 (bronze / silver / gold)
//
// Batch 3 (2026-10-26, the user: "get money from every thing you do,
// depending on how long you play or how hard it is ... even playing
// friends locally or online"): COINS from everything -
//   a match                     a coin a rally, +1 a rally won, +15 a game
//                               to win if you win; x the opponent (0.8 at a
//                               60-rated club player .. 2 at the No. 1),
//                               x1 local and online, x1.25 ranked. Local and
//                               casual online: past 300 coins a day, a
//                               quarter (no farming with a pad on the floor)
//   Target Arcade               10 a play, +20 a new star
//   an Academy lesson (first)   30
//   an achievement              25 / 75 / 200
//   a level                     a ticket and 50 + 5 x level coins; every
//                               5th level a x10 ticket instead (levels no
//                               longer give kit: that is in the kit store)
// Coins buy tickets (Gear Drops), kit-store items and the Pro Shop.
// Never real money.
//
// Award(...) adds XP, unlocks what the new levels give, and returns the
// lines for the result screens' reward strip. It saves the profile.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;

namespace SquashGame.Progression
{
    public sealed class RewardResult
    {
        public readonly List<string> Lines = new List<string>();      // "Match won  +60"
        public int Xp, FromLevel, ToLevel;
        public int XpBefore;
        public readonly List<Item> Unlocked = new List<Item>();
        public readonly List<Achievement> Achieved = new List<Achievement>();
        public readonly List<string> Challenges = new List<string>();      // P3-R2: challenges finished by this result
        public int Tickets, TenTickets;                                     // P4: Gear Drops tickets earned by this result
        public readonly List<string> TicketWhy = new List<string>();
        public int Coins;                                                   // batch 3: coins earned by this result
        public readonly List<string> CoinWhy = new List<string>();
        public bool LevelledUp { get { return ToLevel > FromLevel; } }
        public bool Any { get { return Xp > 0 || Unlocked.Count > 0 || Achieved.Count > 0; } }
    }

    public static class Rewards
    {
        public const int MaxLevel = 50;
        public const int RallyXp = 2, RallyWonXp = 3, MatchWonXp = 60, GreatHitXp = 5;
        public const int ArcadePlayXp = 20, ArcadeStarXp = 100, DrillXp = 30, LessonXp = 80;
        public const int EventXp = 30, RoundXp = 40, TitleXp = 200, GoalXp = 150;
        public static readonly double[] TierFactor = { 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

        /// <summary>XP to go from this level to the next.</summary>
        public static int Need(int level) { return 200 + 60 * (level - 1); }

        /// <summary>Total XP to reach this level (level 1 = 0).</summary>
        public static int TotalFor(int level)
        {
            int t = 0;
            for (int n = 1; n < level; n++) t += Need(n);
            return t;
        }

        public static int LevelOf(int xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= TotalFor(level + 1)) level++;
            return level;
        }

        /// <summary>Progress into the current level, 0..1 (at the top: into the next prestige star).</summary>
        public static float Into(int xp)
        {
            int level = LevelOf(xp);
            int start = TotalFor(level);
            int need = Need(level);
            int over = xp - start;
            if (level >= MaxLevel) over %= need;
            return Math.Min(1f, over / (float)need);
        }

        public static int Prestige(int xp) { int top = TotalFor(MaxLevel); return xp < top ? 0 : (xp - top) / Need(MaxLevel); }

        /// <summary>P3-R1 (visible progress): the next level, what it gives (batch 3: coins and a ticket - every 5th a x10
        /// ticket) and the XP to go. Level 0 at the top.</summary>
        public static int NextUnlock(int xp, out string gives, out int xpToGo)
        {
            int level = LevelOf(xp) + 1;
            if (level > MaxLevel) { gives = ""; xpToGo = 0; return 0; }
            gives = LevelGives(level); xpToGo = TotalFor(level) - xp;
            return level;
        }

        /// <summary>Batch 3: a level's reward - coins, and a ticket (every 5th level: a x10 ticket).</summary>
        public static int LevelCoins(int level) { return 50 + 5 * level; }
        public static bool LevelTen(int level) { return level % 5 == 0; }
        public static string LevelGives(int level) { return (LevelTen(level) ? "a x10 ticket" : "a ticket") + " + " + LevelCoins(level) + " coins"; }

        // ---------------- batch 3: coins ----------------

        public const int RallyCoin = 1, RallyWonCoin = 1, GameWonCoins = 15, LocalDailyCap = 300, ArcadeCoins = 10, ArcadeStarCoins = 20, LessonCoins = 30;
        public static readonly int[] AchievementCoins = { 25, 75, 200 };

        /// <summary>How much a match against this opponent pays: 0.8 against a 60-rated player, up to 2 against the No. 1.</summary>
        public static double CoinFactor(MatchFacts f)
        {
            if (f.TwoPlayer) return 1.0;
            if (f.Online) return f.Ranked ? 1.25 : 1.0;
            return Math.Max(0.8, Math.Min(2.0, 0.8 + (f.OppRating - 60) * 0.031));
        }

        /// <summary>A match's coins (before the daily cap).</summary>
        public static int MatchCoins(MatchFacts f)
        {
            int raw = f.Rallies * RallyCoin + f.RalliesWon * RallyWonCoin + (f.Won ? GameWonCoins * Math.Max(1, f.GamesToWin) : 0);
            return (int)Math.Round(raw * CoinFactor(f));
        }

        /// <summary>A local or casual online match's coins after today's soft cap (past LocalDailyCap: a quarter).</summary>
        public static int CapLocal(int coins)
        {
            var w = GearDrops.Wallet;
            int day = Challenges.DayKey(Challenges.Now());
            if (w.CapDay != day) { w.CapDay = day; w.CapCoins = 0; }
            int full = Math.Max(0, Math.Min(coins, LocalDailyCap - w.CapCoins));
            w.CapCoins += full;
            return full + (coins - full) / 4;
        }

        /// <summary>Coins earned, and why.</summary>
        public static void Coin(RewardResult r, int coins, string why)
        {
            if (coins <= 0) return;
            GearDrops.Wallet.Coins += coins;
            r.Coins += coins;
            r.CoinWhy.Add(why);
        }

        /// <summary>The XP a match has earned so far from its rallies (the post-match adds great hits and the win).</summary>
        public static int SoFar(int rallies, int ralliesWon) { return rallies * RallyXp + ralliesWon * RallyWonXp; }

        /// <summary>"Celebration: Fist pump" - an item and its slot, for the next-unlock lines.</summary>
        public static string Describe(List<Item> items)
        {
            var names = new List<string>();
            foreach (var it in items) names.Add(it.Name + " (" + Cosmetics.SlotNames[(int)it.Slot].ToLowerInvariant() + ")");
            return string.Join(", ", names);
        }

        static Progress P { get { return ProfileStore.Current.Progress; } }

        /// <summary>Owned: free items, and everything unlocked.</summary>
        public static bool Owns(string id)
        {
            var it = Cosmetics.Find(id);
            if (it == null) return false;
            return it.Unlock == UnlockKind.Free || P.Owned.Contains(id);
        }

        /// <summary>P4: Gear Drops tickets (and x10 tickets) earned, and why. Earned by playing - never bought.</summary>
        public static void Ticket(RewardResult r, int tickets, int tens, string why)
        {
            if (tickets <= 0 && tens <= 0) return;
            var w = GearDrops.Wallet;
            w.Tickets += tickets; w.TenTickets += tens;
            r.Tickets += tickets; r.TenTickets += tens;
            r.TicketWhy.Add(why);
        }

        public const int WinsPerTicket = 3, DailyTickets = 2, GoalTickets = 2, RankedWinTickets = 1;

        /// <summary>P3-R2: XP from outside the usual sources (a challenge), with its line.</summary>
        public static void AddXp(RewardResult r, string line, int xp) { Xp(r, line, xp); }

        /// <summary>P3-R4: an item given outside a result (a sponsor's kit). Saves.</summary>
        public static void GrantNow(string id) { var it = Cosmetics.Find(id); if (it != null && !P.Owned.Contains(id)) { P.Owned.Add(id); ProfileStore.Save(); } }

        /// <summary>P3-R2: an item given outright (a challenge reward).</summary>
        public static void Grant(RewardResult r, string id) { Unlock(Cosmetics.Find(id), r); }

        static void Unlock(Item it, RewardResult r)
        {
            if (it == null || it.Unlock == UnlockKind.Free || P.Owned.Contains(it.Id)) return;
            P.Owned.Add(it.Id);
            r.Unlocked.Add(it);
        }

        /// <summary>Add XP (a line for the reward strip); level-ups unlock the level table's items.</summary>
        static void Xp(RewardResult r, string line, int xp)
        {
            if (xp <= 0) return;
            int before = LevelOf(P.Xp);
            P.Xp += xp;
            r.Xp += xp;
            r.Lines.Add(line + "  +" + xp);
            int after = LevelOf(P.Xp);
            for (int l = before + 1; l <= after; l++)                                    // batch 3: a level pays, it no longer dresses
            {
                if (LevelTen(l)) Ticket(r, 0, 1, "level " + l); else Ticket(r, 1, 0, "level " + l);
                Coin(r, LevelCoins(l), "level " + l);
            }
        }

        static RewardResult Begin() { var r = new RewardResult { XpBefore = P.Xp, FromLevel = LevelOf(P.Xp) }; return r; }
        static RewardResult End(RewardResult r) { r.ToLevel = LevelOf(P.Xp); ProfileStore.Save(); return r; }

        /// <summary>Achievements earned now (ids): each once, with its XP and reward.</summary>
        public static void Achieve(RewardResult r, IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                var a = Achievements.Find(id);
                var st = State(id);
                if (a == null || st.Done) continue;
                st.Done = true; st.Progress = a.Target; st.When = DateTime.Now.ToString("yyyy-MM-dd");
                r.Achieved.Add(a);
                Xp(r, "Achievement: " + a.Name, a.Xp);
                Coin(r, AchievementCoins[(int)a.Tier], a.Name);
                if (a.Reward != null) Unlock(Cosmetics.Find(a.Reward), r);
            }
        }

        /// <summary>A counted achievement's progress (lifetime counts): earned when it reaches the target.</summary>
        public static void Count(RewardResult r, string id, int value)
        {
            var a = Achievements.Find(id);
            if (a == null) return;
            var st = State(id);
            if (st.Done) return;
            st.Progress = Math.Min(value, a.Target);
            if (value >= a.Target) Achieve(r, new[] { id });
        }

        public static AchievementState State(string id)
        {
            foreach (var s in P.Achievements) if (s.Id == id) return s;
            var n = new AchievementState { Id = id };
            P.Achievements.Add(n);
            return n;
        }

        public static bool Done(string id) { foreach (var s in P.Achievements) if (s.Id == id) return s.Done; return false; }

        // ---------------- the sources ----------------

        /// <summary>A finished match (Quick Match, Versus as player 1, Career).</summary>
        public static RewardResult Match(MatchFacts f, double careerTierFactor = 0)
        {
            var r = Begin();
            Xp(r, f.Rallies + " rallies", f.Rallies * RallyXp);
            Xp(r, f.RalliesWon + " rallies won", f.RalliesWon * RallyWonXp);
            Xp(r, f.GreatHits + " great hits", f.GreatHits * GreatHitXp);
            if (f.Won) Xp(r, "Match won", (int)Math.Round(MatchWonXp * (f.AgainstHard || careerTierFactor >= 2 ? 1.5 : 1.0)));
            // batch 3: coins for every match - local and online too; local and casual online taper past the day's cap
            int coins = MatchCoins(f);
            if (f.TwoPlayer || (f.Online && !f.Ranked)) coins = CapLocal(coins);
            Coin(r, coins, f.Won ? "a match won" : "a match");
            LifetimeStats.AddMatch(P.Stats, f);
            Achieve(r, Achievements.FromMatch(f));
            if (!f.TwoPlayer)
            {
                Count(r, "rallies100", P.Stats.RalliesWon);
                Count(r, "wins25", P.Stats.MatchesWon);
                Challenges.Record(r, new ChallengeEvent { Match = f });
                if (f.Won && f.Ranked) Ticket(r, RankedWinTickets, 0, "Ranked win");             // P2-M8 (batch 3: the every-3rd-win ticket is coins now)
            }
            return End(r);
        }

        /// <summary>A Target Arcade play or a drill: stars before and after.</summary>
        public static RewardResult Arcade(string challengeId, bool drill, int starsBefore, int starsNow, int score, bool streakMode)
        {
            var r = Begin();
            Xp(r, drill ? "Drill finished" : "Target Arcade", drill ? DrillXp : ArcadePlayXp);
            int fresh = Math.Max(0, starsNow - Math.Max(0, starsBefore));
            Coin(r, ArcadeCoins + ArcadeStarCoins * fresh, drill ? "a drill" : "Target Arcade");
            Xp(r, fresh == 1 ? "A new star" : fresh + " new stars", fresh * ArcadeStarXp);
            P.Stats.ArcadePlays++;
            var ids = new List<string>();
            if (starsNow >= 3) ids.Add("arcade3");
            if (streakMode && score >= 25) ids.Add("streak25");
            Achieve(r, ids);
            ArcadeSweep(r);
            Challenges.Record(r, new ChallengeEvent { Arcade = !drill, ArcadeStars = fresh });
            return End(r);
        }

        /// <summary>The arcade's "every star" achievements, from the saved records.</summary>
        static void ArcadeSweep(RewardResult r)
        {
            int all = 0, max = 0, easy = 0, easyCount = 0, drillsDone = 0, drillCount = 0;
            foreach (var c in SquashSim.Arcade.Challenges.All())
            {
                var rec = ProfileStore.Record(c.Id);
                if (c.Mode == SquashSim.Arcade.ArcadeMode.Drill) { drillCount++; if (rec != null && rec.Plays > 0) drillsDone++; continue; }
                int s = rec != null ? rec.Stars : 0;
                all += s; max += 3;
                if (c.Level == 0) { easyCount++; if (s >= 3) easy++; }
            }
            var ids = new List<string>();
            if (easyCount > 0 && easy == easyCount) ids.Add("arcade_easy");
            if (max > 0 && all >= max) ids.Add("arcade_all");
            if (drillCount > 0 && drillsDone == drillCount) ids.Add("drills");
            Achieve(r, ids);
        }

        /// <summary>An Academy lesson passed (XP the first time); the whole Academy done.</summary>
        public static RewardResult Lesson(bool firstTime, bool academyFinished)
        {
            var r = Begin();
            if (firstTime) { Xp(r, "Lesson passed", LessonXp); Coin(r, LessonCoins, "a lesson"); }
            P.Stats.LessonsPassed = ProfileStore.Current.Academy.Done.Count;
            if (academyFinished) Achieve(r, new[] { "academy" });
            return End(r);
        }

        public const int GauntletFloorXp = 40, GauntletFloorStepXp = 10;
        public static readonly int[] GauntletMilestones = { 5, 10, 15, 20 };
        public static readonly string[] GauntletItems = { "gt_banner_iron", "gt_shoes_ember", "gt_frame_storm", "gt_title_unbroken" };

        /// <summary>P3-R5: a Gauntlet floor won (the floors won so far, this one included; P5: a boss).</summary>
        public static RewardResult GauntletFloor(int floorsWon, bool boss = false)
        {
            var r = Begin();
            Xp(r, "Gauntlet floor " + floorsWon, GauntletFloorXp + GauntletFloorStepXp * floorsWon);
            Ticket(r, 1, 0, "a Gauntlet floor");
            if (boss) { Xp(r, "A boss beaten", GauntletBossXp); Ticket(r, 2, 0, "a boss"); }
            var ids = new List<string>();
            if (boss) ids.Add("gt_boss");
            if (floorsWon >= 30) ids.Add("gt_floor30");
            Achieve(r, ids);
            for (int i = 0; i < GauntletMilestones.Length; i++) if (floorsWon == GauntletMilestones[i]) { Grant(r, GauntletItems[i]); Ticket(r, 0, 1, "floor " + floorsWon); }
            Challenges.Record(r, new ChallengeEvent { GauntletFloor = floorsWon, GauntletBoss = boss });
            return End(r);
        }

        public const int GauntletBossXp = 150;

        /// <summary>P5: an evolution taken in a Gauntlet run.</summary>
        public static RewardResult GauntletEvolved()
        {
            var r = Begin();
            Achieve(r, new[] { "gt_evolve" });
            return End(r);
        }

        public const int ScenarioPlayXp = 30, ScenarioStarXp = 60;
        public static readonly int[] ScenarioMilestones = { 15, 30, 45 };
        public static readonly string[] ScenarioItems = { "sc_title_clutch", "sc_shirt_ice", "sc_title_legend" };

        /// <summary>P3-R6: a scenario played - the stars it earned, the best before, the stars in total now.</summary>
        public static RewardResult Scenario(int stars, int bestBefore, int totalNow)
        {
            var r = Begin();
            Xp(r, "Scenario", ScenarioPlayXp);
            int fresh = Math.Max(0, stars - bestBefore);
            Xp(r, fresh == 1 ? "A new star" : fresh + " new stars", fresh * ScenarioStarXp);
            Ticket(r, fresh, 0, fresh == 1 ? "a new star" : "new stars");
            for (int i = 0; i < ScenarioMilestones.Length; i++) if (totalNow >= ScenarioMilestones[i]) Grant(r, ScenarioItems[i]);
            Challenges.Record(r, new ChallengeEvent { ScenarioStars = stars });
            return End(r);
        }

        /// <summary>A career event finished: how far you got, the ranking, rivals, streaks.</summary>
        public static RewardResult CareerEvent(SquashSim.Career.CareerSession c, SquashSim.Career.EventState e)
        {
            var r = Begin();
            double k = TierFactor[SquashSim.Career.Tour.Band(e.Tier)];
            int rounds = 0;
            foreach (var m in c.S.Matches) if (m.Slot == e.Slot && m.Won) rounds++;
            Xp(r, e.Name, (int)Math.Round(EventXp * k));
            Xp(r, rounds == 1 ? "1 round won" : rounds + " rounds won", (int)Math.Round(rounds * RoundXp * k));
            var ids = new List<string>();
            if (e.YouReached == 0)
            {
                Xp(r, "Title!", (int)Math.Round(TitleXp * k));
                P.Stats.CareerTitles++;
                ids.Add("first_title");
                string[] byBand = { "club", "regional", "challenger", "tour", "major", "world" };
                ids.Add(byBand[SquashSim.Career.Tour.Band(e.Tier)]);
            }
            int rank = c.RankOf(SquashSim.Career.CareerSession.You);
            if (rank != SquashSim.Career.CareerSession.Unranked && (P.Stats.CareerBestRank == 0 || rank < P.Stats.CareerBestRank)) P.Stats.CareerBestRank = rank;
            if (rank <= 20) ids.Add("top20");
            if (rank <= 10) ids.Add("top10");
            if (rank <= 1) ids.Add("no1");
            foreach (var m in c.S.Matches) if (m.Slot == e.Slot && m.Won && c.IsRival(m.Opp)) ids.Add("rival");
            int run = 0, best = 0;
            foreach (var m in c.S.Matches) { run = m.Won ? run + 1 : 0; best = Math.Max(best, run); }
            if (best >= 10) ids.Add("streak10");
            Achieve(r, ids);
            // P3-R4: season goals done in this event's month
            foreach (var g in c.Goals) if (g.Done && g.DoneSlot == e.Slot) { Xp(r, "Season goal: " + g.Text, GoalXp); Ticket(r, GoalTickets, 0, "a season goal"); }
            if (e.YouReached == 0) { int band = SquashSim.Career.Tour.Band(e.Tier); Ticket(r, band <= 1 ? 1 : band <= 3 ? 2 : 3, 0, "a title"); }   // 1 to 3 tickets by the event
            Challenges.Record(r, new ChallengeEvent { CareerTitle = e.YouReached == 0 });
            return End(r);
        }
    }
}
