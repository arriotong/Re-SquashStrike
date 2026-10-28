// ===========================================================
// Assets/SquashSim/Modes/ShotPuzzles.cs
// Playable challenges (2026-10-28): the SHOT PUZZLE - "what shot should
// you hit, like a chess puzzle, but instead you choose a shot to hit when
// the AI are playing the rallies, and that can train your decisions"
// (the user).
//
//   the moment   an AI rally (you are athlete 0, an AI standing in) is
//                played to a moment when your next stroke is coming and
//                the AI has not chosen it yet - three shots in or more
//   the answer   each of the six shots (straight, cross, lob, drop, kill,
//                boast) is forced from that exact moment and the rally
//                played out, Trials times with the AIs' randomness
//                varied; the one that wins most is the answer - measured
//                by the sim, never written by hand
//   a good one   a clear best (at least 30 points ahead of the shots
//                shown with it); else the next candidate rally
//
// Everything is rebuilt from the same setup and stepped the same way, so
// the game can play the very same rally to the very same moment
// (MatchRunner's puzzle mode) and then the chosen shot (trial 0).
// The maker is time-sliced (Step(budget)) so the game never stalls.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim.Replays;

namespace SquashSim.Modes
{
    [Serializable]
    public sealed class ShotPuzzle
    {
        public uint Seed;
        /// <summary>Who plays: your stand-in's style and level, the opponent's.</summary>
        public string You, Them;
        public float YouLevel, ThemLevel;
        /// <summary>The step the rally freezes at (your stroke is coming).</summary>
        public long Freeze;
        /// <summary>The shots offered (ShotPuzzles.Shots ids), what each wins from here (0..1), the best one's index.</summary>
        public List<string> Options = new List<string>();
        public List<double> Rates = new List<double>();
        public int Best;
        public string Why;

        public ReplaySetup Setup() { return ShotPuzzles.SetupFor(this); }
        /// <summary>A choice counts as solved: the best, or within a trial of it.</summary>
        public bool Solves(int option) { return option >= 0 && option < Rates.Count && Rates[option] >= Rates[Best] - 1.0 / ShotPuzzles.Trials + 1e-9; }
    }

    public static class ShotPuzzles
    {
        public static readonly string[] Shots = { "straight", "cross", "lob", "drop", "kill", "boast" };
        public const int Trials = 6, Shown = 4;
        public const double Margin = 0.3, MaxSeconds = 20, PlayoutSeconds = 12;
        static readonly string[] Styles = { "retriever", "attacker", "shotmaker", "grinder" };

        public static string Name(string shot)
        {
            switch (shot)
            {
                case "straight": return "Straight drive";
                case "cross": return "Cross-court drive";
                case "lob": return "Lob";
                case "drop": return "Drop shot";
                case "kill": return "Kill";
                case "boast": return "Boast";
                default: return shot;
            }
        }

        public static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }

        /// <summary>A candidate's players: two styles, decent levels (the seed picks them).</summary>
        public static ShotPuzzle Candidate(uint seed)
        {
            uint h = Hash(seed + 7u);
            return new ShotPuzzle { Seed = seed == 0 ? 1u : seed, You = Styles[h % 4], Them = Styles[(h >> 8) % 4], YouLevel = 0.72f, ThemLevel = 0.62f + 0.1f * ((h >> 16) % 3) };
        }

        public static ReplaySetup SetupFor(ShotPuzzle p)
        {
            return new ReplaySetup { Opponent0 = p.You, Difficulty0 = p.YouLevel, Opponent = p.Them, Difficulty = p.ThemLevel, GamesToWin = 1, Scoring = "par",
                                     WarmUp = false, Lets = false, BotSeed = p.Seed };
        }

        /// <summary>The random streams the AIs play a trial with, from the freeze on.</summary>
        public static uint TrialSeed(uint seed, int trial, int who) { return Hash(seed * 31u + (uint)trial * 7919u + (uint)who * 104729u + 3u); }

