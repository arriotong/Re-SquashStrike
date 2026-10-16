// ===========================================================
// Assets/SquashGame/Runtime/SimToWorld.cs
// The ONE place that converts simulation space to Unity world space.
//
// Sim (the lab's convention, right-handed): +Y up, front wall z = 0,
// back wall z = 9.75, +x = the right-hand side wall seen from the back.
// Unity is left-handed, so x is mirrored: world = (-x, y, z). Seen from
// behind the back wall, sim +x then still appears on screen right.
//
// Nothing else may flip axes. Views and cameras call these.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public static class SimToWorld
    {
        public static Vector3 Pos(V3 p) { return new Vector3((float)-p.X, (float)p.Y, (float)p.Z); }
        public static Vector3 Pos(double x, double y, double z) { return new Vector3((float)-x, (float)y, (float)z); }

        /// <summary>Directions transform like positions (a polar vector).</summary>
        public static Vector3 Dir(V3 d) { return new Vector3((float)-d.X, (float)d.Y, (float)d.Z); }

        /// <summary>World forward for a sim yaw (0 = facing the front wall, +ve turns toward sim +x).</summary>
        public static Vector3 YawForward(double simYaw)
        {
            return new Vector3((float)-System.Math.Sin(simYaw), 0f, (float)-System.Math.Cos(simYaw));
        }

        public static Quaternion Yaw(double simYaw) { return Quaternion.LookRotation(YawForward(simYaw), Vector3.up); }

        /// <summary>
        /// A sim rotation (right-handed, angle about axis) as a world rotation.
        /// Mirroring maps a rotation by +angle about a to a rotation by -angle about the mirrored axis.
        /// Angular velocity is an axial vector; this is the only correct way to show spin.
        /// </summary>
        public static Quaternion Rotation(V3 simAxis, double angleRad)
        {
            Vector3 axis = Dir(simAxis);
            return Quaternion.AngleAxis((float)(-angleRad * Mathf.Rad2Deg), axis);
        }
    }
}
