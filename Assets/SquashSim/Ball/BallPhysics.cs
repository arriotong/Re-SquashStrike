// ===========================================================
// Assets/SquashSim/Ball/BallPhysics.cs
// Port of lab/ball/BallPhysics.js. Deterministic fixed-timestep ball.
//
// - The ball never knows what shot it is. No canned trajectories.
// - Rebound = surface base x temperature response x impact-speed response.
// - A nick is DETECTED after a low wall impact, never forced.
// - No randomness. Same inputs, same outputs, always.
//
// Swept sphere vs bounded analytic half-spaces, earliest time of
// impact first, iterated within the step.
// ===========================================================

using System.Collections.Generic;

namespace SquashSim
{
    public static class BallPhysics
    {
        const double CONTACT_SKIN = 1e-4;

        // Scratch, reused every step (no allocation in the hot loop).
        static readonly V3 _tmpA = new V3();
        static readonly V3 _tmpB = new V3();
        static readonly V3 _contact = new V3();
        static readonly V3 _rVec = new V3();
        static readonly V3 _tangent = new V3();

        public static double EffectiveRestitution(string surfaceClass, double temperature, double normalImpactSpeed)
        {
            var cfg = PhysicsConfig.Current.Restitution;
            double baseE = (PhysicsConfig.Current.Surfaces.Get(surfaceClass) ?? PhysicsConfig.Current.Surfaces.Floor).Restitution;
            double tempMul = BallTemperature.RestitutionMultiplier(temperature);
            var fade = PhysicsConfig.Current.Temperature.HotSpeedFade;
            if (fade != null)       // at pace the ball's temperature matters less, warm or cold
                tempMul = 1 + (tempMul - 1) * PhysicsConfig.SampleCurve(fade, System.Math.Abs(normalImpactSpeed));
            double speedMul = PhysicsConfig.SampleCurve(cfg.SpeedResponse, System.Math.Abs(normalImpactSpeed));
            return PhysicsConfig.Clamp(baseE * tempMul * speedMul, cfg.MinRestitution, cfg.MaxRestitution);
        }

        static V3 AccumulateAcceleration(V3 output, BallState ball)
        {
            var w = PhysicsConfig.Current.World;
            output.X = 0; output.Y = -w.Gravity; output.Z = 0;

            V3 v = ball.Vel;
            double speed = V3.Length(v);
            if (speed > 1e-6 && w.DragK > 0)
            {
                double k = w.DragK * speed;
                output.X -= k * v.X;
                output.Y -= k * v.Y;
                output.Z -= k * v.Z;
            }

            if (w.MagnusK != 0)
            {
                V3.Cross(_tmpB, ball.Omega, v);
                output.X += w.MagnusK * _tmpB.X;
                output.Y += w.MagnusK * _tmpB.Y;
                output.Z += w.MagnusK * _tmpB.Z;
            }
            return output;
        }

        struct Hit { public Surface Surf; public double T; public double Dist; }

        static bool FindEarliestImpact(BallState ball, List<Surface> surfaces, double remaining, double radius, out Hit best)
        {
            best = default(Hit);
            bool found = false;

            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface surf = surfaces[i];
                double vn = CourtSurfaces.NormalVelocity(surf, ball.Vel);
                if (vn >= 0) continue;

                double dist = CourtSurfaces.SignedDistance(surf, ball.Pos);
                double t;
                if (dist <= radius)
                {
                    t = 0;
                }
                else
                {
                    t = (radius - dist) / vn;
                    if (!(t >= 0) || t > remaining) continue;
                }

                _tmpA.X = ball.Pos.X + ball.Vel.X * t - surf.N.X * radius;
                _tmpA.Y = ball.Pos.Y + ball.Vel.Y * t - surf.N.Y * radius;
                _tmpA.Z = ball.Pos.Z + ball.Vel.Z * t - surf.N.Z * radius;
                if (!surf.Contains(_tmpA)) continue;

                if (!found || t < best.T) { best = new Hit { Surf = surf, T = t, Dist = dist }; found = true; }
            }
            return found;
        }