        /// <summary>The moment to freeze at: your stroke is coming, the AI hasn't chosen it, three shots in or more.</summary>
        public static bool IsFreezeMoment(MatchSimulation m)
        {
            if (m.Phase != MatchPhase.Rally || m.Referee.Outcome != null || m.Referee.PendingStriker != 0 || m.Referee.Shots < 3 || !m.CanStrike(0)) return false;
            var b = m.Bots[0] as OpponentBrain;
            var pred = m.Players[0].Prediction;
            return b != null && !b.Decided && pred.Valid && pred.Time > b.DecisionLead + 0.12 && pred.Time < b.DecisionLead + 0.4;
        }

        /// <summary>At the freeze: the shot forced, the trial's randomness (the game does exactly this after you choose).</summary>
        public static void Force(MatchSimulation m, ShotPuzzle p, string shot, int trial)
        {
            var me = (OpponentBrain)m.Bots[0];
            me.ForceShot = shot;
            me.Reseed(TrialSeed(p.Seed, trial, 0));
            if (m.Bots[1] is OpponentBrain them) them.Reseed(TrialSeed(p.Seed, trial, 1));
        }

        /// <summary>How a played-out rally went for you: 1 won, 0 lost, 0.5 a let.</summary>
        public static double Score(RallyOutcome o) { return o == null || o.Kind == RallyEnd.Let || o.Winner < 0 ? 0.5 : o.Winner == 0 ? 1 : 0; }

