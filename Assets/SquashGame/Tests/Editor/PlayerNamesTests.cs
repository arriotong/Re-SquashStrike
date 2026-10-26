// ===========================================================
// Assets/SquashGame/Tests/Editor/PlayerNamesTests.cs
// P6 the name editor: a rename reaches the star's every name and
// survives a restart; the star's own name again drops the rename;
// export, then import elsewhere; an older save's Easy / Medium / Hard
// becomes a Quick Match opponent.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using SquashSim.Career;

namespace SquashGame.Tests
{
    public class PlayerNamesTests
    {
        string _dir;

        [SetUp]
        public void Temp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeNamesTests");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
        }

        [TearDown]
        public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); }

        [Test] public void ARenameReachesEveryNameAndIsKept()
        {
            PlayerNames.Load();
            PlayerNames.Set("s01", "Real", "Champion");
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo("Real Champion"));
            Assert.That(Roster.Generate(3)[0].Bug, Is.EqualTo("CHAMPION"), "the career's score bug");
            Assert.That(UI.MatchChoices.Label("star:s01"), Does.StartWith("Real Champion"), "the Quick Match ladder");

            ProfileStore.Reset();                                            // a restart
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo(Stars.No1.First + " " + Stars.No1.Last), "nothing until loaded");
            PlayerNames.Load();
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo("Real Champion"), "read back from players.json");
            Assert.That(PlayerNames.Count, Is.EqualTo(1));

            PlayerNames.Set("s01", "", "Hammer");                            // blank: that part stays
            Assert.That(Stars.FullName(Stars.No1), Is.EqualTo("Real Hammer"));
            PlayerNames.ResetOne("s01");
            Assert.That(PlayerNames.Renamed("s01"), Is.False, "its own name again: no rename kept");
        }

        [Test] public void ExportThenImport()
        {
            PlayerNames.Load();
            PlayerNames.Set("s02", "Ana", "Uno");
            PlayerNames.Set("s03", "Bo", "Dos");
            Assert.That(PlayerNames.Export(), Is.EqualTo(2));
            File.Copy(PlayerNames.ExportPath, PlayerNames.ImportPath);
            File.Delete(Path.Combine(_dir, PlayerNames.FileName));
            ProfileStore.Reset();
            PlayerNames.Load();
            Assert.That(PlayerNames.Count, Is.EqualTo(0));
            Assert.That(PlayerNames.Import(), Is.EqualTo(2));
            Assert.That(Stars.FullName(Stars.Find("s03")), Is.EqualTo("Bo Dos"));
            File.Delete(PlayerNames.ImportPath);
            Assert.That(PlayerNames.Import(), Is.EqualTo(-1), "no file: nothing");
        }

        [Test] public void AnOlderSavesArenaSettingBecomesAGraphicsLevel()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":6,\"Settings\":{\"Venue\":1}}");
            ProfileStore.Reset();
            Assert.That(ProfileStore.Current.Settings.Graphics, Is.EqualTo(3), "a lowered arena: Low");
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":6,\"Settings\":{\"Venue\":2}}");
            ProfileStore.Reset();
            Assert.That(ProfileStore.Current.Settings.Graphics, Is.EqualTo(0), "the full arena: Auto");
            Assert.That(SquashGame.GraphicsTier.Names[ProfileStore.Current.Settings.Graphics], Is.EqualTo("Auto"));
        }

        [Test] public void AnOlderSavesDifficultyBecomesAnOpponent()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{\"Version\":5,\"Choices\":{\"Opponent\":1,\"Difficulty\":2,\"Length\":1}}");
            ProfileStore.Reset();
            var c = ProfileStore.Current.Choices;
            Assert.That(c.OpponentId, Is.EqualTo(UI.MatchChoices.OldToId(2)));
            Assert.That(Stars.DifficultyFor(c.OpponentId, 0), Is.GreaterThan(Stars.DifficultyFor(UI.MatchChoices.OldToId(0), 0)), "Hard: a stronger opponent than Easy");
            Assert.That(c.Length, Is.EqualTo(1));
        }
    }
}
