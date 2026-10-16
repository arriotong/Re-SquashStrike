// ===========================================================
// Assets/SquashSim/Player/ShotInterpreter.cs
// Port of lab/player/ShotInterpreter.js. A READER: names what the
// control system thinks you are attempting, for debug, tutorials,
// analytics and AI cues. It has NO path to the ball. Delete it and no
// ball outcome changes.
// ===========================================================

namespace SquashSim
{
    public sealed class ShotLabel
    {
        public string Label;
        public string Family;
        public ShotLabel(string label, string family) { Label = label; Family = family; }
    }

    public static class ShotInterpreter
    {
        const double CROSS_DEG = 14;

        public static ShotLabel Interpret(AimState aim, string stroke, bool isVolley)
        {
            bool isTouch = stroke == "touch";
            bool sideWall = aim.IntendedWall == "sideWallLeft" || aim.IntendedWall == "sideWallRight";
            double lateral = System.Math.Abs(aim.DesiredYaw) * 180 / DMath.PI;
            double up = aim.BiasY;

            string label;
            string family;

            if (sideWall)
            {
                family = "boast";
                label = isTouch ? "SOFT BOAST" : "BOAST";
            }
            else if (isTouch)
            {
                family = "touch";
                if (up > 0.45) label = "LOB-LIKE TOUCH";
                else label = lateral > CROSS_DEG ? "CROSS-COURT DROP" : "DROP";
            }
            else if (up > 0.55)
            {
                family = "lob";
                label = lateral > CROSS_DEG ? "CROSS LOB" : "HIGH LENGTH";
            }
            else if (up < -0.35)
            {
                family = "attack";
                label = lateral > CROSS_DEG ? "LOW CROSS" : "LOW ATTACK";
            }
            else
            {
                family = "drive";
                label = lateral > CROSS_DEG ? "CROSS-COURT" : "STRAIGHT";
            }

            if (isVolley) label = "VOLLEY " + label;
            return new ShotLabel(label, family);
        }
    }
}
