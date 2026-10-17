// ===========================================================
// Assets/SquashGame/Runtime/MatchSetupPanel.cs
// The M5 pre-match panel: who you play (an AI personality and its
// difficulty, or a second local player), the match length, the scoring
// and the knock-up. Replaces the M3 F6/F7 debug keys.
//
//   Navigate  W/S, arrows, D-pad or left stick (any pad)
//   Change    A/D, arrows, D-pad left/right
//   Start     Enter / Space / pad A / pad Start
//   Practice  the warm-up court (one athlete, the feed machine)
//   Quit      leave the game
//
// IMGUI is a stand-in for the Phase 2 UI Toolkit front end. Reads and
// writes MatchSettings only; MatchRunner.BeginMatch starts the match.
// ===========================================================

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SquashGame
{
    public sealed class MatchSetupPanel : MonoBehaviour
    {
        public MatchRunner Runner;

        public static readonly string[] OpponentLabels = { "Retriever", "Attacker", "Shot-maker", "Grinder", "Wall-bot", "Player 2 (local)" };
        public static readonly string[] DifficultyLabels = { "Easy", "Medium", "Hard" };
        public static readonly float[] DifficultyValues = { 0.2f, 0.5f, 0.85f };
        public static readonly string[] LengthLabels = { "One game", "Best of 3", "Best of 5" };
        public static readonly int[] LengthGames = { 1, 2, 3 };
        public static readonly string[] ScoringLabels = { "PAR-11", "English (to 9)" };
        public static readonly string[] ScoringValues = { "par", "english" };
        public static readonly string[] WarmUpLabels = { "On", "Off" };
        public static readonly string[] SensitivityLabels = { "Slow", "Calm", "Normal", "Quick", "Fast" };
        public static readonly string[] InvertLabels = { "Off", "On" };

        public const int RowOpponent = 0, RowDifficulty = 1, RowLength = 2, RowScoring = 3, RowWarmUp = 4,
                         RowMouse = 5, RowInvert = 6, RowStart = 7, RowPractice = 8, RowQuit = 9, Rows = 10;
        public const string PracticeScene = "WarmUpCourt";

        // Current choice per row (indices into the label arrays).
        public int Opponent, Difficulty = 1, Length = 2, Scoring, WarmUp;
        public int Selected { get; private set; } = RowStart;

        GUIStyle _title, _row, _rowSel, _value, _hint, _panel, _start, _startSel;
        float _repeatAt;
        int _heldDir;

        public bool TwoPlayer { get { return Opponent == OpponentLabels.Length - 1; } }

        public MatchSettings CurrentSettings()
        {
            return new MatchSettings
            {
                TwoPlayer = TwoPlayer,
                Opponent = TwoPlayer ? "retriever" : MatchRunner.Opponents[Opponent],
                Difficulty = DifficultyValues[Difficulty],
                GamesToWin = LengthGames[Length],
                Scoring = ScoringValues[Scoring],
                WarmUp = WarmUp == 0
            };
        }

        int Count(int row)
        {
            switch (row)
            {
                case RowOpponent: return OpponentLabels.Length;
                case RowDifficulty: return DifficultyLabels.Length;
                case RowLength: return LengthLabels.Length;
                case RowScoring: return ScoringLabels.Length;
                case RowWarmUp: return WarmUpLabels.Length;
                case RowMouse: return SensitivityLabels.Length;
                case RowInvert: return InvertLabels.Length;
                default: return 1;
            }
        }

        bool RowEnabled(int row) { return !(row == RowDifficulty && (TwoPlayer || Opponent == 4)); }

        /// <summary>Change the value of a row by d steps (wraps). The Player 2 option is skipped with no pad connected.</summary>
        public void Change(int row, int d)
        {
            int n = Count(row);
            switch (row)
            {
                case RowOpponent:
                    Opponent = ((Opponent + d) % n + n) % n;
                    if (TwoPlayer && !MatchRunner.CanPlayTwoPlayer) Opponent = ((Opponent + d) % n + n) % n;
                    break;
                case RowDifficulty: Difficulty = ((Difficulty + d) % n + n) % n; break;
                case RowLength: Length = ((Length + d) % n + n) % n; break;
                case RowScoring: Scoring = ((Scoring + d) % n + n) % n; break;
                case RowWarmUp: WarmUp = ((WarmUp + d) % n + n) % n; break;
                case RowMouse:
                    IntentSource.MouseSensitivityScale = IntentSource.SensitivitySteps[(SensitivityIndex() + d + n) % n];
                    IntentSource.SaveSettings();
                    break;
                case RowInvert:
                    IntentSource.InvertMouseY = !IntentSource.InvertMouseY;
                    IntentSource.SaveSettings();
                    break;
            }
        }

        /// <summary>The sensitivity step closest to the saved multiplier.</summary>
        static int SensitivityIndex()
        {
            int best = 2; float bestD = float.MaxValue;
            for (int i = 0; i < IntentSource.SensitivitySteps.Length; i++)
            {
                float dd = Mathf.Abs(IntentSource.SensitivitySteps[i] - IntentSource.MouseSensitivityScale);
                if (dd < bestD) { bestD = dd; best = i; }
            }
            return best;
        }

        public void Move(int d)
        {
            for (int k = 0; k < Rows; k++)
            {
                Selected = ((Selected + d) % Rows + Rows) % Rows;
                if (RowEnabled(Selected)) return;
            }
        }

        public void StartMatch() { Runner.BeginMatch(CurrentSettings()); }

        public void Practice() { SceneManager.LoadScene(PracticeScene); }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Enter / A on the selected row: the three action rows act, a setting row starts the match.</summary>
        public void Activate()
        {
            if (Selected == RowPractice) Practice();
            else if (Selected == RowQuit) Quit();
            else StartMatch();
        }

        // ---------------- input (any keyboard, any pad) ----------------

        void Update()
        {
            if (Runner == null || !Runner.InSetup) return;
            if (TwoPlayer && !MatchRunner.CanPlayTwoPlayer) Opponent = 0;     // pad unplugged on the panel

            int v = 0, h = 0;
            bool confirm = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) v = -1;
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) v = 1;
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) h = -1;
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) h = 1;
                confirm |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            }
            int stickDir = 0;
            foreach (var pad in Gamepad.all)
            {
                if (pad.dpad.up.wasPressedThisFrame) v = -1;
                if (pad.dpad.down.wasPressedThisFrame) v = 1;
                if (pad.dpad.left.wasPressedThisFrame) h = -1;
                if (pad.dpad.right.wasPressedThisFrame) h = 1;
                confirm |= pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
                Vector2 s = pad.leftStick.ReadValue();
                if (s.y > 0.6f) stickDir = -1; else if (s.y < -0.6f) stickDir = 1;
                else if (s.x < -0.6f) stickDir = -2; else if (s.x > 0.6f) stickDir = 2;
            }
            // The stick steps once when pushed, then repeats while held.
            if (stickDir != 0 && (stickDir != _heldDir || Time.unscaledTime >= _repeatAt))
            {
                _repeatAt = Time.unscaledTime + (stickDir != _heldDir ? 0.4f : 0.15f);
                if (stickDir == -1 || stickDir == 1) v = stickDir; else h = stickDir / 2;
            }
            _heldDir = stickDir;

            if (v != 0) Move(v);
            if (h != 0 && Selected < RowStart) Change(Selected, h);
            if (confirm) Activate();
        }

        // ---------------- drawing ----------------

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void EnsureStyles()
        {
            if (_title != null) return;
            _panel = new GUIStyle { normal = { background = Tex(new Color(0.07f, 0.08f, 0.10f, 0.94f)) } };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;
            _row = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 8, 0, 0) };
            _row.normal.textColor = new Color(0.72f, 0.76f, 0.82f);
            _rowSel = new GUIStyle(_row) { fontStyle = FontStyle.Bold };
            _rowSel.normal.textColor = Color.white;
            _rowSel.normal.background = Tex(new Color(0.85f, 0.22f, 0.18f, 0.85f));
            _value = new GUIStyle(_row) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(8, 16, 0, 0) };
            _value.normal.textColor = Color.white;
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _hint.normal.textColor = new Color(0.70f, 0.74f, 0.80f);
            _start = new GUIStyle(_row) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            _start.normal.textColor = Color.white;
            _start.normal.background = Tex(new Color(0.18f, 0.20f, 0.24f, 1f));
            _startSel = new GUIStyle(_start);
            _startSel.normal.background = Tex(new Color(0.85f, 0.22f, 0.18f, 1f));
        }

        void OnGUI()
        {
            if (Runner == null || !Runner.InSetup) return;
            EnsureStyles();
            float w = 560, rowH = 42, h = 120 + rowH * RowStart + 60 + 46 + 70;
            var r = new Rect(Screen.width / 2f - w / 2, Screen.height / 2f - h / 2, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(new Rect(r.x, r.y + 18, w, 44), "re:SquashStrike - MATCH", _title);

            string[] names = { "Opponent", "Difficulty", "Match", "Scoring", "Knock-up", "Mouse aim", "Invert mouse Y" };
            string[] values =
            {
                OpponentLabels[Opponent] + (Opponent == 5 && !MatchRunner.CanPlayTwoPlayer ? " (connect a pad)" : ""),
                RowEnabled(RowDifficulty) ? DifficultyLabels[Difficulty] : "-",
                LengthLabels[Length], ScoringLabels[Scoring], WarmUpLabels[WarmUp],
                SensitivityLabels[SensitivityIndex()], InvertLabels[IntentSource.InvertMouseY ? 1 : 0]
            };
            float y = r.y + 84;
            for (int i = 0; i < names.Length; i++)
            {
                var rr = new Rect(r.x + 20, y, w - 40, rowH - 4);
                bool sel = Selected == i;
                GUI.enabled = RowEnabled(i);
                if (GUI.Button(rr, names[i], sel ? _rowSel : _row)) { Selected = i; Change(i, 1); }
                GUI.Label(rr, (sel ? "<  " : "") + values[i] + (sel ? "  >" : ""), _value);
                GUI.enabled = true;
                y += rowH;
            }
            y += 10;
            if (GUI.Button(new Rect(r.x + 120, y, w - 240, 50), "START", Selected == RowStart ? _startSel : _start)) StartMatch();
            y += 58;
            float half = (w - 240 - 10) / 2;
            if (GUI.Button(new Rect(r.x + 120, y, half, 38), "PRACTICE", Selected == RowPractice ? _startSel : _start)) Practice();
            if (GUI.Button(new Rect(r.x + 130 + half, y, half, 38), "QUIT", Selected == RowQuit ? _startSel : _start)) Quit();
            y += 50;

            string who;
            if (TwoPlayer)
            {
                bool twoPads = Gamepad.all.Count >= 2;
                who = "Player 1: " + (twoPads ? "pad 1 (or keyboard + mouse)" : "keyboard + mouse") +
                      "      Player 2: " + (twoPads ? "pad 2" : "pad 1");
            }
            else who = "You: keyboard + mouse or any pad";
            GUI.Label(new Rect(r.x + 16, y, w - 32, 22), who, _hint);
            GUI.Label(new Rect(r.x + 16, y + 22, w - 32, 40),
                "Up/Down choose   Left/Right change   Enter / A start.   In the match: Enter / Y skips the knock-up or a break.", _hint);
        }
    }
}
