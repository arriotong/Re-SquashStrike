// ===========================================================
// Assets/SquashSim/Player/SwingController.cs
// Port of lab/player/SwingController.js. CONTROL SYSTEM V5.
//
//   TAP  (released before tapThreshold) -> QUICK, auto-timed
//   HOLD (still down past tapThreshold) -> PREPARING a PREPARED stroke;
//        release = commit now (small timing correction); still holding
//        at the ideal moment fires on time.
//
// ONE buffered action; presses never queue. The swing PATH is committed
// to the direction the body showed at swing start (RacketIntentSolver).
//
// The control flow (including the PREPARING block running on after a
// stroke switch) is kept exactly as in the lab. Parity depends on it.
// ===========================================================

namespace SquashSim
{
    public static class SwingStates
    {
        public const string Idle = "idle";
        public const string Armed = "armed";          // Quick stroke waiting for its moment
        public const string Preparing = "preparing";  // button held: Prepared stroke building
        public const string Scheduled = "scheduled";  // committed, counting down to swing start
        public const string Swinging = "swinging";
        public const string Recovering = "recovering";
    }

    public sealed class SwingSummary
    {
        public string Stroke, Mode, Technique;
        public bool Contacted, Rushed;
        public int Side;
    }

    public sealed class SwingController
    {
        public string State = SwingStates.Idle;

        public string Stroke = "hit";        // "hit" | "touch"
        public string Mode;                  // "quick" | "prepared" | null
        public double HoldSeconds;
        public double PrepQuality;
        public double ArmedAge;
        public double? ExpectedStartIn;      // seconds until the ideal swing start

        public string Buffered;              // the single remembered stroke, or null
        public double BufferedAge;

        public double StartIn;
        public SwingPlan Plan;
        public bool Rushed;
        public double TimingCorrection;

        public double SwingTime;
        public double Duration;
        public double Phase;
        public double TotalArc;
        public double StartAngle;
        public double ContactAngle;
        public double ContactHeight = 1.0;
        public readonly V2 Center = new V2();    // swing pivot, set at commit
        public double Reach = 0.76;
        public int Side = 1;
        public double PathYaw;
        public bool HasContacted;
        /// <summary>How far the body has lunged toward the contact in this swing (LungeStep, game only).</summary>
        public double LungeUsed;
        public double Cooldown;

        public SwingSummary LastSwing;

        public void Reset()
        {
            State = SwingStates.Idle;
            Stroke = "hit"; Mode = null; HoldSeconds = 0; PrepQuality = 0; ArmedAge = 0; ExpectedStartIn = null;
            Buffered = null; BufferedAge = 0;
            StartIn = 0; Plan = null; Rushed = false; TimingCorrection = 0;
            SwingTime = 0; Duration = 0; Phase = 0; TotalArc = 0; StartAngle = 0; ContactAngle = 0;
            ContactHeight = 1.0; Center.X = 0; Center.Z = 0; Reach = 0.76; Side = 1; PathYaw = 0;
            HasContacted = false; Cooldown = 0;
            LastSwing = null;
        }

        // The plan's head speed/duration/face pitch/path rise, or the neutral
        // profile's when no plan exists yet (lab: `sw.plan || swing.neutral`).
        public double PlanHeadSpeed { get { return Plan != null ? Plan.HeadSpeed : PhysicsConfig.Current.Racket.Swing.Neutral.HeadSpeed; } }
        public double PlanDuration { get { return Plan != null ? Plan.Duration : PhysicsConfig.Current.Racket.Swing.Neutral.Duration; } }
        public double PlanFacePitch { get { return Plan != null ? Plan.FacePitch : PhysicsConfig.Current.Racket.Swing.Neutral.FacePitch; } }
        public double PlanPathRise { get { return Plan != null ? Plan.PathRise : PhysicsConfig.Current.Racket.Swing.Neutral.PathRise; } }
    }

    public struct SwingCtx
    {
        public double Quality;
        public string ReachState;
    }

    public static class Swing
    {
        static double ArcFor(double headSpeed, double duration, double reach)
        {
            return (headSpeed * duration) / (2 * DMath.Max(0.05, reach));
        }

