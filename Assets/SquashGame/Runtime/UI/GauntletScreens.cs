// ===========================================================
// Assets/SquashGame/Runtime/UI/GauntletScreens.cs
// P3-R5: the Gauntlet's screen and flow (the rules: SquashSim.Modes.
// Gauntlet). One screen shows where the run stands - the next floor, or
// the perks to pick from after a floor won, or the run's end - and plays
// the floors; the match comes back here through GauntletFlow.
// ===========================================================

using System.Collections.Generic;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using SquashSim.Career;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class GauntletFlow
    {
        static GauntletState G { get { var p = ProfileStore.Current.Progress; if (p.Gauntlet == null) p.Gauntlet = new GauntletState(); return p.Gauntlet; } }
        public static GauntletRun Run { get { var r = G.Run; return r != null && r.Active ? r : null; } }
        public static GauntletState State { get { return G; } }
        /// <summary>The floor's reward, shown once on the Gauntlet screen.</summary>
        public static RewardResult LastReward;

        public static void NewRun()
        {
            G.Run = Gauntlet.NewRun(unchecked((uint)System.Environment.TickCount * 2654435761u) | 1u);
            G.Runs++;
            ProfileStore.Save();
        }

        public static void Abandon() { if (G.Run != null) { Finish(G.Run); G.Run = null; ProfileStore.Save(); } }

        static void Finish(GauntletRun run)
        {
            G.BestFloor = Mathf.Max(G.BestFloor, run.FloorsWon);
            G.BestScore = Mathf.Max(G.BestScore, run.Score);
        }

        /// <summary>The court a floor is played on: the club for the first floors, the arena by floor 12.</summary>
        public static int CourtTier(int floor) { return Mathf.Clamp(floor - 1, 0, 12); }

        public static MatchSettings Settings(GauntletRun run)
        {
            int f = run.Floor;
            string opp = Gauntlet.Opponent(run, f);
            return new MatchSettings
            {
                Opponent = opp, Difficulty = (float)Gauntlet.Difficulty(f), GamesToWin = 1, Scoring = "par", WarmUp = false,
                Gauntlet = true, Attributes = run.Attributes.Clone(), EachGame0 = run.HeadStart,
                Court = Courts.ForCareerTier(CourtTier(f)), CrowdFill = Courts.CrowdForCareerTier(CourtTier(f)),
                P2Name = "FLOOR " + f,
            };
        }

        public static void Play(FrontEnd fe)
        {
            var run = Run;
            if (run == null || run.Over || run.AwaitingPerk) return;
            fe.StartMatch(Settings(run));
        }

        /// <summary>The floor's match is over (or forfeited): the run moves on; back to the Gauntlet screen.</summary>
        public static void AfterMatch(FrontEnd fe, bool won, int margin)
        {
            var run = Run;
            if (run != null)
            {
                if (won)
                {
                    Gauntlet.Won(run, margin);
                    LastReward = Rewards.GauntletFloor(run.FloorsWon);
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

        public static string Hearts(GauntletRun r)
        {
            int max = Mathf.Max(Gauntlet.StartLives, r.Lives);
            return new string('♥', r.Lives) + new string('♡', max - r.Lives);
        }

        public static string Line()
        {
            var r = Run;
            if (r == null || r.Over) return State.BestFloor > 0 ? "Best: floor " + State.BestFloor + "  ·  " + State.BestScore.ToString("N0") + " pts" : "How far can you go?";
            return "Floor " + r.Floor + "  ·  " + Hearts(r) + "  ·  " + r.Score.ToString("N0") + " pts";
        }
    }

    public sealed class GauntletScreen : GameScreen
    {
        protected override string Kicker { get { return "CHALLENGES  ·  THE GAUNTLET"; } }
        protected override string Title
        {
            get
            {
                var r = GauntletFlow.Run;
                if (r == null) return "The Gauntlet";
                if (r.Over) return "Run over";
                if (r.AwaitingPerk) return "Floor " + r.FloorsWon + " cleared";
                return "Floor " + r.Floor;
            }
        }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var run = GauntletFlow.Run;
            var st = GauntletFlow.State;
            var grid = Kit.Box(Content, "gt-grid");

            if (GauntletFlow.LastReward != null) { Content.Insert(0, new RewardStrip(GauntletFlow.LastReward)); Fe.ToastAchievements(GauntletFlow.LastReward); GauntletFlow.LastReward = null; }

            if (run == null || run.Over)
            {
                var about = Kit.Panel(grid, run != null ? "THE RUN" : "HOW IT WORKS"); about.AddToClassList("gt-main");
                if (run != null)
                {
                    Kit.L(about, "Floor " + run.FloorsWon + " cleared  ·  " + run.Score.ToString("N0") + " points", "gt-big");
                    Kit.L(about, "Perks: " + (run.Perks.Count == 0 ? "none" : string.Join(", ", run.Perks.ConvertAll(id => Gauntlet.Find(id).Name))), "k-cr-note");
                }
                else
                {
                    Kit.L(about, "One game to 11 a floor, against an opponent who gets better every floor.", "gt-text");
                    Kit.L(about, "You have " + Gauntlet.StartLives + " lives: lose a floor and you play it again, a life down.", "gt-text");
                    Kit.L(about, "Win one and pick a perk: speed, power, a 2-0 head start, an extra life...", "gt-text");
                    Kit.L(about, "Floors 5, 10, 15 and 20 unlock Gauntlet kit.", "gt-text");
                }
                Records(grid, st);
                AddTo(Root, new PrimaryItem(run != null ? "New run" : "Start a run", () => { GauntletFlow.NewRun(); Fe.Replace(new GauntletScreen()); }));
                return;
            }

            if (run.AwaitingPerk)
            {
                var row = Kit.Box(Content, "k-row");
                foreach (var id in run.Offer)
                {
                    var p = Gauntlet.Find(id);
                    var pick = id;
                    AddTo(row, new CardItem(p.Name, "PERK", () => { Gauntlet.Choose(run, pick); ProfileStore.Save(); Fe.Replace(new GauntletScreen()); })
                        .Drawn(PerkArt(id), new Color32(20, 24, 16, 255), PerkArt(id) == "stripes" ? new Color(0.78f, 1f, 0.24f, 0.10f) : (Color)new Color32(200, 255, 61, 255)).Line(p.Text));
                }
                Kit.L(Content, "Pick one - it lasts for the rest of the run.", "k-cr-note");
                Status(grid, run);
                return;
            }

            // the next floor
            var next = Kit.Panel(grid, "NEXT"); next.AddToClassList("gt-main");
            string opp = Gauntlet.Opponent(run, run.Floor);
            double d = Gauntlet.Difficulty(run.Floor);
            Kit.L(next, Roster.StyleName(opp).ToUpperInvariant() + "  ·  " + Level(d), "gt-big");
            foreach (var line in Roster.StyleLines(opp)) Kit.L(next, line, "gt-text");
            var court = Courts.Find(Courts.ForCareerTier(GauntletFlow.CourtTier(run.Floor)));
            Kit.L(next, "One game to 11" + (run.HeadStart > 0 ? ", you start " + run.HeadStart + "-0 up" : "") + "  ·  " + (court != null ? court.Name : ""), "k-cr-note");
            Status(grid, run);
            AddTo(Root, new PrimaryItem("Play floor " + run.Floor, () => GauntletFlow.Play(Fe)));
            var quit = new ActionItem("Abandon the run", () => Fe.Push(new ConfirmScreen("Abandon this run? It ends here.", () => { GauntletFlow.Abandon(); Fe.Replace(new GauntletScreen()); })));
            quit.Root.AddToClassList("k-action--chip");
            AddTo(TopBar, quit);
        }

        static string Level(double d)
        {
            return d < 0.3 ? "EASY" : d < 0.5 ? "MEDIUM" : d < 0.65 ? "HARD" : d < 0.8 ? "VERY HARD" : "ELITE";
        }

        static string PerkArt(string id)
        {
            switch (id) { case "speed": return "chevrons"; case "life": return "trophy"; case "head": return "bracket"; case "golden": return "rings"; default: return "stripes"; }
        }

        static void Status(VisualElement grid, GauntletRun run)
        {
            var s = Kit.Panel(grid, "THE RUN"); s.AddToClassList("gt-side");
            Kit.L(s, GauntletFlow.Hearts(run) + (run.Golden ? "   + golden racket" : ""), "gt-hearts");
            Kit.L(s, run.Score.ToString("N0") + " points  ·  floor " + run.Floor, "gt-text");
            var a = run.Attributes;
            Kit.L(s, "Speed " + a.Speed + "  ·  Stamina " + a.Stamina + "  ·  Power " + a.Power + "  ·  Control " + a.Control, "k-cr-note");
            if (run.HeadStart > 0) Kit.L(s, "Head start " + run.HeadStart + "-0 every game", "k-cr-note");
            Kit.L(s, "Perks: " + (run.Perks.Count == 0 ? "none yet" : string.Join(", ", run.Perks.ConvertAll(id => Gauntlet.Find(id).Name))), "k-cr-note");
        }

        static void Records(VisualElement grid, GauntletState st)
        {
            var r = Kit.Panel(grid, "RECORDS"); r.AddToClassList("gt-side");
            Kit.L(r, "Best floor " + st.BestFloor, "gt-big");
            Kit.L(r, "Best score " + st.BestScore.ToString("N0") + "  ·  " + st.Runs + (st.Runs == 1 ? " run" : " runs"), "gt-text");
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }
}
