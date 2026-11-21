// ===========================================================
// Assets/SquashGame/Runtime/UI/BriefingScreens.cs
// 2026-11-21 a "get ready" screen before every activity that used to
// start the moment it was clicked (the user: "some modes or challenges
// just get me in right away - sometimes I don't even know what I clicked
// and I'm already playing - add a preparation screen so you can
// understand what to do"). The same layout everywhere, like the career's:
//   a big START card (what it is), and beside it HOW TO PLAY (2-3 short
//   steps), HOW TO WIN (the goal, the stars), YOUR KEYS - and a small
//   BACK card. Enter starts it, so playing again is still one press.
// Shown on the activity's own toon world. Briefing builds one for each:
// a lesson, an arcade level or drill, free practice, a Big Moment, the
// daily shot puzzle / rules quiz / skill test, the weekly challenge.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashGame.Profile;
using UnityEngine;
using UnityEngine.UIElements;
using ArcadeChallenges = SquashSim.Arcade.Challenges;

namespace SquashGame.UI
{
    public sealed class BriefingScreen : GameScreen
    {
        public string Name { get; private set; }
        readonly MenuWorld _world;
        readonly string _kicker, _title, _line, _art, _goal, _keys, _start;
        readonly Color _a, _b;
        readonly string[] _steps;
        readonly Action _go;
        /// <summary>The START card (tests).</summary>
        public CardItem StartCard { get; private set; }

        public BriefingScreen(MenuWorld world, string kicker, string title, string line, string art, Color a, Color b, string[] steps, string goal, string keys, Action go, string start = "START")
        {
            _world = world; _kicker = kicker; _title = title; _line = line; _art = art; _a = a; _b = b; _steps = steps; _goal = goal; _keys = keys; _go = go; _start = start;
            Name = title;
        }

        public override MenuWorld Backdrop { get { return _world; } }
        public override bool Spatial { get { return true; } }
        protected override string Kicker { get { return _kicker; } }
        protected override string Title { get { return "Get ready"; } }
        protected override bool ShowChip { get { return false; } }

        protected override void Compose()
        {
            var stage = Kit.Box(Content, "bf-stage");
            StartCard = AddTo(stage, new CardItem(_title.ToUpperInvariant(), _start, () => _go(), CardSize.Hero)
                .Drawn(_art, _a, _b).Line(_line).Badge(() => "ENTER TO " + _start, true));
            StartCard.Root.AddToClassList("bf-hero");
            var col = Kit.Box(stage, "bf-col");
            var how = Kit.Box(col, "bf-panel");
            Kit.L(how, "HOW TO PLAY", "k-card-kicker");
            for (int i = 0; i < _steps.Length; i++)
            {
                var r = Kit.Box(how, "bf-step");
                Kit.L(r, (i + 1).ToString(), "bf-step-n");
                Kit.L(r, _steps[i], "bf-step-text");
            }
            if (!string.IsNullOrEmpty(_goal))
            {
                var win = Kit.Box(col, "bf-panel");
                Kit.L(win, "HOW TO WIN", "k-card-kicker");
                Kit.L(win, _goal, "bf-goal");
            }
            if (!string.IsNullOrEmpty(_keys))
            {
                var k = Kit.Box(col, "bf-panel");
                Kit.L(k, "YOUR KEYS", "k-card-kicker");
                Kit.L(k, _keys, "bf-keys");
            }
            var back = AddTo(col, new CardItem("BACK", "NOT NOW", () => Back()));
            back.Root.AddToClassList("bf-back");
        }

        public override void OnShow() { base.OnShow(); if (StartCard != null) Focus(StartCard); }
        protected override void SetPrompts() { Prompts.Set((PromptBar.Btn.Move, "Choose"), (PromptBar.Btn.A, "Select"), (PromptBar.Btn.B, "Back")); }
    }

