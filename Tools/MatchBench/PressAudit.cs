// ===========================================================
// Tools/MatchBench/PressAudit.cs
// M8: what happens to a human's Hit presses? A scripted "human" with
// human flaws plays the AI, and every rally press is followed to its
// outcome (contact, whiff and why, dropped, nothing).
//
//   dotnet run --project Tools/MatchBench -c Release -- --press-audit [--games N] [--p1 retriever] [--d1 0.3]
//
// The human: reacts 0.25 s after the opponent's shot, runs to the ball
// with a +-0.35 m positioning error, presses Hit 0.15-1.4 s before the
// ball arrives (holds it 30% of the time), aims around the middle.
// ===========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SquashSim;

sealed class HumanStyle : IAthleteBrain
{
    readonly BallReader _read = new BallReader();
    uint _s;
    double _ax, _ay;
    public double ReactionTime = 0.25, PosError = 0.35, LeadMin = 0.15, LeadMax = 1.4, HoldChance = 0.3;
    /// <summary>The simple hitting system: hold early, let go as the ball arrives (+- ReleaseJitter).</summary>
    public bool Arcade;
    /// <summary>Aim high (lobs and high drives) instead of around the middle.</summary>
    public bool AimHigh;
    public double ReleaseJitter = 0.15;
    double _arrival, _releaseOffset = -1;
    double _oppContact = -9, _errX, _errZ, _pressAt = -1, _releaseAt = -1;
    long _turn = -1;
    bool _held;
    /// <summary>Counts rally presses (read by the audit).</summary>
    public int Presses;

    public HumanStyle(uint seed) { _s = seed; }
    double R() { _s = unchecked(_s * 1664525u + 1013904223u); return _s / 4294967296.0; }

    public void Fill(InputIntent it, MatchSimulation m, int me, double dt)
    {
        var c = m.Players[me]; var p = c.Player; double t = m.Time;
        it.Move.X = 0; it.Move.Z = 0; it.MoveMagnitude = 0; it.Hit.Pressed = false; it.Hit.Released = false;
        long turn = m.History.Count * 1000L + m.TurnShots;
        if (turn != _turn)
        {
            _turn = turn;
            if (m.TurnLastHitter != me) _oppContact = t;
            _errX = (R() * 2 - 1) * PosError; _errZ = (R() * 2 - 1) * PosError; _pressAt = -1;
        }
        bool mine = m.GoingForBall(me);
        bool serving = m.Phase == MatchPhase.Rally && m.TurnShots == 0 && m.Score.Server == me;

        // the toss
        if (m.Phase == MatchPhase.AwaitToss && m.Score.Server == me && m.PhaseTime > 0.6 && !_held)
        {
            _ax = -m.Score.ServeSide * 0.55; _ay = 0.85;
            it.Hit.Pressed = true; it.Hit.Held = true; _held = true; _releaseAt = t + 0.05;
        }
        // movement: after reacting, toward where the ball will be (with an error)
        if (m.BallLive && !serving)
        {
            double tx = 0, tz = 5.2;
            if (mine && t - _oppContact > ReactionTime && _read.Read(m.Ball, m.Surfaces, p)) { tx = _read.X - 0.6 + _errX; tz = _read.Z + 0.1 + _errZ; }
            double dx = tx - p.Pos.X, dz = tz - p.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
            if (d > 0.12) { it.Move.X = dx / d; it.Move.Z = dz / d; it.MoveMagnitude = Math.Min(1, Math.Sqrt(2 * 30 * d) / 5.2); }
        }
        // the serve: strike the falling toss
        if (serving && m.CanStrike(me) && !_held && _pressAt < 0 && m.PhaseTime >= 0.26 && m.Ball.Vel.Y < 0 && c.Prediction.Valid && c.Prediction.Time < 0.4)
        {
            _ax = -m.Score.ServeSide * 0.55; _ay = 0.85;
            it.Hit.Pressed = true; it.Hit.Held = true; _held = true; _releaseAt = t + 0.05; _pressAt = double.MaxValue;
        }
        // a rally press, some time before the ball arrives
        if (!serving && mine && m.BallLive && _pressAt < 0 && t - _oppContact > ReactionTime && _read.Valid)
        {
            double lead = LeadMin + R() * (LeadMax - LeadMin);
            _pressAt = t + Math.Max(0, _read.Time - lead);
            _arrival = t + _read.Time;
            _ax = (R() * 2 - 1) * 0.5; _ay = AimHigh ? 0.45 + R() * 0.55 : -0.2 + R() * 0.6;
        }
        if (_pressAt > 0 && t >= _pressAt && !_held)
        {
            it.Hit.Pressed = true; it.Hit.Held = true; _held = true;
            if (Arcade) { _releaseAt = double.MaxValue; _releaseOffset = -0.12 + R() * 0.30; }   // let go as the ball arrives: 0.18 s early .. 0.12 s late
            else _releaseAt = t + (R() < HoldChance ? 0.2 + R() * 0.4 : 0.06);
            _pressAt = double.MaxValue;
            if (m.Phase == MatchPhase.Rally && m.TurnShots > 0) Presses++;
        }
        // Arcade: watch the ball come; let go when it is (about) there.
        if (Arcade && _held && _releaseOffset > -1 && c.Prediction.Valid && c.Prediction.Time <= _releaseOffset + 1e-9) { _releaseAt = t; _releaseOffset = -1; }
        if (Arcade && _held && _releaseOffset > -1 && t - _arrival > 1.0) { _releaseAt = t; _releaseOffset = -1; }
        if (_held) { if (t >= _releaseAt) { it.Hit.Released = true; it.Hit.Held = false; _held = false; } else it.Hit.Held = true; }
        it.AimRawX = _ax; it.AimRawY = _ay;
    }
}

