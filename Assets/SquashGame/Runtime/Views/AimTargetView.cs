// ===========================================================
// Assets/SquashGame/Runtime/Views/AimTargetView.cs
// The original SquashStrike reticle: a small ring that sits exactly where
// you aim (front wall, or a side wall for a boast), always on in play,
// coloured by the shot its height plays: DRIVE green, DROP blue, LOB
// orange, KILL red (a low aim, charged). Older notes, M6:
// the always-on aim target. A ring on the front wall where the
// local player's current aim would send the ball (ShotPreview: the swing's
// own launch maths, flown through the real ball physics). The ring's size
// is the expected spread: tight on a set, prepared stroke; wide when quick,
// stretched or volleyed. Dim when no contact is predicted yet (the ball
// is on its way to the opponent): then it is the aim's straight line.
// Red when the shot would go down (tin / floor). For a boast (aim pushed
// past the edge of the front wall) the ring sits on that side wall, at the
// point the shot is solved to strike first, tagged BOAST.
//
// One per local player (two colours in local 2P). Presentation only.
// ===========================================================

using SquashSim;
using UnityEngine;

namespace SquashGame
{
    public sealed class AimTargetView : MonoBehaviour
    {
        public SimRunnerBase Runner;
        public int Athlete;
        public Color Colour = new Color(1f, 0.78f, 0.22f);
        public bool DrawRing = true;
        /// <summary>Words by the ring (shot type, BOAST, FAULT). Off: a plain ring (playtest 2026-10-18).</summary>
        public static bool ShowWords = false;
        /// <summary>The plain ring is about 1.5x the drawn ball across (playtest 2026-10-18).</summary>
        public static float CircleRadius
        {
            get
            {
                var cfg = PhysicsConfig.Current;
                float ballDiameter = 2f * (float)(cfg.Ball.Radius * cfg.Render.BallVisualScale);
                return 0.75f * ballDiameter;
            }
        }
        /// <summary>Plain white, a little see-through (in local 2P the second player's ring is a soft grey-blue).</summary>
        public static readonly Color Plain = new Color(1f, 1f, 1f, 0.85f);
        /// <summary>0..1 how visible the reticle is this frame (tests).</summary>
        public float Fade { get; private set; }
        /// <summary>Boast armed: the shot goes off <see cref="SideWall"/> (-1 / +1) near <see cref="SideMarker"/> (world).</summary>
        public bool Boasting { get; private set; }
        public int SideWall { get; private set; }
        public Vector3 SideMarker { get; private set; }
        Transform _side;
        /// <summary>The shot the reticle would play now: drive / drop / lob / kill.</summary>
        public string ShotType { get; private set; } = "drive";

        public static readonly Color DriveColour = new Color(0.78f, 1f, 0.24f);
        public static readonly Color DropColour = new Color(0.23f, 0.64f, 1f);
        public static readonly Color LobColour = new Color(1f, 0.72f, 0.23f);
        public static readonly Color KillColour = new Color(1f, 0.29f, 0.25f);
        public static Color ColourFor(string shot)
        {
            switch (shot)
            {
                case "drop": return DropColour; case "lob": case "lob serve": return LobColour;
                case "kill": case "hard serve": return KillColour; default: return DriveColour;
            }
        }

        /// <summary>Your serve, previewed: would it land legally in the receiver's back quarter? (Shown as FAULT when not.)</summary>
        public bool ServeLegal { get; private set; } = true;
        public bool Serving { get; private set; }
        readonly ArcadeStroke _servePreview = new ArcadeStroke(1);
        float _lastPreviewX = float.NaN, _lastPreviewY, _previewAt;
        bool _lastHard, _lastAssisted;

