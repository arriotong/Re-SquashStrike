// ===========================================================
// Assets/SquashSim/Match/LetSurvey.cs
// P2-M7: how often interference comes up - AI against AI in the game's
// own setup (ReplayMatch), appeals on. What the referee rework is tuned
// against (MatchBench --let-survey; a smaller run in the EditMode tests).
//
//   flagged turns   turns in which an obstruction counted (Interference)
//   appeals         per minute of live rally
//   calls           rallies ended by Let / No let / Stroke (appeals) and
//                   by the ball hitting a player (in a live rally)
//   rally length    median shots, to see the rework does not change play
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim.Replays;

namespace SquashSim
{
    public sealed class LetSurvey
    {
        public int Games, Rallies, Turns, FlaggedTurns, Appeals, BodyHitsLive;
        public double LiveSeconds;
        public readonly Dictionary<string, int> FlaggedBy = new Dictionary<string, int>();
        /// <summary>What the referee would have called on each flagged turn (had the striker asked at its end).</summary>
        public readonly Dictionary<string, int> WouldCall = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Calls = new Dictionary<string, int>();
        public readonly List<int> Shots = new List<int>();

        public double FlaggedShare { get { return Turns == 0 ? 0 : (double)FlaggedTurns / Turns; } }
        public int InterferenceCalls { get { int n = 0; foreach (var kv in Calls) if (kv.Key.StartsWith("Let") || kv.Key.StartsWith("NoLet") || kv.Key.StartsWith("Stroke")) n += kv.Value; return n; } }
        public double CallShare { get { return Rallies == 0 ? 0 : (double)InterferenceCalls / Rallies; } }
        public int Count(string kindPrefix) { int n = 0; foreach (var kv in Calls) if (kv.Key.StartsWith(kindPrefix + ":")) n += kv.Value; return n; }
        public double MedianShots { get { if (Shots.Count == 0) return 0; var l = new List<int>(Shots); l.Sort(); return l[l.Count / 2]; } }

        /// <summary>Play <paramref name="games"/> one-game matches across the personalities and levels.</summary>
        public static LetSurvey Run(int games, bool appeals = true, double[] levelsOverride = null)
        {
            var keep = PhysicsConfig.Current;
            var r = new LetSurvey { Games = games };
            string[] opps = { "retriever", "attacker", "shotmaker", "grinder" };
            double[] levels = levelsOverride ?? GameTuning.Ai.Levels;
            try
            {
                for (int g = 0; g < games; g++)
                {
                    var s = new ReplaySetup
                    {
                        Opponent0 = opps[(g + 1) % 4], Difficulty0 = (float)levels[(g / 4) % 3],
                        Opponent = opps[g % 4], Difficulty = (float)levels[(g / 2) % 3],
                        GamesToWin = 1, BotSeed = (uint)(77 + g * 13),
                    };
                    ReplayMatch.Configure(s);
                    PhysicsConfig.Current.Match.AppealsEnabled = appeals;
                    var m = ReplayMatch.Build(s);
                    m.Appealed += i => r.Appeals++;
                    double dt = PhysicsConfig.Current.Sim.FixedDt;
                    int seen = 0, lastShots = -1;
                    ObstructionKind worst = ObstructionKind.None;
                    string would = null;
                    bool inTurn = false;
                    while (m.Phase != MatchPhase.MatchOver && m.Phase != MatchPhase.GameOver && m.StepCount < 240L * 1800)
                    {
                        bool live = m.Phase == MatchPhase.Rally && m.Referee.Outcome == null;
                        m.Step(dt);
                        if (live)
                        {
                            r.LiveSeconds += dt;
                            foreach (var e in m.StepEvents) if (e.Type == SimEventType.BodyHit) r.BodyHitsLive++;
                            if (m.Referee.Shots != lastShots)
                            {
                                if (inTurn) { r.Turns++; if (worst != ObstructionKind.None) { r.FlaggedTurns++; Add(r.FlaggedBy, worst.ToString()); Add(r.WouldCall, would ?? "?"); } }
                                lastShots = m.Referee.Shots; worst = ObstructionKind.None; inTurn = m.Referee.Shots > 0; would = null;
                            }
                            if (m.Interference.ThisTurn > worst) worst = m.Interference.ThisTurn;
                            if (m.Interference.ThisTurn != ObstructionKind.None && m.Referee.Outcome == null) would = m.Interference.Decide().Reason;
                        }
                        if (m.History.Count != seen)
                        {
                            seen = m.History.Count;
                            var o = m.LastOutcome;
                            r.Rallies++;
                            r.Shots.Add(m.ShotsPerRally.Count > 0 ? m.ShotsPerRally[m.ShotsPerRally.Count - 1] : 0);
                            if (o.Kind == RallyEnd.Let || o.Kind == RallyEnd.NoLet || o.Kind == RallyEnd.Stroke || o.Reason.Contains("ball hit") || o.Reason.Contains("Ball hit"))
                                Add(r.Calls, o.Kind + ": " + o.Reason.Replace("P1", "P").Replace("P2", "P"));
                        }
                    }
                }
            }
            finally { PhysicsConfig.Current = keep; }
            return r;
        }

        static void Add(Dictionary<string, int> d, string k) { d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LET SURVEY  " + Games + " games, " + Rallies + " rallies, " + (LiveSeconds / 60).ToString("0") + " min of live rally, median " + MedianShots + " shots");
            sb.AppendLine("  flagged turns " + FlaggedTurns + " / " + Turns + " = " + (100 * FlaggedShare).ToString("0.0") + "%");
            foreach (var kv in FlaggedBy) sb.AppendLine("    " + kv.Key + " " + kv.Value);
            sb.AppendLine("  the referee would have called (flagged turns)");
            foreach (var kv in WouldCall) sb.AppendLine("    " + kv.Value.ToString().PadLeft(4) + "  " + kv.Key);
            sb.AppendLine("  appeals " + Appeals + " (" + (60.0 * Appeals / Math.Max(1, LiveSeconds)).ToString("0.00") + " a minute), ball hit a player in a live rally " + BodyHitsLive);
            sb.AppendLine("  interference calls " + InterferenceCalls + " = " + (100 * CallShare).ToString("0.0") + "% of rallies");
            var l = new List<KeyValuePair<string, int>>(Calls); l.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in l) sb.AppendLine("    " + kv.Value.ToString().PadLeft(4) + "  " + kv.Key);
            return sb.ToString();
        }
    }
}
