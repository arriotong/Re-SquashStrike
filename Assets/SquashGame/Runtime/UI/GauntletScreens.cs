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
        /// <summary>2026-11-21 the last floor played (its result screen), and whether a failed one is played again.</summary>
        public static FloorPlay LastFloor;
        public static bool LastRetry;

        /// <summary>2026-10-28: your cards, the deck a run draws from.</summary>
        public static CardCollection Cards
        {
            get
            {
                // a new profile (or one from before the decks): the starter set
                if (G.Cards == null || G.Cards.Cards == null || G.Cards.Cards.Count == 0)
                {
                    G.Cards = GauntletCards.Starter(unchecked((uint)System.Environment.TickCount * 2654435761u) | 1u);
                    ProfileStore.Save();
                }
                if (G.Cards.Deck == null) G.Cards.Deck = new List<string>();
                return G.Cards;
            }
        }

        /// <summary>The card gacha (2026-10-29): 1 or 10 cards for coins - the cards, in order (empty: not enough coins).</summary>
        public static List<string> BuyCards(int n)
        {
            var w = GearDrops.Wallet;
            int cost = GauntletCards.Cost(n);
            if (w.Coins < cost) return new List<string>();
            w.Coins -= cost;
            Playtest.Log("coins", ("spent", cost), ("on", "cards x" + n));
            var got = GauntletCards.Draw(Cards, n);
            ProfileStore.Save();
            return got;
        }

        /// <summary>Evolve a level-3 card for coins.</summary>
        public static bool Evolve(string id)
        {
            int cost = GauntletCards.EvolveCostOf(id);
            if (!GauntletCards.CanEvolve(Cards, id) || GearDrops.Wallet.Coins < cost) return false;
            GearDrops.Wallet.Coins -= cost;
            Playtest.Log("coins", ("spent", cost), ("on", "evolve " + id));
            GauntletCards.Evolve(Cards, id);
            ProfileStore.Save();
            return true;
        }

        public static void NewRun()
        {
            if (Cards.Deck.Count == 0) GauntletCards.AutoDeck(Cards);
            G.Run = Gauntlet.NewRun(unchecked((uint)System.Environment.TickCount * 2654435761u) | 1u, Talents, GauntletCards.RunDeck(Cards));
            G.Run.Class = SetupFlow.Mine(0);                                   // 2026-11-23: the run's class, fixed for it
            _sessionSeed = G.Run.Seed;
            G.Runs++;
            Codex(G.Run);
            ProfileStore.Save();
        }

        public static void Abandon() { if (G.Run != null) { Finish(G.Run); G.Run = null; ProfileStore.Save(); } }

        // ---------------- 2026-11-23 (the user: "make it if you quit you lose your run, you cant resume it") ----------------
        static uint _sessionSeed;
        /// <summary>A run in progress (not over, not waiting on the win's choice).</summary>
        public static bool InProgress { get { var r = Run; return r != null && !r.Over && !r.ChoiceDue; } }
        /// <summary>The run left (the Gauntlet screen left, the floor quit, the game closed): it's over - no resuming.</summary>
        public static void QuitRun()
        {
            var r = Run;
            if (r == null || r.Over) return;
            r.Over = true; r.AwaitingPick = false;
            Playtest.Log("gauntlet_quit", ("floor", r.Floor), ("score", r.Score));
            Finish(r);
            ProfileStore.Save();
        }
        /// <summary>A run from an earlier session (the game was closed in the middle of it): ended. True: it was.</summary>
        public static bool EndStaleRun()
        {
            var r = Run;
            if (r == null || r.Over || r.ChoiceDue || r.Seed == _sessionSeed) return false;
            QuitRun();
            return true;
        }
        /// <summary>The class the run plays as (its own; an older run: the profile's).</summary>
        public static string RunClass(GauntletRun r) { return r != null && !string.IsNullOrEmpty(r.Class) ? r.Class : SetupFlow.Mine(0); }

        /// <summary>A run's end: its coins kept, the records.</summary>
        static void Finish(GauntletRun run)
        {
            if (run.RunWon) G.RunsWon++;
            G.BestFloorScore = Mathf.Max(G.BestFloorScore, run.BestFloorScore);
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
                Gauntlet = true, ParPoints = 999,                                    // 2026-11-21: the floor's target decides it, not a game
                GauntletFloor = Gauntlet.BeginFloor(run, Talents),
                Attributes = Gauntlet.Attributes(run), Mods = Gauntlet.SimMods(run), EachGame0 = Gauntlet.HeadStart(run), FocusCharges = Gauntlet.FocusCharges(run),
                Perks = true,                                                        // 2026-11-24 your training perks
                Class = RunClass(run),                                               // 2026-11-22 your class: the base the cards build on (2026-11-23: the run's)
                OppAttributes = fl.Attributes, OppMods = Gauntlet.OppMods(fl), KitSeed = star != null ? star.KitSeed : 0u,
                Court = CourtFor(run, f), CrowdFill = CrowdFor(f),
                P2Name = star != null ? SquashSim.Career.Stars.LastName(star).ToUpperInvariant() : "FLOOR " + f,
                Prestige = fl.Kind == FloorKind.Boss ? ("THE BOSS  ·  FLOOR " + f + "  ·  " + fl.Name).ToUpperInvariant() : null,     // camera work
            };
        }

        public static void Play(FrontEnd fe)
        {
            var run = Run;
            if (run == null || run.Over || run.AwaitingPick) return;
            fe.StartMatch(Settings(run));
        }

        /// <summary>The floor's match over another way (the old post-match, a forfeit): as a floor played.</summary>
        public static void AfterMatch(FrontEnd fe, bool won, int margin)
        {
            var f = fe.Match != null ? fe.Match.Floor : null;
            if (f == null) f = new FloorPlay { Target = 1, Rallies = 1, Played = 1, Score = won ? 1 : 0, Done = true };
            AfterFloor(fe, f);
        }

        /// <summary>2026-11-21 a floor over (FloorResultScreen's Continue): cleared - the score, the coins, the pick (the run won
        /// at floor 15); failed - the run over, unless the Golden Racket or Second Chance plays it again.</summary>
        public static void AfterFloor(FrontEnd fe, FloorPlay play)
        {
            var run = Run;
            LastUnlock = null; LastFloor = play; LastRetry = false;
            if (run != null)
            {
                bool won = play.Cleared;
                if (won)
                {
                    int aces = 0, nicks = 0;
                    if (fe.Stats != null && fe.Match != null) { var facts = fe.Stats.Facts(fe.Match); aces = facts.Aces; nicks = facts.Nicks; }
                    bool boss = Gauntlet.KindOf(run.Floor) == FloorKind.Boss;
                    Gauntlet.Won(run, play, aces, nicks, Talents);
                    LastReward = Rewards.GauntletFloor(run.FloorsWon, boss);
                    if (boss) LastUnlock = UnlockOne(run);
                    if (run.RunWon && run.ChoiceDue) LastReward = Rewards.GauntletRunWon();
                }
                else
                {
                    LastRetry = Gauntlet.Lost(run, play);
                    LastReward = null;
                }
                Playtest.Log("gauntlet", ("floor", won ? run.FloorsWon : run.Floor), ("won", won), ("score", play.Score), ("target", play.Target), ("retry", LastRetry), ("deck", run.HasDeck ? run.Deck.Count : 0), ("active", run.Abilities.Count));
                if (run.Over) { Playtest.Log("gauntlet_over", ("floors", run.FloorsWon), ("score", run.Score), ("bosses", run.BossesBeaten)); Finish(run); }
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

        /// <summary>2026-11-21 (no hearts): the run so far in a line - the floor, the run's score, the coins.</summary>
        public static string RunLine(GauntletRun r)
        {
            return (r.Endless ? "ENDLESS  ·  " : "") + "Floor " + r.Floor + (r.Endless ? "" : " of " + Gauntlet.WinFloor) + "  ·  " + r.Score.ToString("N0") + " points  ·  " + r.Coins + " coins";
        }

        /// <summary>Floor 15 cleared: take the win or go on into endless.</summary>
        public static void Choose(bool endless)
        {
            var run = Run;
            if (run == null) return;
            Gauntlet.Choose(run, endless);
            if (run.Over) Finish(run);
            ProfileStore.Save();
        }

        public static string Line()
        {
            var r = Run;
            if (r == null || r.Over) return State.BestFloor > 0 ? "Best: floor " + State.BestFloor + "  ·  runs won " + State.RunsWon + "  ·  " + State.Coins + " coins" : "Hit the target score on every floor. Clear 15 to win!";
            return RunLine(r);
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
        protected override bool ShowCoins { get { return true; } }
        protected override string Kicker { get { return "MODES  ·  THE GAUNTLET"; } }
        protected override string Title
        {
            get
            {
                var r = GauntletFlow.Run;
                if (r == null) return "The Gauntlet";
                if (r.Over) return r.RunWon ? "You won the Gauntlet!" : "Run over";
                if (r.ChoiceDue) return "You won the Gauntlet!";
                if (r.AwaitingPick) return r.BossPick ? "Boss beaten - your reward" : "Floor " + r.FloorsWon + " cleared";
                var f = Gauntlet.Floor(r, r.Floor);
                return f.Kind == FloorKind.Boss ? "Boss: " + f.Name : "Floor " + r.Floor;
            }
        }
        protected override bool ShowChip { get { return false; } }
        public override MenuWorld Backdrop { get { return MenuWorld.Gauntlet; } }                  // 2026-11-20 the floating island behind it
        public override bool Spatial { get { return true; } }
        bool _banishing;

        /// <summary>2026-11-23: leaving with a run in progress ends it (no resuming) - asked first.</summary>
        public override void Back()
        {
            if (!GauntletFlow.InProgress) { base.Back(); return; }
            Fe.Push(new ConfirmScreen("Leave the Gauntlet? Your run ends - it can't be resumed (its coins are kept).", () => { GauntletFlow.QuitRun(); Fe.Pop(); }));
        }

        protected override void Compose()
        {
            if (GauntletFlow.EndStaleRun()) GauntletFlow.LastUnlock = null;          // (closed mid-run last time: that run is over)
            var run = GauntletFlow.Run;
            var st = GauntletFlow.State;
            if (GauntletFlow.LastReward != null) { Content.Add(new RewardStrip(GauntletFlow.LastReward)); Fe.ToastAchievements(GauntletFlow.LastReward); GauntletFlow.LastReward = null; }
            if (GauntletFlow.LastUnlock != null) { Kit.L(Content, "UNLOCKED  ·  " + GauntletFlow.LastUnlock + " joins the cards", "gt-unlock"); GauntletFlow.LastUnlock = null; }
            var grid = Kit.Box(Content, "gt-grid");

            if (run == null || run.Over)
            {
                // 2026-11-20 (the user: "the ui larger and shaped like the career one"): one big card to start, four picture
                // cards beside it, the how-to-play behind a ? - the island on the right
                grid.RemoveFromHierarchy();
                var stage = Kit.Box(Content, "gt-stage");
                var hero = AddTo(stage, new CardItem(run != null ? "NEW RUN" : "START A RUN", run != null ? (run.RunWon ? "RUN WON!" : "RUN OVER") : "THE GAUNTLET",
                        () => Fe.Push(new ClassPickScreen("THE GAUNTLET  ·  YOUR CLASS FOR THIS RUN", SetupFlow.Mine(0), id =>
                        {
                            // 2026-11-23: the class for the run (kept to its end), then the intro, then floor 1
                            ProfileStore.Current.Choices.Class = id; ProfileStore.Save();
                            GauntletFlow.NewRun();
                            Fe.Replace(new GauntletIntroScreen());
                        })), CardSize.Hero)        // first: it has the focus
                    .Drawn("chevrons", new Color32(30, 14, 14, 255), new Color32(255, 90, 74, 255))
                    .Line(run != null
                        ? (run.RunWon ? "All " + Gauntlet.WinFloor + " floors" + (run.FloorsWon > Gauntlet.WinFloor ? " and " + (run.FloorsWon - Gauntlet.WinFloor) + " endless" : "") : "Floor " + run.Floor + " was too much") + "  ·  " + run.Score.ToString("N0") + " points"
                        : "Hit the target score on every floor. Clear floor " + Gauntlet.WinFloor + " to win!")
                    .Badge(() => st.RunsWon > 0 ? "WON " + st.RunsWon + "  ·  BEST FLOOR " + st.BestFloor : st.BestFloor > 0 ? "BEST  ·  FLOOR " + st.BestFloor : "NEW", true));
                hero.Root.AddToClassList("gt-hero");
                var sides = Kit.Box(stage, "gt-sides");
                Mini(sides, new CardItem("YOUR CARDS", "YOUR DECK", () => Fe.Push(new DeckScreen()))
                    .Drawn("stripes", new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255))
                    .Badge(() => GauntletFlow.Cards.Deck.Count + " CARDS"));
                Mini(sides, new CardItem("GET MORE CARDS", "PACKS", () => Fe.Push(new StoreScreen(StoreScreen.CardsTab)))
                    .Drawn("spots", new Color32(30, 18, 52, 255), new Color32(190, 110, 255, 255)));
                Mini(sides, new CardItem("UPGRADES", "SPEND COINS", () => Fe.Push(new TalentsScreen()))
                    .Drawn("trophy", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255))
                    .Badge(() => GauntletFlow.State.Coins + " COINS", GauntletFlow.State.Coins > 0));
                Mini(sides, new CardItem("CARD BOOK", "EVERY CARD", () => Fe.Push(new CodexScreen()))
                    .Drawn("rings", new Color32(14, 26, 22, 255), new Color32(200, 255, 61, 255)));
                var help = new ActionItem("?  How to play", () => Fe.Push(new GauntletHelpScreen()));
                help.Root.AddToClassList("k-action--chip");
                AddTo(TopBar, help);
                return;
            }

            if (run.ChoiceDue)
            {
                // 2026-11-21 floor 15 cleared: the run is won - take the win, or keep going into endless
                grid.RemoveFromHierarchy();
                var won = Kit.Box(Content, "gt-stage");
                var take = AddTo(won, new CardItem("TAKE THE WIN", "THE GAUNTLET  ·  WON", () => { GauntletFlow.Choose(false); Fe.Replace(new GauntletScreen()); }, CardSize.Hero)
                    .Drawn("trophy", new Color32(30, 24, 10, 255), new Color32(255, 196, 64, 255))
                    .Line(run.Score.ToString("N0") + " points  ·  " + run.Coins + " coins to bank  ·  the run ends here, won")
                    .Badge(() => "ALL " + Gauntlet.WinFloor + " FLOORS", true));
                take.Root.AddToClassList("gt-hero");
                var go = AddTo(won, new CardItem("KEEP GOING", "ENDLESS", () => { GauntletFlow.Choose(true); Fe.Replace(new GauntletScreen()); }, CardSize.Hero)
                    .Drawn("chevrons", new Color32(30, 14, 14, 255), new Color32(255, 90, 74, 255))
                    .Line("The floors never end - the targets keep climbing. Fail one and the run is over (it still counts as won).")
                    .Badge(() => "HIGH SCORE", false));
                go.Root.AddToClassList("gt-hero");
                return;
            }

            if (run.AwaitingPick)
            {
                Kit.L(Content, GauntletFlow.RunLine(run) + "  ·  next: floor " + run.Floor +
                               (run.BossPick ? (run.HasDeck ? "  ·  a boss's reward: the card plays a level higher" : "  ·  a boss's reward: every card takes two levels") : "")
                               + (run.HasDeck ? "  ·  from your deck" : ""), "gt-line");
                var row = Kit.Box(Content, "k-row");
                foreach (var id in run.Offer)
                {
                    var a = GauntletAbilities.Find(id);
                    if (a == null) continue;
                    var pick = id;
                    int now = Gauntlet.Level(run, id), lvNext = Gauntlet.LevelAfter(run, id);
                    string fam = a.Family != Family.None ? a.FamilyName + "  ·  " : "";
                    int dl = Gauntlet.DeckLevel(run, id);
                    string kicker = a.Kind == AbilityKind.Evolved ? fam + "EVOLUTION" : a.Kind == AbilityKind.Instant ? "BONUS"
                                  : dl > 0 ? fam + (dl > GauntletCards.MaxLevel ? "EVOLVED " : "LV " + dl + " ") + a.KindName.ToUpperInvariant()       // a deck card: its level
                                  : fam + (now == 0 ? "NEW " : "LV " + now + " → " + lvNext + " ") + a.KindName.ToUpperInvariant();
                    var card = AddTo(row, new CardItem(a.Name, kicker, () => { if (_banishing) { if (GauntletFlow.Banish(pick)) Fe.Replace(new GauntletScreen()); } else { GauntletFlow.Pick(pick); Fe.Replace(new GauntletScreen()); } })
                        .Drawn(GauntletFlow.Art(a), new Color32(18, 20, 26, 255), a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : GauntletFlow.RarityColour(a.Rarity),
                               a.Family != Family.None ? GauntletFlow.FamilyColour(a.Family) : (Color?)null)
                        .Line(a.Level(Mathf.Max(1, lvNext)) + (a.Kind != AbilityKind.Instant && a.MaxLevel > 1 ? "\n" + GauntletFlow.Pips(lvNext, a.MaxLevel) : ""))
                        .Badge(() => GauntletAbilities.RarityNames[(int)a.Rarity].ToUpperInvariant(), a.Rarity >= Rarity.Epic));
                    card.Root.AddToClassList("gt-card");
                }
                var acts = Kit.Box(Content, "gt-more");
                if (run.Rerolls > 0) AddTo(acts, new ActionItem("New cards  ·  " + run.Rerolls + " left", () => { if (GauntletFlow.Reroll()) Fe.Replace(new GauntletScreen()); }));
                if (run.Banishes > 0)
                {
                    ActionItem b = null;
                    b = AddTo(acts, new ActionItem("Throw a card away  ·  " + run.Banishes + " left", () =>
                    {
                        _banishing = !_banishing;
                        b.SetLabel(_banishing ? "Now pick the card to throw away" : "Throw a card away  ·  " + run.Banishes + " left");
                    }));
                }
                Kit.L(Content, "Pick one!", "k-cr-note");
                return;
            }

            // the next floor: a big card to play it (2026-11-20, like the career's), the opponent and your build beside it
            var fl = Gauntlet.Floor(run, run.Floor);
            grid.RemoveFromHierarchy();
            var stage2 = Kit.Box(Content, "gt-stage");
            bool boss = fl.Kind == FloorKind.Boss;
            int rallies = Gauntlet.RalliesFor(run, fl, GauntletFlow.State.Talents);
            var play = AddTo(stage2, new CardItem(boss ? "FACE THE BOSS" : "PLAY FLOOR " + run.Floor,
                    (run.Endless ? "ENDLESS  ·  " : "FLOOR " + run.Floor + " OF " + Gauntlet.WinFloor + "  ·  ") + (boss ? "THE BOSS" : "NEXT FLOOR"),
                    () => GauntletFlow.Play(Fe), CardSize.Hero)
                .Drawn(boss ? "trophy" : "chevrons", boss ? new Color32(34, 10, 18, 255) : new Color32(30, 14, 14, 255), boss ? new Color32(255, 60, 90, 255) : new Color32(255, 90, 74, 255))
                .Line("Score " + fl.Target.ToString("N0") + " in " + rallies + " rallies" + (GauntletFlow.LastRetry ? "  ·  SECOND CHANCE: try it again" : "") + "\n" + GauntletFlow.RunLine(run))
                .Badge(() => "TARGET " + fl.Target.ToString("N0"), true));
            GauntletFlow.LastRetry = false;
            var target = Kit.L(play.Art, fl.Target.ToString("N0"), "gt-target-big");
            play.Root.AddToClassList("gt-hero");
            var col = Kit.Box(stage2, "gt-col");
            var next = Kit.Panel(col, boss ? "THE BOSS" : fl.Kind == FloorKind.Elite ? "ELITE" : "YOUR OPPONENT"); next.AddToClassList("gt-info");
            // (2026-11-23 the user: "the gauntlet crashes after i reached level 5" - floor 5's boss had no title: nothing here may be missing)
            string bossTitle = string.IsNullOrEmpty(fl.Title) ? "" : fl.Title.ToUpperInvariant() + "  ·  ";
            Kit.L(next, (boss ? fl.Name + "  ·  " + bossTitle : fl.Kind == FloorKind.Elite ? fl.Name + "  ·  " : "") + (Roster.StyleName(fl.Style) ?? "").ToUpperInvariant() + "  ·  " + Level(fl.Difficulty), "gt-big");
            foreach (var line in Roster.StyleLines(fl.Style)) Kit.L(next, line, "gt-text");
            foreach (var tw in fl.Twists) Kit.L(next, "TWIST: " + Gauntlet.TwistName(tw) + " - " + Gauntlet.TwistText(tw), "gt-twist");
            if (fl.Traits.Count > 0) Kit.L(next, "Traits: " + Traits(fl), "gt-trait");
            if (fl.Signature.Count > 0) Kit.L(next, "Signature: " + string.Join(", ", fl.Signature.ConvertAll(m => { var a = GauntletAbilities.Find(m.Id); return a != null ? a.Name : m.Id; })), "gt-trait");
            var court = Courts.Find(GauntletFlow.CourtFor(run, run.Floor));
            Kit.L(next, rallies + " rallies" + (Gauntlet.HeadStart(run) > 0 ? "  ·  you start with " + Gauntlet.HeadStart(run) * 100 + " points" : "") + "  ·  " + (court != null ? court.Name : ""), "k-cr-note");
            var me = Kit.Panel(col, "YOUR BUILD"); me.AddToClassList("gt-info");
            Kit.L(me, run.Score.ToString("N0") + " points  ·  " + run.Coins + " coins" + (run.Golden ? "  ·  golden racket: one retry" : "") + (run.CanRevive && !run.Revived ? "  ·  second chance ready" : ""), "gt-hearts");
            Kit.L(me, "CLASS  ·  " + SetupFlow.ClassName(GauntletFlow.RunClass(run)).ToUpperInvariant() + "  ·  for the whole run", "gt-class");   // 2026-11-23: locked
            Build(me, run);
            var quit = new ActionItem("End the run", () => Fe.Push(new ConfirmScreen("End this run? It can't be resumed - its coins are kept.", () => { GauntletFlow.QuitRun(); Fe.Replace(new GauntletScreen()); })));
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
            var r = Kit.Panel(grid, "YOUR BEST"); r.AddToClassList("gt-side");
            Kit.L(r, st.BestFloor > 0 ? "Floor " + st.BestFloor : "Not played yet", "gt-big");
            if (st.Coins > 0) Kit.L(r, st.Coins + " Gauntlet coins to spend on upgrades", "gt-text");
        }

        /// <summary>A side card, smaller (the start screen's four).</summary>
        void Mini(VisualElement row, CardItem card) { AddTo(row, card); card.Root.AddToClassList("gt-mini"); }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    /// <summary>2026-11-20 the Gauntlet's how-to-play, behind the start screen's ? (the three steps of the kid test).</summary>
    public sealed class GauntletHelpScreen : DialogScreen
    {
        protected override string Title { get { return "How to play"; } }
        protected override string Line
        {
            get
            {
                return "1   Score the floor's target before your rallies run out. Miss it and the run is over!\n" +
                       "2   Win rallies in a row to build your multiplier, up to 5x. Winners, nicks and aces count most.\n" +
                       "3   At 5x the wheel spins: points, rallies, double scores - or the Golden Wheel!\n" +
                       "4   After every floor, pick a power card. Clear floor " + Gauntlet.WinFloor + " to win.";
            }
        }
        protected override string YesLabel { get { return "Got it"; } }
        protected override string NoLabel { get { return "Back"; } }
        protected override void OnYes() { Fe.Pop(); }
    }

    // ---------------- talents ----------------

    public sealed class TalentsScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  YOU HAVE " + GauntletFlow.State.Coins + " GAUNTLET COINS"; } }
        protected override string Title { get { return "Upgrades"; } }
        protected override bool ShowChip { get { return false; } }
        public override MenuWorld Backdrop { get { return MenuWorld.Gauntlet; } }
        static readonly string[][] Pages = { new[] { "hearty", "reroll", "wider", "banish" }, new[] { "kit", "revive", "treasure" } };

        protected override void Compose()
        {
            string[] names = { "HELP", "MORE HELP" };
            for (int p = 0; p < Pages.Length; p++)
            {
                var row = Kit.Box(Page(names[p]), "k-row");
                foreach (var id in Pages[p]) Card(row, GauntletTalents.Find(id));
            }
            var unlocks = GauntletTalents.All().FindAll(t => t.Unlocks != null);
            for (int i = 0; i < unlocks.Count; i += 4)
            {
                var row = Kit.Box(Page(i == 0 ? "NEW CARDS" : "MORE NEW CARDS"), "k-row");
                for (int k = i; k < i + 4 && k < unlocks.Count; k++) Card(row, unlocks[k]);
            }
        }

        void Card(VisualElement row, Talent t)
        {
            if (t == null) return;
            var owned = GauntletFlow.State.Talents;
            AddTo(row, new CardItem(t.Name, "", () => { if (GauntletFlow.BuyTalent(t.Id)) Fe.Replace(new TalentsScreen()); })
                .Drawn(TalentArt(t), new Color32(20, 22, 30, 255), new Color32(255, 184, 59, 255))
                .Line(t.Text + (t.MaxLevel > 1 ? "\nBought " + GauntletTalents.Level(owned, t.Id) + " of " + t.MaxLevel : ""))
                .Badge(() => { int c = GauntletTalents.NextCost(owned, t.Id); return c < 0 ? (t.Unlocks != null ? "UNLOCKED" : "MAX") : c + " COINS"; },
                       GauntletTalents.NextCost(owned, t.Id) >= 0 && GauntletTalents.NextCost(owned, t.Id) <= GauntletFlow.State.Coins));
        }

        /// <summary>2026-10-30 (the kid test): each upgrade its own picture (they were all the same bracket).</summary>
        static string TalentArt(Talent t)
        {
            if (t.Unlocks != null) { var a = GauntletAbilities.Find(t.Unlocks); return a != null ? GauntletFlow.Art(a) : "rings"; }
            switch (t.Id)
            {
                case "hearty": return "trophy";
                case "reroll": return "split";
                case "wider": return "menu";
                case "banish": return "angle";
                case "kit": return "spots";
                case "revive": return "clock";
                default: return "corner";
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Buy"), (PromptBar.Btn.Tabs, "Pages"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the codex ----------------

    public sealed class CodexScreen : GameScreen
    {
        protected override string Kicker { get { return "THE GAUNTLET  ·  EVERY CARD"; } }
        protected override string Title { get { return "Card book"; } }
        protected override bool ShowChip { get { return false; } }
        public override MenuWorld Backdrop { get { return MenuWorld.Gauntlet; } }

        public override bool Spatial { get { return true; } }

        protected override void Compose()
        {
            var st = GauntletFlow.State;
            // 2026-11-21 (the screen sweep: six columns of tiny names): a tab per build, its cards as pictures - bright: you've
            // used it; LOCKED: buy it in Upgrades first; a mix: the two cards that make it
            foreach (var fam in new[] { Family.Time, Family.Power, Family.Trick, Family.Jackpot, Family.Guard, Family.None })
            {
                var grid = Kit.Box(Page(fam == Family.None ? "ANY BUILD" : fam.ToString().ToUpperInvariant()), "dk-grid");
                Color col = fam != Family.None ? GauntletFlow.FamilyColour(fam) : (Color)new Color32(200, 255, 61, 255);
                foreach (var a in GauntletAbilities.All().FindAll(x => x.Family == fam && !x.Retired && x.Kind != AbilityKind.Instant))
                {
                    var ab = a;
                    var best = st.Codex != null ? st.Codex.Find(c => c.Id == a.Id) : null;
                    bool open = GauntletTalents.Unlocked(st.Talents, a);
                    string mix = a.Recipe != null ? "Mix " + GauntletAbilities.Find(a.Recipe[0]).Name + " + " + GauntletAbilities.Find(a.Recipe[1]).Name : null;
                    var card = AddTo(grid, new CardItem(a.Name.ToUpperInvariant(), best != null ? "USED  ·  BEST LEVEL " + best.Level : "NOT USED YET", null)
                        .Drawn(GauntletFlow.Art(a), new Color32(18, 20, 26, 255), best != null ? col : new Color(col.r, col.g, col.b, 0.35f))
                        .Line(mix ?? a.Blurb)
                        .Badge(() => best != null ? (a.MaxLevel > 1 ? GauntletFlow.Pips(best.Level, a.MaxLevel) : "✓") : "", best != null));
                    card.Root.AddToClassList("dk-card");
                    card.Root.AddToClassList("dk-card--book");
                    card.Locked = () => !open;
                    card.LockLine = () => "in Upgrades";
                }
            }
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.B, "Back")); }
    }
}
