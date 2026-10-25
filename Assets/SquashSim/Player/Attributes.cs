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
//   Speed      -9%         +9%     top speed and acceleration
//   Stamina    x1.3        x0.7    stamina drain (running, sprints, dives)
//   Power      -9%         +9%     drive and kill pace
//   Control    x1.5        x0.5    aim spread and gross-miss chance
//   Speed      x0.95       x1.08   reach (how far from you the ball is hittable)
//   Control    x0.8        x1.35   the grace after an early release
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

        public static AthleteAttributes NeutralSet() { return new AthleteAttributes(); }

        public bool IsNeutral { get { return Speed == Neutral && Stamina == Neutral && Power == Neutral && Control == Neutral; } }

        /// <summary>1 at 5, at0 at 0, at10 at 10, straight lines between.</summary>
        static double Scale(double v, double at0, double at10)
        {
            if (v == Neutral) return 1.0;
            v = v < 0 ? 0 : (v > Max ? Max : v);
            return v < Neutral ? 1 + (at0 - 1) * (Neutral - v) / Neutral : 1 + (at10 - 1) * (v - Neutral) / Neutral;
        }

        public double SpeedScale { get { return Scale(Speed, 0.91, 1.09); } }
        public double DrainScale { get { return Scale(Stamina, 1.3, 0.7); } }
        public double PowerScale { get { return Scale(Power, 0.91, 1.09); } }
        public double ControlScale { get { return Scale(Control, 1.5, 0.5); } }
        /// <summary>Reach and the release grace: points are won and lost on the timing of the hit, which spread, pace and
        /// foot speed alone barely touched (2026-10-25: a maxed player won no more rallies).</summary>
        public double ReachScale { get { return Scale(Speed, 0.95, 1.08); } }
        public double GraceScale { get { return Scale(Control, 0.8, 1.35); } }

        public AthleteAttributes Clone() { return (AthleteAttributes)MemberwiseClone(); }

        public void CopyFrom(AthleteAttributes a)
        {
            if (a == null) { Speed = Stamina = Power = Control = Neutral; return; }
            Speed = a.Speed; Stamina = a.Stamina; Power = a.Power; Control = a.Control;
        }

        public double Mean { get { return (Speed + Stamina + Power + Control) / 4; } }

        public override string ToString() { return "spd " + Speed + " sta " + Stamina + " pow " + Power + " ctl " + Control; }
    }
}
