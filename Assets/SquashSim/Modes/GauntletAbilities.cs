// ===========================================================
// Assets/SquashSim/Modes/GauntletAbilities.cs
// P5 the Gauntlet roguelite: every ability a run can pick - what it is
// called, its group, rarity and levels, the words for each level, and
// the evolutions (two abilities at their top level make a third).
// What the abilities do in a match lives in RunMods (the sim) and the
// run's own rules (Gauntlet.cs): this is the catalog.
//
//   active    ultimates, shots, powers, evolved - up to 6 in a run
//   passive   stats, perks - up to 6
//   instant   taken at once, nothing kept (a life, coins, the golden
//             racket) - always possible, and the pool's filler
//
// The chaos remake (2026-10-28): five build families - TIME, POWER,
// TRICK, JACKPOT, GUARD - each with an ultimate or a payoff, and
// legendary evolutions; the old small-number cards are retired (out of
// the pool; their effects stay in the sim for old runs and replays).
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    public enum AbilityKind { Shot, Power, Evolved, Stat, Perk, Instant, Ultimate }
    /// <summary>The chaos remake's builds (None: a perk or a bonus for any build).</summary>
    public enum Family { None, Time, Power, Trick, Jackpot, Guard }
    public enum Rarity { Common, Rare, Epic, Legendary }

    public sealed class Ability
    {
        public string Id, Name, Blurb;
        public AbilityKind Kind;
        public Rarity Rarity;
        public int MaxLevel = 5;
        /// <summary>What level n gives (1-based).</summary>
        public Func<int, string> Level;
        /// <summary>Evolutions: the two abilities that make it (both at their top level).</summary>
        public string[] Recipe;
        /// <summary>Not in a run's pool until unlocked (a talent, or a boss beaten).</summary>
        public bool Locked;
        /// <summary>Applies in the sim (RunMods) - else the run or the game handles it.</summary>
        public bool InSim = true;
        /// <summary>The build it belongs to (the chaos remake).</summary>
        public Family Family;
        /// <summary>Out of the pool (the chaos remake): kept so an old run still shows it.</summary>
        public bool Retired;

        public bool Active { get { return Kind == AbilityKind.Shot || Kind == AbilityKind.Power || Kind == AbilityKind.Evolved || Kind == AbilityKind.Ultimate; } }
        public bool Passive { get { return Kind == AbilityKind.Stat || Kind == AbilityKind.Perk; } }
        public string KindName { get { return Kind == AbilityKind.Instant ? "Bonus" : Kind.ToString(); } }
        public string FamilyName { get { return Family == Family.None ? "" : Family.ToString().ToUpperInvariant(); } }
    }

    public static class GauntletAbilities
    {
        public const int ActiveSlots = 6, PassiveSlots = 6;
        public static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

        static List<Ability> _all;
        static Dictionary<string, Ability> _byId;

        static string Pct(int l, double per) { return "+" + Math.Round(per * l * 100) + "%"; }

        public static List<Ability> All()
        {
            if (_all != null) return _all;
            _all = new List<Ability>();
            void A(string id, string name, AbilityKind kind, Rarity r, string blurb, Func<int, string> level, int max = 5, bool locked = false, bool inSim = true, string[] recipe = null, Family fam = Family.None, bool retired = false)
            { _all.Add(new Ability { Id = id, Name = name, Kind = kind, Rarity = r, Blurb = blurb, Level = level, MaxLevel = max, Locked = locked, InSim = inSim, Recipe = recipe, Family = fam, Retired = retired }); }
            void R(string id) { var a = _all.Find(x => x.Id == id); a.Retired = true; }
            void F(string id, Family f) { _all.Find(x => x.Id == id).Family = f; }

            // ---- shot techniques ----
            A("nick", "Nick Hunter", AbilityKind.Shot, Rarity.Rare, "Your kills hunt the nick: lower, wider, faster.",
              l => "Kills " + (l * 10) + "% lower over the tin, aimed " + (l * 8) + " cm wider, " + Pct(l, 0.02) + " pace");
            A("vdrop", "Volley Drop", AbilityKind.Shot, Rarity.Rare, "A drop taken before the bounce dies in the front.",
              l => "Volleyed drops " + (l * 10) + "% lower and " + (l * 5) + "% softer");
            A("trickle", "Trickle Boast", AbilityKind.Shot, Rarity.Common, "Soft boasts that stay in the front corner.",
              l => "Boasts " + (l * 7) + "% softer and " + (l * 8) + "% lower");
            A("hlob", "Hanging Lob", AbilityKind.Shot, Rarity.Common, "Lobs that hang high and die in the back.",
              l => "Lobs aimed " + (l * 12) + " cm higher on the front wall");
            A("rail", "Rail Drive", AbilityKind.Shot, Rarity.Common, "Straight drives that cling to the wall.",
              l => "Straight drives " + (l * 12) + "% tighter, " + (l * 4) + " cm closer to the wall");
            A("wide", "Wide Cross", AbilityKind.Shot, Rarity.Common, "Cross-courts that beat the volley.",
              l => "Cross drives " + (l * 7) + " cm wider, " + Pct(l, 0.02) + " pace");
            A("thunder", "Thunder Serve", AbilityKind.Shot, Rarity.Rare, "Hard serves that fly.",
              l => "Hard serves gain " + Pct(l, 0.3) + " of their charge pace", locked: true);
            A("sniper", "Sniper", AbilityKind.Shot, Rarity.Rare, "Charged shots go exactly where you aim.",
              l => "Charged shots " + (l * 12) + "% tighter", locked: true);

            // ---- arcade powers ----
            A("ace", "Ace Machine", AbilityKind.Power, Rarity.Epic, "An ace is worth more.",
              l => l >= 5 ? "Aces score 3 points" : "Aces score 2 points");
            A("nickbank", "Nick Bank", AbilityKind.Power, Rarity.Epic, "A nick is worth more.",
              l => l >= 5 ? "Nicks score 3 points, and your first nick of the run heals a heart" : "Nicks score 2 points");
            A("pressure", "Pressure", AbilityKind.Power, Rarity.Common, "Make them run: the opponent tires faster.",
              l => "Opponent's stamina drain " + Pct(l, 0.1));
            A("momentum", "Momentum", AbilityKind.Power, Rarity.Rare, "Three rallies in a row and you're on fire.",
              l => "After 3 rallies won in a row: drives and kills " + Pct(l, 0.02) + " pace", locked: true);
            A("vampire", "Second Wind", AbilityKind.Power, Rarity.Rare, "Every winner gives you breath back.",
              l => "A winner restores " + (l * 4) + "% stamina", locked: true);
            A("second", "Second Serve", AbilityKind.Power, Rarity.Rare, "One fault a game is forgiven.",
              l => "Your first fault each game is served again", max: 1, locked: true);
            A("focus", "Focus", AbilityKind.Power, Rarity.Epic, "Slow the world down to reach the impossible.",
              l => l + (l == 1 ? " slow-motion focus" : " slow-motion focuses") + " a game (Focus key)", locked: true, inSim: false);

            // ---- evolutions ----
            A("exec", "Executioner", AbilityKind.Evolved, Rarity.Legendary, "Nick Hunter + Nick Bank: the nick is your weapon.",
              l => "Nicks score one more point again", max: 1, recipe: new[] { "nick", "nickbank" });
            A("ghost", "Ghost", AbilityKind.Evolved, Rarity.Legendary, "Quick Feet + Long Arms: nothing gets past you.",
              l => "Once a rally, reach a ball 60% further away", max: 1, recipe: new[] { "quick", "reach" });
            A("clock", "Clockwork", AbilityKind.Evolved, Rarity.Legendary, "Rail Drive + Steady Hands: the perfect length.",
              l => "Straight drives never spray and never mishit", max: 1, recipe: new[] { "rail", "steady" });
            A("showtime", "Showtime", AbilityKind.Evolved, Rarity.Legendary, "Ace Machine + Thunder Serve: the crowd's favourite.",
              l => "Your first ace of each floor heals a heart", max: 1, inSim: false, recipe: new[] { "ace", "thunder" });
            A("matador", "Matador", AbilityKind.Evolved, Rarity.Legendary, "Trickle Boast + Volley Drop: send them the wrong way.",
              l => "After your drop or boast, the opponent is 20% slower for a moment", max: 1, recipe: new[] { "trickle", "vdrop" });

            // ---- athlete stats (attributes, reach, grace) ----
            A("quick", "Quick Feet", AbilityKind.Stat, Rarity.Common, "Faster to every corner.", l => "Speed +" + l, inSim: false);
            A("engine", "Engine", AbilityKind.Stat, Rarity.Common, "Run all day.", l => "Stamina +" + l, inSim: false);
            A("heavy", "Heavy Hitter", AbilityKind.Stat, Rarity.Common, "Harder drives and kills.", l => "Power +" + l, inSim: false);
            A("steady", "Steady Hands", AbilityKind.Stat, Rarity.Common, "Tighter at a stretch.", l => "Control +" + l, inSim: false);
            A("reach", "Long Arms", AbilityKind.Stat, Rarity.Rare, "The ball is in reach from further away.", l => "Reach " + Pct(l, 0.03));
            A("grace", "Early Bird", AbilityKind.Stat, Rarity.Rare, "Swing early and still connect.", l => "Timing window " + Pct(l, 0.12));

            // ---- run perks ----
            A("head", "Head Start", AbilityKind.Perk, Rarity.Rare, "Start every game ahead.", l => "Every game starts " + l + "-0 up", max: 3, inSim: false);
            A("lucky", "Lucky", AbilityKind.Perk, Rarity.Rare, "Better cards.", l => "Rare and better cards " + Pct(l, 0.25) + " more often", max: 3, locked: true, inSim: false);
            A("treasure", "Treasure", AbilityKind.Perk, Rarity.Common, "More coins from every floor.", l => "Coins " + Pct(l, 0.2), max: 3, inSim: false);
            A("scout", "Scout", AbilityKind.Perk, Rarity.Common, "Know who is next.", l => "See the next floor's opponent and trait before you pick", max: 1, locked: true, inSim: false);

            // ---- instant ----
            A("life", "Extra Life", AbilityKind.Instant, Rarity.Rare, "One more go.", l => "+1 heart (up to " + Gauntlet.MaxLives + ")", max: 1, inSim: false);
            A("golden", "Golden Racket", AbilityKind.Instant, Rarity.Epic, "The next floor you lose costs nothing.", l => "Your next lost floor costs no heart", max: 1, inSim: false);
            A("coins", "Purse", AbilityKind.Instant, Rarity.Common, "Coins now.", l => "+" + Gauntlet.PurseCoins + " coins", max: 1, inSim: false);

            // =========== the chaos remake (2026-10-28): the builds ===========
            const AbilityKind U = AbilityKind.Ultimate, S = AbilityKind.Shot, P = AbilityKind.Perk;
            // ---- TIME ----
            A("freeze", "Time Stop", U, Rarity.Epic, "ULTIMATE. The world stops - all but you.",
              l => "Your ultimate freezes the opponent solid for " + (0.7 + 0.25 * (l - 1)).ToString("0.0#") + " s: no step, no swing", max: 3, fam: Family.Time);
            A("slowfield", "Slow Field", P, Rarity.Rare, "Every shot of yours drags time around them.",
              l => "After each of your shots they move " + (6 * l) + "% slower for 0.6 s", fam: Family.Time);
            A("haste", "Haste", P, Rarity.Common, "Every shot of yours speeds you up.",
              l => "After each of your shots you move " + (5 * l) + "% faster for 1 s", fam: Family.Time);
            A("rewind", "Rewind", P, Rarity.Epic, "That never happened.",
              l => "The first " + (l == 1 ? "rally" : l + " rallies") + " you lose each game " + (l == 1 ? "is" : "are") + " played again", max: 2, locked: true, fam: Family.Time);
            A("overclock", "Overclock", P, Rarity.Rare, "Your POWER meter fills faster.",
              l => "POWER fills " + (20 * l) + "% faster", fam: Family.Time);
            // ---- POWER ----
            A("inferno", "Inferno", U, Rarity.Epic, "ULTIMATE. Set the ball on fire.",
              l => "Your ultimate makes your next " + (RunMods.FireShotsPerUlt - 1 + l) + " shots fireballs: 35% faster, and a fireball winner scores +2", max: 3, fam: Family.Power);
            A("meteor", "Meteor", S, Rarity.Rare, "Charged shots come down like meteors.",
              l => "Charged drives and kills " + (8 * l) + "% faster", fam: Family.Power);
            A("quake", "Quake", S, Rarity.Rare, "Your kills shake the court.",
              l => "Your kill's bounce makes them stumble: frozen " + (0.12 * l).ToString("0.0#") + " s", locked: true, fam: Family.Power);
            A("heavyball", "Heavy Ball", S, Rarity.Common, "Hard to dig out.",
              l => "After your drive or kill their reach is " + (4 * l) + "% shorter", fam: Family.Power);
            // ---- TRICK ----
            A("curve", "Curveball", S, Rarity.Rare, "Your drives bend after the front wall.",
              l => "Drives and kills curve away: " + (1.2 + 0.6 * l).ToString("0.0") + " m/s² sideways for 0.6 s", fam: Family.Trick);
            A("deaddrop", "Dead Drop", S, Rarity.Rare, "Drops and boasts that just die.",
              l => "Your drops' and boasts' first bounce keeps only " + (100 - 15 * l) + "% of its height", fam: Family.Trick);
            A("ricochet", "Ricochet", S, Rarity.Epic, "The walls make it faster.",
              l => "Every side wall your shot hits adds " + (5 * l) + "% pace", fam: Family.Trick);
            A("knuckle", "Knuckleball", S, Rarity.Common, "Impossible to time.",
              l => "Their timing window on your shots " + (8 * l) + "% shorter", fam: Family.Trick);
            // ---- JACKPOT ----
            A("jackpot", "Jackpot", U, Rarity.Epic, "ULTIMATE. Hit the jackpot.",
              l => "A winner off your ultimate scores +" + (1 + l) + " more", max: 3, fam: Family.Jackpot);
            A("combo", "Combo", P, Rarity.Rare, "Keep winning, win more.",
              l => "Each rally won in a row scores 1 more (up to +" + l + ")", fam: Family.Jackpot);
            A("wallride", "Wall Ride", P, Rarity.Epic, "Trick shots pay.",
              l => "+1 point for each wall your winner hit after the front wall (up to +" + l + ")", max: 3, locked: true, fam: Family.Jackpot);
            A("bounty", "Bounty", P, Rarity.Common, "Long rallies are worth more.",
              l => "Win a rally of " + (l >= 3 ? 6 : 8) + "+ shots: +" + (l >= 5 ? 2 : 1), fam: Family.Jackpot);
            A("firstblood", "First Blood", P, Rarity.Rare, "Strike first.",
              l => "The first point of each game scores +" + l, max: 3, locked: true, fam: Family.Jackpot);
            // ---- GUARD ----
            A("magnet", "Magnet Racket", P, Rarity.Rare, "The ball comes to you.",
              l => "Reach +" + (6 * l) + "%", fam: Family.Guard);
            A("shield", "Shield", P, Rarity.Rare, "Your mistakes don't count.",
              l => "Your first " + (l == 1 ? "tin or out" : l + " tins or outs") + " each game " + (l == 1 ? "is a let" : "are lets"), max: 3, fam: Family.Guard);
            // ---- the legendary evolutions ----
            A("world", "The World", AbilityKind.Evolved, Rarity.Legendary, "Time Stop + Slow Field: time is yours.",
              l => "Time Stop lasts twice as long, and every game starts with a full POWER meter", max: 1, recipe: new[] { "freeze", "slowfield" }, fam: Family.Time);
            A("supernova", "Supernova", AbilityKind.Evolved, Rarity.Legendary, "Inferno + Meteor: everything burns.",
              l => "Every charged shot is a fireball", max: 1, recipe: new[] { "inferno", "meteor" }, fam: Family.Power);
            A("wizard", "Wizard", AbilityKind.Evolved, Rarity.Legendary, "Curveball + Dead Drop: magic.",
              l => "A winner that curved scores +1", max: 1, recipe: new[] { "curve", "deaddrop" }, fam: Family.Trick);
            A("allin", "All In", AbilityKind.Evolved, Rarity.Legendary, "Jackpot + Combo: let it ride.",
              l => "The jackpot is +4, times your combo (up to x4)", max: 1, recipe: new[] { "jackpot", "combo" }, fam: Family.Jackpot);
            A("fortress", "Fortress", AbilityKind.Evolved, Rarity.Legendary, "Magnet Racket + Shield: nothing gets through.",
              l => "Your tins and outs are always lets", max: 1, recipe: new[] { "magnet", "shield" }, fam: Family.Guard);

            // the kept cards in their builds; the old small-number cards out of the pool
            F("focus", Family.Time); F("quick", Family.Time);
            F("heavy", Family.Power);
            F("steady", Family.Trick); F("grace", Family.Trick);
            F("ace", Family.Jackpot); F("nickbank", Family.Jackpot);
            F("vampire", Family.Guard); F("engine", Family.Guard); F("reach", Family.Guard); F("ghost", Family.Guard);
            foreach (var id in new[] { "nick", "vdrop", "trickle", "hlob", "rail", "wide", "thunder", "sniper", "momentum", "pressure", "second", "exec", "clock", "showtime", "matador" }) R(id);

            _byId = new Dictionary<string, Ability>();
            foreach (var a in _all) _byId[a.Id] = a;
            return _all;
        }

        public static Ability Find(string id) { All(); return id != null && _byId.TryGetValue(id, out var a) ? a : null; }
    }
}
