// ===========================================================
// Assets/SquashGame/Runtime/UI/ArcadeHud.cs
// P2-M2: the Target Arcade HUD, broadcast style like the score bug:
//
//   panel     challenge + level, SCORE, the clock or balls left,
//             combo, the next star
//   call      Shot Menu / Boast-Drive: the shot to play ("PLAY  DROP")
//   callout   brief: "+300", "x2 COMBO", "MISSED", "WRONG SHOT", "FAULT"
//   prompt    the serve (Serve Targets), ghosting
// ===========================================================

using SquashSim.Arcade;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class ArcadeHud : VisualElement
    {
        readonly VisualElement _panel = new VisualElement();
        readonly Label _name = new Label(), _score = new Label(), _clock = new Label(), _combo = new Label(), _next = new Label();
        readonly Label _call = new Label(), _callout = new Label(), _prompt = new Label();
        float _calloutAge = 99f;

        /// <summary>The last callout (tests).</summary>
        public string LastCallout { get; private set; }

        public ArcadeHud(ArcadeRunner runner)
        {
            AddToClassList("fe-layer");
            pickingMode = PickingMode.Ignore;
            _panel.AddToClassList("hud-bug");
            _panel.AddToClassList("ah-panel");
            _name.AddToClassList("ah-name");
            _score.AddToClassList("ah-score");
            _clock.AddToClassList("ah-line");
            _combo.AddToClassList("ah-line");
            _next.AddToClassList("hud-meta");
            _panel.Add(_name); _panel.Add(_score); _panel.Add(_clock); _panel.Add(_combo); _panel.Add(_next);
            Add(_panel);
            _call.AddToClassList("hud-banner"); Add(_call);
            _callout.AddToClassList("ah-callout"); Add(_callout);
            _prompt.AddToClassList("hud-prompt"); Add(_prompt);
            runner.Judged += OnJudged;
        }

        void OnJudged(ArcadeEvent e)
        {
            string text = null;
            switch (e.Kind)
            {
                case ArcadeEventKind.Hit:
                    text = "+" + e.Points + (e.Great ? "   GREAT" : "") + (e.Combo >= 3 ? "   x" + Multiplier(e.Combo) + " COMBO" : "");
                    break;
                case ArcadeEventKind.Miss: case ArcadeEventKind.Fault: text = e.Note; break;
            }
            if (text == null) return;
            LastCallout = text;
            _callout.text = text;
            _calloutAge = 0f;
        }

        static string Multiplier(int combo) { double m = ArcadeSession.ComboMultiplier(combo); return m == 1.5 ? "1.5" : ((int)m).ToString(); }

        public void Refresh(ArcadeRunner r, bool allowed)
        {
            var s = r != null ? r.Session : null;
            bool visible = allowed && s != null && !s.Over;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            var c = s.Challenge;
            _name.text = (c.Mode == ArcadeMode.Drill ? "DRILL  ·  " : "") + c.Name.ToUpperInvariant() + (c.Mode == ArcadeMode.Drill ? "" : "  ·  " + c.LevelName.ToUpperInvariant());
            _score.text = c.Mode == ArcadeMode.Streak ? "STREAK  " + s.Score : c.Mode == ArcadeMode.Drill ? s.Score + " / " + Mathf.Max(s.Attempts, 1) : s.Score.ToString("N0");
            if (s.TimeLeft >= 0) { int t = Mathf.CeilToInt((float)s.TimeLeft); _clock.text = (t / 60) + ":" + (t % 60).ToString("00"); }
            else if (s.BallsLeft >= 0) _clock.text = "Balls left  " + s.BallsLeft;
            else _clock.text = "";
            _combo.text = s.Combo >= 2 ? "Combo  " + s.Combo + (s.Combo >= 3 ? "   x" + Multiplier(s.Combo) : "") : "";
            int stars = s.Stars;
            _next.text = stars >= 3 ? "★★★" : c.Stars[stars] > 0 ? new string('★', stars) + new string('☆', 3 - stars) + "   next star at " + c.Stars[stars] : "";

            string call = s.CalledShot != null ? "PLAY   " + s.CalledShot.ToUpperInvariant() : null;
            _call.style.display = call != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (call != null) _call.text = call;

            string prompt = null;
            if (s.Serving) prompt = "Serve from the " + (s.ServeSide > 0 ? "right" : "left") + " box:  tap for a lob serve, hold for a hard serve.";
            else if (c.Drill == DrillKind.Ghosting) prompt = "Run to the lit spot, then back to the T.";
            _prompt.style.display = prompt != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (prompt != null) _prompt.text = prompt;

            _calloutAge += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(1f - (_calloutAge - 0.7f) / 0.4f);
            _callout.style.opacity = a;
            _callout.style.display = _calloutAge < 1.1f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
