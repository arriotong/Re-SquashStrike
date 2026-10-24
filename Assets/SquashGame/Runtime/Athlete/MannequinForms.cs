// ===========================================================
// Assets/SquashGame/Runtime/Athlete/MannequinForms.cs
// Port of the form functions in lab/character/MannequinBuilder.js:
// the frozen Gate A body, ring for ring.
//
// Each form is built in the rig frame around its Gate A PIVOT (the
// group origin the lab builds it under), and is listed with the
// Gate B BONE it rides plus the pivot's rest position. AthleteRig
// places it at (pivot - bone) in the bone's local space, so the Gate A
// surface lands exactly where it was frozen while every form moves
// with the production skeleton.
//
// Presentation geometry, as in the lab: separate lofted forms that
// overlap at the creases, with blend spheres for bent joints. Gate B's
// skinned production mesh replaces this later without touching the
// skeleton or the poser.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using R = SquashGame.Athlete.Ring;

namespace SquashGame.Athlete
{
    public struct FormPart
    {
        public string Name;
        public string Bone;         // Gate B bone the form rides
        public Vector3 Pivot;       // rig-frame rest position of the Gate A pivot
        public MeshData Mesh;       // rig frame, pivot-local
    }

    public struct JointBlend
    {
        public string Bone;         // bone whose bend reveals the blend
        public float W, D, H;
    }

    public static class MannequinForms
    {
        const float HIP = GateA.Hip;

        static MeshData Pelvis()
        {
            System.Func<float, float> y = w => w - HIP;
            return Loft.Make(new List<R>
            {
                R.Make(y(0.845f), 0.146f, 0.034f, 0.088f, 2.30f),
                R.Make(y(0.868f), 0.228f, 0.044f, 0.132f, 2.52f),
                R.Make(y(0.898f), 0.294f, 0.066f, 0.152f, 2.65f),
                R.Make(y(0.933f), 0.334f, 0.083f, 0.146f, 2.66f),
                R.Make(y(0.968f), 0.322f, 0.085f, 0.128f, 2.62f),
                R.Make(y(1.002f), 0.310f, 0.087f, 0.114f, 2.56f),
                R.Make(y(GateA.Crest), 0.300f, 0.089f, 0.104f, 2.44f),
                R.Make(y(1.058f), 0.283f, 0.090f, 0.096f, 2.48f),
                R.Make(y(GateA.Waist), 0.270f, 0.091f, 0.090f, 2.50f),
                R.Make(y(1.098f), 0.232f, 0.078f, 0.077f, 2.50f),
                R.Make(y(1.110f), 0.150f, 0.050f, 0.050f, 2.40f)
            });
        }

        static MeshData Abdomen(float originY)
        {
            System.Func<float, float> y = w => w - originY;
            return Loft.Make(new List<R>
            {
                R.Make(y(1.018f), 0.236f, 0.078f, 0.082f, 2.42f),
                R.Make(y(1.050f), 0.256f, 0.086f, 0.086f, 2.46f),
                R.Make(y(GateA.Waist), 0.269f, 0.092f, 0.090f, 2.50f),
                R.Make(y(1.100f), 0.274f, 0.093f, 0.091f, 2.50f),
                R.Make(y(1.130f), 0.282f, 0.096f, 0.094f, 2.50f),
                R.Make(y(1.165f), 0.291f, 0.099f, 0.097f, 2.51f),
                R.Make(y(1.196f), 0.300f, 0.103f, 0.100f, 2.52f),
                R.Make(y(1.212f), 0.294f, 0.100f, 0.098f, 2.52f),
                R.Make(y(1.224f), 0.240f, 0.082f, 0.080f, 2.50f)
            });
        }