        /// <summary>Normal impulse + Coulomb-limited tangential impulse. Returns null for a
        /// continuing resting contact (physics still runs; it is just not a bounce).</summary>
        static SimEvent ResolveImpact(BallState ball, Surface surf, double simTime)
        {
            var cfgBall = PhysicsConfig.Current.Ball;
            SurfaceProps props = CourtSurfaces.Props(surf);
            V3 n = surf.N;
            double m = cfgBall.Mass;
            double r = cfgBall.Radius;
            double I = cfgBall.InertiaFactor * m * r * r;

            bool continuingRest = ball.Resting && ball.LastCollisionSurface == surf.Id;
            bool wasAirborne = !ball.Resting;

            double energyBefore = ball.KineticEnergy();
            double impactSpeed = V3.Length(ball.Vel);
            double vn = CourtSurfaces.NormalVelocity(surf, ball.Vel);

            _rVec.X = -n.X * r; _rVec.Y = -n.Y * r; _rVec.Z = -n.Z * r;
            _contact.X = ball.Pos.X + _rVec.X;
            _contact.Y = ball.Pos.Y + _rVec.Y;
            _contact.Z = ball.Pos.Z + _rVec.Z;

            V3.Cross(_tmpB, ball.Omega, _rVec);
            double cvx = ball.Vel.X + _tmpB.X;
            double cvy = ball.Vel.Y + _tmpB.Y;
            double cvz = ball.Vel.Z + _tmpB.Z;

            // ---- normal response ----
            double absVn = System.Math.Abs(vn);
            double e = EffectiveRestitution(surf.SurfaceClass, ball.Temperature, absVn);
            bool resting = false;
            if (absVn < PhysicsConfig.Current.Restitution.RestingNormalSpeed)
            {
                e = 0;
                resting = true;
            }

            double jn = -(1 + e) * m * vn;
            ball.Vel.X += (jn / m) * n.X;
            ball.Vel.Y += (jn / m) * n.Y;
            ball.Vel.Z += (jn / m) * n.Z;

            // ---- tangential response (friction <-> spin) ----
            double cvn = cvx * n.X + cvy * n.Y + cvz * n.Z;
            _tangent.X = cvx - cvn * n.X;
            _tangent.Y = cvy - cvn * n.Y;
            _tangent.Z = cvz - cvn * n.Z;
            double vtMag = V3.Length(_tangent);

            if (vtMag > 1e-6 && jn > 0)
            {
                double tx = _tangent.X / vtMag, ty = _tangent.Y / vtMag, tz = _tangent.Z / vtMag;

                double jStick = vtMag * m / (1 + 1 / cfgBall.InertiaFactor);
                double jMax = props.Friction * jn;
                double jt = DMath.Min(jStick, jMax);

                ball.Vel.X -= (jt / m) * tx;
                ball.Vel.Y -= (jt / m) * ty;
                ball.Vel.Z -= (jt / m) * tz;

                _tmpA.X = -jt * tx; _tmpA.Y = -jt * ty; _tmpA.Z = -jt * tz;
                V3.Cross(_tmpB, _rVec, _tmpA);
                double gs = props.SpinGrip / I;
                ball.Omega.X += _tmpB.X * gs;
                ball.Omega.Y += _tmpB.Y * gs;
                ball.Omega.Z += _tmpB.Z * gs;
            }

            // ---- separate cleanly ----
            double distNow = CourtSurfaces.SignedDistance(surf, ball.Pos);
            if (distNow < r + CONTACT_SKIN)
            {
                double push = (r + CONTACT_SKIN) - distNow;
                ball.Pos.X += n.X * push;
                ball.Pos.Y += n.Y * push;
                ball.Pos.Z += n.Z * push;
            }

            // ---- thermal + bookkeeping ----
            double energyAfter = ball.KineticEnergy();
            double energyLost = DMath.Max(0, energyBefore - energyAfter);
            string heatSource = surf.SurfaceClass == "floor" ? "floor" : "wall";
            BallTemperature.ApplyImpactHeat(ball, energyLost, heatSource);

            ball.Resting = resting && surf.SurfaceClass == "floor";

            if (continuingRest && resting) return null;

            ball.LastCollisionSurface = surf.Id;
            ball.LastCollisionClass = surf.SurfaceClass;
            ball.LastImpactSpeed = impactSpeed;
            ball.LastImpactNormalSpeed = absVn;
            ball.LastImpactEnergyLost = energyLost;
            ball.LastImpactRestitution = e;
            ball.LastImpactPoint.CopyFrom(_contact);
            ball.TotalImpacts++;
            if (surf.SurfaceClass == "floor") { ball.FloorBounces++; ball.FloorSinceFrontWall++; }
            if (surf.SurfaceClass == "frontWall") ball.FloorSinceFrontWall = 0;
            else ball.WallImpacts++;

            // ---- nick DETECTION (observational only) ----
            bool nick = false;
            bool isWall = surf.SurfaceClass == "sideWall" || surf.SurfaceClass == "backWall" ||
                          surf.SurfaceClass == "frontWall";
            var nickCfg = PhysicsConfig.Current.Nick;
            if (isWall && wasAirborne &&
                _contact.Y <= nickCfg.DetectHeight &&
                absVn >= nickCfg.MinNormalSpeed)
            {
                nick = true;
                ball.LastNick = new NickRecord { Surface = surf.Id, Point = _contact.Clone(), Time = simTime };
            }

            var ev = new SimEvent
            {
                Type = SimEventType.Impact,
                SurfaceId = surf.Id,
                SurfaceClass = surf.SurfaceClass,
                Point = _contact.Clone(),
                ImpactSpeed = impactSpeed,
                NormalSpeed = absVn,
                Restitution = e,
                EnergyLost = energyLost,
                Temperature = ball.Temperature,
                Resting = resting,
                Nick = nick,
                Time = simTime
            };
            ball.RecordImpact(ev);
            return ev;
        }

