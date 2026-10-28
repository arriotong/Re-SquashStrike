// ===========================================================
// Assets/SquashGame/Runtime/UI/ChallengeScreens.cs
// P3-R2: the daily and weekly challenges. Playable challenges
// (2026-10-28, the user: "actual challenges ... the quizzes from the
// academy ... what shot should you hit, like a chess puzzle"):
//
//   ChallengesScreen   today's three to play - RULES QUIZ, SHOT PUZZLE,
//                      SKILL TEST - this week's, the streak
//   PuzzleScreen       the shot puzzle over the court: watch the rally,
//                      it freezes - pick your shot - see it played,
//                      then what every shot wins from there and why
//   ChallengeQuizDone  a challenge quiz passed (the Academy plays it)
// ===========================================================

using SquashGame.Progression;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SquashGame.UI
{
    static class ChallengeText
    {
        public static int DoneToday() { int n = 0; foreach (var c in Challenges.Current().Daily) if (c.Done) n++; return n; }

        public static string Streak()
        {
            int s = Challenges.StreakNow();
            return s == 0 ? "NO STREAK" : s + (s == 1 ? " DAY" : " DAYS");
        }

        /// <summary>A daily's card line: done, or what it pays.</summary>
        public static string Status(string id)
        {
            var c = Challenges.Today(id);
            if (c == null) return "";
            return c.Done ? "✓  DONE TODAY" : "+" + DailyXp() + " XP  ·  " + (Rewards.DailyPulls * GearDrops.PullCost) + " COINS";
        }

        public static int DailyXp()
        {
            int streak = Challenges.StreakNow();
            return (int)System.Math.Round(Challenges.DailyXp * (1 + Challenges.StreakBonus(System.Math.Max(1, streak + (DoneToday() > 0 ? 0 : 1)))));
        }

        public static string Week()
        {
            var w = Challenges.Current().Weekly;
            var d = w != null ? Challenges.Find(w.Id) : null;
            if (d == null) return "";
            return (w.Done ? "✓  " : "") + d.Describe() + (w.Done || d.Single ? "" : "   " + w.Progress + "/" + d.Target) + "\n" + Challenges.WeekLeft();
        }

        /// <summary>Today's venue for the puzzles (a World Tour stage, by the day).</summary>
        public static string PuzzleCourt()
        {
            var all = TourVenues.All;
            return Courts.TourPrefix + all[((Challenges.Current().Day % all.Length) + all.Length) % all.Length].Key;
        }
    }

    /// <summary>Starting the challenges' activities from anywhere (the hub's cards, the challenges screen).</summary>
    static class ChallengePlay
    {
        public static void Quiz(bool exam)
        {
            FrontEnd.FromChallenges = true;
            FrontEnd.LaunchLesson(Challenges.QuizLessonId(exam));
        }

        public static void Puzzle(FrontEnd fe)
        {
            var p = ShotPuzzleBank.At(Challenges.PuzzleIndex());
            if (p == null || fe.Match == null) return;
            fe.Match.BeginPuzzle(p, ChallengeText.PuzzleCourt());
            fe.Push(new PuzzleScreen());
        }

        public static void Skill()
        {
            var c = Challenges.Today(Challenges.Skill);
            if (c == null || string.IsNullOrEmpty(c.Ref)) return;
            FrontEnd.FromChallenges = true;
            FrontEnd.LaunchChallenge(c.Ref);
        }

        public static void Weekly(FrontEnd fe)
        {
            var w = Challenges.Current().Weekly;
            if (w != null && w.Id == Challenges.Exam) Quiz(true); else Puzzle(fe);
        }
    }

    public sealed class ChallengesScreen : GameScreen
    {
        protected override string Kicker { get { return "CHALLENGES  ·  " + Challenges.LeftToday() + " LEFT TODAY"; } }
        protected override string Title { get { return "Today's three"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var s = Challenges.Current();
            int streak = Challenges.StreakNow();
            var head = Kit.Box(Content, "ch-head");
            Kit.L(head, streak.ToString(), "ch-streak-n");
            var words = Kit.Box(head, "ch-head-words");
            Kit.L(words, (streak == 1 ? "DAY" : "DAYS") + " IN A ROW", "ch-item-k");
            Kit.L(words, "Best " + s.BestStreak + "  ·  daily XP +" + (int)(100 * Challenges.StreakBonus(System.Math.Max(1, streak))) + "%  ·  one done a day keeps it going", "ch-head-line");
            for (int i = 0; i < Challenges.StreakRewards.Length; i++)
            {
                if (s.BestStreak >= Challenges.StreakRewards[i]) continue;
                var it = Cosmetics.Find(Challenges.StreakItems[i]);
                if (it != null) Kit.L(words, "At " + Challenges.StreakRewards[i] + " days: " + it.Name + "  ·  " + Cosmetics.SlotNames[(int)it.Slot], "ch-head-line");
                break;
            }

            var row = Kit.Box(Content, "k-row");
            Card(row, Challenges.Puzzle, "SHOT PUZZLE", "A rally freezes. Your shot?", "trail", new Color32(30, 18, 52, 255), new Color32(190, 110, 255, 255), () => ChallengePlay.Puzzle(Fe), CardSize.Hero);
            Card(row, Challenges.Quiz, "RULES QUIZ", "5 calls  ·  4 right to pass", "rings", new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255), () => ChallengePlay.Quiz(false));
            Card(row, Challenges.Skill, "SKILL TEST", Challenges.SkillWords(), "chevrons", new Color32(22, 30, 12, 255), new Color32(200, 255, 61, 255), ChallengePlay.Skill);
            var w = s.Weekly;
            var wd = w != null ? Challenges.Find(w.Id) : null;
            var prize = Cosmetics.Find(Challenges.WeeklyItems[s.WeekliesDone % Challenges.WeeklyItems.Length]);
            AddTo(row, new CardItem(w != null && w.Id == Challenges.Exam ? "RULES EXAM" : "PUZZLE RUSH", "THIS WEEK  ·  " + Challenges.WeekLeft().ToUpperInvariant(), () => ChallengePlay.Weekly(Fe))
                .Drawn("clock", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255))
                .Line(() => (wd != null ? wd.Describe() + (w.Done || wd.Single ? "" : "  ·  " + w.Progress + " / " + wd.Target) : "") + (prize != null ? "\nPrize: " + prize.Name : ""))
                .Badge(() => w != null && w.Done ? "✓  DONE" : "+" + Challenges.WeeklyXp + " XP", w != null && w.Done));
        }

        void Card(VisualElement row, string id, string title, string line, string art, Color a, Color b, System.Action run, CardSize size = CardSize.M)
        {
            AddTo(row, new CardItem(title, "DAILY", run, size).Drawn(art, a, b)
                .Line(line).Badge(() => ChallengeText.Status(id), Challenges.Today(id) != null && Challenges.Today(id).Done));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Play"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the shot puzzle ----------------

    /// <summary>Over the court: watching the rally; the freeze and the choice; your shot played; the answer.</summary>
    public sealed class PuzzleScreen : GameScreen
    {
        MatchRunner.PuzzleStep? _at;
        RewardResult _reward;
        /// <summary>The stage this screen shows (taken when it is made: the runner moving on replaces the screen).</summary>
        MatchRunner.PuzzleStep _stage { get { if (_at == null) _at = Fe.Match != null ? Fe.Match.PuzzleStage : MatchRunner.PuzzleStep.Watching; return _at.Value; } }
        protected override string Kicker { get { return "SHOT PUZZLE"; } }
        protected override string Title
        {
            get
            {
                switch (_stage)
                {
                    case MatchRunner.PuzzleStep.Choosing: return "Your shot?";
                    case MatchRunner.PuzzleStep.Playing: return "Your shot: " + ShotPuzzles.Name(P.Options[Fe.Match.PuzzleChoice]);
                    case MatchRunner.PuzzleStep.Over: return P.Solves(Fe.Match.PuzzleChoice) ? "The best shot" : "Not the best shot";
                    default: return "Watch the rally";
                }
            }
        }
        protected override bool ShowChip { get { return false; } }
        ShotPuzzle P { get { return Fe.Match.Puzzle; } }
        public MatchRunner.PuzzleStep Stage { get { return _stage; } }

        protected override void Compose()
        {
            var m = Fe.Match;
            if (_stage == MatchRunner.PuzzleStep.Watching)
            {
                var box = Kit.Panel(Content, "YOU ARE IN YOUR KIT"); box.AddToClassList("pz-note");
                Kit.L(box, "An AI plays the rally for you. Watch where they are - it freezes before your shot.", "pz-text");
                return;
            }
            if (_stage == MatchRunner.PuzzleStep.Choosing)
            {
                var box = Kit.Panel(Content, "THE RALLY'S FROZEN"); box.AddToClassList("pz-note");
                Kit.L(box, "The ball is coming to you. Look where they are standing - which shot wins the rally from here?", "pz-text");
                var row = Kit.Box(Content, "k-row"); row.AddToClassList("pz-row");
                for (int i = 0; i < P.Options.Count; i++)
                {
                    int k = i;
                    AddTo(row, new CardItem(ShotPuzzles.Name(P.Options[i]).ToUpperInvariant(), "OPTION " + (char)('A' + i), () => { m.ChoosePuzzle(k); Fe.Replace(new PuzzleScreen()); }, CardSize.S)
                        .Drawn(Art(P.Options[i]), new Color32(18, 20, 26, 255), new Color32(190, 110, 255, 255)));
                }
                return;
            }
            if (_stage == MatchRunner.PuzzleStep.Playing)
            {
                var box = Kit.Panel(Content, "YOUR CHOICE"); box.AddToClassList("pz-note");
                Kit.L(box, "Played from the frozen moment, in slow motion.", "pz-text");
                return;
            }
            // ---- the answer ----
            bool solved = P.Solves(m.PuzzleChoice);
            _reward = Rewards.Puzzle(solved);
            var col = Kit.Box(Content, "pz-result");
            var st = new Stamp(solved ? "✓  BEST SHOT" : "✗  NOT THE BEST", 0.15f);
            st.AddToClassList(solved ? "k-stamp--good" : "k-stamp--bad");
            col.Add(st);
            Kit.L(col, "This time: " + (m.PuzzleResult >= 1 ? "you won the rally." : m.PuzzleResult <= 0 ? "you lost the rally." : "the rally went on."), "pz-text");
            var panel = Kit.Panel(col, "WHAT EACH SHOT WINS FROM HERE  ·  " + ShotPuzzles.Trials + " TRIES EACH");
            for (int i = 0; i < P.Options.Count; i++)
            {
                var r = Kit.Box(panel, "pz-bar-row");
                if (i == P.Best) r.AddToClassList("pz-bar-row--best");
                if (i == m.PuzzleChoice) r.AddToClassList("pz-bar-row--you");
                Kit.L(r, ShotPuzzles.Name(P.Options[i]) + (i == m.PuzzleChoice ? "  (you)" : "") + (i == P.Best ? "  ★" : ""), "pz-bar-name");
                var bar = Kit.Box(r, "k-ta-bar"); bar.AddToClassList("pz-bar");
                Kit.Box(bar, "k-ta-bar-fill").style.width = Length.Percent(100f * (float)P.Rates[i]);
                Kit.L(r, Mathf.RoundToInt(100f * (float)P.Rates[i]) + "%", "pz-bar-pct");
            }
            Kit.L(panel, P.Why, "pz-why");
            if (_reward != null && _reward.Any) { col.Add(new RewardStrip(_reward)); Fe.ToastAchievements(_reward); }
            AddTo(Root, new PrimaryItem("Next puzzle", () => { Fe.Pop(); ChallengePlay.Puzzle(Fe); }));
            var more = Kit.Box(Root, "k-ft-more");
            AddTo(more, new ActionItem("Back to challenges", BackToChallenges));
        }

        static string Art(string shot)
        {
            switch (shot) { case "drop": case "kill": return "drop"; case "boast": return "angle"; case "lob": return "trail"; default: return "corner"; }
        }

        void BackToChallenges()
        {
            Fe.QuitToMenu();
            Fe.Push(new ChallengesScreen());
        }

        public override void Tick(float dt)
        {
            var m = Fe.Match;
            if (m == null || m.Puzzle == null) return;
            if (m.PuzzleStage != _stage) Fe.Replace(new PuzzleScreen());
        }

        protected override void SetPrompts()
        {
            if (_stage == MatchRunner.PuzzleStep.Choosing) Prompts.Set((PromptBar.Btn.A, "Play this shot"), (PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.B, "Quit"));
            else if (_stage == MatchRunner.PuzzleStep.Over) Prompts.Set();                                   // like full time: the actions say it (bottom left)
            else Prompts.Set((PromptBar.Btn.B, "Quit"));
        }

        public override void Back() { BackToChallenges(); }
    }

    // ---------------- a challenge quiz, passed ----------------

    public sealed class ChallengeQuizDoneScreen : GameScreen
    {
        readonly SquashSim.Academy.Lesson _l;
        readonly int _right, _count;
        public RewardResult Reward { get; private set; }
        public ChallengeQuizDoneScreen(SquashSim.Academy.Lesson l, int right, int count)
        {
            _l = l; _right = right; _count = count;
            Reward = Rewards.Quiz(count, right, true);
        }
        protected override bool ShowChip { get { return false; } }
        protected override string Kicker { get { return "CHALLENGES  ·  " + _l.Title.ToUpperInvariant(); } }
        protected override string Title { get { return _right + " of " + _count + " right"; } }

        protected override void Compose()
        {
            var col = Kit.Box(Content, "k-cr-result");
            var st = new Stamp("✓  PASSED", 0.25f); st.AddToClassList("k-stamp--good");
            col.Add(st);
            if (Reward.Any) { col.Add(new RewardStrip(Reward)); Fe.ToastAchievements(Reward); }
            AddTo(Root, new PrimaryItem("Back to challenges", () => Fe.BackToChallenges()));
        }
        public override void Back() { Fe.BackToChallenges(); }
    }
}
