// ===========================================================
// Assets/SquashGame/Runtime/UI/Screens.cs
// P2-M1: the front end's screens.
//
//   Title, Settings (Controls, Video, Audio, Accessibility, Gameplay),
//   Controls reference, Practice pause, Confirm.
//
// The main menu hub, the versus screens, the match pause and full time
// are in HubScreens.cs (UI redesign U2); Target Arcade, the Academy,
// the Career and the Locker Room have their own screen files.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    // ---------------- title ----------------

    public sealed class TitleScreen : MenuScreen
    {
        protected override string Title { get { return "re:SquashStrike"; } }
        protected override string RootClass { get { return "fe-screen--title"; } }
        protected override bool Sidebar { get { return false; } }
        protected override string Hint { get { return "Enter / A   start"; } }
        protected override void Build()
        {
            // the old intro's logo: outlined SQUASH, glowing lime STRIKE
            Content.style.alignItems = Align.Center;
            Text("re:", "fe-logo-re");
            Text("SQUASH", "fe-logo-squash");
            Text("STRIKE", "fe-logo-strike");
            Text("Real squash physics. Simple to play.", "fe-press");
            Add(new ButtonItem("START", () => Fe.Replace(new MainMenuScreen()), true));
        }
        public override void Back() { }                                   // nowhere to go back to

        // ---- P2-M6: real rallies behind the logo - your pinned match (or the latest), else a bundled one ----
        Replays.TheatreSession _attract;
        int _source;
        /// <summary>What plays behind the logo now (tests).</summary>
        public Replays.TheatreSession Attract { get { return _attract; } }

        public override void OnShow() { base.OnShow(); StartAttract(); }
        public override void OnHide() { StopAttract(); }

        void StartAttract()
        {
            if (Fe.Match == null) return;
            var f = Pick(out var entry);
            if (f == null) return;
            try { _attract = Replays.TheatreSession.Open(entry, f, Fe.Match); _attract.PlayReel(); }
            catch (Exception e) { Debug.LogWarning("[Title] no highlights: " + e.Message); _attract = null; }
        }

        void StopAttract() { if (_attract != null) { _attract.Close(); _attract = null; } }

        SquashSim.Replays.ReplayFile Pick(out Replays.ReplayEntry entry)
        {
            entry = null;
            if (_source == 0)
            {
                var lib = Replays.ReplayLibrary.Entries;
                entry = lib.Find(x => x.Pinned && x.SameVersion) ?? lib.Find(x => x.SameVersion && x.Tagged > 0);
                if (entry != null) { try { return Replays.ReplayLibrary.Load(entry); } catch { entry = null; } }
            }
            var bundled = Resources.LoadAll<TextAsset>("Replays");
            if (bundled.Length == 0) return null;
            var t = bundled[(_source + (int)(Time.realtimeSinceStartup * 7)) % bundled.Length];
            try { return SquashSim.Replays.ReplayFile.FromBytes(t.bytes); } catch { return null; }
        }

        public override void Tick(float dt)
        {
            var a = _attract;
            if (a == null) return;
            a.Tick(dt);
            if (a.Reel != null && !a.Playing && a.Reel.Count > 0 && a.Builder.Clips[a.Reel[0]].Ready) a.PlayReel(a.Reel);   // its first rally is ready now
            if (a.Playing && a.Paused) { StopAttract(); _source++; StartAttract(); }                                     // the reel is over: the next
        }
    }

    // ---------------- match setup ----------------

    public static class MatchChoices
    {
        public static readonly string[] OpponentLabels = { "Retriever", "Attacker", "Shot-maker", "Grinder", "Wall-bot" };
        public static readonly string[] DifficultyLabels = { "Easy", "Medium", "Hard" };
        /// <summary>Balanced with MatchBench --balance (human-style model wins ~55 / 44 / 29% of rallies).</summary>
        public static readonly float[] DifficultyValues = System.Array.ConvertAll(SquashSim.GameTuning.Ai.Levels, v => (float)v);
        public static readonly string[] LengthLabels = { "One game", "Best of 3", "Best of 5" };
        public static readonly int[] LengthGames = { 1, 2, 3 };
        public static readonly string[] ScoringLabels = { "PAR-11", "English (to 9)" };
        public static readonly string[] ScoringValues = { "par", "english" };
        public static readonly string[] ControlLabels = { "Non-assisted", "Assisted" };
    }

    // ---------------- settings (UI redesign U5: one screen, five tabs) ----------------

    /// <summary>Every setting, by tab: big selectors with a line on what each does; live previews where they help.
    /// Every change applies at once and is saved on leaving.</summary>
    public sealed class SettingsScreen : GameScreen
    {
        static GameSettings S { get { return ProfileStore.Current.Settings; } }
        protected override string Kicker { get { return "SETTINGS"; } }
        protected override string Title { get { return "Settings"; } }
        protected override bool ShowChip { get { return false; } }
        public override bool Dims { get { return Fe.Match == null || !Fe.Match.Demo; } }
        readonly List<Vector2Int> _sizes = new List<Vector2Int>();
        VisualElement _ball, _ring;
        Label _call;

        protected override void Compose()
        {
            // ---- CONTROLS ----
            var c = Panel("CONTROLS");
            Row(c, new SelectorItem("Control mode", MatchChoices.ControlLabels, () => S.Assisted ? 1 : 0, i => { S.Assisted = i == 1; Changed(false); }),
                "Both: aim at your half of the front wall for straight, across for cross-court - the shot plays its line. Assisted also keeps drives off the side wall, crosses from rattling and serves deep and legal.");
            Row(c, new SelectorItem("Aim speed", new[] { "Slowest", "Slow", "Normal", "Fast", "Fastest" }, () => Nearest(IntentSource.SensitivitySteps, S.AimSpeed),
                i => { S.AimSpeed = IntentSource.SensitivitySteps[i]; Changed(false); }, true), "How fast the ring moves with the mouse or the right stick.");
            Row(c, new SelectorItem("Invert mouse Y", new[] { "Off", "On" }, () => S.InvertMouseY ? 1 : 0, i => { S.InvertMouseY = i == 1; Changed(false); }), "Mouse up moves the ring down.");
            AddTo(c, new ActionItem("View the controls", () => Fe.Push(new ControlsHelpScreen()), "▶"));

            // ---- VIDEO ----
            var v = Panel("VIDEO");
            foreach (var r in Screen.resolutions) { var s = new Vector2Int(r.width, r.height); if (!_sizes.Contains(s)) _sizes.Add(s); }
            var names = new List<string> { "Current" };
            foreach (var s in _sizes) names.Add(s.x + " x " + s.y);
            Row(v, new CarouselItem("Resolution", names.ToArray(),
                () => { int i = _sizes.IndexOf(new Vector2Int(S.ResolutionW, S.ResolutionH)); return i < 0 ? 0 : i + 1; },
                i => { if (i == 0) { S.ResolutionW = 0; S.ResolutionH = 0; } else { S.ResolutionW = _sizes[i - 1].x; S.ResolutionH = _sizes[i - 1].y; } Changed(true); }),
                "The game's size. \"Current\" keeps the desktop's.");
            Row(v, new SelectorItem("Display", new[] { "Borderless", "Fullscreen", "Windowed" }, () => S.DisplayMode, i => { S.DisplayMode = i; Changed(true); }), "Borderless switches windows fastest.");
            Row(v, new SelectorItem("VSync", new[] { "Off", "On" }, () => S.VSync ? 1 : 0, i => { S.VSync = i == 1; Changed(true); }), "On: no tearing, a little more input delay.");
            var caps = new[] { 0, 60, 120, 144, 240 };
            Row(v, new SelectorItem("Frame cap", new[] { "Off", "60", "120", "144", "240" }, () => Math.Max(0, Array.IndexOf(caps, S.FrameCap)), i => { S.FrameCap = caps[i]; Changed(true); }), "The most frames a second (with VSync off).");
            var q = new List<string> { "Default" }; q.AddRange(QualitySettings.names);
            Row(v, new CarouselItem("Quality", q.ToArray(), () => S.Quality + 1, i => { S.Quality = i - 1; Changed(true); }), "Shadows and smoothing. The ball plays the same at every setting.");

            // ---- AUDIO ----
            var a = Panel("AUDIO");
            Row(a, Volume("Master", () => S.MasterVolume, x => S.MasterVolume = x), "Everything.");
            Row(a, Volume("Sound effects", () => S.SfxVolume, x => S.SfxVolume = x), "The ball, the walls, the shoes, the menus.");
            Row(a, Volume("Referee", () => S.RefereeVolume, x => S.RefereeVolume = x), "The calls.");

            // ---- ACCESSIBILITY ----
            var acc = Panel("ACCESSIBILITY");
            float[] balls = { 0.85f, 1f, 1.2f, 1.4f }, calls = { 1f, 1.2f, 1.4f };
            Row(acc, new SelectorItem("Ball size", new[] { "Small", "Normal", "Large", "Larger" }, () => Nearest(balls, S.BallScale), i => { S.BallScale = balls[i]; Changed(false); }),
                "Only how the ball is drawn - the ball in play is the same for everyone.");
            Row(acc, new SelectorItem("Ring colour", new[] { "White", "Yellow", "Cyan" }, () => S.RingColour, i => { S.RingColour = i; Changed(false); }), "The aim ring on the wall.");
            Row(acc, new SelectorItem("Call text", new[] { "Normal", "Large", "Larger" }, () => Nearest(calls, S.CallTextScale), i => { S.CallTextScale = calls[i]; Changed(false); }), "The referee's calls on screen.");
            var pv = Kit.Panel(acc.parent, "PREVIEW"); pv.AddToClassList("k-set-preview");
            var pvRow = Kit.Box(pv, "k-set-pv-row");
            var ballBox = Kit.Box(pvRow, "k-set-pv-box"); ballBox.AddToClassList("k-set-pv-box--ball"); _ball = Kit.Box(ballBox, "k-set-ball"); Kit.L(ballBox, "BALL", "k-cr-tile-n");
            var ringBox = Kit.Box(pvRow, "k-set-pv-box"); _ring = Kit.Box(ringBox, "k-set-ring"); Kit.L(ringBox, "RING", "k-cr-tile-n");
            _call = Kit.L(pv, "NOT UP   —   point YOU", "k-set-call");

            // ---- GAMEPLAY ----
            var g = Panel("GAMEPLAY");
            Row(g, new SelectorItem("\"Hit!\" pop-ups", new[] { "Off", "On" }, () => S.Popups ? 1 : 0, i => { S.Popups = i == 1; Changed(false); }), "The word over a good contact. The ring's flash stays either way.");
            Row(g, new SelectorItem("Let calls", new[] { "Off", "On" }, () => S.Lets ? 1 : 0, i => { S.Lets = i == 1; Changed(false); }), "On: ask for a let (Q / Y) when you are blocked, and the referee calls lets and strokes. Off: no let calls - play through. From your next match.");
        }

        VisualElement Panel(string tab)
        {
            var page = Kit.Box(Page(tab), "k-set-page");
            var p = Kit.Panel(page, tab); p.AddToClassList("k-set-panel");
            return p;
        }

        void Row(VisualElement panel, MenuItem item, string hint)
        {
            var box = Kit.Box(panel, "k-set-row");
            AddTo(box, item);
            Kit.L(box, hint, "k-set-hint");
        }

        static SelectorItem Volume(string label, Func<float> get, Action<float> set)
        {
            var steps = new string[11]; for (int i = 0; i <= 10; i++) steps[i] = (i * 10) + "%";
            return new SelectorItem(label, steps, () => Mathf.Clamp(Mathf.RoundToInt(get() * 10), 0, 10), i => { set(i / 10f); ProfileStore.Apply(S, false); }, true);
        }

        static int Nearest(float[] values, float v) { int best = 0; for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i; return best; }

        static void Changed(bool video) { ProfileStore.Apply(S, video); }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Tabs")); }

        public override void Tick(float dt)
        {
            if (_ball == null) return;
            float d = 44f * S.BallScale;
            _ball.style.width = d; _ball.style.height = d; _ball.style.borderTopLeftRadius = _ball.style.borderTopRightRadius = _ball.style.borderBottomLeftRadius = _ball.style.borderBottomRightRadius = d * 0.5f;
            var rc = S.RingColour == 1 ? new Color(1f, 0.85f, 0.2f) : S.RingColour == 2 ? new Color(0.3f, 0.9f, 1f) : Color.white;
            _ring.style.borderTopColor = _ring.style.borderBottomColor = _ring.style.borderLeftColor = _ring.style.borderRightColor = rc;
            _call.style.fontSize = 28 * S.CallTextScale;
        }

        public override void OnHide() { ProfileStore.Save(); }
    }

    // ---------------- controls reference ----------------

    public sealed class ControlsHelpScreen : GameScreen
    {
        protected override string Kicker { get { return "HOW TO PLAY"; } }
        protected override string Title { get { return "Controls"; } }
        protected override bool ShowChip { get { return false; } }
        public override bool Dims { get { return Fe.Match == null || !Fe.Match.Demo; } }
        static readonly string[,] Rows =
        {
            { "Move", "WASD", "left stick" },
            { "Aim", "mouse", "right stick" },
            { "Hit", "hold LMB, let go as it arrives", "hold A, let go" },
            { "Height picks the shot", "high = lob, middle = drive, low = drop", "" },
            { "Kill", "aim low and charge", "" },
            { "Boast", "tap E", "RB" },
            { "Dive", "Space", "B" },
            { "Sprint", "Shift", "LB" },
            { "Serve", "tap: lob serve, hold: hard serve", "" },
            { "Ask for a let", "Q (when LET? shows)", "Y" },
            { "Replay the last call", "V", "" },
            { "Pause", "Esc", "Start" },
        };
        protected override void Compose()
        {
            var grid = Kit.Box(Content, "k-ctl-grid");
            for (int i = 0; i < Rows.GetLength(0); i++)
            {
                var row = Kit.Box(grid, "k-ctl");
                Kit.L(row, Rows[i, 0], "k-ctl-action");
                var keys = Kit.Box(row, "k-ctl-keys");
                Kit.L(keys, Rows[i, 1], "k-ctl-key");
                if (Rows[i, 2].Length > 0) { Kit.L(keys, "·", "k-ctl-or"); var pad = Kit.L(keys, Rows[i, 2], "k-ctl-key"); pad.AddToClassList("k-ctl-key--pad"); }
            }
            Kit.L(Content, "Get set early and let go on time: the ring flashes on a great hit.", "k-cr-note");
        }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.B, "Back")); }
    }

    // ---------------- pause (the practice court; the match: HubScreens.cs) ----------------

    public sealed class PracticePauseScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-side"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Hint { get { return null; } }
        protected override string Kicker { get { return "PRACTICE COURT"; } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            Add(new ActionItem("Resume", () => Fe.Resume(), "▶"));
            Add(new ActionItem("Coach tray", () => Fe.Push(new CoachScreen())));
            Add(new ActionItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ActionItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ActionItem("Quit to menu", () => Fe.QuitToMenu()));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Resume"));
            Root.Add(prompts);
        }
        public override void Back() { Fe.Resume(); }
    }

    // ---------------- confirm ----------------

    /// <summary>A small centred panel: the question, a line, two choices side by side (yes has the focus).</summary>
    public abstract class DialogScreen : MenuScreen
    {
        protected virtual string YesLabel { get { return "Yes"; } }
        protected virtual string NoLabel { get { return "No"; } }
        protected virtual string Line { get { return null; } }
        /// <summary>The yes choice.</summary>
        protected abstract void OnYes();
        ActionItem _yesItem;
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-confirm"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Hint { get { return null; } }
        protected override void Build()
        {
            if (!string.IsNullOrEmpty(Line)) Kit.L(Content, Line, "k-confirm-text");
            var row = Kit.Box(Content, "k-confirm-row");
            AddTo(row, new ActionItem(NoLabel, () => Back()));
            _yesItem = AddTo(row, new ActionItem(YesLabel, OnYes, "▶"));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--confirm");
            prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, NoLabel));
            Root.Add(prompts);
        }
        public override void OnShow() { base.OnShow(); if (_yesItem != null) Focus(_yesItem); }
    }

    public sealed class ConfirmScreen : DialogScreen
    {
        readonly string _question;
        readonly Action _yes;
        public ConfirmScreen(string question, Action yes) { _question = question; _yes = yes; }
        protected override string Title { get { return _question; } }
        protected override void OnYes() { Fe.Pop(); _yes(); }
    }
}
