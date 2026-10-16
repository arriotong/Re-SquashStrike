// ===========================================================
// Assets/SquashSim/Court/CourtDimensions.cs
// Port of lab/court/CourtDimensions.js. WSF singles court, metres.
//
// COORDINATES (sim space): +Y up, x = -HALF_W..+HALF_W side walls,
// z = 0 FRONT wall, z = LENGTH BACK wall (glass). Origin on the floor
// at the centre of the front wall.
// ===========================================================

namespace SquashSim
{
    public static class CourtDimensions
    {
        public const double WIDTH = 6.40;
        public const double HALF_W = WIDTH / 2;           // 3.20
        public const double LENGTH = 9.75;
        public const double FRONT_WALL_OUT_H = 4.57;
        public const double BACK_WALL_OUT_H = 2.13;
        public const double TIN_H = 0.48;
        public const double SERVICE_LINE_H = 1.78;
        public const double SHORT_LINE_Z = 4.26;
        public const double SERVICE_BOX = 1.60;
        public const double HALF_COURT_LINE_X = 0;

        /// <summary>Physical ceiling; closes the volume so a mishit cannot escape.</summary>
        public const double CEILING_H = 5.64;

        /// <summary>Catcher plane behind the back wall for balls hit over the glass.</summary>
        public const double OUT_BACKSTOP_Z = LENGTH + 2.0;

        public static bool IsInsideCourt(double x, double z, double margin = 0)
        {
            return x >= -HALF_W + margin && x <= HALF_W - margin &&
                   z >= margin && z <= LENGTH - margin;
        }

        /// <summary>Height of the sloping side-wall out line at depth z.</summary>
        public static double SideWallOutHeight(double z)
        {
            double t = DMath.Max(0, DMath.Min(1, z / LENGTH));
            return FRONT_WALL_OUT_H + (BACK_WALL_OUT_H - FRONT_WALL_OUT_H) * t;
        }
    }
}
