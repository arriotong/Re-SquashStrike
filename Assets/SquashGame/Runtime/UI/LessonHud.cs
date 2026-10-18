// ===========================================================
// Assets/SquashGame/Runtime/UI/LessonHud.cs
// P2-M3: the Academy HUD, broadcast style like the arcade panel:
//
//   card      lesson + step ("LESSON 3  ·  HIT     STEP 2 / 4"),
//             the instruction, the keys, progress "2 / 3", a tick
//             when the step is passed; "Enter / A  next" on cards
//   callout   brief notes: "THAT WAS A LOB", "FAULT", "DEEPER"...
// ===========================================================

using SquashSim.Academy;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class LessonHud : VisualElement
    {
        readonly VisualElement _card = new VisualElement();
        readonly Label _head = new Label(), _text = new Label(), _keys = new Label(), _progress = new Label();
        readonly Label _callout = new Label();
        float _calloutAge = 99f, _tickAge = 99f;

        /// <summary>The last callout (tests).</summary>
        public string LastCallout { get; private set; }
        /// <summary>The instruction showing (tests, captures).</summary>
        public string Instruction { get { return _text.text; } }

        public const string NextHint = "Enter / Space / A   next";

        public LessonHud(AcademyRunner runner)
        {
            AddToClassList("fe-layer");
            pickingMode = PickingMode.Ignore;
            _card.AddToClassList("hud-bug");
            _card.AddToClassList("ls-card");
            _head.AddToClassList("ah-name");
            _text.AddToClassList("ls-text");
            _keys.AddToClassList("ls-keys");
            _progress.AddToClassList("ls-progress");
            _card.Add(_head); _card.Add(_text); _card.Add(_keys); _card.Add(_progress);
            Add(_card);
            _callout.AddToClassList("ah-callout");
            Add(_callout);
            runner.Judged += OnJudged;
        }

        void OnJudged(AcademyEvent e)
        {
            switch (e.Kind)
            {
                case AcademyEventKind.Note: Callout(e.Note); break;
                case AcademyEventKind.StepPassed: _tickAge = 0f; Callout("✓"); break;
            }
        }

        void Callout(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            LastCallout = text;
            _callout.text = text;
            _calloutAge = 0f;
        }

        public void Refresh(AcademyRunner r, bool allowed)
        {
            var s = r != null ? r.Session : null;
            bool visible = allowed && s != null && !s.Done && s.Current != null;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            var st = s.Current;
            int n = Lessons.All().IndexOf(s.Lesson) + 1;
            _head.text = (s.Lesson.Chapter == Chapter.Rules ? "RULES  ·  " : "LESSON " + n + "  ·  ") + s.Lesson.Title.ToUpperInvariant() +
                         "     STEP " + (s.StepIndex + 1) + " / " + s.Lesson.Steps.Count;

            string text = st.Text, keys = st.Keys, progress = "";
            if (st.Goal == GoalKind.Quiz)
            {
                text = s.AwaitingAnswer ? st.Question.Text : "Watch the ball - then make the call.";
                keys = null;
                progress = "Question " + (s.QuizAsked + (s.AwaitingAnswer ? 0 : 1)) + "   ·   right so far " + s.QuizRight;
            }
            else if (st.Goal == GoalKind.Card) keys = NextHint;
            else if (st.Count > 1) progress = Mathf.Min(s.Progress, st.Count) + " / " + st.Count;
            _text.text = text ?? "";
            _keys.text = keys ?? "";
            _keys.style.display = string.IsNullOrEmpty(keys) ? DisplayStyle.None : DisplayStyle.Flex;

            bool ticked = _tickAge < TickSeconds;
            if (ticked) progress = "✓  " + (progress.Length > 0 ? progress : "done");
            _progress.text = progress;
            _progress.EnableInClassList("ls-progress--done", ticked);
            _progress.style.display = progress.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            float dt = Time.unscaledDeltaTime;
            _tickAge += dt;
            _calloutAge += dt;
            _callout.style.opacity = Mathf.Clamp01(1f - (_calloutAge - 0.8f) / 0.4f);
            _callout.style.display = _calloutAge < 1.2f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static readonly float TickSeconds = (float)AcademySession.StepPause;
    }
}
