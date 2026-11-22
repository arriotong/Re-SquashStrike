// ===========================================================
// Assets/SquashSim/Player/PlayerClasses.cs
// 2026-11-22 the player classes (the user: "a class separation of characters
// ... power shots, accuracy"; plan: SQUASH FRONTEND MODIFIED/
// RE_SQUASHSTRIKE_PROS_CLASSES_PLAN.md). Five, picked before a match:
//
//   power      drives and kills +12%; aim -16%      perk: Heavy Ball (their reach cut on your drives)
//   accuracy   aim +30%; pace -4%                    perk: the nick (low kills and drops find it more)
//   speed      move +6%, reach +3%; power -8%        perk: Ghost (one out-of-reach ball saved a rally)
//   touch      finer control; drives -8%            perk: Knuckleball (their timing window cut)
//   allround   a little of everything, no weakness  perk: stamina +10%
//
// A class is attribute deltas (AthleteAttributes: 4% a point for speed and
// power, 8% a point for control's aim, 8% for stamina's drain) and a few of
// the run cards (RunMods) at low level - so a match's setup (attributes and
// mods) carries it into replays and online with nothing else to send.
// Deterministic, UnityEngine-free.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public static class PlayerClasses
    {
        public sealed class Def
        {
            public string Id, Name, Short, Perk, PerkText, Weakness;
            /// <summary>2026-11-22 evening: which of the five it belongs with (its colour, the pros' reading).</summary>
            public string Family;
            public string[] Strengths;
            public double Speed, Stamina, Power, Control;
            public RunMod[] Cards;
        }

        public const string Default = "allround";

        public static readonly Def[] All =
        {
            new Def
            {
                Id = "power", Family = "power", Name = "Power", Short = "Hits the hardest", Strengths = new[] { "Drives and kills 12% harder", "Fully charged shots crush" },
                Weakness = "Aim 16% looser", Perk = "Heavy Ball", PerkText = "Your drives and kills cut their reach",
                Power = 3, Control = -2, Cards = new[] { new RunMod { Id = "heavyball", Level = 1 } },
            },
            new Def
            {
                Id = "accuracy", Family = "accuracy", Name = "Accuracy", Short = "Puts it on a coin", Strengths = new[] { "Aim 30% tighter", "Lines hug the wall" },
                Weakness = "Pace 4% lower", Perk = "Nick Hunter", PerkText = "Low kills and drops find the nick more",
                Control = 3.75, Power = -1, Cards = new[] { new RunMod { Id = "nick", Level = 2 }, new RunMod { Id = "rail", Level = 1 } },
            },
            new Def
            {
                Id = "speed", Family = "speed", Name = "Speed", Short = "Gets everything back", Strengths = new[] { "Moves 6% faster", "Reaches further" },
                Weakness = "Power 8% lower", Perk = "Ghost", PerkText = "One out-of-reach ball saved every rally",
                Speed = 1.5, Power = -2, Cards = new[] { new RunMod { Id = "ghost", Level = 1 } },
            },
            new Def
            {
                Id = "touch", Family = "touch", Name = "Touch", Short = "Soft hands, sharp mind", Strengths = new[] { "Tighter drops and boasts", "Hard to read" },
                Weakness = "Drives 8% softer", Perk = "Knuckleball", PerkText = "Hard to read: their timing window shrinks",
                Power = -2, Control = 1.5, Cards = new[] { new RunMod { Id = "knuckle", Level = 1 } },
            },
            new Def
            {
                Id = "allround", Family = "allround", Name = "All-rounder", Short = "No weak spot", Strengths = new[] { "A little better at everything", "Stamina 10% longer" },
                Weakness = "None", Perk = "Engine", PerkText = "Tires 10% slower",
                Speed = 0.5, Power = 0.5, Control = 0.5, Stamina = 1.25, Cards = new RunMod[0],
            },
            // ---- 2026-11-22 evening (the user: "add more different player classes just play around with the stats and give them a new
            // name i want maybe like 30 - 40"): 30 more - each a mix of the four stats and a card or two from the Gauntlet's fair ones
            // (no extra points, no ultimates), balanced with MatchBench --classes ----
            C("slugger", "Powerhouse", "power", "Pace off both sides", "Drives and kills 16% harder", "Heavy shots cut their reach", "Slower and looser", "Heavy Hitter", "Harder drives and a heavy ball", -1.5, 0, 4, -1, new RunMod { Id = "heavy", Level = 1 }, new RunMod { Id = "heavyball", Level = 1 }),
            C("bomber", "Bomber", "power", "Serves like a cannon", "Hard serves that fly", "Harder drives", "Tires sooner", "Thunder Serve", "Your hard serve flies", 0, -1.5, 3, 0, new RunMod { Id = "thunder", Level = 2 }, new RunMod { Id = "heavy", Level = 1 }),
            C("bully", "Bully", "power", "Makes you run", "Harder drives", "Your opponent tires faster", "Looser aim", "Pressure", "They tire faster", -0.5, 0, 2.5, -0.5, new RunMod { Id = "pressure", Level = 1 }),
            C("curver", "Curveball", "power", "Bends it round you", "Drives bend after the wall", "A little harder", "A little looser", "Curveball", "Your drives curve", 0, 0, 1, -1, new RunMod { Id = "curve", Level = 1 }),
            C("tank", "Tank", "power", "Heavy and tireless", "Runs all day", "Heavy drives", "Slow round the court", "Heavy Ball", "Hard to dig out", -1.5, 2, 1.5, 0, new RunMod { Id = "heavyball", Level = 1 }),
            C("showman", "Showman", "power", "Plays for the crowd", "Hard serves", "Wide cross-courts", "A little looser", "Showtime", "Serves and crosses that pop", 0, 0, 1.5, -0.5, new RunMod { Id = "thunder", Level = 1 }, new RunMod { Id = "wide", Level = 1 }),
            C("sniper", "Sniper", "accuracy", "Charged shots go exactly there", "Aim far tighter", "Charged shots on the spot", "A step slower", "Sniper", "Charged shots go where you aim", -1, 0, 0, 3, new RunMod { Id = "sniper", Level = 1 }),
            C("railman", "Wall Hugger", "accuracy", "Straight and tight", "Rail drives cling to the wall", "Tighter aim", "A little softer", "Rail Drive", "Straight drives hug the wall", 0, 0, -1, 1.5, new RunMod { Id = "rail", Level = 1 }),
            C("crosscourt", "Cross-Court King", "accuracy", "Splits the court in two", "Cross-courts beat the volley", "Harder and tighter", "A step slower", "Wide Cross", "Cross-courts that get past them", -1, 0, 1, 1, new RunMod { Id = "wide", Level = 2 }),
            C("nickartist", "Nick Artist", "accuracy", "Lives in the nick", "Kills find the nick", "Tighter aim", "Tires sooner", "Nick Hunter", "Low kills hunt the nick", 0, -1.5, 0.5, 1.5, new RunMod { Id = "nick", Level = 3 }),
            C("surgeon", "Surgeon", "accuracy", "Cuts you apart", "Aim the tightest of all", "Steady at a stretch", "Slower, softer", "Steady Hands", "Tight even when stretched", -0.5, 0, -1, 3.75, new RunMod { Id = "steady", Level = 2 }),
            C("clockwork", "Clockwork", "accuracy", "Perfect length, every time", "Straight drives never mishit", "Tighter aim", "A step slower", "Clockwork", "The perfect length", -1, 0, 0, 2, new RunMod { Id = "clock", Level = 1 }),
            C("zen", "Zen", "accuracy", "Calm under fire", "Steady at a stretch", "Tires slower", "A little slower and softer", "Steady Hands", "Tight under pressure", -0.5, 1, -0.5, 1, new RunMod { Id = "steady", Level = 1 }),
            C("chaser", "Retriever", "speed", "Gets every ball back", "Faster and fitter", "Quick feet", "Soft hitting", "Quick Feet", "Faster to every corner", 1.5, 1.5, -2.5, 0, new RunMod { Id = "quick", Level = 1 }),
            C("marathon", "Marathon", "speed", "Never gets tired", "Stamina far longer", "Breath back on the T", "Soft hitting", "Engine", "Run all day", 0, 3, -1.5, 0, new RunMod { Id = "engine", Level = 2 }),
            C("sprinter", "Sprinter", "speed", "First to the ball", "Fastest round the court", "Quick feet", "Tires much sooner", "Quick Feet", "Faster to every corner", 2, -2.5, 0, 0, new RunMod { Id = "quick", Level = 1 }),
            C("wall", "The Wall", "speed", "Nothing gets past", "Faster", "Reaches further", "Softer hitting", "Long Arms", "In reach from further away", 1, 0, -2, 1, new RunMod { Id = "reach", Level = 1 }),
            C("longarms", "Long Arms", "speed", "A huge reach", "Reaches much further", "A little harder", "A step slower", "Long Arms", "In reach from further away", -0.5, 0, 0.5, 0, new RunMod { Id = "reach", Level = 2 }),
            C("hunter", "Hunter", "speed", "Fast and hard", "Faster", "Harder", "Tires sooner", "Quick Feet", "Faster to every corner", 1, -1.5, 1, 0, new RunMod { Id = "quick", Level = 1 }),
            C("hustler", "Hustler", "speed", "Every shot gets you going", "Each shot speeds you up", "A little faster", "A little softer", "Haste", "Your shots speed you up", 0, 0, -1, 0, new RunMod { Id = "haste", Level = 1 }),
            C("volleyshark", "Volley Shark", "touch", "Takes it early", "Volley drops die", "Swing late and still connect", "Tires sooner", "Volley Drop", "Drops before the bounce die", 1, -1, 0, 1, new RunMod { Id = "vdrop", Level = 1 }, new RunMod { Id = "grace", Level = 1 }),
            C("lobber", "Lob Artist", "touch", "Puts it over your head", "Lobs hang and die deep", "Finer touch", "Softer drives", "Hanging Lob", "Lobs that die in the back", 0, 0, -1.5, 1.5, new RunMod { Id = "hlob", Level = 2 }),
            C("trickster", "Trickster", "touch", "Never shows you the shot", "Hard to time", "Boasts that stay short", "Softer drives", "Knuckleball", "Their timing window shrinks", 0, 0, -1.5, 1, new RunMod { Id = "knuckle", Level = 1 }, new RunMod { Id = "trickle", Level = 1 }),
            C("matador", "Matador", "touch", "Sends you the wrong way", "Deception that freezes them", "Finer touch", "Soft drives", "Matador", "They're sent the wrong way", 0, 0, -2, 1, new RunMod { Id = "matador", Level = 1 }),
            C("dropper", "Drop Shot", "touch", "Loves the front of the court", "Volley drops die", "Boasts that stay short", "Soft drives", "Volley Drop", "Short balls that die", 0, 0, -2, 2, new RunMod { Id = "vdrop", Level = 2 }, new RunMod { Id = "trickle", Level = 1 }),
            C("earlybird", "Early Bird", "touch", "Always on time", "Swing early and still connect", "Finer touch", "Tires a little sooner", "Early Bird", "A wider timing window", 0, -0.5, 0, 1, new RunMod { Id = "grace", Level = 2 }),
            C("grafter", "Grinder", "allround", "Wins the long rallies", "Tires much slower", "They tire faster", "Soft hitting", "Pressure", "They tire faster", 0, 1.5, -3, 1, new RunMod { Id = "pressure", Level = 1 }),
            C("closer", "Closer", "allround", "Strings points together", "On fire after three in a row", "Tighter aim", "A step slower", "Momentum", "Three in a row and you're on fire", -0.5, 0, 0, 1, new RunMod { Id = "momentum", Level = 1 }),
            C("secondwind", "Second Wind", "allround", "Winners give you breath", "Breath back on every winner", "Tires slower", "A little softer", "Second Wind", "Every winner gives breath back", 0, 1, -0.5, 0, new RunMod { Id = "vampire", Level = 1 }),
            C("lightning", "Lightning", "allround", "Fast, hard, wild", "Faster and harder", "No card, pure speed", "Wild aim, tires sooner", "None", "All athlete", 1.5, -1, 1, -2.5),
        };

        static Def C(string id, string name, string family, string shortText, string s1, string s2, string weak, string perk, string perkText,
                     double speed, double stamina, double power, double control, params RunMod[] cards)
        {
            return new Def { Id = id, Name = name, Family = family, Short = shortText, Strengths = new[] { s1, s2 }, Weakness = weak, Perk = perk, PerkText = perkText,
                             Speed = speed, Stamina = stamina, Power = power, Control = control, Cards = cards };
        }

        /// <summary>The family a class belongs with (the five are their own).</summary>
        public static string FamilyOf(string id) { var d = Find(id); return d == null ? Default : d.Family ?? d.Id; }

        /// <summary>The class nearest an athlete's shape (their edges over the middle), among all of them - the pros' and club
        /// players' cards. Ties and flat athletes: the All-rounder.</summary>
        public static string Nearest(AthleteAttributes a, uint salt = 0)
        {
            if (a == null) return Default;
            // the shape, not the level: each stat against the athlete's own average (a great pro is good at everything)
            double mean = (a.Speed + a.Stamina + a.Power + a.Control) / 4;
            double sp = a.Speed - mean, st = a.Stamina - mean, pw = a.Power - mean, ct = a.Control - mean;
            double norm = System.Math.Sqrt(sp * sp + st * st + pw * pw + ct * ct);
            if (norm < 0.4) return Default;
            string best = Default; double bestScore = -2;
            for (int k = 0; k < All.Length; k++)
            {
                var d = All[k];
                double dm = (d.Speed + d.Stamina + d.Power + d.Control) / 4;
                double ds = d.Speed - dm, dt = d.Stamina - dm, dp = d.Power - dm, dc = d.Control - dm;
                double dn = System.Math.Sqrt(ds * ds + dt * dt + dp * dp + dc * dc);
                if (dn < 1e-9) continue;
                double cos = (sp * ds + st * dt + pw * dp + ct * dc) / (norm * dn);
                cos += 0.02 * (((k * 2654435761u) ^ salt) % 7) / 7.0;              // (a little variety between near-equals)
                if (cos > bestScore) { bestScore = cos; best = d.Id; }
            }
            return best;
        }

        public static Def Find(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }

        /// <summary>The class's attributes on top of these (clamped 0..10); a new set.</summary>
        public static AthleteAttributes Attributes(AthleteAttributes baseAttr, string id)
        {
            var a = (baseAttr ?? new AthleteAttributes()).Clone();
            var d = Find(id);
            if (d == null) return a;
            a.Speed = Clamp(a.Speed + d.Speed); a.Stamina = Clamp(a.Stamina + d.Stamina);
            a.Power = Clamp(a.Power + d.Power); a.Control = Clamp(a.Control + d.Control);
            return a;
        }

        /// <summary>The class's cards merged into these (a card already held: the higher level); a new list, or null when both are empty.</summary>
        public static List<RunMod> Mods(List<RunMod> baseMods, string id)
        {
            var d = Find(id);
            if (d == null || d.Cards.Length == 0) return baseMods != null ? new List<RunMod>(baseMods) : null;
            var l = new List<RunMod>();
            if (baseMods != null) foreach (var m in baseMods) l.Add(new RunMod { Id = m.Id, Level = m.Level });
            foreach (var c in d.Cards)
            {
                var have = l.Find(m => m.Id == c.Id);
                if (have == null) l.Add(new RunMod { Id = c.Id, Level = c.Level });
                else if (have.Level < c.Level) have.Level = c.Level;
            }
            return l;
        }

        /// <summary>An online match (both players people, neutral athletes, no cards): each slot's class from the setup's ids, on
        /// the server and both clients alike (TickSim). An unknown or empty id: the plain athlete.</summary>
        public static void ApplyOnline(SquashSim.Replays.ReplaySetup s)
        {
            if (Find(s.Class0) == null) s.Class0 = "";
            if (Find(s.Class1) == null) s.Class1 = "";
            if (s.Class0 == "" && s.Class1 == "") return;
            s.Attr0 = Attributes(new AthleteAttributes(), s.Class0); s.Mods0 = Mods(null, s.Class0);
            s.Attr1 = Attributes(new AthleteAttributes(), s.Class1); s.Mods1 = Mods(null, s.Class1);
        }

        static double Clamp(double v) { return v < 0 ? 0 : v > AthleteAttributes.Max ? AthleteAttributes.Max : v; }

        /// <summary>The class an athlete's attributes read as (the pros' cards): their biggest edge over the middle.</summary>
        public static string ReadAs(AthleteAttributes a, List<RunMod> signature = null)
        {
            if (a == null) return Default;
            double sp = a.Speed - 5, pw = a.Power - 5, ct = a.Control - 5;
            bool touchy = false;
            if (signature != null) foreach (var m in signature) if (m.Id == "deaddrop" || m.Id == "trickle" || m.Id == "vdrop" || m.Id == "knuckle") touchy = true;
            double max = System.Math.Max(sp, System.Math.Max(pw, ct));
            if (max < 0.75) return Default;
            if (touchy && ct >= max - 0.5) return "touch";
            if (pw == max) return "power";
            if (sp == max) return "speed";
            return "accuracy";
        }
    }
}
