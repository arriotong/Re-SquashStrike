// ===========================================================
// Assets/SquashSim/Math/V3.cs
// Port of lab/core/Vec3.js.
//
// A MUTABLE REFERENCE type on purpose: the lab passes {x,y,z} objects
// around and mutates them in place (ball.pos, racket.headVel, ...).
// Keeping the same semantics makes the port a line-for-line
// translation, which is what makes bit-exact parity achievable.
// Do not "optimise" this into a struct without re-running SimParity.
//
// Simulation coordinates (same as the lab, right-handed):
//   +Y up, front wall at z = 0, back wall at z = 9.75,
//   side walls at x = -3.2 / +3.2. Only the view layer converts to
//   Unity's left-handed world (see SimToWorld in the game assembly).
// ===========================================================

namespace SquashSim
{
    public sealed class V3
    {
        public double X, Y, Z;

        public V3() { }
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public V3 Set(double x, double y, double z) { X = x; Y = y; Z = z; return this; }
        public V3 CopyFrom(V3 b) { X = b.X; Y = b.Y; Z = b.Z; return this; }
        public V3 Clone() { return new V3(X, Y, Z); }

        public override string ToString() { return "(" + X + ", " + Y + ", " + Z + ")"; }

        public static double Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

        public static double Length(V3 a) { return System.Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z); }

        /// <summary>out = a x b. Safe when out aliases a or b.</summary>
        public static V3 Cross(V3 output, V3 a, V3 b)
        {
            double x = a.Y * b.Z - a.Z * b.Y;
            double y = a.Z * b.X - a.X * b.Z;
            double z = a.X * b.Y - a.Y * b.X;
            output.X = x; output.Y = y; output.Z = z;
            return output;
        }
    }

    /// <summary>Two-component (x, z) floor-plane vector, mutable like V3.</summary>
    public sealed class V2
    {
        public double X, Z;
        public V2() { }
        public V2(double x, double z) { X = x; Z = z; }
    }
}
