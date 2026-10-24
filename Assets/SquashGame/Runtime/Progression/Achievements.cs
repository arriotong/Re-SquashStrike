// ===========================================================
// Assets/SquashGame/Runtime/Progression/Achievements.cs
// P2-M5: achievements - skill-based, from what happened in play.
//
//   Achievements   the catalogue (ids stay fixed: Steam later)
//   MatchFacts     one match, summed up by MatchStats
//   Check*         given facts, which achievements are earned now
//
// They read results only; they never change play. Unlocking one gives
// XP by tier and, for some, a Locker Room item or a title.
// ===========================================================

using System.Collections.Generic;

namespace SquashGame.Progression
{
    public enum AchTier { Bronze, Silver, Gold }

    public sealed class Achievement
    {
        public string Id, Name, Desc, Group;
        public AchTier Tier;
        public int Target = 1;            // > 1: shown with progress ("7 / 10")
        public bool Hidden;
        public string Reward;             // an item id (cosmetic or title), or null
        public int Xp { get { return Tier == AchTier.Gold ? 400 : Tier == AchTier.Silver ? 150 : 50; } }
    }

    /// <summary>One match, summed up (from MatchStats) - everything the match achievements need.</summary>
    public sealed class MatchFacts
    {
        public bool Won, Career, TwoPlayer;
        public bool AgainstHard;               // Quick Match on Hard (or harder)
        public int GamesToWin;
        public List<int[]> Games = new List<int[]>();   // [you, them] per finished game
        public int RalliesWon, Rallies, LongestRally;
        public int GreatHits, MostGreatHitsInAGame, Nicks, WinningBoasts, Winners, Errors, Aces, TinsInARow;
        public int Tins, Outs, NotUps, Faults;
        public int Drives, Lobs, Drops, Kills, Boasts;
        public double TSeconds, Seconds;        // your time on the T; the match's playing time
        public bool CameBackFrom0_5, HitSelf;
        public double TopPaceKmh;
        // P2-M7: calls (given to you; "Against": to the opponent)
        public int Lets, Strokes, NoLets, LetsAgainst, StrokesAgainst, NoLetsAgainst;
        public bool WonAfterLet, MatchPointOnAStroke;
    }

    public static class Achievements
    {
        public const double RocketKmh = 145;            // a Hard-plus AI tops out near 138

        static List<Achievement> _all;
        static Dictionary<string, Achievement> _byId;

        static void A(string group, string id, string name, string desc, AchTier tier, string reward = null, int target = 1, bool hidden = false)
        {
            _all.Add(new Achievement { Group = group, Id = id, Name = name, Desc = desc, Tier = tier, Reward = reward, Target = target, Hidden = hidden });
        }

