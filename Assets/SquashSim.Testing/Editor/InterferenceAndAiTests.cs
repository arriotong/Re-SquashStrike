// ===========================================================
// Assets/SquashSim.Testing/Editor/InterferenceAndAiTests.cs
// M3: obstruction detection geometry, the appeal (Let / No let), who
// may appeal, and the AI opponent v1 (beats the wall-bot, difficulty
// matters, deterministic, plays legal rally shots most of the time).
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class InterferenceAndAiTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.ResetToDefaults(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }

        // ---------------- detection geometry ----------------
        // Striker at (sx, sz) going to hit at (tx, tz); opponent at (ox, oz).

        [Test] public void OpponentBetweenHittingPointAndFrontWallIsLine()
        {
            var i = new Interference();
            Assert.That(i.Detect(-2.0, 8.0, -2.2, 5.0, -2.2, 7.0), Is.EqualTo(ObstructionKind.Line));
        }

        [Test] public void OpponentNextToTheHittingPointIsSwing()
        {
            var i = new Interference();
            Assert.That(i.Detect(0, 8.0, -1.0, 7.4, -1.6, 7.0), Is.EqualTo(ObstructionKind.Swing));
        }

        [Test] public void OpponentOnThePathIsAccess()
        {
            var i = new Interference();
            // Striker at the back going forward to a drop; opponent standing on that path, well off the line.
            Assert.That(i.Detect(2.0, 8.0, 1.7, 5.0, 1.0, 1.8), Is.EqualTo(ObstructionKind.Access).Or.EqualTo(ObstructionKind.Line));
            Assert.That(i.Detect(2.5, 5.0, 1.0, 5.0, -2.5, 5.0), Is.EqualTo(ObstructionKind.Access));
        }

        [Test] public void OpponentOnTheTWhileStrikerHitsFromTheBackCornerIsClear()
        {
            var i = new Interference();
            Assert.That(i.Detect(-2.4, 8.6, 0.0, 5.0, -2.5, 8.2), Is.EqualTo(ObstructionKind.None));
        }

        // ---------------- appeals ----------------

        static RallyReferee RallyWithP1ToPlay()
        {
            var r = new RallyReferee();
            r.BeginRally(0, 1);
            r.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 0, Point = new V3(0, 1, 5) },
                new V3(2.4, 0, 5.06));
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "frontWall", SurfaceId = "frontWall", Point = new V3(0.5, 3, 0) });
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "floor", SurfaceId = "floor", Point = new V3(-1.6, 0, 7.5) });
            r.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 1, Point = new V3(-1.6, 1, 7.5) }, new V3(-1.6, 0, 8));
            // P1's ball is now travelling to the front wall; P0 is the one going for the next ball.
            return r;
        }

        static InterferenceDecision D(InterferenceCall c) { return new InterferenceDecision { Call = c, Reason = c.ToString() }; }

        [Test] public void UpheldAppealIsALet()
        {
            var r = RallyWithP1ToPlay();
            Assert.That(r.PendingStriker, Is.EqualTo(0));
            Assert.That(r.OnAppeal(0, D(InterferenceCall.Let), 3.0), Is.True);
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.Let));
            Assert.That(r.Outcome.Winner, Is.EqualTo(-1));
        }

        [Test] public void StrokeAwardsTheRallyToTheAppellant()
        {
            var r = RallyWithP1ToPlay();
            r.OnAppeal(0, D(InterferenceCall.Stroke), 3.0);
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.Stroke));
            Assert.That(r.Outcome.Winner, Is.EqualTo(0));
            Assert.That(r.Outcome.Loser, Is.EqualTo(1));
        }

        [Test] public void NoLetAndTheAppellantLoses()
        {
            var r = RallyWithP1ToPlay();
            r.OnAppeal(0, D(InterferenceCall.NoLet), 3.0);
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.NoLet));
            Assert.That(r.Outcome.Winner, Is.EqualTo(1));
        }

        [Test] public void OnlyThePlayerGoingForTheBallMayAppeal()
        {
            var r = RallyWithP1ToPlay();
            Assert.That(r.OnAppeal(1, D(InterferenceCall.Stroke), 3.0), Is.False);
            Assert.That(r.Outcome, Is.Null);
        }

        [Test] public void NoAppealsDuringTheServe()
        {
            var r = new RallyReferee();
            r.BeginRally(0, 1);
            Assert.That(r.OnAppeal(1, D(InterferenceCall.Let), 1.0), Is.False);
            Assert.That(r.OnAppeal(0, D(InterferenceCall.Let), 1.0), Is.False);
        }

        // ---------------- interference v2: the referee's judgement ----------------

        static InterferenceCall Judge(ObstructionKind k, bool reachable, double effort, double dist, double secs, double clearing)
        {
            return new Interference().Judge(k, reachable, effort, dist, secs, clearing).Call;
        }

        [Test] public void NoObstructionIsNoLet() { Assert.That(Judge(ObstructionKind.None, true, 3, 3, 0, 0), Is.EqualTo(InterferenceCall.NoLet)); }
        [Test] public void UnreachableBallIsNoLet() { Assert.That(Judge(ObstructionKind.Access, false, 3, 3, 1, 0), Is.EqualTo(InterferenceCall.NoLet)); }
        [Test] public void NoEffortIsNoLet() { Assert.That(Judge(ObstructionKind.Access, true, 0.3, 3, 1, 0), Is.EqualTo(InterferenceCall.NoLet)); }
        [Test] public void AlreadyCloseNeedsNoRunning() { Assert.That(Judge(ObstructionKind.Access, true, 0.0, 0.8, 0.1, 0), Is.EqualTo(InterferenceCall.Let)); }
        [Test] public void InTheLineIsAStroke() { Assert.That(Judge(ObstructionKind.Line, true, 3, 3, 0.5, 3), Is.EqualTo(InterferenceCall.Stroke)); }
        [Test] public void InTheSwingIsAStroke() { Assert.That(Judge(ObstructionKind.Swing, true, 3, 3, 0.5, 3), Is.EqualTo(InterferenceCall.Stroke)); }
        [Test] public void AccessWhileClearingIsALet() { Assert.That(Judge(ObstructionKind.Access, true, 3, 3, 0.6, 1.8), Is.EqualTo(InterferenceCall.Let)); }
        [Test] public void AccessWithoutClearingIsAStroke() { Assert.That(Judge(ObstructionKind.Access, true, 3, 3, 0.6, 0.2), Is.EqualTo(InterferenceCall.Stroke)); }
        [Test] public void MomentaryAccessIsALet() { Assert.That(Judge(ObstructionKind.Access, true, 3, 3, 0.1, 0.0), Is.EqualTo(InterferenceCall.Let)); }

        // ---------------- ball hits a player ----------------

        static RallyReferee RefereeWithCourt()
        {
            var r = new RallyReferee { Surfaces = CourtSurfaces.Create() };
            r.BeginRally(0, 1);
            r.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 0 }, new V3(2.4, 0, 5.06));
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "frontWall", SurfaceId = "frontWall", Point = new V3(0.5, 3, 0) });
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "floor", SurfaceId = "floor", Point = new V3(-1.6, 0, 7.5) });
            r.OnRacketContact(new SimEvent { Type = SimEventType.RacketContact, PlayerIndex = 1 }, new V3(-1.6, 0, 8));
            return r;   // P1 has just struck; ball on its way to the front wall
        }

        static SimEvent BodyHit(int player, V3 pos, V3 vel)
        {
            return new SimEvent { Type = SimEventType.BodyHit, PlayerIndex = player, Point = pos.Clone(), InPos = pos, InVel = vel, Temperature = 30 };
        }

        [Test] public void BallGoingStraightToTheFrontWallHitsOpponentIsAStroke()
        {
            var r = RefereeWithCourt();
            r.OnBodyHit(BodyHit(0, new V3(-1.0, 1.2, 5.0), new V3(0, 1.0, -18)));
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.Stroke), r.Outcome.ToString());
            Assert.That(r.Outcome.Winner, Is.EqualTo(1));
        }

        [Test] public void BallGoingViaTheSideWallHitsOpponentIsALet()
        {
            var r = RefereeWithCourt();
            r.OnBodyHit(BodyHit(0, new V3(-1.0, 1.2, 5.0), new V3(-16, 4, -14)));   // side wall first, then the front wall at ~2 m
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.Let), r.Outcome.ToString());
        }

        [Test] public void BallThatWouldNotHaveBeenGoodLosesForTheStriker()
        {
            var r = RefereeWithCourt();
            r.OnBodyHit(BodyHit(0, new V3(-1.0, 0.4, 5.0), new V3(0, -6, -10)));   // heading into the floor
            Assert.That(r.Outcome.Winner, Is.EqualTo(0), r.Outcome.ToString());
        }

        [Test] public void BallAfterTheFrontWallHittingTheNonStrikerIsAStroke()
        {
            var r = RefereeWithCourt();
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "frontWall", SurfaceId = "frontWall", Point = new V3(0, 1.0, 0) });
            r.OnBodyHit(BodyHit(1, new V3(-1.6, 1.0, 6.5), new V3(0, 0, 10)));   // hits P1, who just played it
            Assert.That(r.Outcome.Kind, Is.EqualTo(RallyEnd.Stroke));
            Assert.That(r.Outcome.Winner, Is.EqualTo(0));
        }

        [Test] public void BallAfterTheFrontWallTouchingTheStrikerLosesForThem()
        {
            var r = RefereeWithCourt();
            r.OnImpact(new SimEvent { Type = SimEventType.Impact, SurfaceClass = "frontWall", SurfaceId = "frontWall", Point = new V3(0, 1.0, 0) });
            r.OnBodyHit(BodyHit(0, new V3(0.0, 1.0, 5.0), new V3(0, 0, 10)));
            Assert.That(r.Outcome.Winner, Is.EqualTo(1));
        }

        // ---------------- fatigue ----------------

        [Test] public void FatigueDrainsWhenRunningAndRecoversBetweenRallies()
        {
            var p = new TestPlayer();
            p.Speed = PhysicsConfig.Current.Player.MaxSpeed;
            for (int i = 0; i < 240 * 10; i++) Fatigue.Update(p, true, 1.0 / 240);   // 10 s flat out
            double tired = p.Stamina;
            Assert.That(tired, Is.LessThan(0.5));
            p.Speed = 0;
            for (int i = 0; i < 240 * 3; i++) Fatigue.Update(p, false, 1.0 / 240);    // a break
            Assert.That(p.Stamina, Is.GreaterThan(tired + 0.2));
        }

        [Test] public void StretchedStrokesCostStamina()
        {
            var p = new TestPlayer();
            Fatigue.OnStroke(p, ReachStates.Normal);
            Assert.That(p.Stamina, Is.EqualTo(1.0));
            Fatigue.OnStroke(p, ReachStates.Stretch);
            Assert.That(p.Stamina, Is.LessThan(1.0));
        }

        static double RunFor(double stamina)
        {
            var p = new TestPlayer(new V3(0, 0, 5)) { Stamina = stamina };
            var intent = new InputIntent { MoveMagnitude = 1 };
            intent.Move.X = 1;
            for (int i = 0; i < 120; i++) PlayerMovement.Step(p, intent, null, 1.0 / 240);
            return p.Pos.X;
        }

        [Test] public void TiredAthleteIsSlower()
        {
            Assert.That(RunFor(0.3), Is.LessThan(RunFor(1.0)));
        }

        [Test] public void FreshAthleteMovesExactlyAsInTheLab()
        {
            // Stamina 1.0 must take no fatigue path at all (the lab parity traces depend on it).
            var a = new TestPlayer(new V3(0, 0, 5));
            var b = new TestPlayer(new V3(0, 0, 5)) { Stamina = 1.0 };
            var intent = new InputIntent { MoveMagnitude = 1 };
            intent.Move.X = 0.6; intent.Move.Z = -0.8;
            for (int i = 0; i < 240; i++) { PlayerMovement.Step(a, intent, null, 1.0 / 240); PlayerMovement.Step(b, intent, null, 1.0 / 240); }
            Assert.That(b.Pos.X, Is.EqualTo(a.Pos.X));
            Assert.That(b.Pos.Z, Is.EqualTo(a.Pos.Z));
        }

        [Test] public void NoLetCountsAsAPointInPar()
        {
            var s = new MatchScore(ScoringSystem.Par, 0, 1, true);
            s.Apply(new RallyOutcome { Kind = RallyEnd.NoLet, Winner = 1, Loser = 0 });
            Assert.That(s.Points[1], Is.EqualTo(1));
            Assert.That(s.Server, Is.EqualTo(1));
        }

        [Test] public void AppealThroughTheMatchSimulationEndsTheRally()
        {
            // Two wall-bots; the moment P0 is going for a ball, P0 appeals. Nobody is obstructing
            // (bots stand apart), so it must be a No let unless detection saw an obstruction.
            var m = new MatchSimulation(1);
            m.Bots[1] = new WallBot(5);
            m.Bots[0] = new WallBot(6);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            bool appealed = false;
            for (int i = 0; i < 240 * 60 && !appealed; i++)
            {
                if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 1 && m.Referee.PendingStriker == 0 && m.Referee.Outcome == null)
                {
                    var bot = m.Bots[0];
                    m.Bots[0] = null;                       // take over P0 for one step
                    m.Intents[0].AppealPressed = true;
                    m.Step(dt);
                    m.Bots[0] = bot;
                    appealed = true;
                    break;
                }
                m.Step(dt);
            }
            Assert.That(appealed, Is.True, "never had a ball to appeal on");
            Assert.That(m.LastOutcome, Is.Not.Null);
            Assert.That(m.LastOutcome.Kind == RallyEnd.Let || m.LastOutcome.Kind == RallyEnd.NoLet || m.LastOutcome.Kind == RallyEnd.Stroke,
                m.LastOutcome.ToString());
        }

        // ---------------- AI opponent ----------------

        static MatchSimulation Play(IAthleteBrain a, IAthleteBrain b)
        {
            var m = new MatchSimulation(0);
            m.Bots[0] = a; m.Bots[1] = b;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            while (m.Phase != MatchPhase.GameOver && m.Phase != MatchPhase.MatchOver && m.StepCount < 240L * 1800) m.Step(dt);
            Assert.That(m.Phase == MatchPhase.GameOver || m.Phase == MatchPhase.MatchOver, "game did not finish");
            return m;
        }

        [Test] public void AiBeatsTheWallBot()
        {
            int wins = 0;
            for (int g = 0; g < 4; g++)
            {
                PhysicsConfig.ResetToDefaults();
                var m = Play(new OpponentBrain(AiProfile.Retriever().WithDifficulty(0.7), (uint)(10 + g)), new WallBot((uint)(20 + g)));
                if (m.Score.GameWinner == 0) wins++;
            }
            Assert.That(wins, Is.GreaterThanOrEqualTo(3), "AI won only " + wins + " of 4 against the wall-bot");
        }

        [Test] public void HardAiBeatsEasyAi()
        {
            int wins = 0;
            for (int g = 0; g < 4; g++)
            {
                PhysicsConfig.ResetToDefaults();
                var m = Play(new OpponentBrain(AiProfile.Grinder().WithDifficulty(0.9), (uint)(30 + g)),
                             new OpponentBrain(AiProfile.Grinder().WithDifficulty(0.1), (uint)(40 + g)));
                if (m.Score.GameWinner == 0) wins++;
            }
            Assert.That(wins, Is.GreaterThanOrEqualTo(3), "hard AI won only " + wins + " of 4 against easy AI");
        }

        [Test] public void AiGameIsDeterministic()
        {
            PhysicsConfig.ResetToDefaults();
            var a = Play(new OpponentBrain(AiProfile.ShotMaker().WithDifficulty(0.6), 77), new OpponentBrain(AiProfile.Attacker().WithDifficulty(0.6), 78));
            PhysicsConfig.ResetToDefaults();
            var b = Play(new OpponentBrain(AiProfile.ShotMaker().WithDifficulty(0.6), 77), new OpponentBrain(AiProfile.Attacker().WithDifficulty(0.6), 78));
            Assert.That(b.History.Count, Is.EqualTo(a.History.Count));
            for (int i = 0; i < a.History.Count; i++) Assert.That(b.History[i].ToString(), Is.EqualTo(a.History[i].ToString()));
            Assert.That(b.StepCount, Is.EqualTo(a.StepCount));
        }

        [Test] public void AiRalliesProduceStatsForFatigueTuning()
        {
            PhysicsConfig.ResetToDefaults();
            var m = Play(new OpponentBrain(AiProfile.Grinder().WithDifficulty(0.8), 5), new OpponentBrain(AiProfile.Retriever().WithDifficulty(0.8), 6));
            Assert.That(m.Stats.Count, Is.EqualTo(m.History.Count));
            Assert.That(m.Stats.Exists(s => s.Shots >= 4), "expected at least one rally of 4+ shots");
            Assert.That(m.Stats.TrueForAll(s => s.Run0 >= 0 && s.Run1 >= 0 && s.Seconds >= 0));
        }
    }
}
