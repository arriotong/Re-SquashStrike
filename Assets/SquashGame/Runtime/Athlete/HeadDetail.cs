// ===========================================================
// Assets/SquashGame/Runtime/Athlete/HeadDetail.cs
// PR3: what makes an athlete someone - hair (seven styles) and a face
// (eyes, brows, nose, ears, mouth) on the Gate A head.
//
// Built like MannequinForms: lofted rings and ellipsoids in the rig
// frame (front = -Z), around the head form's pivot, so they ride the
// Head bone exactly as the head does. The hair shells hug the skull
// (MannequinForms.Head) a few millimetres proud; at the front each
// ring stays inside the forehead so a hairline shows. Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using R = SquashGame.Athlete.Ring;

namespace SquashGame.Athlete
{
    public static class HeadDetail
    {
        /// <summary>The head form's pivot (MannequinForms: form_head).</summary>
        public static readonly Vector3 Pivot = new Vector3(0, 1.560f, 0.002f);
        public const int StyleCount = 7;           // Cosmetics.HairStyles: shaved, crop, short, curls, bun, ponytail, mohawk

        static float Y(float world) { return world - Pivot.y; }

        static MeshData Ell(float w, float d, float h, float x, float yWorld, float z, int lon = 16, int lat = 10)
        {
            return Loft.Ellipsoid(w, d, h, lon, lat).Transformed(Matrix4x4.Translate(new Vector3(x, Y(yWorld), z)));
        }

        /// <summary>A hair shell: puff = how far it stands off the skull (m).</summary>
        static MeshData Shell(float puff, float nape = 1.630f)
        {
            float p = puff;
            return Loft.Make(new List<R>
            {
                R.Make(Y(nape), 0.146f + 2 * p, 0.020f, 0.094f + p, 2.3f),
                R.Make(Y(1.660f), 0.152f + 2 * p, 0.040f, 0.100f + p, 2.4f),
                R.Make(Y(1.700f), 0.152f + 2 * p, 0.065f, 0.100f + p, 2.4f),
                R.Make(Y(1.728f), 0.145f + 2 * p, 0.082f, 0.097f + p, 2.3f),
                R.Make(Y(1.748f), 0.134f + 2 * p, 0.084f + p, 0.091f + p, 2.25f),
                R.Make(Y(1.765f), 0.120f + 2 * p, 0.076f + p, 0.083f + p, 2.2f),
                R.Make(Y(1.788f), 0.084f + 2 * p, 0.051f + p, 0.056f + p, 2.1f),
                R.Make(Y(1.800f + p * 0.8f), 0.036f + p, 0.020f + p * 0.5f, 0.022f + p * 0.5f, 2.0f),
                R.Make(Y(1.803f + p), 0.004f, 0.002f, 0.002f, 2.0f),
            }, 4, 24);
        }

        /// <summary>The hair for a style (null for shaved).</summary>
        public static MeshData Hair(int style)
        {
            var m = new MeshData();
            switch (style)
            {
                case 0: return null;                                                    // shaved
                case 1: m.Append(Shell(0.004f)); break;                                 // crop
                case 2: m.Append(Shell(0.009f)); break;                                 // short
                case 3:                                                                 // curls: a fuller shell, lumpy on top
                    m.Append(Shell(0.020f, 1.640f));
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f, ring = i % 2 == 0 ? 0.055f : 0.035f;
                        m.Append(Ell(0.045f, 0.045f, 0.035f, Mathf.Cos(a) * ring, 1.790f - ring * 0.25f, Mathf.Sin(a) * ring * 1.15f + 0.006f, 10, 6));
                    }
                    break;
                case 4:                                                                 // bun
                    m.Append(Shell(0.007f));
                    m.Append(Ell(0.070f, 0.066f, 0.062f, 0, 1.772f, 0.098f));
                    break;
                case 5:                                                                 // ponytail
                    m.Append(Shell(0.007f));
                    m.Append(Ell(0.044f, 0.040f, 0.040f, 0, 1.712f, 0.106f));
                    m.Append(Ell(0.050f, 0.046f, 0.170f, 0, 1.625f, 0.120f));
                    break;
                case 6:                                                                 // mohawk: cropped sides, a fin front to back
                    m.Append(Shell(0.003f));
                    m.Append(Ell(0.030f, 0.190f, 0.075f, 0, 1.800f, 0.004f));
                    break;
            }
            return m;
        }

        /// <summary>Features drawn in the skin's colour: nose and ears.</summary>
        public static MeshData SkinFeatures()
        {
            var m = new MeshData();
            m.Append(Ell(0.020f, 0.034f, 0.038f, 0, 1.646f, -0.090f));                        // nose
            foreach (int s in new[] { -1, 1 })
                m.Append(Ell(0.016f, 0.032f, 0.052f, s * 0.076f, 1.652f, 0.006f));             // ears
            return m;
        }

        /// <summary>Features drawn dark: eyes and mouth.</summary>
        public static MeshData DarkFeatures()
        {
            var m = new MeshData();
            foreach (int s in new[] { -1, 1 })
                m.Append(Ell(0.018f, 0.010f, 0.010f, s * 0.032f, 1.668f, -0.087f, 12, 8));     // eyes
            m.Append(Ell(0.032f, 0.008f, 0.006f, 0, 1.610f, -0.088f, 12, 6));                  // mouth
            return m;
        }

        /// <summary>Brows: drawn in the hair's colour.</summary>
        public static MeshData Brows()
        {
            var m = new MeshData();
            foreach (int s in new[] { -1, 1 })
                m.Append(Ell(0.032f, 0.010f, 0.007f, s * 0.033f, 1.686f, -0.087f, 12, 6));
            return m;
        }
    }
}
