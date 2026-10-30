// ===========================================================
// Assets/SquashGame/Runtime/Profile/Playtest.cs
// The playtest recorder (2026-10-30: friends play, the numbers come back
// - how the game really feels, not the bench's guesses). LOCAL only: a
// file a session in the save folder's "playtest" folder, one JSON object
// a line. No names, nothing sent anywhere; the player sends the files.
//
//   session   the version, the controls, the graphics; quit: where
//   screen    every screen opened
//   match     the mode, the opponent, the court; its end: the result
//   shot      each of yours: the shot, the contact, and where the ball
//             died - its first bounce, a side wall before it, its second
//   rally     who won it, how many shots, how long, how it ended
//   felt_bad  F8 (a pad's right stick click): "that felt wrong" - with the
//             last shot and the score
//   coins     earned (why) and spent (on what)
//   gauntlet  a floor won or lost, a run's end
//
// Tools/Playtest/report.py sums the files up.
// ===========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SquashSim;
using UnityEngine;

namespace SquashGame.Profile
{
    public static class Playtest
    {
        /// <summary>Recording (off in the tests unless they ask: their files would pile up).</summary>
        public static bool Enabled = !Application.isBatchMode;
        public static string Folder { get { return Path.Combine(ProfileStore.Folder ?? Application.persistentDataPath, "playtest"); } }
        /// <summary>This session's file (null until the first line).</summary>
        public static string FilePath { get; private set; }
        public static string LastScreen { get; private set; }
        static StreamWriter _w;
        static float _t0 = -1;

        /// <summary>One line: the event, the seconds into the session, and its fields.</summary>
        public static void Log(string ev, params (string key, object value)[] fields)
        {
            if (!Enabled) return;
            try
            {
                if (_w == null) Open();
                var sb = new StringBuilder(160);
                sb.Append("{\"ev\":").Append(Str(ev)).Append(",\"t\":").Append(Num(Time.realtimeSinceStartup - _t0));
                foreach (var (k, v) in fields) sb.Append(',').Append(Str(k)).Append(':').Append(Val(v));
                sb.Append('}');
                _w.WriteLine(sb.ToString());
                _w.Flush();
            }
            catch (Exception e) { Debug.LogWarning("[Playtest] " + e.Message); Enabled = false; }
        }

        static void Open()
        {
            Directory.CreateDirectory(Folder);
            FilePath = Path.Combine(Folder, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
            _w = new StreamWriter(FilePath, true, new UTF8Encoding(false));
            _t0 = Time.realtimeSinceStartup;
            var s = ProfileStore.Current.Settings;
            _w.WriteLine("{\"ev\":\"session\",\"t\":0,\"version\":" + Str(Application.version) + ",\"date\":" + Str(DateTime.Now.ToString("s", CultureInfo.InvariantCulture)) +
                         ",\"assisted\":" + Val(s.Assisted) + ",\"graphics\":" + s.Graphics + ",\"camera\":" + s.Camera + ",\"os\":" + Str(SystemInfo.operatingSystem) + "}");
            Application.quitting += () => { Log("quit", ("screen", LastScreen)); Close(); };
        }

        public static void Close() { if (_w != null) { _w.Dispose(); _w = null; } }

        /// <summary>A screen opened (FrontEnd).</summary>
        public static void Screen(object screen)
        {
            if (screen == null) return;
            LastScreen = screen.GetType().Name;
            Log("screen", ("name", LastScreen));
        }

        static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        static string Num(double d) { return double.IsNaN(d) || double.IsInfinity(d) ? "null" : Math.Round(d, 3).ToString(CultureInfo.InvariantCulture); }

        static string Val(object v)
        {
            switch (v)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case float f: return Num(f);
                case double d: return Num(d);
                default: return Str(v.ToString());
            }
        }
    }

    /// <summary>A match's shots and rallies into the playtest file (FrontEnd attaches it next to MatchStats).</summary>
    public sealed class PlaytestMatch
    {
        MatchRunner _runner;
        MatchSimulation _sim;
        bool _started, _ended;
        double _rallyStart;
        // your shot in flight: what it was, then where it went
        bool _open;
        string _tech;
        double _quality, _bx, _bz, _pace, _x1, _z1 = -1, _x2, _z2 = -1;
        bool _front, _sideFirst, _serve;
        int _floors;
        string _lastShot = "";

        public void Attach(MatchRunner runner) { _runner = runner; _runner.Stepped += OnStepped; }
        public void Detach() { if (_runner != null) _runner.Stepped -= OnStepped; Watch(null); }

        bool Counting { get { return _runner != null && !_runner.Demo && !_runner.InSetup && _runner.Puzzle == null; } }

