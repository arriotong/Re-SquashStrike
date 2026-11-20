// ===========================================================
// Assets/SquashGame/Runtime/Views/KitStudio.cs
// 2026-11-20 the Locker Room's photo studio (the user: "put the character
// into another place, not on the court anymore, and make it stand
// completely straight"): a closed round room far from every court - a
// soft backdrop, a dark glossy floor with a lime ring, its own key, fill
// and rim lights. Your athlete stands in it, straight, while the Locker
// Room is open (MatchRunner.Studio). Presentation only.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public static class KitStudio
    {
        /// <summary>Where the studio is: sim metres (x, 0, z) - on the ground, beyond every venue (the biggest reach ~900 m).</summary>
        public static readonly Vector3 Sim = new Vector3(0f, 0f, -1500f);
        public static Vector3 World { get { return SimToWorld.Pos(Sim.x, 0.0, Sim.z); } }
        /// <summary>Where the opponent stands in it, for the match setup's picture of them (beside you, out of your shot).</summary>
        public static readonly Vector3 ThemSim = new Vector3(Sim.x + 4.5f, 0f, Sim.z);
        public static Vector3 ThemWorld { get { return SimToWorld.Pos(ThemSim.x, 0.0, ThemSim.z); } }
        const float Radius = 9f;

        static GameObject _room;

        /// <summary>Show or hide the studio (built the first time, in the court's scene).</summary>
        public static void Show(bool on)
        {
            if (on && _room == null) Build();
            if (_room != null) _room.SetActive(on);
        }

        static void Build()
        {
            var court = Object.FindFirstObjectByType<MatchCourt>();
            if (court == null || court.Palette == null) return;
            var pal = court.Palette;
            _room = new GameObject("KitStudio");
            _room.transform.SetParent(court.transform, false);
            _room.transform.position = World;
            var t = _room.transform;

            // the backdrop: a dome all round, dark at the floor, a soft grey-blue up behind you
            var back = pal.UnlitColor(Color.white);
            back.mainTexture = ProcTex.LegacySky("studio", new Color(0.04f, 0.045f, 0.055f), new Color(0.16f, 0.18f, 0.22f), new Color(0.10f, 0.11f, 0.14f), new Color(0.05f, 0.055f, 0.07f), 0, 201);
            var dome = Greybox.MeshObject("Backdrop", t, VenueView.Dome(Radius, 40, 20), back);
            dome.transform.localPosition = Vector3.zero;
            // the floor: dark and glossy, a lime ring where you stand
            var floor = pal.LitColor(new Color(0.07f, 0.075f, 0.09f)); floor.SetFloat("_Smoothness", 0.85f);
            var disc = Greybox.MeshObject("Floor", t, Greybox.Disc(Radius, 64), floor);
            disc.transform.localPosition = new Vector3(0, 0.001f, 0); disc.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var ring = pal.UnlitColor(new Color(0.78f, 1.0f, 0.24f) * 1.6f);
            var r = Greybox.MeshObject("Ring", t, Greybox.Torus(0.75f, 0.02f, 64, 6), ring);
            r.transform.localPosition = new Vector3(0, 0.01f, 0); r.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            // the lights: a warm key front-left, a cool fill front-right, a lime rim behind
            Light(t, "StudioKey", new Vector3(-2.2f, 3.2f, -2.6f), new Color(1f, 0.95f, 0.88f), 9f, 7f);
            Light(t, "StudioFill", new Vector3(2.4f, 2.0f, -2.2f), new Color(0.75f, 0.85f, 1f), 8f, 3f);
            Light(t, "StudioRim", new Vector3(0.4f, 2.6f, 2.4f), new Color(0.78f, 1f, 0.4f), 7f, 4f);
            Light(t, "StudioKeyThem", SimToWorld.Pos(ThemSim.x, 3.2, ThemSim.z - 2.6) - World, new Color(1f, 0.95f, 0.88f), 9f, 7f);
            var ring2 = Greybox.MeshObject("RingThem", t, Greybox.Torus(0.75f, 0.02f, 64, 6), ring);
            ring2.transform.position = ThemWorld + new Vector3(0, 0.01f, 0); ring2.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        static void Light(Transform parent, string name, Vector3 local, Color colour, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = local;
            l.type = LightType.Point; l.color = colour; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }
    }
}
