// ===========================================================
// Assets/SquashGame/Runtime/UI/MatchStats.cs
// P2-M1: per-match numbers for the post-match screen, read from the
// sim's own events (contacts, rally ends, game ends). Reused by the
// stats and achievements of P2-M5: shot mix (the stroke's technique),
// nicks, errors by type, aces, winning boasts, great hits per game,
// tins in a row, a 0-5 comeback, time on the T - Facts() sums it up.
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
            // P2-M5
            public int Drives, Lobs, Drops, Kills, Boasts, Nicks, Aces, WinningBoasts, Tins, Outs, NotUps, Faults;
            public int GreatThisGame, MostGreatInAGame, TinRun, MostTinsInARow;
            public bool HitSelf, CameBack;
            // P2-M7: calls given to this player (a let or a stroke for asking, a stroke on the ball) and refused (no let)
            public int Lets, Strokes, NoLets;
            public bool WonAfterLet;
            public double TSeconds;
            public double TopPace, PaceSum;
            public double AvgPace { get { return Contacts > 0 ? PaceSum / Contacts : 0; } }
        }

        public readonly Side[] Players = { new Side(), new Side() };
        public int Rallies, LongestRally;
        public double PlaySeconds;
        int _lastStriker = -1, _server = -1;
        bool _afterLet;
        string _lastTech;
        bool _down05;
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
            Rallies = 0; LongestRally = 0; PlaySeconds = 0;
            Games.Clear();
            _lastStriker = -1; _server = -1; _lastTech = null; _down05 = false; _afterLet = false;
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
                if (AimTargetView.IsGreat(e) && _sim.Referee.Shots > 1) { p.GreatHits++; p.GreatThisGame++; if (p.GreatThisGame > p.MostGreatInAGame) p.MostGreatInAGame = p.GreatThisGame; }
                if (_sim.Referee.Shots <= 1) _server = e.PlayerIndex;                       // the serve
                switch (e.Technique)
                {
                    case "drive": p.Drives++; break; case "lob": p.Lobs++; break; case "drop": p.Drops++; break;
                    case "kill": p.Kills++; break; case "boast": p.Boasts++; break;
                }
                _lastStriker = e.PlayerIndex; _lastTech = e.Technique;
            }
            for (int i = 0; i < events.Count; i++)
                if (events[i].Type == SimEventType.Impact && events[i].Nick && _lastStriker >= 0) Players[_lastStriker].Nicks++;

            var ph = _sim.Phase;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            if (ph == MatchPhase.Rally)
            {
                PlaySeconds += dt;
                double tz = PhysicsConfig.Current.Match.TZ;
                for (int k = 0; k < 2; k++)
                {
                    var pos = _sim.Players[k].Player.Pos;
                    if (System.Math.Abs(pos.X) < 1.0 && System.Math.Abs(pos.Z - tz) < 1.0) Players[k].TSeconds += dt;
                }
            }
            var sc = _sim.Score;
            int me = _runner.HumanIndex;                                // P2-M8: online you may be athlete 1
            if (!sc.GameOver && sc.Points[me] == 0 && sc.Points[1 - me] >= 5) _down05 = true;
            if (ph != _lastPhase && (ph == MatchPhase.GameOver || ph == MatchPhase.MatchOver))
            {
                Games.Add(new[] { sc.Points[0], sc.Points[1] });
                if (_down05 && sc.GameWinner == me) Players[me].CameBack = true;
                _down05 = false;
                Players[0].GreatThisGame = 0; Players[1].GreatThisGame = 0;
            }
            _lastPhase = ph;
        }

        void OnRallyEnded(RallyOutcome o)
        {
            if (!Counting) return;
            Rallies++;
            int shots = _sim.Referee.Shots;
            if (shots > LongestRally) LongestRally = shots;
            if (o.Winner >= 0 && o.Winner <= 1) Players[o.Winner].Rallies++;
            if (_afterLet && o.Winner >= 0 && o.Winner <= 1) Players[o.Winner].WonAfterLet = true;
            _afterLet = o.Kind == RallyEnd.Let;
            int asker = _sim.Interference.Striker;               // still the call's (P2-M7)
            switch (o.Kind)
            {
                case RallyEnd.Let:
                    if (asker >= 0 && asker <= 1) Players[asker].Lets++;
                    break;
                case RallyEnd.Stroke:
                    if (o.Winner >= 0 && o.Winner <= 1) Players[o.Winner].Strokes++;
                    break;
                case RallyEnd.NoLet:
                    if (o.Loser >= 0 && o.Loser <= 1) Players[o.Loser].NoLets++;
                    break;
                case RallyEnd.NotUp:
                    if (o.Winner >= 0 && o.Winner <= 1)
                    {
                        var w = Players[o.Winner];
                        w.Winners++;                                   // the opponent could not get it back
                        if (shots == 1 && o.Winner == _server) w.Aces++;
                        if (o.Winner == _lastStriker && _lastTech == "boast") w.WinningBoasts++;
                    }
                    if (o.Loser >= 0 && o.Loser <= 1) Players[o.Loser].NotUps++;
                    break;
                case RallyEnd.Down: case RallyEnd.Out: case RallyEnd.Fault: case RallyEnd.FootFault: case RallyEnd.HitSelf:
                    if (o.Loser >= 0 && o.Loser <= 1)
                    {
                        var l = Players[o.Loser];
                        l.Errors++;
                        if (o.Kind == RallyEnd.Down) l.Tins++;
                        else if (o.Kind == RallyEnd.Out) l.Outs++;
                        else if (o.Kind == RallyEnd.HitSelf) l.HitSelf = true;
                        else l.Faults++;
                    }
                    break;
            }
            for (int k = 0; k < 2; k++)
            {
                var s = Players[k];
                if (o.Loser == k && o.Kind == RallyEnd.Down) { s.TinRun++; if (s.TinRun > s.MostTinsInARow) s.MostTinsInARow = s.TinRun; }
                else if (o.Loser == k || o.Winner == k) s.TinRun = 0;
            }
            _lastStriker = -1; _lastTech = null; _server = -1;
        }
        /// <summary>The match for you (athlete 0; online the one you played), summed up for the rewards and achievements (P2-M5).</summary>
        public Progression.MatchFacts Facts(MatchRunner r)
        {
            int h = r.HumanIndex;
            var me = Players[h];
            var them = Players[1 - h];
            var s = r.Settings;
            var last = r.Sim.LastOutcome;
            var f = new Progression.MatchFacts
            {
                Won = r.Sim.Score.MatchWinner == h, Career = s.Career, TwoPlayer = s.TwoPlayer, Online = s.Online, Ranked = s.Ranked,
                AgainstHard = !s.TwoPlayer && s.Opponent != "wallbot" && s.Difficulty >= (float)SquashSim.Career.Stars.Difficulty(85) - 1e-4f,   // P6: rated 85+
                AgainstNo1 = !s.TwoPlayer && !s.Online && SquashSim.Career.Stars.Find(s.Opponent) == SquashSim.Career.Stars.No1,
                GamesToWin = s.GamesToWin,
                RalliesWon = me.Rallies, Rallies = Players[0].Rallies + Players[1].Rallies, LongestRally = LongestRally,
                GreatHits = me.GreatHits, MostGreatHitsInAGame = me.MostGreatInAGame, Nicks = me.Nicks, WinningBoasts = me.WinningBoasts,
                Winners = me.Winners, Errors = me.Errors, Aces = me.Aces, TinsInARow = me.MostTinsInARow,
                Tins = me.Tins, Outs = me.Outs, NotUps = me.NotUps, Faults = me.Faults,
                Drives = me.Drives, Lobs = me.Lobs, Drops = me.Drops, Kills = me.Kills, Boasts = me.Boasts,
                CameBackFrom0_5 = me.CameBack, HitSelf = me.HitSelf, TopPaceKmh = me.TopPace * 3.6,
                TSeconds = me.TSeconds, Seconds = PlaySeconds,
                Lets = me.Lets, Strokes = me.Strokes, NoLets = me.NoLets,
                LetsAgainst = them.Lets, StrokesAgainst = them.Strokes, NoLetsAgainst = them.NoLets,
                WonAfterLet = me.WonAfterLet,
                MatchPointOnAStroke = r.Sim.Score.MatchWinner == h && last != null && last.Kind == RallyEnd.Stroke && last.Winner == h,
            };
            foreach (var g in Games) f.Games.Add(new[] { g[h], g[1 - h] });
            return f;
        }
    }
}
