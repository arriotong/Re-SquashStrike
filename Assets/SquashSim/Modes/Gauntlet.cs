// ===========================================================
// Assets/SquashSim/Modes/Gauntlet.cs
// P3-R5: the Gauntlet - a run, floor by floor, until you run out of
// lives. The "one more run" loop.
//
//   floors     one game to 11 (PAR) against an AI that gets harder every
//              floor (difficulty 0.15 at floor 1, +0.04 a floor, up to
//              0.95 past floor 20), the personality rotating by the
//              run's seed
//   lives      3; losing a floor costs one and you play it again
//   perks      after each floor won, pick 1 of 3: attributes (+2 Speed,
//              ...; P3-C1), a 2-0 head start every game, an extra life,
//              the golden racket (the next lost floor costs no life),
//              +1 to everything
//   score      100 a floor, +10 a point of the winning margin
//
// Deterministic from the run's seed (the offers, the opponents); the
// game keeps the run in the profile, so it can be left and resumed.
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Modes
{
    [Serializable]
    public sealed class GauntletRun
    {
        public bool Active;
        public uint Seed;
        public int Floor = 1, Lives = Gauntlet.StartLives, Score, HeadStart;
        public bool Golden, Over, AwaitingPerk;
        public AthleteAttributes Attributes = new AthleteAttributes();
        public List<string> Perks = new List<string>();     // taken, in order
        public List<string> Offer = new List<string>();     // on the table after a floor won
        public int FloorsWon { get { return Floor - 1; } }
    }

    public sealed class GauntletPerk
    {
        public string Id, Name, Text;
        public Func<GauntletRun, bool> Allowed;
        public Action<GauntletRun> Apply;
    }

    public static class Gauntlet
    {
        public const int StartLives = 3, MaxLives = 5, MaxHeadStart = 4, OfferSize = 3;
        public static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };

        static double Cap(double v) { return v > AthleteAttributes.Max ? AthleteAttributes.Max : v; }

        static List<GauntletPerk> _perks;
        public static List<GauntletPerk> Perks()
        {
            if (_perks != null) return _perks;
            _perks = new List<GauntletPerk>();
            void P(string id, string name, string text, Func<GauntletRun, bool> allowed, Action<GauntletRun> apply)
            { _perks.Add(new GauntletPerk { Id = id, Name = name, Text = text, Allowed = allowed, Apply = apply }); }
            P("speed", "Quick feet", "+2 Speed: faster to every corner", r => r.Attributes.Speed < AthleteAttributes.Max, r => r.Attributes.Speed = Cap(r.Attributes.Speed + 2));
            P("stamina", "Second wind", "+2 Stamina: sprint and dive for longer", r => r.Attributes.Stamina < AthleteAttributes.Max, r => r.Attributes.Stamina = Cap(r.Attributes.Stamina + 2));
            P("power", "Heavy hitter", "+2 Power: harder drives and kills", r => r.Attributes.Power < AthleteAttributes.Max, r => r.Attributes.Power = Cap(r.Attributes.Power + 2));
            P("control", "Steady hands", "+2 Control: tighter shots at a stretch", r => r.Attributes.Control < AthleteAttributes.Max, r => r.Attributes.Control = Cap(r.Attributes.Control + 2));
            P("all", "All-rounder", "+1 to every attribute", r => r.Attributes.Mean < AthleteAttributes.Max, r =>
            {
                r.Attributes.Speed = Cap(r.Attributes.Speed + 1); r.Attributes.Stamina = Cap(r.Attributes.Stamina + 1);
                r.Attributes.Power = Cap(r.Attributes.Power + 1); r.Attributes.Control = Cap(r.Attributes.Control + 1);
            });
            P("head", "Head start", "Start every game 2-0 up", r => r.HeadStart < MaxHeadStart, r => r.HeadStart += 2);
            P("life", "Extra life", "+1 life (up to " + MaxLives + ")", r => r.Lives < MaxLives, r => r.Lives++);
            P("golden", "Golden racket", "The next floor you lose costs no life", r => !r.Golden, r => r.Golden = true);
            return _perks;
        }

        public static GauntletPerk Find(string id) { foreach (var p in Perks()) if (p.Id == id) return p; return null; }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        public static GauntletRun NewRun(uint seed)
        {
            return new GauntletRun { Active = true, Seed = seed == 0 ? 1u : seed };
        }

        /// <summary>The AI's difficulty on a floor.</summary>
        public static double Difficulty(int floor) { return Math.Min(0.95, 0.15 + 0.04 * (Math.Max(1, floor) - 1)); }

        /// <summary>The opponent's personality on a floor (the run's own order).</summary>
        public static string Opponent(GauntletRun run, int floor)
        {
            return Styles[(int)(Hash(run.Seed + (uint)floor * 0x9E3779B9u) % (uint)Styles.Length)];
        }

        /// <summary>The opponent's seed on a floor (its own rallies; a replayed floor plays out differently).</summary>
        public static uint BotSeed(GauntletRun run) { return Hash(run.Seed ^ ((uint)run.Floor * 2654435761u) ^ ((uint)run.Lives * 40503u) ^ (uint)run.Score); }

        /// <summary>A floor won by this many points: score, then three perks to choose from.</summary>
        public static void Won(GauntletRun run, int margin)
        {
            if (!run.Active || run.Over || run.AwaitingPerk) return;
            run.Score += 100 + 10 * Math.Max(0, margin);
            run.Floor++;
            run.Offer = OfferFor(run);
            run.AwaitingPerk = run.Offer.Count > 0;
        }

        /// <summary>A floor lost: a life (unless the golden racket saves it); none left, the run is over.</summary>
        public static void Lost(GauntletRun run)
        {
            if (!run.Active || run.Over) return;
            if (run.Golden) { run.Golden = false; return; }
            run.Lives--;
            if (run.Lives <= 0) { run.Lives = 0; run.Over = true; }
        }

        public static List<string> OfferFor(GauntletRun run)
        {
            var pool = Perks().FindAll(p => p.Allowed(run));
            var offer = new List<string>();
            uint h = Hash(run.Seed * 31u + (uint)run.Floor * 7919u);
            while (offer.Count < OfferSize && pool.Count > 0)
            {
                h = Hash(h + 0x6D2B79F5u);
                var p = pool[(int)(h % (uint)pool.Count)];
                pool.Remove(p);
                offer.Add(p.Id);
            }
            return offer;
        }

        /// <summary>Take one of the offered perks (false when it is not on offer).</summary>
        public static bool Choose(GauntletRun run, string id)
        {
            if (!run.AwaitingPerk || !run.Offer.Contains(id)) return false;
            var p = Find(id);
            if (p == null) return false;
            p.Apply(run);
            run.Perks.Add(id);
            run.Offer.Clear();
            run.AwaitingPerk = false;
            return true;
        }
    }
}
