// ===========================================================
// Assets/SquashSim/Player/RacketPhysics.cs
// Port of lab/player/RacketPhysics.js. The racket as a real, tracked,
// physical object: position, face normal, head velocity, sweet spot.
//
// THE RULE: the ball changes ONLY because the racket physically
// contacted it. Outgoing ball state comes from incoming velocity, head
// velocity, face normal and where on the face it landed - nothing else.
// Head velocity is MEASURED from the swing path, never authored.
// ===========================================================

namespace SquashSim
{
    public sealed class Racket
    {
        // Transform
        public readonly V3 Anchor = new V3(0, 1.32, 6.2);       // shoulder
        public readonly V3 HeadPos = new V3(0, 0.95, 5.5);      // assisted position (rendered + collided)
        public readonly V3 HeadPosRaw = new V3(0, 0.95, 5.5);   // true swing path, drives head velocity
        public readonly V3 PrevHeadRaw = new V3(0, 0.95, 5.5);
        public readonly V3 HeadVel = new V3(0, 0, 0);
        public readonly V3 FaceNormal = new V3(0, 0, -1);
        public readonly V3 DesiredFaceNormal = new V3(0, 0, -1);   // debug only
        public bool WasSwinging;
        public readonly V3 FaceUp = new V3(0, 1, 0);
        public readonly V3 SweetSpot = new V3(0, 0.95, 5.5);
        public double Angle;
        public double Reach = 0.76;

        // Debug / readout
        public double HeadSpeed;
        public readonly V3 AssistOffset = new V3(0, 0, 0);
        public double AssistApplied;
        public double AssistMiss = double.PositiveInfinity;
        public bool AssistInRange;
        public SimEvent LastContact;
        public bool Teleported = true;

        /// <summary>Call whenever the racket pose jumps, so velocity is not faked.</summary>
        public void MarkTeleported()
        {
            Teleported = true;
            HeadVel.X = 0; HeadVel.Y = 0; HeadVel.Z = 0;
            HeadSpeed = 0;
        }
    }

    public static class RacketPhysics
    {
        const double CONTACT_SKIN = 5e-4;

        static readonly V3 _tmp = new V3();
        static readonly V3 _tangent = new V3();
        static readonly V3 _rVec = new V3();

        /// <summary>Unit direction for a court-relative angle. 0 = toward the front wall.</summary>
        static V3 DirFromAngle(V3 output, double angle)
        {
            output.X = DMath.Sin(angle);
            output.Y = 0;
            output.Z = -DMath.Cos(angle);
            return output;
        }

        /// <summary>Face normal from yaw + pitch, pitch clamped to what a racket can present.</summary>
        static V3 ComputeFaceNormal(V3 output, double aimYaw, double pitch)
        {
            var cfg = PhysicsConfig.Current.Racket;
            pitch = DMath.Max(cfg.FacePitchMin, DMath.Min(cfg.FacePitchMax, pitch));
            double cp = DMath.Cos(pitch);
            output.X = DMath.Sin(aimYaw) * cp;
            output.Y = DMath.Sin(pitch);
            output.Z = -DMath.Cos(aimYaw) * cp;
            double len = System.Math.Sqrt(output.X * output.X + output.Y * output.Y + output.Z * output.Z);
            if (len == 0 || double.IsNaN(len)) len = 1;   // JS: `|| 1`
            output.X /= len; output.Y /= len; output.Z /= len;
            return output;
        }

