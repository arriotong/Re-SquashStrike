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
        float _lateral;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 120f;
        }

        void LateUpdate()
        {
            var cfg = PhysicsConfig.Current.Camera;

            // Referee replay: high above the court, looking down on the incident.
            if (Runner != null && Runner.ReplayActive)
            {
                _cam.fieldOfView = 55f;
                Vector3 focus = Runner.ReplayFocus;
                transform.position = SimToWorld.Pos(0, 8.5, 8.5);
                transform.LookAt(new Vector3(focus.x * 0.6f, 0.6f, focus.z), Vector3.up);
                return;
            }

            // Projection: hold the horizontal field on narrow windows.
            float aspect = _cam.aspect;
            float fov = (float)cfg.Fov;
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

            transform.position = SimToWorld.Pos(_lateral, cfg.Height, CourtDimensions.LENGTH + cfg.Distance);
            transform.LookAt(SimToWorld.Pos(_lateral * 0.5, cfg.LookAtHeight, cfg.LookAtDepth), Vector3.up);
        }
    }
}
