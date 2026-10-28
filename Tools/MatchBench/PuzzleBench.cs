// ===========================================================
// Tools/MatchBench/PuzzleBench.cs
// Playable challenges (2026-10-28): shot puzzles made headless - how long
// one takes (sim steps, seconds), how many candidate rallies it needs,
// and what the answers look like.
//
//   --puzzle [--games N]
//   --puzzle-bank COUNT START OUT   COUNT puzzles from maker seeds START,
//                                   START+1, ... as JSON lines to OUT (the
//                                   game's bank is merged and balanced from
//                                   these: Tools/MatchBench/merge_puzzles.py)
// ===========================================================

using System;
using System.Diagnostics;
using SquashSim;
using SquashSim.Modes;

static class PuzzleBench
{
    public static void Bank(int count, uint start, string outPath)
    {
        using (var w = new System.IO.StreamWriter(outPath))
            for (int k = 0; k < count; k++)
            {
                var maker = new PuzzleMaker(start + (uint)k);
                while (!maker.Step(20000)) { }
                var p = maker.Result;
                w.WriteLine(UnityLikeJson(p));
                w.Flush();
                Console.WriteLine(k + ": " + p.Options[p.Best] + "  (" + maker.Candidates + " candidates)");
            }
    }

    static string UnityLikeJson(ShotPuzzle p)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string Rates() { var r = new System.Text.StringBuilder(); for (int i = 0; i < p.Rates.Count; i++) r.Append((i > 0 ? "," : "") + p.Rates[i].ToString("R", inv)); return r.ToString(); }
        return "{\"Seed\":" + p.Seed + ",\"You\":\"" + p.You + "\",\"Them\":\"" + p.Them + "\",\"YouLevel\":" + p.YouLevel.ToString("R", inv) + ",\"ThemLevel\":" + p.ThemLevel.ToString("R", inv) +
               ",\"Freeze\":" + p.Freeze + ",\"Options\":[\"" + string.Join("\",\"", p.Options) + "\"],\"Rates\":[" + Rates() + "],\"Best\":" + p.Best + ",\"Why\":\"" + p.Why.Replace("\"", "'") + "\"}";
    }

    public static void Run(int n)
    {
        long totalSteps = 0; double totalMs = 0;
        for (int d = 0; d < n; d++)
        {
            var maker = new PuzzleMaker((uint)(20261028 + d));
            var sw = Stopwatch.StartNew();
            long steps = 0;
            while (!maker.Step(5000)) steps += 5000;
            sw.Stop();
            totalSteps += steps; totalMs += sw.Elapsed.TotalMilliseconds;
            var p = maker.Result;
            var opts = "";
            for (int i = 0; i < p.Options.Count; i++) opts += (i == p.Best ? "*" : "") + p.Options[i] + " " + (p.Rates[i] * 100).ToString("0") + "%  ";
            Console.WriteLine("puzzle " + d + ": " + maker.Candidates + " candidates, ~" + steps + " steps, " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s  |  " + p.You + " v " + p.Them + "  |  " + opts);
            Console.WriteLine("    " + p.Why);
        }
        Console.WriteLine("mean " + (totalMs / n / 1000).ToString("0.0") + " s, " + (totalSteps / n) + " steps a puzzle");
    }
}
