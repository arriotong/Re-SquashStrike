/* ===========================================================
   Tools/TraceRecorder/dmath.mjs
   Deterministic math, JavaScript side.

   This file and Assets/SquashSim/Math/DMath.cs are THE SAME
   ALGORITHMS, line for line. Both are ports of fdlibm (Sun's
   freely distributable libm), which V8 and most C runtimes are
   derived from, but which no runtime guarantees to use. Owning the
   implementation is what makes the simulation bit-identical
   between Node and .NET, and between two players' PCs.

   + - * / and sqrt are IEEE-754 exact in both runtimes and are
   used natively. Everything transcendental goes through here.

   If you change one file, change the other identically.
=========================================================== */

const _f64 = new Float64Array(1);
const _i32 = new Int32Array(_f64.buffer);   // little-endian: [0] = low, [1] = high

function HI(x) { _f64[0] = x; return _i32[1]; }
function LO(x) { _f64[0] = x; return _i32[0]; }
function withHI(x, hi) { _f64[0] = x; _i32[1] = hi; return _f64[0]; }
function fromWords(hi, lo) { _i32[1] = hi; _i32[0] = lo; return _f64[0]; }

/* ---------------- sin / cos ---------------- */

const S1 = -1.66666666666666324348e-01;
const S2 = 8.33333333332248946124e-03;
const S3 = -1.98412698298579493134e-04;
const S4 = 2.75573137070700676789e-06;
const S5 = -2.50507602534068634195e-08;
const S6 = 1.58969099521155010221e-10;

function kernelSin(x, y, iy) {
  const ix = HI(x) & 0x7fffffff;
  if (ix < 0x3e400000) { if ((x | 0) === 0) return x; }
  const z = x * x;
  const v = z * x;
  const r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
  if (iy === 0) return x + v * (S1 + z * r);
  return x - ((z * (0.5 * y - v * r) - y) - v * S1);
}

const C1 = 4.16666666666666019037e-02;
const C2 = -1.38888888888741095749e-03;
const C3 = 2.48015872894767294178e-05;
const C4 = -2.75573143513906633035e-07;
const C5 = 2.08757232129817482790e-09;
const C6 = -1.13596475577881948265e-11;

function kernelCos(x, y) {
  const ix = HI(x) & 0x7fffffff;
  if (ix < 0x3e400000) { if ((x | 0) === 0) return 1.0; }
  const z = x * x;
  const r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
  if (ix < 0x3FD33333) return 1.0 - (0.5 * z - (z * r - x * y));
  let qx;
  if (ix > 0x3fe90000) qx = 0.28125;
  else qx = fromWords(ix - 0x00200000, 0);
  const hz = 0.5 * z - qx;
  const a = 1.0 - qx;
  return a - (hz - (z * r - x * y));
}

const invpio2 = 6.36619772367581382433e-01;
const pio2_1 = 1.57079632673412561417e+00;
const pio2_1t = 6.07710050650619224932e-11;
const pio2_2 = 6.07710050630396597660e-11;
const pio2_2t = 2.02226624879595063154e-21;
const pio2_3 = 2.02226624871116645580e-21;
const pio2_3t = 8.47842766036889956997e-32;

/* Argument reduction by pi/2 (fdlibm's medium-size path, used for every
   finite argument; the simulation only ever sees small angles). Writes
   the reduced value into y[0] + y[1] and returns the quadrant. */
function remPio2(x, y) {
  const hx = HI(x);
  const ix = hx & 0x7fffffff;
  if (ix <= 0x3fe921fb) { y[0] = x; y[1] = 0; return 0; }
  if (ix < 0x4002d97c) {
    let z;
    if (hx > 0) {
      z = x - pio2_1;
      if (ix !== 0x3ff921fb) { y[0] = z - pio2_1t; y[1] = (z - y[0]) - pio2_1t; }
      else { z -= pio2_2; y[0] = z - pio2_2t; y[1] = (z - y[0]) - pio2_2t; }
      return 1;
    }
    z = x + pio2_1;
    if (ix !== 0x3ff921fb) { y[0] = z + pio2_1t; y[1] = (z - y[0]) + pio2_1t; }
    else { z += pio2_2; y[0] = z + pio2_2t; y[1] = (z - y[0]) + pio2_2t; }
    return -1;
  }
  let t = Math.abs(x);
  const n = Math.floor(t * invpio2 + 0.5);
  const fn = n;
  let r = t - fn * pio2_1;
  let w = fn * pio2_1t;
  const j = ix >> 20;
  y[0] = r - w;
  let i = j - ((HI(y[0]) >> 20) & 0x7ff);
  if (i > 16) {
    t = r;
    w = fn * pio2_2;
    r = t - w;
    w = fn * pio2_2t - ((t - r) - w);
    y[0] = r - w;
    i = j - ((HI(y[0]) >> 20) & 0x7ff);
    if (i > 49) {
      t = r;
      w = fn * pio2_3;
      r = t - w;
      w = fn * pio2_3t - ((t - r) - w);
      y[0] = r - w;
    }
  }
  y[1] = (r - y[0]) - w;
  if (hx < 0) { y[0] = -y[0]; y[1] = -y[1]; return -n; }
  return n;
}

