// ===========================================================
// Assets/SquashGame/Runtime/Views/ImpactBurst.cs
// 2026-11-21 the polish pass ("big-shot hit feel"): where a winner, a
// nick or a kill dies, rings burst out across the floor (two, one after
// the other) and shrink away. On real time, so it plays through the
// freeze-frame. MatchRunner.BigHit tells it where.
// ===========================================================

using UnityEngine;

namespace SquashGame
{
    public sealed class ImpactBurst : MonoBehaviour
    {
        const int Segments = 48, Rings = 2;
        const float Seconds = 0.55f, Lift = 0.03f;
        readonly LineRenderer[] _rings = new LineRenderer[Rings];
        Material[] _mats;
        MatchRunner _runner;
        float _t = 99f, _size = 1f;
        Vector3 _at;
        /// <summary>The bursts so far (tests).</summary>
        public int Bursts { get; private set; }

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner as MatchRunner;
            _mats = new[] { palette.UnlitColor(new Color(0.78f, 1f, 0.24f)), palette.UnlitColor(new Color(1f, 0.84f, 0.3f)), palette.UnlitColor(new Color(1f, 1f, 1f)) };
            for (int i = 0; i < Rings; i++)
            {
                var go = new GameObject("Ring" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = Segments;
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _rings[i] = lr;
            }
            if (_runner != null) _runner.BigHit += Fire;
        }

        void OnDestroy() { if (_runner != null) _runner.BigHit -= Fire; }

        /// <summary>A burst at this world point: kind 0 a winner, 1 a nick (gold), 2 a kill (white, bigger).</summary>
        public void Fire(Vector3 at, int kind)
        {
            _at = new Vector3(at.x, Mathf.Max(at.y, 0f) + Lift, at.z);
            _at.y = Lift;                                                   // the rings lie on the floor
            _t = 0f;
            _size = kind == 2 ? 1.35f : 1f;
            var m = _mats[Mathf.Clamp(kind, 0, _mats.Length - 1)];
            foreach (var r in _rings) { r.sharedMaterial = m; r.enabled = true; }
            Bursts++;
        }

        void LateUpdate()
        {
            if (_t > Seconds + 0.2f) return;
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < Rings; i++)
            {
                float u = Mathf.Clamp01((_t - i * 0.12f) / Seconds);
                var lr = _rings[i];
                if (u <= 0f || u >= 1f) { lr.enabled = u <= 0f && _t < Seconds; if (u >= 1f) lr.enabled = false; continue; }
                lr.enabled = true;
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                float radius = Mathf.Lerp(0.08f, 1.3f * _size, e);
                lr.widthMultiplier = Mathf.Lerp(0.09f, 0.0f, u) * _size;
                for (int k = 0; k < Segments; k++)
                {
                    float a = k / (float)Segments * Mathf.PI * 2f;
                    lr.SetPosition(k, _at + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                }
            }
        }
    }
}
