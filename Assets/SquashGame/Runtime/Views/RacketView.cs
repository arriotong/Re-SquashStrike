// ===========================================================
// Assets/SquashGame/Runtime/Views/RacketView.cs
// Port of lab/player/RacketView.js. Draws the racket from the exact
// transform RacketPhysics collides with - anchor, head position, face
// normal - so the racket you see IS the racket that hits.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class RacketView : MonoBehaviour
    {
        SimRunnerBase _runner;
        int _index;
        Transform _shaft, _head;

        public void Build(SimRunnerBase runner, int index, GreyboxPalette palette)
        {
            _runner = runner;
            _index = index;
            float hr = (float)PhysicsConfig.Current.Racket.HeadRadius;
            float sweet = (float)PhysicsConfig.Current.Racket.SweetSpotRadius;

            _shaft = Greybox.Primitive(PrimitiveType.Cube, "Shaft", transform,
                palette.LitColor(Greybox.Hex(0x2b2f38))).transform;

            _head = new GameObject("Head").transform;
            _head.SetParent(transform, false);
            Greybox.MeshObject("Rim", _head, Greybox.Torus(hr, 0.011f, 32, 8),
                palette.LitColor(Greybox.Hex(0xd8a33a)), true);
            var strings = Greybox.MeshObject("Strings", _head, Greybox.Disc(hr * 0.94f, 24),
                palette.Clear(Greybox.Hex(0xbfe6ff, 0.20f)));
            var back = Greybox.MeshObject("StringsBack", _head, Greybox.Disc(hr * 0.94f, 24),
                palette.Clear(Greybox.Hex(0xbfe6ff, 0.20f)));
            back.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var sweetGo = Greybox.MeshObject("SweetSpot", _head, Greybox.Disc(sweet, 16),
                palette.Clear(Greybox.Hex(0x5effc0, 0.30f)));
            sweetGo.transform.localPosition = new Vector3(0, 0, 0.002f);
            strings.transform.localPosition = Vector3.zero;
        }

        void LateUpdate()
        {
            if (_runner == null || _runner.Clock == null) return;
            AthleteSnapshot v = _runner.View.Athlete(_index);

            Greybox.PlaceBar(_shaft, v.RacketAnchor, v.RacketHead, 0.03f, 0.03f);

            _head.position = v.RacketHead;
            Vector3 n = v.RacketFaceNormal.sqrMagnitude > 1e-8f ? v.RacketFaceNormal.normalized : Vector3.forward;
            // Disc geometry faces local +Z; turn it to the face normal (strings are drawn on both sides).
            _head.rotation = Quaternion.LookRotation(n, Vector3.up);
        }
    }
}
