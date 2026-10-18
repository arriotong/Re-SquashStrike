// ===========================================================
// Assets/SquashGame/Runtime/UI/ArcadeScreens.cs
// P2-M2: the Target Arcade and Practice Court screens.
//
//   TargetArcadeScreen   the six modes, with your stars
//   LevelScreen          Easy / Medium / Hard: best score, stars
//   PracticeScreen       free practice, or a drill
//   ArcadePauseScreen    resume, restart, settings, controls, quit
//   ResultsScreen        score, stars, best, breakdown -> retry / next
//   CoachScreen          the practice court's feeder (Tab / pad View)
// ===========================================================

using SquashGame.Profile;
using SquashSim.Arcade;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class Stars
    {
        public static string Of(int n) { return new string('★', n) + new string('☆', 3 - n); }
        public static string Best(string id)
        {
            var r = ProfileStore.Record(id);
            return r == null ? "not played" : Of(r.Stars) + "   best " + r.Best.ToString("N0");
        }
    }

    public sealed class TargetArcadeScreen : MenuScreen
    {
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Target Arcade"; } }
        protected override void Build()
        {
            Text("Just you, the feeder and the targets. Earn up to three stars on every level.");
            for (int m = 0; m < Challenges.ModeNames.Length; m++)
            {
                var mode = (ArcadeMode)m;
                Add(new ButtonItem(Challenges.ModeNames[m], () => Fe.Push(new LevelScreen(mode)), false, () => Total(mode)));
            }
        }
        static string Total(ArcadeMode mode)
        {
            int n = 0;
            for (int l = 0; l < 3; l++) { var r = ProfileStore.Record(Challenges.Get(mode, l).Id); if (r != null) n += r.Stars; }
            return "★ " + n + " / 9";
        }
    }

    public sealed class LevelScreen : MenuScreen
    {
        readonly ArcadeMode _mode;
        public LevelScreen(ArcadeMode mode) { _mode = mode; }
        protected override string Kicker { get { return "TARGET ARCADE"; } }
        protected override string Title { get { return Challenges.ModeNames[(int)_mode]; } }
        protected override void Build()
        {
            Text(Challenges.ModeBlurbs[(int)_mode]);
            for (int l = 0; l < 3; l++)
            {
                var c = Challenges.Get(_mode, l);
                Add(new ButtonItem(c.LevelName, () => FrontEnd.LaunchChallenge(c.Id), l == 0, () => Stars.Best(c.Id)));
            }
        }
    }

    public sealed class PracticeScreen : MenuScreen
    {
        protected override string Kicker { get { return "PRACTICE"; } }
        protected override string Title { get { return "Practice Court"; } }
        protected override void Build()
        {
            Add(new ButtonItem("Free practice", () => FrontEnd.LaunchFreePractice(), true, () => "Tab / View: Coach panel"));
            Section("Drills");
            foreach (var c in Challenges.All())
            {
                if (c.Mode != ArcadeMode.Drill) continue;
                var cc = c;
                Add(new ButtonItem(c.Name, () => FrontEnd.LaunchChallenge(cc.Id), false, () => Stars.Best(cc.Id)));
            }
        }
    }

    public sealed class ArcadePauseScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Kicker { get { return Fe.Arcade.Challenge.Mode == ArcadeMode.Drill ? "DRILL" : "TARGET ARCADE"; } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            Add(new ButtonItem("Resume", () => Fe.Resume(), true));
            Add(new ButtonItem("Restart", () => { Fe.Clear(); Fe.Arcade.Restart(); }));
            Add(new ButtonItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ButtonItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ButtonItem(Fe.Arcade.Challenge.Mode == ArcadeMode.Drill ? "Quit to Practice" : "Quit to Target Arcade", () => Fe.BackToArcadeMenu()));
        }
        public override void Back() { Fe.Resume(); }
    }

    public sealed class ResultsScreen : MenuScreen
    {
        readonly int _score, _stars;
        readonly bool _best;
        readonly int _prev;
        readonly Challenge _c;
        readonly ArcadeSession _s;

        public ResultsScreen(ArcadeRunner r)
        {
            _s = r.Session;
            _c = r.Challenge;
            _score = _s.Score;
            _stars = _s.Stars;
            var rec = ProfileStore.Record(_c.Id);
            _prev = rec != null ? rec.Best : int.MinValue;
            int starsBefore = rec != null ? rec.Stars : 0;
            _best = ProfileStore.Submit(_c.Id, _score, _stars);
            // P2-M5: XP, new stars, the arcade achievements
            Reward = Progression.Rewards.Arcade(_c.Id, _c.Mode == ArcadeMode.Drill, starsBefore, _stars, _score, _c.Mode == ArcadeMode.Streak);
        }

        /// <summary>This play set a new best (tests).</summary>
        public bool NewBest { get { return _best; } }
        public Progression.RewardResult Reward { get; private set; }

        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return (_c.Mode == ArcadeMode.Drill ? "DRILL  ·  " + _c.Name : _c.Name + "  ·  " + _c.LevelName).ToUpperInvariant(); } }
        protected override string Title { get { return _c.Mode == ArcadeMode.Streak ? "Streak " + _score : _c.Mode == ArcadeMode.Drill ? _score + " / " + _s.Attempts : _score.ToString("N0"); } }

        protected override void Build()
        {
            var stars = new Label(Stars.Of(_stars)); stars.AddToClassList("ta-big-stars"); Content.Add(stars);
            var best = new Label(_best ? (_prev == int.MinValue ? "First play - that's the one to beat." : "New best!   (was " + _prev.ToString("N0") + ")")
                                       : "Best  " + ProfileStore.Record(_c.Id).Best.ToString("N0"));
            best.AddToClassList("ta-best");
            if (_best) best.AddToClassList("ta-new");
            Content.Add(best);
            var box = new VisualElement(); box.AddToClassList("pm-stats");
            Row(box, _c.Drill == DrillKind.Ghosting ? "Spots" : "Targets hit", _s.Hits + " / " + _s.Attempts);
            Row(box, "Best combo", _s.BestCombo.ToString());
            Row(box, "Great hits", _s.GreatHits.ToString());
            if (_stars < 3 && _c.Stars[_stars] > 0) Row(box, "Next star", _c.Stars[_stars].ToString("N0"));
            Content.Add(box);
            Content.Add(new RewardStrip(Reward));
            Fe.ToastAchievements(Reward);

            Add(new ButtonItem("Retry", () => { Fe.Clear(); Fe.Arcade.Restart(); }, true));
            if (_c.Mode != ArcadeMode.Drill && _c.Level < 2)
            {
                var next = Challenges.Get(_c.Mode, _c.Level + 1);
                Add(new ButtonItem("Next level: " + next.LevelName, () => { Fe.Clear(); Fe.Arcade.Play(next); }));
            }
            Add(new ButtonItem(_c.Mode == ArcadeMode.Drill ? "Practice menu" : "Target Arcade", () => Fe.BackToArcadeMenu()));
        }

        static void Row(VisualElement box, string name, string value)
        {
            var row = new VisualElement(); row.AddToClassList("pm-stat");
            var n = new Label(name); n.AddToClassList("pm-stat__name"); n.style.flexGrow = 1;
            var v = new Label(value); v.AddToClassList("pm-stat__val");
            row.Add(n); row.Add(v);
            box.Add(row);
        }

        public override void Back() { Fe.BackToArcadeMenu(); }
    }

    public sealed class CoachScreen : MenuScreen
    {
        protected override string Kicker { get { return "PRACTICE COURT"; } }
        protected override string Title { get { return "Coach"; } }
        protected override string Hint { get { return "←→ change    Enter / A  select    Tab / Esc  close"; } }
        static CoachChoices C { get { return ProfileStore.Current.Coach; } }
        CoachFeeder Feeder { get { return Fe.Coach; } }

        protected override void Build()
        {
            Opt("Feed", CoachFeeder.FeedNames, () => C.Feed, v => C.Feed = v);
            Opt("Side", CoachFeeder.SideNames, () => C.Side, v => C.Side = v);
            Opt("Speed", CoachFeeder.SpeedNames, () => C.Speed, v => C.Speed = v);
            Add(new OptionItem("Feeding", () => C.Auto ? "Automatic" : "On button (F)", d => C.Auto = !C.Auto));
            Opt("Every", CoachFeeder.IntervalNames, () => C.Interval, v => C.Interval = v).Enabled = () => C.Auto;
            Opt("Slow motion", CoachFeeder.SlowNames, () => C.Slow, v => C.Slow = v);
            Add(new OptionItem("Trail", () => C.LongTrail ? "Long" : "Short", d => { C.LongTrail = !C.LongTrail; Feeder.Apply(); }));
            Add(new OptionItem("Ball", () => C.WarmBall ? "Warm (match)" : "Cold", d => C.WarmBall = !C.WarmBall));
            Add(new ButtonItem("Feed now", () => { Fe.Pop(); Feeder.FeedNow(); }, true));
            Add(new ButtonItem("Close", () => Fe.Pop()));
        }

        OptionItem Opt(string label, string[] names, System.Func<int> get, System.Action<int> set)
        {
            return Add(new OptionItem(label, () => names[System.Math.Max(0, System.Math.Min(names.Length - 1, get()))],
                d => set(OptionItem.Step(get(), d, names.Length))));
        }

        public override void OnShow() { base.OnShow(); Fe.Runner.Clock.Pause(); }
        public override void OnHide()
        {
            ProfileStore.Save();
            if (Feeder != null) Feeder.Apply();
            Fe.Runner.Clock.Resume();
        }
    }
}
