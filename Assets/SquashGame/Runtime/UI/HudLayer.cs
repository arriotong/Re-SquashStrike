// ===========================================================
// Assets/SquashGame/Runtime/UI/HudLayer.cs
// P2-M1: the match HUD in UI Toolkit (replaces MatchHud's IMGUI),
// minimal, in the old SquashStrike's style:
//
//   score bug     YOU 7 | PAR 11 · GAMES 1-0 | OPPONENT 5 - lime
//                 digits, a dot by the server; under it the box
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
        readonly Label[] _name = new Label[2], _points = new Label[2];
        readonly Label _system = new Label(), _games = new Label(), _meta = new Label();
        readonly Label _banner = new Label(), _sub = new Label(), _centre = new Label(), _prompt = new Label();

        /// <summary>What the banner says now (tests).</summary>
        public string BannerText { get { return _banner.style.display == DisplayStyle.Flex ? _banner.text : null; } }
        public string PromptText { get { return _prompt.style.display == DisplayStyle.Flex ? _prompt.text : null; } }

        public HudLayer()
        {
            AddToClassList("fe-layer");
            pickingMode = PickingMode.Ignore;
            _bug.AddToClassList("sb");
            var mid = new VisualElement(); mid.AddToClassList("sb-mid");
            _system.AddToClassList("sb-label"); _games.AddToClassList("sb-games");
            mid.Add(_system); mid.Add(_games);
            for (int p = 0; p < 2; p++)
            {
                var side = new VisualElement(); side.AddToClassList("sb-side");
                var nameRow = new VisualElement(); nameRow.AddToClassList("sb-name-row");
                _serve[p] = new VisualElement(); _serve[p].AddToClassList("sb-serve");
                _name[p] = new Label(); _name[p].AddToClassList("sb-name");
                _points[p] = new Label(); _points[p].AddToClassList("sb-score");
                nameRow.Add(_serve[p]); nameRow.Add(_name[p]);
                side.Add(nameRow); side.Add(_points[p]);
                _bug.Add(side);
                if (p == 0) _bug.Add(mid);
            }
            Add(_bug);
            _meta.AddToClassList("sb-meta"); Add(_meta);
            _banner.AddToClassList("hud-banner"); Add(_banner);
            _sub.AddToClassList("hud-sub"); Add(_sub);
            _centre.AddToClassList("hud-centre"); Add(_centre);
            _prompt.AddToClassList("hud-prompt"); Add(_prompt);
            foreach (var l in new[] { _banner, _sub, _centre, _prompt, _meta }) l.pickingMode = PickingMode.Ignore;
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
                _serve[p].EnableInClassList("sb-serve--on", s.Server == p);
                _name[p].text = names[p];
                _points[p].text = s.Points[p].ToString();
            }
            _system.text = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints : "ENGLISH " + s.GameTarget;
            _games.text = "GAMES " + s.Games[0] + "-" + s.Games[1];
            _games.style.display = s.GamesToWin > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            string server = !twoPlayer && s.Server == r.HumanIndex ? "You serve" : names[s.Server] + " serves";
            _meta.text = server + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box";

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
