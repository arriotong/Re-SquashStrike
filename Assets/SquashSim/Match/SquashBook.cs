// ===========================================================
// Assets/SquashSim/Match/SquashBook.cs
// "Squash Rules: The Visual Book" (2026-11-01, the user's 12 rules)
// as checks on a shot's picture: where the ball is met, where the
// striker and the opponent stand, whether the striker is late, the
// ball tight on a side wall, off the back wall, or the opponent's
// boast. Each rule that applies says which of the six shots it likes
// and which it forbids.
//
// The AI adds the rules to its shot scores, weighted by its squash
// sense (rating) and its style; the coach reads the same rules to
// tell a player which one a shot broke. Rules 1, 8 and 9 are about
// moving, not the shot: the brain does those itself.
//
// Pure data and arithmetic: deterministic, no randomness, no Unity.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    /// <summary>The picture a shot is played in (court metres: x across, 0 the middle; z from the front wall).</summary>
    public struct ShotPicture
    {
        public double BallX, BallZ;        // where the ball is met
        public double MeX, MeZ;            // the striker at contact
        public double OppX, OppZ;          // the opponent
        public bool Late;                  // stretched or late on it
        public bool OffBackWall;           // it came off the back wall
        public bool OppBoasted;            // the opponent's last shot was a boast from the back of the court
    }

    public static class SquashBook
    {
        /// <summary>The six shots, in the order every table below uses.</summary>
        public static readonly string[] Shots = { "straight", "cross", "lob", "drop", "kill", "boast" };

        public const double TZ = 5.0;                          // the T (PhysicsConfig's Match.TZ)
        public const double TightGap = 0.4;                    // a ball this close to a side wall is "stuck" on it
        public const double FrontBehindGap = 1.0;              // the opponent is in front / behind beyond this
        public const double BackOfCourt = 6.0;                 // a boast played from behind this is "from the back"

        /// <summary>A rule's number, its words (the book's), and what it says about each shot (+ do it, - don't).</summary>
        public sealed class Rule
        {
            public int N;
            public string Title, Words;
            public double[] Effect;       // per shot (Shots order); null: rule 5's own sum (by where the ball goes)
        }

        public static readonly Rule[] Rules =
        {
            new Rule { N = 1, Title = "Opponent in the way? Stop.", Words = "If you might hit them with the ball or your racket, don't play the shot. Stop and ask for a let." },
            new Rule { N = 2, Title = "Late? Hit it high.", Words = "When you're late or stretched, hit high and straight. It gives you time to get back to the T.",
                       Effect = new[] { 0.4, -0.6, 1.2, -0.8, -1.0, -0.3 } },
            new Rule { N = 3, Title = "Opponent in front? Hit to the back.", Words = "Hit deep into a back corner so they have to turn and run backwards.",
                       Effect = new[] { 0.5, 0.5, 0.4, -0.6, -0.5, -0.6 } },
            new Rule { N = 4, Title = "Opponent behind? Hit to the front.", Words = "Play a short shot into a front corner: the longest run on the court for them.",
                       Effect = new[] { -0.2, -0.2, -0.4, 0.7, 0.5, 0.3 } },
            new Rule { N = 5, Title = "Opponent on one side? Hit to the other.", Words = "Always hit to the space they're not in." },
            new Rule { N = 6, Title = "Opponent on the T? Hit it down the wall.", Words = "Hit straight and tight along the side wall to the back. It pulls them off the T.",
                       Effect = new[] { 0.7, -0.4, 0.0, 0.0, 0.0, -0.2 } },
            new Rule { N = 7, Title = "Attack only from in front of the T.", Words = "Only go short (a drop, kill or boast) from in front of the T with them behind you. From the back, hit length.",
                       Effect = new[] { 0.0, 0.0, 0.0, -0.9, -0.9, -0.6 } },
            new Rule { N = 8, Title = "Ball going to the back? Volley it.", Words = "If it's going past you and you can reach it balanced, hit it before it bounces." },
            new Rule { N = 9, Title = "After every shot, go back to the T.", Words = "Start moving back as soon as you finish your swing. Don't stop to watch." },
            new Rule { N = 10, Title = "Ball off the back wall? Wait for it.", Words = "Let it come out, then hit it straight. You have more time than it feels like.",
                       Effect = new[] { 0.7, -0.4, 0.0, -0.3, -0.4, 0.0 } },
            new Rule { N = 11, Title = "Ball stuck on the side wall? Hit it straight back.", Words = "Never cross-court from the wall: it ends up in the middle.",
                       Effect = new[] { 0.5, -1.2, -0.6, 0.0, 0.0, 0.0 } },     // (the book forbids the cross-court, not the boast:
                                                                                 // MatchBench --book - forbidding the boast too cost the No. 1 a tenth of its rallies)
            new Rule { N = 12, Title = "Their boast comes to the front? Drop it.", Words = "They're stuck at the back: move in early and drop it into the front corner.",
                       Effect = new[] { 0.0, 0.0, -0.3, 0.9, 0.4, 0.0 } },
        };

        public static Rule Get(int n) { return Rules[n - 1]; }

        public static int ShotIndex(string shot) { return System.Array.IndexOf(Shots, shot); }

        static bool OppInFront(ShotPicture p) { return p.OppZ < p.MeZ - FrontBehindGap; }
        static bool OppBehind(ShotPicture p) { return p.OppZ > p.MeZ + FrontBehindGap; }
        static bool OppOnT(ShotPicture p) { double dx = p.OppX, dz = p.OppZ - TZ; return dx * dx + dz * dz < 0.8 * 0.8; }

        /// <summary>Does this rule speak to the picture?</summary>
        public static bool Applies(int n, ShotPicture p)
        {
            switch (n)
            {
                case 2: return p.Late;
                case 3: return OppInFront(p);
                case 4: return OppBehind(p) && p.BallZ < TZ + 0.5;
                case 5: return System.Math.Abs(p.OppX) > 0.7;
                case 6: return OppOnT(p);
                case 7: return p.BallZ > TZ + 0.5 && !OppBehind(p) && !p.OppBoasted;
                case 10: return p.OffBackWall;
                case 11: return System.Math.Abs(p.BallX) > CourtDimensions.HALF_W - TightGap;
                case 12: return p.OppBoasted && p.BallZ < TZ;
                default: return false;
            }
        }

        /// <summary>What rule n says about a shot that ends up across the court at endX (rule 5 goes by that).</summary>
        public static double Effect(int n, ShotPicture p, int shot, double endX)
        {
            var r = Rules[n - 1];
            if (n == 5) return System.Math.Sign(endX) != System.Math.Sign(p.OppX) ? 0.4 : -0.3;
            return r.Effect == null || shot < 0 ? 0 : r.Effect[shot];
        }

        /// <summary>A rule this much against a shot forbids it (the coach's "you broke rule n").</summary>
        public const double BreakAt = -0.4;

        /// <summary>The rules that apply and forbid this shot (most forbidding first) - the coach's "you broke rule n".</summary>
        public static List<int> Broken(ShotPicture p, int shot, double endX)
        {
            var o = new List<int>();
            for (int n = 2; n <= 12; n++)
                if (Applies(n, p) && Effect(n, p, shot, endX) <= BreakAt) o.Add(n);
            o.Sort((a, b) => Effect(a, p, shot, endX).CompareTo(Effect(b, p, shot, endX)));
            return o;
        }

        /// <summary>The AI's bonus for a shot: every rule that applies, by its effect, the profile's leaning on that kind of rule,
        /// and its squash sense.</summary>
        public static double Bonus(AiProfile prof, ShotPicture p, int shot, double endX)
        {
            double sum = 0;
            for (int n = 2; n <= 12; n++)
            {
                if (!Applies(n, p)) continue;
                sum += Lean(prof, n) * Effect(n, p, shot, endX);
            }
            return Scale * prof.Sense * sum;
        }

        /// <summary>How much a style leans on rule n: the safe-length rules, the attacking ones, the patience of rule 7.</summary>
        public static double Lean(AiProfile prof, int n)
        {
            switch (n)
            {
                case 2: case 3: case 6: case 10: case 11: return prof.BookSafe;
                case 4: case 5: case 12: return prof.BookAttack;
                case 7: return prof.BookPatience;
                case 8: return prof.BookVolley;
                default: return 1;
            }
        }

        /// <summary>The rules' weight against the rest of the AI's shot score (MatchBench --stars).</summary>
        public const double Scale = 1.0;
    }
}
