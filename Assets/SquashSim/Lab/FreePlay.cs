// ===========================================================
// Assets/SquashSim/Lab/FreePlay.cs
// The warm-up court's free play: no feeder - just you, the ball and the
// front wall, the way players warm up on their own.
//
//   ball in hand   you walk anywhere with it; press to drop it and
//                  hit (tap: soft, hold: harder - the serve's controls,
//                  aimed with the reticle)
//   in play        hit it back as often as you like
//   dead ball      down (tin, or the floor before the front wall), out,
//                  a second bounce, or it stops: a moment later it is
//                  back in your hand
//
// It runs the LabSimulation's own pieces (player, ball, court) in its
// own step, so the lab's step - which SimParity holds bit-identical to
// the HTML lab - is untouched. Deterministic, like the rest of the sim.
// ===========================================================

namespace SquashSim.Lab
{
    public sealed class FreePlay
    {
        /// <summary>Seconds a dead ball lies before it is back in your hand.</summary>
        public const double BackToHand = 0.7;
        /// <summary>A ball in play this long without a dead-ball event is given back anyway.</summary>
        public const double MaxFlight = 8.0;

        public bool InHand { get; private set; } = true;
        /// <summary>Your shots that reached the front wall; the current and best run of them in a row.</summary>
        public int WallHits { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        /// <summary>Why the last ball died ("Down", "Out", "Not up", ...), null while it is in play.</summary>
        public string LastDead { get; private set; }

        bool _front, _dead, _charging;
        int _floors;
        double _deadFor, _sinceHit;

        /// <summary>Start (again) with the ball in your hand.</summary>
        public void Begin(LabSimulation sim)
        {
            sim.Launcher.SetRepeat(false);
            Streak = 0; LastDead = null;
            ToHand(sim);
        }

        void ToHand(LabSimulation sim)
        {
            InHand = true; _dead = false; _deadFor = 0; _sinceHit = 0; _front = false; _floors = 0;
            sim.Ball.Reset(HandPosition(sim), new V3(0, 0, 0), null, PhysicsConfig.Current.Match.StartTemperature);
            sim.Ball.Asleep = true;
        }

        static V3 HandPosition(LabSimulation sim)
        {
            var m = PhysicsConfig.Current.Match;
            var p = sim.Player.Player.Pos;
            return new V3(p.X + m.ServeTossSide, m.ServeTossHeight, p.Z - m.ServeTossForward);
        }

        /// <summary>One fixed step (in place of LabSimulation.Step). Consumes the intent's one-shot edges.</summary>
        public void Step(LabSimulation sim, InputIntent intent, double dt)
        {
            sim.StepEvents.Clear();
            var ctrl = sim.Player;
            var ball = sim.Ball;
            SimEvent drop = null;

            if (InHand)
            {
                // Ball in hand: it goes where you go. Press to drop and hit (the serve's tap / hold).
                ball.Pos.CopyFrom(HandPosition(sim));
                ball.Asleep = true;
                var arc = ctrl.Arcade;
                if (intent.Hit.Pressed) { intent.Hit.Pressed = false; arc.BeginServeCharge(); _charging = true; }
                if (_charging && arc.ServeCharging)
                {
                    if (intent.Hit.Held) arc.ChargeServe(dt);
                    else
                    {
                        var m = PhysicsConfig.Current.Match;
                        ball.Reset(HandPosition(sim), new V3(0, m.ServeTossSpeed, 0), null, ball.Temperature);
                        BallPhysics.Wake(ball);
                        ctrl.ServeStroke = true;
                        ctrl.ServeDefaultX = 0;
                        drop = arc.ServeNow(ctrl, ball, sim.Surfaces);
                        _charging = false;
                        InHand = false;
                    }
                }
                else _charging = false;
            }

            ctrl.ContactEnabled = !InHand && drop == null;
            SimEvent contact = ctrl.Step(intent, ball, sim.Surfaces, dt);
            ctrl.ServeStroke = false;
            ctrl.ContactEnabled = true;
            if (drop != null) contact = drop;
            intent.ClearEdges();
            if (InHand) ball.Pos.CopyFrom(HandPosition(sim));               // after this step's move

            if (contact != null)
            {
                contact.PlayerIndex = 0;
                sim.LastRacketContact = contact;
                sim.StepEvents.Add(contact);
                OnContact();
            }

            if (!InHand)
            {
                int before = sim.StepEvents.Count;
                if (!ball.Asleep) BallPhysics.Step(ball, sim.Surfaces, dt, sim.StepEvents);
                for (int i = before; i < sim.StepEvents.Count; i++) Watch(sim.StepEvents[i]);
                _sinceHit += dt;
                if (!_dead && (ball.Asleep || _sinceHit > MaxFlight)) Dead("Not up");
                if (_dead)
                {
                    _deadFor += dt;
                    if (_deadFor >= BackToHand) ToHand(sim);
                }
            }
            sim.StepCount++;
        }

        void OnContact()
        {
            if (_dead) return;
            _front = false; _floors = 0; _sinceHit = 0;
        }

        void Watch(SimEvent e)
        {
            if (_dead || e.Type != SimEventType.Impact) return;
            if (RallyReferee.OutReason(e) != null) { Dead("Out"); return; }
            string cls = e.SurfaceClass;
            if (!_front)
            {
                if (cls == "tin" || cls == "floor") { Dead("Down"); return; }
                if (cls == "frontWall")
                {
                    _front = true;
                    WallHits++; Streak++;
                    if (Streak > BestStreak) BestStreak = Streak;
                }
                return;
            }
            if (cls == "floor" && ++_floors >= 2) Dead("Not up");
        }

        void Dead(string why)
        {
            _dead = true; _deadFor = 0;
            LastDead = why;
            Streak = 0;
        }
    }
}
