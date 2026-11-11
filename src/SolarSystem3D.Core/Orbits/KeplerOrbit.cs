using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.Core.Orbits;

/// <summary>
/// Two-body Keplerian orbit with optional uniform precession of node and apsides.
/// Used for asteroids, comets and natural satellites (JPL mean elements).
/// </summary>
/// <remarks>
/// Angles are measured in a reference plane described by <see cref="Frame"/> (identity = J2000 ecliptic).
/// The node and the argument of periapsis drift linearly; the mean anomaly advances at <see cref="MeanMotion"/>.
/// </remarks>
public sealed class KeplerOrbit : IOrbitModel
{
    private const double AuKm = 149_597_870.7;
    private readonly Matrix3d? _cachedRotation;

    public double PeriapsisDistance { get; }   // AU
    public double Eccentricity { get; }
    public double Inclination { get; }         // rad
    public double AscendingNode { get; }       // rad, at epoch
    public double ArgPeriapsis { get; }        // rad, at epoch
    public double Epoch { get; }               // TT JD
    public double MeanAnomalyAtEpoch { get; }  // rad (unused for parabolic orbits)
    public double MeanMotion { get; }          // rad/day (rate of mean anomaly)
    public double NodeRate { get; }            // rad/day
    public double ApsisRate { get; }           // rad/day (argument of periapsis)
    public double SqrtGm { get; }              // AU^1.5/day
    public Matrix3d Frame { get; }
    public string ReferencePlaneName { get; }
    public string Source { get; }

    public double SemiMajorAxis => Math.Abs(Eccentricity - 1) < 1e-7 ? double.PositiveInfinity : PeriapsisDistance / (1 - Eccentricity);
    public bool IsParabolic => Math.Abs(Eccentricity - 1) < 1e-7;
    public bool IsClosed => Eccentricity < 1 && !IsParabolic;

    private KeplerOrbit(double q, double e, double i, double node, double w, double epoch, double m0, double n,
                        double nodeRate, double apsisRate, double sqrtGm, Matrix3d frame, string planeName, string source)
    {
        PeriapsisDistance = q; Eccentricity = e; Inclination = i; AscendingNode = node; ArgPeriapsis = w;
        Epoch = epoch; MeanAnomalyAtEpoch = m0; MeanMotion = n; NodeRate = nodeRate; ApsisRate = apsisRate;
        SqrtGm = sqrtGm; Frame = frame; ReferencePlaneName = planeName; Source = source;
        if (nodeRate == 0 && apsisRate == 0)
            _cachedRotation = frame * Conic.OrbitRotation(i, node, w);
    }

    /// <summary>Heliocentric orbit from classic elements (a in AU, angles in degrees) — e.g. JPL SBDB asteroids.</summary>
    public static KeplerOrbit FromMeanAnomaly(double aAu, double e, double iDeg, double nodeDeg, double argPeriDeg,
                                              double meanAnomalyDeg, double epochJd, string source = "JPL SBDB osculating elements")
    {
        const double d = Frames.Deg;
        double n = Conic.GaussK / Math.Pow(Math.Abs(aAu), 1.5);
        return new KeplerOrbit(aAu * (1 - e), e, iDeg * d, nodeDeg * d, argPeriDeg * d, epochJd, meanAnomalyDeg * d, n,
                               0, 0, Conic.GaussK, Matrix3d.Identity, "J2000 ecliptic", source);
    }

    /// <summary>Heliocentric orbit from perihelion distance and time of perihelion — e.g. JPL SBDB comets.</summary>
    public static KeplerOrbit FromPerihelion(double qAu, double e, double iDeg, double nodeDeg, double argPeriDeg,
                                             double perihelionJd, string source = "JPL SBDB osculating elements")
    {
        const double d = Frames.Deg;
        double n = 0;
        if (Math.Abs(e - 1) >= 1e-7)
            n = Conic.GaussK / Math.Pow(Math.Abs(qAu / (1 - e)), 1.5);
        return new KeplerOrbit(qAu, e, iDeg * d, nodeDeg * d, argPeriDeg * d, perihelionJd, 0, n,
                               0, 0, Conic.GaussK, Matrix3d.Identity, "J2000 ecliptic", source);
    }