const _y = new Float64Array(2);

export function sin(x) {
  const ix = HI(x) & 0x7fffffff;
  if (ix <= 0x3fe921fb) return kernelSin(x, 0, 0);
  if (ix >= 0x7ff00000) return x - x;
  const n = remPio2(x, _y);
  switch (((n % 4) + 4) % 4) {
    case 0: return kernelSin(_y[0], _y[1], 1);
    case 1: return kernelCos(_y[0], _y[1]);
    case 2: return -kernelSin(_y[0], _y[1], 1);
    default: return -kernelCos(_y[0], _y[1]);
  }
}

export function cos(x) {
  const ix = HI(x) & 0x7fffffff;
  if (ix <= 0x3fe921fb) return kernelCos(x, 0);
  if (ix >= 0x7ff00000) return x - x;
  const n = remPio2(x, _y);
  switch (((n % 4) + 4) % 4) {
    case 0: return kernelCos(_y[0], _y[1]);
    case 1: return -kernelSin(_y[0], _y[1], 1);
    case 2: return -kernelCos(_y[0], _y[1]);
    default: return kernelSin(_y[0], _y[1], 1);
  }
}

/* ---------------- atan / atan2 ---------------- */

const atanhi = [
  4.63647609000806093515e-01,
  7.85398163397448278999e-01,
  9.82793723247329054082e-01,
  1.57079632679489655800e+00
];
const atanlo = [
  2.26987774529616870924e-17,
  3.06161699786838301793e-17,
  1.39033110312309984516e-17,
  6.12323399573676603587e-17
];
const aT = [
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
];

export function atan(x) {
  const hx = HI(x);
  const ix = hx & 0x7fffffff;
  let id;
  if (ix >= 0x44100000) {
    if (x !== x) return x + x;
    return hx > 0 ? atanhi[3] + atanlo[3] : -atanhi[3] - atanlo[3];
  }
  if (ix < 0x3fdc0000) {
    if (ix < 0x3e200000) return x;
    id = -1;
  } else {
    x = Math.abs(x);
    if (ix < 0x3ff30000) {
      if (ix < 0x3fe60000) { id = 0; x = (2.0 * x - 1.0) / (2.0 + x); }
      else { id = 1; x = (x - 1.0) / (x + 1.0); }
    } else if (ix < 0x40038000) { id = 2; x = (x - 1.5) / (1.0 + 1.5 * x); }
    else { id = 3; x = -1.0 / x; }
  }
  const z = x * x;
  const w = z * z;
  const s1 = z * (aT[0] + w * (aT[2] + w * (aT[4] + w * (aT[6] + w * (aT[8] + w * aT[10])))));
  const s2 = w * (aT[1] + w * (aT[3] + w * (aT[5] + w * (aT[7] + w * aT[9]))));
  if (id < 0) return x - x * (s1 + s2);
  const r = atanhi[id] - ((x * (s1 + s2) - atanlo[id]) - x);
  return hx < 0 ? -r : r;
}

const PI_O_4 = 7.8539816339744827900e-01;
const PI_O_2 = 1.5707963267948965580e+00;
const PI = 3.1415926535897931160e+00;
const PI_LO = 1.2246467991473531772e-16;

