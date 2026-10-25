// ===========================================================
// Assets/SquashGame/Tests/Editor/ProfileStoreTests.cs
// P2-M1: the profile file - defaults, a save/load round trip, the
// one-time import of the old PlayerPrefs, a damaged file, and the
// settings reaching the game.
// ===========================================================

using System.IO;
using NUnit.Framework;
using SquashGame.Profile;
using UnityEngine;

namespace SquashGame.Tests
{
    public class ProfileStoreTests
    {
        string _dir;

        [SetUp]
        public void Temp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "reSquashStrikeProfileTests");
            Directory.CreateDirectory(_dir);
            File.Delete(Path.Combine(_dir, ProfileStore.FileName));
            ProfileStore.Folder = _dir;
            ProfileStore.Reset();
        }

        [TearDown]
        public void Restore() { ProfileStore.Folder = null; ProfileStore.Reset(); }

        [Test] public void RoundTrip()
        {
            var p = ProfileStore.Current;
            p.Settings.Assisted = true; p.Settings.AimSpeed = 1.4f; p.Settings.MasterVolume = 0.5f;
            p.Choices.OpponentId = "star:s10"; p.Choices.Length = 2;
            ProfileStore.Save();
            ProfileStore.Reset();
            var q = ProfileStore.Current;
            Assert.That(q.Settings.Assisted, Is.True);
            Assert.That(q.Settings.AimSpeed, Is.EqualTo(1.4f));
            Assert.That(q.Settings.MasterVolume, Is.EqualTo(0.5f));
            Assert.That(q.Choices.OpponentId, Is.EqualTo("star:s10"));
            Assert.That(q.Choices.Length, Is.EqualTo(2));
            Assert.That(q.Version, Is.EqualTo(ProfileStore.CurrentVersion));
        }

        [Test] public void FirstRunImportsThePlayerPrefs()
        {
            PlayerPrefs.SetFloat("aim.mouseSensitivity", 2f);
            PlayerPrefs.SetInt("controls.assisted", 1);
            try
            {
                var p = ProfileStore.Load();
                Assert.That(p.Settings.AimSpeed, Is.EqualTo(2f));
                Assert.That(p.Settings.Assisted, Is.True);
            }
            finally { PlayerPrefs.DeleteKey("aim.mouseSensitivity"); PlayerPrefs.DeleteKey("controls.assisted"); }
        }

        [Test] public void ADamagedFileStartsFresh()
        {
            File.WriteAllText(Path.Combine(_dir, ProfileStore.FileName), "{ not json");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var p = ProfileStore.Load();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.That(p, Is.Not.Null);
            Assert.That(p.Settings, Is.Not.Null);
        }

        [Test] public void SettingsReachTheGame()
        {
            var s = new GameSettings { Assisted = true, AimSpeed = 0.7f, InvertMouseY = true, Popups = false, RingColour = 2, BallScale = 1.2f };
            bool a = IntentSource.Assisted; float sp = IntentSource.MouseSensitivityScale; bool inv = IntentSource.InvertMouseY; bool pop = ArcadePopups.Enabled;
            try
            {
                ProfileStore.Apply(s, false);
                Assert.That(IntentSource.Assisted, Is.True);
                Assert.That(IntentSource.MouseSensitivityScale, Is.EqualTo(0.7f));
                Assert.That(IntentSource.InvertMouseY, Is.True);
                Assert.That(ArcadePopups.Enabled, Is.False);
                Assert.That(AimTargetView.RingColourIndex, Is.EqualTo(2));
                Assert.That(BallView.UserScale, Is.EqualTo(1.2f));
            }
            finally { ProfileStore.Apply(new GameSettings { Assisted = a, AimSpeed = sp, InvertMouseY = inv, Popups = pop }, false); }
        }
    }
}
