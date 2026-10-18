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
}
