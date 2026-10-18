// ===========================================================
// Assets/SquashSim/Career/Roster.cs
// P2-M4: the tour's pros - 80 fictional players, generated once per
// career from its seed. No real players' names.
//
//   strength   the AI's difficulty (AiProfile.WithDifficulty), 0..0.62;
//              made so the ranking bands get the tier strengths of the
//              plan: Club ~0-0.04, Regional ~0.04-0.13, Challenger
//              ~0.13-0.26, Tour 0.26-0.60
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
        public string Style;          // retriever / attacker / shotmaker / grinder
        public double Strength;
        public int Age;
        public bool Retired;

        public string Name { get { return First + " " + Last; } }
        /// <summary>The score bug's name: "SURNAME".</summary>
        public string Bug { get { return Last.ToUpperInvariant(); } }
    }

    public static class Roster
    {
        public static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };
        public static readonly string[] StyleNames = { "Retriever", "Attacker", "Shot-maker", "Grinder" };

        /// <summary>The scouting card's lines for a style.</summary>
        public static string[] StyleLines(string style)
        {
            switch (style)
            {
                case "attacker": return new[] { "Hits low and hard - expect the kill.", "Takes risks to end rallies: keep it tight and make them stretch." };
                case "shotmaker": return new[] { "Loves the drop and the boast from the front.", "Disguises a lot - stay on the T and don't commit early." };
                case "grinder": return new[] { "Goes for length all day - be ready deep.", "Gives nothing away: you'll have to work for every point." };
                default: return new[] { "Gets everything back - be patient.", "Safe height, deep length: wait for the short ball." };
            }
        }

        public static string StyleName(string style) { int i = Array.IndexOf(Styles, style); return i >= 0 ? StyleNames[i] : style; }

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

        public const double MaxStrength = 0.62;

        /// <summary>The roster, best first (index 0 = strongest).</summary>
        public static List<Pro> Generate(uint seed)
        {
            var rng = new CareerRng(seed, 1);
            var list = new List<Pro>();
            var usedLast = new HashSet<string>();
            int n = Tour.RosterSize;
            for (int i = 0; i < n; i++)
            {
                double u = 1.0 - (double)i / (n - 1);
                double s = 0.60 * Math.Pow(u, 1.6) + rng.Between(-0.015, 0.015);
                var p = NewPro(rng, usedLast, i + 1);
                p.Strength = Clamp(s);
                p.Age = 19 + rng.Range(17);
                list.Add(p);
            }
            return list;
        }

        static Pro NewPro(CareerRng rng, HashSet<string> usedLast, int id)
        {
            string last;
            int guard = 0;
            do { last = Lasts[rng.Range(Lasts.Length)]; } while (usedLast.Contains(last) && guard++ < 200);
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
                double drift = p.Age < 24 ? rng.Between(0.0, 0.04)
                             : p.Age < 30 ? rng.Between(-0.015, 0.025)
                             : p.Age < 33 ? rng.Between(-0.03, 0.01)
                             : rng.Between(-0.045, 0.0);
                p.Strength = Clamp(p.Strength + drift);
                if (p.Age >= 36 || (p.Age >= 33 && rng.Next() < 0.25))
                {
                    p.Retired = true;
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
