// ===========================================================
// Assets/SquashGame/Runtime/Views/Dioramas.cs
// 2026-11-20 the menu worlds (the user, after the Gauntlet's island: "our
// other modes should use this style ... don't have to use the same
// background, I just want the same style ... every background should have
// squash elements in it"). One toon world per mode, each its own time of
// day, far from every court and from each other:
//   Gauntlet    the floating island at sunset (GauntletIsland)
//   Home        a park island on a sunny afternoon (the Play hub)
//   Career      a city on a hill at dusk: the stadium, the trophy
//   Challenges  a floating platform under the stars: the giant front wall
//   Training    a practice camp on a bright morning: cones, the ball machine
// A screen names its world (MenuScreen.Backdrop); FrontEnd shows the top
// one while the menus' demo plays - the real court stays for match setup,
// the title and the Locker Room's studio. The camera holds still on it
// (MenuCinematics). Presentation only.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;

namespace SquashGame
{
    public enum MenuWorld { None, Gauntlet, Home, Career, Challenges, Training, Modes, Online, Packs, Store, You, Replays, Settings, Play }

    public static partial class Dioramas
    {
        /// <summary>The camera's far plane must reach a world's sky (radius 420).</summary>
        public const float FarClip = 1000f;

        struct Def
        {
            public Vector3 Sim, Cam, Look; public float Fov;
            public System.Action<Transform, DioramaLife> Build;
        }

        static Def Get(MenuWorld w)
        {
            switch (w)
            {
                case MenuWorld.Gauntlet: return new Def { Sim = new Vector3(0f, 0f, -4000f), Cam = GauntletIsland.CamLocal, Look = GauntletIsland.LookLocal, Fov = GauntletIsland.Fov, Build = GauntletIsland.Build };
                case MenuWorld.Home: return new Def { Sim = new Vector3(3000f, 0f, -4000f), Cam = HomeCam, Look = HomeLook, Fov = 38f, Build = BuildHome };
                case MenuWorld.Career: return new Def { Sim = new Vector3(-3000f, 0f, -4000f), Cam = CareerCam, Look = CareerLook, Fov = 38f, Build = BuildCareer };
                case MenuWorld.Challenges: return new Def { Sim = new Vector3(0f, 0f, -7000f), Cam = ChallengeCam, Look = ChallengeLook, Fov = 38f, Build = BuildChallenges };
                case MenuWorld.Training: return new Def { Sim = new Vector3(3000f, 0f, -7000f), Cam = TrainingCam, Look = TrainingLook, Fov = 38f, Build = BuildTraining };
                // 2026-11-21 a world for every hub tab
                case MenuWorld.Modes: return new Def { Sim = new Vector3(-3000f, 0f, -7000f), Cam = StdCam, Look = StdLook, Fov = 38f, Build = BuildModes };
                case MenuWorld.Online: return new Def { Sim = new Vector3(0f, 0f, -10000f), Cam = new Vector3(4f, 16f, 84f), Look = new Vector3(22f, 4f, -12f), Fov = 40f, Build = BuildOnline };
                case MenuWorld.Packs: return new Def { Sim = new Vector3(3000f, 0f, -10000f), Cam = NearCam, Look = NearLook, Fov = 38f, Build = BuildPacks };
                case MenuWorld.Store: return new Def { Sim = new Vector3(-3000f, 0f, -10000f), Cam = NearCam, Look = NearLook, Fov = 38f, Build = BuildStore };
                case MenuWorld.You: return new Def { Sim = new Vector3(6000f, 0f, -4000f), Cam = NearCam, Look = NearLook, Fov = 38f, Build = BuildYou };
                case MenuWorld.Settings: return new Def { Sim = new Vector3(6000f, 0f, -10000f), Cam = NearCam, Look = NearLook, Fov = 38f, Build = BuildSettings };   // 2026-11-21
                case MenuWorld.Play: return new Def { Sim = new Vector3(-6000f, 0f, -4000f), Cam = StdCam, Look = StdLook, Fov = 38f, Build = BuildPlay };   // 2026-11-22 Versus Night
                case MenuWorld.Replays: return new Def { Sim = new Vector3(6000f, 0f, -7000f), Cam = NearCam, Look = NearLook, Fov = 38f, Build = BuildReplays };
                default: return default;
            }
        }

        public static Vector3 Origin(MenuWorld w) { var d = Get(w); return SimToWorld.Pos(d.Sim.x, 0.0, d.Sim.z); }
        public static Vector3 CamPos(MenuWorld w) { return Origin(w) + Get(w).Cam; }
        public static Vector3 LookAt(MenuWorld w) { return Origin(w) + Get(w).Look; }
        public static float Fov(MenuWorld w) { return Get(w).Fov; }

        static readonly Dictionary<MenuWorld, GameObject> _roots = new Dictionary<MenuWorld, GameObject>();
        /// <summary>The world up now (None: the court).</summary>
        public static MenuWorld Current { get; private set; }
        public static bool Shown(MenuWorld w) { return _roots.TryGetValue(w, out var r) && r != null && r.activeSelf; }
        static bool _fogSaved, _fogWas;

        /// <summary>A new scene (MatchRunner.Awake): the old worlds went with the last one, its fog too.</summary>
        public static void Reset() { _roots.Clear(); Current = MenuWorld.None; _fogSaved = false; _ambSaved = false; }

