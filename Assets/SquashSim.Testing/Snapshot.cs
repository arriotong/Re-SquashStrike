// ===========================================================
// Assets/SquashSim.Testing/Snapshot.cs
// Builds a trace frame from C# state and fingerprints it.
//
// MIRRORS Tools/TraceRecorder/snapshot.mjs: same fields, same order,
// same encoding (finite number | 'Inf'/'-Inf'/'NaN' | 1/0 for bools |
// null | nested list), same two-pass FNV-1a hash.
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;

namespace SquashSim.Testing
{
    public static class Snapshot
    {
        public static object Num(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Inf";
            if (double.IsNegativeInfinity(v)) return "-Inf";
            return v == 0 ? 0.0 : v;
        }
        public static object Num(double? v) { return v.HasValue ? Num(v.Value) : null; }
        public static object Num(bool v) { return v ? 1.0 : 0.0; }
        public static object Num(int v) { return (double)v; }
        static object Str(string s) { return s; }

        static List<object> L(params object[] items) { return new List<object>(items); }

        static List<object> BallPart(BallState b)
        {
            return L(
                Num(b.Pos.X), Num(b.Pos.Y), Num(b.Pos.Z),
                Num(b.Vel.X), Num(b.Vel.Y), Num(b.Vel.Z),
                Num(b.Omega.X), Num(b.Omega.Y), Num(b.Omega.Z),
                Num(b.Temperature), Num(b.Resting), Num(b.Asleep),
                Num(b.FloorSinceFrontWall), Num(b.FloorBounces), Num(b.SimTime));
        }

        static List<object> EventPart(SimEvent ev)
        {
            if (ev.Type == SimEventType.Impact)
            {
                return L("i", Str(ev.SurfaceId),
                    Num(ev.Point.X), Num(ev.Point.Y), Num(ev.Point.Z),
                    Num(ev.ImpactSpeed), Num(ev.NormalSpeed), Num(ev.Restitution),
                    Num(ev.EnergyLost), Num(ev.Temperature), Num(ev.Resting), Num(ev.Nick), Num(ev.Time));
            }
            DeceptionRecord d = ev.Deception;
            return L("r",
                Num(ev.Point.X), Num(ev.Point.Y), Num(ev.Point.Z),
                Str(ev.Stroke), Str(ev.Mode), Str(ev.Technique), Num(ev.Side), Num(ev.Rushed),
                Num(ev.HeadSpeed), Num(ev.FaceNormal.X), Num(ev.FaceNormal.Y), Num(ev.FaceNormal.Z),
                Num(ev.OffCentre), Num(ev.OffCentreNorm), Num(ev.Restitution), Num(ev.AssistApplied),
                Num(ev.BallSpeedOut), Num(ev.OutVel.X), Num(ev.OutVel.Y), Num(ev.OutVel.Z),
                Num(ev.EnergyLost), Num(ev.Temperature),
                Str(ev.Label), Str(ev.ReachState), Num(ev.Balance), Num(ev.TimingCorrection),
                d != null ? Num(d.ShortfallDeg) : null, d != null ? Num(d.LateBySeconds) : null,
                d != null ? Num(d.Achieved) : null, d != null ? Num(d.RangeScale) : null);
        }

        static List<object> Events(List<SimEvent> events)
        {
            var list = new List<object>(events.Count);
            foreach (var e in events) list.Add(EventPart(e));
            return list;
        }

        public static List<object> Ball(BallState ball, List<SimEvent> events)
        {
            return L(BallPart(ball), Events(events));
        }

