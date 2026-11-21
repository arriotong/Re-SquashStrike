// ===========================================================
// Assets/SquashSim/Modes/BookPuzzles.cs
// 2026-11-21 the shot puzzle remade on the user's rulebook ("Squash Rules:
// The Visual Book", 12 rules - the user: "most of the time it's not what I
// am thinking of, can we redo the whole decision system ... follow that").
// The old puzzles froze an AI rally and called "best" whichever shot won
// most of six played-out tries; these follow the book:
//
//   the moment   built from a rule: you, the opponent and the ball placed so
//                that rule clearly applies (a seed varies the side, the
//                spots, the flavour) - every rule has its own moments
//   the choice   a shot AND where it goes ("Straight drive to the back",
//                "Drop into the front corner", "Volley it straight",
//                "Wait for it off the back wall", "Stop: let, please", ...)
//   the verdict  BEST  the book's shot for this moment
//                OK    breaks no rule that applies, but not the book's shot
//                WRONG breaks a rule that applies (which, and why)
//   checked      a moment is kept only when its book shot breaks nothing
//                and the four choices shown hold one BEST and a WRONG
//
// Rule 9 ("after every shot, go back to the T") is about moving, not the
// shot: its moments ask where you go next. Everything is deterministic
// (the seed); no bank - the day's puzzle is made on the spot.
// ===========================================================

using System;
using System.Collections.Generic;


namespace SquashSim.Modes
{
    public enum Verdict { Wrong, Ok, Best }

    /// <summary>A moment: where you meet the ball, where you and they are, and what kind of ball it is.</summary>
    [Serializable]
    public sealed class BookMoment
    {
        public double BallX, BallZ, BallY;
        public double MeX, MeZ, OppX, OppZ;
        public bool Late, OffBackWall, Passing, OppBoasted, InTheWay;
        /// <summary>Rule 9's moments: you have just played this shot (they ask where you go).</summary>
        public bool AfterShot;
        public int Side { get { return BallX >= 0 ? 1 : -1; } }
        public bool Tight { get { return Math.Abs(BallX) > CourtDimensions.HALF_W - SquashBook.TightGap; } }
    }

    /// <summary>One choice: a shot and where it goes (or, for rule 9, where you go).</summary>
    public sealed class BookChoice
    {
        public string Id, Name;
        public bool Front, Straight, High, Volley, Wait, Let, Move;
        /// <summary>The AI's shot to play it with (OpponentBrain.ForceAim): the arcade shot name, aim height, charge, a boast.</summary>
        public string Shot; public double AimY, Charge; public bool Boast;
    }

    [Serializable]
    public sealed class BookPuzzle
    {
        public uint Seed;
        public int Rule;
        public BookMoment Moment = new BookMoment();
        public List<string> Options = new List<string>();
        public List<Verdict> Verdicts = new List<Verdict>();
        public List<string> Reasons = new List<string>();
        public int Best;
        /// <summary>The moment, in a line (what to look at).</summary>
        public string Look;
        public bool Solves(int option) { return option == Best; }
        public Verdict VerdictOf(int option) { return option >= 0 && option < Verdicts.Count ? Verdicts[option] : Verdict.Wrong; }
    }

    public static class BookPuzzles
    {
        // ---------------- the choices ----------------

