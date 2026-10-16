// ===========================================================
// Assets/SquashSim/Math/DMath.cs
// Deterministic math, C# side.
//
// This file and Tools/TraceRecorder/dmath.mjs are THE SAME
// ALGORITHMS, line for line: ports of fdlibm. Owning the
// implementation is what makes the simulation bit-identical
// between Node and .NET, and between two players' PCs (rollback
// netcode requires that).
//
// + - * / and Math.Sqrt are IEEE-754 exact and used natively.
// Everything transcendental in SquashSim.Core goes through here.
// NEVER call System.Math.Sin/Cos/Atan2/Exp/Pow from the sim.
//
// If you change one file, change the other identically.
// ===========================================================

using System;

namespace SquashSim
{
    public static class DMath
    {
        public const double PI = 3.141592653589793;

        static int HI(double x) { return (int)(BitConverter.DoubleToInt64Bits(x) >> 32); }
        static int LO(double x) { return (int)BitConverter.DoubleToInt64Bits(x); }

        static double WithHI(double x, int hi)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            return BitConverter.Int64BitsToDouble(((long)hi << 32) | (bits & 0xffffffffL));
        }

        static double FromWords(int hi, int lo)
        {
            return BitConverter.Int64BitsToDouble(((long)hi << 32) | ((long)lo & 0xffffffffL));
        }

        // ---------------- sin / cos ----------------

        const double S1 = -1.66666666666666324348e-01;
        const double S2 = 8.33333333332248946124e-03;
        const double S3 = -1.98412698298579493134e-04;
        const double S4 = 2.75573137070700676789e-06;
        const double S5 = -2.50507602534068634195e-08;
        const double S6 = 1.58969099521155010221e-10;

        static double KernelSin(double x, double y, int iy)
        {
            int ix = HI(x) & 0x7fffffff;
            if (ix < 0x3e400000) { if ((int)x == 0) return x; }
            double z = x * x;
            double v = z * x;
            double r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
            if (iy == 0) return x + v * (S1 + z * r);
            return x - ((z * (0.5 * y - v * r) - y) - v * S1);
        }

        const double C1 = 4.16666666666666019037e-02;
        const double C2 = -1.38888888888741095749e-03;
        const double C3 = 2.48015872894767294178e-05;
        const double C4 = -2.75573143513906633035e-07;
        const double C5 = 2.08757232129817482790e-09;
        const double C6 = -1.13596475577881948265e-11;

        static double KernelCos(double x, double y)
        {
            int ix = HI(x) & 0x7fffffff;
            if (ix < 0x3e400000) { if ((int)x == 0) return 1.0; }
            double z = x * x;
            double r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
            if (ix < 0x3FD33333) return 1.0 - (0.5 * z - (z * r - x * y));
            double qx;
            if (ix > 0x3fe90000) qx = 0.28125;
            else qx = FromWords(ix - 0x00200000, 0);
            double hz = 0.5 * z - qx;
            double a = 1.0 - qx;
            return a - (hz - (z * r - x * y));
        }

        const double Invpio2 = 6.36619772367581382433e-01;
        const double Pio2_1 = 1.57079632673412561417e+00;
        const double Pio2_1t = 6.07710050650619224932e-11;
        const double Pio2_2 = 6.07710050630396597660e-11;
        const double Pio2_2t = 2.02226624879595063154e-21;
        const double Pio2_3 = 2.02226624871116645580e-21;
        const double Pio2_3t = 8.47842766036889956997e-32;

