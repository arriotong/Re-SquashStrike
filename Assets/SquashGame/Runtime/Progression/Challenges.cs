// ===========================================================
// Assets/SquashGame/Runtime/Progression/Challenges.cs
// P3-R2: daily and weekly challenges - a reason to play today, and to
// come back tomorrow. Playable challenges (2026-10-28, the user: "actual
// challenges, not just missions to tell you do things in other modes";
// the old counter missions dropped):
//
//   daily     three things to play, the same for everyone on the day:
//             RULES QUIZ   five calls (QuizMaker), four right
//             SHOT PUZZLE  a frozen rally - pick the shot (the bank,
//                          ShotPuzzleBank); solve one
//             SKILL TEST   today's Target Arcade level, a star target
//   weekly    PUZZLE RUSH (solve 5 shot puzzles) or RULES EXAM (10 calls,
//             9 right), week about
//   rewards   150 XP a daily (more on a streak), 600 a weekly; the
//             challenge collection's items at streaks of 3 / 7 / 14 /
//             30 days and for each weekly done
//   streak    days in a row with at least one daily done: +10% daily
//             XP a day, up to +100%
//
// Progress comes from the result sources in Rewards (a match, an arcade
// play, a career event, a gauntlet floor, a scenario) through Record.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;

namespace SquashGame.Progression
{
    [Serializable]
    public sealed class ChallengeProgress
    {
        public string Id;
        public int Progress;
        public bool Done;
        /// <summary>Playable challenges: what it is today (the skill test's level), its target, the tries so far (a failed quiz
        /// and a missed puzzle move on to new ones).</summary>
        public string Ref;
        public int Need, Tries;
    }

    [Serializable]
    public sealed class ChallengeState
    {
        public int Day = -1, Week = -1;
        public List<ChallengeProgress> Daily = new List<ChallengeProgress>();
        public ChallengeProgress Weekly = new ChallengeProgress();
        public int Streak, LastStreakDay = -10, BestStreak;
        public int DailiesDone, WeekliesDone;
    }

    /// <summary>What just happened, for the challenges to count.</summary>
    public sealed class ChallengeEvent
    {
        public MatchFacts Match;                 // a finished match (null otherwise)
        public bool Arcade;                      // a Target Arcade play
        public int ArcadeStars;                  // new stars it earned
        public int GauntletFloor;                // the floor just won (0 = none)
        public bool GauntletBoss;                // P5: that floor was a boss
        public int ScenarioStars;                // stars a scenario play earned (3 = a perfect one)
        public bool CareerTitle;                 // a career event won
        // playable challenges (2026-10-28)
        public int QuizCount;                    // a challenge quiz finished: its size, and passed
        public bool QuizPassed;
        public bool PuzzleSolved;                // a shot puzzle solved
        public string ArcadeId;                  // a Target Arcade play: the level and its stars now
        public int ArcadeStarsNow;
    }

    public sealed class ChallengeDef
    {
        public string Id, Group, Text;            // Text: "{0}" is the target
        public int Target;
        public bool Weekly;
        public Func<ChallengeEvent, int> Count;   // how much this event adds (single-shot ones: Target or 0)
        public Func<bool> Available = () => true;
        /// <summary>Done in one go: no running count to show.</summary>
        public bool Single { get { return Target == 1; } }
        public string Describe() { return string.Format(Text, Target); }
    }

    public static class Challenges
    {
        public const int DailyXp = 150, WeeklyXp = 600, DailyCount = 3;
        public static readonly int[] StreakRewards = { 3, 7, 14, 30 };
        public static readonly string[] StreakItems = { "ch_title_regular", "ch_shirt_dawn", "ch_frame_ember", "ch_title_everyday" };
        public static readonly string[] WeeklyItems =
            { "ch_shirt_pinstripe", "ch_banner_aurora", "ch_shoes_volt", "ch_wrist_flame", "ch_socks_hoop", "ch_grip_tiger", "ch_strings_neon", "ch_title_weekly" };

        /// <summary>Off: results count toward nothing here (tests of the other rewards, which need exact XP).</summary>
        public static bool Enabled = true;

        /// <summary>The clock (tests set it).</summary>
        public static Func<DateTime> Now = () => DateTime.Now;

        static readonly DateTime Epoch = new DateTime(2000, 1, 3);       // a Monday
        public static int DayKey(DateTime t) { return (int)(t.Date - Epoch).TotalDays; }
        public static int WeekKey(DateTime t) { return DayKey(t) / 7; }

