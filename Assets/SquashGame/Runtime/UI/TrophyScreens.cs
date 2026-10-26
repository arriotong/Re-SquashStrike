// ===========================================================
// Assets/SquashGame/Runtime/UI/TrophyScreens.cs
// P7 phase 6 (2026-10-27): the trophy cabinet - every career title on
// shelves by the tour's bands, drawn (no image assets):
//
//   your club's open     a wooden plaque
//   Challengers          a plate (silver, the bigger ones blue)
//   Copper .. Gold       a cup in the tier's colour, handles from Silver
//   Platinum, Diamond    a tall cup on a plinth
//   the Worlds, Finals   the grand cup: lid, handles, a gold plinth
//
// An event won more than once is one trophy with its count ("x3"). The
// one in focus lists every time you won it: the season, whom you beat in
// the final and the score. Trophies are a cabinet you browse, not
// choices (ChoiceBudgetTests).
// ===========================================================

using System.Collections.Generic;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    /// <summary>A drawn trophy: its shape by the tour's band, its colour by the tier.</summary>
    public sealed class TrophyArt : VisualElement
    {
        readonly int _band;
        readonly bool _home;
        readonly Color _c;
        static readonly Color Gold = new Color32(255, 184, 59, 255), Wood = new Color32(120, 78, 44, 255), Silver = new Color32(200, 206, 214, 255);

        public TrophyArt(int tier, bool home, Color colour)
        {
            _band = Tour.Band(tier); _home = home; _c = Metal(tier, colour);
            pickingMode = PickingMode.Ignore;
            AddToClassList("k-tro-art");
            generateVisualContent += Paint;
        }

        /// <summary>A cup's metal by its tier (copper, bronze, silver, gold, platinum, diamond); the plates steel blue.</summary>
        static Color Metal(int tier, Color fallback)
        {
            switch ((Tier)tier)
            {
                case Tier.Challenger9: case Tier.Challenger12: case Tier.Challenger15: return new Color32(122, 162, 214, 255);
                case Tier.Copper: return new Color32(196, 116, 64, 255);
                case Tier.Bronze: return new Color32(205, 140, 70, 255);
                case Tier.Silver: return new Color32(206, 212, 220, 255);
                case Tier.Gold: return new Color32(255, 196, 64, 255);
                case Tier.Platinum: return new Color32(229, 228, 236, 255);
                case Tier.Diamond: return new Color32(170, 236, 255, 255);
                default: return fallback;
            }
        }

        void Paint(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 8) return;
            var p = mgc.painter2D;
            float w = r.width, h = r.height, cx = w / 2;
            if (_home)
            {
                Box(p, new Rect(w * 0.2f, h * 0.25f, w * 0.6f, h * 0.6f), Wood);
                Box(p, new Rect(w * 0.3f, h * 0.38f, w * 0.4f, h * 0.2f), Gold);
                return;
            }
            if (_band <= 1)
            {
                // a plate on a stand
                Box(p, new Rect(cx - w * 0.08f, h * 0.78f, w * 0.16f, h * 0.12f), new Color(0.3f, 0.32f, 0.36f));
                Disc(p, new Vector2(cx, h * 0.45f), w * 0.36f, _band == 0 ? Silver : _c);
                Disc(p, new Vector2(cx, h * 0.45f), w * 0.26f, Color.Lerp(_band == 0 ? Silver : _c, Color.black, 0.25f));
                Disc(p, new Vector2(cx, h * 0.45f), w * 0.08f, Gold);
                return;
            }
            float scale = _band == 2 ? 0.7f : _band == 3 ? 0.8f : _band == 4 ? 0.92f : 1f;
            float top = h * (1 - 0.9f * scale), cupW = w * 0.5f * scale;
            var cup = _band == 5 ? Gold : _c;
            // the plinth
            Box(p, new Rect(cx - w * 0.28f, h * 0.86f, w * 0.56f, h * 0.1f), _band >= 4 ? Gold : new Color(0.25f, 0.27f, 0.31f));
            if (_band >= 4) Box(p, new Rect(cx - w * 0.2f, h * 0.78f, w * 0.4f, h * 0.08f), new Color(0.18f, 0.19f, 0.22f));
            // the stem and foot
            float stemTop = top + cupW * 0.95f;
            Box(p, new Rect(cx - w * 0.04f, stemTop, w * 0.08f, h * 0.78f - stemTop), cup);
            Box(p, new Rect(cx - w * 0.14f, h * 0.74f, w * 0.28f, h * 0.05f), cup);
            // the bowl
            p.fillColor = cup;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - cupW / 2, top)); p.LineTo(new Vector2(cx + cupW / 2, top));
            p.BezierCurveTo(new Vector2(cx + cupW / 2, top + cupW * 0.7f), new Vector2(cx + cupW * 0.2f, stemTop), new Vector2(cx, stemTop));
            p.BezierCurveTo(new Vector2(cx - cupW * 0.2f, stemTop), new Vector2(cx - cupW / 2, top + cupW * 0.7f), new Vector2(cx - cupW / 2, top));
            p.ClosePath(); p.Fill();
            // handles (Silver up)
            if (_band >= 3)
            {
                p.strokeColor = cup; p.lineWidth = Mathf.Max(2, w * 0.035f);
                p.BeginPath(); p.Arc(new Vector2(cx - cupW / 2, top + cupW * 0.3f), cupW * 0.2f, Angle.Degrees(90), Angle.Degrees(270)); p.Stroke();
                p.BeginPath(); p.Arc(new Vector2(cx + cupW / 2, top + cupW * 0.3f), cupW * 0.2f, Angle.Degrees(270), Angle.Degrees(450)); p.Stroke();
            }
            // the grand cup's lid
            if (_band == 5)
            {
                Box(p, new Rect(cx - cupW * 0.4f, top - h * 0.05f, cupW * 0.8f, h * 0.05f), Gold);
                Disc(p, new Vector2(cx, top - h * 0.08f), w * 0.04f, Gold);
            }
            // a shine
            p.strokeColor = new Color(1, 1, 1, 0.35f); p.lineWidth = Mathf.Max(1.5f, w * 0.02f);
            p.BeginPath(); p.MoveTo(new Vector2(cx - cupW * 0.3f, top + cupW * 0.1f)); p.LineTo(new Vector2(cx - cupW * 0.22f, top + cupW * 0.55f)); p.Stroke();
        }

        static void Box(Painter2D p, Rect r, Color c)
        {
            p.fillColor = c; p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMax)); p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath(); p.Fill();
        }

        static void Disc(Painter2D p, Vector2 c, float rad, Color col) { p.fillColor = col; p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
    }

    /// <summary>One event's trophy on a shelf (all the times you won it).</summary>
    public sealed class TrophyItem : MenuItem
    {
        public readonly List<TrophyRec> Wins;
        public override bool WantsSlant { get { return false; } }

        public TrophyItem(List<TrophyRec> wins) : base("trophy_" + wins[0].Event)
        {
            Wins = wins;
            var t = wins[wins.Count - 1];
            Root.RemoveFromClassList("fe-item");
            Root.AddToClassList("k-tro");
            LabelEl.style.display = DisplayStyle.None;
            Root.Add(new TrophyArt(t.Tier, t.Home, Career.TierColor(t.Tier)));
            Kit.L(Root, t.Event, "k-tro-name");
            Kit.L(Root, "SEASON " + t.Season, "k-tro-meta");
            if (wins.Count > 1) Kit.L(Root, "×" + wins.Count, "k-tro-count");
        }
    }

    public sealed class TrophyCabinetScreen : GameScreen
    {
        protected override string Kicker { get { return "CAREER  ·  " + Career.S.Name.ToUpperInvariant(); } }
        protected override string Title { get { return "Trophy cabinet"; } }
        static readonly string[] Shelves = { "Your club's open", "Challenger 3 · 6", "Challenger 9 · 12 · 15", "Copper · Bronze", "Silver · Gold", "Platinum · Diamond", "World Championship · Tour Finals" };

        public readonly List<TrophyItem> Items = new List<TrophyItem>();
        ScrollView _scroll;
        VisualElement _detail;
        MenuItem _shown;

        protected override void Compose()
        {
            var c = Career.C;
            var wrap = Kit.Box(Content, "k-tro-wrap");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("k-tro-scroll");
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.contentContainer.AddToClassList("k-tro-shelves");
            wrap.Add(_scroll);
            _detail = Kit.Panel(wrap, "THE CABINET");
            _detail.AddToClassList("k-tro-detail");

            // shelves: the biggest at the top
            var byShelf = new List<List<List<TrophyRec>>>();
            for (int i = 0; i < Shelves.Length; i++) byShelf.Add(new List<List<TrophyRec>>());
            var byEvent = new Dictionary<string, List<TrophyRec>>();
            foreach (var t in c.Trophies)
            {
                string key = t.Tier + "|" + t.Event;
                if (!byEvent.TryGetValue(key, out var list)) { byEvent[key] = list = new List<TrophyRec>(); byShelf[t.Home ? 0 : 1 + Tour.Band(t.Tier)].Add(list); }
                list.Add(t);
            }
            int shown = 0;
            for (int i = Shelves.Length - 1; i >= 0; i--)
            {
                if (byShelf[i].Count == 0) continue;
                shown++;
                int count = 0; foreach (var l in byShelf[i]) count += l.Count;
                var band = Kit.Box(_scroll, "k-tro-band");
                Kit.L(band, Shelves[i].ToUpperInvariant() + "  ·  " + count, "k-tro-shelf-name");
                var shelf = Kit.Box(band, "k-tro-shelf");
                foreach (var l in byShelf[i]) Items.Add(AddTo(shelf, new TrophyItem(l)));
            }
            if (shown == 0) Kit.L(_scroll, "No titles yet. Win your club's open and the first one goes here.", "k-cr-empty");
            Summary();
        }

        void Summary()
        {
            var c = Career.C;
            _detail.Clear();
            Kit.L(_detail, "THE CABINET", "k-panel-title");
            int world = 0, majors = 0;
            foreach (var t in c.Trophies) { if (!t.Home && Tour.Info((Tier)t.Tier).WorldTour) world++; if (Tour.Band(t.Tier) >= 4) majors++; }
            Kit.L(_detail, c.Trophies.Count + (c.Trophies.Count == 1 ? " title" : " titles"), "k-tro-big");
            Kit.L(_detail, world + " on the World Tour  ·  " + majors + " Platinum or bigger", "k-tro-line");
        }

        void Show(TrophyItem it)
        {
            var c = Career.C;
            _detail.Clear();
            var last = it.Wins[it.Wins.Count - 1];
            Kit.L(_detail, (last.Home ? "CLUB OPEN" : Tour.Info((Tier)last.Tier).Name.ToUpperInvariant()) + "  ·  " + last.City.ToUpperInvariant(), "k-panel-title");
            Kit.L(_detail, last.Event, "k-tro-big");
            Kit.L(_detail, it.Wins.Count == 1 ? "Won once" : "Won " + it.Wins.Count + " times", "k-tro-line");
            for (int i = it.Wins.Count - 1; i >= 0; i--)
            {
                var t = it.Wins[i];
                var row = Kit.Box(_detail, "k-wm-dt-row");
                Kit.L(row, "SEASON " + t.Season + "  ·  " + Tour.WeekLabel(t.Slot).ToUpperInvariant(), "k-wm-dt-k");
                Kit.L(row, "Final: beat " + (string.IsNullOrEmpty(t.OppName) ? "?" : t.OppName) + (string.IsNullOrEmpty(t.Score) ? "" : "  " + t.Score), "k-wm-dt-v");
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Browse"), (PromptBar.Btn.B, "Back")); }

        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is TrophyItem it)) return;
            _shown = Focused;
            _scroll.ScrollTo(it.Root);
            Show(it);
        }
    }
}
