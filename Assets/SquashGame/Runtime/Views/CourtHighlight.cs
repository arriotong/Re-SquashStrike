// ===========================================================
// Assets/SquashGame/Runtime/Views/CourtHighlight.cs
// P2-M3: the Academy's rules cards light up the part of the court
// they explain, in a soft pulsing yellow over the court's own lines:
//
//   tin          the tin board          outlines     the out lines (front, sides, back)
//   floor        short + half-court lines and the boxes
//   boxes        the two service boxes  serviceline  the front-wall service line
//   quarters     the two back quarters
//
// A quiz: a ring where the deciding bounce was, kept up while you make
// the call (the quiz screens hold the game).
//
// Presentation only - reads the lesson's current step.
// ===========================================================

using System.Collections.Generic;
using UnityEngine;
using static SquashSim.CourtDimensions;

namespace SquashGame
{
    public sealed class CourtHighlight : MonoBehaviour
    {
        public AcademyRunner Runner;
        readonly Dictionary<string, GameObject> _groups = new Dictionary<string, GameObject>();
        Material _bar, _zone;
        float _t;
        Transform _mark;
        bool _marked;

        /// <summary>The quiz's mark is up (tests).</summary>
        public bool Marking { get { return _mark != null && _mark.gameObject.activeSelf; } }

        /// <summary>The highlight showing now, or null (tests).</summary>
        public string Showing { get; private set; }

        public static readonly string[] Ids = { "tin", "outlines", "floor", "boxes", "serviceline", "quarters" };
        static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        public void Build(AcademyRunner runner, GreyboxPalette palette)
        {
            Runner = runner;
            runner.Judged += OnLesson;
            _bar = palette.Clear(new Color(Gold.r, Gold.g, Gold.b, 0.9f));
            _zone = palette.Clear(new Color(Gold.r, Gold.g, Gold.b, 0.3f));
            float W = (float)WIDTH, L = (float)LENGTH, back = (float)(LENGTH - SHORT_LINE_Z);
            const float bar = 0.09f, y = 0.008f;

            var g = Group("tin");
            Greybox.Box("Tin", g, _zone, SimToWorld.Pos(0, TIN_H / 2, 0.05), new Vector3(W, (float)TIN_H, 0.01f), false);
            Greybox.Box("TinLine", g, _bar, SimToWorld.Pos(0, TIN_H, 0.05), new Vector3(W, bar, 0.01f), false);

            g = Group("outlines");
            Greybox.Box("Front", g, _bar, SimToWorld.Pos(0, FRONT_WALL_OUT_H, 0.012), new Vector3(W, bar, 0.01f), false);
            Greybox.Box("Back", g, _bar, SimToWorld.Pos(0, BACK_WALL_OUT_H, LENGTH - 0.02), new Vector3(W, bar, 0.01f), false);
            foreach (int side in new[] { -1, 1 })
            {
                var s = Greybox.Primitive(PrimitiveType.Cube, "Side", g, _bar, false);
                double x = side * (HALF_W - 0.015);
                Greybox.PlaceBar(s.transform, SimToWorld.Pos(x, FRONT_WALL_OUT_H, 0), SimToWorld.Pos(x, BACK_WALL_OUT_H, LENGTH), 0.012f, bar);
            }

            g = Group("floor");
            Greybox.Box("Short", g, _bar, SimToWorld.Pos(0, y, SHORT_LINE_Z), new Vector3(W, 0.004f, bar), false);
            Greybox.Box("Half", g, _bar, SimToWorld.Pos(0, y, SHORT_LINE_Z + back / 2), new Vector3(bar, 0.004f, back), false);
            foreach (int side in new[] { -1, 1 })
            {
                Greybox.Box("BoxInner", g, _bar, SimToWorld.Pos(side * (HALF_W - SERVICE_BOX), y, SHORT_LINE_Z + SERVICE_BOX / 2), new Vector3(bar, 0.004f, (float)SERVICE_BOX), false);
                Greybox.Box("BoxBack", g, _bar, SimToWorld.Pos(side * (HALF_W - SERVICE_BOX / 2), y, SHORT_LINE_Z + SERVICE_BOX), new Vector3((float)SERVICE_BOX, 0.004f, bar), false);
            }

            g = Group("boxes");
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("Box", g, _zone, SimToWorld.Pos(side * (HALF_W - SERVICE_BOX / 2), y, SHORT_LINE_Z + SERVICE_BOX / 2),
                    new Vector3((float)SERVICE_BOX, 0.004f, (float)SERVICE_BOX), false);

            _mark = Greybox.MeshObject("CallMark", transform, Greybox.Torus(1f, 0.12f, 48, 6), _bar).transform;
            _mark.gameObject.SetActive(false);

            g = Group("serviceline");
            Greybox.Box("ServiceLine", g, _bar, SimToWorld.Pos(0, SERVICE_LINE_H, 0.012), new Vector3(W, bar, 0.01f), false);

            g = Group("quarters");
            foreach (int side in new[] { -1, 1 })
                Greybox.Box("Quarter", g, _zone, SimToWorld.Pos(side * HALF_W / 2, y, SHORT_LINE_Z + back / 2),
                    new Vector3((float)HALF_W, 0.004f, back), false);
        }

        void OnDestroy() { if (Runner != null) Runner.Judged -= OnLesson; }

        void OnLesson(SquashSim.Academy.AcademyEvent e)
        {
            if (e.Kind != SquashSim.Academy.AcademyEventKind.QuizAsked || e.Point == null) return;
            bool wall = e.Point.Z < 0.05;
            _mark.position = SimToWorld.Pos(e.Point.X, System.Math.Max(0.012, e.Point.Y), wall ? 0.065 : e.Point.Z);
            _mark.rotation = wall ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
            _mark.localScale = new Vector3(0.16f, 0.16f, 1f);
            _marked = true;
        }

        Transform Group(string id)
        {
            var go = new GameObject("Highlight_" + id);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _groups[id] = go;
            return go.transform;
        }

        void LateUpdate()
        {
            var s = Runner != null ? Runner.Session : null;
            if (_marked && s != null && !s.AwaitingAnswer && !Runner.Clock.Paused) _marked = false;
            _mark.gameObject.SetActive(_marked);
            string want = s != null && !s.Done && s.Current != null ? s.Current.Highlight : null;
            if (want != Showing)
            {
                foreach (var kv in _groups) kv.Value.SetActive(kv.Key == want);
                Showing = want != null && _groups.ContainsKey(want) ? want : null;
                _t = 0f;
            }
            _t += Time.unscaledDeltaTime;
            if (Showing == null && !_marked) return;
            float pulse = 0.65f + 0.35f * Mathf.Cos(_t * 4f);
            _bar.color = new Color(Gold.r, Gold.g, Gold.b, 0.95f * pulse);
            _zone.color = new Color(Gold.r, Gold.g, Gold.b, 0.32f * pulse);
        }
    }
}