        static List<ChallengeDef> _all;
        public static List<ChallengeDef> All()
        {
            if (_all != null) return _all;
            _all = new List<ChallengeDef>();
            void D(string id, string group, string text, int target, Func<ChallengeEvent, int> count, bool weekly = false)
            {
                _all.Add(new ChallengeDef { Id = id, Group = group, Text = text, Target = target, Count = count, Weekly = weekly });
            }
            // ---- daily: three to play ----
            D(Quiz, "quiz", "Rules quiz: make " + QuizSize + " calls, get " + SquashSim.Academy.QuizMaker.PassMark(QuizSize) + " right", 1, e => e.QuizPassed && e.QuizCount == QuizSize ? 1 : 0);
            D(Puzzle, "puzzle", "Shot puzzle: find the best shot", 1, e => e.PuzzleSolved ? 1 : 0);
            D(Skill, "skill", "Skill test: today's Target Arcade level", 1, e => SkillDone(e) ? 1 : 0);
            // ---- weekly ----
            D(Rush, "puzzle", "Puzzle rush: solve {0} shot puzzles this week", 5, e => e.PuzzleSolved ? 1 : 0, true);
            D(Exam, "quiz", "Rules exam: " + ExamSize + " calls, " + SquashSim.Academy.QuizMaker.PassMark(ExamSize) + " right", 1, e => e.QuizPassed && e.QuizCount == ExamSize ? 1 : 0, true);
            return _all;
        }

        public const string Quiz = "d_quiz", Puzzle = "d_puzzle", Skill = "d_skill", Rush = "w_rush", Exam = "w_exam";
        public const int QuizSize = 5, ExamSize = 10;

        /// <summary>Today's entry for a daily (null: not today's).</summary>
        public static ChallengeProgress Today(string id) { foreach (var c in Current().Daily) if (c.Id == id) return c; return null; }

        /// <summary>The quiz to play now: today's (or this week's exam), new questions after each failed try.</summary>
        public static string QuizLessonId(bool exam)
        {
            var s = Current();
            var c = exam ? s.Weekly : Today(Quiz);
            uint seed = exam ? (uint)(s.Week * 7919 + 13) : (uint)(s.Day * 101 + 7);
            return SquashSim.Academy.QuizMaker.Id(seed + (uint)(c != null ? c.Tries : 0) * 977u, exam ? ExamSize : QuizSize);
        }

        /// <summary>A quiz try is over (passed or not): a failed one moves on to new questions.</summary>
        public static void QuizTried(bool exam, bool passed)
        {
            var s = Current();
            var c = exam ? s.Weekly : Today(Quiz);
            if (c != null && !passed) c.Tries++;
        }

        /// <summary>The bank index of the shot puzzle to play now: the day's, then the next ones as puzzles are played.</summary>
        public static int PuzzleIndex()
        {
            var c = Today(Puzzle);
            return Current().Day * 3 + (c != null ? c.Tries : 0);
        }

        /// <summary>A shot puzzle played: the next one is a new one.</summary>
        public static void PuzzleTried() { var c = Today(Puzzle); if (c != null) c.Tries++; }

        static bool SkillDone(ChallengeEvent e)
        {
            var c = Today(Skill);
            return c != null && e.ArcadeId != null && e.ArcadeId == c.Ref && e.ArcadeStarsNow >= c.Need;
        }

        /// <summary>Today's skill test: a Target Arcade level (seeded by the day) not yet at three stars, and its target -
        /// two stars, or one more than your best.</summary>
        static void RollSkill(ChallengeProgress c, int day)
        {
            var levels = SquashSim.Arcade.Challenges.All().FindAll(x => x.Mode != SquashSim.Arcade.ArcadeMode.Drill);
            uint h = Hash((uint)day * 40503u + 11u);
            for (int t = 0; t < levels.Count * 2; t++)
            {
                h = Hash(h + 1u);
                var l = levels[(int)(h % (uint)levels.Count)];
                var rec = ProfileStore.Record(l.Id);
                int have = rec != null ? rec.Stars : 0;
                if (have >= 3 && t < levels.Count) continue;
                c.Ref = l.Id; c.Need = Math.Min(3, Math.Max(2, have + 1));
                return;
            }
        }

        /// <summary>The skill test's level and target in words ("Time Attack 2  ·  2 stars").</summary>
        public static string SkillWords()
        {
            var c = Today(Skill);
            var l = c != null ? SquashSim.Arcade.Challenges.Find(c.Ref) : null;
            return l == null ? "" : SquashSim.Arcade.Challenges.ModeNames[(int)l.Mode] + "  ·  " + l.LevelName + "  ·  " + c.Need + " stars";
        }

