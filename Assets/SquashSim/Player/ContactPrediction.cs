// ===========================================================
// Assets/SquashSim/Player/ContactPrediction.cs
// Port of lab/player/ContactPrediction.js. Where and when could this
// player plausibly strike the ball? Runs the REAL ball integrator on a
// scratch copy against the REAL court - no parallel approximate physics.
// Predicts the BALL only; never moves the player.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public sealed class Prediction
    {
        public bool Valid;
        public double Time;                       // seconds from now until contact
        public readonly V3 Point = new V3();
        public readonly V3 Vel = new V3();
        public double Distance;                   // horizontal distance from the player then
        public int BouncesBefore;
        public bool IsVolley;
        public int StepsSinceRefresh;

        /// <summary>Match rules: ignore contacts after this many floor bounces since the front wall
        /// (1 = a ball may bounce once). -1 = unlimited, the lab's behaviour.</summary>
        public int MaxFloorBounces = -1;
    }

    public static class ContactPrediction
    {
        // One reusable scratch ball (the lab keeps one module-level scratch too).
        static readonly BallState _scratch = new BallState();
        static readonly List<SimEvent> _events = new List<SimEvent>();

        static void CopyBall(BallState dst, BallState src)
        {
            dst.Pos.X = src.Pos.X; dst.Pos.Y = src.Pos.Y; dst.Pos.Z = src.Pos.Z;
            dst.Vel.X = src.Vel.X; dst.Vel.Y = src.Vel.Y; dst.Vel.Z = src.Vel.Z;
            dst.Omega.X = src.Omega.X; dst.Omega.Y = src.Omega.Y; dst.Omega.Z = src.Omega.Z;
            dst.Temperature = src.Temperature;
            dst.Resting = src.Resting;
            dst.Asleep = src.Asleep;
            dst.SimTime = 0;
            dst.FloorBounces = 0;
            dst.LastCollisionSurface = src.LastCollisionSurface;
            dst.ImpactHistory.Clear();
        }

        struct Best { public double T, X, Y, Z, Vx, Vy, Vz, D; public int Bounces; }

        /// <summary>Refreshes only every few steps; the integration is the expensive part.</summary>
        public static Prediction Step(Prediction pred, BallState ball, TestPlayer player, List<Surface> surfaces, bool force = false)
        {
            var cfg = PhysicsConfig.Current.Prediction;

            if (!force && ++pred.StepsSinceRefresh < cfg.RefreshEverySteps) return pred;
            pred.StepsSinceRefresh = 0;

            if (ball.Asleep)
            {
                // A parked ball is still playable (it can be scooped).
                pred.Valid = true;
                pred.Time = 0;
                pred.Point.X = ball.Pos.X; pred.Point.Y = ball.Pos.Y; pred.Point.Z = ball.Pos.Z;
                pred.Vel.X = 0; pred.Vel.Y = 0; pred.Vel.Z = 0;
                pred.Distance = DMath.Hypot(ball.Pos.X - player.Pos.X, ball.Pos.Z - player.Pos.Z);
                pred.BouncesBefore = 0;
                pred.IsVolley = false;
                return pred;
            }

            CopyBall(_scratch, ball);

            double dt = cfg.StepSeconds;
            int steps = (int)DMath.RoundJs(cfg.HorizonSeconds / dt);
            double maxReach = PhysicsConfig.Current.Reach.Stretch;
            double minH = PhysicsConfig.Current.Ball.Radius * 0.9;
            double maxH = PhysicsConfig.Current.Racket.MaxContactHeight;

            bool haveBest = false;
            Best best = default(Best);
            double bestScore = double.PositiveInfinity;

            for (int i = 1; i <= steps; i++)
            {
                _events.Clear();
                BallPhysics.Step(_scratch, surfaces, dt, _events);
                double t = i * dt;

                // Match only (the lab leaves this at -1): a ball past its legal bounce is not a contact.
                if (pred.MaxFloorBounces >= 0 && ball.FloorSinceFrontWall + _scratch.FloorBounces > pred.MaxFloorBounces) break;

                if (_scratch.Pos.Y < minH || _scratch.Pos.Y > maxH) continue;

                double d = DMath.Hypot(_scratch.Pos.X - player.Pos.X, _scratch.Pos.Z - player.Pos.Z);
                if (d > maxReach) continue;

                // Natural hitting moment: closest approach, mild preference for earlier.
                double score = d + t * 0.35;
                if (score < bestScore)
                {
                    bestScore = score;
                    haveBest = true;
                    best = new Best
                    {
                        T = t,
                        X = _scratch.Pos.X, Y = _scratch.Pos.Y, Z = _scratch.Pos.Z,
                        Vx = _scratch.Vel.X, Vy = _scratch.Vel.Y, Vz = _scratch.Vel.Z,
                        D = d, Bounces = _scratch.FloorBounces
                    };
                }
            }

            if (!haveBest)
            {
                pred.Valid = false;
                return pred;
            }

            pred.Valid = true;
            pred.Time = best.T;
            pred.Point.X = best.X; pred.Point.Y = best.Y; pred.Point.Z = best.Z;
            pred.Vel.X = best.Vx; pred.Vel.Y = best.Vy; pred.Vel.Z = best.Vz;
            pred.Distance = best.D;
            pred.BouncesBefore = best.Bounces;
            pred.IsVolley = (ball.FloorSinceFrontWall + best.Bounces) == 0;
            return pred;
        }

        /// <summary>Which rung of the reach ladder does this distance need?</summary>
        public static string ReachStateFor(double distance)
        {
            var r = PhysicsConfig.Current.Reach;
            if (distance <= r.Normal) return ReachStates.Normal;
            if (distance <= r.Lunge) return ReachStates.Lunge;
            if (distance <= r.DeepLunge) return ReachStates.DeepLunge;
            if (distance <= r.Stretch) return ReachStates.Stretch;
            return ReachStates.Unreachable;
        }
    }
}
