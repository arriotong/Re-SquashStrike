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

        /// <summary>Settings > Gameplay > Camera: 0 back wall (the TV view), 1 high, 2 low glass.</summary>
        public static int Preset;
        public static readonly string[] PresetNames = { "TV", "High", "Low glass", "Wide" };
        // per preset: height, distance behind the back wall, field of view, look-at height, look-at depth (back wall = the config)
        // TV (2026-10-24, measured off a PSA broadcast): far back in the stands and up high on a long lens - the court
        // fills the frame, near and far players look nearly the same size, as on television. Wide is the old lab camera.
        static readonly float[,] Presets =
        {
            { 6.0f, 12.5f, 24f, 0.9f, 1.5f },      // TV
            { 8.2f, 4.5f, 50f, 0.7f, 4.4f },       // High
            { 1.7f, 3.4f, 60f, 1.5f, 2.2f },       // Low glass
            { 3.95f, 6.6f, 52f, 1.55f, 3.2f },     // Wide
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
        bool _wasCut;

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
            bool club = VenueView.Detail > 0 && Courts.Current.Id == "club" && p <= 1;
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
        }
    }
}
