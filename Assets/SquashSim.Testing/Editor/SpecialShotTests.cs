// ===========================================================
// Assets/SquashSim.Testing/Editor/SpecialShotTests.cs
// 2026-11-22 the Special Shot (MatchSimulation.Special):
//   - the meter (the Special card) fills 1.5x slower; mistakes cost meter
//   - 2026-11-22 evening: a squat and a jump where you stand; the defender's
//     duel (Focus + let go of Hit on the ring)
//   - a full meter and your ball: Special teleports you to it, the rally
//     stands still while you aim, the meter drains, Hit (or empty) fires
//   - the shot is almost unreturnable; Focus (meter) slows it and the
//     defender gets far more back
// ===========================================================

using NUnit.Framework;
using SquashSim;

namespace SquashSim.Tests
{
    public class SpecialShotTests
    {
        [SetUp] public void Defaults() { PhysicsConfig.UseGameProfile(); }
        [TearDown] public void Restore() { PhysicsConfig.ResetToDefaults(); }
        static double Dt { get { return PhysicsConfig.Current.Sim.FixedDt; } }

        static MatchSimulation Match(int seed)
        {
            var c = PhysicsConfig.Current.Match;
            c.WarmUpEnabled = false;
            c.GamesToWin = 3;
            var m = new MatchSimulation(0);
            m.Bots[0] = new OpponentBrain(AiProfile.ByName("attacker").WithDifficulty(0.8), (uint)(11 + seed));
            m.Bots[1] = new OpponentBrain(AiProfile.ByName("retriever").WithDifficulty(0.8), (uint)(12 + seed));
            for (int i = 0; i < 2; i++)
            {
                m.Players[i].ArcadeHitEnabled = true; m.Players[i].WallAimEnabled = true;
                m.Players[i].Player.Mods = RunMods.From(new[] { new RunMod { Id = "special", Level = 1 } });
            }
            return m;
        }

        [Test] public void TheMeterFillsThreeTimesSlowerAndMistakesCostIt()
        {
            var plain = RunMods.From(new[] { new RunMod { Id = "inferno", Level = 1 } });
            var special = RunMods.From(new[] { new RunMod { Id = "special", Level = 1 } });
            plain.MeterShot(); special.MeterShot();
            Assert.That(special.Power, Is.EqualTo(plain.Power * RunMods.SpecialMeterRate).Within(1e-9), "two thirds as fast (2026-11-22 evening: 2x the third)");
            special.Power = 0.5;
            special.MeterPenalty(RunMods.ErrorPenalty);
            Assert.That(special.Power, Is.EqualTo(0.5 - RunMods.ErrorPenalty).Within(1e-9));
            plain.Power = 0.5; plain.MeterPenalty(RunMods.ErrorPenalty);
            Assert.That(plain.Power, Is.EqualTo(0.5), "only the Special card's meter");
            special.Power = 1; special.AtStroke(1, false);
            Assert.That(special.UltNow, Is.False, "a charged stroke doesn't fire it any more - the Special button does");
        }

        /// <summary>Rallies where athlete 0 fires a Special Shot (a full meter on each of its balls); athlete 1 may or may not defend.
        /// Returns (specials fired, rallies the striker won after one, defended).</summary>
        static (int fired, int won, int defended) Play(bool defend, int rallies)
        {
            var m = Match(defend ? 1 : 0);
            int fired = 0, won = 0, wasFired = 0, returned = 0, contactsAt = 0;
            bool frozeSeen = false;
            int done = 0, matches = 0, defendedSum = 0;
            while (done + m.History.Count < rallies && matches < 20)
            {
                if (m.Phase == MatchPhase.MatchOver || m.Time > 3600) { done += m.History.Count; matches++; defendedSum += m.SpecialsDefended; m = Match((defend ? 1 : 0) + 10 * matches); wasFired = 0; continue; }
                var a = m.Players[0].Player.Mods; var b = m.Players[1].Player.Mods;
                if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 2 && m.ZoneBy < 0 && m.SpecialBy < 0 && wasFired == 0) a.Power = 1;
                if (m.ZoneBy < 0) b.Power = defend ? 0.6 : 0;                         // enough to defend, never to fire its own
                int before = m.History.Count;
                bool zoning = m.ZoneBy == 0;
                var ballAt = m.Ball.Pos.Clone();
                m.Step(Dt);
                if (zoning && m.ZoneBy == 0) { frozeSeen = true; Assert.That(m.Ball.Pos.X, Is.EqualTo(ballAt.X).Within(1e-9), "the ball hangs while they aim"); }
                if (m.SpecialsFired > fired) { fired = m.SpecialsFired; wasFired = 1; contactsAt = m.RallyContacts[1]; }
                if (wasFired == 1 && m.RallyContacts[1] > contactsAt) { returned++; wasFired = 2; }
                if (m.History.Count > before && wasFired == 1 && m.History[m.History.Count - 1].Winner == 0) won++;
                if (m.History.Count > before && wasFired == 1 && defend) { var oc = m.History[m.History.Count - 1]; UnityEngine.Debug.Log("[SpecialEnd] " + oc.Kind + " w" + oc.Winner + " " + oc.Reason + " | def at z " + m.Players[1].Player.Pos.Z.ToString("0.0")); }
                if (m.History.Count > before) wasFired = 0;
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
            }
            Assert.That(frozeSeen, Is.True, "the rally stood still while aiming");
            UnityEngine.Debug.Log("[Special] defend " + defend + ": fired " + fired + ", returned " + returned + ", won outright " + won);
            return (fired, won, defendedSum + m.SpecialsDefended);
        }

