// ===========================================================
// Assets/SquashGame/Runtime/UI/CareerPlayer.cs
// The career's PLAYER page, after EA FC's Clubs "Archetypes" screen
// (2026-10-29, the user: "this is what I expect the career mode to look
// about like"):
//
//   the icon row     your four attributes (Speed, Power, Accuracy,
//                    Stamina) - the one picked lit
//   the card         your trading card: the portrait, your level, the stat
//                    column and its total, your name and ranking
//   the middle       the attribute: what it does, the points you have, its
//                    level -> the next with the price, and its pips
//   the right        Upgrade, Equipment, Trophy cabinet, Your record
//
// Your record (CareerRecordScreen) has what the page used to: the
// matches, titles by tier, rivals and the match history.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>An attribute's drawn icon: chevrons (Speed), a bolt (Power), a target (Accuracy), a heart (Stamina).</summary>
    public sealed class AttrIcon : VisualElement
    {
        readonly string _attr;
        public Color Tint = Color.white;

        public AttrIcon(string attribute)
        {
            _attr = attribute;
            AddToClassList("fc-icon-art");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 2f) return;
            var c = r.center;
            var p = mgc.painter2D;
            p.strokeColor = Tint; p.fillColor = Tint;
            p.lineWidth = s * 0.08f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            Vector2 P(float x, float y) { return c + new Vector2(x * s, y * s); }
            switch (_attr)
            {
                case "Speed":
                    foreach (float dx in new[] { -0.16f, 0.1f })
                    {
                        p.BeginPath(); p.MoveTo(P(dx - 0.1f, -0.24f)); p.LineTo(P(dx + 0.12f, 0f)); p.LineTo(P(dx - 0.1f, 0.24f)); p.Stroke();
                    }
                    break;
                case "Power":
                    p.BeginPath(); p.MoveTo(P(0.06f, -0.32f)); p.LineTo(P(-0.16f, 0.04f)); p.LineTo(P(0.0f, 0.04f)); p.LineTo(P(-0.06f, 0.32f)); p.LineTo(P(0.17f, -0.06f)); p.LineTo(P(0.01f, -0.06f)); p.ClosePath(); p.Fill();
                    break;
                case "Control":
                    p.BeginPath(); p.Arc(c, s * 0.28f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    p.BeginPath(); p.Arc(c, s * 0.15f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    p.BeginPath(); p.Arc(c, s * 0.05f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    break;
                default:                                                         // Stamina: a heart
                    p.BeginPath();
                    p.MoveTo(P(0f, 0.27f));
                    p.BezierCurveTo(P(-0.34f, 0.02f), P(-0.26f, -0.3f), P(0f, -0.12f));
                    p.BezierCurveTo(P(0.26f, -0.3f), P(0.34f, 0.02f), P(0f, 0.27f));
                    p.Fill();
                    break;
            }
        }
    }

    /// <summary>The icon row's choice: a ring with the attribute's icon, its name under it; the one picked is lit.</summary>
    public sealed class AttrIconItem : MenuItem
    {
        public readonly string Attribute;
        readonly AttrIcon _icon;
        readonly VisualElement _ring;
        readonly Label _dot;
        public override bool WantsSlant { get { return false; } }
        public System.Action OnPress;

        public AttrIconItem(string attribute) : base(CareerSession.DisplayName(attribute))
        {
            Attribute = attribute;
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("fc-icon");
            _ring = Kit.Box(null, "fc-icon-ring");
            Root.Insert(0, _ring);
            _icon = new AttrIcon(attribute);
            _ring.Add(_icon);
            LabelEl.RemoveFromClassList("fe-item__label");
            LabelEl.AddToClassList("fc-icon-name");
            _dot = Kit.L(_ring, "", "fc-icon-dot");
        }

        public void SetPicked(bool on, bool canRaise)
        {
            Root.EnableInClassList("fc-icon--on", on);
            _icon.Tint = on ? new Color(0.04f, 0.05f, 0.06f) : Color.white;
            _icon.MarkDirtyRepaint();
            _dot.style.display = canRaise ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void Submit() { OnPress?.Invoke(); }
    }

    /// <summary>Your record: the matches, the titles by tier, the rivals and the match history (the PLAYER page's old panels).</summary>
    public sealed class CareerRecordScreen : GameScreen
    {
        /// <summary>The way into the trophy cabinet (tests).</summary>
        public ActionItem Cabinet { get; private set; }
        protected override string Kicker { get { return "CAREER  ·  " + Career.When(Career.S); } }
        protected override string Title { get { return "Your record"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var c = Career.C;
            var s = c.S;
            const int You = CareerSession.You;
            var pg = Kit.Box(Content, "k-cr-pgrid");
            var cs = Kit.Panel(pg, "CAREER");
            var tiles = Kit.Box(cs, "k-cr-tiles");
            int won = 0, lost = 0;
            foreach (var mrec in s.Matches) if (mrec.Won) won++; else lost++;
            Career.Tile(tiles, "MATCHES", won + "-" + lost);
            Career.Tile(tiles, "WIN %", won + lost == 0 ? "–" : (100 * won / (won + lost)) + "%");
            Career.Tile(tiles, "TITLES", s.Titles.ToString());
            Career.Tile(tiles, "EARNINGS", Career.Money(s.Earnings));
            Career.Tile(tiles, "CASH", Career.Money(s.Cash));
            var byTier = Kit.Box(cs, "k-cr-bytier");
            var counts = new int[Career.BandNames.Length];
            foreach (var r in s.Results) if (r.Player == You && r.Reached == 0) counts[Tour.Band(r.Tier)]++;
            for (int b0 = 0; b0 < counts.Length; b0++)
            {
                var b = Kit.Box(byTier, "k-cr-bytier-item");
                b.Add(new TierBadge(b0, 18));
                Kit.L(b, counts[b0] + "  " + Career.BandNames[b0], "k-cr-bytier-n");
            }
            int best = CareerSession.Unranked;
            foreach (var sr in s.Seasons) best = Mathf.Min(best, sr.Rank);
            Kit.L(cs, "Best season finish " + (s.Seasons.Count == 0 ? "– (first season)" : Career.Rank(best)), "k-cr-note");
            var rv = Kit.Box(cs, "k-cr-privals");
            Kit.L(rv, "RIVALS", "k-cr-attr-line");
            var rivals = c.Rivals();
            if (rivals.Count == 0) Kit.L(rv, "None yet. A pro who beats you twice, or meets you in two finals, becomes one.", "k-cr-empty");
            foreach (var id in rivals)
            {
                c.HeadToHead(id, out int w, out int l);
                var rr = Kit.Box(rv, "k-cr-rival");
                Kit.L(rr, "★", "k-cr-rival-star");
                Kit.L(rr, c.NameOf(id), "k-cr-rival-name");
                Kit.L(rr, Career.Rank(c.RankOf(id)) + "   " + w + "-" + l, "k-cr-rival-h2h");
            }
            Cabinet = AddTo(cs, new ActionItem("Trophy cabinet  ·  " + c.Trophies.Count + (c.Trophies.Count == 1 ? " title" : " titles"), () => Fe.Push(new TrophyCabinetScreen())));
            Cabinet.Root.AddToClassList("k-cr-eq-btn");
            var hist = Kit.Panel(pg, "MATCH HISTORY"); hist.AddToClassList("k-cr-hist");
            if (s.Matches.Count == 0) Kit.L(hist, "No matches yet. Your first event is waiting.", "k-cr-empty");
            for (int i = s.Matches.Count - 1, k = 0; i >= 0 && k < 12; i--, k++)
            {
                var mrec = s.Matches[i];
                var hr = Kit.Box(hist, "k-cr-hrow");
                var wl = Kit.L(hr, mrec.Won ? "W" : "L", "k-cr-chip"); if (mrec.Won) wl.AddToClassList("k-cr-chip--w");
                var mid2 = Kit.Box(hr, "k-cr-hmid");
                Kit.L(mid2, "v " + c.NameOf(mrec.Opp) + "  (" + Career.Rank(mrec.OppRank) + ")", "k-cr-hname");
                Kit.L(mid2, mrec.Event + "  ·  " + Tour.RoundName(mrec.Left) + (mrec.Simulated ? "  ·  simulated" : ""), "k-cr-hline");
                Kit.L(hr, mrec.Score ?? "", "k-cr-hscore");
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.B, "Back")); }
    }
}
