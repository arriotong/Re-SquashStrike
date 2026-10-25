// ===========================================================
// Assets/SquashGame/Runtime/UI/ScenarioScreens.cs
// P3-R6: Scenarios - the clutch situations (SquashSim.Modes.Scenarios),
// a page per group, each card one scenario with its stars. Playing one
// starts the match part-way; the match comes back here with the stars
// it earned (ScenarioFlow).
// ===========================================================

using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim.Career;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    public static class ScenarioFlow
    {
        /// <summary>The last play's outcome, shown once on the Scenarios screen.</summary>
        public static Scenario LastScenario;
        public static int LastStars = -1;
        public static bool LastBest;
        public static RewardResult LastReward;

        public static ScenarioRecord Record(string id)
        {
            var list = ProfileStore.Current.Progress.Scenarios;
            foreach (var r in list) if (r.Id == id) return r;
            return null;
        }

        public static int StarsOf(string id) { var r = Record(id); return r != null ? r.Stars : 0; }

        public static int TotalStars() { int n = 0; foreach (var r in ProfileStore.Current.Progress.Scenarios) n += r.Stars; return n; }

        public static bool GroupOpen(int g) { return TotalStars() >= Scenarios.GroupUnlock[Mathf.Clamp(g, 0, Scenarios.GroupUnlock.Length - 1)]; }

        public static MatchSettings Settings(Scenario s)
        {
            return new MatchSettings
            {
                Opponent = s.Opponent, Difficulty = (float)s.Difficulty, GamesToWin = s.GamesToWin, Scoring = s.Scoring, WarmUp = false,
                Scenario = s.Id, StartPoints0 = s.StartPoints0, StartPoints1 = s.StartPoints1, StartGames0 = s.StartGames0, StartGames1 = s.StartGames1,
                StartServer = s.StartServer, Court = Courts.ForCareerTier(s.CourtTier), CrowdFill = Courts.CrowdForCareerTier(s.CourtTier),
                P2Name = Roster.StyleName(s.Opponent).ToUpperInvariant(),
            };
        }

        public static void Play(FrontEnd fe, Scenario s) { fe.StartMatch(Settings(s)); }

        public static ScenarioResult ResultOf(MatchFacts f)
        {
            var last = f.Games != null && f.Games.Count > 0 ? f.Games[f.Games.Count - 1] : null;
            return new ScenarioResult
            {
                Won = f.Won, Rallies = f.Rallies, RalliesWon = f.RalliesWon, Tins = f.Tins, Errors = f.Errors, Winners = f.Winners, Nicks = f.Nicks,
                Kills = f.Kills, GreatHits = f.GreatHits, LongestRally = f.LongestRally, Margin = last != null ? Mathf.Abs(last[0] - last[1]) : 0,
            };
        }

        /// <summary>The scenario's match is over: its stars, the record, the reward; back to the Scenarios screen.</summary>
        public static void AfterMatch(FrontEnd fe, MatchFacts f)
        {
            var s = Scenarios.Find(fe.Match != null ? fe.Match.Settings.Scenario : null);
            if (s != null)
            {
                int stars = s.Stars(ResultOf(f));
                var rec = Record(s.Id);
                if (rec == null) { rec = new ScenarioRecord { Id = s.Id }; ProfileStore.Current.Progress.Scenarios.Add(rec); }
                int before = rec.Stars;
                rec.Plays++;
                if (stars > 0) rec.Wins++;
                rec.Stars = Mathf.Max(rec.Stars, stars);
                LastScenario = s; LastStars = stars; LastBest = stars > before;
                LastReward = Rewards.Scenario(stars, before, TotalStars());
            }
            fe.QuitToMenu();
            fe.Push(new ScenarioScreen(s != null ? s.Group : 0));
        }

        public static string StarText(int n) { return new string('★', Mathf.Clamp(n, 0, 3)) + new string('☆', 3 - Mathf.Clamp(n, 0, 3)); }
    }

    public sealed class ScenarioScreen : GameScreen
    {
        static readonly string[] Arts = { "rings", "clock", "chevrons", "trophy", "bracket", "wall", "corner", "split", "nick", "menu", "drop", "angle", "spots", "stripes", "trophy" };
        readonly int _startGroup;
        public ScenarioScreen(int group = 0) { _startGroup = group; }
        protected override string Kicker { get { return "CHALLENGES  ·  SCENARIOS"; } }
        protected override string Title { get { return "Clutch situations"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            if (ScenarioFlow.LastScenario != null)
            {
                var s = ScenarioFlow.LastScenario;
                var head = Kit.Panel(Content, s.Name.ToUpperInvariant()); head.AddToClassList("sc-result");
                Kit.L(head, ScenarioFlow.StarText(ScenarioFlow.LastStars) + (ScenarioFlow.LastStars == 0 ? "   not this time" : ScenarioFlow.LastBest ? "   new best!" : ""), "sc-result-stars");
                if (ScenarioFlow.LastReward != null) { head.Add(new RewardStrip(ScenarioFlow.LastReward)); Fe.ToastAchievements(ScenarioFlow.LastReward); }
                ScenarioFlow.LastScenario = null; ScenarioFlow.LastReward = null;
            }
            Kit.L(Content, ScenarioFlow.TotalStars() + " / " + Scenarios.MaxStars + " stars", "sc-total");
            for (int g = 0; g < Scenarios.GroupNames.Length; g++)
            {
                int group = g;
                var row = Kit.Box(Page(Scenarios.GroupNames[g].ToUpperInvariant()), "k-row");
                int k = 0;
                foreach (var sc in Scenarios.InGroup(g))
                {
                    var s = sc;
                    string art = Arts[(g * 5 + k++) % Arts.Length];                    // 2026-10-25 playtest: each card its own picture
                    var card = AddTo(row, new CardItem(s.Name, s.Score + "  ·  " + Roster.StyleName(s.Opponent).ToUpperInvariant(), () => ScenarioFlow.Play(Fe, s))
                        .Drawn(art, new Color32(18, 20, 28, 255), g == 2 ? (Color)new Color32(255, 184, 59, 255) : new Color32(200, 255, 61, 255))
                        .Line(s.Story + "\n★ win   ★ " + s.Star2Text.ToLowerInvariant() + "   ★ " + s.Star3Text.ToLowerInvariant())
                        .Badge(() => ScenarioFlow.StarText(ScenarioFlow.StarsOf(s.Id)), ScenarioFlow.StarsOf(s.Id) == 3));
                    card.Root.AddToClassList("k-card--scenario");
                    card.Locked = () => !ScenarioFlow.GroupOpen(group);
                    card.LockLine = () => Scenarios.GroupUnlock[group] + " stars to open";
                }
            }
            if (_startGroup > 0 && _startGroup < Scenarios.GroupNames.Length) Root.schedule.Execute(() => Tabs.Select(_startGroup));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Play"), (PromptBar.Btn.Tabs, "Groups"), (PromptBar.Btn.B, "Back")); }
    }
}
