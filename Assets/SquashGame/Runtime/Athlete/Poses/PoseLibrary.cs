// ===========================================================
// Assets/SquashGame/Runtime/Athlete/Poses/PoseLibrary.cs
// M7: the athlete's key poses. DefaultPoses builds the authored set from
// code; a PoseLibrary asset at Resources/PoseLibrary (saved by the Pose
// Lab window) overrides it.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame.Athlete
{
    [CreateAssetMenu(menuName = "re:SquashStrike/Pose Library")]
    public sealed class PoseLibrary : ScriptableObject
    {
        public List<AthletePose> Poses = new List<AthletePose>();

        public AthletePose Find(string name)
        {
            foreach (var p in Poses) if (p.Name == name) return p;
            return null;
        }

        static PoseLibrary _active;

        /// <summary>The library in use: the saved asset (Resources/PoseLibrary) if there is one, else the authored defaults.</summary>
        public static PoseLibrary Active
        {
            get
            {
                if (_active != null) return _active;
                _active = Resources.Load<PoseLibrary>("PoseLibrary");
                if (_active == null || _active.Poses.Count == 0)
                {
                    _active = CreateInstance<PoseLibrary>();
                    _active.Poses = DefaultPoses.Build();
                }
                return _active;
            }
        }

        public static void ResetActive() { _active = null; }
    }
}