        public static readonly BookChoice[] Choices =
        {
            new BookChoice { Id = "drive_straight", Name = "Straight drive to the back", Straight = true, Shot = "straight", AimY = 1.7 },
            new BookChoice { Id = "drive_cross", Name = "Cross-court drive to the back", Shot = "cross", AimY = 1.8 },
            new BookChoice { Id = "lob_straight", Name = "High lob, straight", Straight = true, High = true, Shot = "lob", AimY = 3.6 },
            new BookChoice { Id = "lob_cross", Name = "High lob across the court", High = true, Shot = "lob", AimY = 3.6 },
            new BookChoice { Id = "drop_straight", Name = "Drop into the front corner", Front = true, Straight = true, Shot = "drop", AimY = 0.95 },
            new BookChoice { Id = "drop_cross", Name = "Cross-court drop", Front = true, Shot = "drop", AimY = 0.95 },
            new BookChoice { Id = "boast", Name = "Boast into the front", Front = true, Shot = "boast", AimY = 1.2, Boast = true },
            new BookChoice { Id = "kill", Name = "Kill it low", Front = true, Straight = true, Shot = "kill", AimY = 0.92, Charge = 0.45 },
            new BookChoice { Id = "volley_straight", Name = "Volley it straight, early", Straight = true, Volley = true, Shot = "straight", AimY = 1.7 },
            new BookChoice { Id = "rush_early", Name = "Hit it early, before the back wall", Straight = true, Volley = true, Shot = "straight", AimY = 1.7 },
            new BookChoice { Id = "wait_straight", Name = "Wait for it, then hit straight", Straight = true, Wait = true, Shot = "straight", AimY = 1.7 },
            new BookChoice { Id = "let", Name = "Stop: \"Let, please\"", Let = true },
            // rule 9: where you go after your shot
            new BookChoice { Id = "go_t", Name = "Go straight back to the T", Move = true },
            new BookChoice { Id = "watch", Name = "Stay where you are and watch it", Move = true },
            new BookChoice { Id = "follow", Name = "Follow the ball to the back", Move = true },
            new BookChoice { Id = "front_wall", Name = "Run up to the front wall", Move = true },
        };

        public static BookChoice Choice(string id) { foreach (var c in Choices) if (c.Id == id) return c; return null; }
        public static string Name(string id) { var c = Choice(id); return c != null ? c.Name : id; }

        /// <summary>Where a choice sends the ball across the court: +1 your side (the ball's), -1 the other.</summary>
        static int EndSide(BookChoice c, BookMoment m) { return (c.Straight ? 1 : -1) * m.Side; }

        // ---------------- the rules on a moment ----------------

        const double TZ = SquashBook.TZ;

        public static bool OppInFront(BookMoment m) { return m.OppZ < m.MeZ - 1.0; }
        public static bool OppBehind(BookMoment m) { return m.OppZ > m.MeZ + 1.0; }
        public static bool OppOnT(BookMoment m) { double dx = m.OppX, dz = m.OppZ - TZ; return dx * dx + dz * dz < 0.8 * 0.8; }
        public static bool OppOnASide(BookMoment m) { return Math.Abs(m.OppX) > 0.9 && !OppOnT(m); }

        /// <summary>Does rule n speak to this moment?</summary>
        public static bool Applies(int n, BookMoment m)
        {
            if (m.AfterShot) return n == 9;
            switch (n)
            {
                case 1: return m.InTheWay;
                case 2: return m.Late;
                case 3: return OppInFront(m);
                case 4: return OppBehind(m) && m.BallZ < TZ + 0.5;
                case 5: return OppOnASide(m);
                case 6: return OppOnT(m);
                case 7: return m.BallZ > TZ + 0.5 && !OppBehind(m) && !m.OppBoasted;
                case 8: return m.Passing;
                case 10: return m.OffBackWall;
                case 11: return m.Tight;
                case 12: return m.OppBoasted && m.BallZ < TZ;
                default: return false;
            }
        }