        public static ChallengeDef Find(string id) { foreach (var d in All()) if (d.Id == id) return d; return null; }

        static ChallengeState S
        {
            get
            {
                var p = ProfileStore.Current.Progress;
                if (p.Challenges == null) p.Challenges = new ChallengeState();
                return p.Challenges;
            }
        }

        /// <summary>A tiny deterministic generator (the same picks for the same day on every machine).</summary>
        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        /// <summary>Today's and this week's challenges, rolled when the day or the week changes. Returns the state.</summary>
        public static ChallengeState Current()
        {
            var s = S;
            var now = Now();
            int day = DayKey(now), week = WeekKey(now);
            // (a profile from before the playable challenges: its old missions go, today's three come in)
            if (s.Day == day && (s.Daily.Count != DailyCount || s.Daily.Exists(c => Find(c.Id) == null))) s.Day = -1;
            if (s.Week == week && (s.Weekly == null || Find(s.Weekly.Id) == null)) s.Week = -1;
            if (s.Day != day)
            {
                s.Day = day;
                s.Daily = new List<ChallengeProgress> { new ChallengeProgress { Id = Quiz }, new ChallengeProgress { Id = Puzzle }, new ChallengeProgress { Id = Skill } };
                RollSkill(s.Daily[2], day);
            }
            if (s.Week != week)
            {
                s.Week = week;
                s.Weekly = new ChallengeProgress { Id = week % 2 == 0 ? Rush : Exam };
            }
            return s;
        }

        /// <summary>The streak as it stands today (0 once a day has been missed).</summary>
        public static int StreakNow()
        {
            var s = S;
            int today = DayKey(Now());
            return s.LastStreakDay >= today - 1 ? s.Streak : 0;
        }

        /// <summary>The XP bonus a daily earns on the current streak (0.1 a day after the first, up to 1).</summary>
        public static double StreakBonus(int streak) { return Math.Min(1.0, 0.1 * Math.Max(0, streak - 1)); }

        /// <summary>Time left today, "5h 12m".</summary>
        public static string LeftToday()
        {
            var now = Now();
            var left = now.Date.AddDays(1) - now;
            return left.Hours + "h " + left.Minutes + "m";
        }

        public static int DaysLeftThisWeek() { return 7 - DayKey(Now()) % 7; }
        public static string WeekLeft() { int d = DaysLeftThisWeek(); return d == 1 ? "1 day left" : d + " days left"; }

        /// <summary>Count an event toward today's and this week's challenges; finished ones pay out into r.</summary>
        public static void Record(RewardResult r, ChallengeEvent e)
        {
            if (!Enabled) return;
            var s = Current();
            int today = s.Day;
            foreach (var c in s.Daily)
            {
                var d = Find(c.Id);
                if (d == null || c.Done) continue;
                int add = d.Count(e);
                if (add <= 0) continue;
                c.Progress = Math.Min(d.Target, c.Progress + add);
                if (c.Progress < d.Target) continue;
                c.Done = true;
                s.DailiesDone++;
                // the streak: the first daily done today extends it (or starts a new one)
                if (s.LastStreakDay != today)
                {
                    s.Streak = s.LastStreakDay == today - 1 ? s.Streak + 1 : 1;
                    s.LastStreakDay = today;
                    s.BestStreak = Math.Max(s.BestStreak, s.Streak);
                    for (int i = 0; i < StreakRewards.Length; i++)
                        if (s.Streak == StreakRewards[i]) Rewards.Grant(r, StreakItems[i]);
                }
                int xp = (int)Math.Round(DailyXp * (1 + StreakBonus(s.Streak)));
                Rewards.AddXp(r, "Challenge done: " + d.Describe(), xp);
                r.Challenges.Add(d.Describe());
                Rewards.Ticket(r, Rewards.DailyTickets, 0, "a daily challenge");
            }
            var w = s.Weekly;
            var wd = w != null ? Find(w.Id) : null;
            if (wd != null && !w.Done)
            {
                int add = wd.Count(e);
                if (add > 0)
                {
                    w.Progress = Math.Min(wd.Target, w.Progress + add);
                    if (w.Progress >= wd.Target)
                    {
                        w.Done = true;
                        Rewards.AddXp(r, "Weekly done: " + wd.Describe(), WeeklyXp);
                        r.Challenges.Add(wd.Describe());
                        Rewards.Ticket(r, 0, 1, "the weekly");
                        Rewards.Grant(r, WeeklyItems[s.WeekliesDone % WeeklyItems.Length]);
                        s.WeekliesDone++;
                    }
                }
            }
        }
    }
}
