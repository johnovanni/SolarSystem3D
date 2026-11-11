using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.Core.Orbits;

/// <summary>
/// Heliocentric planetary orbit from JPL's "Keplerian Elements for Approximate Positions of the Major
/// Planets" (E.M. Standish), Table 1: J2000 elements with linear rates per Julian century.
/// Valid 1800–2050 AD to roughly an arc-minute; usable (with degrading accuracy) outside that span.
/// </summary>
public sealed class PlanetOrbit : IOrbitModel
{
    private readonly double _a0, _aDot, _e0, _eDot, _i0, _iDot, _l0, _lDot, _w0, _wDot, _n0, _nDot;

    /// <param name="a0">Semi-major axis (AU) and rate per century.</param>
    /// <param name="l0">Mean longitude (deg) and rate (deg/century).</param>
    /// <param name="lonPeri0">Longitude of perihelion ϖ (deg) and rate.</param>
    /// <param name="node0">Longitude of ascending node Ω (deg) and rate.</param>
    public PlanetOrbit(double a0, double aDot, double e0, double eDot, double i0, double iDot,
                       double l0, double lDot, double lonPeri0, double lonPeriDot, double node0, double nodeDot)
    {
        _a0 = a0; _aDot = aDot; _e0 = e0; _eDot = eDot; _i0 = i0; _iDot = iDot;
        _l0 = l0; _lDot = lDot; _w0 = lonPeri0; _wDot = lonPeriDot; _n0 = node0; _nDot = nodeDot;
    }

    public static bool IsWithinValidRange(double jdTT) => jdTT is >= 2378496.5 and <= 2470172.5; // 1800–2050

    private (double a, double e, double i, double l, double lonPeri, double node) ElementsAt(double jdTT)
    {
        double t = AstroTime.CenturiesSinceJ2000(jdTT);
        return (_a0 + _aDot * t, _e0 + _eDot * t, _i0 + _iDot * t, _l0 + _lDot * t, _w0 + _wDot * t, _n0 + _nDot * t);
    }

    private static Matrix3d Rotation(double i, double lonPeri, double node) =>
        Conic.OrbitRotation(i * Frames.Deg, node * Frames.Deg, (lonPeri - node) * Frames.Deg);

    public Vector3d PositionAt(double jdTT)
    {
        var (a, e, i, l, lonPeri, node) = ElementsAt(jdTT);
        double m = Frames.NormalizeDegrees(l - lonPeri) * Frames.Deg;
        var (x, y) = Conic.PlanePositionFromMeanAnomaly(a, e, m);
        return Rotation(i, lonPeri, node) * new Vector3d(x, y, 0);
    }

    public double? PeriodDays => 360.0 / _lDot * AstroTime.DaysPerJulianCentury;

    public IReadOnlyList<Vector3d> SamplePath(double jdTT, int segments, double maxRadiusAu)
    {
        var (a, e, i, _, lonPeri, node) = ElementsAt(jdTT);
        var rot = Rotation(i, lonPeri, node);
        var curve = Conic.SamplePlaneCurve(a * (1 - e), e, segments, double.PositiveInfinity, out _);
        return curve.Select(p => rot * new Vector3d(p.x, p.y, 0)).ToArray();
    }

    public OrbitSummary Describe(double jdTT)
    {
        var (a, e, i, _, _, _) = ElementsAt(jdTT);
        return new OrbitSummary(a, e, i, a * (1 - e), a * (1 + e), PeriodDays, "J2000 ecliptic",
            IsWithinValidRange(jdTT) ? "JPL approximate planetary elements (1800–2050)" : "JPL approximate elements — outside 1800–2050, reduced accuracy");
    }
}
