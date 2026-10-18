// ===========================================================
// Assets/SquashGame/Runtime/Progression/LifetimeStats.cs
// P2-M5: the profile's lifetime totals (the Stats screen), split by
// mode where it matters. Added to after every match, arcade play,
// lesson and career event (Rewards).
// ===========================================================

using System;

namespace SquashGame.Progression
{
    [Serializable]
    public sealed class ModeTotals
    {
        public int Played, Won, Games, GamesWon, Rallies, RalliesWon;
    }

    [Serializable]
    public sealed class LifetimeStats
    {
        public ModeTotals Quick = new ModeTotals(), Versus = new ModeTotals(), Career = new ModeTotals();
        public int MatchesWon, RalliesWon, Rallies, LongestRally;
        public double TopPaceKmh;
        public int Winners, Errors, Tins, Outs, NotUps, Faults, Nicks, GreatHits, Aces;
        public int Drives, Lobs, Drops, Kills, Boasts;
        public double TSeconds, PlaySeconds;
        public int ArcadePlays, LessonsPassed, CareerTitles, CareerBestRank;

        public int Shots { get { return Drives + Lobs + Drops + Kills + Boasts; } }
        public float TShare { get { return PlaySeconds > 0 ? (float)(TSeconds / PlaySeconds) : 0f; } }

        public static void AddMatch(LifetimeStats s, MatchFacts f)
        {
            var m = f.TwoPlayer ? s.Versus : f.Career ? s.Career : s.Quick;
            m.Played++; if (f.Won) m.Won++;
            foreach (var g in f.Games) { m.Games++; if (g[0] > g[1]) m.GamesWon++; }
            m.Rallies += f.Rallies; m.RalliesWon += f.RalliesWon;
            if (f.TwoPlayer) return;                                 // the rest is "you": not a shared screen
            if (f.Won) s.MatchesWon++;
            s.Rallies += f.Rallies; s.RalliesWon += f.RalliesWon;
            s.LongestRally = Math.Max(s.LongestRally, f.LongestRally);
            s.TopPaceKmh = Math.Max(s.TopPaceKmh, f.TopPaceKmh);
            s.Winners += f.Winners; s.Errors += f.Errors; s.Tins += f.Tins; s.Outs += f.Outs; s.NotUps += f.NotUps; s.Faults += f.Faults;
            s.Nicks += f.Nicks; s.GreatHits += f.GreatHits; s.Aces += f.Aces;
            s.Drives += f.Drives; s.Lobs += f.Lobs; s.Drops += f.Drops; s.Kills += f.Kills; s.Boasts += f.Boasts;
            s.TSeconds += f.TSeconds; s.PlaySeconds += f.Seconds;
        }
    }
}
