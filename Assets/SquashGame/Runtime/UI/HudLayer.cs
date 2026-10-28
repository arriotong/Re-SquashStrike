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
            _pop.AddToClassList("hud-pop"); _pop.style.display = DisplayStyle.None; Add(_pop);
            _popSmall.AddToClassList("hud-pop-small"); _popSmall.style.display = DisplayStyle.None; Add(_popSmall);
            foreach (var l in new[] { _banner, _sub, _centre, _prompt, _promptKey, _meta, _letKey, _ball, _rally, _introNames, _introSub, _powerLabel, _pop, _popSmall, _freezeWord }) l.pickingMode = PickingMode.Ignore;
            foreach (var v in new[] { _power, _powerFill, track, _freeze }) v.pickingMode = PickingMode.Ignore;
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
            _system.text = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints : "ENGLISH " + s.GameTarget;
            _games.text = "GAMES " + s.Games[0] + "-" + s.Games[1];
            _games.style.display = s.GamesToWin > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            string server = !twoPlayer && s.Server == r.HumanIndex ? "You serve" : names[s.Server] + " serves";
            _meta.text = server + " from the " + (s.ServeSide > 0 ? "right" : "left") + " box" +
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
            // batch 4: the knock-up - whose half, how long, how to skip it
            if (m.Phase == MatchPhase.WarmUp && !r.ReplayActive)
            {
                bool mine = m.WarmUpOwner == r.HumanIndex;
                string whose = twoPlayer ? names[m.WarmUpOwner] + "'s" : mine ? "Your" : "Their";
                key = "Enter / Y";
                prompt = "WARM-UP   ·   " + whose + " side   ·   " + Mathf.CeilToInt((float)m.WarmUpRemaining) + " s   ·   skip" +
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
            bool meter = mine != null && mine.HasUltimate && !r.ReplayActive;
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
                string system = s.System == ScoringSystem.Par ? "PAR " + PhysicsConfig.Current.Match.ParPoints : "ENGLISH " + s.GameTarget;
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
