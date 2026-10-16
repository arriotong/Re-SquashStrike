// ===========================================================
// Assets/SquashGame/Runtime/Athlete/RigContract.cs
// Port of lab/character/RigDefinition.js - the GATE B rig contract -
// with the frozen GATE A proportions it is derived from
// (lab/character/MannequinProportions.js, gate-a/gate-a-reference.json).
//
// Every bone is a REST POSITION in the rig frame plus a parent name:
//   +X athlete's RIGHT, -Z the way they FACE, +Y up, metres,
//   origin between the feet on the floor.
// That is the simulation's frame too (right-handed). Unity conversion
// (mirror X) happens once, in AthleteRig, never here.
//
// 74 bones, the racket sockets and the helpers the poser uses. Values
// are copied exactly; RigContractTests re-derive the frozen Gate A
// measurements from them, like the lab's RigValidation.mjs.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    public static class GateA
    {
        // ---- landmarks (heights, neutral standing) ----
        public const float Crown = 1.800f, Chin = 1.566f, Jaw = 1.600f, NeckBase = 1.490f, Acromion = 1.470f;
        public const float Shoulder = 1.430f, Chest = 1.290f, RibLower = 1.150f, Waist = 1.075f, Crest = 1.030f;
        public const float Hip = 0.945f, Crotch = 0.845f, ThighMid = 0.720f, Knee = 0.505f, CalfMax = 0.365f, Ankle = 0.075f;

        // ---- segments ----
        public const float UpperArm = 0.315f, Forearm = 0.255f, Hand = 0.190f, ArmTotal = 0.760f;
        public const float Thigh = 0.440f, Shin = 0.430f, LegTotal = 0.870f, Torso = 0.485f, Neck = 0.076f;

        // ---- joints ----
        public const float ShoulderSeparation = 0.335f, HipSeparation = 0.190f;

        // ---- racket (frozen proxy A.1) ----
        public const float RacketOuterLength = 0.684f, RacketCentreline = 0.679f, SweetSpotFromButt = 0.570f;
        public const float ThroatTop = 0.378f, GripLength = 0.190f, HeadWidth = 0.188f;
        public static readonly Vector3 GripOffsetFromWrist = new Vector3(-0.004f, -0.062f, -0.004f);   // right hand, rig frame

        // ---- forms (full breadths) used by the builder ----
        public const float HeadW = 0.152f, HeadD = 0.195f, NeckW = 0.126f, NeckD = 0.130f;
        public const float StanceShoulderY = 1.32f;          // PhysicsConfig.racket.shoulderHeight
    }

    public struct RigBone
    {
        public string Name, Parent;
        public Vector3 Pos;          // rig frame, rest
        public RigBone(string name, string parent, float x, float y, float z) { Name = name; Parent = parent; Pos = new Vector3(x, y, z); }
    }

    public static class RigContract
    {
        // ---- derived rig heights (see RigDefinition.js for the reasoning) ----
        public const float Spine01Y = 1.012f, Spine02Y = 1.126f, ChestY = 1.248f, NeckY = 1.470f, HeadY = 1.585f;
        public const float ShoulderX = GateA.ShoulderSeparation / 2f;      // 0.1675
        public const float HipX = GateA.HipSeparation / 2f;                // 0.095
        public const float ElbowY = GateA.Shoulder - GateA.UpperArm;       // 1.115
        public const float WristY = ElbowY - GateA.Forearm;                // 0.860
        public const float ToeY = 0.028f, ToeZ = -0.100f;

        struct Finger { public string Name; public float Z, Knuckle; public float[] Bones; }
        static readonly Finger[] Fingers =
        {
            new Finger { Name = "Index", Z = -0.030f, Knuckle = 0.755f, Bones = new[] { 0.040f, 0.024f, 0.018f } },
            new Finger { Name = "Middle", Z = -0.010f, Knuckle = 0.757f, Bones = new[] { 0.044f, 0.026f, 0.019f } },
            new Finger { Name = "Ring", Z = 0.010f, Knuckle = 0.754f, Bones = new[] { 0.041f, 0.025f, 0.018f } },
            new Finger { Name = "Pinky", Z = 0.030f, Knuckle = 0.748f, Bones = new[] { 0.033f, 0.019f, 0.015f } }
        };
        static readonly Vector3[] Thumb =
        {
            new Vector3(0.006f, 0.828f, -0.026f), new Vector3(0.014f, 0.790f, -0.052f),
            new Vector3(0.018f, 0.770f, -0.066f), new Vector3(0.020f, 0.757f, -0.076f)
        };

        static List<RigBone> _bones;

        /// <summary>All 74 bones, parents before children.</summary>
        public static List<RigBone> Bones
        {
            get
            {
                if (_bones != null) return _bones;
                var b = new List<RigBone>
                {
                    new RigBone("Root", null, 0, 0, 0),
                    new RigBone("Pelvis", "Root", 0, GateA.Hip, 0),
                    new RigBone("Spine_01", "Pelvis", 0, Spine01Y, 0),
                    new RigBone("Spine_02", "Spine_01", 0, Spine02Y, 0),
                    new RigBone("Chest", "Spine_02", 0, ChestY, 0),
                    new RigBone("Neck", "Chest", 0, NeckY, 0.006f),
                    new RigBone("Head", "Neck", 0, HeadY, 0.012f),
                    new RigBone("Head_End", "Head", 0, GateA.Crown, 0)
                };
                Side(b, "R");
                Side(b, "L");
                _bones = b;
                return b;
            }
        }

        static void Side(List<RigBone> b, string side)
        {
            float s = side == "R" ? 1f : -1f;
            float X = s * ShoulderX, HX = s * HipX;
            b.Add(new RigBone("Clavicle_" + side, "Chest", s * 0.022f, 1.452f, -0.030f));
            b.Add(new RigBone("UpperArm_" + side, "Clavicle_" + side, X, GateA.Shoulder, 0));
            b.Add(new RigBone("UpperArmTwist_1_" + side, "UpperArm_" + side, X, GateA.Shoulder - GateA.UpperArm * 0.50f, 0));
            b.Add(new RigBone("Forearm_" + side, "UpperArm_" + side, X, ElbowY, 0));
            b.Add(new RigBone("ForearmTwist_1_" + side, "Forearm_" + side, X, ElbowY - GateA.Forearm * 0.40f, 0));
            b.Add(new RigBone("ForearmTwist_2_" + side, "Forearm_" + side, X, ElbowY - GateA.Forearm * 0.75f, 0));
            b.Add(new RigBone("Hand_" + side, "Forearm_" + side, X, WristY, 0));
            foreach (var f in Fingers)
            {
                float y = f.Knuckle, z = s * f.Z;
                b.Add(new RigBone(f.Name + "_01_" + side, "Hand_" + side, X, y, z));
                y -= f.Bones[0];
                b.Add(new RigBone(f.Name + "_02_" + side, f.Name + "_01_" + side, X, y, z));
                y -= f.Bones[1];
                b.Add(new RigBone(f.Name + "_03_" + side, f.Name + "_02_" + side, X, y, z));
                y -= f.Bones[2];
                b.Add(new RigBone(f.Name + "_End_" + side, f.Name + "_03_" + side, X, y, z));
            }
            string[] tn = { "Thumb_01", "Thumb_02", "Thumb_03", "Thumb_End" };
            for (int i = 0; i < Thumb.Length; i++)
                b.Add(new RigBone(tn[i] + "_" + side, i == 0 ? "Hand_" + side : tn[i - 1] + "_" + side,
                    X + s * Thumb[i].x, Thumb[i].y, Thumb[i].z));

            b.Add(new RigBone("Thigh_" + side, "Pelvis", HX, GateA.Hip, 0));
            b.Add(new RigBone("ThighTwist_1_" + side, "Thigh_" + side, HX, GateA.Hip - GateA.Thigh * 0.40f, 0));
            b.Add(new RigBone("Shin_" + side, "Thigh_" + side, HX, GateA.Knee, 0));
            b.Add(new RigBone("Foot_" + side, "Shin_" + side, HX, GateA.Ankle, 0));
            b.Add(new RigBone("Toe_" + side, "Foot_" + side, HX, ToeY, ToeZ));
            b.Add(new RigBone("Toe_End_" + side, "Toe_" + side, HX, 0.020f, -0.185f));
        }

        // ---- the helpers the game uses (rig frame) ----

        /// <summary>Racket grip socket on the hand: butt cap position.</summary>
        public static Vector3 RacketGrip(string side)
        {
            float s = side == "R" ? 1f : -1f;
            var o = GateA.GripOffsetFromWrist;
            return new Vector3(s * ShoulderX + s * o.x, WristY + o.y, o.z);
        }

        public static Vector3 HeadLook { get { return new Vector3(0, 1.672f, -0.098f); } }
    }
}
