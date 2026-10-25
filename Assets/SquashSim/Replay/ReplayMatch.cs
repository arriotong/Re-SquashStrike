// ===========================================================
// Assets/SquashSim/Replay/ReplayMatch.cs
// P2-M6 Replay Theatre: recording a match and playing it again.
//
//   ReplayMatch    builds a match from a setup - the config, the sim,
//                  the opponent, who plays the simple hitting system.
//                  The game's MatchRunner builds every match through it,
//                  so a replay is built exactly as the match was.
//   MatchRecorder  sits beside a live match: before each step it
//                  quantises each human's intent and hands the sim the
//                  decoded copy; after it, clears the live edges (as the
//                  sim did); logs commands and each rally's checksum
//   ReplaySim      plays a file headless, step by step (time-sliceable),
//                  checking every rally against its checksum
// ===========================================================

using System;
using System.Collections.Generic;

namespace SquashSim.Replays
{
    public static class ReplayMatch
    {
        /// <summary>The config a match runs on: the game profile, with its scoring, length and knock-up.</summary>
        public static void Configure(ReplaySetup s)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.UseGameProfile();          // the game's double yellow at PSA pace
            var m = PhysicsConfig.Current.Match;
            m.Scoring = s.Scoring;
            m.GamesToWin = Math.Max(1, s.GamesToWin);
            m.WarmUpEnabled = s.WarmUp;
            m.AutoNextGame = true;
            m.AppealsEnabled = s.Lets;               // P2-M7: the Lets setting, recorded with the match
            m.StartPoints0 = s.StartPoints0; m.StartPoints1 = s.StartPoints1; m.StartGames0 = s.StartGames0; m.StartGames1 = s.StartGames1;   // P3-C1
            m.StartServer = s.StartServer; m.EachGamePoints0 = s.EachGame0; m.EachGamePoints1 = s.EachGame1;
            if (s.ParPoints > 0) m.ParPoints = s.ParPoints;                    // P5: a Gauntlet floor's game to 7
        }

        /// <summary>The match itself (Configure first): the opponent in slot 1 unless two play; everyone plays the simple
        /// hitting system, aiming at the front wall - the AI too. The wall-bot keeps the physical swing.</summary>
        public static MatchSimulation Build(ReplaySetup s)
        {
            var sim = new MatchSimulation(s.FirstServer);
            if (s.Attr0 != null) sim.Players[0].Player.Attributes.CopyFrom(s.Attr0);       // P3-C1 (neutral: no effect)
            if (s.Attr1 != null) sim.Players[1].Player.Attributes.CopyFrom(s.Attr1);       // P5: a Gauntlet floor's opponent
            ApplyMods(sim, 0, s.Mods0); ApplyMods(sim, 1, s.Mods1);
            sim.Bots[1] = s.TwoPlayer ? null : Opponent(s);
            if (!string.IsNullOrEmpty(s.Opponent0))
                sim.Bots[0] = new OpponentBrain(AiProfile.ByName(s.Opponent0).WithDifficulty(s.Difficulty0), s.BotSeed ^ 0x9E3779B9u);
            bool p2Arcade = s.TwoPlayer || sim.Bots[1] is OpponentBrain;
            sim.Players[0].WallAimEnabled = true;
            sim.Players[1].WallAimEnabled = p2Arcade;
            sim.Players[0].ArcadeHitEnabled = true;
            sim.Players[1].ArcadeHitEnabled = p2Arcade;
            return sim;
        }

        /// <summary>P5: a Gauntlet run's abilities on athlete i (and its Pressure on the other). None: nothing at all.</summary>
        static void ApplyMods(MatchSimulation sim, int i, List<RunMod> list)
        {
            var mods = RunMods.From(list);
            if (mods == null) return;
            sim.Players[i].Player.Mods = mods;
            double p = mods.DrainOnOpponent;
            if (p != 1.0) sim.Players[1 - i].Player.OpponentDrain = p;
        }

