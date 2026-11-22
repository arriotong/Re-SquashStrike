// ===========================================================
// Tools/MatchBench/ServeBench.cs
// 2026-11-22 (the user: "ai opponent still serves out pretty frequently"):
// every Quick Match opponent, every class, Abilities on/off - the computer's
// serve faults and why, and the faults on the serve after a Special Shot.
//
//   --serves [--games N]
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;
using SquashSim.Replays;

static class ServeBench
{
    static bool _land; static int _side; public static System.Collections.Generic.List<double> Margins = new System.Collections.Generic.List<double>(); static double _sx, _sz; static bool _watch; static int Samples; public static int Hard; public static double MaxServe; public static float DiffArg = 0.8f; public static string OnlyOpp;
    static readonly string[] Kit = { "ace", "special" };

    public static void Run(int games)
    {
        int all = 0, bad = 0, afterSp = 0, afterSpBad = 0;
        var why = new Dictionary<string, int>();
        foreach (var opp in OnlyOpp != null ? new[] { OnlyOpp } : SquashSim.Career.Stars.QuickLadder)
            foreach (var cls in new[] { "allround", "power", "accuracy", "speed", "touch" })
                foreach (var ab in new[] { false, true })
                {
                    int s0 = 0, f0 = 0;
                    for (int g = 0; g < games; g++)
                    {
                        var s = new ReplaySetup { Opponent = opp, Difficulty = DiffArg, Scoring = "par", GamesToWin = 1, BotSeed = (uint)(900 + g * 13), Lets = true, Opponent0 = "retriever", Difficulty0 = 0.6f, Class1 = cls };
                        if (ab)
                        {
                            s.Mods1 = new List<RunMod>(); s.Mods0 = new List<RunMod>();
                            foreach (var k in Kit) { s.Mods1.Add(new RunMod { Id = k, Level = 1 }); s.Mods0.Add(new RunMod { Id = k, Level = 1 }); }
                            var star = SquashSim.Career.Stars.Find(opp);
                            if (star != null) s.Mods1.AddRange(star.Signature);
                        }
                        ReplayMatch.Configure(s);
                        var m = ReplayMatch.Build(s);
                        Program.ArcadeAi(m);
                        double dt = PhysicsConfig.Current.Sim.FixedDt;
                        int seenSp = 0; bool lastSp = false;
                        while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
                        {
                            int before = m.History.Count, by = m.Score.Server;
                            m.Step(dt);
                            foreach (var ev in m.StepEvents)
                            {
                                if (ev.Type == SimEventType.RacketContact && ev.PlayerIndex == 1 && m.Referee.Shots == 1) { double sp = V3.Length(m.Ball.Vel); if (sp > 30) { Hard++; _sx = m.Ball.Vel.X; _sz = m.Ball.Vel.Z; _watch = true; _land = true; _side = m.Score.ServeSide; } if (sp > MaxServe) MaxServe = sp; }
                                if (_land && m.Referee.Shots > 1) _land = false;
                                if (_land && ev.Type == SimEventType.Impact && ev.SurfaceId == "floor") { _land = false; double mg = -_side * ev.Point.X; Margins.Add(mg); }
                                if (_watch && ev.Type == SimEventType.Impact && ev.SurfaceId == "frontWall") { _watch = false; double ratio = (System.Math.Abs(m.Ball.Vel.X) / System.Math.Max(0.1, System.Math.Abs(m.Ball.Vel.Z))) / (System.Math.Abs(_sx) / System.Math.Abs(_sz)); if (false) Console.WriteLine("  e~" + (1 / ratio).ToString("0.00") + " in " + V3.Length(new V3(_sx, 0, _sz)).ToString("0") + " temp " + m.Ball.Temperature.ToString("0")); Samples++; }
                            }
                            if (m.History.Count == before) continue;
                            bool spThis = m.SpecialsFired > seenSp; seenSp = m.SpecialsFired;
                            if (by == 1)
                            {
                                var o = m.History[m.History.Count - 1];
                                bool fault = o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.FootFault;
                                s0++; if (fault) f0++;
                                if (lastSp) { afterSp++; if (fault) afterSpBad++; }
                                if (fault) { string r = o.Reason ?? o.Kind.ToString(); why[r] = why.TryGetValue(r, out int n) ? n + 1 : 1; }
                            }
                            lastSp = spThis;
                        }
                        PhysicsConfig.ResetToDefaults();
                    }
                    all += s0; bad += f0;
                    if (f0 > 0) Console.WriteLine("  " + opp.PadRight(20) + cls.PadRight(9) + (ab ? "abil " : "     ") + f0 + "/" + s0);
                }
        Margins.Sort(); if (Margins.Count > 0) Console.WriteLine("hard serve landing past the half line (m): min " + Margins[0].ToString("0.00") + "  5% " + Margins[Margins.Count / 20].ToString("0.00") + "  median " + Margins[Margins.Count / 2].ToString("0.00"));
        Console.WriteLine("hard serves " + Hard + "  fastest " + MaxServe.ToString("0.0"));
        Console.WriteLine("ALL " + bad + "/" + all + " = " + (100.0 * bad / Math.Max(1, all)).ToString("0.00") + "%   after a special " + afterSpBad + "/" + afterSp);
        foreach (var kv in why) Console.WriteLine("  " + kv.Key + " x" + kv.Value);
    }
}
