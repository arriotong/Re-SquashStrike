// ===========================================================
// Assets/SquashGame/Runtime/Views/ArcadePopups.cs
// Arcade-style pop-up words: big, coloured, punchy, gone in a second.
//
//   strokes     HIT! / VOLLEY! / TOUCH! / SMASH! / SERVE!  (with the shot's
//               name underneath), where the ball was struck
//   the ball    NICK!
//   appeals     LET, PLEASE?  over the player asking
//   calls       LET! / NO LET! / STROKE!  centre screen
//   rally ends  DOWN! (TIN!) / OUT! / NOT UP! / FAULT! / FOOT FAULT! / OUCH!
//   games       GAME! / MATCH!  with who won
//
// Presentation only: listens to the runner's events and the match's
// RallyEnded / Appealed. Can be switched off on the setup panel.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class ArcadePopups : MonoBehaviour
    {
        public SimRunnerBase Runner;

        /// <summary>Player setting (setup panel), saved per machine.</summary>
        public static bool Enabled = true;

        public static void LoadSettings()
        {
            try { Enabled = PlayerPrefs.GetInt("ui.popups", 1) == 1; } catch (System.Exception) { }
        }

        public static void SaveSettings()
        {
            try { PlayerPrefs.SetInt("ui.popups", Enabled ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        // ---- palette ----
        static readonly Color Yellow = new Color(1f, 0.86f, 0.2f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.15f);
        static readonly Color Red = new Color(1f, 0.28f, 0.25f);
        static readonly Color Green = new Color(0.4f, 1f, 0.45f);
        static readonly Color Cyan = new Color(0.45f, 0.9f, 1f);
        static readonly Color Magenta = new Color(1f, 0.4f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.75f, 0.15f);
        static readonly Color White = new Color(0.97f, 0.97f, 1f);

        sealed class Pop
        {
            public string Text, Sub;
            public Color Colour;
            public bool World;          // anchored in the world (else in screen space, 0..1)
            public Vector3 Anchor;
            public float Age, Life, Size, Tilt;
        }

        readonly List<Pop> _pops = new List<Pop>();
        MatchSimulation _sim;
        MatchPhase _lastPhase;
        GUIStyle _main, _sub;

        /// <summary>Words popped so far (tests and diagnostics).</summary>
        public readonly List<string> Shown = new List<string>();

        public void Build(SimRunnerBase runner)
        {
            Runner = runner;
            LoadSettings();
            Runner.Stepped += OnStepped;
        }

        void OnDestroy()
        {
            if (Runner != null) Runner.Stepped -= OnStepped;
            Watch(null);
        }

        // ---------------- spawning ----------------

        void Spawn(string text, string sub, Color c, bool world, Vector3 anchor, float size, float life = 1.1f)
        {
            Shown.Add(text);
            if (Shown.Count > 200) Shown.RemoveAt(0);
            if (!Enabled) return;
            // Don't let world pops pile up on each other: nudge a new one above a fresh neighbour.
            if (world)
                foreach (var p in _pops)
                    if (p.World && p.Age < 0.35f && (p.Anchor - anchor).sqrMagnitude < 0.16f) anchor += Vector3.up * 0.25f;
            _pops.Add(new Pop
            {
                Text = text, Sub = sub, Colour = c, World = world, Anchor = anchor,
                Life = life, Size = size, Tilt = Random.Range(-7f, 7f)
            });
            if (_pops.Count > 12) _pops.RemoveAt(0);
        }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                Vector3 at = SimToWorld.Pos(e.Point) + Vector3.up * 0.35f;
                if (e.Type == SimEventType.RacketContact)
                {
                    string label = string.IsNullOrEmpty(e.Label) ? null : e.Label;
                    string tech = (e.Technique ?? "").ToLowerInvariant();
                    bool serve = _sim != null && _sim.Phase == MatchPhase.Rally && _sim.Referee.Shots == 1;
                    // Small and quick: a little "Hit!" by the ball, no shot name.
                    if (serve) Spawn("Serve!", null, Cyan, true, at, 0.42f, 0.7f);
                    else if (e.Stroke == "touch") Spawn("Touch!", null, Cyan, true, at, 0.42f, 0.7f);
                    else if (e.BallSpeedOut >= 42) Spawn("Smash!", null, Orange, true, at, 0.5f, 0.75f);
                    else if (tech.Contains("volley") || (label != null && label.Contains("VOLLEY"))) Spawn("Volley!", null, Green, true, at, 0.42f, 0.7f);
                    else Spawn("Hit!", null, Yellow, true, at, 0.42f, 0.7f);
                }
                else if (e.Type == SimEventType.Impact && e.Nick)
                {
                    Spawn("NICK!", null, Magenta, true, at, 0.7f, 1.1f);
                }
            }
        }

        void Watch(MatchSimulation sim)
        {
            if (_sim == sim) return;
            if (_sim != null) { _sim.RallyEnded -= OnRallyEnded; _sim.Appealed -= OnAppealed; }
            _sim = sim;
            if (_sim != null) { _sim.RallyEnded += OnRallyEnded; _sim.Appealed += OnAppealed; _lastPhase = _sim.Phase; }
        }

        string NameOf(int i)
        {
            var mr = Runner as MatchRunner;
            return mr != null && i >= 0 && i < mr.Names.Length ? mr.Names[i] : "P" + (i + 1);
        }

        void OnAppealed(int i)
        {
            if (Runner == null || i >= Runner.View.Count) return;
            Vector3 head = Runner.View.Athlete(i).Pos + Vector3.up * 2.15f;
            Spawn("Let, please?", null, White, true, head, 0.5f, 1.1f);
        }

        void OnRallyEnded(RallyOutcome o)
        {
            string point = o.Winner >= 0 ? "point " + NameOf(o.Winner) : null;
            Vector3 centre = new Vector3(0.5f, 0.36f, 0f);
            bool hasPoint = o.Point != null && (o.Point.X != 0 || o.Point.Y != 0 || o.Point.Z != 0);
            Vector3 where = hasPoint ? SimToWorld.Pos(o.Point) + Vector3.up * 0.4f
                          : (Runner != null ? Runner.View.BallPos + Vector3.up * 0.4f : Vector3.up);
            string reason = o.Reason ?? "";
            switch (o.Kind)
            {
                case RallyEnd.Let: Spawn("LET!", null, Yellow, false, centre, 0.85f, 1.3f); break;
                case RallyEnd.NoLet: Spawn("NO LET!", point, Red, false, centre, 0.85f, 1.3f); break;
                case RallyEnd.Stroke: Spawn("STROKE!", point, Gold, false, centre, 0.9f, 1.3f); break;
                case RallyEnd.Down: Spawn(reason.ToLowerInvariant().Contains("tin") ? "TIN!" : "DOWN!", null, Red, true, where, 0.65f, 1.1f); break;
                case RallyEnd.Out: Spawn("OUT!", null, Red, true, where, 0.65f, 1.1f); break;
                case RallyEnd.NotUp: Spawn("NOT UP!", null, Orange, true, where, 0.62f, 1.1f); break;
                case RallyEnd.Fault: Spawn("FAULT!", null, Orange, false, centre, 0.75f, 1.1f); break;
                case RallyEnd.FootFault: Spawn("FOOT FAULT!", null, Orange, false, centre, 0.7f, 1.1f); break;
                case RallyEnd.HitSelf: Spawn("OUCH!", null, Orange, true, where, 0.6f, 1.0f); break;
            }
        }

        // ---------------- per frame ----------------

        void Update()
        {
            var mr = Runner as MatchRunner;
            Watch(mr != null ? mr.Sim : null);
            if (_sim != null)
            {
                var ph = _sim.Phase;
                if (ph != _lastPhase && (ph == MatchPhase.GameOver || ph == MatchPhase.MatchOver))
                {
                    var s = _sim.Score;
                    int w = ph == MatchPhase.MatchOver && s.GamesToWin > 1 ? s.MatchWinner : s.GameWinner;
                    Spawn(ph == MatchPhase.MatchOver ? "MATCH!" : "GAME!", NameOf(w) + " wins", Gold, false, new Vector3(0.5f, 0.3f, 0f), 1.1f, 2.0f);
                }
                _lastPhase = ph;
            }
            float dt = Time.unscaledDeltaTime;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                _pops[i].Age += dt;
                if (_pops[i].Age >= _pops[i].Life) _pops.RemoveAt(i);
            }
        }

        void EnsureStyles()
        {
            if (_main != null) return;
            _main = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
        }

        static void Outlined(Rect r, string text, GUIStyle st, Color c, float alpha)
        {
            var outline = new Color(0.05f, 0.05f, 0.08f, 0.85f * alpha);
            st.normal.textColor = outline;
            for (int dx = -2; dx <= 2; dx += 2)
                for (int dy = -2; dy <= 2; dy += 2)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, st);
            c.a = alpha;
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }

        void OnGUI()
        {
            if (!Enabled || _pops.Count == 0) return;
            var mr = Runner as MatchRunner;
            if (mr != null && mr.InSetup) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();
            Matrix4x4 saved = GUI.matrix;
            foreach (var p in _pops)
            {
                float u = p.Age / p.Life;
                Vector2 c;
                if (p.World)
                {
                    Vector3 sp = cam.WorldToScreenPoint(p.Anchor + Vector3.up * (0.5f * u));
                    if (sp.z <= 0) continue;
                    c = new Vector2(sp.x, Screen.height - sp.y);
                }
                else c = new Vector2(p.Anchor.x * Screen.width, p.Anchor.y * Screen.height - 30f * u);

                // Punch in (overshoot), hold, fade out.
                float a = p.Age < 0.08f ? p.Age / 0.08f : 1f;
                float punch = p.Age < 0.15f ? Mathf.Lerp(1.3f, 1f, p.Age / 0.15f) + Mathf.Sin(p.Age / 0.15f * Mathf.PI) * 0.08f : 1f;
                float alpha = Mathf.Clamp01(a) * Mathf.Clamp01((1f - u) / 0.3f);
                float scale = p.Size * punch * Mathf.Clamp(Screen.height / 900f, 0.7f, 1.6f);

                GUI.matrix = saved * Matrix4x4.TRS(new Vector3(c.x, c.y, 0), Quaternion.Euler(0, 0, p.Tilt), new Vector3(scale, scale, 1));
                Outlined(new Rect(-200, -24, 400, 48), p.Text, _main, p.Colour, alpha);
                if (!string.IsNullOrEmpty(p.Sub)) Outlined(new Rect(-200, 20, 400, 22), p.Sub, _sub, White, alpha * 0.9f);
            }
            GUI.matrix = saved;
        }
    }
}