        void PreviewServe(TestPlayerController ctrl, MatchRunner mr, float x, float y)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double charge = ctrl.Arcade.ServeCharging ? ctrl.Arcade.ChargeRatio : 0;
            bool hard = c != null && charge >= c.ServeHardCharge;
            ShotType = hard ? "hard serve" : "lob serve";
            // Assisted fixes the serve's line itself (and that search is the expensive one): nothing to warn about.
            if (c == null || IntentSource.Assisted) { ServeLegal = true; return; }
            bool same = Mathf.Abs(x - _lastPreviewX) < 0.02f && Mathf.Abs(y - _lastPreviewY) < 0.02f && hard == _lastHard && IntentSource.Assisted == _lastAssisted;
            if (same || Time.unscaledTime - _previewAt < 0.15f) return;          // a few times a second at most
            _lastPreviewX = x; _lastPreviewY = y; _lastHard = hard; _lastAssisted = IntentSource.Assisted; _previewAt = Time.unscaledTime;
            _servePreview.Assisted = IntentSource.Assisted;
            ServeLegal = _servePreview.PreviewServe(mr.Sim.Ball.Pos, mr.Sim.Ball.Temperature, mr.Sim.Surfaces, x, y,
                                                    hard ? System.Math.Max(charge, c.ServeHardCharge) : 0, mr.Sim.Score.ServeSide);
        }

        /// <summary>The preview drawn this frame (tests and diagnostics).</summary>
        public readonly ShotPreview Preview = new ShotPreview();
        public bool Shown { get; private set; }
        /// <summary>The smoothed world position of the target centre.</summary>
        public Vector3 WorldPoint { get; private set; }
        /// <summary>0 = aiming at the front wall; -1 / +1 = a boast off the left / right side wall.</summary>
        public int Wall { get; private set; }
        GUIStyle _tag;

        Transform _ring, _dot;
        Material _mat;
        Vector3 _pos;
        float _radius = 0.3f;
        bool _hasPos;

        public void Build(SimRunnerBase runner, int athlete, GreyboxPalette palette, Color colour)
        {
            Runner = runner; Athlete = athlete; Colour = colour;
            _mat = palette.Clear(colour);
            // Unit ring (radius 1) scaled to the spread; a fixed-size centre dot.
            _ring = Greybox.MeshObject("Ring", transform, Greybox.Torus(1f, 0.16f, 48, 6), _mat).transform;
            _dot = Greybox.MeshObject("Dot", transform, Greybox.Torus(0.045f, 0.045f, 12, 6), _mat).transform;
            _side = Greybox.MeshObject("BoastMark", transform, Greybox.Torus(1f, 0.16f, 32, 6), _mat).transform;
            _side.gameObject.SetActive(false);
        }

        bool Visible()
        {
            if (Runner == null) return false;
            if (Runner.ReplayActive) return false;
            var mr = Runner as MatchRunner;
            if (mr == null) return true;                                  // the practice court: always
            if (mr.InSetup || mr.Sim == null) return false;
            var ph = mr.Sim.Phase;
            return ph == MatchPhase.WarmUp || ph == MatchPhase.AwaitToss || ph == MatchPhase.Rally;
        }

