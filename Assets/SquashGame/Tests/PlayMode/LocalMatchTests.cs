// ===========================================================
// Assets/SquashGame/Tests/PlayMode/LocalMatchTests.cs
// M5 in the real Match scene, with virtual devices only:
//   - the front end (title -> main menu -> Local Versus), driven by a
//     pad, starts a Local 2P match;
//     each pad steers only its own athlete; the match starts at the
//     serve (no knock-up); only the server can serve, in one click
//   - the audio director plays the event sounds (no errors)
//   - vs AI with the keyboard: straight to the serve; Enter serves
// Writes Captures/M5_*.png.
// ===========================================================

using System.Collections;
using System.IO;
using NUnit.Framework;
using SquashGame.Audio;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class LocalMatchTests : InputTestFixture
    {
        static IEnumerator Wait(float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds && !done()) yield return null;
        }

        IEnumerator Tap(ButtonControl b)
        {
            Press(b); yield return null;
            Release(b); yield return null;
        }

        /// <summary>A fresh profile in a temp folder, and the game opening on its title screen.
        /// (Not a new player unless asked: the Academy suggestion is already seen.)</summary>
        public static void FreshProfile(bool newPlayer = false)
        {
            ProfileStore.Folder = Path.Combine(Path.GetTempPath(), "reSquashStrikeTests");
            if (File.Exists(Path.Combine(ProfileStore.Folder, ProfileStore.FileName))) File.Delete(Path.Combine(ProfileStore.Folder, ProfileStore.FileName));
            ProfileStore.Reset();
            ProfileStore.Current.Settings = new GameSettings();
            ProfileStore.Current.Academy.Suggested = !newPlayer;
            FrontEnd.StartAtTitle = true;
        }

        [UnityTest]
        public IEnumerator TwoPadsPlayLocalTwoPlayerFromTheFrontEnd()
        {
            FreshProfile();
            var pad1 = InputSystem.AddDevice<Gamepad>();
            var pad2 = InputSystem.AddDevice<Gamepad>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            Assert.That(fe.Top, Is.InstanceOf<TitleScreen>(), "the game opens on the title");
            Assert.That(r.Demo, Is.True, "a demo rally plays behind the menus");

            yield return Tap(pad1.buttonSouth);                     // title -> main menu
            Assert.That(fe.Top, Is.InstanceOf<MainMenuScreen>());
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("QUICK MATCH"));
            yield return Tap(pad1.dpad.right);
            yield return Tap(pad1.dpad.right);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("LOCAL VERSUS"));
            yield return Tap(pad1.buttonSouth);
            Assert.That(fe.Top, Is.InstanceOf<LocalVersusScreen>());
            Capture("P2M1_LocalVersus.png");
            yield return HubNav.ToPrimary(fe, pad1.dpad.down, pad1.dpad.right, Tap);
            yield return Tap(pad1.buttonSouth);                     // start
            Assert.That(fe.Top, Is.Null, "the menus close");
            Assert.That(r.Demo, Is.False);

            MatchSimulation m = r.Sim;
            Assert.That(r.Settings.TwoPlayer, Is.True);
            Assert.That(m.Bots[1], Is.Null, "no AI in a two-player match");
            Assert.That(m.Score.GamesToWin, Is.EqualTo(3), "best of 5 by default");
            Assert.That(r.Input2, Is.Not.Null);
            Assert.That(r.Input2.Devices, Is.EquivalentTo(new InputDevice[] { pad2 }), "player 2 has pad 2 only");
            Assert.That(System.Array.IndexOf(r.Input.Devices, pad1), Is.GreaterThanOrEqualTo(0), "player 1 has pad 1");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "no knock-up: straight to the serve");

            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            Assert.That(m.Score.Server, Is.EqualTo(0));

            Vector3 a0 = r.View.A0.Pos, a1 = r.View.A1.Pos;
            Set(pad2.leftStick, new Vector2(-1f, 0f));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.6f) yield return null;
            Set(pad2.leftStick, Vector2.zero);
            yield return null;
            float moved1 = Vector3.Distance(r.View.A1.Pos, a1), moved0 = Vector3.Distance(r.View.A0.Pos, a0);
            Debug.Log("[M5] pad 2 stick: athlete 1 moved " + moved1.ToString("0.00") + " m, athlete 0 " + moved0.ToString("0.00") + " m");
            Assert.That(moved1, Is.GreaterThan(0.3f), "pad 2 moves player 2");
            Assert.That(moved0, Is.LessThan(0.05f), "pad 2 must not move player 1");

            // Only the server tosses: pad 2's Hit does nothing, pad 1's tosses.
            yield return Tap(pad2.buttonSouth);
            yield return Wait(0.25f, () => false);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "the receiver cannot toss");
            Set(pad1.rightStick, new Vector2(-0.55f, 0.85f));
            yield return Tap(pad1.buttonSouth);
            yield return Wait(0.25f, () => m.Phase != MatchPhase.AwaitToss);   // frames here are faster than the 240 Hz step
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Rally), "player 1 tossed");

            // Strike, and let the rally make some noise.
            int heard = audio.PlayedTotal;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 4f && m.Referee.Shots == 0)
            {
                yield return Tap(pad1.buttonSouth);
                yield return null;
            }
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
            Debug.Log("[M5] sounds: " + string.Join(", ", System.Linq.Enumerable.Select(audio.Played, kv => kv.Key + " " + kv.Value)));
            Assert.That(audio.PlayedTotal, Is.GreaterThan(heard), "the rally should be heard");
            Assert.That(audio.Played.ContainsKey("floor") || audio.Played.ContainsKey("front") || audio.Played.ContainsKey("ping"), Is.True);
            Capture("M5_TwoPlayer.png");
            Set(pad1.rightStick, Vector2.zero);
        }

        [UnityTest]
        public IEnumerator KeyboardStartsAQuickMatchAgainstTheAiAndOneClickServes()
        {
            FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            MatchRunner r = Object.FindFirstObjectByType<MatchCourt>().Runner;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            Assert.That(MatchRunner.CanPlayTwoPlayer, Is.False);
            yield return Tap(kb.enterKey);                          // title -> main menu
            var menu = fe.Top;
            Assert.That(menu, Is.InstanceOf<MainMenuScreen>());
            Assert.That(menu.Find("LOCAL VERSUS"), Is.Not.Null, "the card is there (its START needs a pad)");
            yield return Tap(kb.enterKey);                          // Quick Match
            Assert.That(fe.Top, Is.InstanceOf<QuickMatchScreen>());
            Capture("P2M1_QuickMatch.png");
            yield return HubNav.ToPrimary(fe, kb.downArrowKey, kb.rightArrowKey, Tap);
            Assert.That(fe.Top.Focused.Name, Is.EqualTo("Play"));
            yield return Tap(kb.enterKey);

            MatchSimulation m = r.Sim;
            Assert.That(fe.Top, Is.Null);
            Assert.That(r.Demo, Is.False);
            Assert.That(m.Bots[1], Is.Not.Null, "the AI plays");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "no knock-up: straight to the serve");
            Assert.That(m.Score.Points[0] + m.Score.Points[1], Is.EqualTo(0));

            // One click serves: the mouse's Hit, and the ball is in play at once (no toss to time).
            var mouse = Mouse.current;
            yield return Tap(mouse.leftButton);
            yield return Wait(0.3f, () => m.Phase != MatchPhase.AwaitToss);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Rally), "one click serves");
            Assert.That(m.Referee.Shots, Is.EqualTo(1), "the serve is already struck");
        }

        static void Capture(string file)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