        public static List<object> Lab(TestPlayerController ctrl, BallState ball, List<SimEvent> events)
        {
            TestPlayer p = ctrl.Player; AimState a = ctrl.Aim; Prediction pr = ctrl.Prediction; MovementAssist ma = ctrl.Assist;
            SwingController s = ctrl.Swing; RacketIntent ri = ctrl.IntentSolver; Racket r = ctrl.Racket;
            return L(
                BallPart(ball),
                L(Num(p.Pos.X), Num(p.Pos.Z), Num(p.Vel.X), Num(p.Vel.Z), Num(p.Facing), Num(p.Speed),
                  Num(p.Balance), Num(p.SwingSide), Str(p.MovementState), Str(p.ReachState), Num(p.Adjusting)),
                L(Num(a.BiasX), Num(a.BiasY), Num(a.DesiredYaw), Num(a.DesiredPitch), Num(a.NeutralPitch),
                  Str(a.IntendedWall)),
                L(Num(pr.Valid), Num(pr.Time), Num(pr.Point.X), Num(pr.Point.Y), Num(pr.Point.Z),
                  Num(pr.Vel.X), Num(pr.Vel.Y), Num(pr.Vel.Z), Num(pr.Distance), Num(pr.BouncesBefore),
                  Num(pr.IsVolley), Num(pr.StepsSinceRefresh)),
                L(Num(ma.Active), Num(ma.TravelUsed), Num(ma.HasIdealSpot), Num(ma.IdealSpot.X), Num(ma.IdealSpot.Z),
                  Str(ma.ReachState), Num(ma.Balance), Num(ma.Quality), Num(ma.Correction.X), Num(ma.Correction.Z)),
                L(Str(s.State), Str(s.Stroke), Str(s.Mode), Num(s.HoldSeconds), Num(s.PrepQuality), Num(s.ArmedAge),
                  Num(s.ExpectedStartIn), Str(s.Buffered), Num(s.BufferedAge),
                  Num(s.StartIn), Num(s.Rushed), Num(s.TimingCorrection), Num(s.SwingTime), Num(s.Duration),
                  Num(s.Phase), Num(s.TotalArc), Num(s.StartAngle), Num(s.ContactAngle), Num(s.ContactHeight),
                  Num(s.Center.X), Num(s.Center.Z), Num(s.Reach), Num(s.Side), Num(s.PathYaw),
                  Num(s.HasContacted), Num(s.Cooldown),
                  s.Plan != null ? Str(s.Plan.Technique) : null, s.Plan != null ? Num(s.Plan.HeadSpeed) : null),
                L(Str(ri.Phase), Num(ri.BodyYaw), Num(ri.FaceYaw), Num(ri.FacePitch), Num(ri.PathYaw),
                  Num(ri.RangeScale), Num(ri.FaceLimited)),
                L(Num(r.Anchor.X), Num(r.Anchor.Y), Num(r.Anchor.Z),
                  Num(r.HeadPos.X), Num(r.HeadPos.Y), Num(r.HeadPos.Z),
                  Num(r.HeadVel.X), Num(r.HeadVel.Y), Num(r.HeadVel.Z),
                  Num(r.FaceNormal.X), Num(r.FaceNormal.Y), Num(r.FaceNormal.Z),
                  Num(r.Angle), Num(r.Reach), Num(r.HeadSpeed), Num(r.AssistApplied), Num(r.AssistMiss)),
                Str(ctrl.Interpretation.Label),
                Events(events));
        }

        // ---------------- fingerprint (two-pass FNV-1a 32) ----------------

        const uint PRIME = 16777619;

        struct H { public uint A, B; }

        static void Feed(ref H h, uint b)
        {
            unchecked
            {
                h.A = (h.A ^ b) * PRIME;
                h.B = (h.B ^ b) * PRIME;
            }
        }

        static void Walk(ref H h, object v)
        {
            if (v == null) { Feed(ref h, 3); return; }
            if (v is double d)
            {
                Feed(ref h, 1);
                long bits = BitConverter.DoubleToInt64Bits(d);
                for (int i = 0; i < 8; i++) Feed(ref h, (uint)((bits >> (8 * i)) & 0xff));
                return;
            }
            if (v is string s)
            {
                Feed(ref h, 2);
                foreach (char c in s) { Feed(ref h, (uint)(c & 0xff)); Feed(ref h, (uint)(c >> 8)); }
                Feed(ref h, 0xff); Feed(ref h, 0xff);
                return;
            }
            if (v is List<object> list)
            {
                Feed(ref h, 4);
                int n = list.Count;
                for (int i = 0; i < 4; i++) Feed(ref h, (uint)((n >> (8 * i)) & 0xff));
                foreach (var e in list) Walk(ref h, e);
                return;
            }
            throw new InvalidOperationException("unencodable value in frame: " + v.GetType());
        }

        public static string Hash(List<object> frame)
        {
            var h = new H { A = 2166136261u, B = 0x9747b28cu };
            Walk(ref h, frame);
            return h.A.ToString("x8") + h.B.ToString("x8");
        }
    }
}
