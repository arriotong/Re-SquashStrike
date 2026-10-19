// ===========================================================
// Assets/SquashGame/Runtime/UI/HudLayer.cs
// P2-M1: the match HUD in UI Toolkit (replaces MatchHud's IMGUI),
// minimal, in the old SquashStrike's style:
//
//   score bug     YOU 7 | PAR 11 · GAMES 1-0 | OPPONENT 5 - lime
//                 digits, a dot by the server; under it the box
//   call          the referee's call as a broadcast lower third: the
//                 call big, "point X" under it; slides in (U5)
//   serve prompt  a glyph strip when it is a local player's serve
//   flips         a point ticks with a flip; a won game flashes GAMES
//   game over     the game result, the break countdown
//   banners       referee review, controller disconnected
//   LET?          over a local striker's head while the referee would
//                 give a let or a stroke for asking (P2-M7), with the key
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
        readonly Label _banner = new Label(), _sub = new Label(), _centre = new Label(), _prompt = new Label(), _promptKey = new Label();
        readonly VisualElement _third = new VisualElement(), _promptBox = new VisualElement();
        readonly VisualElement _let = new VisualElement();
        readonly Label _letKey = new Label();
        float _letHold;
        int _letFor = -1;
        readonly int[] _lastPoints = { -1, -1 };
        int _lastGames = -1;

        /// <summary>What the call says now (tests).</summary>
        public string BannerText { get { return _third.style.display == DisplayStyle.Flex ? _banner.text : null; } }
        public string PromptText { get { return _promptBox.style.display == DisplayStyle.Flex ? _prompt.text : null; } }
        /// <summary>The player the LET? chip is over now, or -1 (tests).</summary>
        public int LetPromptFor { get { return _let.style.display == DisplayStyle.Flex ? _letFor : -1; } }

        /// <summary>Would the referee give player <paramref name="i"/> a let or a stroke for asking now?</summary>
        public static bool WouldGrant(MatchSimulation m, int i)
        {
            var it = m.Interference;
            return PhysicsConfig.Current.Match.AppealsEnabled && m.Phase == MatchPhase.Rally && m.Referee.Outcome == null &&
                   it.Striker == i && it.ThisTurn != ObstructionKind.None && it.Decide().Call != InterferenceCall.NoLet;
        }

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
            _third.AddToClassList("hud-third"); _third.pickingMode = PickingMode.Ignore;
            _banner.AddToClassList("hud-third-call"); _sub.AddToClassList("hud-third-sub");
            _third.Add(_banner); _third.Add(_sub); Add(_third);
            _centre.AddToClassList("hud-centre"); Add(_centre);
            _promptBox.AddToClassList("hud-strip"); _promptBox.pickingMode = PickingMode.Ignore;
            _promptKey.AddToClassList("hud-strip-key"); _prompt.AddToClassList("hud-strip-text");
            _promptBox.Add(_promptKey); _promptBox.Add(_prompt); Add(_promptBox);
            _let.AddToClassList("hud-let"); _let.pickingMode = PickingMode.Ignore;
            var letWord = new Label("LET?"); letWord.AddToClassList("hud-let-word"); letWord.pickingMode = PickingMode.Ignore;
            _letKey.AddToClassList("hud-let-key");
            _let.Add(letWord); _let.Add(_letKey); _let.style.display = DisplayStyle.None; Add(_let);
            foreach (var l in new[] { _banner, _sub, _centre, _prompt, _promptKey, _meta, _letKey }) l.pickingMode = PickingMode.Ignore;
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
                if (_lastPoints[p] != s.Points[p])
                {
                    var l = _points[p];
                    if (_lastPoints[p] >= 0) { l.AddToClassList("sb-score--flip"); l.schedule.Execute(() => l.RemoveFromClassList("sb-score--flip")).StartingIn(160); }
                    _lastPoints[p] = s.Points[p];
                }
                _points[p].text = s.Points[p].ToString();
            }
            int games = s.Games[0] + s.Games[1];
            if (games != _lastGames)
            {
                if (_lastGames >= 0 && games > _lastGames) { _games.AddToClassList("sb-games--flash"); _games.schedule.Execute(() => _games.RemoveFromClassList("sb-games--flash")).StartingIn(900); }
                _lastGames = games;
            }
            _system.text = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints : "ENGLISH " + s.GameTarget;
            _games.text = "GAMES " + s.Games[0] + "-" + s.Games[1];
            _games.style.display = s.GamesToWin > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            string server = !twoPlayer && s.Server == r.HumanIndex ? "You serve" : names[s.Server] + " serves";
            _meta.text = server + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box";

            // ---- banners ----
            string banner = null, sub = null, centre = null, prompt = null, key = null;
            if (r.DeviceLost != null) centre = r.DeviceLost;
            if (r.ReplayActive)
            {
                banner = "REFEREE REVIEW";
                sub = r.ReplayCaption + "   ·   replay " + r.ReplaySpeed.ToString("0.0") + "x   ·   Esc skips";
            }
            else if (m.Phase == MatchPhase.RallyOver && m.LastOutcome != null)
            {
                RallyOutcome o = m.LastOutcome;
                banner = o.Reason;
                sub = (o.Kind == RallyEnd.Let || o.Winner < 0 ? "" : "POINT  " + names[o.Winner] + "   ·   ") + "V  replay";
            }
            if (m.Phase == MatchPhase.AwaitToss && (s.Server == r.HumanIndex || twoPlayer) && !r.Clock.Paused)
            {
                string who = twoPlayer ? names[s.Server] + " to serve" : "Your serve";
                key = twoPlayer && s.Server == 1 ? "A" : "LMB / A";
                prompt = who + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box   ·   tap: lob serve   ·   hold: hard serve";
            }
            if (m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver)
            {
                int w = s.GameWinner;
                centre = (m.Phase == MatchPhase.MatchOver ? "MATCH  " : "GAME  ") + names[w] + "  " + s.Points[w] + "-" + s.Points[1 - w];
                if (m.Phase == MatchPhase.GameOver)
                {
                    key = "Enter / Y";
                    prompt = "play now   ·   next game in " + Mathf.CeilToInt((float)m.BreakRemaining) + " s   ·   games " + s.Games[0] + "-" + s.Games[1];
                }
            }
            bool call = !string.IsNullOrEmpty(banner);
            if (call) { _banner.text = banner; _sub.text = sub ?? ""; }
            _third.style.display = call ? DisplayStyle.Flex : DisplayStyle.None;
            _third.EnableInClassList("hud-third--in", call);
            Show(_centre, centre);
            bool strip = !string.IsNullOrEmpty(prompt);
            _promptBox.style.display = strip ? DisplayStyle.Flex : DisplayStyle.None;
            if (strip) { _prompt.text = prompt; _promptKey.text = key ?? ""; _promptKey.style.display = key != null ? DisplayStyle.Flex : DisplayStyle.None; }
            _banner.style.fontSize = 40 * CallTextScale;
            LetChip(r, m, twoPlayer);
        }

        /// <summary>LET? over the local striker while asking would be granted; held a moment after it stops being so.</summary>
        void LetChip(MatchRunner r, MatchSimulation m, bool twoPlayer)
        {
            int who = -1;
            if (!r.ReplayActive)
                for (int i = 0; i < 2; i++)
                    if ((i == r.HumanIndex || twoPlayer) && WouldGrant(m, i)) who = i;
            if (who >= 0) { _letFor = who; _letHold = 0.5f; }
            else if (_letHold > 0 && m.Phase == MatchPhase.Rally && m.Referee.Outcome == null) _letHold -= Time.unscaledDeltaTime;
            else _letHold = 0;
            bool on = _letHold > 0 && _letFor >= 0;
            _let.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on || panel == null) return;
            _letKey.text = twoPlayer && _letFor == 1 ? "Y" : "Q / Y";
            var cam = Camera.main;
            if (cam == null || _letFor >= r.View.Count) return;
            Vector3 head = r.View.Athlete(_letFor).Pos + Vector3.up * 2.25f;
            if (cam.WorldToScreenPoint(head).z <= 0) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, head, cam);
            float w = float.IsNaN(_let.resolvedStyle.width) ? 0 : _let.resolvedStyle.width, h = float.IsNaN(_let.resolvedStyle.height) ? 0 : _let.resolvedStyle.height;
            _let.style.left = p.x - w * 0.5f; _let.style.top = p.y - h;      // centred over the head
        }
    }
}
