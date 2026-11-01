// ===========================================================
// Assets/SquashSim/Match/SquashCoach.cs
// 2026-11-01: the visual book's coach. Watches a player's shots in a
// match - where they met the ball, where the opponent stood, late or
// not, off the back wall, after the opponent's boast - and checks each
// one against the same 12 rules the AI plays by (SquashBook). After
// the match: the rule broken most, said in plain words ("You hit 4
// cross-courts from the side wall"), or the rule kept most.
//
// Observational only: reads the sim's events, never changes it.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class SquashCoach
    {
        /// <summary>Times a shot broke rule n / followed it (the rule applied and liked the shot), by rule number.</summary>
        public readonly int[] Broken = new int[13], Kept = new int[13];
        /// <summary>Which of the six shots broke rule n (SquashBook.Shots order).</summary>
        public readonly int[,] BrokenBy = new int[13, 6];
        public int Shots { get; private set; }

        int _lastStriker = -1;
        double _strikerZ;
        bool _front, _boast, _back;

        public void Reset()
        {
            System.Array.Clear(Broken, 0, Broken.Length); System.Array.Clear(Kept, 0, Kept.Length); System.Array.Clear(BrokenBy, 0, BrokenBy.Length);
            Shots = 0; _lastStriker = -1; _front = _boast = _back = false;
        }

        /// <summary>After each step: this step's events, the player being coached.</summary>
        public void Observe(MatchSimulation m, IReadOnlyList<SimEvent> events, int who)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == SimEventType.Impact)
                {
                    switch (e.SurfaceClass)
                    {
                        case "frontWall": _front = true; break;
                        case "sideWall": if (!_front && _strikerZ > SquashBook.BackOfCourt) _boast = true; break;
                        case "backWall": if (_front) _back = true; break;
                    }
                }
                else if (e.Type == SimEventType.RacketContact && e.PlayerIndex >= 0 && e.PlayerIndex <= 1)
                {
                    if (e.PlayerIndex == who && m.Referee.Shots > 1) Judge(m, e, who);          // (not the serve)
                    _lastStriker = e.PlayerIndex;
                    _strikerZ = m.Players[e.PlayerIndex].Player.Pos.Z;
                    _front = _boast = _back = false;
                }
            }
        }

        void Judge(MatchSimulation m, SimEvent e, int who)
        {
            var me = m.Players[who].Player.Pos; var op = m.Players[1 - who].Player.Pos;
            var p = new ShotPicture
            {
                BallX = e.Point.X, BallZ = e.Point.Z, MeX = me.X, MeZ = me.Z, OppX = op.X, OppZ = op.Z,
                Late = e.ReachState == ReachStates.DeepLunge || e.ReachState == ReachStates.Stretch,
                OffBackWall = _back, OppBoasted = _boast && _lastStriker == 1 - who
            };
            int shot = Classify(e, out double endX);
            Shots++;
            foreach (int n in SquashBook.Broken(p, shot, endX)) { Broken[n]++; BrokenBy[n, shot]++; }
            for (int n = 2; n <= 12; n++)
                if (SquashBook.Applies(n, p) && SquashBook.Effect(n, p, shot, endX) > 0) Kept[n]++;
        }

        /// <summary>Which of the book's six shots a stroke was: its technique, and for a drive or a lob whether it crossed the
        /// court (where it heads after the front wall).</summary>
        public static int Classify(SimEvent e, out double endX)
        {
            double vz = e.OutVel != null ? e.OutVel.Z : 0, vx = e.OutVel != null ? e.OutVel.X : 0;
            double x = e.Point != null ? e.Point.X : 0, z = e.Point != null ? e.Point.Z : 5;
            endX = x;
            if (vz < -0.1)
            {
                double front = x + vx * (z / -vz);                         // where it meets the front wall
                endX = front + vx * (8.0 / -vz);                           // and about where it's going back down the court
                endX = System.Math.Max(-CourtDimensions.HALF_W, System.Math.Min(CourtDimensions.HALF_W, endX));
            }
            bool cross = System.Math.Sign(endX) != System.Math.Sign(x) && System.Math.Abs(endX) > 0.4;
            switch (e.Technique)
            {
                case "boast": endX = -System.Math.Sign(x) * 2.6; return SquashBook.ShotIndex("boast");
                case "drop": return SquashBook.ShotIndex("drop");
                case "kill": return SquashBook.ShotIndex("kill");
                case "lob": return SquashBook.ShotIndex(cross ? "lob" : "straight");   // the book's lob is the cross-court one
                default: return SquashBook.ShotIndex(cross ? "cross" : "straight");
            }
        }

        /// <summary>The rule broken most (at least twice), and the shot that broke it most; 0 if none.</summary>
        public int WorstRule(out int count, out int shot)
        {
            int best = 0; count = 0; shot = 0;
            for (int n = 2; n <= 12; n++) if (Broken[n] > count) { count = Broken[n]; best = n; }
            if (count < 2) { count = 0; return 0; }
            for (int s = 0, top = -1; s < 6; s++) if (BrokenBy[best, s] > top) { top = BrokenBy[best, s]; shot = s; }
            return best;
        }

        /// <summary>The rule followed most (at least three times); 0 if none.</summary>
        public int BestRule(out int count)
        {
            int best = 0; count = 0;
            for (int n = 2; n <= 12; n++) if (Kept[n] > count) { count = Kept[n]; best = n; }
            if (count < 3) { count = 0; return 0; }
            return best;
        }

        static readonly string[] One = { "straight drive", "cross-court", "cross-court lob", "drop", "kill", "boast" };
        static readonly string[] Many = { "straight drives", "cross-courts", "cross-court lobs", "drops", "kills", "boasts" };

        /// <summary>Where the rule's picture was, in words ("from the side wall").</summary>
        public static string Where(int rule)
        {
            switch (rule)
            {
                case 2: return "when you were stretched";
                case 3: return "with them in front of you";
                case 4: return "with them behind you";
                case 6: return "with them on the T";
                case 7: return "from behind the T";
                case 10: return "off the back wall";
                case 11: return "from the side wall";
                case 12: return "after their boast";
                default: return "";
            }
        }

        /// <summary>The coach's line after a match: the rule broken most, else the rule kept most; null if too few shots.</summary>
        public string Tip(out int rule)
        {
            rule = WorstRule(out int n, out int shot);
            if (rule > 0) return "You hit " + n + " " + (n == 1 ? One[shot] : Many[shot]) + " " + Where(rule) + ".";
            rule = BestRule(out n);
            if (rule > 0) return "You followed this rule " + n + " times. Great squash!";
            return null;
        }
    }
}
