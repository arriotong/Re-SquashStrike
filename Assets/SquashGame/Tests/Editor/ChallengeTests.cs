// ===========================================================
// Assets/SquashGame/Tests/Editor/ChallengeTests.cs
// P3-R2 daily and weekly challenges - playable since 2026-10-28:
//   - every day the same three to play (quiz, puzzle, skill test), the
//     skill test's level and target from the day; an old profile's
//     missions are replaced; the weekly alternates rush and exam
//   - a quiz passed, a puzzle solved, the day's arcade level at its
//     target: each pays once; a failed quiz and a played puzzle move on
//   - the streak: consecutive days extend it, a missed day resets it
//   - the weekly pays its XP and the next weekly item
//   - the shipped shot-puzzle bank still matches the sim (each freeze is
//     a freeze moment; a puzzle plays the same twice)
// ===========================================================

using System;
using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.Progression;
using SquashSim;
using SquashSim.Modes;
using SquashSim.Replays;

namespace SquashGame.Tests
{
    public class ChallengeTests
    {
        string _dir;
        DateTime _now;

        [SetUp]
        public void Fresh()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeChallenges");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
            Challenges.Enabled = true;
            _now = new DateTime(2026, 10, 26, 10, 0, 0);          // a Monday
            Challenges.Now = () => _now;
        }

