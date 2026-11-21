// ===========================================================
// Tools/MatchBench/RogueBench.cs
// P5: the Gauntlet roguelite, run after run by the human-style model -
// how far a median player gets, and which abilities carry runs.
//
//   --rogue [--games N] [--policy random|greedy] [--talents 0..3]
//
//   random   picks any card
//   greedy   levels up what it has first (a build), else a new card
//   talents  every talent at this level (capped at its own top)
//   --deck starter|full|max   2026-10-28: the run draws from a card deck -
//            the starter set; every card at level 2; the best 20 evolved
//
// Prints floors reached (median, quartiles, best), bosses, coins a run,
// and per ability: how often taken and the mean floor of runs that took it.
// ===========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SquashSim;
using SquashSim.Modes;
using SquashSim.Replays;

static class RogueBench
{
    public const int FloorCap = 40;
    /// <summary>The card deck the runs draw from (null: the old draw from every ability).</summary>
    public static string DeckMode;

    static List<RunMod> DeckFor(int r)
    {
        if (DeckMode == null) return null;
        CardCollection c;
        if (DeckMode == "starter") c = GauntletCards.Starter((uint)(77 + r));
        else
        {
            c = new CardCollection();
            foreach (var a in GauntletCards.All()) c.Cards.Add(new CardRec { Id = a.Id, Copies = DeckMode == "max" ? GauntletCards.CopiesFor[3] : GauntletCards.CopiesFor[2], Evolved = DeckMode == "max" });
            GauntletCards.AutoDeck(c);
        }
        return GauntletCards.RunDeck(c);
    }

    public static void Run(int runs, string policy, int talentLevel)
    {
        var talents = new List<RunMod>();
        if (talentLevel > 0)
            foreach (var t in GauntletTalents.All())
                talents.Add(new RunMod { Id = t.Id, Level = Math.Min(t.MaxLevel, talentLevel) });
        Console.WriteLine("ROGUE  " + runs + " runs, the human-style model, policy " + policy + ", talents " + talentLevel + (DeckMode != null ? ", deck " + DeckMode : ""));
        var floors = new List<int>();
        var taken = new Dictionary<string, List<int>>();
        int bosses = 0; long coins = 0; int lostAt = 0;
        var lossFloors = new List<int>();
        var ratios = new List<double>(); int wins = 0; long spins = 0;
        for (int r = 0; r < runs; r++)
        {
            var run = Gauntlet.NewRun((uint)(1000 + r * 7919), talents, DeckFor(r));
            var rng = new Random(r);
            int guard = 0;
            while (!run.Over && run.Floor <= FloorCap && guard++ < 400)
            {
                if (run.AwaitingPick) { Gauntlet.Pick(run, Choose(run, policy, rng)); continue; }
                if (run.ChoiceDue) { Gauntlet.Choose(run, Endless); if (run.Over) break; continue; }
                var (play, aces, nicks) = Floor(run, (uint)(r * 1000 + guard), talents);
                ratios.Add((double)play.Score / play.Target);
                if (Verbose) Console.WriteLine("    floor " + play.Floor + ": " + play.Score + "/" + play.Target + "  rallies " + play.Played + "/" + play.Rallies + "  done " + play.Done + "  best " + play.BestRally + "  mult " + play.Mult + "  spins " + play.Spins);
                spins += play.Spins;
                if (play.Cleared) Gauntlet.Won(run, play, aces, nicks, talents);
                else { int at = run.Floor; if (!Gauntlet.Lost(run, play)) lossFloors.Add(at); }
            }
            if (run.RunWon) wins++;
            floors.Add(run.FloorsWon);
            bosses += run.BossesBeaten; coins += run.Coins;
            foreach (var m in run.Abilities)
            {
                if (!taken.TryGetValue(m.Id, out var l)) taken[m.Id] = l = new List<int>();
                l.Add(run.FloorsWon);
            }
            Console.WriteLine("  run " + (r + 1).ToString().PadLeft(2) + ": floor " + run.FloorsWon.ToString().PadLeft(2) + "  bosses " + run.BossesBeaten + "  coins " + run.Coins.ToString().PadLeft(4) +
                              "  " + string.Join(" ", run.Abilities.ConvertAll(m => m.Id + m.Level)));
        }
        floors.Sort();
        int Q(double q) { return floors[Math.Min(floors.Count - 1, (int)(q * floors.Count))]; }
        Console.WriteLine("floors cleared  median " + Q(0.5) + "   p25 " + Q(0.25) + "   p75 " + Q(0.75) + "   best " + floors[floors.Count - 1] +
                          "   bosses a run " + ((double)bosses / runs).ToString("0.00") + "   coins a run " + (coins / runs));
        Console.WriteLine("runs won (floor " + Gauntlet.WinFloor + ") " + wins + " of " + runs + "   wheel spins a run " + ((double)spins / runs).ToString("0.0") +
                          "   score/target a floor  median " + ratios.OrderBy(x => x).ElementAt(ratios.Count / 2).ToString("0.00"));
        if (lossFloors.Count > 0) Console.WriteLine("runs ended at floors (median) " + lossFloors.OrderBy(x => x).ElementAt(lossFloors.Count / 2));
        Console.WriteLine("ability            taken   mean floor of those runs");
        foreach (var kv in taken.OrderByDescending(k => k.Value.Average()))
            Console.WriteLine("  " + kv.Key.PadRight(16) + kv.Value.Count.ToString().PadLeft(5) + "   " + kv.Value.Average().ToString("0.0"));
    }

