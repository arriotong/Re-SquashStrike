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
//   PR5           GAME BALL / MATCH BALL under the bug; a rally counter
//                 once a rally gets long; a walk-on card as a match starts
//   chaos         the Gauntlet's POWER meter, ULTIMATE READY, the pop-ups
//                 (TIME STOP!, INFERNO!, 6 POINTS!), the time-stop tint
//                 (the chaos remake, 2026-10-28: the sim's ChaosLog)
//
// Reads state only. Hidden behind menus (the pause screen keeps it).
// ===========================================================

using SquashSim;
using SquashGame.UI;
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
        readonly VisualElement[] _stamina = new VisualElement[2];               // P7 phase 1: each player's stamina, under the score
        /// <summary>The stamina bars' fill, 0..1 (tests).</summary>
        public float StaminaShown(int p) { return _stamina[p] == null ? -1f : _stamina[p].resolvedStyle.width / 64f; }
        readonly Label _system = new Label(), _games = new Label(), _meta = new Label();
        readonly Label _banner = new Label(), _sub = new Label(), _centre = new Label(), _prompt = new Label(), _promptKey = new Label();
        readonly VisualElement _third = new VisualElement(), _promptBox = new VisualElement();
        readonly VisualElement _let = new VisualElement();
        readonly Label _letKey = new Label();
        float _letHold;
        int _letFor = -1;
        readonly int[] _lastPoints = { -1, -1 };
        int _lastGames = -1;
        // PR5
        readonly Label _ball = new Label(), _rally = new Label(), _introNames = new Label(), _introSub = new Label();
        readonly Label _net = new Label();                       // P2-M8: ping, input delay; waiting / reconnecting
        readonly Label _chat = new Label();                      // batch 4: quick chat
        public string ChatShown { get { return _chat.style.display == DisplayStyle.Flex ? _chat.text : null; } }
        public string NetText { get { return _net.style.display == DisplayStyle.Flex ? _net.text : null; } }
        readonly VisualElement _intro = new VisualElement();
        // camera work: a cutscene's title card
        readonly VisualElement _cine = new VisualElement();
        readonly Label _cineTitle = new Label(), _cineSub = new Label();
        public string CineText { get { return _cine.style.display == DisplayStyle.Flex ? _cineTitle.text : null; } }
        // the chaos remake
        readonly VisualElement _power = new VisualElement(), _powerFill = new VisualElement(), _freeze = new VisualElement();
        // 2026-11-20 Quick Match's specials (after Mario Tennis): the meters top right, Focus dims the screen, a Special Shot flashes
        readonly SpecialMeter _spMine = new SpecialMeter(true), _spThem = new SpecialMeter(false);
        readonly VisualElement _focusDim = new VisualElement(), _flash = new VisualElement();
        readonly Label _focusWord = new Label("FOCUS");
        float _flashLeft, _dimLevel;
        /// <summary>The specials' state (tests): your meter shown, the dim's strength.</summary>
        public bool SpecialMeterShown { get { return _spMine.style.display == DisplayStyle.Flex; } }
        public float FocusDim { get { return _dimLevel; } }
        readonly Label _powerLabel = new Label(), _pop = new Label(), _popSmall = new Label(), _freezeWord = new Label("TIME STOP");
        MatchSimulation _chaosFor;
        int _chaosRead;
        float _popLeft, _popSmallLeft;
        public string PopText { get { return _pop.style.display == DisplayStyle.Flex ? _pop.text : null; } }
        public string PowerText { get { return _power.style.display == DisplayStyle.Flex ? _powerLabel.text : null; } }
        MatchSimulation _introFor;
        float _introLeft;
        /// <summary>PR5: a rally shows its shot count from this many shots.</summary>
        public const int RallyCountFrom = 6;
        public string BallText { get { return _ball.style.display == DisplayStyle.Flex ? _ball.text : null; } }
        public string RallyText { get { return _rally.style.display == DisplayStyle.Flex ? _rally.text : null; } }
        public bool IntroShown { get { return _intro.style.display == DisplayStyle.Flex; } }

        /// <summary>PR5: would player p win the game with the next point? (Presentation only.)</summary>
        public static bool GameBall(MatchScore s, int p)
        {
            if (s.GameOver || s.MatchOver) return false;
            int me = s.Points[p], them = s.Points[1 - p];
            return s.System == ScoringSystem.Par ? me + 1 >= s.GameTarget && me + 1 - them >= 2
                                                 : s.Server == p && me + 1 >= s.GameTarget;
        }

        /// <summary>What the call says now (tests).</summary>
        public string BannerText { get { return _third.style.display == DisplayStyle.Flex ? _banner.text : null; } }
        public string BannerSub { get { return _third.style.display == DisplayStyle.Flex ? _sub.text : null; } }
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
                var bar = new VisualElement(); bar.AddToClassList("sb-stamina");
                _stamina[p] = new VisualElement(); _stamina[p].AddToClassList("sb-stamina-fill");
                bar.Add(_stamina[p]); side.Add(bar);
                _plus[p] = new Label("+1"); _plus[p].AddToClassList("sb-plus"); _plus[p].pickingMode = PickingMode.Ignore; side.Add(_plus[p]);
                _side[p] = side;
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
            _ball.AddToClassList("hud-ball"); Add(_ball);
            _rally.AddToClassList("hud-rally"); Add(_rally);
            _net.AddToClassList("hud-net"); _net.pickingMode = PickingMode.Ignore; Add(_net);
            _chat.AddToClassList("hud-chat"); _chat.pickingMode = PickingMode.Ignore; Add(_chat);
            _intro.AddToClassList("hud-intro"); _intro.pickingMode = PickingMode.Ignore;
            _introNames.AddToClassList("hud-intro-names"); _introSub.AddToClassList("hud-intro-sub");
            _intro.Add(_introNames); _intro.Add(_introSub); _intro.style.display = DisplayStyle.None; Add(_intro);
            _cine.AddToClassList("hud-cine"); _cineTitle.AddToClassList("hud-cine-title"); _cineSub.AddToClassList("hud-cine-sub");
            _cine.Add(_cineTitle); _cine.Add(_cineSub); _cine.style.display = DisplayStyle.None; _cine.pickingMode = PickingMode.Ignore; Add(_cine);
            _cineTitle.pickingMode = PickingMode.Ignore; _cineSub.pickingMode = PickingMode.Ignore;
            // the chaos remake: the time-stop tint under everything, the meter under the bug, the pop-ups centre
            _freeze.AddToClassList("hud-freeze"); _freezeWord.AddToClassList("hud-freeze-word"); _freeze.Add(_freezeWord); _freeze.style.display = DisplayStyle.None; Insert(0, _freeze);
            _power.AddToClassList("hud-power"); _powerLabel.AddToClassList("hud-power-label");
            var track = new VisualElement(); track.AddToClassList("hud-power-track"); _powerFill.AddToClassList("hud-power-fill"); track.Add(_powerFill);
            _power.Add(_powerLabel); _power.Add(track); _power.style.display = DisplayStyle.None; Add(_power);
            _focusDim.AddToClassList("hud-focus-dim"); _focusDim.style.backgroundImage = Background.FromTexture2D(Vignette()); _focusDim.style.opacity = 0;
            _focusWord.AddToClassList("hud-focus-word"); _focusDim.Add(_focusWord); _focusDim.pickingMode = PickingMode.Ignore; _focusWord.pickingMode = PickingMode.Ignore;
            Insert(0, _focusDim);
            _flash.AddToClassList("hud-flash"); _flash.style.opacity = 0; _flash.pickingMode = PickingMode.Ignore; Add(_flash);
            _spMine.style.display = DisplayStyle.None; _spThem.style.display = DisplayStyle.None; Add(_spMine); Add(_spThem);
            _pop.AddToClassList("hud-pop"); _pop.style.display = DisplayStyle.None; Add(_pop);
            _popSmall.AddToClassList("hud-pop-small"); _popSmall.style.display = DisplayStyle.None; Add(_popSmall);
            _floorBar.style.display = DisplayStyle.None; Add(_floorBar);
            // 2026-11-21 the polish pass: the callouts (3 IN A ROW, COMEBACK), the match won's title, the confetti
            _callout.AddToClassList("hud-callout"); _callout.pickingMode = PickingMode.Ignore; _callout.style.display = DisplayStyle.None; Add(_callout);
            _confetti.AddToClassList("hud-confetti"); _confetti.pickingMode = PickingMode.Ignore; Add(_confetti);
            _win.AddToClassList("hud-win"); _win.pickingMode = PickingMode.Ignore; _win.style.display = DisplayStyle.None;
            _winTitle.AddToClassList("hud-win-title"); _winSub.AddToClassList("hud-win-sub");
            _winTitle.pickingMode = PickingMode.Ignore; _winSub.pickingMode = PickingMode.Ignore;
            _win.Add(_winTitle); _win.Add(_winSub); Add(_win);
            foreach (var l in new[] { _banner, _sub, _centre, _prompt, _promptKey, _meta, _letKey, _ball, _rally, _introNames, _introSub, _powerLabel, _pop, _popSmall, _freezeWord }) l.pickingMode = PickingMode.Ignore;
            foreach (var v in new[] { _power, _powerFill, track, _freeze }) v.pickingMode = PickingMode.Ignore;
        }

        static void Show(Label l, string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            l.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on && l.text != text) l.text = text;
        }

        // ---------------- 2026-11-21 the polish pass ----------------
        readonly Label[] _plus = new Label[2];
        readonly VisualElement[] _side = new VisualElement[2];
        readonly Label _callout = new Label(), _winTitle = new Label(), _winSub = new Label();
        readonly VisualElement _confetti = new VisualElement(), _win = new VisualElement();
        float _calloutLeft;
        int _seenRallies = -1, _streak, _streakWho = -1, _gamesSeen = -1;
        readonly int[] _down = new int[2];
        bool _comebackCalled;
        object _celebFor;
        /// <summary>The last callout (tests), the match-won title while it shows.</summary>
        public string LastCallout { get; private set; }
        public string WinTitle { get { return _win.style.display == DisplayStyle.Flex ? _winTitle.text : null; } }

        void Callout(string text)
        {
            LastCallout = text;
            _callout.text = text;
            _callout.style.display = DisplayStyle.Flex;
            _callout.RemoveFromClassList("hud-callout--in");
            _callout.schedule.Execute(() => _callout.AddToClassList("hud-callout--in")).StartingIn(16);
            _calloutLeft = 1.8f;
        }

        /// <summary>A rally won: the streaks, the comebacks.</summary>
        void Callouts(MatchRunner r, MatchSimulation m, MatchScore s, string[] names)
        {
            int games = s.Games[0] + s.Games[1];
            if (games != _gamesSeen) { _gamesSeen = games; _down[0] = _down[1] = 0; _comebackCalled = false; }
            if (_seenRallies < 0 || m.History.Count < _seenRallies) { _seenRallies = m.History.Count; _streak = 0; _streakWho = -1; return; }
            if (m.History.Count == _seenRallies) return;
            _seenRallies = m.History.Count;
            var o = m.History[m.History.Count - 1];
            if (o == null || o.Winner < 0 || r.Floor != null) return;
            int w = o.Winner;
            if (w == _streakWho) _streak++; else { _streak = 1; _streakWho = w; }
            bool you = !r.Settings.TwoPlayer && w == r.HumanIndex;
            string who = you ? "" : names[w].ToUpperInvariant() + ":  ";
            if (m.Phase == MatchPhase.MatchOver || m.Phase == MatchPhase.GameOver) return;
            // (this game's points: down by 4 or more, then level or ahead - a comeback)
            for (int p = 0; p < 2; p++) _down[p] = Mathf.Max(_down[p], s.Points[1 - p] - s.Points[p]);
            if (!_comebackCalled && _down[w] >= 4 && s.Points[w] >= s.Points[1 - w]) { _comebackCalled = true; Callout(who + "COMEBACK!"); return; }
            if (_streak == 3 || _streak == 5 || (_streak >= 7 && _streak % 2 == 1)) Callout(who + _streak + " IN A ROW!");
        }

        /// <summary>The match won: the title slams in, the confetti falls (once).</summary>
        void Celebrate(MatchRunner r, MatchSimulation m, MatchScore s, string[] names)
        {
            bool on = m.Phase == MatchPhase.MatchOver && r.CelebrationTime >= 0 && !r.ReplayActive && Juice.Big && r.OnlineMatch == null;
            if (!on) { _win.style.display = DisplayStyle.None; _win.RemoveFromClassList("hud-win--in"); return; }
            int w = s.MatchWinner;
            if (w < 0) return;
            bool you = !r.Settings.TwoPlayer && w == r.HumanIndex;
            _winTitle.text = you ? "YOU WIN!" : r.Settings.TwoPlayer ? names[w].ToUpperInvariant() + " WINS!" : "MATCH";
            _winSub.text = (you || r.Settings.TwoPlayer ? "" : names[w] + " wins   ·   ") + "games " + s.Games[w] + "-" + s.Games[1 - w];
            _win.EnableInClassList("hud-win--lost", !you && !r.Settings.TwoPlayer);
            if (r.CelebrationTime > 0.7f && _win.style.display == DisplayStyle.None)
            {
                _win.style.display = DisplayStyle.Flex;
                _win.schedule.Execute(() => _win.AddToClassList("hud-win--in")).StartingIn(16);
                Sounds.Play("stamp");
            }
            if (!ReferenceEquals(_celebFor, m) && r.CelebrationTime > 0.8f && (you || r.Settings.TwoPlayer)) { _celebFor = m; Confetti(); }
        }

        static readonly Color[] ConfettiColours =
        {
            new Color32(200, 255, 61, 255), new Color32(255, 90, 74, 255), new Color32(60, 170, 255, 255),
            new Color32(255, 196, 64, 255), new Color32(190, 110, 255, 255), new Color32(255, 255, 255, 255)
        };

        void Confetti()
        {
            var lay = _confetti.layout;
            float w = lay.width > 10 ? lay.width : 1920f, h = lay.height > 10 ? lay.height : 1080f;
            for (int i = 0; i < 90; i++)
            {
                var c = new VisualElement(); c.AddToClassList("hud-confetti-bit"); c.pickingMode = PickingMode.Ignore;
                c.style.backgroundColor = ConfettiColours[i % ConfettiColours.Length];
                float x = Random.Range(0f, w), y = -Random.Range(20f, h * 0.6f), vy = Random.Range(160f, 320f), sway = Random.Range(20f, 60f);
                float spin = Random.Range(-540f, 540f), phase = Random.value * 6f, t = 0, life = Random.Range(3.2f, 4.6f);
                c.style.width = Random.Range(8f, 14f); c.style.height = Random.Range(12f, 22f);
                _confetti.Add(c);
                c.schedule.Execute(() =>
                {
                    t += 0.016f;
                    c.style.left = x + Mathf.Sin(phase + t * 3f) * sway;
                    c.style.top = y + vy * t;
                    c.style.rotate = new Rotate(spin * t);
                    c.style.opacity = Mathf.Clamp01((life - t) * 2f);
                    if (t >= life) c.RemoveFromHierarchy();
                }).Every(16).Until(() => c.parent == null);
            }
        }

        /// <summary>2026-11-21 the Gauntlet floor's bar (target, score, rallies left, multiplier, wheel).</summary>
        readonly SquashGame.UI.FloorBar _floorBar = new SquashGame.UI.FloorBar();
        public SquashGame.UI.FloorBar FloorBar { get { return _floorBar; } }

        public void Refresh(MatchRunner r, bool allowed)
        {
            bool visible = allowed && r != null && r.Sim != null && !r.Demo && !r.InSetup && r.Puzzle == null;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            MatchSimulation m = r.Sim;
            MatchScore s = m.Score;
            var names = r.Names;
            bool twoPlayer = r.Settings.TwoPlayer;
            _floorBar.Set(r);
            _bug.style.display = r.Floor != null ? DisplayStyle.None : DisplayStyle.Flex;       // a floor: its bar, not a game's score

            // ---- score bug ----
            for (int p = 0; p < 2; p++)
            {
                _serve[p].EnableInClassList("sb-serve--on", s.Server == p);
                _name[p].text = names[p];
                if (_lastPoints[p] != s.Points[p])
                {
                    var l = _points[p];
                    if (_lastPoints[p] >= 0) { l.AddToClassList("sb-score--flip"); l.schedule.Execute(() => l.RemoveFromClassList("sb-score--flip")).StartingIn(160); }
                    if (_lastPoints[p] >= 0 && s.Points[p] > _lastPoints[p] && Juice.On)
                    {
                        // 2026-11-21 the polish pass: a "+1" floats up off the score, the side flashes
                        var plus = _plus[p]; var side = _side[p];
                        plus.text = "+" + (s.Points[p] - _lastPoints[p]);
                        plus.RemoveFromClassList("sb-plus--go"); plus.AddToClassList("sb-plus--on");
                        plus.schedule.Execute(() => plus.AddToClassList("sb-plus--go")).StartingIn(30);
                        plus.schedule.Execute(() => plus.RemoveFromClassList("sb-plus--on")).StartingIn(700);
                        side.AddToClassList("sb-side--won");
                        side.schedule.Execute(() => side.RemoveFromClassList("sb-side--won")).StartingIn(450);
                    }
                    _lastPoints[p] = s.Points[p];
                }
                _points[p].text = s.Points[p].ToString();
                // P7 phase 1: the stamina bar - amber when tired, red when spent
                float st = Mathf.Clamp01((float)m.Players[p].Player.Stamina);
                _stamina[p].style.width = 64f * st;
                float tiredAt = (float)PhysicsConfig.Current.Fatigue.TiredBelow;
                _stamina[p].EnableInClassList("sb-stamina-fill--tired", tiredAt > 0 && st < tiredAt);
                _stamina[p].EnableInClassList("sb-stamina-fill--spent", tiredAt > 0 && st < 0.35f);
            }
            int games = s.Games[0] + s.Games[1];
            if (games != _lastGames)
            {
                if (_lastGames >= 0 && games > _lastGames) { _games.AddToClassList("sb-games--flash"); _games.schedule.Execute(() => _games.RemoveFromClassList("sb-games--flash")).StartingIn(900); }
                _lastGames = games;
            }
            _system.text = s.System == ScoringSystem.Par ? "TO " + PhysicsConfig.Current.Match.ParPoints : "TO " + s.GameTarget;
            _games.text = "GAMES " + s.Games[0] + "-" + s.Games[1];
            _games.style.display = s.GamesToWin > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            string server = !twoPlayer && s.Server == r.HumanIndex ? "You serve" : names[s.Server] + " serves";
            _meta.text = (m.Phase == MatchPhase.WarmUp ? "Knock-up" : server + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box") +
                         (r.Settings.FocusCharges > 0 ? "   ·   FOCUS " + r.FocusLeft + "  (" + IntentSource.FocusKey + ")" : "");     // P5

            // ---- banners ----
            string banner = null, sub = null, centre = null, prompt = null, key = null;
            if (r.DeviceLost != null) centre = r.DeviceLost;
            if (r.ReplayActive && r.CinematicKind != null) { }                                    // a cutscene: its own captions (Cinematic)
            else if (r.ReplayActive)
            {
                bool review = r.ReviewingCall || r.MomentCaption == null;
                banner = review ? "REFEREE REVIEW" : "REPLAY";
                string what = review ? r.ReplayCaption : r.MomentCaption == "REPLAY" ? null : r.MomentCaption;
                sub = (what != null ? what + "   ·   " : "") + r.ReplaySpeed.ToString("0.0") + "x   ·   any key skips";
            }
            else if (m.Phase == MatchPhase.RallyOver && m.LastOutcome != null)
            {
                RallyOutcome o = m.LastOutcome;
                string point = o.Kind == RallyEnd.Let || o.Winner < 0 ? "" : "POINT  " + names[o.Winner] + "   ·   ";
                if (UI.CallWords.IsCall(o))
                {
                    // P2-M7: the decision big, why in plain words, who gets the point
                    banner = UI.CallWords.Decision(o);
                    sub = UI.CallWords.Reason(o, names, r.LastCallStriker, twoPlayer ? -1 : r.HumanIndex) + "   ·   " + point + (r.OnlineMatch != null ? "" : "V  review");
                }
                else
                {
                    banner = o.Reason;
                    sub = point + (r.OnlineMatch != null ? "" : "V  replay");
                }
            }
            if (m.Phase == MatchPhase.AwaitToss && (s.Server == r.HumanIndex || twoPlayer) && !r.Clock.Paused && !r.ReplayActive)
            {
                string who = twoPlayer ? names[s.Server] + " to serve" : "Your serve";
                key = twoPlayer && s.Server == 1 ? IntentSource.BindingLabel("Hit", true) : IntentSource.Keys("Hit");
                prompt = who + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box   ·   tap: lob serve   ·   hold: hard serve";
            }
            // batch 4: the knock-up - how long, how to skip it; 2026-10-28: the ball in your hand, drop and hit
            if (m.Phase == MatchPhase.WarmUp && !r.ReplayActive)
            {
                string hand = m.WarmUpInHand ? (twoPlayer ? names[m.WarmUpOwner] + ": ball in hand" : m.WarmUpOwner == r.HumanIndex ? "Ball in hand: " + IntentSource.Keys("Hit") + " to drop and hit" : "") : "Knock up together";
                key = "Enter / Y";
                prompt = "WARM-UP   ·   " + (hand.Length > 0 ? hand + "   ·   " : "") + Mathf.CeilToInt((float)m.WarmUpRemaining) + " s   ·   skip" +
                         (r.OnlineMatch != null ? " (either of you)" : "") + (r.OnlineMatch != null ? "   ·   1-4: quick chat" : "");
            }
            if (m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver)
            {
                int w = s.GameWinner;
                centre = (m.Phase == MatchPhase.MatchOver ? "MATCH  " : "GAME  ") + names[w] + "  " + s.Points[w] + "-" + s.Points[1 - w];
                if (m.Phase == MatchPhase.GameOver)
                {
                    key = "Enter / Y";
                    prompt = "play now   ·   next game in " + Mathf.CeilToInt((float)m.BreakRemaining) + " s   ·   games " + s.Games[0] + "-" + s.Games[1];
                    // P3-R1: the XP the match has earned so far (the rallies; the win and great hits come at full time)
                    if (!twoPlayer)
                    {
                        int played = 0, won = 0;
                        foreach (var o in m.History) { if (o.Winner < 0) continue; played++; if (o.Winner == r.HumanIndex) won++; }
                        prompt += "   ·   +" + Progression.Rewards.SoFar(played, won) + " XP so far";
                    }
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
            Callouts(r, m, s, names);
            if (_calloutLeft > 0) { _calloutLeft -= Time.unscaledDeltaTime; if (_calloutLeft <= 0) { _callout.style.display = DisplayStyle.None; _callout.RemoveFromClassList("hud-callout--in"); } }
            Celebrate(r, m, s, names);
            if (_win.style.display == DisplayStyle.Flex) { _centre.style.display = DisplayStyle.None; _meta.style.display = DisplayStyle.None; }
            else _meta.style.display = DisplayStyle.Flex;
            LetChip(r, m, twoPlayer);
            NetChip(r);
            Broadcast(r, m, s, names);
            Chaos(r, m, twoPlayer);
        }

        /// <summary>The Gauntlet chaos remake: your POWER meter (with an ultimate card), the pop-ups for what the cards just
        /// did (from the sim's ChaosLog), the blue of a time stop.</summary>
        void Chaos(MatchRunner r, MatchSimulation m, bool twoPlayer)
        {
            if (_chaosFor != m) { _chaosFor = m; _chaosRead = m.Chaos.Count; _popLeft = 0; _popSmallLeft = 0; }
            int me = twoPlayer ? 0 : r.HumanIndex;
            var mine = me >= 0 && me < 2 ? m.Players[me].Player.Mods : null;
            // Quick Match's specials: the Mario-style meters top right instead of the Gauntlet's POWER bar
            var them = me >= 0 && me < 2 ? m.Players[1 - me].Player.Mods : null;
            bool special = mine != null && mine.Level("special") > 0 && !r.ReplayActive;
            _spMine.style.display = special ? DisplayStyle.Flex : DisplayStyle.None;
            _spThem.style.display = special && them != null && them.Level("special") > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (special)
            {
                float v = Mathf.Clamp01((float)mine.Power);
                // 2026-11-22: the Special Shot is its own button; aiming, the meter drains
                string hint = m.ZoneBy == me ? "AIM IT  ·  " + IntentSource.Keys("Hit") + " to fire" :
                              m.SpecialIncoming(me) ? (v >= (float)SquashSim.RunMods.FocusCost ? "FOCUS NOW!  " + IntentSource.FocusKey : "NO METER TO FOCUS") :
                              v >= 1f ? "READY!  " + IntentSource.FocusKey + " when the ball comes to you" :
                              v >= (float)SquashSim.RunMods.FocusCost ? "FOCUS  " + IntentSource.FocusKey : "FILL IT: GOOD SHOTS, WINNERS";
                _spMine.Set(v, "YOUR SPECIAL", hint);
                if (them != null) { float t = Mathf.Clamp01((float)them.Power); _spThem.Set(t, "THEIR SPECIAL", t >= 1f ? "READY - LOOK OUT" : ""); }
            }
            float dtF = Time.unscaledDeltaTime;
            bool dimOn = r.Focusing || m.ZoneBy >= 0;                                 // 2026-11-22: aiming a Special Shot - time stands still
            _dimLevel = Mathf.MoveTowards(_dimLevel, dimOn ? 1f : 0f, dtF * (dimOn ? 5f : 2.5f));
            _focusDim.style.opacity = _dimLevel;
            if (_flashLeft > 0) { _flashLeft -= dtF; _flash.style.opacity = Mathf.Clamp01(_flashLeft / 0.35f) * (Juice.ReduceMotion ? 0.12f : 0.3f); }
            _focusWord.text = m.ZoneBy >= 0 ? (m.ZoneBy == me ? "AIM" : "") : "FOCUS";
            bool meter = mine != null && mine.HasUltimate && !special && !r.ReplayActive;
            _power.style.display = meter ? DisplayStyle.Flex : DisplayStyle.None;
            if (meter)
            {
                float v = Mathf.Clamp01((float)mine.Power);
                _powerFill.style.width = Length.Percent(100f * v);
                bool ready = v >= 1f;
                _power.EnableInClassList("hud-power--ready", ready);
                _powerLabel.text = ready ? "ULTIMATE READY  ·  hold " + IntentSource.Keys("Hit") + " for a charged shot" : "POWER  " + Mathf.RoundToInt(100 * v) + "%" + (mine.FireShots > 0 ? "   ·   FIREBALLS " + mine.FireShots : "");
            }
            // what just happened: the newest notes since the last frame
            for (; _chaosRead < m.Chaos.Count; _chaosRead++)
            {
                var n = m.Chaos[_chaosRead];
                bool mineNote = n.Player == me;
                var pm = n.Player >= 0 && n.Player < 2 ? m.Players[n.Player].Player.Mods : null;
                switch (n.Kind)
                {
                    case "zone":
                        Pop(mineNote ? "SPECIAL SHOT  ·  AIM!" : "THEIR SPECIAL SHOT!", true);
                        if (mineNote) _flashLeft = 0.15f;
                        VenueView.Excite(0.8f);
                        break;
                    case "special":
                        Pop(mineNote ? "SPECIAL!" : "FOCUS TO SAVE IT!  " + IntentSource.FocusKey, !mineNote);
                        _flashLeft = 0.12f;
                        VenueView.Excite(1f);
                        break;
                    case "defend": Pop(mineNote ? "FOCUS!  GET IT BACK" : "THEY FOCUSED", false); break;
                    case "ult" when pm != null && pm.Level("special") > 0:
                        Pop(mineNote ? "SPECIAL SHOT!" : "THEIR SPECIAL!", true);
                        _flashLeft = 0.35f;
                        VenueView.Excite(1f);
                        break;
                    case "ult":
                        Pop(pm != null && pm.Level("freeze") + pm.Level("world") > 0 ? "TIME STOP!" : pm != null && pm.Level("inferno") > 0 ? "INFERNO!" : "JACKPOT!", true);
                        VenueView.Excite(0.9f);
                        break;
                    case "bonus":
                        Pop((mineNote ? "" : "THEY SCORE ") + (1 + n.Value) + " POINTS!", true);
                        if (n.Value >= 2) VenueView.Excite(1f);
                        break;
                    case "shield": Pop("SHIELD  ·  play it again", false); break;
                    case "rewind": Pop("REWIND!", true); break;
                    case "curve": Pop(mineNote ? "CURVEBALL" : "THEIR CURVEBALL", false); break;
                    case "ricochet": Pop("RICOCHET x" + n.Value, false); break;
                    case "deaddrop": Pop("DEAD DROP", false); break;
                    case "quake": Pop("QUAKE!", false); break;
                }
            }
            float dt = Time.unscaledDeltaTime;
            if (_popLeft > 0) { _popLeft -= dt; if (_popLeft <= 0) _pop.style.display = DisplayStyle.None; }
            if (_popSmallLeft > 0) { _popSmallLeft -= dt; if (_popSmallLeft <= 0) _popSmall.style.display = DisplayStyle.None; }
            _pop.EnableInClassList("hud-pop--in", _popLeft > 0.25f);
            bool frozen = false;
            for (int i = 0; i < 2; i++) { var pm = m.Players[i].Player.Mods; if (pm != null && pm.Frozen) frozen = true; }
            _freeze.style.display = frozen && !r.ReplayActive && m.Phase == MatchPhase.Rally ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The Focus dim: dark blue, clear in the middle, heavy at the edges.</summary>
        static Texture2D Vignette()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                    float a = Mathf.Lerp(0.18f, 0.85f, Mathf.SmoothStep(0.25f, 1f, d));
                    px[y * N + x] = new Color(0.02f, 0.05f, 0.16f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        void Pop(string text, bool big)
        {
            var l = big ? _pop : _popSmall;
            l.text = text; l.style.display = DisplayStyle.Flex;
            if (big) { _popLeft = 1.5f; _pop.RemoveFromClassList("hud-pop--in"); } else _popSmallLeft = 1.0f;
        }

        /// <summary>PR5: the game/match-ball chip, the rally counter, the walk-on card.</summary>
        void Broadcast(MatchRunner r, MatchSimulation m, MatchScore s, string[] names)
        {
            string ball = null;
            bool between = m.Phase == MatchPhase.AwaitToss || m.Phase == MatchPhase.Rally;
            if (between && !r.ReplayActive)
                for (int p = 0; p < 2; p++)
                    if (GameBall(s, p)) ball = (s.Games[p] + 1 >= s.GamesToWin ? "MATCH BALL" : "GAME BALL") + "  ·  " + names[p];
            Show(_ball, ball);
            // 2026-11-21: a match ball pulses (and the score bug with it)
            bool mb = ball != null && ball.StartsWith("MATCH") && Juice.On;
            bool beat = mb && Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.45f;
            _ball.EnableInClassList("hud-ball--pulse", beat);
            _bug.EnableInClassList("sb--matchball", mb);

            int shots = m.Referee.Shots;
            Show(_rally, m.Phase == MatchPhase.Rally && shots >= RallyCountFrom && !r.ReplayActive ? "RALLY  " + shots : null);
            _rally.EnableInClassList("hud-rally--hot", shots >= 15);

            // The walk-on: once per match, as it starts.
            if (_introFor != m) { _introFor = m; _introLeft = s.Rallies == 0 && m.History.Count == 0 ? 3.6f : 0f; }
            if (_introLeft > 0 && m.Phase == MatchPhase.AwaitToss && !r.ReplayActive) _introLeft -= Time.unscaledDeltaTime; else if (m.Phase != MatchPhase.AwaitToss && m.Phase != MatchPhase.WarmUp) _introLeft = 0;
            bool intro = _introLeft > 0 && !r.ReplayActive;
            // camera work: a cutscene's card - the prestige title over the fly-around, the names as they walk on
            bool cine = r.ReplayActive && r.CinematicKind != null;
            if (cine)
            {
                bool orbiting = r.ReplayProgress < r.CinematicOrbit && r.CinematicTitle != null;
                _cineTitle.text = orbiting ? r.CinematicTitle : names[0] + "   v   " + names[1];
                _cineSub.text = (r.CinematicSub ?? "") + "   ·   any key skips";
            }
            _cine.style.display = cine ? DisplayStyle.Flex : DisplayStyle.None;
            _cine.EnableInClassList("hud-cine--in", cine && r.ReplayProgress > 0.02f && r.ReplayProgress < 0.97f);
            if (intro)
            {
                _introNames.text = names[0] + "   v   " + names[1];
                string system = s.System == ScoringSystem.Par ? "TO " + PhysicsConfig.Current.Match.ParPoints : "TO " + s.GameTarget;
                _introSub.text = (s.GamesToWin > 1 ? "BEST OF " + (s.GamesToWin * 2 - 1) : "ONE GAME") + "   ·   " + system + "   ·   GLASS COURT";
            }
            _intro.style.display = intro ? DisplayStyle.Flex : DisplayStyle.None;
            _intro.EnableInClassList("hud-intro--in", intro && _introLeft < 3.4f && _introLeft > 0.3f);
        }

        /// <summary>P2-M8: online, top right: the ping and the input delay; a word when the other's input is late or the link is down.</summary>
        void NetChip(MatchRunner r)
        {
            var s = r.OnlineMatch;
            string t = null;
            if (s != null && !s.Finished)
            {
                bool down = Online.OnlineService.State != Online.NetState.Online;
                double ping = Online.OnlineService.Ping;
                t = down ? "RECONNECTING..." : s.Resuming ? "CATCHING UP..." : s.WaitingSeconds > 0.15 ? "WAITING FOR OPPONENT" :
                    (ping >= 0 ? UI.OnlineText.Ms(ping) : "--") + "  ·  DELAY " + s.Delay;
                _net.EnableInClassList("hud-net--warn", down || s.WaitingSeconds > 0.15);
            }
            Show(_net, t);
            // batch 4: what was said, for a few seconds
            bool chat = s != null && r.ChatText != null && Time.unscaledTime - r.ChatAt < 3f;
            Show(_chat, chat ? (r.ChatMine ? "YOU:  " : r.OpponentName + ":  ") + r.ChatText : null);
            if (chat) _chat.EnableInClassList("hud-chat--mine", r.ChatMine);
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
            _letKey.text = twoPlayer && _letFor == 1 ? IntentSource.BindingLabel("Appeal", true) : IntentSource.Keys("Appeal");
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