        public static IAthleteBrain Opponent(ReplaySetup s)
        {
            if (s.Opponent == "wallbot") return new WallBot(s.BotSeed);
            return new OpponentBrain(AiProfile.ByName(s.Opponent).WithDifficulty(s.Difficulty), s.BotSeed);
        }

        /// <summary>The athletes a person plays (recorded): athlete 0, and athlete 1 when two play.</summary>
        public static bool Human(ReplaySetup s, int i) { return i == 0 ? string.IsNullOrEmpty(s.Opponent0) : s.TwoPlayer; }
    }

    /// <summary>Watches a match step by step for what happens in each rally: when its serve began, the fastest shot,
    /// nicks, great hits, dives, the last striker. The recorder and the replay both use it.</summary>
    public sealed class RallyWatch
    {
        MatchPhase _phase = (MatchPhase)(-1);
        long _serve;
        double _top; int _topBy = -1, _g0, _g1, _dives, _last = -1; bool _nick; string _tech = "";
        readonly int[] _diveCount = new int[2];

        /// <summary>After each step.</summary>
        public void Observe(MatchSimulation m)
        {
            if (m.Phase != _phase)
            {
                if (m.Phase == MatchPhase.AwaitToss) { _serve = m.StepCount; _top = 0; _topBy = -1; _g0 = _g1 = _dives = 0; _last = -1; _nick = false; _tech = ""; }
                _phase = m.Phase;
            }
            var ev = m.StepEvents;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.Type == SimEventType.RacketContact && e.PlayerIndex >= 0 && e.PlayerIndex <= 1)
                {
                    if (e.BallSpeedOut > _top) { _top = e.BallSpeedOut; _topBy = e.PlayerIndex; }
                    if (m.Referee.Shots > 1 && ArcadeStroke.IsGreat(e)) { if (e.PlayerIndex == 0) _g0++; else _g1++; }
                    _last = e.PlayerIndex; _tech = e.Technique ?? "";
                }
                else if (e.Type == SimEventType.Impact && e.Nick && _last >= 0) _nick = true;
            }
            for (int k = 0; k < 2; k++)
            {
                if (!m.Players[k].ArcadeHitEnabled) continue;
                int d = m.Players[k].Arcade.DiveCount;
                if (d > _diveCount[k]) _dives += d - _diveCount[k];
                _diveCount[k] = d;
            }
        }

        /// <summary>The rally just ended: what happened in it.</summary>
        public void Fill(RallyRec r)
        {
            r.ServeStep = _serve; r.TopPace = _top; r.TopPaceBy = _topBy; r.Nick = _nick;
            r.Great0 = _g0; r.Great1 = _g1; r.Dives = _dives; r.LastStriker = _last; r.LastTech = _tech;
        }
    }

    /// <summary>An AI playing *as a person*: it only steers through the intent. (OpponentBrain also sets its athlete's
    /// shot pace - a person's athlete keeps its own, so that is put back.) Tests and the game's autopilot use it.</summary>
    public sealed class PersonStandIn : IAthleteBrain
    {
        readonly IAthleteBrain _inner;
        public PersonStandIn(IAthleteBrain inner) { _inner = inner; }
        public void Fill(InputIntent intent, MatchSimulation m, int me, double dt)
        {
            var p = m.Players[me].Player;
            double pace = p.PaceScale;
            _inner.Fill(intent, m, me, dt);
            p.PaceScale = pace;
        }
    }

    public sealed class MatchRecorder
    {
        public readonly ReplayFile File = new ReplayFile();
        readonly MatchSimulation _sim;
        readonly InputIntent[] _live = new InputIntent[2], _fed = new InputIntent[2];
        readonly QFrame[] _last = new QFrame[2];
        readonly RallyWatch _watch = new RallyWatch();
        bool _rallyEnded;

        /// <summary>The match could not be recorded faithfully (an AI drove a human's athlete: a test's autopilot).</summary>
        public bool Invalid { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Record this match. <paramref name="live"/>: the intents the people steer (by athlete; null for the AI).
        /// The sim is handed its own copies from now on.</summary>
        public MatchRecorder(MatchSimulation sim, ReplaySetup setup, InputIntent[] live, string gameVersion, long createdTicks)
        {
            _sim = sim;
            File.Setup = setup;
            File.GameVersion = gameVersion ?? "";
            File.Fingerprint = SimFingerprint.Current;
            File.CreatedTicks = createdTicks;
            for (int i = 0; i < 2; i++)
            {
                if (!ReplayMatch.Human(setup, i) || live[i] == null) continue;
                _live[i] = live[i];
                _fed[i] = new InputIntent();
                sim.Intents[i] = _fed[i];
                File.Tracks[i] = new List<FrameRun>();
            }
            sim.RallyEnded += o => _rallyEnded = true;
        }

        /// <summary>The intent athlete i's person steers (the live one, not the sim's copy).</summary>
        public InputIntent Live(int i) { return _live[i]; }

        /// <summary>Just before the sim steps: quantise each person's input, feed the sim the decoded copy, keep the frame.</summary>
        public void BeforeStep()
        {
            if (Finished) return;
            for (int i = 0; i < 2; i++)
            {
                if (_live[i] == null) continue;
                if (_sim.Bots[i] != null) Invalid = true;
                var q = IntentCodec.Encode(_live[i], _sim.Players[i].Arcade.Assisted, _sim.Players[i].Arcade.ShotLines);
                IntentCodec.Decode(q, _fed[i]);
                var track = File.Tracks[i];
                if (track.Count > 0 && _last[i].Equals(q)) { var r = track[track.Count - 1]; r.Count++; track[track.Count - 1] = r; }
                else { track.Add(new FrameRun { Frame = q, Count = 1 }); _last[i] = q; }
            }
        }

        /// <summary>Just after: the edges were used (the sim cleared its copy's - clear the live ones too); a rally's checksum.</summary>
        public void AfterStep()
        {
            if (Finished) return;
            for (int i = 0; i < 2; i++) if (_live[i] != null) _live[i].ClearEdges();
            _watch.Observe(_sim);
            if (_rallyEnded) { _rallyEnded = false; var r = RallyRec.Of(_sim); _watch.Fill(r); File.Rallies.Add(r); }
            if (_sim.Phase == MatchPhase.MatchOver) Finish(false);
        }

        /// <summary>The front end did something to the sim between steps.</summary>
        public void Command(ReplayCommand kind) { if (!Finished) File.Commands.Add(new ReplayCmd { Step = _sim.StepCount, Kind = kind }); }

        /// <summary>The end: the match is over, or was left (retired / quit).</summary>
        public void Finish(bool left)
        {
            if (Finished) return;
            Finished = true;
            File.TotalSteps = _sim.StepCount;
            File.Winner = _sim.Score.MatchWinner;
            File.Retired = left && _sim.Phase != MatchPhase.MatchOver;
        }
    }

    public sealed class ReplaySim
    {
        public readonly ReplayFile File;
        public readonly MatchSimulation Sim;
        readonly double _dt;
        readonly InputIntent[] _fed = new InputIntent[2];
        readonly int[] _run = new int[2], _left = new int[2];
        int _cmd, _rally;
        bool _rallyEnded;
        /// <summary>What happens in each rally, as it plays (the builder reads it).</summary>
        public readonly RallyWatch Watch = new RallyWatch();

        /// <summary>The first rally that did not match its checksum (-1: none so far).</summary>
        public int FirstMismatch { get; private set; } = -1;
        public bool Diverged { get { return FirstMismatch >= 0; } }
        /// <summary>Rallies played so far.</summary>
        public int RalliesDone { get { return _rally; } }
        public bool Done { get { return Sim.StepCount >= File.TotalSteps; } }
        /// <summary>A rally ended: its index (in the file's order).</summary>
        public event Action<int> RallyFinished;

        /// <summary>Build the match again (this sets PhysicsConfig.Current, as a live match does).</summary>
        public ReplaySim(ReplayFile file)
        {
            File = file;
            ReplayMatch.Configure(file.Setup);
            Sim = ReplayMatch.Build(file.Setup);
            _dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int i = 0; i < 2; i++)
            {
                if (file.Tracks[i] == null) continue;
                _fed[i] = new InputIntent();
                Sim.Intents[i] = _fed[i];
                _left[i] = file.Tracks[i].Count > 0 ? file.Tracks[i][0].Count : 0;
            }
            Sim.RallyEnded += o => _rallyEnded = true;
        }

        /// <summary>One fixed step, exactly as the live match took it.</summary>
        public void Step()
        {
            if (Done) return;
            while (_cmd < File.Commands.Count && File.Commands[_cmd].Step <= Sim.StepCount)
            {
                var c = File.Commands[_cmd++];
                if (c.Kind == ReplayCommand.NextGame && Sim.Phase == MatchPhase.GameOver) Sim.NextGame();
                else if (c.Kind == ReplayCommand.SkipWarmUp && Sim.Phase == MatchPhase.WarmUp) Sim.SkipWarmUp();
            }
            for (int i = 0; i < 2; i++)
            {
                var track = File.Tracks[i];
                if (track == null || _run[i] >= track.Count) continue;
                var q = track[_run[i]].Frame;
                IntentCodec.Decode(q, _fed[i]);
                Sim.Players[i].Arcade.Assisted = q.Assisted;
                Sim.Players[i].Arcade.ShotLines = q.Lines;       // as the person played
                if (--_left[i] <= 0) { _run[i]++; _left[i] = _run[i] < track.Count ? track[_run[i]].Count : 0; }
            }
            Sim.Step(_dt);
            Watch.Observe(Sim);
            if (_rallyEnded)
            {
                _rallyEnded = false;
                var now = RallyRec.Of(Sim);
                if (FirstMismatch < 0 && (_rally >= File.Rallies.Count || !now.SameAs(File.Rallies[_rally]))) FirstMismatch = _rally;
                int index = _rally++;
                RallyFinished?.Invoke(index);
            }
        }

        /// <summary>Up to n steps (a time slice); true when the replay is done.</summary>
        public bool Run(int steps) { for (int i = 0; i < steps && !Done; i++) Step(); return Done; }

        public sealed class Verdict
        {
            public bool SameFingerprint, AllRallies, SameResult;
            public int FirstMismatch, Rallies;
            public bool Ok { get { return AllRallies && SameResult; } }
            public override string ToString()
            {
                return Ok ? "identical: " + Rallies + " rallies" : "diverged at rally " + (FirstMismatch + 1) + (SameFingerprint ? "" : " (another version's fingerprint)");
            }
        }

        /// <summary>Play a whole file and compare it to its record (the exit test; later the online verifier).</summary>
        public static Verdict Verify(ReplayFile file)
        {
            var keep = PhysicsConfig.Current;
            try
            {
                var r = new ReplaySim(file);
                while (!r.Done) r.Step();
                bool sameResult = r.Sim.StepCount == file.TotalSteps && (file.Retired || r.Sim.Score.MatchWinner == file.Winner);
                return new Verdict
                {
                    SameFingerprint = file.Fingerprint == SimFingerprint.Current,
                    AllRallies = !r.Diverged && r.RalliesDone == file.Rallies.Count,
                    SameResult = sameResult,
                    FirstMismatch = r.FirstMismatch, Rallies = r.RalliesDone,
                };
            }
            finally { PhysicsConfig.Current = keep; }
        }
    }
}
