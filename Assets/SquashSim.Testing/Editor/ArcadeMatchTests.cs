// ===========================================================
// Assets/SquashSim.Testing/Editor/ArcadeMatchTests.cs
// The simple hitting system inside a real match (game profile, P1 local):
// the serve goes where you aim and you walk about the box while serving;
// a local player is never slowed (no near-ball cap); no let appeals for
// now; getting there early tightens the shot; boasts arm with a tap.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class ArcadeMatchTests
    {
        [SetUp] public void Game() { PhysicsConfig.ResetToDefaults(); PhysicsConfig.UseGameProfile(); PhysicsConfig.Current.Match.WarmUpEnabled = false; }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        static MatchSimulation NewMatch()
        {
            var m = new MatchSimulation(0);
            m.Bots[0] = null;
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.3), 5);
            m.Players[0].WallAimEnabled = true;
            m.Players[0].ArcadeHitEnabled = true;
            return m;
        }

        static void Aim(InputIntent it, double x, double y) { it.HasAimPoint = true; it.AimPointX = x; it.AimPointY = y; }

        [Test] public void TheServerWalksAboutTheBoxAtFullSpeedAndNeverFootFaults()
        {
            var m = NewMatch();
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss));
            double dt = PhysicsConfig.Current.Sim.FixedDt, top = 0;
            var me = m.Players[0]; var it = m.Intents[0];
            for (int s = 0; s < 480; s++)
            {
                it.Move.X = s < 240 ? -1 : 0; it.Move.Z = s < 240 ? 0 : 1; it.MoveMagnitude = 1;
                Aim(it, 1, 2.5);
                m.Step(dt);
                top = System.Math.Max(top, me.Player.Speed);
            }
            UnityEngine.Debug.Log("[Serve] top speed while waiting to serve " + top.ToString("0.00") + " m/s, at (" + me.Player.Pos.X.ToString("0.00") + ", " + me.Player.Pos.Z.ToString("0.00") + ")");
            Assert.That(top, Is.GreaterThan(PhysicsConfig.Current.Player.MaxSpeed * 0.8), "no slow walk before the serve");
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.AwaitToss), "walking about does not end the rally");
            Assert.That(RallyReferee.InServiceBox(m.Score.ServeSide, me.Player.Pos, 0), Is.True, "kept in the service box");
        }

        static V3 ServeAt(double x, double y, out RallyOutcome outcome)
        {
            var m = NewMatch();
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var it = m.Intents[0];
            for (int s = 0; s < 24; s++) { Aim(it, x, y); m.Step(dt); }
            V3 front = null; outcome = null;
            RallyOutcome got = null;
            m.RallyEnded += o => got = o;
            Aim(it, x, y); it.Hit.Pressed = true;
            for (int s = 0; s < 480 && front == null && got == null; s++)
            {
                m.Step(dt);
                foreach (var e in m.StepEvents) if (e.Type == SimEventType.Impact && e.SurfaceClass == "frontWall" && front == null) front = e.Point.Clone();
                Aim(it, x, y);
            }
            for (int s = 0; s < 240 && got == null; s++) m.Step(dt);
            outcome = got;
            return front;
        }

        [Test] public void TheServeGoesWhereYouAim()
        {
            foreach (var t in new[] { new[] { -2.0, 2.4 }, new[] { 1.5, 3.2 }, new[] { -0.5, 2.0 } })
            {
                V3 f = ServeAt(t[0], t[1], out _);
                Assert.That(f, Is.Not.Null);
                double err = System.Math.Sqrt((f.X - t[0]) * (f.X - t[0]) + (f.Y - t[1]) * (f.Y - t[1]));
                UnityEngine.Debug.Log("[Serve] aimed (" + t[0] + ", " + t[1] + "): front wall at (" + f.X.ToString("0.00") + ", " + f.Y.ToString("0.00") + ")");
                Assert.That(err, Is.LessThan(0.25), "the serve lands on the aim");
            }
            ServeAt(0.5, 1.2, out RallyOutcome low);
            Assert.That(low, Is.Not.Null);
            Assert.That(low.Kind, Is.EqualTo(RallyEnd.Fault), "aimed under the service line: a fault");
        }

        /// <summary>Serve from P1's box: aim, hold Hit for <paramref name="holdSteps"/> fixed steps, let go. Returns the serve contact.</summary>
        static SimEvent ServeHeld(double x, double y, int holdSteps, bool assisted, out ArcadeStroke arcade)
        {
            var m = NewMatch();
            m.Players[0].Arcade.Assisted = assisted;
            arcade = m.Players[0].Arcade;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            var it = m.Intents[0];
            for (int s = 0; s < 24; s++) { Aim(it, x, y); m.Step(dt); }
            it.Hit.Pressed = true; it.Hit.Held = holdSteps > 0;
            for (int s = 0; s < holdSteps + 4; s++)
            {
                Aim(it, x, y);
                if (s >= holdSteps) it.Hit.Held = false;
                m.Step(dt);
                foreach (var e in m.StepEvents) if (e.Type == SimEventType.RacketContact) return e;
            }
            return null;
        }

        [Test] public void TapIsALobServeHoldIsAHardServe()
        {
            SimEvent tap = ServeHeld(0.4, 2.6, 0, false, out _);
            SimEvent hard = ServeHeld(0.4, 2.6, 216, false, out _);       // ~0.9 s: a full charge
            UnityEngine.Debug.Log("[Serve] tap: " + tap.Technique + " " + tap.BallSpeedOut.ToString("0.0") + " m/s (up " + tap.OutVel.Y.ToString("0.0") +
                                  "), hold: " + hard.Technique + " " + hard.BallSpeedOut.ToString("0.0") + " m/s (up " + hard.OutVel.Y.ToString("0.0") + ")");
            Assert.That(tap.Technique, Is.EqualTo("lob serve"));
            Assert.That(hard.Technique, Is.EqualTo("hard serve"));
            Assert.That(hard.BallSpeedOut, Is.GreaterThan(tap.BallSpeedOut * 1.2), "the hard serve is clearly faster");
            Assert.That(hard.OutVel.Y / hard.BallSpeedOut, Is.LessThan(tap.OutVel.Y / tap.BallSpeedOut), "and flatter (a lower launch angle)");
        }

        [Test] public void AssistedServesLandDeepAndLegalWhereverYouAim()
        {
            int legal = 0, n = 0;
            foreach (var a in new[] { new[] { -0.9, 2.4 }, new[] { -1.6, 2.2 }, new[] { -2.4, 2.6 }, new[] { 0.4, 2.8 }, new[] { 2.0, 3.0 } })
                foreach (int hold in new[] { 0, 216 })
                {
                    ServeHeld(a[0], a[1], hold, true, out ArcadeStroke arc);
                    n++; if (arc.LastServeLegal) legal++;
                }
            UnityEngine.Debug.Log("[Serve] assisted: " + legal + "/" + n + " land deep and legal");
            Assert.That(legal, Is.EqualTo(n));
        }

        [Test] public void AssistedStraightDrivesStayOffTheSideWall()
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var surfaces = CourtSurfaces.Create();
            int n = 0, clipOff = 0, clipOn = 0;
            foreach (double side in new[] { 1.0, -1.0 })
                foreach (double bz in new[] { 4.0, 6.0, 8.0 })
                    foreach (double tx in new[] { 2.6, 3.0 })
                    {
                        var ball = new BallState(); ball.Reset(new V3(side * 2.6, 0.8, bz), new V3(0, 0, 0), null, 42);
                        var s = new ArcadeStroke(3);
                        n++;
                        s.Solve(ball, surfaces, side * tx, 1.6, 0, 0, c.DriveSpeed);
                        if (s.ClipsSide(ball, surfaces, (int)side)) clipOff++;
                        double x = s.StraightAssist(ball, surfaces, side * tx, 1.6, c.DriveSpeed);
                        s.Solve(ball, surfaces, x, 1.6, 0, 0, c.DriveSpeed);
                        if (s.ClipsSide(ball, surfaces, (int)side)) clipOn++;
                    }
            UnityEngine.Debug.Log("[Assist] straight drives clipping the side wall: non-assisted " + clipOff + "/" + n + ", assisted " + clipOn + "/" + n);
            Assert.That(clipOn, Is.EqualTo(0));
            Assert.That(clipOff, Is.GreaterThan(0), "the test should include drives that clip without help");
        }

        [Test] public void AssistedCrossCourtsDoNotRattleOffTheSideWalls()
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var surfaces = CourtSurfaces.Create();
            int n = 0, rattleOff = 0, rattleOn = 0;
            foreach (double side in new[] { 1.0, -1.0 })
                foreach (double bz in new[] { 3.5, 6.0, 8.5 })
                    foreach (double tx in new[] { -1.0, -2.6 })
                        foreach (double charge in new[] { 0.0, 1.0 })
                        {
                            var ball = new BallState(); ball.Reset(new V3(side * 2.2, 0.8, bz), new V3(0, 0, 0), null, 42);
                            var s = new ArcadeStroke(3);
                            double speed = c.DriveSpeed * (1 + c.ChargeBonus * charge);
                            n++;
                            s.Solve(ball, surfaces, side * tx, 1.8, 0, 0, speed);
                            if (!s.CleanCross(ball, surfaces)) rattleOff++;
                            double x = s.CrossAssist(ball, surfaces, side * tx, 1.8, ref speed);
                            s.Solve(ball, surfaces, x, 1.8, 0, 0, speed);
                            if (!s.CleanCross(ball, surfaces)) rattleOn++;
                        }
            UnityEngine.Debug.Log("[Assist] cross-courts rattling off the side walls: non-assisted " + rattleOff + "/" + n + ", assisted " + rattleOn + "/" + n);
            Assert.That(rattleOn, Is.EqualTo(0));
            Assert.That(rattleOff, Is.GreaterThan(0), "the test should include crosses that rattle without help");
        }

        [Test] public void LobsLandDeepAndStayIn()
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var surfaces = CourtSurfaces.Create();
            int n = 0, outs = 0; double sum = 0;
            foreach (double bx in new[] { -2.2, 0.0, 2.2 })
                foreach (double bz in new[] { 2.5, 5.0, 7.5, 8.8 })
                    foreach (double lx in new[] { -2.0, -0.5, 0.5, 2.0 })
                    {
                        var ball = new BallState(); ball.Reset(new V3(bx, 0.6, bz), new V3(0, 0, 0), null, 42);
                        var s = new ArcadeStroke(3);
                        s.SolveLob(ball, surfaces, lx, c.LobHeight);
                        var b = new BallState(); b.Reset(ball.Pos, s.LaunchVelocity.Clone(), null, 42); BallPhysics.Wake(b);
                        var evs = new List<SimEvent>(); bool front = false, done = false;
                        n++;
                        for (int i = 0; i < 240 * 5 && !done; i++)
                        {
                            evs.Clear(); BallPhysics.Step(b, surfaces, PhysicsConfig.Current.Sim.FixedDt, evs);
                            foreach (var e in evs)
                            {
                                if (e.Type != SimEventType.Impact || done) continue;
                                if (RallyReferee.OutReason(e) != null) { outs++; done = true; }
                                else if (e.SurfaceClass == "frontWall") front = true;
                                else if (front && e.SurfaceClass == "floor") { sum += e.Point.Z; done = true; }
                            }
                        }
                    }
            double mean = sum / System.Math.Max(1, n - outs);
            UnityEngine.Debug.Log("[Lob] " + n + " lobs: out " + outs + ", mean first bounce " + mean.ToString("0.0") + " m deep");
            Assert.That(outs, Is.EqualTo(0), "lobs stay in");
            Assert.That(mean, Is.GreaterThan(7.0), "and land deep");
        }

        [Test] public void TheAIPlaysTheSimpleSystemToo()
        {
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.42), 11);
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("shotmaker").WithDifficulty(0.42), 12);
            for (int i = 0; i < 2; i++) { m.Players[i].WallAimEnabled = true; m.Players[i].ArcadeHitEnabled = true; }
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            int arcadeContacts = 0, rallies = 0, faults = 0;
            var shots = new HashSet<string>();
            m.RallyEnded += o => { rallies++; if (o.Kind == RallyEnd.Fault || o.Kind == RallyEnd.FootFault) faults++; };
            int great = 0;
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.Time < 900)
            {
                m.Step(dt);
                foreach (var e in m.StepEvents)
                    if (e.Type == SimEventType.RacketContact && e.ArcadeQuality >= 0)
                    {
                        arcadeContacts++; shots.Add(e.Technique);
                        if (e.ArcadeDistance >= 0 && System.Math.Abs(e.ArcadeDistance - PhysicsConfig.Current.ArcadeHit.SweetSpot) <= 0.15 &&
                            e.Earliness >= 0.75 && e.ReleaseWait >= 0 && e.ReleaseWait <= 0.06 && m.Referee.Shots > 1) great++;      // the ring's great-hit flash
                    }
            }
            UnityEngine.Debug.Log("[AI] simple-system game: " + rallies + " rallies, " + arcadeContacts + " contacts, faults " + faults + ", shots " + string.Join(" ", shots) + ", great hits " + great);
            Assert.That(m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver, Is.True, "a whole game is played");
            Assert.That(great, Is.GreaterThan(0), "getting set early and striking cleanly happens (and flashes)");
            Assert.That(arcadeContacts, Is.GreaterThan(rallies), "every stroke through the simple system");
            Assert.That(shots.Count, Is.GreaterThanOrEqualTo(4), "with a mix of shots");
            Assert.That(faults, Is.LessThan(rallies / 4 + 1), "and serves that are mostly legal");
        }

        [Test] public void ALocalPlayerIsNeverCappedNearTheBall()
        {
            var me = new TestPlayerController();
            me.ArcadeHitEnabled = true;
            me.Player.ArcadeMovement = true;
            me.Player.Pos.X = -2.0; me.Player.Pos.Z = 5.0;
            var assist = new MovementAssist { HasIdealSpot = true };
            assist.IdealSpot.X = me.Player.Pos.X + 0.3; assist.IdealSpot.Z = me.Player.Pos.Z;
            var it = new InputIntent(); it.Move.X = 1; it.MoveMagnitude = 1;
            for (int s = 0; s < 120; s++)
            {
                assist.IdealSpot.X = me.Player.Pos.X + 0.3; assist.IdealSpot.Z = me.Player.Pos.Z;   // the ball always just ahead
                PlayerMovement.Step(me.Player, it, assist, PhysicsConfig.Current.Sim.FixedDt);
            }
            Assert.That(me.Player.Adjusting, Is.False);
            Assert.That(me.Player.Speed, Is.GreaterThan(PhysicsConfig.Current.Player.AdjustSpeed + 1), "full speed near the ball");
        }

        [Test] public void NoLetAppealsInTheGame()
        {
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.False);
            PhysicsConfig.ResetToDefaults();
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.True, "the lab keeps them");
        }

        [Test] public void BoastLandsOnTheFarHalf()
        {
            Assert.That(ArcadeStroke.BoastTarget(2.5, 2.0, out double lx), Is.EqualTo(1));
            Assert.That(lx, Is.LessThanOrEqualTo(-PhysicsConfig.Current.ArcadeHit.BoastCross), "from the right: across to the left half");
            Assert.That(ArcadeStroke.BoastTarget(-2.5, 1.7, out lx), Is.EqualTo(-1));
            Assert.That(lx, Is.EqualTo(1.7).Within(1e-9), "already on the far half: exactly the aim");
        }
    }
}