        // Argument reduction by pi/2 (fdlibm's medium-size path, used for
        // every finite argument). Returns the quadrant; y0 + y1 is the
        // reduced value.
        static long RemPio2(double x, out double y0, out double y1)
        {
            int hx = HI(x);
            int ix = hx & 0x7fffffff;
            if (ix <= 0x3fe921fb) { y0 = x; y1 = 0; return 0; }
            if (ix < 0x4002d97c)
            {
                double z;
                if (hx > 0)
                {
                    z = x - Pio2_1;
                    if (ix != 0x3ff921fb) { y0 = z - Pio2_1t; y1 = (z - y0) - Pio2_1t; }
                    else { z -= Pio2_2; y0 = z - Pio2_2t; y1 = (z - y0) - Pio2_2t; }
                    return 1;
                }
                z = x + Pio2_1;
                if (ix != 0x3ff921fb) { y0 = z + Pio2_1t; y1 = (z - y0) + Pio2_1t; }
                else { z += Pio2_2; y0 = z + Pio2_2t; y1 = (z - y0) + Pio2_2t; }
                return -1;
            }
            double t = Math.Abs(x);
            double nd = Math.Floor(t * Invpio2 + 0.5);
            long n = (long)nd;
            double fn = nd;
            double r = t - fn * Pio2_1;
            double w = fn * Pio2_1t;
            int j = ix >> 20;
            y0 = r - w;
            int i = j - ((HI(y0) >> 20) & 0x7ff);
            if (i > 16)
            {
                t = r;
                w = fn * Pio2_2;
                r = t - w;
                w = fn * Pio2_2t - ((t - r) - w);
                y0 = r - w;
                i = j - ((HI(y0) >> 20) & 0x7ff);
                if (i > 49)
                {
                    t = r;
                    w = fn * Pio2_3;
                    r = t - w;
                    w = fn * Pio2_3t - ((t - r) - w);
                    y0 = r - w;
                }
            }
            y1 = (r - y0) - w;
            if (hx < 0) { y0 = -y0; y1 = -y1; return -n; }
            return n;
        }