        public static List<Achievement> All()
        {
            if (_all != null) return _all;
            _all = new List<Achievement>();
            // ---- firsts ----
            A("Firsts", "first_win", "First win", "Win a match.", AchTier.Bronze);
            A("Firsts", "first_nick", "Nick of time", "Hit a nick - the ball dies in the join of the wall and the floor.", AchTier.Bronze, "headband_gold");
            A("Firsts", "first_great", "Great hit", "Get there early, let go on time: the ring flashes.", AchTier.Bronze);
            A("Firsts", "first_title", "Silverware", "Win a career event.", AchTier.Silver, "banner_gold");
            A("Firsts", "academy", "Graduate", "Pass every Academy lesson.", AchTier.Silver, "title_graduate");
            // ---- rallies ----
            A("Rallies", "rally10", "Ten-shot rally", "Play a rally of 10 shots or more.", AchTier.Bronze);
            A("Rallies", "rally30", "Marathon", "Play a rally of 30 shots or more.", AchTier.Gold, "title_marathon");
            A("Rallies", "rallies100", "Hundred up", "Win 100 rallies.", AchTier.Silver, null, 100);
            // ---- matches ----
            A("Matches", "bagel", "Eleven-love", "Win a game without losing a point.", AchTier.Gold, "shirt_blacklime");
            A("Matches", "comeback", "Comeback", "Win a game from 0-5 down.", AchTier.Silver, "title_comeback");
            A("Matches", "beat_hard", "Giant killer", "Beat a Hard opponent in a Quick Match.", AchTier.Silver, "frame_black");
            A("Matches", "bo5", "The long game", "Win a best-of-5 match.", AchTier.Bronze);
            A("Matches", "wins25", "Regular", "Win 25 matches.", AchTier.Silver, null, 25);
            // ---- shots ----
            A("Shots", "great10", "In the zone", "10 great hits in one game.", AchTier.Silver, "cel_sky");
            A("Shots", "nicks5", "Nick hunter", "5 nicks in one match.", AchTier.Gold, "title_nick");
            A("Shots", "ace", "Ace", "Win a rally with a serve your opponent never touches.", AchTier.Bronze);
            A("Shots", "boasts3", "Boast master", "Win 3 rallies with a boast in one match.", AchTier.Silver, "grip_orange");
            A("Shots", "rocket", "Rocket", "Hit the ball at " + RocketKmh.ToString("0") + " km/h.", AchTier.Gold, "strings_red");
            A("Shots", "winners10", "Sharpshooter", "10 winners in one match.", AchTier.Silver, "title_sharpshooter");
            // ---- the referee (P2-M7) ----
            A("Rallies", "fair_play", "Fair play", "Win the rally after a let.", AchTier.Bronze);
            A("Rallies", "stroke_genius", "Stroke of genius", "Win a match point on a stroke.", AchTier.Silver);
            // ---- Target Arcade ----
            A("Target Arcade", "arcade3", "Three stars", "Three stars on any Target Arcade level.", AchTier.Bronze);
            A("Target Arcade", "arcade_easy", "Easy does it", "Three stars on every Easy level.", AchTier.Silver, "shoes_gold");
            A("Target Arcade", "arcade_all", "Full house", "Every star in the Target Arcade.", AchTier.Gold, "banner_neon");
            A("Target Arcade", "streak25", "On a roll", "A streak of 25 in Streak.", AchTier.Silver);
            A("Target Arcade", "drills", "Drilled", "Finish every drill in the Practice Court.", AchTier.Bronze);
            // ---- career ----
            A("Career", "club", "Challenger champion", "Win a Challenger 3 or 6.", AchTier.Bronze, "title_clubchamp");
            A("Career", "regional", "Challenger elite", "Win a Challenger 9, 15 or 20.", AchTier.Silver, "title_regional");
            A("Career", "challenger", "World Tour winner", "Win a Copper or Bronze event.", AchTier.Silver, "title_challenger");
            A("Career", "tour", "Gold standard", "Win a Silver or Gold event.", AchTier.Gold, "title_tour");
            A("Career", "major", "Platinum class", "Win a Platinum or Diamond event.", AchTier.Gold, "title_major");
            A("Career", "world", "World champion", "Win the World Championship or the World Tour Finals.", AchTier.Gold, "title_world");
            A("Career", "top20", "Top 20", "Reach the world's top 20.", AchTier.Silver, "frame_gold");
            A("Career", "top10", "Top 10", "Reach the world's top 10.", AchTier.Gold, "shirt_navygold");
            A("Career", "no1", "World No. 1", "Reach number one in the world.", AchTier.Gold, "title_no1");
            A("Career", "rival", "Rivalry", "Beat one of your rivals.", AchTier.Silver, "wristband_gold");
            A("Career", "streak10", "Unstoppable", "Win 10 career matches in a row.", AchTier.Gold, "banner_blackgold");
            // ---- hidden ----
            A("Hidden", "hitself", "Own goal", "Hit yourself with your own shot.", AchTier.Bronze, null, 1, true);
            A("Hidden", "tin3", "Tin man", "Three tins in a row.", AchTier.Bronze, null, 1, true);
            return _all;
        }

        public static Achievement Find(string id)
        {
            if (_byId == null) { _byId = new Dictionary<string, Achievement>(); foreach (var a in All()) _byId[a.Id] = a; }
            return id != null && _byId.TryGetValue(id, out var x) ? x : null;
        }

        // ---------------- what a result earns ----------------

        /// <summary>The match achievements these facts earn (all that apply; the book skips ones already done).</summary>
        public static List<string> FromMatch(MatchFacts f)
        {
            var l = new List<string>();
            if (f.TwoPlayer) return l;                                  // local versus: shared screen, no achievements
            if (f.Won) l.Add("first_win");
            if (f.Nicks > 0) l.Add("first_nick");
            if (f.MostGreatHitsInAGame > 0) l.Add("first_great");
            if (f.LongestRally >= 10) l.Add("rally10");
            if (f.LongestRally >= 30) l.Add("rally30");
            foreach (var g in f.Games) if (g[0] > 0 && g[1] == 0) { l.Add("bagel"); break; }
            if (f.CameBackFrom0_5) l.Add("comeback");
            if (f.Won && f.AgainstHard && !f.Career) l.Add("beat_hard");
            if (f.Won && f.GamesToWin >= 3) l.Add("bo5");
            if (f.MostGreatHitsInAGame >= 10) l.Add("great10");
            if (f.Nicks >= 5) l.Add("nicks5");
            if (f.Aces > 0) l.Add("ace");
            if (f.WinningBoasts >= 3) l.Add("boasts3");
            if (f.TopPaceKmh >= RocketKmh) l.Add("rocket");
            if (f.Winners >= 10) l.Add("winners10");
            if (f.HitSelf) l.Add("hitself");
            if (f.TinsInARow >= 3) l.Add("tin3");
            if (f.WonAfterLet) l.Add("fair_play");
            if (f.MatchPointOnAStroke) l.Add("stroke_genius");
            return l;
        }
    }
}
