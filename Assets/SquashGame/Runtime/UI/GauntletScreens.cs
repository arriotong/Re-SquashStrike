// ===========================================================
// Assets/SquashGame/Runtime/UI/GauntletScreens.cs
// P5: the Gauntlet roguelite's screens and flow (the rules: SquashSim.
// Modes.Gauntlet, GauntletAbilities, GauntletTalents).
//
//   GauntletFlow     a run in the profile: start one (with the talents),
//                    the floor's match settings (game to 7, the build, the
//                    opponent's traits), its end (score, coins, the pick,
//                    a boss's unlock), the run's end (coins banked)
//   GauntletScreen   home (start, talents, codex, records) - the pick
//                    after a floor (3-4 cards; reroll; banish) - the next
//                    floor (the opponent, your build)
//   TalentsScreen    permanent upgrades for coins
//   CodexScreen      every ability: seen or not, its best level
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using SquashSim.Career;
using SquashSim.Modes;
using Rarity = SquashSim.Modes.Rarity;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class GauntletFlow
    {
        static GauntletState G { get { var p = ProfileStore.Current.Progress; if (p.Gauntlet == null) p.Gauntlet = new GauntletState(); return p.Gauntlet; } }
        public static GauntletRun Run { get { var r = G.Run; return r != null && r.Active ? r : null; } }
        public static GauntletState State { get { return G; } }
        static List<RunMod> Talents { get { if (G.Talents == null) G.Talents = new List<RunMod>(); return G.Talents; } }
        /// <summary>The floor's reward, shown once on the Gauntlet screen.</summary>
        public static RewardResult LastReward;
        /// <summary>What a boss gave (an ability unlocked), shown once.</summary>
        public static string LastUnlock;

        public static void NewRun()
        {
            G.Run = Gauntlet.NewRun(unchecked((uint)System.Environment.TickCount * 2654435761u) | 1u, Talents);
            G.Runs++;
            Codex(G.Run);
            ProfileStore.Save();
        }

        public static void Abandon() { if (G.Run != null) { Finish(G.Run); G.Run = null; ProfileStore.Save(); } }

        /// <summary>A run's end: its coins kept, the records.</summary>
        static void Finish(GauntletRun run)
        {
            G.BestFloor = Mathf.Max(G.BestFloor, run.FloorsWon);
            G.BestScore = Mathf.Max(G.BestScore, run.Score);
            G.Coins += run.Coins;
            G.BossesBeaten += run.BossesBeaten;
            run.Coins = 0;                                                     // banked (a finished run shown again does not pay twice)
        }

        /// <summary>The career court a floor would have had (by tier: the club first, the arena by floor 12, then round again).</summary>
        public static int CourtTier(int floor) { return (floor - 1) % 13; }

        /// <summary>UI remake (2026-10-28): "make the courts change every time now that we have so many". Every floor on a
        /// different court from all of them - the World Tour's stages, the game's own courts, the Challenger venues: clubs
        /// and the game's courts early, the big stages more often as the run climbs, a boss always on a big stage; never the
        /// same court twice running. From the run's seed, so a run's courts are always the same.</summary>
        public static string CourtFor(GauntletRun run, int floor)
        {
            string prev = null;
            for (int f = 1; f <= floor; f++)
            {
                string c = PickCourt(run.Seed, f, 0);
                for (int salt = 1; c == prev && salt < 8; salt++) c = PickCourt(run.Seed, f, salt);
                prev = c;
            }
            return prev;
        }

        static string PickCourt(uint seed, int floor, int salt)
        {
            uint h = seed ^ unchecked((uint)floor * 2654435761u) ^ unchecked((uint)(salt + 1) * 40503u);
            h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13; h = unchecked(h * 3266489917u); h ^= h >> 16;
            var tour = TourVenues.All;
            if (Gauntlet.KindOf(floor) == FloorKind.Boss) return Courts.TourPrefix + tour[h % (uint)tour.Length].Key;
            double r = (h >> 8) / 16777216.0;
            double big = Mathf.Clamp(0.2f + floor * 0.04f, 0.2f, 0.65f);
            uint k = h % 9973u;
            if (r < big) return Courts.TourPrefix + tour[k % (uint)tour.Length].Key;
            if (r < big + 0.15) return Courts.All[k % (uint)Courts.All.Length].Id;
            return Courts.VenuePrefix + ClubVenues.All[k % (uint)ClubVenues.All.Length].Key;
        }

        /// <summary>How full the floor's stands are: filling as the run climbs, packed for a boss.</summary>
        public static float CrowdFor(int floor) { return Gauntlet.KindOf(floor) == FloorKind.Boss ? 1f : Mathf.Clamp(0.35f + floor * 0.05f, 0.35f, 1f); }

        public static MatchSettings Settings(GauntletRun run)
        {
            int f = run.Floor;
            var fl = Gauntlet.Floor(run, f);
            var star = Gauntlet.StarFor(f);
            return new MatchSettings
            {
                Opponent = fl.OpponentId, Difficulty = (float)fl.Difficulty, GamesToWin = 1, Scoring = "par", WarmUp = false,
                Gauntlet = true, ParPoints = fl.Points,
                Attributes = Gauntlet.Attributes(run), Mods = Gauntlet.SimMods(run), EachGame0 = Gauntlet.HeadStart(run), FocusCharges = Gauntlet.FocusCharges(run),
                OppAttributes = fl.Attributes, OppMods = Gauntlet.OppMods(fl), KitSeed = star != null ? star.KitSeed : 0u,
                Court = CourtFor(run, f), CrowdFill = CrowdFor(f),
                P2Name = star != null ? SquashSim.Career.Stars.LastName(star).ToUpperInvariant() : "FLOOR " + f,
            };
        }

        public static void Play(FrontEnd fe)
        {
            var run = Run;
            if (run == null || run.Over || run.AwaitingPick) return;
            fe.StartMatch(Settings(run));
        }

        /// <summary>The floor's match is over (or forfeited): the run moves on; back to the Gauntlet screen.</summary>
        public static void AfterMatch(FrontEnd fe, bool won, int margin)
        {
            var run = Run;
            LastUnlock = null;
            if (run != null)
            {
                if (won)
                {
                    int aces = 0, nicks = 0;
                    if (fe.Stats != null && fe.Match != null) { var facts = fe.Stats.Facts(fe.Match); aces = facts.Aces; nicks = facts.Nicks; }
                    bool boss = Gauntlet.KindOf(run.Floor) == FloorKind.Boss;
                    Gauntlet.Won(run, margin, aces, nicks, Talents);
                    LastReward = Rewards.GauntletFloor(run.FloorsWon, boss);
                    if (boss) LastUnlock = UnlockOne(run);
                }
                else
                {
                    Gauntlet.Lost(run);
                    LastReward = null;
                }
                if (run.Over) Finish(run);
                else G.BestFloor = Mathf.Max(G.BestFloor, run.FloorsWon);
                ProfileStore.Save();
            }
            fe.QuitToMenu();
            fe.Push(new GauntletScreen());
        }

        /// <summary>A boss beaten: one locked ability joins the cards for good (null: none left to unlock).</summary>
        static string UnlockOne(GauntletRun run)
        {
            var locked = GauntletAbilities.All().FindAll(a => !a.Retired && !GauntletTalents.Unlocked(Talents, a));
            if (locked.Count == 0) return null;
            var a = locked[(int)((run.Seed + (uint)run.Floor * 7919u) % (uint)locked.Count)];
            Talents.Add(new RunMod { Id = "unlock_" + a.Id, Level = 1 });
            return a.Name;
        }

        public static void Pick(string id)
        {
            var run = Run;
            if (run == null) return;
            if (Gauntlet.Pick(run, id))
            {
                Codex(run);
                var a = GauntletAbilities.Find(id);
                if (a != null && a.Kind == AbilityKind.Evolved) LastReward = Rewards.GauntletEvolved();
            }
            ProfileStore.Save();
        }

        public static bool Reroll() { var r = Run; bool ok = r != null && Gauntlet.Reroll(r, Talents); if (ok) ProfileStore.Save(); return ok; }
        public static bool Banish(string id) { var r = Run; bool ok = r != null && Gauntlet.Banish(r, id, Talents); if (ok) ProfileStore.Save(); return ok; }

        /// <summary>The codex keeps each ability's best level.</summary>
        static void Codex(GauntletRun run)
        {
            if (G.Codex == null) G.Codex = new List<RunMod>();
            foreach (var m in run.Abilities)
            {
                var c = G.Codex.Find(x => x.Id == m.Id);
                if (c == null) G.Codex.Add(new RunMod { Id = m.Id, Level = m.Level });
                else if (m.Level > c.Level) c.Level = m.Level;
            }
        }

        public static bool BuyTalent(string id)
        {
            int coins = G.Coins;
            bool ok = GauntletTalents.Buy(Talents, ref coins, id);
            G.Coins = coins;
            if (ok) ProfileStore.Save();
            return ok;
        }

        public static string Hearts(GauntletRun r)
        {
            int max = Mathf.Max(Gauntlet.StartLives, r.Lives);
            return new string('♥', r.Lives) + new string('♡', max - r.Lives);
        }

        public static string Line()
        {
            var r = Run;
            if (r == null || r.Over) return State.BestFloor > 0 ? "Best: floor " + State.BestFloor + "  ·  " + State.BestScore.ToString("N0") + " pts  ·  " + State.Coins + " coins" : "Endless floors. Build your player. How far can you go?";
            return "Floor " + r.Floor + "  ·  " + Hearts(r) + "  ·  " + r.Score.ToString("N0") + " pts";
        }

        /// <summary>"●●●○○".</summary>
        public static string Pips(int level, int max) { return new string('●', level) + new string('○', Mathf.Max(0, max - level)); }

        public static Color RarityColour(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color32(111, 184, 255, 255);
                case Rarity.Epic: return new Color32(190, 110, 255, 255);
                case Rarity.Legendary: return new Color32(255, 184, 59, 255);
                default: return new Color32(200, 255, 61, 255);
            }
        }

        /// <summary>The chaos remake's builds, each its own colour (the cards, the build panel, the codex, the HUD).</summary>
        public static Color FamilyColour(Family f)
        {
            switch (f)
            {
                case Family.Time: return new Color32(96, 176, 255, 255);
                case Family.Power: return new Color32(255, 100, 64, 255);
                case Family.Trick: return new Color32(196, 112, 255, 255);
                case Family.Jackpot: return new Color32(255, 204, 64, 255);
                case Family.Guard: return new Color32(96, 224, 144, 255);
                default: return new Color32(200, 200, 210, 255);
            }
        }

        public static string Art(Ability a)
        {
            if (a.Kind == AbilityKind.Evolved) return "trophy";
            switch (a.Family)
            {
                case Family.Time: return "clock";
                case Family.Power: return "chevrons";
                case Family.Trick: return "trail";
                case Family.Jackpot: return "rings";
                case Family.Guard: return "bracket";
            }
            switch (a.Kind)
            {
                case AbilityKind.Shot: return "rings";
                case AbilityKind.Power: return "chevrons";
                case AbilityKind.Evolved: return "trophy";
                case AbilityKind.Stat: return "corner";
                case AbilityKind.Perk: return "bracket";
                default: return "trophy";
            }
        }
    }

    public sealed class GauntletScreen : GameScreen
    {
        protected override string Kicker { get { return "MODES  ·  THE GAUNTLET"; } }
        protected override string Title
        {
            get
            {
                var r = GauntletFlow.Run;
                if (r == null) return "The Gauntlet";
                if (r.Over) return "Run over";
                if (r.AwaitingPick) return r.BossPick ? "Boss beaten - your reward" : "Floor " + r.FloorsWon + " cleared";
                var f = Gauntlet.Floor(r, r.Floor);
                return f.Kind == FloorKind.Boss ? "Boss: " + f.Name : (f.Kind == FloorKind.Elite ? "Elite floor " : "Floor ") + r.Floor;
            }
        }
        protected override bool ShowChip { get { return false; } }
        bool _banishing;

        protected override void Compose()
        {
            var run = GauntletFlow.Run;
            var st = GauntletFlow.State;
            if (GauntletFlow.LastReward != null) { Content.Add(new RewardStrip(GauntletFlow.LastReward)); Fe.ToastAchievements(GauntletFlow.LastReward); GauntletFlow.LastReward = null; }
            if (GauntletFlow.LastUnlock != null) { Kit.L(Content, "UNLOCKED  ·  " + GauntletFlow.LastUnlock + " joins the cards", "gt-unlock"); GauntletFlow.LastUnlock = null; }
            var grid = Kit.Box(Content, "gt-grid");

            if (run == null || run.Over)
            {
                var about = Kit.Panel(grid, run != null ? "THE RUN" : "HOW IT WORKS"); about.AddToClassList("gt-main");
                if (run != null)
                {
                    Kit.L(about, "Floor " + run.FloorsWon + " cleared  ·  " + run.Score.ToString("N0") + " points", "gt-big");
                    Kit.L(about, run.BossesBeaten + (run.BossesBeaten == 1 ? " boss" : " bosses") + " beaten  ·  " + run.Picks + " abilities picked", "gt-text");
                    Build(about, run);
                }
                else
                {
                    Kit.L(about, "Endless floors: one game to 7 each. Every 5th an elite, every 10th a boss.", "gt-text");
                    Kit.L(about, "Win a floor and pick one of three cards: new shots, powers and stats, or a level up. Two at the top can evolve.", "gt-text");
                    Kit.L(about, "Lose a floor and it costs a heart. Coins from every floor buy talents that last.", "gt-text");
                }
                Records(grid, st);
                AddTo(Root, new PrimaryItem(run != null ? "New run" : "Start a run", () => { GauntletFlow.NewRun(); Fe.Replace(new GauntletScreen()); }));   // first: it has the focus
                var more = Kit.Box(Content, "gt-more");
                AddTo(more, new ActionItem("Talents  ·  " + st.Coins + " coins", () => Fe.Push(new TalentsScreen()), "▶"));
                AddTo(more, new ActionItem("Codex", () => Fe.Push(new CodexScreen()), "▶"));
                return;
            }

            if (run.AwaitingPick)
            {
                Kit.L(Content, GauntletFlow.Hearts(run) + "   " + run.Score.ToString("N0") + " points  ·  " + run.Coins + " coins  ·  next: floor " + run.Floor +
                               (run.BossPick ? "  ·  a boss's reward: every card takes two levels" : ""), "gt-line");
                var row = Kit.Box(Content, "k-row");
                foreach (var id in run.Offer)
                {
                    var a = GauntletAbilities.Find(id);
                    if (a == null) continue;
                    var pick = id;
                    int now = Gauntlet.Level(run, id), lvNext = Gauntlet.LevelAfter(run, id);
                    string fam = a.Family != Family.None ? a.FamilyName + "  ·  " : "";
                    string kicker = a.Kind == AbilityKind.Evolved ? fam + "EVOLUTION" : a.Kind == AbilityKind.Instant ? "BONUS" : fam + (now == 0 ? "NEW " : "LV " + now + " → " + lvNext + " ") + a.KindName.ToUpperInvariant();
                    var card = AddTo(row, new CardItem(a.Name, kicker, () => { if (_banishing) { if (GauntletFlow.Banish(pick)) Fe.Replace(new GauntletScreen()); } else { GauntletFlow.Pick(pick); Fe.Replace(new GauntletScreen()); } })
                        .Drawn(GauntletFlow.Art(a), new Color32(18, 20, 26, 255), a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : GauntletFlow.RarityColour(a.Rarity),
                               a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : (Color?)null)
                        .Line(a.Level(Mathf.Max(1, lvNext)) + (a.Kind != AbilityKind.Instant && a.MaxLevel > 1 ? "\n" + GauntletFlow.Pips(lvNext, a.MaxLevel) : ""))
                        .Badge(() => GauntletAbilities.RarityNames[(int)a.Rarity].ToUpperInvariant(), a.Rarity >= Rarity.Epic));
                    card.Root.AddToClassList("gt-card");
                }
                var acts = Kit.Box(Content, "gt-more");
                if (run.Rerolls > 0) AddTo(acts, new ActionItem("Reroll  ·  " + run.Rerolls + " left", () => { if (GauntletFlow.Reroll()) Fe.Replace(new GauntletScreen()); }));
                if (run.Banishes > 0)
                {
                    ActionItem b = null;
                    b = AddTo(acts, new ActionItem("Banish a card  ·  " + run.Banishes + " left", () =>
                    {
                        _banishing = !_banishing;
                        b.SetLabel(_banishing ? "Banishing: choose the card to strike" : "Banish a card  ·  " + run.Banishes + " left");
                    }));
                }
                Kit.L(Content, "Pick one. " + (run.Banishes > 0 ? "Banish: choose it, then the card to strike out of this run." : ""), "k-cr-note");
                return;
            }

            // the next floor
            var fl = Gauntlet.Floor(run, run.Floor);
            var next = Kit.Panel(grid, fl.Kind == FloorKind.Boss ? "THE BOSS" : fl.Kind == FloorKind.Elite ? "ELITE" : "NEXT"); next.AddToClassList("gt-main");
            Kit.L(next, (fl.Kind == FloorKind.Boss ? fl.Name + "  ·  " + fl.Title.ToUpperInvariant() + "  ·  " : fl.Kind == FloorKind.Elite ? fl.Name + "  ·  " : "") + Roster.StyleName(fl.Style).ToUpperInvariant() + "  ·  " + Level(fl.Difficulty), "gt-big");
            foreach (var line in Roster.StyleLines(fl.Style)) Kit.L(next, line, "gt-text");
            if (fl.Traits.Count > 0) Kit.L(next, "Traits: " + Traits(fl), "gt-trait");
            if (fl.Signature.Count > 0) Kit.L(next, "Signature: " + string.Join(", ", fl.Signature.ConvertAll(m => { var a = GauntletAbilities.Find(m.Id); return a != null ? a.Name : m.Id; })), "gt-trait");
            var court = Courts.Find(GauntletFlow.CourtFor(run, run.Floor));
            Kit.L(next, "One game to " + fl.Points + (Gauntlet.HeadStart(run) > 0 ? ", you start " + Gauntlet.HeadStart(run) + "-0 up" : "") + "  ·  " + (court != null ? court.Name : ""), "k-cr-note");
            var me = Kit.Panel(grid, "YOUR BUILD"); me.AddToClassList("gt-side");
            Kit.L(me, GauntletFlow.Hearts(run) + (run.Golden ? "   + golden racket" : ""), "gt-hearts");
            Kit.L(me, run.Score.ToString("N0") + " points  ·  " + run.Coins + " coins", "gt-text");
            Build(me, run);
            AddTo(Root, new PrimaryItem((fl.Kind == FloorKind.Boss ? "Face the boss" : "Play floor " + run.Floor), () => GauntletFlow.Play(Fe)));
            var quit = new ActionItem("Abandon the run", () => Fe.Push(new ConfirmScreen("Abandon this run? Its coins are kept.", () => { GauntletFlow.Abandon(); Fe.Replace(new GauntletScreen()); })));
            quit.Root.AddToClassList("k-action--chip");
            AddTo(TopBar, quit);
        }

        static string Traits(FloorInfo f)
        {
            var l = new List<string>();
            foreach (var t in f.Traits) foreach (var tr in Gauntlet.Traits) if (tr.id == t.Id) l.Add(tr.name + (t.Level > 1 ? " " + t.Level : "") + " (" + tr.text + ")");
            return string.Join(",  ", l);
        }

        /// <summary>P6: how strong, as a rating (no more Easy / Medium / Hard).</summary>
        static string Level(double d) { return "PLAYS LIKE A " + SquashSim.Career.Stars.RatingFor(d); }

        /// <summary>The run's abilities, each with its level as pips.</summary>
        static void Build(VisualElement panel, GauntletRun run)
        {
            if (run.Abilities.Count == 0) { Kit.L(panel, "No abilities yet - win a floor to pick one. Five builds: TIME, POWER, TRICK, JACKPOT, GUARD - an ultimate fires on a charged shot when POWER is full.", "k-cr-note"); return; }
            // the builds you are on, strongest first (the chaos remake)
            var count = new Dictionary<Family, int>();
            foreach (var m in run.Abilities) { var a = GauntletAbilities.Find(m.Id); if (a != null && a.Family != Family.None) count[a.Family] = (count.TryGetValue(a.Family, out int c) ? c : 0) + m.Level; }
            var fams = new List<Family>(count.Keys); fams.Sort((x, y) => count[y].CompareTo(count[x]));
            if (fams.Count > 0)
            {
                var strip = Kit.Box(panel, "gt-fams");
                foreach (var f in fams) { var l = Kit.L(strip, f.ToString().ToUpperInvariant() + " " + count[f], "gt-fam"); l.style.color = GauntletFlow.FamilyColour(f); }
            }
            foreach (var m in run.Abilities)
            {
                var a = GauntletAbilities.Find(m.Id);
                if (a == null) continue;
                var r = Kit.Box(panel, "gt-ab");
                var n = Kit.L(r, a.Name, "gt-ab-name"); n.style.color = a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : GauntletFlow.RarityColour(a.Rarity);
                Kit.L(r, a.MaxLevel > 1 ? GauntletFlow.Pips(m.Level, a.MaxLevel) : "", "gt-ab-pips");
            }
            Kit.L(panel, "Active " + Gauntlet.Count(run, true) + " / " + GauntletAbilities.ActiveSlots + "  ·  passive " + Gauntlet.Count(run, false) + " / " + GauntletAbilities.PassiveSlots, "k-cr-note");
        }

        static void Records(VisualElement grid, GauntletState st)
        {
            var r = Kit.Panel(grid, "RECORDS"); r.AddToClassList("gt-side");
            Kit.L(r, "Best floor " + st.BestFloor, "gt-big");
            Kit.L(r, "Best score " + st.BestScore.ToString("N0") + "  ·  " + st.Runs + (st.Runs == 1 ? " run" : " runs") + "  ·  " + st.BossesBeaten + " bosses", "gt-text");
            Kit.L(r, st.Coins + " coins to spend on talents", "gt-text");
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- talents ----------------

    public sealed class TalentsScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  " + GauntletFlow.State.Coins + " COINS"; } }
        protected override string Title { get { return "Talents"; } }
        protected override bool ShowChip { get { return false; } }
        static readonly string[][] Pages = { new[] { "hearty", "reroll", "wider", "banish" }, new[] { "kit", "revive", "treasure" } };

        protected override void Compose()
        {
            string[] names = { "THE RUN", "THE EDGE" };
            for (int p = 0; p < Pages.Length; p++)
            {
                var row = Kit.Box(Page(names[p]), "k-row");
                foreach (var id in Pages[p]) Card(row, GauntletTalents.Find(id));
            }
            var unlocks = GauntletTalents.All().FindAll(t => t.Unlocks != null);
            for (int i = 0; i < unlocks.Count; i += 4)
            {
                var row = Kit.Box(Page(i == 0 ? "UNLOCKS" : "MORE UNLOCKS"), "k-row");
                for (int k = i; k < i + 4 && k < unlocks.Count; k++) Card(row, unlocks[k]);
            }
        }

        void Card(VisualElement row, Talent t)
        {
            if (t == null) return;
            var owned = GauntletFlow.State.Talents;
            AddTo(row, new CardItem(t.Name, "", () => { if (GauntletFlow.BuyTalent(t.Id)) Fe.Replace(new TalentsScreen()); })
                .Drawn(t.Unlocks != null ? "rings" : "bracket", new Color32(20, 22, 30, 255), new Color32(255, 184, 59, 255))
                .Line(t.Text + "\n" + (t.MaxLevel > 1 ? GauntletFlow.Pips(GauntletTalents.Level(owned, t.Id), t.MaxLevel) : ""))
                .Badge(() => { int c = GauntletTalents.NextCost(owned, t.Id); return c < 0 ? (t.Unlocks != null ? "UNLOCKED" : "MAX") : c + " COINS"; },
                       GauntletTalents.NextCost(owned, t.Id) >= 0 && GauntletTalents.NextCost(owned, t.Id) <= GauntletFlow.State.Coins));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Buy"), (PromptBar.Btn.Tabs, "Pages"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the codex ----------------

    public sealed class CodexScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET"; } }
        protected override string Title { get { return "Codex"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var st = GauntletFlow.State;
            var grid = Kit.Box(Content, "gt-codex");
            // the chaos remake: by build (the retired cards are out)
            foreach (var fam in new[] { Family.Time, Family.Power, Family.Trick, Family.Jackpot, Family.Guard, Family.None })
            {
                var p = Kit.Panel(grid, fam == Family.None ? "ANY BUILD" : fam.ToString().ToUpperInvariant()); p.AddToClassList("gt-codex-panel");
                if (fam != Family.None) p.Q<Label>(className: "k-panel-title").style.color = GauntletFlow.FamilyColour(fam);
                foreach (var a in GauntletAbilities.All().FindAll(x => x.Family == fam && !x.Retired && x.Kind != AbilityKind.Instant))
                {
                    var best = st.Codex != null ? st.Codex.Find(c => c.Id == a.Id) : null;
                    bool open = GauntletTalents.Unlocked(st.Talents, a);
                    var r = Kit.Box(p, "gt-ab");
                    var n = Kit.L(r, open ? a.Name : a.Name + "  (locked)", "gt-ab-name");
                    n.style.color = best != null ? GauntletFlow.RarityColour(a.Rarity) : new Color(1, 1, 1, open ? 0.55f : 0.3f);
                    Kit.L(r, best != null ? (a.MaxLevel > 1 ? GauntletFlow.Pips(best.Level, a.MaxLevel) : "✓") : (a.Recipe != null ? GauntletAbilities.Find(a.Recipe[0]).Name + " + " + GauntletAbilities.Find(a.Recipe[1]).Name : "-"), "gt-ab-pips");
                }
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.B, "Back")); }
    }
}