        public static double Sin(double x)
        {
            int ix = HI(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelSin(x, 0, 0);
            if (ix >= 0x7ff00000) return x - x;
            long n = RemPio2(x, out double y0, out double y1);
            switch ((int)(((n % 4) + 4) % 4))
            {
                case 0: return KernelSin(y0, y1, 1);
                case 1: return KernelCos(y0, y1);
                case 2: return -KernelSin(y0, y1, 1);
                default: return -KernelCos(y0, y1);
            }
        }

        public static double Cos(double x)
        {
            int ix = HI(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelCos(x, 0);
            if (ix >= 0x7ff00000) return x - x;
            long n = RemPio2(x, out double y0, out double y1);
            switch ((int)(((n % 4) + 4) % 4))
            {
                case 0: return KernelCos(y0, y1);
                case 1: return -KernelSin(y0, y1, 1);
                case 2: return -KernelCos(y0, y1);
                default: return KernelSin(y0, y1, 1);
            }
        }

        // ---------------- atan / atan2 ----------------

        static readonly double[] Atanhi =
        {
            4.63647609000806093515e-01,
            7.85398163397448278999e-01,
            9.82793723247329054082e-01,
            1.57079632679489655800e+00
        };
        static readonly double[] Atanlo =
        {
            2.26987774529616870924e-17,
            3.06161699786838301793e-17,
            1.39033110312309984516e-17,
            6.12323399573676603587e-17
        };
        static readonly double[] AT =
        {
            3.33333333333329318027e-01,
            -1.99999999998764832476e-01,
            1.42857142725034663711e-01,
            -1.11111104054623557880e-01,
            9.09088713343650656196e-02,
            -7.69187620504482999495e-02,
            6.66107313738753120669e-02,
            -5.83357013379057348645e-02,
            4.97687799461593236017e-02,
            -3.65315727442169155270e-02,
            1.62858201153657823623e-02
        };

        public static double Atan(double x)
        {
            int hx = HI(x);
            int ix = hx & 0x7fffffff;
            int id;
            if (ix >= 0x44100000)
            {
                if (double.IsNaN(x)) return x + x;
                return hx > 0 ? Atanhi[3] + Atanlo[3] : -Atanhi[3] - Atanlo[3];
            }
            if (ix < 0x3fdc0000)
            {
                if (ix < 0x3e200000) return x;
                id = -1;
            }
            else
            {
                x = Math.Abs(x);
                if (ix < 0x3ff30000)
                {
                    if (ix < 0x3fe60000) { id = 0; x = (2.0 * x - 1.0) / (2.0 + x); }
                    else { id = 1; x = (x - 1.0) / (x + 1.0); }
                }
                else if (ix < 0x40038000) { id = 2; x = (x - 1.5) / (1.0 + 1.5 * x); }
                else { id = 3; x = -1.0 / x; }
            }
            double z = x * x;
            double w = z * z;
            double s1 = z * (AT[0] + w * (AT[2] + w * (AT[4] + w * (AT[6] + w * (AT[8] + w * AT[10])))));
            double s2 = w * (AT[1] + w * (AT[3] + w * (AT[5] + w * (AT[7] + w * AT[9]))));
            if (id < 0) return x - x * (s1 + s2);
            double r = Atanhi[id] - ((x * (s1 + s2) - Atanlo[id]) - x);
            return hx < 0 ? -r : r;
        }

        const double PI_O_4 = 7.8539816339744827900e-01;
        const double PI_O_2 = 1.5707963267948965580e+00;
        const double PI_LO = 1.2246467991473531772e-16;

        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return x + y;
            int hx = HI(x), ix = hx & 0x7fffffff, lx = LO(x);
            int hy = HI(y), iy = hy & 0x7fffffff, ly = LO(y);
            if (x == 1.0) return Atan(y);
            int m = ((hy >> 31) & 1) | ((hx >> 30) & 2);

            if ((iy | ly) == 0)
            {
                switch (m)
                {
                    case 0: case 1: return y;
                    case 2: return PI;
                    default: return -PI;
                }
            }
            if ((ix | lx) == 0) return hy < 0 ? -PI_O_2 : PI_O_2;

            if (ix == 0x7ff00000)
            {
                if (iy == 0x7ff00000)
                {
                    switch (m)
                    {
                        case 0: return PI_O_4;
                        case 1: return -PI_O_4;
                        case 2: return 3.0 * PI_O_4;
                        default: return -3.0 * PI_O_4;
                    }
                }
                switch (m)
                {
                    case 0: return 0.0;
                    case 1: return -0.0;
                    case 2: return PI;
                    default: return -PI;
                }
            }
            if (iy == 0x7ff00000) return hy < 0 ? -PI_O_2 : PI_O_2;

            int k = (iy - ix) >> 20;
            double z;
            if (k > 60) z = PI_O_2 + 0.5 * PI_LO;
            else if (hx < 0 && k < -60) z = 0.0;
            else z = Atan(Math.Abs(y / x));
            switch (m)
            {
                case 0: return z;
                case 1: return -z;
                case 2: return PI - (z - PI_LO);
                default: return (z - PI_LO) - PI;
            }
        }

        // ---------------- exp ----------------

        const double O_THRESHOLD = 7.09782712893383973096e+02;
        const double U_THRESHOLD = -7.45133219101941108420e+02;
        static readonly double[] LN2HI = { 6.93147180369123816490e-01, -6.93147180369123816490e-01 };
        static readonly double[] LN2LO = { 1.90821492927058770002e-10, -1.90821492927058770002e-10 };
        static readonly double[] HALF = { 0.5, -0.5 };
        const double INVLN2 = 1.44269504088896338700e+00;
        const double P1 = 1.66666666666666019037e-01;
        const double P2 = -2.77777777770155933842e-03;
        const double P3 = 6.61375632143793436117e-05;
        const double P4 = -1.65339022054652515390e-06;
        const double P5 = 4.13813679705723846039e-08;
        const double TWOM1000 = 9.33263618503218878990e-302;

        public static double Exp(double x)
        {
            int hx = HI(x);
            int xsb = (hx >> 31) & 1;
            hx &= 0x7fffffff;

            if (hx >= 0x40862E42)
            {
                if (hx >= 0x7ff00000)
                {
                    if (double.IsNaN(x)) return x + x;
                    return xsb == 0 ? x : 0.0;
                }
                if (x > O_THRESHOLD) return double.PositiveInfinity;
                if (x < U_THRESHOLD) return 0.0;
            }

            double hi = 0, lo = 0;
            int k;
            if (hx > 0x3fd62e42)
            {
                if (hx < 0x3FF0A2B2)
                {
                    hi = x - LN2HI[xsb]; lo = LN2LO[xsb]; k = 1 - xsb - xsb;
                }
                else
                {
                    k = (int)(INVLN2 * x + HALF[xsb]);
                    double tk = k;
                    hi = x - tk * LN2HI[0];
                    lo = tk * LN2LO[0];
                }
                x = hi - lo;
            }
            else if (hx < 0x3e300000)
            {
                return 1.0 + x;
            }
            else
            {
                k = 0;
            }

            double t = x * x;
            double c = x - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
            if (k == 0) return 1.0 - ((x * c) / (c - 2.0) - x);
            double y = 1.0 - ((lo - (x * c) / (2.0 - c)) - hi);
            if (k >= -1021) return WithHI(y, unchecked(HI(y) + (k << 20)));
            return WithHI(y, unchecked(HI(y) + ((k + 1000) << 20))) * TWOM1000;
        }

        // ---------------- log ----------------

        const double LN2_HI = 6.93147180369123816490e-01;
        const double LN2_LO = 1.90821492927058770002e-10;
        const double TWO54 = 1.80143985094819840000e+16;
        const double Lg1 = 6.666666666666735130e-01;
        const double Lg2 = 3.999999999940941908e-01;
        const double Lg3 = 2.857142874366239149e-01;
        const double Lg4 = 2.222219843214978396e-01;
        const double Lg5 = 1.818357216161805012e-01;
        const double Lg6 = 1.531383769920937332e-01;
        const double Lg7 = 1.479819860511658591e-01;

        public static double Log(double x)
        {
            int hx = HI(x);
            int lx = LO(x);
            int k = 0;
            if (hx < 0x00100000)
            {
                if (((hx & 0x7fffffff) | lx) == 0) return double.NegativeInfinity;
                if (hx < 0) return double.NaN;
                k -= 54; x *= TWO54;
                hx = HI(x);
            }
            if (hx >= 0x7ff00000) return x + x;
            k += (hx >> 20) - 1023;
            hx &= 0x000fffff;
            int i = (hx + 0x95f64) & 0x100000;
            x = WithHI(x, hx | (i ^ 0x3ff00000));
            k += (i >> 20);
            double f = x - 1.0;
            double dk;
            if ((0x000fffff & (2 + hx)) < 3)
            {
                if (f == 0.0)
                {
                    if (k == 0) return 0.0;
                    dk = k;
                    return dk * LN2_HI + dk * LN2_LO;
                }
                double R0 = f * f * (0.5 - 0.33333333333333333 * f);
                if (k == 0) return f - R0;
                dk = k;
                return dk * LN2_HI - ((R0 - dk * LN2_LO) - f);
            }
            double s = f / (2.0 + f);
            dk = k;
            double z = s * s;
            i = hx - 0x6147a;
            double w = z * z;
            int j = 0x6b851 - hx;
            double t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
            double t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            i |= j;
            double R = t2 + t1;
            if (i > 0)
            {
                double hfsq = 0.5 * f * f;
                if (k == 0) return f - (hfsq - s * (hfsq + R));
                return dk * LN2_HI - ((hfsq - (s * (hfsq + R) + dk * LN2_LO)) - f);
            }
            if (k == 0) return f - s * (f - R);
            return dk * LN2_HI - ((s * (f - R) - dk * LN2_LO) - f);
        }

        // ---------------- pow ----------------
        // fdlibm e_pow.c for a finite, positive base and finite exponent - the
        // only case the simulation produces (response curves on magnitudes).

        static readonly double[] BP = { 1.0, 1.5 };
        static readonly double[] DP_H = { 0.0, 5.84962487220764160156e-01 };
        static readonly double[] DP_L = { 0.0, 1.35003920212974897128e-08 };
        const double TWO53 = 9007199254740992.0;
        const double L1 = 5.99999999999994648725e-01;
        const double L2 = 4.28571428578550184252e-01;
        const double L3 = 3.33333329818377432918e-01;
        const double L4 = 2.72728123808534006489e-01;
        const double L5 = 2.30660745775561754067e-01;
        const double L6 = 2.06975017800338417784e-01;
        const double LG2 = 6.93147180559945286227e-01;
        const double LG2_H = 6.93147182464599609375e-01;
        const double LG2_L = -1.90465429995776804525e-09;
        const double OVT = 8.0085662595372944372e-17;
        const double CP = 9.61796693925975554329e-01;
        const double CP_H = 9.61796700954437255859e-01;
        const double CP_L = -7.02846165095275826516e-09;
        const double IVLN2 = 1.44269504088896338700e+00;
        const double IVLN2_H = 1.44269502162933349609e+00;
        const double IVLN2_L = 1.92596299112661746887e-08;

        static double WithLO(double x, int lo)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            return BitConverter.Int64BitsToDouble((bits & unchecked((long)0xffffffff00000000L)) | ((long)lo & 0xffffffffL));
        }

