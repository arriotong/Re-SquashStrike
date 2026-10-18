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
        public bool LevelledUp { get { return ToLevel > FromLevel; } }
        public bool Any { get { return Xp > 0 || Unlocked.Count > 0 || Achieved.Count > 0; } }
    }

    public static class Rewards
    {
        public const int MaxLevel = 50;
        public const int RallyXp = 2, RallyWonXp = 3, MatchWonXp = 60, GreatHitXp = 5;
        public const int ArcadePlayXp = 20, ArcadeStarXp = 100, DrillXp = 30, LessonXp = 80;
        public const int EventXp = 30, RoundXp = 40, TitleXp = 200;
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

        static Progress P { get { return ProfileStore.Current.Progress; } }

        /// <summary>Owned: free items, and everything unlocked.</summary>
        public static bool Owns(string id)
        {
            var it = Cosmetics.Find(id);
            if (it == null) return false;
            return it.Unlock == UnlockKind.Free || P.Owned.Contains(id);
        }

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
            for (int l = before + 1; l <= after; l++) foreach (var it in Cosmetics.AtLevel(l)) Unlock(it, r);
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
            LifetimeStats.AddMatch(P.Stats, f);
            Achieve(r, Achievements.FromMatch(f));
            if (!f.TwoPlayer)
            {
                Count(r, "rallies100", P.Stats.RalliesWon);
                Count(r, "wins25", P.Stats.MatchesWon);
            }
            return End(r);
        }

        /// <summary>A Target Arcade play or a drill: stars before and after.</summary>
        public static RewardResult Arcade(string challengeId, bool drill, int starsBefore, int starsNow, int score, bool streakMode)
        {
            var r = Begin();
            Xp(r, drill ? "Drill finished" : "Target Arcade", drill ? DrillXp : ArcadePlayXp);
            int fresh = Math.Max(0, starsNow - Math.Max(0, starsBefore));
            Xp(r, fresh == 1 ? "A new star" : fresh + " new stars", fresh * ArcadeStarXp);
            P.Stats.ArcadePlays++;
            var ids = new List<string>();
            if (starsNow >= 3) ids.Add("arcade3");
            if (streakMode && score >= 25) ids.Add("streak25");
            Achieve(r, ids);
            ArcadeSweep(r);
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
            if (firstTime) Xp(r, "Lesson passed", LessonXp);
            P.Stats.LessonsPassed = ProfileStore.Current.Academy.Done.Count;
            if (academyFinished) Achieve(r, new[] { "academy" });
            return End(r);
        }

        /// <summary>A career event finished: how far you got, the ranking, rivals, streaks.</summary>
        public static RewardResult CareerEvent(SquashSim.Career.CareerSession c, SquashSim.Career.EventState e)
        {
            var r = Begin();
            double k = TierFactor[Math.Max(0, Math.Min(TierFactor.Length - 1, e.Tier))];
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
                string[] byTier = { "club", "regional", "challenger", "tour", "major", "world" };
                if (e.Tier >= 0 && e.Tier < byTier.Length) ids.Add(byTier[e.Tier]);
            }
            int rank = c.RankOf(SquashSim.Career.CareerSession.You);
            if (P.Stats.CareerBestRank == 0 || rank < P.Stats.CareerBestRank) P.Stats.CareerBestRank = rank;
            if (rank <= 20) ids.Add("top20");
            if (rank <= 10) ids.Add("top10");
            if (rank <= 1) ids.Add("no1");
            foreach (var m in c.S.Matches) if (m.Slot == e.Slot && m.Won && c.IsRival(m.Opp)) ids.Add("rival");
            int run = 0, best = 0;
            foreach (var m in c.S.Matches) { run = m.Won ? run + 1 : 0; best = Math.Max(best, run); }
            if (best >= 10) ids.Add("streak10");
            Achieve(r, ids);
            return End(r);
        }
    }
}
