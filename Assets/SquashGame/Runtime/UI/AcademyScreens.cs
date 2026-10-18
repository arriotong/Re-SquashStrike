// ===========================================================
// Assets/SquashGame/Runtime/UI/AcademyScreens.cs
// P2-M3: the Academy's screens.
//
//   AcademyScreen          Continue, then the lessons by chapter, ticked
//   AcademySuggestScreen   "New to re:SquashStrike?" - once, to a new profile
//   AcademyPauseScreen     resume, restart, settings, controls, quit
//   QuizScreen             "you make the call": the answer buttons
//   QuizRevealScreen       right / not quite, and why
//   QuizFailedScreen       fewer than 2 of 3: try the questions again
//   LessonCompleteScreen   next lesson, or the Academy (lesson 9 picks
//                          the control mode; the last one suggests a match)
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim.Academy;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class AcademyScreen : MenuScreen
    {
        protected override string Kicker { get { return "PRACTICE"; } }
        protected override string Title { get { return "Academy"; } }

        /// <summary>The main menu row's note: "4 / 14 lessons".</summary>
        public static string Summary()
        {
            int n = 0;
            foreach (var l in Lessons.All()) if (ProfileStore.LessonDone(l.Id)) n++;
            return n == 0 ? "start here" : n + " / " + Lessons.All().Count + " lessons";
        }

        /// <summary>The first lesson not passed yet (the first one when all are).</summary>
        public static Lesson NextUp()
        {
            foreach (var l in Lessons.All()) if (!ProfileStore.LessonDone(l.Id)) return l;
            return Lessons.All()[0];
        }

        public static IEnumerable<string> AllIds() { foreach (var l in Lessons.All()) yield return l.Id; }

        protected override void Build()
        {
            var next = NextUp();
            bool all = ProfileStore.Current.Academy.Finished;
            Add(new ButtonItem(all ? "Play again: " + next.Title : "Continue: " + next.Title, () => FrontEnd.LaunchLesson(next.Id), true));
            int n = 0;
            foreach (var ch in new[] { Chapter.Controls, Chapter.Rules })
            {
                Section(ch == Chapter.Controls ? "Controls" : "Rules of squash");
                foreach (var l in Lessons.All())
                {
                    if (l.Chapter != ch) continue;
                    var ll = l;
                    string label = (ch == Chapter.Controls ? (++n) + ".  " : "") + l.Title;
                    Add(new ButtonItem(label, () => FrontEnd.LaunchLesson(ll.Id), false, () => ProfileStore.LessonDone(ll.Id) ? "✓" : ""));
                }
            }
        }
    }

    public sealed class AcademySuggestScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "WELCOME"; } }
        protected override string Title { get { return "New to re:SquashStrike?"; } }
        protected override void Build()
        {
            Text("Start with the Academy: short lessons on the controls, then the rules of squash.");
            Add(new ButtonItem("Start the Academy", () => { Fe.Pop(); Fe.Push(new AcademyScreen()); }, true));
            Add(new ButtonItem("Not now", () => Fe.Pop()));
        }
    }

    public sealed class AcademyPauseScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Kicker { get { return "ACADEMY  ·  " + Fe.Academy.Lesson.Title.ToUpperInvariant(); } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            Add(new ButtonItem("Resume", () => Fe.Resume(), true));
            Add(new ButtonItem("Restart lesson", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            Add(new ButtonItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ButtonItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ButtonItem("Quit to the Academy", () => Fe.BackToAcademyMenu()));
        }
        public override void Back() { Fe.Resume(); }
    }

    public sealed class QuizScreen : MenuScreen
    {
        readonly QuizQuestion _q;
        public QuizScreen(QuizQuestion q) { _q = q; }
        protected override string Hint { get { return "↑↓ choose    Enter / A  answer"; } }
        public override void OnShow() { base.OnShow(); Fe.Runner.Clock.Pause(); }
        public override void OnHide() { Fe.Runner.Clock.Resume(); }
        protected override string Kicker { get { return "YOU MAKE THE CALL"; } }
        protected override string Title { get { return _q.Text; } }
        protected override void Build()
        {
            for (int i = 0; i < _q.Choices.Length; i++)
            {
                int c = i;
                Add(new ButtonItem(_q.Choices[i], () => Pick(c), i == 0));
            }
        }

        /// <summary>Give this answer (the buttons; tests).</summary>
        public void Pick(int choice)
        {
            var s = Fe.Academy.Session;
            if (!s.AwaitingAnswer) return;
            bool ok = s.Answer(choice);
            Fe.Replace(new QuizRevealScreen(_q, ok, s.LastCorrect));
        }
        public override void Back() { }
    }

    public sealed class QuizRevealScreen : MenuScreen
    {
        readonly QuizQuestion _q;
        readonly bool _ok;
        readonly string _right;
        public QuizRevealScreen(QuizQuestion q, bool ok, string right) { _q = q; _ok = ok; _right = right; }
        public bool Right { get { return _ok; } }
        protected override string Hint { get { return "Enter / A  continue"; } }
        public override void OnShow() { base.OnShow(); Fe.Runner.Clock.Pause(); }
        public override void OnHide() { Fe.Runner.Clock.Resume(); }
        protected override string Kicker { get { return "YOU MAKE THE CALL"; } }
        protected override string Title { get { return _ok ? "Right - " + _right : "Not quite - " + _right; } }
        protected override void Build()
        {
            Text(_q.Explain);
            Add(new ButtonItem("Continue", Continue, true));
        }
        void Continue()
        {
            Fe.Pop();
            if (Fe.Academy.Session.QuizFailed) Fe.Push(new QuizFailedScreen());
        }
        public override void Back() { Continue(); }
    }

    public sealed class QuizFailedScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "ACADEMY  ·  " + Fe.Academy.Lesson.Title.ToUpperInvariant(); } }
        protected override string Title { get { return Fe.Academy.Session.QuizRight + " of 3 right"; } }
        protected override void Build()
        {
            Text("Get 2 of the 3 calls right to pass. Have another look.");
            Add(new ButtonItem("Try the questions again", () => { Fe.Pop(); Fe.Academy.Session.RetryQuiz(); }, true));
            Add(new ButtonItem("Restart lesson", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            Add(new ButtonItem("Quit to the Academy", () => Fe.BackToAcademyMenu()));
        }
        public override void Back() { }
    }

    public sealed class LessonCompleteScreen : MenuScreen
    {
        readonly Lesson _l;
        readonly bool _finished;
        public LessonCompleteScreen(Lesson l)
        {
            _l = l;
            bool first = !ProfileStore.LessonDone(l.Id);
            _finished = ProfileStore.CompleteLesson(l.Id, AcademyScreen.AllIds());
            Reward = Progression.Rewards.Lesson(first, ProfileStore.Current.Academy.Finished);
        }

        public Progression.RewardResult Reward { get; private set; }

        /// <summary>This lesson finished the whole Academy (tests).</summary>
        public bool FinishedAcademy { get { return _finished; } }
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "LESSON PASSED"; } }
        protected override string Title { get { return _finished ? "Academy complete!" : _l.Title + "  ✓"; } }
        static GameSettings S { get { return ProfileStore.Current.Settings; } }

        protected override void Build()
        {
            var next = Lessons.Next(_l);
            if (Reward.Any) { Content.Add(new RewardStrip(Reward)); Fe.ToastAchievements(Reward); }
            if (_l.Id == "mode")
            {
                Text("Which do you want to play with? You can change it any time in Settings.");
                Add(new OptionItem("Control mode", () => S.Assisted ? "Assisted" : "Non-assisted",
                    d => { S.Assisted = !S.Assisted; ProfileStore.Apply(S, false); ProfileStore.Save(); }));
            }
            if (_finished)
            {
                Text("You know the controls and the rules. Try a match against an Easy opponent.");
                Add(new ButtonItem("Quick Match on Easy", () => FrontEnd.LaunchQuickMatch(0), true));
            }
            if (next != null) Add(new ButtonItem("Next: " + next.Title, () => { Fe.Clear(); Fe.Academy.Play(next); }, !_finished));
            Add(new ButtonItem("Play it again", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            Add(new ButtonItem("Academy", () => Fe.BackToAcademyMenu()));
        }
        public override void Back() { Fe.BackToAcademyMenu(); }
    }
}
