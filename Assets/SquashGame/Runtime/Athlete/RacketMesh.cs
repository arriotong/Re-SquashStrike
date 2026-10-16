// ===========================================================
// Assets/SquashGame/Runtime/Athlete/RacketMesh.cs
// Port of lab/character/RacketProxy.js: the frozen Gate A squash
// racket (A.1, 684 mm outside-to-outside, 478 cm^2 strung, teardrop
// 301 x 188 head, open throat, sweet spot 570 mm from the butt).
//
// LOCAL FRAME (rig frame, as in the lab): butt cap at y = 0, the racket
// runs along -Y to the tip, the string plane is YZ, so the face normal
// is X. Parts: frame (elliptical tube), string bed (a tone, not lines),
// handle. Presentation only; the ball meets RacketPhysics, and the
// RacketFigure places this mesh exactly on that racket.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public static class RacketMesh
    {
        public const float Length = 0.679f, GripTop = 0.190f, ThroatTop = 0.378f, SweetSpot = 0.570f;
        const float FrameBand = 0.011f, FrameBandTip = 0.0075f, FrameDepth = 0.019f, TipFrom = 0.654f;

        static readonly float[,] HeadHalf =
        {
            { 0.378f, 0.011f }, { 0.392f, 0.028f }, { 0.408f, 0.043f }, { 0.428f, 0.058f }, { 0.452f, 0.072f },
            { 0.480f, 0.083f }, { 0.512f, 0.0905f }, { 0.545f, 0.094f }, { 0.576f, 0.0925f }, { 0.606f, 0.0865f },
            { 0.634f, 0.077f }, { 0.654f, 0.063f }, { 0.668f, 0.043f }, { 0.679f, 0.013f }
        };

        static List<Vector2> SmoothLoop(List<Vector2> loop, int steps)
        {
            int n = loop.Count;
            System.Func<int, Vector2> at = i => loop[((i % n) + n) % n];
            var outp = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = at(i - 1), p1 = at(i), p2 = at(i + 1), p3 = at(i + 2);
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    outp.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            return outp;
        }

        /// <summary>Frame centreline as (y, z) with y = -u.</summary>
        static List<Vector2> HeadLoop()
        {
            var loop = new List<Vector2>();
            int n = HeadHalf.GetLength(0);
            for (int i = 0; i < n; i++) loop.Add(new Vector2(-HeadHalf[i, 0], HeadHalf[i, 1]));
            loop.Add(new Vector2(-Length, 0));
            for (int i = n - 2; i >= 1; i--) loop.Add(new Vector2(-HeadHalf[i, 0], -HeadHalf[i, 1]));
            return SmoothLoop(loop, 5);
        }

        static MeshData Handle()
        {
            System.Func<float, float, float, float, Ring> r = (u, bx, dz, n) => Ring.Make(-u, bx, dz * 0.5f, dz * 0.5f, n);
            return Loft.Make(new List<Ring>
            {
                r(0.386f, 0.021f, 0.016f, 2.4f), r(0.368f, 0.018f, 0.014f, 2.4f), r(0.330f, 0.019f, 0.015f, 2.4f),
                r(0.280f, 0.022f, 0.017f, 2.5f), r(0.228f, 0.026f, 0.021f, 2.7f), r(0.196f, 0.030f, 0.024f, 2.9f),
                r(0.190f, 0.033f, 0.027f, 3.0f), r(0.150f, 0.033f, 0.027f, 3.0f), r(0.080f, 0.033f, 0.027f, 3.0f),
                r(0.022f, 0.032f, 0.026f, 3.0f), r(0.010f, 0.038f, 0.031f, 2.8f), r(0.000f, 0.034f, 0.028f, 2.5f)
            }, 4, 16);
        }

        static MeshData StringBed(List<Vector2> inner)
        {
            var m = new MeshData();
            Vector2 c = Vector2.zero;
            foreach (var p in inner) c += p;
            c /= inner.Count;
            m.V.Add(new Vector3(0, c.x, c.y)); m.N.Add(Vector3.right);
            foreach (var p in inner) { m.V.Add(new Vector3(0, p.x, p.y)); m.N.Add(Vector3.right); }
            for (int i = 0; i < inner.Count; i++) { m.T.Add(0); m.T.Add(1 + i); m.T.Add(1 + (i + 1) % inner.Count); }
            return m;
        }

        public struct Parts { public MeshData Frame, Bed, Handle; }

        public static Parts Build()
        {
            var loop = HeadLoop();
            var band = new float[loop.Count];
            for (int i = 0; i < loop.Count; i++)
            {
                float u = -loop[i].x;
                if (u <= TipFrom) band[i] = FrameBand / 2;
                else
                {
                    float t = Mathf.Min(1, (u - TipFrom) / (Length - TipFrom));
                    band[i] = (FrameBand + (FrameBandTip - FrameBand) * t) / 2;
                }
            }
            return new Parts
            {
                Frame = Loft.TubeAlongPath(loop, FrameDepth / 2, band, 10),
                Bed = StringBed(Loft.OffsetPath(loop, band)),
                Handle = Handle()
            };
        }
    }
}