export function atan2(y, x) {
  if (x !== x || y !== y) return x + y;
  const hx = HI(x), ix = hx & 0x7fffffff, lx = LO(x);
  const hy = HI(y), iy = hy & 0x7fffffff, ly = LO(y);
  if (x === 1.0) return atan(y);
  const m = ((hy >> 31) & 1) | ((hx >> 30) & 2);

  if ((iy | ly) === 0) {
    switch (m) {
      case 0: case 1: return y;
      case 2: return PI;
      default: return -PI;
    }
  }
  if ((ix | lx) === 0) return hy < 0 ? -PI_O_2 : PI_O_2;

  if (ix === 0x7ff00000) {
    if (iy === 0x7ff00000) {
      switch (m) {
        case 0: return PI_O_4;
        case 1: return -PI_O_4;
        case 2: return 3.0 * PI_O_4;
        default: return -3.0 * PI_O_4;
      }
    }
    switch (m) {
      case 0: return 0.0;
      case 1: return -0.0;
      case 2: return PI;
      default: return -PI;
    }
  }
  if (iy === 0x7ff00000) return hy < 0 ? -PI_O_2 : PI_O_2;

  const k = (iy - ix) >> 20;
  let z;
  if (k > 60) z = PI_O_2 + 0.5 * PI_LO;
  else if (hx < 0 && k < -60) z = 0.0;
  else z = atan(Math.abs(y / x));
  switch (m) {
    case 0: return z;
    case 1: return -z;
    case 2: return PI - (z - PI_LO);
    default: return (z - PI_LO) - PI;
  }
}

/* ---------------- exp ---------------- */

const O_THRESHOLD = 7.09782712893383973096e+02;
const U_THRESHOLD = -7.45133219101941108420e+02;
const LN2HI = [6.93147180369123816490e-01, -6.93147180369123816490e-01];
const LN2LO = [1.90821492927058770002e-10, -1.90821492927058770002e-10];
const HALF = [0.5, -0.5];
const INVLN2 = 1.44269504088896338700e+00;
const P1 = 1.66666666666666019037e-01;
const P2 = -2.77777777770155933842e-03;
const P3 = 6.61375632143793436117e-05;
const P4 = -1.65339022054652515390e-06;
const P5 = 4.13813679705723846039e-08;
const TWOM1000 = 9.33263618503218878990e-302;

export function exp(x) {
  let hx = HI(x);
  const xsb = (hx >> 31) & 1;
  hx &= 0x7fffffff;

  if (hx >= 0x40862E42) {
    if (hx >= 0x7ff00000) {
      if (x !== x) return x + x;
      return xsb === 0 ? x : 0.0;
    }
    if (x > O_THRESHOLD) return Infinity;
    if (x < U_THRESHOLD) return 0.0;
  }

  let hi = 0, lo = 0, k = 0;
  if (hx > 0x3fd62e42) {
    if (hx < 0x3FF0A2B2) {
      hi = x - LN2HI[xsb]; lo = LN2LO[xsb]; k = 1 - xsb - xsb;
    } else {
      k = (INVLN2 * x + HALF[xsb]) | 0;
      const t = k;
      hi = x - t * LN2HI[0];
      lo = t * LN2LO[0];
    }
    x = hi - lo;
  } else if (hx < 0x3e300000) {
    return 1.0 + x;
  } else {
    k = 0;
  }

  const t = x * x;
  const c = x - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
  if (k === 0) return 1.0 - ((x * c) / (c - 2.0) - x);
  const y = 1.0 - ((lo - (x * c) / (2.0 - c)) - hi);
  if (k >= -1021) return withHI(y, (HI(y) + (k << 20)) | 0);
  return withHI(y, (HI(y) + ((k + 1000) << 20)) | 0) * TWOM1000;
}

/* ---------------- log ---------------- */

const LN2_HI = 6.93147180369123816490e-01;
const LN2_LO = 1.90821492927058770002e-10;
const TWO54 = 1.80143985094819840000e+16;
const Lg1 = 6.666666666666735130e-01;
const Lg2 = 3.999999999940941908e-01;
const Lg3 = 2.857142874366239149e-01;
const Lg4 = 2.222219843214978396e-01;
const Lg5 = 1.818357216161805012e-01;
const Lg6 = 1.531383769920937332e-01;
const Lg7 = 1.479819860511658591e-01;

