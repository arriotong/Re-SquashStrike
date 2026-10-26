// ===========================================================
// Assets/SquashGame/Tests/PlayMode/MusicPlayTests.cs
// Batch 6: the menus play songs (MusicLibrary), announce them ("Now
// playing"), and each screen picks its mood - the career's, the
// Gauntlet's, the Locker's and online's, else the menu's.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Audio;
using SquashGame.Profile;
using SquashGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class MusicPlayTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        [UnityTest]
        public IEnumerator TheMenusPlaySongsByMood()
        {
            LocalMatchTests.FreshProfile();
            CareerStore.Delete();
            FrontEnd.StartAtTitle = false;
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return Wait(5f, () => Object.FindFirstObjectByType<FrontEnd>() != null);
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return Wait(4f, () => fe.Music.Current != null && fe.Music.Playing);
            Assert.That(fe.Music.Current, Is.Not.Null, "a song, not the made-up track");
            Assert.That(fe.Music.Playing, Is.True);
            Assert.That(fe.ToastLog.Exists(t => t.Contains(fe.Music.Current.Title)), Is.True, "now playing: " + fe.Music.Current.Title);

            Assert.That(FrontEnd.MoodFor(fe.Top), Is.EqualTo(MusicMood.Menu));
            Assert.That(FrontEnd.MoodFor(new LockerRoomScreen()), Is.EqualTo(MusicMood.Locker));
            Assert.That(FrontEnd.MoodFor(new GauntletScreen()), Is.EqualTo(MusicMood.Gauntlet));
            var hub = (MainMenuScreen)fe.Top;
            hub.Tabs.Select(hub.Tabs.Count - 1); yield return null;
            int career = -1; for (int i = 0; i < hub.Tabs.Count; i++) { hub.Tabs.Select(i); if (hub.Tabs.Current == "CAREER") { career = i; break; } }
            yield return null; yield return null;
            Assert.That(FrontEnd.MoodFor(fe.Top), Is.EqualTo(MusicMood.Career), "the hub's career tab");
            yield return Wait(3f, () => fe.Music.Current != null && fe.Music.Current.Mood == MusicMood.Career);
            Assert.That(fe.Music.Current.Mood, Is.EqualTo(MusicMood.Career), "the career's song: " + fe.Music.Current.Title);
        }
    }
}
