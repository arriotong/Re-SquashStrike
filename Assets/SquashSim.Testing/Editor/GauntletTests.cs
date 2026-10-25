// ===========================================================
// Assets/SquashSim.Testing/Editor/GauntletTests.cs
// P3-R5: the Gauntlet's rules.
//   - a floor won scores, moves on and offers three different perks; a
//     perk taken applies once and lasts
//   - a floor lost costs a life (the golden racket saves one); no lives,
//     the run is over
//   - the opposition hardens every floor; the same seed, the same run
// ===========================================================

using NUnit.Framework;
using SquashSim;
using SquashSim.Modes;

namespace SquashSim.Tests
{
    public class GauntletTests
    {
        [Test] public void AFloorWonScoresAndOffersThreePerks()
        {
            var r = Gauntlet.NewRun(77);
            Assert.That(r.Floor, Is.EqualTo(1)); Assert.That(r.Lives, Is.EqualTo(Gauntlet.StartLives));
            Gauntlet.Won(r, 4);
            Assert.That(r.Floor, Is.EqualTo(2)); Assert.That(r.Score, Is.EqualTo(140));
            Assert.That(r.AwaitingPerk, Is.True);
            Assert.That(r.Offer.Count, Is.EqualTo(3)); Assert.That(r.Offer, Is.Unique);
            Gauntlet.Won(r, 4);
            Assert.That(r.Floor, Is.EqualTo(2), "no next floor before the perk is picked");
            Assert.That(Gauntlet.Choose(r, "nonsense"), Is.False);
            string pick = r.Offer[0];
            Assert.That(Gauntlet.Choose(r, pick), Is.True);
            Assert.That(r.Perks, Is.EqualTo(new[] { pick })); Assert.That(r.AwaitingPerk, Is.False);
        }

        [Test] public void PerksDoWhatTheySay()
        {
            var r = Gauntlet.NewRun(5);
            Gauntlet.Find("speed").Apply(r); Assert.That(r.Attributes.Speed, Is.EqualTo(7));
            Gauntlet.Find("all").Apply(r); Assert.That(r.Attributes.Speed, Is.EqualTo(8)); Assert.That(r.Attributes.Control, Is.EqualTo(6));
            for (int i = 0; i < 4; i++) Gauntlet.Find("speed").Apply(r);
            Assert.That(r.Attributes.Speed, Is.EqualTo(AthleteAttributes.Max), "capped at 10");
            Assert.That(Gauntlet.Find("speed").Allowed(r), Is.False, "no longer offered");
            Gauntlet.Find("head").Apply(r); Assert.That(r.HeadStart, Is.EqualTo(2));
            Gauntlet.Find("life").Apply(r); Assert.That(r.Lives, Is.EqualTo(4));
        }

        [Test] public void LivesGoneTheRunIsOver()
        {
            var r = Gauntlet.NewRun(9);
            Gauntlet.Find("golden").Apply(r);
            Gauntlet.Lost(r);
            Assert.That(r.Lives, Is.EqualTo(3), "the golden racket saves a life"); Assert.That(r.Golden, Is.False);
            for (int i = 0; i < 2; i++) Gauntlet.Lost(r);
            Assert.That(r.Over, Is.False); Assert.That(r.Lives, Is.EqualTo(1));
            Gauntlet.Lost(r);
            Assert.That(r.Over, Is.True); Assert.That(r.Floor, Is.EqualTo(1), "the floor is replayed, not passed");
        }

        [Test] public void TheOppositionHardensAndTheRunIsRepeatable()
        {
            Assert.That(Gauntlet.Difficulty(1), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(Gauntlet.Difficulty(12), Is.EqualTo(0.59).Within(1e-9));
            Assert.That(Gauntlet.Difficulty(40), Is.EqualTo(0.95).Within(1e-9));
            for (int f = 2; f < 25; f++) Assert.That(Gauntlet.Difficulty(f), Is.GreaterThanOrEqualTo(Gauntlet.Difficulty(f - 1)));
            var a = Gauntlet.NewRun(123); var b = Gauntlet.NewRun(123);
            for (int f = 1; f <= 6; f++) Assert.That(Gauntlet.Opponent(a, f), Is.EqualTo(Gauntlet.Opponent(b, f)));
            Gauntlet.Won(a, 2); Gauntlet.Won(b, 2);
            Assert.That(a.Offer, Is.EqualTo(b.Offer), "the same seed, the same offers");
        }
    }
}