export function log(x) {
  let hx = HI(x);
  const lx = LO(x);
  let k = 0;
  if (hx < 0x00100000) {
    if (((hx & 0x7fffffff) | lx) === 0) return -Infinity;
    if (hx < 0) return NaN;
    k -= 54; x *= TWO54;
    hx = HI(x);
  }
  if (hx >= 0x7ff00000) return x + x;
  k += (hx >> 20) - 1023;
  hx &= 0x000fffff;
  let i = (hx + 0x95f64) & 0x100000;
  x = withHI(x, hx | (i ^ 0x3ff00000));
  k += (i >> 20);
  const f = x - 1.0;
  let dk;
  if ((0x000fffff & (2 + hx)) < 3) {
    if (f === 0.0) {
      if (k === 0) return 0.0;
      dk = k;
      return dk * LN2_HI + dk * LN2_LO;
    }
    const R0 = f * f * (0.5 - 0.33333333333333333 * f);
    if (k === 0) return f - R0;
    dk = k;
    return dk * LN2_HI - ((R0 - dk * LN2_LO) - f);
  }
  const s = f / (2.0 + f);
  dk = k;
  const z = s * s;
  i = hx - 0x6147a;
  const w = z * z;
  const j = 0x6b851 - hx;
  const t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
  const t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
  i |= j;
  const R = t2 + t1;
  if (i > 0) {
    const hfsq = 0.5 * f * f;
    if (k === 0) return f - (hfsq - s * (hfsq + R));
    return dk * LN2_HI - ((hfsq - (s * (hfsq + R) + dk * LN2_LO)) - f);
  }
  if (k === 0) return f - s * (f - R);
  return dk * LN2_HI - ((s * (f - R) - dk * LN2_LO) - f);
}

/* ---------------- pow ----------------
   fdlibm e_pow.c for a finite, positive base and finite exponent - the
   only case the simulation produces (response curves on magnitudes).
   Other cases are handled by the plain special-value rules below. */
const BP = [1.0, 1.5];
const DP_H = [0.0, 5.84962487220764160156e-01];
const DP_L = [0.0, 1.35003920212974897128e-08];
const TWO53 = 9007199254740992.0;
const L1 = 5.99999999999994648725e-01;
const L2 = 4.28571428578550184252e-01;
const L3 = 3.33333329818377432918e-01;
const L4 = 2.72728123808534006489e-01;
const L5 = 2.30660745775561754067e-01;
const L6 = 2.06975017800338417784e-01;
const LG2 = 6.93147180559945286227e-01;
const LG2_H = 6.93147182464599609375e-01;
const LG2_L = -1.90465429995776804525e-09;
const OVT = 8.0085662595372944372e-17;
const CP = 9.61796693925975554329e-01;
const CP_H = 9.61796700954437255859e-01;
const CP_L = -7.02846165095275826516e-09;
const IVLN2 = 1.44269504088896338700e+00;
const IVLN2_H = 1.44269502162933349609e+00;
const IVLN2_L = 1.92596299112661746887e-08;

function withLO(x, lo) { _f64[0] = x; _i32[0] = lo; return _f64[0]; }