        static SwingPlan MakePlan(SwingController sw, AimState aim, TestPlayer player, Prediction pred, SwingCtx ctx, bool rushed)
        {
            return SwingPlanner.Plan(sw.Stroke, aim, player.SwingSide >= 0 ? 1 : -1, new SwingContext
            {
                PlayerPace = player.PaceScale,
                Mode = sw.Mode,
                PrepQuality = sw.PrepQuality,
                Quality = ctx.Quality,
                ReachState = ctx.ReachState,
                IsVolley = pred.IsVolley,
                Rushed = rushed
            });
        }

        /// <summary>Latch the stroke. Path = what the body shows NOW; the arc passes
        /// through the ball a quarter turn round from the path, on the hitting side.</summary>
        static readonly Prediction _arcadePred = new Prediction();

        /// <summary>
        /// The simple hitting system (ArcadeStroke): the ball was hit at <paramref name="contact"/> - lay the swing
        /// through that point and put it at its contact moment, so the stroke (and the drawn racket) plays through it.
        /// </summary>
        public static void ArcadeCommit(SwingController sw, TestPlayerController ctrl, V3 contact)
        {
            _arcadePred.Valid = true;
            _arcadePred.Time = 0;
            _arcadePred.Point.CopyFrom(contact);
            var ctx = new SwingCtx { Quality = ctrl.Assist.Quality, ReachState = ctrl.Assist.ReachState };
            Commit(sw, MakePlan(sw, ctrl.Aim, ctrl.Player, _arcadePred, ctx, false), ctrl.Player, _arcadePred, ctrl.IntentSolver);
            sw.SwingTime = sw.Duration * 0.5;
            sw.Phase = 0.5;
            sw.HasContacted = true;
        }

        static void Commit(SwingController sw, SwingPlan plan, TestPlayer player, Prediction pred, RacketIntent ri)
        {
            var cfg = PhysicsConfig.Current.Racket;
            sw.Plan = plan;
            sw.Duration = plan.Duration;
            sw.SwingTime = 0;
            sw.Phase = 0;
            sw.HasContacted = false;
            sw.LungeUsed = 0;
            sw.Side = player.SwingSide >= 0 ? 1 : -1;
            sw.PathYaw = ri.BodyYaw;

            double cx = pred.Valid ? pred.Point.X : player.Pos.X + sw.Side * cfg.Reach;
            double cz = pred.Valid ? pred.Point.Z : player.Pos.Z;
            double cy = pred.Valid ? pred.Point.Y : cfg.RestHeight;

            double needed = DMath.Hypot(cx - player.Pos.X, cz - player.Pos.Z);
            sw.Reach = DMath.Max(cfg.MinReach, DMath.Min(PhysicsConfig.Current.Reach.Stretch, needed));

            sw.ContactAngle = sw.PathYaw + sw.Side * (DMath.PI / 2);
            sw.Center.X = cx - DMath.Sin(sw.ContactAngle) * sw.Reach;
            sw.Center.Z = cz + DMath.Cos(sw.ContactAngle) * sw.Reach;

            sw.TotalArc = ArcFor(plan.HeadSpeed, plan.Duration, sw.Reach);
            sw.StartAngle = sw.ContactAngle + sw.Side * sw.TotalArc * 0.5;
            sw.ContactHeight = DMath.Max(cfg.MinContactHeight, DMath.Min(cfg.MaxContactHeight, cy));

            sw.State = SwingStates.Swinging;
        }

        static void Schedule(SwingController sw, SwingPlan plan, TestPlayer player, Prediction pred, RacketIntent ri, double startIn)
        {
            sw.Plan = plan;
            if (startIn <= 0) { Commit(sw, plan, player, pred, ri); return; }
            sw.StartIn = startIn;
            sw.State = SwingStates.Scheduled;
        }

        static double? IdealStartFor(SwingController sw, AimState aim, TestPlayer player, Prediction pred, SwingCtx ctx)
        {
            if (!pred.Valid) return null;
            SwingPlan plan = MakePlan(sw, aim, player, pred, ctx, false);
            return pred.Time - plan.Duration * 0.5;
        }

        static void BeginStroke(SwingController sw, string stroke, bool stillHeld)
        {
            sw.Stroke = stroke;
            sw.HoldSeconds = 0;
            sw.ArmedAge = 0;
            sw.TimingCorrection = 0;
            sw.Rushed = false;
            if (stillHeld)
            {
                sw.Mode = null;
                sw.State = SwingStates.Preparing;
            }
            else
            {
                sw.Mode = "quick";
                sw.PrepQuality = PhysicsConfig.Current.Strokes.Quick.PrepQuality;
                sw.State = SwingStates.Armed;
            }
        }

