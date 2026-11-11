namespace SolarSystem3D.Core.Astronomy;

/// <summary>
/// Two-body conic-section mathematics: solves Kepler's equation for elliptic, parabolic and
/// hyperbolic orbits and returns positions in the orbital plane.
/// </summary>
public static class Conic
{
    /// <summary>Gaussian gravitational constant k (AU^1.5 / day), i.e. sqrt(GM_sun).</summary>
    public const double GaussK = 0.01720209895;

    /// <summary>Solves E − e·sin E = M for the eccentric anomaly (elliptic orbits).</summary>
    public static double SolveElliptic(double meanAnomaly, double e)
    {
        double m = Math.IEEERemainder(meanAnomaly, 2 * Math.PI); // −π..π
        double ecc = e < 0.8 ? m : (m >= 0 ? Math.PI : -Math.PI);
        for (int iter = 0; iter < 50; iter++)
        {
            double f = ecc - e * Math.Sin(ecc) - m;
            double d = f / (1 - e * Math.Cos(ecc));
            ecc -= d;
            if (Math.Abs(d) < 1e-13) break;
        }
        return ecc;
    }

    /// <summary>Solves e·sinh H − H = M for the hyperbolic anomaly.</summary>
    public static double SolveHyperbolic(double meanAnomaly, double e)
    {
        double m = meanAnomaly;
        double h = Math.Asinh(m / e);
        if (Math.Abs(m) > 6 * e) h = Math.Sign(m) * Math.Log(2 * Math.Abs(m) / e + 1.8);
        for (int iter = 0; iter < 100; iter++)
        {
            double f = e * Math.Sinh(h) - h - m;
            double d = f / (e * Math.Cosh(h) - 1);
            h -= d;
            if (Math.Abs(d) < 1e-13 * Math.Max(1, Math.Abs(h))) break;
        }
        return h;
    }

    /// <summary>
    /// Position in the orbital plane (x toward periapsis) at time <paramref name="dtFromPeriapsis"/> days
    /// after periapsis passage. <paramref name="gm"/> is sqrt(GM) in AU^1.5/day units (k for the Sun).
    /// </summary>
    public static (double x, double y) PlanePositionFromTime(double q, double e, double dtFromPeriapsis, double sqrtGm)
    {
        if (Math.Abs(e - 1.0) < 1e-7)
        {
            // Parabola — Barker's equation.
            double w = 3 * sqrtGm / (q * Math.Sqrt(2 * q)) * dtFromPeriapsis;
            double yb = Math.Cbrt(w / 2 + Math.Sqrt(w * w / 4 + 1));
            double s = yb - 1 / yb;
            return (q * (1 - s * s), 2 * q * s);
        }
        double a = q / (1 - e);
        double n = sqrtGm / Math.Pow(Math.Abs(a), 1.5);
        return PlanePositionFromMeanAnomaly(a, e, n * dtFromPeriapsis);
    }

    /// <summary>Position in the orbital plane from semi-major axis (negative for hyperbolae) and mean anomaly.</summary>
    public static (double x, double y) PlanePositionFromMeanAnomaly(double a, double e, double meanAnomaly)
    {
        if (e < 1)
        {
            double ea = SolveElliptic(meanAnomaly, e);
            return (a * (Math.Cos(ea) - e), a * Math.Sqrt(1 - e * e) * Math.Sin(ea));
        }
        double h = SolveHyperbolic(meanAnomaly, e);
        double aa = Math.Abs(a);
        return (aa * (e - Math.Cosh(h)), aa * Math.Sqrt(e * e - 1) * Math.Sinh(h));
    }

    /// <summary>Rotation from the orbital plane (x to periapsis) to the reference frame: Rz(Ω)·Rx(i)·Rz(ω).</summary>
    public static Matrix3d OrbitRotation(double inclination, double ascendingNode, double argPeriapsis) =>
        Matrix3d.RotationZ(ascendingNode) * Matrix3d.RotationX(inclination) * Matrix3d.RotationZ(argPeriapsis);

    /// <summary>
    /// Samples the orbit curve in its own plane by true anomaly, clipped to radius <paramref name="maxRadius"/>.
    /// Combines uniform true-anomaly and eccentric-anomaly spacing so both perihelion and aphelion look smooth.
    /// </summary>
    public static List<(double x, double y)> SamplePlaneCurve(double q, double e, int segments, double maxRadius, out bool closed)
    {
        var result = new List<(double, double)>(segments + 2);
        double p = q * (1 + e);
        double nuMax = Math.PI;
        closed = e < 1 && q * (1 + e) / (1 - e) <= maxRadius;
        if (!closed)
        {
            if (q >= maxRadius) return result;
            double c = (p / maxRadius - 1) / e;
            nuMax = Math.Acos(Math.Clamp(c, -1, 1));
        }

        var angles = new List<double>(segments * 2);
        int half = Math.Max(8, segments / 2);
        for (int k = 0; k <= half; k++)
            angles.Add(-nuMax + 2 * nuMax * k / half);
        if (e < 1)
        {
            double sq = Math.Sqrt((1 + e) / (1 - e));
            for (int k = 0; k < half; k++)
            {
                double ea = -Math.PI + 2 * Math.PI * k / half;
                double nu = 2 * Math.Atan(sq * Math.Tan(ea / 2));
                if (Math.Abs(nu) <= nuMax) angles.Add(nu);
            }
        }
        angles.Sort();
        double prev = double.NegativeInfinity;
        foreach (double nu in angles)
        {
            if (nu - prev < 1e-9) continue;
            prev = nu;
            double r = p / (1 + e * Math.Cos(nu));
            result.Add((r * Math.Cos(nu), r * Math.Sin(nu)));
        }
        if (closed && result.Count > 0) result.Add(result[0]);
        return result;
    }
}
