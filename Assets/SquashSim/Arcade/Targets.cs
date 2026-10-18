// ===========================================================
// Assets/SquashSim/Arcade/Targets.cs
// P2-M2: what the Target Arcade (and the drills) aim at, judged on
// the sim's own impact events - like the referee, never from frames.
//
//   FrontRing   a ring on the front wall {X, Y, R}: judged on the
//               first front-wall impact after your contact
//   FloorZone   a box on the floor {X0..X1, Z0..Z1}: judged on the
//               first floor bounce after the front wall
//   Spot        a floor spot to stand on (ghosting) {X, Z, R}
//   Nick        any nick (the ball dying in the wall-floor join)
// ===========================================================

namespace SquashSim.Arcade
{
    public enum TargetKind { FrontRing, FloorZone, Spot, Nick }

    public sealed class Target
    {
        public TargetKind Kind;
        public double X, Y, R;                    // ring / spot
        public double X0, X1, Z0, Z1;             // floor zone
        public int Points = 100;
        public string Label;

        public static Target Ring(double x, double y, double r, int points = 100)
        {
            return new Target { Kind = TargetKind.FrontRing, X = x, Y = y, R = r, Points = points };
        }

        public static Target Zone(double x0, double x1, double z0, double z1, int points = 100, string label = null)
        {
            return new Target
            {
                Kind = TargetKind.FloorZone, X0 = System.Math.Min(x0, x1), X1 = System.Math.Max(x0, x1),
                Z0 = System.Math.Min(z0, z1), Z1 = System.Math.Max(z0, z1), Points = points, Label = label
            };
        }

        public static Target StandOn(double x, double z, double r) { return new Target { Kind = TargetKind.Spot, X = x, Z0 = z, R = r }; }

        /// <summary>How far from the ring's centre (m) this front-wall point is, or -1 if not a ring.</summary>
        public double RingDistance(V3 p)
        {
            if (Kind != TargetKind.FrontRing) return -1;
            return System.Math.Sqrt((p.X - X) * (p.X - X) + (p.Y - Y) * (p.Y - Y));
        }

        public bool InRing(V3 p) { return Kind == TargetKind.FrontRing && RingDistance(p) <= R; }

        public bool InZone(V3 p)
        {
            return Kind == TargetKind.FloorZone && p.X >= X0 && p.X <= X1 && p.Z >= Z0 && p.Z <= Z1;
        }

        public bool OnSpot(V3 pos)
        {
            return Kind == TargetKind.Spot && System.Math.Sqrt((pos.X - X) * (pos.X - X) + (pos.Z - Z0) * (pos.Z - Z0)) <= R;
        }

        /// <summary>Precision scoring: 3 in the inner third, 2 in the middle third, 1 on the outer third, 0 outside.</summary>
        public int PrecisionRing(V3 p)
        {
            double d = RingDistance(p);
            if (d < 0 || d > R) return 0;
            if (d <= R / 3) return 3;
            if (d <= 2 * R / 3) return 2;
            return 1;
        }
    }

    /// <summary>What happened to a shot, for the views and the HUD.</summary>
    public enum ArcadeEventKind { Hit, Miss, Fault, ShotCalled, NewTarget, Over }

    public sealed class ArcadeEvent
    {
        public ArcadeEventKind Kind;
        public Target Target;
        public V3 Point;                 // where it was judged (may be null)
        public int Points;               // added to the score
        public int Combo;                // the combo after this shot
        public bool Great;               // a great hit (bonus)
        public string Note;              // "x2 combo", "WRONG SHOT", "FAULT", ...
    }
}
