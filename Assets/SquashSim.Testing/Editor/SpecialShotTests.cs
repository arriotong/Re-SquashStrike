// ===========================================================
// Assets/SquashSim.Testing/Editor/SpecialShotTests.cs
// 2026-11-22 the Special Shot (MatchSimulation.Special):
//   - the meter (the Special card) fills 3x slower; mistakes cost meter
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
            Assert.That(special.Power, Is.EqualTo(plain.Power / 3).Within(1e-9), "a third as fast");
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
            while (m.History.Count < rallies && m.Time < 3600 && m.Phase != MatchPhase.MatchOver)
            {
                var a = m.Players[0].Player.Mods; var b = m.Players[1].Player.Mods;
                if (m.Phase == MatchPhase.Rally && m.Referee.Shots >= 2 && m.ZoneBy < 0 && m.SpecialBy < 0 && wasFired == 0) a.Power = 1;
                if (m.ZoneBy < 0) b.Power = defend ? RunMods.FocusCost + 0.01 : 0;      // enough to defend, never to fire its own
                int before = m.History.Count;
                bool zoning = m.ZoneBy == 0;
                var ballAt = m.Ball.Pos.Clone();
                m.Step(Dt);
                if (zoning && m.ZoneBy == 0) { frozeSeen = true; Assert.That(m.Ball.Pos.X, Is.EqualTo(ballAt.X).Within(1e-9), "the ball hangs while they aim"); }
                if (m.SpecialsFired > fired) { fired = m.SpecialsFired; wasFired = 1; contactsAt = m.RallyContacts[1]; }
                if (wasFired == 1 && m.RallyContacts[1] > contactsAt) { returned++; wasFired = 2; }
                if (m.History.Count > before && wasFired == 1 && m.History[m.History.Count - 1].Winner == 0) won++;
                if (m.History.Count > before) wasFired = 0;
                if (m.Phase == MatchPhase.GameOver) m.NextGame();
            }
            Assert.That(frozeSeen, Is.True, "the rally stood still while aiming");
            UnityEngine.Debug.Log("[Special] defend " + defend + ": fired " + fired + ", returned " + returned + ", won outright " + won);
            return (fired, won, m.SpecialsDefended);
        }

        [Test] public void ASpecialShotIsAlmostUnreturnableUnlessFocused()
        {
            var raw = Play(false, 40);
            var foc = Play(true, 40);
            double rawRate = raw.fired == 0 ? 0 : (double)raw.won / raw.fired, focRate = foc.fired == 0 ? 0 : (double)foc.won / foc.fired;
            UnityEngine.Debug.Log("[Special] no focus: fired " + raw.fired + ", won " + raw.won + " (" + rawRate.ToString("0.00") + ")   " +
                                  "focus: fired " + foc.fired + ", won " + foc.won + " (" + focRate.ToString("0.00") + "), defended " + foc.defended);
            Assert.That(raw.fired, Is.GreaterThan(8), "the AI fires them");
            Assert.That(rawRate, Is.GreaterThan(0.75), "without Focus, it mostly wins the point");
            Assert.That(foc.defended, Is.GreaterThan(3), "the AI defends with Focus");
            Assert.That(focRate, Is.LessThan(rawRate - 0.15), "Focus gets many more back");
        }
    }
}
