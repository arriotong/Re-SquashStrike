// ===========================================================
// Assets/SquashSim/Court/CourtSurfaces.cs
// Port of lab/court/CourtSurfaces.js. The court AS PHYSICS: bounded
// analytic planes. Never triangle meshes.
//
// A surface: id, surfaceClass (key into PhysicsConfig.Surfaces, read
// live every impact), unit normal N pointing INTO the playable volume,
// plane constant D (dot(N, p) = D), and a finite-extent test.
// ===========================================================

using System;
using System.Collections.Generic;
using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed class Surface
    {
        public readonly string Id;
        public readonly string SurfaceClass;
        public readonly V3 N;
        public readonly double D;
        readonly Func<V3, bool> _contains;

        public Surface(string id, string surfaceClass, V3 n, double d, Func<V3, bool> contains)
        {
            Id = id; SurfaceClass = surfaceClass; N = n; D = d; _contains = contains;
        }

        public bool Contains(V3 p) { return _contains(p); }
    }

    public static class CourtSurfaces
    {
        const double OUT_ZONE_HALF_W = HALF_W + 2.0;

        public static List<Surface> Create()
        {
            var s = new List<Surface>();

            s.Add(new Surface("floor", "floor", new V3(0, 1, 0), 0,
                p => p.X >= -OUT_ZONE_HALF_W && p.X <= OUT_ZONE_HALF_W && p.Z >= -0.5 && p.Z <= OUT_BACKSTOP_Z));

            s.Add(new Surface("ceiling", "ceiling", new V3(0, -1, 0), -CEILING_H,
                p => p.X >= -OUT_ZONE_HALF_W && p.X <= OUT_ZONE_HALF_W && p.Z >= -0.5 && p.Z <= OUT_BACKSTOP_Z));

            s.Add(new Surface("tin", "tin", new V3(0, 0, 1), 0,
                p => p.X >= -HALF_W && p.X <= HALF_W && p.Y <= TIN_H));

            s.Add(new Surface("frontWall", "frontWall", new V3(0, 0, 1), 0,
                p => p.X >= -HALF_W && p.X <= HALF_W && p.Y > TIN_H && p.Y <= CEILING_H));

            s.Add(new Surface("sideWallLeft", "sideWall", new V3(1, 0, 0), -HALF_W,
                p => p.Z >= 0 && p.Z <= LENGTH && p.Y >= 0 && p.Y <= CEILING_H));

            s.Add(new Surface("sideWallRight", "sideWall", new V3(-1, 0, 0), -HALF_W,
                p => p.Z >= 0 && p.Z <= LENGTH && p.Y >= 0 && p.Y <= CEILING_H));

            s.Add(new Surface("backWall", "backWall", new V3(0, 0, -1), -LENGTH,
                p => p.X >= -HALF_W && p.X <= HALF_W && p.Y >= 0 && p.Y <= BACK_WALL_OUT_H));

            s.Add(new Surface("outBackstop", "outBackstop", new V3(0, 0, -1), -OUT_BACKSTOP_Z,
                p => p.Y >= 0 && p.Y <= CEILING_H));

            return s;
        }

        /// <summary>Live surface properties. Read every impact, never cached.</summary>
        public static SurfaceProps Props(Surface surf)
        {
            return PhysicsConfig.Current.Surfaces.Get(surf.SurfaceClass) ?? PhysicsConfig.Current.Surfaces.Floor;
        }

        /// <summary>Signed distance from a point to the plane (positive = inside).</summary>
        public static double SignedDistance(Surface surf, V3 p)
        {
            return surf.N.X * p.X + surf.N.Y * p.Y + surf.N.Z * p.Z - surf.D;
        }

        /// <summary>Velocity component along the normal (negative = approaching).</summary>
        public static double NormalVelocity(Surface surf, V3 v)
        {
            return surf.N.X * v.X + surf.N.Y * v.Y + surf.N.Z * v.Z;
        }
    }
}
