// ===========================================================
// Assets/SquashGame/Runtime/UI/HudLayer.cs
// P2-M1: the match HUD in UI Toolkit (replaces MatchHud's IMGUI),
// broadcast style and minimal:
//
//   score bug     names, games, points, who serves (dot), box, scoring
//   call banner   the referee's call with its reason; "point X"
//   serve prompt  when it is a local player's serve
//   game over     the game result, the break countdown
//   banners       referee review, controller disconnected
//
// Reads state only. Hidden behind menus (the pause screen keeps it).
// ===========================================================

using SquashSim;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame
{
    public sealed class HudLayer : VisualElement
    {
        /// <summary>Accessibility setting: size of the call banner text.</summary>
        public static float CallTextScale = 1f;

        readonly VisualElement _bug = new VisualElement();
        readonly VisualElement[] _serve = new VisualElement[2];
        readonly Label[] _name = new Label[2], _games = new Label[2], _points = new Label[2];
        readonly Label _meta = new Label();
        readonly Label _banner = new Label(), _sub = new Label(), _centre = new Label(), _prompt = new Label();

        /// <summary>What the banner says now (tests).</summary>
        public string BannerText { get { return _banner.style.display == DisplayStyle.Flex ? _banner.text : null; } }
        public string PromptText { get { return _prompt.style.display == DisplayStyle.Flex ? _prompt.text : null; } }

        public HudLayer()
        {
            AddToClassList("fe-layer");
            pickingMode = PickingMode.Ignore;
            _bug.AddToClassList("hud-bug");
            for (int p = 0; p < 2; p++)
            {
                var row = new VisualElement(); row.AddToClassList("hud-row");
                _serve[p] = new VisualElement(); _serve[p].AddToClassList("hud-serve");
                _name[p] = new Label(); _name[p].AddToClassList("hud-name");
                _games[p] = new Label(); _games[p].AddToClassList("hud-games");
                _points[p] = new Label(); _points[p].AddToClassList("hud-points");
                row.Add(_serve[p]); row.Add(_name[p]); row.Add(_games[p]); row.Add(_points[p]);
                _bug.Add(row);
            }
            _meta.AddToClassList("hud-meta");
            _bug.Add(_meta);
            Add(_bug);
            _banner.AddToClassList("hud-banner"); Add(_banner);
            _sub.AddToClassList("hud-sub"); Add(_sub);
            _centre.AddToClassList("hud-centre"); Add(_centre);
            _prompt.AddToClassList("hud-prompt"); Add(_prompt);
            foreach (var l in new[] { _banner, _sub, _centre, _prompt }) l.pickingMode = PickingMode.Ignore;
        }

        static void Show(Label l, string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            l.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on && l.text != text) l.text = text;
        }

        public void Refresh(MatchRunner r, bool allowed)
        {
            bool visible = allowed && r != null && r.Sim != null && !r.Demo && !r.InSetup;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            MatchSimulation m = r.Sim;
            MatchScore s = m.Score;
            var names = r.Names;
            bool twoPlayer = r.Settings.TwoPlayer;

            // ---- score bug ----
            for (int p = 0; p < 2; p++)
            {
                _serve[p].EnableInClassList("hud-serve--on", s.Server == p);
                _name[p].text = names[p];
                _games[p].text = s.GamesToWin > 1 ? s.Games[p].ToString() : "";
                _points[p].text = s.Points[p].ToString();
            }
            string system = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints : "ENGLISH to " + s.GameTarget;
            string server = !twoPlayer && s.Server == r.HumanIndex ? "You serve" : names[s.Server] + " serves";
            _meta.text = system + "   ·   " + server + ", " + (s.ServeSide > 0 ? "right" : "left") + " box";

            // ---- banners ----
            string banner = null, sub = null, centre = null, prompt = null;
            if (r.DeviceLost != null) centre = r.DeviceLost;
            if (r.ReplayActive)
            {
                banner = "REFEREE REVIEW   " + r.ReplayCaption;
                sub = "replay " + r.ReplaySpeed.ToString("0.0") + "x   (Esc skips)";
            }
            else if (m.Phase == MatchPhase.RallyOver && m.LastOutcome != null)
            {
                RallyOutcome o = m.LastOutcome;
                banner = o.Kind == RallyEnd.Let || o.Winner < 0 ? o.Reason : o.Reason + "   —   point " + names[o.Winner];
                sub = "V: replay";
            }
            if (m.Phase == MatchPhase.AwaitToss && (s.Server == r.HumanIndex || twoPlayer) && !r.Clock.Paused)
            {
                string who = twoPlayer ? names[s.Server] + " to serve" : "Your serve";
                string hit = twoPlayer && s.Server == 1 ? "A" : "LMB / A";
                prompt = who + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box.   " + hit + ":  tap for a lob serve, hold for a hard serve.";
            }
            if (m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver)
            {
                int w = s.GameWinner;
                centre = (m.Phase == MatchPhase.MatchOver ? "MATCH  " : "GAME  ") + names[w] + "  " + s.Points[w] + "-" + s.Points[1 - w];
                if (m.Phase == MatchPhase.GameOver)
                    prompt = "Next game in " + Mathf.CeilToInt((float)m.BreakRemaining) + " s   (games " + s.Games[0] + "-" + s.Games[1] + ")   Enter / Y: play now";
            }
            Show(_banner, banner); Show(_sub, sub); Show(_centre, centre); Show(_prompt, prompt);
            _banner.style.fontSize = 28 * CallTextScale;
        }
    }
}
