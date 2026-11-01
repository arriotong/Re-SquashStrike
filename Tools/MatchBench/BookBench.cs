// ===========================================================
// Tools/MatchBench/BookBench.cs
// 2026-11-01: the visual book's 12 rules - how often the AI breaks
// each one, before the book (0) and with it (1), against the
// human-style model; its volleys (rule 8) and the rallies it wins.
//
//   --book [--games N]
// ===========================================================

using System;
using SquashSim;
using SquashSim.Replays;

static class BookBench
{
    static readonly bool NoVolley = Array.IndexOf(Environment.GetCommandLineArgs(), "--no-volley") >= 0;
    static readonly string Lean = Array.IndexOf(Environment.GetCommandLineArgs(), "--lean") >= 0 ? Environment.GetCommandLineArgs()[Array.IndexOf(Environment.GetCommandLineArgs(), "--lean") + 1] : null;

    static bool _argsApplied;
    /// <summary>The experiments' --rule-scale (once per run).</summary>
    public static void ApplyArgs()
    {
        if (_argsApplied) return; _argsApplied = true;
        // --rule-scale 6=0,3=0.5: scale one rule's say on the shots (an experiment; the book's tables are the bench's to bend)
        int ia = Array.IndexOf(Environment.GetCommandLineArgs(), "--rule-scale");
        if (ia >= 0)
            foreach (var kv in Environment.GetCommandLineArgs()[ia + 1].Split(','))
            {
                var parts = kv.Split('='); var r = SquashBook.Get(int.Parse(parts[0])); double k = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                if (r.Effect != null) for (int i = 0; i < r.Effect.Length; i++) r.Effect[i] *= k;
            }
    }

    /// <summary>The experiments' --no-volley and --lean on one AI.</summary>
    public static void ApplyLean(OpponentBrain ai)
    {
        if (NoVolley) ai.Profile.BookVolley = 0;
        if (Lean == null) return;
        foreach (var kv in Lean.Split(','))
        {
            var parts = kv.Split('='); double v = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            switch (parts[0]) { case "safe": ai.Profile.BookSafe = v; break; case "attack": ai.Profile.BookAttack = v; break;
                                case "patience": ai.Profile.BookPatience = v; break; case "volley": ai.Profile.BookVolley = v; break; }
        }
    }

    public static void Run(int games)
    {
        ApplyArgs();
        Console.WriteLine("BOOK  the AI against the human-style model, " + games + " games to 11 per line");
        string only = Array.IndexOf(Environment.GetCommandLineArgs(), "--only") >= 0 ? Environment.GetCommandLineArgs()[Array.IndexOf(Environment.GetCommandLineArgs(), "--only") + 1] : null;
        foreach (var id in new[] { "club:retriever:62", "star:s20", "star:s05", "star:s01", "star:s01 v shotmaker" })
            if (only == null || id.StartsWith(only))
            for (int book = 0; book <= 1; book++)
            {
                var applied = new int[13]; var broken = new int[13];
                int choices = 0, volleys = 0, aiWon = 0, rallies = 0, boasts = 0, deepBoasts = 0;
                for (int g = 0; g < games; g++)
                {
                    uint seed = (uint)(9100 + g * 211);
                    bool vsAi = id.EndsWith(" v shotmaker");          // an AI that boasts (the human-style model never does: rule 12)
                    var s = new ReplaySetup { Opponent = id.Split(' ')[0], Difficulty = 0.15f, GamesToWin = 1, Scoring = "par", BotSeed = seed, Lets = true, Book = book };
                    if (vsAi) { s.Opponent0 = "shotmaker"; s.Difficulty0 = 0.6f; }
                    ReplayMatch.DressStar(s);
                    ReplayMatch.Configure(s);
                    var m = ReplayMatch.Build(s);
                    if (!vsAi) m.Bots[0] = new HumanStyle(seed * 2654435761u + 3) { Arcade = true };
                    m.Players[0].WallAimEnabled = true; m.Players[0].ArcadeHitEnabled = true;
                    Program.ArcadeAi(m);
                    var ai = (OpponentBrain)m.Bots[1];
                    ApplyLean(ai);                                                 // --no-volley, --lean safe=0,...: experiments
                    double dt = PhysicsConfig.Current.Sim.FixedDt;
                    int seen = 0;
                    while (m.Phase != MatchPhase.MatchOver && m.Time < 1800)
                    {
                        m.Step(dt);
                        if (ai.Choices == seen) continue;
                        seen = ai.Choices; choices++;
                        var p = ai.LastPicture;
                        if (p.OppBoasted) { boasts++; if (p.BallZ < SquashBook.TZ) deepBoasts++; }
                        int shot = SquashBook.ShotIndex(ai.ArcadeShot);
                        double endX = EndX(ai.ArcadeShot, p.BallX);
                        for (int n = 2; n <= 12; n++)
                        {
                            if (!SquashBook.Applies(n, p)) continue;
                            applied[n]++;
                            if (SquashBook.Effect(n, p, shot, endX) <= SquashBook.BreakAt) broken[n]++;
                        }
                    }
                    volleys += ai.BookVolleys;
                    foreach (var r in m.History) { if (r.Winner < 0) continue; rallies++; if (r.Winner == 1) aiWon++; }
                }
                Console.WriteLine("  " + id.PadRight(20) + " book " + book + ":  AI won " + (100.0 * aiWon / Math.Max(1, rallies)).ToString("0") + "% of rallies, " +
                                  choices + " shots, " + volleys + " rule-8 volleys, met " + boasts + " boasts from the back (" + deepBoasts + " met in front of the T)");
                var line = "      broke (of the times it applied):";
                for (int n = 2; n <= 12; n++) if (n != 5 && n != 8 && n != 9) line += "  R" + n + " " + Pct(broken[n], applied[n]);
                Console.WriteLine(line);
            }
    }

    static string Pct(int a, int b) { return b == 0 ? "-" : (100.0 * a / b).ToString("0") + "%/" + b; }

    /// <summary>Where each of the six shots ends up across the court (OpponentBrain.ChooseArcadeShot's table).</summary>
    public static double EndX(string shot, double ballX)
    {
        int side = ballX >= 0 ? 1 : -1;
        switch (shot) { case "cross": case "boast": return -side * 2.6; case "lob": return -side * 2.4; default: return side * 2.6; }
    }
}
