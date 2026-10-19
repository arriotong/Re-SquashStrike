// ===========================================================
// Assets/SquashSim/Replay/Highlights.cs
// P2-M6 Replay Theatre: tagging a match's rallies and choosing a reel,
// from the file alone (each RallyRec says what happened in it).
//
//   Longest    the most shots in the match (6 or more)
//   Marathon   20 shots or more
//   Rocket     the fastest shot of the match
//   Nick       a winner that died in the nick
//   Winner     a clean winner by you (they never got it back)
//   GameBall / MatchBall   the rally that ended a game / the match
//   Comeback   levelling a game, or taking the lead, from 3+ down
//   GreatHit   one of your great hits in it
//   Dive       someone dived and the rally went on
//
// The reel: the best 3-5 rallies by a weighted score, in the order they
// were played, always with the match ball.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Replays
{
    [Flags]
    public enum RallyTag
    {
        None = 0, Longest = 1, Marathon = 2, Rocket = 4, Nick = 8, Winner = 16, GameBall = 32, MatchBall = 64, Comeback = 128, GreatHit = 256, Dive = 512,
    }

    public static class Highlights
    {
        public static readonly RallyTag[] All =
            { RallyTag.MatchBall, RallyTag.GameBall, RallyTag.Nick, RallyTag.Marathon, RallyTag.Longest, RallyTag.Rocket, RallyTag.Comeback, RallyTag.Winner, RallyTag.GreatHit, RallyTag.Dive };

        public static string Name(RallyTag t)
        {
            switch (t)
            {
                case RallyTag.Longest: return "Longest rally"; case RallyTag.Marathon: return "Marathon"; case RallyTag.Rocket: return "Rocket";
                case RallyTag.Nick: return "Nick"; case RallyTag.Winner: return "Winner"; case RallyTag.GameBall: return "Game ball";
                case RallyTag.MatchBall: return "Match ball"; case RallyTag.Comeback: return "Comeback"; case RallyTag.GreatHit: return "Great hit";
                case RallyTag.Dive: return "Dive";
            }
            return "";
        }

        static int Weight(RallyTag t)
        {
            switch (t)
            {
                case RallyTag.MatchBall: return 6; case RallyTag.Nick: return 5; case RallyTag.Marathon: return 5; case RallyTag.Longest: return 4;
                case RallyTag.Rocket: return 4; case RallyTag.Comeback: return 4; case RallyTag.GameBall: return 3; case RallyTag.Winner: return 2;
                case RallyTag.Dive: return 2; case RallyTag.GreatHit: return 1;
            }
            return 0;
        }

        /// <summary>Each rally's tags (seen from athlete <paramref name="you"/>).</summary>
        public static RallyTag[] Tag(ReplayFile f, int you = 0)
        {
            var rs = f.Rallies;
            var tags = new RallyTag[rs.Count];
            int longest = -1, fastest = -1;
            int[] down = new int[2];                  // the deficit each side has been at, this game
            for (int i = 0; i < rs.Count; i++)
            {
                var r = rs[i];
                if (r.Shots >= 6 && (longest < 0 || r.Shots > rs[longest].Shots)) longest = i;
                if (r.TopPace > 0 && (fastest < 0 || r.TopPace > rs[fastest].TopPace)) fastest = i;
                if (r.Shots >= 20) tags[i] |= RallyTag.Marathon;
                if (r.Nick && r.Winner >= 0 && r.Winner == r.LastStriker) tags[i] |= RallyTag.Nick;
                if (r.Winner == you && r.LastStriker == you && r.Kind == (byte)RallyEnd.NotUp) tags[i] |= RallyTag.Winner;
                if ((you == 0 ? r.Great0 : r.Great1) > 0) tags[i] |= RallyTag.GreatHit;
                if (r.Dives > 0) tags[i] |= RallyTag.Dive;

                int prevGames = i == 0 ? 0 : rs[i - 1].Games0 + rs[i - 1].Games1;
                bool gameEnded = r.Games0 + r.Games1 > prevGames;
                if (gameEnded) tags[i] |= RallyTag.GameBall;
                if (i == rs.Count - 1 && f.Winner >= 0) { tags[i] |= RallyTag.MatchBall; tags[i] &= ~RallyTag.GameBall; }

                // comebacks: the score before and after this rally, in this game (a new game starts at 0-0)
                bool newGame = i == 0 || (tags[i - 1] & (RallyTag.GameBall | RallyTag.MatchBall)) != 0;
                int b0 = newGame ? 0 : rs[i - 1].Points0, b1 = newGame ? 0 : rs[i - 1].Points1;
                if (newGame) { down[0] = 0; down[1] = 0; }
                down[0] = Math.Max(down[0], b1 - b0); down[1] = Math.Max(down[1], b0 - b1);
                if (r.Winner >= 0 && !gameEnded)
                {
                    int w = r.Winner, a0 = r.Points0, a1 = r.Points1;
                    int mine = w == 0 ? a0 : a1, theirs = w == 0 ? a1 : a0, wasMine = w == 0 ? b0 : b1, wasTheirs = w == 0 ? b1 : b0;
                    if (down[w] >= 3 && mine >= theirs && wasMine < wasTheirs) tags[i] |= RallyTag.Comeback;
                }
            }
            if (longest >= 0) tags[longest] |= RallyTag.Longest;
            if (fastest >= 0) tags[fastest] |= RallyTag.Rocket;
            return tags;
        }

        public static int Score(RallyTag t, int shots)
        {
            int s = 0;
            foreach (var x in All) if ((t & x) != 0) s += Weight(x) * 10;
            return s + Math.Min(shots, 30);
        }

        /// <summary>The highlight reel: rally indices in play order (3-5, fewer only for a short match; always the match ball).</summary>
        public static List<int> Reel(ReplayFile f, int max = 5, int you = 0)
        {
            var tags = Tag(f, you);
            var order = new List<int>();
            for (int i = 0; i < tags.Length; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int sa = Score(tags[a], f.Rallies[a].Shots), sb = Score(tags[b], f.Rallies[b].Shots);
                return sa != sb ? sb.CompareTo(sa) : f.Rallies[b].Shots.CompareTo(f.Rallies[a].Shots);
            });
            var reel = new List<int>();
            int last = tags.Length - 1;
            if (last >= 0 && (tags[last] & RallyTag.MatchBall) != 0) reel.Add(last);
            foreach (int i in order)
            {
                if (reel.Count >= max) break;
                if (reel.Contains(i)) continue;
                if (tags[i] == RallyTag.None && reel.Count >= 3) break;        // only rallies with something in them past the first three
                if (reel.Contains(i - 1) && tags[i] == RallyTag.None) continue; // not two plain rallies side by side
                reel.Add(i);
            }
            reel.Sort();
            return reel;
        }

        /// <summary>The highlights a match has: its reel's length (the library card's count).</summary>
        public static int Count(ReplayFile f, int you = 0) { return f.Rallies.Count == 0 ? 0 : Reel(f, 5, you).Count; }

        /// <summary>Game scores as "11-7 9-11 11-5" (each game's last rally).</summary>
        public static string GameScores(ReplayFile f)
        {
            var parts = new List<string>();
            var rs = f.Rallies;
            for (int i = 0; i < rs.Count; i++)
            {
                int prev = i == 0 ? 0 : rs[i - 1].Games0 + rs[i - 1].Games1;
                if (rs[i].Games0 + rs[i].Games1 > prev) parts.Add(rs[i].Points0 + "-" + rs[i].Points1);
            }
            if (f.Retired && rs.Count > 0)
            {
                int n = rs.Count - 1, before = n == 0 ? 0 : rs[n - 1].Games0 + rs[n - 1].Games1;
                if (rs[n].Games0 + rs[n].Games1 == before) parts.Add(rs[n].Points0 + "-" + rs[n].Points1 + " ret.");
            }
            return string.Join(" ", parts);
        }
    }
}
