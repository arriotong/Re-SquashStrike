// ===========================================================
// Assets/SquashGame/Runtime/MatchHud.cs
// The M2 match HUD, broadcast style (Phase 2 decision 6) and minimal,
// as Phase 1 requires - no aim cursor, no power meter:
//
//   score bug     names, points, games, who serves and from which box
//   call banner   the referee's call with its reason ("Out - side wall")
//   serve prompt  how to serve, when it is the player's serve
//   game over     result and how to continue
//   knock-up      whose half, time left, the ball warming (M5)
//   break         countdown to the next game, the ball cooling (M5)
//
// IMGUI is a stand-in; the Phase 2 UI is UI Toolkit. Reads state only.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class MatchHud : MonoBehaviour
    {
        public MatchRunner Runner;
        public string[] Names { get { return Runner.Names; } }

        GUIStyle _bug, _bugText, _bugPoints, _banner, _prompt, _small;
        Texture2D _dark, _accent, _serveDot;

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void EnsureStyles()
        {
            if (_bug != null) return;
            _dark = Tex(new Color(0.07f, 0.08f, 0.10f, 0.92f));
            _accent = Tex(new Color(0.85f, 0.22f, 0.18f, 1f));
            _serveDot = Tex(new Color(1f, 0.82f, 0.25f, 1f));
            _bug = new GUIStyle { normal = { background = _dark } };
            _bugText = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _bugText.normal.textColor = Color.white;
            _bugPoints = new GUIStyle(_bugText) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            _banner = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _banner.normal.textColor = Color.white;
            _banner.normal.background = _dark;
            _prompt = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _prompt.normal.textColor = new Color(0.92f, 0.94f, 0.97f);
            _prompt.normal.background = Tex(new Color(0.07f, 0.08f, 0.10f, 0.75f));
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _small.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
        }

        void OnGUI()
        {
            if (Runner == null || Runner.Sim == null || Runner.InSetup) return;
            EnsureStyles();
            MatchSimulation m = Runner.Sim;
            MatchScore s = m.Score;
            bool twoPlayer = Runner.Settings.TwoPlayer;

            if (Runner.Clock.Paused && Runner.DeviceLost == null && !Runner.ReplayActive)
            {
                GUI.Label(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 60, 520, 64), "PAUSED", _banner);
                GUI.Label(new Rect(Screen.width / 2f - 260, Screen.height / 2f + 10, 520, 30),
                    "Esc / Start: resume      M / View: quit to the menu", _prompt);
            }

            if (Runner.DeviceLost != null)
                GUI.Label(new Rect(Screen.width / 2f - 330, Screen.height / 2f - 30, 660, 60), Runner.DeviceLost, _banner);

            // ---------------- referee replay ----------------
            if (Runner.ReplayActive)
            {
                GUI.Label(new Rect(Screen.width / 2f - 330, 22, 660, 52), "REFEREE REVIEW   " + Runner.ReplayCaption, _banner);
                GUI.Label(new Rect(Screen.width / 2f - 200, 80, 400, 24), "replay " + Runner.ReplaySpeed.ToString("0.0") + "x   (Esc skips)", _prompt);
            }

            // ---------------- score bug (top left) ----------------
            float x = 16, y = 16, w = 300, row = 34;
            GUI.Box(new Rect(x, y, w, row * 2 + 22), GUIContent.none, _bug);
            GUI.DrawTexture(new Rect(x, y, 4, row * 2), _accent);
            for (int p = 0; p < 2; p++)
            {
                float ry = y + p * row;
                if (s.Server == p) GUI.DrawTexture(new Rect(x + 12, ry + row / 2 - 4, 8, 8), _serveDot);
                GUI.Label(new Rect(x + 28, ry, 170, row), Names[p], _bugText);
                if (s.GamesToWin > 1) GUI.Label(new Rect(x + w - 96, ry, 40, row), s.Games[p].ToString(), _bugPoints);
                GUI.Label(new Rect(x + w - 56, ry, 48, row), s.Points[p].ToString(), _bugPoints);
            }
            string box = s.ServeSide > 0 ? "R" : "L";
            string system = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints
                : "ENGLISH to " + s.GameTarget;
            GUI.Label(new Rect(x + 12, y + row * 2, w - 24, 20),
                system + "   serve: " + Names[s.Server] + " (" + box + " box)   ball " +
                BallTemperature.StateName(m.Ball.Temperature).ToLowerInvariant() + " " + m.Ball.Temperature.ToString("0") + " C", _small);

            // ---------------- knock-up ----------------
            if (m.Phase == MatchPhase.WarmUp)
            {
                GUI.Label(new Rect(Screen.width / 2f - 330, 22, 660, 52),
                    "KNOCK-UP   " + Names[m.WarmUpOwner] + "   " + Mathf.CeilToInt((float)m.WarmUpRemaining) + " s", _banner);
                GUI.Label(new Rect(Screen.width / 2f - 300, 80, 600, 24),
                    (m.WarmUpHalf == 0 ? "first half - " : "second half - ") + "warm the ball up.   Enter / Y: start the match", _prompt);
            }

            // ---------------- referee call ----------------
            if (m.Phase == MatchPhase.RallyOver && m.LastOutcome != null && !Runner.ReplayActive)
            {
                RallyOutcome o = m.LastOutcome;
                string call = o.Kind == RallyEnd.Let ? o.Reason : o.Reason + "   -   point " + Names[o.Winner];
                GUI.Label(new Rect(Screen.width / 2f - 200, 80, 400, 24), "V: replay", _prompt);
                GUI.Label(new Rect(Screen.width / 2f - 330, 22, 660, 52), call, _banner);
            }

            // ---------------- serve prompt ----------------
            if (m.Phase == MatchPhase.AwaitToss && (s.Server == Runner.HumanIndex || twoPlayer))
            {
                string who = twoPlayer ? Names[s.Server] + " to serve" : "Your serve";
                string hit = twoPlayer && s.Server == 1 ? "Hit (A)" : "Hit (LMB / A)";
                GUI.Label(new Rect(Screen.width / 2f - 300, Screen.height - 120, 600, 54),
                    who + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box.  " + hit + " to serve (one click).\n" +
                    "Aim up and across: above the service line, landing in the opposite back quarter.", _prompt);
            }

            // ---------------- game / match over ----------------
            if (m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver)
            {
                int wnr = s.GameWinner;
                string title = (m.Phase == MatchPhase.MatchOver ? "MATCH  " : "GAME  ") + Names[wnr] + "  " +
                               s.Points[wnr] + "-" + s.Points[1 - wnr];
                GUI.Label(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 60, 520, 64), title, _banner);
                if (m.Phase == MatchPhase.MatchOver && s.GamesToWin > 1)
                    GUI.Label(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 128, 520, 60),
                        "MATCH  " + Names[s.MatchWinner] + "  " + s.Games[s.MatchWinner] + "-" + s.Games[1 - s.MatchWinner], _banner);
                string next = m.Phase == MatchPhase.MatchOver ? "Enter / Y: back to match setup"
                    : "Break - next game in " + Mathf.CeilToInt((float)m.BreakRemaining) + " s   (games " + s.Games[0] + "-" + s.Games[1] +
                      ", the ball is cooling)   Enter / Y: play now";
                GUI.Label(new Rect(Screen.width / 2f - 300, Screen.height / 2f + 10, 600, 30), next, _prompt);
            }

            GUI.Label(new Rect(16, Screen.height - 48, 900, 20),
                twoPlayer
                ? "P1 (" + Runner.Input.DeviceLabel + ") vs P2 (" + (Runner.Input2 != null ? Runner.Input2.DeviceLabel : "pad") + ")    Move WASD / left stick   Aim mouse / right stick (high = lob, low = drop, low + charge = kill)   Hold LMB / A to charge, let go as the ball arrives   Dive Space / B   Sprint Shift / LB   Let RMB / Y   Pause Esc / Start"
                : "Move WASD / left stick   Aim mouse / right stick (high = lob, low = drop, low + charge = kill)   Hold LMB / A to charge, let go as the ball arrives   Dive Space / B   Sprint Shift / LB   Let RMB / Y   Pause Esc / Start",
                _small);
        }
    }
}
