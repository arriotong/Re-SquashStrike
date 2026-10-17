// ===========================================================
// Assets/SquashGame/Runtime/DebugOverlay.cs
// The warm-up court's developer tools (port of the lab's dev bindings,
// Physics Lab readouts and debug visualisation). Development aid only:
// normal play shows none of this (no aim cursor, no labels).
//
//   F1 / `   lab panel (frees the mouse while open)
//   F2 / \   debug view: trajectory, predicted contact, ideal spot,
//            aim target, face normal, contact markers, shot label
//   1 2 3 4  time scale 1 / 0.5 / 0.25 / 0.1      P  pause    .  single step
//   R        reset ball      Ctrl+R  reset full   F  feed     G  repeat feed
//   [ ]      previous / next preset               H  contact assist
//   F3       freeze on collision
// ===========================================================

using System.Collections.Generic;
using System.Text;
using SquashSim;
using SquashSim.Lab;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashGame
{
    public sealed class DebugOverlay : MonoBehaviour
    {
        public SimRunnerBase Runner;
        LabSimRunner Lab { get { return Runner as LabSimRunner; } }
        TestPlayerController Human { get { var a = Runner.Athletes; int h = Runner.HumanIndex; return a[h < a.Length ? h : 0]; } }
        public bool PanelVisible;
        public bool DebugView;   // off by default: normal play shows no aim target (lab invariant 11)

        GreyboxPalette _palette;
        LineRenderer _trail, _faceNormal, _aimRay;
        Transform _predMarker, _idealMarker, _aimMarker;
        readonly List<Transform> _contactMarkers = new List<Transform>();
        float _fps = 60f, _fpsAccum; int _fpsFrames;
        GUIStyle _box, _label, _small;

        public void Build(SimRunnerBase runner, GreyboxPalette palette)
        {
            Runner = runner;
            _palette = palette;
            _trail = MakeLine("Trail", new Color(1f, 0.85f, 0.3f), 0.012f);
            _faceNormal = MakeLine("FaceNormal", new Color(0.37f, 1f, 0.75f), 0.01f);
            _aimRay = MakeLine("AimRay", new Color(0.43f, 0.89f, 1f, 1f), 0.008f);
            _predMarker = Marker("PredictedContact", new Color(0.37f, 1f, 0.75f), 0.07f);
            _idealMarker = Marker("IdealSpot", new Color(1f, 0.6f, 0.2f), 0.10f);
            _aimMarker = Marker("AimTarget", new Color(0.43f, 0.89f, 1f), 0.09f);
            for (int i = 0; i < LabSimRunner.RecentEventMax; i++)
            {
                var m = Marker("Contact" + i, Color.white, 0.05f);
                m.gameObject.SetActive(false);
                _contactMarkers.Add(m);
            }
        }

        LineRenderer MakeLine(string name, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _palette.UnlitColor(c);
            lr.widthMultiplier = width;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;
            return lr;
        }

        Transform Marker(string name, Color c, float size)
        {
            var go = Greybox.Primitive(PrimitiveType.Sphere, name, transform, _palette.UnlitColor(c), false);
            go.transform.localScale = Vector3.one * size;
            return go.transform;
        }

        void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime; _fpsFrames++;
            if (_fpsAccum >= 0.4f) { _fps = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }
            if (Runner == null || Runner.Clock == null) return;

            HandleKeys();
            UpdateCursor();
            DrawDebug();
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            if (kb.f1Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame) PanelVisible = !PanelVisible;
            if (kb.f2Key.wasPressedThisFrame || kb.backslashKey.wasPressedThisFrame) DebugView = !DebugView;
            if (kb.f3Key.wasPressedThisFrame) Runner.FreezeOnCollision = !Runner.FreezeOnCollision;
            if (kb.digit1Key.wasPressedThisFrame) Runner.SetTimeScale(1.0);
            if (kb.digit2Key.wasPressedThisFrame) Runner.SetTimeScale(0.5);
            if (kb.digit3Key.wasPressedThisFrame) Runner.SetTimeScale(0.25);
            if (kb.digit4Key.wasPressedThisFrame) Runner.SetTimeScale(0.1);
            if (kb.pKey.wasPressedThisFrame) Runner.TogglePause();
            if (kb.periodKey.wasPressedThisFrame) Runner.StepOnce();
            var lab = Lab;
            if (lab != null)
            {
                if (kb.rKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                {
                    if (ctrl) lab.ResetFull(); else lab.ResetBall();
                }
                if (kb.fKey.wasPressedThisFrame) lab.FeedSingle();
                if (kb.gKey.wasPressedThisFrame) lab.ToggleRepeat();
                if (kb.rightBracketKey.wasPressedThisFrame) lab.CyclePreset(1);
                if (kb.leftBracketKey.wasPressedThisFrame) lab.CyclePreset(-1);
            }
            if (kb.hKey.wasPressedThisFrame) PhysicsConfig.Current.Assist.Enabled = !PhysicsConfig.Current.Assist.Enabled;
        }

        /// <summary>The mouse is a relative virtual stick, so the cursor is locked in play.
        /// The panel frees it; clicking the court captures it again (that click is not a stroke).</summary>
        void UpdateCursor()
        {
            var mouse = Mouse.current;
            var match = Runner as MatchRunner;
            bool wantFree = PanelVisible || Runner.Clock.Paused || (match != null && match.InSetup);
            if (wantFree)
            {
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Runner.Input.MouseGameplayEnabled = false;
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Runner.Input.MouseGameplayEnabled = false;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            Runner.Input.MouseGameplayEnabled = true;
        }

        void DrawDebug()
        {
            bool on = DebugView;
            var ctrl = Human;
            AthleteSnapshot hv = Runner.View.Athlete(Runner.HumanIndex);

            _trail.positionCount = on ? Runner.Trail.Count : 0;
            if (on)
            {
                int i = 0;
                foreach (var p in Runner.Trail) _trail.SetPosition(i++, p);
            }

            _predMarker.gameObject.SetActive(on && ctrl.Prediction.Valid);
            if (ctrl.Prediction.Valid) _predMarker.position = SimToWorld.Pos(ctrl.Prediction.Point);

            _idealMarker.gameObject.SetActive(on && ctrl.Assist.HasIdealSpot);
            if (ctrl.Assist.HasIdealSpot) _idealMarker.position = SimToWorld.Pos(ctrl.Assist.IdealSpot.X, 0.02, ctrl.Assist.IdealSpot.Z);

            // Aim target and desired direction: lab debug view only (aim.showDebugIntent).
            bool showAim = on && PhysicsConfig.Current.Aim.ShowDebugIntent;
            _aimMarker.gameObject.SetActive(showAim);
            _aimRay.positionCount = showAim ? 2 : 0;
            if (showAim)
            {
                Vector3 target = SimToWorld.Pos(ctrl.Aim.IntendedPoint);
                _aimMarker.position = target;
                Vector3 from = ctrl.Prediction.Valid ? SimToWorld.Pos(ctrl.Prediction.Point) : hv.RacketHead;
                _aimRay.SetPosition(0, from);
                _aimRay.SetPosition(1, target);
            }

            _faceNormal.positionCount = on ? 2 : 0;
            if (on)
            {
                _faceNormal.SetPosition(0, hv.RacketHead);
                _faceNormal.SetPosition(1, hv.RacketHead + hv.RacketFaceNormal * 0.35f);
            }

            var events = Runner.RecentEvents;
            for (int i = 0; i < _contactMarkers.Count; i++)
            {
                bool show = on && i < events.Count;
                _contactMarkers[i].gameObject.SetActive(show);
                if (!show) continue;
                var ev = events[i];
                _contactMarkers[i].position = SimToWorld.Pos(ev.Point);
                var col = ev.Type == SimEventType.RacketContact ? new Color(0.37f, 1f, 0.75f)
                    : ev.Type == SimEventType.BodyHit ? new Color(1f, 0.85f, 0.2f)
                    : ev.Nick ? new Color(1f, 0.2f, 0.9f)
                    : ev.SurfaceClass == "tin" ? new Color(1f, 0.25f, 0.25f) : Color.white;
                _contactMarkers[i].GetComponent<MeshRenderer>().sharedMaterial.color = col;
            }
        }

        // ---------------- panel ----------------

        void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            _box.normal.background = MakeTex(new Color(0.06f, 0.07f, 0.09f, 0.86f));
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            _label.normal.textColor = new Color(0.9f, 0.92f, 0.95f);
            _small = new GUIStyle(_label) { fontSize = 12 };
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (Runner == null || Runner.Clock == null) return;
            EnsureStyles();
            var clock = Runner.Clock;

            // Always-on one-liner, bottom left.
            string where = Lab != null ? "warm-up court  |  preset: " + Lab.CurrentPreset : "match";
            string status = string.Format("re:SquashStrike {0}  |  {1:0} fps  |  {2}x{3}  |  F1 lab panel  F2 debug view",
                where, _fps, clock.TimeScale, clock.Paused ? " PAUSED" : "");
            GUI.Label(new Rect(12, Screen.height - 26, Screen.width - 24, 22), status, _small);

            if (Cursor.lockState != CursorLockMode.Locked && !PanelVisible && !clock.Paused)
                GUI.Label(new Rect(12, Screen.height - 48, 600, 22), "<b>Click the court to capture the mouse (aim is a relative virtual stick).</b>", _small);

            if (DebugView && PhysicsConfig.Current.Aim.ShowDebugLabel)
            {
                var lc = Runner.LastRacketContact;
                string label = Human.Interpretation.Label;
                GUI.Label(new Rect(Screen.width / 2 - 200, 14, 400, 24), "<b>" + label + "</b>" +
                    (lc != null ? "   last: " + lc.Technique : ""), _label);
            }

            if (!PanelVisible) return;
            GUILayout.BeginArea(new Rect(12, 12, 380, Screen.height - 60), _box);
            GUILayout.Label(PanelText(), _label);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(clock.Paused ? "Resume" : "Pause")) Runner.TogglePause();
            if (GUILayout.Button("Step")) Runner.StepOnce();
            if (GUILayout.Button("1x")) Runner.SetTimeScale(1);
            if (GUILayout.Button("0.25x")) Runner.SetTimeScale(0.25);
            if (GUILayout.Button("0.1x")) Runner.SetTimeScale(0.1);
            GUILayout.EndHorizontal();

            var lab = Lab;
            if (lab != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Feed")) lab.FeedSingle();
                if (GUILayout.Button(lab.Sim.Launcher.IsRepeating ? "Repeat: ON" : "Repeat: off")) lab.ToggleRepeat();
                if (GUILayout.Button("Reset ball")) lab.ResetBall();
                if (GUILayout.Button("Reset all")) lab.ResetFull();
                GUILayout.EndHorizontal();

                GUILayout.Label("Preset", _small);
                int cols = 3, i = 0;
                GUILayout.BeginHorizontal();
                foreach (var p in TestPresets.All())
                {
                    if (i > 0 && i % cols == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                    string n = p.Id == lab.CurrentPreset ? "[" + p.Name + "]" : p.Name;
                    if (GUILayout.Button(n)) lab.SelectPreset(p.Id);
                    i++;
                }
                GUILayout.EndHorizontal();
            }

            var cfg = PhysicsConfig.Current;
            cfg.Assist.Enabled = GUILayout.Toggle(cfg.Assist.Enabled, " Contact assistance");
            cfg.MovementAssist.Enabled = GUILayout.Toggle(cfg.MovementAssist.Enabled, " Movement assistance");
            Runner.FreezeOnCollision = GUILayout.Toggle(Runner.FreezeOnCollision, " Freeze on collision");
            Runner.Interpolate = GUILayout.Toggle(Runner.Interpolate, " Render interpolation");
            DebugView = GUILayout.Toggle(DebugView, " Debug view (F2)");
            GUILayout.EndArea();
        }

        string PanelText()
        {
            var ball = Runner.Ball;
            var ctrl = Human;
            var clock = Runner.Clock;
            var sb = new StringBuilder();
            sb.AppendFormat("<b>WARM-UP COURT</b>   {0:0} fps   steps/frame {1}{2}\n", _fps, clock.StepsLastFrame, clock.Clamped ? " (clamped)" : "");
            sb.AppendFormat("sim t {0:0.00}s   step {1}   {2}x{3}\n\n", clock.SimulatedTime, clock.TotalSteps, clock.TimeScale, clock.Paused ? "  PAUSED" : "");

            sb.AppendFormat("<b>Ball</b>  {0:0.0} m/s   spin {1:0} rad/s\n", ball.Speed(), ball.SpinRate());
            sb.AppendFormat("temp {0:0.0}  ({1})\n", ball.Temperature, BallTemperature.StateName(ball.Temperature));
            var li = Runner.LastImpact;
            if (li != null)
                sb.AppendFormat("last impact {0}  {1:0.0} m/s  e={2:0.00}{3}\n", li.SurfaceId, li.ImpactSpeed, li.Restitution, li.Nick ? "  NICK" : "");
            sb.Append('\n');

            var p = ctrl.Player;
            sb.AppendFormat("<b>Athlete</b>  {0}   reach {1}\n", p.MovementState, p.ReachState);
            sb.AppendFormat("balance {0:0.00}   {1}   speed {2:0.0}\n", p.Balance, PlayerFacing.SwingSideName(p), p.Speed);
            sb.AppendFormat("swing {0}  {1} {2}   prep {3:0.00}\n", ctrl.Swing.State, ctrl.Swing.Stroke, ctrl.Swing.Mode ?? "", ctrl.Swing.PrepQuality);
            sb.AppendFormat("commit {0}   body {1:0}°  face {2:0}°\n", ctrl.IntentSolver.Phase,
                ctrl.IntentSolver.BodyYaw * Mathf.Rad2Deg, ctrl.IntentSolver.FaceYaw * Mathf.Rad2Deg);
            sb.AppendFormat("aim bias ({0:0.00}, {1:0.00})   device {2}\n\n", ctrl.Aim.BiasX, ctrl.Aim.BiasY, Runner.Input.Device);

            var match = Runner as MatchRunner;
            if (match != null)
            {
                var it = match.Sim.Interference;
                sb.AppendFormat("<b>Interference</b>  striker {0}   now {1}   this turn {2}\n\n",
                    it.Striker < 0 ? "-" : "P" + (it.Striker + 1), it.Current, it.ThisTurn);
            }

            var c = Runner.LastRacketContact;
            if (c != null)
            {
                sb.AppendFormat("<b>Last contact</b>  {0}\n", c.Technique);
                sb.AppendFormat("head {0:0.0} m/s -> ball {1:0.0} m/s\n", c.HeadSpeed, c.BallSpeedOut);
                sb.AppendFormat("off-centre {0:0}%   assist {1:0.00} m{2}\n", c.OffCentreNorm * 100, c.AssistApplied, c.Rushed ? "   RUSHED" : "");
                if (c.Deception != null)
                    sb.AppendFormat("aim change {0:0}°   shortfall {1:0}°   late by {2:0.00}s\n",
                        c.Deception.AimChangeDeg, c.Deception.ShortfallDeg, c.Deception.LateBySeconds);
                if (Runner.LastFirstWall != null) sb.AppendFormat("first wall: {0}\n", Runner.LastFirstWall);
            }
            return sb.ToString();
        }
    }
}