        /// <summary>2026-11-22 evening: the Special Shot starts where you stand - a squat, then straight up (no teleport to the T).</summary>
        [Test] public void ItSquatsAndJumpsRightWhereYouStand()
        {
            var m = Match(5);
            double px = 0, pz = 0; bool seen = false, squat = false, air = false;
            while (m.Time < 900 && m.Phase != MatchPhase.MatchOver && !(seen && air))
            {
                var p0 = m.Players[0].Player.Pos;
                if (m.ChargeBy < 0) { px = p0.X; pz = p0.Z; }
                if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 2 && m.ChargeBy < 0 && m.ZoneBy < 0 && m.SpecialBy < 0) m.Players[0].Player.Mods.Power = 1;
                bool was = m.ChargeBy == 0;
                m.Step(Dt);
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
                if (!was && m.ChargeBy == 0)
                {
                    seen = true;
                    var at = m.Players[0].Player.Pos;
                    Assert.That(DMath.Hypot(at.X - px, at.Z - pz), Is.LessThan(0.95), "right where they stood (kept off the walls)");
                    Assert.That(m.Squatting(0), Is.True, "a squat first");
                    Assert.That(at.Y, Is.EqualTo(0).Within(1e-9));
                    squat = true;
                }
                if (seen && m.ZoneBy == 0) { air = true; Assert.That(m.Players[0].Player.Pos.Y, Is.EqualTo(MatchSimulation.AirHeight).Within(0.01), "then up in the air, aiming"); }
            }
            Assert.That(seen && squat && air, Is.True, "a Special Shot was charged and aimed");
        }

        /// <summary>The duel: the defender (a person now) holds Focus and lets go of Hit as the ball arrives - won; early - lost.</summary>
        static MatchSimulation DuelMoment(int seed)
        {
            for (int k = 0; k < 6; k++)
            {
                var m = Match(seed + 20 * k);
                while (m.Time < 900 && m.Phase != MatchPhase.MatchOver)
                {
                    if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 2 && m.ChargeBy < 0 && m.ZoneBy < 0 && m.WindupBy < 0 && m.SpecialBy < 0) m.Players[0].Player.Mods.Power = 1;
                    m.Players[1].Player.Mods.Power = System.Math.Min(m.Players[1].Player.Mods.Power, 0.99);
                    m.Step(Dt);
                    if (m.Phase == MatchPhase.GameOver) m.NextGame();
                    if (m.SpecialBy == 0 && m.WindupBy < 0)
                    {
                        m.Bots[1] = null; m.Intents[1] = new InputIntent();
                        m.Players[1].Player.Mods.Power = 0.8;
                        m.Intents[1].FocusHeld = true;
                        return m;
                    }
                }
            }
            return null;
        }

        [Test] public void LettingGoOnTheRingWinsTheDuel()
        {
            var m = DuelMoment(3);
            Assert.That(m, Is.Not.Null, "a Special Shot came");
            for (int k = 0; k < 400 && (double.IsNaN(m.DuelAt) || m.Time < m.DuelAt - Dt * 0.5) && m.SpecialIncoming(1); k++) m.Step(Dt);
            Assume.That(m.SpecialIncoming(1) && m.Focusing(1), "still coming, focusing");
            m.Intents[1].Hit.Released = true; m.Step(Dt); m.Intents[1].Hit.Released = false;
            Assert.That(m.DuelResult, Is.EqualTo(1), "on the ring: the duel won");
            Assert.That(m.SpecialDefended, Is.True);
            Assert.That(m.Players[1].Player.Mods.ReachCut, Is.EqualTo(0), "the cut lifted");
        }

        [Test] public void LettingGoEarlyLosesTheDuel()
        {
            var m = DuelMoment(4);
            Assert.That(m, Is.Not.Null, "a Special Shot came");
            for (int k = 0; k < 400 && double.IsNaN(m.DuelAt) && m.SpecialIncoming(1); k++) m.Step(Dt);
            Assume.That(m.SpecialIncoming(1) && m.Focusing(1) && m.DuelAt - m.Time > MatchSimulation.DuelWindow * 2, "time to spare");
            m.Intents[1].Hit.Released = true; m.Step(Dt); m.Intents[1].Hit.Released = false;
            Assert.That(m.DuelResult, Is.EqualTo(-1), "too early: lost");
            Assert.That(m.Players[1].Player.Mods.ReachCut, Is.EqualTo(MatchSimulation.SpecialReachCut), "still cut");
            int lost = m.DuelsLost;
            m.Intents[1].Hit.Released = true; m.Step(Dt);
            Assert.That(m.SpecialDefended, Is.False, "one try only");
        }

        [Test] public void ASpecialShotIsAlmostUnreturnableUnlessFocused()
        {
            var raw = Play(false, 120);
            var foc = Play(true, 40);
            double rawRate = raw.fired == 0 ? 0 : (double)raw.won / raw.fired, focRate = foc.fired == 0 ? 0 : (double)foc.won / foc.fired;
            UnityEngine.Debug.Log("[Special] no focus: fired " + raw.fired + ", won " + raw.won + " (" + rawRate.ToString("0.00") + ")   " +
                                  "focus: fired " + foc.fired + ", won " + foc.won + " (" + focRate.ToString("0.00") + "), defended " + foc.defended);
            Assert.That(raw.fired, Is.GreaterThan(5), "the AI fires them");
            Assert.That(rawRate, Is.GreaterThan(0.75), "without Focus, it mostly wins the point");
            Assert.That(foc.defended, Is.GreaterThan(3), "the AI defends with Focus");
            Assert.That(focRate, Is.LessThan(rawRate - 0.15), "Focus gets many more back");
        }
    }
}
