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
        public static readonly string[] GroupNames = { "Pressure", "Comebacks", "Legends" };
        public static readonly int[] GroupUnlock = { 0, 6, 18 };      // total stars to open the group

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
            S(0, "closeout", "Close it out", "10-7 up. Three game balls. Don't let them back in.", "retriever", 0.30, 0, 10, 7,
              "Without losing a rally", r => r.PointsLost == 0, "Win it with a winner", r => r.Winners >= 1);
            S(0, "gameball", "Game ball down", "9-10. One rally and it's gone. Hold your nerve.", "grinder", 0.35, 3, 9, 10,
              "No tins", r => r.Tins == 0, "Two winners", r => r.Winners >= 2);
            var hand = S(0, "handout", "Hand-out at 8-all", "English scoring, 8-8, and they're serving. Win it back - then win the game.", "shotmaker", 0.35, 3, 8, 8,
              "One error at most", r => r.Errors <= 1, "Hit a nick", r => r.Nicks >= 1);
            hand.Scoring = "english"; hand.StartServer = 1;
            var dec = S(0, "decider", "The decider", "One game all. Everything on this one.", "attacker", 0.45, 5, 0, 0,
              "Win by 4 or more", r => r.Margin >= 4, "No tins", r => r.Tins == 0);
            dec.GamesToWin = 2; dec.StartGames0 = 1; dec.StartGames1 = 1;
            S(0, "longhaul", "The long haul", "A full game against an attacker. Make it physical.", "attacker", 0.40, 5, 0, 0,
              "Play a 15-shot rally", r => r.LongestRally >= 15, "5 great hits", r => r.GreatHits >= 5);
            // ---- Comebacks ----
            S(1, "sixten", "Down 6-10", "Four game balls against you. Save them all.", "retriever", 0.45, 5, 6, 10,
              "No tins", r => r.Tins == 0, "Inside 9 rallies", r => r.Rallies <= 9);
            var twodown = S(1, "twodown", "Two games down", "Best of five, 0-2 down. Win three in a row.", "grinder", 0.45, 7, 0, 0,
              "10 winners", r => r.Winners >= 10, "No tins", r => r.Tins == 0);
            twodown.GamesToWin = 3; twodown.StartGames1 = 2;
            S(1, "twonine", "Down 2-9", "They need two points. You need nine.", "shotmaker", 0.50, 7, 2, 9,
              "Two errors at most", r => r.Errors <= 2, "Hit a nick", r => r.Nicks >= 1);
            var eng = S(1, "english", "Old school", "English scoring, 3-8 down and receiving. Only the server scores.", "attacker", 0.50, 7, 3, 8,
              "No tins", r => r.Tins == 0, "4 great hits", r => r.GreatHits >= 4);
            eng.Scoring = "english"; eng.StartServer = 1;
            var mb = S(1, "matchball", "Match ball down", "One game all, 9-10 in the decider.", "attacker", 0.55, 9, 9, 10,
              "Three winners", r => r.Winners >= 3, "No tins", r => r.Tins == 0);
            mb.GamesToWin = 2; mb.StartGames0 = 1; mb.StartGames1 = 1;
            // ---- Legends ----
            var wf = S(2, "worldfinal", "World final", "Best of five, two games all, the arena on its feet.", "grinder", 0.62, 12, 0, 0,
              "No tins", r => r.Tins == 0, "Win by 3 or more", r => r.Margin >= 3);
            wf.GamesToWin = 3; wf.StartGames0 = 2; wf.StartGames1 = 2;
            S(2, "marathon", "Marathon", "A grinder who never misses. Outlast them.", "grinder", 0.70, 10, 0, 0,
              "A 20-shot rally", r => r.LongestRally >= 20, "Two errors at most", r => r.Errors <= 2);
            S(2, "sixballs", "Six match balls", "4-10. Six match balls to save.", "attacker", 0.70, 10, 4, 10,
              "No tins", r => r.Tins == 0, "Hit a nick", r => r.Nicks >= 1);
            S(2, "nickhunter", "Nick hunter", "A shot-maker at their best. Beat them at their own game.", "shotmaker", 0.70, 9, 0, 0,
              "Three nicks", r => r.Nicks >= 3, "8 kills", r => r.Kills >= 8);
            var last = S(2, "lastdance", "The last dance", "Best of five, 0-2 and 3-10 down. The greatest comeback.", "retriever", 0.80, 12, 3, 10,
              "No tins", r => r.Tins == 0, "Lose 3 rallies at most", r => r.PointsLost <= 3);
            last.GamesToWin = 3; last.StartGames1 = 2;
            return _all;
        }

        public static Scenario Find(string id) { foreach (var s in All()) if (s.Id == id) return s; return null; }

        public static List<Scenario> InGroup(int g) { return All().FindAll(s => s.Group == g); }

        public static int MaxStars { get { return All().Count * 3; } }
    }
}
