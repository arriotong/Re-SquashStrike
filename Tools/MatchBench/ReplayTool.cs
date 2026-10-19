// ===========================================================
// Tools/MatchBench/ReplayTool.cs
// P2-M6: the bundled replays - three AI-vs-AI matches the title screen
// plays before you have your own. AI on both sides, so a file holds no
// input at all: just the setup and the rallies' checksums (a few KB).
// Run again whenever the sim's fingerprint changes (a test says so):
//   dotnet run --project Tools/MatchBench -c Release -- --make-replays
// ===========================================================

using System;
using System.IO;
using SquashSim;
using SquashSim.Replays;

static class ReplayTool
{
    public static void Make(string folder)
    {
        Directory.CreateDirectory(folder);
        var matches = new[]
        {
            ("attacker", 0.9, "retriever", 0.9, 11u),
            ("shotmaker", 0.85, "grinder", 0.9, 23u),
            ("grinder", 0.9, "attacker", 0.85, 37u),
        };
        for (int i = 0; i < matches.Length; i++)
        {
            var (p0, d0, p1, d1, seed) = matches[i];
            var s = new ReplaySetup { Opponent0 = p0, Difficulty0 = (float)d0, Opponent = p1, Difficulty = (float)d1, GamesToWin = 1, Scoring = "par", BotSeed = seed };
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var rec = new MatchRecorder(sim, s, new InputIntent[2], "bundled", 0);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (!rec.Finished && sim.StepCount < 240L * 1800) { rec.BeforeStep(); sim.Step(dt); rec.AfterStep(); }
            rec.Finish(true);
            var f = rec.File;
            f.Meta["mode"] = "demo"; f.Meta["p1"] = p0.ToUpperInvariant(); f.Meta["p2"] = p1.ToUpperInvariant();
            f.Meta["sig0"] = p0; f.Meta["sig1"] = p1;
            var bytes = f.ToBytes();
            var v = ReplaySim.Verify(ReplayFile.FromBytes(bytes));
            string path = Path.Combine(folder, "demo" + i + ".bytes");
            File.WriteAllBytes(path, bytes);
            Console.WriteLine(path + ": " + p0 + " v " + p1 + ", " + f.Rallies.Count + " rallies, " + Highlights.GameScores(f) + ", " + bytes.Length + " bytes, " + v);
        }
    }
}
