// ===========================================================
// Assets/SquashGame/Runtime/UI/GauntletIntroScreen.cs
// 2026-11-23 (the user: "add a cutscene like before floor 1 because it is a
// bit like getting into it too quickly i didn't even realised it started,
// maybe try adding like a big GAUNTLET and then some rules and playing steps
// in short example like ONE PERSON INFINATE LEVELS ... covering the picking
// cards and infinate levels"): a run's opening - THE GAUNTLET slams in, then
// the rules one at a time, each a big line and a short one under it; Enter
// (or A) goes on, Esc (or B) jumps to the end; then floor 1.
// ===========================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class GauntletIntroScreen : GameScreen
    {
        protected override string Kicker { get { return ""; } }
        protected override string Title { get { return ""; } }
        protected override bool ShowChip { get { return false; } }
        public override MenuWorld Backdrop { get { return MenuWorld.Gauntlet; } }

        /// <summary>The steps: the big line, the short one.</summary>
        public static readonly (string big, string small)[] Steps =
        {
            ("ONE PLAYER  ·  ENDLESS FLOORS", "You against the floors, one after another - as far as you can go"),
            ("HIT THE TARGET", "Every floor gives you a target score and a few rallies to reach it"),
            ("WIN A FLOOR  ·  PICK A CARD", "Each floor won, choose one of three cards - your build grows all run"),
            ("STREAKS POWER UP", "Win rallies in a row to fill your power - then spin the wheel for more"),
            ("EVERY 5TH FLOOR  ·  A BOSS", "A pro with their own shots and a twist - beat them for a bigger reward"),
            ("CLEAR FLOOR " + SquashSim.Modes.Gauntlet.WinFloor + " TO WIN", "Then take the win - or keep going into the endless floors"),
            ("MISS A TARGET  ·  THE RUN IS OVER", "Quit or leave and it's over too - one run, no second tries"),
        };
        public const float StepSeconds = 2.6f;

        Label _title, _big, _small, _count;
        VisualElement _card;
        int _step = -1;
        float _t;
        PrimaryItem _go;

        /// <summary>The step shown now (-1: the title; tests).</summary>
        public int Step { get { return _step; } }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "gi-stage");
            _title = Kit.L(stage, "THE GAUNTLET", "gi-title");
            _card = Kit.Box(stage, "gi-card");
            _count = Kit.L(_card, "", "gi-count");
            _big = Kit.L(_card, "", "gi-big");
            _small = Kit.L(_card, "", "gi-small");
            _card.style.display = DisplayStyle.None;
            _go = AddTo(Root, new PrimaryItem("Next", Next));
            Juice.FadeIn(_title);
        }

        void Next()
        {
            if (_step >= Steps.Length - 1) { Begin(); return; }
            Show(_step + 1);
        }

        void Show(int i)
        {
            _step = i; _t = 0f;
            _title.EnableInClassList("gi-title--small", true);
            _card.style.display = DisplayStyle.Flex;
            _count.text = (i + 1) + " / " + Steps.Length;
            _big.text = Steps[i].big; _small.text = Steps[i].small;
            _card.RemoveFromClassList("gi-card--in");
            _card.schedule.Execute(() => _card.AddToClassList("gi-card--in")).StartingIn(16);
            _go.SetLabel(i == Steps.Length - 1 ? "Start floor 1" : "Next");
        }

        void Begin() { Fe.Replace(new GauntletScreen()); }

        public override void Tick(float dt)
        {
            _t += Time.unscaledDeltaTime;
            if (_step < 0 ? _t > 1.4f : _step < Steps.Length - 1 && _t > StepSeconds) Show(_step + 1);   // (it plays on by itself; the last waits)
        }

        /// <summary>Esc / B: straight to the end (the last rule, then floor 1).</summary>
        public override void Back() { if (_step < Steps.Length - 1) Show(Steps.Length - 1); else Begin(); }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Next"), (PromptBar.Btn.B, "Skip")); }
    }
}
