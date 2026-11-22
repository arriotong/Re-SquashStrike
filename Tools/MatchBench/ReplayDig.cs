// ===========================================================
// Tools/MatchBench/ReplayDig.cs
// 2026-11-22 a person's recorded match, rally by rally: the notes (focus,
// special, dive), the strokes, how it ended - and, near the person, what
// stopped a hit. Diagnosis only.
//   --dig <file.ssr>
// ===========================================================

using System;
using System.IO;
using SquashSim;
using SquashSim.Replays;

static class ReplayDig
{
    public static void Run(string path)
    {
        var file = ReplayFile.FromBytes(File.ReadAllBytes(path));
        var r = new ReplaySim(file);
        var m = r.Sim;
        int notes = 0, rally = 0, hits = 0; double rallyStart = 0;
        string line = "";
        bool[] hitHeld = new bool[2]; int pressAt = -1;
        r.RallyFinished += i =>
        {
            var o = m.History[m.History.Count - 1];
            Console.WriteLine("R" + (i + 1).ToString().PadLeft(2) + " srv " + m.Score.Server + " shots " + m.Referee.Shots + " " + o.Kind + " won by " + o.Winner + " (" + o.Reason + ")" + line);
            line = "";
        };
        while (!r.Done)
        {
            var it = m.Intents[0];
            bool press = it != null && (it.Hit.Pressed || it.Touch.Pressed);
            double t = m.Time;
            int shotsBefore = m.Referee.Shots;
            r.Step();
            foreach (var ev in m.StepEvents)
            {
                if (ev.Type == SimEventType.RacketContact) { var pl = m.Players[ev.PlayerIndex].Player; line += " || hit by " + ev.PlayerIndex + " " + ev.Stroke + "/" + ev.Mode + " ball(" + ev.Point.X.ToString("0.00") + "," + ev.Point.Y.ToString("0.00") + "," + ev.Point.Z.ToString("0.00") + ") me(" + pl.Pos.X.ToString("0.00") + "," + pl.Pos.Z.ToString("0.00") + ") v(" + m.Ball.Vel.X.ToString("0.0") + "," + m.Ball.Vel.Y.ToString("0.0") + "," + m.Ball.Vel.Z.ToString("0.0") + ")"; }
                else if (ev.Type == SimEventType.BodyHit) line += " || BODY " + ev.PlayerIndex + " at(" + ev.InPos.X.ToString("0.00") + "," + ev.InPos.Y.ToString("0.00") + "," + ev.InPos.Z.ToString("0.00") + ") v(" + ev.InVel.X.ToString("0.0") + "," + ev.InVel.Y.ToString("0.0") + "," + ev.InVel.Z.ToString("0.0") + ")";
                else if (ev.Type == SimEventType.Impact) line += " ~" + ev.SurfaceId + "(" + ev.Point.X.ToString("0.0") + "," + ev.Point.Y.ToString("0.0") + "," + ev.Point.Z.ToString("0.0") + ")";
            }
            while (notes < m.Chaos.Count) { var n = m.Chaos[notes++]; line += "  [" + n.Player + ":" + n.Kind + " @" + (n.Time).ToString("0.00") + "]"; }
            var p = m.Players[0].Player; var b = m.Ball;
            double dx = b.Pos.X - p.Pos.X, dz = b.Pos.Z - p.Pos.Z, d = Math.Sqrt(dx * dx + dz * dz);
            if (press && m.Phase == MatchPhase.Rally) line += "  {press d=" + d.ToString("0.0") + " y=" + b.Pos.Y.ToString("0.0") + (m.Referee.ExpectedStriker == 0 ? "" : " notmine") + (m.Players[0].Player.Mods != null ? " rc=" + m.Players[0].Player.Mods.ReachCut.ToString("0.0") : "") + (m.Focusing(0) ? " F" : "") + " sw=" + m.Players[0].Swing.State + "}";
        }
        var su = file.Setup; Console.WriteLine("opp " + su.Opponent + " d " + su.Difficulty + " class1 " + su.Class1 + " class0 " + su.Class0 + " mods1 " + string.Join(",", (su.Mods1 ?? new System.Collections.Generic.List<RunMod>()).ConvertAll(x => x.Id + x.Level)) + " attr1 " + (su.Attr1 == null ? "-" : su.Attr1.Power + "/" + su.Attr1.Control));
        Console.WriteLine("mods0: " + string.Join(",", (file.Setup.Mods0 ?? new System.Collections.Generic.List<RunMod>()).ConvertAll(x => x.Id)) + "   diverged: " + r.Diverged);
    }
}