        /// <summary>Does choice c break rule n (which applies)? The reason, or null.</summary>
        public static string Breaks(int n, BookMoment m, BookChoice c)
        {
            if (n == 9) return c.Id == "go_t" ? null : c.Id == "watch" ? "you stopped to watch - they're already moving" : c.Id == "follow" ? "you left the middle of the court open" : "you left the whole back of the court open";
            if (n == 1) return c.Let ? null : "you could hit them - stop and ask for a let";
            if (c.Let) return "nobody was in your way - play the ball";
            if (c.Move) return null;
            switch (n)
            {
                case 2: return c.Front ? "you were late - a short shot from there is a gift" : !c.High && !c.Straight ? "you were late - a fast cross-court leaves you no time to recover" : null;
                case 3: return c.Front ? "they were in front of you - a short ball runs straight to them" : null;
                case 4: return !c.Front && !c.Volley && !c.Wait ? "they were behind you - a deep ball goes right back to them" : null;
                case 5: return EndSide(c, m) == Math.Sign(m.OppX) ? "it went to the side they were on" : null;
                case 6: return !c.Straight && !c.Front ? "they were on the T - a cross-court goes right past them" : null;
                case 7: return c.Front ? "you were behind the T - going short from there leaves the front open" : null;
                case 8: return null;
                case 10: return c.Id == "rush_early" ? "you rushed it - it was coming off the back wall, you had time"
                              : c.Front ? "off the back wall, wait and hit it straight to the back - a short ball from there leaves the front open"
                              : !c.Straight ? "off the back wall, hit it straight - a cross-court comes back to the middle" : null;
                case 11: return !c.Straight && !c.Front ? "it was stuck on the wall - a cross-court from there ends up in the middle" : c.High && !c.Straight ? "it was stuck on the wall - across the court it lands in the middle" : null;
                case 12: return c.High ? "they were stuck at the back - a lob gives them time to get back" : null;
                default: return null;
            }
        }

        /// <summary>The book's shot for this moment, by its rule.</summary>
        public static string Ideal(int rule, BookMoment m)
        {
            switch (rule)
            {
                case 1: return "let";
                case 2: return "lob_straight";
                case 3: return OppOnASide(m) && Math.Sign(m.OppX) == m.Side ? "drive_cross" : "drive_straight";
                case 4: case 12: return OppOnASide(m) && Math.Sign(m.OppX) == m.Side ? "drop_cross" : "drop_straight";
                case 5: return OppBehind(m) ? (Math.Sign(m.OppX) == m.Side ? "drop_cross" : "drop_straight") : (Math.Sign(m.OppX) == m.Side ? "drive_cross" : "drive_straight");
                case 6: return "drive_straight";
                case 7: return "drive_straight";
                case 8: return "volley_straight";
                case 9: return "go_t";
                case 10: return "wait_straight";
                case 11: return "drive_straight";
                default: return "drive_straight";
            }
        }

        /// <summary>A choice's verdict on a moment (its rule the one asked about): its reason too.</summary>
        public static Verdict Judge(int rule, BookMoment m, BookChoice c, out string reason)
        {
            // the puzzle's own rule first (its reason is the one to read), then rule 1, then the rest
            var order = new List<int> { rule, 1 };
            for (int k = 2; k <= 12; k++) if (k != rule) order.Add(k);
            foreach (int n in order)
            {
                if (!Applies(n, m)) continue;
                var why = Breaks(n, m, c);
                if (why != null) { reason = "Breaks rule " + n + ": " + why + "."; return Verdict.Wrong; }
                if (n == 1) break;                                  // in the way: nothing else matters
            }
            if (c.Id == Ideal(rule, m)) { reason = "Rule " + rule + ": " + SquashBook.Get(rule).Title; return Verdict.Best; }
            reason = "Breaks no rule, but the book's shot here is: " + Name(Ideal(rule, m)).ToLowerInvariant() + ".";
            return Verdict.Ok;
        }

        // ---------------- playing one (the game's puzzle mode) ----------------

        /// <summary>The match it is played in: two AIs (one stands in for you), the opponent a little weaker, the current book.</summary>
        public static Replays.ReplaySetup Setup(BookPuzzle p)
        {
            return new Replays.ReplaySetup { Opponent0 = "shotmaker", Difficulty0 = 0.75f, Opponent = "grinder", Difficulty = 0.62f, GamesToWin = 1, Scoring = "par",
                                             WarmUp = false, Lets = false, BotSeed = p.Seed == 0 ? 1u : p.Seed, Book = AiProfile.CurrentBook };
        }

