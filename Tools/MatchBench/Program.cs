// ===========================================================
// Tools/MatchBench/Program.cs
// Plays full games wall-bot vs wall-bot, headless, and reports:
//   - how rallies end (tin, out, not up, faults, lets, ...)
//   - shots per rally, rallies per game, simulated game length
//   - determinism: every game is played twice and must match exactly
//
//   dotnet run --project Tools/MatchBench -c Release -- [--games N] [--scoring par|english] [--verbose]
// ===========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using SquashSim;

public static class Program
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        int games = int.Parse(Arg(args, "--games") ?? "20");
        string scoring = Arg(args, "--scoring") ?? "par";
        bool verbose = Array.IndexOf(args, "--verbose") >= 0;
        if (Array.IndexOf(args, "--trace") >= 0) { TraceRallies(int.Parse(Arg(args, "--trace-rallies") ?? "4")); return 0; }

        var kinds = new Dictionary<string, int>();
        var shotsPerRally = new List<int>();
        int rallies = 0, unfinished = 0, nondeterministic = 0;
        double simSeconds = 0;
        var sw = Stopwatch.StartNew();

        for (int g = 0; g < games; g++)
        {
            uint seedA = (uint)(1000 + g * 2), seedB = (uint)(1001 + g * 2);
            string log1 = PlayGame(seedA, seedB, scoring, out MatchSimulation m, out bool finished);
            string log2 = PlayGame(seedA, seedB, scoring, out _, out _);
            if (log1 != log2) nondeterministic++;
            if (!finished) unfinished++;

            simSeconds += m.Time;
            foreach (var o in m.History)
            {
                rallies++;
                string k = o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.Out ? o.Reason : o.Kind.ToString();
                kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            shotsPerRally.AddRange(m.ShotsPerRally);

            Console.WriteLine("game " + (g + 1).ToString().PadLeft(2) + ": " + m.Score.Points[0] + "-" + m.Score.Points[1]
                + "  winner P" + (m.Score.GameWinner + 1) + "  " + m.History.Count + " rallies  "
                + m.Time.ToString("0") + " s sim" + (finished ? "" : "  (DID NOT FINISH)"));
            if (verbose) foreach (var o in m.History) Console.WriteLine("      " + o);
        }

        Console.WriteLine();
        Console.WriteLine("Rally endings over " + rallies + " rallies:");
        foreach (var kv in kinds.OrderByDescending(k => k.Value))
            Console.WriteLine("  " + kv.Key.PadRight(44) + kv.Value.ToString().PadLeft(5) + "  " + (100.0 * kv.Value / rallies).ToString("0.0").PadLeft(5) + "%");
        Console.WriteLine();
        Console.WriteLine("shots per rally: mean " + (shotsPerRally.Count > 0 ? shotsPerRally.Average() : 0).ToString("0.00")
            + ", max " + (shotsPerRally.Count > 0 ? shotsPerRally.Max() : 0)
            + ", rallies with 3+ shots " + shotsPerRally.Count(s => s >= 3));
        Console.WriteLine("simulated " + simSeconds.ToString("0") + " s in " + sw.ElapsedMilliseconds + " ms (" + (games * 2) + " games incl. replays)");
        Console.WriteLine("determinism: " + (nondeterministic == 0 ? "every game identical on replay" : nondeterministic + " games DIFFERED"));
        Console.WriteLine("unfinished games: " + unfinished);
        return nondeterministic == 0 && unfinished == 0 ? 0 : 1;
    }

    /// <summary>Print every event of the first few rallies of one game.</summary>
    static void TraceRallies(int count)
    {
        PhysicsConfig.ResetToDefaults();
        var m = new MatchSimulation(0);
        m.Bots[0] = new WallBot(1000);
        m.Bots[1] = new WallBot(1001);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        int shown = 0;
        m.RallyEnded += o => { Console.WriteLine("  => " + o); Console.WriteLine(); shown++; };
        Console.WriteLine("rally 1: server P" + (m.Score.Server + 1) + " box " + m.Score.ServeSide);
        while (shown < count && m.StepCount < 200000)
        {
            var phase = m.Phase;
            m.Step(dt);
            if (phase != MatchPhase.AwaitToss && m.Phase == MatchPhase.Rally && m.PhaseTime <= dt * 1.5)
                Console.WriteLine("  toss at t=" + m.Time.ToString("0.000"));
            if (phase == MatchPhase.RallyOver && m.Phase == MatchPhase.AwaitToss)
                Console.WriteLine("rally " + (shown + 1) + ": server P" + (m.Score.Server + 1) + " box " + m.Score.ServeSide
                    + "  P1 at " + m.Players[0].Player.Pos + "  P2 at " + m.Players[1].Player.Pos);
            foreach (var e in m.StepEvents)
            {
                string w = "  t=" + e.Time.ToString("0.000") + " ";
                if (e.Type == SimEventType.RacketContact)
                    Console.WriteLine(w + "RACKET P" + (e.PlayerIndex + 1) + " " + e.Technique + " at " + e.Point
                        + " out " + e.OutVel + " head " + e.HeadSpeed.ToString("0.0") + " offc " + e.OffCentreNorm.ToString("0.00")
                        + " reach " + e.ReachState);
                else if (e.Type == SimEventType.Impact)
                    Console.WriteLine(w + e.SurfaceId + " at " + e.Point + " speed " + e.ImpactSpeed.ToString("0.0"));
                else
                    Console.WriteLine(w + e.Type + " P" + (e.PlayerIndex + 1) + " at " + e.Point);
            }
        }
    }

    static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Play one game; returns a log that fingerprints everything that happened.</summary>
    static string PlayGame(uint seedA, uint seedB, string scoring, out MatchSimulation m, out bool finished)
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.Current.Match.Scoring = scoring;
        m = new MatchSimulation(0);
        m.Bots[0] = new WallBot(seedA);
        m.Bots[1] = new WallBot(seedB);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        var sb = new StringBuilder();
        long maxSteps = (long)(60 * 60 / dt);      // an hour of play
        while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < maxSteps)
            m.Step(dt);
        foreach (var o in m.History) sb.Append(o).Append('|').Append(o.Time.ToString("R")).Append('\n');
        sb.Append(m.Ball.Pos).Append(m.Ball.Vel).Append(m.Players[0].Player.Pos).Append(m.Players[1].Player.Pos);
        finished = m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver;
        return sb.ToString();
    }
}