static class PressAudit
{
    public static bool AimHigh;

    public static void Run(int games, string opponent, double difficulty, bool gameProfile)
    {
        int outBack = 0, ownEnds = 0;
        var outcome = new Dictionary<string, int>(); var whyWhiff = new Dictionary<string, int>(); var commitErr = new List<double>();
        int presses = 0, contacts = 0, rallies = 0, won = 0;
        for (int g = 0; g < games; g++)
        {
            PhysicsConfig.ResetToDefaults();
            if (gameProfile) PhysicsConfig.UseGameProfile();
            var m = new MatchSimulation(0);
            var h = new HumanStyle((uint)(500 + g)) { Arcade = gameProfile, AimHigh = AimHigh };
            m.Bots[0] = h;
            m.Bots[1] = opponent == "wallbot" ? (IAthleteBrain)new WallBot((uint)(40 + g))
                                              : new OpponentBrain(AiProfile.ByName(opponent).WithDifficulty(difficulty), (uint)(40 + g));
            m.Players[0].WallAimEnabled = gameProfile;
            m.Players[0].ArcadeHitEnabled = gameProfile;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var c = m.Players[0];
            string lastState = c.Swing.State;
            bool pending = false;
            double minMiss = 99, bestD = 99, bestPhase = 0;
            V3 predAtCommit = null, ballAtBest = null;
            int hist = 0;
            while (m.Phase != MatchPhase.GameOver && m.Time < 1800)
            {
                int before = h.Presses;
                m.Step(dt);
                if (h.Presses > before) { presses++; pending = true; minMiss = 99; }
                var sw = c.Swing;
                if (pending)
                {
                    if (sw.State == SwingStates.Swinging && lastState != SwingStates.Swinging)
                    {
                        predAtCommit = c.Prediction.Valid ? c.Prediction.Point.Clone() : null;
                        bestD = 99;
                    }
                    if (sw.State == SwingStates.Swinging)
                    {
                        var hp = c.Racket.HeadPos; var b = m.Ball.Pos;
                        double d = Math.Sqrt((hp.X - b.X) * (hp.X - b.X) + (hp.Y - b.Y) * (hp.Y - b.Y) + (hp.Z - b.Z) * (hp.Z - b.Z));
                        if (d < minMiss) minMiss = d;
                        if (d < bestD) { bestD = d; bestPhase = sw.Phase; ballAtBest = b.Clone(); }
                    }
                    foreach (var e in m.StepEvents)
                        if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 0)
                        {
                            pending = false; contacts++;
                            Add(outcome, sw.Rushed ? "contact (late)" : "contact");
                        }
                    if (pending && lastState != SwingStates.Idle && sw.State == SwingStates.Idle && lastState != SwingStates.Recovering)
                    {
                        pending = false;
                        Add(outcome, c.Prediction.Valid ? "dropped: waited too long" : "dropped: ball never in reach");
                    }
                    if (pending && lastState == SwingStates.Swinging && sw.State == SwingStates.Recovering && !sw.HasContacted)
                    {
                        pending = false;
                        Add(outcome, minMiss < 0.3 ? "whiff: missed by < 0.3 m" : (minMiss < 0.8 ? "whiff: missed by 0.3-0.8 m" : "whiff: swung at air (> 0.8 m)"));
                        string k = (sw.LastSwing != null ? sw.LastSwing.Mode : "?") + ", ";
                        if (predAtCommit == null) k += "no contact predicted at swing start";
                        else if (bestPhase < 0.4) k += "ball came early (swung late)";
                        else if (bestPhase > 0.6) k += "ball came late (swung early)";
                        else k += "on time, racket in the wrong place";
                        Add(whyWhiff, k);
                        if (predAtCommit != null && ballAtBest != null)
                            commitErr.Add(Math.Sqrt((predAtCommit.X - ballAtBest.X) * (predAtCommit.X - ballAtBest.X) + (predAtCommit.Y - ballAtBest.Y) * (predAtCommit.Y - ballAtBest.Y) + (predAtCommit.Z - ballAtBest.Z) * (predAtCommit.Z - ballAtBest.Z)));
                    }
                    if (pending && m.History.Count != hist) { pending = false; Add(outcome, "rally ended first"); }
                }
                lastState = sw.State;
                if (m.History.Count != hist)
                {
                    hist = m.History.Count; rallies++; if (m.LastOutcome.Winner == 0) won++;
                    if (m.LastOutcome.Loser == 0) { ownEnds++; if ((m.LastOutcome.Reason ?? "").Contains("back wall")) outBack++; }
                }
            }
        }
        Console.WriteLine("PRESS AUDIT  human-style vs " + opponent + " (" + difficulty.ToString("0.0") + ")" + (gameProfile ? "  game profile" : "  lab") +
                          ":  " + presses + " rally presses, " + (100.0 * contacts / Math.Max(1, presses)).ToString("0") + "% connect, rallies won " + won + "/" + rallies +
                          " (" + (100.0 * won / Math.Max(1, rallies)).ToString("0") + "%)");
        Console.WriteLine("  rallies lost by the human: " + ownEnds + ", of them out off the back wall: " + outBack + " (" + (100.0 * outBack / Math.Max(1, ownEnds)).ToString("0") + "%)");
        foreach (var kv in outcome.OrderByDescending(k => k.Value))
            Console.WriteLine("  " + kv.Key.PadRight(36) + (100.0 * kv.Value / Math.Max(1, presses)).ToString("0.0").PadLeft(6) + " %");
        int ws = whyWhiff.Values.Sum();
        if (ws > 0)
        {
            Console.WriteLine("  why the whiffs:");
            foreach (var kv in whyWhiff.OrderByDescending(k => k.Value))
                Console.WriteLine("    " + kv.Key.PadRight(50) + (100.0 * kv.Value / ws).ToString("0").PadLeft(4) + " %");
        }
        commitErr.Sort();
        if (commitErr.Count > 0)
            Console.WriteLine("  whiffs: predicted contact at swing start vs the ball, median " + commitErr[commitErr.Count / 2].ToString("0.00") + " m");
    }

    static void Add(Dictionary<string, int> d, string k) { d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1; }
}
