// ===========================================================
// Assets/SquashSim/Player/Attributes.cs
// P3-C1: an athlete's attributes, 0..10 each - the career's player
// growth and the Gauntlet's perks.
//
//   5 is the game as it has always been: every multiplier is exactly 1,
//   and every place that reads one skips the arithmetic at 1, so a
//   neutral athlete's match is bit-identical (SimParity, the bundled
//   replays, the checksum test).
//
//              at 0        at 10
//   Speed      -20%        +20%    top speed and acceleration
//   Stamina    x1.4        x0.6    stamina drain (the rally, running, charged strokes, sprints, dives)
//   Power      -20%        +20%    drive and kill pace
//   Control    x1.4        x0.6    aim spread and gross-miss chance ("Accuracy" in the game)
//   Speed      x0.9        x1.1    reach (how far from you the ball is hittable)
//   Control    x0.8        x1.35   the grace after an early release
//   Stability  x1.4        x0.6    P7 phase 5, gear only: off-centre mishits, and the
//                                  spray of a bad contact (stretched, jammed, late)
//
//   P7 phase 2 (2026-10-26, the user: "start is everything -20% of the
//   quick play character and maxed out is +20%"): each level is 4%.
//
//   2026-10-25 playtest round 2: at +-6% / x0.6 a maxed career player
//   was barely better - stronger now (replays kept before show as
//   diverged where the athlete had trained attributes)
// ===========================================================

namespace SquashSim
{
    [System.Serializable]
    public sealed class AthleteAttributes
    {
        public const double Neutral = 5, Max = 10;
        public double Speed = Neutral, Stamina = Neutral, Power = Neutral, Control = Neutral;
        public double Stability = Neutral;                      // P7 phase 5: the career's gear only (5 everywhere else)

        public static AthleteAttributes NeutralSet() { return new AthleteAttributes(); }

        public bool IsNeutral { get { return Speed == Neutral && Stamina == Neutral && Power == Neutral && Control == Neutral && Stability == Neutral; } }

        /// <summary>1 at 5, at0 at 0, at10 at 10, straight lines between.</summary>
        static double Scale(double v, double at0, double at10)
        {
            if (v == Neutral) return 1.0;
            v = v < 0 ? 0 : (v > Max ? Max : v);
            return v < Neutral ? 1 + (at0 - 1) * (Neutral - v) / Neutral : 1 + (at10 - 1) * (v - Neutral) / Neutral;
        }

        public double SpeedScale { get { return Scale(Speed, 0.8, 1.2); } }
        public double DrainScale { get { return Scale(Stamina, 1.4, 0.6); } }
        public double PowerScale { get { return Scale(Power, 0.8, 1.2); } }
        public double ControlScale { get { return Scale(Control, 1.4, 0.6); } }
        /// <summary>Reach and the release grace: points are won and lost on the timing of the hit, which spread, pace and
        /// foot speed alone barely touched (2026-10-25: a maxed player won no more rallies).</summary>
        public double ReachScale { get { return Scale(Speed, 0.9, 1.1); } }
        public double GraceScale { get { return Scale(Control, 0.8, 1.35); } }
        public double StabilityScale { get { return Scale(Stability, 1.4, 0.6); } }

        public AthleteAttributes Clone() { return (AthleteAttributes)MemberwiseClone(); }

        public void CopyFrom(AthleteAttributes a)
        {
            if (a == null) { Speed = Stamina = Power = Control = Stability = Neutral; return; }
            Speed = a.Speed; Stamina = a.Stamina; Power = a.Power; Control = a.Control; Stability = a.Stability;
        }

        public double Mean { get { return (Speed + Stamina + Power + Control) / 4; } }

        public override string ToString() { return "spd " + Speed + " sta " + Stamina + " pow " + Power + " ctl " + Control + (Stability != Neutral ? " stb " + Stability : ""); }
    }
}
