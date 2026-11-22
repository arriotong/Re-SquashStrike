// ===========================================================
// Assets/SquashSim/Match/ProPersonas.cs
// 2026-11-22 the top 10 play like themselves (the user: "adopt real pro
// players' playstyle to them so it actually feels like you are playing
// with them"; plan: SQUASH FRONTEND MODIFIED/RE_SQUASHSTRIKE_PROS_CLASSES_
// PLAN.md). Drawn from public match reports, PSA features and coaching
// write-ups - no footage. Keyed by star id only (the names a player sees
// are the game's own, or their own in the local players.json).
//
// A persona leans the AI's shot choice (OpponentBrain.ChooseArcadeShot):
//   Bias        on each shot: straight, cross, lob, drop, kill, boast
//   DeepAttack  0..1: how much of the "not from the back" caution on drops
//               and kills it ignores - winners from anywhere
//   Volley      its book's lean on taking the ball early (BookVolley x)
//   (a pro's disguise is their Knuckleball card: Career.Stars' signature)
//   LengthRun   after this many lengths in a row, an attack is due (0: no)
//   DropAfterDrive  after its own drive, with the opponent back: the drop
//   Charge      how long it loads a drive or kill (pace) - power players
// ===========================================================

namespace SquashSim
{
    public sealed class ProPersona
    {
        public string Id, Summary;
        /// <summary>The class shown on their card (PlayerClasses id).</summary>
        public string Class;
        /// <summary>straight, cross, lob, drop, kill, boast.</summary>
        public double[] Bias = new double[6];
        public double DeepAttack, Volley = 1, Charge = 1, DropAfterDrive;
        public int LengthRun;
    }

    public static class ProPersonas
    {
        const int S = 0, C = 1, L = 2, D = 3, K = 4, B = 5;

        static ProPersona P(string id, string summary, double s, double c, double l, double d, double k, double b)
        {
            return new ProPersona { Id = id, Summary = summary, Bias = new[] { s, c, l, d, k, b }, Class = ClassOf(id) };
        }

        static string ClassOf(string id)
        {
            switch (id)
            {
                case "s01": case "s09": return "power";
                case "s02": case "s10": return "speed";
                case "s03": case "s06": case "s08": return "accuracy";
                case "s04": case "s05": return "touch";
                default: return "allround";
            }
        }

        static readonly ProPersona[] All =
        {
            // s01: relentless power from the T - takes it early, crushes the drive, then kills or drops from anywhere
            Mod(P("s01", "Takes it early from the T and hits through you - then drops it dead from anywhere.", 0.2, 0.1, -0.4, 0.4, 0.6, 0.0),
                deep: 0.6, volley: 1.6, charge: 1.5, dropAfterDrive: 0.6),
            // s02: the fittest man on tour - length, length, length; retrieves everything; counters short when you're back
            Mod(P("s02", "Gets everything back and makes you play one more shot - length, then the counter-drop.", 0.6, 0.4, 0.5, 0.1, -0.4, -0.2),
                deep: 0.0, volley: 1.0, lengthRun: 4, dropAfterDrive: 0.3),
            // s03: smooth, owns the T with precise volleys, finishes with the soft drop
            Mod(P("s03", "Owns the T with precise volleys, then finishes with a soft drop.", 0.3, 0.2, 0.0, 0.7, 0.0, -0.1),
                deep: 0.2, volley: 1.8, dropAfterDrive: 0.7),
            // s04: high squash IQ - awkward angles and low, hard kills in the front three-quarters
            Mod(P("s04", "Awkward angles and low, hard kills in the front of the court.", 0.0, 0.2, -0.2, 0.3, 0.6, 0.7),
                deep: 0.2, volley: 1.2, charge: 1.2),
            // s05: the elegant shot-maker - straight drops from any corner, never hurried, holds and flicks
            Mod(P("s05", "Feathers straight drops in from any corner - never looks hurried.", 0.2, 0.0, 0.0, 1.0, 0.1, 0.3),
                deep: 0.8, volley: 1.1),
            // s06: fluid and calm - the signature fake straight (shows the line, plays the cross)
            Mod(P("s06", "Calm and fluid - shows you the straight, plays the cross.", 0.3, 0.5, 0.1, 0.3, 0.1, 0.0),
                deep: 0.2, volley: 1.2),
            // s07: the grinder - gruelling lengths, very few kills or nicks, pushes you to your limit
            Mod(P("s07", "Gruelling length rally after rally - very few kills, all legs.", 0.9, 0.5, 0.4, -0.2, -0.6, -0.4),
                deep: 0.0, volley: 1.0, lengthRun: 6),
            // s08: tactically astute, strong off both wings - the right shot, rarely the risky one
            Mod(P("s08", "Strong off both wings and always picks the right shot.", 0.5, 0.4, 0.2, 0.3, -0.5, 0.1),
                deep: 0.1, volley: 1.3, lengthRun: 3, dropAfterDrive: 0.3),
            // s09: the left-handed attacker - winners from almost anywhere, high pace, plays on instinct
            Mod(P("s09", "Left-handed and lethal - goes for winners from almost anywhere.", 0.0, 0.2, -0.4, 0.4, 0.9, 0.2),
                deep: 0.9, volley: 1.3, charge: 1.4),
            // s10: speed and agility, a consistent, traditional game - and the backhand volley drop
            Mod(P("s10", "Quick and consistent - waits for the short ball, then the volley drop.", 0.6, 0.3, 0.3, 0.3, -0.2, -0.1),
                deep: 0.0, volley: 1.5, lengthRun: 3, dropAfterDrive: 0.4),
        };

        static ProPersona Mod(ProPersona p, double deep = 0, double volley = 1, double charge = 1, int lengthRun = 0, double dropAfterDrive = 0)
        {
            p.DeepAttack = deep; p.Volley = volley; p.Charge = charge; p.LengthRun = lengthRun; p.DropAfterDrive = dropAfterDrive;
            return p;
        }

        /// <summary>The persona of a star (by id, "s01" or "star:s01"); null for the rest.</summary>
        public static ProPersona For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("star:")) id = id.Substring(5);
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }
    }
}
