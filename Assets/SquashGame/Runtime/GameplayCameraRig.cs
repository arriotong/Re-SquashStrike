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
        public static readonly string[] PresetNames = { "Back wall", "High", "Low glass" };
        // per preset: height, distance behind the back wall, field of view, look-at height, look-at depth (back wall = the config)
        static readonly float[,] Presets = { { 0f, 0f, 0f, 0f, 0f }, { 6.4f, 3.6f, 56f, 0.7f, 4.4f }, { 1.7f, 3.4f, 60f, 1.5f, 2.2f } };
        float _lateral;
        /// <summary>PR5: after a rally this long, the break cuts to a high side angle (the broadcast's "what a rally").</summary>
        public const int BigRallyShots = 15;
        public bool CutAway { get; private set; }
        float _cutSide = 1f;
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
            bool custom = p != 0 && !(Runner is MatchRunner mr0 && mr0.Demo);           // the menus' demo keeps its cinematics
            float fov = custom ? Presets[p, 2] : (float)cfg.Fov;
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

            double h = custom ? Presets[p, 0] : cfg.Height, dist = custom ? Presets[p, 1] : cfg.Distance;
            double lh = custom ? Presets[p, 3] : cfg.LookAtHeight, ld = custom ? Presets[p, 4] : cfg.LookAtDepth;
            transform.position = SimToWorld.Pos(_lateral, h, CourtDimensions.LENGTH + dist);
            transform.LookAt(SimToWorld.Pos(_lateral * 0.5, lh, ld), Vector3.up);
        }
    }
}
