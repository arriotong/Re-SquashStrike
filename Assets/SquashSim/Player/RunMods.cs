// ===========================================================
// Assets/SquashSim/Player/RunMods.cs
// P5 the Gauntlet roguelite: what a run's abilities do to an athlete in
// the sim. A run is a set of ability ids with levels (1..5); every effect
// is worked out here from the levels, so a replay only has to keep the
// pairs. Null on an athlete (every match outside the Gauntlet, online
// included) - and every hook skips its arithmetic when the level is 0 -
// so the game, the lab parity and the bundled replays are untouched.
//
//   shots      nick       kill: lower, wider (toward the nick), faster
//              vdrop      drop on the volley: lower and softer
//              trickle    boast: softer and lower (dies in the front)
//              hlob       lob: higher (deeper into the back)
//              rail       straight drive: tighter, closer to the wall
//              wide       cross drive: wider, a little faster
//              thunder    hard serve: faster
//              sniper     charged shots: tighter
//   powers     ace        an ace scores extra points
//              nickbank   a nick scores extra points
//              pressure   (on the opponent) they tire faster
//              momentum   3 rallies won in a row: drives and kills faster
//              vampire    a winner gives stamina back
//              second     the first fault each game is replayed
//   stats      reach      Long Arms: reach x(1 + 3% a level)
//              grace      Early Bird: the release grace x(1 + 12% a level)
//   evolved    exec       nicks score more again
//              ghost      once a rally, a ball out of reach is reached
//              clock      straight drives: no spread, no mishit
//              matador    after your drop or boast the opponent is slower
//   elite      e_cannon   drives and kills faster   (the AI's traits)
//              e_tireless tires at half the rate
//              e_sniper   tighter on every shot
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    [System.Serializable]
    public sealed class RunMod
    {
        public string Id;
        public int Level;
    }

    public sealed class RunMods
    {
        public const int MaxLevel = 5;
        readonly SortedDictionary<string, int> _lv = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        // ---- state the match keeps (not part of a replay's setup) ----
        /// <summary>Rallies won in a row (Momentum).</summary>
        public int Streak;
        /// <summary>Ghost spent this rally.</summary>
        public bool GhostUsed;
        /// <summary>Second Serve spent this game.</summary>
        public bool SecondUsed;
        /// <summary>Seconds left slowed (the opponent's Matador).</summary>
        public double SlowTimer;

        public int Level(string id) { return _lv.TryGetValue(id, out int l) ? l : 0; }
        public void Set(string id, int level) { if (level <= 0) _lv.Remove(id); else _lv[id] = level; }
        public bool Empty { get { return _lv.Count == 0; } }
        public IEnumerable<KeyValuePair<string, int>> All { get { return _lv; } }

        public static RunMods From(IEnumerable<RunMod> list)
        {
            if (list == null) return null;
            var m = new RunMods();
            foreach (var x in list) if (x != null && !string.IsNullOrEmpty(x.Id) && x.Level > 0) m.Set(x.Id, x.Level);
            return m.Empty ? null : m;
        }

        public List<RunMod> ToList()
        {
            var l = new List<RunMod>();
            foreach (var kv in _lv) l.Add(new RunMod { Id = kv.Key, Level = kv.Value });
            return l;
        }

        /// <summary>A fresh copy of the levels (a match's own state starts clean).</summary>
        public RunMods Clone() { var m = new RunMods(); foreach (var kv in _lv) m._lv[kv.Key] = kv.Value; return m; }

        // ---------------- shots (ArcadeStroke.Hit) ----------------

        /// <summary>Pace multiplier for a shot of this type (1 when nothing applies).</summary>
        public double Pace(string shot, bool straight, bool cross, double charge)
        {
            double k = 1;
            switch (shot)
            {
                case "kill":
                    k *= 1 + 0.02 * Level("nick");
                    k *= 1 + 0.15 * Level("e_cannon");
                    if (Streak >= 3) k *= 1 + 0.02 * Level("momentum");
                    break;
                case "drive":
                    if (cross) k *= 1 + 0.02 * Level("wide");
                    k *= 1 + 0.15 * Level("e_cannon");
                    if (Streak >= 3) k *= 1 + 0.02 * Level("momentum");
                    break;
                case "boast": k *= 1 - 0.07 * Level("trickle"); break;
            }
            return k;
        }

        /// <summary>Spread multiplier (aim error) for this shot.</summary>
        public double Spread(string shot, bool straight, double charge)
        {
            double k = 1;
            if (shot == "drive" && straight)
            {
                if (Level("clock") > 0) return 0;
                k *= 1 - 0.12 * Level("rail");
            }
            if (charge >= 0.5) k *= 1 - 0.12 * Level("sniper");
            if (Level("e_sniper") > 0) k *= 0.5;
            return k < 0 ? 0 : k;
        }

        /// <summary>Straight drives that never mishit (Clockwork).</summary>
        public bool NoMishit(string shot, bool straight) { return shot == "drive" && straight && Level("clock") > 0; }

        /// <summary>Kill height over the tin, as a share of the normal (Nick Hunter: lower).</summary>
        public double KillHeight { get { return 1 - 0.1 * Level("nick"); } }
        /// <summary>How far a kill's aim moves toward its side wall, m (Nick Hunter: toward the nick).</summary>
        public double KillWiden { get { return 0.08 * Level("nick"); } }
        /// <summary>Drop on the volley: height share and pace share.</summary>
        public double VolleyDropHeight { get { return 1 - 0.1 * Level("vdrop"); } }
        public double VolleyDropPace { get { return 1 - 0.05 * Level("vdrop"); } }
        /// <summary>Boast: the highest it may be aimed, as a share (Trickle: lower).</summary>
        public double BoastHeight { get { return 1 - 0.08 * Level("trickle"); } }
        /// <summary>Lob: extra height on the front wall, m (Hanging Lob).</summary>
        public double LobLift { get { return 0.12 * Level("hlob"); } }
        /// <summary>Straight drive: m closer to its side wall (Rail Drive).</summary>
        public double RailPull { get { return 0.04 * Level("rail"); } }
        /// <summary>Cross drive: m wider (Wide Cross).</summary>
        public double CrossWiden { get { return 0.07 * Level("wide"); } }
        /// <summary>Hard serve: the charge bonus share (Thunder Serve).</summary>
        public double ServeBonus { get { return 1 + 0.3 * Level("thunder"); } }

        /// <summary>Reach multiplier (Long Arms) and release-grace multiplier (Early Bird).</summary>
        public double ReachBonus { get { return 1 + 0.03 * Level("reach"); } }
        public double GraceBonus { get { return 1 + 0.12 * Level("grace"); } }

        /// <summary>Once a rally: reach this much further (Ghost).</summary>
        public double GhostReach { get { return Level("ghost") > 0 && !GhostUsed ? 1.6 : 1.0; } }

        // ---------------- the rally's end (MatchSimulation, Scoring) ----------------

        /// <summary>Extra points for an ace.</summary>
        public int AceBonus { get { int l = Level("ace"); return l == 0 ? 0 : (l >= 5 ? 2 : 1); } }
        /// <summary>Extra points for a nick.</summary>
        public int NickBonus { get { int l = Level("nickbank"); return (l == 0 ? 0 : (l >= 5 ? 2 : 1)) + (Level("exec") > 0 ? 1 : 0); } }
        /// <summary>Stamina back after a winner.</summary>
        public double VampireHeal { get { return 0.04 * Level("vampire"); } }
        /// <summary>The first fault of each game is replayed.</summary>
        public bool SecondServe { get { return Level("second") > 0; } }

        // ---------------- fatigue ----------------

        /// <summary>This athlete's own drain multiplier (an elite's Tireless).</summary>
        public double DrainSelf { get { return Level("e_tireless") > 0 ? 0.5 : 1.0; } }
        /// <summary>The drain this athlete puts on the opponent (Pressure).</summary>
        public double DrainOnOpponent { get { return 1 + 0.1 * Level("pressure"); } }

        /// <summary>Seconds the opponent is slowed after your drop or boast (Matador), and how slow.</summary>
        public double MatadorSeconds { get { return Level("matador") > 0 ? 0.45 : 0; } }
        public const double MatadorSpeed = 0.8;
    }
}
