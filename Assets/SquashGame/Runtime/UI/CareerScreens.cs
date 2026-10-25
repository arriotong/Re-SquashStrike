// ===========================================================
// Assets/SquashGame/Runtime/UI/CareerScreens.cs
// The Career's screens. UI redesign U3: after FC's career "Central".
//
//   CreatePlayerScreen    one thing per step (name -> nation -> matches),
//                         your athlete live beside it
//   CareerHubScreen       tabs CENTRAL / CALENDAR / RANKINGS / PLAYER
//                         CENTRAL: the next thing to do (your match card,
//                         or this month's event cards), the ranking
//                         widget, news, the season strip; CONTINUE
//   MatchPreviewScreen    tale of the tape: you and them live, rank,
//                         points, titles, form, head to head -> PLAY
//   DrawScreen            the bracket with lines; a focused match shows
//                         its detail
//   SimResultScreen       a simulated match's result
//   EventResultScreen     the stamp, points and prize counting up, the
//                         ranking move, the rewards
//   SeasonSummaryScreen   the season review
//   CareerSettingsScreen  match length and scoring for career matches
//   RetireScreen          leaving a career match = retiring (a loss)
//
// The career itself is SquashSim.Career; this is its presentation. It
// is saved (CareerStore) after every change.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim.Career;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class Career
    {
        public static CareerSession C { get { return CareerStore.Current; } }
        public static CareerState S { get { return CareerStore.Current.S; } }
        const int You = CareerSession.You;

        public static string Money(int n) { return "$" + n.ToString("N0"); }
        public static string Rank(int r) { return r >= CareerSession.Unranked ? "unranked" : "#" + r; }
        /// <summary>Ranking points (an average): one decimal while small.</summary>
        public static string Pts(double v) { return v < 100 ? v.ToString("0.0") : v.ToString("N0"); }
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

        // ---------------- U3 helpers ----------------

        /// <summary>A pro's own kit, the same every match.</summary>
        public static uint KitSeed(CareerSession c, int pro) { return (c.S.Seed ^ ((uint)pro * 2654435761u)) | 1u; }

        /// <summary>"J. Madigan" (you: your name).</summary>
        public static string Short(CareerSession c, int id)
        {
            if (id == You) return c.S.Name;
            var p = c.Pro(id);
            return p == null ? "?" : p.First[0] + ". " + p.Last;
        }

        public static string StyleOf(CareerSession c, int id)
        {
            if (id == You) return "Your own game";
            var p = c.Pro(id);
            return p == null ? "" : Roster.StyleName(p.Style);
        }

        /// <summary>The field's strength, as stars.</summary>
        public static string Stars(int tier)
        {
            int n = Mathf.Clamp(Tour.Band(tier) + 1, 1, 5);
            return new string('★', n) + new string('☆', 5 - n);
        }

        public static Color TierColor(int t) { return TierBadge.Fill[Mathf.Clamp(Tour.Band(t), 0, TierBadge.Fill.Length - 1)]; }
        /// <summary>A tier's badge (its band's colour).</summary>
        public static TierBadge Badge(int tier, float size) { return new TierBadge(Tour.Band(tier), size); }
        public static readonly string[] BandNames = { "Challenger 3-6", "Challenger 9-20", "Copper-Bronze", "Silver-Gold", "Platinum-Diamond", "World" };
        public static Color TierBg(int t) { var c = TierColor(t); return new Color(0.08f + c.r * 0.08f, 0.09f + c.g * 0.08f, 0.11f + c.b * 0.08f, 1f); }

        /// <summary>A tier diamond in a card's picture, top left.</summary>
        public static void TierOn(VisualElement art, int tier)
        {
            var b = Badge(tier, 34); b.AddToClassList("k-cr-cardtier"); art.Add(b);
        }

        /// <summary>The seeds of an event (the best-ranked quarter of the field): id -> seed.</summary>
        public static Dictionary<int, int> Seeds(CareerSession c, EventState e)
        {
            var field = new List<int>(e.Rounds[0].Players);
            field.Sort((a, b) => c.RankOf(a).CompareTo(c.RankOf(b)));
            var seeds = new Dictionary<int, int>();
            for (int i = 0; i < field.Count / 4; i++) seeds[field[i]] = i + 1;
            return seeds;
        }

        /// <summary>Recent titles (the last two years of results).</summary>
        public static int Titles(CareerSession c, int id)
        {
            int n = 0, from = c.S.Slot - 2 * Tour.RankingWindow;
            foreach (var r in c.S.Results) if (r.Player == id && r.Reached == 0 && r.Slot >= from) n++;
            return n;
        }

        /// <summary>Rows of the ranking for a table: movement, nation, rival star, you lit.</summary>
        public static List<RankTable.R> Rows(CareerSession c, IEnumerable<CareerSession.Row> rows)
        {
            var list = new List<RankTable.R>();
            foreach (var r in rows)
                list.Add(new RankTable.R
                {
                    Pos = r.Rank, Move = c.Movement(r.Id), Points = r.Points, Name = c.NameOf(r.Id), Chip = c.NatOf(r.Id),
                    You = r.Id == You, Star = r.Id != You && c.IsRival(r.Id),
                });
            return list;
        }

        /// <summary>The ranking around a player: k places either side.</summary>
        public static List<CareerSession.Row> Around(CareerSession c, int id, int k)
        {
            var all = c.Ranking();
            int at = Mathf.Min(c.RankOf(id) - 1, all.Count - 1);       // unranked: the bottom of the list
            int from = Mathf.Clamp(at - k, 0, Mathf.Max(0, all.Count - (2 * k + 1)));
            return all.GetRange(from, Mathf.Min(2 * k + 1, all.Count - from));
        }

        /// <summary>"35 pts behind #47" (the next place up).</summary>
        public static string ToNext(CareerSession c)
        {
            var all = c.Ranking();
            if (!c.IsRanked(You)) return "Unranked. Your first event puts you on the list.";
            int at = c.RankOf(You) - 1;
            if (at <= 0) return "World No. 1. Stay there.";
            double gap = all[at - 1].Average - all[at].Average;
            return gap <= 0 ? "Level on points with #" + at + ": your next result moves you up." : Pts(gap) + " pts behind #" + at;
        }

        /// <summary>A season's months for a timeline: your result under each (or what's coming).</summary>
        public static List<(string, string, int)> Months(CareerSession c, int season)
        {
            var s = c.S;
            var list = new List<(string, string, int)>();
            for (int m = 0; m < Tour.SlotsPerSeason; m++)
            {
                int slot = season * Tour.SlotsPerSeason + m;
                int state = slot < s.Slot ? -1 : slot == s.Slot ? 0 : 1;
                string note = "";
                foreach (var r in s.Results) if (r.Player == You && r.Slot == slot) note = CareerSession.ReachedShort(r.Reached);
                if (state == -1 && note == "") note = "–";
                if (state == 0 && s.Current != null) note = "LIVE";
                if (state == 1)
                {
                    var top = Tour.HeadlineOf(m);
                    note = top == Tier.WorldTourFinals ? "FINALS" : top == Tier.WorldChampionship ? "WORLDS" : top == Tier.Platinum ? "PLAT" : Tour.Info(top).Name.ToUpperInvariant();
                }
                list.Add((Tour.Month(m).ToUpperInvariant(), note, state));
            }
            return list;
        }

        /// <summary>Three headlines from what really happened: your result, your move, last month's champions.</summary>
        public static List<(string kicker, string text)> News(CareerSession c)
        {
            var s = c.S;
            var list = new List<(string, string)>();
            if (s.TrainingPoints > 0) list.Add(("TRAINING", s.TrainingPoints + (s.TrainingPoints == 1 ? " point" : " points") + " to spend on your attributes - the PLAYER tab"));
            foreach (var g in c.Goals) if (g.Done && g.DoneSlot == s.Slot - 1) list.Add(("GOAL DONE", g.Text + "  ·  +" + g.Points + " points, " + Money(g.Money)));
            if (s.Month == 0 && !string.IsNullOrEmpty(s.SponsorNews)) list.Add(("SPONSOR", s.SponsorNews));
            int upset = c.UpsetWinner(out int upRank, out int upTier);
            if (upset >= 0) list.Add(("UPSET", "#" + upRank + " " + c.NameOf(upset) + " wins a " + Tour.Info((Tier)upTier).Name + " event"));
            if (s.Month <= 1)
            {
                int ret = c.LatestRetiree();
                if (ret >= 0)
                {
                    c.HeadToHead(ret, out int hw, out int hl);
                    list.Add(("RETIRES", c.NameOf(ret) + " retires" + (hl > 0 && hw == 0 ? " - and you never beat them" : hw > 0 ? " (you met " + (hw + hl) + " times)" : "")));
                }
            }
            foreach (var id in c.Rivals())
                if (c.RankOf(id) < c.RankOf(You) && c.Movement(id) > 0) { list.Add(("RIVAL", c.NameOf(id) + " climbs to #" + c.RankOf(id) + " - ahead of you")); break; }
            if (s.LastTrainedSlot == s.Slot - 1) list.Add(("TRAINING BLOCK", "A month on the practice court: +" + CareerSession.TrainingMonthPoints + " points"));
            if (s.Last != null && s.Last.Slot == s.Slot - 1 && s.Last.YouReached >= 0)
                list.Add(("YOUR RESULT", s.Last.YouReached == 0 ? "You win the " + s.Last.Name + "!" : CareerSession.ReachedLong(s.Last.YouReached) + " at the " + s.Last.Name));
            int mv = c.Movement(You), rank = c.RankOf(You);
            if (mv == 0 && rank != CareerSession.Unranked && c.EventsOf(You) == 1) list.Add(("RANKINGS", "You're on the world ranking: a new entry at #" + rank));
            if (mv > 0) list.Add(("RANKINGS", "You climb " + mv + (mv == 1 ? " place" : " places") + " to #" + rank));
            else if (mv < 0) list.Add(("RANKINGS", "You slip " + (-mv) + (mv == -1 ? " place" : " places") + " to #" + rank));
            int prev = s.Slot - 1, month = ((prev % Tour.SlotsPerSeason) + Tour.SlotsPerSeason) % Tour.SlotsPerSeason;
            var champs = s.Results.FindAll(r => r.Slot == prev && r.Reached == 0 && r.Player != You);
            champs.Sort((a, b) => b.Tier.CompareTo(a.Tier));
            foreach (var r in champs)
            {
                if (list.Count >= 3) break;
                list.Add((Tour.Info((Tier)r.Tier).Name.ToUpperInvariant(), c.NameOf(r.Player) + " wins the " + Tour.EventName(month, Tour.IndexOf(month, (Tier)r.Tier)) + (c.IsRival(r.Player) ? " (your rival)" : "")));
            }
            if (list.Count < 3) { var top = c.Ranking()[0]; list.Add(("WORLD NO. 1", c.NameOf(top.Id) + " leads the world on " + Pts(top.Average) + " points")); }
            if (list.Count < 3) list.Add(("THE TOUR", "Challenger 3s are open to all. Climb the ranking: the World Tour's Copper events open at #" + Tour.Info(Tier.Copper).UnlockRank + "."));
            if (list.Count < 3) list.Add(("WORLD TOUR FINALS", "The world's top 8 meet in December."));
            return list;
        }

        /// <summary>P3-R4: a season goal - done, failed or open - and what it pays.</summary>
        public static void GoalRow(VisualElement panel, GoalRec g)
        {
            var row = Kit.Box(panel, "k-cr-goal");
            if (g.Done) row.AddToClassList("k-cr-goal--done");
            if (g.Failed) row.AddToClassList("k-cr-goal--failed");
            Kit.L(row, g.Done ? "✓" : g.Failed ? "✗" : "○", "k-cr-goal-mark");
            var t = Kit.Box(row, "k-cr-goal-text");
            Kit.L(t, g.Text + (g.Kind == "titles" && !g.Done ? "   " + g.Progress + "/" + g.Target : ""), "k-cr-goal-name");
            Kit.L(t, "+" + g.Points + " training points  ·  " + Money(g.Money), "k-cr-goal-pay");
        }

        /// <summary>A stat tile: a big value over a small name.</summary>
        public static VisualElement Tile(VisualElement parent, string name, VisualElement value)
        {
            var t = Kit.Box(parent, "k-cr-tile");
            value.AddToClassList("k-cr-tile-v");
            t.Add(value);
            Kit.L(t, name, "k-cr-tile-n");
            return t;
        }

        public static VisualElement Tile(VisualElement parent, string name, string value) { return Tile(parent, name, Kit.L(null, value, "k-count")); }

        /// <summary>Form chips: W lit, the rest dim.</summary>
        public static VisualElement FormChips(VisualElement parent, List<string> form)
        {
            var row = Kit.Box(parent, "k-cr-form");
            if (form.Count == 0) Kit.L(row, "no results yet", "k-cr-form-none");
            foreach (var f in form) { var l = Kit.L(row, f, "k-cr-chip"); if (f == "W") l.AddToClassList("k-cr-chip--w"); else if (f == "F") l.AddToClassList("k-cr-chip--f"); }
            return row;
        }

        /// <summary>The court a career event is played on, by its tier: the club court for the small Challengers, then up
        /// through the glass courts - the rooftop, the station, the pyramids, the arena - to the show court for the World
        /// Championship and the Finals. The bigger the event, the bigger the stage.</summary>
        public static string CourtFor(int tier) { return Courts.ForCareerTier(tier); }

        /// <summary>How full the stands are: a few dozen at a Challenger 3, a sell-out at the top.</summary>
        public static float CrowdFill(int tier) { return Courts.CrowdForCareerTier(tier); }

        public static string CourtName(int tier) { var t = Courts.Find(CourtFor(tier)); return t != null ? t.Name : ""; }

        /// <summary>Start your next career match in the match scene.</summary>
        public static void PlayNext(FrontEnd fe)
        {
            var c = C;
            int opp = c.NextOpponent();
            if (opp < 0) return;
            var pro = c.Pro(opp);
            KitDirector.OpponentPreview = null;
            fe.StartMatch(new MatchSettings
            {
                TwoPlayer = false,
                Opponent = pro.Style,
                Difficulty = (float)pro.Strength,
                GamesToWin = c.S.GamesToWin,
                Scoring = c.S.English ? "english" : "par",
                WarmUp = false,
                Career = true,
                P1Name = c.BugOf(You),
                P2Name = pro.Bug,
                KitSeed = KitSeed(c, pro.Id),                              // the pro's own kit, the same every match
                Court = CourtFor(c.S.Current != null ? c.S.Current.Tier : 0),  // the bigger the event, the bigger the stage
                CrowdFill = CrowdFill(c.S.Current != null ? c.S.Current.Tier : 0),
                Attributes = c.S.Attributes != null ? c.S.Attributes.Clone() : null,    // P3-R3: your athlete as you trained it
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

    // ---------------- create a player ----------------

    public sealed class CreatePlayerScreen : GameScreen
    {
        protected override string Kicker { get { return "NEW CAREER"; } }
        protected override string Title { get { return "Create your player"; } }
        protected override bool ShowChip { get { return false; } }
        static readonly string[] StepNames = { "NAME", "NATION", "MATCHES" };
        int _nat, _length = 1, _scoring;
        TextItem _name;
        readonly VisualElement[] _steps = new VisualElement[3];
        readonly Label[] _dots = new Label[3];
        PrimaryItem _next, _start;
        Label _summary;
        public int Step { get; private set; }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "k-cr-create");
            var pic = Kit.Box(stage, "k-vs-pic"); Vignettes.Attach(pic, "you", 512, 640);
            var right = Kit.Box(stage, "k-cr-steps");
            var dots = Kit.Box(right, "k-cr-dots");
            for (int i = 0; i < 3; i++) _dots[i] = Kit.L(dots, (i + 1) + "  " + StepNames[i], "k-cr-dot");

            _steps[0] = Kit.Box(right, "k-cr-step");
            Kit.L(_steps[0], "What's your name?", "k-cr-step-q");
            _name = AddTo(_steps[0], new TextItem("Name", "Player", 16));
            _name.Root.AddToClassList("k-cr-name");
            Kit.L(_steps[0], "It goes on the score bug and in every draw. Enter to type, Enter again when done.", "k-cr-step-hint");

            _steps[1] = Kit.Box(right, "k-cr-step");
            Kit.L(_steps[1], "Who do you play for?", "k-cr-step-q");
            AddTo(_steps[1], new CarouselItem("Nation", Roster.Nations, () => _nat, v => _nat = v));
            Kit.L(_steps[1], "Your flag in the rankings and the draws.", "k-cr-step-hint");

            _steps[2] = Kit.Box(right, "k-cr-step");
            Kit.L(_steps[2], "How long are your matches?", "k-cr-step-q");
            AddTo(_steps[2], new SelectorItem("Match", Career.LengthLabels, () => _length, v => _length = v));
            AddTo(_steps[2], new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => _scoring, v => _scoring = v));
            Kit.L(_steps[2], "No stats to buy: your skill is the only thing that improves. You can change these from the career hub.", "k-cr-step-hint");

            _summary = Kit.L(right, "", "k-cr-summary");
            _next = AddTo(Root, new PrimaryItem("Next", () => { _name.StopTyping(); ShowStep(Step + 1); }));
            _start = AddTo(Root, new PrimaryItem("Start career", Create));
            ShowStep(0);
        }

        void ShowStep(int i)
        {
            Step = Mathf.Clamp(i, 0, 2);
            for (int k = 0; k < 3; k++)
            {
                _steps[k].style.display = k == Step ? DisplayStyle.Flex : DisplayStyle.None;
                _dots[k].EnableInClassList("k-cr-dot--on", k == Step);
                _dots[k].EnableInClassList("k-cr-dot--done", k < Step);
            }
            _next.Root.style.display = Step < 2 ? DisplayStyle.Flex : DisplayStyle.None;
            _start.Root.style.display = Step == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            Root.schedule.Execute(FocusFirstShown);
        }

        protected override void SetPrompts()
        {
            Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back"));
        }

        public override void Tick(float dt)
        {
            _summary.text = NameValue.ToUpperInvariant() + "   ·   " + Roster.Nations[_nat] + "   ·   " + Career.LengthLabels[_length] + "   ·   " + (_scoring == 1 ? "English" : "PAR-11");
        }

        public override void Back() { if (Step > 0) ShowStep(Step - 1); else Fe.Pop(); }

        /// <summary>The name to use (trimmed; "Player" when empty).</summary>
        public string NameValue { get { var n = _name.Value.Trim(); return n.Length == 0 ? "Player" : n; } }

        void Create()
        {
            CareerStore.New(NameValue, Roster.Nations[_nat], _length + 1, _scoring == 1);
            Fe.Pop();                                             // this screen
            Fe.Push(new CareerHubScreen());
        }
    }

    // ---------------- the hub: Central ----------------

    public sealed class CareerHubScreen : GameScreen
    {
        const int You = CareerSession.You;
        protected override string Kicker { get { return "CAREER  ·  " + Career.When(Career.S); } }
        protected override string Title { get { return Career.S.Name; } }

        public CardItem MatchCard { get; private set; }
        readonly List<(CardItem card, Tier tier)> _events = new List<(CardItem, Tier)>();
        CardItem _lastEvent;
        int _rankFilter = 1;
        VisualElement _table;
        bool _placed;

        protected override void Compose()
        {
            var c = Career.C;
            var s = c.S;

            // ---- CENTRAL ----
            var central = Page("CENTRAL");
            var top = Kit.Box(central, "k-cr-top");
            var hero = Kit.Box(top, "k-cr-hero");
            int opp = c.NextOpponent();
            if (opp >= 0)
            {
                var p = c.Pro(opp);
                c.HeadToHead(opp, out int w, out int l);
                Kit.L(hero, "YOUR NEXT MATCH", "k-cr-head");
                var row = Kit.Box(hero, "k-row");
                string round = Tour.RoundName(c.PlayersLeft);
                MatchCard = AddTo(row, new CardItem(round + " v " + p.Last, s.Current.Name.ToUpperInvariant() + "  ·  " + Career.TierName(s.Current.Tier).ToUpperInvariant(),
                        () => Fe.Push(new MatchPreviewScreen(opp)), CardSize.Hero).Vignette("them")
                    .Line(Career.Rank(c.RankOf(opp)) + " " + p.Nat + "  ·  " + Roster.StyleName(p.Style) + "  ·  head to head " + w + "-" + l + "  ·  form " + Career.Form(c, opp))
                    .Badge(() => c.IsRival(opp) ? "RIVAL" : round.ToUpperInvariant(), true));
                MatchCard.Root.AddToClassList("k-card--match");
                Career.TierOn(MatchCard.Art, s.Current.Tier);
                int n = s.Current.Rounds[0].Players.Count;
                AddTo(row, new CardItem("THE DRAW", Career.TierName(s.Current.Tier).ToUpperInvariant(), () => Fe.Push(new DrawScreen(s.Current)))
                    .Drawn("bracket", Career.TierBg(s.Current.Tier), Career.TierColor(s.Current.Tier))
                    .Line(n + " players  ·  you're in the " + round.ToLowerInvariant())).Root.AddToClassList("k-card--event");
            }
            else
            {
                Kit.L(hero, Tour.Month(s.Month).ToUpperInvariant() + "  ·  CHOOSE YOUR EVENT", "k-cr-head");
                var row = Kit.Box(hero, "k-row");
                foreach (var t in c.EventsForYou())
                {
                    var tier = t;
                    var info = Tour.Info(t);
                    var card = AddTo(row, new CardItem(Tour.EventName(s.Month, Tour.IndexOf(s.Month, t)), info.Name.ToUpperInvariant(), () => Enter(tier))
                        .Drawn("trophy", Career.TierBg((int)t), Career.TierColor((int)t))
                        .Line("Winner +" + info.Points[0].ToString("N0") + " pts  ·  " + Career.Money(info.Prize[0]) + "\n" + info.Draw + "-player draw  ·  " + Career.CourtName((int)t))
                        .Badge(() => Career.Stars((int)tier)));
                    card.Root.AddToClassList("k-card--event");
                    card.Locked = () => !c.CanEnter(tier);
                    card.LockLine = () => c.Entry(tier);
                    Career.TierOn(card.Art, (int)t);
                    _events.Add((card, t));
                }
                // P3-R3: or a month on the practice court
                var train = AddTo(row, new CardItem("Training block", "TRAIN THIS MONTH", Train)
                    .Drawn("stripes", new Color32(18, 30, 24, 255), new Color(0.78f, 1f, 0.24f, 0.08f))
                    .Line("+" + CareerSession.TrainingMonthPoints + " training points\nThe month's events go on without you")
                    .Badge(() => c.S.TrainingPoints > 0 ? c.S.TrainingPoints + " TO SPEND" : "+" + CareerSession.TrainingMonthPoints, true));
                train.Root.AddToClassList("k-card--event");
            }

            // the ranking widget
            var rw = Kit.Panel(top, "WORLD RANKING"); rw.AddToClassList("k-cr-rankw");
            int rank = c.RankOf(You), mv = c.Movement(You);
            var big = Kit.Box(rw, "k-cr-rank-row");
            Kit.L(big, rank == CareerSession.Unranked ? "NR" : "#" + rank, "k-cr-rank-big");
            var mvl = Kit.L(big, mv > 0 ? "▲" + mv : mv < 0 ? "▼" + (-mv) : "–", "k-cr-rank-move");
            mvl.AddToClassList(mv > 0 ? "k-up" : mv < 0 ? "k-down" : "k-same");
            Kit.L(rw, rank == CareerSession.Unranked ? "UNRANKED  ·  " + s.Nat : Career.Pts(c.AverageOf(You)) + " POINTS  ·  " + c.EventsOf(You) + (c.EventsOf(You) == 1 ? " EVENT" : " EVENTS") + "  ·  " + s.Nat, "k-cr-rank-sub");
            rw.Add(new RankTable(Career.Rows(c, Career.Around(c, You, 2))));
            Kit.L(rw, Career.ToNext(c), "k-cr-note");

            var bottom = Kit.Box(central, "k-cr-bottom");
            var news = Kit.Panel(bottom, "NEWS"); news.AddToClassList("k-cr-news");
            var nrow = Kit.Box(news, "k-cr-news-row");
            foreach (var (k, text) in Career.News(c))
            {
                var item = Kit.Box(nrow, "k-cr-news-item");
                Kit.L(item, k, "k-cr-news-k");
                Kit.L(item, text, "k-cr-news-t");
            }
            var season = Kit.Panel(bottom, "SEASON " + s.Season); season.AddToClassList("k-cr-season");
            season.Add(new Timeline(Career.Months(c, s.Season)));

            // ---- CALENDAR ----
            var cal = Kit.Box(Page("CALENDAR"), "k-cr-cal");
            for (int m = 0; m < Tour.SlotsPerSeason; m++)
            {
                var cell = Kit.Box(cal, "k-cr-month");
                if (m == s.Month) cell.AddToClassList("k-cr-month--now"); else if (m < s.Month) cell.AddToClassList("k-cr-month--past");
                var head = Kit.Box(cell, "k-cr-month-head");
                Kit.L(head, Tour.Month(m).ToUpperInvariant(), "k-cr-month-name");
                if (m == s.Month) Kit.L(head, "NOW", "k-cr-now");
                var month = Tour.EventsIn(m);
                int challengers = 0;
                for (int i = 0; i < month.Length; i++)
                {
                    if (!Tour.Info(month[i]).WorldTour) { challengers++; continue; }
                    var ev = Kit.Box(cell, "k-cr-ev");
                    ev.Add(Career.Badge((int)month[i], 14));
                    Kit.L(ev, Tour.EventName(m, i), "k-cr-ev-name");
                }
                if (challengers > 0)
                {
                    var ev = Kit.Box(cell, "k-cr-ev");
                    ev.Add(Career.Badge((int)Tier.Challenger3, 14));
                    Kit.L(ev, "+ " + challengers + " Challengers", "k-cr-ev-name");
                }
                int slot = s.Season * Tour.SlotsPerSeason + m;
                string mine = null; bool title = false;
                foreach (var r in s.Results)
                    if (r.Player == You && r.Slot == slot) { mine = CareerSession.ReachedShort(r.Reached) + "  ·  " + Career.TierName(r.Tier) + "  +" + r.Points; title = r.Reached == 0; }
                if (m == s.Month && s.Current != null) mine = "PLAYING  ·  " + s.Current.Name;
                if (mine != null) { var lab = Kit.L(cell, mine, "k-cr-mine"); if (title) lab.AddToClassList("k-cr-mine--won"); }
            }

            // ---- RANKINGS ----
            var rgrid = Kit.Box(Page("RANKINGS"), "k-cr-rgrid");
            var tp = Kit.Panel(rgrid, "WORLD RANKING  ·  " + Career.When(s)); tp.AddToClassList("k-cr-tablep");
            AddTo(tp, new SelectorItem("Show", new[] { "Top 15", "Around me", "Rivals" }, () => _rankFilter, v => { _rankFilter = v; FillTable(); }));
            _table = Kit.Box(tp, "k-cr-tablebox");
            FillTable();
            var ladder = Kit.Panel(rgrid, "THE TIERS"); ladder.AddToClassList("k-cr-ladder");
            for (int t = 0; t < Tour.Tiers.Length; t++)
            {
                var info = Tour.Tiers[t];
                bool open = info.UnlockRank == 0 || rank <= info.UnlockRank;
                var lr = Kit.Box(ladder, "k-cr-lad");
                if (open) lr.AddToClassList("k-cr-lad--open");
                lr.Add(Career.Badge(t, 26));
                var txt = Kit.Box(lr, "k-cr-lad-text");
                Kit.L(txt, info.Name.ToUpperInvariant(), "k-cr-lad-name");
                Kit.L(txt, info.Draw + " players  ·  winner +" + info.Points[0].ToString("N0") + " pts  ·  " + Career.Money(info.Prize[0]) + "  ·  " + Career.CourtName(t), "k-cr-lad-line");
                Kit.L(lr, open ? "OPEN" : (t == (int)Tier.WorldTourFinals ? "TOP 8" : "#" + info.UnlockRank), "k-cr-lad-gate");
            }

            // ---- PLAYER ----
            var pg = Kit.Box(Page("PLAYER"), "k-cr-pgrid");
            var card2 = Kit.Panel(pg, "PLAYER CARD"); card2.AddToClassList("k-cr-pcard");
            var ppic = Kit.Box(card2, "k-cr-ppic"); Vignettes.Attach(ppic, "you", 400, 400);
            Kit.L(card2, s.Name, "k-cr-pname");
            Kit.L(card2, s.Nat + "  ·  SEASON " + s.Season + "  ·  " + Career.Rank(rank).ToUpperInvariant(), "k-cr-pmeta");
            var mid = Kit.Box(pg, "k-cr-pmid");
            var cs = Kit.Panel(mid, "CAREER");
            var tiles = Kit.Box(cs, "k-cr-tiles");
            int won = 0, lost = 0;
            foreach (var mrec in s.Matches) if (mrec.Won) won++; else lost++;
            Career.Tile(tiles, "MATCHES", won + "-" + lost);
            Career.Tile(tiles, "WIN %", won + lost == 0 ? "–" : (100 * won / (won + lost)) + "%");
            Career.Tile(tiles, "TITLES", s.Titles.ToString());
            Career.Tile(tiles, "EARNINGS", Career.Money(s.Earnings));
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
            Kit.L(cs, "Best season finish " + (s.Seasons.Count == 0 ? "– (first season)" : Career.Rank(best)) + "   ·   rating " + s.Rating.ToString("0.00"), "k-cr-note");
            // P3-R3: your attributes, and the points to spend on them
            var at = Kit.Panel(mid, "ATTRIBUTES  ·  " + s.TrainingPoints + (s.TrainingPoints == 1 ? " POINT" : " POINTS") + " TO SPEND"); at.AddToClassList("k-cr-attr");
            _attrTitle = at.Q<Label>(className: "k-panel-title");
            foreach (var name in CareerSession.AttributeNames)
            {
                var n = name;
                var r0 = Kit.Box(at, "k-cr-attr-row");
                Kit.L(r0, n.ToUpperInvariant(), "k-cr-attr-name");
                var bar = Kit.Box(r0, "k-cr-attr-bar");
                var fill = Kit.Box(bar, "k-cr-attr-fill");
                var val = Kit.L(r0, "", "k-cr-attr-val");
                var plus = AddTo(r0, new ActionItem("+", () => { if (c.Spend(n)) { CareerStore.Save(); RefreshAttrs(); } }));
                plus.Root.AddToClassList("k-cr-attr-plus");
                plus.Enabled = () => c.S.TrainingPoints > 0 && CareerSession.Get(c.S.Attributes, n) < SquashSim.AthleteAttributes.Max;
                Kit.L(at, AttrLine(n), "k-cr-attr-line");
                _attrs.Add((n, fill, val));
            }
            RefreshAttrs();
            Kit.L(at, "Points come from wins (+1), titles (+1 to +3), each season (+2) and training blocks (+" + CareerSession.TrainingMonthPoints + ").", "k-cr-note");
            var rv = Kit.Panel(mid, "RIVALS");
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
            var hist = Kit.Panel(pg, "MATCH HISTORY"); hist.AddToClassList("k-cr-hist");
            if (s.Matches.Count == 0) Kit.L(hist, "No matches yet. Your first event is waiting.", "k-cr-empty");
            for (int i = s.Matches.Count - 1, k = 0; i >= 0 && k < 9; i--, k++)
            {
                var mrec = s.Matches[i];
                var hr = Kit.Box(hist, "k-cr-hrow");
                var wl = Kit.L(hr, mrec.Won ? "W" : "L", "k-cr-chip"); if (mrec.Won) wl.AddToClassList("k-cr-chip--w");
                var mid2 = Kit.Box(hr, "k-cr-hmid");
                Kit.L(mid2, "v " + c.NameOf(mrec.Opp) + "  (" + Career.Rank(mrec.OppRank) + ")", "k-cr-hname");
                Kit.L(mid2, mrec.Event + "  ·  " + Tour.RoundName(mrec.Left) + (mrec.Simulated ? "  ·  simulated" : ""), "k-cr-hline");
                Kit.L(hr, mrec.Score ?? "", "k-cr-hscore");
            }

            // ---- GOALS (P3-R4): the season's goals and your sponsor ----
            var gg = Kit.Box(Page("GOALS"), "k-cr-ggrid");
            var gp = Kit.Panel(gg, "SEASON " + s.Season + " GOALS  ·  " + c.GoalsDoneNow + " OF " + c.Goals.Count + " DONE"); gp.AddToClassList("k-cr-goals");
            foreach (var g in c.Goals) Career.GoalRow(gp, g);
            Kit.L(gp, "Goals are set from your ranking at the start of each season. Each one pays training points and prize money.", "k-cr-note");
            var sp = Kit.Panel(gg, "SPONSOR"); sp.AddToClassList("k-cr-sponsor");
            if (s.Sponsor != null)
            {
                Kit.L(sp, s.Sponsor.Brand, "k-cr-sp-brand");
                Kit.L(sp, Career.Money(s.Sponsor.Retainer) + " a season  ·  " + Career.Money(s.Sponsor.TitleBonus) + " a title", "k-cr-sp-line");
                bool keeping = rank <= s.Sponsor.Clause;
                var cl = Kit.L(sp, "CLAUSE  stay inside the top " + s.Sponsor.Clause + "  ·  now " + Career.Rank(rank) + (keeping ? "  ✓" : "  ✗"), "k-cr-sp-clause");
                cl.AddToClassList(keeping ? "k-up" : "k-down");
                Kit.L(sp, "Paid so far " + Career.Money(s.SponsorPaid), "k-cr-note");
            }
            else if (c.OffersWaiting)
            {
                Kit.L(sp, s.Offers.Count + " offers on the table", "k-cr-sp-brand");
                AddTo(sp, new ActionItem("See the offers", () => Fe.Push(new SponsorScreen())));
            }
            else Kit.L(sp, rank == CareerSession.Unranked ? "Get a world ranking and the sponsors will call. Offers come at the start of each season."
                                                             : "No sponsor this season. Offers come at the start of each season.", "k-cr-empty");

            // the primary, then the settings chip (the primary is the focus on pages without choices)
            AddTo(Root, new PrimaryItem("Continue", Continue));
            var gear = new ActionItem("Match settings", () => Fe.Push(new CareerSettingsScreen()), PromptBar.Btn.Y);
            gear.Root.AddToClassList("k-action--chip");
            AddTo(TopBar, gear);
        }

        readonly List<(string name, VisualElement fill, Label val)> _attrs = new List<(string, VisualElement, Label)>();
        Label _attrTitle;

        static string AttrLine(string n)
        {
            switch (n)
            {
                case "Speed": return "Top speed and acceleration: up to +6% at 10";
                case "Stamina": return "Tires slower - longer sprints, more dives: drain down to x0.7";
                case "Power": return "Harder drives and kills: up to +6% pace";
                default: return "Tighter shots at a stretch, fewer miss-hits: down to x0.6 spread";
            }
        }

        void RefreshAttrs()
        {
            var s = Career.S;
            foreach (var (n, fill, val) in _attrs)
            {
                double v = CareerSession.Get(s.Attributes, n);
                fill.style.width = Length.Percent((float)(100 * v / SquashSim.AthleteAttributes.Max));
                val.text = v.ToString("0");
            }
            if (_attrTitle != null) _attrTitle.text = "ATTRIBUTES  ·  " + s.TrainingPoints + (s.TrainingPoints == 1 ? " POINT" : " POINTS") + " TO SPEND";
        }

        void FillTable()
        {
            if (_table == null) return;
            _table.Clear();
            var c = Career.C;
            var rows = new List<CareerSession.Row>();
            if (_rankFilter == 0) rows = c.Ranking().GetRange(0, Mathf.Min(15, c.Ranking().Count));
            else if (_rankFilter == 1 && !c.IsRanked(You)) { Kit.L(_table, "You're not on the world ranking yet: your first event puts you on it.", "k-cr-empty"); return; }
            else if (_rankFilter == 1) rows = Career.Around(c, You, 7);
            else foreach (var id in c.Rivals()) foreach (var r in c.Ranking()) if (r.Id == id) rows.Add(r);
            if (rows.Count == 0) { Kit.L(_table, "No rivals yet. A pro who beats you twice becomes one.", "k-cr-empty"); return; }
            _table.Add(new RankTable(Career.Rows(c, rows), c.Ranking()[0].Points));
        }

        protected override void SetPrompts()
        {
            bool between = Career.C.S.Current == null;
            if (between) Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Tabs"), (PromptBar.Btn.X, "Skip month"), (PromptBar.Btn.Y, "Match settings"));
            else Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back"), (PromptBar.Btn.Tabs, "Tabs"), (PromptBar.Btn.Y, "Match settings"));
        }

        public override void OnActionX() { if (Career.C.S.Current == null) Fe.Push(new ConfirmScreen("Skip " + Tour.Month(Career.S.Month) + "? Its events go on without you.", Skip)); }
        public override void OnActionY() { Fe.Push(new CareerSettingsScreen()); }

        /// <summary>CONTINUE: always the next thing - your match preview, or into the chosen (else the best open) event.</summary>
        void Continue()
        {
            var c = Career.C;
            int opp = c.NextOpponent();
            if (opp >= 0) { Fe.Push(new MatchPreviewScreen(opp)); return; }
            foreach (var (card, t) in _events) if (card == _lastEvent && c.CanEnter(t)) { Enter(t); return; }
            foreach (var (card, t) in _events) if (c.CanEnter(t)) { Enter(t); return; }
            Skip();
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

        static int _offersShown = -1;          // the season whose offers came up (once a season, by themselves)

        void Train()
        {
            Career.C.Train();
            CareerStore.Save();
            Fe.Replace(new CareerHubScreen());
        }

        public override void OnShow()
        {
            base.OnShow();
            var c = Career.C;
            if (c == null) return;
            int opp = c.NextOpponent();
            KitDirector.OpponentPreview = opp >= 0 ? Cosmetics.AiKit(Career.KitSeed(c, opp)) : null;
            MenuCinematics.Scene = 2;
            if (_placed) return;
            _placed = true;
            // the first focus: your match, or the best event open to you
            // after the first tab's own focus (ShowPage runs on the next update)
            Root.schedule.Execute(() =>
            {
                if (MatchCard != null) { Focus(MatchCard); return; }
                foreach (var (card, t) in _events) if (c.CanEnter(t)) { Focus(card); return; }
            });
        }

        public override void OnHide() { KitDirector.OpponentPreview = null; }

        /// <summary>The month's end, results and the season summary, as they fall due.</summary>
        public override void Tick(float dt)
        {
            var c = Career.C;
            if (c == null) { Fe.Pop(); return; }                     // the career is gone (deleted)
            if (Focused is CardItem fc) foreach (var (card, _) in _events) if (card == fc) _lastEvent = fc;
            if (c.S.Current != null && c.CanFinishSlot)
            {
                c.FinishSlot();
                CareerStore.Save();
                var last = c.S.Last;
                Fe.Replace(new CareerHubScreen());
                if (last != null) Fe.Push(new EventResultScreen(last));
                return;
            }
            if (c.S.SeasonJustEnded) { Fe.Push(new SeasonSummaryScreen()); return; }
            if (c.OffersWaiting && c.S.OffersSeason == c.S.Season && _offersShown != c.S.Season) { _offersShown = c.S.Season; Fe.Push(new SponsorScreen()); }
        }

        public override void Back() { Fe.Pop(); }
    }

    // ---------------- before a match ----------------

    public sealed class MatchPreviewScreen : GameScreen
    {
        const int You = CareerSession.You;
        readonly int _opp;
        public MatchPreviewScreen(int opp) { _opp = opp; }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { var c = Career.C; return (c.S.Current.Name + "  ·  " + Tour.RoundName(c.PlayersLeft)).ToUpperInvariant(); } }
        protected override string Title { get { return "Match preview"; } }
        bool Finals { get { return Career.C.S.Current.Tier == (int)Tier.WorldTourFinals; } }

        protected override void Compose()
        {
            var c = Career.C;
            var s = c.S;
            var p = c.Pro(_opp);
            AddTo(Root, new PrimaryItem("Play", () => Career.PlayNext(Fe)));

            var stage = Kit.Box(Content, "k-versus");
            stage.AddToClassList("k-cr-preview");
            var you = Kit.Box(stage, "k-vs-side");
            Vignettes.Attach(Kit.Box(you, "k-vs-pic"), "you", 512, 640);
            Kit.L(you, "YOU", "k-vs-kicker");
            Kit.L(you, s.Name, "k-vs-name");
            Kit.L(you, Career.Rank(c.RankOf(You)).ToUpperInvariant() + "  ·  " + s.Nat, "k-vs-meta");

            var mid = Kit.Box(stage, "k-vs-mid");
            var ev = Kit.Box(mid, "k-cr-pv-event");
            ev.Add(Career.Badge(s.Current.Tier, 28));
            Kit.L(ev, Career.TierName(s.Current.Tier).ToUpperInvariant() + "  ·  " + Tour.RoundName(c.PlayersLeft).ToUpperInvariant(), "k-cr-pv-round");
            var tape = Kit.Panel(mid, "TALE OF THE TAPE");
            int ra = c.RankOf(You), rb = c.RankOf(_opp);
            Row(tape, "WORLD RANK", Career.Rank(ra).ToUpperInvariant(), Career.Rank(rb).ToUpperInvariant(), ra < rb ? 0 : rb < ra ? 1 : -1);
            double pa = c.AverageOf(You), pb = c.AverageOf(_opp);
            Row(tape, "POINTS", Career.Pts(pa), Career.Pts(pb), pa > pb ? 0 : pb > pa ? 1 : -1);
            int ta = Career.Titles(c, You), tb = Career.Titles(c, _opp);
            Row(tape, "TITLES (2 YRS)", ta.ToString(), tb.ToString(), ta > tb ? 0 : tb > ta ? 1 : -1);
            Row(tape, "NATION", s.Nat, p.Nat, -1);
            Row(tape, "AGE", "–", p.Age.ToString(), -1);
            var form = Kit.Box(tape, "k-cr-tape-row");
            Career.FormChips(Kit.Box(form, "k-cr-tape-a"), c.Form(You));
            Kit.L(form, "FORM", "k-cr-tape-n");
            Career.FormChips(Kit.Box(form, "k-cr-tape-b"), c.Form(_opp));
            c.HeadToHead(_opp, out int w, out int l);
            Row(tape, "HEAD TO HEAD", w.ToString(), l.ToString(), w > l ? 0 : l > w ? 1 : -1);
            var acts = Kit.Box(mid, "k-cr-pv-acts");
            var sim = AddTo(acts, new ActionItem("Simulate", Simulate, PromptBar.Btn.X));
            sim.Enabled = () => !Finals;
            AddTo(acts, new ActionItem("The draw", () => Fe.Push(new DrawScreen(s.Current)), PromptBar.Btn.Y));
            if (Finals) Kit.L(mid, "The World Tour Finals can't be simulated: play them.", "k-cr-note");
            Kit.L(mid, "On the " + Career.CourtName(s.Current.Tier), "k-cr-note");

            var them = Kit.Box(stage, "k-vs-side");
            them.AddToClassList("k-vs-side--right");
            Vignettes.Attach(Kit.Box(them, "k-vs-pic"), "them", 512, 640);
            var kick = Kit.L(them, c.IsRival(_opp) ? "★ RIVAL" : "OPPONENT", "k-vs-kicker");
            if (c.IsRival(_opp)) kick.AddToClassList("k-cr-rivalkick");
            Kit.L(them, p.Name, "k-vs-name");
            Kit.L(them, Career.Rank(rb).ToUpperInvariant() + "  ·  " + p.Nat + "  ·  " + Roster.StyleName(p.Style).ToUpperInvariant(), "k-vs-meta");
            foreach (var line in Roster.StyleLines(p.Style)) Kit.L(them, line, "k-cr-callout");
        }

        static void Row(VisualElement tape, string name, string a, string b, int better)
        {
            var row = Kit.Box(tape, "k-cr-tape-row");
            var la = Kit.L(Kit.Box(row, "k-cr-tape-a"), a, "k-cr-tape-v");
            Kit.L(row, name, "k-cr-tape-n");
            var lb = Kit.L(Kit.Box(row, "k-cr-tape-b"), b, "k-cr-tape-v");
            if (better == 0) la.AddToClassList("k-cr-tape-v--win");
            if (better == 1) lb.AddToClassList("k-cr-tape-v--win");
        }

        protected override void SetPrompts()
        {
            if (Finals) Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.Y, "The draw"), (PromptBar.Btn.B, "Back"));
            else Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.X, "Simulate"), (PromptBar.Btn.Y, "The draw"), (PromptBar.Btn.B, "Back"));
        }

        public override void OnActionX() { if (!Finals) Simulate(); }
        public override void OnActionY() { Fe.Push(new DrawScreen(Career.S.Current)); }

        public override void OnShow()
        {
            base.OnShow();
            KitDirector.OpponentPreview = Cosmetics.AiKit(Career.KitSeed(Career.C, _opp));
            MenuCinematics.Scene = 0;
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

    public sealed class SimResultScreen : GameScreen
    {
        readonly int _opp; readonly bool _won; readonly string _score;
        public SimResultScreen(int opp, bool won, string score) { _opp = opp; _won = won; _score = score; }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "SIMULATED"; } }
        protected override string Title { get { return "Match result"; } }
        protected override void Compose()
        {
            AddTo(Root, new PrimaryItem("Continue", () => Fe.Pop()));
            var col = Kit.Box(Content, "k-cr-result");
            col.AddToClassList("k-cr-result--card");
            var st = new Stamp(_won ? "WON" : "LOST", 0.2f);
            if (!_won) st.AddToClassList("k-stamp--plain");
            col.Add(st);
            Kit.L(col, "v " + Career.C.NameOf(_opp), "k-cr-res-v");
            var chips = Kit.Box(col, "k-ft-chips");
            foreach (var g in (_score ?? "").Split(' '))
            {
                if (g.Length == 0) continue;
                var ch = Kit.L(chips, g, "k-ft-chip");
                var p = g.Split('-');
                if (p.Length == 2 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b) && a > b) ch.AddToClassList("k-ft-chip--won");
            }
        }
    }

    // ---------------- the draw ----------------

    public sealed class DrawScreen : GameScreen
    {
        const int You = CareerSession.You;
        readonly EventState _e;
        public DrawScreen(EventState e) { _e = e; }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return Career.TierName(_e.Tier).ToUpperInvariant() + "  ·  THE DRAW"; } }
        protected override string Title { get { return _e.Name; } }
        public BracketView Bracket { get; private set; }
        ScrollView _scroll;
        VisualElement _detail;
        int _n;
        readonly Dictionary<BracketMatchItem, (int a, int b, int winner, string score)> _info = new Dictionary<BracketMatchItem, (int, int, int, string)>();

        protected override void Compose()
        {
            var c = Career.C;
            var wrap = Kit.Box(Content, "k-cr-draw");
            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scroll.AddToClassList("k-cr-draw-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;          // the focus scrolls it (and the wheel)
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            wrap.Add(_scroll);
            Bracket = new BracketView();
            _scroll.Add(Bracket);
            _n = _e.Rounds[0].Players.Count;
            int rounds = 0; for (int k = _n; k > 1; k >>= 1) rounds++;
            var seeds = Career.Seeds(c, _e);
            for (int r = 0; r < rounds; r++)
            {
                Bracket.Column(r, Tour.RoundName(_n >> r).ToUpperInvariant());
                for (int i = 0; i < _n >> (r + 1); i++)
                {
                    int a = -1, b = -1, w = -1; string score = null;
                    if (r < _e.Rounds.Count)
                    {
                        var rr = _e.Rounds[r];
                        a = rr.Players[2 * i]; b = rr.Players[2 * i + 1];
                        if (i < rr.Winners.Count) w = rr.Winners[i] == a ? 0 : 1;
                        if (i < rr.Scores.Count) score = rr.Scores[i];
                    }
                    var m = new BracketMatchItem(r, i, Entry(c, a, seeds), Entry(c, b, seeds), w, score, a == You, b == You, null);
                    AddTo(Bracket, m);
                    Bracket.Place(m, _n / 2);
                    _info[m] = (a, b, w, score);
                }
            }
            float colW = BracketView.MatchW + BracketView.ColGap;
            int champ = CareerSession.Winner(_e);
            var cb = Kit.Box(Bracket, "k-cr-champ");                                  // under the final
            cb.style.left = (rounds - 1) * colW;
            cb.style.top = BracketView.HeaderH + (_n / 2) * (BracketView.MatchH + 14) * 0.5f + BracketView.MatchH * 0.5f + 22;
            Kit.L(cb, "CHAMPION", "k-cr-champ-k");
            var cn = Kit.L(cb, champ >= 0 ? Career.Short(c, champ) : "?", "k-cr-champ-n");
            if (champ == You) cn.AddToClassList("k-cr-champ-n--you");
            Bracket.style.width = rounds * colW;

            _detail = Kit.Panel(wrap, "MATCH");
            _detail.AddToClassList("k-cr-draw-detail");
        }

        static string Entry(CareerSession c, int id, Dictionary<int, int> seeds)
        {
            if (id < 0) return null;
            return (seeds.TryGetValue(id, out int s) ? "[" + s + "] " : "") + Career.Short(c, id);
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Browse"), (PromptBar.Btn.B, "Back")); }

        public override void OnShow()
        {
            base.OnShow();
            // start on your latest match (Tick scrolls to it once it is laid out)
            BracketMatchItem pick = null;
            foreach (var kv in _info) if (kv.Key.YouIn && (pick == null || kv.Key.Round > pick.Round)) pick = kv.Key;
            if (pick != null && !_placed) { _placed = true; Focus(pick); }
        }

        MenuItem _shown;
        bool _placed;
        public override void Tick(float dt)
        {
            if (Focused == _shown || !(Focused is BracketMatchItem m)) return;
            _shown = Focused;
            _scroll.ScrollTo(m.Root);
            ShowDetail(m);
        }

        void ShowDetail(BracketMatchItem m)
        {
            var c = Career.C;
            var (a, b, w, score) = _info[m];
            _detail.Clear();
            Kit.L(_detail, Tour.RoundName(_n >> m.Round).ToUpperInvariant(), "k-panel-title");
            if (a < 0)
            {
                Kit.L(_detail, "To be decided", "k-cr-dt-name");
                Kit.L(_detail, "The winners of the two matches before this one meet here.", "k-cr-dt-meta");
                return;
            }
            Player(c, a, w == 0);
            Kit.L(_detail, "v", "k-cr-dt-v");
            Player(c, b, w == 1);
            if (!string.IsNullOrEmpty(score))
            {
                var chips = Kit.Box(_detail, "k-cr-dt-chips");
                foreach (var g in score.Split(' '))
                {
                    var ch = Kit.L(chips, g, "k-ft-chip");
                    var p = g.Split('-');
                    if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) && x > y) ch.AddToClassList("k-ft-chip--won");
                }
            }
            else Kit.L(_detail, "Not played yet", "k-cr-dt-meta");
            int other = a == You ? b : b == You ? a : -1;
            if (other >= 0)
            {
                c.HeadToHead(other, out int hw, out int hl);
                Kit.L(_detail, "Your head to head: " + hw + "-" + hl + (c.IsRival(other) ? "  ·  ★ rival" : ""), "k-cr-dt-h2h");
            }
        }

        void Player(CareerSession c, int id, bool won)
        {
            var row = Kit.Box(_detail, "k-cr-dt-player");
            if (won) row.AddToClassList("k-cr-dt-player--won");
            if (id == You) row.AddToClassList("k-cr-dt-player--you");
            Kit.L(row, c.NameOf(id), "k-cr-dt-name");
            Kit.L(row, Career.Rank(c.RankOf(id)).ToUpperInvariant() + "  ·  " + c.NatOf(id) + "  ·  " + Career.StyleOf(c, id).ToUpperInvariant(), "k-cr-dt-meta");
        }
    }

    // ---------------- after an event / a season ----------------

    public sealed class EventResultScreen : GameScreen
    {
        const int You = CareerSession.You;
        readonly EventState _e;
        public EventResultScreen(EventState e) { _e = e; Reward = Rewards.CareerEvent(Career.C, e); }
        public RewardResult Reward { get; private set; }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return Career.TierName(_e.Tier).ToUpperInvariant() + "  ·  RESULT"; } }
        protected override string Title { get { return _e.Name; } }

        protected override void Compose()
        {
            var c = Career.C;
            AddTo(Root, new PrimaryItem("Continue", () => Fe.Pop()));
            var col = Kit.Box(Content, "k-cr-result");
            var head = Kit.Box(col, "k-cr-res-head");
            head.Add(Career.Badge(_e.Tier, 56));
            var st = new Stamp(_e.YouReached == 0 ? "CHAMPION" : CareerSession.ReachedLong(_e.YouReached).ToUpperInvariant(), 0.3f);
            if (_e.YouReached != 0) st.AddToClassList("k-stamp--plain");
            head.Add(st);
            int champ = CareerSession.Winner(_e);
            if (champ >= 0 && champ != You) Kit.L(col, "Won by " + c.NameOf(champ), "k-cr-res-v");

            var tiles = Kit.Box(col, "k-cr-tiles");
            Career.Tile(tiles, "RANKING POINTS", new CountUp(_e.YouPoints, 1.0, "N0", "+"));
            Career.Tile(tiles, "PRIZE MONEY", new CountUp(_e.YouPrize, 1.0, "N0", "$"));
            int rank = c.RankOf(You), mv = c.Movement(You);
            var rt = Career.Tile(tiles, "WORLD RANKING", Career.Rank(rank));
            if (mv != 0)
            {
                var ml = Kit.L(rt, (mv > 0 ? "▲ " + mv + " from " : "▼ " + (-mv) + " from ") + Career.Rank(rank + mv), "k-cr-tile-move");
                ml.AddToClassList(mv > 0 ? "k-up" : "k-down");
            }
            else if (rank != CareerSession.Unranked && c.EventsOf(You) == 1) Kit.L(rt, "NEW ENTRY", "k-cr-tile-move").AddToClassList("k-up");
            col.Add(new RewardStrip(Reward));
            Fe.ToastAchievements(Reward);
            var acts = Kit.Box(col, "k-cr-pv-acts");
            AddTo(acts, new ActionItem("View the draw", () => Fe.Push(new DrawScreen(_e)), PromptBar.Btn.Y));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.Y, "The draw"), (PromptBar.Btn.B, "Back")); }
        public override void OnActionY() { Fe.Push(new DrawScreen(_e)); }
    }

    public sealed class SeasonSummaryScreen : GameScreen
    {
        SeasonRec R { get { var l = Career.S.Seasons; return l[l.Count - 1]; } }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "SEASON " + R.Season + " REVIEW"; } }
        protected override string Title { get { return "Season complete"; } }

        protected override void Compose()
        {
            var r = R;
            AddTo(Root, new PrimaryItem("On to season " + Career.S.Season, Done));
            var col = Kit.Box(Content, "k-cr-result");
            var head = Kit.Box(col, "k-cr-res-head");
            Kit.L(head, "FINAL WORLD RANKING", "k-cr-res-k");
            head.Add(new Stamp(Career.Rank(r.Rank).ToUpperInvariant(), 0.3f));
            var tiles = Kit.Box(col, "k-cr-tiles");
            Career.Tile(tiles, "MATCHES", r.Won + "-" + (r.Played - r.Won));
            Career.Tile(tiles, "WIN %", new CountUp(r.Played == 0 ? 0 : 100.0 * r.Won / r.Played, 1.0, "0", "", "%"));
            Career.Tile(tiles, "TITLES", new CountUp(r.Titles, 0.8));
            Career.Tile(tiles, "EARNINGS", new CountUp(r.Earnings, 1.2, "N0", "$"));
            Kit.L(col, "BEST WIN   " + (string.IsNullOrEmpty(r.BestWin) ? "–" : r.BestWin), "k-cr-res-v");
            var tl = Kit.Panel(col, "THE YEAR"); tl.AddToClassList("k-cr-res-year");
            tl.Add(new Timeline(Career.Months(Career.C, r.Season)));
            var lg = Career.S.LastGoals;
            if (lg != null && lg.Count > 0)
            {
                int done = lg.FindAll(g => g.Done).Count;
                var gpn = Kit.Panel(col, "SEASON GOALS  ·  " + done + " OF " + lg.Count); gpn.AddToClassList("k-cr-res-goals");
                foreach (var g in lg) Career.GoalRow(gpn, g);
            }
            Kit.L(col, "A new season: some veterans retire, newcomers arrive. Your ranking carries on - and new goals are set from it.", "k-cr-note");
        }

        void Done() { Career.S.SeasonJustEnded = false; CareerStore.Save(); Fe.Pop(); }
        public override void Back() { Done(); }
    }

    // ---------------- P3-R4: sponsors ----------------

    public sealed class SponsorScreen : GameScreen
    {
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "SEASON " + Career.S.Season + "  ·  SPONSORS"; } }
        protected override string Title { get { return "Two offers on the table"; } }

        protected override void Compose()
        {
            var c = Career.C;
            var s = c.S;
            int rank = c.RankOf(CareerSession.You);
            var row = Kit.Box(Content, "k-row");
            for (int i = 0; i < s.Offers.Count; i++)
            {
                int k = i;
                var d = s.Offers[i];
                var item = Cosmetics.Find(d.ItemId);
                var card = AddTo(row, new CardItem(d.Brand, i == 0 ? "THE SAFE DEAL" : "THE BIG DEAL", () => Sign(k), CardSize.Hero)
                    .Drawn("stripes", item != null ? item.A * 0.35f + new Color(0, 0, 0, 0.65f) : new Color32(20, 20, 26, 255), item != null ? item.B : Color.white)
                    .Line(Career.Money(d.Retainer) + " a season (paid at its end)  ·  " + Career.Money(d.TitleBonus) + " a title\n"
                          + "Clause: stay inside the top " + d.Clause + " (you are " + Career.Rank(rank) + ")\n"
                          + "Kit: " + (item != null ? item.Name + " " + Cosmetics.SlotNames[(int)item.Slot].ToLowerInvariant() : "-"))
                    .Badge(() => Career.Money(d.Retainer), true));
                card.Root.AddToClassList("k-card--sponsor");
            }
            AddTo(Root, new PrimaryItem("No thanks", () => Sign(-1)));
            Kit.L(Content, "Miss the clause at the season's end and the deal is off - with no retainer. Titles pay the bonus straight away.", "k-cr-note");
        }

        void Sign(int i)
        {
            var c = Career.C;
            var d = c.Sign(i);
            if (d != null) Rewards.GrantNow(d.ItemId);
            CareerStore.Save();
            Fe.Pop();
            Fe.Replace(new CareerHubScreen());
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Browse"), (PromptBar.Btn.A, "Sign"), (PromptBar.Btn.B, "No thanks")); }
        public override void Back() { Sign(-1); }
    }

    // ---------------- settings / retiring ----------------

    public sealed class CareerSettingsScreen : GameScreen
    {
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "CAREER"; } }
        protected override string Title { get { return "Match settings"; } }
        protected override void Compose()
        {
            var s = Career.S;
            var p = Kit.Panel(Content, "CAREER MATCHES"); p.AddToClassList("k-cr-settings");
            AddTo(p, new SelectorItem("Match", Career.LengthLabels, () => System.Math.Max(0, System.Math.Min(2, s.GamesToWin - 1)), v => s.GamesToWin = v + 1));
            AddTo(p, new SelectorItem("Scoring", new[] { "PAR-11", "English" }, () => s.English ? 1 : 0, v => s.English = v == 1));
            Kit.L(p, "For every career match from now on. Your opponents' strength comes from their ranking.", "k-cr-step-hint");
            AddTo(Root, new PrimaryItem("Done", () => Fe.Pop()));
        }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Change"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
        public override void OnHide() { CareerStore.Save(); }
    }

    public sealed class RetireScreen : DialogScreen
    {
        protected override string Kicker { get { return "CAREER MATCH"; } }
        protected override string Title { get { return "Retire from the match?"; } }
        protected override string Line { get { return "Leaving counts as a loss."; } }
        protected override string YesLabel { get { return "Retire"; } }
        protected override string NoLabel { get { return "Play on"; } }
        protected override void OnYes() { Fe.Clear(); Fe.Runner.Clock.Resume(); Career.Record(Fe, false, true); }
    }
}
