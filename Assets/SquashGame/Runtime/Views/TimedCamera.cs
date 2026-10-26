// ===========================================================
// Assets/SquashGame/Runtime/Views/TimedCamera.cs
// Batch 1 (2026-10-26): a camera drawn a few times a second instead of
// every frame (the arena's live feed into its screens), by hand - like
// the menu cards' pictures (Vignettes). Spread over frames, not bunched.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class TimedCamera : MonoBehaviour
    {
        public float Fps = 12f;
        Camera _cam;
        float _since;

        void Awake() { _cam = GetComponent<Camera>(); _cam.enabled = false; _since = Random.value / Fps; }

        void LateUpdate()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < 1f / Fps) return;
            _since = 0;
            _cam.Render();
        }
    }
}
