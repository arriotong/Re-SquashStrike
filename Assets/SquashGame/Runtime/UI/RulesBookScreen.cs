// ===========================================================
// Assets/SquashGame/Runtime/UI/RulesBookScreen.cs
// 2026-11-01: "How to win" - the visual book's 12 rules, one a page,
// each with its picture (RuleDiagram). The AI plays by the same rules
// (SquashBook), and the coach after a match points at the one you
// broke most. Reached from PLAY > TRAINING.
// ===========================================================

using SquashSim;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class RulesBookScreen : GameScreen
    {
        protected override string Kicker { get { return "LEARN TO PLAY  ·  WHERE TO HIT THE BALL"; } }
        protected override string Title { get { return "How to win"; } }
        protected override bool ShowChip { get { return false; } }

        int _rule;
        VisualElement _picBox;
        Label _num, _head, _words;

        /// <summary>The rule shown (1-12).</summary>
        public int Rule { get { return _rule; } }

        public RulesBookScreen(int rule = 1) { _rule = System.Math.Max(1, System.Math.Min(12, rule)); }

        static readonly string[] Names = { "Rule 1", "Rule 2", "Rule 3", "Rule 4", "Rule 5", "Rule 6", "Rule 7", "Rule 8", "Rule 9", "Rule 10", "Rule 11", "Rule 12" };

        protected override void Compose()
        {
            var row = Kit.Box(Content, "k-rule-row");
            _picBox = Kit.Box(row, "k-rule-picbox");
            var text = Kit.Box(row, "k-rule-text");
            _num = Kit.L(text, "", "k-rule-num");
            _head = Kit.L(text, "", "k-rule-head");
            _words = Kit.L(text, "", "k-rule-words");
            var key = Kit.Box(text, "k-rule-key");
            Key(key, RuleDiagram.You, "you");
            Key(key, RuleDiagram.Them, "them");
            Key(key, RuleDiagram.Go, "where the ball should go");
            Key(key, RuleDiagram.NoGo, "don't");
            Kit.L(text, "The computer players follow these rules too - the better they are, the more they stick to them.", "k-cr-note");
            AddTo(text, new CarouselItem("Rule", Names, () => _rule - 1, v => { _rule = v + 1; Show(); }));
            Show();
        }

        static void Key(VisualElement row, UnityEngine.Color c, string what)
        {
            var k = Kit.Box(row, "k-rule-keyitem");
            var dot = Kit.Box(k, "k-rule-keydot"); dot.style.backgroundColor = c;
            Kit.L(k, what, "k-rule-keytext");
        }

        void Show()
        {
            var r = SquashBook.Get(_rule);
            _num.text = "RULE " + r.N + " OF 12";
            _head.text = r.Title;
            _words.text = r.Words;
            _picBox.Clear();
            _picBox.Add(new RuleDiagram(_rule));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Next rule"), (PromptBar.Btn.B, "Back")); }
    }
}
