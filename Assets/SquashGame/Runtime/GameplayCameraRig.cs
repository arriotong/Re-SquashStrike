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
        /// <summary>Focus: the field of view narrows by this share (a zoom in), easing over FocusZoomSeconds (real time).</summary>
        public const float FocusZoom = 0.12f, FocusZoomSeconds = 0.18f;
        float _focusZoom;
        /// <summary>How far into the Focus zoom (0..1; tests).</summary>
        public float FocusZoomNow { get { return _focusZoom; } }
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

        // ---------------- 2026-11-22 evening: the Special Shot's cut-in (Captain Tsubasa style) ----------------
        static int _cutWho = -1;
        static float _cutLeft, _cutTotal;
        /// <summary>Film the striker's face, close and low, pushing in, for this long (real seconds) - the strike's cut-in.</summary>
        public static void CutIn(int who, float seconds) { _cutWho = who; _cutLeft = seconds; _cutTotal = Mathf.Max(0.01f, seconds); }
        public static bool CutInShowing { get { return _cutLeft > 0f; } }
        public static int CutInWho { get { return _cutWho; } }
        bool FilmCutIn(MatchRunner m)
        {
            if (_cutLeft <= 0f) return false;
            if (m == null || m.Sim == null || m.Demo || m.ReplayActive || _cutWho < 0 || _cutWho > 1) { _cutLeft = 0f; return false; }
            _cutLeft -= Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = 1f - Mathf.Clamp01(_cutLeft / _cutTotal);
            float push = k * k * (3f - 2f * k);
            // the face as drawn: in front of it, a little to the side and low - looking up at it, pushing in
            Athlete.AthleteFigure fig = null;
            foreach (var f in FindObjectsByType<Athlete.AthleteFigure>(FindObjectsSortMode.None)) if (f.Index == _cutWho) fig = f;
            Vector3 head, fwd;
            if (fig != null) { head = fig.HeadPosition; fwd = fig.DrawnForward; }
            else { var pl = m.Sim.Players[_cutWho].Player.Pos; head = SimToWorld.Pos(pl.X, pl.Y + 1.52, pl.Z); fwd = SimToWorld.Pos(0, 0, -1) - SimToWorld.Pos(0, 0, 0); }
            fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 pos = head + fwd * (1.7f - 0.6f * push) + side * (0.55f - 0.25f * push) + Vector3.down * (0.32f - 0.08f * push);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(head - pos, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(40f, 30f, push);
            return true;
        }

        /// <summary>2026-11-22 aiming a Special Shot (yours, or either local player's): the camera drops in behind the ball, looking
        /// at the front wall - the aiming screen. True: filmed.</summary>
        public bool ZoneCam { get; private set; }
        float _zoneBlend;
        Vector3 _zoneLook; bool _zoneLookSet;
        bool FilmZone(MatchRunner m)
        {
            int z = m != null && m.Sim != null ? (m.Sim.ZoneBy >= 0 ? m.Sim.ZoneBy : m.Sim.WindupBy) : -1;     // (the wind-up too)
            bool on = m != null && !m.Demo && m.Sim != null && z >= 0 && (z == m.HumanIndex || m.Settings.TwoPlayer) && !m.ReplayActive;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _zoneBlend = on ? Mathf.MoveTowards(_zoneBlend, 1f, dt / 0.25f) : 0f;
            ZoneCam = on;
            if (!on) { _zoneLookSet = false; return false; }
            var b = m.Sim.Ball.Pos;
            double ax = m.Sim.SpecialAimX, ay = m.Sim.SpecialAimY > 0 ? m.Sim.SpecialAimY : 1.2;
            // 2026-11-22 (the user: "make it first person aiming"): through the striker's eyes, up in the air above the T - just in
            // front of the head (so the body isn't in the way), turning to follow the aim on the front wall
            var pl = m.Sim.Players[z].Player.Pos;
            Vector3 pos = SimToWorld.Pos(pl.X, pl.Y + 1.62, pl.Z - 0.32);
            Vector3 aimAt = SimToWorld.Pos(ax, ay, 0.0);
            if (!_zoneLookSet) { _zoneLook = Vector3.Lerp(SimToWorld.Pos(0, 1.5, 0.0), aimAt, 0.5f); _zoneLookSet = true; }
            _zoneLook = Vector3.Lerp(_zoneLook, aimAt, 1f - Mathf.Exp(-12f * dt));
            Vector3 look = Vector3.Lerp(aimAt, _zoneLook, 0.6f);
            float e = _zoneBlend * _zoneBlend * (3f - 2f * _zoneBlend);
            transform.position = Vector3.Lerp(transform.position, pos, e);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - pos, Vector3.up), e);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 64f, e);
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
            if (FilmCutIn(Runner as MatchRunner)) return;
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
            // 2026-11-22 evening (the user: "when you hold you get into focus mode the screen zooms in a bit"): eased in and out
            bool focusing = Runner is MatchRunner fr && fr.Focusing;
            _focusZoom = Mathf.MoveTowards(_focusZoom, focusing ? 1f : 0f, Time.unscaledDeltaTime / FocusZoomSeconds);
            fov *= 1f - FocusZoom * Mathf.SmoothStep(0f, 1f, _focusZoom);
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
