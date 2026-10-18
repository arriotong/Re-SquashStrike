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
//        [--lab-ball]        (the lab's ball instead of the game's double-yellow match ball)
//        [--ballcheck]       (the ball vs the WSF double-yellow spec and PSA pace)
//        [--drive S [--temp C] [--aim-height m]]  (trace one straight drive)
//        [--feel]            (movement, reach and animation-input metrics vs the PSA data targets)
//        [--press-audit]     (a human-style player's Hit presses, followed to what they led to)
//        [--hitself]         (every HitSelf: the stroke, planned vs real contact, body, ball path)
//        [--interference [--track] [--only text]]  (every let / stroke: the obstructor's situation)
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
    static bool MatchBall;

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
        MatchBall = Array.IndexOf(args, "--lab-ball") < 0;      // the game's ball unless asked for the lab's
        if (Array.IndexOf(args, "--trace") >= 0) { TraceRallies(int.Parse(Arg(args, "--trace-rallies") ?? "4")); return 0; }
        if (Array.IndexOf(args, "--calibrate") >= 0) { Calibrate(games); return 0; }
        if (Array.IndexOf(args, "--notup") >= 0) { NotUpReport(games); return 0; }
        if (Array.IndexOf(args, "--hitself") >= 0) { HitSelfReport(games); return 0; }
        if (Array.IndexOf(args, "--interference") >= 0) { InterferenceReport(games); return 0; }
        if (Array.IndexOf(args, "--knockup") >= 0) { MatchBall = Array.IndexOf(args, "--lab-ball") < 0; KnockUpReport(); return 0; }
        if (Arg(args, "--diff") != null)
        {
            // Try difficulty knobs: reaction0,reactionSpan,noise0,noiseSpan,move0[,pace0,paceSpan]
            var k = Array.ConvertAll(Arg(args, "--diff").Split(','), double.Parse);
            AiProfile.DiffReaction0 = k[0]; AiProfile.DiffReactionSpan = k[1]; AiProfile.DiffNoise0 = k[2]; AiProfile.DiffNoiseSpan = k[3]; AiProfile.DiffMove0 = k[4]; if (k.Length > 5) AiProfile.DiffPace0 = k[5]; if (k.Length > 6) AiProfile.DiffPaceSpan = k[6];
        }
        if (Arg(args, "--levels") != null) PressAudit.Levels = Array.ConvertAll(Arg(args, "--levels").Split(','), double.Parse);
        if (Array.IndexOf(args, "--balance") >= 0) { PressAudit.Balance(Arg(args, "--games") != null ? games : 12); return 0; }
        if (Array.IndexOf(args, "--press-audit") >= 0)
        {
            PressAudit.AimHigh = Array.IndexOf(args, "--aim-high") >= 0;
            PressAudit.Run(Arg(args, "--games") != null ? games : 12, Arg(args, "--p1") ?? "retriever", double.Parse(Arg(args, "--d1") ?? "0.3"), Array.IndexOf(args, "--lab-ball") < 0);
            return 0;
        }
        if (Array.IndexOf(args, "--feel") >= 0) { MatchBall = Array.IndexOf(args, "--lab-ball") < 0; FeelReport(games); return 0; }
        if (Array.IndexOf(args, "--drive") >= 0) { DriveTrace(double.Parse(Arg(args, "--drive")), double.Parse(Arg(args, "--temp") ?? "45"), double.Parse(Arg(args, "--aim-height") ?? "0.9"), Array.IndexOf(args, "--lab-ball") < 0); return 0; }
        if (Array.IndexOf(args, "--ballcheck") >= 0) { BallCheck(Array.IndexOf(args, "--lab-ball") < 0); return 0; }

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
                if (o.Kind == RallyEnd.Down) k = "Down - " + (o.Reason.StartsWith("Down - ") ? o.Reason.Substring(7) : o.Reason.Replace("P1", "P").Replace("P2", "P"));
                if (o.Kind == RallyEnd.HitSelf)
                    k += o.Reason.StartsWith("Ball hit the striker") ? " - own shot on its way to the front wall" : " - incoming ball before playing it";
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

    /// <summary>
    /// M6 feel bench: how the athletes move, reach and present, against real squash data
    /// (RE_SQUASHSTRIKE_M6_CONTROLS_PLAN.md section 2). Every M6 step must move these toward target.
    /// </summary>
    static void FeelReport(int games)
    {
        var speed = new List<double>(); var gapShots = new List<double>(); var contactDist = new List<double>(); var assist = new List<double>();
        var facingRate = new List<double>(); var idleHead = new List<double>(); var accel = new List<double>();
        double rallyTime = 0, idleTime = 0; int reachFlipsIdle = 0, sideFlips = 0, shots = 0, rallies = 0;
        int good = 0, early = 0, late = 0;
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            if (MatchBall) PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(0);
            m.Bots[0] = Brain(P0, D0, (uint)(100 + g)); m.Bots[1] = Brain(P1, D1, (uint)(200 + g));
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var lastReach = new string[2]; var lastSide = new int[2]; var lastFacing = new double[2]; var lastHead = new V3[2]; var lastVel = new V3[2];
            for (int i = 0; i < 2; i++)
            {
                lastReach[i] = m.Players[i].Player.ReachState; lastSide[i] = m.Players[i].Player.SwingSide; lastFacing[i] = m.Players[i].Player.Facing;
                lastHead[i] = m.Players[i].Racket.HeadPos.Clone(); lastVel[i] = m.Players[i].Player.Vel.Clone();
            }
            double lastShotTime = -1; int lastRallies = 0;
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.Time < 1800)
            {
                m.Step(dt);
                bool live = m.Phase == MatchPhase.Rally && m.Referee.Outcome == null;
                if (m.History.Count != lastRallies) { lastRallies = m.History.Count; lastShotTime = -1; rallies++; }
                foreach (var e in m.StepEvents)
                    if (e.Type == SimEventType.RacketContact && m.Phase == MatchPhase.Rally)
                    {
                        shots++;
                        double off = e.SwingPhaseAtContact - 0.5;
                        if (e.SwingPhaseAtContact >= 0) { if (Math.Abs(off) <= 0.07) good++; else if (off > 0) early++; else late++; }
                        if (lastShotTime >= 0 && m.Referee.Shots > 1) gapShots.Add(m.Time - lastShotTime);
                        lastShotTime = m.Time;
                        if (m.Referee.Shots > 1)
                        {
                            var p = m.Players[e.PlayerIndex].Player.Pos;
                            contactDist.Add(Math.Sqrt((e.Point.X - p.X) * (e.Point.X - p.X) + (e.Point.Z - p.Z) * (e.Point.Z - p.Z)));
                            assist.Add(e.AssistApplied);
                        }
                    }
                for (int i = 0; i < 2; i++)
                {
                    var c = m.Players[i];
                    if (live)
                    {
                        rallyTime += dt;
                        speed.Add(Math.Sqrt(c.Player.Vel.X * c.Player.Vel.X + c.Player.Vel.Z * c.Player.Vel.Z));
                        double ax = (c.Player.Vel.X - lastVel[i].X) / dt, az = (c.Player.Vel.Z - lastVel[i].Z) / dt;
                        accel.Add(Math.Sqrt(ax * ax + az * az));
                        double df = c.Player.Facing - lastFacing[i];
                        while (df > Math.PI) df -= 2 * Math.PI;
                        while (df < -Math.PI) df += 2 * Math.PI;
                        facingRate.Add(Math.Abs(df) / dt * 57.29578);
                        if (c.Swing.State == SwingStates.Idle)
                        {
                            idleTime += dt;
                            if (c.Player.ReachState != lastReach[i]) reachFlipsIdle++;
                            var h = c.Racket.HeadPos;
                            double hx = h.X - lastHead[i].X, hy = h.Y - lastHead[i].Y, hz = h.Z - lastHead[i].Z;
                            idleHead.Add(Math.Sqrt(hx * hx + hy * hy + hz * hz) / dt);
                        }
                        if (c.Player.SwingSide != lastSide[i]) sideFlips++;
                    }
                    lastReach[i] = c.Player.ReachState; lastSide[i] = c.Player.SwingSide; lastFacing[i] = c.Player.Facing;
                    lastHead[i].CopyFrom(c.Racket.HeadPos); lastVel[i].CopyFrom(c.Player.Vel);
                }
            }
        }
        Func<List<double>, double, double> Q = (l, q) => { if (l.Count == 0) return 0; var a = l.OrderBy(x => x).ToList(); return a[(int)Math.Min(a.Count - 1, q * a.Count)]; };
        Func<string, string, string, string> Row = (name, value, target) => "  " + name.PadRight(40) + value.PadRight(30) + target;
        double rallyTimePerPlayer = rallyTime / 2;
        Console.WriteLine("FEEL  " + Describe(P0, D0) + " vs " + Describe(P1, D1) + (MatchBall ? "  (game profile)" : "  (lab)") + "   " + games + " games, " + rallies + " rallies");
        Console.WriteLine(Row("measure", "now", "target (real squash)"));
        Console.WriteLine(Row("movement speed mean", speed.Average().ToString("0.00") + " m/s", "1.88 m/s (PSA)"));
        Console.WriteLine(Row("time below 1 m/s", (100.0 * speed.Count(x => x < 1) / speed.Count).ToString("0") + " %", "31 %"));
        Console.WriteLine(Row("speed 95th / max", Q(speed, .95).ToString("0.0") + " / " + Q(speed, 1).ToString("0.0") + " m/s", "~4.8 / ~5-6 m/s"));
        Console.WriteLine(Row("acceleration 95th", Q(accel, .95).ToString("0") + " m/s2", "no published squash figure (lab kept)"));
        Console.WriteLine(Row("time between shots median", Q(gapShots, .5).ToString("0.00") + " s", "1.1-1.6 s"));
        Console.WriteLine(Row("shots per rally", (shots / (double)Math.Max(1, rallies)).ToString("0.0"), "~10-13 (PSA)"));
        Console.WriteLine(Row("contact dist. from body med/95/max", Q(contactDist, .5).ToString("0.00") + " / " + Q(contactDist, .95).ToString("0.00") + " / " + Q(contactDist, 1).ToString("0.00") + " m", "max <= ~1.9 m with visible lunge"));
        Console.WriteLine(Row("contact assist pull 90th / max", Q(assist, .9).ToString("0.00") + " / " + Q(assist, 1).ToString("0.00") + " m", "max ~0.12 m"));
        Console.WriteLine(Row("reach-state flicker (not swinging)", (reachFlipsIdle / Math.Max(1, idleTime)).ToString("0.0") + " /s", "pose keyed on swings"));
        Console.WriteLine(Row("forehand/backhand flips", (sideFlips / Math.Max(1, rallyTimePerPlayer)).ToString("0.0") + " /s per player", "drawn as switches"));
        Console.WriteLine(Row("feet facing turn rate 50th / 95th", Q(facingRate, .5).ToString("0") + " / " + Q(facingRate, .95).ToString("0") + " deg/s", "sim; drawn <= 540 (AthleteTests)"));
        int timed = Math.Max(1, good + early + late);
        Console.WriteLine(Row("stroke timing good / early / late", (100 * good / timed) + " / " + (100 * early / timed) + " / " + (100 * late / timed) + " %", "readable feedback"));
        Console.WriteLine(Row("idle racket head 95th / max", Q(idleHead, .95).ToString("0.0") + " / " + Q(idleHead, 1).ToString("0") + " m/s", "drawn racket smoothed"));
    }

    /// <summary>One straight drive from the back at S m/s, aimed aimH up the front wall: every impact.</summary>
    static void DriveTrace(double S, double T, double aimH, bool matchBall)
    {
        PhysicsConfig.ResetToDefaults();
        if (matchBall) PhysicsConfig.UseGameProfile();
        var surfaces = CourtSurfaces.Create(); var evs = new List<SimEvent>();
        double z0 = 7.5, y0 = 0.6, t = z0 / S, vy = (aimH - y0) / t + 0.5 * 9.81 * t;
        var b = new BallState(); b.Reset(new V3(2.2, y0, z0), new V3(0, vy, -Math.Sqrt(S * S - vy * vy)), null, T); BallPhysics.Wake(b);
        int floors = 0;
        for (int i = 0; i < 240 * 6 && floors < 3; i++)
        {
            evs.Clear(); BallPhysics.Step(b, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
            foreach (var e in evs) { if (e.SurfaceClass == "floor") floors++; Console.WriteLine((i / 240.0).ToString("0.000") + "  " + e.SurfaceId.PadRight(14) + " at " + e.Point + "  in " + e.ImpactSpeed.ToString("0.0") + " (n " + e.NormalSpeed.ToString("0.0") + ")  out " + V3.Length(b.Vel).ToString("0.0") + "  e " + e.Restitution.ToString("0.00")); }
        }
    }

    /// <summary>The ball against the double-yellow spec and PSA pace: drop rebound, drives, match temperature.</summary>
    static void BallCheck(bool matchBall)
    {
        PhysicsConfig.ResetToDefaults();
        if (matchBall) PhysicsConfig.UseGameProfile();
        var surfaces = CourtSurfaces.Create();
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        var evs = new List<SimEvent>();
        Console.WriteLine(matchBall ? "MATCH BALL" : "LAB BALL");
        Console.WriteLine("Drop from 100 in (2.54 m) onto the floor - WSF double yellow: 12-14% at 23 C, 26-33% at 45 C");
        foreach (double T in new[] { 16.0, 23.0, 30.0, 37.0, 45.0, 48.0 })
        {
            var b = new BallState(); b.Reset(new V3(0, 2.54, 5), new V3(0, 0, 0), null, T); BallPhysics.Wake(b);
            double peak = 0; bool bounced = false;
            for (int i = 0; i < 240 * 4; i++)
            {
                evs.Clear(); BallPhysics.Step(b, surfaces, dt, evs); b.Temperature = T;
                if (evs.Count > 0) { if (bounced) break; bounced = true; }
                if (bounced) peak = Math.Max(peak, b.Pos.Y - 0.0203);
            }
            Console.WriteLine("  " + T.ToString("0").PadLeft(3) + " C   rebound " + (100 * peak / (2.54 - 0.0203)).ToString("0.0").PadLeft(5) + " %");
        }
        Console.WriteLine("Straight drive struck at 0.6 m from z = 7.5, 1.0 m off the side wall, aimed 2.0 m up the front wall");
        Console.WriteLine("  (PSA: ~40-50 m/s off the racket; a good length dies near the back wall)");
        foreach (double T in new[] { 23.0, 45.0 })
            foreach (double S in new[] { 20.0, 30.0, 45.0 })
            {
                double x = 2.2, y0 = 0.6, z0 = 7.5, yAim = 2.0;
                // Aim: straight at (x, yAim, 0), lofted for gravity over the flight time.
                double tFly = z0 / S;
                double vy = (yAim - y0) / tFly + 0.5 * 9.81 * tFly;
                double vz = -Math.Sqrt(Math.Max(1, S * S - vy * vy));
                var b = new BallState(); b.Reset(new V3(x, y0, z0), new V3(0, vy, vz), null, T); BallPhysics.Wake(b);
                string report = ""; int floors = 0; double offFront = 0, frontH = 0;
                for (int i = 0; i < 240 * 6 && floors < 2; i++)
                {
                    evs.Clear(); BallPhysics.Step(b, surfaces, dt, evs); b.Temperature = T;
                    foreach (var e in evs)
                    {
                        if (e.SurfaceClass == "frontWall") { offFront = V3.Length(b.Vel); frontH = e.Point.Y; }
                        if (e.SurfaceClass == "floor") { floors++; report += "  floor" + floors + " z " + e.Point.Z.ToString("0.0"); }
                        if (e.SurfaceClass == "backWall") report += "  BACK WALL (" + e.ImpactSpeed.ToString("0.0") + " m/s, " + e.Point.Y.ToString("0.00") + " m up)";
                    }
                }
                Console.WriteLine("  " + T.ToString("0") + " C  " + S.ToString("0").PadLeft(2) + " m/s -> front wall at " + frontH.ToString("0.0") + " m, off it at " + offFront.ToString("0.0") + " m/s (" + (offFront / S).ToString("0.00") + ")" + report);
            }
        // In play: racket pace and the ball's temperature over a best of 5.
        PhysicsConfig.ResetToDefaults();
        if (matchBall) PhysicsConfig.UseGameProfile();
        PhysicsConfig.Current.Match.GamesToWin = 3; PhysicsConfig.Current.Match.WarmUpEnabled = true;
        var m = new MatchSimulation(0);
        m.Bots[0] = Brain("grinder", 0.85, 3); m.Bots[1] = Brain("retriever", 0.85, 4);
        var outs = new List<double>(); var temps = new List<double>(); var offWall = new List<double>();
        double sampleAt = 0;
        while (m.Phase != MatchPhase.MatchOver && m.Time < 3 * 3600)
        {
            m.Step(dt);
            foreach (var e in m.StepEvents)
            {
                if (e.Type == SimEventType.RacketContact && m.Phase == MatchPhase.Rally) outs.Add(e.BallSpeedOut);
                if (e.Type == SimEventType.Impact && e.SurfaceClass == "frontWall" && m.Phase == MatchPhase.Rally) offWall.Add(V3.Length(m.Ball.Vel));
            }
            if (m.Phase == MatchPhase.Rally && m.Time >= sampleAt) { temps.Add(m.Ball.Temperature); sampleAt = m.Time + 1; }
        }
        outs.Sort(); offWall.Sort(); temps.Sort();
        Func<List<double>, double, double> Q = (l, q) => l.Count == 0 ? 0 : l[(int)Math.Min(l.Count - 1, q * l.Count)];
        Console.WriteLine("In play (best of 5, hard AIs, " + outs.Count + " strokes):");
        Console.WriteLine("  off the racket  median " + Q(outs, 0.5).ToString("0.0") + "  90th " + Q(outs, 0.9).ToString("0.0") + "  max " + Q(outs, 1).ToString("0.0") + " m/s");
        Console.WriteLine("  off front wall  median " + Q(offWall, 0.5).ToString("0.0") + "  90th " + Q(offWall, 0.9).ToString("0.0") + " m/s");
        Console.WriteLine("  ball in rallies median " + Q(temps, 0.5).ToString("0.0") + "  10th " + Q(temps, 0.1).ToString("0.0") + "  90th " + Q(temps, 0.9).ToString("0.0") + " C");
    }

    /// <summary>One knock-up half: every feed, stroke and dead ball, with times.</summary>
    static void KnockUpReport()
    {
        PhysicsConfig.ResetToDefaults();
        if (MatchBall) PhysicsConfig.UseGameProfile();
        PhysicsConfig.Current.Match.WarmUpEnabled = true;
        var m = new MatchSimulation(0);
        m.Bots[0] = Brain(P0, D0, 7); m.Bots[1] = Brain(P1, D1, 8);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        int shots = 0; bool asleep = false;
        while (m.Phase == MatchPhase.WarmUp)
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
        if (MatchBall) PhysicsConfig.UseGameProfile();
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
            PhysicsConfig.ResetToDefaults(); if (MatchBall) PhysicsConfig.UseGameProfile();
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
            PhysicsConfig.ResetToDefaults(); if (MatchBall) PhysicsConfig.UseGameProfile();
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
                    if (shown < 3 && g == 0) { shown++; Console.WriteLine("--- " + (swung ? "swung: " : "never pressed: ") + m.LastOutcome); foreach (var l in log) Console.WriteLine(l); }
                }
            }
        }
        Console.WriteLine("Not up calls: " + total);
        Console.WriteLine("  ball was reachable at <= 3.5 m/s average: " + easy + "   (3.5-5.5 m/s): " + hard + "   (> 5.5 m/s, a winner): " + impossible);
        Console.WriteLine("  failing player never started a swing: " + neverPressed + "   swung and missed / too late: " + whiffed);
    }

    /// <summary>
    /// For every HitSelf (and stroke/let ending): the stroke that led to it - where it was struck from,
    /// what the ball touched on the way, how long after contact, and what the striker was doing.
    /// </summary>
    static void HitSelfReport(int games)
    {
        int total = 0;
        var walls = new Dictionary<string, int>();
        var dts = new List<double>();
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            if (MatchBall) PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(0);
            m.Bots[0] = Brain(P0, D0, (uint)(1000 + g * 2));
            m.Bots[1] = Brain(P1, D1, (uint)(1001 + g * 2));
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            double contactT = 0; V3 contactAt = new V3(), strikerAt = new V3(); int hitter = -1; string path = ""; string tech = "", choice = "";
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < 240L * 1800)
            {
                m.Step(dt);
                foreach (var e in m.StepEvents)
                {
                    if (e.Type == SimEventType.RacketContact)
                    {
                        contactT = m.Time; contactAt.CopyFrom(e.Point); hitter = e.PlayerIndex; path = ""; tech = e.Technique;
                        strikerAt.CopyFrom(m.Players[hitter].Player.Pos);
                        choice = m.Bots[hitter] is OpponentBrain ob ? ob.LastChoice + " planned contact " + ob.LastPredPoint + " body " + ob.LastPredBody : "";
                    }
                    else if (e.Type == SimEventType.Impact && !e.Resting) path += " " + e.SurfaceClass;
                }
                if (m.Phase == MatchPhase.RallyOver && m.PhaseTime <= dt * 1.5 && m.LastOutcome.Kind == RallyEnd.HitSelf && hitter >= 0)
                {
                    total++;
                    var p = m.Players[hitter].Player;
                    string key = path.Trim().Length == 0 ? "(direct)" : path.Trim();
                    walls[key] = walls.TryGetValue(key, out int n) ? n + 1 : 1;
                    dts.Add(m.Time - contactT);
                    if (total <= 12)
                        Console.WriteLine("  +" + (m.Time - contactT).ToString("0.00") + " s  " + tech + " " + choice + " from " + contactAt + " striker was at " + strikerAt
                            + " now " + p.Pos + " vel (" + p.Vel.X.ToString("0.0") + "," + p.Vel.Z.ToString("0.0") + ")  path:" + key);
                }
            }
        }
        Console.WriteLine("HitSelf: " + total + "   median time after contact " + Median(dts).ToString("0.00") + " s");
        foreach (var kv in walls.OrderByDescending(k => k.Value)) Console.WriteLine("  via " + kv.Key.PadRight(40) + kv.Value);
    }

    /// <summary>
    /// For every let / stroke on an appeal: the obstructor's situation - how long since their own stroke,
    /// where they struck from, where they stand vs the striker's hitting point, how fast they were clearing.
    /// </summary>
    static void InterferenceReport(int games)
    {
        var rows = new List<(string reason, double since, double dist, double fromContact, double speed, double tToBall)>();
        int shown = 0;
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            if (MatchBall) PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(0);
            m.Bots[0] = Brain(P0, D0, (uint)(1000 + g * 2));
            m.Bots[1] = Brain(P1, D1, (uint)(1001 + g * 2));
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            double contactT = 0; var contactBody = new V3(); var contactPt = new V3(); int hitter = -1; int seen = 0;
            var track = new Queue<string>();
            double obsAt = -1; var obsO = new V3(); var obsS = new V3(); double obsTx = 0, obsTz = 0; string obsKind = "";
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < 240L * 1800)
            {
                m.Step(dt);
                foreach (var e in m.StepEvents)
                    if (e.Type == SimEventType.RacketContact)
                    { contactT = m.Time; hitter = e.PlayerIndex; contactBody.CopyFrom(m.Players[hitter].Player.Pos); contactPt.CopyFrom(e.Point); obsAt = -1; track.Clear(); }
                var it = m.Interference;
                if (m.Phase == MatchPhase.Rally && it.Striker >= 0 && (m.StepCount % 12) == 0)
                {
                    var op = m.Players[1 - it.Striker].Player; var sp = m.Players[it.Striker].Player;
                    track.Enqueue("    +" + (m.Time - contactT).ToString("0.00") + " obstructor " + op.Pos + " v(" + op.Vel.X.ToString("0.0") + "," + op.Vel.Z.ToString("0.0") + ") swing " + m.Players[1 - it.Striker].Swing.State
                        + "  striker " + sp.Pos + "  target (" + it.TargetX.ToString("0.0") + "," + it.TargetZ.ToString("0.0") + ") " + it.Current);
                    while (track.Count > 14) track.Dequeue();
                }
                if (m.Phase == MatchPhase.Rally && it.Current != ObstructionKind.None && obsAt < 0 && it.Striker >= 0)
                {
                    obsAt = m.Time; obsO.CopyFrom(m.Players[1 - it.Striker].Player.Pos); obsS.CopyFrom(m.Players[it.Striker].Player.Pos);
                    obsTx = it.TargetX; obsTz = it.TargetZ; obsKind = it.Current.ToString();
                }
                if (m.History.Count != seen)
                {
                    seen = m.History.Count;
                    var o = m.LastOutcome;
                    string only = Arg(Environment.GetCommandLineArgs(), "--only");
                    if ((o.Kind == RallyEnd.Let || o.Kind == RallyEnd.Stroke) && obsAt >= 0 && hitter >= 0 && (only == null || o.Reason.Contains(only)))
                    {
                        var ob = m.Players[hitter].Player;
                        double dist = Math.Sqrt((obsO.X - obsTx) * (obsO.X - obsTx) + (obsO.Z - obsTz) * (obsO.Z - obsTz));
                        double moved = Math.Sqrt((obsO.X - contactBody.X) * (obsO.X - contactBody.X) + (obsO.Z - contactBody.Z) * (obsO.Z - contactBody.Z));
                        rows.Add((o.Reason, obsAt - contactT, dist, moved, 0, 0));
                        if (shown++ < 15)
                            Console.WriteLine("  " + o.Reason.PadRight(48) + " first " + obsKind.PadRight(7) + " +" + (obsAt - contactT).ToString("0.00") + " s after own stroke from " + contactPt
                                + "  obstructor " + obsO + " (moved " + moved.ToString("0.0") + ")  striker " + obsS + "  hitting pt (" + obsTx.ToString("0.0") + "," + obsTz.ToString("0.0") + ")");
                        if (shown <= 4 && Array.IndexOf(Environment.GetCommandLineArgs(), "--track") >= 0) foreach (var l in track) Console.WriteLine(l);
                    }
                }
            }
        }
        Console.WriteLine("lets/strokes: " + rows.Count + "   first obstruction median " + Median(rows.Select(r => r.since)).ToString("0.00")
            + " s after the obstructor's stroke; obstructor had moved median " + Median(rows.Select(r => r.fromContact)).ToString("0.00") + " m from where they struck");
        foreach (var grp in rows.GroupBy(r => r.since < 0.5 ? "< 0.5 s" : r.since < 1.0 ? "0.5-1 s" : ">= 1 s"))
            Console.WriteLine("  first obstructed " + grp.Key.PadRight(8) + grp.Count());
    }

    /// <summary>Print every event of the first few rallies of one game.</summary>
    static void TraceRallies(int count)
    {
        PhysicsConfig.ResetToDefaults(); if (MatchBall) PhysicsConfig.UseGameProfile();
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
