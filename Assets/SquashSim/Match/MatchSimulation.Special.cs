// ===========================================================
// Assets/SquashSim/Match/MatchSimulation.Special.cs
// The Special Shot. 2026-11-22 first (after Mario Tennis' sniping); remade
// that evening (the user: "squat down charging and then high hard jump into
// the air right where youre standing and then zooms into aiming ... the
// ball goes to where you are but not you just go to the middle of the
// court"; "the special shot ... looks like its pretty easy to get back";
// answers: a Captain-Tsubasa cut-in + a duel, a small chance without Focus):
//
//   charge    a full meter (the Special card), their shot off the front wall
//             coming to you: press Special (Q / D-pad up). Time stands; the
//             ball comes to your racket where you are; you squat
//             (SquatSeconds), then jump straight up (to AirHeight)
//   aim       first person (the game): move the aim anywhere on the front
//             wall; the meter drains (ZoneSeconds from full); Hit fires
//             (an empty meter fires)
//   wind-up   WindupSeconds: the world moves again - your opponent sees the
//             spark and the target and can move - then the strike (the
//             game plays the cut-in on it), and you drop back down
//   the shot  SpecialPace, straight at the aim; your opponent's reach and
//             timing cut (SpecialReachCut, SpecialGraceCut): a small
//             chance to get it back as it is
//   the duel  the defender holding Focus (Space / RT) and letting go of Hit
//             within DuelWindow of the moment the ball reaches them
//             (DuelAt - the game draws the closing ring): the cuts are
//             lifted, they move FocusSpeed times as fast - and their return
//             is weak (DuelReturnPace). One press: early or late, it's lost
//   focus     held whenever their shot comes to you: slow motion (the
//             game's clock), the meter drains (FocusDrainSeconds from full);
//             let go with the ball out of reach: a dive (below)
//
// A sim action from the intent (InputIntent.SpecialPressed / FocusHeld /
// Hit), so replays and both machines online see the same; the AI uses it
// too (OpponentBrain). Deterministic, UnityEngine-free.
// ===========================================================

using static SquashSim.CourtDimensions;

namespace SquashSim
{
    public sealed partial class MatchSimulation
    {
        public const double ZoneSeconds = 2.6, SpecialPace = 64.0;
        /// <summary>The Special Shot's cuts on the defender, unless they win the duel: a small chance to get it back.</summary>
        public const double SpecialReachCut = 0.68, SpecialGraceCut = 0.78;
        /// <summary>The jump: this high (feet), right where you stand; the squat before it, the rise; the wind-up; the fall.</summary>
        public const double AirHeight = 1.4, SquatSeconds = 0.35, RiseSeconds = 0.25, WindupSeconds = 0.5, FallSpeed = 6.0;
        public const double ChargeSeconds = SquatSeconds + RiseSeconds;
        /// <summary>The duel: Hit within this many (sim) seconds of the ball reaching you, holding Focus; the return's pace then.</summary>
        public const double DuelWindow = 0.08, DuelReturnPace = 0.7;
        /// <summary>Focus speed for the duel's winner.</summary>
        public const double FocusSpeed = 1.5;
        /// <summary>Seconds of (sim) time a full meter lasts while focusing - about four seconds on the slowed clock.</summary>
        public const double FocusDrainSeconds = 1.5;

        /// <summary>Who is charging (squat, jump) a Special Shot (-1: nobody), for how long.</summary>
        public int ChargeBy { get; private set; } = -1;
        public double ChargeTime { get; private set; }
        /// <summary>Who is winding up a Special Shot (-1: nobody), for how long; where it's going (the front wall: x, y - locked at the
        /// press, or the aim while aiming).</summary>
        public int WindupBy { get; private set; } = -1;
        public double WindupTime { get; private set; }
        public double SpecialAimX { get; private set; }
        public double SpecialAimY { get; private set; }
        int _landBy = -1;
        /// <summary>The athlete in the air now (rising, aiming, winding up, or falling back), and how high.</summary>
        public bool InAir(int i) { return i >= 0 && i < 2 && Players[i].Player.Pos.Y > 0.001; }
        /// <summary>The squat before the jump (the game crouches them).</summary>
        public bool Squatting(int i) { return ChargeBy == i && ChargeTime < SquatSeconds; }
        readonly bool[] _focusing = new bool[2];
        /// <summary>Player i is focusing now (the game slows its clock for a local one).</summary>
        public bool Focusing(int i) { return i >= 0 && i < 2 && _focusing[i]; }