    /// <summary>The get-ready screens, one for each kind of activity.</summary>
    public static class Briefing
    {
        static string HitKey { get { return IntentSource.Keys("Hit"); } }
        static string PlayKeys { get { return "Move: WASD / left stick  ·  Aim: mouse / right stick  ·  Hit: " + HitKey; } }
        static string Stars(int[] s) { return "★ " + s[0] + "    ★★ " + s[1] + "    ★★★ " + s[2] + " points"; }
        static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int cut = text.IndexOfAny(new[] { '.', '!', '?' });
            string s = cut > 0 && cut < 90 ? text.Substring(0, cut + 1) : text;
            return s.Length > 90 ? s.Substring(0, 87) + "..." : s;
        }

        // ---- training ----

        /// <summary>A lesson (Learn to play, the lesson trail).</summary>
        public static void Lesson(FrontEnd fe, string id)
        {
            var l = SquashSim.Academy.Lessons.Find(id);
            if (l == null) { FrontEnd.LaunchLesson(id); return; }
            var steps = new List<string>(); var keys = new List<string>();
            foreach (var s in l.Steps)
            {
                if (steps.Count < 3 && !string.IsNullOrEmpty(s.Text)) steps.Add(Short(s.Text));
                if (!string.IsNullOrEmpty(s.Keys) && !keys.Contains(s.Keys) && keys.Count < 3) keys.Add(s.Keys);
            }
            if (l.Steps.Count > 3) steps.Add("...and " + (l.Steps.Count - 3) + " more steps");
            string goal = l.HasQuiz ? "Answer the questions: get " + l.QuizPass + " of " + l.QuizCount + " right." : "Do every step. The coach tells you what's next.";
            fe.Push(new BriefingScreen(MenuWorld.Training, "LESSON", l.Title, l.Blurb, "trail", new Color32(26, 20, 40, 255), new Color32(200, 255, 61, 255),
                steps.ToArray(), goal, keys.Count > 0 ? string.Join("  ·  ", keys) : PlayKeys, () => FrontEnd.LaunchLesson(id)));
        }

        /// <summary>An arcade level or a practice drill (an arcade challenge).</summary>
        public static void Arcade(FrontEnd fe, string id, MenuWorld world, bool fromChallenges = false, string kicker = null)
        {
            var c = ArcadeChallenges.Find(id);
            if (c == null) { FrontEnd.LaunchChallenge(id); return; }
            int mode = (int)c.Mode;
            var steps = new List<string>();
            if (mode >= 0 && mode < ArcadeChallenges.ModeBlurbs.Length) steps.Add(ArcadeChallenges.ModeBlurbs[mode]);
            if (!string.IsNullOrEmpty(c.Description)) steps.Add(Short(c.Description));
            steps.Add(c.Seconds > 0 ? "You have " + Mathf.RoundToInt((float)c.Seconds) + " seconds." : c.Balls > 0 ? "You have " + c.Balls + " balls." : "Keep going as long as you can.");
            string mname = mode >= 0 && mode < ArcadeChallenges.ModeNames.Length ? ArcadeChallenges.ModeNames[mode] : "Arcade";
            fe.Push(new BriefingScreen(world, kicker ?? (mname.ToUpperInvariant() + "  ·  " + c.LevelName.ToUpperInvariant()), c.Name, mname + "  ·  " + c.LevelName,
                "rings", new Color32(20, 23, 30, 255), new Color32(200, 255, 61, 255), steps.ToArray(),
                c.Stars != null && c.Stars.Length >= 3 && c.Stars[2] > 0 ? "Score points for stars:  " + Stars(c.Stars) : "Get the best score you can.",
                PlayKeys, () => { if (fromChallenges) FrontEnd.FromChallenges = true; FrontEnd.LaunchChallenge(id); }));
        }

        /// <summary>Free practice on the court.</summary>
        public static void FreePractice(FrontEnd fe)
        {
            fe.Push(new BriefingScreen(MenuWorld.Training, "PRACTICE COURT", "Free practice", "Just you, a ball and the court.", "corner", new Color32(20, 32, 28, 255), new Color32(200, 255, 61, 255),
                new[] { "Hit the ball anywhere you like.", "Try every shot: drives, drops, lobs, boasts.", "Stop any time with Esc." },
                "No score here - practise until it feels easy.", PlayKeys, () => FrontEnd.LaunchFreePractice()));
        }

        // ---- big moments ----

