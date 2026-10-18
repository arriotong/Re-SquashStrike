// ===========================================================
// Assets/SquashSim/Academy/Lessons.cs
// P2-M3: the Academy's lessons, as data. A lesson is a list of steps;
// a step is one task with a pass condition (controls), one card of
// the rules, or one "make the call" question.
//
//   Controls  Move, Aim, Hit, Height picks the shot, Charge, Boast,
//             Serve, Get set early, Assisted or not
//   Rules     The court, Serving, How a rally ends, Scoring, Lets
// ===========================================================

using System.Collections.Generic;
using SquashSim.Arcade;

namespace SquashSim.Academy
{
    public enum Chapter { Controls, Rules }
    public enum StepSetup { NoBall, Feed, Serve, Demo }
    public enum GoalKind
    {
        ReachSpots, SprintTo, Dive, ReticleRings, FrontWallHits, ShotType, PaceOver, Boast, Serve, FaultThenServe,
        GreatHits, ModeDrive, Card, Quiz
    }
    public enum QuizKind { Wall, Serve, RallyEnd, Scoring }

    public sealed class QuizQuestion
    {
        public QuizKind Kind;
        public string Text;
        public string[] Choices;
        /// <summary>The call the question is written for; RulesQuiz.Answer must agree (tests).</summary>
        public string Intended;
        // a shot: from (Px, 1.0, Pz) to the front wall at (Wx, Wy) at Speed
        public double Px, Pz, Wx, Wy, Speed = 18;
        // scoring: the system and who won the rally
        public bool English, ServerWins;
        public string Explain;
    }

    public sealed class LessonStep
    {
        public string Text, Keys;
        public StepSetup Setup = StepSetup.NoBall;
        public GoalKind Goal;
        public int Count = 1;
        public string Shot;                       // ShotType / PaceOver: the shot type the game must play
        public double MinZ = -1, MaxZ = 99;       // ShotType: where its first bounce must be
        public double Pace;                       // PaceOver
        public string ServeKind;                  // Serve: "lob serve" / "hard serve" (null = either)
        public bool Assisted;                     // ModeDrive
        public double Seconds;                    // SprintTo: time to get there
        public double StartX = double.NaN, StartZ = double.NaN;
        public readonly List<Target> Targets = new List<Target>();
        public string Highlight;                  // rules cards: court lines to light up
        public QuizQuestion Question;

        public LessonStep Spot(double x, double z, double r = 0.55) { Targets.Add(Target.StandOn(x, z, r)); return this; }
        public LessonStep Ring(double x, double y, double r) { Targets.Add(Target.Ring(x, y, r)); return this; }
        public LessonStep From(double x, double z) { StartX = x; StartZ = z; return this; }
    }

    public sealed class Lesson
    {
        public string Id, Title, Blurb;
        public Chapter Chapter;
        public readonly List<LessonStep> Steps = new List<LessonStep>();
        public bool HasQuiz { get { foreach (var s in Steps) if (s.Goal == GoalKind.Quiz) return true; return false; } }
    }

    public static class Lessons
    {
        static List<Lesson> _all;

        static Lesson L(string id, Chapter ch, string title, string blurb) { return new Lesson { Id = id, Chapter = ch, Title = title, Blurb = blurb }; }

        static LessonStep S(GoalKind goal, string text, string keys, StepSetup setup = StepSetup.NoBall, int count = 1)
        {
            return new LessonStep { Goal = goal, Text = text, Keys = keys, Setup = setup, Count = count };
        }

        static LessonStep Card(string text, string highlight = null) { return new LessonStep { Goal = GoalKind.Card, Text = text, Highlight = highlight }; }

        static LessonStep Q(QuizQuestion q) { return new LessonStep { Goal = GoalKind.Quiz, Setup = StepSetup.Demo, Question = q, Text = q.Text }; }

