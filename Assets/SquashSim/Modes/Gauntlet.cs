// ===========================================================
// Assets/SquashSim/Modes/Gauntlet.cs
// P5: the Gauntlet - an endless roguelite. Floor after floor, one short
// game each, until the hearts run out; after every floor won, pick one
// of three cards and build a player (P3-R5's Gauntlet, grown up).
//
//   floors     one game to 7 (PAR). Every 5th an ELITE (a trait - more
//              pace, never tires, deadly accurate, everywhere) and a pick
//              of 4; every 10th a BOSS: a legend, a game to 11, two
//              traits, and a reward pick (rare and up, two levels at once)
//   endless    the AI's difficulty climbs a little every floor; past
//              floor 30 the opponent's athlete gets stronger too; past 60
//              its Cannon grows - it never stops
//   cards      abilities (GauntletAbilities): new ones at level 1,
//              owned ones a level up, an evolution when its two parts
//              are at the top. 6 active and 6 passive at most: once a
//              group is full only its own come up - a build
//   hearts     3 (+ talents). A lost floor costs one and is played again
//              (the golden racket, Revive, Showtime, Nick Bank can save)
//   score      100 a floor + 10 a point of margin; x1.5 elite, x3 boss
//   coins      every floor, kept when the run ends: talents
//              (GauntletTalents) between runs
//
// Deterministic from the run's seed (the cards, the opponents); the game
// keeps the run in the profile, so it can be left and resumed.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    public enum FloorKind { Normal, Elite, Boss }

    [Serializable]
    public sealed class GauntletRun
    {
        public const int CurrentVersion = 2;
        public int Version = CurrentVersion;
        public bool Active;
        public uint Seed;
        public int Floor = 1, Lives = Gauntlet.StartLives, Score, Coins, Picks;
        public bool Golden, Over, AwaitingPick, Revived, CanRevive, NickHealed;
        public int Rerolls, Banishes, Cards = 3, Roll, BossesBeaten;
        public List<RunMod> Abilities = new List<RunMod>();
        public List<string> Offer = new List<string>();
        public List<string> Banished = new List<string>();
        /// <summary>The floor just won was a boss (its pick takes two levels).</summary>
        public bool BossPick;
        public int FloorsWon { get { return Floor - 1; } }
    }

    public sealed class FloorInfo
    {
        public FloorKind Kind;
        public string Style, Name, Title;
        public double Difficulty;
        public int Points;
        public List<RunMod> Traits = new List<RunMod>();
        public AthleteAttributes Attributes;            // null: neutral
    }

    public static class Gauntlet
    {
        public const int StartLives = 3, MaxLives = 6, PurseCoins = 40;
        public static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };

        /// <summary>The legends at every 10th floor, in turn (fictional).</summary>
        public static readonly (string name, string title, string style)[] Bosses =
        {
            ("Marek Stahl", "The Wall", "retriever"),
            ("Aiko Tanabe", "The Assassin", "attacker"),
            ("Ruben Okafor", "The Magician", "shotmaker"),
            ("Hollis Brandt", "The Machine", "grinder"),
            ("Sola Vance", "The Storm", "attacker"),
            ("Ilya Korsak", "The Ghost", "retriever"),
        };

        /// <summary>The traits an elite can have (and a boss two of): RunMods ids on the opponent, and their names.</summary>
        public static readonly (string id, string name, string text)[] Traits =
        {
            ("e_cannon", "Cannon", "drives and kills 15% faster"),
            ("e_tireless", "Tireless", "tires at half the rate"),
            ("e_sniper", "Sniper", "deadly accurate"),
            ("e_wall", "The Wall", "gets everywhere (Speed +3)"),
        };

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
        static uint Mix(uint a, uint b) { return Hash(a * 0x9E3779B9u ^ Hash(b + 0x6D2B79F5u)); }

        // ---------------- a new run ----------------

        public static GauntletRun NewRun(uint seed, List<RunMod> talents = null)
        {
            var run = new GauntletRun { Active = true, Seed = seed == 0 ? 1u : seed };
            run.Lives = StartLives + GauntletTalents.Level(talents, "hearty");
            run.Rerolls = 1 + GauntletTalents.Level(talents, "reroll");
            run.Banishes = GauntletTalents.Level(talents, "banish");
            run.Cards = 3 + GauntletTalents.Level(talents, "wider");
            run.CanRevive = GauntletTalents.Level(talents, "revive") > 0;
            // Kit Bag: a random common ability or two to start with
            int kit = GauntletTalents.Level(talents, "kit");
            var commons = GauntletAbilities.All().FindAll(a => a.Rarity == Rarity.Common && a.Kind != AbilityKind.Instant && GauntletTalents.Unlocked(talents, a));
            uint h = Mix(run.Seed, 777);
            for (int i = 0; i < kit && commons.Count > 0; i++)
            {
                h = Hash(h + 1);
                var a = commons[(int)(h % (uint)commons.Count)];
                commons.Remove(a);
                SetLevel(run, a.Id, 1);
            }
            return run;
        }

        // ---------------- the floors ----------------

        public static FloorKind KindOf(int floor) { return floor % 10 == 0 ? FloorKind.Boss : floor % 5 == 0 ? FloorKind.Elite : FloorKind.Normal; }

        /// <summary>The AI's difficulty on a floor: from the easiest, a slow climb - the game's Medium about floor 20, Hard about
        /// 37, the top past 125 (MatchBench --rogue: a median player clears about 10 floors).</summary>
        public static double Difficulty(int floor) { return Math.Min(1.0, Start + Climb * (Math.Max(1, floor) - 1)); }
        public const double Start = 0.0, Climb = 0.008;

        public static FloorInfo Floor(GauntletRun run, int floor)
        {
            var f = new FloorInfo { Kind = KindOf(floor), Difficulty = Difficulty(floor), Points = KindOf(floor) == FloorKind.Boss ? 11 : 7 };
            uint h = Mix(run.Seed, (uint)floor);
            if (f.Kind == FloorKind.Boss)
            {
                var b = Bosses[(floor / 10 - 1) % Bosses.Length];
                f.Style = b.style; f.Name = b.name; f.Title = b.title;
                f.Difficulty = Math.Min(1.0, f.Difficulty + 0.02);
            }
            else
            {
                f.Style = Styles[(int)(h % (uint)Styles.Length)];
                f.Name = "Floor " + floor;
            }
            int traits = f.Kind == FloorKind.Boss ? (floor >= 20 ? 2 : 1) : f.Kind == FloorKind.Elite ? 1 : 0;
            if (floor > 40) traits++;                                         // late floors: every opponent has one more
            var pool = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < traits && pool.Count > 0; i++)
            {
                h = Hash(h + 17);
                int k = pool[(int)(h % (uint)pool.Count)];
                pool.Remove(k);
                f.Traits.Add(new RunMod { Id = Traits[k].id, Level = 1 });
            }
            if (f.Kind == FloorKind.Elite) f.Title = "Elite  ·  " + TraitNames(f);
            // past floor 30 the opponent's athlete grows; past 60 the Cannon does
            int lift = floor > 30 ? Math.Min(5, (floor - 30) / 6) : 0;
            bool wall = f.Traits.Exists(t => t.Id == "e_wall");
            if (lift > 0 || wall)
            {
                double v = AthleteAttributes.Neutral + lift;
                f.Attributes = new AthleteAttributes { Speed = Math.Min(10, v + (wall ? 3 : 0)), Stamina = v, Power = v, Control = v };
            }
            if (floor > 60)
            {
                var c = f.Traits.Find(t => t.Id == "e_cannon");
                if (c == null) { c = new RunMod { Id = "e_cannon", Level = 0 }; f.Traits.Add(c); }
                c.Level += (floor - 60) / 10 + 1;
            }
            f.Traits.RemoveAll(t => t.Id == "e_wall");                        // the Wall is its attributes, not a sim trait
            if (wall) f.Traits.Add(new RunMod { Id = "e_wall", Level = 0 });   // (kept for its name)
            return f;
        }

        public static string TraitNames(FloorInfo f)
        {
            var names = new List<string>();
            foreach (var t in f.Traits) foreach (var tr in Traits) if (tr.id == t.Id) names.Add(tr.name);
            return string.Join(" + ", names);
        }

        // ---------------- your player, from the abilities ----------------

        public static int Level(GauntletRun run, string id)
        {
            foreach (var a in run.Abilities) if (a.Id == id) return a.Level;
            return 0;
        }

        static void SetLevel(GauntletRun run, string id, int level)
        {
            foreach (var a in run.Abilities) if (a.Id == id) { a.Level = level; return; }
            run.Abilities.Add(new RunMod { Id = id, Level = level });
        }

        public static AthleteAttributes Attributes(GauntletRun run)
        {
            double V(string id) { return Math.Min(AthleteAttributes.Max, AthleteAttributes.Neutral + Level(run, id)); }
            return new AthleteAttributes { Speed = V("quick"), Stamina = V("engine"), Power = V("heavy"), Control = V("steady") };
        }

        /// <summary>The abilities the sim plays (RunMods ids with levels).</summary>
        public static List<RunMod> SimMods(GauntletRun run)
        {
            var l = new List<RunMod>();
            foreach (var m in run.Abilities)
            {
                var a = GauntletAbilities.Find(m.Id);
                if (a != null && a.InSim && m.Level > 0) l.Add(new RunMod { Id = m.Id, Level = m.Level });
            }
            return l;
        }

        public static int HeadStart(GauntletRun run) { return Level(run, "head"); }
        public static int FocusCharges(GauntletRun run) { return Level(run, "focus"); }

        public static int Count(GauntletRun run, bool active)
        {
            int n = 0;
            foreach (var m in run.Abilities)
            {
                var a = GauntletAbilities.Find(m.Id);
                if (a != null && (active ? a.Active : a.Passive)) n++;
            }
            return n;
        }

        // ---------------- a floor's end ----------------

        public static int CoinsFor(GauntletRun run, int floor, List<RunMod> talents)
        {
            var k = KindOf(floor);
            double c = 8 + floor;
            if (k == FloorKind.Elite) c *= 2; else if (k == FloorKind.Boss) c *= 5;
            c *= 1 + 0.1 * GauntletTalents.Level(talents, "treasure");
            c *= 1 + 0.2 * Level(run, "treasure");
            return (int)Math.Round(c);
        }

        /// <summary>A floor won by this many points (with this many aces and nicks of yours): score, coins, heals, the pick.</summary>
        public static void Won(GauntletRun run, int margin, int aces, int nicks, List<RunMod> talents = null)
        {
            if (!run.Active || run.Over || run.AwaitingPick) return;
            int floor = run.Floor;
            var kind = KindOf(floor);
            double mult = kind == FloorKind.Boss ? 3 : kind == FloorKind.Elite ? 1.5 : 1;
            run.Score += (int)Math.Round((100 + 10 * Math.Max(0, margin)) * mult);
            run.Coins += CoinsFor(run, floor, talents);
            if (aces > 0 && Level(run, "showtime") > 0) Heal(run);
            if (nicks > 0 && Level(run, "nickbank") >= 5 && !run.NickHealed) { run.NickHealed = true; Heal(run); }
            if (kind == FloorKind.Boss) run.BossesBeaten++;
            run.Floor++;
            run.BossPick = kind == FloorKind.Boss;
            run.Roll = 0;
            int cards = run.Cards + (kind == FloorKind.Elite ? 1 : 0);
            run.Offer = OfferFor(run, talents, cards);
            run.AwaitingPick = run.Offer.Count > 0;
        }

        static void Heal(GauntletRun run) { if (run.Lives < MaxLives) run.Lives++; }

        /// <summary>A floor lost: a heart (unless the golden racket saves it); none left - Revive once, else the run is over.</summary>
        public static void Lost(GauntletRun run)
        {
            if (!run.Active || run.Over) return;
            if (run.Golden) { run.Golden = false; return; }
            run.Lives--;
            if (run.Lives > 0) return;
            if (run.CanRevive && !run.Revived) { run.Revived = true; run.Lives = 1; return; }
            run.Lives = 0; run.Over = true;
        }

        // ---------------- the cards ----------------

        static readonly int[] RarityWeight = { 60, 28, 10, 3 };

        /// <summary>The cards on offer now (seeded: the run, the floor, the rerolls so far).</summary>
        public static List<string> OfferFor(GauntletRun run, List<RunMod> talents, int cards = -1)
        {
            if (cards < 0) cards = run.Cards + (KindOf(run.Floor - 1) == FloorKind.Elite ? 1 : 0);
            bool boss = run.BossPick;
            int lucky = Level(run, "lucky");
            var offer = new List<string>();
            var pool = new List<(string id, double w)>();
            bool activeFull = Count(run, true) >= GauntletAbilities.ActiveSlots, passiveFull = Count(run, false) >= GauntletAbilities.PassiveSlots;
            foreach (var a in GauntletAbilities.All())
            {
                if (run.Banished.Contains(a.Id) || !GauntletTalents.Unlocked(talents, a)) continue;
                int lv = Level(run, a.Id);
                double w;
                if (a.Kind == AbilityKind.Evolved)
                {
                    if (lv > 0 || a.Recipe == null) continue;
                    var p0 = GauntletAbilities.Find(a.Recipe[0]); var p1 = GauntletAbilities.Find(a.Recipe[1]);
                    if (p0 == null || p1 == null || Level(run, p0.Id) < p0.MaxLevel || Level(run, p1.Id) < p1.MaxLevel) continue;
                    offer.Add(a.Id);                                          // ready: always on the table
                    continue;
                }
                if (a.Kind == AbilityKind.Instant)
                {
                    if (boss) continue;
                    if (a.Id == "life" && run.Lives >= MaxLives) continue;
                    if (a.Id == "golden" && run.Golden) continue;
                    w = a.Id == "coins" ? 6 : a.Id == "life" ? 8 : 4;
                }
                else
                {
                    if (lv >= a.MaxLevel) continue;
                    if (lv == 0 && (a.Active ? activeFull : passiveFull)) continue;
                    if (boss && a.Rarity == Rarity.Common) continue;
                    w = RarityWeight[(int)a.Rarity];
                    if (a.Rarity != Rarity.Common) w *= 1 + 0.25 * lucky;
                    if (lv > 0) w *= 1.4;                                      // the build you have comes up a little more
                }
                pool.Add((a.Id, w));
            }
            uint h = Mix(run.Seed ^ 0xC0FFEEu, (uint)run.Floor * 131u + (uint)run.Roll);
            while (offer.Count < cards && pool.Count > 0)
            {
                double total = 0; foreach (var p in pool) total += p.w;
                h = Hash(h + 0x6D2B79F5u);
                double x = (h / 4294967296.0) * total;
                int pick = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++) { x -= pool[i].w; if (x <= 0) { pick = i; break; } }
                offer.Add(pool[pick].id);
                pool.RemoveAt(pick);
            }
            return offer;
        }

        /// <summary>The level a card would take an ability to.</summary>
        public static int LevelAfter(GauntletRun run, string id)
        {
            var a = GauntletAbilities.Find(id);
            if (a == null) return 0;
            return Math.Min(a.MaxLevel, Level(run, id) + (run.BossPick ? 2 : 1));
        }

        /// <summary>Take one of the cards (false when it is not on offer).</summary>
        public static bool Pick(GauntletRun run, string id)
        {
            if (!run.AwaitingPick || !run.Offer.Contains(id)) return false;
            var a = GauntletAbilities.Find(id);
            if (a == null) return false;
            switch (a.Kind == AbilityKind.Instant ? a.Id : null)
            {
                case "life": Heal(run); break;
                case "golden": run.Golden = true; break;
                case "coins": run.Coins += PurseCoins; break;
                default: SetLevel(run, id, LevelAfter(run, id)); break;
            }
            run.Picks++;
            run.Offer.Clear();
            run.AwaitingPick = false;
            run.BossPick = false;
            return true;
        }

        /// <summary>New cards, for a reroll.</summary>
        public static bool Reroll(GauntletRun run, List<RunMod> talents)
        {
            if (!run.AwaitingPick || run.Rerolls <= 0) return false;
            run.Rerolls--; run.Roll++;
            run.Offer = OfferFor(run, talents, run.Offer.Count);
            return true;
        }

        /// <summary>Strike a card out of the run, and draw again.</summary>
        public static bool Banish(GauntletRun run, string id, List<RunMod> talents)
        {
            if (!run.AwaitingPick || run.Banishes <= 0 || !run.Offer.Contains(id)) return false;
            var a = GauntletAbilities.Find(id);
            if (a == null || a.Kind == AbilityKind.Evolved) return false;
            run.Banishes--; run.Roll++;
            run.Banished.Add(id);
            run.Offer = OfferFor(run, talents, run.Offer.Count);
            return true;
        }
    }
}
