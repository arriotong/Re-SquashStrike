// ===========================================================
// Assets/SquashSim/Career/RallyModel.cs
// P2-M4: the Career's background results. A match between two pros
// is played rally by rally with the real scoring rules (MatchScore),
// each rally won with a probability from the strength gap:
//
//   pro vs pro    p = 1 / (1 + exp(-K  * (sA - sB)))     K  = 1.52
//   you vs pro    p = 1 / (1 + exp(-KH * (r  - s )))     KH = 4.7
//
// K: MatchBench --career-calibrate (2026-10-18), AI vs AI on the game's
// simple hitting system: gaps 0.15 / 0.30 / 0.45 / 0.60 won 56 / 62 /
// 66 / 71% of rallies; the fit is within 0.5% at every gap. The
// personalities' matchups at equal strength were within the bench's
// noise, so there is no matchup term.
// KH: MatchBench --balance - the human-style model wins 59 / 45 / 31%
// of rallies at 0.045 / 0.15 / 0.29 (2026-10-20, after the AI's release
// fix; was 3.0): a steeper curve, with that model at r ~ 0.12. Your r is
// a rating, moved by your real rallies (Rate). The matchups at equal
// strength now spread a little (grinder 35-45%); still no matchup term.
// ===========================================================

namespace SquashSim.Career
{
    public static class RallyModel
    {
        public const double K = 1.52, KH = 4.7;
        public const double RatingStep = 0.006;      // per rally: r += step * (won - expected)

        public static double ProRally(double sA, double sB) { return 1.0 / (1.0 + System.Math.Exp(-K * (sA - sB))); }
        public static double YouRally(double r, double s) { return 1.0 / (1.0 + System.Math.Exp(-KH * (r - s))); }

        /// <summary>Your rating after a match you played: won / total rallies against a pro of strength s.</summary>
        public static double Rate(double r, double s, int won, int total)
        {
            double expected = total * YouRally(r, s);
            r += RatingStep * (won - expected);
            return System.Math.Round(r < -0.3 ? -0.3 : (r > 1.0 ? 1.0 : r), 4);       // 4 decimals: saves load back exactly
        }

        public sealed class Result
        {
            public bool AWon;
            public int RalliesA, RalliesB;
            public string Score;            // games from A's side, "11-7 9-11 11-4"
        }

        /// <summary>A whole match, rally by rally, with A winning each rally with probability pA.</summary>
        public static Result Play(double pA, int gamesToWin, bool english, CareerRng rng)
        {
            var score = new MatchScore(english ? ScoringSystem.English : ScoringSystem.Par, rng.Next() < 0.5 ? 0 : 1, gamesToWin, true);
            var res = new Result();
            var sb = new System.Text.StringBuilder();
            var o = new RallyOutcome { Kind = RallyEnd.NotUp };
            int guard = 0;
            while (!score.MatchOver && guard++ < 2000)
            {
                bool a = rng.Next() < pA;
                o.Winner = a ? 0 : 1; o.Loser = a ? 1 : 0;
                if (a) res.RalliesA++; else res.RalliesB++;
                score.Apply(o);
                if (score.GameOver)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(score.Points[0]).Append('-').Append(score.Points[1]);
                    score.NextGame();
                }
            }
            res.AWon = score.MatchWinner == 0;
            res.Score = sb.ToString();
            return res;
        }
    }

    /// <summary>The Career's random numbers (an LCG, like the sim's others): deterministic from a seed.</summary>
    public sealed class CareerRng
    {
        uint _s;
        public CareerRng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
        public CareerRng(uint seed, int a, int b = 0) : this(Mix(seed, a, b)) { }
        public static uint Mix(uint seed, int a, int b)
        {
            uint h = seed ^ 0x85EBCA6Bu;
            h = unchecked((h ^ (uint)a) * 0x01000193u + 0x9E3779B9u);
            h = unchecked((h ^ (uint)b) * 0x01000193u + 0x7F4A7C15u);
            h ^= h >> 15;
            return h == 0 ? 1u : h;
        }
        public double Next() { _s = unchecked(_s * 1664525u + 1013904223u); return (_s >> 8) / 16777216.0; }
        public int Range(int n) { return (int)(Next() * n) % n; }
        public double Between(double a, double b) { return a + (b - a) * Next(); }
    }
}