        public static double Pow(double x, double y)
        {
            if (y == 0) return 1.0;
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == 1.0) return 1.0;
            if (y == 1.0) return x;
            if (x == 0) return y > 0 ? 0.0 : double.PositiveInfinity;
            if (!(x > 0) || double.IsInfinity(x) || double.IsInfinity(y)) return Exp(y * Log(x));
            if (y == 2.0) return x * x;
            if (y == 0.5) return Math.Sqrt(x);
            if (y == -1.0) return 1.0 / x;

            int hy = HI(y);
            int ix = HI(x) & 0x7fffffff;
            int iy = hy & 0x7fffffff;
            double ax = x;
            double t1, t2;
            int n;

            if (iy > 0x41e00000)
            {
                if (iy > 0x43f00000)
                {
                    if (ix <= 0x3fefffff) return hy < 0 ? double.PositiveInfinity : 0.0;
                    if (ix >= 0x3ff00000) return hy > 0 ? double.PositiveInfinity : 0.0;
                }
                if (ix < 0x3fefffff) return hy < 0 ? double.PositiveInfinity : 0.0;
                if (ix > 0x3ff00000) return hy > 0 ? double.PositiveInfinity : 0.0;
                double t = ax - 1.0;
                double w = (t * t) * (0.5 - t * (0.3333333333333333333333 - t * 0.25));
                double u = IVLN2_H * t;
                double v = t * IVLN2_L - w * IVLN2;
                t1 = WithLO(u + v, 0);
                t2 = v - (t1 - u);
            }
            else
            {
                n = 0;
                if (ix < 0x00100000) { ax *= TWO53; n -= 53; ix = HI(ax); }
                n += (ix >> 20) - 0x3ff;
                int j = ix & 0x000fffff;
                int k;
                ix = j | 0x3ff00000;
                if (j <= 0x3988E) k = 0;
                else if (j < 0xBB67A) k = 1;
                else { k = 0; n += 1; ix -= 0x00100000; }
                ax = WithHI(ax, ix);

                double u = ax - BP[k];
                double v = 1.0 / (ax + BP[k]);
                double ss = u * v;
                double s_h = WithLO(ss, 0);
                double t_h = FromWords(((ix >> 1) | 0x20000000) + 0x00080000 + (k << 18), 0);
                double t_l = ax - (t_h - BP[k]);
                double s_l = v * ((u - s_h * t_h) - s_h * t_l);
                double s2 = ss * ss;
                double r = s2 * s2 * (L1 + s2 * (L2 + s2 * (L3 + s2 * (L4 + s2 * (L5 + s2 * L6)))));
                r += s_l * (s_h + ss);
                s2 = s_h * s_h;
                t_h = WithLO(3.0 + s2 + r, 0);
                t_l = r - ((t_h - 3.0) - s2);
                u = s_h * t_h;
                v = s_l * t_h + t_l * ss;
                double p_h0 = WithLO(u + v, 0);
                double p_l0 = v - (p_h0 - u);
                double z_h = CP_H * p_h0;
                double z_l = CP_L * p_h0 + p_l0 * CP + DP_L[k];
                double tn = n;
                t1 = WithLO(((z_h + z_l) + DP_H[k]) + tn, 0);
                t2 = z_l - (((t1 - tn) - DP_H[k]) - z_h);
            }