        /// <summary>The ball's flight into the moment: where it is a beat before you meet it, how it's moving, how many floor bounces it
        /// has had since the front wall - each kind of ball its own way in.</summary>
        public static void Flight(BookMoment m, out V3 pos, out V3 vel, out int bounces)
        {
            int s = m.Side;
            bounces = 0;
            if (m.OffBackWall) { pos = new V3(m.BallX, 1.0, 9.1); vel = new V3(0, 0.6, 3.2); bounces = 1; return; }
            if (m.Passing) { pos = new V3(m.BallX, 1.75, m.BallZ - 3.2); vel = new V3(0, 0.9, 10.5); return; }
            if (m.OppBoasted) { pos = new V3(m.BallX - s * 1.0, 0.9, Math.Max(0.4, m.BallZ - 1.2)); vel = new V3(s * 2.8, 1.4, 3.4); return; }
            const double t = 0.38;
            double vz = m.BallZ < 4.5 ? 4.0 : 7.0;
            pos = new V3(m.BallX, m.BallY + 0.5 * 9.81 * t * t, Math.Max(0.4, m.BallZ - vz * t));
            vel = new V3(0, 0, vz);
        }

        /// <summary>A choice as the shot the AI standing in for you plays (rule 9's moves: a straight drive, then the move).</summary>
        public static OpponentBrain.Aim AimFor(BookChoice c, BookMoment m)
        {
            int s = m.Side;
            if (c.Move || c.Let) c = Choice("drive_straight");
            double x;
            switch (c.Id)
            {
                case "drive_cross": x = -s * 0.7; break;
                case "lob_straight": x = s * 2.4; break;
                case "lob_cross": x = -s * 0.8; break;
                case "drop_straight": x = s * 2.5; break;
                case "drop_cross": x = -s * 2.5; break;
                case "boast": x = -s * 1.5; break;
                case "kill": x = s * 2.4; break;
                default: x = s * 2.75; break;
            }
            return new OpponentBrain.Aim { Shot = c.Shot ?? "straight", X = x, Y = c.AimY, Charge = c.Charge, Boast = c.Boast };
        }

        // ---------------- making one ----------------

        public static readonly int[] RuleOrder = { 3, 4, 5, 6, 2, 7, 11, 10, 8, 12, 1, 9 };

        /// <summary>A puzzle from a seed (the rule it's about comes from the seed, every rule in turn).</summary>
        public static BookPuzzle Make(uint seed) { return Make(seed, RuleOrder[(int)(Hash(seed) % (uint)RuleOrder.Length)]); }

        /// <summary>A puzzle about rule <paramref name="rule"/>: tries seeds from <paramref name="seed"/> until the moment is clear.</summary>
        public static BookPuzzle Make(uint seed, int rule)
        {
            for (uint k = 0; k < 64; k++)
            {
                var p = TryMake(seed + k * 7919u, rule);
                if (p != null) { p.Seed = seed; return p; }
            }
            return null;
        }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        sealed class Rng
        {
            uint _s;
            public Rng(uint s) { _s = s == 0 ? 1u : s; }
            public double U() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / (double)0x1000000; }
            public double R(double a, double b) { return a + (b - a) * U(); }
            public int Sign() { return U() < 0.5 ? -1 : 1; }
        }

