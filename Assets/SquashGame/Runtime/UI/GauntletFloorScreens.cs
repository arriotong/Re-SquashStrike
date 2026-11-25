// ===========================================================
// Assets/SquashGame/Runtime/UI/GauntletFloorScreens.cs
// 2026-11-21 the Gauntlet's arcade floors (plan: SQUASH FRONTEND
// MODIFIED/RE_SQUASHSTRIKE_GAUNTLET_ARCADE_PLAN.md):
//   FloorBar          the HUD over a floor: the target, the score (a bar),
//                     the rallies left, the multiplier and the wheel's
//                     charge; each rally's pop ("+240 WINNER x2.5")
//   WheelScreen       the wheel, over the paused match: it spins onto the
//                     prize the floor's play already drew (GauntletFloor.
//                     Spin); GOLDEN turns it into the Golden Wheel and it
//                     spins again; then on with the floor
//   FloorResultScreen the floor over: CLEARED or FAILED, the score against
//                     the target, the best rally, what's left over; on to
//                     the pick (or the run's end)
// ===========================================================

using System.Collections.Generic;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    // ---------------- the HUD's bar ----------------

    public sealed class FloorBar : VisualElement
    {
        readonly Label _floor = new Label(), _score = new Label(), _target = new Label(), _left = new Label(), _mult = new Label(), _pop = new Label(), _power = new Label();
        readonly VisualElement _fill = new VisualElement(), _bar = new VisualElement();
        int _popsSeen;

        public FloorBar()
        {
            AddToClassList("fb");
            pickingMode = PickingMode.Ignore;
            var top = Kit.Box(this, "fb-top");
            _floor.AddToClassList("fb-floor"); top.Add(_floor);
            _left.AddToClassList("fb-left"); top.Add(_left);
            var mid = Kit.Box(this, "fb-mid");
            _score.AddToClassList("fb-score"); mid.Add(_score);
            _target.AddToClassList("fb-target"); mid.Add(_target);
            _bar.AddToClassList("fb-bar"); Add(_bar);
            _fill.AddToClassList("fb-fill"); _bar.Add(_fill);
            var mrow = Kit.Box(this, "fb-mrow");
            _mult.AddToClassList("fb-mult"); mrow.Add(_mult);
            _power.AddToClassList("fb-power"); mrow.Add(_power);
            _pop.AddToClassList("fb-pop"); Add(_pop);
            foreach (var e in Children()) e.pickingMode = PickingMode.Ignore;
        }

        /// <summary>Show this floor's state (each frame).</summary>
        public void Set(MatchRunner m)
        {
            var f = m.Floor;
            style.display = f != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (f == null) return;
            _floor.text = "FLOOR " + f.Floor + (SquashSim.Modes.Gauntlet.KindOf(f.Floor) == FloorKind.Boss ? "  ·  BOSS" : "");
            _left.text = f.Left + (f.Left == 1 ? " RALLY LEFT" : " RALLIES LEFT");
            _left.EnableInClassList("fb-left--last", f.Left <= 2 && !f.Cleared);
            _score.text = f.Score.ToString("N0");
            _target.text = "/ " + f.Target.ToString("N0");
            _fill.style.width = Length.Percent(Mathf.Clamp01(f.Target > 0 ? (float)f.Score / f.Target : 1f) * 100f);
            _fill.EnableInClassList("fb-fill--done", f.Cleared);
            _mult.text = "x" + f.Mult.ToString("0.0");
            _mult.EnableInClassList("fb-mult--max", f.Mult >= f.MaxMult - 1e-6);
            _power.text = f.NoWheel ? "NO WHEEL" : f.Power > 0 ? "WHEEL " + new string('●', f.Power) + new string('○', GauntletFloor.MaxPower - f.Power) : (f.DoubleNext > 0 || f.TripleNext > 0 ? (f.TripleNext > 0 ? "x3 NEXT " + f.TripleNext : "x2 NEXT") : "");
            if (m.FloorPops != _popsSeen)
            {
                _popsSeen = m.FloorPops;
                var p = m.LastFloorPop;
                _pop.text = p.Points > 0 ? "+" + p.Points.ToString("N0") + "  " + p.What + (p.Mult > 1.001 ? "  x" + p.Mult.ToString("0.0") : "") : p.What;
                _pop.EnableInClassList("fb-pop--lost", p.Points <= 0);
            }
            float age = Time.unscaledTime - m.LastFloorPopAt;
            _pop.style.opacity = Mathf.Clamp01(1.6f - age);
        }
    }

    // ---------------- the wheel ----------------

    /// <summary>A round wheel of slices (painted), turned by its rotation; the pointer is fixed at the top.</summary>
    public sealed class WheelView : VisualElement
    {
        WheelPrize[] _slices;
        bool _gold;
        readonly VisualElement _disc = new VisualElement();
        readonly List<Label> _labels = new List<Label>();
        static readonly Color[] Plain = { new Color32(255, 90, 74, 255), new Color32(60, 170, 255, 255), new Color32(200, 255, 61, 255), new Color32(190, 110, 255, 255) };
        static readonly Color[] Gold = { new Color32(255, 196, 64, 255), new Color32(255, 150, 40, 255) };

        public WheelView()
        {
            AddToClassList("wh");
            _disc.AddToClassList("wh-disc");
            _disc.generateVisualContent += Paint;
            Add(_disc);
            var pointer = Kit.L(this, "▼", "wh-pointer");
            pointer.pickingMode = PickingMode.Ignore;
        }

        public void Show(WheelPrize[] slices, bool gold)
        {
            _slices = slices; _gold = gold;
            foreach (var l in _labels) l.RemoveFromHierarchy();
            _labels.Clear();
            for (int i = 0; i < slices.Length; i++)
            {
                float a = (i + 0.5f) / slices.Length * Mathf.PI * 2f;          // clockwise from the top
                var l = Kit.L(_disc, GauntletFloor.SliceName(slices[i]), "wh-label");
                l.style.left = Length.Percent(50f + Mathf.Sin(a) * 31f);
                l.style.top = Length.Percent(50f - Mathf.Cos(a) * 31f);
                l.style.rotate = new Rotate(new Angle(a * Mathf.Rad2Deg, AngleUnit.Degree));
                _labels.Add(l);
            }
            _disc.MarkDirtyRepaint();
        }

        public void Turn(float degrees) { _disc.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree)); }

        void Paint(MeshGenerationContext mgc)
        {
            if (_slices == null) return;
            var r = _disc.contentRect;
            var c = new Vector2(r.width / 2, r.height / 2);
            float rad = Mathf.Min(r.width, r.height) / 2 - 2;
            var p = mgc.painter2D;
            int n = _slices.Length;
            for (int i = 0; i < n; i++)
            {
                float a0 = -90f + 360f * i / n, a1 = -90f + 360f * (i + 1) / n;
                p.fillColor = _slices[i] == WheelPrize.Golden ? new Color32(255, 214, 80, 255) : (_gold ? Gold[i % Gold.Length] : Plain[i % Plain.Length]);
                p.BeginPath(); p.MoveTo(c); p.Arc(c, rad, a0, a1); p.ClosePath(); p.Fill();
                p.strokeColor = new Color(0.05f, 0.05f, 0.07f, 1f); p.lineWidth = 4f;
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * rad); p.Stroke();
            }
            p.strokeColor = new Color(0.05f, 0.05f, 0.07f, 1f); p.lineWidth = 8f;
            p.BeginPath(); p.Arc(c, rad, 0f, 360f); p.Stroke();
            p.fillColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            p.BeginPath(); p.Arc(c, rad * 0.14f, 0f, 360f); p.Fill();
        }
    }

    /// <summary>The wheel spins over the paused floor, onto the prize already drawn; the Golden Wheel spins after it.</summary>
    public sealed class WheelScreen : MenuScreen
    {
        protected override bool Sidebar { get { return false; } }
        protected override string RootClass { get { return "wh-screen"; } }
        protected override string Title { get { return ""; } }
        protected override string Hint { get { return null; } }
        public override bool Overlay { get { return true; } }

        List<WheelSpin> _spins;
        int _at;
        float _t;
        WheelView _wheel;
        Label _head, _result;
        float _from, _to, _tickAt;
        bool _landed;
        /// <summary>The prizes (tests).</summary>
        public List<WheelSpin> Spins { get { return _spins; } }
        public bool Finished { get; private set; }
        const float SpinSeconds = 2.4f, HoldSeconds = 1.3f;

        protected override void Build()
        {
            var m = Fe.Match;
            m.Clock.Pause();
            _spins = GauntletFloor.Spin(m.Floor);
            _head = Kit.L(Content, "THE WHEEL!", "wh-head");
            _wheel = new WheelView(); Content.Add(_wheel);
            _result = Kit.L(Content, "", "wh-result");
            if (_spins.Count == 0) { Finished = true; return; }
            Begin(0);
            Sounds.Play("stamp");
        }

        void Begin(int i)
        {
            _at = i; _t = 0; _landed = false;
            var s = _spins[i];
            _wheel.Show(s.Slices, i > 0);
            _head.text = i > 0 ? "THE GOLDEN WHEEL!" : "THE WHEEL!";
            _head.EnableInClassList("wh-head--gold", i > 0);
            _result.text = "";
            float slice = 360f / s.Slices.Length;
            _from = 0f; _tickAt = 0f;
            _to = 360f * 5f - (s.Slice + 0.5f) * slice;                          // the slice's middle under the pointer
            _wheel.Turn(0f);
        }

        public override void Tick(float dt)
        {
            if (Finished) { Close(); return; }
            float d = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _t += d;
            if (!_landed)
            {
                float k = Mathf.Clamp01(_t / SpinSeconds), e = 1f - Mathf.Pow(1f - k, 3f);
                float ang = Mathf.Lerp(_from, _to, e);
                _wheel.Turn(ang);
                if (Mathf.Abs(ang - _tickAt) >= 30f) { _tickAt = ang; SquashGame.Audio.GameSfx.Play("wheel_tick", 0.5f, 0.04f); }   // 2026-11-25 a click a segment
                if (k >= 1f) { _landed = true; _t = 0; _result.text = _spins[_at].Words; Sounds.Play(_spins[_at].Golden ? "star" : "tick"); }
                return;
            }
            if (_t < HoldSeconds) return;
            if (_at + 1 < _spins.Count) { Begin(_at + 1); return; }
            Finished = true;
            Close();
        }

        bool _closed;
        void Close()
        {
            if (_closed) return;
            _closed = true;
            var m = Fe.Match;
            Fe.Pop();
            m.WheelSpun();
        }

        public override void Back() { }                                         // it spins to its end
    }

    // ---------------- the floor's result ----------------

    public sealed class FloorResultScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  FLOOR " + P.Floor; } }
        protected override string Title { get { return P.Cleared ? "Floor cleared!" : "Out of rallies"; } }
        protected override bool ShowChip { get { return false; } }
        public override bool Dims { get { return true; } }
        FloorPlay P { get { return Fe.Match.Floor; } }
        public FloorPlay Play { get { return P; } }

        protected override void Compose()
        {
            var p = P;
            var box = Kit.Box(Content, "fr-box");
            var st = new Stamp(p.Cleared ? "✓  CLEARED" : "✗  FAILED", 0.15f);
            st.AddToClassList(p.Cleared ? "k-stamp--good" : "k-stamp--bad");
            box.Add(st);
            var big = Kit.Box(box, "fr-big");
            Kit.L(big, p.Score.ToString("N0"), "fr-score");
            Kit.L(big, "/ " + p.Target.ToString("N0"), "fr-target");
            var rows = Kit.Box(box, "fr-rows");
            Row(rows, "Rallies played", p.Played + " of " + p.Rallies);
            Row(rows, "Best rally", p.BestRally.ToString("N0") + " points");
            if (p.Cleared && p.Left > 0) Row(rows, "Rallies left over", p.Left + "  ·  bonus points and coins");
            if (p.Spins > 0) Row(rows, "Wheel spins", p.Spins.ToString());
            if (p.Coins > 0) Row(rows, "Coins this floor", "+" + p.Coins);
            var run = GauntletFlow.Run;
            bool retry = !p.Cleared && run != null && (run.Golden || (run.CanRevive && !run.Revived));
            Kit.L(box, p.Cleared ? (p.Floor == SquashSim.Modes.Gauntlet.WinFloor && run != null && !run.Endless ? "That's floor " + p.Floor + " - you've won the Gauntlet!" : "On to the card pick.")
                               : retry ? (run.Golden ? "Your Golden Racket: play this floor again." : "Second Chance: play this floor again.") : "The run is over.", "fr-note");
            AddTo(Root, new PrimaryItem(p.Cleared ? "Continue" : retry ? "Try again" : "Run over", () => GauntletFlow.AfterFloor(Fe, P)));
        }

        static void Row(VisualElement parent, string k, string v)
        {
            var r = Kit.Box(parent, "fr-row");
            Kit.L(r, k, "fr-row-k");
            Kit.L(r, v, "fr-row-v");
        }

        public override void Back() { GauntletFlow.AfterFloor(Fe, P); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Continue")); }
    }
}
