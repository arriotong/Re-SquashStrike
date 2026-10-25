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
        int w = 0, t = 0;
        foreach (var o in m.History) { if (o.Winner < 0) continue; t++; if (o.Winner == 0) { w++; string k = o.Kind + " " + o.Reason; Ends[k] = (Ends.TryGetValue(k, out int c) ? c : 0) + 1; } }
        PhysicsConfig.ResetToDefaults();
        return (w, t, m.Score.MatchWinner == 0 ? 1 : 0);
    }
}