    static string Choose(GauntletRun run, string policy, Random rng)
    {
        var offer = run.Offer;
        var evo = offer.Find(id => GauntletAbilities.Find(id).Kind == AbilityKind.Evolved);
        if (evo != null) return evo;
        if (policy == "greedy")
        {
            var up = offer.FindAll(id => Gauntlet.Level(run, id) > 0);
            if (up.Count > 0) return up[rng.Next(up.Count)];
            var life = offer.Find(id => id == "life");
            if (life != null && run.Floor % 5 == 4) return life;                // the extra rallies before a boss
            var keep = offer.FindAll(id => GauntletAbilities.Find(id).Kind != AbilityKind.Instant);
            if (keep.Count > 0) return keep[rng.Next(keep.Count)];
        }
        return offer[rng.Next(offer.Count)];
    }

    /// <summary>2026-11-21 the run goes into endless after floor 15 (--endless).</summary>
    public static bool Endless = true, Verbose = Environment.GetEnvironmentVariable("ROGUE_V") == "1";

    static (FloorPlay, int, int) Floor(GauntletRun run, uint seed, List<RunMod> talents)
    {
        var play = Gauntlet.BeginFloor(run, talents);
        var f = Gauntlet.Floor(run, run.Floor);
        var traits = Gauntlet.OppMods(f);
        var s = new ReplaySetup
        {
            Opponent = f.Style, Difficulty = (float)f.Difficulty, GamesToWin = 1, Scoring = "par", ParPoints = 999, BotSeed = seed, Lets = true,
            Attr0 = Gauntlet.Attributes(run), Mods0 = Gauntlet.SimMods(run), Attr1 = f.Attributes, Mods1 = traits,
        };
        ReplayMatch.Configure(s);
        var m = ReplayMatch.Build(s);
        m.Bots[0] = new HumanStyle(seed * 2654435761u + 7) { Arcade = true };
        m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
        Program.ArcadeAi(m);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        int aces = 0, nicks = 0, last = -1, pts = m.Score.Points[0];
        m.RallyEnded += o =>
        {
            int gp = m.Score.Points[0] - pts; pts = m.Score.Points[0];
            bool let = o == null || o.Kind == RallyEnd.Let || o.Winner < 0, won = !let && o.Winner == 0, notUp = !let && o.Kind == RallyEnd.NotUp;
            var t = m.Ball.LastRacketContact;
            GauntletFloor.Rally(play, new FloorRally
            {
                Won = won, Let = let, Kind = let ? RallyEnd.Let : o.Kind, Shots = m.Referee.Shots, GamePoints = gp,
                Ace = won && notUp && m.Referee.Shots == 1, Nick = won && notUp && m.NickBy == 0,
                Kill = won && notUp && m.LastStriker == 0 && t != null && t.Technique == "kill",
            });
            if (play.SpinDue) GauntletFloor.Spin(play);
        };
        while (!play.Done && m.Phase != MatchPhase.MatchOver && m.Time < 1800)
        {
            m.Step(dt);
            foreach (var e in m.StepEvents)
            {
                if (e.Type == SimEventType.RacketContact) last = e.PlayerIndex;
                else if (e.Type == SimEventType.Impact && e.Nick && last == 0) nicks++;
            }
        }
        foreach (var st in m.Stats) if (st.Shots == 1 && st.Outcome != null && st.Outcome.Kind == RallyEnd.NotUp && st.Outcome.Winner == 0) aces++;
        PhysicsConfig.ResetToDefaults();
        return (play, aces, nicks);
    }
}
