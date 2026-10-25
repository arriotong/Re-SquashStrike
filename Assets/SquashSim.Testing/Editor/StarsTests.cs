// ===========================================================
// Assets/SquashSim.Testing/Editor/StarsTests.cs
// P6 the star players:
//   - 50 stars, ids s01..s50, best first, the No. 1 rated 99
//   - opponent ids resolve: star:, club:, the old personalities
//   - a higher rating never plays easier (Difficulty is monotonic)
//   - a new career's top 50 are the stars
//   - the name editor's override reaches every name
//   - a star match (rating, attributes, signature) replays exactly
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;
using SquashSim.Career;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class StarsTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); Stars.NameOverride = null; }

        [Test] public void FiftyStarsBestFirst()
        {
            var all = Stars.All();
            Assert.That(all.Count, Is.EqualTo(50));
            var ids = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                Assert.That(all[i].Id, Is.EqualTo("s" + (i + 1).ToString("00")));
                Assert.That(all[i].Rank, Is.EqualTo(i + 1));
                Assert.That(ids.Add(all[i].Last), Is.True, "one of each surname: " + all[i].Last);
                if (i > 0) Assert.That(all[i].Rating, Is.LessThanOrEqualTo(all[i - 1].Rating), all[i].Id);
                Assert.That(all[i].Signature.Count, Is.GreaterThan(0), all[i].Id + " has a signature shot");
            }
            Assert.That(Stars.No1.Rating, Is.EqualTo(99));
            Assert.That(Stars.Difficulty(Stars.No1.Rating), Is.EqualTo(1.0), "the No. 1 plays at the AI's very best");
        }

        [Test] public void OpponentIdsResolve()
        {
            Assert.That(Stars.Find("star:s01"), Is.SameAs(Stars.No1));
            Assert.That(Stars.Find("s01"), Is.SameAs(Stars.No1));
            Assert.That(Stars.Find("retriever"), Is.Null);
            foreach (var id in Stars.QuickLadder)
                Assert.That(Stars.Describe(id).rating, Is.GreaterThan(0), id);
            Assert.That(AiProfile.For("star:s01", 1).Name, Is.EqualTo(Stars.FullName(Stars.No1)));
            Assert.That(AiProfile.For("club:grinder:66", 0.1).Name, Does.Contain("Grinder"));
            // the old names (saves, bundled replays) still give their personalities at the difficulty given
            foreach (var old in new[] { "retriever", "attacker", "shotmaker", "grinder" })
            {
                var p = AiProfile.For(old, 0.4);
                Assert.That(p.Skill, Is.EqualTo(0.4).Within(1e-9), old);
                Assert.That(Stars.DifficultyFor(old, 0.4), Is.EqualTo(0.4), old);
            }
            var names = new HashSet<string>();
            foreach (var style in Roster.AllStyles) Assert.That(names.Add(AiProfile.ByName(style).Name), Is.True, "eight distinct styles: " + style);
        }

        [Test] public void AHigherRatingIsNeverEasier()
        {
            for (int r = 50; r < 99; r++) Assert.That(Stars.Difficulty(r + 1), Is.GreaterThanOrEqualTo(Stars.Difficulty(r)), "rating " + r);
            double last = -1;
            foreach (var id in Stars.QuickLadder)
            {
                double d = Stars.DifficultyFor(id, -1);
                Assert.That(d, Is.GreaterThan(last), "the Quick Match ladder climbs: " + id);
                last = d;
            }
        }

        [Test] public void ANewCareersTopFiftyAreTheStars()
        {
            var pros = Roster.Generate(77);
            for (int i = 0; i < 50; i++)
            {
                Assert.That(pros[i].StarId, Is.EqualTo(Stars.All()[i].Id));
                Assert.That(pros[i].OpponentId, Is.EqualTo("star:" + pros[i].StarId));
                Assert.That(Roster.RatingOf(pros[i]), Is.EqualTo(Stars.All()[i].Rating));
            }
            Assert.That(pros[50].StarId, Is.Empty);
            Assert.That(Roster.RatingOf(pros[50]), Is.LessThan(Stars.All()[49].Rating + 1));
            for (int i = 51; i < pros.Count; i++) Assert.That(pros[i].Strength, Is.LessThanOrEqualTo(pros[50].Strength + 0.02), "the generated pros below the stars");
        }

        [Test] public void TheNameEditorReachesEveryName()
        {
            var pros = Roster.Generate(5);
            Stars.NameOverride = id => id == "s01" ? ("Real", "Name") : ((string, string)?)null;
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo("Real Name"));
            Assert.That(pros[0].Name, Is.EqualTo("Real Name"));
            Assert.That(pros[0].Bug, Is.EqualTo("NAME"));
            Assert.That(AiProfile.For("star:s01", 1).Name, Is.EqualTo("Real Name"));
            Assert.That(Stars.Describe("star:s01").name, Is.EqualTo("Real Name"));
            Assert.That(pros[1].Name, Is.EqualTo(Stars.All()[1].First + " " + Stars.All()[1].Last), "the others keep theirs");
        }

        [Test] public void EveryScenarioIsPlayedAgainstAStarOfItsStyle()
        {
            foreach (var sc in SquashSim.Modes.Scenarios.All())
            {
                var st = SquashSim.Modes.Scenarios.StarOf(sc);
                Assert.That(st, Is.Not.Null, sc.Id);
                Assert.That(st.Style, Is.EqualTo(sc.Opponent), sc.Id);
                Assert.That(SquashSim.Modes.Scenarios.OpponentId(sc), Is.EqualTo("star:" + st.Id));
            }
        }

        [Test] public void AStarMatchReplaysExactly()
        {
            var s = new ReplaySetup { Opponent0 = "attacker", Difficulty0 = 0.5f, Opponent = "star:s05", GamesToWin = 1, Scoring = "par", BotSeed = 29, ParPoints = 7, Lets = true };
            ReplayMatch.DressStar(s);
            var star = Stars.Find("s05");
            Assert.That(s.Difficulty, Is.EqualTo((float)Stars.Difficulty(star.Rating)));
            Assert.That(s.Attr1.Power, Is.EqualTo(star.Attributes.Power));
            Assert.That(s.Mods1.Count, Is.EqualTo(star.Signature.Count));

            ReplayMatch.Configure(s);
            var sim = ReplayMatch.Build(s);
            var rec = new MatchRecorder(sim, s, new InputIntent[2], "test", 0);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (!rec.Finished && sim.Time < 1800) { rec.BeforeStep(); sim.Step(dt); rec.AfterStep(); }
            rec.Finish(true);
            Assert.That(sim.Phase, Is.EqualTo(MatchPhase.MatchOver));
            var back = ReplayFile.FromBytes(rec.File.ToBytes());
            Assert.That(back.Setup.Opponent, Is.EqualTo("star:s05"));
            var v = ReplaySim.Verify(back);
            Assert.That(v.Ok, Is.True, v.ToString());
        }
    }
}
