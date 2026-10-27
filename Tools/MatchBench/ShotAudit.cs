// ===========================================================
// Tools/MatchBench/ShotAudit.cs
// Assisted mode's shots, measured (playtest 2026-10-27: "the lobs aren't
// going to the back ... too much to the side", "lobs and drops went so
// slow, the lobs land short", "when I dive to the front ... it went crazy,
// bouncing off both side walls"). The stroke's own line and solver from a
// grid of contact points, flown through the real ball physics:
//   lobs     where they land (depth, distance off the side wall), side-wall
//            touches before the bounce, touch (soft) lobs too
//   drops    their pace and where they bounce
//   front    crosses struck from near the front (a dive's ball): side-wall
//            touches before the second bounce - "pinball" at 3 or more
//
//   --shot-audit
// ===========================================================

using System;
using System.Collections.Generic;
using SquashSim;

static class ShotAudit
{
    sealed class Flight { public double LandX, LandZ = -1, Bounce2Z = -1, Peak; public int SideBefore, SideTo2; public bool Back, Out; }

    static Flight Fly(BallState from, V3 vel, List<Surface> surfaces)
    {
        var b = new BallState(); b.Reset(from.Pos, vel, null, from.Temperature);
        var f = new Flight();
        var ev = new List<SimEvent>();
        bool front = false; int floors = 0;
        for (int i = 0; i < 1200 && floors < 2; i++)
        {
            ev.Clear();
            BallPhysics.Step(b, surfaces, 1.0 / 240, ev);
            if (b.Pos.Y > f.Peak) f.Peak = b.Pos.Y;
            foreach (var e in ev)
            {
                if (RallyReferee.OutReason(e) != null) f.Out = true;
                if (e.SurfaceClass == "frontWall") front = true;
                else if (front && e.SurfaceClass == "sideWall") { if (floors == 0) f.SideBefore++; f.SideTo2++; }
                else if (front && e.SurfaceClass == "backWall" && floors == 0) f.Back = true;
                else if (front && e.SurfaceClass == "floor")
                {
                    floors++;
                    if (floors == 1) { f.LandX = e.Point.X; f.LandZ = e.Point.Z; } else f.Bounce2Z = e.Point.Z;
                }
            }
        }
        return f;
    }