        static MeshData Chest(float originY)
        {
            System.Func<float, float> y = w => w - originY;
            return Loft.Make(new List<R>
            {
                R.Make(y(1.150f), 0.244f, 0.084f, 0.082f, 2.46f),
                R.Make(y(1.180f), 0.272f, 0.093f, 0.090f, 2.50f),
                R.Make(y(1.200f), 0.303f, 0.104f, 0.100f, 2.52f),
                R.Make(y(1.230f), 0.313f, 0.108f, 0.102f, 2.55f),
                R.Make(y(GateA.Chest), 0.320f, 0.112f, 0.106f, 2.60f),
                R.Make(y(1.370f), 0.348f, 0.104f, 0.100f, 2.60f),
                R.Make(y(GateA.Shoulder), 0.368f, 0.094f, 0.094f, 2.70f),
                R.Make(y(1.462f), 0.372f, 0.080f, 0.086f, 2.85f),
                R.Make(y(GateA.NeckBase), 0.296f, 0.064f, 0.076f, 2.60f),
                R.Make(y(1.506f), 0.168f, 0.046f, 0.054f, 2.45f),
                R.Make(y(1.516f), 0.108f, 0.034f, 0.038f, 2.30f)
            });
        }

        static MeshData Neck()
        {
            System.Func<float, float> y = w => w - 1.482f;
            return Loft.Make(new List<R>
            {
                R.D(y(1.448f), 0.116f, 0.118f, 2.30f),
                R.D(y(1.482f), GateA.NeckW, GateA.NeckD, 2.32f),
                R.D(y(1.512f), GateA.NeckW, GateA.NeckD, 2.30f),
                R.D(y(1.542f), GateA.NeckW - 0.002f, GateA.NeckD, 2.30f),
                R.D(y(1.570f), GateA.NeckW + 0.002f, GateA.NeckD + 0.006f, 2.30f),
                R.D(y(1.582f), 0.108f, 0.112f, 2.25f)
            });
        }

        static MeshData Head()
        {
            System.Func<float, float> y = w => w - 1.560f;
            return Loft.Make(new List<R>
            {
                R.Make(y(1.534f), 0.094f, 0.048f, 0.062f, 2.35f),
                R.Make(y(1.552f), 0.118f, 0.068f, 0.078f, 2.45f),
                R.Make(y(GateA.Chin), 0.132f, 0.079f, 0.085f, 2.50f),
                R.Make(y(GateA.Jaw), 0.146f, 0.089f, 0.095f, 2.55f),
                R.Make(y(1.640f), GateA.HeadW, 0.094f, 0.100f, 2.50f),
                R.Make(y(1.685f), 0.150f, 0.095f, 0.099f, 2.40f),
                R.Make(y(1.725f), 0.145f, 0.090f, 0.097f, 2.30f),
                R.Make(y(1.765f), 0.120f, 0.076f, 0.083f, 2.20f),
                R.Make(y(1.788f), 0.084f, 0.051f, 0.056f, 2.10f),
                R.Make(y(GateA.Crown), 0.032f, 0.019f, 0.021f, 2.00f)
            });
        }

        static MeshData UpperArm()
        {
            return Loft.Make(new List<R>
            {
                R.D(0.048f, 0.100f, 0.104f, 2.30f),
                R.D(0.018f, 0.116f, 0.118f, 2.30f),
                R.D(-0.030f, 0.112f, 0.114f, 2.25f),
                R.D(-0.100f, 0.100f, 0.102f, 2.20f),
                R.D(-0.180f, 0.094f, 0.096f, 2.20f),
                R.D(-0.260f, 0.084f, 0.088f, 2.15f),
                R.D(-GateA.UpperArm, 0.080f, 0.084f, 2.10f),
                R.D(-GateA.UpperArm - 0.026f, 0.068f, 0.072f, 2.05f)
            });
        }

        static MeshData Forearm()
        {
            return Loft.Make(new List<R>
            {
                R.D(0.034f, 0.072f, 0.076f, 2.10f),
                R.D(0.004f, 0.081f, 0.085f, 2.15f),
                R.D(-0.045f, 0.100f, 0.094f, 2.20f),
                R.D(-0.100f, 0.092f, 0.086f, 2.15f),
                R.D(-0.165f, 0.076f, 0.068f, 2.10f),
                R.D(-0.222f, 0.064f, 0.052f, 2.10f),
                R.D(-GateA.Forearm, 0.062f, 0.048f, 2.10f),
                R.D(-GateA.Forearm - 0.022f, 0.052f, 0.040f, 2.05f)
            });
        }

