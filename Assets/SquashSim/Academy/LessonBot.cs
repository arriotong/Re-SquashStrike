// ===========================================================
// Assets/SquashSim/Academy/LessonBot.cs
// P2-M3: a player for the Academy - for the tests that prove every
// controls lesson can be passed, and for the PlayMode checks. It does
// what each step asks: walks / sprints / dives to the spots, holds the
// reticle on the rings, plays the asked shot off the feed (reading the
// ball, holding Hit, letting go as it arrives), taps boast, serves
// (tap or hold; first under the line when the step wants a fault),
// reads on through the cards and makes the right call in the quizzes.
// ===========================================================

using SquashSim.Arcade;

namespace SquashSim.Academy
{
    public sealed class LessonBot
    {
        readonly BallReader _read = new BallReader();
        bool _holding, _boastTapped, _faultDone;
        double _diveTimer, _cardTimer, _serveTimer, _releaseLead = 0.04;
        int _lastStep = -1;
        long _feedKey;

        /// <summary>Fill this step's intent (and read on / answer when the step is a card or a quiz).</summary>
        public void Fill(InputIntent it, AcademySession s, double dt)
        {
            it.Hit.Pressed = false; it.Hit.Released = false; it.BoastPressed = false; it.DivePressed = false;
            it.Move.X = 0; it.Move.Z = 0; it.MoveMagnitude = 0; it.Sprint = false;
            it.HasAimPoint = true; it.AimPointX = 0; it.AimPointY = 1.8;
            if (s.Done) { it.Hit.Held = false; _holding = false; return; }
            var st = s.Current;
            if (s.StepIndex != _lastStep) { _lastStep = s.StepIndex; _faultDone = false; _cardTimer = 0; _serveTimer = 0; _boastTapped = false; _holding = false; }
            foreach (var e in s.Events) if (e.Kind == AcademyEventKind.Note && e.Note != null && e.Note.StartsWith("FAULT")) _faultDone = true;
            var p = s.Player.Player.Pos;

            switch (st.Goal)
            {
                case GoalKind.Card:
                    _cardTimer += dt;
                    if (_cardTimer > 0.4) s.NextCard();
                    return;
                case GoalKind.Quiz:
                    if (s.AwaitingAnswer) s.Answer(System.Array.IndexOf(st.Question.Choices, RulesQuiz.Answer(st.Question)));
                    return;
                case GoalKind.ReachSpots: case GoalKind.SprintTo:
                    if (s.Targets.Count > 0) MoveTo(it, p, s.Targets[0].X, s.Targets[0].Z0);
                    it.Sprint = st.Goal == GoalKind.SprintTo;
                    return;
                case GoalKind.Dive:
                    MoveTo(it, p, p.X > 0 ? -2.0 : 2.0, 5.0);
                    _diveTimer += dt;
                    if (_diveTimer > 1.8) { it.DivePressed = true; _diveTimer = 0; }
                    return;
                case GoalKind.ReticleRings:
                    if (s.Targets.Count > 0) { it.AimPointX = s.Targets[0].X; it.AimPointY = s.Targets[0].Y; }
                    return;
                case GoalKind.Serve: case GoalKind.FaultThenServe:
                    Serve(it, s, st, dt);
                    return;
            }

            // ---- the shots off the feed ----
            string shot = st.Shot ?? (st.Goal == GoalKind.Boast ? "boast" : "drive");
            double side = p.X >= 0 ? 1 : -1;
            it.AimPointX = st.Goal == GoalKind.ModeDrive ? side * 2.95 : st.Line == "straight" ? side * 1.0 : -side * 1.0;
            it.AimPointY = shot == "lob" ? 3.5 : shot == "drop" || shot == "kill" ? 0.85 : shot == "boast" ? 1.3 : 1.8;
            double charge = shot == "kill" ? 0.45 : st.Goal == GoalKind.PaceOver ? 0.6 : 0;
            var ctrl = s.Player;
            bool live = ctrl.ContactEnabled && !s.Ball.Asleep;
            long key = s.StepCount;
            if (!live) { _read.Invalidate(); _boastTapped = false; }
            bool read = live && _read.Read(s.Ball, s.Surfaces, ctrl.Player);
            if (read) { double sd = _read.X >= p.X ? 1 : -1; MoveTo(it, p, _read.X - sd * 0.7, _read.Z + 0.15); }
            else if (!live) MoveTo(it, p, 0, 5.4);
            if (shot == "boast" && live && !_boastTapped && !ctrl.Arcade.BoastArmed) { it.BoastPressed = true; _boastTapped = true; }
            var pred = ctrl.Prediction;
            double until = pred.Valid ? pred.Time : (read ? _read.Time : 9);
            double ready = shot == "drop" ? 0.15 : charge + 0.45;                 // a drop is a quick press (a long hold makes a kill)
            if (!_holding && live && until < ready) { it.Hit.Pressed = true; _holding = true; }
            if (_holding && (until < _releaseLead || !live)) { it.Hit.Held = false; it.Hit.Released = true; _holding = false; return; }
            it.Hit.Held = _holding;
            _feedKey = key;
        }

        void Serve(InputIntent it, AcademySession s, LessonStep st, double dt)
        {
            bool wantFault = st.Goal == GoalKind.FaultThenServe && !_faultDone;
            it.AimPointX = s.ServeSide * (st.ServeKind == "hard serve" ? 1.3 : 0.45);   // a hard serve flies flatter: a line nearer your side
            it.AimPointY = wantFault ? 1.2 : 2.7;
            if (!s.Serving) { _holding = false; it.Hit.Held = false; _serveTimer = 0; return; }
            _serveTimer += dt;
            double hold = st.ServeKind == "hard serve" ? 0.6 : 0.03;
            if (!_holding && _serveTimer > 0.5) { it.Hit.Pressed = true; it.Hit.Held = true; _holding = true; _serveTimer = 0.5; }
            else if (_holding && _serveTimer - 0.5 >= hold) { it.Hit.Held = false; it.Hit.Released = true; _holding = false; }
            else it.Hit.Held = _holding;
        }

        static void MoveTo(InputIntent it, V3 p, double x, double z)
        {
            double dx = x - p.X, dz = z - p.Z, d = System.Math.Sqrt(dx * dx + dz * dz);
            if (d < 0.1) return;
            it.Move.X = dx / d; it.Move.Z = dz / d;
            it.MoveMagnitude = System.Math.Min(1, d / 0.5);
        }
    }
}
