// ===========================================================
// Assets/SquashGame/Runtime/Views/SpecialFx.cs
// 2026-11-22 the Special Shot's show (the user picked them all):
//   aura      while a player aims (in the air above the T) and winds up:
//             gold rings turning round them, sparks rising
//   spark     the 0.5 s wind-up: a crackling star at the ball, so the
//             opponent knows it's coming
//   warning   during the wind-up: a red target on the front wall where
//             it's aimed - the opponent's fair warning
//   lightning the Special Shot in flight: a jagged electric trail
//   impact    where it hits the front wall: a big flash ring and a hard
//             camera shake
// Views only: it reads the sim (MatchSimulation.Special) and draws.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class SpecialFx : MonoBehaviour
    {
        MatchRunner _runner;
        Material _gold, _white, _red, _cyan;
        readonly LineRenderer[] _aura = new LineRenderer[3];
        readonly LineRenderer[] _spark = new LineRenderer[6];
        LineRenderer _target, _cross1, _cross2, _trail, _wallRing;
        readonly List<Vector3> _trailPts = new List<Vector3>();
        float _t, _wallT = 99f;
        Vector3 _wallAt;
        int _wallFor = -1, _shotSeen;
        /// <summary>What shows now (tests): the aura, the wind-up spark, the target warning, the lightning trail.</summary>
        public bool AuraShown { get; private set; }
        public bool SparkShown { get; private set; }
        public bool TargetShown { get; private set; }
        public bool TrailShown { get; private set; }
        public int WallFlashes { get; private set; }

        LineRenderer Line(string name, Material m, int points, bool loop, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m; lr.positionCount = points; lr.loop = loop; lr.widthMultiplier = width; lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false;
            return lr;
        }

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            _runner = runner as MatchRunner;
            _gold = palette.UnlitColor(new Color(1f, 0.84f, 0.25f));
            _white = palette.UnlitColor(new Color(1f, 1f, 0.9f));
            _red = palette.UnlitColor(new Color(1f, 0.2f, 0.2f));
            _cyan = palette.UnlitColor(new Color(0.55f, 0.95f, 1f));
            for (int i = 0; i < _aura.Length; i++) _aura[i] = Line("Aura" + i, _gold, 40, true, 0.045f);
            for (int i = 0; i < _spark.Length; i++) _spark[i] = Line("Spark" + i, i % 2 == 0 ? _white : _gold, 4, false, 0.03f);
            _target = Line("Target", _red, 40, true, 0.06f);
            _cross1 = Line("TargetX1", _red, 2, false, 0.05f);
            _cross2 = Line("TargetX2", _red, 2, false, 0.05f);
            _trail = Line("Lightning", _cyan, 24, false, 0.07f);
            _wallRing = Line("WallFlash", _white, 48, true, 0.12f);
            if (_runner != null) _runner.Stepped += OnStepped;
        }

        void OnDestroy() { if (_runner != null) _runner.Stepped -= OnStepped; }

        /// <summary>The Special Shot meeting the front wall: the flash and the shake.</summary>
        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            var m = _runner != null ? _runner.Sim : null;
            if (m == null || m.SpecialBy < 0) return;
            if (_wallFor == m.SpecialsFired) return;                              // one flash a shot
            foreach (var e in events)
            {
                if (e.Type != SimEventType.Impact || e.SurfaceClass != "frontWall") continue;
                _wallFor = m.SpecialsFired;
                _wallAt = SimToWorld.Pos(e.Point.X, e.Point.Y, 0.03);
                _wallT = 0f; WallFlashes++;
                GameplayCameraRig.Kick(0.3f);
                UI.Juice.Rumble(0.8f, 0.8f, 0.25f);
                break;
            }
        }

        void Ring(LineRenderer lr, Vector3 c, float r, bool flat, float phase = 0f)
        {
            int n = lr.positionCount;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f + phase;
                lr.SetPosition(k, flat ? c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r) : c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _t += dt;
            var m = _runner != null && !_runner.Demo ? _runner.Sim : null;
            bool live = m != null && !_runner.ReplayActive;

            // ---- the aura: aiming or winding up ----
            int who = live ? (m.ZoneBy >= 0 ? m.ZoneBy : m.WindupBy) : -1;
            AuraShown = who >= 0;
            if (AuraShown && Camera.main != null)
            {
                // (seen from inside - the first-person aim: the rings would cross the view, so the striker doesn't see their own)
                var ap = SimToWorld.Pos(m.Players[who].Player.Pos);
                var cp = Camera.main.transform.position;
                if (new Vector2(cp.x - ap.x, cp.z - ap.z).magnitude < 1.2f) AuraShown = false;
            }
            for (int i = 0; i < _aura.Length; i++)
            {
                _aura[i].enabled = AuraShown;
                if (!AuraShown) continue;
                var p = m.Players[who].Player.Pos;
                float h = 0.25f + i * 0.7f + 0.12f * Mathf.Sin(_t * 3f + i);
                float r = 0.55f + 0.08f * Mathf.Sin(_t * 5f + i * 2f);
                Ring(_aura[i], SimToWorld.Pos(p.X, p.Y + h, p.Z), r, true, _t * (i % 2 == 0 ? 2f : -2f));
            }

            // ---- the wind-up: the spark at the ball, the target on the front wall ----
            SparkShown = live && m.WindupBy >= 0;
            var ball = live ? SimToWorld.Pos(m.Ball.Pos) : Vector3.zero;
            for (int i = 0; i < _spark.Length; i++)
            {
                _spark[i].enabled = SparkShown;
                if (!SparkShown) continue;
                float a = (i / (float)_spark.Length) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f), len = Random.Range(0.25f, 0.6f);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), Random.Range(-0.4f, 0.4f)).normalized;
                Vector3 mid = ball + dir * len * 0.5f + Random.insideUnitSphere * 0.08f;
                _spark[i].SetPosition(0, ball); _spark[i].SetPosition(1, mid); _spark[i].SetPosition(2, mid + dir * len * 0.3f + Random.insideUnitSphere * 0.06f); _spark[i].SetPosition(3, ball + dir * len);
            }
            TargetShown = live && m.WindupBy >= 0;
            _target.enabled = _cross1.enabled = _cross2.enabled = TargetShown;
            if (TargetShown)
            {
                Vector3 c = SimToWorld.Pos(m.SpecialAimX, m.SpecialAimY, 0.04);
                float pulse = 0.28f + 0.07f * Mathf.Sin(_t * 18f);
                Ring(_target, c, pulse, false);
                _cross1.SetPosition(0, c + new Vector3(-0.4f, 0, 0)); _cross1.SetPosition(1, c + new Vector3(0.4f, 0, 0));
                _cross2.SetPosition(0, c + new Vector3(0, -0.4f, 0)); _cross2.SetPosition(1, c + new Vector3(0, 0.4f, 0));
            }

            // ---- the lightning trail: a Special Shot in flight ----
            TrailShown = live && m.SpecialBy >= 0 && m.Phase == MatchPhase.Rally && !m.Ball.Asleep;
            if (m == null || m.SpecialsFired != _shotSeen) { _trailPts.Clear(); _shotSeen = m != null ? m.SpecialsFired : 0; }
            if (TrailShown)
            {
                _trailPts.Add(ball);
                while (_trailPts.Count > _trail.positionCount) _trailPts.RemoveAt(0);
                _trail.enabled = _trailPts.Count >= 2;
                if (_trail.enabled)
                {
                    _trail.positionCount = Mathf.Max(2, _trailPts.Count);
                    for (int k = 0; k < _trailPts.Count; k++)
                    {
                        float jag = k == _trailPts.Count - 1 ? 0f : 0.09f;
                        _trail.SetPosition(k, _trailPts[k] + Random.insideUnitSphere * jag);
                    }
                    _trail.positionCount = _trailPts.Count;
                }
            }
            else { _trail.enabled = false; if (!live || m.SpecialBy < 0) _trailPts.Clear(); _trail.positionCount = 24; }

            // ---- the front wall's flash ----
            if (_wallT < 0.6f)
            {
                _wallT += dt;
                float u = Mathf.Clamp01(_wallT / 0.6f);
                _wallRing.enabled = true;
                _wallRing.widthMultiplier = Mathf.Lerp(0.18f, 0f, u);
                Ring(_wallRing, _wallAt, Mathf.Lerp(0.1f, 1.8f, 1f - (1f - u) * (1f - u)), false);
            }
            else _wallRing.enabled = false;
        }
    }
}