        static MeshData Hand()
        {
            var g = Loft.Make(new List<R>
            {
                R.D(0.024f, 0.054f, 0.042f, 2.15f),
                R.D(0.000f, 0.061f, 0.047f, 2.20f),
                R.D(-0.035f, 0.084f, 0.040f, 2.60f),
                R.D(-0.075f, 0.090f, 0.037f, 2.70f),
                R.D(-0.105f, 0.088f, 0.034f, 2.70f),
                R.D(-0.140f, 0.084f, 0.030f, 2.70f),
                R.D(-0.172f, 0.074f, 0.026f, 2.60f),
                R.D(-GateA.Hand, 0.052f, 0.018f, 2.40f)
            }, 4);
            return g.Transformed(Matrix4x4.Rotate(Quaternion.Euler(0, 90f, 0)));   // palm faces the thigh
        }

        static MeshData ThumbForm()
        {
            var g = Loft.Make(new List<R>
            {
                R.D(0.004f, 0.034f, 0.032f, 2.2f),
                R.D(-0.024f, 0.032f, 0.030f, 2.2f),
                R.D(-0.052f, 0.026f, 0.024f, 2.1f),
                R.D(-0.070f, 0.014f, 0.013f, 2.0f)
            }, 4, 16);
            g = g.Transformed(Matrix4x4.Rotate(Quaternion.Euler(0, 0, -0.85f * Mathf.Rad2Deg)));
            g = g.Transformed(Matrix4x4.Rotate(Quaternion.Euler(0.35f * Mathf.Rad2Deg, 0, 0)));
            g = g.Translated(new Vector3(0.036f, -0.032f, -0.010f));
            return g.Transformed(Matrix4x4.Rotate(Quaternion.Euler(0, 90f, 0)));
        }

        static MeshData Thigh()
        {
            System.Func<float, float> y = w => w - GateA.Hip;
            return Loft.Make(new List<R>
            {
                R.Make(y(1.000f), 0.116f, 0.070f, 0.068f, 2.28f),
                R.Make(y(0.965f), 0.140f, 0.086f, 0.080f, 2.30f),
                R.Make(y(0.933f), 0.156f, 0.095f, 0.087f, 2.32f),
                R.Make(y(0.898f), 0.170f, 0.102f, 0.094f, 2.34f),
                R.Make(y(0.870f), 0.174f, 0.104f, 0.096f, 2.35f),
                R.Make(y(0.815f), 0.170f, 0.102f, 0.094f, 2.32f),
                R.Make(y(GateA.ThighMid), 0.158f, 0.092f, 0.080f, 2.30f),
                R.Make(y(0.615f), 0.138f, 0.080f, 0.070f, 2.25f),
                R.Make(y(0.550f), 0.121f, 0.070f, 0.061f, 2.20f),
                R.Make(y(GateA.Knee), 0.114f, 0.066f, 0.058f, 2.20f),
                R.Make(y(GateA.Knee - 0.026f), 0.104f, 0.060f, 0.052f, 2.15f)
            });
        }

        static MeshData Shin()
        {
            return Loft.Make(new List<R>
            {
                R.Make(0.030f, 0.112f, 0.058f, 0.064f, 2.20f),
                R.Make(0.000f, 0.115f, 0.060f, 0.065f, 2.20f),
                R.Make(-0.060f, 0.114f, 0.052f, 0.074f, 2.22f),
                R.Make(-0.135f, 0.124f, 0.048f, 0.090f, 2.25f),
                R.Make(-0.230f, 0.108f, 0.044f, 0.076f, 2.20f),
                R.Make(-0.320f, 0.086f, 0.038f, 0.058f, 2.15f),
                R.Make(-0.390f, 0.076f, 0.036f, 0.048f, 2.10f),
                R.Make(-GateA.Shin, 0.074f, 0.036f, 0.046f, 2.10f),
                R.Make(-GateA.Shin - 0.028f, 0.064f, 0.032f, 0.040f, 2.05f)
            });
        }