        /// <summary>Advance the ball by exactly one FIXED timestep.</summary>
        public static void Step(BallState ball, List<Surface> surfaces, double dt, List<SimEvent> events)
        {
            var cfgBall = PhysicsConfig.Current.Ball;
            double r = cfgBall.Radius;

            BallTemperature.CoolOverTime(ball, dt);
            ball.SimTime += dt;

            if (ball.Asleep) return;

            // ---- integrate velocity (semi-implicit Euler) ----
            AccumulateAcceleration(_tmpA, ball);
            ball.Vel.X += _tmpA.X * dt;
            ball.Vel.Y += _tmpA.Y * dt;
            ball.Vel.Z += _tmpA.Z * dt;

            if (cfgBall.SpinDampingPerSecond > 0)
            {
                double sd = DMath.Exp(-cfgBall.SpinDampingPerSecond * dt);
                ball.Omega.X *= sd; ball.Omega.Y *= sd; ball.Omega.Z *= sd;
            }

            // ---- swept collision over the step ----
            double remaining = dt;
            int iterations = 0;
            int maxIter = PhysicsConfig.Current.Sim.MaxCollisionIterations;

            while (remaining > 0 && iterations < maxIter)
            {
                Hit hit;
                if (!FindEarliestImpact(ball, surfaces, remaining, r, out hit)) break;

                if (hit.T > 0)
                {
                    ball.Pos.X += ball.Vel.X * hit.T;
                    ball.Pos.Y += ball.Vel.Y * hit.T;
                    ball.Pos.Z += ball.Vel.Z * hit.T;
                    remaining -= hit.T;
                }

                SimEvent ev = ResolveImpact(ball, hit.Surf, ball.SimTime);
                if (ev != null) events.Add(ev);
                iterations++;
            }

            if (remaining > 0)
            {
                ball.Pos.X += ball.Vel.X * remaining;
                ball.Pos.Y += ball.Vel.Y * remaining;
                ball.Pos.Z += ball.Vel.Z * remaining;
            }

            // ---- rolling resistance + sleep ----
            if (ball.Resting)
            {
                SurfaceProps props = PhysicsConfig.Current.Surfaces.Floor;
                double rr = props.RollingResistance;
                if (rr > 0)
                {
                    double damp = DMath.Exp(-rr * dt);
                    ball.Vel.X *= damp; ball.Vel.Z *= damp;
                    ball.Omega.X *= damp; ball.Omega.Y *= damp; ball.Omega.Z *= damp;
                }
                if (V3.Length(ball.Vel) < cfgBall.SleepSpeed && V3.Length(ball.Omega) < 1.0)
                {
                    ball.Vel.X = 0; ball.Vel.Y = 0; ball.Vel.Z = 0;
                    ball.Omega.X = 0; ball.Omega.Y = 0; ball.Omega.Z = 0;
                    ball.Asleep = true;
                }
            }
        }

        /// <summary>Wake a parked ball. Any external impulse must call this.</summary>
        public static void Wake(BallState ball)
        {
            ball.Asleep = false;
            ball.Resting = false;
        }
    }
}