        static StrokeButton HeldOf(InputIntent intent, string s) { return s == "touch" ? intent.Touch : intent.Hit; }

        /// <summary>Advance the swing on the fixed timestep.</summary>
        public static SwingController Step(SwingController sw, InputIntent intent, AimState aim, TestPlayer player,
                                           Prediction pred, SwingCtx ctx, double dt, RacketIntent ri)
        {
            var cfg = PhysicsConfig.Current.Racket;
            var timing = PhysicsConfig.Current.InputTiming;
            var strokesCfg = PhysicsConfig.Current.Strokes;

            if (sw.Cooldown > 0) sw.Cooldown = DMath.Max(0, sw.Cooldown - dt);

            string pressed = intent.Hit.Pressed ? "hit" : (intent.Touch.Pressed ? "touch" : null);

            bool busy = sw.State == SwingStates.Swinging || sw.State == SwingStates.Recovering ||
                        sw.State == SwingStates.Scheduled;
            if (pressed != null && busy) { sw.Buffered = pressed; sw.BufferedAge = 0; }
            if (sw.Buffered != null)
            {
                sw.BufferedAge += dt;
                if (sw.BufferedAge > timing.BufferSeconds) sw.Buffered = null;
            }

            switch (sw.State)
            {
                case SwingStates.Swinging:
                    sw.SwingTime += dt;
                    sw.Phase = sw.Duration > 0 ? DMath.Min(1, sw.SwingTime / sw.Duration) : 1;
                    sw.ExpectedStartIn = null;
                    if (sw.SwingTime >= sw.Duration)
                    {
                        sw.LastSwing = new SwingSummary
                        {
                            Stroke = sw.Stroke, Mode = sw.Mode,
                            Technique = sw.Plan != null ? sw.Plan.Technique : null,
                            Contacted = sw.HasContacted, Rushed = sw.Rushed, Side = sw.Side
                        };
                        sw.State = SwingStates.Recovering;
                        sw.Cooldown = cfg.CooldownSeconds;
                        sw.Phase = 0;
                    }
                    return sw;

                case SwingStates.Scheduled:
                    sw.StartIn -= dt;
                    // Game: stay on the LATEST prediction until the swing starts - the athlete is usually still
                    // moving, and the contact (the ball's closest approach to them) moves with them.
                    if (timing.RetimeScheduled && pred != null && pred.Valid)
                        sw.StartIn = pred.Time - (sw.Plan != null ? sw.Plan.Duration : 0) * 0.5;
                    sw.ExpectedStartIn = sw.StartIn;
                    if (sw.StartIn <= 0)
                    {
                        Commit(sw, MakePlan(sw, aim, player, pred, ctx, sw.Rushed), player, pred, ri);
                    }
                    return sw;

                case SwingStates.Recovering:
                    if (sw.Cooldown > 0) return sw;
                    sw.State = SwingStates.Idle;
                    break;   // fall through into IDLE handling below
            }

            // ---------------- IDLE ----------------
            if (sw.State == SwingStates.Idle)
            {
                sw.ExpectedStartIn = null;
                string start = pressed ?? sw.Buffered;
                if (start == null) return sw;
                bool stillHeld = HeldOf(intent, start).Held;
                sw.Buffered = null;
                BeginStroke(sw, start, stillHeld);
            }

            // ---------------- PREPARING (button held) ----------------
            if (sw.State == SwingStates.Preparing)
            {
                if (pressed != null && pressed != sw.Stroke)
                {
                    BeginStroke(sw, pressed, HeldOf(intent, pressed).Held);
                }
                StrokeButton btn = HeldOf(intent, sw.Stroke);
                sw.HoldSeconds += dt;

                double? idealStart = IdealStartFor(sw, aim, player, pred, ctx);
                sw.ExpectedStartIn = idealStart;

                bool releasedNow = !btn.Held;
                if (releasedNow && sw.HoldSeconds < strokesCfg.TapThresholdSeconds)
                {
                    sw.Mode = "quick";
                    sw.PrepQuality = strokesCfg.Quick.PrepQuality;
                    sw.State = SwingStates.Armed;
                    sw.ArmedAge = 0;
                }
                else
                {
                    sw.Mode = "prepared";
                    var p = strokesCfg.Prepared;
                    double t = DMath.Min(1, sw.HoldSeconds / strokesCfg.PrepFullSeconds);
                    sw.PrepQuality = p.MinPrepQuality + (p.MaxPrepQuality - p.MinPrepQuality) * t;

                    if (releasedNow)
                    {
                        if (!idealStart.HasValue && timing.NoAirSwings)
                        {
                            // Game: no ball in reach yet - keep the prepared stroke ready; it swings when the ball comes.
                            sw.State = SwingStates.Armed;
                            sw.ArmedAge = 0;
                        }
                        else if (!idealStart.HasValue)
                        {
                            sw.Rushed = false;
                            Commit(sw, MakePlan(sw, aim, player, pred, ctx, false), player, pred, ri);
                        }
                        else if (idealStart.Value > 0)
                        {
                            double delay = DMath.Min(idealStart.Value, timing.PreparedTimingAssist);
                            sw.TimingCorrection = delay;
                            sw.Rushed = false;
                            Schedule(sw, MakePlan(sw, aim, player, pred, ctx, false), player, pred, ri, delay);
                        }
                        else
                        {
                            sw.Rushed = idealStart.Value < 0;
                            Commit(sw, MakePlan(sw, aim, player, pred, ctx, sw.Rushed), player, pred, ri);
                        }
                    }
                    else if (idealStart.HasValue && idealStart.Value <= 0)
                    {
                        sw.Rushed = false;
                        Commit(sw, MakePlan(sw, aim, player, pred, ctx, false), player, pred, ri);
                    }
                }
                if (sw.State != SwingStates.Armed) return sw;
            }

            // ---------------- ARMED (Quick, auto-timed) ----------------
            if (sw.State == SwingStates.Armed)
            {
                if (pressed != null && pressed != sw.Stroke) BeginStroke(sw, pressed, HeldOf(intent, pressed).Held);
                if (sw.State != SwingStates.Armed) return sw;

                sw.ArmedAge += dt;
                double? idealStart = IdealStartFor(sw, aim, player, pred, ctx);
                sw.ExpectedStartIn = idealStart;

                if (!idealStart.HasValue || idealStart.Value > timing.MaxLeadSeconds)
                {
                    if (sw.ArmedAge > timing.BufferSeconds) { sw.State = SwingStates.Idle; sw.Mode = null; }
                    return sw;
                }
                if (idealStart.Value < -timing.LateToleranceSeconds)
                {
                    if (sw.ArmedAge > timing.BufferSeconds) { sw.State = SwingStates.Idle; sw.Mode = null; }
                    return sw;
                }
                sw.Rushed = idealStart.Value < 0;
                sw.TimingCorrection = DMath.Max(0, idealStart.Value);
                Schedule(sw, MakePlan(sw, aim, player, pred, ctx, sw.Rushed), player, pred, ri, idealStart.Value);
            }
            return sw;
        }

