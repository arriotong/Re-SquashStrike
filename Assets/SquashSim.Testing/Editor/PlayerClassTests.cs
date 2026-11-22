// ===========================================================
// Assets/SquashSim.Testing/Editor/PlayerClassTests.cs
// 2026-11-22 the player classes (PlayerClasses): five, each its attributes
// and cards on top of the rest; read back from a pro's attributes; carried
// in a match's setup (and so its replay).
// MatchBench --classes (60 games each, an attacker v a retriever):
// points won Power 62.7%, Accuracy 62.1%, Speed 65.2%, Touch 60.1%,
// All-rounder 63.9%.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class PlayerClassTests
    {
        [Test] public void FiveClassesEachWithAStrengthAWeaknessAndAPerk()
        {
            Assert.That(PlayerClasses.All.Length, Is.EqualTo(35), "the five, and 30 more (2026-11-22 evening)");
            foreach (var c in PlayerClasses.All)
            {
                Assert.That(c.Strengths.Length, Is.EqualTo(2), c.Id);
                Assert.That(c.Weakness, Is.Not.Empty, c.Id);
                Assert.That(c.Perk, Is.Not.Empty, c.Id);
                Assert.That(PlayerClasses.Find(c.Id), Is.SameAs(c));
            }
            var p = PlayerClasses.Attributes(null, "power");
            Assert.That(p.PowerScale, Is.GreaterThan(1.1), "Power hits harder");
            Assert.That(p.ControlScale, Is.GreaterThan(1.0), "and aims looser");
            var a = PlayerClasses.Attributes(null, "accuracy");
            Assert.That(a.ControlScale, Is.LessThan(0.75), "Accuracy aims tighter");
            Assert.That(PlayerClasses.Attributes(null, "speed").SpeedScale, Is.GreaterThan(1.05));
            Assert.That(PlayerClasses.Attributes(null, "allround").DrainScale, Is.LessThan(0.95), "the All-rounder tires slower");
        }

        [Test] public void AClassAddsToWhatYouHave()
        {
            var career = new AthleteAttributes { Speed = 7, Power = 9.5, Control = 4, Stamina = 6 };
            var p = PlayerClasses.Attributes(career, "power");
            Assert.That(p.Power, Is.EqualTo(AthleteAttributes.Max), "clamped at the top");
            Assert.That(career.Power, Is.EqualTo(9.5), "the original untouched");
            var mods = PlayerClasses.Mods(new List<RunMod> { new RunMod { Id = "nick", Level = 1 }, new RunMod { Id = "special", Level = 1 } }, "accuracy");
            Assert.That(mods.Find(m => m.Id == "nick").Level, Is.EqualTo(2), "the higher level of a card held twice");
            Assert.That(mods.Exists(m => m.Id == "special"), Is.True, "the rest kept");
            Assert.That(mods.Exists(m => m.Id == "rail"), Is.True);
            Assert.That(PlayerClasses.Mods(null, "allround"), Is.Null, "nothing to add: no list");
            Assert.That(PlayerClasses.Mods(null, "nope"), Is.Null, "an unknown class: nothing");
        }

        [Test] public void APlayersClassIsReadFromTheirAthlete()
        {
            Assert.That(PlayerClasses.ReadAs(new AthleteAttributes { Power = 8.5, Speed = 6 }), Is.EqualTo("power"));
            Assert.That(PlayerClasses.ReadAs(new AthleteAttributes { Speed = 8.5 }), Is.EqualTo("speed"));
            Assert.That(PlayerClasses.ReadAs(new AthleteAttributes { Control = 8 }), Is.EqualTo("accuracy"));
            Assert.That(PlayerClasses.ReadAs(new AthleteAttributes { Control = 8 }, new List<RunMod> { new RunMod { Id = "vdrop", Level = 2 } }), Is.EqualTo("touch"));
            Assert.That(PlayerClasses.ReadAs(new AthleteAttributes()), Is.EqualTo("allround"));
            foreach (var s in SquashSim.Career.Stars.All()) Assert.That(PlayerClasses.Find(PlayerClasses.ReadAs(s.Attributes, s.Signature)), Is.Not.Null, s.Id);
        }

        /// <summary>2026-11-22 evening (the user: "more different player classes ... maybe like 30 - 40"): 35 classes, each its own, in
        /// one of the five families, with real cards; and 100 Quick Match opponents (50 named club players, the 50 pros), each with one.</summary>
        [Test] public void ThirtyFiveClassesAndAHundredOpponents()
        {
            Assert.That(PlayerClasses.All.Length, Is.InRange(30, 40));
            var ids = new HashSet<string>(); var names = new HashSet<string>();
            string[] families = { "power", "accuracy", "speed", "touch", "allround" };
            foreach (var d in PlayerClasses.All)
            {
                Assert.That(ids.Add(d.Id), Is.True, "unique id " + d.Id);
                Assert.That(names.Add(d.Name), Is.True, "unique name " + d.Name);
                Assert.That(System.Array.IndexOf(families, d.Family), Is.GreaterThanOrEqualTo(0), d.Id + " has a family");
                Assert.That(d.Strengths.Length, Is.EqualTo(2), d.Id);
                foreach (var c in d.Cards) Assert.That(SquashSim.Modes.GauntletAbilities.Find(c.Id), Is.Not.Null, d.Id + ": card " + c.Id);
                Assert.That(System.Array.IndexOf(SquashSim.Career.Roster.AllStyles, d.Id), Is.EqualTo(-1), d.Id + ": not an AI style's name");
            }
            var all = SquashSim.Career.Stars.AllOpponents;
            Assert.That(all.Length, Is.EqualTo(100), "50 club players and the 50 pros");
            Assert.That(new HashSet<string>(all).Count, Is.EqualTo(100), "no one twice");
            foreach (var c in SquashSim.Career.Stars.Clubs())
            {
                Assert.That(PlayerClasses.Find(c.Class), Is.Not.Null, c.Id + "'s class");
                Assert.That(SquashSim.Career.Stars.Describe(c.Id).name, Is.EqualTo(c.Name));
                Assert.That(SquashSim.Career.Stars.DifficultyFor(c.Id, -1), Is.GreaterThanOrEqualTo(0), "it plays at its rating");
                Assert.That(AiProfile.For(c.Id, 0.5), Is.Not.Null);
            }
            foreach (var s in SquashSim.Career.Stars.All()) Assert.That(PlayerClasses.Find(PlayerClasses.Nearest(s.Attributes, (uint)s.Rank)), Is.Not.Null, s.Id);
        }

        [Test] public void TheClassIsKeptWithTheMatch()
        {
            var f = new SquashSim.Replays.ReplayFile();
            f.Setup.Class0 = "touch"; f.Setup.Class1 = "speed";
            var back = SquashSim.Replays.ReplayFile.FromBytes(f.ToBytes());
            Assert.That(back.Setup.Class0, Is.EqualTo("touch"));
            Assert.That(back.Setup.Class1, Is.EqualTo("speed"));
        }
    }
}