        static BookPuzzle TryMake(uint seed, int rule)
        {
            var r = new Rng(Hash(seed * 2654435761u + (uint)rule));
            var m = new BookMoment { BallY = 0.8 };
            int s = r.Sign();                                         // the side the ball is on (your side)
            string[] pool;
            switch (rule)
            {
                case 1:   // in the way: they're between you and the ball
                    m.BallX = s * r.R(1.8, 2.5); m.BallZ = r.R(6.2, 7.6); m.MeX = s * r.R(0.6, 1.0); m.MeZ = m.BallZ + r.R(0.4, 0.9);
                    m.OppX = (m.MeX + m.BallX) / 2; m.OppZ = (m.MeZ + m.BallZ) / 2 - 0.3; m.InTheWay = true;
                    pool = new[] { "let", "drive_straight", "drive_cross", "boast", "lob_straight" }; break;
                case 2:   // late: the ball deep in the corner, you stretched
                    m.BallX = s * r.R(2.5, 2.9); m.BallZ = r.R(8.7, 9.3); m.MeX = s * r.R(1.3, 1.7); m.MeZ = r.R(7.4, 7.9); m.Late = true;
                    m.OppX = r.R(-0.4, 0.4); m.OppZ = r.R(4.7, 5.3);
                    pool = new[] { "lob_straight", "drive_straight", "drop_straight", "drive_cross", "boast", "kill" }; break;
                case 3:   // they're in front
                    m.BallX = s * r.R(1.2, 2.2); m.BallZ = r.R(6.2, 7.4); m.MeX = m.BallX - s * 0.5; m.MeZ = m.BallZ + 0.4;
                    m.OppX = r.U() < 0.5 ? s * r.R(1.2, 2.2) : -s * r.R(1.2, 2.2); m.OppZ = r.R(3.0, 4.2);
                    pool = new[] { "drive_straight", "drive_cross", "drop_straight", "drop_cross", "boast", "kill" }; break;
                case 4:   // they're behind
                    m.BallX = s * r.R(1.0, 2.2); m.BallZ = r.R(2.6, 4.0); m.MeX = m.BallX - s * 0.5; m.MeZ = m.BallZ + 0.4;
                    m.OppX = r.U() < 0.5 ? s * r.R(1.2, 2.2) : -s * r.R(1.2, 2.2); m.OppZ = r.R(7.2, 8.6);
                    pool = new[] { "drop_straight", "drop_cross", "drive_straight", "drive_cross", "lob_cross", "lob_straight" }; break;
                case 5:   // they're on one side, level with you
                    m.BallX = s * r.R(0.8, 1.6); m.BallZ = r.R(4.6, 5.4); m.MeX = m.BallX - s * 0.4; m.MeZ = m.BallZ + 0.3;
                    m.OppX = (r.U() < 0.5 ? s : -s) * r.R(1.8, 2.5); m.OppZ = m.MeZ + r.R(-0.6, 0.6);
                    pool = new[] { "drive_straight", "drive_cross", "lob_straight", "lob_cross" }; break;
                case 6:   // they're on the T
                    m.BallX = s * r.R(2.0, 2.6); m.BallZ = r.R(6.8, 7.8); m.MeX = m.BallX - s * 0.5; m.MeZ = m.BallZ + 0.4;
                    m.OppX = r.R(-0.3, 0.3); m.OppZ = r.R(4.8, 5.2);
                    pool = new[] { "drive_straight", "drive_cross", "lob_cross", "drop_straight", "boast" }; break;
                case 7:   // you're behind the T, they're not behind you
                    m.BallX = s * r.R(1.0, 2.0); m.BallZ = r.R(7.4, 8.4); m.MeX = m.BallX - s * 0.4; m.MeZ = m.BallZ + 0.4;
                    m.OppX = r.R(-0.5, 0.5); m.OppZ = r.R(5.6, 6.4);
                    pool = new[] { "drive_straight", "drop_straight", "kill", "boast", "drop_cross" }; break;
                case 8:   // the ball going past you to the back, and you can reach it
                    m.BallX = s * r.R(0.9, 1.5); m.BallZ = r.R(4.6, 5.4); m.BallY = 1.5; m.MeX = s * r.R(0.2, 0.5); m.MeZ = r.R(5.0, 5.6); m.Passing = true;
                    m.OppX = -s * r.R(1.2, 2.0); m.OppZ = r.R(3.0, 3.8);
                    pool = new[] { "volley_straight", "drive_straight", "drop_straight", "boast", "drop_cross" }; break;
                case 10:  // off the back wall
                    m.BallX = s * r.R(1.6, 2.4); m.BallZ = r.R(8.4, 9.0); m.MeX = m.BallX - s * 0.6; m.MeZ = r.R(7.2, 7.8); m.OffBackWall = true;
                    m.OppX = r.R(-0.4, 0.4); m.OppZ = r.R(4.7, 5.3);
                    pool = new[] { "wait_straight", "rush_early", "drive_cross", "boast" }; break;
                case 11:  // stuck on the side wall
                    m.BallX = s * r.R(2.85, 3.05); m.BallZ = r.R(6.4, 7.6); m.MeX = s * r.R(2.0, 2.3); m.MeZ = m.BallZ + 0.4;
                    m.OppX = r.R(-0.5, 0.5); m.OppZ = r.R(4.8, 5.4);
                    pool = new[] { "drive_straight", "drive_cross", "lob_cross", "boast", "drop_cross" }; break;
                case 12:  // their boast from the back comes to the front
                    m.BallX = s * r.R(1.9, 2.6); m.BallZ = r.R(1.6, 2.6); m.MeX = s * r.R(0.6, 1.2); m.MeZ = r.R(3.4, 4.2); m.OppBoasted = true;
                    m.OppX = -s * r.R(1.6, 2.4); m.OppZ = r.R(8.2, 8.9);
                    pool = new[] { "drop_straight", "drive_straight", "lob_straight", "lob_cross", "drive_cross" }; break;
                case 9:   // you've just played a straight drive: where now?
                    m.BallX = s * r.R(2.2, 2.7); m.BallZ = r.R(7.4, 8.2); m.MeX = s * r.R(1.8, 2.2); m.MeZ = m.BallZ + 0.3; m.AfterShot = true;
                    m.OppX = -s * r.R(0.6, 1.4); m.OppZ = r.R(5.2, 6.0);
                    pool = new[] { "go_t", "watch", "follow", "front_wall" }; break;
                default: return null;
            }

            // the rule must apply and its shot break nothing; four choices: the book's, at least one wrong
            if (!Applies(rule, m)) return null;
            string ideal = Ideal(rule, m);
            var idealC = Choice(ideal);
            if (Judge(rule, m, idealC, out _) != Verdict.Best) return null;
            var p = new BookPuzzle { Rule = rule, Moment = m };
            var rest = new List<string>(pool); rest.Remove(ideal);
            for (int i = rest.Count - 1; i > 0; i--) { int j = (int)(r.U() * (i + 1)); var t = rest[i]; rest[i] = rest[j]; rest[j] = t; }
            var pick = new List<string> { ideal };
            bool wrong = false;
            foreach (var id in rest)
            {
                if (pick.Count >= 4) break;
                var v = Judge(rule, m, Choice(id), out _);
                if (v == Verdict.Wrong) wrong = true;
                if (pick.Count == 3 && !wrong && v != Verdict.Wrong) continue;    // keep a place for a wrong one
                pick.Add(id);
            }
            if (!wrong || pick.Count < 3) return null;
            for (int i = pick.Count - 1; i > 0; i--) { int j = (int)(r.U() * (i + 1)); var t = pick[i]; pick[i] = pick[j]; pick[j] = t; }
            foreach (var id in pick)
            {
                var v = Judge(rule, m, Choice(id), out string why);
                p.Options.Add(id); p.Verdicts.Add(v); p.Reasons.Add(why);
                if (v == Verdict.Best) p.Best = p.Options.Count - 1;
            }
            int bests = 0; foreach (var v in p.Verdicts) if (v == Verdict.Best) bests++;
            if (bests != 1) return null;
            p.Look = LookOf(rule, m);
            return p;
        }

        /// <summary>What to look at, in a line (never the answer).</summary>
        static string LookOf(int rule, BookMoment m)
        {
            switch (rule)
            {
                case 1: return "The ball's coming - and they're right there. What now?";
                case 2: return "The ball's deep in the corner and you're stretching for it.";
                case 9: return "You've just hit a straight drive. Where do you go now?";
                case 10: return "The ball's going into the back wall behind you.";
                case 11: return "The ball's hugging the side wall.";
                case 12: return "They boasted from the back - the ball's coming to the front.";
                case 8: return "The ball's going past you at shoulder height.";
                default: return "Look where they're standing. Where do you hit it?";
            }
        }
    }
}
