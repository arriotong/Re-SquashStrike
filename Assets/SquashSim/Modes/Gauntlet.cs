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
        /// <summary>3: 2026-11-21 the arcade remake - no hearts; a floor is a target score in a set number of rallies.</summary>
        public const int CurrentVersion = 3;
        public int Version = CurrentVersion;
        public bool Active;
        public uint Seed;
        public int Floor = 1, Lives = Gauntlet.StartLives, Score, Coins, Picks;
        public bool Golden, Over, AwaitingPick, Revived, CanRevive, NickHealed;
        public int Rerolls, Banishes, Cards = 3, Roll, BossesBeaten;
        public List<RunMod> Abilities = new List<RunMod>();
        public List<string> Offer = new List<string>();
        /// <summary>2026-11-23 the class the run is played as - chosen at its start, kept to its end ("" - the profile's, older runs).</summary>
        public string Class = "";
        public List<string> Banished = new List<string>();
        /// <summary>2026-10-28: the deck this run draws from (GauntletCards: each card and its level, 1-3, or 4 evolved).
        /// Empty: the old draw from every ability (runs from before the decks, the bench).</summary>
        public List<RunMod> Deck = new List<RunMod>();
        public bool HasDeck { get { return Deck != null && Deck.Count > 0; } }
        /// <summary>The floor just won was a boss (its pick takes two levels).</summary>
        public bool BossPick;
        /// <summary>2026-11-21 the arcade remake: rallies added to the next floor (Extra Rallies, Showtime...), floor 15 cleared
        /// (the run won), the choice after it waiting, endless chosen, the last floor's score and the best one.</summary>
        public int BonusRallies, LastFloorScore, BestFloorScore;
        public bool RunWon, ChoiceDue, Endless;
        /// <summary>The run ended by a failed floor (not taken as a win).</summary>
        public bool Failed;
        public int FloorsWon { get { return Floor - 1; } }
    }

    public sealed class FloorInfo
    {
        /// <summary>2026-11-21: the score to reach, and a boss's twists (Gauntlet.Twists ids).</summary>
        public int Target;
        public List<string> Twists = new List<string>();
        public FloorKind Kind;
        public string Style, Name, Title;
        public double Difficulty;
        public int Points;
        public List<RunMod> Traits = new List<RunMod>();
        public AthleteAttributes Attributes;            // null: neutral
        /// <summary>P6: a boss or an elite is a star (Stars): their id ("star:s45"; else the style), and a boss's signature shots.</summary>
        public string OpponentId;
        public List<RunMod> Signature = new List<RunMod>();
    }

    public static class Gauntlet
    {
        public const int StartLives = 3, MaxLives = 6, PurseCoins = 40;
        public static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };

        /// <summary>P6: the star a boss or elite floor is (null: an ordinary floor). Bosses climb the world ranking - floor 10 the
        /// No. 45 ... floor 90 the No. 5, and floor 100 the world No. 1; then round the top 10 again, the No. 1 every 100th floor.
        /// Elites are the stars just below the next boss (floor 5 the No. 48 ...); past 100, from 11 to 30.</summary>
        public static Career.Star StarFor(int floor)
        {
            var all = Career.Stars.All();
            if (KindOf(floor) != FloorKind.Boss) return null;
            // 2026-11-21: floor 5 the No. 30, 10 the No. 15, 15 the No. 5; endless climbs to the No. 1 (floor 35), then round the top 10
            int rank = floor == 5 ? 30 : floor == 10 ? 15 : floor == 15 ? 5 : floor <= 35 ? Math.Max(1, 5 - (floor - 15) / 5) : (floor / 5) % 10 + 1;
            return rank == 1 ? Career.Stars.No1 : all[rank - 1];
        }

        /// <summary>The traits an elite can have (and a boss two of): RunMods ids on the opponent, and their names.</summary>
        public static readonly (string id, string name, string text)[] Traits =
        {
            ("e_cannon", "Cannon", "drives and kills 15% faster"),
            ("e_tireless", "Tireless", "tires at half the rate"),
            ("e_sniper", "Sniper", "deadly accurate"),
            ("e_wall", "The Wall", "gets everywhere (Speed +3)"),
            // the chaos remake: the opponents bend the rules too (the player's cards at level 3)
            ("curve", "Curveball", "drives that bend"),
            ("deaddrop", "Dead Drop", "drops that die"),
            ("ricochet", "Ricochet", "faster off every wall"),
            ("knuckle", "Knuckleball", "hard to time"),
        };
        /// <summary>A chaos trait's level on an opponent.</summary>
        public const int ChaosTraitLevel = 3;

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
        static uint Mix(uint a, uint b) { return Hash(a * 0x9E3779B9u ^ Hash(b + 0x6D2B79F5u)); }

        // ---------------- a new run ----------------

        public static GauntletRun NewRun(uint seed, List<RunMod> talents = null, List<RunMod> deck = null)
        {
            var run = new GauntletRun { Active = true, Seed = seed == 0 ? 1u : seed };
            if (deck != null) run.Deck = new List<RunMod>(deck);
            run.Lives = StartLives + GauntletTalents.Level(talents, "hearty");
            run.Rerolls = 1 + GauntletTalents.Level(talents, "reroll");
            run.Banishes = GauntletTalents.Level(talents, "banish");
            run.Cards = 3 + GauntletTalents.Level(talents, "wider");
            run.CanRevive = GauntletTalents.Level(talents, "revive") > 0;
            // Kit Bag: a random common ability or two to start with
            int kit = run.HasDeck ? 0 : GauntletTalents.Level(talents, "kit");      // a deck: nothing active at first
            var commons = GauntletAbilities.All().FindAll(a => a.Rarity == Rarity.Common && a.Kind != AbilityKind.Instant && !a.Retired && GauntletTalents.Unlocked(talents, a));
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

        /// <summary>2026-11-21 the arcade remake: a boss every 5th floor (5, 10, 15 - then endless: 20, 25 ...).</summary>
        public static FloorKind KindOf(int floor) { return floor % 5 == 0 ? FloorKind.Boss : FloorKind.Normal; }
        /// <summary>Clear this floor and the run is won (then endless, if you choose).</summary>
        public const int WinFloor = 15;

        // ---------------- a floor's goal (2026-11-21) ----------------

        public const int Rallies = 12, BossRallies = 14, FirstTarget = 300;
        public const double TargetGrowth = 1.07, BossTarget = 1.15;

        /// <summary>The score to reach on a floor: 300, then 7% more a floor (rounded to 50); a boss 15% more (MatchBench --rogue: a new player clears about 5 floors, with talents 8, 1 in 9 wins).</summary>
        public static int TargetFor(int floor)
        {
            double t = FirstTarget * Math.Pow(TargetGrowth, Math.Max(0, floor - 1));
            if (KindOf(floor) == FloorKind.Boss) t *= BossTarget;
            return (int)(Math.Round(t / 50.0) * 50);
        }

        /// <summary>The rallies a floor gives: 12 (a boss 14), the Extra Rally talent, the run's bonus rallies, a Short Floor twist.</summary>
        public static int RalliesFor(GauntletRun run, FloorInfo f, List<RunMod> talents)
        {
            int n = (f.Kind == FloorKind.Boss ? BossRallies : Rallies) + GauntletTalents.Level(talents, "hearty") + run.BonusRallies;
            if (f.Twists.Contains("short")) n -= 2;
            return Math.Max(3, n);
        }

        /// <summary>The bosses' twists (the user's "bad coins"): one a boss, two from floor 20.</summary>
        public static readonly (string id, string name, string text)[] Twists =
        {
            ("ceiling", "Low Ceiling", "your multiplier stops at 3x"),
            ("winners", "Winners Only", "their mistakes score nothing"),
            ("short", "Short Floor", "2 fewer rallies"),
            ("cannon", "Cannon", "their drives are faster"),
            ("cold", "Cold Start", "your multiplier grows half as fast"),
            ("nowheel", "No Wheel", "the wheel doesn't spin this floor"),
        };

        public static string TwistName(string id) { foreach (var t in Twists) if (t.id == id) return t.name; return id; }
        public static string TwistText(string id) { foreach (var t in Twists) if (t.id == id) return t.text; return ""; }

        /// <summary>A floor's play: its target and rallies, the twists set on the multiplier and the wheel, a head start's points.</summary>
        public static FloorPlay BeginFloor(GauntletRun run, List<RunMod> talents)
        {
            var f = Floor(run, run.Floor);
            var p = GauntletFloor.Begin(run.Floor, f.Target, RalliesFor(run, f, talents), Mix(run.Seed, (uint)run.Floor * 31u + 7u), HeadStart(run) * 100);
            if (f.Twists.Contains("ceiling")) p.MaxMult = 3.0;
            if (f.Twists.Contains("cold")) p.MultStep = 0.5;
            if (f.Twists.Contains("winners")) p.WinnersOnly = true;
            if (f.Twists.Contains("nowheel")) p.NoWheel = true;
            return p;
        }

        /// <summary>The AI's difficulty on a floor: from the easiest, a slow climb - the game's Medium about floor 20, Hard about
        /// 37, the top past 125 (MatchBench --rogue: a median player clears about 9 floors, 40% beat the first boss).
        /// 2026-10-28 (card decks: "bots needs to be harder and harder so it doesn't get boring when you keep winning"): past
        /// the Knee the climb steepens (Climb2 a floor more), and the opponent's athlete starts to grow from LiftFrom.</summary>
        public static double Difficulty(int floor)
        {
            // 2026-11-21 the arcade remake: 0.10 on floor 1 to 0.70 on floor 15, then +0.02 a floor in endless
            int f = Math.Max(1, floor);
            double d = f <= WinFloor ? 0.10 + 0.60 * (f - 1) / (WinFloor - 1) : 0.70 + 0.02 * (f - WinFloor);
            return Math.Min(1.0, d);
        }
        public const int Knee = 15, LiftFrom = 10, LiftEvery = 3, CannonFrom = 30;
        public const double Climb2 = 0.012;
        public const double Start = 0.0, Climb = 0.0075;     // P6: 0.008 after the AI learned to keep out of its own shot and the line (median 8 floors); the chaos remake: 0.0055 -> 0.0075 (the cards are stronger: MatchBench --rogue median 15 -> 13, p25 9, a great build 80)

        public static FloorInfo Floor(GauntletRun run, int floor)
        {
            var f = new FloorInfo { Kind = KindOf(floor), Difficulty = Difficulty(floor), Points = KindOf(floor) == FloorKind.Boss ? 11 : 7, Target = TargetFor(floor) };
            uint h = Mix(run.Seed, (uint)floor);
            var star = StarFor(floor);
            if (star != null)
            {
                f.Style = star.Style; f.Name = Career.Stars.FullName(star); f.OpponentId = "star:" + star.Id;
            }
            if (f.Kind == FloorKind.Boss)
            {
                f.Title = star.Title ?? "";                                     // 2026-11-23: the pros past the top 10 have none (the floor-5 crash)
                f.Difficulty = Math.Min(1.0, f.Difficulty + 0.05);
                if (star == Career.Stars.No1) f.Difficulty = 1.0;               // the world No. 1 plays at their very best
                f.Signature.AddRange(star.Signature);
            }
            else if (star == null)
            {
                f.Style = Styles[(int)(h % (uint)Styles.Length)];
                f.Name = "Floor " + floor;
                f.OpponentId = f.Style;
            }
            int traits = floor > WinFloor ? 1 + (floor - WinFloor) / 10 : 0;   // 2026-11-21: endless floors bend the rules too
            // the bosses' twists: one, two from floor 20 (never the same twice)
            if (f.Kind == FloorKind.Boss)
            {
                var tw = new List<int>(); for (int k = 0; k < Twists.Length; k++) tw.Add(k);
                uint th = Mix(run.Seed ^ 0xB055u, (uint)floor);
                for (int i = 0; i < (floor >= 20 ? 2 : 1) && tw.Count > 0; i++)
                {
                    th = Hash(th + 29);
                    int k = tw[(int)(th % (uint)tw.Count)];
                    tw.Remove(k);
                    f.Twists.Add(Twists[k].id);
                }
            }
            var pool = new List<int>();
            for (int k = 0; k < Traits.Length; k++) pool.Add(k);
            for (int i = 0; i < traits && pool.Count > 0; i++)
            {
                h = Hash(h + 17);
                int k = pool[(int)(h % (uint)pool.Count)];
                pool.Remove(k);
                f.Traits.Add(new RunMod { Id = Traits[k].id, Level = Traits[k].id.StartsWith("e_") ? 1 : ChaosTraitLevel + (floor > 50 ? Math.Min(2, (floor - 50) / 15 + 1) : 0) });
            }
            if (f.Twists.Contains("cannon") && !f.Traits.Exists(t => t.Id == "e_cannon")) f.Traits.Add(new RunMod { Id = "e_cannon", Level = 1 });
            // past LiftFrom the opponent's athlete grows; past CannonFrom the Cannon does
            int lift = floor > LiftFrom ? Math.Min(5, (floor - LiftFrom) / LiftEvery) : 0;
            bool wall = f.Traits.Exists(t => t.Id == "e_wall");
            if (lift > 0 || wall)
            {
                double v = AthleteAttributes.Neutral + lift;
                f.Attributes = new AthleteAttributes { Speed = Math.Min(10, v + (wall ? 3 : 0)), Stamina = v, Power = v, Control = v };
            }
            if (floor > CannonFrom)
            {
                var c = f.Traits.Find(t => t.Id == "e_cannon");
                if (c == null) { c = new RunMod { Id = "e_cannon", Level = 0 }; f.Traits.Add(c); }
                c.Level += (floor - CannonFrom) / 10 + 1;
            }
            f.Traits.RemoveAll(t => t.Id == "e_wall");                        // the Wall is its attributes, not a sim trait
            if (wall) f.Traits.Add(new RunMod { Id = "e_wall", Level = 0 });   // (kept for its name)
            return f;
        }

        /// <summary>The opponent's abilities in the sim: its traits (the ones that act) and a boss's signature shots.</summary>
        public static List<RunMod> OppMods(FloorInfo f)
        {
            var l = f.Traits.FindAll(t => t.Level > 0);
            l.AddRange(f.Signature);
            return l.Count > 0 ? l : null;
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

        /// <summary>2026-11-21 a floor cleared (its target reached): the score and coins (the wheel's too), its card levels, the
        /// next floor's bonus rallies (Showtime, Nick Bank), the pick - one more card for beating the target by half again, and the
        /// wheel's extra picks. Floor 15: the run is won (the choice: take the win, or endless).</summary>
        public static void Won(GauntletRun run, FloorPlay play, int aces, int nicks, List<RunMod> talents = null)
        {
            if (!run.Active || run.Over || run.AwaitingPick) return;
            int floor = run.Floor;
            var kind = KindOf(floor);
            run.Score += play.Score;
            run.LastFloorScore = play.Score;
            if (play.Score > run.BestFloorScore) run.BestFloorScore = play.Score;
            run.Coins += CoinsFor(run, floor, talents) + play.Coins;
            run.BonusRallies = 0;
            if (aces > 0 && Level(run, "showtime") > 0) run.BonusRallies++;
            if (nicks > 0 && Level(run, "nickbank") >= 5 && !run.NickHealed) { run.NickHealed = true; run.BonusRallies++; }
            for (int i = 0; i < play.CardLevels; i++) LevelUpOne(run, (uint)i);
            if (kind == FloorKind.Boss) run.BossesBeaten++;
            run.Floor++;
            run.BossPick = kind == FloorKind.Boss;
            run.Roll = 0;
            if (floor == WinFloor && !run.Endless) { run.RunWon = true; run.ChoiceDue = true; }
            bool big = (long)play.Score * 2 >= (long)play.Target * 3;
            int cards = Math.Min(MaxDraw, run.Cards + (big ? 1 : 0) + play.ExtraPicks + LuckyCards(Level(run, "lucky")));
            run.Offer = OfferFor(run, talents, cards);
            run.AwaitingPick = run.Offer.Count > 0;
        }

        /// <summary>The wheel's "a card levels up": one of your powers, not yet at its top, one level up (seeded).</summary>
        static void LevelUpOne(GauntletRun run, uint salt)
        {
            var can = new List<RunMod>();
            foreach (var m in run.Abilities) { var a = GauntletAbilities.Find(m.Id); if (a != null && a.Kind != AbilityKind.Instant && m.Level < a.MaxLevel) can.Add(m); }
            if (can.Count == 0) { run.Coins += 20; return; }
            can[(int)(Mix(run.Seed, (uint)run.Floor * 977u + salt) % (uint)can.Count)].Level++;
        }

        /// <summary>2026-11-21 a floor failed (the rallies ran out short of the target): the run is over - unless the Golden Racket
        /// or Second Chance gives the floor again (true: play it again).</summary>
        public static bool Lost(GauntletRun run, FloorPlay play = null)
        {
            if (!run.Active || run.Over) return false;
            if (play != null) { run.LastFloorScore = play.Score; if (play.Score > run.BestFloorScore) run.BestFloorScore = play.Score; }
            if (run.Golden) { run.Golden = false; return true; }
            if (run.CanRevive && !run.Revived) { run.Revived = true; return true; }
            run.Over = true; run.Failed = true;
            return false;
        }

        /// <summary>Floor 15 cleared: take the win (the run ends, won) or go on into endless.</summary>
        public static void Choose(GauntletRun run, bool endless)
        {
            if (!run.ChoiceDue) return;
            run.ChoiceDue = false;
            if (endless) run.Endless = true;
            else { run.Over = true; run.AwaitingPick = false; run.Offer.Clear(); }
        }

        // ---------------- the cards ----------------

        static readonly int[] RarityWeight = { 60, 28, 10, 3 };

        /// <summary>The cards on offer now (seeded: the run, the floor, the rerolls so far).</summary>
        public static List<string> OfferFor(GauntletRun run, List<RunMod> talents, int cards = -1)
        {
            if (cards < 0) cards = Math.Min(MaxDraw, run.Cards + (KindOf(run.Floor - 1) == FloorKind.Elite ? 1 : 0) + LuckyCards(Level(run, "lucky")));
            if (run.HasDeck) return DeckOffer(run, cards);
            bool boss = run.BossPick;
            var offer = new List<string>();
            var pool = new List<(string id, double w)>();
            bool activeFull = Count(run, true) >= GauntletAbilities.ActiveSlots, passiveFull = Count(run, false) >= GauntletAbilities.PassiveSlots;
            foreach (var a in GauntletAbilities.All())
            {
                if (a.Retired || run.Banished.Contains(a.Id) || !GauntletTalents.Unlocked(talents, a)) continue;
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
                    if (a.Id == "life" && run.BonusRallies >= 4) continue;
                    if (a.Id == "golden" && run.Golden) continue;
                    w = a.Id == "coins" ? 6 : a.Id == "life" ? 8 : 4;
                }
                else
                {
                    if (lv >= a.MaxLevel) continue;
                    if (lv == 0 && (a.Active ? activeFull : passiveFull)) continue;
                    if (boss && a.Rarity == Rarity.Common) continue;
                    w = RarityWeight[(int)a.Rarity];
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

        /// <summary>A deck's draw: cards not yet active, any of them alike (it is your deck); an in-run evolution that is ready
        /// always; with the deck spent (or the slots full), a bonus (a heart, the purse) instead.</summary>
        /// <summary>Lucky: more cards in every draw (1 at levels 1-2, 2 at 3).</summary>
        public static int LuckyCards(int level) { return level >= 3 ? 2 : level >= 1 ? 1 : 0; }
        /// <summary>The most cards a floor's draw shows (the pick screen's room).</summary>
        public const int MaxDraw = 5;

        static List<string> DeckOffer(GauntletRun run, int cards)
        {
            var offer = new List<string>();
            bool activeFull = Count(run, true) >= GauntletAbilities.ActiveSlots, passiveFull = Count(run, false) >= GauntletAbilities.PassiveSlots;
            foreach (var a in GauntletAbilities.All())
            {
                if (a.Kind != AbilityKind.Evolved || a.Recipe == null || Level(run, a.Id) > 0) continue;
                var p0 = GauntletAbilities.Find(a.Recipe[0]); var p1 = GauntletAbilities.Find(a.Recipe[1]);
                if (p0 != null && p1 != null && Level(run, p0.Id) >= p0.MaxLevel && Level(run, p1.Id) >= p1.MaxLevel) offer.Add(a.Id);
            }
            var pool = new List<string>();
            foreach (var c in run.Deck)
            {
                var a = GauntletAbilities.Find(c.Id);
                if (a == null || Level(run, c.Id) > 0 || run.Banished.Contains(c.Id)) continue;
                if (a.Active ? activeFull : passiveFull) continue;
                if (!pool.Contains(c.Id)) pool.Add(c.Id);
            }
            uint h = Mix(run.Seed ^ 0xC0FFEEu, (uint)run.Floor * 131u + (uint)run.Roll);
            while (offer.Count < cards && pool.Count > 0)
            {
                h = Hash(h + 0x6D2B79F5u);
                int k = (int)(h % (uint)pool.Count);
                offer.Add(pool[k]);
                pool.RemoveAt(k);
            }
            if (offer.Count == 0 && !run.BossPick) offer.Add(run.BonusRallies < 4 ? "life" : "coins");
            return offer;
        }

        /// <summary>A card's level in this run's deck (0: not in it).</summary>
        public static int DeckLevel(GauntletRun run, string id)
        {
            if (!run.HasDeck) return 0;
            foreach (var c in run.Deck) if (c.Id == id) return c.Level;
            return 0;
        }

        /// <summary>The level a card would take an ability to.</summary>
        public static int LevelAfter(GauntletRun run, string id)
        {
            var a = GauntletAbilities.Find(id);
            if (a == null) return 0;
            int dl = DeckLevel(run, id);
            if (dl > 0) return Math.Min(a.MaxLevel, GauntletCards.PlayLevel(a, dl) + (run.BossPick ? 1 : 0));   // a deck card: its level
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
                case "life": run.BonusRallies += 2; break;                     // 2026-11-21: +2 rallies on the next floor
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