export function pow(x, y) {
  if (y === 0) return 1.0;
  if (x !== x || y !== y) return NaN;
  if (x === 1.0) return 1.0;
  if (y === 1.0) return x;
  if (x === 0) return y > 0 ? 0.0 : Infinity;
  if (!(x > 0) || !Number.isFinite(x) || !Number.isFinite(y)) return Math.exp(y * Math.log(x));
  if (y === 2.0) return x * x;
  if (y === 0.5) return Math.sqrt(x);
  if (y === -1.0) return 1.0 / x;

  const hx = HI(x);
  const hy = HI(y);
  let ix = hx & 0x7fffffff;
  const iy = hy & 0x7fffffff;
  let ax = x;
  let t1, t2, n;

  if (iy > 0x41e00000) {
    if (iy > 0x43f00000) {
      if (ix <= 0x3fefffff) return hy < 0 ? Infinity : 0.0;
      if (ix >= 0x3ff00000) return hy > 0 ? Infinity : 0.0;
    }
    if (ix < 0x3fefffff) return hy < 0 ? Infinity : 0.0;
    if (ix > 0x3ff00000) return hy > 0 ? Infinity : 0.0;
    const t = ax - 1.0;
    const w = (t * t) * (0.5 - t * (0.3333333333333333333333 - t * 0.25));
    const u = IVLN2_H * t;
    const v = t * IVLN2_L - w * IVLN2;
    t1 = withLO(u + v, 0);
    t2 = v - (t1 - u);
  } else {
    n = 0;
    if (ix < 0x00100000) { ax *= TWO53; n -= 53; ix = HI(ax); }
    n += (ix >> 20) - 0x3ff;
    const j = ix & 0x000fffff;
    let k;
    ix = j | 0x3ff00000;
    if (j <= 0x3988E) k = 0;
    else if (j < 0xBB67A) k = 1;
    else { k = 0; n += 1; ix -= 0x00100000; }
    ax = withHI(ax, ix);

    let u = ax - BP[k];
    let v = 1.0 / (ax + BP[k]);
    const ss = u * v;
    const s_h = withLO(ss, 0);
    let t_h = fromWords(((ix >> 1) | 0x20000000) + 0x00080000 + (k << 18), 0);
    let t_l = ax - (t_h - BP[k]);
    const s_l = v * ((u - s_h * t_h) - s_h * t_l);
    let s2 = ss * ss;
    let r = s2 * s2 * (L1 + s2 * (L2 + s2 * (L3 + s2 * (L4 + s2 * (L5 + s2 * L6)))));
    r += s_l * (s_h + ss);
    s2 = s_h * s_h;
    t_h = withLO(3.0 + s2 + r, 0);
    t_l = r - ((t_h - 3.0) - s2);
    u = s_h * t_h;
    v = s_l * t_h + t_l * ss;
    const p_h0 = withLO(u + v, 0);
    const p_l0 = v - (p_h0 - u);
    const z_h = CP_H * p_h0;
    const z_l = CP_L * p_h0 + p_l0 * CP + DP_L[k];
    const t = n;
    t1 = withLO(((z_h + z_l) + DP_H[k]) + t, 0);
    t2 = z_l - (((t1 - t) - DP_H[k]) - z_h);
  }

  const y1 = withLO(y, 0);
  const p_l = (y - y1) * t1 + y * t2;
  let p_h = y1 * t1;
  let z = p_l + p_h;
  let j = HI(z);
  let i = LO(z);
  if (j >= 0x40900000) {
    if (((j - 0x40900000) | i) !== 0) return Infinity;
    if (p_l + OVT > z - p_h) return Infinity;
  } else if ((j & 0x7fffffff) >= 0x4090cc00) {
    if (((j - 0xc090cc00) | i) !== 0) return 0.0;
    if (p_l <= z - p_h) return 0.0;
  }

  i = j & 0x7fffffff;
  let k = (i >> 20) - 0x3ff;
  n = 0;
  if (i > 0x3fe00000) {
    n = (j + (0x00100000 >> (k + 1))) | 0;
    k = ((n & 0x7fffffff) >> 20) - 0x3ff;
    const t = fromWords(n & ~(0x000fffff >> k), 0);
    n = ((n & 0x000fffff) | 0x00100000) >> (20 - k);
    if (j < 0) n = -n;
    p_h -= t;
  }
  const t = withLO(p_l + p_h, 0);
  const u = t * LG2_H;
  const v = (p_l - (t - p_h)) * LG2 + t * LG2_L;
  z = u + v;
  const w = v - (z - u);
  const tt = z * z;
  const tt1 = z - tt * (P1 + tt * (P2 + tt * (P3 + tt * (P4 + tt * P5))));
  const r = (z * tt1) / (tt1 - 2.0) - (w + z * w);
  z = 1.0 - (r - z);
  j = (HI(z) + (n << 20)) | 0;
  if ((j >> 20) <= 0) return z * Math.pow(2, n);   // subnormal result: never reached by the sim
  return withHI(z, j);
}

/* ---------------- hypot ----------------
   V8's algorithm (src/builtins/math.tq): scale by the largest magnitude,
   Kahan-compensated sum of squares, sqrt, rescale. Matches Node's native
   Math.hypot bit for bit, so patching it in changes nothing in the lab. */
const _abs = [0, 0, 0, 0];
export function hypot(...args) {
  const n = args.length;
  let max = 0, sawNaN = false;
  for (let i = 0; i < n; i++) {
    const a = Math.abs(args[i]);
    if (a === Infinity) return Infinity;
    if (a !== a) sawNaN = true;
    else if (a > max) max = a;
    _abs[i] = a;
  }
  if (sawNaN) return NaN;
  if (max === 0) return 0;
  let sum = 0, compensation = 0;
  for (let i = 0; i < n; i++) {
    const v = _abs[i] / max;
    const summand = v * v - compensation;
    const preliminary = sum + summand;
    compensation = (preliminary - sum) - summand;
    sum = preliminary;
  }
  return Math.sqrt(sum) * max;
}

/** Replace the transcendental Math functions on a global Math object. */
export function installOn(M) {
  M.sin = sin; M.cos = cos; M.atan = atan; M.atan2 = atan2;
  M.exp = exp; M.log = log; M.pow = pow; M.hypot = hypot;
}