        [TearDown] public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); Challenges.Now = () => DateTime.Now; PhysicsConfig.ResetToDefaults(); }

        [Test] public void EveryDayTheThreeToPlay()
        {
            var s = Challenges.Current();
            Assert.That(s.Daily.ConvertAll(c => c.Id), Is.EqualTo(new[] { Challenges.Quiz, Challenges.Puzzle, Challenges.Skill }));
            var skill = Challenges.Today(Challenges.Skill);
            Assert.That(SquashSim.Arcade.Challenges.Find(skill.Ref), Is.Not.Null, "a real arcade level");
            Assert.That(skill.Need, Is.InRange(2, 3));
            Assert.That(Challenges.SkillWords(), Does.Contain("stars"));
            string quiz = Challenges.QuizLessonId(false);
            int puzzle = Challenges.PuzzleIndex();
            _now = _now.AddDays(1);
            Assert.That(Challenges.QuizLessonId(false), Is.Not.EqualTo(quiz), "new calls tomorrow");
            Assert.That(Challenges.PuzzleIndex(), Is.Not.EqualTo(puzzle), "a new puzzle tomorrow");
            string w0 = Challenges.Current().Weekly.Id;
            _now = _now.AddDays(7);
            Assert.That(Challenges.Current().Weekly.Id, Is.Not.EqualTo(w0), "the weekly alternates");
        }

        [Test] public void AnOldProfilesMissionsAreReplaced()
        {
            var s = Challenges.Current();
            s.Daily = new System.Collections.Generic.List<ChallengeProgress> { new ChallengeProgress { Id = "win2" }, new ChallengeProgress { Id = "nicks3" }, new ChallengeProgress { Id = "ace" } };
            s.Weekly = new ChallengeProgress { Id = "w_win10" };
            var again = Challenges.Current();
            Assert.That(again.Daily.ConvertAll(c => c.Id), Is.EqualTo(new[] { Challenges.Quiz, Challenges.Puzzle, Challenges.Skill }));
            Assert.That(Challenges.Find(again.Weekly.Id), Is.Not.Null);
        }

        [Test] public void APassedQuizPaysOnceAndAFailedOneMovesOn()
        {
            string first = Challenges.QuizLessonId(false);
            var failed = Rewards.Quiz(Challenges.QuizSize, 2, false);
            Assert.That(failed.Lines.Exists(l => l.StartsWith("Challenge done")), Is.False);
            Assert.That(Challenges.QuizLessonId(false), Is.Not.EqualTo(first), "new calls after a failed try");
            var r = Rewards.Quiz(Challenges.QuizSize, 5, true);
            Assert.That(Challenges.Today(Challenges.Quiz).Done, Is.True);
            Assert.That(r.Lines.Exists(l => l.StartsWith("Challenge done")), Is.True);
            Assert.That(r.Coins, Is.GreaterThanOrEqualTo(Rewards.DailyPulls * GearDrops.PullCost), "a daily: two pulls' worth");
            var again = Rewards.Quiz(Challenges.QuizSize, 5, true);
            Assert.That(again.Lines.Exists(l => l.StartsWith("Challenge done")), Is.False, "pays once");
            Assert.That(ProfileStore.Load().Progress.Challenges.DailiesDone, Is.EqualTo(1), "saved");
        }

        [Test] public void APuzzleSolvedCountsForTheDayAndTheRush()
        {
            int i0 = Challenges.PuzzleIndex();
            Rewards.Puzzle(false);
            Assert.That(Challenges.Today(Challenges.Puzzle).Done, Is.False);
            Assert.That(Challenges.PuzzleIndex(), Is.EqualTo(i0 + 1), "a new puzzle next");
            var r = Rewards.Puzzle(true);
            Assert.That(Challenges.Today(Challenges.Puzzle).Done, Is.True);
            Assert.That(r.Lines.Exists(l => l.StartsWith("Shot puzzle solved")), Is.True);
            var w = Challenges.Current().Weekly;
            if (w.Id == Challenges.Rush) Assert.That(w.Progress, Is.EqualTo(1), "and one for the week's rush");
        }

        [Test] public void TheSkillTestIsTodaysLevelAtItsTarget()
        {
            var c = Challenges.Today(Challenges.Skill);
            var other = SquashSim.Arcade.Challenges.All().Find(x => x.Id != c.Ref && x.Mode != SquashSim.Arcade.ArcadeMode.Drill);
            Rewards.Arcade(other.Id, false, 0, 3, 10, false);
            Assert.That(c.Done, Is.False, "another level doesn't count");
            Rewards.Arcade(c.Ref, false, 0, c.Need - 1, 10, false);
            Assert.That(c.Done, Is.False, "under the target");
            Rewards.Arcade(c.Ref, false, 0, c.Need, 10, false);
            Assert.That(c.Done, Is.True);
        }

        [Test] public void TheStreakGrowsDayByDayAndBreaksOnAMissedDay()
        {
            for (int d = 0; d < 3; d++)
            {
                Rewards.Puzzle(true);
                Assert.That(Challenges.StreakNow(), Is.EqualTo(d + 1), "day " + (d + 1));
                _now = _now.AddDays(1);
            }
            Assert.That(Rewards.Owns(Challenges.StreakItems[0]), Is.True, "3 days: the first streak item");
            Assert.That(Challenges.StreakBonus(3), Is.EqualTo(0.2).Within(1e-9));
            _now = _now.AddDays(1);                                   // a day missed
            Assert.That(Challenges.StreakNow(), Is.EqualTo(0));
            Rewards.Puzzle(true);
            Assert.That(Challenges.StreakNow(), Is.EqualTo(1), "a new streak");
            Assert.That(Challenges.Current().BestStreak, Is.EqualTo(3));
        }

        [Test] public void TheWeeklyPaysXpAndAnItem()
        {
            var s = Challenges.Current();
            var d = Challenges.Find(s.Weekly.Id);
            s.Weekly.Progress = d.Target - 1;
            var r = new RewardResult();
            Challenges.Record(r, d.Id == Challenges.Rush ? new ChallengeEvent { PuzzleSolved = true } : new ChallengeEvent { QuizCount = Challenges.ExamSize, QuizPassed = true });
            Assert.That(s.Weekly.Done, Is.True, d.Id);
            Assert.That(r.Lines.Exists(l => l.StartsWith("Weekly done") && l.EndsWith("+" + Challenges.WeeklyXp)), Is.True);
            Assert.That(r.Unlocked.ConvertAll(i => i.Id), Does.Contain(Challenges.WeeklyItems[0]));
        }

        [Test] public void EveryChallengeItemIsInTheCatalogue()
        {
            foreach (var id in Challenges.StreakItems) Assert.That(Cosmetics.Find(id)?.Unlock, Is.EqualTo(UnlockKind.Challenge), id);
            foreach (var id in Challenges.WeeklyItems) Assert.That(Cosmetics.Find(id)?.Unlock, Is.EqualTo(UnlockKind.Challenge), id);
            foreach (var d in Challenges.All()) Assert.That(d.Describe(), Does.Not.Contain("{"), d.Id);
        }

        [Test] public void AVersion2ProfileGetsChallenges()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":2,\"Progress\":{\"Xp\":500}}");
            ProfileStore.Reset();
            var p = ProfileStore.Current;
            Assert.That(p.Version, Is.EqualTo(ProfileStore.CurrentVersion));
            Assert.That(p.Progress.Xp, Is.EqualTo(500));
            Assert.That(p.Progress.Challenges, Is.Not.Null);
            Assert.That(Challenges.Current().Daily.Count, Is.EqualTo(Challenges.DailyCount));
        }

        [Test] public void TheShotPuzzleBankStillMatchesTheSim()
        {
            Assert.That(ShotPuzzleBank.Count, Is.GreaterThanOrEqualTo(200), "the bank shipped");
            var answers = new System.Collections.Generic.HashSet<string>();
            foreach (var p in ShotPuzzleBank.All()) answers.Add(p.Options[p.Best]);
            Assert.That(answers.Count, Is.EqualTo(ShotPuzzles.Shots.Length), "every shot is somebody's answer");
            double dt = 1.0 / 240;
            for (int k = 0; k < 3; k++)
            {
                var p = ShotPuzzleBank.At(k * 37);
                Assert.That(p.Rates[p.Best], Is.GreaterThanOrEqualTo(0.5));
                RallyOutcome Play()
                {
                    ReplayMatch.Configure(p.Setup());
                    var sim = ReplayMatch.Build(p.Setup());
                    while (sim.StepCount < p.Freeze) sim.Step(dt);
                    Assert.That(ShotPuzzles.IsFreezeMoment(sim), Is.True, "puzzle " + p.Seed + ": the freeze is still the moment (re-make the bank if the sim changed)");
                    ShotPuzzles.Force(sim, p, p.Options[p.Best], 0);
                    double t0 = sim.Time;
                    while (sim.Phase == MatchPhase.Rally && sim.Time - t0 < ShotPuzzles.PlayoutSeconds) sim.Step(dt);
                    return sim.LastOutcome;
                }
                var a = Play(); var b = Play();
                Assert.That(b?.Winner, Is.EqualTo(a?.Winner), "the same rally twice");
                Assert.That(b?.Kind, Is.EqualTo(a?.Kind));
            }
        }
    }
}
