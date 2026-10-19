// ===========================================================
// Assets/SquashGame/Tests/PlayMode/PlaytestRound2Tests.cs
// The playtest round of 2026-10-19: the landing marker sits where the
// ball really lands; the aim ring fills while charging; the athlete is
// drawn inside the walls (a dive into the side wall); the lab keys do
// nothing outside developer mode; the menus play music and a match
// does not. Writes captures.
// ===========================================================

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SquashGame.UI;
using SquashSim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SquashGame.Tests
{
    public class PlaytestRound2Tests : InputTestFixture
    {
        static IEnumerator WaitReal(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }
        static IEnumerator WaitFor(float s, System.Func<bool> done) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s && !done()) yield return null; }

        [UnityTest]
        public IEnumerator LandingMarkerAndChargeRing()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            LabSimRunner r = Object.FindFirstObjectByType<WarmUpCourt>().Runner;
            var aim = GameObject.Find("AimTarget0").GetComponent<AimTargetView>();
            var marker = Object.FindFirstObjectByType<LandingMarkerView>();
            yield return WaitReal(0.3f);
            r.Input.SetReticle(0.4f, 2.4f);

            // hold: the ring fills
            Press(mouse.leftButton);
            yield return WaitReal(0.25f);
            float early = aim.ChargeLevel;
            yield return WaitReal(0.45f);
            float later = aim.ChargeLevel;
            Debug.Log("[Round2] charge " + early.ToString("0.00") + " -> " + later.ToString("0.00"));
            Assert.That(aim.ChargeShown, Is.True, "the charge shows on the ring");
            Assert.That(later, Is.GreaterThan(early + 0.2f), "and fills as you hold");
            yield return FrontEndTests.CaptureUI(fe, "Round2_Charge.png");
            Release(mouse.leftButton);

            // in the air: the square shows where it lands - and it lands there
            var bounces = new List<Vector3>();
            System.Action<IReadOnlyList<SimEvent>> onStep = evs => { foreach (var e in evs) if (e.Type == SimEventType.Impact && e.SurfaceClass == "floor") bounces.Add(SimToWorld.Pos(e.Point.X, 0, e.Point.Z)); };
            yield return WaitFor(1f, () => marker.Showing);
            Assert.That(marker.Showing, Is.True, "the landing square shows while the ball is in the air");
            Vector3 promised = marker.Point;
            r.Stepped += onStep;
            yield return FrontEndTests.CaptureUI(fe, "Round2_Landing.png");
            yield return WaitFor(3f, () => bounces.Count > 0);
            r.Stepped -= onStep;
            Assert.That(bounces.Count, Is.GreaterThan(0));
            float err = Vector2.Distance(new Vector2(promised.x, promised.z), new Vector2(bounces[0].x, bounces[0].z));
            Debug.Log("[Round2] landing square " + err.ToString("0.000") + " m from the bounce");
            Assert.That(err, Is.LessThan(0.15f), "the square is where it lands");
        }

        [UnityTest]
        public IEnumerator TheAthleteStaysInsideTheWallsAndDevKeysAreHidden()
        {
            LocalMatchTests.FreshProfile();
            var kb = InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            FrontEnd.LaunchFreePractice();
            SceneManager.LoadScene("WarmUpCourt");
            yield return null; yield return null;
            LabSimRunner r = Object.FindFirstObjectByType<WarmUpCourt>().Runner;
            var fig = Object.FindFirstObjectByType<Athlete.AthleteFigure>();
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return WaitReal(0.3f);

            // a dive into the left wall (sim x negative is world +x)
            r.Sim.Player.Player.Pos.X = -(CourtDimensions.HALF_W - 0.35); r.Sim.Player.Player.Pos.Z = 6.0;
            Press(kb.aKey); yield return WaitReal(0.2f);
            Press(kb.spaceKey); yield return null; Release(kb.spaceKey);
            float worst = 0f;
            float t0 = Time.realtimeSinceStartup;
            bool captured = false;
            while (Time.realtimeSinceStartup - t0 < 1.2f)
            {
                yield return null;
                foreach (var b in fig.Rig.Bones.Values) worst = Mathf.Max(worst, Mathf.Abs(b.position.x) - (float)CourtDimensions.HALF_W);
                if (!captured && Time.realtimeSinceStartup - t0 > 0.25f) { captured = true; yield return FrontEndTests.CaptureUI(fe, "Round2_WallDive.png"); }
            }
            Release(kb.aKey);
            Debug.Log("[Round2] deepest joint past the side wall: " + (worst * 100).ToString("0.0") + " cm");
            Assert.That(worst, Is.LessThanOrEqualTo(0.01f), "no part of the athlete through the wall");

            // the lab panel: nothing without developer mode
            var dbg = Object.FindFirstObjectByType<DebugOverlay>();
            bool keep = DebugOverlay.DevMode;
            DebugOverlay.DevMode = false;
            Press(kb.f1Key); yield return null; Release(kb.f1Key); yield return null;
            Assert.That(dbg.PanelVisible, Is.False, "F1 does nothing for players");
            DebugOverlay.DevMode = true;
            Press(kb.f1Key); yield return null; Release(kb.f1Key); yield return null;
            Assert.That(dbg.PanelVisible, Is.True, "developers still have it");
            dbg.PanelVisible = false;
            DebugOverlay.DevMode = keep;
        }

        [UnityTest]
        public IEnumerator MusicOnTheMenusNotInAMatch()
        {
            LocalMatchTests.FreshProfile();
            InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("Match");
            yield return null; yield return null;
            var fe = Object.FindFirstObjectByType<FrontEnd>();
            yield return WaitFor(15f, () => fe.Music.Playing && fe.Music.Level > 0.5f);
            Debug.Log("[Round2] menu music level " + fe.Music.Level.ToString("0.00"));
            Assert.That(fe.Music.Playing, Is.True, "music on the title / menus");
            fe.StartMatch(MatchSettings.QuickGame());
            yield return WaitReal(1.5f);
            Assert.That(fe.Music.Level, Is.LessThan(0.05f), "faded out in a match");
        }
    }
}
