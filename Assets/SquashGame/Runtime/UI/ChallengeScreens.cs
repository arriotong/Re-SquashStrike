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
            return c.Done ? "✓  DONE TODAY" : "WIN " + (Rewards.DailyPulls * GearDrops.PullCost) + " COINS";             // (the kid test: no "XP")
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
        public static void StartQuiz(bool exam)
        {
            FrontEnd.FromChallenges = true;
            FrontEnd.LaunchLesson(Challenges.QuizLessonId(exam));
        }

        public static void StartPuzzle(FrontEnd fe)
        {
            var p = BookPuzzles.Make(Challenges.PuzzleSeed());      // 2026-11-21 the rulebook's puzzle, made on the spot
            if (p == null || fe.Match == null) return;
            fe.Match.BeginPuzzle(p, ChallengeText.PuzzleCourt());
            fe.Push(new PuzzleScreen());
        }

        public static void StartSkill()
        {
            var c = Challenges.Today(Challenges.Skill);
            if (c == null || string.IsNullOrEmpty(c.Ref)) return;
            FrontEnd.FromChallenges = true;
            FrontEnd.LaunchChallenge(c.Ref);
        }

        public static void Weekly(FrontEnd fe)
        {
            var w = Challenges.Current().Weekly;
            Briefing.Weekly(fe);   // 2026-11-21 a get-ready screen first
        }
    }

    public sealed class ChallengesScreen : GameScreen
    {
        public override MenuWorld Backdrop { get { return MenuWorld.Challenges; } }   // 2026-11-20 its toon world (Dioramas)
        protected override string Kicker { get { return "CHALLENGES  ·  NEW ONES IN " + Challenges.LeftToday(); } }
        protected override string Title { get { return "Today's games"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var s = Challenges.Current();
            int streak = Challenges.StreakNow();
            var head = Kit.Box(Content, "ch-head");
            Kit.L(head, streak.ToString(), "ch-streak-n");
            var words = Kit.Box(head, "ch-head-words");
            Kit.L(words, (streak == 1 ? "DAY" : "DAYS") + " IN A ROW", "ch-item-k");
            // 2026-10-30 (the kid test): no "daily XP +0%" - just what keeps the streak going
            Kit.L(words, "Play one every day to keep it going!", "ch-head-line");
            for (int i = 0; i < Challenges.StreakRewards.Length; i++)
            {
                if (s.BestStreak >= Challenges.StreakRewards[i]) continue;
                var it = Cosmetics.Find(Challenges.StreakItems[i]);
                if (it != null) Kit.L(words, Challenges.StreakRewards[i] + " days in a row wins: " + it.Name + " (" + Cosmetics.SlotNames[(int)it.Slot].ToLowerInvariant() + ")", "ch-head-line");
                break;
            }

            var row = Kit.Box(Content, "k-row");
            Card(row, Challenges.Puzzle, "SHOT PUZZLE", "Pick the shot that wins the point!", "trail", new Color32(30, 18, 52, 255), new Color32(190, 110, 255, 255), () => Briefing.Puzzle(Fe), CardSize.Hero);
            Card(row, Challenges.Quiz, "RULES QUIZ", "Be the referee: 5 questions", "rings", new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255), () => Briefing.Quiz(Fe, false));
            Card(row, Challenges.Skill, "SKILL TEST", Challenges.SkillWords(), "chevrons", new Color32(22, 30, 12, 255), new Color32(200, 255, 61, 255), () => Briefing.Skill(Fe));
            var w = s.Weekly;
            var wd = w != null ? Challenges.Find(w.Id) : null;
            var prize = Cosmetics.Find(Challenges.WeeklyItems[s.WeekliesDone % Challenges.WeeklyItems.Length]);
            AddTo(row, new CardItem(w != null && w.Id == Challenges.Exam ? "RULES EXAM" : "PUZZLE RUSH", "THIS WEEK  ·  " + Challenges.WeekLeft().ToUpperInvariant(), () => ChallengePlay.Weekly(Fe))
                .Drawn("clock", new Color32(26, 22, 14, 255), new Color32(255, 184, 59, 255))
                .Line(() => (wd != null ? wd.Describe() + (w.Done || wd.Single ? "" : "  ·  " + w.Progress + " / " + wd.Target) : ""))
                .Badge(() => w != null && w.Done ? "✓  DONE" : prize != null ? "WIN: " + prize.Name.ToUpperInvariant() : "BIG PRIZE", w != null && w.Done));
        }

        void Card(VisualElement row, string id, string title, string line, string art, Color a, Color b, System.Action run, CardSize size = CardSize.M)
        {
            AddTo(row, new CardItem(title, "TODAY", run, size).Drawn(art, a, b)
                .Line(line).Badge(() => ChallengeText.Status(id), Challenges.Today(id) != null && Challenges.Today(id).Done));
        }

        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.A, "Play"), (PromptBar.Btn.B, "Back")); }
    }

    // ---------------- the shot puzzle ----------------

    /// <summary>2026-11-21 the book's shot puzzle (BookPuzzles) over the court: the moment frozen - a top-down picture like the
    /// book's and four choices (a shot and where it goes); your shot played out, slow; the verdict (best / OK / wrong), the
    /// rule's page and why each choice was good or not.</summary>
    public sealed class PuzzleScreen : GameScreen
    {
        MatchRunner.PuzzleStep? _at;
        RewardResult _reward;
        /// <summary>The stage this screen shows (taken when it is made: the runner moving on replaces the screen).</summary>
        MatchRunner.PuzzleStep _stage { get { if (_at == null) _at = Fe.Match != null ? Fe.Match.PuzzleStage : MatchRunner.PuzzleStep.Choosing; return _at.Value; } }
        public override bool Spatial { get { return true; } }
        protected override string Kicker { get { return "SHOT PUZZLE  ·  THE RULEBOOK"; } }
        protected override string Title
        {
            get
            {
                switch (_stage)
                {
                    case MatchRunner.PuzzleStep.Choosing: return "What do you do?";
                    case MatchRunner.PuzzleStep.Playing: return BookPuzzles.Name(P.Options[Fe.Match.PuzzleChoice]);
                    case MatchRunner.PuzzleStep.Over: return P.VerdictOf(Fe.Match.PuzzleChoice) == Verdict.Best ? "The book's shot!" : P.VerdictOf(Fe.Match.PuzzleChoice) == Verdict.Ok ? "Good - but not the best" : "Not this time";
                    default: return "Shot puzzle";
                }
            }
        }
        protected override bool ShowChip { get { return false; } }
        BookPuzzle P { get { return Fe.Match.Puzzle; } }
        public MatchRunner.PuzzleStep Stage { get { return _stage; } }

        protected override void Compose()
        {
            var m = Fe.Match;
            if (_stage == MatchRunner.PuzzleStep.Choosing)
            {
                var stage = Kit.Box(Content, "pz2-stage");
                var look = Kit.Box(stage, "pz2-look");
                Kit.L(look, "THE MOMENT", "k-card-kicker");
                var pic = Kit.Box(look, "pz2-pic"); pic.Add(new RuleDiagram(P.Moment));
                var key = Kit.Box(look, "pz2-key");
                Kit.L(key, "●  you", "pz2-key-you"); Kit.L(key, "●  them", "pz2-key-them"); Kit.L(key, "●  the ball", "pz2-key-ball");
                Kit.L(look, P.Look, "pz2-look-text");
                var grid = Kit.Box(stage, "pz2-choices");
                for (int i = 0; i < P.Options.Count; i++)
                {
                    int k = i;
                    var c = BookPuzzles.Choice(P.Options[i]);
                    var card = AddTo(grid, new CardItem(c.Name.ToUpperInvariant(), "CHOICE " + (char)('A' + i), () => { m.ChoosePuzzle(k); Fe.Replace(new PuzzleScreen()); })
                        .Drawn(Art(c), new Color32(18, 20, 26, 255), new Color32(190, 110, 255, 255)));
                    card.Root.AddToClassList("pz2-card");
                }
                return;
            }
            if (_stage == MatchRunner.PuzzleStep.Playing)
            {
                var box = Kit.Box(Content, "pz2-note");
                Kit.L(box, "YOUR CHOICE", "k-card-kicker");
                Kit.L(box, "Played from the moment, in slow motion.", "pz2-note-text");
                return;
            }
            // ---- the answer ----
            int pick = m.PuzzleChoice;
            var v = P.VerdictOf(pick);
            _reward = Rewards.Puzzle(v);
            var res = Kit.Box(Content, "pz2-stage");
            var left = Kit.Box(res, "pz2-verdict");
            var st = new Stamp(v == Verdict.Best ? "★  BEST" : v == Verdict.Ok ? "✓  OK" : "✗  WRONG", 0.15f);
            st.AddToClassList(v == Verdict.Wrong ? "k-stamp--bad" : "k-stamp--good");
            left.Add(st);
            var chosen = BookPuzzles.Choice(P.Options[pick]);
            Kit.L(left, chosen != null && chosen.Let ? "You stopped and asked for a let." :
                        "This time: " + (m.PuzzleResult >= 1 ? "you won the rally." : m.PuzzleResult <= 0 ? "you lost the rally." : "the rally went on."), "pz2-note-text");
            var list = Kit.Box(left, "pz2-list");
            for (int i = 0; i < P.Options.Count; i++)
            {
                var row = Kit.Box(list, "pz2-row");
                var vi = P.Verdicts[i];
                row.AddToClassList(vi == Verdict.Best ? "pz2-row--best" : vi == Verdict.Ok ? "pz2-row--ok" : "pz2-row--wrong");
                if (i == pick) row.AddToClassList("pz2-row--you");
                var head = Kit.Box(row, "pz2-row-head");
                Kit.L(head, vi == Verdict.Best ? "BEST" : vi == Verdict.Ok ? "OK" : "WRONG", "pz2-chip");
                Kit.L(head, BookPuzzles.Name(P.Options[i]) + (i == pick ? "   (you)" : ""), "pz2-row-name");
                Kit.L(row, P.Reasons[i], "pz2-row-why");
            }
            if (_reward != null && _reward.Any) { left.Add(new RewardStrip(_reward)); Fe.ToastAchievements(_reward); }
            // the rule's page, as the book has it
            var rule = SquashSim.SquashBook.Get(P.Rule);
            var page = Kit.Box(res, "pz2-page");
            Kit.L(page, "THE RULEBOOK  ·  RULE " + rule.N, "k-card-kicker");
            Kit.L(page, rule.Title, "pz2-page-title");
            var rp = Kit.Box(page, "pz2-pic"); rp.AddToClassList("pz2-pic--page"); rp.Add(new RuleDiagram(P.Rule));
            Kit.L(page, rule.Words, "pz2-page-words");
            var next = AddTo(Root, new PrimaryItem("Next puzzle", () => { Fe.Pop(); ChallengePlay.StartPuzzle(Fe); }));
            var more = Kit.Box(Root, "k-ft-more");
            AddTo(more, new ActionItem("Back to challenges", BackToChallenges));
        }

        static string Art(BookChoice c)
        {
            if (c.Let) return "split";
            if (c.Move) return "corner";
            if (c.Wait) return "clock";
            if (c.Volley) return "chevrons";
            if (c.Boast) return "angle";
            if (c.High) return "trail";
            if (c.Front) return "drop";
            return "corner";
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
            if (_stage == MatchRunner.PuzzleStep.Choosing) Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Do this"), (PromptBar.Btn.B, "Quit"));
            else if (_stage == MatchRunner.PuzzleStep.Over) Prompts.Set();
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
