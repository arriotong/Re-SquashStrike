// ===========================================================
// Assets/SquashGame/Runtime/UI/MatchStats.cs
// P2-M1: per-match numbers for the post-match screen, read from the
// sim's own events (contacts, rally ends, game ends). Reused by the
// stats and achievements of P2-M5.
// ===========================================================

using System.Collections.Generic;
using SquashSim;

namespace SquashGame.UI
{
    public sealed class MatchStats
    {
        public sealed class Side
        {
            public int Rallies, Winners, Errors, Contacts, GreatHits;
            public double TopPace, PaceSum;
            public double AvgPace { get { return Contacts > 0 ? PaceSum / Contacts : 0; } }
        }

        public readonly Side[] Players = { new Side(), new Side() };
        public int Rallies, LongestRally;
        /// <summary>Each finished game's points (P1, P2).</summary>
        public readonly List<int[]> Games = new List<int[]>();

        MatchRunner _runner;
        MatchSimulation _sim;
        MatchPhase _lastPhase;

        public void Attach(MatchRunner runner)
        {
            _runner = runner;
            _runner.Stepped += OnStepped;
        }

        public void Detach()
        {
            if (_runner != null) _runner.Stepped -= OnStepped;
            Watch(null);
        }

        public void Reset()
        {
            Players[0] = new Side(); Players[1] = new Side();
            Rallies = 0; LongestRally = 0;
            Games.Clear();
        }

        void Watch(MatchSimulation sim)
        {
            if (_sim == sim) return;
            if (_sim != null) _sim.RallyEnded -= OnRallyEnded;
            _sim = sim;
            if (_sim != null) { _sim.RallyEnded += OnRallyEnded; _lastPhase = _sim.Phase; }
        }

        bool Counting { get { return _runner != null && !_runner.Demo && !_runner.InSetup; } }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            Watch(_runner.Sim);
            if (!Counting || _sim == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type != SimEventType.RacketContact || e.PlayerIndex < 0 || e.PlayerIndex > 1) continue;
                var p = Players[e.PlayerIndex];
                p.Contacts++;
                p.PaceSum += e.BallSpeedOut;
                if (e.BallSpeedOut > p.TopPace) p.TopPace = e.BallSpeedOut;
                if (AimTargetView.IsGreat(e) && _sim.Referee.Shots > 1) p.GreatHits++;
            }
            var ph = _sim.Phase;
            if (ph != _lastPhase && (ph == MatchPhase.GameOver || ph == MatchPhase.MatchOver))
                Games.Add(new[] { _sim.Score.Points[0], _sim.Score.Points[1] });
            _lastPhase = ph;
        }

        void OnRallyEnded(RallyOutcome o)
        {
            if (!Counting) return;
            Rallies++;
            int shots = _sim.Referee.Shots;
            if (shots > LongestRally) LongestRally = shots;
            if (o.Winner >= 0 && o.Winner <= 1) Players[o.Winner].Rallies++;
            switch (o.Kind)
            {
                case RallyEnd.NotUp:
                    if (o.Winner >= 0) Players[o.Winner].Winners++;       // the opponent could not get it back
                    break;
                case RallyEnd.Down: case RallyEnd.Out: case RallyEnd.Fault: case RallyEnd.FootFault: case RallyEnd.HitSelf:
                    if (o.Loser >= 0 && o.Loser <= 1) Players[o.Loser].Errors++;
                    break;
            }
        }
    }
}
