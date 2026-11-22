// ===========================================================
// Assets/SquashSim/Career/Stars.cs
// P6: the stars - the world's top 50, each a person with a play style,
// a rating, attributes and signature shots - and the club players
// beginners start against. Every name here is made up; the game's name
// editor (PlayerNames) can rename anyone on a player's own PC.
//
//   rating     FC-style, 55..99: the difficulty the AI plays at
//              (Difficulty), calibrated with MatchBench --stars
//   the No. 1  99 - the AI's top execution, attributes at 9-10, three
//              signature shots: the hardest opponent in the game
//   ids        "star:s01" .. "star:s50", "club:retriever:62" - and the
//              old personality names ("retriever"...) still work
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    public sealed class Star
    {
        public string Id, First, Last, Nat, Title, Bio;
        public int Rank, Age, Rating;
        public char Hand = 'R';
        public string Style;
        /// <summary>Personal tweaks on the style: added to its risk, touch and deception; discipline set when > 0.</summary>
        public double Risk, Touch, Deception, Discipline;
        public AthleteAttributes Attributes;
        public List<RunMod> Signature = new List<RunMod>();
        public uint KitSeed;

        public string OpponentId { get { return "star:" + Id; } }
    }

    public static class Stars
    {
        /// <summary>Set by the game: a name the player gave this star (first, last), or null for the star's own.</summary>
        public static Func<string, (string first, string last)?> NameOverride;

        // ---------------- the rating's difficulty ----------------

        static readonly (int rating, double d)[] Curve =
        {
            (55, 0.00), (60, 0.01), (70, 0.06), (80, 0.13), (85, 0.17), (90, 0.24), (95, 0.40), (98, 0.70), (99, 1.00),
        };

        /// <summary>The AI's difficulty (0..1) for a rating - a straight line between the calibrated points.</summary>
        public static double Difficulty(int rating)
        {
            if (rating <= Curve[0].rating) return Curve[0].d;
            for (int i = 1; i < Curve.Length; i++)
                if (rating <= Curve[i].rating)
                {
                    var a = Curve[i - 1]; var b = Curve[i];
                    return a.d + (b.d - a.d) * (rating - a.rating) / (double)(b.rating - a.rating);
                }
            return Curve[Curve.Length - 1].d;
        }

        // ---------------- the 50 ----------------

        static List<Star> _all;
        static Dictionary<string, Star> _byId;

        /// <summary>The touch shots lower the ball (Trickle Boast, Volley Drop): past level 2 the AI's aim puts them in the tin.</summary>
        public static int MaxLevel(string id) { return id == "trickle" || id == "vdrop" ? 2 : 5; }

        /// <summary>Signature shots that fit a style (sim abilities: RunMods ids).</summary>
        static List<RunMod> SignatureFor(string style, int level)
        {
            RunMod M(string id, int l) { return new RunMod { Id = id, Level = Math.Max(1, Math.Min(MaxLevel(id), l)) }; }
            switch (style)
            {
                case "attacker": return new List<RunMod> { M("nick", level) };
                case "shotmaker": return new List<RunMod> { M("trickle", level), M("vdrop", level - 1) };
                case "grinder": return new List<RunMod> { M("rail", level) };
                case "powerhouse": return new List<RunMod> { M("wide", level), M("thunder", level - 1) };
                case "counter": return new List<RunMod> { M("vdrop", level) };
                case "magician": return new List<RunMod> { M("trickle", level), M("vdrop", level) };
                case "volleyer": return new List<RunMod> { M("sniper", level), M("rail", level - 1) };
                default: return new List<RunMod> { M("hlob", level) };
            }
        }

        static AthleteAttributes Attr(double s, double st, double p, double c) { return new AthleteAttributes { Speed = s, Stamina = st, Power = p, Control = c }; }

        public static List<Star> All()
        {
            if (_all != null) return _all;
            _all = new List<Star>();
            void Top(string first, string last, string nat, int age, char hand, string style, int rating, AthleteAttributes a, List<RunMod> sig,
                     double risk, double touch, double dec, double disc, string title, string bio)
            {
                int rank = _all.Count + 1;
                _all.Add(new Star
                {
                    Id = "s" + rank.ToString("00"), Rank = rank, First = first, Last = last, Nat = nat, Age = age, Hand = hand, Style = style, Rating = rating,
                    Attributes = a, Signature = sig, Risk = risk, Touch = touch, Deception = dec, Discipline = disc, Title = title, Bio = bio,
                    KitSeed = (uint)(0x5A11 + rank * 7919),
                });
            }
            RunMod Sg(string id, int l) { return new RunMod { Id = id, Level = Math.Min(MaxLevel(id), l) }; }

            // ---- the top 10: each their own (2026-11-22: their game from public match reports and features - ProPersonas;
            // the names here are the game's own: a player's own names come from the local players.json) ----
            Top("Zayn", "Mahrous", "EGY", 24, 'R', "powerhouse", 99, Attr(10, 9, 10, 9), new List<RunMod> { Sg("nick", 5), Sg("thunder", 4), Sg("wide", 3) },
                0.2, 0.3, 0.2, 1.0, "The Storm", "The world No. 1. Charges the T, takes everything early and hits straight through you - then drops it dead from the back.");
            Top("Tomas", "Arriaga", "NZL", 31, 'R', "retriever", 97, Attr(10, 10, 7, 8), new List<RunMod> { Sg("hlob", 4), Sg("vdrop", 2) },
                -0.1, 0, 0.05, 1.0, "The Flyer", "The fittest man on tour. Dives for everything and makes you hit one more - then one more.");
            Top("Oskar", "Lindqvist", "PER", 28, 'R', "volleyer", 96, Attr(9, 9, 8, 10), new List<RunMod> { Sg("vdrop", 4), Sg("sniper", 3), Sg("rail", 2) },
                0, 0.2, 0.15, 1.0, "The Smooth", "Owns the T with precise volleys and the softest drop on tour. Makes it look easy.");
            Top("Hazem", "Rafik", "EGY", 22, 'R', "shotmaker", 95, Attr(9, 8, 8, 9), new List<RunMod> { Sg("trickle", 3), Sg("nick", 3) },
                0.1, 0.1, 0.15, 0.95, "The Angle", "Three-time junior world champion. Awkward angles and low, hard kills from the front of the court.");
            Top("Callum", "Brereton", "EGY", 34, 'R', "magician", 94, Attr(8, 8, 8, 10), new List<RunMod> { Sg("trickle", 4), Sg("vdrop", 3), Sg("knuckle", 1) },
                0.05, 0.3, 0.3, 0.9, "The Artist", "A former world champion who never looks hurried. Straight drops from any corner of the court.");
            Top("Kai", "Teoh", "FRA", 27, 'R', "volleyer", 93, Attr(9, 9, 8, 9), new List<RunMod> { Sg("knuckle", 2), Sg("rail", 3) },
                0, 0.1, 0.4, 1.0, "The Marshal", "Calm and fluid. His signature: shows you the straight drive, then plays it across.");
            Top("Rafael", "Montoya", "WAL", 31, 'R', "grinder", 92, Attr(9, 10, 7, 8), new List<RunMod> { Sg("rail", 5), Sg("hlob", 2) },
                -0.2, -0.1, 0, 1.0, "The Engine", "Length rally after length rally. Very few winners - and nobody is fitter.");
            Top("Omar", "Tantawy", "ENG", 31, 'R', "attacker", 91, Attr(8, 8, 8, 9), new List<RunMod> { Sg("rail", 3), Sg("wide", 2), Sg("vdrop", 2) },
                0, 0.1, 0.1, 1.0, "The Viper", "One of the most tactically astute players on tour. Strong off both wings, always the right shot.");
            Top("Jules", "Marchetti", "EGY", 26, 'L', "attacker", 90, Attr(8, 8, 10, 7), new List<RunMod> { Sg("nick", 4), Sg("thunder", 2) },
                0.25, 0.1, 0.1, 0.85, "The Lefty", "A left-hander who goes for winners from almost anywhere, on instinct.");
            Top("Rhys", "Penhallow", "EGY", 29, 'R', "counter", 90, Attr(10, 8, 7, 8), new List<RunMod> { Sg("vdrop", 4), Sg("hlob", 2) },
                0, 0.1, 0.05, 1.0, "The Hare", "Quick, patient and consistent - waits for the short ball, then the backhand volley drop.");

            // ---- 11..50: a style each, the rating falling, a signature by style ----
            (string f, string l, string n, char h)[] rest =
            {
                ("Yusuf", "Halawa", "EGY", 'R'), ("Bram", "de Wilde", "NED", 'R'), ("Arjun", "Mehra", "IND", 'R'), ("Lucas", "Ferreyra", "ARG", 'L'),
                ("Kenji", "Hamada", "JPN", 'R'), ("Daniel", "Okonkwo", "NGR", 'R'), ("Matteo", "Galli", "ITA", 'R'), ("Sami", "Al-Rashed", "KUW", 'R'),
                ("Finn", "Gallagher", "IRL", 'L'), ("Hamza", "Qureshi", "PAK", 'R'), ("Ethan", "Calloway", "USA", 'R'), ("Mathis", "Leroux", "FRA", 'R'),
                ("Ondrej", "Vavra", "CZE", 'R'), ("Aaron", "Whitlock", "AUS", 'R'), ("Mahmoud", "Fekry", "EGY", 'R'), ("Diego", "Salcedo", "MEX", 'L'),
                ("Tariq", "Benali", "FRA", 'R'), ("Niklas", "Brandt", "GER", 'R'), ("Jamie", "Ferguson", "SCO", 'R'), ("Ricardo", "Amaral", "BRA", 'R'),
                ("Wei Jun", "Tan", "SGP", 'R'), ("Lorenzo", "Vidal", "ESP", 'R'), ("Kofi", "Mensah", "RSA", 'R'), ("Samuel", "Ashworth", "ENG", 'L'),
                ("Adam", "Kowalczyk", "POL", 'R'), ("Mazin", "Al-Hashimi", "QAT", 'R'), ("Pierre", "Lambert", "BEL", 'R'), ("Noah", "Fischer", "SUI", 'R'),
                ("Cameron", "Doyle", "NZL", 'R'), ("Ravi", "Suresh", "IND", 'R'), ("Karim", "Abdelaal", "EGY", 'R'), ("Mika", "Pinheiro", "BRA", 'R'),
                ("Elliot", "Hartley", "ENG", 'R'), ("Kyle", "Brennan", "CAN", 'L'), ("Haruto", "Mori", "JPN", 'R'), ("Ismail", "Darwish", "JOR", 'R'),
                ("Tobias", "Lund", "SWE", 'R'), ("Victor", "Moreau", "FRA", 'R'), ("Sebastian", "Kraus", "AUT", 'R'), ("Ben", "Achterberg", "RSA", 'R'),
            };
            string[] cycle = { "attacker", "retriever", "magician", "grinder", "volleyer", "counter", "shotmaker", "powerhouse" };
            for (int i = 0; i < rest.Length; i++)
            {
                int rank = 11 + i;
                int rating = 89 - (int)Math.Round(i * 11.0 / (rest.Length - 1));          // 89 at No. 11 .. 78 at No. 50
                string style = cycle[i % cycle.Length];
                double b = 5 + (rating - 76) / 5.0;                                         // attributes 5.4 .. 7.6
                double[] lean = StyleLean(style);
                var a = Attr(Math.Round(b + lean[0]), Math.Round(b + lean[1]), Math.Round(b + lean[2]), Math.Round(b + lean[3]));
                int sigLevel = rating >= 86 ? 3 : rating >= 82 ? 2 : 1;
                var r = rest[i];
                Top(r.f, r.l, r.n, 22 + (rank * 7) % 12, r.h, style, rating, a, SignatureFor(style, sigLevel), 0, 0, 0, 0, null, null);
            }
            _byId = new Dictionary<string, Star>();
            foreach (var s in _all) _byId[s.Id] = s;
            return _all;
        }

        /// <summary>How a style leans its attributes (speed, stamina, power, control).</summary>
        static double[] StyleLean(string style)
        {
            switch (style)
            {
                case "attacker": return new[] { 0.0, -1.0, 1.0, 0.0 };
                case "retriever": return new[] { 1.0, 1.0, -1.0, 0.0 };
                case "magician": return new[] { 0.0, -1.0, -1.0, 2.0 };
                case "grinder": return new[] { 0.0, 2.0, 0.0, 0.0 };
                case "volleyer": return new[] { 1.0, 0.0, 0.0, 1.0 };
                case "counter": return new[] { 1.0, 1.0, -1.0, 0.0 };
                case "shotmaker": return new[] { 0.0, 0.0, -1.0, 1.0 };
                default: return new[] { 0.0, 0.0, 2.0, -1.0 };
            }
        }

        public static Star Find(string id)
        {
            All();
            if (id == null) return null;
            if (id.StartsWith("star:")) id = id.Substring(5);
            return _byId.TryGetValue(id, out var s) ? s : null;
        }

        public static Star No1 { get { return All()[0]; } }

        /// <summary>The star's name as the player sees it (their own, or what the name editor gave them).</summary>
        public static (string first, string last) Names(Star s)
        {
            var o = NameOverride != null ? NameOverride(s.Id) : null;
            return o ?? (s.First, s.Last);
        }

        public static string FullName(Star s) { var n = Names(s); return n.first + " " + n.last; }
        public static string LastName(Star s) { return Names(s).last; }

        // ---------------- opponents: stars, club players, the old personalities ----------------

        public static readonly int[] ClubRatings = { 58, 62, 66, 70, 74 };

        public static string ClubId(string style, int rating) { return "club:" + style + ":" + rating; }

        /// <summary>A club player's style and rating from its id (false: not one).</summary>
        public static bool ParseClub(string id, out string style, out int rating)
        {
            style = null; rating = 0;
            if (id == null || !id.StartsWith("club:")) return false;
            var p = id.Split(':');
            if ((p.Length != 3 && p.Length != 4) || !int.TryParse(p[2], out rating)) return false;   // (2026-11-22 evening: a 4th part names one)
            style = p[1];
            return true;
        }

        // ---------------- 2026-11-22 evening (the user: "consider putting more different characters in maybe all world no.1 to no.50
        // ... adding more like club players in would be good ... opponents ideally club players + pro players add up to about 100") ----

        /// <summary>One of Quick Match's named club players: "club:style:rating:NN" - played as the style at the rating.</summary>
        public sealed class ClubPlayer { public int No; public string Name, Style, Class; public int Rating; public string Id; }
        public const int ClubCount = 50;
        static List<ClubPlayer> _clubs;
        static readonly string[] ClubStyles = { "retriever", "attacker", "grinder", "shotmaker", "counter", "volleyer", "powerhouse", "magician" };
        /// <summary>The club players, easiest first (rated 52 to 84).</summary>
        public static List<ClubPlayer> Clubs()
        {
            if (_clubs != null) return _clubs;
            _clubs = new List<ClubPlayer>();
            for (int k = 0; k < ClubCount; k++)
            {
                int rating = 52 + (int)System.Math.Round(32.0 * k / (ClubCount - 1));
                string style = ClubStyles[(k * 3 + k / 8) % ClubStyles.Length];
                var c = new ClubPlayer { No = k + 1, Name = Roster.PersonName(k + 200), Style = style, Rating = rating };
                c.Id = "club:" + style + ":" + rating + ":" + c.No.ToString("00");
                c.Class = ClubClassFor(style, k);
                _clubs.Add(c);
            }
            return _clubs;
        }
        static string ClubClassFor(string style, int k)
        {
            string[] pick;
            switch (style)
            {
                case "attacker": pick = new[] { "power", "bully", "nickartist", "hunter" }; break;
                case "powerhouse": pick = new[] { "slugger", "bomber", "tank", "showman" }; break;
                case "retriever": pick = new[] { "speed", "chaser", "sprinter", "wall" }; break;
                case "counter": pick = new[] { "wall", "longarms", "secondwind", "hustler" }; break;
                case "grinder": pick = new[] { "grafter", "marathon", "railman", "zen" }; break;
                case "shotmaker": pick = new[] { "touch", "dropper", "lobber", "trickster" }; break;
                case "magician": pick = new[] { "matador", "trickster", "curver", "earlybird" }; break;
                case "volleyer": pick = new[] { "volleyshark", "accuracy", "sniper", "closer" }; break;
                default: pick = new[] { "allround", "clockwork", "crosscourt", "lightning" }; break;
            }
            string id = pick[(k / 2) % pick.Length];
            return PlayerClasses.Find(id) != null ? id : PlayerClasses.Default;
        }
        public static ClubPlayer FindClub(string id)
        {
            if (id == null || !id.StartsWith("club:")) return null;
            foreach (var c in Clubs()) if (c.Id == id) return c;
            return null;
        }
        static string[] _allOpponents;
        /// <summary>Quick Match's opponents, easiest first: the 50 club players, then the 50 pros from No. 50 up to the No. 1.</summary>
        public static string[] AllOpponents
        {
            get
            {
                if (_allOpponents != null) return _allOpponents;
                var l = new List<string>();
                foreach (var c in Clubs()) l.Add(c.Id);
                var stars = All();
                for (int i = stars.Count - 1; i >= 0; i--) l.Add("star:" + stars[i].Id);
                _allOpponents = l.ToArray();
                return _allOpponents;
            }
        }

        /// <summary>Quick Match's opponents, easiest first: two club players, then stars up to the world No. 1.</summary>
        public static readonly string[] QuickLadder =
        {
            "club:retriever:62", "club:attacker:70", "star:s50", "star:s40", "star:s30", "star:s20", "star:s10", "star:s05", "star:s04", "star:s03", "star:s02", "star:s01",
        };

        /// <summary>Anyone an opponent id names: their name, style, rating (rating 0: the old personalities / the wall-bot).</summary>
        public static (string name, string style, int rating) Describe(string id)
        {
            var s = Find(id);
            if (s != null) return (FullName(s), s.Style, s.Rating);
            var club = FindClub(id);
            if (club != null) return (club.Name, club.Style, club.Rating);
            if (ParseClub(id, out string style, out int rating)) return ("Club " + Roster.StyleName(style), style, rating);
            return (id == "wallbot" ? "Wall-bot" : Roster.StyleName(id), id, 0);
        }

        /// <summary>The rating a difficulty plays like (the lowest whose difficulty reaches it; 55..99).</summary>
        public static int RatingFor(double difficulty)
        {
            for (int r = 55; r < 99; r++) if (Difficulty(r) >= difficulty - 1e-9) return r;
            return 99;
        }

        /// <summary>The difficulty an opponent id plays at (the old personalities: the given one).</summary>
        public static double DifficultyFor(string id, double fallback)
        {
            var s = Find(id);
            if (s != null) return Difficulty(s.Rating);
            if (ParseClub(id, out _, out int rating)) return Difficulty(rating);
            return fallback;
        }
    }
}
