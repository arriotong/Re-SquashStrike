// ===========================================================
// Assets/SquashSim/Arcade/Feeder.cs
// P2-M3: the feed machine's aim, shared by the Target Arcade, the
// drills and the Academy: launch from a point in the court so the ball
// meets the front wall at (wx, wy) at about this pace.
// ===========================================================

namespace SquashSim.Arcade
{
    public static class Feeder
    {
        /// <summary>Configure the launcher (from (px, 1.0, pz) to the front-wall point (wx, wy)) and fire.</summary>
        public static void Front(Lab.BallLauncher launcher, double px, double pz, double wx, double wy, double speed, double temperature = 40)
        {
            double py = 1.0;
            double dx = wx - px, dz = pz, horiz = System.Math.Sqrt(dx * dx + dz * dz);
            double t = horiz / speed;
            double vy = (wy - py) / t + 0.5 * PhysicsConfig.Current.World.Gravity * t;
            var cfg = launcher.Config;
            cfg.Position.X = px; cfg.Position.Y = py; cfg.Position.Z = pz;
            cfg.Speed = System.Math.Sqrt(speed * speed + vy * vy);
            cfg.AzimuthDeg = DMath.Atan2(dx, dz) * 180 / DMath.PI;
            cfg.ElevationDeg = DMath.Atan2(vy, speed) * 180 / DMath.PI;
            cfg.Temperature = temperature;
            cfg.Spin.X = 0; cfg.Spin.Y = 0; cfg.Spin.Z = 0;
            launcher.SetRepeat(false);
            launcher.Fire(fresh: true);
        }
    }

    /// <summary>
    /// One shot's life, read from the sim's events: the feed meets the front wall -> the player strikes it ->
    /// its first front-wall impact -> its first floor bounce -> done (second bounce, down, out, never struck).
    /// </summary>
    public sealed class ShotTracker
    {
        public enum Phase { None, FeedFrontWall, FrontWall, FirstBounce, Done }

        public bool FeedFront { get; private set; }
        public bool Struck { get; private set; }
        public bool FrontAfterContact { get; private set; }
        public int FloorsAfterFront { get; private set; }
        public SimEvent Contact { get; private set; }
        public V3 FrontPoint { get; private set; }
        public V3 FirstBounce { get; private set; }
        /// <summary>How it ended: "landed" (two bounces after the front wall), "down", "out", "missed" (never struck).</summary>
        public string Result { get; private set; }
        int _feedFloors;

        public void Reset()
        {
            FeedFront = false; Struck = false; FrontAfterContact = false; FloorsAfterFront = 0; _feedFloors = 0;
            Contact = null; FrontPoint = null; FirstBounce = null; Result = null;
        }

        /// <summary>For a shot struck from the hand (a serve): there is no feed to meet the wall first.</summary>
        public void FromHand() { FeedFront = true; }

        public void OnContact(SimEvent e)
        {
            Struck = true; Contact = e; FrontAfterContact = false; FloorsAfterFront = 0; FrontPoint = null; FirstBounce = null;
        }

        public Phase OnImpact(SimEvent e)
        {
            if (Result != null) return Phase.None;
            string cls = e.SurfaceClass;
            if (!Struck)
            {
                if (cls == "frontWall" && !FeedFront) { FeedFront = true; return Phase.FeedFrontWall; }
                if (FeedFront && cls == "floor" && ++_feedFloors >= 2) { Result = "missed"; return Phase.Done; }
                return Phase.None;
            }
            if (RallyReferee.OutReason(e) != null) { Result = "out"; return Phase.Done; }
            if (!FrontAfterContact)
            {
                if (cls == "frontWall") { FrontAfterContact = true; FrontPoint = e.Point.Clone(); return Phase.FrontWall; }
                if (cls == "floor" || cls == "tin") { Result = "down"; return Phase.Done; }
                return Phase.None;
            }
            if (cls == "floor")
            {
                FloorsAfterFront++;
                if (FloorsAfterFront == 1) { FirstBounce = e.Point.Clone(); return Phase.FirstBounce; }
                Result = "landed";
                return Phase.Done;
            }
            return Phase.None;
        }
    }
}