        // ---- 2026-11-25 (the user: "some legacy courts have like effects and when i choose the courts they like appear in the background
        // try to make them not show"): the ambient light every venue sets for the whole scene ----
        static bool _ambSaved;
        static UnityEngine.Rendering.AmbientMode _ambMode;
        static Color _ambSky, _ambEq, _ambGround, _ambFlat;
        /// <summary>The worlds' ambient: the show court's night (what they were made under).</summary>
        public static readonly Color NeutralSky = new Color(0.18f, 0.174f, 0.186f), NeutralEquator = new Color(0.162f, 0.144f, 0.132f), NeutralGround = new Color(0.072f, 0.066f, 0.066f);
        static void SaveAmbient() { _ambMode = RenderSettings.ambientMode; _ambSky = RenderSettings.ambientSkyColor; _ambEq = RenderSettings.ambientEquatorColor; _ambGround = RenderSettings.ambientGroundColor; _ambFlat = RenderSettings.ambientLight; }
        static void RestoreAmbient() { RenderSettings.ambientMode = _ambMode; RenderSettings.ambientSkyColor = _ambSky; RenderSettings.ambientEquatorColor = _ambEq; RenderSettings.ambientGroundColor = _ambGround; if (_ambMode == UnityEngine.Rendering.AmbientMode.Flat) RenderSettings.ambientLight = _ambFlat; }
        static void ApplyNeutral() { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = NeutralSky; RenderSettings.ambientEquatorColor = NeutralEquator; RenderSettings.ambientGroundColor = NeutralGround; }

        /// <summary>Each frame a world is up: a court built under it (the court picker) set its own ambient - keep that for later, the
        /// neutral back on.</summary>
        public static void HoldAmbient()
        {
            if (Current == MenuWorld.None || !_ambSaved) return;
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight && RenderSettings.ambientSkyColor == NeutralSky &&
                RenderSettings.ambientEquatorColor == NeutralEquator && RenderSettings.ambientGroundColor == NeutralGround) return;
            // a court was built under the world (every court sets its ambient): its light and its fog (the station's haze, the reef's
            // water, the jungle's mist - it swallowed the world's sky) kept for the court, off here
            SaveAmbient();
            if (_fogSaved) { _fogWas = RenderSettings.fog; RenderSettings.fog = false; }
            ApplyNeutral();
        }
        /// <summary>The ambient on now is the worlds' neutral (tests).</summary>
        public static bool AmbientHeld { get { return RenderSettings.ambientSkyColor == NeutralSky && !RenderSettings.fog; } }

        /// <summary>Show this world (built the first time), hide the rest; None: the court again.</summary>
        public static void Show(MenuWorld w)
        {
            if (w != MenuWorld.None && (!_roots.TryGetValue(w, out var r) || r == null)) Build(w);
            if (w != MenuWorld.None && (!_roots.TryGetValue(w, out var built) || built == null)) w = MenuWorld.None;   // (no court to build in)
            foreach (var kv in _roots) if (kv.Value != null && kv.Value.activeSelf != (kv.Key == w)) kv.Value.SetActive(kv.Key == w);
            Current = w;
            // 2026-11-25 the court's ambient light (a legacy court's teal sea, green jungle, brass hall...) tinted the world: held
            // at the show court's while a world is up (HoldAmbient), the court's own back after
            if (w != MenuWorld.None && !_ambSaved) { SaveAmbient(); _ambSaved = true; ApplyNeutral(); }
            else if (w == MenuWorld.None && _ambSaved) { RestoreAmbient(); _ambSaved = false; }
            // the arenas' dark fog (it ends ~90 m) would swallow a sky: off while a world is up, back after
            if (w != MenuWorld.None && !_fogSaved) { _fogWas = RenderSettings.fog; _fogSaved = true; RenderSettings.fog = false; }
            else if (w == MenuWorld.None && _fogSaved) { RenderSettings.fog = _fogWas; _fogSaved = false; }
        }

        static void Build(MenuWorld w)
        {
            var court = Object.FindFirstObjectByType<MatchCourt>();
            if (court == null || court.Palette == null) return;
            ToonKit.Begin(court.Palette);
            var root = new GameObject("World_" + w);
            root.transform.SetParent(court.transform, false);
            root.transform.position = Origin(w);
            var life = root.AddComponent<DioramaLife>();
            Get(w).Build(root.transform, life);
            Motes(root.transform, life, Get(w).Cam, Get(w).Look, (int)w);
            _roots[w] = root;
        }

        /// <summary>2026-11-21 the polish pass (idle menu life): soft glowing motes drift up through every world, between the
        /// camera and what it looks at.</summary>
        static void Motes(Transform root, DioramaLife life, Vector3 cam, Vector3 look, int seed)
        {
            var rng = new System.Random(4242 + seed);
            float dist = Vector3.Distance(cam, look);
            var holder = new GameObject("Motes").transform; holder.SetParent(root, false);
            for (int i = 0; i < 34; i++)
            {
                float along = 0.3f + 0.65f * (float)rng.NextDouble();
                Vector3 at = Vector3.Lerp(cam, look, along);
                float spread = dist * 0.32f * along;
                at += new Vector3(((float)rng.NextDouble() - 0.5f) * 2f * spread, ((float)rng.NextDouble() - 0.5f) * spread, ((float)rng.NextDouble() - 0.5f) * spread * 0.6f);
                Color c = i % 3 == 0 ? new Color(0.85f, 1f, 0.45f, 0.75f) : new Color(1f, 0.97f, 0.85f, 0.6f);
                var g = ToonKit.Billboard(holder, "Mote", at, dist * (0.004f + 0.006f * (float)rng.NextDouble()), c, cam);
                life.Float(g.transform, dist * (0.006f + 0.01f * (float)rng.NextDouble()), spread * 0.9f + 1f, (float)rng.NextDouble() * 6f);
            }
        }
    }
}
