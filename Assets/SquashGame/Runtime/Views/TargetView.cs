// ===========================================================
// Assets/SquashGame/Runtime/Views/TargetView.cs
// P2-M2: the Target Arcade's targets, in the plain style of the aim
// ring - white, a little see-through:
//
//   front-wall rings   a ring on the wall (its radius = the target's)
//   floor zones        a soft white panel on the floor
//   spots (ghosting)   a ring on the floor
//   nick               a strip along the foot of the front wall
//
// A hit: the target pulses out and fades. A miss: a small dot where
// the ball went, fading. Presentation only - reads the session.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using SquashSim.Arcade;
using UnityEngine;

namespace SquashGame
{
    public sealed class TargetView : MonoBehaviour
    {
        public ArcadeRunner Runner;
        Material _mat, _zoneMat, _flashMat, _missMat;
        Mesh _torus, _disc;
        readonly List<GameObject> _shown = new List<GameObject>();
        readonly List<Target> _for = new List<Target>();
        Transform _flash, _miss;
        float _flashAge = 99f, _missAge = 99f;
        float _flashR = 0.4f;
        public const float FlashSeconds = 0.45f, MissSeconds = 1.0f;

        /// <summary>Hits and misses shown so far (tests).</summary>
        public int HitsShown { get; private set; }
        public int MissesShown { get; private set; }
        /// <summary>How many target shapes are up now (tests).</summary>
        public int Showing { get { return _shown.Count; } }

        public void Build(ArcadeRunner runner, GreyboxPalette palette)
        {
            Runner = runner;
            _mat = palette.Clear(new Color(1f, 1f, 1f, 0.85f));
            _zoneMat = palette.Clear(new Color(1f, 1f, 1f, 0.16f));
            _flashMat = palette.Clear(new Color(1f, 1f, 1f, 0f));
            _missMat = palette.Clear(new Color(0.75f, 0.78f, 0.82f, 0f));
            _torus = Greybox.Torus(1f, 0.07f, 64, 6);
            _disc = Greybox.Torus(1f, 0.6f, 20, 8);          // a fat little ring reads as a dot from any side
            _flash = Greybox.MeshObject("HitFlash", transform, _torus, _flashMat).transform;
            _flash.gameObject.SetActive(false);
            _miss = Greybox.MeshObject("Miss", transform, _disc, _missMat).transform;
            _miss.gameObject.SetActive(false);
            Runner.Judged += OnJudged;
        }

        void OnDestroy() { if (Runner != null) Runner.Judged -= OnJudged; }

        void OnJudged(ArcadeEvent e)
        {
            if (e.Kind == ArcadeEventKind.Hit)
            {
                HitsShown++;
                var t = e.Target;
                _flashAge = 0f;
                if (t != null && t.Kind == TargetKind.FrontRing) { Place(_flash, t, out _flashR); }
                else if (e.Point != null)
                {
                    _flash.position = SimToWorld.Pos(e.Point.X, System.Math.Max(0.02, e.Point.Y), System.Math.Max(0.02, e.Point.Z));
                    _flash.rotation = e.Point.Z < 0.05 ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                    _flashR = 0.35f;
                }
            }
            else if ((e.Kind == ArcadeEventKind.Miss || e.Kind == ArcadeEventKind.Fault) && e.Point != null)
            {
                MissesShown++;
                _missAge = 0f;
                bool wall = e.Point.Z < 0.05;
                _miss.position = SimToWorld.Pos(e.Point.X, System.Math.Max(0.015, e.Point.Y), wall ? 0.02 : e.Point.Z);
                _miss.rotation = wall ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                _miss.localScale = new Vector3(0.07f, 0.07f, 1f);
            }
        }

        void Place(Transform tr, Target t, out float r)
        {
            tr.position = SimToWorld.Pos(t.X, t.Y, 0.018);
            tr.rotation = Quaternion.identity;
            r = (float)t.R;
        }

        void LateUpdate()
        {
            if (Runner == null || Runner.Session == null) return;
            var targets = Runner.Session.Targets;
            if (!Same(targets)) Rebuild(targets);

            float dt = Time.unscaledDeltaTime;
            _flashAge += dt; _missAge += dt;
            bool flashOn = _flashAge < FlashSeconds;
            _flash.gameObject.SetActive(flashOn);
            if (flashOn)
            {
                float u = _flashAge / FlashSeconds;
                float r = _flashR * (1f + 0.8f * u);
                _flash.localScale = new Vector3(r, r, 1f);
                _flashMat.color = new Color(1f, 1f, 1f, 0.9f * (1f - u));
            }
            bool missOn = _missAge < MissSeconds;
            _miss.gameObject.SetActive(missOn);
            if (missOn) _missMat.color = new Color(0.75f, 0.78f, 0.82f, 0.8f * (1f - _missAge / MissSeconds));
        }

        bool Same(List<Target> targets)
        {
            if (targets.Count != _for.Count) return false;
            for (int i = 0; i < targets.Count; i++) if (!ReferenceEquals(targets[i], _for[i])) return false;
            return true;
        }

        void Rebuild(List<Target> targets)
        {
            foreach (var g in _shown) Destroy(g);
            _shown.Clear(); _for.Clear();
            foreach (var t in targets)
            {
                _for.Add(t);
                switch (t.Kind)
                {
                    case TargetKind.FrontRing:
                    {
                        var g = Greybox.MeshObject("Ring", transform, _torus, _mat);
                        Place(g.transform, t, out float r);
                        g.transform.localScale = new Vector3(r, r, 1f);
                        _shown.Add(g);
                        break;
                    }
                    case TargetKind.FloorZone:
                    {
                        Vector3 a = SimToWorld.Pos(t.X0, 0, t.Z0), b = SimToWorld.Pos(t.X1, 0, t.Z1);
                        var centre = (a + b) * 0.5f; centre.y = 0.006f;
                        var size = new Vector3(Mathf.Abs(a.x - b.x), 0.004f, Mathf.Abs(a.z - b.z));
                        _shown.Add(Greybox.Box("Zone", transform, _zoneMat, centre, size, false));
                        break;
                    }
                    case TargetKind.Spot:
                    {
                        var g = Greybox.MeshObject("Spot", transform, _torus, _mat);
                        g.transform.position = SimToWorld.Pos(t.X, 0.02, t.Z0);
                        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        g.transform.localScale = new Vector3((float)t.R, (float)t.R, 1f);
                        _shown.Add(g);
                        break;
                    }
                    case TargetKind.Nick:
                        _shown.Add(Greybox.Box("Nick", transform, _zoneMat, SimToWorld.Pos(0, 0.04, 0.03),
                            new Vector3((float)(CourtDimensions.HALF_W * 2), 0.08f, 0.04f), false));
                        break;
                }
            }
        }
    }
}
