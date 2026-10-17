// ===========================================================
// Assets/SquashGame/Runtime/Views/TimingFeedbackView.cs
// M6: after each stroke, a small note by the aim target says how it was
// timed, so the timing can be learned:
//
//   GOOD    the ball met the strings within +-7% of the swing's ideal
//           contact moment (half way through the swing)
//   EARLY   the swing was already past its ideal point (you swung early)
//   LATE    the swing had not got there yet (you swung late)
//   MISS    the swing ended without touching the ball
//
// Presentation only: reads the contact event's swing phase.
// ===========================================================

using System.Collections.Generic;
using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class TimingFeedbackView : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public AimTargetView Target;
        public int Athlete;
        public const double GoodWindow = 0.07;

        /// <summary>The last note shown (tests and diagnostics).</summary>
        public string Last { get; private set; }
        public int Count { get; private set; }

        string _text;
        Color _colour;
        float _age = 99f;
        bool _contactThisSwing;
        GUIStyle _style;

        public void Build(SimRunnerBase runner, AimTargetView target, int athlete)
        {
            Runner = runner; Target = target; Athlete = athlete;
            Runner.Stepped += OnStepped;
            Runner.StrokeEnded += OnStrokeEnded;
        }

        void OnDestroy()
        {
            if (Runner == null) return;
            Runner.Stepped -= OnStepped;
            Runner.StrokeEnded -= OnStrokeEnded;
        }

        public static string Judge(double phaseAtContact, out Color colour)
        {
            double off = phaseAtContact - 0.5;
            if (System.Math.Abs(off) <= GoodWindow) { colour = new Color(0.45f, 0.95f, 0.5f); return "GOOD"; }
            colour = new Color(1f, 0.75f, 0.3f);
            return off > 0 ? "EARLY" : "LATE";
        }

        void Show(string text, Color c) { _text = text; _colour = c; _age = 0f; Last = text; Count++; }

        void OnStepped(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Type != SimEventType.RacketContact || e.SwingPhaseAtContact < 0) continue;
                if (e.PlayerIndex != Athlete && !(e.PlayerIndex < 0 && Athlete == 0)) continue;
                _contactThisSwing = true;
                string t = Judge(e.SwingPhaseAtContact, out Color c);
                Show(t, c);
            }
        }

        void OnStrokeEnded(int athlete, bool contacted)
        {
            if (athlete != Athlete) return;
            if (!contacted && !_contactThisSwing) Show("MISS", new Color(0.95f, 0.35f, 0.3f));
            _contactThisSwing = false;
        }

        void Update() { _age += Time.unscaledDeltaTime; }

        void OnGUI()
        {
            if (_text == null || _age > 1.1f || Target == null || !Target.Shown) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            Vector3 sp = cam.WorldToScreenPoint(Target.WorldPoint + Vector3.up * 0.55f);
            if (sp.z <= 0) return;
            float a = Mathf.Clamp01(1.4f - _age * 1.2f);
            var shadow = new Color(0, 0, 0, 0.6f * a);
            var rect = new Rect(sp.x - 80, Screen.height - sp.y - 16, 160, 32);
            _style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), _text, _style);
            var c = _colour; c.a = a;
            _style.normal.textColor = c;
            GUI.Label(rect, _text, _style);
        }
    }
}
