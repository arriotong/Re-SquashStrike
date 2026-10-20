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

        [Test] public void AssistedStraightKillsClipTheirWallLess()
        {
            // A kill gets the straight assist too (2026-10-20: a safety of at most 0.3 m, not the perfect line): a kill
            // aimed tight down its own wall touches it before the second bounce less often - not never.
            var c = PhysicsConfig.Current.ArcadeHit;
            var surfaces = CourtSurfaces.Create();
            double ky = CourtDimensions.TIN_H + c.KillHeightAboveTin, speed = c.DriveSpeed * (1 + c.ChargeBonus);
            int n = 0, clipOff = 0, clipOn = 0;
            foreach (double side in new[] { 1.0, -1.0 })
                foreach (double bz in new[] { 3.5, 5.0, 6.5 })
                    foreach (double tx in new[] { 2.7, 3.05 })
                    {
                        var ball = new BallState(); ball.Reset(new V3(side * 2.5, 0.7, bz), new V3(0, 0, 0), null, 42);
                        var s = new ArcadeStroke(3);
                        n++;
                        s.Solve(ball, surfaces, side * tx, ky, 0, 0, speed);
                        if (s.ClipsSide(ball, surfaces, (int)side, 2)) clipOff++;
                        double x = s.StraightAssist(ball, surfaces, side * tx, ky, speed, true);
                        Assert.That(System.Math.Abs(x - side * tx), Is.LessThanOrEqualTo(GameTuning.Assist.StraightMaxShift + 1e-9), "a small safety, not a new line");
                        s.Solve(ball, surfaces, x, ky, 0, 0, speed);
                        if (s.ClipsSide(ball, surfaces, (int)side, 2)) clipOn++;
                    }
            UnityEngine.Debug.Log("[Assist] straight kills touching their wall before the 2nd bounce: non-assisted " + clipOff + "/" + n + ", assisted " + clipOn + "/" + n);
            Assert.That(clipOn, Is.LessThan(clipOff));
        }

        [Test] public void ABadContactMayLoseTheAssistAGoodOneNever()
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            Assert.That(ArcadeStroke.BadnessOf(c.SweetSpot, 0.6, false, false, c), Is.EqualTo(0), "sweet spot, set early: good");
            Assert.That(ArcadeStroke.BadnessOf(c.SweetSpot + 0.2, 0.6, false, false, c), Is.EqualTo(0), "a little off: still good");
            Assert.That(ArcadeStroke.AssistLossChance(0, c), Is.EqualTo(0), "a good contact always keeps the clean line");
            double jammed = ArcadeStroke.BadnessOf(c.SweetSpot - 0.6, 0.6, false, false, c);
            double stretched = ArcadeStroke.BadnessOf(c.SweetSpot + 0.6, 0.6, false, false, c);
            double late = ArcadeStroke.BadnessOf(c.SweetSpot, 0.05, false, false, c);
            double diving = ArcadeStroke.BadnessOf(c.SweetSpot, 0.6, true, false, c);
            Assert.That(jammed, Is.GreaterThan(0.5), "too close");
            Assert.That(stretched, Is.GreaterThan(0.5), "too far");
            Assert.That(late, Is.GreaterThan(0.5), "got there late");
            Assert.That(diving, Is.EqualTo(1));
            double worst = ArcadeStroke.AssistLossChance(1, c), slight = ArcadeStroke.AssistLossChance(0.01, c);
            Assert.That(worst, Is.LessThan(1), "even the worst contact still has a chance");
            Assert.That(slight, Is.GreaterThan(0.2));
            UnityEngine.Debug.Log("[Assist] loss chance: just bad " + (100 * slight).ToString("0") + "%, fully bad " + (100 * worst).ToString("0") + "%");
        }

        [Test] public void DrivesComeOffTheFrontWallFlatterAndStillGoDeep()
        {
            // Playtest 2026-10-18: drives and charged drives popped up off the front wall and came back slow. Now they
            // keep only a little of that rise, with the same pace: lower when they pass the T (no easy volley), still deep.
            var c = PhysicsConfig.Current.ArcadeHit;
            var surfaces = CourtSurfaces.Create();
            double dip = c.FrontWallRiseKeep;
            int n = 0, rising = 0, shallow = 0; double hBefore = 0, hAfter = 0, zBefore = 0, zAfter = 0;
            foreach (double bz in new[] { 5.0, 7.0, 8.5 })
                foreach (double charge in new[] { 0.0, 1.0 })
                    foreach (double ty in new[] { 1.5, 2.2 })
                    {
                        double speed = c.DriveSpeed * (1 + c.ChargeBonus * charge);
                        var ball = new BallState(); ball.Reset(new V3(1.8, 0.8, bz), new V3(0, 0, 0), null, 42);
                        var s = new ArcadeStroke(3);
                        s.Solve(ball, surfaces, 2.4, ty, 0, 0, speed);
                        Fly(ball, s.LaunchVelocity, false, surfaces, out double vyA, out double hA, out double zA);
                        Fly(ball, s.LaunchVelocity, true, surfaces, out double vyB, out double hB, out double zB);
                        n++;
                        if (vyB > dip * System.Math.Max(0, vyA) + 1e-6) rising++;     // keeps no more than its share of the rise
                        if (zB < CourtDimensions.SHORT_LINE_Z + 0.5) shallow++;                // not a ball that dies in the front court
                        hBefore += hA; hAfter += hB; zBefore += zA; zAfter += zB;
                    }
            UnityEngine.Debug.Log("[Dip] " + n + " drives off the front wall (rise kept " + dip + "): height passing the short line " + (hBefore / n).ToString("0.00") + " m -> " +
                                  (hAfter / n).ToString("0.00") + " m; first bounce " + (zBefore / n).ToString("0.00") + " m -> " + (zAfter / n).ToString("0.00") + " m");
            Assert.That(rising, Is.EqualTo(0), "no drive keeps more of its rise off the wall than the setting");
            Assert.That(hAfter, Is.LessThan(hBefore), "lower through the middle of the court");
            Assert.That(shallow, Is.EqualTo(0), "and none dies short");
            Assert.That(zAfter / n, Is.GreaterThan(6.8), "still a good length on average (the back third of the court)");
            Assert.That(hAfter / n, Is.LessThan(0.7 * hBefore / n), "clearly lower through the middle");
        }

        /// <summary>Fly a launch: its vertical speed just off the front wall, its height passing the short line on the way
        /// back, and where it first bounces.</summary>
        static void Fly(BallState from, V3 vel, bool dip, List<Surface> surfaces, out double vyOff, out double hShort, out double zBounce)
        {
            var b = new BallState(); b.Reset(from.Pos, vel, null, from.Temperature); b.DipOffFrontWall = dip;
            var ev = new List<SimEvent>();
            vyOff = 0; hShort = -1; zBounce = -1;
            bool front = false;
            for (int i = 0; i < 1200 && zBounce < 0; i++)
            {
                ev.Clear();
                double z0 = b.Pos.Z;
                BallPhysics.Step(b, surfaces, 1.0 / 240, ev);
                foreach (var e in ev)
                {
                    if (e.SurfaceClass == "frontWall" && !front) { front = true; vyOff = b.Vel.Y; }
                    else if (front && e.SurfaceClass == "floor") zBounce = e.Point.Z;
                }
                if (front && hShort < 0 && z0 < CourtDimensions.SHORT_LINE_Z && b.Pos.Z >= CourtDimensions.SHORT_LINE_Z) hShort = b.Pos.Y;
            }
            if (hShort < 0) hShort = 0;
        }

        [Test] public void TheGameProfileIsGameTuning()
        {
            // Every on-court number lives in GameTuning; the game profile is the lab plus exactly those.
            var c = PhysicsConfig.Current;
            double pace = GameTuning.Ball.Pace;
            Assert.That(c.ArcadeHit.DriveSpeed, Is.EqualTo(GameTuning.Shots.DriveSpeed * pace));
            Assert.That(c.ArcadeHit.ServeSpeed, Is.EqualTo(GameTuning.Serve.Speed * pace));
            Assert.That(c.ArcadeHit.SweetSpot, Is.EqualTo(GameTuning.Hit.SweetSpot));
            Assert.That(c.ArcadeHit.FrontWallRiseKeep, Is.EqualTo(GameTuning.Ball.FrontWallRiseKeep));
            Assert.That(c.ArcadeHit.AssistMissMax, Is.EqualTo(GameTuning.Assist.MissMax));
            Assert.That(c.Surfaces.FrontWall.Restitution, Is.EqualTo(GameTuning.Ball.FrontWallRestitution));
            Assert.That(c.WallAim.MinHeight, Is.EqualTo(GameTuning.Aim.MinHeight));
            Assert.That(c.Reach.Stretch, Is.EqualTo(GameTuning.Movement.Stretch));
            Assert.That(c.Racket.Swing.Attack.HeadSpeed, Is.EqualTo(GameTuning.Ball.AttackHeadSpeed * GameTuning.Ball.SwingPaceScale * pace));
            Assert.That(c.Restitution.SpeedResponse.Length, Is.EqualTo(GameTuning.Ball.SpeedResponse.GetLength(0)));
            var easy = AiProfile.Retriever().WithDifficulty(0);
            Assert.That(easy.ReactionTime, Is.EqualTo(GameTuning.Ai.Reaction0));
            Assert.That(easy.AimNoise, Is.EqualTo(GameTuning.Ai.Noise0));
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

        [Test] public void LetAppealsAreOnInTheGame()
        {
            Assert.That(PhysicsConfig.Current.Match.AppealsEnabled, Is.True, "P2-M7: lets are back (the Lets setting turns them off per match)");
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
