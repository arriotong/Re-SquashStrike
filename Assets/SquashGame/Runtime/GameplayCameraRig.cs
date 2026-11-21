// ===========================================================
// Assets/SquashGame/Runtime/GameplayCameraRig.cs
// Port of lab/core/GameplayCamera.js: the TV-style back-wall camera.
// Behind the glass, drifting only slightly with play. Below a 4:3
// aspect the vertical FOV widens so the court never gets cropped
// sideways. Presentation only: it smooths on render time, and nothing
// in gameplay reads the camera (movement and aim are court-relative).
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        const float REFERENCE_ASPECT = 4f / 3f;
        const float MAX_V_FOV = 72f;

        public SimRunnerBase Runner;
        Camera _cam;

        /// <summary>Settings > Gameplay > Camera: 0 classic (the back-wall camera the game was tuned on), 1 high, 2 low glass, 3 TV.</summary>
        public static int Preset;
        public const int TV = 3;
        public static readonly string[] PresetNames = { "Classic", "High", "Low glass", "TV" };
        // per preset: height, distance behind the back wall, field of view, look-at height, look-at depth (back wall = the config)
        // Classic: the back-wall camera the hitting was tuned on - close, a wide lens, the ball's pace reads.
        // TV (2026-10-24, measured off a PSA broadcast): far back in the stands and up high on a long lens - the court
        // fills the frame as on television, but the long lens flattens depth: the ball looks slower (playtest 2026-10-24:
        // "floaty" - Classic is the default again).
        static readonly float[,] Presets =
        {
            { 3.95f, 6.6f, 52f, 1.55f, 3.2f },     // Classic
            { 8.2f, 4.5f, 50f, 0.7f, 4.4f },       // High
            { 1.7f, 3.4f, 60f, 1.5f, 2.2f },       // Low glass
            { 6.0f, 12.5f, 24f, 0.9f, 1.5f },      // TV
        };
        /// <summary>A club's gallery is small and low: its TV and High views stay inside it.</summary>
        static readonly float[] ClubView = { 4.3f, 8.6f, 34f, 1.2f, 2.5f };
        float _lateral;
        /// <summary>PR5: after a rally this long, the break cuts to a high side angle (the broadcast's "what a rally").</summary>
        public const int BigRallyShots = 15;
        public bool CutAway { get; private set; }
        float _cutSide = 1f;
        /// <summary>The establishing shot (the Canary Wharf photo): high behind the back stand, the whole hall in view.
        /// Each match opens on it and flies down to the match camera.</summary>
        public static readonly Vector3 EstablishingPos = new Vector3(0f, 12.5f, 30f), EstablishingLook = new Vector3(0f, 0.6f, 4.2f);
        public const float EstablishingFov = 58f, FlySeconds = 3.4f;
        object _flyFor;
        float _flyT = 99f;
        public bool FlyingIn { get; private set; }
        readonly Replays.ShotCamera _shots = new Replays.ShotCamera();
        Vector3 _cutPos, _cutLook;
        int _replayFor = -1;
        /// <summary>The shot an in-match replay is on now (tests).</summary>
        public Replays.Shot ReplayShot { get { return _shots.Current; } }
        bool _wasCut;

        // ---------------- 2026-11-21 the polish pass: a kick on a big shot, the winner's close-up ----------------

        static float _kick;
        /// <summary>A small shake (a big shot): decays in a fraction of a second. Off with Reduce motion.</summary>
        public static void Kick(float amount) { if (UI.Juice.Big) _kick = Mathf.Max(_kick, amount); }
        /// <summary>The winner's close-up is on (tests).</summary>
        public bool WinnerCam { get; private set; }
        public const float WinnerCamFrom = 0.6f, WinnerCamBlend = 1.3f;
        Vector3 _wcFromPos; Quaternion _wcFromRot; float _wcFromFov; object _wcFor;

        void ApplyKick()
        {
            if (_kick <= 0.0005f) { _kick = 0f; return; }
            float t = Time.unscaledTime * 60f;
            transform.position += transform.right * (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * _kick + transform.up * (Mathf.PerlinNoise(0.7f, t) - 0.5f) * _kick;
            _kick *= Mathf.Exp(-14f * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        /// <summary>The match won: after the last ball, the camera swings down in front of the winner as they celebrate, and drifts
        /// round them. True: filmed.</summary>
        bool FilmWinner(MatchRunner m)
        {
            WinnerCam = false;
            if (m == null || m.Demo || m.Sim == null || m.Sim.Phase != MatchPhase.MatchOver || m.CelebrationTime < 0 || !UI.Juice.Big) return false;
            int w = m.Sim.Score.MatchWinner;
            if (w < 0 || w >= m.Athletes.Length) return false;
            float t = m.CelebrationTime - WinnerCamFrom;
            if (t < 0) { _wcFor = null; return false; }
            if (!ReferenceEquals(_wcFor, m.Sim)) { _wcFor = m.Sim; _wcFromPos = transform.position; _wcFromRot = transform.rotation; _wcFromFov = _cam.fieldOfView; }
            var p = m.Athletes[w].Player.Pos;
            double side = p.X >= 0 ? -1 : 1;                                             // from the open side of the court
            double orbit = side * (0.9 + 0.35 * Mathf.Sin(t * 0.35f));
            double camZ = System.Math.Max(0.7, p.Z - 2.6);
            Vector3 pos = SimToWorld.Pos(p.X + orbit, 1.45 + 0.1 * Mathf.Sin(t * 0.5f), camZ);
            Vector3 look = SimToWorld.Pos(p.X, 1.05, p.Z);
            float u = Mathf.Clamp01(t / WinnerCamBlend), e = u * u * u * (u * (u * 6f - 15f) + 10f);
            transform.position = Vector3.Lerp(_wcFromPos, pos, e);
            transform.rotation = Quaternion.Slerp(_wcFromRot, Quaternion.LookRotation(look - pos, Vector3.up), e);
            _cam.fieldOfView = Mathf.Lerp(_wcFromFov, 40f, e);
            WinnerCam = true;
            return true;
        }

        /// <summary>2026-11-22 aiming a Special Shot (yours, or either local player's): the camera drops in behind the ball, looking
        /// at the front wall - the aiming screen. True: filmed.</summary>
        public bool ZoneCam { get; private set; }
        float _zoneBlend;
        bool FilmZone(MatchRunner m)
        {
            bool on = m != null && !m.Demo && m.Sim != null && m.Sim.ZoneBy >= 0 && (m.Sim.ZoneBy == m.HumanIndex || m.Settings.TwoPlayer) && !m.ReplayActive;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _zoneBlend = on ? Mathf.MoveTowards(_zoneBlend, 1f, dt / 0.25f) : 0f;
            ZoneCam = on;
            if (!on) return false;
            var b = m.Sim.Ball.Pos;
            var aim = m.Sim.Players[m.Sim.ZoneBy].Aim;
            double ax = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.X : b.X, ay = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.Y : 1.2;
            // up and to the ball's side, away from the player (who stands beside it), so the front wall and the aim are in view
            var pl = m.Sim.Players[m.Sim.ZoneBy].Player.Pos;
            double side = b.X >= pl.X ? 1 : -1;
            double cx = System.Math.Max(-2.9, System.Math.Min(2.9, b.X + side * 1.0));
            Vector3 pos = SimToWorld.Pos(cx, b.Y + 1.6, System.Math.Min(CourtDimensions.LENGTH + 0.5, b.Z + 3.4));
            Vector3 look = Vector3.Lerp(SimToWorld.Pos(b.X * 0.5, 1.6, 0.0), SimToWorld.Pos(ax, ay, 0.0), 0.5f);
            float e = _zoneBlend * _zoneBlend * (3f - 2f * _zoneBlend);
            transform.position = Vector3.Lerp(transform.position, pos, e);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - pos, Vector3.up), e);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 50f, e);
            return true;
        }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 120f;
        }

        void LateUpdate()
        {
            var cfg = PhysicsConfig.Current.Camera;

            // P2-M6: the replay theatre's cameras.
            if (Replays.TheatreSession.Active != null && Runner != null && Runner.FramesActive)
            {
                Replays.TheatreSession.Active.Film(_cam, Runner, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                return;
            }

            // A cutscene: the walk-on, the prestige fly-around (camera work, 2026-10-28).
            if (Runner != null && Runner.ReplayActive && Runner.CinematicKind != null)
            {
                Replays.Cutscenes.Film(_cam, Runner.View, Runner.ReplayProgress, Runner.CinematicOrbit, Courts.Current.Establishing, ref _cutPos, ref _cutLook, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                return;
            }

            // A replay between rallies: the director's cuts (camera work, 2026-10-28) - the referee's review keeps its view.
            if (Runner != null && Runner.ReplayActive && !(Runner is MatchRunner rv && rv.ReviewingCall))
            {
                if (_replayFor != Runner.ReplayNumber) { _replayFor = Runner.ReplayNumber; _shots.Reset(); }
                _shots.Film(_cam, Replays.BroadcastDirector.Cut(Runner.ReplayProgress, Runner.ReplayBig, Runner.ReplayNumber), Runner.View, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                return;
            }

            // Referee replay: high above the court, looking down on the incident.
            if (Runner != null && Runner.ReplayActive)
            {
                _cam.fieldOfView = 55f;
                Vector3 focus = Runner.ReplayFocus;
                transform.position = SimToWorld.Pos(0, 8.5, 8.5);
                transform.LookAt(new Vector3(focus.x * 0.6f, 0.6f, focus.z), Vector3.up);
                return;
            }

            // PR5: a big rally just ended - hold a high side angle through the break.
            CutAway = false;
            if (Runner is MatchRunner big && !big.Demo && !big.InSetup && big.Sim != null && big.Sim.Phase == MatchPhase.RallyOver &&
                big.Sim.ShotsPerRally.Count > 0 && big.Sim.ShotsPerRally[big.Sim.ShotsPerRally.Count - 1] >= BigRallyShots)
            {
                if (!_wasCut) _cutSide = Runner.Ball.Pos.X >= 0 ? -1f : 1f;           // from the side the ball isn't
                CutAway = _wasCut = true;
                float kc = 1f - Mathf.Exp(-3f * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                Vector3 want = SimToWorld.Pos(_cutSide * 2.7, 3.6, 9.3);
                transform.position = Vector3.Lerp(transform.position, want, kc);
                Quaternion look = Quaternion.LookRotation(SimToWorld.Pos(-_cutSide * 0.4, 0.9, 3.4) - transform.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, kc);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 54f, kc);
                return;
            }
            _wasCut = false;

            if (FilmWinner(Runner as MatchRunner)) return;
            if (FilmZone(Runner as MatchRunner)) return;

            // The menus' demo rally: slow-motion cuts from cinematic angles (the old home screen).
            if (Runner is MatchRunner menu && menu.Demo)
            {
                MenuCinematics.Apply(_cam, Runner, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                return;
            }

            // Projection: hold the horizontal field on narrow windows.
            float aspect = _cam.aspect;
            int p = Mathf.Clamp(Preset, 0, PresetNames.Length - 1);
            if (Runner is MatchRunner mr0 && mr0.Demo) p = 0;                            // (the menus' demo films its own cinematics)
            bool club = VenueView.Detail > 0 && Courts.Current.Id == "club" && (p == TV || p == 1);
            float Pv(int k) { return club ? ClubView[k] : Presets[p, k]; }
            float fov = Pv(2);
            if (aspect < REFERENCE_ASPECT)
            {
                float refTan = Mathf.Tan(fov * Mathf.Deg2Rad / 2f);
                fov = Mathf.Min(2f * Mathf.Atan(refTan * REFERENCE_ASPECT / aspect) * Mathf.Rad2Deg, MAX_V_FOV);
            }
            _cam.fieldOfView = fov;

            // Drift a little toward the action, midway between player and ball (sim x).
            double focusX = 0;
            if (Runner != null && Runner.Clock != null)
            {
                // Midway between the ball and the athletes (the lab: one athlete).
                var a = Runner.Athletes;
                double px = 0;
                for (int i = 0; i < a.Length; i++) px += a[i].Player.Pos.X;
                if (a.Length > 0) px /= a.Length;
                focusX = (px + Runner.Ball.Pos.X) * 0.5;
            }
            float target = (float)(focusX * cfg.LateralFollow);
            float k = 1f - Mathf.Exp(-(float)cfg.FollowSmoothing * Mathf.Max(0f, Time.unscaledDeltaTime));
            _lateral += (target - _lateral) * k;

            double h = Pv(0), dist = Pv(1);
            double lh = Pv(3), ld = Pv(4);
            transform.position = SimToWorld.Pos(_lateral, h, CourtDimensions.LENGTH + dist);
            transform.LookAt(SimToWorld.Pos(_lateral * 0.5, lh, ld), Vector3.up);

            // A new match: open on the establishing shot and fly down to this view while the players get ready.
            FlyingIn = false;
            if (Runner is MatchRunner fm && !fm.Demo && !fm.InSetup && fm.Sim != null && VenueView.Detail > 0 && Courts.Current.Establishing)
            {
                if (_flyFor != fm.Sim) { _flyFor = fm.Sim; _flyT = fm.Sim.History.Count == 0 ? 0f : 99f; }
                if (_flyT < FlySeconds && fm.Sim.Phase == MatchPhase.AwaitToss && fm.Sim.History.Count == 0)
                {
                    _flyT += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    float u = Mathf.Clamp01(_flyT / FlySeconds);
                    float e = u * u * u * (u * (u * 6f - 15f) + 10f);                          // smootherstep
                    Vector3 from = SimToWorld.Pos(EstablishingPos.x, EstablishingPos.y, EstablishingPos.z);
                    Quaternion fromRot = Quaternion.LookRotation(SimToWorld.Pos(EstablishingLook.x, EstablishingLook.y, EstablishingLook.z) - from, Vector3.up);
                    transform.position = Vector3.Lerp(from, transform.position, e);
                    transform.rotation = Quaternion.Slerp(fromRot, transform.rotation, e);
                    _cam.fieldOfView = Mathf.Lerp(EstablishingFov, _cam.fieldOfView, e);
                    FlyingIn = u < 1f;
                }
                else _flyT = 99f;
            }
            ApplyKick();
        }
    }
}
