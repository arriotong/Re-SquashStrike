// ===========================================================
// Assets/SquashGame/Runtime/UI/ReplayScreens.cs
// P2-M6 Replay Theatre: the screens.
//
//   ReplayLibraryScreen  tabs RECENT / PINNED / CAREER: a card per
//                        match (result, opponent, scores, highlights),
//                        a window of five that slides with the focus;
//                        X pins, Y deletes
//   MatchReplayScreen    the match: its summary, the focused rally's
//                        card (score before -> after, the call, shots,
//                        length, pace, tags), the rally strip (a node a
//                        rally, coloured by who won it, games apart);
//                        the court shows the focused rally; PLAY REEL
//   TheatrePlayerScreen  watching: the court full screen, the score bug
//                        as it was, the call as a lower third, a slim
//                        transport bar (play / pause, scrub with shot
//                        ticks, speed, camera); H hides it all
// ===========================================================

using System.Collections.Generic;
using SquashGame.Replays;
using SquashSim;
using SquashSim.Replays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class ReplayText
    {
        public static string Call(RallyRec r)
        {
            switch ((RallyEnd)r.Kind)
            {
                case RallyEnd.Down: return "DOWN"; case RallyEnd.Out: return "OUT"; case RallyEnd.NotUp: return "NOT UP";
                case RallyEnd.Fault: return "FAULT"; case RallyEnd.FootFault: return "FOOT FAULT"; case RallyEnd.HitSelf: return "HIT YOURSELF";
                case RallyEnd.Let: return "LET"; case RallyEnd.NoLet: return "NO LET"; case RallyEnd.Stroke: return "STROKE";
            }
            return "";
        }

        public static string Mode(ReplayEntry e) { return e.Mode == "career" ? "CAREER" : e.Mode == "versus" ? "LOCAL VERSUS" : e.Mode == "ranked" ? "RANKED" : e.Mode == "online" ? "ONLINE" : "QUICK MATCH"; }
        public static string When(ReplayEntry e) { return e.When.ToString("d MMM  HH:mm").ToUpperInvariant(); }
        public static string Title(ReplayEntry e) { return e.Mode == "versus" ? e.P1 + " v " + e.P2 : "v " + e.Opponent; }
        public static string Seconds(float s) { int t = Mathf.Max(0, Mathf.RoundToInt(s)); return (t / 60) + ":" + (t % 60).ToString("00"); }

        public static VisualElement Tags(VisualElement parent, RallyTag tags)
        {
            var row = Kit.Box(parent, "k-rp-tags");
            foreach (var t in Highlights.All) if ((tags & t) != 0) Kit.L(row, Highlights.Name(t).ToUpperInvariant(), "k-rp-tag");
            return row;
        }
    }

    // ---------------- the library ----------------

    public sealed class ReplayLibraryScreen : GameScreen
    {
        protected override string Kicker { get { return "REPLAY THEATRE"; } }
        protected override string Title { get { return "Library"; } }
        protected override bool ShowChip { get { return false; } }
        public const int Window = 5;
        readonly List<CardItem>[] _cards = { new List<CardItem>(), new List<CardItem>(), new List<CardItem>() };
        readonly Dictionary<CardItem, ReplayEntry> _entry = new Dictionary<CardItem, ReplayEntry>();
        readonly int[] _start = new int[3];
        bool _dirty;

        protected override void Compose()
        {
            string[] tabs = { "RECENT", "PINNED", "CAREER" };
            for (int t = 0; t < 3; t++)
            {
                var page = Page(tabs[t]);
                var row = Kit.Box(page, "k-row"); row.AddToClassList("k-rl-row");
                var list = ReplayLibrary.Entries.FindAll(e => t == 0 || (t == 1 ? e.Pinned : e.Mode == "career"));
                if (list.Count == 0)
                    Kit.L(page, t == 0 ? "No replays yet. Every match you play is kept here - the last 20, and the ones you pin."
                               : t == 1 ? "Nothing pinned. X on a replay keeps it for good." : "No career matches yet.", "k-cr-empty");
                foreach (var e in list)
                {
                    var ee = e;
                    var card = AddTo(row, new CardItem(ReplayText.Title(e), (e.Pinned ? "★ " : "") + ReplayText.Mode(e) + "  ·  " + ReplayText.When(e),
                            () => Fe.Push(new MatchReplayScreen(ee)))
                        .Drawn(e.Winner == 0 ? "trophy" : "stripes", e.Winner == 0 ? new Color32(26, 30, 14, 255) : new Color32(24, 24, 28, 255),
                               e.Winner == 0 ? new Color32(200, 255, 61, 255) : new Color(1, 1, 1, 0.05f))
                        .Line(() => (string.IsNullOrEmpty(ee.Event) ? "" : ee.Event + "  ·  " + ee.Round + "\n") + ee.Scores + "\n" + ee.Rallies + " rallies  ·  " + ee.Tagged + " highlights" + (ee.SameVersion ? "" : "\nmade with an older version"))
                        .Badge(() => ee.Result, ee.Winner == 0 && !ee.Retired));
                    card.Root.AddToClassList("k-card--replay");
                    _cards[t].Add(card);
                    _entry[card] = e;
                }
                Slide(t);
            }
            ReplayLibrary.Changed += OnChanged;
        }

        void OnChanged() { _dirty = true; }

        /// <summary>Show the window of five around the start.</summary>
        void Slide(int t)
        {
            var l = _cards[t];
            _start[t] = Mathf.Clamp(_start[t], 0, Mathf.Max(0, l.Count - Window));
            for (int i = 0; i < l.Count; i++) l[i].Root.style.display = i >= _start[t] && i < _start[t] + Window ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void Tick(float dt)
        {
            if (_dirty) { _dirty = false; ReplayLibrary.Changed -= OnChanged; Fe.Replace(new ReplayLibraryScreen()); return; }
            // the window follows the focus: at its edge, slide one more into view
            int t = PageIndex;
            if (!(Focused is CardItem c)) return;
            int i = _cards[t].IndexOf(c);
            if (i < 0) return;
            if (i == _start[t] + Window - 1 && i < _cards[t].Count - 1) { _start[t]++; Slide(t); }
            else if (i == _start[t] && i > 0) { _start[t]--; Slide(t); }
        }

        ReplayEntry FocusedEntry { get { return Focused is CardItem c && _entry.TryGetValue(c, out var e) ? e : null; } }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Watch"), (PromptBar.Btn.X, "Pin"), (PromptBar.Btn.Y, "Delete"), (PromptBar.Btn.Tabs, "Tabs"), (PromptBar.Btn.B, "Back")); }
        public override void OnActionX() { var e = FocusedEntry; if (e != null) ReplayLibrary.Pin(e, !e.Pinned); }
        public override void OnActionY()
        {
            var e = FocusedEntry;
            if (e != null) Fe.Push(new ConfirmScreen("Delete this replay?", () => ReplayLibrary.Delete(e)));
        }
        public override void OnHide() { ReplayLibrary.Changed -= OnChanged; }
        public override void OnShow() { base.OnShow(); ReplayLibrary.Changed -= OnChanged; ReplayLibrary.Changed += OnChanged; MenuCinematics.Scene = 2; }
    }

    // ---------------- a match ----------------

    /// <summary>A rally on the strip: a dot (who won it), a mark when it has a highlight; dim until the pass reaches it.</summary>
    public sealed class RallyNode : MenuItem
    {
        public readonly int Index;
        readonly System.Func<bool> _ready;
        readonly System.Action<int> _play;
        public override bool WantsSlant { get { return false; } }
        public RallyNode(int index, RallyRec r, RallyTag tags, System.Func<bool> ready, System.Action<int> play) : base("r" + index)
        {
            Index = index; _ready = ready; _play = play;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-rnode");
            LabelEl.style.display = DisplayStyle.None;
            var dot = Kit.Box(Root, "k-rnode-dot");
            dot.AddToClassList(r.Winner == 0 ? "k-rnode-dot--p0" : r.Winner == 1 ? "k-rnode-dot--p1" : "k-rnode-dot--let");
            if (tags != RallyTag.None) Kit.Box(Root, "k-rnode-tag");
        }
        public override void Submit() { if (_ready()) _play(Index); }
        public override void Refresh() { base.Refresh(); Root.EnableInClassList("k-rnode--wait", !_ready()); }
    }

    public sealed class MatchReplayScreen : GameScreen
    {
        readonly ReplayEntry _e;
        readonly bool _autoReel;
        ReplayFile _file;
        TheatreSession _t;
        VisualElement _card, _strip;
        ScrollView _scroll;
        Label _progress;
        readonly List<RallyNode> _nodes = new List<RallyNode>();
        MenuItem _shown;
        bool _reelStarted;

        public MatchReplayScreen(ReplayEntry e, bool autoReel = false) { _e = e; _autoReel = autoReel; }
        public TheatreSession Session { get { return _t; } }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return ReplayText.Mode(_e) + "  ·  " + ReplayText.When(_e); } }
        protected override string Title { get { return ReplayText.Title(_e); } }

        protected override void Compose()
        {
            try { _file = ReplayLibrary.Load(_e); }
            catch (System.Exception ex) { Kit.L(Content, "This replay could not be read (" + ex.Message + ").", "k-cr-empty"); return; }
            _t = TheatreSession.Open(_e, _file, Fe.Match);

            var head = Kit.Box(Content, "k-rp-head");
            var res = Kit.L(head, _e.Result, "k-rp-result");
            if (_e.Winner == 0 && !_e.Retired) res.AddToClassList("k-rp-result--won");
            var chips = Kit.Box(head, "k-ft-chips");
            foreach (var g in (_e.Scores ?? "").Split(' ')) if (g.Length > 0 && g != "ret.") Kit.L(chips, g, "k-ft-chip");
            Kit.L(head, _e.Rallies + " rallies  ·  " + _e.Tagged + " highlights" + (string.IsNullOrEmpty(_e.Event) ? "" : "  ·  " + _e.Event + ", " + _e.Round), "k-rp-meta");
            if (!_e.SameVersion) Kit.L(head, "MADE WITH AN OLDER VERSION", "k-rp-old");
            _progress = Kit.L(head, "", "k-rp-progress");

            _card = Kit.Panel(Content, "THE RALLY"); _card.AddToClassList("k-rp-card");

            var strip = Kit.Panel(Content, "THE MATCH, RALLY BY RALLY"); strip.AddToClassList("k-rp-strip");
            _scroll = new ScrollView(ScrollViewMode.Horizontal); _scroll.AddToClassList("k-rp-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.Add(_scroll);
            _strip = Kit.Box(null, "k-rp-nodes"); _scroll.Add(_strip);
            var clips = _t.Builder.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                int k = i;
                if (i > 0 && (clips[i - 1].Tags & (RallyTag.GameBall | RallyTag.MatchBall)) != 0) Kit.Box(_strip, "k-rnode-gap");
                var n = AddTo(_strip, new RallyNode(i, clips[i].Rec, clips[i].Tags, () => clips[k].Ready, Watch));
                _nodes.Add(n);
            }
            AddTo(Root, new PrimaryItem("Play reel", () => { _t.PlayReel(); Fe.Push(new TheatrePlayerScreen(_t)); }));
        }

        void Watch(int rally) { if (_t.Play(rally)) Fe.Push(new TheatrePlayerScreen(_t)); }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Rallies"), (PromptBar.Btn.A, "Watch"), (PromptBar.Btn.X, _e.Pinned ? "Unpin" : "Pin"), (PromptBar.Btn.B, "Back")); }
        public override void OnActionX() { ReplayLibrary.Pin(_e, !_e.Pinned); SetPrompts(); }

        public override void OnShow()
        {
            base.OnShow();
            if (_nodes.Count > 0 && !(Focused is RallyNode)) { var pick = _nodes[0]; foreach (var n in _nodes) if (_t.Builder.Clips[n.Index].Tags != RallyTag.None) { pick = n; break; } Focus(pick); }
            _shown = null;
        }

        public override void Tick(float dt)
        {
            if (_t == null) return;
            _t.Tick(dt);
            _progress.text = _t.Builder.Done ? (_t.Builder.Diverged ? "DIVERGED AT RALLY " + (_t.Builder.FirstMismatch + 1) : "") : "PREPARING  " + Mathf.RoundToInt(_t.Builder.Progress * 100) + "%";
            foreach (var n in _nodes) n.Refresh();
            if (_autoReel && !_reelStarted)
            {
                var reel = Highlights.Reel(_file);
                if (reel.Count > 0 && _t.Builder.Clips[reel[0]].Ready) { _reelStarted = true; _t.PlayReel(reel); Fe.Push(new TheatrePlayerScreen(_t)); return; }
            }
            if (Focused is RallyNode node && node != _shown)
            {
                _shown = node;
                _scroll.ScrollTo(node.Root);
                ShowCard(node.Index);
            }
            else if (Focused is RallyNode n2 && !_previewed && _t.Builder.Clips[n2.Index].Ready) ShowCard(n2.Index);
        }

        bool _previewed;
        void ShowCard(int i)
        {
            var c = _t.Builder.Clips[i];
            var r = c.Rec;
            _card.Clear();
            Kit.L(_card, "RALLY " + (i + 1) + " OF " + _t.Builder.Clips.Length + "  ·  GAME " + (c.GamesBefore0 + c.GamesBefore1 + 1), "k-panel-title");
            var sc = Kit.Box(_card, "k-rp-score");
            Kit.L(sc, c.Before0 + "-" + c.Before1, "k-rp-before");
            Kit.L(sc, "→", "k-rp-arrow");
            bool ended = (c.Tags & (RallyTag.GameBall | RallyTag.MatchBall)) != 0;
            Kit.L(sc, ended ? "GAME " + r.Points0 + "-" + r.Points1 : r.Points0 + "-" + r.Points1, "k-rp-after");
            string who = r.Winner == 0 ? _file.Get("p1", "YOU") : r.Winner == 1 ? _file.Get("p2", "OPPONENT") : "";
            Kit.L(_card, ReplayText.Call(r) + (r.Winner >= 0 ? "   ·   point " + who : ""), "k-rp-call");
            var facts = Kit.Box(_card, "k-rp-facts");
            Kit.L(facts, r.Shots + " shots", "k-rp-fact");
            Kit.L(facts, c.Ready ? ReplayText.Seconds((c.EndFrame) / SimRunnerBase.FrameRate) + " long" : "preparing", "k-rp-fact");
            if (r.TopPace > 0) Kit.L(facts, "fastest " + (r.TopPace * 3.6).ToString("0") + " km/h", "k-rp-fact");
            ReplayText.Tags(_card, c.Tags);
            // the court shows it, paused near the toss
            _previewed = c.Ready;
            if (c.Ready && _t.Play(i)) { _t.Paused = true; _t.Cursor = Mathf.Min(c.Frames.Count - 1, (int)(1.5f * SimRunnerBase.FrameRate)); }
        }

        public override void Back() { if (_t != null) _t.Close(); Fe.Pop(); }
    }

    // ---------------- watching ----------------

    public sealed class TheatrePlayerScreen : MenuScreen
    {
        readonly TheatreSession _t;
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "k-theatre"; } }
        protected override string Hint { get { return null; } }
        protected override string Kicker { get { return null; } }
        protected override string Title { get { return ""; } }
        Label _p0, _p1, _g0, _g1, _s0, _s1, _rally, _time, _speed, _cam, _play, _call, _callSub;
        VisualElement _bug, _bar, _fill, _ticks, _third, _tags, _serve0, _serve1;
        int _forRally = -2;
        bool _hidden, _called;
        public bool Hidden { get { return _hidden; } }

        public TheatrePlayerScreen(TheatreSession t) { _t = t; }

        protected override void Build()
        {
            var f = _t.File;
            // the score bug, as it was when the rally was served
            _bug = Kit.Box(Root, "k-th-bug");
            var r0 = Kit.Box(_bug, "k-th-row"); _serve0 = Kit.Box(r0, "k-th-serve"); _p0 = Kit.L(r0, f.Get("p1", "YOU"), "k-th-name"); _g0 = Kit.L(r0, "", "k-th-games"); _s0 = Kit.L(r0, "", "k-th-points");
            var r1 = Kit.Box(_bug, "k-th-row"); _serve1 = Kit.Box(r1, "k-th-serve"); _p1 = Kit.L(r1, f.Get("p2", "OPPONENT"), "k-th-name"); _g1 = Kit.L(r1, "", "k-th-games"); _s1 = Kit.L(r1, "", "k-th-points");
            var top = Kit.Box(Root, "k-th-top");
            _rally = Kit.L(top, "", "k-th-rally");
            _tags = Kit.Box(top, "k-th-tagbox");
            // the call, as a lower third
            _third = Kit.Box(Root, "hud-third"); _third.AddToClassList("k-th-third");
            _call = Kit.L(_third, "", "hud-third-call"); _callSub = Kit.L(_third, "", "hud-third-sub");
            // the transport
            _bar = Kit.Box(Root, "k-th-bar");
            _play = Kit.L(_bar, "", "k-th-play");
            var track = Kit.Box(_bar, "k-th-track");
            _fill = Kit.Box(track, "k-th-fill");
            _ticks = Kit.Box(track, "k-th-ticks");
            track.pickingMode = PickingMode.Position;
            track.RegisterCallback<PointerDownEvent>(e => Scrub(track, e.localPosition.x));
            track.RegisterCallback<PointerMoveEvent>(e => { if ((e.pressedButtons & 1) != 0) Scrub(track, e.localPosition.x); });
            _time = Kit.L(_bar, "", "k-th-time");
            _speed = Kit.L(_bar, "", "k-th-chip");
            _cam = Kit.L(_bar, "", "k-th-chip");
            var prompts = new PromptBar(); prompts.AddToClassList("k-th-prompts");
            prompts.Set((PromptBar.Btn.A, "Play / pause"), (PromptBar.Btn.Move, "Scrub"), (PromptBar.Btn.X, "Speed"), (PromptBar.Btn.Y, "Camera"), (PromptBar.Btn.Tabs, "Rallies"), (PromptBar.Btn.B, "Back"));
            Root.Add(prompts);
        }

        void Scrub(VisualElement track, float x) { _t.Paused = true; _t.Position = Mathf.Clamp01(x / Mathf.Max(1f, track.resolvedStyle.width)); }

        void NewRally()
        {
            var c = _t.Clip;
            _forRally = _t.Rally;
            _called = false;
            if (c == null) return;
            _g0.text = c.GamesBefore0.ToString(); _g1.text = c.GamesBefore1.ToString();
            _s0.text = c.Before0.ToString(); _s1.text = c.Before1.ToString();
            _serve0.EnableInClassList("k-th-serve--on", c.Server == 0); _serve1.EnableInClassList("k-th-serve--on", c.Server == 1);
            _rally.text = (_t.Reel != null ? "HIGHLIGHTS  ·  " : "") + "RALLY " + (c.Index + 1) + " / " + _t.Builder.Clips.Length;
            _tags.Clear(); ReplayText.Tags(_tags, c.Tags);
            _ticks.Clear();
            for (int i = 0; i < c.Events.Count; i++)
                foreach (var e in c.Events[i])
                    if (e.Type == SimEventType.RacketContact)
                    {
                        var tk = Kit.Box(_ticks, "k-th-tick");
                        tk.style.left = Length.Percent(100f * i / Mathf.Max(1, c.Frames.Count - 1));
                        if (e.PlayerIndex == 1) tk.AddToClassList("k-th-tick--p1");
                        break;
                    }
            _call.text = ReplayText.Call(c.Rec);
            string who = c.Rec.Winner == 0 ? _t.File.Get("p1", "YOU") : c.Rec.Winner == 1 ? _t.File.Get("p2", "OPPONENT") : "";
            _callSub.text = c.Rec.Winner >= 0 ? "POINT  " + who : "";
        }

        public override void Tick(float dt)
        {
            _t.Tick(dt);
            if (_t.Rally != _forRally) NewRally();
            var c = _t.Clip;
            ReadControls(dt);
            if (c == null) return;
            bool past = _t.PastTheCall;
            _third.style.display = past ? DisplayStyle.Flex : DisplayStyle.None;
            _third.EnableInClassList("hud-third--in", past);
            if (past && !_called) { _called = true; if (!_t.Paused) CallSound(); }
            if (past)
            {
                // the score after the call
                _s0.text = (c.Tags & (RallyTag.GameBall | RallyTag.MatchBall)) != 0 ? c.Rec.Points0.ToString() : c.Rec.Points0.ToString();
                _s1.text = c.Rec.Points1.ToString();
                _g0.text = c.Rec.Games0.ToString(); _g1.text = c.Rec.Games1.ToString();
            }
            else { _s0.text = c.Before0.ToString(); _s1.text = c.Before1.ToString(); _g0.text = c.GamesBefore0.ToString(); _g1.text = c.GamesBefore1.ToString(); }
            _fill.style.width = Length.Percent(100f * _t.Position);
            _time.text = ReplayText.Seconds((float)(_t.Cursor / SimRunnerBase.FrameRate)) + " / " + ReplayText.Seconds(c.Seconds);
            _speed.text = _t.Speed == 1 ? "1x" : _t.Speed == 0.5 ? "0.5x" : "0.25x";
            _cam.text = _t.Camera.ToString().ToUpperInvariant().Replace("BACKWALL", "BACK WALL");
            _play.text = _t.Paused ? "▶" : "❚❚";
        }

        void CallSound()
        {
            var audio = Object.FindFirstObjectByType<Audio.AudioDirector>();
            if (audio != null && Camera.main != null) audio.Play("call", Camera.main.transform.position, 0.8f);
        }

        void ReadControls(float dt)
        {
            var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            bool toggle = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (toggle)
            {
                if (_t.Position >= 0.999f) { _t.Position = 0; _t.Paused = false; }
                else _t.Paused = !_t.Paused;
            }
            // scrub: arrows / d-pad / triggers, two seconds of play a second
            float s = 0;
            if (kb != null) { if (kb.leftArrowKey.isPressed) s -= 1; if (kb.rightArrowKey.isPressed) s += 1; }
            if (pad != null) { s += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue(); if (pad.dpad.left.isPressed) s -= 1; if (pad.dpad.right.isPressed) s += 1; }
            if (Mathf.Abs(s) > 0.05f) { _t.Paused = true; _t.Cursor = _t.Cursor + s * 2f * SimRunnerBase.FrameRate * dt; }
            // the free camera: right mouse drag / right stick to orbit, wheel / shoulders to zoom
            if (_t.Camera == TheatreCam.Free)
            {
                if (mouse != null && mouse.rightButton.isPressed) { var d = mouse.delta.ReadValue(); _t.FreeYaw += d.x * 0.25f; _t.FreePitch -= d.y * 0.25f; }
                if (mouse != null) _t.FreeDistance -= mouse.scroll.ReadValue().y * 0.004f;
                if (pad != null) { var st = pad.rightStick.ReadValue(); _t.FreeYaw += st.x * 90f * dt; _t.FreePitch += st.y * 60f * dt; }
            }
            // hide everything (a clean picture)
            if ((kb != null && kb.hKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
            {
                _hidden = !_hidden;
                Root.style.opacity = _hidden ? 0f : 1f;
            }
        }

        public override void OnActionX() { _t.NextSpeed(); }
        public override void OnActionY() { _t.NextCamera(); }
        public override void OnTab(int d) { _t.Step(d); }
        public override void Back() { _t.Paused = true; Fe.Pop(); }
    }
}