        void LateUpdate()
        {
            IntentSource src = Runner != null ? Runner.LocalSource(Athlete) : null;
            Shown = src != null && Visible();
            Fade = Mathf.MoveTowards(Fade, Shown ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
            _ring.gameObject.SetActive(DrawRing && Fade > 0.01f);
            _dot.gameObject.SetActive(false);                      // plain: no centre dot
            Boasting = Shown && Runner.Athletes[Athlete].ArcadeHitEnabled && Runner.Athletes[Athlete].Arcade.BoastArmed;
            _side.gameObject.SetActive(DrawRing && Boasting && Fade > 0.01f);
            if (!Shown) { _hasPos = false; return; }

            var athletes = Runner.Athletes;
            Preview.Update(athletes[Athlete], src.Intent, Runner.Ball, Runner.Surfaces);
            if (!Preview.Valid) return;

            // The reticle itself (not a prediction): exactly where the player aims, so it never lags the mouse.
            var ctrl = athletes[Athlete];
            var aim = new V3();
            int wall = WallAim.TargetFromPoint(src.ReticleX, src.ReticleY, aim);
            ShotType = ctrl.ArcadeHitEnabled ? ctrl.Arcade.ShotTypeFor(src.ReticleY) : "drive";
            var mrun = Runner as MatchRunner;
            Serving = ctrl.ArcadeHitEnabled && mrun != null && mrun.Sim != null && mrun.Sim.Phase == MatchPhase.AwaitToss && mrun.Sim.Score.Server == Athlete;
            if (Serving) PreviewServe(ctrl, mrun, src.ReticleX, src.ReticleY);
            else ServeLegal = true;
            // Just off the wall, toward the court, so it never z-fights the plaster.
            Vector3 target = wall == 0
                ? SimToWorld.Pos(aim.X, aim.Y, 0.02)
                : SimToWorld.Pos(aim.X - wall * 0.02, aim.Y, aim.Z);
            if (Boasting)
            {
                // The ring shows where the boast lands (the aim, kept on the far half); the marker where it meets the side wall
                // (the straight line from the contact to the landing point's mirror image).
                var from = ctrl.Prediction.Valid ? ctrl.Prediction.Point : ctrl.Player.Pos;
                int side = ArcadeStroke.BoastTarget(from.X, aim.X, out double landX);
                aim.X = landX;
                target = SimToWorld.Pos(aim.X, aim.Y, 0.02);
                double wx = side * CourtDimensions.HALF_W, mx = 2 * wx - aim.X;
                double fy = ctrl.Prediction.Valid ? from.Y : 0.9;
                double t = System.Math.Abs(mx - from.X) > 1e-6 ? (wx - from.X) / (mx - from.X) : 0;
                t = System.Math.Max(0, System.Math.Min(1, t));
                double sz = from.Z * (1 - t), sy = fy + (aim.Y - fy) * t;
                SideWall = side;
                SideMarker = SimToWorld.Pos(wx - side * 0.02, System.Math.Max(0.2, sy), sz);
                _side.position = SideMarker;
                _side.rotation = Quaternion.Euler(0f, 90f, 0f);
                _side.localScale = new Vector3(CircleRadius * 0.8f, CircleRadius * 0.8f, 1f);
            }
            if (wall != Wall) _hasPos = false;                // jump to the other wall, don't glide across the court
            Wall = wall;
            float radius = CircleRadius;
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-60f * dt);
            _pos = _hasPos ? Vector3.Lerp(_pos, target, k) : target;
            _radius = _hasPos ? Mathf.Lerp(_radius, radius, k) : radius;
            _hasPos = true;
            WorldPoint = _pos;

            _ring.position = _pos; _dot.position = _pos;
            // The front wall is the world XY plane; a side wall is the YZ plane.
            Quaternion face = wall == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            _ring.rotation = face; _dot.rotation = face;
            _ring.localScale = new Vector3(_radius, _radius, 1f);

            // Plain: one colour, no words (the shot type is still worked out for tests / later UI).
            Color c = Plain;
            if (Athlete != 0 && Runner.LocalSource(0) != null) c = new Color(0.75f, 0.85f, 1f, 0.85f);
            c.a *= Fade;
            _mat.color = c;
        }

        void OnGUI()
        {
            if (!Shown || !DrawRing || Fade < 0.05f || !ShowWords) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(WorldPoint + Vector3.up * (_radius + 0.25f));
            if (sp.z <= 0) return;
            if (_tag == null) _tag = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var r = new Rect(sp.x - 70, Screen.height - sp.y - 12, 140, 24);
            string text = (Wall != 0 || Boasting ? "BOAST " : "") + ShotType.ToUpperInvariant() + (Serving && !ServeLegal ? " - FAULT" : "");
            _tag.normal.textColor = new Color(0, 0, 0, 0.7f * Fade);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, _tag);
            Color tc = Serving && !ServeLegal ? new Color(1f, 0.25f, 0.2f) : ColourFor(ShotType); tc.a = 0.9f * Fade;
            _tag.normal.textColor = tc;
            GUI.Label(r, text, _tag);
        }
    }
}
