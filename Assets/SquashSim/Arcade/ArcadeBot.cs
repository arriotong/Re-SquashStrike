// ===========================================================
// Assets/SquashSim/Arcade/ArcadeBot.cs
// P2-M2: a human-style player for the Target Arcade - for the tests,
// the star calibration (MatchBench --arcade) and the PlayMode checks.
// It does what a person does: reacts late-ish, runs to where the ball
// will be, aims the reticle at the target (with an error), holds Hit
// and lets go about as the ball arrives (with a timing error), plays
// the called shot, serves with a tap or a hold.
// ===========================================================

namespace SquashSim.Arcade
{
    public sealed class ArcadeBot
    {
        public double AimError = 0.25;        // m, std dev of the reticle error
        public double TimingError = 0.05;     // s, std dev of the release
        public double Reaction = 0.25;        // s before it moves to a new ball

        uint _rng;
        bool _holding, _boastTapped;
        double _releaseLead, _aimDx, _aimDy, _watch, _serveHold;
        long _ballKey = -1;
        readonly BallReader _read = new BallReader();

        public ArcadeBot(uint seed) { _rng = seed * 747796405u + 2891336453u; }

        double Uniform() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }
        double Gauss() { double a = System.Math.Max(1e-9, Uniform()), b = Uniform(); return System.Math.Sqrt(-2 * System.Math.Log(a)) * System.Math.Cos(2 * System.Math.PI * b); }

        /// <summary>Fill this step's intent for the session.</summary>
        public void Fill(InputIntent it, ArcadeSession s, double dt)
        {
            it.Hit.Pressed = false; it.Hit.Released = false; it.BoastPressed = false; it.DivePressed = false;
            it.HasAimPoint = true;
            it.Move.X = 0; it.Move.Z = 0; it.MoveMagnitude = 0;
            if (s.Over) { it.Hit.Held = false; _holding = false; return; }
            var ctrl = s.Player;
            var p = ctrl.Player.Pos;
            long key = s.Attempts;
            if (key != _ballKey)
            {
                _ballKey = key;
                _watch = 0;
                _boastTapped = false;
                _aimDx = Gauss() * AimError; _aimDy = Gauss() * AimError * 0.6;
                _releaseLead = 0.04 + Gauss() * TimingError;
                _serveHold = Uniform() < 0.4 ? 0.5 + 0.4 * Uniform() : 0.03;
                _read.Invalidate();
            }
            _watch += dt;

            // ---- where to aim ----
            double ax = 0, ay = 1.8;
            string shot = s.CalledShot;
            if (shot == null && s.Challenge.Mode == ArcadeMode.Drill)
                shot = s.Challenge.Drill == DrillKind.Drop ? "drop" : s.Challenge.Drill == DrillKind.NickHunt ? "kill" : s.Challenge.Drill == DrillKind.Length ? "drive" : null;
            if (s.Targets.Count > 0)
            {
                var t = s.Targets[0];
                if (t.Kind == TargetKind.FrontRing) { ax = t.X; ay = t.Y; }
                else if (shot != null || t.Kind == TargetKind.Nick)
                {
                    ay = shot == "lob" ? 3.4 : shot == "drop" ? 0.9 : shot == "kill" ? 0.9 : shot == "boast" ? 1.3 : 1.7;
                    ax = (p.X > 0 ? 1 : -1) * 2.2;
                }
            }
            if (s.Challenge.Mode == ArcadeMode.ServeTargets) { ax = s.ServeSide * 0.45; ay = 2.7; }
            it.AimPointX = System.Math.Max(-3.0, System.Math.Min(3.0, ax + _aimDx));
            it.AimPointY = System.Math.Max(0.7, System.Math.Min(4.0, ay + _aimDy));

            // ---- ghosting: run to the spot ----
            if (s.Challenge.Drill == DrillKind.Ghosting)
            {
                if (s.Targets.Count > 0 && _watch > Reaction) MoveTo(it, p, s.Targets[0].X, s.Targets[0].Z0);
                return;
            }

            // ---- the serve ----
            if (s.Serving)
            {
                if (!_holding && _watch > 0.6) { it.Hit.Pressed = true; it.Hit.Held = true; _holding = true; _watch = 0.6; }
                else if (_holding && _watch - 0.6 >= _serveHold) { it.Hit.Held = false; it.Hit.Released = true; _holding = false; }
                else it.Hit.Held = _holding;
                return;
            }

            // ---- the rally: read the ball, run to the intercept, hold, let go as it arrives ----
            var pred = ctrl.Prediction;
            bool live = ctrl.ContactEnabled && !s.Ball.Asleep;
            bool read = live && _watch > Reaction && _read.Read(s.Ball, s.Surfaces, ctrl.Player);
            if (read)
            {
                double side = _read.X >= p.X ? 1 : -1;
                MoveTo(it, p, _read.X - side * 0.7, _read.Z + 0.15);
            }
            else if (!live) MoveTo(it, p, 0, 5.6);
            if ((shot == "boast") && !_boastTapped && !ctrl.Arcade.BoastArmed) { it.BoastPressed = true; _boastTapped = true; }
            double charge = shot == "kill" ? 0.45 : 0;
            double until = pred.Valid ? pred.Time : (read ? _read.Time : 9);
            if (!_holding && live && until < charge + 0.45) { it.Hit.Pressed = true; _holding = true; }
            if (_holding && (until < _releaseLead || !live))
            {
                it.Hit.Held = false; it.Hit.Released = true; _holding = false;
                return;
            }
            it.Hit.Held = _holding;
        }

        static void MoveTo(InputIntent it, V3 p, double x, double z)
        {
            double dx = x - p.X, dz = z - p.Z, d = System.Math.Sqrt(dx * dx + dz * dz);
            if (d < 0.12) return;
            it.Move.X = dx / d; it.Move.Z = dz / d;
            it.MoveMagnitude = System.Math.Min(1, d / 0.6);
        }
    }
}