        /// <summary>Why the best shot is best, in a line, from the moment's picture.</summary>
        public static string Why(MatchSimulation m, ShotPuzzle p)
        {
            var opp = m.Players[1].Player.Pos;
            var ball = m.Players[0].Prediction.Valid ? m.Players[0].Prediction.Point : m.Ball.Pos;
            string best = p.Options[p.Best];
            int wins = (int)Math.Round(p.Rates[p.Best] * Trials);
            bool oppBack = opp.Z > 6.2, oppFront = opp.Z < 4.6, sameSide = (opp.X >= 0) == (ball.X >= 0);
            string reason;
            switch (best)
            {
                case "drop": case "kill": case "boast":
                    reason = oppBack ? "they were stuck at the back" : "it's short and low - they had to run the length of the court"; break;
                case "lob":
                    reason = oppFront ? "they were up at the front - over their head" : "it buys you time to get back to the T"; break;
                case "cross":
                    reason = sameSide ? "they were on your side of the court - the open side is across" : "the width beats them on the volley"; break;
                default:
                    reason = !sameSide ? "they were across the court - down the wall is open" : "tight down the wall, they can't attack it"; break;
            }
            return Name(best) + " won " + wins + " of " + Trials + " from here: " + reason + ".";
        }
    }

    /// <summary>Makes one shot puzzle, a slice at a time: finds a moment, measures every shot, keeps a clear one.</summary>
    public sealed class PuzzleMaker
    {
        public const int MaxCandidates = 8;
        readonly uint _seed;
        int _candidate;
        ShotPuzzle _p;
        MatchSimulation _find, _run;
        int _opt, _trial;
        bool _forced;
        double _runStart;
        readonly double[] _wins = new double[ShotPuzzles.Shots.Length];
        public ShotPuzzle Result { get; private set; }
        public bool Done { get { return Result != null; } }
        /// <summary>0..1, roughly (the screen's bar).</summary>
        public float Progress { get { return Result != null ? 1f : _p == null || _p.Freeze == 0 ? 0.05f : (float)(0.1 + 0.9 * (_opt * ShotPuzzles.Trials + _trial) / (ShotPuzzles.Shots.Length * ShotPuzzles.Trials)); } }
        public int Candidates { get { return _candidate; } }

        public PuzzleMaker(uint seed) { _seed = seed == 0 ? 1u : seed; }

        /// <summary>Run up to <paramref name="budget"/> sim steps. True when the puzzle is made.</summary>
        public bool Step(int budget)
        {
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (budget > 0 && Result == null)
            {
                if (_p == null)
                {
                    _p = ShotPuzzles.Candidate(ShotPuzzles.Hash(_seed + (uint)_candidate * 977u));
                    _candidate++;
                    ReplayMatch.Configure(_p.Setup());
                    _find = ReplayMatch.Build(_p.Setup());
                    _opt = 0; _trial = 0; _run = null;
                    Array.Clear(_wins, 0, _wins.Length);
                }
                if (_p.Freeze == 0)
                {
                    // ---- find the moment ----
                    _find.Step(dt); budget--;
                    if (ShotPuzzles.IsFreezeMoment(_find)) { _p.Freeze = _find.StepCount; continue; }
                    if (_find.Time > ShotPuzzles.MaxSeconds || _find.Phase == MatchPhase.MatchOver) _p = null;          // nothing good: the next rally
                    continue;
                }
                // ---- measure: each shot, each trial, from the freeze ----
                if (_run == null) { _run = ReplayMatch.Build(_p.Setup()); _forced = false; }
                if (!_forced)
                {
                    while (budget > 0 && _run.StepCount < _p.Freeze) { _run.Step(dt); budget--; }
                    if (_run.StepCount < _p.Freeze) continue;
                    ShotPuzzles.Force(_run, _p, ShotPuzzles.Shots[_opt], _trial);
                    _forced = true; _runStart = _run.Time;
                }
                while (budget > 0 && _run.Phase == MatchPhase.Rally && _run.Time - _runStart < ShotPuzzles.PlayoutSeconds) { _run.Step(dt); budget--; }
                if (_run.Phase == MatchPhase.Rally && _run.Time - _runStart < ShotPuzzles.PlayoutSeconds) continue;
                _wins[_opt] += _run.Phase == MatchPhase.Rally ? 0.5 : ShotPuzzles.Score(_run.LastOutcome);
                _run = null;
                if (++_trial < ShotPuzzles.Trials) continue;
                _trial = 0;
                if (++_opt < ShotPuzzles.Shots.Length) continue;
                if (!Pick() ) _p = null;                                     // no clear answer: the next rally
            }
            return Result != null;
        }

        /// <summary>The shots to show: the best and three it clearly beats (shuffled by the seed). False: not a clear puzzle.</summary>
        bool Pick()
        {
            int n = ShotPuzzles.Shots.Length;
            var rates = new double[n];
            for (int i = 0; i < n; i++) rates[i] = _wins[i] / ShotPuzzles.Trials;
            int best = 0;
            for (int i = 1; i < n; i++) if (rates[i] > rates[best]) best = i;
            bool last = _candidate >= MaxCandidates;
            double margin = last ? 1.0 / ShotPuzzles.Trials : ShotPuzzles.Margin;
            var worse = new List<int>();
            for (int i = 0; i < n; i++) if (i != best && rates[i] <= rates[best] - margin + 1e-9) worse.Add(i);
            if (rates[best] < 0.5 && !last) return false;
            if (worse.Count < ShotPuzzles.Shown - 1) return false;
            uint h = ShotPuzzles.Hash(_p.Seed + 5u);
            var shown = new List<int> { best };
            while (shown.Count < ShotPuzzles.Shown) { h = ShotPuzzles.Hash(h + 1u); int k = (int)(h % (uint)worse.Count); shown.Add(worse[k]); worse.RemoveAt(k); }
            // the order on screen: shuffled
            for (int i = shown.Count - 1; i > 0; i--) { h = ShotPuzzles.Hash(h + 3u); int j = (int)(h % (uint)(i + 1)); int t = shown[i]; shown[i] = shown[j]; shown[j] = t; }
            foreach (int i in shown) { _p.Options.Add(ShotPuzzles.Shots[i]); _p.Rates.Add(rates[i]); }
            _p.Best = shown.IndexOf(best);
            _p.Why = ShotPuzzles.Why(_find, _p);
            Result = _p;
            return true;
        }
    }
}
