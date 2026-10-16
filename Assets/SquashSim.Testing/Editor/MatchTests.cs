// ===========================================================
// Assets/SquashSim.Testing/Editor/MatchTests.cs
// M2 rules and mechanics, as a scripted test suite:
//   referee calls (tin, out, down, not up, serve faults, foot fault,
//   lets, dropped toss), PAR and English scoring, ball-vs-body,
//   body-vs-body, recovery, split-step, and full wall-bot games
//   (finish, deterministic).
// ===========================================================

using NUnit.Framework;
using SquashSim;
using static SquashSim.CourtDimensions;

namespace SquashSim.Tests
{
    public class MatchTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        // ---------------- helpers ----------------

        static SimEvent Hit(int player) { return new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = player, Point = new V3(0, 1, 5) }; }

        static SimEvent At(string surfaceClass, double x, double y, double z, string id = null)
        {
            return new SimEvent { Type = SimEventType.Impact, SurfaceClass = surfaceClass, SurfaceId = id ?? surfaceClass, Point = new V3(x, y, z) };
        }

        static SimEvent Body(int player) { return new SimEvent { Type = SimEventType.BodyHit, PlayerIndex = player, Point = new V3(0, 1, 6) }; }

        static readonly V3 RightBoxCentre = new V3(HALF_W - SERVICE_BOX / 2, 0, SHORT_LINE_Z + SERVICE_BOX / 2);

        /// <summary>P0 serves legally from the right box; the ball lands deep left. Ready for P1 to strike.</summary>
        static RallyReferee AfterGoodServe()
        {
            var r = new RallyReferee();
            r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("frontWall", 0.5, 3.0, 0));
            r.OnImpact(At("floor", -1.6, 0, 7.5));
            Assert.That(r.Outcome, Is.Null, "a good serve should not end the rally");
            Assert.That(r.ExpectedStriker, Is.EqualTo(1));
            return r;
        }

        static void AssertEnd(RallyReferee r, RallyEnd kind, int winner)
        {
            Assert.That(r.Outcome, Is.Not.Null, "expected the rally to end");
            Assert.That(r.Outcome.Kind, Is.EqualTo(kind), r.Outcome.ToString());
            Assert.That(r.Outcome.Winner, Is.EqualTo(winner), r.Outcome.ToString());
        }

        // ---------------- serve ----------------

        [Test] public void GoodServeThenSecondBounceIsNotUp()
        {
            var r = AfterGoodServe();
            r.OnImpact(At("floor", -1.4, 0, 8.9));
            AssertEnd(r, RallyEnd.NotUp, 0);
        }

        [Test] public void ServeBelowServiceLineIsFault()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("frontWall", 0.5, SERVICE_LINE_H - 0.01, 0));
            AssertEnd(r, RallyEnd.Fault, 1);
        }

        [Test] public void ServeOnServiceLineIsFault()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("frontWall", 0.5, SERVICE_LINE_H, 0));
            AssertEnd(r, RallyEnd.Fault, 1);
        }

        [Test] public void ServeSideWallFirstIsFault()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("sideWall", -HALF_W, 2.0, 2.0, "sideWallLeft"));
            AssertEnd(r, RallyEnd.Fault, 1);
        }

        [TestCase(1.0, 7.0, TestName = "ServeLandsOnServersOwnSide")]
        [TestCase(-0.01, 7.0, TestName = "ServeLandsOnHalfCourtLine")]
        [TestCase(-1.5, 4.27, TestName = "ServeLandsOnShortLine")]
        [TestCase(-1.5, 3.5, TestName = "ServeLandsShort")]
        public void ServeLandingFaults(double x, double z)
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("frontWall", 0.5, 3.0, 0));
            r.OnImpact(At("floor", x, 0, z));
            AssertEnd(r, RallyEnd.Fault, 1);
        }

        [Test] public void ServeFromLeftBoxMustLandRight()
        {
            var r = new RallyReferee(); r.BeginRally(1, -1);
            r.OnRacketContact(Hit(1), new V3(-2.4, 0, 5.06));
            r.OnImpact(At("frontWall", -0.5, 3.0, 0));
            r.OnImpact(At("floor", 1.6, 0, 7.5));
            Assert.That(r.Outcome, Is.Null);
            Assert.That(r.ExpectedStriker, Is.EqualTo(0));
        }

        [Test] public void ServerOutsideTheBoxIsFootFault()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), new V3(0, 0, 6));
            AssertEnd(r, RallyEnd.FootFault, 1);
        }

        [Test] public void VolleyedServeWaivesTheLandingRule()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnRacketContact(Hit(0), RightBoxCentre);
            r.OnImpact(At("frontWall", 0.5, 3.0, 0));
            r.OnRacketContact(Hit(1), new V3(0.5, 0, 3.0));   // receiver volleys before the bounce
            Assert.That(r.ServeVolleyed, Is.True);
            r.OnImpact(At("frontWall", 0.2, 1.0, 0));
            r.OnImpact(At("floor", 0.5, 0, 3.0));             // short and on the wrong side: no longer a serve
            Assert.That(r.Outcome, Is.Null);
        }

        [Test] public void DroppedTossIsNotAFault()
        {
            var r = new RallyReferee(); r.BeginRally(0, 1);
            r.OnImpact(At("floor", 2.7, 0, 4.7));
            Assert.That(r.TossDropped, Is.True);
            Assert.That(r.Outcome, Is.Null);
        }

        // ---------------- rally ----------------

        [Test] public void TinIsDown()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("tin", -1.0, 0.3, 0));
            AssertEnd(r, RallyEnd.Down, 0);
        }

        [Test] public void FloorBeforeFrontWallIsDown()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("floor", -1.0, 0, 3.0));
            AssertEnd(r, RallyEnd.Down, 0);
        }

        [Test] public void SideThenBackWallBeforeFrontWallIsLegal()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("backWall", -1.0, 1.0, LENGTH));
            r.OnImpact(At("sideWall", -HALF_W, 1.5, 6.0, "sideWallLeft"));
            r.OnImpact(At("frontWall", 1.0, 1.0, 0));
            Assert.That(r.Outcome, Is.Null);
            Assert.That(r.ExpectedStriker, Is.EqualTo(0));
        }

        [Test] public void FrontWallOutLineIsOut()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("frontWall", 0, FRONT_WALL_OUT_H - 0.02, 0));    // touching the line
            AssertEnd(r, RallyEnd.Out, 0);
        }

        [Test] public void SideWallUsesTheSlopingOutLine()
        {
            double z = 8.0, h = SideWallOutHeight(z);
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("sideWall", -HALF_W, h - 0.10, z, "sideWallLeft"));     // below the line: in
            Assert.That(r.Outcome, Is.Null);
            // Same height, 2 m nearer the front: the line is higher there, so still in.
            r.OnImpact(At("sideWall", -HALF_W, h - 0.01, z - 2, "sideWallLeft"));
            Assert.That(r.Outcome, Is.Null);
            r.OnImpact(At("sideWall", HALF_W, SideWallOutHeight(9.0), 9.0, "sideWallRight"));
            AssertEnd(r, RallyEnd.Out, 0);
        }

        [Test] public void CeilingIsOut()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnImpact(At("frontWall", 0, 3.0, 0));
            r.OnImpact(At("ceiling", 0, CEILING_H, 4.0));
            AssertEnd(r, RallyEnd.Out, 0);
        }

        [Test] public void BallHittingTheOpponentIsALet()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnBodyHit(Body(0));
            AssertEnd(r, RallyEnd.Let, -1);
        }

        [Test] public void OwnShotHittingStrikerLosesTheRally()
        {
            var r = AfterGoodServe();
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            r.OnBodyHit(Body(1));
            AssertEnd(r, RallyEnd.HitSelf, 0);
        }

        [Test] public void OnlyTheExpectedPlayerMayStrike()
        {
            var r = AfterGoodServe();
            Assert.That(r.ExpectedStriker, Is.EqualTo(1));
            r.OnRacketContact(Hit(1), new V3(-1.5, 0, 7.5));
            Assert.That(r.ExpectedStriker, Is.EqualTo(-1), "nobody may strike until the front wall");
            r.OnImpact(At("frontWall", 0, 1.0, 0));
            Assert.That(r.ExpectedStriker, Is.EqualTo(0));
        }

        // ---------------- scoring ----------------

        static RallyOutcome Win(int p) { return new RallyOutcome { Kind = RallyEnd.Down, Winner = p, Loser = 1 - p }; }

        [Test] public void ParServerKeepsServeAndAlternatesBoxes()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 1, true);
            Assert.That(s.ServeSide, Is.EqualTo(1));
            s.Apply(Win(0));
            Assert.That(s.Points[0], Is.EqualTo(1));
            Assert.That(s.Server, Is.EqualTo(0));
            Assert.That(s.ServeSide, Is.EqualTo(-1));
            s.Apply(Win(1));
            Assert.That(s.Points[1], Is.EqualTo(1));
            Assert.That(s.Server, Is.EqualTo(1));
            Assert.That(s.ServeSide, Is.EqualTo(1), "a new server starts in the right box");
        }

        [Test] public void ParLetChangesNothing()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 1, true);
            s.Apply(Win(0));
            s.Apply(new RallyOutcome { Kind = RallyEnd.Let });
            Assert.That(s.Points[0], Is.EqualTo(1));
            Assert.That(s.ServeSide, Is.EqualTo(-1), "a let replays from the same box");
        }

        [Test] public void ParWinByTwoFromTenAll()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 1, true);
            for (int i = 0; i < 10; i++) { s.Apply(Win(0)); s.Apply(Win(1)); }
            Assert.That(s.Points[0], Is.EqualTo(10)); Assert.That(s.Points[1], Is.EqualTo(10));
            s.Apply(Win(0));
            Assert.That(s.GameOver, Is.False, "11-10 is not enough");
            s.Apply(Win(1));
            s.Apply(Win(1));
            Assert.That(s.GameOver, Is.False);
            s.Apply(Win(1));
            Assert.That(s.GameOver, Is.True);
            Assert.That(s.GameWinner, Is.EqualTo(1));
            Assert.That(s.Points[1], Is.EqualTo(13)); Assert.That(s.Points[0], Is.EqualTo(11));
        }

        [Test] public void ParElevenNineEndsTheGame()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 1, true);
            for (int i = 0; i < 9; i++) { s.Apply(Win(0)); s.Apply(Win(1)); }
            s.Apply(Win(0)); s.Apply(Win(0));
            Assert.That(s.GameOver, Is.True);
            Assert.That(s.MatchOver, Is.True);
            Assert.That(s.MatchWinner, Is.EqualTo(0));
        }

        [Test] public void EnglishOnlyServerScores()
        {
            var s = new MatchScore(ScoringSystem.English, 0, 1, true);
            s.Apply(Win(1));                         // receiver wins: hand-out, no point
            Assert.That(s.Points[1], Is.EqualTo(0));
            Assert.That(s.Server, Is.EqualTo(1));
            s.Apply(Win(1));                         // now the server: scores
            Assert.That(s.Points[1], Is.EqualTo(1));
        }

        [TestCase(true, 10)]
        [TestCase(false, 9)]
        public void EnglishEightAllSetChoice(bool setTwo, int target)
        {
            var s = new MatchScore(ScoringSystem.English, 0, 1, setTwo);
            for (int i = 0; i < 8; i++) s.Apply(Win(0));   // P0 serves to 8-0
            s.Apply(Win(1));                               // hand-out
            for (int i = 0; i < 8; i++) s.Apply(Win(1));   // P1 serves to 8-8
            Assert.That(s.GameTarget, Is.EqualTo(target));
            while (!s.GameOver) s.Apply(Win(1));
            Assert.That(s.Points[1], Is.EqualTo(target));
        }

        // ---------------- collisions ----------------

        [Test] public void BallBouncesDeadOffABody()
        {
            var body = new TestPlayer(new V3(0, 0, 6));
            var ball = new BallState();
            ball.Reset(new V3(0, 1.2, 5.62), new V3(0, 0, 20), null, 30.0);
            var prev = ball.Pos.Clone();
            ball.Pos.Z += 20.0 / 240;                     // one step of travel, into the body
            SimEvent ev = BodyCollision.BallVsBody(ball, prev, body, 0, 1.0);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Type, Is.EqualTo(SimEventType.BodyHit));
            Assert.That(ball.Vel.Z, Is.LessThan(0), "ball should come back off the body");
            Assert.That(System.Math.Abs(ball.Vel.Z), Is.LessThan(20 * 0.3), "a body is dead: low restitution");
            double d = System.Math.Sqrt(ball.Pos.X * ball.Pos.X + (ball.Pos.Z - 6) * (ball.Pos.Z - 6));
            Assert.That(d, Is.GreaterThanOrEqualTo(PhysicsConfig.Current.Player.Radius + PhysicsConfig.Current.Ball.Radius));
        }

        [Test] public void FastBallCannotSlipThroughAGraze()
        {
            var body = new TestPlayer(new V3(0, 0, 6));
            var ball = new BallState();
            // Both ends of this step's path are clear of the body; only the middle grazes it.
            ball.Reset(new V3(0.30, 1.2, 5.85), new V3(0, 0, 70), null, 30.0);
            var prev = ball.Pos.Clone();
            ball.Pos.Z += 70.0 / 240;                    // 0.29 m in one step
            Assert.That(BodyCollision.BallVsBody(ball, prev, body, 0, 1.0), Is.Not.Null);
        }

        [Test] public void PlayersCannotOverlap()
        {
            var a = new TestPlayer(new V3(0, 0, 5)); var b = new TestPlayer(new V3(0.2, 0, 5));
            a.Vel.X = 3; b.Vel.X = -3;
            SimEvent ev = BodyCollision.BodyVsBody(a, 0, b, 1, false, out bool touching, 1.0);
            Assert.That(touching, Is.True);
            Assert.That(ev, Is.Not.Null);
            Assert.That(b.Pos.X - a.Pos.X, Is.EqualTo(PhysicsConfig.Current.Player.Radius * 2).Within(1e-9));
            Assert.That(b.Vel.X - a.Vel.X, Is.GreaterThan(-6), "closing speed reduced");
            Assert.That(BodyCollision.BodyVsBody(a, 0, b, 1, true, out _, 1.1), Is.Null, "contact reported once");
        }

        // ---------------- recovery + split-step ----------------

        static double SpeedAfter(System.Action<TestPlayer> setup, double seconds)
        {
            var p = new TestPlayer(new V3(0, 0, 5));
            setup(p);
            var intent = new InputIntent { MoveMagnitude = 1 };
            intent.Move.X = 1;
            double dt = 1.0 / 240;
            for (int i = 0; i < (int)(seconds / dt); i++) PlayerMovement.Step(p, intent, null, dt);
            return p.Speed;
        }

        [Test] public void RecoverySlowsThePushOff()
        {
            double fresh = SpeedAfter(p => { }, 0.1);
            double recovering = SpeedAfter(p => p.RecoveryTimer = 0.35, 0.1);
            Assert.That(recovering, Is.LessThan(fresh * 0.6));
        }

        [Test] public void SplitStepTimedToContactBoosts()
        {
            var t = new SplitStepTracker();
            var p0 = new TestPlayer(); var p1 = new TestPlayer();
            t.Press(1, 1.00, p1);
            t.Contact(0, 1.05, p1);                       // opponent strikes 50 ms later
            Assert.That(p1.BoostTimer, Is.GreaterThan(0));
            Assert.That(SpeedAfter(p => p.BoostTimer = 0.25, 0.1), Is.GreaterThan(SpeedAfter(p => { }, 0.1)));
        }

        [Test] public void SplitStepJustAfterContactStillBoosts()
        {
            var t = new SplitStepTracker();
            var p1 = new TestPlayer();
            t.Contact(0, 2.00, p1);
            t.Press(1, 2.08, p1);
            Assert.That(p1.BoostTimer, Is.GreaterThan(0));
        }

        [Test] public void MistimedSplitStepPlants()
        {
            var t = new SplitStepTracker();
            var p0 = new TestPlayer(); var p1 = new TestPlayer();
            t.Press(1, 1.00, p1);
            t.Update(1.20, p0, p1);                       // no contact inside the window
            Assert.That(p1.PlantTimer, Is.GreaterThan(0));
            Assert.That(p1.BoostTimer, Is.EqualTo(0));
        }

        // ---------------- full games ----------------

        static MatchSimulation PlayBotGame(uint a, uint b, string scoring)
        {
            PhysicsConfig.ResetToDefaults();
            PhysicsConfig.Current.Match.Scoring = scoring;
            var m = new MatchSimulation(0);
            m.Bots[0] = new WallBot(a); m.Bots[1] = new WallBot(b);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (m.Phase != MatchPhase.MatchOver && m.Phase != MatchPhase.GameOver && m.StepCount < 240L * 1800) m.Step(dt);
            return m;
        }

        [TestCase("par")]
        [TestCase("english")]
        public void WallBotGameFinishesAndIsDeterministic(string scoring)
        {
            MatchSimulation m1 = PlayBotGame(7, 8, scoring);
            MatchSimulation m2 = PlayBotGame(7, 8, scoring);
            Assert.That(m1.Phase == MatchPhase.MatchOver || m1.Phase == MatchPhase.GameOver, "game did not finish");
            Assert.That(m1.Score.GameWinner, Is.Not.EqualTo(-1));
            Assert.That(m1.History.Count, Is.EqualTo(m2.History.Count));
            for (int i = 0; i < m1.History.Count; i++)
            {
                Assert.That(m2.History[i].ToString(), Is.EqualTo(m1.History[i].ToString()), "rally " + i);
                Assert.That(m2.History[i].Time, Is.EqualTo(m1.History[i].Time), "rally " + i);
            }
            Assert.That(m2.Ball.Pos.X, Is.EqualTo(m1.Ball.Pos.X));
            Assert.That(m2.StepCount, Is.EqualTo(m1.StepCount));
        }
    }
}
