// ===========================================================
// Tools/MatchBench/ReviewBench.cs
// 2026-11-22 evening, the referee and the video review: appeals decided,
// how many the referee got wrong (close calls), challenges, overturned.
//   --reviews [--games N]
// ===========================================================

using System;
using SquashSim;

static class ReviewBench
{
    public static void Run(int games)
    {
        int calls = 0, wrong = 0, reviews = 0, over = 0, lineStrokes = 0;
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.UseGameProfile();
            var c = PhysicsConfig.Current.Match; c.WarmUpEnabled = false; c.GamesToWin = 1;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName(g % 2 == 0 ? "attacker" : "grinder").WithDifficulty(0.8), (uint)(900 + g));
            m.Bots[1] = new OpponentBrain(AiProfile.ByName(g % 2 == 0 ? "retriever" : "shotmaker").WithDifficulty(0.8), (uint)(950 + g));
            for (int i = 0; i < 2; i++) { m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true; }
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            int seen = 0;
            while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
            {
                m.Step(dt);
                if (m.History.Count > seen)
                {
                    seen = m.History.Count;
                    var o = m.History[seen - 1];
                    if (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke) { calls++; if (o.Reason != null && o.Reason.Contains("line")) lineStrokes++; }
                }
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
            }
            wrong += m.RefErrors; reviews += m.Reviews; over += m.Overturned;
        }
        Console.WriteLine("calls " + calls + " (line strokes " + lineStrokes + "), referee wrong " + wrong + " (" + (100.0 * wrong / Math.Max(1, calls)).ToString("0") + "%), AI reviews " + reviews + ", overturned " + over);
    }
}
