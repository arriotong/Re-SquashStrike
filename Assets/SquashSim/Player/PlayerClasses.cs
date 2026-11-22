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
            public string[] Strengths;
            public double Speed, Stamina, Power, Control;
            public RunMod[] Cards;
        }

        public const string Default = "allround";

        public static readonly Def[] All =
        {
            new Def
            {
                Id = "power", Name = "Power", Short = "Hits the hardest", Strengths = new[] { "Drives and kills 12% harder", "Fully charged shots crush" },
                Weakness = "Aim 16% looser", Perk = "Heavy Ball", PerkText = "Your drives and kills cut their reach",
                Power = 3, Control = -2, Cards = new[] { new RunMod { Id = "heavyball", Level = 1 } },
            },
            new Def
            {
                Id = "accuracy", Name = "Accuracy", Short = "Puts it on a coin", Strengths = new[] { "Aim 30% tighter", "Lines hug the wall" },
                Weakness = "Pace 4% lower", Perk = "Nick Hunter", PerkText = "Low kills and drops find the nick more",
                Control = 3.75, Power = -1, Cards = new[] { new RunMod { Id = "nick", Level = 2 }, new RunMod { Id = "rail", Level = 1 } },
            },
            new Def
            {
                Id = "speed", Name = "Speed", Short = "Gets everything back", Strengths = new[] { "Moves 6% faster", "Reaches further" },
                Weakness = "Power 8% lower", Perk = "Ghost", PerkText = "One out-of-reach ball saved every rally",
                Speed = 1.5, Power = -2, Cards = new[] { new RunMod { Id = "ghost", Level = 1 } },
            },
            new Def
            {
                Id = "touch", Name = "Touch", Short = "Soft hands, sharp mind", Strengths = new[] { "Tighter drops and boasts", "Hard to read" },
                Weakness = "Drives 8% softer", Perk = "Knuckleball", PerkText = "Hard to read: their timing window shrinks",
                Power = -2, Control = 1.5, Cards = new[] { new RunMod { Id = "knuckle", Level = 1 } },
            },
            new Def
            {
                Id = "allround", Name = "All-rounder", Short = "No weak spot", Strengths = new[] { "A little better at everything", "Stamina 10% longer" },
                Weakness = "None", Perk = "Engine", PerkText = "Tires 10% slower",
                Speed = 0.5, Power = 0.5, Control = 0.5, Stamina = 1.25, Cards = new RunMod[0],
            },
        };

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
