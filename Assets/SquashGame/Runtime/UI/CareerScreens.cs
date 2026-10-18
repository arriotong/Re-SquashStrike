// ===========================================================
// Assets/SquashGame/Runtime/UI/CareerScreens.cs
// P2-M4: the Career's screens, in the old SquashStrike look.
//
//   CareerStartScreen     continue, or a new career
//   CreatePlayerScreen    name, nationality, match length, scoring
//   CareerHubScreen       this month's events (or your next match),
//                         rankings, calendar; the panel on the right:
//                         ranking, points, titles, earnings, form, rivals
//   ScoutingScreen        the opponent: ranking, style, form, head to
//                         head, rival badge -> play / simulate
//   DrawScreen            the bracket, round by round
//   RankingsScreen        everyone, you highlighted
//   CalendarScreen        the year's months, events and your results
//   SimResultScreen       a simulated match's result
//   EventResultScreen     how far you got, points, prize, new ranking
//   SeasonSummaryScreen   the season's record
//   CareerSettingsScreen  match length and scoring for career matches
//   RetireScreen          leaving a career match = retiring (a loss)
//
// The career itself is SquashSim.Career; this is its presentation. It
// is saved (CareerStore) after every change.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashSim.Career;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class Career
    {
        public static CareerSession C { get { return CareerStore.Current; } }
        public static CareerState S { get { return CareerStore.Current.S; } }

        public static string Money(int n) { return "$" + n.ToString("N0"); }
        public static string Rank(int r) { return r > Tour.RosterSize ? "unranked" : "#" + r; }
        public static string TierName(int t) { return Tour.Info((Tier)t).Name; }
        public static string When(CareerState s) { return "SEASON " + s.Season + "  ·  " + Tour.Month(s.Month).ToUpperInvariant(); }
        public static readonly string[] LengthLabels = { "One game", "Best of 3", "Best of 5" };

        /// <summary>A label in a panel.</summary>
        public static Label L(VisualElement parent, string text, string cls)
        {
            var l = new Label(text); l.AddToClassList(cls); parent.Add(l); return l;
        }

        /// <summary>The right-hand panel (the old home's preview area).</summary>
        public static VisualElement Panel(VisualElement root)
        {
            var p = new VisualElement(); p.AddToClassList("cr-panel"); p.pickingMode = PickingMode.Ignore;
            root.Add(p);
            return p;
        }

        public static void Stat(VisualElement box, string name, string value)
        {
            var row = new VisualElement(); row.AddToClassList("pm-stat");
            var n = new Label(name); n.AddToClassList("pm-stat__name"); n.style.flexGrow = 1;
            var v = new Label(value); v.AddToClassList("pm-stat__val");
            row.Add(n); row.Add(v);
            box.Add(row);
        }

        public static string Form(CareerSession c, int id)
        {
            var f = c.Form(id);
            return f.Count == 0 ? "no results yet" : string.Join("  ", f);
        }

        /// <summary>Start your next career match in the match scene.</summary>
        public static void PlayNext(FrontEnd fe)
        {
            var c = C;
            int opp = c.NextOpponent();
            if (opp < 0) return;
            var pro = c.Pro(opp);
            fe.StartMatch(new MatchSettings
            {
                TwoPlayer = false,
                Opponent = pro.Style,
                Difficulty = (float)pro.Strength,
                GamesToWin = c.S.GamesToWin,
                Scoring = c.S.English ? "english" : "par",
                WarmUp = false,
                Career = true,
                P1Name = c.BugOf(CareerSession.You),
                P2Name = pro.Bug,
            });
        }

        /// <summary>Your match is over (post-match Continue, or retiring): record it and go back to the hub.</summary>
        public static void Record(FrontEnd fe, bool won, bool retired)
        {
            var c = C;
            if (c != null && c.NextOpponent() >= 0)
            {
                var st = fe.Stats;
                var games = new List<string>();
                if (st != null) foreach (var g in st.Games) games.Add(g[0] + "-" + g[1]);
                int mine = st != null ? st.Players[0].Rallies : 0, all = st != null ? st.Players[0].Rallies + st.Players[1].Rallies : 0;
                c.RecordYours(won, string.Join(" ", games), mine, all, simulated: false, retired: retired);
                CareerStore.Save();
            }
            fe.QuitToMenu();
            fe.Push(new CareerHubScreen());
        }
    }

    // ---------------- start / create ----------------

    public sealed class CareerStartScreen : MenuScreen
    {
        protected override string Kicker { get { return "PLAY"; } }
        protected override string Title { get { return "Career"; } }
        protected override void Build()
        {
            Text("Climb the world tour: club events first, then Regional, Challenger, the Tour, the Majors - and the World Finals.");
            if (CareerStore.Exists)
            {
                var c = Career.C;
                Add(new ButtonItem("Continue", () => Fe.Replace(new CareerHubScreen()), true,
                    () => c.S.Name + "  ·  " + Career.Rank(c.RankOf(CareerSession.You)) + "  ·  season " + c.S.Season));
                Add(new ButtonItem("New career", () => Fe.Push(new ConfirmScreen("Start again? Your career will be lost.", () => Fe.Push(new CreatePlayerScreen())))));
            }
            else Add(new ButtonItem("New career", () => Fe.Push(new CreatePlayerScreen()), true));
            Add(new ButtonItem("Back", () => Fe.Pop()));
        }
    }

    public sealed class CreatePlayerScreen : MenuScreen
    {
        protected override string Kicker { get { return "CAREER"; } }
        protected override string Title { get { return "Your player"; } }
        protected override string Hint { get { return "↑↓ choose    ←→ change    Enter / A  select (Enter types the name)    Esc / B  back"; } }
        int _nat, _length = 1, _scoring;
        TextItem _name;

        protected override void Build()
        {
            Text("No stats to buy - your skill is the only thing that improves.");
            _name = Add(new TextItem("Name", "Player", 16));
            Add(new OptionItem("Nationality", () => Roster.Nations[_nat], d => _nat = OptionItem.Step(_nat, d, Roster.Nations.Length)));
            Add(new OptionItem("Match", () => Career.LengthLabels[_length], d => _length = OptionItem.Step(_length, d, 3)));
            Add(new OptionItem("Scoring", () => MatchChoices.ScoringLabels[_scoring], d => _scoring = OptionItem.Step(_scoring, d, 2)));
            Add(new ButtonItem("Start career", Create, true));
        }

        /// <summary>The name to use (trimmed; "Player" when empty).</summary>
        public string NameValue { get { var n = _name.Value.Trim(); return n.Length == 0 ? "Player" : n; } }

        void Create()
        {
            CareerStore.New(NameValue, Roster.Nations[_nat], _length + 1, _scoring == 1);
            Fe.Pop();                                             // this screen
            Fe.Replace(new CareerHubScreen());                     // the start screen
        }
    }

    // ---------------- the hub ----------------

    public sealed class CareerHubScreen : MenuScreen
    {
        protected override string Kicker { get { return "CAREER  ·  " + Career.When(Career.S); } }
        protected override string Title { get { return Career.S.Name; } }

        protected override void Build()
        {
            var c = Career.C;
            var s = c.S;
            if (s.Current != null)
            {
                int opp = c.NextOpponent();
                Section(s.Current.Name);
                if (opp >= 0)
                    Add(new ButtonItem("Next: " + Tour.RoundName(c.PlayersLeft) + " v " + c.Pro(opp).Last, () => Fe.Push(new ScoutingScreen(opp)), true));
                Add(new ButtonItem("View the draw", () => Fe.Push(new DrawScreen(s.Current))));
            }
            else
            {
                Section(Tour.Month(s.Month) + " - enter one");
                bool first = true;
                foreach (var t in c.EventsNow())
                {
                    var tier = t;
                    string why = c.Entry(t);
                    var b = Add(new ButtonItem(Tour.EventName(s.Month, t), () => Enter(tier), why == null && first,
                        () => Tour.Info(tier).Name + (c.Entry(tier) != null ? "  ·  " + c.Entry(tier) : "")));
                    b.Enabled = () => c.CanEnter(tier);
                    if (why == null) first = false;
                }
                Add(new ButtonItem("Skip this month", Skip));
            }
            Section("Tour");
            Add(new ButtonItem("Rankings", () => Fe.Push(new RankingsScreen())));
            Add(new ButtonItem("Calendar", () => Fe.Push(new CalendarScreen())));
            Add(new ButtonItem("Match settings", () => Fe.Push(new CareerSettingsScreen())));
            Add(new ButtonItem("Main menu", () => Fe.Pop()));
            BuildPanel(c);
        }

        void BuildPanel(CareerSession c)
        {
            var s = c.S;
            var p = Career.Panel(Root);
            int rank = c.RankOf(CareerSession.You);
            Career.L(p, "WORLD RANKING", "cr-label");
            Career.L(p, "#" + rank, "cr-big");
            Career.L(p, (rank > Tour.RosterSize ? "unranked  ·  " : "") + c.PointsOf(CareerSession.You).ToString("N0") + " points   ·   " + s.Nat, "cr-sub");
            var box = new VisualElement(); box.AddToClassList("pm-stats"); box.AddToClassList("cr-stats");
            int won = 0, lost = 0;
            foreach (var m in s.Matches) if (m.Won) won++; else lost++;
            Career.Stat(box, "Matches", won + " won  ·  " + lost + " lost");
            Career.Stat(box, "Titles", s.Titles.ToString());
            Career.Stat(box, "Earnings", Career.Money(s.Earnings));
            Career.Stat(box, "Form", Career.Form(c, CareerSession.You));
            var rivals = c.Rivals();
            var names = new List<string>();
            foreach (var r in rivals) names.Add(c.Pro(r).Last);
            Career.Stat(box, "Rivals", names.Count == 0 ? "-" : string.Join(", ", names));
            p.Add(box);
            Career.L(p, OpensNext(c, rank), "cr-note");
        }

        static string OpensNext(CareerSession c, int rank)
        {
            for (int t = (int)Tier.Major; t >= (int)Tier.Regional; t--)
            {
                var info = Tour.Info((Tier)t);
                if (rank <= info.UnlockRank) return t == (int)Tier.Major ? "Every tier is open to you." : Tour.Info((Tier)(t + 1)).Name + " events open at #" + Tour.Info((Tier)(t + 1)).UnlockRank + ".";
            }
            return "Regional events open at #" + Tour.Info(Tier.Regional).UnlockRank + ".";
        }

        void Enter(Tier t)
        {
            Career.C.Enter(t);
            CareerStore.Save();
            Fe.Replace(new CareerHubScreen());
        }

        void Skip()
        {
            Career.C.Skip();
            CareerStore.Save();
            Fe.Replace(new CareerHubScreen());
        }

        /// <summary>The month's end, results and the season summary, as they fall due.</summary>
        public override void Tick(float dt)
        {
            var c = Career.C;
            if (c == null) { Fe.Pop(); return; }                     // the career is gone (deleted)
            if (c.S.Current != null && c.CanFinishSlot)
            {
                c.FinishSlot();
                CareerStore.Save();
                var last = c.S.Last;
                Fe.Replace(new CareerHubScreen());
                if (last != null) Fe.Push(new EventResultScreen(last));
                return;
            }
            if (c.S.SeasonJustEnded) Fe.Push(new SeasonSummaryScreen());
        }

        public override void Back() { Fe.Pop(); }
    }

    // ---------------- before a match ----------------

    public sealed class ScoutingScreen : MenuScreen
    {
        readonly int _opp;
        public ScoutingScreen(int opp) { _opp = opp; }
        Pro P { get { return Career.C.Pro(_opp); } }
        protected override string Kicker { get { var c = Career.C; return (c.S.Current.Name + "  ·  " + Tour.RoundName(c.PlayersLeft)).ToUpperInvariant(); } }
        protected override string Title { get { return P.Name; } }

        protected override void Build()
        {
            var c = Career.C;
            bool finals = c.S.Current.Tier == (int)Tier.WorldFinals;
            Add(new ButtonItem("Play the match", () => Career.PlayNext(Fe), true));
            var sim = Add(new ButtonItem("Simulate the match", Simulate, false, () => finals ? "not in the World Finals" : "decided for you"));
            sim.Enabled = () => !finals;
            Add(new ButtonItem("View the draw", () => Fe.Push(new DrawScreen(c.S.Current))));
            Add(new ButtonItem("Back", () => Fe.Pop()));

            var p = Career.Panel(Root);
            Career.L(p, "SCOUTING REPORT", "cr-label");
            if (c.IsRival(_opp)) Career.L(p, "RIVAL", "cr-rival");
            Career.L(p, Career.Rank(c.RankOf(_opp)), "cr-big");
            Career.L(p, P.Nat + "   ·   " + Roster.StyleName(P.Style) + "   ·   age " + P.Age, "cr-sub");
            var box = new VisualElement(); box.AddToClassList("pm-stats"); box.AddToClassList("cr-stats");
            c.HeadToHead(_opp, out int w, out int l);
            Career.Stat(box, "Form", Career.Form(c, _opp));
            Career.Stat(box, "Head to head", w + " - " + l);
            Career.Stat(box, "Points", c.PointsOf(_opp).ToString("N0"));
            p.Add(box);
            foreach (var line in Roster.StyleLines(P.Style)) Career.L(p, line, "cr-line");
        }

        void Simulate()
        {
            var c = Career.C;
            int opp = _opp;
            var res = c.SimulateYours();
            CareerStore.Save();
            Fe.Pop();                                                  // this screen
            Fe.Replace(new CareerHubScreen());
            Fe.Push(new SimResultScreen(opp, res.AWon, res.Score));
        }
    }

    public sealed class SimResultScreen : MenuScreen
    {
        readonly int _opp; readonly bool _won; readonly string _score;
        public SimResultScreen(int opp, bool won, string score) { _opp = opp; _won = won; _score = score; }
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "SIMULATED"; } }
        protected override string Title { get { return (_won ? "Won v " : "Lost v ") + Career.C.Pro(_opp).Last; } }
        protected override void Build()
        {
            var l = new Label(_score); l.AddToClassList("pm-score"); Content.Add(l);
            Add(new ButtonItem("Continue", () => Fe.Pop(), true));
        }
    }

    // ---------------- the draw, rankings, calendar ----------------

    public sealed class DrawScreen : MenuScreen
    {
        readonly EventState _e;
        public DrawScreen(EventState e) { _e = e; }
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string RootClass { get { return "cr-wide"; } }
        protected override string Kicker { get { return Career.TierName(_e.Tier).ToUpperInvariant(); } }
        protected override string Title { get { return _e.Name; } }

        protected override void Build()
        {
            var c = Career.C;
            var cols = new VisualElement(); cols.AddToClassList("cr-draw");
            for (int r = 0; r < _e.Rounds.Count; r++)
            {
                var round = _e.Rounds[r];
                var col = new VisualElement(); col.AddToClassList("cr-draw-col");
                Career.L(col, Tour.RoundName(round.Players.Count).ToUpperInvariant(), "cr-label");
                for (int m = 0; m * 2 + 1 < round.Players.Count; m++)
                {
                    // a match: the two names, the score beside them
                    var match = new VisualElement(); match.AddToClassList("cr-draw-match");
                    var names = new VisualElement(); names.style.flexGrow = 1; names.style.flexShrink = 1;
                    int winner = m < round.Winners.Count ? round.Winners[m] : -1;
                    for (int k = 0; k < 2; k++)
                    {
                        int id = round.Players[m * 2 + k];
                        var n = Career.L(names, (id == CareerSession.You ? "► " : "") + Short(c, id), "cr-draw-name");
                        if (winner == id) n.AddToClassList("cr-draw-name--won");
                        if (id == CareerSession.You) n.AddToClassList("cr-draw-name--you");
                    }
                    match.Add(names);
                    if (m < round.Scores.Count && !string.IsNullOrEmpty(round.Scores[m])) Career.L(match, round.Scores[m].Replace(" ", System.Environment.NewLine), "cr-draw-score");
                    col.Add(match);
                }
                cols.Add(col);
            }
            if (_e.Done) { var col = new VisualElement(); col.AddToClassList("cr-draw-col"); Career.L(col, "CHAMPION", "cr-label"); Career.L(col, Short(c, CareerSession.Winner(_e)), "cr-draw-name").AddToClassList("cr-draw-name--won"); cols.Add(col); }
            Content.Add(cols);
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }

        static string Short(CareerSession c, int id) { return id == CareerSession.You ? c.S.Name : c.Pro(id).First[0] + ". " + c.Pro(id).Last; }
    }

    public sealed class RankingsScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string RootClass { get { return "cr-wide"; } }
        protected override string Kicker { get { return "CAREER  ·  " + Career.When(Career.S); } }
        protected override string Title { get { return "World rankings"; } }

        protected override void Build()
        {
            var c = Career.C;
            var rows = c.Ranking();
            var cols = new VisualElement(); cols.AddToClassList("cr-draw");
            const int per = 27;
            for (int k = 0; k * per < rows.Count; k++)
            {
                var col = new VisualElement(); col.AddToClassList("cr-rank-col");
                for (int i = k * per; i < rows.Count && i < (k + 1) * per; i++)
                {
                    var r = rows[i];
                    bool you = r.Id == CareerSession.You;
                    var line = new VisualElement(); line.AddToClassList("cr-rank-row");
                    if (you) line.AddToClassList("cr-rank-row--you");
                    Career.L(line, r.Rank.ToString(), "cr-rank-n");
                    Career.L(line, (you ? c.S.Name : c.Pro(r.Id).Name) + (c.IsRival(r.Id) ? "  ★" : ""), "cr-rank-name");
                    Career.L(line, c.NatOf(r.Id), "cr-rank-nat");
                    Career.L(line, r.Points.ToString("N0"), "cr-rank-pts");
                    col.Add(line);
                }
                cols.Add(col);
            }
            Content.Add(cols);
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }
    }

    public sealed class CalendarScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string RootClass { get { return "cr-wide"; } }
        protected override string Kicker { get { return "CAREER  ·  SEASON " + Career.S.Season; } }
        protected override string Title { get { return "Calendar"; } }

        protected override void Build()
        {
            var c = Career.C;
            var s = c.S;
            var list = new VisualElement(); list.AddToClassList("cr-cal");
            for (int m = 0; m < Tour.SlotsPerSeason; m++)
            {
                var row = new VisualElement(); row.AddToClassList("cr-rank-row");
                if (m == s.Month) row.AddToClassList("cr-rank-row--you");
                Career.L(row, Tour.Month(m), "cr-rank-n");
                var names = new List<string>();
                foreach (var t in Tour.EventsIn(m)) names.Add(Tour.Info(t).Name);
                Career.L(row, Tour.EventName(m, Tour.EventsIn(m)[0]) + "   ·   " + string.Join(" / ", names), "cr-rank-name");
                string mine = "";
                int slot = s.Season * Tour.SlotsPerSeason + m;
                foreach (var r in s.Results) if (r.Player == CareerSession.You && r.Slot == slot) mine = Career.TierName(r.Tier) + "  " + CareerSession.ReachedShort(r.Reached) + "  +" + r.Points;
                if (m == s.Month && s.Current != null) mine = "playing " + s.Current.Name;
                Career.L(row, mine, "cr-cal-mine");
                list.Add(row);
            }
            Content.Add(list);
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }
    }

    // ---------------- after an event / a season ----------------

    public sealed class EventResultScreen : MenuScreen
    {
        readonly EventState _e;
        public EventResultScreen(EventState e) { _e = e; }
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return _e.Name.ToUpperInvariant(); } }
        protected override string Title { get { return _e.YouReached == 0 ? "Champion!" : CareerSession.ReachedLong(_e.YouReached); } }

        protected override void Build()
        {
            var c = Career.C;
            var box = new VisualElement(); box.AddToClassList("pm-stats");
            Career.Stat(box, "Ranking points", "+" + _e.YouPoints.ToString("N0"));
            Career.Stat(box, "Prize money", Career.Money(_e.YouPrize));
            Career.Stat(box, "World ranking", Career.Rank(c.RankOf(CareerSession.You)));
            int champ = CareerSession.Winner(_e);
            if (champ != CareerSession.You && champ >= 0) Career.Stat(box, "Champion", c.NameOf(champ));
            Content.Add(box);
            Add(new ButtonItem("Continue", () => Fe.Pop(), true));
            Add(new ButtonItem("View the draw", () => Fe.Push(new DrawScreen(_e))));
        }
    }

    public sealed class SeasonSummaryScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        SeasonRec R { get { var l = Career.S.Seasons; return l[l.Count - 1]; } }
        protected override string Kicker { get { return "SEASON " + R.Season + " COMPLETE"; } }
        protected override string Title { get { return "Ranked " + Career.Rank(R.Rank); } }

        protected override void Build()
        {
            var r = R;
            var box = new VisualElement(); box.AddToClassList("pm-stats");
            Career.Stat(box, "Matches", r.Won + " won  ·  " + (r.Played - r.Won) + " lost");
            Career.Stat(box, "Titles", r.Titles.ToString());
            Career.Stat(box, "Earnings", Career.Money(r.Earnings));
            Career.Stat(box, "Best win", string.IsNullOrEmpty(r.BestWin) ? "-" : r.BestWin);
            Content.Add(box);
            Text("A new season: the tour moves on - some veterans retire, newcomers arrive.");
            Add(new ButtonItem("On to season " + Career.S.Season, Done, true));
        }

        void Done() { Career.S.SeasonJustEnded = false; CareerStore.Save(); Fe.Pop(); }
        public override void Back() { Done(); }
    }

    // ---------------- settings / retiring ----------------

    public sealed class CareerSettingsScreen : MenuScreen
    {
        protected override string Kicker { get { return "CAREER"; } }
        protected override string Title { get { return "Match settings"; } }
        protected override void Build()
        {
            var s = Career.S;
            Add(new OptionItem("Match", () => Career.LengthLabels[System.Math.Max(0, System.Math.Min(2, s.GamesToWin - 1))],
                d => s.GamesToWin = OptionItem.Step(s.GamesToWin - 1, d, 3) + 1));
            Add(new OptionItem("Scoring", () => MatchChoices.ScoringLabels[s.English ? 1 : 0], d => s.English = !s.English));
            Add(new ButtonItem("Back", () => Fe.Pop(), true));
        }
        public override void OnHide() { CareerStore.Save(); }
    }

    public sealed class RetireScreen : MenuScreen
    {
        protected override bool Centred { get { return true; } }
        public override bool Dims { get { return true; } }
        protected override string Kicker { get { return "CAREER MATCH"; } }
        protected override string Title { get { return "Retire from the match?"; } }
        protected override void Build()
        {
            Text("Leaving counts as a loss.");
            Add(new ButtonItem("No", () => Fe.Pop(), true));
            Add(new ButtonItem("Retire", () => { Fe.Clear(); Fe.Runner.Clock.Resume(); Career.Record(Fe, false, true); }));
        }
    }
}
