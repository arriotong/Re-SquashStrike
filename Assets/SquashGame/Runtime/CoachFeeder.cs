// ===========================================================
// Assets/SquashGame/Runtime/CoachFeeder.cs
// P2-M2: the practice court's feeder, run by the Coach panel - takes
// over from the lab launcher's own repeat. Feed type, side (forehand /
// backhand / alternate / random), speed, auto or on demand, interval,
// a warm or cold ball, slow motion, a long "where that went" trail.
//
// Feeds aim at a point on the front wall (like the arcade's feeder),
// high and firm enough to come back past the short line; the boast
// comes off the side wall first, the back-wall feed off the back.
// ===========================================================

using SquashGame.Profile;
using SquashSim;
using SquashSim.Lab;
using UnityEngine;

namespace SquashGame
{
    public sealed class CoachFeeder : MonoBehaviour
    {
        public static readonly string[] FeedNames = { "Warm-up", "Length", "Drop", "Lob", "Volley", "Boast", "Back wall", "Tight rail", "Mix" };
        public static readonly string[] SideNames = { "Forehand", "Backhand", "Alternate", "Random" };
        public static readonly string[] SpeedNames = { "Slow", "Medium", "Fast" };
        public static readonly float[] SpeedScale = { 0.85f, 1f, 1.15f };
        public static readonly string[] IntervalNames = { "2 s", "3 s", "4 s", "5 s" };
        /// <summary>The Feeding selector: every N seconds, or on your button.</summary>
        public static readonly string[] FeedingNames = { "Every 2 s", "Every 3 s", "Every 4 s", "Every 5 s", "On F" };
        public static readonly string[] ModeNames = { "Free play", "Feeder" };
        public static readonly float[] Intervals = { 2f, 3f, 4f, 5f };
        public static readonly string[] SlowNames = { "1x", "0.5x", "0.25x" };
        public static readonly float[] Slow = { 1f, 0.5f, 0.25f };

        public LabSimRunner Runner;
        double _timer;
        int _alt = 1, _count;
        uint _rng = 12345;
        /// <summary>Feeds fired so far (tests).</summary>
        public int Fed { get { return _count; } }

        static CoachChoices C { get { return ProfileStore.Current.Coach; } }

        public void Build(LabSimRunner runner)
        {
            Runner = runner;
            Runner.Sim.Launcher.SetRepeat(false);              // the coach feeds from now on
            Runner.Stepped += OnStepped;
            Apply();
            if (C.Feeder) FeedNow();
        }

        void OnDestroy() { if (Runner != null) Runner.Stepped -= OnStepped; }

        double Rnd() { _rng = unchecked(_rng * 1664525u + 1013904223u); return _rng / 4294967296.0; }

        /// <summary>The panel's slow motion and trail (call after a change).</summary>
        public void Apply()
        {
            Runner.SetFreePlay(!C.Feeder);                     // free play: no feeds, the ball in your hand
            Runner.Clock.TimeScale = SimRunnerBase.GameSpeed * Slow[Mathf.Clamp(C.Slow, 0, Slow.Length - 1)];
            BallView.LongTrail = C.LongTrail;
        }

        void OnStepped(System.Collections.Generic.IReadOnlyList<SimEvent> events)
        {
            if (!C.Auto || !C.Feeder) return;
            _timer += PhysicsConfig.Current.Sim.FixedDt;
            if (_timer >= Intervals[Mathf.Clamp(C.Interval, 0, Intervals.Length - 1)]) FeedNow();
        }

        /// <summary>Fire the next feed as the panel says.</summary>
        public void FeedNow()
        {
            if (!C.Feeder) return;
            _timer = 0;
            _count++;
            int side;
            switch (C.Side)
            {
                case 0: side = 1; break;
                case 1: side = -1; break;
                case 2: side = _alt; _alt = -_alt; break;
                default: side = Rnd() < 0.5 ? -1 : 1; break;
            }
            int feed = C.Feed;
            if (feed == FeedNames.Length - 1) feed = (int)(Rnd() * 5) % 5;            // mix: warm-up .. volley
            float speed = SpeedScale[Mathf.Clamp(C.Speed, 0, SpeedScale.Length - 1)];
            var cfg = Runner.Sim.Launcher.Config;
            cfg.Temperature = C.WarmBall ? 42 : PhysicsConfig.Current.Temperature.Initial;
            cfg.Spin.X = 0; cfg.Spin.Y = 0; cfg.Spin.Z = 0;
            switch (feed)
            {
                case 0: AimAt(cfg, side * 0.5, 7.2, side * 1.0, 2.2, 21 * speed); break;
                case 1: AimAt(cfg, side * 0.5, 7.4, side * 1.5, 2.5, 25 * speed); break;
                case 2: AimAt(cfg, side * 0.4, 6.0, side * 0.8, 1.5, 17 * speed); break;
                case 3: AimAt(cfg, side * 0.4, 6.5, side * 0.9, 3.6, 21 * speed); break;
                case 4: AimAt(cfg, side * 0.6, 8.0, side * 1.0, 2.9, 27 * speed); break;
                case 5: AimAtSide(cfg, -side, 22 * speed); break;
                case 6:
                    cfg.Position.X = side * 1.2; cfg.Position.Y = 1.2; cfg.Position.Z = 3.0;
                    cfg.Speed = 21 * speed; cfg.AzimuthDeg = 180 - side * 8; cfg.ElevationDeg = 8;
                    break;
                default: AimAt(cfg, side * 2.6, 7.4, side * 2.9, 2.3, 25 * speed); break;
            }
            Runner.Sim.Launcher.Fire(fresh: true);
        }

        /// <summary>From (px, 1.0, pz) to the front-wall point (wx, wy) at about this pace.</summary>
        static void AimAt(LaunchConfig cfg, double px, double pz, double wx, double wy, double speed)
        {
            double py = 1.0, dx = wx - px, dz = pz, horiz = System.Math.Sqrt(dx * dx + dz * dz);
            double t = horiz / speed;
            double vy = (wy - py) / t + 0.5 * PhysicsConfig.Current.World.Gravity * t;
            cfg.Position.X = px; cfg.Position.Y = py; cfg.Position.Z = pz;
            cfg.Speed = System.Math.Sqrt(speed * speed + vy * vy);
            cfg.AzimuthDeg = System.Math.Atan2(dx, dz) * 180 / System.Math.PI;
            cfg.ElevationDeg = System.Math.Atan2(vy, speed) * 180 / System.Math.PI;
        }

        /// <summary>A boast: off the side wall (wall = -1 left / +1 right) first, then the front wall.</summary>
        static void AimAtSide(LaunchConfig cfg, int wall, double speed)
        {
            double px = -wall * 0.8, pz = 7.0, sx = wall * CourtDimensions.HALF_W, sz = 4.2;
            double dx = sx - px, dz = pz - sz, horiz = System.Math.Sqrt(dx * dx + dz * dz);
            double t = horiz / speed;
            double vy = (2.0 - 1.0) / t + 0.5 * PhysicsConfig.Current.World.Gravity * t;
            cfg.Position.X = px; cfg.Position.Y = 1.0; cfg.Position.Z = pz;
            cfg.Speed = System.Math.Sqrt(speed * speed + vy * vy);
            cfg.AzimuthDeg = System.Math.Atan2(dx, dz) * 180 / System.Math.PI;
            cfg.ElevationDeg = System.Math.Atan2(vy, speed) * 180 / System.Math.PI;
        }
    }
}