        /// <summary>The window for Focus and the Special Shot: the opponent's shot has hit the front wall and it's coming to player i.</summary>
        public bool BallComing(int i)
        {
            return Phase == MatchPhase.Rally && Referee.Outcome == null && Referee.Shots > 0 && Referee.ExpectedStriker == i &&
                   Referee.LastHitter == 1 - i && !Ball.Asleep && Ball.FloorSinceFrontWall < 2;
        }

        /// <summary>Player i may focus: the Special card, some meter, the ball coming.</summary>
        public bool CanFocus(int i)
        {
            var mm = M(Players[i]);
            return mm != null && mm.Level("special") > 0 && mm.Power > 0 && ZoneBy < 0 && WindupBy < 0 && ChargeBy < 0 && BallComing(i);
        }

        /// <summary>Who is aiming a Special Shot now (-1: nobody) and for how long; who struck the Special Shot in flight (-1: none),
        /// and whether its target won the duel.</summary>
        public int ZoneBy { get; private set; } = -1;
        public double ZoneTime { get; private set; }
        public int SpecialBy { get; private set; } = -1;
        public bool SpecialDefended { get; private set; }
        /// <summary>The Special Shots fired, the duels won and lost so far (tests).</summary>
        public int SpecialsFired { get; private set; }
        public int SpecialsDefended { get; private set; }
        public int DuelsLost { get; private set; }
        /// <summary>The duel: when the ball reaches the defender (sim time; NaN until it's off the front wall); the result (0 none
        /// yet, 1 won, -1 lost - the one press spent early or late).</summary>
        public double DuelAt { get; private set; } = double.NaN;
        public int DuelResult { get; private set; }
        bool _duelPressed;
        readonly bool[] _weakReturn = new bool[2];
        readonly V3 _zoneBall = new V3();

        static RunMods M(TestPlayerController c) { return c.Player.Mods; }

        /// <summary>Player i may start a Special Shot now: the card, a full meter, the ball on its way to them.</summary>
        public bool CanSpecial(int i)
        {
            var mm = M(Players[i]);
            if (mm == null || mm.Level("special") <= 0 || mm.Power < 1) return false;
            if (Phase != MatchPhase.Rally || Referee.Outcome != null || Referee.Shots == 0 || ZoneBy >= 0 || WindupBy >= 0 || ChargeBy >= 0) return false;
            if (SpecialBy == 1 - i) return false;                                         // (a Special Shot coming: duel it, no counter)
            return BallComing(i);
        }

        /// <summary>A Special Shot is coming at player j, the duel not yet won.</summary>
        public bool SpecialIncoming(int j) { return SpecialBy == 1 - j && !SpecialDefended && Phase == MatchPhase.Rally && Referee.Outcome == null; }

