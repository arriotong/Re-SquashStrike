// ===========================================================
// Assets/SquashSim/Lab/BallLauncher.cs
// Port of lab/devlab/BallLauncher.js. Repeatable feed: never reads a
// clock, never uses randomness, timer runs on SIMULATED time - so a
// repeat feed is bit-identical every time.
//
// Angles in degrees, court-relative: azimuth 0 = toward the FRONT wall
// (-z), +ve toward +x; elevation +ve = upward.
// ===========================================================

namespace SquashSim.Lab
{
    public sealed class LaunchConfig
    {
        public V3 Position = new V3(0, 1.0, 6.5);
        public double Speed = 20.0;
        public double AzimuthDeg = 0;
        public double ElevationDeg = 5;
        public double Temperature = PhysicsConfig.Current.Temperature.Initial;
        public V3 Spin = new V3(0, 0, 0);
        public double RepeatIntervalSeconds = 2.5;
        /// <summary>false: every feed restarts at Temperature (A/B testing).
        /// true: the feed inherits the ball's current temperature (warm-up).</summary>
        public bool CarryTemperature = false;

        public LaunchConfig Clone()
        {
            return new LaunchConfig
            {
                Position = Position.Clone(), Speed = Speed, AzimuthDeg = AzimuthDeg, ElevationDeg = ElevationDeg,
                Temperature = Temperature, Spin = Spin.Clone(), RepeatIntervalSeconds = RepeatIntervalSeconds,
                CarryTemperature = CarryTemperature
            };
        }
    }

    /// <summary>A partial launch config: null fields are left unchanged (lab applyConfig).</summary>
    public sealed class LaunchOverride
    {
        public V3 Position;
        public double? Speed, AzimuthDeg, ElevationDeg, Temperature, RepeatIntervalSeconds;
        public V3 Spin;
        public bool? CarryTemperature;
    }

    public sealed class BallLauncher
    {
        const double DEG = DMath.PI / 180;

        public readonly LaunchConfig Config = new LaunchConfig();
        readonly BallState _ball;
        bool _repeat;
        double _timer;

        public int LaunchCount { get; private set; }
        public bool IsRepeating { get { return _repeat; } }

        public BallLauncher(BallState ball) { _ball = ball; }

        public static V3 LaunchVelocity(LaunchConfig cfg)
        {
            double az = cfg.AzimuthDeg * DEG;
            double el = cfg.ElevationDeg * DEG;
            double horizontal = cfg.Speed * DMath.Cos(el);
            return new V3(
                horizontal * DMath.Sin(az),
                cfg.Speed * DMath.Sin(el),
                -horizontal * DMath.Cos(az));
        }

        /// <param name="fresh">true = always start from Config.Temperature (preset select, reset).</param>
        public void Fire(bool fresh = false)
        {
            double temperature = (!fresh && Config.CarryTemperature) ? _ball.Temperature : Config.Temperature;
            _ball.Reset(Config.Position, LaunchVelocity(Config), Config.Spin, temperature);
            BallPhysics.Wake(_ball);
            LaunchCount++;
            _timer = 0;
        }

        /// <summary>Once per FIXED step, before the ball is stepped.</summary>
        public void Step(double dt)
        {
            if (!_repeat) return;
            _timer += dt;
            if (_timer >= Config.RepeatIntervalSeconds) Fire();
        }

        public void Apply(LaunchOverride o)
        {
            if (o == null) return;
            if (o.Position != null) Config.Position.CopyFrom(o.Position);
            if (o.Spin != null) Config.Spin.CopyFrom(o.Spin);
            if (o.Speed.HasValue) Config.Speed = o.Speed.Value;
            if (o.AzimuthDeg.HasValue) Config.AzimuthDeg = o.AzimuthDeg.Value;
            if (o.ElevationDeg.HasValue) Config.ElevationDeg = o.ElevationDeg.Value;
            if (o.Temperature.HasValue) Config.Temperature = o.Temperature.Value;
            if (o.RepeatIntervalSeconds.HasValue) Config.RepeatIntervalSeconds = o.RepeatIntervalSeconds.Value;
            if (o.CarryTemperature.HasValue) Config.CarryTemperature = o.CarryTemperature.Value;
        }

        public void SetRepeat(bool on) { _repeat = on; _timer = 0; }
        public bool ToggleRepeat() { _repeat = !_repeat; _timer = 0; return _repeat; }
    }
}
