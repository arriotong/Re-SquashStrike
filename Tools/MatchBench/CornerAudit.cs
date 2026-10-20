// ===========================================================
// Tools/MatchBench/CornerAudit.cs
// Why the AI lets the ball bounce twice (playtest 2026-10-20: "I hit it
// into the back corners and the AI just stood there"). Plays the
// human-style player against an AI and follows every AI turn that ends
// "Not up": where the ball died, how much time the AI had, how far it
// ran, how close it got, and whether it ever had a contact predicted or
// pressed Hit.
//
//   dotnet run --project Tools/MatchBench -c Release -- --corner-audit [--games N] [--p1 retriever] [--d1 0.45]
// ===========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SquashSim;

static class CornerAudit
{
    public static void Run(int games, string opponent, double difficulty)
    {
        int rallies = 0, aiTurns = 0, notUp = 0;
        var where = new Dictionary<string, int>();
        var why = new Dictionary<string, int>();
        var samples = new List<string>();
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(0);
            var h = new HumanStyle((uint)(500 + g)) { Arcade = true };
            var ai = new OpponentBrain(AiProfile.ByName(opponent).WithDifficulty(difficulty), (uint)(40 + g));
            m.Bots[0] = h; m.Bots[1] = ai;
            m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true; m.Players[0].Arcade.ShotLines = true;
            Program.ArcadeAi(m);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            int hist = 0, lastShots = -1;
            bool turn = false; double turnStart = 0, minD = 99, ran = 0, startD = 0; int predSteps = 0, readSteps = 0, presses = 0;
            V3 last = null; string state = "", stateMid = "";
            var a = m.Players[1];
            while (m.Phase != MatchPhase.GameOver && m.Time < 1800)
            {
                int p0 = ai.RallyPresses;
                m.Step(dt);
                presses += ai.RallyPresses - p0;
                bool mine = m.Phase == MatchPhase.Rally && m.GoingForBall(1) && m.TurnShots > 0;
                if (mine && (!turn || m.TurnShots != lastShots))
                {
                    turn = true; lastShots = m.TurnShots; turnStart = m.Time; minD = 99; ran = 0; predSteps = 0; readSteps = 0; presses = 0; aiTurns++;
                    last = a.Player.Pos.Clone(); stateMid = "";
                    startD = Dist(a.Player.Pos, m.Ball.Pos);
                }
                if (mine)
                {
                    double d = Dist(a.Player.Pos, m.Ball.Pos);
                    if (d < minD) minD = d;
                    ran += Dist(a.Player.Pos, last); last = a.Player.Pos.Clone();
                    if (a.Prediction.Valid) predSteps++;
                    state = ai.DebugState;
                    if (stateMid == "" && m.Time - turnStart > 0.5) stateMid = ai.DebugState;
                }
                if (!mine) turn = false;
                if (m.History.Count != hist)
                {
                    hist = m.History.Count; rallies++;
                    var o = m.LastOutcome;
                    if (o.Loser == 1 && (o.Reason ?? "").StartsWith("Not up") && m.Referee.Shots > 1)
                    {
                        notUp++;
                        var b = m.Ball.LastImpactPoint;
                        string w = (b.Z > 7.5 ? "back" : b.Z > 4.5 ? "middle" : "front") + (Math.Abs(b.X) > 2.0 ? " corner/wall" : " centre");
                        Add(where, w);
                        double had = m.Time - turnStart;
                        string k;
                        if (predSteps == 0 && presses == 0) k = minD < 1.2 ? "near it, never a contact predicted" : (ran < 0.8 ? "hardly moved, no contact predicted" : "ran, never got in reach");
                        else if (presses == 0) k = "contact predicted, never pressed";
                        else k = "pressed, did not connect";
                        Add(why, w + " | " + k);
                        if (samples.Count < 24 && w.StartsWith("back"))
                            samples.Add("  died (" + b.X.ToString("0.0") + ", " + b.Z.ToString("0.0") + ") had " + had.ToString("0.00") + "s, ran " + ran.ToString("0.0") + " m, start " + startD.ToString("0.0") +
                                        " m, closest " + minD.ToString("0.00") + " m, pred " + predSteps + ", presses " + presses + " | mid: " + stateMid + " | end: " + state);
                    }
                }
            }
        }
        Console.WriteLine("CORNER AUDIT  human-style vs " + opponent + " (" + difficulty.ToString("0.00") + "): " + rallies + " rallies, " + aiTurns + " AI turns, " + notUp + " ended with the AI not getting to it");
        foreach (var kv in where.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key.PadRight(24) + kv.Value);
        Console.WriteLine(" why:");
        foreach (var kv in why.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key.PadRight(60) + kv.Value);
        Console.WriteLine(" back-court samples:");
        foreach (var s in samples) Console.WriteLine(s);
    }

    static double Dist(V3 p, V3 q) { double dx = p.X - q.X, dz = p.Z - q.Z; return Math.Sqrt(dx * dx + dz * dz); }
    static void Add(Dictionary<string, int> d, string k) { d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1; }
}