    public static void Run()
    {
        PhysicsConfig.ResetToDefaults();
        PhysicsConfig.UseGameProfile();
        var c = PhysicsConfig.Current.ArcadeHit;
        var surfaces = CourtSurfaces.Create();
        var st = new ArcadeStroke(1) { Assisted = true, ShotLines = true };
        var ball = new BallState();
        double hw = CourtDimensions.HALF_W;

        // ---- lobs: straight and cross from mid and back court ----
        foreach (bool touch in new[] { false, true })
        {
            int n = 0, deep = 0, short_ = 0, sideTouch = 0; double sumZ = 0, sumOff = 0, minZ = 99;
            foreach (double bz in new[] { 4.0, 5.5, 7.0, 8.4 })
                foreach (double bx in new[] { -2.6, -1.9, -1.2, 1.2, 1.9, 2.6 })
                    foreach (bool cross in new[] { false, true })
                    {
                        ball.Reset(new V3(bx, 0.7, bz), new V3(0, 0, 0), null, 40);
                        double aim = (cross ? -1 : 1) * Math.Sign(bx) * 2.0;
                        double tx = ArcadeStroke.PullToLine(aim, ArcadeStroke.ShotLine(bx, bz, "lob", aim), true, "lob");
                        if (touch && !GameTuning.Hit.TouchLobSolved) st.Solve(ball, surfaces, tx, c.LobHeight + 0.4, 0, 0, c.TouchSpeed);
                        else st.SolveLob(ball, surfaces, tx, c.LobHeight);
                        var f = Fly(ball, st.LaunchVelocity.Clone(), surfaces);
                        n++; if (f.LandZ < 0) { short_++; continue; }
                        sumZ += f.LandZ; minZ = Math.Min(minZ, f.LandZ); sumOff += hw - Math.Abs(f.LandX);
                        if (f.LandZ >= 7.8) deep++; if (f.LandZ < 6.5) short_++; if (f.SideBefore > 0) sideTouch++;
                        if (!touch && Environment.GetEnvironmentVariable("SHOT_AUDIT_V") != null)
                            Console.WriteLine("    lob from " + bx.ToString("0.0") + "," + bz.ToString("0.0") + (cross ? " cross   " : " straight") + " -> tx " + tx.ToString("0.00") + "  lands " + f.LandX.ToString("0.00") + "," + f.LandZ.ToString("0.00") + "  side " + f.SideBefore + "  peak " + f.Peak.ToString("0.0") + (f.Back ? " BACK" : ""));
                    }
            Console.WriteLine("LOBS " + (touch ? "(touch)" : "(hit)  ") + "  " + n + " shots: lands at " + (sumZ / Math.Max(1, n - 0)).ToString("0.00") + " m on average (min " + minZ.ToString("0.0") +
                ")  deep (7.8 m+) " + (100 * deep / n) + "%  short (<6.5 m) " + (100 * short_ / n) + "%  " + (sumOff / n).ToString("0.00") + " m off the side wall  side wall before the bounce " + (100 * sideTouch / n) + "%");
        }

        // ---- drops: pace and bounce ----
        {
            int n = 0; double sumPace = 0, minPace = 99, sumZ = 0;
            foreach (double bz in new[] { 2.5, 4.5, 6.5 })
                foreach (double bx in new[] { -2.2, -1.0, 1.0, 2.2 })
                    foreach (double miss in new[] { 0.0, 0.5, 1.0 })
                    {
                        ball.Reset(new V3(bx, 0.5, bz), new V3(0, 0, 0), null, 40);
                        double aim = Math.Sign(bx) * 2.0, ty = CourtDimensions.TIN_H + c.DropHeightAboveTin;
                        double tx = ArcadeStroke.PullToLine(aim, ArcadeStroke.ShotLine(bx, bz, "drop", aim), true);
                        double reach = Math.Sqrt((tx - bx) * (tx - bx) + bz * bz);
                        double speed = Math.Max(c.DropSpeedMin, Math.Min(c.DropSpeedMax, reach / c.DropFlightTime));
                        speed = ArcadeStroke.MissPace(speed, "drop", miss, true);
                        st.Solve(ball, surfaces, tx, ty, 0, 0, speed);
                        var f = Fly(ball, st.LaunchVelocity.Clone(), surfaces);
                        n++; sumPace += speed; minPace = Math.Min(minPace, speed); if (f.LandZ > 0) sumZ += f.LandZ;
                    }
            Console.WriteLine("DROPS  " + n + " shots: pace " + (sumPace / n).ToString("0.0") + " m/s on average, slowest " + minPace.ToString("0.0") + "  first bounce " + (sumZ / n).ToString("0.00") + " m from the front wall");
        }

        // ---- crosses from the front (a dive's ball), with and without the dive's lost line ----
        foreach (bool lost in new[] { false, true })
        {
            int n = 0, pinball = 0, twoWalls = 0; double sumAngle = 0;
            foreach (double bz in new[] { 0.5, 0.9, 1.4, 2.0 })
                foreach (double bx in new[] { -2.8, -2.2, -1.5, 1.5, 2.2, 2.8 })
                    foreach (string shot in new[] { "drive", "drop" })
                    {
                        ball.Reset(new V3(bx, 0.35, bz), new V3(0, 0, 0), null, 40);
                        double aim = -Math.Sign(bx) * 2.6, ty = shot == "drop" ? CourtDimensions.TIN_H + c.DropHeightAboveTin : 1.4;
                        double speed = shot == "drop" ? c.DropSpeedMin : c.DriveSpeed;
                        double line = shot == "drop" ? ArcadeStroke.ShotLine(bx, bz, shot, aim) : st.LineFor(ball, surfaces, shot, aim, ty, speed);
                        double tx = ArcadeStroke.PullToLine(aim, line, true);
                        if (lost) tx += -Math.Sign(bx) * c.AssistMissOffset * 1.5;                // the bad contact's lost line, the worst way
                        tx = ArcadeStroke.KeepAngle(bx, bz, tx);
                        st.Solve(ball, surfaces, tx, ty, 0, 0, speed * (1 - GameTuning.Hit.MissPaceLoss));
                        var v = st.LaunchVelocity;
                        sumAngle += Math.Atan2(Math.Abs(v.X), Math.Abs(v.Z)) * 180 / Math.PI;
                        var f = Fly(ball, v.Clone(), surfaces);
                        n++; if (f.SideTo2 >= 3) pinball++; if (f.SideTo2 >= 2) twoWalls++;
                    }
            Console.WriteLine("FRONT CROSSES " + (lost ? "(line lost)" : "(clean)    ") + "  " + n + " shots: launch " + (sumAngle / n).ToString("0") + " deg off straight on average  side walls before the 2nd bounce: 2+ " +
                (100 * twoWalls / n) + "%, 3+ (pinball) " + (100 * pinball / n) + "%");
        }
    }
}
