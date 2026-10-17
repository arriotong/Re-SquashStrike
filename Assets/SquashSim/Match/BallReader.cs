// ===========================================================
// Assets/SquashSim/Match/BallReader.cs
// A player's READ of the ball: where will it next be playable?
//
// Runs the real integrator on a scratch ball (like ContactPrediction)
// but is not limited to the athlete's current reach - it answers "where
// should I go", not "can I hit it from here". Used by the bots to move
// early and by interference detection to know where a striker is going.
//
// Playable = after the front wall, at most one floor bounce, at a
// comfortable height, falling or near the top, not jammed in the front
// corners. Deterministic.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class BallReader
    {
        // MinHeight 0.12: a double yellow on a tight drive often never rises above knee height (M6; was 0.35).
        public double Horizon = 2.2, MinHeight = 0.12, MaxHeight = 1.4, Step = 1.0 / 120, MinZ = 1.2;
        public int RefreshSteps = 8;

        public bool Valid { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }
        /// <summary>Seconds from the read until the ball is there.</summary>
        public double Time { get; private set; }
        /// <summary>With a player: seconds the player is short of the intercept (0 = they can make it).</summary>
        public double ShortBy { get; private set; }

        readonly BallState _scratch = new BallState();
        readonly List<SimEvent> _events = new List<SimEvent>();
        int _age = 1 << 20;

        public void Invalidate() { _age = 1 << 20; Valid = false; }

        /// <summary>Refreshes every RefreshSteps calls (or when forced). Returns Valid.
        /// Without a player: the first playable point. With one: the first playable point that
        /// player can get to in time (an INTERCEPT); if none is reachable, the one they come
        /// closest to reaching.</summary>
        /// <summary>Time to cover d from standing: accelerate, then run at top speed.</summary>
        public static double TravelTime(double d, double vmax, double accel)
        {
            if (d <= 0) return 0;
            if (accel <= 0) return d / vmax;
            double dAccel = vmax * vmax / (2 * accel);
            return d <= dAccel ? System.Math.Sqrt(2 * d / accel) : d / vmax + vmax / (2 * accel);
        }

        public bool Read(BallState ball, List<Surface> surfaces, bool force = false)
        {
            return Read(ball, surfaces, null, force);
        }

        public bool Read(BallState ball, List<Surface> surfaces, TestPlayer player, bool force = false)
        {
            if (!force && ++_age < RefreshSteps) return Valid;
            _age = 0;
            Valid = false;
            ShortBy = 0;
            if (ball.Asleep) return false;

            _scratch.Pos.CopyFrom(ball.Pos); _scratch.Vel.CopyFrom(ball.Vel); _scratch.Omega.CopyFrom(ball.Omega);
            _scratch.Temperature = ball.Temperature; _scratch.Resting = ball.Resting; _scratch.Asleep = false;
            _scratch.LastCollisionSurface = ball.LastCollisionSurface;
            _scratch.FloorBounces = 0; _scratch.ImpactHistory.Clear();

            double speed = PhysicsConfig.Current.Player.MaxSpeed;
            double accel = PhysicsConfig.Current.Player.Acceleration;
            double reach = PhysicsConfig.Current.Reach.Normal;
            double bestShort = double.PositiveInfinity;
            int bounces = ball.FloorSinceFrontWall;
            int steps = (int)(Horizon / Step);
            for (int i = 1; i <= steps; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, Step, _events);
                foreach (var e in _events) if (e.SurfaceClass == "floor") bounces++;
                if (bounces >= 2) break;
                if (!(_scratch.Pos.Y >= MinHeight && _scratch.Pos.Y <= MaxHeight && _scratch.Vel.Y <= 0.5 && _scratch.Pos.Z > MinZ))
                    continue;

                double t = i * Step;
                if (player == null)
                {
                    X = _scratch.Pos.X; Y = _scratch.Pos.Y; Z = _scratch.Pos.Z; Time = t; Valid = true;
                    break;
                }
                double dx = _scratch.Pos.X - player.Pos.X, dz = _scratch.Pos.Z - player.Pos.Z;
                double need = TravelTime(System.Math.Max(0, System.Math.Sqrt(dx * dx + dz * dz) - reach), speed, accel);
                double shortBy = need - t;
                if (shortBy <= 0)
                {
                    X = _scratch.Pos.X; Y = _scratch.Pos.Y; Z = _scratch.Pos.Z; Time = t; Valid = true; ShortBy = 0;
                    break;                                    // first point we can be at in time
                }
                if (shortBy < bestShort)
                {
                    bestShort = shortBy;
                    X = _scratch.Pos.X; Y = _scratch.Pos.Y; Z = _scratch.Pos.Z; Time = t; Valid = true; ShortBy = shortBy;
                }
            }
            return Valid;
        }
    }

    /// <summary>Anything that can drive an athlete in a match through InputIntent: the wall-bot, the AI.</summary>
    public interface IAthleteBrain
    {
        void Fill(InputIntent intent, MatchSimulation m, int me, double dt);
    }
}
