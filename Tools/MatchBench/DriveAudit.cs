// ===========================================================
// Tools/MatchBench/DriveAudit.cs
// Drives and crosses, measured (playtest 2026-10-28: "when I try to drive
// the ball straight 95% of the time the ball just skits out the side wall
// and lands in the middle of the court ... I just want them to be going
// towards the corners ... if your aim is more to the right guide it to the
// right corner, same goes to the left"). The real stroke (ArcadeStroke.Hit,
// a clean contact) from a grid of contact points, the reticle anywhere on
// either half of the front wall at drive height, flown through the real
// ball physics:
//   corner    the ball dies (2nd bounce, or the back wall then the floor)
//             in the back quarter on the aim's side
//   wrong     it goes to the other side's corner from the aim
//   middle    the 2nd bounce is in the middle of the court
//   side      it touches a side wall before its first bounce
//
//   --drive-audit [--assisted] [--charge 0.0]
// ===========================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using SquashSim;

static class DriveAudit
{
    sealed class Flight { public double Land1X, Land1Z = -1, EndX, EndZ = -1; public int SideBefore; public bool Back, Out, Down; }

    static Flight Fly(BallState from, List<Surface> surfaces)
    {
        var b = new BallState(); b.Reset(from.Pos, from.Vel, null, from.Temperature); b.DipOffFrontWall = from.DipOffFrontWall;
        var f = new Flight();
        var ev = new List<SimEvent>();
        bool front = false; int floors = 0;
        for (int i = 0; i < 2400 && floors < 2; i++)
        {
            ev.Clear();
            BallPhysics.Step(b, surfaces, 1.0 / 240, ev);
            foreach (var e in ev)
            {
                if (RallyReferee.OutReason(e) != null) f.Out = true;
                if (e.SurfaceClass == "tin" && !front) f.Down = true;
                if (e.SurfaceClass == "frontWall") front = true;
                else if (!front && e.SurfaceClass == "floor") { f.Down = true; floors = 2; }
                else if (front && e.SurfaceClass == "sideWall" && floors == 0) f.SideBefore++;
                else if (front && e.SurfaceClass == "backWall") f.Back = true;
                else if (front && e.SurfaceClass == "floor")
                {
                    floors++;
                    if (floors == 1) { f.Land1X = e.Point.X; f.Land1Z = e.Point.Z; }
                    else { f.EndX = e.Point.X; f.EndZ = e.Point.Z; }
                }
            }
        }
        return f;
    }

    public static void Run(bool assisted, double charge)
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.UseGameProfile();
        var c = PhysicsConfig.Current.ArcadeHit;
        var hit = typeof(ArcadeStroke).GetMethod("Hit", BindingFlags.NonPublic | BindingFlags.Instance);
        var setTime = typeof(ArcadeStroke).GetField("_setTime", BindingFlags.NonPublic | BindingFlags.Instance);
        double len = CourtDimensions.LENGTH;

        foreach (string kind in new[] { "straight", "cross" })
        {
            int n = 0, corner = 0, wrong = 0, middle = 0, side = 0, down = 0, outs = 0;
            double sumEndZ = 0;
            var rows = new List<string>();
            foreach (double bz in new[] { 4.5, 5.8, 7.0, 8.2 })
                foreach (double bx in new[] { -2.6, -1.8, -1.0, 1.0, 1.8, 2.6 })
                    foreach (double reach in new[] { 0.3, 1.0, 1.8, 2.6 })               // how far over the reticle sits
                        foreach (double ty in new[] { 1.3, 1.8, 2.4 })
                        {
                            int to = kind == "straight" ? Math.Sign(bx) : -Math.Sign(bx);  // the corner the aim asks for
                            double tx = to * reach;
                            var m = new MatchSimulation(0);
                            var p = m.Players[0];
                            p.ArcadeHitEnabled = true; p.WallAimEnabled = true;
                            p.Arcade.ShotLines = true; p.Arcade.Assisted = assisted;
                            p.Player.Pos.X = bx + 0.5; p.Player.Pos.Z = bz + 0.3;
                            p.Aim.HasWallTarget = true; p.Aim.TargetWall = 0; p.Aim.FromPoint = true;
                            p.Aim.WallTarget.X = tx; p.Aim.WallTarget.Y = ty; p.Aim.WallTarget.Z = 0;
                            m.Ball.Reset(new V3(bx, 0.6, bz), new V3(0, 0, 0), null, 40);
                            setTime.SetValue(p.Arcade, 1.0);                                // set in position: a clean contact
                            hit.Invoke(p.Arcade, new object[] { p, m.Ball, m.Surfaces, c.SweetSpot * 0.5, charge, c });
                            var f = Fly(m.Ball, m.Surfaces);
                            n++;
                            if (f.Down) { down++; continue; }
                            if (f.Out) outs++;
                            if (f.SideBefore > 0) side++;
                            double ex = f.EndZ > 0 ? f.EndX : f.Land1X, ez = f.EndZ > 0 ? f.EndZ : len;
                            sumEndZ += ez;
                            bool deep = ez >= 7.0, onSide = ex * to > 1.0;
                            if (deep && onSide) corner++;
                            else if (ex * to < -1.0) wrong++;
                            else if (ez < 7.0 && Math.Abs(ex) <= 1.6) middle++;
                            if (Environment.GetEnvironmentVariable("DRIVE_AUDIT_V") != null)
                                rows.Add("    " + kind + " ball " + bx.ToString("0.0") + "," + bz.ToString("0.0") + " aim " + tx.ToString("0.0") + "," + ty.ToString("0.0") +
                                    " -> 1st " + f.Land1X.ToString("0.0") + "," + f.Land1Z.ToString("0.0") + " end " + ex.ToString("0.0") + "," + ez.ToString("0.0") + " side " + f.SideBefore + (f.Back ? " BACK" : ""));
                        }
            foreach (var r in rows) Console.WriteLine(r);
            Console.WriteLine((kind == "straight" ? "STRAIGHT" : "CROSS   ") + (assisted ? " (assisted)" : " (normal)  ") + "  " + n + " shots:  corner on the aim's side " + (100 * corner / n) + "%   wrong corner " + (100 * wrong / n) +
                "%   died mid-court " + (100 * middle / n) + "%   side wall before the bounce " + (100 * side / n) + "%   down " + (100 * down / n) + "%   out " + (100 * outs / n) + "%   ends at z " + (sumEndZ / Math.Max(1, n - down)).ToString("0.0"));
        }
    }
}
