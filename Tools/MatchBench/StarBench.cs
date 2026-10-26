// ===========================================================
// Tools/MatchBench/StarBench.cs
// P6: the human-style model against the club players and the stars, by
// rating - rallies won and games won - to calibrate Stars.Difficulty.
//
//   --stars [--games N] [--only star:s01]
//
// Targets: rating 60 ~62% of rallies, 70 ~52%, 80 ~42%, 90 ~30%, 95 ~18%,
// the No. 1 (99) under 10% and no games.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;
using SquashSim.Career;
using SquashSim.Replays;

static class StarBench
{
    public static readonly Dictionary<string, int> Ends = new Dictionary<string, int>();
    public static readonly Dictionary<string, int> Hits = new Dictionary<string, int>();
    /// <summary>Batch 7 phase 1: every rally's stats (stamina at its end, its length) for the stamina report.</summary>
    public static readonly List<RallyStats> Rallies = new List<RallyStats>();
    public static readonly double[] Dist = new double[2], Fast = new double[2], RallyTime = new double[2];
    public static bool NoSig = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--no-sig") >= 0;
    public static bool Geo = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--geo") >= 0;
    public static bool NoAttr = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--no-attr") >= 0;

    public static void Run(int games, string only)
    {
        var ids = new List<string>();
        foreach (int r in Stars.ClubRatings) ids.Add(Stars.ClubId("retriever", r));
        foreach (var n in new[] { 50, 40, 30, 20, 10, 5, 3, 2, 1 }) ids.Add("star:s" + n.ToString("00"));
        Console.WriteLine("STARS  the human-style model, " + games + " games to 11 each (PAR)");
        Console.WriteLine("  opponent                      rating   diff    rallies won   games won");
        foreach (var id in ids)
        {
            if (only != null && id != only) continue;
            var d = Stars.Describe(id);
            int won = 0, total = 0, gamesWon = 0;
            for (int g = 0; g < games; g++)
            {
                var (rw, rt, gw) = Play(id, (uint)(4000 + g * 131));
                won += rw; total += rt; gamesWon += gw;
            }
            Console.WriteLine("  " + (d.name + " (" + Roster.StyleName(d.style) + ")").PadRight(30) + d.rating.ToString().PadLeft(6) + "   " + Stars.DifficultyFor(id, 0).ToString("0.00") +
                              "   " + (100.0 * won / Math.Max(1, total)).ToString("0").PadLeft(6) + "%      " + gamesWon + " / " + games);
            if (Rallies.Count > 0 && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--stamina") >= 0)
            {
                double Avg(System.Func<RallyStats, double> f, System.Func<RallyStats, bool> where) { double sum = 0; int n = 0; foreach (var r in Rallies) if (where(r)) { sum += f(r); n++; } return n > 0 ? sum / n : double.NaN; }
                double low = 1; int tired = 0; foreach (var r in Rallies) { low = Math.Min(low, Math.Min(r.Stamina0, r.Stamina1)); if (Math.Min(r.Stamina0, r.Stamina1) < 0.6) tired++; }
                Console.WriteLine("      stamina at rally end: you " + Avg(r => r.Stamina0, r => true).ToString("0.00") + ", them " + Avg(r => r.Stamina1, r => true).ToString("0.00") +
                    ", lowest " + low.ToString("0.00") + ", after 10+ s rallies " + Avg(r => Math.Min(r.Stamina0, r.Stamina1), r => r.Seconds >= 10).ToString("0.00") +
                    ", after 20+ s " + Avg(r => Math.Min(r.Stamina0, r.Stamina1), r => r.Seconds >= 20).ToString("0.00") + ", rallies ending tired (<0.6) " + (100.0 * tired / Rallies.Count).ToString("0") + "%" +
                    ", mean rally " + Avg(r => r.Seconds, r => true).ToString("0.0") + " s");
                Console.WriteLine("      in rallies: you ran " + (Dist[0] / Math.Max(1, RallyTime[0])).ToString("0.00") + " m/s (flat out " + (100 * Fast[0] / Math.Max(1, RallyTime[0])).ToString("0") + "% of the time), them " +
                    (Dist[1] / Math.Max(1, RallyTime[1])).ToString("0.00") + " m/s (" + (100 * Fast[1] / Math.Max(1, RallyTime[1])).ToString("0") + "%)");
                for (int i = 0; i < 2; i++) { Dist[i] = 0; Fast[i] = 0; RallyTime[i] = 0; }
                Rallies.Clear();
            }
            if (only != null) foreach (var kv in Ends) Console.WriteLine("      the model won by: " + kv.Key + "  x" + kv.Value);
            if (only != null) foreach (var kv in Hits) Console.WriteLine("      " + kv.Key + "  x" + kv.Value);
        }
    }

    /// <summary>One game to 11: rallies the human-style model won, rallies, games won (0/1).</summary>
    public static (int, int, int) Play(string id, uint seed)
    {
        var s = new ReplaySetup { Opponent = id, Difficulty = 0.15f, GamesToWin = 1, Scoring = "par", BotSeed = seed, Lets = true };
        ReplayMatch.DressStar(s);
        if (NoAttr) s.Attr1 = null;
        if (NoSig) s.Mods1 = null;
        ReplayMatch.Configure(s);
        var m = ReplayMatch.Build(s);
        m.Bots[0] = new HumanStyle(seed * 2654435761u + 3) { Arcade = true };
        m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
        Program.ArcadeAi(m);
        double dt = PhysicsConfig.Current.Sim.FixedDt;
        double lastT = 0; string lastTech = "", lastGeo = ""; bool lastAssist = false; double lastSpeed = 0;
        int hist = 0;
        while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
        {
            m.Step(dt);
            if (m.Phase == MatchPhase.Rally) for (int i = 0; i < 2; i++) { var pl = m.Players[i].Player; Dist[i] += pl.Speed * dt; if (pl.Speed > 0.85 * PhysicsConfig.Current.Player.MaxSpeed) Fast[i] += dt; RallyTime[i] += dt; }
            if (Geo && m.History.Count != hist)
            {
                hist = m.History.Count;
                var oc = m.History[hist - 1];
                if (oc.Winner == 0 && oc.Kind == RallyEnd.Stroke)
                {
                    var a = m.Players[1].Player; var h = m.Players[0].Player; var it = m.Interference;
                    Console.WriteLine("    STROKE " + oc.Reason + " | ai " + a.Pos.X.ToString("0.00") + "," + a.Pos.Z.ToString("0.00") + " v " + a.Vel.X.ToString("0.0") + "," + a.Vel.Z.ToString("0.0") +
                        " | model " + h.Pos.X.ToString("0.00") + "," + h.Pos.Z.ToString("0.00") + " | target " + it.TargetX.ToString("0.00") + "," + it.TargetZ.ToString("0.00") +
                        " | ball " + m.Ball.Pos.X.ToString("0.00") + "," + m.Ball.Pos.Y.ToString("0.00") + "," + m.Ball.Pos.Z.ToString("0.00") + " | ai shot " + lastGeo + " " + ((m.Time - lastT) * 1000).ToString("0") + "ms ago");
                }
            }
            foreach (var e in m.StepEvents)
            {
                if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 1) { lastT = m.Time; lastTech = e.Technique; lastSpeed = e.BallSpeedOut;
                    var pp = m.Players[1].Player.Pos; lastGeo = "body " + pp.X.ToString("0.00") + "," + pp.Z.ToString("0.00") + " ball " + m.Ball.Pos.X.ToString("0.00") + "," + m.Ball.Pos.Z.ToString("0.00") + " v " + m.Ball.Vel.X.ToString("0.0") + "," + m.Ball.Vel.Z.ToString("0.0") + " aim " + (m.Bots[1] is OpponentBrain ob ? ob.ArcadeShot : ""); }
                if (e.Type == SimEventType.BodyHit && e.PlayerIndex == 1 && m.Referee.LastHitter == 1)
                {
                    string k = "hit by own " + lastTech + " after " + ((m.Time - lastT) * 1000).ToString("0") + " ms (" + (m.Time - lastT < 0.1 ? "<100" : "later") + ")";
                    string kk = "own " + lastTech + (m.Time - lastT < 0.1 ? " <100ms" : " >100ms");
                    Hits[kk] = (Hits.TryGetValue(kk, out int c2) ? c2 : 0) + 1;
                    if (Geo) { var pp = m.Players[1].Player.Pos; Console.WriteLine("    " + kk + " " + ((m.Time - lastT) * 1000).ToString("0") + "ms | at contact " + lastGeo + " | at hit body " + pp.X.ToString("0.00") + "," + pp.Z.ToString("0.00") + " ball " + m.Ball.Pos.X.ToString("0.00") + "," + m.Ball.Pos.Z.ToString("0.00")); }
                }
            }
        }
        Rallies.AddRange(m.Stats);
        int w = 0, t = 0;
        foreach (var o in m.History) { if (o.Winner < 0) continue; t++; if (o.Winner == 0) { w++; string k = o.Kind + " " + o.Reason; Ends[k] = (Ends.TryGetValue(k, out int c) ? c : 0) + 1; } }
        PhysicsConfig.ResetToDefaults();
        return (w, t, m.Score.MatchWinner == 0 ? 1 : 0);
    }
}
