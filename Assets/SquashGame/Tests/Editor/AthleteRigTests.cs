// ===========================================================
// Assets/SquashGame/Tests/Editor/AthleteRigTests.cs
// The C# rig contract against the frozen Gate A reference (the lab's
// RigValidation.mjs checks, re-derived from bone positions), the
// ported body against the frozen stature, and the two-bone IK.
// ===========================================================

using System.Collections.Generic;
using NUnit.Framework;
using SquashGame.Athlete;
using UnityEngine;

namespace SquashGame.Tests
{
    public class AthleteRigTests
    {
        static Dictionary<string, Vector3> P()
        {
            var d = new Dictionary<string, Vector3>();
            foreach (var b in RigContract.Bones) d[b.Name] = b.Pos;
            return d;
        }

        [Test] public void SeventyFourBonesWithParentsFirst()
        {
            var seen = new HashSet<string>();
            foreach (var b in RigContract.Bones)
            {
                if (b.Parent != null) Assert.That(seen.Contains(b.Parent), b.Name + " listed before its parent " + b.Parent);
                Assert.That(seen.Add(b.Name), "duplicate " + b.Name);
            }
            Assert.That(seen.Count, Is.EqualTo(74));
        }

        [Test] public void FrozenGateAMeasurementsReDerivedFromBones()
        {
            var p = P();
            float upper = Vector3.Distance(p["UpperArm_R"], p["Forearm_R"]);
            float fore = Vector3.Distance(p["Forearm_R"], p["Hand_R"]);
            float thigh = Vector3.Distance(p["Thigh_R"], p["Shin_R"]);
            float shin = Vector3.Distance(p["Shin_R"], p["Foot_R"]);
            Assert.That(upper, Is.EqualTo(0.315f).Within(0.001f));
            Assert.That(fore, Is.EqualTo(0.255f).Within(0.001f));
            Assert.That(thigh, Is.EqualTo(0.440f).Within(0.001f));
            Assert.That(shin, Is.EqualTo(0.430f).Within(0.001f));
            Assert.That(thigh + shin, Is.EqualTo(GateA.LegTotal).Within(0.001f));
            Assert.That(Vector3.Distance(p["UpperArm_R"], p["UpperArm_L"]), Is.EqualTo(GateA.ShoulderSeparation).Within(0.001f));
            Assert.That(Vector3.Distance(p["Thigh_R"], p["Thigh_L"]), Is.EqualTo(GateA.HipSeparation).Within(0.001f));
            Assert.That(p["Head_End"].y, Is.EqualTo(GateA.Crown).Within(0.001f));
            Assert.That(p["UpperArm_R"].y - p["Thigh_R"].y, Is.EqualTo(GateA.Torso).Within(0.001f));
            // Hand: wrist to the middle fingertip, as the contract measures it (+2 mm, documented).
            Assert.That(p["Hand_R"].y - p["Middle_End_R"].y, Is.EqualTo(GateA.Hand).Within(0.003f));
        }

        [Test] public void RacketSocketSitsWhereGateAHeldTheRacket()
        {
            Vector3 grip = RigContract.RacketGrip("R");
            Vector3 wrist = new Vector3(RigContract.ShoulderX, RigContract.WristY, 0);
            Assert.That(Vector3.Distance(grip, wrist), Is.EqualTo(0.0623f).Within(0.0005f), "socket 62.3 mm from the wrist");
            Vector3 gripL = RigContract.RacketGrip("L");
            Assert.That(gripL.x, Is.EqualTo(-grip.x).Within(1e-6f), "left-handed socket mirrors");
        }

        [Test] public void BodyStandsOnTheFloorAtFrozenStature()
        {
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            int verts = 0;
            foreach (var f in MannequinForms.Build())
            {
                foreach (var v in f.Mesh.V)
                {
                    Vector3 w = v + f.Pivot;
                    minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                    minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                    verts++;
                }
                Assert.That(f.Mesh.T.Count % 3, Is.EqualTo(0));
            }
            Assert.That(maxY, Is.EqualTo(GateA.Crown).Within(0.003f), "crown");
            Assert.That(minY, Is.EqualTo(0f).Within(0.003f), "soles on the floor");
            Assert.That(maxX - minX, Is.LessThan(0.62f), "rest pose arms hang inside the frozen span");
            Assert.That(verts, Is.GreaterThan(5000));
        }

        [Test] public void RacketProxyMatchesFrozenSpec()
        {
            var parts = RacketMesh.Build();
            float minY = float.MaxValue, maxY = float.MinValue, maxZ = 0;
            foreach (var v in parts.Frame.V) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); maxZ = Mathf.Max(maxZ, Mathf.Abs(v.z)); }
            foreach (var v in parts.Handle.V) { maxY = Mathf.Max(maxY, v.y); }
            Assert.That(maxY - minY, Is.EqualTo(GateA.RacketOuterLength).Within(0.004f), "outside-to-outside length");
            Assert.That(maxY - minY, Is.LessThanOrEqualTo(0.686f), "WSF maximum");
            Assert.That(maxZ * 2, Is.EqualTo(GateA.HeadWidth + 0.011f).Within(0.004f), "head width incl. frame band");
        }

        [Test] public void TwoBoneIkReachesAndBends()
        {
            var a = new GameObject("a").transform;
            var b = new GameObject("b").transform; b.SetParent(a); b.localPosition = new Vector3(0, -0.3f, 0);
            var c = new GameObject("c").transform; c.SetParent(b); c.localPosition = new Vector3(0, -0.25f, 0);
            try
            {
                Vector3 target = new Vector3(0.2f, -0.3f, -0.15f);
                Vector3 pole = new Vector3(0, -0.2f, 0.5f);
                float err = TwoBoneIK.Solve(a, b, c, target, pole);
                Assert.That(err, Is.LessThan(0.001f), "reachable target");
                Assert.That(Vector3.Distance(a.position, b.position), Is.EqualTo(0.3f).Within(1e-4f), "bones keep their length");
                Assert.That(Vector3.Dot(b.position - a.position, pole - a.position), Is.GreaterThan(0), "bends toward the pole");

                float far = TwoBoneIK.Solve(a, b, c, new Vector3(0, -2f, 0), pole);
                Assert.That(far, Is.EqualTo(2f - 0.55f).Within(0.002f), "unreachable: fully extended, never stretched");
            }
            finally { Object.DestroyImmediate(a.gameObject); }
        }
    }
}
