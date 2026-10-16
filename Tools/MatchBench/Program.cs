// ===========================================================
// Tools/MatchBench/Program.cs
// Headless games between any two brains (wall-bot or AI personality),
// the Phase 1 "AI-vs-AI balance dashboard". Reports:
//   - wins per side
//   - how rallies end (tin, out, not up, faults, lets, no lets, ...)
//   - shots per rally, rally duration, metres run per rally (fatigue data)
//   - determinism: every game is played twice and must match exactly
//
//   dotnet run --project Tools/MatchBench -c Release -- [--games N] [--scoring par|english]
//        [--p0 wallbot|retriever|attacker|shotmaker|grinder] [--p1 ...]
//        [--d0 0..1] [--d1 0..1] [--verbose] [--trace]
//        [--games-to-win N]  (3 = best of 5: breaks between games, the ball cools)
//        [--warmup]          (each "game" starts with the knock-up and a cold ball)
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
    static string P0 = "retriever", P1 = "wallbot";
    static double D0 = 0.7, D1 = 0.7;
    static int GamesToWin = 1;
    static bool WarmUp;

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        int games = int.Parse(Arg(args, "--games") ?? "20");
        string scoring = Arg(args, "--scoring") ?? "par";
        P0 = Arg(args, "--p0") ?? P0; P1 = Arg(args, "--p1") ?? P1;
        D0 = double.Parse(Arg(args, "--d0") ?? D0.ToString()); D1 = double.Parse(Arg(args, "--d1") ?? D1.ToString());
        bool verbose = Array.IndexOf(args, "--verbose") >= 0;
        GamesToWin = int.Parse(Arg(args, "--games-to-win") ?? "1");
        WarmUp = Array.IndexOf(args, "--warmup") >= 0;
        if (Array.IndexOf(args, "--trace") >= 0) { TraceRallies(int.Parse(Arg(args, "--trace-rallies") ?? "4")); return 0; }
        if (Array.IndexOf(args, "--calibrate") >= 0) { Calibrate(games); return 0; }
        if (Array.IndexOf(args, "--notup") >= 0) { NotUpReport(games); return 0; }
        if (Array.IndexOf(args, "--knockup") >= 0) { KnockUpReport(); return 0; }

        Console.WriteLine("P1 = " + Describe(P0, D0) + "   vs   P2 = " + Describe(P1, D1) + "   (" + scoring + ")");
        var kinds = new Dictionary<string, int>();
        var stats = new List<RallyStats>();
        int[] wins = new int[2];
        int[] presses = new int[2], contacts = new int[2];
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
            else wins[GamesToWin > 1 ? m.Score.MatchWinner : m.Score.GameWinner]++;

            simSeconds += m.Time;
            foreach (var o in m.History)
            {
                rallies++;
                string k = o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.Out || o.Kind == RallyEnd.Let || o.Kind == RallyEnd.Stroke || o.Kind == RallyEnd.NoLet ? o.Reason : o.Kind.ToString();
                kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            stats.AddRange(m.Stats);
            for (int i = 0; i < 2; i++)
            {
                if (m.Bots[i] is OpponentBrain ob) { presses[i] += ob.RallyPresses; }
                contacts[i] += m.RallyContacts[i];
            }

            string result = GamesToWin > 1
                ? "games " + m.Score.Games[0] + "-" + m.Score.Games[1] + "  winner P" + (m.Score.MatchWinner + 1)
                : m.Score.Points[0] + "-" + m.Score.Points[1] + "  winner P" + (m.Score.GameWinner + 1);
            Console.WriteLine((GamesToWin > 1 ? "match " : "game ") + (g + 1).ToString().PadLeft(2) + ": " + result
                + "  " + m.History.Count + " rallies  "
                + m.Time.ToString("0") + " s sim  ball at serves " + LastServeTemps + (finished ? "" : "  (DID NOT FINISH)"));
            if (verbose) foreach (var o in m.History) Console.WriteLine("      " + o);
        }

        Console.WriteLine();
        Console.WriteLine("wins: P1 " + wins[0] + "  P2 " + wins[1]);
        Console.WriteLine("Rally endings over " + rallies + " rallies:");
        foreach (var kv in kinds.OrderByDescending(k => k.Value))
            Console.WriteLine("  " + kv.Key.PadRight(48) + kv.Value.ToString().PadLeft(5) + "  " + (100.0 * kv.Value / rallies).ToString("0.0").PadLeft(5) + "%");
        Console.WriteLine();
        if (stats.Count > 0)
        {
            Console.WriteLine("shots per rally: mean " + stats.Average(s => s.Shots).ToString("0.00") + ", median " + Median(stats.Select(s => (double)s.Shots))
                + ", max " + stats.Max(s => s.Shots) + ", rallies with 6+ shots " + stats.Count(s => s.Shots >= 6));
            Console.WriteLine("rally duration: mean " + stats.Average(s => s.Seconds).ToString("0.0") + " s, max " + stats.Max(s => s.Seconds).ToString("0.0") + " s");
            Console.WriteLine("stamina at rally end: mean " + stats.Average(s => (s.Stamina0 + s.Stamina1) / 2).ToString("0.00")
                + ", lowest " + stats.Min(s => Math.Min(s.Stamina0, s.Stamina1)).ToString("0.00")
                + "   after 10+ s rallies: " + (stats.Any(s => s.Seconds >= 10) ? stats.Where(s => s.Seconds >= 10).Average(s => Math.Min(s.Stamina0, s.Stamina1)).ToString("0.00") : "-")
                + "   after rallies < 3 s: " + (stats.Any(s => s.Seconds < 3) ? stats.Where(s => s.Seconds < 3).Average(s => Math.Min(s.Stamina0, s.Stamina1)).ToString("0.00") : "-"));
            Console.WriteLine("metres run per rally: P1 " + stats.Average(s => s.Run0).ToString("0.0") + ", P2 " + stats.Average(s => s.Run1).ToString("0.0")
                + "   (per second of rally: " + (stats.Sum(s => s.Run0 + s.Run1) / Math.Max(1e-9, 2 * stats.Sum(s => s.Seconds))).ToString("0.00") + " m/s)");
        }
        Console.WriteLine("rally strokes: P1 pressed " + presses[0] + " / contacted " + contacts[0] + "   P2 pressed " + presses[1] + " / contacted " + contacts[1] + "  (serves excluded)");
        Console.WriteLine("simulated " + simSeconds.ToString("0") + " s in " + sw.ElapsedMilliseconds + " ms (" + (games * 2) + " games incl. replays)");
        Console.WriteLine("determinism: " + (nondeterministic == 0 ? "every game identical on replay" : nondeterministic + " games DIFFERED"));
        Console.WriteLine("unfinished games: " + unfinished);
        return nondeterministic == 0 && unfinished == 0 ? 0 : 1;
    }

    static double Median(IEnumerable<double> v)
    {
        var a = v.OrderBy(x => x).ToArray();
        return a.Length == 0 ? 0 : a[a.Length / 2];
    }

    static string Describe(string kind, double d)
    {
        return kind == "wallbot" ? "wall-bot" : AiProfile.ByName(kind).WithDifficulty(d).Name;
    }

    static IAthleteBrain Brain(string kind, double d, uint seed)
    {
        if (kind == "wallbot") return new WallBot(seed);
        return new OpponentBrain(AiProfile.ByName(kind).WithDifficulty(d), seed);
    }

    static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static string LastServeTemps = "";

    /// <summary>One knock-up half: every feed, stroke and dead ball, with times.</summary>
    static void KnockUpReport()
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.Current.Match.WarmUpEnabled = true;
        var m = new MatchSimulation(0);
        m.Bots[0] = Brain(P0, D0, 7); m.Bots[1] = Brain(P1, D1, 8);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        int shots = 0; bool asleep = false;
        while (m.Phase == MatchPhase.WarmUp && m.WarmUpHalf == 0)
        {
            m.Step(dt);
            foreach (var e in m.StepEvents)
            {
                if (e.Type == SimEventType.RacketContact) { shots++; Console.WriteLine(m.PhaseTime.ToString("0.00").PadLeft(6) + "  STROKE " + e.Label + " reach " + e.ReachState + " out " + e.BallSpeedOut.ToString("0.0")); }
                else if (e.Type == SimEventType.Impact && !e.Resting) Console.WriteLine(m.PhaseTime.ToString("0.00").PadLeft(6) + "    " + e.SurfaceClass + " at " + e.Point + " v " + e.ImpactSpeed.ToString("0.0"));
            }
            if (m.Ball.Asleep != asleep) { asleep = m.Ball.Asleep; Console.WriteLine(m.PhaseTime.ToString("0.00").PadLeft(6) + (asleep ? "  ball asleep" : "  ball woken (feed)")); }
        }
        var c = m.Players[0];
        Console.WriteLine("shots " + shots + ", ball " + m.Ball.Temperature.ToString("0.0") + " C, player at " + c.Player.Pos);
    }

    /// <summary>Play one game; returns a log that fingerprints everything that happened.</summary>
    static string PlayGame(uint seedA, uint seedB, string scoring, out MatchSimulation m, out bool finished)
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.Current.Match.Scoring = scoring;
        PhysicsConfig.Current.Match.GamesToWin = GamesToWin;
        PhysicsConfig.Current.Match.WarmUpEnabled = WarmUp;
        m = new MatchSimulation(0);
        m.Bots[0] = Brain(P0, D0, seedA);
        m.Bots[1] = Brain(P1, D1, seedB);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        var sb = new StringBuilder();
        long maxSteps = (long)(60 * 60 * GamesToWin * 2 / dt);      // an hour of play per game
        bool oneGame = GamesToWin <= 1;
        double coldest = 99, warmest = 0;
        while (!(m.Phase == MatchPhase.MatchOver || (oneGame && m.Phase == MatchPhase.GameOver)) && m.StepCount < maxSteps)
        {
            m.Step(dt);
            if (m.Phase == MatchPhase.AwaitToss && m.PhaseTime == dt) { coldest = Math.Min(coldest, m.Ball.Temperature); warmest = Math.Max(warmest, m.Ball.Temperature); }
        }
        LastServeTemps = coldest.ToString("0.0") + "-" + warmest.ToString("0.0") + " C";
        foreach (var o in m.History) sb.Append(o).Append('|').Append(o.Time.ToString("R")).Append('\n');
        sb.Append(m.Ball.Pos.X.ToString("R")).Append(m.Ball.Vel.Z.ToString("R"))
          .Append(m.Players[0].Player.Pos.X.ToString("R")).Append(m.Players[1].Player.Pos.Z.ToString("R"));
        finished = m.Phase == MatchPhase.MatchOver || (oneGame && m.Phase == MatchPhase.GameOver);
        return sb.ToString();
    }

    /// <summary>How well does the AI's rollout model predict its own strokes? Predicted vs actual front-wall height.</summary>
    static void Calibrate(int games)
    {
        var rows = new List<(double pred, double actual, string tech, double contactY, bool volley)>();
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            var m = new MatchSimulation(0);
            m.Bots[0] = Brain(P0, D0, (uint)(1000 + g * 2));
            m.Bots[1] = Brain(P1, D1, (uint)(1001 + g * 2));
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            int pending = -1; double pred = 0; string tech = ""; double cy = 0; bool vol = false;
            int printed = 0;
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < 240L * 1800)
            {
                var vBefore = m.Ball.Vel.Clone(); var pBefore = m.Ball.Pos.Clone();
                m.Step(dt);
                foreach (var e in m.StepEvents)
                    if (g == 0 && printed < 12 && e.Type == SimEventType.RacketContact && m.Referee.Shots > 1 && e.Technique.Contains("VOLLEY") && m.Bots[e.PlayerIndex] is OpponentBrain ob)
                    {
                        printed++;
                        Console.WriteLine("VOLLEY at " + e.Point + " in(actual) " + vBefore + " in(pred) " + ob.LastPredIncoming + " predPt " + ob.LastPredPoint
                            + " out(actual) " + e.OutVel + " out(pred) " + ob.LastPredLaunch + " head " + e.HeadSpeed.ToString("0.0") + " " + e.Technique + (e.Rushed ? " RUSHED" : "") + " offc " + e.OffCentreNorm.ToString("0.00") + " assist " + e.AssistApplied.ToString("0.00"));
                    }
                foreach (var e in m.StepEvents)
                {
                    if (e.Type == SimEventType.RacketContact && m.Referee.Shots > 1 && m.Bots[e.PlayerIndex] is OpponentBrain b)
                    { pending = e.PlayerIndex; pred = b.LastPredFrontH; tech = e.Technique; cy = e.Point.Y; vol = tech.Contains("VOLLEY"); }
                    else if (pending >= 0 && e.Type == SimEventType.Impact)
                    {
                        if (e.SurfaceClass == "frontWall") { rows.Add((pred, e.Point.Y, tech, cy, vol)); pending = -1; }
                        else if (e.SurfaceClass == "tin" || e.SurfaceClass == "floor") { rows.Add((pred, -1, tech, cy, vol)); pending = -1; }
                    }
                }
            }
        }
        Console.WriteLine("AI rally strokes: " + rows.Count + "  (pred = rollout front-wall height for the executed aim; -1 = predicted fail)");
        var ok = rows.Where(r => r.pred > 0 && r.actual > 0).ToList();
        Console.WriteLine("  predicted good, reached front wall: " + ok.Count + "   mean error " + (ok.Count > 0 ? ok.Average(r => r.actual - r.pred) : 0).ToString("0.00")
            + " m, mean |error| " + (ok.Count > 0 ? ok.Average(r => Math.Abs(r.actual - r.pred)) : 0).ToString("0.00") + " m");
        var bad = rows.Where(r => r.pred > 0 && r.actual < 0).ToList();
        Console.WriteLine("  predicted good, went DOWN: " + bad.Count + "  (volleys " + bad.Count(r => r.volley) + ", mean predicted height " + (bad.Count > 0 ? bad.Average(r => r.pred) : 0).ToString("0.00") + ")");
        Console.WriteLine("  predicted fail (noise pushed it): " + rows.Count(r => r.pred < 0));
        foreach (var grp in rows.Where(r => r.pred > 0).GroupBy(r => r.contactY < 0.5 ? "low  (<0.5 m)" : r.contactY < 1.2 ? "mid  (0.5-1.2)" : "high (>1.2 m)"))
        {
            var reached = grp.Where(r => r.actual > 0).ToList();
            Console.WriteLine("  contact " + grp.Key + ": " + grp.Count() + " strokes, down " + grp.Count(r => r.actual < 0)
                + ", mean err " + (reached.Count > 0 ? reached.Average(r => r.actual - r.pred) : 0).ToString("0.00"));
        }
    }

    /// <summary>
    /// For every Not up: was the ball reachable? Records the failing player's distance to the ball's
    /// path and the time they had, from the moment the ball came off the front wall.
    /// </summary>
    static void NotUpReport(int games)
    {
        int total = 0, easy = 0, hard = 0, impossible = 0, neverPressed = 0, whiffed = 0;
        var req = new List<double>();
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            var m = new MatchSimulation(0);
            m.Bots[0] = Brain(P0, D0, (uint)(1000 + g * 2));
            m.Bots[1] = Brain(P1, D1, (uint)(1001 + g * 2));
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            // Per turn: best (lowest) required speed for the pending striker to reach the ball while playable.
            double best = double.PositiveInfinity, prevBest = double.PositiveInfinity; double turnStart = 0; int turnKey = -1;
            bool swung = false, prevSwung = false;
            var log = new Queue<string>(); int shown = 0;
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < 240L * 1800)
            {
                m.Step(dt);
                var r = m.Referee;
                int key = r.Shots * 4 + r.PendingStriker + 1 + m.History.Count * 1000;
                if (key != turnKey) { prevBest = best; prevSwung = swung; turnKey = key; best = double.PositiveInfinity; turnStart = m.Time; swung = false; }
                int s = r.PendingStriker;
                if (m.Phase == MatchPhase.Rally && s >= 0 && r.ExpectedStriker == s && !m.Ball.Asleep &&
                    m.Ball.Pos.Y > 0.15 && m.Ball.Pos.Y < 1.8)
                {
                    var p = m.Players[s].Player.Pos;
                    double dx = m.Ball.Pos.X - p.X, dz = m.Ball.Pos.Z - p.Z;
                    double gap = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - 0.9);
                    double t = m.Time - turnStart;
                    double need = gap <= 0 ? 0 : gap / Math.Max(0.05, t);
                    if (need < best) best = need;
                }
                if (s >= 0 && m.Players[s].Swing.State != SwingStates.Idle) swung = true;
                if (s >= 0 && m.Bots[s] is OpponentBrain obd && (m.StepCount % 6) == 0)
                {
                    var bp = m.Ball.Pos; var pp = m.Players[s].Player.Pos;
                    log.Enqueue("    t=" + m.Time.ToString("0.000") + " P" + (s + 1) + " at (" + pp.X.ToString("0.0") + "," + pp.Z.ToString("0.0") + ") ball (" + bp.X.ToString("0.0") + "," + bp.Y.ToString("0.0") + "," + bp.Z.ToString("0.0") + ") " + obd.DebugState);
                    while (log.Count > 40) log.Dequeue();
                }
                if (m.Phase == MatchPhase.RallyOver && m.PhaseTime <= dt * 1.5 && m.LastOutcome.Kind == RallyEnd.NotUp)
                {
                    total++;
                    if (double.IsPositiveInfinity(best)) { best = prevBest; swung = prevSwung; }
                    req.Add(best);
                    if (best <= 3.5) easy++; else if (best <= 5.5) hard++; else impossible++;
                    if (!swung) neverPressed++; else whiffed++;
                    if (!swung && shown < 2 && g == 0) { shown++; Console.WriteLine("--- never pressed: " + m.LastOutcome); foreach (var l in log) Console.WriteLine(l); }
                }
            }
        }
        Console.WriteLine("Not up calls: " + total);
        Console.WriteLine("  ball was reachable at <= 3.5 m/s average: " + easy + "   (3.5-5.5 m/s): " + hard + "   (> 5.5 m/s, a winner): " + impossible);
        Console.WriteLine("  failing player never started a swing: " + neverPressed + "   swung and missed / too late: " + whiffed);
    }

    /// <summary>Print every event of the first few rallies of one game.</summary>
    static void TraceRallies(int count)
    {
        PhysicsConfig.ResetToDefaults();
        var m = new MatchSimulation(0);
        m.Bots[0] = Brain(P0, D0, 1000);
        m.Bots[1] = Brain(P1, D1, 1001);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        int shown = 0;
        m.RallyEnded += o => { Console.WriteLine("  => " + o); Console.WriteLine(); shown++; };
        Console.WriteLine("rally 1: server P" + (m.Score.Server + 1) + " box " + m.Score.ServeSide);
        while (shown < count && m.StepCount < 400000)
        {
            var phase = m.Phase;
            m.Step(dt);
            if (phase == MatchPhase.RallyOver && m.Phase == MatchPhase.AwaitToss)
                Console.WriteLine("rally " + (shown + 1) + ": server P" + (m.Score.Server + 1) + " box " + m.Score.ServeSide);
            foreach (var e in m.StepEvents)
            {
                string w = "  t=" + e.Time.ToString("0.000") + " ";
                if (e.Type == SimEventType.RacketContact)
                {
                    var brain = m.Bots[e.PlayerIndex] as OpponentBrain;
                    Console.WriteLine(w + "RACKET P" + (e.PlayerIndex + 1) + " " + e.Technique + " at " + e.Point
                        + " out " + e.OutVel + " head " + e.HeadSpeed.ToString("0.0") + " reach " + e.ReachState
                        + (brain != null ? "  chose " + brain.LastChoice + " predH " + brain.LastPredFrontH.ToString("0.00") + " predV " + brain.LastPredSpeed.ToString("0.0") + " actualV " + e.BallSpeedOut.ToString("0.0") : "")
                        + "  aim(" + m.Players[e.PlayerIndex].Aim.RawX.ToString("0.00") + "," + m.Players[e.PlayerIndex].Aim.RawY.ToString("0.00")
                        + ") body " + (m.Players[e.PlayerIndex].IntentSolver.BodyYaw * 57.3).ToString("0") + " face " + (m.Players[e.PlayerIndex].IntentSolver.FaceYaw * 57.3).ToString("0")
                        + " side " + m.Players[e.PlayerIndex].Swing.Side + " mode " + e.Mode);
                }
                else if (e.Type == SimEventType.Impact)
                    Console.WriteLine(w + e.SurfaceId + " at " + e.Point + " speed " + e.ImpactSpeed.ToString("0.0"));
                else
                    Console.WriteLine(w + e.Type + " P" + (e.PlayerIndex + 1) + " at " + e.Point);
            }
        }
    }
}
