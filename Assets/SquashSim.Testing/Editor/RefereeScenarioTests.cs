// ===========================================================
// Assets/SquashSim.Testing/Editor/RefereeScenarioTests.cs
// P2-M7: the referee's scenario suite. Each is a staged moment in a real
// rally (MatchSimulation.StageRally): the opponent has just struck a
// drive, the striker goes for it, the opponent stands, clears or crosses,
// and the striker appeals as the ball arrives. Every textbook situation
// must get the textbook call - and a brief crossing nothing at all.
// Also: the let survey stays within its targets (a short run).
// ===========================================================

using System;
using NUnit.Framework;
using SquashSim;
using SquashSim.Replays;

namespace SquashSim.Tests
{
    public class RefereeScenarioTests
    {
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); _lets = true; }

        static bool _lets = true;

        enum Opp { Stand, Clear, Cross }

        static readonly V3 From = new V3(-1.0, 1.0, 8.0), Drive = new V3(0.0, 7.0, -16.0);   // a drive back down the left side

        static MatchSimulation Match()
        {
            var s = new ReplaySetup { TwoPlayer = true, GamesToWin = 1 };
            ReplayMatch.Configure(s);
            PhysicsConfig.Current.Match.AppealsEnabled = _lets;
            return ReplayMatch.Build(s);
        }

        /// <summary>Where the drive will be playable (no player).</summary>
        static V3 Target()
        {
            var m = Match();
            m.StageRally(From, Drive, 0, 5.5, 2.4, 2.5);
            var rd = new BallReader();
            Assert.That(rd.Read(m.Ball, m.Surfaces, m.Players[0].Player, true), Is.True);   // as a striker on the T reads it
            return new V3(rd.X, 0, rd.Z);
        }

        sealed class Result { public RallyOutcome Outcome; public ObstructionKind Worst; }

        static Result Play(double sx, double sz, double ox, double oz, Opp opp, bool runs = true, bool appeal = true, double appealAt = 0.3)
        {
            var m = Match();
            m.StageRally(From, Drive, sx, sz, ox, oz);
            if (opp == Opp.Cross) m.Players[1].Player.Vel.Set(0, 0, PhysicsConfig.Current.Player.MaxSpeed);   // already on its way
            var t = Target();
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            bool asked = false;
            var res = new Result();
            for (int i = 0; i < 240 * 3 && m.Referee.Outcome == null; i++)
            {
                var me = m.Players[0].Player; var o = m.Players[1].Player;
                var a = m.Intents[0]; var b = m.Intents[1];
                double tx = m.Interference.Striker == 0 ? m.Interference.TargetX : t.X, tz = m.Interference.Striker == 0 ? m.Interference.TargetZ : t.Z;
                double dx = tx - me.Pos.X, dz = tz - me.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
                if (runs && d > 0.5) { a.Move.X = dx / d; a.Move.Z = dz / d; a.MoveMagnitude = 1; } else { a.Move.X = 0; a.Move.Z = 0; a.MoveMagnitude = 0; }
                switch (opp)
                {
                    case Opp.Stand: b.Move.X = 0; b.Move.Z = 0; b.MoveMagnitude = 0; break;
                    case Opp.Clear: b.Move.X = 0; b.Move.Z = 1; b.MoveMagnitude = 0.35; break;   // getting out of the way, but not quickly enough
                    case Opp.Cross: b.Move.X = 0; b.Move.Z = 1; b.MoveMagnitude = 1; b.Sprint = true; break;
                }
                if (appeal && !asked && m.Interference.Striker == 0 && m.Interference.TimeToBall <= appealAt && m.Interference.TimeToBall > 0) { a.AppealPressed = true; asked = true; }
                m.Step(dt);
                if (m.Interference.ThisTurn > res.Worst && m.Interference.Striker == 0) res.Worst = m.Interference.ThisTurn;
            }
            res.Outcome = m.Referee.Outcome;
            return res;
        }

        static void Expect(Result r, RallyEnd kind, string reason, string scenario)
        {
            UnityEngine.Debug.Log("[P2M7] " + scenario + ": " + (r.Outcome != null ? r.Outcome.Kind + " - " + r.Outcome.Reason : "play on") + " (worst " + r.Worst + ")");
            Assert.That(r.Outcome, Is.Not.Null, scenario + ": a call");
            Assert.That(r.Outcome.Kind, Is.EqualTo(kind), scenario + ": " + r.Outcome.Reason);
            if (reason != null) Assert.That(r.Outcome.Reason, Does.Contain(reason), scenario);
        }

        [Test] public void TheStagedDriveComesBackIntoCourt()
        {
            var t = Target();
            UnityEngine.Debug.Log("[P2M7] staged drive playable at (" + t.X.ToString("0.00") + ", " + t.Z.ToString("0.00") + ")");
            Assert.That(t.X, Is.InRange(-2.4, 0.4)); Assert.That(t.Z, Is.InRange(3.0, 9.5), "mid or back court");
        }

        [Test] public void OpponentClearIsNoLet()
        {
            var t = Target();
            Expect(Play(t.X + 1.8, t.Z - 2.2, 2.4, 2.5, Opp.Stand), RallyEnd.NoLet, "clear", "opponent clear");
        }

        [Test] public void InTheWayAndStandingIsAStroke()
        {
            var t = Target();
            Expect(Play(t.X + 3.0, t.Z + 0.2, t.X + 1.5, t.Z + 0.25, Opp.Stand), RallyEnd.Stroke, "did not clear", "in the way, standing");
        }

        [Test] public void InTheWayButClearingIsALet()
        {
            var t = Target();
            Expect(Play(t.X + 3.0, t.Z + 0.2, t.X + 1.5, t.Z + 0.25, Opp.Clear, appealAt: 0.45), RallyEnd.Let, "clearing", "in the way, clearing");
        }

        [Test] public void InTheLineToTheFrontWallIsAStroke()
        {
            var t = Target();
            Expect(Play(t.X + 1.2, t.Z + 1.2, t.X, t.Z - 1.4, Opp.Stand), RallyEnd.Stroke, "line", "in the line");
        }

        [Test] public void InTheSwingIsAStroke()
        {
            var t = Target();
            Expect(Play(t.X + 1.6, t.Z + 0.6, t.X - 0.65, t.Z + 0.3, Opp.Stand), RallyEnd.Stroke, "swing", "in the swing");
        }

        [Test] public void NotTryingIsNoLet()
        {
            var t = Target();
            Expect(Play(t.X + 3.0, t.Z + 0.2, t.X + 1.5, t.Z + 0.25, Opp.Stand, runs: false), RallyEnd.NoLet, null, "not trying");
        }

        [Test] public void CouldNotReachIsNoLet()
        {
            var t = Target();
            Expect(Play(t.X + 3.4, 1.4, t.X + 2.0, 3.5, Opp.Stand), RallyEnd.NoLet, null, "could not reach");
        }

        [Test] public void ABriefCrossingIsNotCounted()
        {
            var t = Target();
            // the opponent sprints across the striker's path (it is already on its way); no appeal, play on
            var r = Play(t.X + 3.0, t.Z + 0.2, t.X + 1.5, t.Z - 0.9, Opp.Cross, appeal: false);
            UnityEngine.Debug.Log("[P2M7] a brief crossing: worst " + r.Worst);
            Assert.That(r.Worst, Is.EqualTo(ObstructionKind.None), "a crossing a referee would not notice");
        }

        /// <summary>The striker runs at the hitting point through a standing opponent (L2: getting out of the way);
        /// returns the closest the striker got and how far the opponent was moved.</summary>
        static void RunThrough(bool assisted, bool runs, out double closest, out double moved)
        {
            var t = Target();
            var m = Match();
            m.StageRally(From, Drive, t.X + 3.0, t.Z + 0.2, t.X + 1.5, t.Z + 0.25);
            m.Players[1].Arcade.Assisted = assisted;
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            double ox = m.Players[1].Player.Pos.X, oz = m.Players[1].Player.Pos.Z;
            closest = double.MaxValue;
            for (int i = 0; i < 240 && m.Referee.Outcome == null; i++)
            {
                var me = m.Players[0].Player; var a = m.Intents[0];
                double dx = t.X - me.Pos.X, dz = t.Z - me.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
                closest = Math.Min(closest, d);
                if (runs && d > 0.3) { a.Move.X = dx / d; a.Move.Z = dz / d; a.MoveMagnitude = 1; } else a.MoveMagnitude = 0;
                m.Intents[1].MoveMagnitude = 0;
                m.Step(dt);
            }
            double mx = m.Players[1].Player.Pos.X - ox, mz = m.Players[1].Player.Pos.Z - oz;
            moved = Math.Sqrt(mx * mx + mz * mz);
        }

        [Test] public void TheNonStrikerGivesWayOnContact()
        {
            RunThrough(false, true, out double closest, out double moved);
            UnityEngine.Debug.Log("[P2M7] give way: striker got within " + closest.ToString("0.00") + " m, opponent moved " + moved.ToString("0.00") + " m");
            Assert.That(closest, Is.LessThan(0.6), "the striker slides past and reaches the ball");
            Assert.That(moved, Is.GreaterThan(0.3), "the standing opponent was moved aside");
        }

        [Test] public void AssistedNonStrikerIsSteeredOutOfTheWay()
        {
            RunThrough(true, false, out _, out double assisted);
            RunThrough(false, false, out _, out double manual);
            UnityEngine.Debug.Log("[P2M7] Assisted give-way: moved " + assisted.ToString("0.00") + " m (Non-assisted " + manual.ToString("0.00") + ")");
            Assert.That(manual, Is.LessThan(0.05), "Non-assisted: you steer yourself");
            Assert.That(assisted, Is.GreaterThan(0.4), "Assisted: stepped off the corridor with no input");
        }

        // ---------------- the referee stops a swing stroke himself (L3) ----------------

        [Test] public void PlayingWithTheOpponentInTheSwingIsStopped()
        {
            var t = Target();
            var m = Match();
            m.StageRally(From, Drive, t.X + 1.6, t.Z + 0.6, t.X - 0.65, t.Z + 0.3);
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            bool released = false;
            for (int i = 0; i < 240 * 3 && m.Referee.Outcome == null; i++)
            {
                var me = m.Players[0].Player; var a = m.Intents[0];
                double dx = t.X - me.Pos.X, dz = t.Z - me.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 0.5) { a.Move.X = dx / d; a.Move.Z = dz / d; a.MoveMagnitude = 1; } else a.MoveMagnitude = 0;
                if (i == 0) a.Hit.Pressed = true;                       // charge from the start ...
                a.Hit.Held = !released;
                if (!released && m.Interference.Striker == 0 && m.Interference.TimeToBall > 0 && m.Interference.TimeToBall < 0.12) { released = true; a.Hit.Held = false; }   // ... and let go as it arrives
                m.Intents[1].MoveMagnitude = 0;
                m.Step(dt);
            }
            var o = m.Referee.Outcome;
            UnityEngine.Debug.Log("[P2M7] played with the opponent in the swing: " + (o != null ? o.Kind + " - " + o.Reason : "play on"));
            Assert.That(o, Is.Not.Null);
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Stroke));
            Assert.That(o.Reason, Is.EqualTo(MatchSimulation.SwingStopReason), "no appeal: the referee stopped it");
            Assert.That(o.Winner, Is.EqualTo(0), "the stroke to the striker");
        }

        [Test] public void WithLetsOffTheRefereeLetsItGo()
        {
            var t = Target();
            _lets = false;
            var r = Play(t.X + 1.2, t.Z + 1.2, t.X, t.Z - 1.4, Opp.Stand);   // in the line, and an appeal
            string why = r.Outcome != null ? r.Outcome.Reason : "";
            UnityEngine.Debug.Log("[P2M7] Lets off, in the line and an appeal: " + (r.Outcome != null ? r.Outcome.ToString() : "play on"));
            Assert.That(why.StartsWith("Let -") || why.StartsWith("No let") || why.StartsWith("Stroke - opponent"), Is.False, "Lets off: appeals do nothing");
        }

        // ---------------- the ball hits a player (L3: the existing rules, in a live rally) ----------------

        static RallyOutcome BallAt(V3 pos, V3 vel, double x0, double z0, double x1, double z1)
        {
            var m = Match();
            PhysicsConfig.Current.MovementAssist.Enabled = false;   // the footwork assist's "never stand on the ball" would move them
            m.StageRally(pos, vel, x0, z0, x1, z1);   // athlete 1 has just struck from pos with vel; athlete 0 plays next
            double dt = PhysicsConfig.Current.Sim.FixedDt;
            for (int i = 0; i < 240 * 2 && m.Referee.Outcome == null; i++)
            {
                m.Intents[0].MoveMagnitude = 0; m.Intents[1].MoveMagnitude = 0;
                m.Step(dt);
            }
            var o = m.Referee.Outcome;
            UnityEngine.Debug.Log("[P2M7] ball hits a player: " + (o != null ? o.Kind + " - " + o.Reason : "play on"));
            Assert.That(o, Is.Not.Null);
            return o;
        }

        [Test] public void BallHitsTheOpponentGoingStraightIsAStroke()
        {
            var o = BallAt(new V3(0, 1, 6), new V3(0, 1, -15), 0, 4.8, 2.4, 8.5);
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Stroke)); Assert.That(o.Reason, Does.Contain("straight")); Assert.That(o.Winner, Is.EqualTo(1));
        }

        [Test] public void BallHitsTheOpponentViaTheSideWallIsALet()
        {
            var o = BallAt(new V3(0, 1, 6), new V3(-12, 5, -14), -1.2, 4.6, 2.4, 8.5);
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Let)); Assert.That(o.Reason, Does.Contain("side wall"));
        }

        [Test] public void BallHitsTheOpponentButWouldNotHaveBeenGoodIsDown()
        {
            var o = BallAt(new V3(0, 1, 6), new V3(0, -2, -8), 0, 4.8, 2.4, 8.5);
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Down)); Assert.That(o.Winner, Is.EqualTo(0), "the hitter loses it");
        }

        [Test] public void BallHitsTheNonStrikerAfterTheFrontWallIsAStroke()
        {
            var t = Target();
            var o = BallAt(From, Drive, 2.4, 6.0, t.X, t.Z);   // athlete 1 (who hit it) stands where it comes back to
            Assert.That(o.Kind, Is.EqualTo(RallyEnd.Stroke)); Assert.That(o.Reason, Does.Contain("non-striker")); Assert.That(o.Winner, Is.EqualTo(0));
        }

        // ---------------- the AI appeals like a person (L3) ----------------

        [Test] public void TheAiAppealsRarelyAtEasy()
        {
            foreach (var name in new[] { "retriever", "attacker", "shotmaker", "grinder" })
            {
                double easy = AiProfile.ByName(name).WithDifficulty(GameTuning.Ai.Levels[0]).AppealTendency;
                double hard = AiProfile.ByName(name).WithDifficulty(GameTuning.Ai.Levels[2]).AppealTendency;
                Assert.That(easy, Is.LessThanOrEqualTo(hard * 0.35), name);
                Assert.That(hard, Is.GreaterThan(0.5), name);
            }
        }

        [Test] public void TheSurveyIsWithinItsTargets()
        {
            var s = LetSurvey.Run(12);
            UnityEngine.Debug.Log("[P2M7] " + s);
            Assert.That(s.FlaggedShare, Is.LessThanOrEqualTo(0.10), "flagged turns (target 8% on the full survey)");
            Assert.That(s.Rallies, Is.GreaterThan(100));
            Assert.That(s.Count("NoLet"), Is.EqualTo(0), "the AI never asks when it was not trying, nor twice for nothing");
            Assert.That(s.CallShare, Is.InRange(0.003, 0.06), "calls: rare (1.5-3% on the full survey)");
        }
    }
}
