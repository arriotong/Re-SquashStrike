// ===========================================================
// Assets/SquashGame/Runtime/UI/Kit/KitData.cs
// UI redesign (U1): showing numbers like a sports broadcast.
//
//   TierBadge    a drawn diamond per tier (Club .. World Finals)
//   StatBars     two-sided comparison bars, "tale of the tape"
//   RankTable    ranked rows: movement arrows, points bars, your row lit
//   BracketView  a real draw: spaced rounds, connector lines (Painter2D),
//                seeds and scores, your path lit lime; matches can be
//                focusable (BracketMatchItem)
//   Timeline     nodes on a line: done / now / to come
//   CountUp      a number that counts up to its value
//   Stamp        a word that thumps on (CHAMPION, NEW BEST)
// ===========================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public sealed class TierBadge : VisualElement
    {
        /// <summary>Each tier's colour (Club .. World Finals).</summary>
        public static readonly Color[] Fill =
        {
            new Color32(150, 160, 172, 255), new Color32(80, 170, 255, 255), new Color32(31, 165, 154, 255),
            new Color32(200, 255, 61, 255), new Color32(255, 184, 59, 255), new Color32(255, 215, 106, 255)
        };
        readonly int _tier;

        public TierBadge(int tier, float size = 26f)
        {
            _tier = Mathf.Clamp(tier, 0, Fill.Length - 1);
            AddToClassList("k-tier");
            style.width = size; style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect; var c = r.center; float h = Mathf.Min(r.width, r.height) * 0.5f;
            var p = mgc.painter2D;
            p.BeginPath(); p.MoveTo(c + new Vector2(0, -h)); p.LineTo(c + new Vector2(h, 0)); p.LineTo(c + new Vector2(0, h)); p.LineTo(c + new Vector2(-h, 0)); p.ClosePath();
            p.fillColor = Fill[_tier]; p.Fill();
            p.strokeColor = new Color(1, 1, 1, 0.35f); p.lineWidth = 1.5f; p.Stroke();
            // a star on the top two tiers
            if (_tier >= 4)
            {
                p.BeginPath(); p.MoveTo(c + new Vector2(0, -h * 0.45f)); p.LineTo(c + new Vector2(h * 0.45f, 0)); p.LineTo(c + new Vector2(0, h * 0.45f)); p.LineTo(c + new Vector2(-h * 0.45f, 0)); p.ClosePath();
                p.fillColor = new Color(1, 1, 1, 0.55f); p.Fill();
            }
        }
    }

    /// <summary>Two-sided comparison bars: name in the middle, your value left, theirs right; the bigger side lit.</summary>
    public sealed class StatBars : VisualElement
    {
        public StatBars() { AddToClassList("k-statbars"); pickingMode = PickingMode.Ignore; }

        public StatBars Row(string name, double a, double b, string fa = null, string fb = null, bool lowerIsBetter = false)
        {
            var row = Kit.Box(this, "k-sb-row");
            double max = Math.Max(1e-9, Math.Max(Math.Abs(a), Math.Abs(b)));
            bool aWins = lowerIsBetter ? a < b : a > b, bWins = lowerIsBetter ? b < a : b > a;
            var la = Kit.L(row, fa ?? Fmt(a), "k-sb-val"); if (aWins) la.AddToClassList("k-sb-val--win");
            var barA = Kit.Box(row, "k-sb-bar"); barA.AddToClassList("k-sb-bar--a");
            var fillA = Kit.Box(barA, "k-sb-fill"); fillA.style.width = Length.Percent((float)(100 * Math.Abs(a) / max)); if (aWins) fillA.AddToClassList("k-sb-fill--win");
            Kit.L(row, name, "k-sb-name");
            var barB = Kit.Box(row, "k-sb-bar"); barB.AddToClassList("k-sb-bar--b");
            var fillB = Kit.Box(barB, "k-sb-fill"); fillB.style.width = Length.Percent((float)(100 * Math.Abs(b) / max)); if (bWins) fillB.AddToClassList("k-sb-fill--win");
            var lb = Kit.L(row, fb ?? Fmt(b), "k-sb-val"); if (bWins) lb.AddToClassList("k-sb-val--win");
            return this;
        }

        static string Fmt(double v) { return Math.Abs(v - Math.Round(v)) < 1e-9 ? v.ToString("0") : v.ToString("0.0"); }
    }

    /// <summary>A ranked table: position, movement (▲3 ▼1), name, a chip, a points bar; your row lit and kept in view.</summary>
    public sealed class RankTable : VisualElement
    {
        public sealed class R { public int Pos, Move, Points; public string Name, Chip; public bool You, Star; }

        public RankTable(IList<R> rows, int maxPoints = 0)
        {
            AddToClassList("k-table");
            pickingMode = PickingMode.Ignore;
            int max = maxPoints; foreach (var r in rows) max = Math.Max(max, r.Points);
            foreach (var r in rows)
            {
                var row = Kit.Box(this, "k-tr");
                if (r.You) row.AddToClassList("k-tr--you");
                Kit.L(row, r.Pos.ToString(), "k-tr-pos");
                var mv = Kit.L(row, r.Move > 0 ? "▲" + r.Move : r.Move < 0 ? "▼" + (-r.Move) : "–", "k-tr-move");
                mv.AddToClassList(r.Move > 0 ? "k-up" : r.Move < 0 ? "k-down" : "k-same");
                Kit.L(row, r.Name + (r.Star ? "  ★" : ""), "k-tr-name");
                Kit.L(row, r.Chip ?? "", "k-tr-chip");
                var bar = Kit.Box(row, "k-tr-bar");
                var fill = Kit.Box(bar, "k-tr-fill"); fill.style.width = Length.Percent(max > 0 ? 100f * r.Points / max : 0);
                Kit.L(row, r.Points.ToString("N0"), "k-tr-pts");
            }
        }
    }

    /// <summary>One match in a bracket: two names (seed, bold winner), the score; focusable to show its detail.</summary>
    public sealed class BracketMatchItem : MenuItem
    {
        public readonly int Round, Index;
        public readonly VisualElement Box;
        readonly Action<BracketMatchItem> _pick;
        public override bool WantsSlant { get { return false; } }

        public BracketMatchItem(int round, int index, string a, string b, int winner, string score, bool youA, bool youB, Action<BracketMatchItem> pick)
            : base("m" + round + "_" + index)
        {
            Round = round; Index = index; _pick = pick;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-bm");
            LabelEl.style.display = DisplayStyle.None;
            Box = Root;
            var names = Kit.Box(Root, "k-bm-names");
            var la = Kit.L(names, a ?? "TBD", "k-bm-name"); var lb = Kit.L(names, b ?? "TBD", "k-bm-name");
            if (a == null) la.AddToClassList("k-bm-name--tbd"); if (b == null) lb.AddToClassList("k-bm-name--tbd");
            if (winner == 0) la.AddToClassList("k-bm-name--won"); if (winner == 1) lb.AddToClassList("k-bm-name--won");
            if (youA) la.AddToClassList("k-bm-name--you"); if (youB) lb.AddToClassList("k-bm-name--you");
            if (youA || youB) Root.AddToClassList("k-bm--you");
            if (!string.IsNullOrEmpty(score)) Kit.L(Root, score.Replace(" ", "\n"), "k-bm-score");
            YouIn = youA || youB;
            YouWon = (youA && winner == 0) || (youB && winner == 1);
        }

        public bool YouIn { get; private set; }
        public bool YouWon { get; private set; }
        public override void Submit() { _pick?.Invoke(this); }
    }

    /// <summary>The draw: a column per round, each match centred between the two it comes from, joined by lines.</summary>
    public sealed class BracketView : VisualElement
    {
        public readonly List<List<BracketMatchItem>> Rounds = new List<List<BracketMatchItem>>();
        readonly VisualElement _lines = new VisualElement();
        readonly List<VisualElement> _cols = new List<VisualElement>();
        public const float MatchW = 200, MatchH = 52, ColGap = 56, HeaderH = 30;

        public BracketView()
        {
            AddToClassList("k-bracket");
            _lines.AddToClassList("k-bracket-lines");
            _lines.pickingMode = PickingMode.Ignore;
            _lines.generateVisualContent += PaintLines;
            Add(_lines);
        }

        /// <summary>Lay out the draw: rounds[r] holds that round's matches (built by the screen, added with its AddTo).</summary>
        public VisualElement Column(int round, string header)
        {
            var col = Kit.Box(this, "k-bracket-col");
            col.style.left = round * (MatchW + ColGap);
            Kit.L(col, header, "k-bracket-head");
            _cols.Add(col);
            while (Rounds.Count <= round) Rounds.Add(new List<BracketMatchItem>());
            return col;
        }

        /// <summary>Place a match: round r, index i, with n matches in round 0 setting the height.</summary>
        public void Place(BracketMatchItem m, int firstRoundMatches)
        {
            Rounds[m.Round].Add(m);
            float slot = MatchH + 14;
            float total = firstRoundMatches * slot;
            int inRound = firstRoundMatches >> m.Round;
            float span = total / Math.Max(1, inRound);
            m.Root.style.position = Position.Absolute;
            m.Root.style.left = m.Round * (MatchW + ColGap);
            m.Root.style.top = HeaderH + span * m.Index + (span - MatchH) * 0.5f;
            m.Root.style.width = MatchW; m.Root.style.height = MatchH;
            style.width = Rounds.Count * (MatchW + ColGap);
            style.height = HeaderH + total + 10;
            _lines.MarkDirtyRepaint();
        }

        void PaintLines(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            for (int r = 1; r < Rounds.Count; r++)
                foreach (var m in Rounds[r])
                {
                    foreach (int k in new[] { 0, 1 })
                    {
                        int src = m.Index * 2 + k;
                        if (src >= Rounds[r - 1].Count) continue;
                        var a = Rounds[r - 1][src];
                        float ax = (r - 1) * (MatchW + ColGap) + MatchW, ay = Top(a) + MatchH * 0.5f;
                        float bx = r * (MatchW + ColGap), by = Top(m) + MatchH * 0.5f, mx = ax + ColGap * 0.5f;
                        bool lit = a.YouIn && a.YouWon && m.YouIn;
                        p.strokeColor = lit ? new Color32(200, 255, 61, 255) : new Color(1, 1, 1, 0.2f);
                        p.lineWidth = lit ? 3f : 1.4f;
                        p.BeginPath(); p.MoveTo(new Vector2(ax, ay)); p.LineTo(new Vector2(mx, ay)); p.LineTo(new Vector2(mx, by)); p.LineTo(new Vector2(bx, by)); p.Stroke();
                    }
                }
        }

        static float Top(BracketMatchItem m) { var t = m.Root.style.top.value; return t.value; }
    }

    /// <summary>Nodes on a line (months, lessons): done ticked, the current one pulsing, the rest dim; a label and a note each.</summary>
    public sealed class Timeline : VisualElement
    {
        public Timeline(IList<(string label, string note, int state)> nodes)       // state: -1 done, 0 now, 1 to come
        {
            AddToClassList("k-timeline");
            pickingMode = PickingMode.Ignore;
            Kit.Box(this, "k-tl-line");
            var row = Kit.Box(this, "k-tl-row");
            foreach (var (label, note, state) in nodes)
            {
                var n = Kit.Box(row, "k-tl-node");
                var dot = Kit.Box(n, "k-tl-dot");
                dot.AddToClassList(state < 0 ? "k-tl-dot--done" : state == 0 ? "k-tl-dot--now" : "k-tl-dot--next");
                Kit.L(n, label, "k-tl-label");
                Kit.L(n, note ?? "", "k-tl-note");
            }
        }
    }

    /// <summary>A number that counts up from 0 (or a start) to its value over a moment.</summary>
    public sealed class CountUp : Label
    {
        double _from, _to, _t;
        readonly double _seconds;
        readonly string _format, _prefix, _suffix;
        public double Shown { get; private set; }

        public CountUp(double to, double seconds = 0.9, string format = "N0", string prefix = "", string suffix = "", double from = 0)
        {
            _from = from; _to = to; _seconds = seconds; _format = format; _prefix = prefix; _suffix = suffix;
            AddToClassList("k-count");
            pickingMode = PickingMode.Ignore;
            Set(0);
            schedule.Execute(() => { _t += 0.016; Set(Mathf.Clamp01((float)(_t / _seconds))); }).Every(16).Until(() => _t >= _seconds);
        }

        void Set(float u)
        {
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            Shown = _from + (_to - _from) * e;
            text = _prefix + Shown.ToString(_format) + _suffix;
        }
    }

    /// <summary>A word that thumps on: big, slightly rotated, then settles (CHAMPION, NEW BEST, 3 STARS).</summary>
    public sealed class Stamp : Label
    {
        public Stamp(string word, float delay = 0.2f)
        {
            text = word;
            AddToClassList("k-stamp");
            AddToClassList("k-stamp--in");
            pickingMode = PickingMode.Ignore;
            schedule.Execute(() => { RemoveFromClassList("k-stamp--in"); Sounds.Play("stamp"); }).StartingIn((long)(delay * 1000));
        }
    }
}