        static MeshData Foot()
        {
            const float sole = 0.004f;
            var g = Loft.Make(new List<R>
            {
                R.Make(0.000f, 0.050f, sole, 0.030f, 2.3f),
                R.Make(0.022f, 0.070f, sole, 0.062f, 2.4f),
                R.Make(0.060f, 0.078f, sole, 0.072f, 2.5f),
                R.Make(0.110f, 0.086f, sole, 0.062f, 2.6f),
                R.Make(0.170f, 0.100f, sole, 0.046f, 2.7f),
                R.Make(0.215f, 0.098f, sole, 0.036f, 2.7f),
                R.Make(0.252f, 0.088f, sole, 0.030f, 2.65f),
                R.Make(0.270f, 0.066f, sole, 0.022f, 2.50f)
            }, 4);
            g = g.Transformed(Matrix4x4.Rotate(Quaternion.Euler(-90f, 0, 0)));    // sweep axis -> -Z (forward)
            return g.Translated(new Vector3(0, -GateA.Ankle + sole, 0.075f));     // sole on the floor, heel behind
        }

        /// <summary>The whole Gate A body as parts riding Gate B bones.</summary>
        public static List<FormPart> Build()
        {
            var parts = new List<FormPart>
            {
                new FormPart { Name = "form_pelvis", Bone = "Pelvis", Pivot = new Vector3(0, GateA.Hip, 0), Mesh = Pelvis() },
                new FormPart { Name = "form_abdomen", Bone = "Spine_01", Pivot = new Vector3(0, GateA.Crest, 0), Mesh = Abdomen(GateA.Crest) },
                new FormPart { Name = "form_chest", Bone = "Chest", Pivot = new Vector3(0, 1.160f, 0), Mesh = Chest(1.160f) },
                new FormPart { Name = "form_neck", Bone = "Neck", Pivot = new Vector3(0, 1.482f, 0.006f), Mesh = Neck() },
                new FormPart { Name = "form_head", Bone = "Head", Pivot = new Vector3(0, 1.560f, 0.002f), Mesh = Head() }
            };

            var upper = UpperArm(); var fore = Forearm(); var hand = Hand(); var thumb = ThumbForm();
            var thigh = Thigh(); var shin = Shin(); var foot = Foot();
            foreach (string side in new[] { "R", "L" })
            {
                float s = side == "R" ? 1f : -1f;
                bool mirror = side == "L";
                System.Func<MeshData, MeshData> m = g => mirror ? g.MirroredX() : g;
                float sx = s * RigContract.ShoulderX, hx = s * RigContract.HipX;
                parts.Add(new FormPart { Name = "form_upperArm" + side, Bone = "UpperArm_" + side, Pivot = new Vector3(sx, GateA.Shoulder, 0), Mesh = m(upper) });
                parts.Add(new FormPart { Name = "form_forearm" + side, Bone = "Forearm_" + side, Pivot = new Vector3(sx, RigContract.ElbowY, 0), Mesh = m(fore) });
                parts.Add(new FormPart { Name = "form_hand" + side, Bone = "Hand_" + side, Pivot = new Vector3(sx, RigContract.WristY, 0), Mesh = m(hand) });
                parts.Add(new FormPart { Name = "form_thumb" + side, Bone = "Hand_" + side, Pivot = new Vector3(sx, RigContract.WristY, 0), Mesh = m(thumb) });
                parts.Add(new FormPart { Name = "form_thigh" + side, Bone = "Thigh_" + side, Pivot = new Vector3(hx, GateA.Hip, 0), Mesh = m(thigh) });
                parts.Add(new FormPart { Name = "form_shin" + side, Bone = "Shin_" + side, Pivot = new Vector3(hx, GateA.Knee, 0), Mesh = m(shin) });
                parts.Add(new FormPart { Name = "form_foot" + side, Bone = "Foot_" + side, Pivot = new Vector3(hx, GateA.Ankle, 0), Mesh = m(foot) });
            }
            return parts;
        }

