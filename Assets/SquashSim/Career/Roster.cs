// ===========================================================
// Assets/SquashSim/Career/Roster.cs
// P2-M4: the tour's pros - a world of fictional players (Tour.RosterSize,
// 480 since the PSA rework), generated once per career from its seed.
// No real players' names.
//
//   strength   the AI's difficulty (AiProfile.WithDifficulty), 0..0.62,
//              falling with the place in the world: the top 32 ~0.54+,
//              #100 ~0.41, #250 ~0.18, the bottom hundred under 0.05 -
//              the Challenger 3s are where a new pro can win
//   style      one of the four AI personalities
//   age        drives the season's drift: the young improve, veterans
//              fade; at 36 (or a little earlier) they retire and a
//              newcomer takes their place at the bottom
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Career
{
    [Serializable]
    public sealed class Pro
    {
        public int Id;
        public string First, Last, Nat;
        public string Style;          // retriever / attacker / shotmaker / grinder (P6: and powerhouse / counter / magician / volleyer)
        public double Strength;
        /// <summary>P6: a star's id (Stars) - its name can be edited, its rating and signature come with it; "" for the rest.</summary>
        public string StarId = "";
        public int Age;
        public bool Retired;
        public int RetiredSeason = -1;      // P3-R4: for the news

        public string Name { get { var s = StarOf; return s != null ? Stars.FullName(s) : First + " " + Last; } }
        /// <summary>The score bug's name: "SURNAME".</summary>
        public string Bug { get { var s = StarOf; return (s != null ? Stars.LastName(s) : Last).ToUpperInvariant(); } }
        public Star StarOf { get { return string.IsNullOrEmpty(StarId) ? null : Stars.Find(StarId); } }
        /// <summary>The opponent id for a match against this pro (a star's own; else the style).</summary>
        public string OpponentId { get { return string.IsNullOrEmpty(StarId) ? Style : "star:" + StarId; } }
    }

    public static class Roster
    {
        public static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };
        public static readonly string[] StyleNames = { "Retriever", "Attacker", "Shot-maker", "Grinder" };
        /// <summary>P6: every style (the stars' four more included).</summary>
        public static readonly string[] AllStyles = { "retriever", "attacker", "shotmaker", "grinder", "powerhouse", "counter", "magician", "volleyer" };
        public static readonly string[] AllStyleNames = { "Retriever", "Attacker", "Shot-maker", "Grinder", "Powerhouse", "Counter-puncher", "Magician", "Volleyer" };

        /// <summary>The scouting card's lines for a style.</summary>
        public static string[] StyleLines(string style)
        {
            switch (style)
            {
                case "attacker": return new[] { "Hits low and hard - expect the kill.", "Takes risks to end rallies: keep it tight and make them stretch." };
                case "shotmaker": return new[] { "Loves the drop and the boast from the front.", "Disguises a lot - stay on the T and don't commit early." };
                case "grinder": return new[] { "Goes for length all day - be ready deep.", "Gives nothing away: you'll have to work for every point." };
                case "powerhouse": return new[] { "Pace off both sides, taken early.", "Make them bend low: lobs and short balls take the power away." };
                case "counter": return new[] { "Soaks up your pressure, then counters your first short ball.", "Be patient: only go short when they're stretched." };
                case "magician": return new[] { "Holds the ball and disguises everything.", "Watch the racket, not the body - and stay on the T." };
                case "volleyer": return new[] { "Owns the T and volleys everything.", "Lob over them and get them moving to the back." };
                default: return new[] { "Gets everything back - be patient.", "Safe height, deep length: wait for the short ball." };
            }
        }

        public static string StyleName(string style) { int i = Array.IndexOf(AllStyles, style); return i >= 0 ? AllStyleNames[i] : style; }

        public static readonly string[] Nations =
        {
            "ENG", "SCO", "WAL", "IRL", "FRA", "ESP", "GER", "NED", "BEL", "SUI", "AUT", "ITA", "CZE", "POL", "SWE", "FIN",
            "EGY", "RSA", "KEN", "NGR", "USA", "CAN", "MEX", "COL", "PER", "ARG", "BRA", "AUS", "NZL", "IND", "PAK", "MAS",
            "SGP", "HKG", "JPN", "KOR", "KUW", "QAT", "JOR", "IRN"
        };

        static readonly string[] Firsts =
        {
            "Aled", "Amara", "Anders", "Arjun", "Bastien", "Bram", "Callum", "Carys", "Dario", "Dmitri", "Eamon", "Elif",
            "Emeka", "Esteban", "Farid", "Femi", "Gael", "Hamza", "Haruto", "Ines", "Iker", "Jarrah", "Jonas", "Kai",
            "Kenji", "Kofi", "Laila", "Lars", "Leandro", "Lucan", "Mateo", "Mika", "Nadia", "Niall", "Noor", "Oisin",
            "Omar", "Pavel", "Priya", "Quentin", "Rafael", "Rania", "Rohan", "Ruben", "Saanvi", "Selim", "Soren", "Tariq",
            "Teodor", "Thabo", "Tomas", "Uriel", "Valen", "Wren", "Xavi", "Yara", "Yusuf", "Zane", "Zara", "Ilya"
        };

        static readonly string[] Lasts =
        {
            "Ashcombe", "Barrowfield", "Belmonte", "Brennholt", "Calderwood", "Castellan", "Dunmore", "Eskildsen", "Fairbrook", "Falkenrath",
            "Garrowby", "Halvorsen", "Hartigan", "Ironside", "Jessop", "Kallistrat", "Kerrigan", "Lindqvist", "Lowther", "Madigan",
            "Marchetti", "Mostyn", "Nakamura-Reed", "Northcote", "Okonkwo", "Oyelaran", "Pellerin", "Quarrington", "Radcliffe-Moss", "Ravensworth",
            "Rookwood", "Saltmarsh", "Sandoval-Prieto", "Sefton", "Silvermane", "Strandberg", "Tamberlane", "Thistlewood", "Underhill", "Valcourt",
            "Vandermeer", "Wainfleet", "Westerhout", "Whitlock", "Wyndham", "Yardley", "Zielinski-Hart", "Abernathy", "Blackthorne", "Corvino",
            "Delacroix-Lund", "Elsworth", "Fenwick", "Galloway-Ruiz", "Hollingsworth", "Ingleby", "Juniper", "Kestrel", "Larkspur", "Merriweather",
            "Nightingale", "Oakhurst", "Pemberly", "Quillfeather", "Redfern", "Stormont", "Tregarthen", "Umberto", "Verlaine", "Winterbourne",
            "Ashgrove", "Brightmore", "Coldridge", "Dewhurst", "Emberly", "Foxworth", "Greywell", "Hawksley", "Ivory", "Jarrold",
            "Kingsmill", "Longacre", "Moorcroft", "Netherby", "Ormsby", "Penhallow", "Quenby", "Rushworth", "Starling", "Tolliver"
        };

        // ~700 more surnames, made of English place-name parts (Ashby, Coldwell, Thornfield...)
        static readonly string[] Heads =
        {
            "Ash", "Barrow", "Bel", "Black", "Bright", "Cal", "Cold", "Dun", "East", "Fair", "Fen", "Gar", "Grey", "Hart", "Haw",
            "Iron", "Kings", "Lang", "Lind", "Long", "Marl", "Moor", "North", "Oak", "Pen", "Red", "Rook", "Salt", "Sef", "Silver",
            "Stan", "Storm", "Thorn", "Under", "Wain", "West", "Whit", "Wind", "Wynd", "Yard"
        };
        static readonly string[] Tails = { "by", "combe", "dale", "field", "ford", "holt", "hurst", "ley", "more", "ton", "well", "wood", "worth", "bridge", "croft", "lake", "stead", "wick" };

        static string[] _pool;
        /// <summary>Every surname a pro may have: the list above, then the made ones.</summary>
        static string[] Pool
        {
            get
            {
                if (_pool != null) return _pool;
                var all = new List<string>(Lasts);
                var seen = new HashSet<string>(Lasts);
                foreach (var h in Heads) foreach (var t in Tails) { string n = h + t; if (seen.Add(n)) all.Add(n); }
                return _pool = all.ToArray();
            }
        }

        public const double MaxStrength = 0.62;
        /// <summary>How fast strength falls down the list (top unchanged). 1.6 made the lower tours a wall: a median player
        /// never got past Challenger 6 in a season (2026-10-25 playtest); 2.2 softens the bottom half.</summary>
        public const double RosterCurve = 2.2;

        /// <summary>The roster, best first (index 0 = strongest). P6: the top 50 are the stars (Stars); below them a rating
        /// from 77 down to 55 on the same curve, so a rating means the same everywhere.</summary>
        public static List<Pro> Generate(uint seed)
        {
            var rng = new CareerRng(seed, 1);
            var list = new List<Pro>();
            var usedLast = new HashSet<string>();
            var stars = Stars.All();
            foreach (var st in stars) usedLast.Add(st.Last);
            int n = Tour.RosterSize;
            for (int i = 0; i < n; i++)
            {
                if (i < stars.Count)
                {
                    var st = stars[i];
                    list.Add(new Pro { Id = i + 1, First = st.First, Last = st.Last, Nat = st.Nat, Style = st.Style, StarId = st.Id, Age = st.Age,
                                       Strength = Math.Round(Stars.Difficulty(st.Rating), 4) });
                    continue;
                }
                double u = (double)(n - 1 - i) / (n - 1 - stars.Count);                     // 1 at No. 51 .. 0 at the last
                double rating = 55 + (Stars.All()[stars.Count - 1].Rating - 1 - 55) * Math.Pow(u, RosterCurve);
                var p = NewPro(rng, usedLast, i + 1);
                p.Strength = Clamp(Stars.Difficulty((int)Math.Round(rating)) + rng.Between(-0.003, 0.003));
                p.Age = 19 + rng.Range(17);
                list.Add(p);
            }
            return list;
        }

        /// <summary>A pro's rating (55..99) for the cards: a star's own, else from its strength.</summary>
        public static int RatingOf(Pro p)
        {
            var s = p.StarOf;
            return s != null ? s.Rating : Stars.RatingFor(p.Strength);
        }

        static Pro NewPro(CareerRng rng, HashSet<string> usedLast, int id)
        {
            string last;
            int guard = 0;
            var pool = Pool;
            do { last = pool[rng.Range(pool.Length)]; } while (usedLast.Contains(last) && guard++ < 400);
            usedLast.Add(last);
            return new Pro
            {
                Id = id,
                First = Firsts[rng.Range(Firsts.Length)],
                Last = last,
                Nat = Nations[rng.Range(Nations.Length)],
                Style = Styles[rng.Range(Styles.Length)],
            };
        }

        /// <summary>Clamped, and kept to 4 decimals so a saved career (JSON) loads back to the very same numbers.</summary>
        static double Clamp(double s) { return System.Math.Round(s < 0 ? 0 : (s > MaxStrength ? MaxStrength : s), 4); }

        /// <summary>A season passes: everyone ages and drifts; the old retire and newcomers join. Returns the newcomers.</summary>
        public static List<Pro> NewSeason(List<Pro> pros, uint seed, int season, ref int nextId)
        {
            var rng = new CareerRng(seed, 2, season);
            var usedLast = new HashSet<string>();
            foreach (var p in pros) if (!p.Retired) usedLast.Add(p.Last);
            var joined = new List<Pro>();
            for (int i = 0; i < pros.Count; i++)
            {
                var p = pros[i];
                if (p.Retired) continue;
                p.Age++;
                if (!string.IsNullOrEmpty(p.StarId))
                {
                    // a star keeps their level; they retire late
                    if (p.Age >= 37 || (p.Age >= 35 && rng.Next() < 0.2)) { p.Retired = true; p.RetiredSeason = season; var r = NewPro(rng, usedLast, nextId++); r.Age = 18 + rng.Range(3); r.Strength = Clamp(rng.Between(0.0, 0.05)); joined.Add(r); }
                    continue;
                }
                double drift = p.Age < 24 ? rng.Between(0.0, 0.04)
                             : p.Age < 30 ? rng.Between(-0.015, 0.025)
                             : p.Age < 33 ? rng.Between(-0.03, 0.01)
                             : rng.Between(-0.045, 0.0);
                p.Strength = Clamp(p.Strength + drift);
                if (p.Age >= 36 || (p.Age >= 33 && rng.Next() < 0.25))
                {
                    p.Retired = true;
                    p.RetiredSeason = season;
                    var q = NewPro(rng, usedLast, nextId++);
                    q.Age = 18 + rng.Range(3);
                    q.Strength = Clamp(rng.Between(0.0, 0.07));
                    joined.Add(q);
                }
            }
            pros.AddRange(joined);
            return joined;
        }
    }
}
