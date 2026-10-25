// ===========================================================
// Assets/SquashGame/Runtime/Progression/Challenges.cs
// P3-R2: daily and weekly challenges - a reason to play today, and to
// come back tomorrow.
//
//   daily     3 a day (local date), picked from the day's seed so a
//             restart can't re-roll them; never two from one group
//   weekly    1 a week (Monday to Sunday), a bigger target
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
        public int ScenarioStars;                // stars a scenario play earned (3 = a perfect one)
        public bool CareerTitle;                 // a career event won
    }

    public sealed class ChallengeDef
    {
        public string Id, Group, Text;            // Text: "{0}" is the target
        public int Target;
        public bool Weekly;
        public Func<ChallengeEvent, int> Count;   // how much this event adds (single-shot ones: Target or 0)
        public Func<bool> Available = () => true;
        /// <summary>Done in one go (a rally of 15, 130 km/h, a win without a tin): no running count to show.</summary>
        public bool Single { get { return Target == 1 || Array.IndexOf(OneGo, Id) >= 0; } }
        static readonly string[] OneGo = { "rally15", "pace130", "gauntlet3", "w_gauntlet8" };
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

        static int Won(ChallengeEvent e) { return e.Match != null && !e.Match.TwoPlayer && e.Match.Won ? 1 : 0; }
        static int M(ChallengeEvent e, Func<MatchFacts, int> f) { return e.Match != null && !e.Match.TwoPlayer ? f(e.Match) : 0; }

        static List<ChallengeDef> _all;
        public static List<ChallengeDef> All()
        {
            if (_all != null) return _all;
            _all = new List<ChallengeDef>();
            void D(string id, string group, string text, int target, Func<ChallengeEvent, int> count, bool weekly = false, Func<bool> available = null)
            {
                _all.Add(new ChallengeDef { Id = id, Group = group, Text = text, Target = target, Count = count, Weekly = weekly, Available = available ?? (() => true) });
            }
            bool career() { return CareerStore.Exists; }
            bool stars(int need) { return ArcadeStarsLeft() >= need; }

            // ---- daily ----
            D("win2", "win", "Win {0} matches", 2, Won);
            D("winhard", "win", "Win a match on Hard", 1, e => e.Match != null && e.Match.Won && e.Match.AgainstHard ? 1 : 0);
            D("notins", "win", "Win a match without hitting the tin", 1, e => M(e, f => f.Won && f.Tins == 0 ? 1 : 0));
            D("comeback", "win", "Win a game from 0-5 down", 1, e => M(e, f => f.CameBackFrom0_5 ? 1 : 0));
            D("rallies30", "rallies", "Win {0} rallies", 30, e => M(e, f => f.RalliesWon));
            D("rally15", "rallies", "Play a rally of {0} shots", 15, e => M(e, f => f.LongestRally >= 15 ? 15 : 0));
            D("tshare", "rallies", "Win a match holding the T half the time", 1, e => M(e, f => f.Won && f.Seconds > 0 && f.TSeconds / f.Seconds >= 0.5 ? 1 : 0));
            D("nicks3", "shots", "Hit {0} nicks", 3, e => M(e, f => f.Nicks));
            D("great10", "shots", "Make {0} great hits", 10, e => M(e, f => f.GreatHits));
            D("winners12", "shots", "Hit {0} winners", 12, e => M(e, f => f.Winners));
            D("kills6", "shots", "Play {0} kills", 6, e => M(e, f => f.Kills));
            D("drops8", "shots", "Play {0} drops", 8, e => M(e, f => f.Drops));
            D("boasts3", "shots", "Win {0} rallies with a boast", 3, e => M(e, f => f.WinningBoasts));
            D("ace", "shots", "Serve an ace", 1, e => M(e, f => f.Aces));
            D("pace130", "shots", "Hit the ball at {0} km/h", 130, e => M(e, f => f.TopPaceKmh >= 130 ? 130 : 0));
            D("arcade2", "arcade", "Earn {0} Target Arcade stars", 2, e => e.ArcadeStars, false, () => stars(2));
            D("arcade3", "arcade", "Play {0} Target Arcade levels", 3, e => e.Arcade ? 1 : 0);
            D("career1", "career", "Win a career match", 1, e => e.Match != null && e.Match.Career && e.Match.Won ? 1 : 0, false, career);
            D("gauntlet3", "modes", "Reach floor {0} of the Gauntlet", 3, e => e.GauntletFloor >= 3 ? 3 : 0, false, () => ModeOpen("gauntlet"));
            D("scenario3", "modes", "Get 3 stars on a Scenario", 1, e => e.ScenarioStars >= 3 ? 1 : 0, false, () => ModeOpen("scenarios"));

            // ---- weekly ----
            D("w_win10", "win", "Win {0} matches this week", 10, Won, true);
            D("w_rallies200", "rallies", "Win {0} rallies this week", 200, e => M(e, f => f.RalliesWon), true);
            D("w_nicks12", "shots", "Hit {0} nicks this week", 12, e => M(e, f => f.Nicks), true);
            D("w_great60", "shots", "Make {0} great hits this week", 60, e => M(e, f => f.GreatHits), true);
            D("w_arcade6", "arcade", "Earn {0} Target Arcade stars this week", 6, e => e.ArcadeStars, true, () => stars(6));
            D("w_career5", "career", "Win {0} career matches this week", 5, e => e.Match != null && e.Match.Career && e.Match.Won ? 1 : 0, true, career);
            D("w_gauntlet8", "modes", "Reach floor {0} of the Gauntlet", 8, e => e.GauntletFloor >= 8 ? 8 : 0, true, () => ModeOpen("gauntlet"));
            return _all;
        }

        /// <summary>The new modes register themselves here as they arrive (R5 the Gauntlet, R6 Scenarios).</summary>
        public static readonly HashSet<string> Modes = new HashSet<string> { "gauntlet", "scenarios" };
        static bool ModeOpen(string id) { return Modes.Contains(id); }

        public static ChallengeDef Find(string id) { foreach (var d in All()) if (d.Id == id) return d; return null; }

        static int ArcadeStarsLeft()
        {
            int have = 0, max = 0;
            foreach (var c in SquashSim.Arcade.Challenges.All())
            {
                if (c.Mode == SquashSim.Arcade.ArcadeMode.Drill) continue;
                var rec = ProfileStore.Record(c.Id);
                have += rec != null ? rec.Stars : 0; max += 3;
            }
            return max - have;
        }

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
            if (s.Day != day)
            {
                s.Day = day;
                s.Daily = new List<ChallengeProgress>();
                var pool = All().FindAll(d => !d.Weekly && d.Available());
                var groups = new HashSet<string>();
                uint h = Hash((uint)day * 2654435761u + 17u);
                for (int guard = 0; s.Daily.Count < DailyCount && guard < 200 && pool.Count > 0; guard++)
                {
                    h = Hash(h + 0x9E3779B9u);
                    var d = pool[(int)(h % (uint)pool.Count)];
                    if (groups.Contains(d.Group) && guard < 150) continue;
                    groups.Add(d.Group);
                    pool.Remove(d);
                    s.Daily.Add(new ChallengeProgress { Id = d.Id });
                }
            }
            if (s.Week != week)
            {
                s.Week = week;
                var pool = All().FindAll(d => d.Weekly && d.Available());
                uint h = Hash((uint)week * 2246822519u + 5u);
                s.Weekly = pool.Count > 0 ? new ChallengeProgress { Id = pool[(int)(h % (uint)pool.Count)].Id } : new ChallengeProgress();
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
                        Rewards.Grant(r, WeeklyItems[s.WeekliesDone % WeeklyItems.Length]);
                        s.WeekliesDone++;
                    }
                }
            }
        }
    }
}
