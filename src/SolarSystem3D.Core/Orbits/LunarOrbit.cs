using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.Core.Orbits;

/// <summary>
/// Geocentric position of the Moon from the principal periodic terms of the ELP-2000/82 theory
/// (as abridged by J. Meeus, "Astronomical Algorithms", ch. 47). Accuracy ≈ 10″ in longitude.
/// </summary>
public sealed class LunarOrbit : IOrbitModel
{
    private const double AuKm = 149_597_870.7;
    private const double SiderealMonth = 27.321661;

    // D, M, M', F, Σl (1e-6 deg), Σr (1e-3 km)
    private static readonly int[,] LonDistTerms =
    {
        { 0, 0, 1, 0, 6288774, -20905355 }, { 2, 0, -1, 0, 1274027, -3699111 }, { 2, 0, 0, 0, 658314, -2955968 },
        { 0, 0, 2, 0, 213618, -569925 },   { 0, 1, 0, 0, -185116, 48888 },     { 0, 0, 0, 2, -114332, -3149 },
        { 2, 0, -2, 0, 58793, 246158 },    { 2, -1, -1, 0, 57066, -152138 },   { 2, 0, 1, 0, 53322, -170733 },
        { 2, -1, 0, 0, 45758, -204586 },   { 0, 1, -1, 0, -40923, -129620 },   { 1, 0, 0, 0, -34720, 108743 },
        { 0, 1, 1, 0, -30383, 104755 },    { 2, 0, 0, -2, 15327, 10321 },      { 0, 0, 1, 2, -12528, 0 },
        { 0, 0, 1, -2, 10980, 79661 },     { 4, 0, -1, 0, 10675, -34782 },     { 0, 0, 3, 0, 10034, -23210 },
        { 4, 0, -2, 0, 8548, -21636 },     { 2, 1, -1, 0, -7888, 24208 },      { 2, 1, 0, 0, -6766, 30824 },
        { 1, 0, -1, 0, -5163, -8379 },     { 1, 1, 0, 0, 4987, -16675 },       { 2, -1, 1, 0, 4036, -12831 },
        { 2, 0, 2, 0, 3994, -10445 },      { 4, 0, 0, 0, 3861, -11650 },       { 2, 0, -3, 0, 3665, 14403 },
        { 0, 1, -2, 0, -2689, -7003 },     { 2, 0, -1, 2, -2602, 0 },          { 2, -1, -2, 0, 2390, 10056 },
        { 1, 0, 1, 0, -2348, 6322 },       { 2, -2, 0, 0, 2236, -9884 },
    };

    // D, M, M', F, Σb (1e-6 deg)
    private static readonly int[,] LatTerms =
    {
        { 0, 0, 0, 1, 5128122 }, { 0, 0, 1, 1, 280602 }, { 0, 0, 1, -1, 277693 }, { 2, 0, 0, -1, 173237 },
        { 2, 0, -1, 1, 55413 },  { 2, 0, -1, -1, 46271 }, { 2, 0, 0, 1, 32573 },   { 0, 0, 2, 1, 17198 },
        { 2, 0, 1, -1, 9266 },   { 0, 0, 2, -1, 8822 },   { 2, -1, 0, -1, 8216 },  { 2, 0, -2, -1, 4324 },
        { 2, 0, 1, 1, 4200 },    { 2, 1, 0, -1, -3359 },  { 2, -1, -1, 1, 2463 },  { 2, -1, 0, 1, 2211 },
        { 2, -1, -1, -1, 2065 },
    };

    public Vector3d PositionAt(double jdTT)
    {
        double t = AstroTime.CenturiesSinceJ2000(jdTT);
        double t2 = t * t, t3 = t2 * t, t4 = t3 * t;
        const double d2r = Frames.Deg;

        double lp = 218.3164477 + 481267.88123421 * t - 0.0015786 * t2 + t3 / 538841 - t4 / 65194000;
        double d = 297.8501921 + 445267.1114034 * t - 0.0018819 * t2 + t3 / 545868 - t4 / 113065000;
        double m = 357.5291092 + 35999.0502909 * t - 0.0001536 * t2 + t3 / 24490000;
        double mp = 134.9633964 + 477198.8675055 * t + 0.0087414 * t2 + t3 / 69699 - t4 / 14712000;
        double f = 93.2720950 + 483202.0175233 * t - 0.0036539 * t2 - t3 / 3526000 + t4 / 863310000;
        double a1 = 119.75 + 131.849 * t, a2 = 53.09 + 479264.290 * t, a3 = 313.45 + 481266.484 * t;
        double ecc = 1 - 0.002516 * t - 0.0000074 * t2;

        double sl = 0, sr = 0, sb = 0;
        for (int k = 0; k < LonDistTerms.GetLength(0); k++)
        {
            double arg = (LonDistTerms[k, 0] * d + LonDistTerms[k, 1] * m + LonDistTerms[k, 2] * mp + LonDistTerms[k, 3] * f) * d2r;
            double factor = Math.Abs(LonDistTerms[k, 1]) switch { 1 => ecc, 2 => ecc * ecc, _ => 1 };
            sl += LonDistTerms[k, 4] * factor * Math.Sin(arg);
            sr += LonDistTerms[k, 5] * factor * Math.Cos(arg);
        }
        for (int k = 0; k < LatTerms.GetLength(0); k++)
        {
            double arg = (LatTerms[k, 0] * d + LatTerms[k, 1] * m + LatTerms[k, 2] * mp + LatTerms[k, 3] * f) * d2r;
            double factor = Math.Abs(LatTerms[k, 1]) switch { 1 => ecc, 2 => ecc * ecc, _ => 1 };
            sb += LatTerms[k, 4] * factor * Math.Sin(arg);
        }
        sl += 3958 * Math.Sin(a1 * d2r) + 1962 * Math.Sin((lp - f) * d2r) + 318 * Math.Sin(a2 * d2r);
        sb += -2235 * Math.Sin(lp * d2r) + 382 * Math.Sin(a3 * d2r) + 175 * Math.Sin((a1 - f) * d2r)
              + 175 * Math.Sin((a1 + f) * d2r) + 127 * Math.Sin((lp - mp) * d2r) - 115 * Math.Sin((lp + mp) * d2r);

        // Ecliptic of date → J2000 ecliptic: remove general precession in longitude.
        double precession = 1.396971 * t + 0.0003086 * t2;
        double lambda = (lp + sl / 1e6 - precession) * d2r;
        double beta = sb / 1e6 * d2r;
        double dist = (385000.56 + sr / 1000) / AuKm;

        return new Vector3d(dist * Math.Cos(beta) * Math.Cos(lambda),
                            dist * Math.Cos(beta) * Math.Sin(lambda),
                            dist * Math.Sin(beta));
    }

    public double? PeriodDays => SiderealMonth;

    /// <summary>Traces the actual (perturbed) path over one sidereal month centred on the current time.</summary>
    public IReadOnlyList<Vector3d> SamplePath(double jdTT, int segments, double maxRadiusAu)
    {
        var pts = new Vector3d[segments + 1];
        for (int k = 0; k <= segments; k++)
            pts[k] = PositionAt(jdTT - SiderealMonth / 2 + SiderealMonth * k / segments);
        return pts;
    }

    public OrbitSummary Describe(double jdTT) =>
        new(384748 / AuKm, 0.0549, 5.145, 363300 / AuKm, 405500 / AuKm, SiderealMonth, "J2000 ecliptic (geocentric)",
            "ELP-2000/82 principal terms (Meeus)");
}