        /// <summary>Update the racket transform for this fixed step.</summary>
        public static void Step(Racket racket, SwingController swing, RacketIntent ri, AimState aim,
                                TestPlayer player, BallState ball, Prediction pred, double dt)
        {
            var cfg = PhysicsConfig.Current.Racket;
            bool swinging = swing.State == SwingStates.Swinging;
            int side = swinging ? swing.Side : (player.SwingSide >= 0 ? 1 : -1);

            if (swinging)
            {
                racket.Anchor.X = swing.Center.X;
                racket.Anchor.Z = swing.Center.Z;
            }
            else
            {
                racket.Anchor.X = player.Pos.X;
                racket.Anchor.Z = player.Pos.Z;
            }
            racket.Anchor.Y = cfg.ShoulderHeight;

            double angle;
            double height;
            double pathRise = swing.PlanPathRise;

            if (swinging)
            {
                angle = Swing.SwingAngleAt(swing, swing.Phase);
                height = swing.ContactHeight + pathRise * (swing.Phase - 0.5);
            }
            else if (swing.State == SwingStates.Preparing || swing.State == SwingStates.Scheduled ||
                     swing.State == SwingStates.Armed)
            {
                angle = Swing.BackswingAngle(swing, side, ri.BodyYaw);
                double target = pred != null && pred.Valid
                    ? DMath.Max(cfg.MinContactHeight, DMath.Min(cfg.MaxContactHeight, pred.Point.Y))
                    : cfg.RestHeight;
                height = target - pathRise * 0.5;
            }
            else
            {
                double bearing = ball != null
                    ? DMath.Atan2(ball.Pos.X - player.Pos.X, -(ball.Pos.Z - player.Pos.Z))
                    : 0;
                angle = bearing + side * 0.45;
                height = cfg.RestHeight;
            }

            double reach = swinging ? swing.Reach : cfg.Reach;

            racket.Angle = angle;
            racket.Reach = reach;
            DirFromAngle(_tmp, angle);
            racket.HeadPosRaw.X = racket.Anchor.X + _tmp.X * reach;
            racket.HeadPosRaw.Y = height;
            racket.HeadPosRaw.Z = racket.Anchor.Z + _tmp.Z * reach;

            // Head velocity from the raw path; a pose jump is not velocity.
            bool wasSwinging = racket.WasSwinging;
            racket.WasSwinging = swinging;
            if (racket.Teleported || swinging != wasSwinging)
            {
                racket.PrevHeadRaw.CopyFrom(racket.HeadPosRaw);
                racket.Teleported = false;
            }
            if (dt > 0)
            {
                racket.HeadVel.X = (racket.HeadPosRaw.X - racket.PrevHeadRaw.X) / dt;
                racket.HeadVel.Y = (racket.HeadPosRaw.Y - racket.PrevHeadRaw.Y) / dt;
                racket.HeadVel.Z = (racket.HeadPosRaw.Z - racket.PrevHeadRaw.Z) / dt;
            }
            racket.HeadSpeed = V3.Length(racket.HeadVel);

            // ACTUAL face from the commitment solver; DESIRED face for debug only.
            double planFacePitch = swing.PlanFacePitch;
            ComputeFaceNormal(racket.FaceNormal, ri.FaceYaw, ri.FacePitch + planFacePitch);
            ComputeFaceNormal(racket.DesiredFaceNormal, aim.DesiredYaw, aim.DesiredPitch + planFacePitch);

            // Contact assistance nudges POSITION only.
            bool active = Swing.InContactWindow(swing) && !swing.HasContacted;
            AssistResult assist = ContactAssistance.Compute(racket.HeadPosRaw, racket.FaceNormal, ball, active);
            racket.AssistOffset.X = assist.X;
            racket.AssistOffset.Y = assist.Y;
            racket.AssistOffset.Z = assist.Z;
            racket.AssistApplied = assist.Applied;
            racket.AssistMiss = assist.MissDistance;
            racket.AssistInRange = assist.InRange;

            racket.HeadPos.X = racket.HeadPosRaw.X + assist.X;
            racket.HeadPos.Y = racket.HeadPosRaw.Y + assist.Y;
            racket.HeadPos.Z = racket.HeadPosRaw.Z + assist.Z;
            racket.SweetSpot.CopyFrom(racket.HeadPos);

            racket.PrevHeadRaw.CopyFrom(racket.HeadPosRaw);
        }