        public static void Scenario(FrontEnd fe, SquashSim.Modes.Scenario s)
        {
            var g = SquashSim.Modes.Scenarios.GroupNames;
            string opp = SquashSim.Modes.Scenarios.StarOf(s) != null ? SquashSim.Career.Stars.LastName(SquashSim.Modes.Scenarios.StarOf(s)) : SquashSim.Career.Roster.StyleName(s.Opponent);
            fe.Push(new BriefingScreen(MenuWorld.Modes, "BIG MOMENTS  ·  " + g[Mathf.Clamp(s.Group, 0, g.Length - 1)].ToUpperInvariant(), s.Name, Short(s.Story),
                "rings", new Color32(16, 18, 30, 255), new Color32(255, 184, 59, 255),
                new[] { "The score is " + s.StartPoints0 + "-" + s.StartPoints1 + (s.GamesToWin > 1 ? " (games " + s.StartGames0 + "-" + s.StartGames1 + ")" : "") + " - you first.",
                        "You play " + opp + ".", "Win from here!" },
                "★ Win    ★★ " + (s.Star2Text ?? "") + "    ★★★ " + (s.Star3Text ?? ""), PlayKeys, () => ScenarioFlow.Play(fe, s)));
        }

        // ---- the challenges ----

        public static void Puzzle(FrontEnd fe, bool weekly = false)
        {
            fe.Push(new BriefingScreen(MenuWorld.Challenges, weekly ? "THIS WEEK  ·  PUZZLE RUSH" : "TODAY'S CHALLENGE", "Shot puzzle", "Pick the shot that wins the point!", "trail",
                new Color32(30, 18, 52, 255), new Color32(190, 110, 255, 255),
                new[] { "Two players have a rally. It freezes.", "Look where everyone is standing.", "Pick the shot you would play - then watch it." },
                weekly ? "Solve 5 puzzles this week." : "Pick the best shot to win today's coins.", "Choose: arrows / left stick  ·  Pick: Enter / A", () => ChallengePlay.StartPuzzle(fe)));
        }

        public static void Quiz(FrontEnd fe, bool exam)
        {
            fe.Push(new BriefingScreen(MenuWorld.Challenges, exam ? "THIS WEEK  ·  RULES EXAM" : "TODAY'S CHALLENGE", exam ? "Rules exam" : "Rules quiz", "Be the referee!", "rings",
                new Color32(16, 22, 34, 255), new Color32(111, 184, 255, 255),
                new[] { "Watch a moment from a rally.", "Was it a let, a stroke or no let? You decide.", exam ? "10 calls in a row." : "5 calls." },
                exam ? "Get 9 of 10 right." : "Get 4 of 5 right.", "Choose: arrows / left stick  ·  Answer: Enter / A", () => ChallengePlay.StartQuiz(exam)));
        }

        public static void Skill(FrontEnd fe)
        {
            var c = Progression.Challenges.Today(Progression.Challenges.Skill);
            if (c == null || string.IsNullOrEmpty(c.Ref)) return;
            var a = ArcadeChallenges.Find(c.Ref);
            if (a == null) { ChallengePlay.StartSkill(); return; }
            int mode = (int)a.Mode;
            fe.Push(new BriefingScreen(MenuWorld.Challenges, "TODAY'S CHALLENGE  ·  SKILL TEST", "Skill test", a.Name, "chevrons", new Color32(22, 30, 12, 255), new Color32(200, 255, 61, 255),
                new[] { mode >= 0 && mode < ArcadeChallenges.ModeBlurbs.Length ? ArcadeChallenges.ModeBlurbs[mode] : "Hit the targets!",
                        a.Seconds > 0 ? "You have " + Mathf.RoundToInt((float)a.Seconds) + " seconds." : a.Balls > 0 ? "You have " + a.Balls + " balls." : "Keep going as long as you can.",
                        "Stars come from your score." },
                "Get " + c.Need + " stars:  " + Stars(a.Stars), PlayKeys, ChallengePlay.StartSkill));
        }

        public static void Weekly(FrontEnd fe)
        {
            var w = Progression.Challenges.Current().Weekly;
            if (w != null && w.Id == Progression.Challenges.Exam) Quiz(fe, true); else Puzzle(fe, true);
        }
    }
}
