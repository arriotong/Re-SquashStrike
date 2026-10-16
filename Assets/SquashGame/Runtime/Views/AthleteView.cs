// ===========================================================
// Assets/SquashGame/Runtime/Views/AthleteView.cs
// Port of lab/player/PlayerView.js: the greybox capsule. A strict
// follower of gameplay state - nothing here writes back. Not a
// character model (that is M4, the Gate B athlete).
//
// The shoulder bar follows the commitment solver's BODY yaw - the
// direction the athlete visibly prepares to play - not the aim. That
// difference is what an opponent reads and what a late change hides.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class AthleteView : MonoBehaviour
    {
        SimRunnerBase _runner;
        int _index;
        Transform _body, _shoulders, _shadow;

        public void Build(SimRunnerBase runner, int index, Color bodyColor, GreyboxPalette palette)
        {
            _runner = runner;
            _index = index;
            var cfg = PhysicsConfig.Current.Player;
            float r = (float)cfg.Radius, h = (float)cfg.Height;

            var bodyMat = palette.LitColor(bodyColor);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var capsule = Greybox.Primitive(PrimitiveType.Capsule, "Capsule", _body, bodyMat);
            // Unity capsule: height 2, radius 0.5.
            capsule.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            capsule.transform.localPosition = new Vector3(0, h * 0.5f, 0);
            // Facing marker (nose): makes court-relative movement legible.
            Greybox.Box("Nose", _body, palette.LitColor(Greybox.Hex(0xd8a33a)),
                new Vector3(0, h * 0.62f, r + 0.12f), new Vector3(0.10f, 0.10f, 0.28f));

            _shoulders = new GameObject("Shoulders").transform;
            _shoulders.SetParent(transform, false);
            Greybox.Box("ShoulderBar", _shoulders, palette.LitColor(Greybox.Hex(0xe8edf2)),
                new Vector3(0, h * 0.78f, 0), new Vector3(0.62f, 0.07f, 0.10f));
            Greybox.Box("Chest", _shoulders, palette.LitColor(Greybox.Hex(0x6fe3ff)),
                new Vector3(0, h * 0.78f, r + 0.06f), new Vector3(0.08f, 0.08f, 0.20f));

            var shadow = Greybox.MeshObject("AthleteShadow", transform, Greybox.Disc(r * 1.05f, 24),
                palette.Clear(new Color(0, 0, 0, 0.32f)));
            _shadow = shadow.transform;
            _shadow.rotation = Quaternion.Euler(-90f, 0f, 0f);   // disc (+Z facing) turned to face up
        }

        void LateUpdate()
        {
            if (_runner == null || _runner.Clock == null) return;
            AthleteSnapshot v = _runner.View.Athlete(_index);
            _body.SetPositionAndRotation(v.Pos, Quaternion.LookRotation(v.Forward, Vector3.up));
            _shoulders.SetPositionAndRotation(v.Pos, Quaternion.LookRotation(v.ShoulderForward, Vector3.up));
            _shadow.position = new Vector3(v.Pos.x, 0.008f, v.Pos.z);
        }
    }
}