        /// <summary>Swept string-bed (disc) vs ball over this step, then a physical
        /// impulse response. Returns the contact event, or null.</summary>
        public static SimEvent TryContact(Racket racket, BallState ball, SwingController swing, double dt)
        {
            if (!Swing.InContactWindow(swing) || swing.HasContacted) return null;

            var cfgBall = PhysicsConfig.Current.Ball;
            var cfg = PhysicsConfig.Current.Racket;
            V3 n = racket.FaceNormal;
            double rBall = cfgBall.Radius;

            double dx = ball.Pos.X - racket.HeadPos.X;
            double dy = ball.Pos.Y - racket.HeadPos.Y;
            double dz = ball.Pos.Z - racket.HeadPos.Z;

            double rvx = ball.Vel.X - racket.HeadVel.X;
            double rvy = ball.Vel.Y - racket.HeadVel.Y;
            double rvz = ball.Vel.Z - racket.HeadVel.Z;

            double d0 = dx * n.X + dy * n.Y + dz * n.Z;
            double un = rvx * n.X + rvy * n.Y + rvz * n.Z;

            if (un >= 0) return null;
            if (d0 < -rBall) return null;

            double t;
            if (d0 <= rBall)
            {
                t = 0;
            }
            else
            {
                t = (rBall - d0) / un;
                if (!(t >= 0) || t > dt) return null;
            }

            double bx = ball.Pos.X + ball.Vel.X * t;
            double by = ball.Pos.Y + ball.Vel.Y * t;
            double bz = ball.Pos.Z + ball.Vel.Z * t;

            double hx = racket.HeadPos.X + racket.HeadVel.X * t;
            double hy = racket.HeadPos.Y + racket.HeadVel.Y * t;
            double hz = racket.HeadPos.Z + racket.HeadVel.Z * t;

            double ox = bx - hx, oy = by - hy, oz = bz - hz;
            double along = ox * n.X + oy * n.Y + oz * n.Z;
            double px = ox - along * n.X, py = oy - along * n.Y, pz = oz - along * n.Z;
            double offCentre = System.Math.Sqrt(px * px + py * py + pz * pz);

            if (offCentre > cfg.HeadRadius) return null;

            double offNorm = DMath.Min(1, offCentre / DMath.Max(1e-6, cfg.HeadRadius));
            double e = cfg.Restitution * (1 - cfg.OffCentrePenalty * offNorm);

            // ---- impulse response in the racket frame ----
            double m = cfgBall.Mass;
            double I = cfgBall.InertiaFactor * m * rBall * rBall;

            double ux = rvx, uy = rvy, uz = rvz;
            double speedBefore = System.Math.Sqrt(ux * ux + uy * uy + uz * uz);

            double unOut = -e * un;
            _tangent.X = ux - un * n.X;
            _tangent.Y = uy - un * n.Y;
            _tangent.Z = uz - un * n.Z;
            double utMag = V3.Length(_tangent);

            double outX = unOut * n.X;
            double outY = unOut * n.Y;
            double outZ = unOut * n.Z;

            if (utMag > 1e-6)
            {
                double tx = _tangent.X / utMag, ty = _tangent.Y / utMag, tz = _tangent.Z / utMag;
                double jn = (1 + e) * m * System.Math.Abs(un);
                double jStick = utMag * m / (1 + 1 / cfgBall.InertiaFactor);
                double jt = DMath.Min(jStick, cfg.Friction * jn);

                double utAfter = utMag - jt / m;
                outX += utAfter * tx;
                outY += utAfter * ty;
                outZ += utAfter * tz;

                // The strings biting the ball is exactly what creates spin.
                _rVec.X = -n.X * rBall; _rVec.Y = -n.Y * rBall; _rVec.Z = -n.Z * rBall;
                _tmp.X = -jt * tx; _tmp.Y = -jt * ty; _tmp.Z = -jt * tz;
                V3.Cross(_tangent, _rVec, _tmp);
                double gs = cfg.SpinGrip / I;
                ball.Omega.X += _tangent.X * gs;
                ball.Omega.Y += _tangent.Y * gs;
                ball.Omega.Z += _tangent.Z * gs;
            }

            double speedAfter = System.Math.Sqrt(outX * outX + outY * outY + outZ * outZ);

            ball.Vel.X = racket.HeadVel.X + outX;
            ball.Vel.Y = racket.HeadVel.Y + outY;
            ball.Vel.Z = racket.HeadVel.Z + outZ;

            ball.Pos.X = bx + n.X * (rBall - DMath.Min(d0, rBall) + CONTACT_SKIN);
            ball.Pos.Y = by + n.Y * (rBall - DMath.Min(d0, rBall) + CONTACT_SKIN);
            ball.Pos.Z = bz + n.Z * (rBall - DMath.Min(d0, rBall) + CONTACT_SKIN);

            BallPhysics.Wake(ball);

            double energyLost = DMath.Max(0, 0.5 * m * (speedBefore * speedBefore - speedAfter * speedAfter));
            BallTemperature.ApplyImpactHeat(ball, energyLost, "racket");

            swing.HasContacted = true;
            ball.FloorSinceFrontWall = 0;

            var ev = new SimEvent
            {
                Type = SimEventType.RacketContact,
                Point = new V3(bx, by, bz),
                Stroke = swing.Stroke,
                Mode = swing.Mode,
                Technique = swing.Plan != null ? swing.Plan.Technique : null,
                Side = swing.Side,
                Rushed = swing.Rushed,
                HeadSpeed = racket.HeadSpeed,
                FaceNormal = new V3(n.X, n.Y, n.Z),
                OffCentre = offCentre,
                OffCentreNorm = offNorm,
                Restitution = e,
                AssistApplied = racket.AssistApplied,
                BallSpeedOut = V3.Length(ball.Vel),
                OutVel = new V3(ball.Vel.X, ball.Vel.Y, ball.Vel.Z),
                EnergyLost = energyLost,
                Temperature = ball.Temperature,
                Time = ball.SimTime
            };

            racket.LastContact = ev;
            ball.LastRacketContact = new RacketTouch
            {
                By = "player",
                Time = ball.SimTime,
                Speed = ev.BallSpeedOut,
                Stroke = swing.Stroke,
                Technique = ev.Technique
            };

            return ev;
        }
    }
}
