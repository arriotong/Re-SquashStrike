// ===========================================================
// Assets/SquashGame/Runtime/UI/Screens.cs
// P2-M1: the front end's screens.
//
//   Title, Main menu, Quick Match, Local Versus, Settings (Controls,
//   Video, Audio, Accessibility, Gameplay), Controls reference,
//   Pause, Practice pause, Confirm, Post-match.
//
// Replays is on the main menu as a "coming soon" row (P2-M6). Target
// Arcade (P2-M2), the Academy (P2-M3), the Career (P2-M4) and the
// Locker Room (P2-M5) have their own screen files.
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
    }

    // ---------------- main menu ----------------

    /// <summary>The old SquashStrike's home: the brand, a column of slanted buttons and, on the right, a glowing
    /// preview of the one in focus over the slow-motion rally (MenuCinematics picks the camera angles).</summary>
    public sealed class MainMenuScreen : MenuScreen
    {
        protected override string Title { get { return ""; } }
        protected override string RootClass { get { return "fe-screen--home"; } }
        static string Soon() { return "coming soon"; }

        static readonly System.Collections.Generic.Dictionary<string, string> Blurbs = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Quick Match", "Jump straight onto the glass court. Pick your opponent, the difficulty and the length, and play." },
            { "Local Versus", "Two players, one screen: pad and keyboard, or two pads." },
            { "Career", "Climb the world tour, from club events to the World Finals." },
            { "Target Arcade", "Just you, the feeder and the targets. Chase high scores and three stars on every level." },
            { "Practice Court", "Free hitting off the feed machine, and drills for length, drops and boasts." },
            { "Academy", "The controls, then the rules of squash - one short lesson at a time." },
            { "Locker Room", "Kit, rackets and celebrations, earned by playing." },
            { "Replays", "Your best rallies again, slowed down, from any angle." },
            { "Settings", "Controls, video, audio and accessibility." },
            { "Quit", "Leave re:SquashStrike." },
        };

        readonly VisualElement _preview = new VisualElement();
        readonly Label _previewTitle = new Label(), _previewDesc = new Label();
        MenuItem _shown;

        /// <summary>The preview's heading (tests).</summary>
        public string PreviewTitle { get { return _previewTitle.text; } }

        protected override void Build()
        {
            var brand = new VisualElement(); brand.AddToClassList("fe-brand");
            foreach (var (text, cls) in new[] { ("re:", "fe-brand-re"), ("SQUASH", "fe-brand-squash"), ("STRIKE", "fe-brand-strike") })
            { var l = new Label(text); l.AddToClassList(cls); brand.Add(l); }
            Content.Add(brand);

            Add(new ButtonItem("Quick Match", () => Fe.Push(new QuickMatchScreen()), true));
            Add(new ButtonItem("Local Versus", () => Fe.Push(new LocalVersusScreen()), false,
                () => MatchRunner.CanPlayTwoPlayer ? "" : "connect a pad"));
            Add(new ButtonItem("Career", () => Fe.Push(new CareerStartScreen()), false,
                () => SquashGame.Profile.CareerStore.Exists ? "season " + SquashGame.Profile.CareerStore.Current.S.Season : ""));
            Add(new ButtonItem("Target Arcade", () => Fe.Push(new TargetArcadeScreen())));
            Add(new ButtonItem("Practice Court", () => Fe.Push(new PracticeScreen())));
            Add(new ButtonItem("Academy", () => Fe.Push(new AcademyScreen()), false, AcademyScreen.Summary));
            Add(new ButtonItem("Locker Room", () => Fe.Push(new LockerRoomScreen()), false,
                () => "level " + Progression.Rewards.LevelOf(SquashGame.Profile.ProfileStore.Current.Progress.Xp)));
            Add(new ButtonItem("Replays", null, false, Soon)).Enabled = () => false;
            Add(new ButtonItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ButtonItem("Quit", () => Fe.Push(new ConfirmScreen("Quit re:SquashStrike?", FrontEnd.QuitGame))));

            _preview.AddToClassList("home-preview");
            _preview.pickingMode = PickingMode.Ignore;
            _previewTitle.AddToClassList("home-preview-title");
            _previewDesc.AddToClassList("home-preview-desc");
            _preview.Add(_previewTitle); _preview.Add(_previewDesc);
            Root.Add(_preview);
        }

        void ShowPreview()
        {
            var f = Focused;
            if (f == _shown) return;
            _shown = f;
            _previewTitle.text = f != null ? f.Name.ToUpperInvariant() : "";
            _previewDesc.text = f != null && Blurbs.TryGetValue(f.Name, out var d) ? d : "";
            MenuCinematics.Scene = f != null ? Items.IndexOf(f) : 0;
        }

        public override void OnShow() { base.OnShow(); _shown = null; ShowPreview(); }
        public override void Back() { }

        /// <summary>P2-M3: a new player is pointed to the Academy, once.</summary>
        public override void Tick(float dt)
        {
            ShowPreview();
            var a = ProfileStore.Current.Academy;
            if (a.Suggested) return;
            a.Suggested = true;
            ProfileStore.Save();
            if (a.Done.Count == 0) Fe.Push(new AcademySuggestScreen());
        }
    }

    // ---------------- match setup ----------------

    public static class MatchChoices
    {
        public static readonly string[] OpponentLabels = { "Retriever", "Attacker", "Shot-maker", "Grinder", "Wall-bot" };
        public static readonly string[] DifficultyLabels = { "Easy", "Medium", "Hard" };
        /// <summary>Balanced with MatchBench --balance (human-style model wins ~55 / 44 / 29% of rallies).</summary>
        public static readonly float[] DifficultyValues = { 0.0f, 0.12f, 0.42f };
        public static readonly string[] LengthLabels = { "One game", "Best of 3", "Best of 5" };
        public static readonly int[] LengthGames = { 1, 2, 3 };
        public static readonly string[] ScoringLabels = { "PAR-11", "English (to 9)" };
        public static readonly string[] ScoringValues = { "par", "english" };
        public static readonly string[] ControlLabels = { "Non-assisted", "Assisted" };
    }

    public sealed class QuickMatchScreen : MenuScreen
    {
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Quick Match"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }
        static GameSettings S { get { return ProfileStore.Current.Settings; } }

        protected override void Build()
        {
            Add(new OptionItem("Opponent", () => MatchChoices.OpponentLabels[C.Opponent],
                d => C.Opponent = OptionItem.Step(C.Opponent, d, MatchChoices.OpponentLabels.Length)));
            Add(new OptionItem("Difficulty", () => C.Opponent == 4 ? "-" : MatchChoices.DifficultyLabels[C.Difficulty],
                d => C.Difficulty = OptionItem.Step(C.Difficulty, d, MatchChoices.DifficultyLabels.Length))).Enabled = () => C.Opponent != 4;
            Add(new OptionItem("Match", () => MatchChoices.LengthLabels[C.Length],
                d => C.Length = OptionItem.Step(C.Length, d, MatchChoices.LengthLabels.Length)));
            Add(new OptionItem("Scoring", () => MatchChoices.ScoringLabels[C.Scoring],
                d => C.Scoring = OptionItem.Step(C.Scoring, d, MatchChoices.ScoringLabels.Length)));
            Add(new OptionItem("Controls", () => MatchChoices.ControlLabels[S.Assisted ? 1 : 0],
                d => { S.Assisted = !S.Assisted; ProfileStore.Apply(S, false); }));
            Add(new ButtonItem("START", Start, true));
        }

        void Start()
        {
            ProfileStore.Save();
            Fe.StartMatch(new MatchSettings
            {
                TwoPlayer = false,
                Opponent = MatchRunner.Opponents[C.Opponent],
                Difficulty = MatchChoices.DifficultyValues[C.Difficulty],
                GamesToWin = MatchChoices.LengthGames[C.Length],
                Scoring = MatchChoices.ScoringValues[C.Scoring],
                WarmUp = false
            });
        }

        public override void OnHide() { ProfileStore.Save(); }
    }

    public sealed class LocalVersusScreen : MenuScreen
    {
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Local Versus"; } }
        static MenuChoices C { get { return ProfileStore.Current.Choices; } }
        Label _devices;

        protected override void Build()
        {
            _devices = Text("");
            Add(new OptionItem("Match", () => MatchChoices.LengthLabels[C.VsLength],
                d => C.VsLength = OptionItem.Step(C.VsLength, d, MatchChoices.LengthLabels.Length)));
            Add(new OptionItem("Scoring", () => MatchChoices.ScoringLabels[C.VsScoring],
                d => C.VsScoring = OptionItem.Step(C.VsScoring, d, MatchChoices.ScoringLabels.Length)));
            var start = Add(new ButtonItem("START", Start, true, () => MatchRunner.CanPlayTwoPlayer ? "" : "connect a pad"));
            start.Enabled = () => MatchRunner.CanPlayTwoPlayer;
        }

        public override void Tick(float dt)
        {
            int pads = UnityEngine.InputSystem.Gamepad.all.Count;
            string t = pads >= 2 ? "Player 1: keyboard & mouse or pad 1     Player 2: pad 2"
                     : pads == 1 ? "Player 1: keyboard & mouse     Player 2: the pad"
                     : "Player 2 needs a pad - connect one to play.";
            if (_devices.text != t) { _devices.text = t; foreach (var i in Items) i.Refresh(); }
        }

        void Start()
        {
            ProfileStore.Save();
            Fe.StartMatch(new MatchSettings
            {
                TwoPlayer = true,
                GamesToWin = MatchChoices.LengthGames[C.VsLength],
                Scoring = MatchChoices.ScoringValues[C.VsScoring],
                WarmUp = false
            });
        }

        public override void OnHide() { ProfileStore.Save(); }
    }

    // ---------------- settings ----------------

    public sealed class SettingsScreen : MenuScreen
    {
        protected override string Kicker { get { return "SETTINGS"; } }
        protected override string Title { get { return "Settings"; } }
        public override bool Dims { get { return Fe.Match == null || !Fe.Match.Demo; } }
        protected override void Build()
        {
            Add(new ButtonItem("Controls", () => Fe.Push(new ControlSettingsScreen())));
            Add(new ButtonItem("Video", () => Fe.Push(new VideoSettingsScreen())));
            Add(new ButtonItem("Audio", () => Fe.Push(new AudioSettingsScreen())));
            Add(new ButtonItem("Accessibility", () => Fe.Push(new AccessibilitySettingsScreen())));
            Add(new ButtonItem("Gameplay", () => Fe.Push(new GameplaySettingsScreen())));
            Add(new ButtonItem("Back", () => Fe.Pop()));
        }
        public override void OnHide() { ProfileStore.Save(); }
    }

    /// <summary>A settings page: every change applies at once and is saved on leaving.</summary>
    public abstract class SettingsPage : MenuScreen
    {
        protected static GameSettings S { get { return ProfileStore.Current.Settings; } }
        protected override string Kicker { get { return "SETTINGS"; } }
        public override bool Dims { get { return Fe.Match == null || !Fe.Match.Demo; } }
        protected virtual bool AppliesVideo { get { return false; } }
        protected void Changed() { ProfileStore.Apply(S, AppliesVideo); }
        protected OptionItem Choice(string label, string[] names, Func<int> get, Action<int> set)
        {
            return Add(new OptionItem(label, () => names[Mathf.Clamp(get(), 0, names.Length - 1)],
                d => { set(OptionItem.Step(get(), d, names.Length)); Changed(); }));
        }
        protected OptionItem OnOff(string label, Func<bool> get, Action<bool> set)
        {
            return Add(new OptionItem(label, () => get() ? "On" : "Off", d => { set(!get()); Changed(); }));
        }
        protected OptionItem Steps(string label, float[] values, string[] names, Func<float> get, Action<float> set)
        {
            Func<int> idx = () =>
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < values.Length; i++) { float d = Mathf.Abs(values[i] - get()); if (d < bd) { bd = d; best = i; } }
                return best;
            };
            return Add(new OptionItem(label, () => names[idx()], d => { set(values[OptionItem.Step(idx(), d, values.Length)]); Changed(); }));
        }
        protected OptionItem Percent(string label, Func<float> get, Action<float> set)
        {
            var v = new float[11]; var n = new string[11];
            for (int i = 0; i <= 10; i++) { v[i] = i / 10f; n[i] = (i * 10) + "%"; }
            return Steps(label, v, n, get, set);
        }
        protected void BackRow() { Add(new ButtonItem("Back", () => Fe.Pop())); }
        public override void OnHide() { ProfileStore.Save(); }
    }

    public sealed class ControlSettingsScreen : SettingsPage
    {
        protected override string Title { get { return "Controls"; } }
        protected override void Build()
        {
            Choice("Control mode", MatchChoices.ControlLabels, () => S.Assisted ? 1 : 0, i => S.Assisted = i == 1);
            Text("Assisted: straight drives stay off the side wall, cross-courts don't rattle, serves land deep and legal.");
            Steps("Aim speed", IntentSource.SensitivitySteps, new[] { "Slowest", "Slow", "Normal", "Fast", "Fastest" },
                () => S.AimSpeed, v => S.AimSpeed = v);
            OnOff("Invert mouse Y", () => S.InvertMouseY, v => S.InvertMouseY = v);
            Add(new ButtonItem("View controls", () => Fe.Push(new ControlsHelpScreen())));
            BackRow();
        }
    }

    public sealed class VideoSettingsScreen : SettingsPage
    {
        protected override string Title { get { return "Video"; } }
        protected override bool AppliesVideo { get { return true; } }
        readonly List<Vector2Int> _sizes = new List<Vector2Int>();
        protected override void Build()
        {
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!_sizes.Contains(v)) _sizes.Add(v);
            }
            var names = new List<string> { "Current" };
            foreach (var v in _sizes) names.Add(v.x + " x " + v.y);
            Choice("Resolution", names.ToArray(),
                () => { int i = _sizes.IndexOf(new Vector2Int(S.ResolutionW, S.ResolutionH)); return i < 0 ? 0 : i + 1; },
                i => { if (i == 0) { S.ResolutionW = 0; S.ResolutionH = 0; } else { S.ResolutionW = _sizes[i - 1].x; S.ResolutionH = _sizes[i - 1].y; } });
            Choice("Display", new[] { "Borderless", "Fullscreen", "Windowed" }, () => S.DisplayMode, i => S.DisplayMode = i);
            OnOff("VSync", () => S.VSync, v => S.VSync = v);
            var caps = new[] { 0, 60, 120, 144, 240 };
            Choice("Frame cap", new[] { "Off", "60", "120", "144", "240" }, () => Math.Max(0, Array.IndexOf(caps, S.FrameCap)), i => S.FrameCap = caps[i]);
            var q = new List<string> { "Default" }; q.AddRange(QualitySettings.names);
            Choice("Quality", q.ToArray(), () => S.Quality + 1, i => S.Quality = i - 1);
            BackRow();
        }
    }

    public sealed class AudioSettingsScreen : SettingsPage
    {
        protected override string Title { get { return "Audio"; } }
        protected override void Build()
        {
            Percent("Master", () => S.MasterVolume, v => S.MasterVolume = v);
            Percent("Sound effects", () => S.SfxVolume, v => S.SfxVolume = v);
            Percent("Referee", () => S.RefereeVolume, v => S.RefereeVolume = v);
            BackRow();
        }
    }

    public sealed class AccessibilitySettingsScreen : SettingsPage
    {
        protected override string Title { get { return "Accessibility"; } }
        protected override void Build()
        {
            Steps("Ball size", new[] { 0.85f, 1f, 1.2f, 1.4f }, new[] { "Small", "Normal", "Large", "Larger" }, () => S.BallScale, v => S.BallScale = v);
            Text("Only how the ball is drawn - the ball in play is the same for everyone.");
            Choice("Ring colour", new[] { "White", "Yellow", "Cyan" }, () => S.RingColour, i => S.RingColour = i);
            Steps("Call text", new[] { 1f, 1.2f, 1.4f }, new[] { "Normal", "Large", "Larger" }, () => S.CallTextScale, v => S.CallTextScale = v);
            BackRow();
        }
    }

    public sealed class GameplaySettingsScreen : SettingsPage
    {
        protected override string Title { get { return "Gameplay"; } }
        protected override void Build()
        {
            OnOff("\"Hit!\" pop-ups", () => S.Popups, v => S.Popups = v);
            BackRow();
        }
    }

    // ---------------- controls reference ----------------

    public sealed class ControlsHelpScreen : MenuScreen
    {
        protected override string Kicker { get { return "HOW TO PLAY"; } }
        protected override string Title { get { return "Controls"; } }
        public override bool Dims { get { return Fe.Match == null || !Fe.Match.Demo; } }
        static readonly string[,] Rows =
        {
            { "Move", "WASD   ·   left stick" },
            { "Aim", "mouse   ·   right stick  (high = lob, low = drop)" },
            { "Hit", "hold LMB / A to charge, let go as the ball arrives" },
            { "Kill", "aim low and charge" },
            { "Boast", "tap E   ·   RB  (arms the next shot)" },
            { "Dive", "Space   ·   B" },
            { "Sprint", "Shift   ·   LB" },
            { "Serve", "tap: lob serve   ·   hold: hard serve" },
            { "Replay last call", "V" },
            { "Pause", "Esc   ·   Start" },
        };
        protected override void Build()
        {
            for (int i = 0; i < Rows.GetLength(0); i++)
            {
                var row = new VisualElement(); row.AddToClassList("ctl-row");
                var a = new Label(Rows[i, 0]); a.AddToClassList("ctl-action");
                var k = new Label(Rows[i, 1]); k.AddToClassList("ctl-keys");
                row.Add(a); row.Add(k);
                Content.Add(row);
            }
            Text("Get set early and let go on time: the ring pulses on a great hit.");
            Add(new ButtonItem("Back", () => Fe.Pop()));
        }
    }

    // ---------------- pause ----------------

    public sealed class PauseScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }        // Esc / Start resume the game itself
        protected override string Kicker { get { return "MATCH"; } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            bool career = Fe.Match.Settings.Career;
            Add(new ButtonItem("Resume", () => Fe.Resume(), true));
            if (!career) Add(new ButtonItem("Restart match", () => Fe.Push(new ConfirmScreen("Restart the match?", () => Fe.Restart()))));
            Add(new ButtonItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ButtonItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            if (career) Add(new ButtonItem("Quit: retire from the match", () => Fe.Push(new RetireScreen())));
            else Add(new ButtonItem("Quit to menu", () => Fe.Push(new ConfirmScreen("Leave the match?", () => Fe.QuitToMenu()))));
        }
        public override void Back() { Fe.Resume(); }
    }

    public sealed class PracticePauseScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Kicker { get { return "PRACTICE COURT"; } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            Add(new ButtonItem("Resume", () => Fe.Resume(), true));
            Add(new ButtonItem("Coach panel", () => Fe.Push(new CoachScreen())));
            Add(new ButtonItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ButtonItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ButtonItem("Main menu", () => Fe.QuitToMenu()));
        }
        public override void Back() { Fe.Resume(); }
    }

    // ---------------- confirm ----------------

    public sealed class ConfirmScreen : MenuScreen
    {
        readonly string _question;
        readonly Action _yes;
        public ConfirmScreen(string question, Action yes) { _question = question; _yes = yes; }
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Title { get { return _question; } }
        protected override void Build()
        {
            Add(new ButtonItem("No", () => Fe.Pop()));
            Add(new ButtonItem("Yes", () => { Fe.Pop(); _yes(); }, true));
        }
    }

    // ---------------- post-match ----------------

    public sealed class PostMatchScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "MATCH OVER"; } }
        protected override string Title { get { return Winner(); } }

        string Winner()
        {
            var m = Fe.Match;
            int w = m.Sim.Score.MatchWinner;
            if (w < 0) w = m.Sim.Score.GameWinner;
            return w < 0 ? "Match over" : (m.Settings.TwoPlayer ? m.Names[w] + " wins" : (w == m.HumanIndex ? "You win" : m.Names[w] + " wins"));
        }

        public Progression.RewardResult Reward { get; private set; }

        protected override void Build()
        {
            var m = Fe.Match;
            var st = Fe.Stats;
            // P2-M5: XP, lifetime stats, the match achievements (once, when the screen is made)
            double tier = 0;
            var career = SquashGame.Profile.CareerStore.Current;
            if (m.Settings.Career && career != null && career.S.Current != null) tier = Progression.Rewards.TierFactor[career.S.Current.Tier];
            Reward = Progression.Rewards.Match(st.Facts(m), tier);
            var games = new List<string>();
            foreach (var g in st.Games) games.Add(g[0] + "-" + g[1]);
            var score = new Label(m.Names[0] + "  " + m.Sim.Score.Games[0] + " - " + m.Sim.Score.Games[1] + "  " + m.Names[1] +
                                  (games.Count > 0 ? "      (" + string.Join(", ", games) + ")" : ""));
            score.AddToClassList("pm-score");
            Content.Add(score);

            var box = new VisualElement(); box.AddToClassList("pm-stats");
            Row(box, "", m.Names[0], m.Names[1]);
            Row(box, "Rallies won", st.Players[0].Rallies.ToString(), st.Players[1].Rallies.ToString());
            Row(box, "Winners", st.Players[0].Winners.ToString(), st.Players[1].Winners.ToString());
            Row(box, "Errors", st.Players[0].Errors.ToString(), st.Players[1].Errors.ToString());
            Row(box, "Great hits", st.Players[0].GreatHits.ToString(), st.Players[1].GreatHits.ToString());
            Row(box, "Top pace", Kmh(st.Players[0].TopPace), Kmh(st.Players[1].TopPace));
            Row(box, "Longest rally", st.LongestRally + " shots", "");
            Content.Add(box);
            Content.Add(new RewardStrip(Reward));
            Fe.ToastAchievements(Reward);

            if (m.Settings.Career)
            {
                bool won = m.Sim.Score.MatchWinner == m.HumanIndex;
                Add(new ButtonItem("Continue", () => Career.Record(Fe, won, false), true));
                return;
            }
            Add(new ButtonItem("Rematch", () => Fe.Restart(), true));
            Add(new ButtonItem("Change settings", () =>
            {
                bool two = m.Settings.TwoPlayer;
                Fe.QuitToMenu();
                Fe.Push(two ? (MenuScreen)new LocalVersusScreen() : new QuickMatchScreen());
            }));
            Add(new ButtonItem("Main menu", () => Fe.QuitToMenu()));
        }

        static string Kmh(double ms) { return ms > 0 ? (ms * 3.6).ToString("0") + " km/h" : "-"; }

        static void Row(VisualElement box, string name, string a, string b)
        {
            var row = new VisualElement(); row.AddToClassList("pm-stat");
            var n = new Label(name); n.AddToClassList("pm-stat__name"); n.style.flexGrow = 1;
            var va = new Label(a); va.AddToClassList("pm-stat__val");
            var vb = new Label(b); vb.AddToClassList("pm-stat__val");
            row.Add(n); row.Add(va); row.Add(vb);
            box.Add(row);
        }

        public override void Back()
        {
            if (Fe.Match.Settings.Career) Career.Record(Fe, Fe.Match.Sim.Score.MatchWinner == Fe.Match.HumanIndex, false);
            else Fe.QuitToMenu();
        }
    }
}
