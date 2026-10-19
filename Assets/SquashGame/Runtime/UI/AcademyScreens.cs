// ===========================================================
// Assets/SquashGame/Runtime/UI/AcademyScreens.cs
// The Academy's screens. UI redesign U4.
//
//   AcademyScreen          the lesson trail: two chapters (Controls,
//                          Rules) as paths of nodes - passed ticked, the
//                          next one pulsing; the focused lesson's card
//   AcademySuggestScreen   "New to re:SquashStrike?" - once, to a new profile
//   AcademyPauseScreen     a side panel: resume, restart, settings, quit
//   QuizScreen             "you make the call": a panel at the bottom
//                          (the court and the marked ball stay in view),
//                          the answers as big cards
//   QuizRevealScreen       a tick or a cross stamp, the call, why
//   QuizFailedScreen       fewer than 2 of 3: try the questions again
//   LessonCompleteScreen   the stamp, the chapter's trail with the new
//                          tick, the rewards; Next (lesson 9 picks the
//                          control mode; the last one suggests a match)
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim.Academy;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>A path through nodes: each node is placed along a wave, the line between them drawn (lime up to the last passed).</summary>
    public sealed class TrailView : VisualElement
    {
        readonly List<VisualElement> _nodes = new List<VisualElement>();
        readonly Func<int, bool> _lit;
        public const float NodeW = 132;

        public TrailView(Func<int, bool> lit)
        {
            _lit = lit;
            AddToClassList("k-trail");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<GeometryChangedEvent>(e => MarkDirtyRepaint());
        }

        /// <summary>Place node i of n along the wave.</summary>
        public void Place(VisualElement node, int i, int n)
        {
            node.style.position = Position.Absolute;
            node.style.left = Length.Percent(n <= 1 ? 50f : 4f + 92f * i / (n - 1));
            node.style.marginLeft = -NodeW * 0.5f;
            node.style.width = NodeW;
            node.style.top = 26f + 34f * Mathf.Sin(i * 1.15f);
            node.RegisterCallback<GeometryChangedEvent>(e => MarkDirtyRepaint());
            Add(node);
            _nodes.Add(node);
        }

        void Paint(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            for (int i = 1; i < _nodes.Count; i++)
            {
                var a = Dot(_nodes[i - 1]); var b = Dot(_nodes[i]);
                bool lit = _lit(i - 1) && _lit(i);
                p.strokeColor = lit ? new Color32(200, 255, 61, 255) : new Color(1, 1, 1, 0.16f);
                p.lineWidth = lit ? 5f : 3f;
                p.BeginPath(); p.MoveTo(a);
                p.BezierCurveTo(new Vector2((a.x + b.x) * 0.5f, a.y), new Vector2((a.x + b.x) * 0.5f, b.y), b);
                p.Stroke();
            }
        }

        static Vector2 Dot(VisualElement n) { var l = n.layout; return new Vector2(l.x + l.width * 0.5f, l.y + 32f); }
    }

    /// <summary>A lesson on the trail: a dot (its number, or a tick once passed) and the title under it.</summary>
    public sealed class TrailNode : MenuItem
    {
        public readonly Lesson Lesson;
        readonly Action _run;
        readonly Label _dot;
        readonly string _number;
        public override bool WantsSlant { get { return false; } }
        public bool Done { get { return ProfileStore.LessonDone(Lesson.Id); } }

        public TrailNode(Lesson l, int number, Action run) : base(l.Title)
        {
            Lesson = l; _run = run; _number = number.ToString();
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-node");
            LabelEl.style.display = DisplayStyle.None;
            _dot = Kit.L(Root, _number, "k-node-dot");
            Kit.L(Root, l.Title, "k-node-title");
        }

        public override void Submit() { _run?.Invoke(); }
        public override void Refresh()
        {
            base.Refresh();
            bool done = Done;
            Root.EnableInClassList("k-node--done", done);
            Root.EnableInClassList("k-node--next", AcademyScreen.NextUp() == Lesson && !ProfileStore.Current.Academy.Finished);
            _dot.text = done ? "✓" : _number;
        }
    }

    public sealed class AcademyScreen : GameScreen
    {
        protected override string Kicker { get { return "LEARN"; } }
        protected override string Title { get { return "Academy"; } }

        /// <summary>The main menu card's note: "4 / 14 lessons".</summary>
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

        public static List<Lesson> InChapter(Chapter ch) { return Lessons.All().FindAll(l => l.Chapter == ch); }

        VisualElement _card;
        MenuItem _shown;
        TrailNode _next;
        bool _placed;

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "k-ac-stage");
            var left = Kit.Box(stage, "k-ac-left");
            foreach (var ch in new[] { SquashSim.Academy.Chapter.Controls, SquashSim.Academy.Chapter.Rules })
            {
                var list = InChapter(ch);
                int done = list.FindAll(l => ProfileStore.LessonDone(l.Id)).Count;
                var panel = Kit.Panel(left, (ch == SquashSim.Academy.Chapter.Controls ? "CHAPTER 1  ·  THE CONTROLS" : "CHAPTER 2  ·  THE RULES OF SQUASH") + "   " + done + " / " + list.Count);
                panel.AddToClassList("k-ac-chapter");
                var trail = new TrailView(i => ProfileStore.LessonDone(list[i].Id));
                panel.Add(trail);
                for (int i = 0; i < list.Count; i++)
                {
                    var l = list[i];
                    var node = new TrailNode(l, i + 1, () => FrontEnd.LaunchLesson(l.Id));
                    Add(node);
                    node.Root.RemoveFromHierarchy();
                    trail.Place(node.Root, i, list.Count);
                    if (l == NextUp()) _next = node;
                }
            }
            _card = Kit.Panel(stage, "THE LESSON");
            _card.AddToClassList("k-ac-card");
            bool all = ProfileStore.Current.Academy.Finished;
            var next = NextUp();
            AddTo(Root, new PrimaryItem(all ? "Play again: " + next.Title : "Continue: " + next.Title, () => FrontEnd.LaunchLesson(NextUp().Id)));
        }

        public override void OnShow()
        {
            base.OnShow();
            MenuCinematics.Scene = 5;
            if (_placed) return;
            _placed = true;
            if (_next != null) Focus(_next);
        }

        public override void Tick(float dt)
        {
            if (!(Focused is TrailNode n) || n == _shown) return;
            _shown = n;
            var l = n.Lesson;
            _card.Clear();
            Kit.L(_card, l.Chapter == SquashSim.Academy.Chapter.Controls ? "THE CONTROLS  ·  LESSON " + (InChapter(l.Chapter).IndexOf(l) + 1) : "THE RULES  ·  LESSON " + (InChapter(l.Chapter).IndexOf(l) + 1), "k-panel-title");
            Kit.L(_card, l.Title, "k-ac-title");
            var st = Kit.L(_card, n.Done ? "✓  PASSED" : l == NextUp() ? "NEXT UP" : "NOT YET", "k-ac-status");
            if (n.Done) st.AddToClassList("k-ac-status--done");
            Kit.L(_card, l.Blurb, "k-ac-blurb");
            var steps = Kit.Box(_card, "k-ac-steps");
            int shown = 0; string keys = null;
            foreach (var s in l.Steps)
            {
                if (keys == null && !string.IsNullOrEmpty(s.Keys)) keys = s.Keys;
                if (shown >= 4 || string.IsNullOrEmpty(s.Text) || s.Goal == GoalKind.Quiz) continue;
                var row = Kit.Box(steps, "k-ac-step");
                Kit.L(row, (++shown).ToString(), "k-ac-step-n");
                Kit.L(row, s.Text, "k-ac-step-t");
            }
            if (l.HasQuiz) Kit.L(_card, "Ends with 3 calls to make: get 2 right.", "k-ac-quiz");
            if (keys != null) { var k = Kit.Box(_card, "k-ac-keys"); Kit.L(k, "KEYS", "k-ac-keys-k"); Kit.L(k, keys, "k-ac-keys-v"); }
        }
    }

    public sealed class AcademySuggestScreen : DialogScreen
    {
        protected override string Kicker { get { return "WELCOME"; } }
        protected override string Title { get { return "New to re:SquashStrike?"; } }
        protected override string Line { get { return "Start with the Academy: short lessons on the controls, then the rules of squash."; } }
        protected override string YesLabel { get { return "Start the Academy"; } }
        protected override string NoLabel { get { return "Not now"; } }
        protected override void OnYes() { Fe.Pop(); Fe.Push(new AcademyScreen()); }
    }

    public sealed class AcademyPauseScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-side"; } }
        public override bool Spatial { get { return true; } }
        public override bool Dims { get { return true; } }
        public override bool EscapeIsGame { get { return true; } }
        protected override string Hint { get { return null; } }
        protected override string Kicker { get { return "ACADEMY  ·  " + Fe.Academy.Lesson.Title.ToUpperInvariant(); } }
        protected override string Title { get { return "Paused"; } }
        protected override void Build()
        {
            var s = Fe.Academy.Session;
            Kit.L(Content, "Step " + Mathf.Min(s.StepIndex + 1, Fe.Academy.Lesson.Steps.Count) + " of " + Fe.Academy.Lesson.Steps.Count + "   ·   " + Fe.Academy.Lesson.Blurb, "k-side-stats");
            Add(new ActionItem("Resume", () => Fe.Resume(), "▶"));
            Add(new ActionItem("Restart lesson", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            Add(new ActionItem("Settings", () => Fe.Push(new SettingsScreen())));
            Add(new ActionItem("Controls", () => Fe.Push(new ControlsHelpScreen())));
            Add(new ActionItem("Quit to the Academy", () => Fe.BackToAcademyMenu()));
            var prompts = new PromptBar(); prompts.AddToClassList("k-prompts--side");
            prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Resume"));
            Root.Add(prompts);
        }
        public override void Back() { Fe.Resume(); }
    }

    // ---------------- the quiz: a panel at the bottom, the court in view ----------------

    public abstract class QuizPanel : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-quiz"; } }
        public override bool Spatial { get { return true; } }
        protected override string Hint { get { return null; } }
        protected override string Kicker { get { return "YOU MAKE THE CALL  ·  " + Fe.Academy.Lesson.Title.ToUpperInvariant(); } }
        /// <summary>The game waits while the panel is up (the quiz itself; not the failed panel).</summary>
        protected virtual bool PausesGame { get { return true; } }
        public override void OnShow() { base.OnShow(); if (PausesGame) Fe.Runner.Clock.Pause(); }
        public override void OnHide() { if (PausesGame) Fe.Runner.Clock.Resume(); }

        /// <summary>"CALL 2 OF 3  ·  1 RIGHT" (top right of the panel).</summary>
        protected void Count()
        {
            var s = Fe.Academy.Session;
            Kit.L(Root, "CALL " + Mathf.Min(s.QuizAsked, 3) + " OF 3   ·   " + s.QuizRight + " RIGHT", "k-quiz-count");
        }

        protected void ShowPrompts(params (PromptBar.Btn, string)[] items)
        {
            var p = new PromptBar(); p.AddToClassList("k-prompts--quiz");
            p.Set(items);
            Root.Add(p);
        }
    }

    public sealed class QuizScreen : QuizPanel
    {
        readonly QuizQuestion _q;
        public QuizScreen(QuizQuestion q) { _q = q; }
        protected override string Title { get { return _q.Text; } }
        static readonly string[] Letters = { "A", "B", "C", "D" };

        protected override void Build()
        {
            Count();
            var row = Kit.Box(Content, "k-quiz-answers");
            for (int i = 0; i < _q.Choices.Length; i++)
            {
                int c = i;
                var card = AddTo(row, new CardItem(_q.Choices[i], "ANSWER " + Letters[Mathf.Min(i, 3)], () => Pick(c), CardSize.S));
                card.Root.AddToClassList("k-card--answer");
            }
            ShowPrompts((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Make the call"));
        }

        /// <summary>Give this answer (the cards; tests).</summary>
        public void Pick(int choice)
        {
            var s = Fe.Academy.Session;
            if (!s.AwaitingAnswer) return;
            bool ok = s.Answer(choice);
            Fe.Replace(new QuizRevealScreen(_q, ok, s.LastCorrect));
        }
        public override void Back() { }
    }

    public sealed class QuizRevealScreen : QuizPanel
    {
        readonly QuizQuestion _q;
        readonly bool _ok;
        readonly string _right;
        public QuizRevealScreen(QuizQuestion q, bool ok, string right) { _q = q; _ok = ok; _right = right; }
        public bool Right { get { return _ok; } }
        protected override string Title { get { return _q.Text; } }

        protected override void Build()
        {
            Count();
            var row = Kit.Box(Content, "k-quiz-reveal");
            var st = new Stamp(_ok ? "✓  RIGHT" : "✗  NOT QUITE", 0.1f);
            st.AddToClassList(_ok ? "k-stamp--good" : "k-stamp--bad");
            row.Add(st);
            var text = Kit.Box(row, "k-quiz-why");
            Kit.L(text, "THE CALL:  " + _right.ToUpperInvariant(), "k-quiz-call");
            Kit.L(text, _q.Explain, "k-quiz-explain");
            AddTo(row, new ActionItem("Continue", Continue, "▶")).Root.AddToClassList("k-quiz-go");
            ShowPrompts((PromptBar.Btn.A, "Continue"));
        }

        void Continue()
        {
            Fe.Pop();
            if (Fe.Academy.Session.QuizFailed) Fe.Push(new QuizFailedScreen());
        }
        public override void Back() { Continue(); }
    }

    public sealed class QuizFailedScreen : QuizPanel
    {
        protected override string Kicker { get { return "ACADEMY  ·  " + Fe.Academy.Lesson.Title.ToUpperInvariant(); } }
        protected override string Title { get { return Fe.Academy.Session.QuizRight + " of 3 right"; } }
        protected override bool PausesGame { get { return false; } }
        protected override void Build()
        {
            var row = Kit.Box(Content, "k-quiz-reveal");
            var st = new Stamp("TRY AGAIN", 0.1f); st.AddToClassList("k-stamp--bad");
            row.Add(st);
            Kit.L(row, "Get 2 of the 3 calls right to pass. Have another look.", "k-quiz-explain");
            var acts = Kit.Box(Content, "k-cr-pv-acts");
            AddTo(acts, new ActionItem("Try the questions again", () => { Fe.Pop(); Fe.Academy.Session.RetryQuiz(); }));
            AddTo(acts, new ActionItem("Restart lesson", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            AddTo(acts, new ActionItem("Quit to the Academy", () => Fe.BackToAcademyMenu()));
            ShowPrompts((PromptBar.Btn.A, "Select"));
        }
        public override void Back() { }
    }

    // ---------------- passed ----------------

    public sealed class LessonCompleteScreen : GameScreen
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
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "ACADEMY  ·  LESSON PASSED"; } }
        protected override string Title { get { return _l.Title; } }
        static GameSettings S { get { return ProfileStore.Current.Settings; } }

        protected override void Compose()
        {
            var next = Lessons.Next(_l);
            bool academyIsPrimary = false;
            if (_finished) AddTo(Root, new PrimaryItem("Quick Match on Easy", () => FrontEnd.LaunchQuickMatch(0)));
            else if (next != null) AddTo(Root, new PrimaryItem("Next: " + next.Title, () => { Fe.Clear(); Fe.Academy.Play(next); }));
            else { AddTo(Root, new PrimaryItem("Academy", () => Fe.BackToAcademyMenu())); academyIsPrimary = true; }

            var col = Kit.Box(Content, "k-cr-result");
            var st = new Stamp(_finished ? "ACADEMY COMPLETE" : "✓  PASSED", 0.25f);
            st.AddToClassList("k-stamp--good");
            col.Add(st);
            if (_finished) Kit.L(col, "You know the controls and the rules. Try a match against an Easy opponent.", "k-cr-res-v");

            var list = AcademyScreen.InChapter(_l.Chapter);
            var panel = Kit.Panel(col, (_l.Chapter == Chapter.Controls ? "THE CONTROLS" : "THE RULES") + "   " + list.FindAll(x => ProfileStore.LessonDone(x.Id)).Count + " / " + list.Count);
            panel.AddToClassList("k-ac-mini");
            var trail = new TrailView(i => ProfileStore.LessonDone(list[i].Id));
            panel.Add(trail);
            for (int i = 0; i < list.Count; i++)
            {
                var node = Kit.Box(null, "k-node");
                bool done = ProfileStore.LessonDone(list[i].Id);
                if (done) node.AddToClassList("k-node--done");
                if (list[i] == _l) node.AddToClassList("k-node--just");
                if (next != null && list[i] == next) node.AddToClassList("k-node--next");
                Kit.L(node, done ? "✓" : (i + 1).ToString(), "k-node-dot");
                Kit.L(node, list[i].Title, "k-node-title");
                trail.Place(node, i, list.Count);
            }

            if (_l.Id == "mode")
            {
                var p = Kit.Panel(col, "WHICH DO YOU WANT TO PLAY WITH?  YOU CAN CHANGE IT ANY TIME IN SETTINGS");
                p.AddToClassList("k-ac-modepick");
                AddTo(p, new SelectorItem("Control mode", new[] { "Non-assisted", "Assisted" }, () => S.Assisted ? 1 : 0,
                    v => { S.Assisted = v == 1; ProfileStore.Apply(S, false); ProfileStore.Save(); }));
            }
            if (Reward.Any) { col.Add(new RewardStrip(Reward)); Fe.ToastAchievements(Reward); }

            var acts = Kit.Box(col, "k-cr-pv-acts");
            AddTo(acts, new ActionItem("Play it again", () => { Fe.Clear(); Fe.Academy.Restart(); }));
            if (!academyIsPrimary) AddTo(acts, new ActionItem("Academy", () => Fe.BackToAcademyMenu()));
        }

        public override void Back() { Fe.BackToAcademyMenu(); }
    }
}