        /// <summary>Each step, before anyone moves: the Special presses, the duel, Focus, the charge, the aiming. True: frozen this step.</summary>
        bool SpecialStep(double dt)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!Intents[i].SpecialPressed) continue;
                Intents[i].SpecialPressed = false;
                if (CanSpecial(i)) StartSpecial(i);
            }
            // the duel: when it'll reach the defender (once it's off the front wall); their one let-go of Hit, holding Focus (a
            // stroke is held to charge and let go as the ball comes - the let-go is the timing)
            if (SpecialBy >= 0 && !SpecialDefended && DuelResult == 0)
            {
                int j = 1 - SpecialBy;
                if (double.IsNaN(DuelAt) && BallComing(j)) DuelAt = Time + ArrivalIn(j);
                var ji = Intents[j];
                if (!_duelPressed && !double.IsNaN(DuelAt) && (ji.Hit.Released || ji.Touch.Released) && _focusing[j])
                {
                    _duelPressed = true;
                    if (System.Math.Abs(Time - DuelAt) <= DuelWindow) WinDuel(j);
                    else { DuelResult = -1; DuelsLost++; Note(j, "duelmiss", 0); }
                }
            }
            // Focus, held: the meter drains; let go with the ball out of reach: a dive
            for (int i = 0; i < 2; i++)
            {
                // 2026-11-22 evening (the user: "both of them in space bar ... if your slow mo ends you release and then your character
                // dives"): Focus let go - a dive, but only with the ball out of your reach (the smart dive)
                bool held = Intents[i].FocusHeld;
                if (_heldWas[i] && !held) TrySmartDive(i);
                _heldWas[i] = held;
                bool want = held && CanFocus(i);
                var fm = M(Players[i]);
                if (want) fm.Power = System.Math.Max(0, fm.Power - dt / FocusDrainSeconds);
                if (fm != null) fm.FocusBoost = SpecialDefended && SpecialBy == 1 - i ? FocusSpeed : 1;
                if (want && !_focusing[i]) Note(i, "focus", 0);
                _focusing[i] = want;
            }
            if (ChargeBy >= 0)
            {
                // the squat, then the jump straight up: the world stands still, the ball held at the racket
                ChargeTime += dt;
                var cp = Players[ChargeBy].Player;
                double rise = System.Math.Max(0, System.Math.Min(1, (ChargeTime - SquatSeconds) / RiseSeconds));
                cp.Pos.Set(_airAt.X, AirHeight * (1 - (1 - rise) * (1 - rise)), _airAt.Z); cp.Vel.Set(0, 0, 0);
                Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
                Players[1 - ChargeBy].Player.Vel.Set(0, 0, 0);
                if (ChargeTime >= ChargeSeconds) { ZoneBy = ChargeBy; ZoneTime = 0; ChargeBy = -1; Note(ZoneBy, "zone", 0); }
                Intents[0].ClearEdges(); Intents[1].ClearEdges();
                return true;
            }
            if (WindupBy >= 0)
            {
                // the wind-up: the world moves, the striker and the ball hang in the air; then the strike
                WindupTime += dt;
                PinWindup();
                var wi = Intents[WindupBy];
                wi.Move.X = 0; wi.Move.Z = 0; wi.MoveMagnitude = 0; wi.Hit.Pressed = false; wi.Touch.Pressed = false; wi.DivePressed = false;
                if (WindupTime >= WindupSeconds) FireSpecial(WindupBy);
                return false;
            }
            if (ZoneBy < 0) return false;
            int z = ZoneBy;
            var c = Players[z];
            var mm = M(c);
            ZoneTime += dt;
            mm.Power = System.Math.Max(0, mm.Power - dt / ZoneSeconds);
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            for (int i = 0; i < 2; i++) { Players[i].Player.Vel.Set(0, 0, 0); }
            c.AimOnly(Intents[z], Ball, Surfaces);
            AimNow(c, out double ax, out double ay); SpecialAimX = ax; SpecialAimY = ay;
            bool fire = (Intents[z].Hit.Pressed || Intents[z].Touch.Pressed) && ZoneTime > 0.15;
            if (fire || mm.Power <= 0) { StartWindup(z); Intents[0].ClearEdges(); Intents[1].ClearEdges(); return true; }
            Intents[0].ClearEdges(); Intents[1].ClearEdges();
            return true;
        }

        static void AimNow(TestPlayerController c, out double tx, out double ty)
        {
            var aim = c.Aim;
            tx = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.X : aim.IntendedPoint.X;
            ty = aim.HasWallTarget && aim.TargetWall == 0 ? aim.WallTarget.Y : aim.IntendedPoint.Y;
            tx = System.Math.Max(-HALF_W + 0.05, System.Math.Min(HALF_W - 0.05, tx));
            ty = System.Math.Max(CourtDimensions.TIN_H + 0.04, System.Math.Min(CourtDimensions.FRONT_WALL_OUT_H - 0.2, ty));
        }

        /// <summary>Hit pressed while aiming: the aim locked, the meter spent, the wind-up begins (the world moves again).</summary>
        void StartWindup(int z)
        {
            ZoneBy = -1;
            WindupBy = z; WindupTime = 0;
            M(Players[z]).Power = 0;
            Note(z, "windup", 0);
            PinWindup();
        }

        /// <summary>The striker in the air where they jumped and the ball at their racket, held there; the swing drawn back.</summary>
        void PinWindup()
        {
            var c = Players[WindupBy];
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            c.Player.Pos.Set(_airAt.X, AirHeight, _airAt.Z); c.Player.Vel.Set(0, 0, 0);
            var sw = c.Swing;
            sw.State = SwingStates.Preparing; sw.Stroke = "hit"; sw.Mode = "prepared"; sw.HoldSeconds = WindupTime;
        }

        /// <summary>The end of each step: the wind-up held through the step's movement; a striker back from the air falls to the floor.</summary>
        void SpecialEndStep(double dt)
        {
            if (WindupBy >= 0) PinWindup();
            if (_landBy >= 0)
            {
                var p = Players[_landBy].Player;
                p.Pos.Y = System.Math.Max(0, p.Pos.Y - FallSpeed * dt);
                if (p.Pos.Y <= 0) { p.Pos.Y = 0; _landBy = -1; }
            }
        }
        readonly V3 _airAt = new V3();

        void StartSpecial(int i)
        {
            var c = Players[i];
            ChargeBy = i; ChargeTime = 0;
            _focusing[0] = _focusing[1] = false;
            // right where you stand (kept off the walls); the ball comes to your racket, beside and above your head
            double sp = PhysicsConfig.Current.MovementAssist.PreferredSpacing;
            var at = c.Player.Pos;
            double x = System.Math.Max(-HALF_W + 0.9, System.Math.Min(HALF_W - 0.9, at.X)), zz = System.Math.Max(2.0, System.Math.Min(LENGTH - 1.2, at.Z));
            double side = x > 0 ? -1 : 1;                                                // (the racket on the open side, towards the middle)
            _airAt.Set(x, 0, zz);
            _zoneBall.Set(x + side * System.Math.Max(0.75, sp), AirHeight + 1.5, zz - 0.3);
            c.Player.Pos.Set(x, 0, zz);
            c.Player.Vel.Set(0, 0, 0);
            c.Racket.MarkTeleported();
            // the opponent never stands in the way of it: moved clear (across the court), and no call carried over
            var o = Players[1 - i].Player;
            double dx = o.Pos.X - x, dz = o.Pos.Z - zz;
            if (dx * dx + dz * dz < 2.0 * 2.0)
            {
                double ox = x >= 0 ? x - 2.2 : x + 2.2, oz = System.Math.Max(3.0, System.Math.Min(LENGTH - 1.0, zz + 1.2));
                o.Pos.Set(System.Math.Max(-HALF_W + 0.4, System.Math.Min(HALF_W - 0.4, ox)), 0, oz);
                o.Vel.Set(0, 0, 0);
                Players[1 - i].Racket.MarkTeleported();
            }
            Interference.Reset();
            Ball.Pos.CopyFrom(_zoneBall); Ball.Vel.Set(0, 0, 0);
            Note(i, "charge", 0);
        }

        void FireSpecial(int i)
        {
            var c = Players[i];
            var mm = M(c);
            ZoneBy = -1; WindupBy = -1;
            mm.Power = 0;
            double tx = SpecialAimX, ty = SpecialAimY;
            Ball.Pos.CopyFrom(_zoneBall);
            SimEvent hit = c.Arcade.SpecialNow(c, Ball, Surfaces, tx, ty, SpecialPace);
            hit.PlayerIndex = i;
            hit.Time = Time;
            StepEvents.Add(hit);
            if (Referee.Shots > 0) RallyContacts[i]++;
            OnRacketContact(i, hit);
            mm.Power = 0;                                                     // (the stroke's own meter share: not for this one)
            SpecialBy = i; SpecialDefended = false; SpecialsFired++;
            DuelAt = double.NaN; DuelResult = 0; _duelPressed = false;
            var op = ModsOf(Players[1 - i].Player);
            op.ReachCut = SpecialReachCut; op.GraceCut = SpecialGraceCut;
            Note(i, "special", 0);
            _landBy = i;                                                      // back down to the floor
        }

        /// <summary>The duel won: the cuts lifted, Focus speed, and the return will be weak.</summary>
        void WinDuel(int j)
        {
            var mm = ModsOf(Players[j].Player);
            mm.ReachCut = 0; mm.GraceCut = 0;
            SpecialDefended = true; SpecialsDefended++; DuelResult = 1;
            _weakReturn[j] = true;
            Note(j, "defend", 0);
        }

        /// <summary>Seconds until the ball comes nearest the defender where they stand (at a playable height, before it's dead).</summary>
        double ArrivalIn(int j)
        {
            var p = Players[j].Player;
            var cfg = PhysicsConfig.Current;
            double minH = cfg.Ball.Radius * 0.9, maxH = cfg.Racket.MaxContactHeight;
            var b = _reachBall;
            b.Pos.CopyFrom(Ball.Pos); b.Vel.CopyFrom(Ball.Vel); b.Omega.CopyFrom(Ball.Omega);
            b.Temperature = Ball.Temperature; b.DipOffFrontWall = Ball.DipOffFrontWall; b.Resting = Ball.Resting; b.Asleep = Ball.Asleep;
            b.SimTime = 0; b.FloorBounces = 0; b.LastCollisionSurface = Ball.LastCollisionSurface; b.ImpactHistory.Clear();
            double dt = 0.01, best = double.MaxValue, at = 0.3;
            int floors = Ball.FloorSinceFrontWall;
            for (int k = 1; k <= 150; k++)
            {
                _reachEvents.Clear();
                BallPhysics.Step(b, Surfaces, dt, _reachEvents);
                if (floors + b.FloorBounces > 1) break;
                if (b.Pos.Y < minH || b.Pos.Y > maxH) continue;
                double d = DMath.Hypot(b.Pos.X - p.Pos.X, b.Pos.Z - p.Pos.Z);
                if (d < best) { best = d; at = k * dt; }
            }
            return at;
        }

        // ---------------- 2026-11-22 evening: the smart dive (Space / RT, let go) ----------------

        /// <summary>A dive's price from the Special meter (the Special card); without the card it costs stamina, as it always did.</summary>
        public const double DiveMeterCost = 0.2;
        readonly bool[] _heldWas = new bool[2];
        /// <summary>Dives the smart dive started, and the let-gos it turned down (the ball was in reach, or no meter) - tests.</summary>
        public int SmartDives { get; private set; }
        public int DivesDeclined { get; private set; }

        /// <summary>Focus let go: dive towards the stick if the ball is coming to you and out of your reach running, and you can pay.</summary>
        void TrySmartDive(int i)
        {
            var c = Players[i];
            if (!c.ArcadeHitEnabled || !BallComing(i) || !c.Arcade.CanDive) return;
            if (Reachable(i)) { DivesDeclined++; return; }
            var mm = M(c);
            bool meter = mm != null && mm.Level("special") > 0;
            if (meter)
            {
                if (mm.Power < DiveMeterCost) { DivesDeclined++; return; }
                mm.Power = System.Math.Max(0, mm.Power - DiveMeterCost);
            }
            c.Arcade.DiveRequest = true; c.Arcade.DivePaid = meter;
            SmartDives++;
            Note(i, "dive", 0);
        }

        static readonly BallState _reachBall = new BallState();
        static readonly System.Collections.Generic.List<SimEvent> _reachEvents = new System.Collections.Generic.List<SimEvent>();

        /// <summary>Can player i get to the ball running (no dive) before it's dead: some moment, before its second bounce, where
        /// the ball is at a playable height within reach of where they can have run to by then.</summary>
        public bool Reachable(int i)
        {
            var p = Players[i].Player;
            var cfg = PhysicsConfig.Current;
            double speed = cfg.Player.MaxSpeed * p.Attributes.SpeedScale * (p.Mods != null ? p.Mods.MoveScale : 1) * (p.AiPaced ? PlayerMovement.AiRunScale : 1);
            double reach = cfg.Reach.Stretch;
            double minH = cfg.Ball.Radius * 0.9, maxH = cfg.Racket.MaxContactHeight;
            var b = _reachBall;
            b.Pos.CopyFrom(Ball.Pos); b.Vel.CopyFrom(Ball.Vel); b.Omega.CopyFrom(Ball.Omega);
            b.Temperature = Ball.Temperature; b.DipOffFrontWall = Ball.DipOffFrontWall; b.Resting = Ball.Resting; b.Asleep = Ball.Asleep;
            b.SimTime = 0; b.FloorBounces = 0; b.LastCollisionSurface = Ball.LastCollisionSurface; b.ImpactHistory.Clear();
            double dt = 0.01;
            int floors = Ball.FloorSinceFrontWall;
            for (int k = 1; k <= 200; k++)
            {
                _reachEvents.Clear();
                BallPhysics.Step(b, Surfaces, dt, _reachEvents);
                if (floors + b.FloorBounces > 1) return false;                      // dead before you got there
                if (b.Pos.Y < minH || b.Pos.Y > maxH) continue;
                double d = DMath.Hypot(b.Pos.X - p.Pos.X, b.Pos.Z - p.Pos.Z) - reach;
                if (d <= speed * k * dt * 0.9) return true;                         // (a little short of flat-out: a run is never perfect)
            }
            return false;
        }

        /// <summary>A stroke by someone: a Special Shot in flight is over (the striker's own one has just begun); a duel's winner plays
        /// a weak return.</summary>
        void SpecialAtContact(int i)
        {
            if (_weakReturn[i])
            {
                _weakReturn[i] = false;
                Ball.Vel.X *= DuelReturnPace; Ball.Vel.Z *= DuelReturnPace; Ball.Vel.Y = Ball.Vel.Y * DuelReturnPace + 1.2;   // (a pop-up)
            }
            if (SpecialBy >= 0 && SpecialBy != i) { SpecialBy = -1; SpecialDefended = false; DuelAt = double.NaN; DuelResult = 0; }
        }

        void SpecialReset()
        {
            ZoneBy = -1; SpecialBy = -1; SpecialDefended = false; _focusing[0] = _focusing[1] = false;
            WindupBy = -1; _landBy = -1; ChargeBy = -1;
            for (int i = 0; i < 2; i++) Players[i].Player.Pos.Y = 0;
            _heldWas[0] = _heldWas[1] = false;
            _weakReturn[0] = _weakReturn[1] = false;
            DuelAt = double.NaN; DuelResult = 0; _duelPressed = false;
        }

    }
}
