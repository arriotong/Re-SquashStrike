// ===========================================================
// Assets/SquashGame/Tests/PlayMode/GauntletRunTests.cs
// 2026-11-23 (the user: "for some reason the gauntlet crashes after i
// reached level 5"): a run walked floor by floor - each floor cleared, each
// pick taken, the Gauntlet screen rebuilt every step as the game does - past
// the first boss (floor 5) and on to floor 8. Any exception fails it.
// ===========================================================

using System.Collections;
using NUnit.Framework;
using SquashGame.Profile;
using SquashGame.UI;
using SquashSim;
using SquashSim.Modes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class GauntletRunTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator ARunWalksPastTheFirstBoss()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return null;
            GauntletFlow.NewRun();
            fe.Push(new GauntletScreen());
            yield return null;
            var run = GauntletFlow.Run;
            for (int guard = 0; guard < 40 && run != null && run.FloorsWon < 8; guard++)
            {
                if (run.ChoiceDue) { GauntletFlow.Choose(true); fe.Replace(new GauntletScreen()); yield return null; continue; }
                if (run.AwaitingPick)
                {
                    Assert.That(run.Offer.Count, Is.GreaterThan(0), "something to pick after floor " + run.FloorsWon);
                    GauntletFlow.Pick(run.Offer[0]);
                    fe.Replace(new GauntletScreen());
                    yield return null; yield return null;
                    continue;
                }
                var fl = Gauntlet.Floor(run, run.Floor);
                var play = new FloorPlay { Floor = run.Floor, Target = fl.Target, Rallies = 5, Played = 5, Score = fl.Target, Done = true };
                Debug.Log("[GauntletWalk] floor " + run.Floor + " (" + fl.Kind + ") cleared");
                GauntletFlow.AfterFloor(fe, play);
                yield return null; yield return null;
                run = GauntletFlow.Run;
            }
            Assert.That(run, Is.Not.Null);
            Assert.That(run.FloorsWon, Is.GreaterThanOrEqualTo(8), "past the first boss");
            // 2026-11-23: leaving with a run in progress ends it - asked first; it can't be resumed
            if (!(fe.Top is GauntletScreen)) { fe.Replace(new GauntletScreen()); yield return null; }
            Assert.That(GauntletFlow.InProgress, Is.True);
            fe.Top.Back();
            yield return null;
            Assert.That(fe.Top, Is.InstanceOf<ConfirmScreen>(), "asked first");
            var confirm = fe.Top.Reachable().Find(i => i.Name == "Yes");
            confirm.Submit();
            yield return null; yield return null;
            Assert.That(GauntletFlow.InProgress, Is.False, "the run is over - no resuming");
        }
    }
}
