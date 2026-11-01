// ===========================================================
// Assets/SquashSim.Testing/Editor/SquashBookTests.cs
// 2026-11-01 the visual book's 12 rules (SquashBook):
//   - twelve rules, numbered as in the book
//   - each rule likes and forbids the shots the book says
//   - squash sense rises with rating: the No. 1 follows the book hardest
//   - a match with the book replays exactly; the file keeps its book,
//     and the book changes how the AI plays (old replays need book 0)
//   - the coach: a stroke's shot (straight, cross-court...), the tip in
//     words, and a whole match watched
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Career;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class SquashBookTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static int S(string shot) { return SquashBook.ShotIndex(shot); }

        /// <summary>A plain picture: the ball met mid-court on the right, me beside it, the opponent nowhere in particular.</summary>
        static ShotPicture Mid() { return new ShotPicture { BallX = 1.5, BallZ = 5.5, MeX = 1.2, MeZ = 5.6, OppX = 0.2, OppZ = 5.3 }; }

        [Test] public void TwelveRulesNumberedAsInTheBook()
        {
            Assert.That(SquashBook.Rules.Length, Is.EqualTo(12));
            for (int i = 0; i < 12; i++)
            {
                Assert.That(SquashBook.Rules[i].N, Is.EqualTo(i + 1));
                Assert.That(SquashBook.Rules[i].Title, Is.Not.Empty);
                Assert.That(SquashBook.Rules[i].Words, Is.Not.Empty);
            }
            Assert.That(SquashBook.Shots, Is.EqualTo(new[] { "straight", "cross", "lob", "drop", "kill", "boast" }));
        }

        [Test] public void EachRuleSaysWhatTheBookSays()
        {
            // 2: late - hit it high, never the kill
            var p = Mid(); p.Late = true;
            Assert.That(SquashBook.Broken(p, S("kill"), 2.6), Has.Member(2));
            Assert.That(SquashBook.Broken(p, S("lob"), -2.4), Has.No.Member(2));
            // 3: they're in front - to the back, not short
            p = Mid(); p.OppZ = 3.5;
            Assert.That(SquashBook.Broken(p, S("drop"), 2.6), Has.Member(3));
            Assert.That(SquashBook.Effect(3, p, S("straight"), 2.6), Is.GreaterThan(0));
            // 4: they're behind - to the front
            p = Mid(); p.BallZ = 3.2; p.MeZ = 3.4; p.OppZ = 6.2;
            Assert.That(SquashBook.Applies(4, p), Is.True);
            Assert.That(SquashBook.Effect(4, p, S("drop"), 2.6), Is.GreaterThan(0));
            // 5: they're on the left - go right
            p = Mid(); p.OppX = -1.5;
            Assert.That(SquashBook.Effect(5, p, S("straight"), 2.6), Is.GreaterThan(0));
            Assert.That(SquashBook.Effect(5, p, S("cross"), -2.6), Is.LessThan(0));
            // 6: they're on the T - straight down the wall
            p = Mid(); p.OppX = 0; p.OppZ = 5.0; p.BallZ = 6.5; p.MeZ = 6.6;
            Assert.That(SquashBook.Applies(6, p), Is.True);
            Assert.That(SquashBook.Effect(6, p, S("straight"), 2.6), Is.GreaterThan(SquashBook.Effect(6, p, S("cross"), -2.6)));
            // 7: from behind the T, no drop, kill or boast
            p = Mid(); p.BallZ = 7.5; p.MeZ = 7.6; p.OppZ = 5.0;
            foreach (var shot in new[] { "drop", "kill", "boast" }) Assert.That(SquashBook.Broken(p, S(shot), 2.6), Has.Member(7), shot);
            Assert.That(SquashBook.Broken(p, S("straight"), 2.6), Has.No.Member(7));
            // 10: off the back wall - straight
            p = Mid(); p.OffBackWall = true;
            Assert.That(SquashBook.Effect(10, p, S("straight"), 2.6), Is.GreaterThan(0));
            // 11: stuck on the side wall - never the cross-court
            p = Mid(); p.BallX = 3.0;
            Assert.That(SquashBook.Broken(p, S("cross"), -2.6), Has.Member(11));
            Assert.That(SquashBook.Broken(p, S("straight"), 2.6), Has.No.Member(11));
            // 12: their boast from the back, met in front - drop it
            p = Mid(); p.OppBoasted = true; p.BallZ = 3.0; p.MeZ = 3.2; p.OppZ = 5.0;
            Assert.That(SquashBook.Applies(12, p), Is.True);
            Assert.That(SquashBook.Effect(12, p, S("drop"), 2.6), Is.GreaterThan(0));
        }

        [Test] public void SquashSenseRisesWithRating()
        {
            double last = -1;
            for (int r = 50; r <= 99; r++) { double s = AiProfile.SenseForRating(r); Assert.That(s, Is.GreaterThanOrEqualTo(last)); last = s; }
            Assert.That(AiProfile.SenseForRating(99), Is.EqualTo(1.0));
            var no1 = AiProfile.For("star:s01", Stars.DifficultyFor("star:s01", 0));
            var club = AiProfile.For(Stars.ClubId("retriever", 62), Stars.DifficultyFor(Stars.ClubId("retriever", 62), 0));
            Assert.That(no1.Sense, Is.GreaterThan(club.Sense + 0.5), "the No. 1 follows the book, a club player often doesn't");
            // the bonus for the right shot, stuck on the wall: the No. 1's is bigger
            var p = Mid(); p.BallX = 3.0;
            Assert.That(SquashBook.Bonus(no1, p, S("straight"), 2.6) - SquashBook.Bonus(no1, p, S("cross"), -2.6),
                        Is.GreaterThan(SquashBook.Bonus(club, p, S("straight"), 2.6) - SquashBook.Bonus(club, p, S("cross"), -2.6)));
            Assert.That(AiProfile.ByName("grinder").BookSafe, Is.GreaterThan(1), "a grinder lives by the safe rules");
            Assert.That(AiProfile.ByName("magician").BookPatience, Is.LessThan(1), "a magician breaks rule 7 on purpose");
            Assert.That(AiProfile.ByName("volleyer").BookVolley, Is.GreaterThan(1));
        }

        static (ReplayFile file, MatchSimulation sim) PlayAiMatch(int book)
        {
            var s = new ReplaySetup { Opponent0 = "shotmaker", Difficulty0 = 0.5f, Opponent = "star:s01", GamesToWin = 1, Scoring = "par", BotSeed = 41, ParPoints = 7, Lets = true, Book = book };
            ReplayMatch.DressStar(s);
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var rec = new MatchRecorder(sim, s, new InputIntent[2], "test", 0);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (!rec.Finished && sim.Time < 1800) { rec.BeforeStep(); sim.Step(dt); rec.AfterStep(); }
            rec.Finish(true);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            return (rec.File, sim);
        }

        [Test] public void TheCoachKnowsTheShotAndSaysTheTipInWords()
        {
            // a drive from the right wall heading across: a cross-court; up the wall: straight
            var e = new SimEvent { Type = SimEventType.RacketContact, Technique = "drive", Point = new V3(3.0, 0.8, 7.2), OutVel = new V3(-4.0, 3.0, -16.0) };
            Assert.That(SquashCoach.Classify(e, out double endX), Is.EqualTo(S("cross")));
            Assert.That(endX, Is.LessThan(0));
            e.OutVel = new V3(0.1, 3.0, -18.0);
            Assert.That(SquashCoach.Classify(e, out _), Is.EqualTo(S("straight")));
            e.Technique = "lob"; Assert.That(SquashCoach.Classify(e, out _), Is.EqualTo(S("straight")), "a straight lob is length, not the book's cross lob");
            e.Technique = "boast"; Assert.That(SquashCoach.Classify(e, out _), Is.EqualTo(S("boast")));

            var c = new SquashCoach();
            Assert.That(c.Tip(out _), Is.Null, "nothing to say yet");
            c.Broken[11] = 4; c.BrokenBy[11, S("cross")] = 4;
            Assert.That(c.Tip(out int rule), Is.EqualTo("You hit 4 cross-courts from the side wall."));
            Assert.That(rule, Is.EqualTo(11));
            c.Reset(); c.Kept[6] = 5;
            Assert.That(c.Tip(out rule), Does.Contain("5 times"));
            Assert.That(rule, Is.EqualTo(6));
        }

        [Test] public void TheCoachWatchesAWholeMatch()
        {
            var s = new ReplaySetup { Opponent0 = "attacker", Difficulty0 = 0.4f, Opponent = "grinder", Difficulty = 0.5f, GamesToWin = 1, Scoring = "par", BotSeed = 12, ParPoints = 7, Lets = true };
            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var coach = new SquashCoach();
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            int contacts = 0;
            while (sim.Phase != MatchPhase.MatchOver && sim.Time < 1800)
            {
                sim.Step(dt);
                foreach (var e in sim.StepEvents) if (e.Type == SimEventType.RacketContact && e.PlayerIndex == 0) contacts++;
                coach.Observe(sim, sim.StepEvents, 0);
            }
            Assert.That(coach.Shots, Is.GreaterThan(10));
            Assert.That(coach.Shots, Is.LessThanOrEqualTo(contacts), "every stroke but the serves");
            int broken = 0, kept = 0; for (int n = 2; n <= 12; n++) { broken += coach.Broken[n]; kept += coach.Kept[n]; }
            Assert.That(broken + kept, Is.GreaterThan(0), "the rules spoke");
            Assert.That(coach.Tip(out int rule), Is.Not.Null);
            Assert.That(rule, Is.InRange(2, 12));
        }

        [Test] public void AMatchWithTheBookReplaysExactlyAndKeepsItsBook()
        {
            Assert.That(new ReplaySetup().Book, Is.EqualTo(AiProfile.CurrentBook), "a new match plays by the book");
            var (f1, sim1) = PlayAiMatch(1);
            var back = ReplayFile.FromBytes(f1.ToBytes());
            Assert.That(back.Setup.Book, Is.EqualTo(1));
            var v = ReplaySim.Verify(back);
            Assert.That(v.Ok, Is.True, v.ToString());
            Assert.That(((OpponentBrain)sim1.Bots[1]).Choices, Is.GreaterThan(0));

            // book 0 is the AI from before the rules: the same seed plays a different match - so an old file must keep it
            var (f0, _) = PlayAiMatch(0);
            var back0 = ReplayFile.FromBytes(f0.ToBytes());
            Assert.That(back0.Setup.Book, Is.EqualTo(0));
            Assert.That(ReplaySim.Verify(back0).Ok, Is.True);
            bool differ = f0.Rallies.Count != f1.Rallies.Count;
            for (int i = 0; !differ && i < f0.Rallies.Count; i++) differ = f0.Rallies[i].Hash != f1.Rallies[i].Hash;
            Assert.That(differ, Is.True, "the book changes how it plays");
        }
    }
}