        // ---------------- the slim, simple build (2026-10-24, after the user's sketch) ----------------
        // The Gate A forms were chunky on purpose (the Mario Tennis look). The athletes are now slim and simple: every
        // form keeps its height and its silhouette but is narrowed (x) and thinned (z) about its own centreline - all
        // the forms hang straight down at rest, so that is the bone's axis. A female build is narrower in the shoulders
        // and the limbs, a little fuller in the hips.

        /// <summary>Width and depth factors for a form (or a joint blend, by its bone).</summary>
        public static Vector2 Slim(string name, bool female)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("pelvis")) return female ? new Vector2(0.92f, 0.90f) : new Vector2(0.84f, 0.86f);
            if (n.Contains("abdomen") || n.Contains("spine")) return female ? new Vector2(0.72f, 0.76f) : new Vector2(0.78f, 0.80f);
            if (n.Contains("chest")) return female ? new Vector2(0.80f, 0.82f) : new Vector2(0.88f, 0.82f);
            if (n.Contains("neck")) return female ? new Vector2(0.80f, 0.84f) : new Vector2(0.86f, 0.88f);
            if (n.Contains("head")) return female ? new Vector2(0.92f, 0.94f) : new Vector2(0.96f, 0.96f);
            if (n.Contains("upperarm")) return female ? new Vector2(0.68f, 0.70f) : new Vector2(0.74f, 0.76f);
            if (n.Contains("forearm")) return female ? new Vector2(0.72f, 0.72f) : new Vector2(0.78f, 0.78f);
            if (n.Contains("hand") || n.Contains("thumb")) return female ? new Vector2(0.86f, 0.86f) : new Vector2(0.92f, 0.92f);
            if (n.Contains("thigh")) return female ? new Vector2(0.80f, 0.80f) : new Vector2(0.78f, 0.80f);
            if (n.Contains("shin")) return female ? new Vector2(0.74f, 0.76f) : new Vector2(0.78f, 0.80f);
            if (n.Contains("foot") || n.Contains("toe")) return female ? new Vector2(0.86f, 0.92f) : new Vector2(0.92f, 0.95f);
            return Vector2.one;
        }

        /// <summary>A form narrowed by <see cref="Slim"/> about its centreline (normals kept true to the new surface).</summary>
        public static MeshData Slimmed(MeshData m, Vector2 s)
        {
            var o = new MeshData();
            foreach (var v in m.V) o.V.Add(new Vector3(v.x * s.x, v.y, v.z * s.y));
            foreach (var nrm in m.N) o.N.Add(new Vector3(nrm.x / s.x, nrm.y, nrm.z / s.y).normalized);
            o.T.AddRange(m.T);
            return o;
        }

        /// <summary>Blend spheres at joints that bend (shown only when bent, as in the lab).</summary>
        public static List<JointBlend> Blends()
        {
            var b = new List<JointBlend>();
            foreach (string side in new[] { "R", "L" })
            {
                b.Add(new JointBlend { Bone = "UpperArm_" + side, W = 0.116f * 0.84f, D = 0.118f * 0.84f, H = 0.116f * 0.84f });
                b.Add(new JointBlend { Bone = "Forearm_" + side, W = 0.080f * 0.88f, D = 0.084f * 0.88f, H = 0.080f * 0.88f });
                b.Add(new JointBlend { Bone = "Hand_" + side, W = 0.062f * 0.92f, D = 0.048f * 0.98f, H = 0.062f * 0.88f });
                b.Add(new JointBlend { Bone = "Shin_" + side, W = 0.114f * 0.89f, D = 0.124f * 0.89f, H = 0.114f * 0.92f });
                b.Add(new JointBlend { Bone = "Foot_" + side, W = 0.074f * 0.92f, D = 0.084f * 0.92f, H = 0.074f * 0.90f });
            }
            return b;
        }
    }
}