        /// <summary>Is the racket in the part of the stroke that can hit?</summary>
        public static bool InContactWindow(SwingController sw)
        {
            if (sw.State != SwingStates.Swinging) return false;
            var cfg = PhysicsConfig.Current.Racket;
            var assist = PhysicsConfig.Current.Assist;
            double halfWindow = cfg.ContactWindow * 0.5;
            if (assist.Enabled && sw.Duration > 0)
            {
                halfWindow += (assist.TimingWindow / sw.Duration) * 0.5;
            }
            return System.Math.Abs(sw.Phase - 0.5) <= halfWindow;
        }

        /// <summary>Angular progress through the stroke, eased so speed peaks at contact.</summary>
        public static double SwingAngleAt(SwingController sw, double phase)
        {
            double p = DMath.Max(0, DMath.Min(1, phase));
            double theta = sw.TotalArc * (p - DMath.Sin(2 * DMath.PI * p) / (2 * DMath.PI));
            return sw.StartAngle - sw.Side * theta;
        }

        /// <summary>Where the racket waits during preparation: aligned with the BODY's direction.</summary>
        public static double BackswingAngle(SwingController sw, int side, double bodyYaw)
        {
            double reach = PhysicsConfig.Current.Racket.Reach;
            double pq = sw.PrepQuality != 0 ? sw.PrepQuality : 0.5;   // JS: `sw.prepQuality || 0.5`
            double prep = DMath.Max(0.3, pq);
            double arc = ArcFor(sw.PlanHeadSpeed, sw.PlanDuration, reach) * prep;
            return bodyYaw + side * (DMath.PI / 2) + side * arc * 0.5;
        }
    }
}
