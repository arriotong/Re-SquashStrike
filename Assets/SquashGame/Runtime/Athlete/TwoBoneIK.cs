// ===========================================================
// Assets/SquashGame/Runtime/Athlete/TwoBoneIK.cs
// Analytic two-bone IK (shoulder-elbow-wrist, hip-knee-ankle).
// Our own, so the athlete needs no animation package.
//
// Law of cosines for the bend, a pole point for the bend plane, then
// minimal rotations (FromToRotation) on the two bones so the chain's
// rest roll is preserved. Unreachable targets are approached along the
// straight line and the chain stops fully extended (never stretches).
// ===========================================================

using UnityEngine;

namespace SquashGame.Athlete
{
    public static class TwoBoneIK
    {
        /// <summary>Solve root -> mid -> end toward <paramref name="target"/>, bending toward
        /// <paramref name="pole"/>. Returns the remaining distance from the end to the target.</summary>
        public static float Solve(Transform root, Transform mid, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 a = root.position, b = mid.position, c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            Vector3 toT = target - a;
            float dist = toT.magnitude;
            if (dist < 1e-5f || l1 < 1e-5f || l2 < 1e-5f) return (end.position - target).magnitude;
            Vector3 dir = toT / dist;
            float d = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);

            // Bend plane from the pole.
            Vector3 toPole = pole - a;
            Vector3 perp = toPole - Vector3.Dot(toPole, dir) * dir;
            if (perp.sqrMagnitude < 1e-8f)
            {
                perp = Vector3.Cross(dir, Vector3.right);
                if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(dir, Vector3.forward);
            }
            perp.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = a + (dir * cosA + perp * sinA) * l1;
            Vector3 wrist = a + dir * d;

            root.rotation = Quaternion.FromToRotation(b - a, elbow - a) * root.rotation;
            Vector3 midNow = mid.position, endNow = end.position;
            mid.rotation = Quaternion.FromToRotation(endNow - midNow, wrist - midNow) * mid.rotation;
            return (end.position - target).magnitude;
        }
    }
}
