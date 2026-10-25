// ===========================================================
// Assets/SquashSim/Player/ArcadeStroke.cs
// The simple hitting system (game profile; local human players only),
// after the original SquashStrike (player.js attemptHit):
//
//   hold Hit / Touch   charge (up to ChargeFull seconds: more pace on drives / kills)
//   release            if the ball is within REACH of you right now, it is
//                      HIT - no racket arc to line up, no swing timing.
//                      Released a little early (the ball will be in reach
//                      within Grace seconds)? It still connects as the ball
//                      comes in. Otherwise: a miss.
//   auto step          while you hold (or have just let go) and the ball is
//                      about to arrive, the athlete steps toward it (up to
//                      LungeMax) - the Mario-style "get there" help.
//   the shot           goes to your aim target on the wall, accurately; only
//                      a stretched ball (past the sweet spot) drifts a little.
//                      The shot type comes from the aim height (the
//                      original SquashStrike): above LobHeight a LOB; below
//                      LowHeight a DROP - or a KILL if charged past
//                      KillCharge; otherwise a DRIVE.
//   dive               (Space / pad B) a lunge in the move direction: 1.5x
//                      reach (less accurate), then a slow recovery.
//   sprint             (Shift / pad LB) faster, at a stamina cost.
//   boast              tap E / pad RB to arm it for the next stroke: off the
//                      side wall by the ball, across to the aim point on the
//                      other half of the front wall (the ring shows where).
//   early              being set (in position) before the ball arrives makes
//                      the shot tighter: less spread, far fewer mishits.
//   drop               struck at a real pace for its distance, low on the
//                      front wall - not floated.
//
// The hit is a normal racket contact to everything downstream (the
// referee, sounds, pop-ups, the timing note), and the swing still plays
// (backswing while charging, the stroke through the contact) for the
// animation. The AI keeps the physical swing; the lab never sees this.
// Deterministic: its spread uses its own seeded generator.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class ArcadeHitSection
    {
        public double Reach;            // horizontal distance from the body the ball can be hit at
        public double SweetSpot;        // within this, a clean, accurate hit
        public double MinHeight, MaxHeight;
        public double Grace;            // released this early, it still connects when the ball comes in
        public double ChargeFull;       // seconds of holding for a full charge
        public double DriveSpeed, ChargeBonus, LobSpeed, LobHeight, TouchSpeed;
        public double MaxSpread;        // aim error at full stretch (m, on the wall)
        public double GrossMissChance;  // at full stretch, the chance the shot goes badly wrong
        public double LungeMax, LungeSpeed;   // the "auto jump closer": a step toward the ball while the stroke is ready
        public double BackWallSafeHeight;     // a shot that would reach the back wall on the full above this is played softer
        // The original SquashStrike: shot type from aim height; dive; sprint.
        public double LowHeight, KillCharge, KillHeightAboveTin, DropHeightAboveTin, DropSpeed;
        public double DiveSeconds, DiveSpeedScale, DiveRecovery, DiveRecoverySpeed, DiveReachScale, DiveStamina, DiveMinStamina;
        public double SprintScale, SprintDrain, WindedStamina;
        public double BoastPaceScale, BoastMaxHeight, BoastMinHeight, BoastCross;
        public double ServeSpeed, ServeHardCharge, ServeChargeBonus, ServeDepth;
        public double LobRise, LobMaxSpeed, LobDepth, LobWallMax;
        public double EarlyFull, EarlySpreadCut, EarlyMishitCut, EarlyNear;
        public double DropFlightTime, DropSpeedMin, DropSpeedMax;
        // Drives, kills and boasts keep only this much of their rise off the front wall (BallPhysics.DipOff): 1 = off.
        public double FrontWallRiseKeep = 1;
        // Assisted: a bad contact may lose the clean line. Bad = met more than AssistBadDistance from the sweet spot
        // (fully bad AssistBadSpan beyond that), set in position less than AssistBadSetTime, or diving. The chance runs
        // from AssistMissMin (just bad) to AssistMissMax (fully bad); a lost one goes AssistMissOffset (and up to as
        // much again) off the aim.
        public double AssistBadDistance, AssistBadSpan, AssistBadSetTime, AssistMissMin, AssistMissMax, AssistMissOffset;
    }

    public sealed class ArcadeStroke
    {
        double _charge;
        bool _charging;
        string _stroke = "hit";
        double _pending = -1;          // grace time left for a released stroke waiting for the ball
        bool _pendingLive;             // was the ball ever this player's to hit while the stroke waited?
        double _pendingCharge, _lunged;
        bool _boast;
        /// <summary>The next stroke will be a boast (E / pad RB tapped).</summary>
        public bool BoastArmed { get { return _boast; } }
        double _setTime;
        /// <summary>0..1: how early the athlete was set for the last stroke (1 = in position EarlyFull seconds ahead).</summary>
        public double LastEarliness { get; private set; }
        /// <summary>Seconds in position for the ball now coming (presentation and tests).</summary>
        public double SetTime { get { return _setTime; } }
        uint _rng;
        readonly InputIntent _idle = new InputIntent();
        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly V3 _vel = new V3();
        readonly V3 _aimPoint = new V3();
        readonly V3 _good = new V3();
        readonly V3 _scratchVel = new V3();
        readonly V3 _best = new V3();
        bool _dip;                      // the shot being solved leaves the front wall heading down (its scratch flights too)
        static readonly Prediction _fakePred = new Prediction();

        /// <summary>0..1 how charged the stroke being held is (presentation).</summary>
        public double ChargeRatio { get { var c = PhysicsConfig.Current.ArcadeHit; return c == null ? 0 : System.Math.Min(1, _charge / c.ChargeFull); } }
        public bool Charging { get { return _charging || _serveCharging; } }

        // ---- the serve: tap = a lob serve (high, deep), hold = charge a hard serve ----
        bool _serveCharging;
        /// <summary>The server is holding Hit: the serve is charging (the ball stays in the hand).</summary>
        public bool ServeCharging { get { return _serveCharging; } }
        public void BeginServeCharge() { _serveCharging = true; _charge = 0; }
        public void ChargeServe(double dt) { if (_serveCharging) _charge += dt; }

        // ---- dive ----
        double _diveT = -1, _diveRecover, _diveX, _diveZ;
        /// <summary>Mid-dive (the lunge).</summary>
        public bool Diving { get { return _diveT > 0; } }
        /// <summary>Seconds of post-dive recovery left (slower, a little less accurate).</summary>
        public double DiveRecovery { get { return _diveRecover; } }
        public int DiveCount { get; private set; }

        /// <summary>Sprint and dive recovery as a top-speed scale; sprinting costs stamina.</summary>
        public double SpeedScaleFor(TestPlayer p, InputIntent intent, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double s = 1.0;
            bool moving = intent.MoveMagnitude > 0.1;
            if (intent.Sprint && moving && p.Stamina > c.WindedStamina)
            {
                s *= c.SprintScale;
                p.Stamina = System.Math.Max(0, p.Stamina - c.SprintDrain * dt * p.Attributes.DrainScale);   // (x1 when neutral: exact)
            }
            return s;
        }

        /// <summary>The dive. Returns true while diving (the athlete is carried by the lunge, not by running).</summary>
        public bool StepDive(TestPlayerController ctrl, InputIntent intent, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var p = ctrl.Player;
            if (_diveRecover > 0) _diveRecover = System.Math.Max(0, _diveRecover - dt);
            if (intent.DivePressed && _diveT <= 0 && _diveRecover <= 0 && p.Stamina >= c.DiveMinStamina)
            {
                double mx = intent.Move.X, mz = intent.Move.Z, m = System.Math.Sqrt(mx * mx + mz * mz);
                if (m < 0.1) { mx = DMath.Sin(p.Facing); mz = -DMath.Cos(p.Facing); m = 1; }
                _diveX = mx / m; _diveZ = mz / m;
                _diveT = c.DiveSeconds;
                p.Stamina = System.Math.Max(0, p.Stamina - c.DiveStamina * p.Attributes.DrainScale);   // (x1 when neutral: exact)
                DiveCount++;
            }
            if (_diveT <= 0) return false;
            double speed = PhysicsConfig.Current.Player.MaxSpeed * c.DiveSpeedScale;
            p.Vel.X = _diveX * speed; p.Vel.Z = _diveZ * speed;
            p.Pos.X += p.Vel.X * dt; p.Pos.Z += p.Vel.Z * dt;
            PlayerMovement.ClampToCourt(p);
            p.Speed = speed;
            _diveT -= dt;
            if (_diveT <= 0) { _diveRecover = c.DiveRecovery; p.Vel.X = 0; p.Vel.Z = 0; }
            return true;
        }

        // ---- the great hit (the ring's flash, the arcade's bonus) ----
        public const double GreatEarliness = GameTuning.Hit.GreatEarliness, GreatReleaseWait = GameTuning.Hit.GreatReleaseWait, GreatDistance = GameTuning.Hit.GreatDistance;

        /// <summary>A great hit: set early, and let go so the ball met the racket at the sweet spot - not early, not jammed.</summary>
        public static bool IsGreat(SimEvent e)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            return c != null && e != null && e.ArcadeDistance >= 0 && System.Math.Abs(e.ArcadeDistance - c.SweetSpot) <= GreatDistance &&
                   e.Earliness >= GreatEarliness && e.ReleaseWait >= 0 && e.ReleaseWait <= GreatReleaseWait;
        }

        /// <summary>The shot type the reticle height (and the charge) would play: lob / drop / kill / drive.</summary>
        public string ShotTypeFor(double aimHeight)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            if (c == null) return "drive";
            if (aimHeight > c.LobHeight) return "lob";
            if (aimHeight < c.LowHeight) return ChargeRatio > c.KillCharge ? "kill" : "drop";
            return "drive";
        }
        /// <summary>"hit" or "touch": the stroke being held.</summary>
        public string Stroke { get { return _stroke; } }


        public bool Waiting { get { return _pending > 0; } }

        /// <summary>How bad the last contact was, 0 (good) .. 1: jammed / stretched, late into position, or diving.</summary>
        public double LastBadness { get; private set; }
        /// <summary>The last Assisted shot lost its clean line (a bad contact's chance).</summary>
        public bool LastAssistLost { get; private set; }

        /// <summary>0 = a good contact .. 1 = fully bad: the worst of distance from the sweet spot (too close or too far),
        /// how little time the athlete was set before the ball came, and a dive.</summary>
        double Badness(double dist, double setTime, ArcadeHitSection c) { return BadnessOf(dist, setTime, Diving, _diveRecover > 0, c); }

        public static double BadnessOf(double dist, double setTime, bool diving, bool recovering, ArcadeHitSection c)
        {
            if (diving) return 1;
            double off = System.Math.Abs(dist - c.SweetSpot) - c.AssistBadDistance;
            double b = off > 0 ? Clamp(off / c.AssistBadSpan, 0, 1) : 0;
            if (setTime < c.AssistBadSetTime) b = System.Math.Max(b, (c.AssistBadSetTime - setTime) / c.AssistBadSetTime);
            if (recovering) b = System.Math.Max(b, 0.5);
            return b;
        }

        /// <summary>The chance an Assisted shot with this badness loses its clean line (0 for a good contact).</summary>
        public static double AssistLossChance(double bad, ArcadeHitSection c) { return bad > 0 ? c.AssistMissMin + (c.AssistMissMax - c.AssistMissMin) * bad : 0; }

        double _releaseWait;

        public ArcadeStroke(uint seed) { _rng = seed * 2654435761u + 12345u; }
        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        /// <summary>Horizontal distance from the athlete to the ball, if the ball is hittable at this height.</summary>
        double ReachScale { get { return Diving ? PhysicsConfig.Current.ArcadeHit.DiveReachScale : 1.0; } }
        /// <summary>P3-C1: the athlete's reach (exactly 1 when neutral).</summary>
        static double AttrReach(TestPlayer p) { return p.Attributes.ReachScale; }

        bool InReach(TestPlayer p, BallState ball, ArcadeHitSection c, out double dist)
        {
            double dx = ball.Pos.X - p.Pos.X, dz = ball.Pos.Z - p.Pos.Z;
            dist = System.Math.Sqrt(dx * dx + dz * dz);
            return dist <= c.Reach * ReachScale * AttrReach(p) && ball.Pos.Y >= c.MinHeight && ball.Pos.Y <= c.MaxHeight;
        }

        /// <summary>One fixed step. Returns the contact event when the ball is hit.</summary>
        public SimEvent Step(TestPlayerController ctrl, InputIntent intent, BallState ball, List<Surface> surfaces, double dt)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            var sw = ctrl.Swing;
            var player = ctrl.Player;

            // The stroke animation (follow-through, recovery) runs on the normal swing clock.
            if (sw.State == SwingStates.Swinging || sw.State == SwingStates.Recovering)
            {
                _idle.ClearEdges();
                Swing.Step(sw, _idle, ctrl.Aim, player, ctrl.Prediction, new SwingCtx { Quality = ctrl.Assist.Quality, ReachState = ctrl.Assist.ReachState }, dt, ctrl.IntentSolver);
            }

            string pressed = intent.Hit.Pressed ? "hit" : (intent.Touch.Pressed ? "touch" : null);
            if (pressed != null)
            {
                _charging = true; _charge = 0; _stroke = pressed; _pending = -1; _lunged = 0;
                if (sw.State == SwingStates.Idle) { sw.State = SwingStates.Preparing; sw.Stroke = pressed; sw.Mode = "prepared"; sw.HoldSeconds = 0; }
            }
            if (_charging)
            {
                bool held = _stroke == "touch" ? intent.Touch.Held : intent.Hit.Held;
                if (held) { _charge += dt; sw.HoldSeconds = _charge; }
                if (!held)
                {
                    _charging = false;
                    _pending = c.Grace;
                    double graceScale = player.Attributes.GraceScale;          // P3-C1: exactly 1 when neutral
                    if (graceScale != 1.0) _pending *= graceScale;
                    _pendingLive = false;
                    _pendingCharge = System.Math.Min(1, _charge / c.ChargeFull);
                }
            }

            // Tap to arm / disarm a boast for the next stroke.
            if (intent.BoastPressed) _boast = !_boast;

            // Getting there early: time spent in position for the ball that is coming.
            var pred = ctrl.Prediction;
            if (ctrl.ContactEnabled && pred.Valid)
            {
                double ex0 = pred.Point.X - player.Pos.X, ez0 = pred.Point.Z - player.Pos.Z;
                if (System.Math.Sqrt(ex0 * ex0 + ez0 * ez0) <= c.SweetSpot + c.EarlyNear) _setTime += dt;
                else _setTime = System.Math.Max(0, _setTime - 2 * dt);
            }
            else _setTime = 0;

            // Auto step toward the ball while the stroke is ready and the ball is nearly there.
            bool ready = _pending > 0 || (_charging && pred.Valid && pred.Time < 0.35);
            if (ready && ctrl.ContactEnabled && pred.Valid && _lunged < c.LungeMax)
            {
                double dx = pred.Point.X - player.Pos.X, dz = pred.Point.Z - player.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dz * dz);
                double want = d - c.SweetSpot;
                if (want > 0 && d > 1e-6)
                {
                    double step = System.Math.Min(System.Math.Min(c.LungeSpeed * dt, want), c.LungeMax - _lunged);
                    player.Pos.X += dx / d * step;
                    player.Pos.Z += dz / d * step;
                    _lunged += step;
                    PlayerMovement.ClampToCourt(player);
                }
            }

            if (_pending > 0)
            {
                if (ctrl.ContactEnabled) _pendingLive = true;
                if (ctrl.ContactEnabled && InReach(player, ball, c, out double dist))
                {
                    _releaseWait = c.Grace * player.Attributes.GraceScale - _pending;
                    _pending = -1;
                    return Hit(ctrl, ball, surfaces, dist, _pendingCharge, c);
                }
                _pending -= dt;
                if (_pending <= 0)
                {
                    _pending = -1;
                    _boast = false;
                    // Let go while the ball was never yours to hit (the opponent's shot still on its way to the front
                    // wall): no swing at it - the stroke is simply dropped (playtest 2026-10-24: a swing there, with
                    // the ball glancing off you, looked like a hit before the front wall).
                    if (_pendingLive) Whiff(ctrl, ball);
                    else if (sw.State == SwingStates.Preparing) sw.State = SwingStates.Idle;
                }
            }
            return null;
        }

        /// <summary>The one-click serve: struck straight from the hand toward the aim target (above the service line).</summary>
        public SimEvent ServeNow(TestPlayerController ctrl, BallState ball, List<Surface> surfaces)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double charge = _serveCharging ? System.Math.Min(1, _charge / c.ChargeFull) : 0;
            _stroke = "hit"; _charging = false; _serveCharging = false; _pending = -1; _boast = false; _releaseWait = 0;
            var ev = Hit(ctrl, ball, surfaces, 0, charge, c);
            _charge = 0;
            return ev;
        }

        SimEvent Hit(TestPlayerController ctrl, BallState ball, List<Surface> surfaces, double dist, double charge, ArcadeHitSection c)
        {
            var player = ctrl.Player;
            var aim = ctrl.Aim;
            double missRatio = Clamp((dist - c.SweetSpot) / (c.Reach * ReachScale * AttrReach(player) - c.SweetSpot), 0, 1);
            double early = ctrl.ServeStroke ? 1 : Clamp(_setTime / c.EarlyFull, 0, 1);
            LastEarliness = early;
            double bad = ctrl.ServeStroke ? 0 : Badness(dist, _setTime, c);
            LastBadness = bad;
            _setTime = 0;
            if (Diving) missRatio = System.Math.Min(1, missRatio + GameTuning.Hit.DiveMiss);            // a diving hit is a scramble
            else if (_diveRecover > 0) missRatio = System.Math.Min(1, missRatio + GameTuning.Hit.DiveRecoveryMiss);

            // ---- where it goes ----
            double tx, ty, tz = 0;
            int wall = aim.HasWallTarget ? aim.TargetWall : 0;
            if (aim.HasWallTarget) { tx = aim.WallTarget.X; ty = aim.WallTarget.Y; tz = aim.WallTarget.Z; }
            else { tx = aim.IntendedPoint.X; ty = aim.IntendedPoint.Y; tz = 0; }
            // The serve goes where you aim (under the service line the referee calls a fault). Bots, with no
            // reticle, keep the old safe serve: above the line, cross-court when the stick is centred.
            if (ctrl.ServeStroke && wall == 0 && !aim.FromPoint)
            {
                ty = System.Math.Max(ty, CourtDimensions.SERVICE_LINE_H + 0.3);
                if (System.Math.Abs(aim.BiasX) < 0.08) tx = ctrl.ServeDefaultX;    // no drag: the normal cross-court serve
            }
            string shot;
            double speed;
            if (ctrl.ServeStroke && wall == 0 && aim.FromPoint)
            {
                // A local player's serve: tap = lob serve, hold = hard serve (struck from the hand: no spread).
                _boast = false;
                _dip = false;
                shot = ServeLaunch(ball, surfaces, tx, ty, charge, ctrl.ServeDefaultX > 0 ? -1 : 1, out speed);
            }
            else
            {
            bool touch = _stroke == "touch";
            // The shot type from the aim height (the original SquashStrike); charge adds pace to drives and kills.
            shot = ctrl.ServeStroke ? (ty > c.LobHeight ? "lob" : "serve") : ShotTypeFor(ty);    // bots serve the old way
            switch (shot)
            {
                case "lob": speed = c.LobSpeed; break;
                case "serve": speed = c.ServeSpeed; break;
                case "drop":
                    // A real pace for the distance (a short, firm flight low onto the front wall), not a float.
                    ty = CourtDimensions.TIN_H + c.DropHeightAboveTin;
                    double reach = System.Math.Sqrt((tx - ball.Pos.X) * (tx - ball.Pos.X) + ball.Pos.Z * ball.Pos.Z);
                    speed = Clamp(reach / c.DropFlightTime, c.DropSpeedMin, c.DropSpeedMax);
                    break;
                case "kill": ty = CourtDimensions.TIN_H + c.KillHeightAboveTin; speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
                default: speed = c.DriveSpeed * (1 + c.ChargeBonus * charge); break;
            }
            if (touch && shot != "drop") speed = c.TouchSpeed;
            bool boast = _boast && !ctrl.ServeStroke && wall == 0;
            _boast = false;
            // Shot lines: you choose the shot (its height) and straight or cross (the half of the wall); it plays that line.
            if (ShotLines && !boast && !ctrl.ServeStroke && wall == 0)
            {
                tx = PullToLine(tx, LineFor(ball, surfaces, shot, tx, ty, speed), Assisted);
            }
            int side = 0;
            if (boast)
            {
                // Off the wall by the ball, across to the aim point - kept on the other half (where the ring shows it).
                side = BoastTarget(ball.Pos.X, tx, out tx);
                if (shot != "drop") ty = Clamp(ty, c.BoastMinHeight, c.BoastMaxHeight);
                if (shot != "drop" && !touch) speed *= c.BoastPaceScale;
            }
            double spread = missRatio * missRatio * c.MaxSpread * (charge > GameTuning.Hit.FullChargeAt ? GameTuning.Hit.FullChargeSpreadCut : 1.0);
            double control = player.Attributes.ControlScale;                 // P3-C1: exactly 1 when neutral
            if (control != 1.0) spread *= control;
            spread *= 1 - c.EarlySpreadCut * early;                         // set early: a tighter shot
            if (Assisted) spread *= GameTuning.Hit.AssistSpreadScale;       // the easier mode: a little tighter

            double ex = (Rnd() - 0.5) * spread, ey = (Rnd() - 0.5) * spread * 0.9;
            if (wall == 0) tx += ex; else tz += ex;
            ty += ey;
            if (missRatio > 0 && Rnd() < missRatio * c.GrossMissChance * (1 - c.EarlyMishitCut * early) * control)
            {
                if (Rnd() < 0.5) ty = CourtDimensions.TIN_H - GameTuning.Hit.GrossMissTinDepth;                       // into the tin
                else ty += (Rnd() < 0.5 ? 1 : -1) * (GameTuning.Hit.GrossMissOff + missRatio * GameTuning.Hit.GrossMissOffSpan);  // well off
            }
            speed *= 1.0 - GameTuning.Hit.MissPaceLoss * missRatio;
            // The AI's difficulty (1 for a human): how hard its drives and kills are. Drops, boasts, lobs and serves
            // set their own pace (a softer drop just dies in the tin).
            if (player.PaceScale > 0 && player.PaceScale != 1.0 && (shot == "drive" || shot == "kill")) speed *= player.PaceScale;
            double power = player.Attributes.PowerScale;                     // P3-C1: the athlete's power (exactly 1 when neutral)
            if (power != 1.0 && (shot == "drive" || shot == "kill")) speed *= power;

            // Drives, kills and boasts leave the front wall heading down (lobs, drops and serves keep their arc).
            _dip = c.FrontWallRiseKeep < 1 && (shot == "drive" || shot == "kill" || boast);

            // Assisted controls (reworked 2026-10-20: "it plays the shot for me"): help with the aim, not the perfect
            // shot - the line pull above does most of it; here a straight drive or kill that would clip its own side
            // wall is moved in a little (StraightMaxShift at most). A bad contact (jammed, stretched, late, diving) may
            // lose the line and go clearly off the aim instead.
            LastAssistLost = false;
            if (Assisted && !boast && wall == 0 && (shot == "drive" || shot == "kill"))
            {
                if (bad > 0 && Rnd() < AssistLossChance(bad, c))
                {
                    LastAssistLost = true;
                    double off = c.AssistMissOffset * (1 + Rnd());
                    tx = Clamp(tx + (Rnd() < 0.5 ? -off : off), -CourtDimensions.HALF_W + 0.1, CourtDimensions.HALF_W - 0.1);
                    ty += (Rnd() - 0.5) * off * 0.6;
                }
                else tx = StraightAssist(ball, surfaces, tx, ty, speed, shot == "kill");
            }

            // ---- the launch that gets there (the real ball physics, a few corrections) ----
            if (boast && !SolveBoast(ball, surfaces, tx, ty, side, ref speed)) boast = false;   // no way round from here: a drive
            if (boast) shot = "boast";
            else if (shot == "lob" && wall == 0 && !touch) speed = SolveLob(ball, surfaces, tx, c.LobHeight);
            else Solve(ball, surfaces, tx, ty, tz, wall, speed);
            // Floor before the back wall, and in: a drive, kill, serve or boast that would reach the back wall on the
            // full - or a drive / kill that would fly over an out line - is struck just soft enough. Lobs keep their own rule.
            _good.CopyFrom(_vel);
            for (int k = 0; k < GameTuning.Hit.SoftenTries && (BackWallHeight(ball, surfaces) >= 0 || (!boast && GoesOut(ball, surfaces))); k++)
            {
                speed *= GameTuning.Hit.SoftenStep;
                if (boast)
                {
                    if (!SolveBoastAt(ball, surfaces, tx, ty, side, speed)) { _vel.CopyFrom(_good); break; }   // keep the last boast that worked
                    _good.CopyFrom(_vel);
                }
                else if (shot == "lob" && wall == 0 && !touch) break;   // the arc search already kept it in court
                else Solve(ball, surfaces, tx, ty, tz, wall, speed);
            }
            }

            double before = V3.Length(ball.Vel);
            ball.Vel.CopyFrom(_vel);
            ball.Omega.X = 0; ball.Omega.Y = 0; ball.Omega.Z = 0;
            BallPhysics.Wake(ball);
            ball.DipOffFrontWall = _dip;
            _dip = false;
            ball.FloorSinceFrontWall = 0;
            double m = PhysicsConfig.Current.Ball.Mass;
            BallTemperature.ApplyImpactHeat(ball, 0.5 * m * (before * before + speed * speed) * 0.25, "racket");

            // ---- the swing plays through this contact (animation; the racket meets the ball here) ----
            var sw = ctrl.Swing;
            sw.Stroke = _stroke;
            sw.Mode = charge > 0.25 ? "prepared" : "quick";
            Swing.ArcadeCommit(sw, ctrl, ball.Pos);
            ctrl.Racket.HeadPos.CopyFrom(ball.Pos);
            ctrl.Racket.SweetSpot.CopyFrom(ball.Pos);

            double quality = 1 - missRatio;
            var ev = new SimEvent
            {
                Type = SimEventType.RacketContact,
                Point = new V3(ball.Pos.X, ball.Pos.Y, ball.Pos.Z),
                Stroke = _stroke,
                Mode = sw.Mode,
                Technique = shot,
                Side = player.SwingSide >= 0 ? 1 : -1,
                HeadSpeed = speed * 0.8,
                FaceNormal = new V3(_vel.X / speed, _vel.Y / speed, _vel.Z / speed),
                OffCentre = missRatio * PhysicsConfig.Current.Racket.HeadRadius,
                OffCentreNorm = missRatio,
                Restitution = PhysicsConfig.Current.Racket.Restitution,
                BallSpeedOut = V3.Length(ball.Vel),
                OutVel = new V3(ball.Vel.X, ball.Vel.Y, ball.Vel.Z),
                Temperature = ball.Temperature,
                Time = ball.SimTime,
                SwingPhaseAtContact = 0.5,
                ArcadeQuality = quality,
                Earliness = early,
                ReleaseWait = _releaseWait,
                ArcadeDistance = ctrl.ServeStroke ? -1 : dist
            };
            ctrl.Racket.LastContact = ev;
            ball.LastRacketContact = new RacketTouch { By = "player", Time = ball.SimTime, Speed = ev.BallSpeedOut, Stroke = _stroke, Technique = ev.Technique };
            return ev;
        }

        /// <summary>A released stroke that never met the ball: the swing still plays, at nothing.</summary>
        void Whiff(TestPlayerController ctrl, BallState ball)
        {
            var p = ctrl.Player;
            double side = p.SwingSide >= 0 ? 1 : -1;
            double fx = DMath.Sin(p.Facing), fz = -DMath.Cos(p.Facing);
            var at = new V3(p.Pos.X + fx * 0.5 + fz * 0 + side * 0.6 * DMath.Cos(p.Facing), 0.9, p.Pos.Z + fz * 0.5 + side * 0.6 * DMath.Sin(p.Facing));
            ctrl.Swing.Stroke = _stroke;
            ctrl.Swing.Mode = "quick";
            Swing.ArcadeCommit(ctrl.Swing, ctrl, at);
            ctrl.Swing.HasContacted = false;
        }

        /// <summary>Velocity of the given speed that carries the ball from where it is to the target point
        /// (front wall, or a side wall for a boast): straight-line start, then corrected on the real flight.</summary>
        /// <summary>Start the next Solve this far above the target (a lob's high arc); 0 = straight at it.</summary>
        double _lift;

        /// <summary>
        /// A lob: still RISING when it meets the front wall, so it carries on up and over to the back. Tries a few
        /// arcs (how fast it is still rising at the wall); each starts from the drag-free flight and is corrected on
        /// the real one by Solve; keeps the one landing nearest LobDepth that stays in court (no ceiling, nothing
        /// out, no back-wall hit on the full above the safe height). Returns the pace; the launch is left in the
        /// stroke velocity.
        /// </summary>
        /// <summary>Assisted controls (the game's setting for local players): straight drives kept off the side wall,
        /// serves nudged onto a line that lands deep and legal. Off = the shot goes exactly where you aim.</summary>
        public bool Assisted;

        /// <summary>Shot lines (a person's setting, both control modes; off for the AI and the Target Arcade, where the
        /// exact spot is the point): the shot you pick plays its line - see ShotLine.</summary>
        public bool ShotLines;

        /// <summary>The last serve launched (or previewed) lands legally in the receiver's back quarter.</summary>
        public bool LastServeLegal { get; private set; }
        /// <summary>Where the last serve met the front wall, x (Assisted may have moved it off the ring).</summary>
        public double LastServeLineX { get; private set; }

        /// <summary>
        /// A local player's serve, launched into the stroke velocity: under ServeHardCharge a lob serve (onto the ring, the
        /// arc that lands deepest and legal), above it a hard serve (straight at the ring, up to +ServeChargeBonus pace,
        /// softened until it bounces before the back wall and stays in). With Assisted on, the line (front-wall x) is
        /// nudged to the nearest one that lands deep and legal. Returns "lob serve" / "hard serve".
        /// </summary>
        public string ServeLaunch(BallState ball, List<Surface> surfaces, double tx, double ty, double charge, int serveSide, out double speed)
        {
            _dip = false;                                       // serves keep their own arc off the wall
            var c = PhysicsConfig.Current.ArcadeHit;
            bool hard = charge >= c.ServeHardCharge;
            double pace = hard ? c.ServeSpeed * (1 + c.ServeChargeBonus * (charge - c.ServeHardCharge) / (1 - c.ServeHardCharge)) : c.ServeSpeed;
            if (Assisted)
            {
                // The ring first; if that is not a deep, legal serve, every line across the wall (0.2 m apart), the
                // nearest good one winning.
                ServeAt(ball, surfaces, tx, ty, hard, pace, serveSide, out _);
                double bestScore = ServeScore(ball, surfaces, serveSide), bestX = tx;
                if (bestScore >= 1.5)
                {
                    double lim = CourtDimensions.HALF_W - 0.3;
                    for (double x = -lim; x <= lim + 1e-9; x += GameTuning.Assist.ServeLineStep)
                    {
                        ServeAt(ball, surfaces, x, ty, hard, pace, serveSide, out _);
                        double score = ServeScore(ball, surfaces, serveSide) + 1.0 * System.Math.Abs(x - tx);
                        if (score < bestScore) { bestScore = score; bestX = x; }
                    }
                }
                tx = bestX;
            }
            else if (!hard && ty >= CourtDimensions.SERVICE_LINE_H)      // (aimed under the service line it stays a fault)
            {
                // Not assisted, a lob serve: struck at the ring if that goes deep; if not (the middle of the wall, the far
                // half, or high - playtest 2026-10-24 "my lob serves land in the middle of the court"), the nearest line
                // within LobServeNudge of it that does - a server picks the line that gets it into the back corner.
                ServeAt(ball, surfaces, tx, ty, false, pace, serveSide, out _);
                double bestScore = ServeScore(ball, surfaces, serveSide), bestX = tx, bestY = ty;
                if (bestScore >= 0.8)
                {
                    double r = GameTuning.Serve.LobNudge, floorY = CourtDimensions.SERVICE_LINE_H + 0.3;
                    // outward from the ring, stopping at the first line that is deep enough (one search, at the serve)
                    double step = 2 * GameTuning.Assist.ServeLineStep;
                    for (int k = 1; k <= (int)System.Math.Round(r / step) && bestScore >= 0.8; k++)
                        foreach (double dx in new[] { k * step, -k * step })
                            for (double dy = -0.4; dy <= 0.41 && bestScore >= 0.8; dy += 0.4)
                            {
                                double x = Clamp(tx + dx, -CourtDimensions.HALF_W + 0.3, CourtDimensions.HALF_W - 0.3), y = System.Math.Max(floorY, ty + dy);
                                ServeAt(ball, surfaces, x, y, false, pace, serveSide, out _);
                                double score = ServeScore(ball, surfaces, serveSide) + 0.3 * (System.Math.Abs(x - tx) + System.Math.Abs(y - ty));   // depth first
                                if (score < bestScore) { bestScore = score; bestX = x; bestY = y; }
                            }
                }
                tx = bestX; ty = bestY;
            }
            ServeAt(ball, surfaces, tx, ty, hard, pace, serveSide, out speed);
            LastServeLegal = ServeScore(ball, surfaces, serveSide) < 100;
            LastServeLineX = tx;
            return hard ? "hard serve" : "lob serve";
        }

        void ServeAt(BallState ball, List<Surface> surfaces, double tx, double ty, bool hard, double pace, int serveSide, out double speed)
        {
            if (!hard) { speed = SolveLobServe(ball, surfaces, tx, ty, serveSide); return; }
            // A hard serve keeps its pace: if it would go out or reach the back wall on the full from the ring's height,
            // it is struck lower (down to just over the service line); only then, softer.
            speed = pace;
            double floorY = CourtDimensions.SERVICE_LINE_H + 0.25, y = ty;
            for (; ; y -= 0.15)
            {
                if (y < floorY) y = System.Math.Min(ty, floorY);
                Solve(ball, surfaces, tx, y, 0, 0, speed);
                if ((BackWallHeight(ball, surfaces) < 0 && !GoesOut(ball, surfaces)) || y <= floorY || y <= ty - 1.6) break;
            }
            for (int k = 0; k < GameTuning.Hit.SoftenTries && (BackWallHeight(ball, surfaces) >= 0 || GoesOut(ball, surfaces)); k++)
            {
                speed *= GameTuning.Hit.SoftenStep;
                Solve(ball, surfaces, tx, y, 0, 0, speed);
            }
        }

        /// <summary>Preview a serve without touching any athlete or ball (the ring's FAULT warning). Leaves LastServeLegal.</summary>
        public bool PreviewServe(V3 from, double temperature, List<Surface> surfaces, double tx, double ty, double charge, int serveSide)
        {
            _preview.Reset(from, null, null, temperature);
            ServeLaunch(_preview, surfaces, tx, ty, charge, serveSide, out _);
            return LastServeLegal;
        }
        readonly BallState _preview = new BallState();

        /// <summary>
        /// The front-wall x of the textbook line for this shot: straight or cross, whichever line the aim is nearer
        /// (IsCross; a ball near the middle goes to the corner on the aim's side). The line is the straight path from the
        /// ball to the mirror image (behind the front wall) of where that shot should land - so it comes back deep down
        /// the wall, or into the far corner.
        /// </summary>
        public static double ShotLine(double bx, double bz, string shot, double tx, double landing = -1)
        {
            bool mid = System.Math.Abs(bx) <= GameTuning.Hit.LineMidBall;
            int side = mid ? (tx >= 0 ? 1 : -1) : (bx > 0 ? 1 : -1);
            bool straight = mid || !IsCross(bx, bz, shot, tx);
            return LineX(bx, bz, shot, straight, side, landing);
        }

        /// <summary>
        /// Straight or cross (2026-10-20: a cross from the front was read as a drive). Not which half of the wall the aim
        /// is on - a cross from near the front meets the front wall on the ball's own half - but which of the two lines,
        /// the straight one and the cross one from this ball, the aim is nearer. A ball near the middle: never a cross
        /// (the aim's side is the corner).
        /// </summary>
        public static bool IsCross(double bx, double bz, string shot, double tx)
        {
            if (System.Math.Abs(bx) <= GameTuning.Hit.LineMidBall) return false;
            int side = bx > 0 ? 1 : -1;
            double straightX = LineX(bx, bz, shot, true, side, -1), crossX = LineX(bx, bz, shot, false, side, -1);
            return System.Math.Abs(tx - crossX) < System.Math.Abs(tx - straightX);
        }

        static double LineX(double bx, double bz, string shot, bool straight, int side, double landing)
        {
            double off, depth;
            switch (shot)
            {
                case "kill": off = straight ? GameTuning.Hit.KillStraightOff : GameTuning.Hit.KillCrossOff; depth = straight ? GameTuning.Hit.KillStraightDepth : GameTuning.Hit.KillCrossDepth; break;
                case "lob": off = straight ? GameTuning.Hit.LobStraightOff : GameTuning.Hit.LobCrossOff; depth = straight ? GameTuning.Hit.LobStraightDepth : GameTuning.Hit.LobCrossDepth; break;
                case "drop": off = straight ? GameTuning.Hit.DropStraightOff : GameTuning.Hit.DropCrossOff; depth = straight ? GameTuning.Hit.DropStraightDepth : GameTuning.Hit.DropCrossDepth; break;
                default: off = straight ? GameTuning.Hit.DriveStraightOff : GameTuning.Hit.DriveCrossOff; depth = straight ? GameTuning.Hit.DriveStraightDepth : GameTuning.Hit.DriveCrossDepth; break;
            }
            if (landing > 0) depth = landing;
            int to = straight ? side : -side;
            double lx = to * (CourtDimensions.HALF_W - off);
            double z = System.Math.Max(0.2, bz);
            return bx + (lx - bx) * z / (z + depth);
        }

        /// <summary>Where a shot aimed at tx really goes with this line: part of the way from the aim to the line - most of
        /// it with Assisted, a little without (LineSnapAssisted / LineSnapNonAssisted). The ring's dot uses it too.</summary>
        public static double PullToLine(double tx, double line, bool assisted)
        {
            return tx + (line - tx) * (assisted ? GameTuning.Hit.LineSnapAssisted : GameTuning.Hit.LineSnapNonAssisted);
        }

        /// <summary>The shot line from here, checked in flight: the line is aimed (ShotLine), flown once through the real
        /// ball physics, and aimed again with where this shot really lands - a drive struck from near the front lands
        /// deeper than one from the back, and a cross from there must be aimed wider to reach the far side.</summary>
        public double LineFor(BallState ball, List<Surface> surfaces, string shot, double tx, double ty, double speed)
        {
            double x = ShotLine(ball.Pos.X, ball.Pos.Z, shot, tx);
            if (shot == "lob") return x;                                   // a lob's arc is its own search
            bool keep = _dip;
            _dip = PhysicsConfig.Current.ArcadeHit.FrontWallRiseKeep < 1 && (shot == "drive" || shot == "kill");   // as it will fly
            Solve(ball, surfaces, x, ty, 0, 0, speed);
            double z = FirstBounceZ(ball, surfaces);
            _dip = keep;
            return z > 0.5 ? ShotLine(ball.Pos.X, ball.Pos.Z, shot, tx, z) : x;
        }

        /// <summary>Where the launch in hand (_vel) first bounces after the front wall (z), or -1.</summary>
        double FirstBounceZ(BallState ball, List<Surface> surfaces)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return front && e.SurfaceClass == "floor" ? e.Point.Z : -1;
                }
            }
            return -1;
        }

        /// <summary>
        /// Assisted: a straight drive (IsCross says straight) that would touch its side wall before the first bounce is
        /// aimed a little further in, 0.1 m at a time, up to StraightMaxShift - a safety, not a perfect line.
        /// </summary>
        public double StraightAssist(BallState ball, List<Surface> surfaces, double tx, double ty, double speed, bool kill = false)
        {
            int side = ball.Pos.X > 0 ? 1 : -1;
            if (System.Math.Abs(ball.Pos.X) < GameTuning.Assist.StraightMinSide || tx * side <= 0
                || IsCross(ball.Pos.X, ball.Pos.Z, kill ? "kill" : "drive", tx)) return tx;   // not a straight shot
            double x = tx;
            int steps = (int)System.Math.Round(GameTuning.Assist.StraightMaxShift / GameTuning.Assist.StraightStep);
            for (int k = 0; k <= steps; k++)
            {
                x = tx - side * GameTuning.Assist.StraightStep * k;
                if (x * side < GameTuning.Assist.StraightInner) break;
                Solve(ball, surfaces, x, ty, 0, 0, speed);
                if (!ClipsSide(ball, surfaces, side, kill ? 2 : 1)) return x;
            }
            return x;
        }

        /// <summary>Does this launch touch side wall <paramref name="side"/> before bounce number <paramref name="bounces"/>
        /// after the front wall (1: in the air - a drive; 2: a kill, which bounces early, along its whole line)?</summary>
        public bool ClipsSide(BallState ball, List<Surface> surfaces, int side, int bounces = 1)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            string id = side > 0 ? "sideWallRight" : "sideWallLeft";
            bool front = false;
            int floors = 0;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceId == id) return true;
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (e.SurfaceClass == "tin" || (!front && e.SurfaceClass == "floor")) return false;
                    else if (e.SurfaceClass == "floor" && ++floors >= bounces) return false;
                    else if (front && e.SurfaceClass == "backWall") return false;
                }
            }
            return false;
        }

        /// <summary>
        /// The lob serve: exactly onto the ring (tx, ty) on the front wall, on the arc that carries it deepest into the
        /// receiver's back quarter while staying in. Returns the pace.
        /// </summary>
        public double SolveLobServe(BallState ball, List<Surface> surfaces, double tx, double ty, int serveSide)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double g = PhysicsConfig.Current.World.Gravity;
            double dx = tx - ball.Pos.X, dz = ball.Pos.Z, d = System.Math.Max(0.5, System.Math.Sqrt(dx * dx + dz * dz));
            double dy = ty - ball.Pos.Y;
            double best = double.MaxValue, bestSpeed = c.ServeSpeed;
            bool any = false;
            for (int k = 0; k < 9; k++)
            {
                double up = 0.5 + k * c.LobRise;
                double t = (-up + System.Math.Sqrt(System.Math.Max(0, up * up + 2 * g * dy))) / g;
                if (t < 0.12) t = 0.12;
                double h = d / t, vy = up + g * t;
                double speed = System.Math.Min(c.LobMaxSpeed, System.Math.Sqrt(h * h + vy * vy) * 1.08);
                _lift = vy / h * d - dy;
                Solve(ball, surfaces, tx, ty, 0, 0, speed);
                _lift = 0;
                double score = ServeScore(ball, surfaces, serveSide);
                if (score < best) { best = score; bestSpeed = speed; _good.CopyFrom(_vel); any = true; }
            }
            if (any) _vel.CopyFrom(_good);
            return bestSpeed;
        }

        /// <summary>
        /// How good this serve launch is (lower = better): a legal landing in the receiver's back quarter first (anything
        /// else costs 100+), then as deep as ServeDepth and toward the side wall. Out of court or the back wall first: 1000.
        /// </summary>
        double ServeScore(BallState ball, List<Surface> surfaces, int serveSide)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            bool front = false;
            for (int i = 0; i < 600; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                if (_scratch.Pos.Y > CourtDimensions.CEILING_H - 0.3) return 1000;
                foreach (var e in _events)
                {
                    if (RallyReferee.OutReason(e) != null) return 1000;
                    if (e.SurfaceClass == "frontWall")
                    {
                        if (e.Point.Y > CourtDimensions.FRONT_WALL_OUT_H - 0.25) return 1000;
                        front = true;
                    }
                    else if (!front) { if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return 1000; }
                    else if (e.SurfaceClass == "backWall") return 1000;
                    else if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.25) return 1000;
                    else if (e.SurfaceClass == "floor")
                    {
                        double want = -serveSide * 1.8;
                        // Legal with a margin (the real flight runs at a finer step than this check).
                        bool legal = e.Point.X * serveSide < -0.45 && e.Point.Z > CourtDimensions.SHORT_LINE_Z + 0.5;
                        return (legal ? 0 : 100) + System.Math.Abs(e.Point.Z - c.ServeDepth) + 0.4 * System.Math.Abs(e.Point.X - want);
                    }
                }
            }
            return 1000;
        }

        /// <summary>Would this launch go out of court (lines, ceiling) before its first bounce after the front wall?</summary>
        bool GoesOut(BallState ball, List<Surface> surfaces)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (RallyReferee.OutReason(e) != null) return true;
                    if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.2) return true;   // with a margin
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (front && e.SurfaceClass == "floor") return false;
                }
            }
            return false;
        }

        public double SolveLob(BallState ball, List<Surface> surfaces, double tx, double ty)
        {
            return SolveArc(ball, surfaces, tx, ty, PhysicsConfig.Current.ArcadeHit.LobDepth);
        }

        /// <summary>
        /// A high arc (lob, lob serve) through front-wall x = tx, landing near <paramref name="depth"/>: tries wall heights from
        /// <paramref name="minWallY"/> up to LobWallMax and, at each, the arcs from barely rising to steeply; keeps the one landing
        /// nearest the depth that stays in court with a margin (never an out lob - with no arc in, the lowest, softest one).
        /// </summary>
        public double SolveArc(BallState ball, List<Surface> surfaces, double tx, double minWallY, double depth)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double lo = System.Math.Min(minWallY, c.LobWallMax), bestScore = double.MaxValue, bestSpeed = 0;
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                double wallY = lo + (c.LobWallMax - lo) * i / 3.0;
                double speed = BestLobArc(ball, surfaces, tx, wallY, depth, out bool found, out double score);
                if (found && score < bestScore - 0.05) { bestScore = score; bestSpeed = speed; _best.CopyFrom(_vel); any = true; }
            }
            if (any) { _vel.CopyFrom(_best); return bestSpeed; }
            for (int drop = 1; drop <= 4; drop++)
            {
                double wallY = System.Math.Max(CourtDimensions.SERVICE_LINE_H + 0.4, lo - 0.3 * drop);
                double speed = BestLobArc(ball, surfaces, tx, wallY, depth, out bool found, out _);
                if (found) return speed;
            }
            return BestLobArc(ball, surfaces, tx, CourtDimensions.SERVICE_LINE_H + 0.4, depth, out _, out _);
        }

        /// <summary>
        /// The arcs for a lob (or lob serve) to (tx, wallY) on the front wall, from barely rising at the wall to steeply:
        /// keeps the one whose first bounce lands nearest <paramref name="depth"/> while staying in court. found = false
        /// when none stays in (the launch left is then the softest arc).
        /// </summary>
        double BestLobArc(BallState ball, List<Surface> surfaces, double tx, double wallY, double depth, out bool found, out double bestScore)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double g = PhysicsConfig.Current.World.Gravity;
            double dx = tx - ball.Pos.X, dz = ball.Pos.Z, d = System.Math.Max(0.5, System.Math.Sqrt(dx * dx + dz * dz));
            double dy = wallY - ball.Pos.Y;
            double best = double.MaxValue, bestSpeed = 0, softest = 0;
            found = false;
            for (int k = 0; k < 9; k++)
            {
                double up = 0.5 + k * c.LobRise;                     // rising this fast at the wall
                double t = (-up + System.Math.Sqrt(System.Math.Max(0, up * up + 2 * g * dy))) / g;
                if (t < 0.12) t = 0.12;
                double h = d / t, vy = up + g * t;
                double speed = System.Math.Min(c.LobMaxSpeed, System.Math.Sqrt(h * h + vy * vy) * 1.08);
                _lift = vy / h * d - dy;
                Solve(ball, surfaces, tx, wallY, 0, 0, speed);
                _lift = 0;
                if (k == 0) { softest = speed; _scratchVel.CopyFrom(_vel); }
                double land = LobLanding(ball, surfaces);
                if (land < 0) continue;
                double score = System.Math.Abs(land - depth);
                if (score < best) { best = score; bestSpeed = speed; _good.CopyFrom(_vel); found = true; }
            }
            bestScore = best;
            if (found) { _vel.CopyFrom(_good); return bestSpeed; }
            _vel.CopyFrom(_scratchVel);
            return softest;
        }

        /// <summary>Where this launch first bounces after the front wall (z), or -1 if it would go out / hit the ceiling / the back wall high on the full.</summary>
        /// <summary>The side-wall out line: 4.57 m at the front wall sloping to 2.13 m at the back.</summary>
        static double SideOutHeight(double z)
        {
            double f = Clamp(z / CourtDimensions.LENGTH, 0, 1);
            return CourtDimensions.FRONT_WALL_OUT_H + (CourtDimensions.BACK_WALL_OUT_H - CourtDimensions.FRONT_WALL_OUT_H) * f;
        }

        double LobLanding(BallState ball, List<Surface> surfaces)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            bool front = false;
            for (int i = 0; i < 480; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                if (_scratch.Pos.Y > CourtDimensions.CEILING_H - 0.3) return -1;
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall")
                    {
                        if (e.Point.Y > CourtDimensions.FRONT_WALL_OUT_H - 0.25) return -1;
                        front = true;
                    }
                    else if (!front) { if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") return -1; }
                    else if (e.SurfaceClass == "backWall") return -1;                      // floor first, like every other shot
                    else if (e.SurfaceClass == "outBackstop" || e.SurfaceClass == "ceiling") return -1;
                    else if (e.SurfaceClass == "sideWall" && e.Point.Y > SideOutHeight(e.Point.Z) - 0.25) return -1;   // a safe margin under the line
                    else if (e.SurfaceClass == "floor") return e.Point.Z;
                }
            }
            return -1;
        }

        public void Solve(BallState ball, List<Surface> surfaces, double tx, double ty, double tz, int wall, double speed)
        {
            _aimPoint.X = tx; _aimPoint.Y = ty + _lift; _aimPoint.Z = tz;
            for (int it = 0; it < 4; it++)
            {
                double dx = _aimPoint.X - ball.Pos.X, dy = _aimPoint.Y - ball.Pos.Y, dz = _aimPoint.Z - ball.Pos.Z;
                double d = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < 1e-6) d = 1e-6;
                _vel.X = dx / d * speed; _vel.Y = dy / d * speed; _vel.Z = dz / d * speed;

                if (!Fly(ball, surfaces, wall, out double hu, out double hv)) break;
                double wantU = wall == 0 ? tx : tz;
                double eu = wantU - hu, ev = ty - hv;
                if (wall == 0) _aimPoint.X += eu; else _aimPoint.Z += eu;
                _aimPoint.Y += ev;
                if (eu * eu + ev * ev < 0.03 * 0.03) break;
            }
            double dx2 = _aimPoint.X - ball.Pos.X, dy2 = _aimPoint.Y - ball.Pos.Y, dz2 = _aimPoint.Z - ball.Pos.Z;
            double d2 = System.Math.Max(1e-6, System.Math.Sqrt(dx2 * dx2 + dy2 * dy2 + dz2 * dz2));
            _vel.X = dx2 / d2 * speed; _vel.Y = dy2 / d2 * speed; _vel.Z = dz2 / d2 * speed;
        }

        /// <summary>
        /// The side wall a boast is played off (+1 right, -1 left): the one nearer the ball; from the middle,
        /// the one away from the target so the ball comes across.
        /// </summary>
        public static int BoastSide(double ballX, double targetX)
        {
            if (System.Math.Abs(ballX) >= 0.4) return ballX > 0 ? 1 : -1;
            return targetX > 0 ? -1 : 1;
        }

        /// <summary>
        /// The boast for a ball at <paramref name="ballX"/> aimed at <paramref name="aimX"/>: the side wall it goes off
        /// (returned) and where it lands on the front wall - the aim, kept on the other half of the wall (a boast
        /// comes across the court).
        /// </summary>
        public static int BoastTarget(double ballX, double aimX, out double landX)
        {
            var c = PhysicsConfig.Current.ArcadeHit;
            double cross = c != null ? c.BoastCross : 0.3;
            int side = BoastSide(ballX, aimX);
            double far = CourtDimensions.HALF_W - 0.15;
            landX = side > 0 ? Clamp(aimX, -far, -cross) : Clamp(aimX, cross, far);
            return side;
        }

        /// <summary>
        /// A boast: off side wall <paramref name="side"/>, then onto the front wall at (tx, ty). Aims at the target's
        /// mirror image across that side wall, then corrects on the real flight. False when the ball would not
        /// reach the side wall first (then play it as a drive). The launch is left in the stroke's velocity.
        /// </summary>
        public bool SolveBoast(BallState ball, List<Surface> surfaces, double tx, double ty, int side, ref double speed)
        {
            // The side wall takes a lot of pace off: too soft to get round from here, play it a little firmer.
            for (int k = 0; k < 5; k++)
            {
                if (SolveBoastAt(ball, surfaces, tx, ty, side, speed)) return true;
                speed *= 1.2;
            }
            return false;
        }

        /// <summary>The boast at exactly this pace: true when it goes off the side wall and lands within 0.2 m of the aim.</summary>
        public bool SolveBoastAt(BallState ball, List<Surface> surfaces, double tx, double ty, int side, double speed)
        {
            // Work in the court unfolded across the side wall: there the boast is a straight shot at the mirror image.
            double wx = side * CourtDimensions.HALF_W, want = 2 * wx - tx;
            _aimPoint.X = want; _aimPoint.Y = ty; _aimPoint.Z = 0;
            for (int it = 0; it < 8; it++)
            {
                AimAt(ball, speed);
                int r = FlyBoast(ball, surfaces, side, out double hu, out double hv);
                if (r < 0) break;
                double eu = want - hu, ev = ty - hv;
                _aimPoint.X += eu;
                _aimPoint.Y += ev;
                if (r == 2 && eu * eu + ev * ev < 0.03 * 0.03) break;
            }
            AimAt(ball, speed);
            if (FlyBoast(ball, surfaces, side, out double fu, out double fv) != 2) return false;
            return (want - fu) * (want - fu) + (ty - fv) * (ty - fv) < 0.2 * 0.2;
        }

        /// <summary>The boast's launch (in the stroke velocity) for tests and tools.</summary>
        public V3 LaunchVelocity { get { return _vel; } }

        void AimAt(BallState ball, double speed)
        {
            double dx = _aimPoint.X - ball.Pos.X, dy = _aimPoint.Y - ball.Pos.Y, dz = _aimPoint.Z - ball.Pos.Z;
            double d = System.Math.Max(1e-6, System.Math.Sqrt(dx * dx + dy * dy + dz * dz));
            _vel.X = dx / d * speed; _vel.Y = dy / d * speed; _vel.Z = dz / d * speed;
        }

        /// <summary>
        /// Fly a boast. 2 = off side wall <paramref name="side"/> and then onto the front wall; 1 = it came up short
        /// (floor / tin) or reached the front wall without the side wall; -1 = something else. (u, v) is where it
        /// got to in the unfolded court (x mirrored once it has come off the side wall; v = 0 for the floor).
        /// </summary>
        int FlyBoast(BallState ball, List<Surface> surfaces, int side, out double u, out double v)
        {
            u = 0; v = 0;
            double wx = side * CourtDimensions.HALF_W;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            string sideId = side > 0 ? "sideWallRight" : "sideWallLeft";
            bool offSide = false;
            for (int i = 0; i < 240; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceId == sideId) { offSide = true; continue; }
                    double ux = offSide ? 2 * wx - e.Point.X : e.Point.X;
                    if (e.SurfaceClass == "frontWall") { u = ux; v = e.Point.Y; return offSide ? 2 : 1; }
                    if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin")
                    {
                        // Short: as if it had reached the front wall at floor level (aim higher), along the same line.
                        u = ux; v = e.SurfaceClass == "tin" ? e.Point.Y - 0.3 : -0.3 - 0.1 * e.Point.Z;
                        return 1;
                    }
                    if (e.SurfaceClass == "sideWall" || e.SurfaceClass == "backWall") return -1;
                }
            }
            return -1;
        }

        /// <summary>How high the solved launch meets the back wall on the full (before it bounces twice), or -1.</summary>
        double BackWallHeight(BallState ball, List<Surface> surfaces)
        {
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            int floors = 0;
            bool front = false;
            for (int i = 0; i < 400; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (e.SurfaceClass == "frontWall") front = true;
                    else if (e.SurfaceClass == "floor" && front) { if (++floors >= 1) return -1; }
                    else if (front && (e.SurfaceClass == "backWall" || e.SurfaceClass == "outBackstop" || e.SurfaceClass == "ceiling")) return e.SurfaceClass == "backWall" ? e.Point.Y : 9.0;
                }
            }
            return -1;
        }

        /// <summary>Where this launch first reaches the target wall: front wall (x, height) or side wall (z, height).</summary>
        bool Fly(BallState ball, List<Surface> surfaces, int wall, out double u, out double v)
        {
            u = 0; v = 0;
            _scratch.Reset(ball.Pos, _vel, null, ball.Temperature); _scratch.DipOffFrontWall = _dip;
            string sideId = wall > 0 ? "sideWallRight" : "sideWallLeft";
            for (int i = 0; i < 200; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, 1.0 / 120, _events);
                foreach (var e in _events)
                {
                    if (wall == 0 && e.SurfaceClass == "frontWall") { u = e.Point.X; v = e.Point.Y; return true; }
                    if (wall != 0 && e.SurfaceId == sideId) { u = e.Point.Z; v = e.Point.Y; return true; }
                    if (wall != 0 && e.SurfaceClass == "frontWall") { u = -(CourtDimensions.HALF_W - wall * e.Point.X); v = e.Point.Y; return true; }
                    if (e.SurfaceClass == "floor" || e.SurfaceClass == "tin") { u = wall == 0 ? e.Point.X : e.Point.Z; v = 0; return true; }
                }
            }
            return false;
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
