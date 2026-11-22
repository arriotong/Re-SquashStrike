// ===========================================================
// Assets/SquashGame/Runtime/Views/VenueMotion.cs
// 2026-11-22 evening (the user: "for the legacy courts ... some of them looks
// a bit boring and too flat like the beach side one maybe adding more
// different elements and 3D things like moving waves that gets high and
// low ... do this with every legacy court"): the venues' life - one small
// component with a few kinds of motion, set when a venue is built:
//
//   Bob     up and down (a wave, a boat, a jellyfish)
//   Sway    a rotation back and forth about an axis (kelp, a flag, a beam)
//   Spin    a steady rotation (a planet, a fan, a school of fish round a
//           pivot - the fish are its children)
//   Drift   along a direction, wrapping round after a distance (rain,
//           snow, bubbles, a balloon, cars)
//   Pulse   the size breathing (a torch, lava, a light)
//
// Presentation only: real time, never the sim. Off when the venue is built
// for a thumbnail or a capture with motion frozen (Frozen).
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public sealed class VenueMotion : MonoBehaviour
    {
        public enum Kind { Bob, Sway, Spin, Drift, Pulse }
        public Kind Mode;
        public Vector3 Axis = Vector3.up;
        public float Amount = 1f, Speed = 1f, Phase;
        /// <summary>Drift: how far before it wraps back to the start.</summary>
        public float Span = 10f;
        /// <summary>Every venue's motion held still (tests that compare frames).</summary>
        public static bool Frozen;
        /// <summary>Live motions (tests).</summary>
        public static int Live { get; private set; }

        Vector3 _pos, _scale; Quaternion _rot; bool _init;

        void OnEnable() { Live++; }
        void OnDisable() { Live--; }

        void Init() { _init = true; _pos = transform.localPosition; _rot = transform.localRotation; _scale = transform.localScale; }

        void Update()
        {
            if (!_init) Init();
            if (Frozen) return;
            float t = Time.unscaledTime * Speed + Phase;
            switch (Mode)
            {
                case Kind.Bob: transform.localPosition = _pos + Axis * (Mathf.Sin(t) * Amount); break;
                case Kind.Sway: transform.localRotation = _rot * Quaternion.AngleAxis(Mathf.Sin(t) * Amount, Axis); break;
                case Kind.Spin: transform.localRotation = _rot * Quaternion.AngleAxis(t * Amount, Axis); break;
                case Kind.Drift: transform.localPosition = _pos + Axis.normalized * Mathf.Repeat(t * Amount, Span); break;
                case Kind.Pulse: transform.localScale = _scale * (1f + Mathf.Sin(t) * Amount); break;
            }
        }

        /// <summary>Put a motion on an object (returns it).</summary>
        public static VenueMotion Add(GameObject go, Kind mode, Vector3 axis, float amount, float speed, float phase = 0f, float span = 10f)
        {
            var m = go.AddComponent<VenueMotion>();
            m.Mode = mode; m.Axis = axis; m.Amount = amount; m.Speed = speed; m.Phase = phase; m.Span = span;
            return m;
        }
    }
}