    /// <summary>
    /// Planet-centric satellite orbit from JPL mean elements.
    /// </summary>
    /// <param name="periodDays">Orbital period; sets the mean-anomaly rate unless <paramref name="meanAnomalyRateDegPerDay"/> is given.</param>
    /// <param name="apsisPeriodYears">Apsidal precession period (0 or NaN = none).</param>
    /// <param name="nodePeriodYears">Nodal regression period (0 or NaN = none).</param>
    /// <param name="meanAnomalyRateDegPerDay">Mean-anomaly rate calibrated against JPL Horizons, if available.</param>
    public static KeplerOrbit ForSatellite(double aKm, double e, double argPeriDeg, double meanAnomalyDeg, double iDeg,
                                           double nodeDeg, double periodDays, double apsisPeriodYears, double nodePeriodYears,
                                           double epochJd, Matrix3d frame, string planeName, double? meanAnomalyRateDegPerDay = null)
    {
        const double d = Frames.Deg;
        double aAu = aKm / AuKm;
        double n = meanAnomalyRateDegPerDay is double rate ? rate * d : 2 * Math.PI / periodDays;
        double apsisRate = apsisPeriodYears > 0 ? 2 * Math.PI / (apsisPeriodYears * AstroTime.DaysPerJulianYear) : 0;
        double nodeRate = nodePeriodYears > 0 ? -2 * Math.PI / (nodePeriodYears * AstroTime.DaysPerJulianYear) : 0;
        if (iDeg > 90) { nodeRate = -nodeRate; apsisRate = -apsisRate; } // retrograde orbits precess the other way
        // sqrt(GM_planet) implied by Kepler's third law, used only for parabolic fallbacks.
        double sqrtGm = n * Math.Pow(aAu, 1.5);
        return new KeplerOrbit(aAu * (1 - e), e, iDeg * d, nodeDeg * d, argPeriDeg * d, epochJd, meanAnomalyDeg * d, n,
                               nodeRate, apsisRate, sqrtGm, frame, planeName,
                               meanAnomalyRateDegPerDay.HasValue ? "JPL mean satellite elements, phase calibrated to JPL Horizons"
                                                                  : "JPL mean satellite elements");
    }

    public double? PeriodDays => IsClosed ? 2 * Math.PI / (MeanMotion + ApsisRate + NodeRate) : null;

    private (double node, double argPeri) AnglesAt(double jd)
    {
        double dt = jd - Epoch;
        return (AscendingNode + NodeRate * dt, ArgPeriapsis + ApsisRate * dt);
    }

    private Matrix3d RotationAt(double jd)
    {
        if (_cachedRotation.HasValue) return _cachedRotation.Value;
        var (node, w) = AnglesAt(jd);
        return Frame * Conic.OrbitRotation(Inclination, node, w);
    }

    public Vector3d PositionAt(double jdTT)
    {
        double x, y;
        if (IsParabolic)
        {
            (x, y) = Conic.PlanePositionFromTime(PeriapsisDistance, Eccentricity, jdTT - Epoch, SqrtGm);
        }
        else
        {
            double m = MeanAnomalyAtEpoch + MeanMotion * (jdTT - Epoch);
            (x, y) = Conic.PlanePositionFromMeanAnomaly(SemiMajorAxis, Eccentricity, m);
        }
        return RotationAt(jdTT) * new Vector3d(x, y, 0);
    }

    public IReadOnlyList<Vector3d> SamplePath(double jdTT, int segments, double maxRadiusAu)
    {
        var rot = RotationAt(jdTT);
        var curve = Conic.SamplePlaneCurve(PeriapsisDistance, Eccentricity, segments, maxRadiusAu, out _);
        var list = new Vector3d[curve.Count];
        for (int k = 0; k < curve.Count; k++)
            list[k] = rot * new Vector3d(curve[k].x, curve[k].y, 0);
        return list;
    }

    public OrbitSummary Describe(double jdTT) => new(
        IsParabolic ? null : SemiMajorAxis,
        Eccentricity,
        Inclination / Frames.Deg,
        PeriapsisDistance,
        IsClosed ? SemiMajorAxis * (1 + Eccentricity) : null,
        PeriodDays,
        ReferencePlaneName,
        Source);
}
