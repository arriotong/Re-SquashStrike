// ===========================================================
// Assets/SquashSim.Testing/Editor/BookPuzzleTests.cs
// 2026-11-21 the rulebook's shot puzzles (BookPuzzles):
//   - every one of the 12 rules makes puzzles, and each is clear: its rule
//     applies, one BEST (the book's shot), at least one WRONG, a reason for
//     every choice
//   - the same seed, the same puzzle
//   - the rules as the book says them (a few moments by hand)
//   - the moment plays: built in a match, you strike the ball with the
//     chosen shot
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim.Modes;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class BookPuzzleTests
    {
        [Test] public void EveryRuleMakesClearPuzzles()
        {
            for (int rule = 1; rule <= 12; rule++)
            {
                int made = 0;
                for (uint seed = 1; seed <= 40; seed++)
                {
                    var p = BookPuzzles.Make(seed * 101u, rule);
                    Assert.That(p, Is.Not.Null, "rule " + rule + " seed " + seed);
                    made++;
                    Assert.That(p.Rule, Is.EqualTo(rule));
                    Assert.That(BookPuzzles.Applies(rule, p.Moment), Is.True, "its rule applies");
                    Assert.That(p.Options.Count, Is.InRange(3, 4));
                    Assert.That(p.Verdicts.FindAll(v => v == Verdict.Best).Count, Is.EqualTo(1), "one best");
                    Assert.That(p.Verdicts.Contains(Verdict.Wrong), Is.True, "a wrong one to spot");
                    Assert.That(p.Options[p.Best], Is.EqualTo(BookPuzzles.Ideal(rule, p.Moment)), "the best is the book's shot");
                    Assert.That(new HashSet<string>(p.Options).Count, Is.EqualTo(p.Options.Count), "no choice twice");
                    foreach (var r in p.Reasons) Assert.That(string.IsNullOrEmpty(r), Is.False, "every choice says why");
                    Assert.That(string.IsNullOrEmpty(p.Look), Is.False);
                }
                Assert.That(made, Is.EqualTo(40));
            }
        }

        [Test] public void TheSameSeedTheSamePuzzle()
        {
            var a = BookPuzzles.Make(424242u); var b = BookPuzzles.Make(424242u);
            Assert.That(b.Rule, Is.EqualTo(a.Rule));
            Assert.That(b.Options, Is.EqualTo(a.Options));
            Assert.That(b.Moment.OppX, Is.EqualTo(a.Moment.OppX));
            var rules = new HashSet<int>();
            for (uint s = 0; s < 400; s++) rules.Add(BookPuzzles.Make(s * 13u + 5u).Rule);
            Assert.That(rules.Count, Is.EqualTo(12), "the days go round every rule");
        }

        static Verdict J(int rule, BookMoment m, string id) { return BookPuzzles.Judge(rule, m, BookPuzzles.Choice(id), out _); }

        [Test] public void TheRulesAsTheBookSaysThem()
        {
            // 3: they're in front (and on your side) - the back corner away from them; a short ball is wrong
            var front = new BookMoment { BallX = 1.8, BallZ = 7.0, MeX = 1.3, MeZ = 7.4, OppX = 1.6, OppZ = 3.6 };
            Assert.That(J(3, front, "drive_cross"), Is.EqualTo(Verdict.Best));
            Assert.That(J(3, front, "drive_straight"), Is.EqualTo(Verdict.Wrong), "rule 5: to the side they're on");
            Assert.That(J(3, front, "drop_straight"), Is.EqualTo(Verdict.Wrong), "rule 3");
            // 4: they're behind - short; deep is wrong
            var behind = new BookMoment { BallX = 1.5, BallZ = 3.2, MeX = 1.0, MeZ = 3.6, OppX = -1.8, OppZ = 8.0 };
            Assert.That(J(4, behind, "drop_straight"), Is.EqualTo(Verdict.Best));
            Assert.That(J(4, behind, "drive_straight"), Is.EqualTo(Verdict.Wrong));
            // 2: late - high and straight; a drop is wrong; a straight drive is OK
            var late = new BookMoment { BallX = 2.6, BallZ = 9.0, MeX = 1.5, MeZ = 7.6, OppX = 0, OppZ = 5, Late = true };
            Assert.That(J(2, late, "lob_straight"), Is.EqualTo(Verdict.Best));
            Assert.That(J(2, late, "drive_straight"), Is.EqualTo(Verdict.Ok));
            Assert.That(J(2, late, "drop_straight"), Is.EqualTo(Verdict.Wrong));
            // 11: stuck on the wall - never across
            var tight = new BookMoment { BallX = 3.0, BallZ = 7.0, MeX = 2.2, MeZ = 7.4, OppX = 0, OppZ = 5.1 };
            Assert.That(J(11, tight, "drive_cross"), Is.EqualTo(Verdict.Wrong));
            Assert.That(J(11, tight, "drive_straight"), Is.EqualTo(Verdict.Best));
            // 1: in the way - only the let; a let with nobody in the way is wrong
            var inWay = new BookMoment { BallX = 2.0, BallZ = 7.0, MeX = 0.8, MeZ = 7.6, OppX = 1.4, OppZ = 7.0, InTheWay = true };
            Assert.That(J(1, inWay, "let"), Is.EqualTo(Verdict.Best));
            Assert.That(J(1, inWay, "drive_straight"), Is.EqualTo(Verdict.Wrong));
            Assert.That(J(3, front, "let"), Is.EqualTo(Verdict.Wrong), "nobody in the way");
            // 10: off the back wall - wait; rushing it is wrong
            var back = new BookMoment { BallX = 2.0, BallZ = 8.7, MeX = 1.4, MeZ = 7.5, OppX = 0, OppZ = 5, OffBackWall = true };
            Assert.That(J(10, back, "wait_straight"), Is.EqualTo(Verdict.Best));
            Assert.That(J(10, back, "rush_early"), Is.EqualTo(Verdict.Wrong));
        }

        [Test] public void TheMomentPlaysYourChosenShot()
        {
            double dt = 1.0 / 240;
            for (int rule = 2; rule <= 12; rule++)
            {
                if (rule == 9) continue;
                var p = BookPuzzles.Make(77u, rule);
                var setup = BookPuzzles.Setup(p);
                ReplayMatch.Configure(setup);
                var sim = ReplayMatch.Build(setup);
                BookPuzzles.Flight(p.Moment, out V3 bp, out V3 bv, out int b);
                sim.BeginMoment(new V3(p.Moment.MeX, 0, p.Moment.MeZ), new V3(p.Moment.OppX, 0, p.Moment.OppZ), bp, bv, b);
                Assert.That(sim.Phase, Is.EqualTo(MatchPhase.Rally));
                Assert.That(sim.Referee.ExpectedStriker, Is.EqualTo(0), "rule " + rule + ": your stroke next");
                ((OpponentBrain)sim.Bots[0]).ForceAim = BookPuzzles.AimFor(BookPuzzles.Choice(p.Options[p.Best]), p.Moment);
                bool struck = false;
                for (int i = 0; i < 240 * 4 && sim.Phase == MatchPhase.Rally && !struck; i++)
                {
                    sim.Step(dt);
                    struck = sim.Referee.LastHitter == 0;
                }
                Assert.That(struck, Is.True, "rule " + rule + ": you hit the ball from the moment (" + sim.LastOutcome?.Kind + ")");
            }
        }
    }
}