        public static List<Lesson> All()
        {
            if (_all != null) return _all;
            _all = new List<Lesson>();
            string feedKeys = "hold LMB / A, let go as the ball arrives";

            // ---------------- controls ----------------
            var move = L("move", Chapter.Controls, "Move", "Get around the court: walk, sprint, dive.");
            move.Steps.Add(S(GoalKind.ReachSpots, "Walk to the lit spot.", "WASD / left stick", count: 3).Spot(-2.3, 1.6).Spot(2.3, 1.6).Spot(0, 5.0));
            var sprint = S(GoalKind.SprintTo, "Hold sprint and get to the far corner in time.", "Shift / LB", count: 1).Spot(-2.3, 8.3).From(2.3, 2.0);
            sprint.Seconds = 4.0;
            move.Steps.Add(sprint);
            move.Steps.Add(S(GoalKind.Dive, "Dive - it throws you the way you're moving. Dive twice.", "Space / B", count: 2));
            _all.Add(move);

            var aim = L("aim", Chapter.Controls, "Aim", "The ring on the wall is where your shot goes.");
            aim.Steps.Add(S(GoalKind.ReticleRings, "Put your ring inside the target and hold it there.", "mouse / right stick", count: 3)
                .Ring(-1.5, 2.5, 0.45).Ring(1.8, 1.2, 0.45).Ring(0.0, 3.4, 0.45));
            _all.Add(aim);

            var hit = L("hit", Chapter.Controls, "Hit", "Hold to get ready, let go as the ball reaches you.");
            hit.Steps.Add(S(GoalKind.FrontWallHits, "Get to the ball and hit it back to the front wall.", feedKeys, StepSetup.Feed, 3));
            _all.Add(hit);

            var height = L("height", Chapter.Controls, "Height picks the shot", "Aim at the middle for a drive, high for a lob, low for a drop.");
            var drive = S(GoalKind.ShotType, "Aim at the middle of the wall: a drive.", "ring about 1.8 m up", StepSetup.Feed); drive.Shot = "drive"; height.Steps.Add(drive);
            var lob = S(GoalKind.ShotType, "Aim high: a lob that lands deep.", "ring above 3 m", StepSetup.Feed); lob.Shot = "lob"; lob.MinZ = 6.5; height.Steps.Add(lob);
            var drop = S(GoalKind.ShotType, "Aim low and let go quickly: a drop that lands short (a long hold makes it a kill).", "ring just above the tin, quick press", StepSetup.Feed); drop.Shot = "drop"; drop.MaxZ = 4.0; height.Steps.Add(drop);
            _all.Add(height);

            var charge = L("charge", Chapter.Controls, "Charge", "Hold longer for more pace. Low and charged is a kill.");
            var hard = S(GoalKind.PaceOver, "Hold a little longer before you let go: a hard drive.", "hold LMB / A ~0.5 s", StepSetup.Feed); hard.Shot = "drive"; hard.Pace = 32; charge.Steps.Add(hard);
            var kill = S(GoalKind.ShotType, "Aim low and charge: a kill.", "low ring + hold", StepSetup.Feed); kill.Shot = "kill"; kill.MaxZ = 5.0; charge.Steps.Add(kill);
            _all.Add(charge);

            var boast = L("boast", Chapter.Controls, "Boast", "Off the side wall first, across to the front.");
            boast.Steps.Add(S(GoalKind.Boast, "Tap boast, then hit: the ring moves to where it lands.", "E / RB, then hit", StepSetup.Feed, 2));
            _all.Add(boast);

            var serve = L("serve", Chapter.Controls, "Serve", "Tap for a lob serve, hold for a hard serve.");
            var ls = S(GoalKind.Serve, "Tap to serve a lob into the opposite back quarter.", "tap LMB / A", StepSetup.Serve); ls.ServeKind = "lob serve"; serve.Steps.Add(ls);
            var hs = S(GoalKind.Serve, "Hold, then let go: a hard serve, still in the back quarter. Aim nearer your own side - it travels flatter.", "hold LMB / A", StepSetup.Serve); hs.ServeKind = "hard serve"; serve.Steps.Add(hs);
            serve.Steps.Add(S(GoalKind.FaultThenServe, "Aim under the service line and serve - see the fault. Then serve a good one.", "the ring says FAULT", StepSetup.Serve));
            _all.Add(serve);

            var early = L("early", Chapter.Controls, "Get set early", "Be there before the ball and let go on time: the ring flashes.");
            early.Steps.Add(S(GoalKind.GreatHits, "Two great hits: get set early, let go as it arrives.", "watch the ring flash", StepSetup.Feed, 2));
            _all.Add(early);

            var mode = L("mode", Chapter.Controls, "Assisted or not", "Non-assisted: exactly where you aim. Assisted: the game keeps your drives clean.");
            var na = S(GoalKind.ModeDrive, "Non-assisted: hit a straight drive down your side wall.", "aim right by the side wall", StepSetup.Feed); na.Assisted = false; mode.Steps.Add(na);
            var aa = S(GoalKind.ModeDrive, "Assisted: the same shot - the game keeps it off the wall.", "aim right by the side wall", StepSetup.Feed); aa.Assisted = true; mode.Steps.Add(aa);
            mode.Steps.Add(Card("Pick the one you like - you can change it any time in Settings > Controls."));
            _all.Add(mode);

            // ---------------- rules ----------------
            var court = L("court", Chapter.Rules, "The court", "The walls, the lines, in and out.");
            court.Steps.Add(Card("Every shot must reach the front wall - above the tin, the strip along the bottom.", "tin"));
            court.Steps.Add(Card("The out line runs across the top of the front wall, down the side walls and along the back wall. On or above a line is out.", "outlines"));
            court.Steps.Add(Card("On the floor: the short line, the half-court line, and a service box on each side.", "floor"));
            court.Steps.Add(Q(new QuizQuestion { Intended = "Out", Kind = QuizKind.Wall, Text = "In or out?", Choices = new[] { "In", "Out" }, Px = 0.5, Pz = 6.0, Wx = 0.5, Wy = 4.85, Speed = 20, Explain = "Above the front-wall out line." }));
            court.Steps.Add(Q(new QuizQuestion { Intended = "In", Kind = QuizKind.Wall, Text = "In or out?", Choices = new[] { "In", "Out" }, Px = -0.6, Pz = 6.0, Wx = -1.0, Wy = 2.4, Speed = 20, Explain = "Between the tin and the out line." }));
            court.Steps.Add(Q(new QuizQuestion { Intended = "In", Kind = QuizKind.Wall, Text = "In or out?", Choices = new[] { "In", "Out" }, Px = 1.0, Pz = 6.0, Wx = 1.5, Wy = 4.25, Speed = 20, Explain = "High - but under the line." }));
            _all.Add(court);

            var serving = L("serving", Chapter.Rules, "Serving", "Where to stand, where to hit, where it must land.");
            serving.Steps.Add(Card("Serve from a service box, a foot inside it.", "boxes"));
            serving.Steps.Add(Card("Hit the front wall above the service line...", "serviceline"));
            serving.Steps.Add(Card("...and land it in the back quarter on the other side. It may touch the side wall first.", "quarters"));
            serving.Steps.Add(Q(new QuizQuestion { Intended = "Good serve", Kind = QuizKind.Serve, Text = "Good serve or fault?", Choices = new[] { "Good serve", "Fault" }, Px = 2.4, Pz = 5.0, Wx = 0.4, Wy = 2.8, Speed = 19, Explain = "Above the line, into the far back quarter." }));
            serving.Steps.Add(Q(new QuizQuestion { Intended = "Fault", Kind = QuizKind.Serve, Text = "Good serve or fault?", Choices = new[] { "Good serve", "Fault" }, Px = 2.4, Pz = 5.0, Wx = 0.5, Wy = 1.35, Speed = 19, Explain = "Under the service line." }));
            serving.Steps.Add(Q(new QuizQuestion { Intended = "Fault", Kind = QuizKind.Serve, Text = "Good serve or fault?", Choices = new[] { "Good serve", "Fault" }, Px = 2.4, Pz = 5.0, Wx = 2.6, Wy = 2.4, Speed = 19, Explain = "It lands on the server's own side." }));
            _all.Add(serving);

            var ends = L("ends", Chapter.Rules, "How a rally ends", "Down, out, not up.");
            ends.Steps.Add(Card("Down: the ball hits the tin, or the floor before the front wall.", "tin"));
            ends.Steps.Add(Card("Out: on or above an out line - or the ceiling.", "outlines"));
            ends.Steps.Add(Card("Not up: it bounced twice before the player got to it. Whoever played the last good shot wins the rally."));
            ends.Steps.Add(Q(new QuizQuestion { Intended = "Down", Kind = QuizKind.RallyEnd, Text = "Down, out, or good?", Choices = new[] { "Down", "Out", "Good" }, Px = 1.0, Pz = 6.0, Wx = 0.0, Wy = 0.25, Speed = 20, Explain = "Into the tin." }));
            ends.Steps.Add(Q(new QuizQuestion { Intended = "Good", Kind = QuizKind.RallyEnd, Text = "Down, out, or good?", Choices = new[] { "Down", "Out", "Good" }, Px = 1.0, Pz = 6.0, Wx = -1.2, Wy = 1.6, Speed = 20, Explain = "A good shot." }));
            ends.Steps.Add(Q(new QuizQuestion { Intended = "Out", Kind = QuizKind.RallyEnd, Text = "Down, out, or good?", Choices = new[] { "Down", "Out", "Good" }, Px = 1.0, Pz = 6.0, Wx = -1.0, Wy = 4.9, Speed = 20, Explain = "Over the out line." }));
            _all.Add(ends);

            var scoring = L("scoring", Chapter.Rules, "Scoring", "PAR-11 and English.");
            scoring.Steps.Add(Card("PAR-11: every rally scores a point, whoever served. First to 11 - win by two."));
            scoring.Steps.Add(Card("English: only the server scores. Win a rally as the receiver and you win the serve. First to 9."));
            scoring.Steps.Add(Q(new QuizQuestion { Intended = "Receiver", Kind = QuizKind.Scoring, Text = "PAR-11: the receiver wins the rally. Who scores?", Choices = new[] { "Server", "Receiver", "Nobody" }, English = false, ServerWins = false, Explain = "In PAR every rally scores." }));
            scoring.Steps.Add(Q(new QuizQuestion { Intended = "Nobody", Kind = QuizKind.Scoring, Text = "English: the receiver wins the rally. Who scores?", Choices = new[] { "Server", "Receiver", "Nobody" }, English = true, ServerWins = false, Explain = "The receiver just wins the serve." }));
            scoring.Steps.Add(Q(new QuizQuestion { Intended = "Server", Kind = QuizKind.Scoring, Text = "English: the server wins the rally. Who scores?", Choices = new[] { "Server", "Receiver", "Nobody" }, English = true, ServerWins = true, Explain = "Only the server scores in English." }));
            _all.Add(scoring);

            var lets = L("lets", Chapter.Rules, "Lets & strokes", "When a player is in the way.");
            lets.Steps.Add(Card("Interference: your opponent is in the way of your swing or your shot."));
            lets.Steps.Add(Card("A let: the rally is played again. A stroke: you win the point, because they stopped a winning shot."));
            lets.Steps.Add(Card("In re:SquashStrike lets are switched off for now - they come back with the referee's rework."));
            _all.Add(lets);

            return _all;
        }

        public static Lesson Find(string id)
        {
            foreach (var l in All()) if (l.Id == id) return l;
            return null;
        }

        /// <summary>The lesson after this one (null after the last).</summary>
        public static Lesson Next(Lesson l)
        {
            var all = All();
            int i = all.IndexOf(l);
            return i >= 0 && i + 1 < all.Count ? all[i + 1] : null;
        }
    }
}