        void Watch(MatchSimulation sim)
        {
            if (_sim == sim) return;
            if (_sim != null) _sim.RallyEnded -= OnRally;
            _sim = sim;
            _started = false; _ended = false;                                  // a new match
            if (_sim != null) _sim.RallyEnded += OnRally;
        }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            Watch(_runner.Sim);
            if (!Counting || _sim == null || _ended) return;
            int me = _runner.HumanIndex;
            if (!_started)
            {
                _started = true;
                var st = _runner.Settings;
                string mode = st.Online ? "online" : st.Career ? "career" : st.Gauntlet ? "gauntlet" : st.Scenario != null ? "scenario" : st.TwoPlayer ? "versus" : "quick";
                Playtest.Log("match", ("mode", mode), ("opponent", st.Opponent), ("difficulty", st.Difficulty), ("court", st.Court), ("games", st.GamesToWin), ("assisted", IntentSource.Assisted));
            }
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == SimEventType.RacketContact)
                {
                    CloseShot(null);
                    if (e.PlayerIndex != me) continue;
                    _open = true; _tech = e.Technique ?? e.Stroke; _quality = e.ArcadeQuality; _pace = e.BallSpeedOut;
                    _bx = e.Point.X; _bz = e.Point.Z; _serve = _sim.Referee.Shots <= 1;
                    _front = false; _sideFirst = false; _floors = 0; _z1 = -1; _z2 = -1;
                }
                else if (e.Type == SimEventType.Impact && _open)
                {
                    if (e.SurfaceClass == "frontWall") _front = true;
                    else if (_front && e.SurfaceClass == "sideWall" && _floors == 0) _sideFirst = true;
                    else if (_front && e.SurfaceClass == "floor")
                    {
                        _floors++;
                        if (_floors == 1) { _x1 = e.Point.X; _z1 = e.Point.Z; }
                        else if (_floors == 2) { _x2 = e.Point.X; _z2 = e.Point.Z; }
                    }
                }
            }
            if (_sim.Phase == MatchPhase.MatchOver && _started)
            {
                var sc = _sim.Score;
                Playtest.Log("match_end", ("won", sc.MatchWinner == me), ("games", sc.Games[me] + "-" + sc.Games[1 - me]), ("seconds", _sim.Time));
                _ended = true;                                                // (once - until the next match)
            }
        }

        /// <summary>Your shot's line: where the ball died (after the opponent's next contact, or the rally's end).</summary>
        void CloseShot(RallyOutcome o)
        {
            if (!_open) return;
            _open = false;
            double ex = _z2 > 0 ? _x2 : _x1, ez = _z2 > 0 ? _z2 : _z1;
            string where = !_front ? "down" : ez < 0 ? "returned" : ez >= 7.0 && Math.Abs(ex) > 1.0 ? "back_corner" : ez >= 7.0 ? "back_middle" : Math.Abs(ex) <= 1.6 ? "middle" : ez < 4.5 ? "front" : "side";
            if (_z1 < 0 && _front) where = "returned";                        // played before its bounce (a volley return)
            _lastShot = _tech + " " + where + (_sideFirst ? " (side wall first)" : "");
            Playtest.Log("shot", ("shot", _tech), ("serve", _serve), ("quality", _quality), ("pace", _pace), ("from_x", _bx), ("from_z", _bz),
                         ("bounce_x", _z1 >= 0 ? (object)_x1 : null), ("bounce_z", _z1 >= 0 ? (object)_z1 : null), ("end_x", ez >= 0 ? (object)ex : null), ("end_z", ez >= 0 ? (object)ez : null),
                         ("side_first", _sideFirst), ("where", where), ("rally_over", o != null), ("won_it", o != null && o.Winner == _runner.HumanIndex));
        }

        void OnRally(RallyOutcome o)
        {
            if (!Counting) return;
            CloseShot(o);
            int me = _runner.HumanIndex;
            Playtest.Log("rally", ("won", o.Winner == me), ("shots", _sim.Referee.Shots), ("seconds", _sim.Time - _rallyStart), ("end", o.Kind.ToString()), ("reason", o.Reason),
                         ("score", _sim.Score.Points[me] + "-" + _sim.Score.Points[1 - me]));
            _rallyStart = _sim.Time;
        }

        /// <summary>"That felt wrong" (F8 / the right stick click): the moment, with the last shot and the score.</summary>
        public void FeltBad()
        {
            if (_sim == null || !Counting) { Playtest.Log("felt_bad", ("where", Playtest.LastScreen)); return; }
            int me = _runner.HumanIndex;
            Playtest.Log("felt_bad", ("in_match", true), ("last_shot", _lastShot), ("phase", _sim.Phase.ToString()), ("score", _sim.Score.Points[me] + "-" + _sim.Score.Points[1 - me]),
                         ("ball_x", _sim.Ball.Pos.X), ("ball_z", _sim.Ball.Pos.Z), ("you_x", _sim.Players[me].Player.Pos.X), ("you_z", _sim.Players[me].Player.Pos.Z));
        }
    }
}
