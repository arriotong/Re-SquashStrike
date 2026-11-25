// ===========================================================
// Assets/SquashSim/Modes/Scenarios.cs
// P3-R6: Scenarios - clutch situations, each a match started part-way
// (P3-C1's start score) against a chosen opponent.
//
//   15 in 3 groups: Pressure (open), Comebacks (6 stars), Legends (18)
//   stars    1 for the win, then one for each of two conditions met in
//            the same win (no tins, a nick, inside N rallies...)
//
// The data and the star rules only; the game plays them (UI/Scenario-
// Screens) and keeps the best stars per scenario.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    /// <summary>What a scenario's match came to (the game fills it from its match stats).</summary>
    public sealed class ScenarioResult
    {
        public bool Won;
        public int Rallies, RalliesWon, Tins, Errors, Winners, Nicks, Kills, GreatHits, LongestRally, Margin;
        public int Specials, DuelsWon, Dives;                                  // 2026-11-25 the Specials group
        public int PointsLost { get { return Rallies - RalliesWon; } }
    }

    public sealed class Scenario
    {
        public string Id, Name, Story;
        public int Group;                        // 0 Pressure, 1 Comebacks, 2 Legends
        public string Opponent;
        public double Difficulty;
        public int CourtTier;                    // the career's court ladder (Courts.ForCareerTier)
        public string Scoring = "par";
        public int GamesToWin = 1;
        public int StartPoints0, StartPoints1, StartGames0, StartGames1, StartServer = -1;
        public string Star2Text, Star3Text;
        /// <summary>2026-11-25 the Specials group: the Special Shot card for you (and the opponent); a meter full at the start.</summary>
        public bool Abilities, YouFull, OppSpecial, OppFull;
        public Func<ScenarioResult, bool> Star2, Star3;

        /// <summary>Stars this result earns: 0 for a loss, 1 the win, +1 for each condition met.</summary>
        public int Stars(ScenarioResult r)
        {
            if (r == null || !r.Won) return 0;
            return 1 + (Star2 != null && Star2(r) ? 1 : 0) + (Star3 != null && Star3(r) ? 1 : 0);
        }

        public string Score { get { return StartPoints0 + "-" + StartPoints1 + (GamesToWin > 1 ? ", games " + StartGames0 + "-" + StartGames1 : ""); } }
    }

    public static class Scenarios
    {
        public static readonly string[] GroupNames = { "Pressure", "Comebacks", "Legends", "Specials", "Iron Man" };
        public static readonly int[] GroupUnlock = { 0, 6, 18, 12, 30 };      // total stars to open the group (2026-11-25: Specials and Iron Man)

        static List<Scenario> _all;
        public static List<Scenario> All()
        {
            if (_all != null) return _all;
            _all = new List<Scenario>();
            Scenario S(int group, string id, string name, string story, string opp, double d, int court, int p0, int p1,
                       string s2, Func<ScenarioResult, bool> f2, string s3, Func<ScenarioResult, bool> f3)
            {
                var s = new Scenario { Group = group, Id = id, Name = name, Story = story, Opponent = opp, Difficulty = d, CourtTier = court,
                    StartPoints0 = p0, StartPoints1 = p1, Star2Text = s2, Star2 = f2, Star3Text = s3, Star3 = f3 };
                _all.Add(s);
                return s;
            }
            // ---- Pressure ----
            S(0, "closeout", "Close it out", "You need one more point to win the game. Get it!", "retriever", 0.30, 0, 10, 7,
              "Without losing a rally", r => r.PointsLost == 0, "Win it with a winner", r => r.Winners >= 1);
            S(0, "gameball", "Game ball down", "One more point and they win. Don't let them!", "grinder", 0.35, 3, 9, 10,
              "Never hit the tin", r => r.Tins == 0, "Two winners", r => r.Winners >= 2);
            var hand = S(0, "handout", "Hand-out at 8-all", "Old rules: only the server can score. Win the serve back, then win!", "shotmaker", 0.35, 3, 8, 8,
              "One error at most", r => r.Errors <= 1, "Hit a nick into the corner", r => r.Nicks >= 1);
            hand.Scoring = "english"; hand.StartServer = 1;
            var dec = S(0, "decider", "The decider", "One game each. Whoever wins this one wins it all!", "attacker", 0.45, 5, 0, 0,
              "Win by 4 or more", r => r.Margin >= 4, "Never hit the tin", r => r.Tins == 0);
            dec.GamesToWin = 2; dec.StartGames0 = 1; dec.StartGames1 = 1;
            S(0, "longhaul", "The long haul", "A whole game against a big hitter. Keep running!", "attacker", 0.40, 5, 0, 0,
              "Play a 15-shot rally", r => r.LongestRally >= 15, "5 great hits", r => r.GreatHits >= 5);
            // ---- Comebacks ----
            S(1, "sixten", "Down 6-10", "They need one point. You need five. Don't give up!", "retriever", 0.45, 5, 6, 10,
              "Never hit the tin", r => r.Tins == 0, "Inside 9 rallies", r => r.Rallies <= 9);
            var twodown = S(1, "twodown", "Two games down", "They won the first two games. Win the next three!", "grinder", 0.45, 7, 0, 0,
              "10 winners", r => r.Winners >= 10, "Never hit the tin", r => r.Tins == 0);
            twodown.GamesToWin = 3; twodown.StartGames1 = 2;
            S(1, "twonine", "Down 2-9", "They need two points. You need nine!", "shotmaker", 0.50, 7, 2, 9,
              "Two errors at most", r => r.Errors <= 2, "Hit a nick into the corner", r => r.Nicks >= 1);
            var eng = S(1, "english", "Old school", "Old rules: only the server can score. You're losing 3-8!", "attacker", 0.50, 7, 3, 8,
              "Never hit the tin", r => r.Tins == 0, "4 great hits", r => r.GreatHits >= 4);
            eng.Scoring = "english"; eng.StartServer = 1;
            var mb = S(1, "matchball", "Match ball down", "Last game. One more point and they win!", "attacker", 0.55, 9, 9, 10,
              "Three winners", r => r.Winners >= 3, "Never hit the tin", r => r.Tins == 0);
            mb.GamesToWin = 2; mb.StartGames0 = 1; mb.StartGames1 = 1;
            // ---- Legends ----
            var wf = S(2, "worldfinal", "World final", "The biggest match in the world. Two games each!", "grinder", 0.62, 12, 0, 0,
              "Never hit the tin", r => r.Tins == 0, "Win by 3 or more", r => r.Margin >= 3);
            wf.GamesToWin = 3; wf.StartGames0 = 2; wf.StartGames1 = 2;
            S(2, "marathon", "Marathon", "They never miss. Can you keep going longer?", "grinder", 0.70, 10, 0, 0,
              "A 20-shot rally", r => r.LongestRally >= 20, "Two errors at most", r => r.Errors <= 2);
            S(2, "sixballs", "Six match balls", "They need one point. You need seven!", "attacker", 0.70, 10, 4, 10,
              "Never hit the tin", r => r.Tins == 0, "Hit a nick into the corner", r => r.Nicks >= 1);
            S(2, "nickhunter", "Nick hunter", "They hit amazing shots. Hit even better ones!", "shotmaker", 0.70, 9, 0, 0,
              "Three nicks", r => r.Nicks >= 3, "8 kills", r => r.Kills >= 8);
            var last = S(2, "lastdance", "The last dance", "Losing by miles. Can you make the greatest comeback ever?", "retriever", 0.80, 12, 3, 10,
              "Never hit the tin", r => r.Tins == 0, "Lose 3 rallies at most", r => r.PointsLost <= 3);
            last.GamesToWin = 3; last.StartGames1 = 2;

            // ---- 2026-11-25 Specials: the Special Shot, the duel, the dive ----
            Scenario Sp(Scenario s, bool youFull, bool oppSpecial, bool oppFull) { s.Abilities = true; s.YouFull = youFull; s.OppSpecial = oppSpecial; s.OppFull = oppFull; return s; }
            Sp(S(3, "spdelivery", "Special delivery", "Your meter's full and it's 5-all. Show them what it can do!", "retriever", 0.40, 4, 5, 5,
              "Fire a Special Shot", r => r.Specials >= 1, "Never hit the tin", r => r.Tins == 0), true, false, false);
            Sp(S(3, "spduel", "Duel at dawn", "They've got a full meter too. When theirs comes, win the duel!", "attacker", 0.45, 5, 6, 6,
              "Win a duel", r => r.DuelsWon >= 1, "Fire a Special Shot", r => r.Specials >= 1), true, true, true);
            Sp(S(3, "spdive", "Fly for it", "Down 6-9 to a runner. Dive for the ones out of reach!", "retriever", 0.50, 6, 6, 9,
              "Dive three times", r => r.Dives >= 3, "Two errors at most", r => r.Errors <= 2), true, false, false);
            var show = Sp(S(3, "spshowdown", "Special showdown", "One game each, 8-all in the last. Both meters full!", "shotmaker", 0.55, 8, 8, 8,
              "Two Special Shots", r => r.Specials >= 2, "Win a duel", r => r.DuelsWon >= 1), true, true, true);
            show.GamesToWin = 2; show.StartGames0 = 1; show.StartGames1 = 1;
            Sp(S(3, "spstorm", "The storm", "They fire Special Shots all game. Duel them back!", "attacker", 0.65, 10, 0, 0,
              "Win two duels", r => r.DuelsWon >= 2, "Four winners", r => r.Winners >= 4), false, true, true);
            // ---- 2026-11-25 Iron Man: long matches ----
            var i1 = S(4, "ironfive", "Best of five", "A full best-of-five against a grinder. Pace yourself!", "grinder", 0.50, 6, 0, 0,
              "Six errors at most", r => r.Errors <= 6, "A 20-shot rally", r => r.LongestRally >= 20);
            i1.GamesToWin = 3;
            var i2 = S(4, "ironlungs", "Iron lungs", "Two games down to a runner. Three to win, every one long.", "retriever", 0.55, 8, 0, 0,
              "12 winners", r => r.Winners >= 12, "One tin at most", r => r.Tins <= 1);
            i2.GamesToWin = 3; i2.StartGames1 = 2;
            var i3 = S(4, "ironold", "Old-school marathon", "Old rules, best of three: only the server scores. It goes on and on.", "shotmaker", 0.55, 8, 0, 0,
              "8 great hits", r => r.GreatHits >= 8, "Hit a nick into the corner", r => r.Nicks >= 1);
            i3.Scoring = "english"; i3.GamesToWin = 2;
            var i4 = S(4, "ironfifth", "Fifth game, 0-5", "Two games each, and you're 0-5 down in the last. Dig in!", "attacker", 0.65, 10, 0, 5,
              "Never hit the tin", r => r.Tins == 0, "Win by 2 or more", r => r.Margin >= 2);
            i4.GamesToWin = 3; i4.StartGames0 = 2; i4.StartGames1 = 2;
            var i5 = S(4, "ironchamp", "Champion of champions", "A whole best-of-five against one of the very best.", "attacker", 0.80, 12, 0, 0,
              "15 winners", r => r.Winners >= 15, "Five errors at most", r => r.Errors <= 5);
            i5.GamesToWin = 3;
            return _all;
        }

        /// <summary>P6: who a scenario's opponent is - the star of its style whose rating plays nearest its difficulty (the
        /// scenario keeps its own difficulty: only the name, the face and the personal touches are the star's).</summary>
        public static Career.Star StarOf(Scenario s)
        {
            Career.Star best = null;
            double gap = double.MaxValue;
            foreach (var st in Career.Stars.All())
            {
                if (st.Style != s.Opponent) continue;
                double g = Math.Abs(Career.Stars.Difficulty(st.Rating) - s.Difficulty);
                if (g < gap) { gap = g; best = st; }
            }
            return best;
        }

        public static string OpponentId(Scenario s) { var st = StarOf(s); return st != null ? "star:" + st.Id : s.Opponent; }

        public static Scenario Find(string id) { foreach (var s in All()) if (s.Id == id) return s; return null; }

        public static List<Scenario> InGroup(int g) { return All().FindAll(s => s.Group == g); }

        public static int MaxStars { get { return All().Count * 3; } }
    }
}