            double y1 = WithLO(y, 0);
            double p_l = (y - y1) * t1 + y * t2;
            double p_h = y1 * t1;
            double z = p_l + p_h;
            int jz = HI(z);
            int iz = LO(z);
            if (jz >= 0x40900000)
            {
                if (((jz - 0x40900000) | iz) != 0) return double.PositiveInfinity;
                if (p_l + OVT > z - p_h) return double.PositiveInfinity;
            }
            else if ((jz & 0x7fffffff) >= 0x4090cc00)
            {
                if ((unchecked(jz - (int)0xc090cc00) | iz) != 0) return 0.0;
                if (p_l <= z - p_h) return 0.0;
            }

            int i = jz & 0x7fffffff;
            int kk = (i >> 20) - 0x3ff;
            n = 0;
            if (i > 0x3fe00000)
            {
                n = unchecked(jz + (0x00100000 >> (kk + 1)));
                kk = ((n & 0x7fffffff) >> 20) - 0x3ff;
                double tt0 = FromWords(n & ~(0x000fffff >> kk), 0);
                n = ((n & 0x000fffff) | 0x00100000) >> (20 - kk);
                if (jz < 0) n = -n;
                p_h -= tt0;
            }
            double tq = WithLO(p_l + p_h, 0);
            double uq = tq * LG2_H;
            double vq = (p_l - (tq - p_h)) * LG2 + tq * LG2_L;
            z = uq + vq;
            double wq = vq - (z - uq);
            double tt = z * z;
            double tt1 = z - tt * (P1 + tt * (P2 + tt * (P3 + tt * (P4 + tt * P5))));
            double rq = (z * tt1) / (tt1 - 2.0) - (wq + z * wq);
            z = 1.0 - (rq - z);
            int jr = unchecked(HI(z) + (n << 20));
            if ((jr >> 20) <= 0) return z * Math.Pow(2, n);   // subnormal result: never reached by the sim
            return WithHI(z, jr);
        }

        // ---------------- hypot ----------------
        // V8's algorithm: scale by the largest magnitude, Kahan-compensated
        // sum of squares, sqrt, rescale (matches dmath.mjs and Node's
        // native Math.hypot bit for bit).
        public static double Hypot(double a, double b)
        {
            a = Math.Abs(a); b = Math.Abs(b);
            if (double.IsPositiveInfinity(a) || double.IsPositiveInfinity(b)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            double max = 0;
            if (a > max) max = a;
            if (b > max) max = b;
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            KahanAdd(ref sum, ref comp, a / max);
            KahanAdd(ref sum, ref comp, b / max);
            return Math.Sqrt(sum) * max;
        }

        public static double Hypot(double a, double b, double c)
        {
            a = Math.Abs(a); b = Math.Abs(b); c = Math.Abs(c);
            if (double.IsPositiveInfinity(a) || double.IsPositiveInfinity(b) || double.IsPositiveInfinity(c))
                return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c)) return double.NaN;
            double max = 0;
            if (a > max) max = a;
            if (b > max) max = b;
            if (c > max) max = c;
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            KahanAdd(ref sum, ref comp, a / max);
            KahanAdd(ref sum, ref comp, b / max);
            KahanAdd(ref sum, ref comp, c / max);
            return Math.Sqrt(sum) * max;
        }

        static void KahanAdd(ref double sum, ref double compensation, double v)
        {
            double summand = v * v - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
        }

        // ---------------- exact helpers (JS semantics) ----------------

        /// <summary>JS Math.round: rounds half toward +infinity.</summary>
        public static double RoundJs(double x) { return Math.Floor(x + 0.5); }

        /// <summary>JS Math.sign (returns +-0 for +-0, NaN for NaN).</summary>
        public static double Sign(double x)
        {
            if (x > 0) return 1.0;
            if (x < 0) return -1.0;
            return x;
        }

        /// <summary>JS Math.min for two arguments (NaN-propagating, -0 &lt; +0).</summary>
        public static double Min(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a == 0 && b == 0) return (BitConverter.DoubleToInt64Bits(a) < 0) ? a : b;
            return a < b ? a : b;
        }

        /// <summary>JS Math.max for two arguments (NaN-propagating, +0 &gt; -0).</summary>
        public static double Max(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a == 0 && b == 0) return (BitConverter.DoubleToInt64Bits(a) < 0) ? b : a;
            return a > b ? a : b;
        }
    }
}
