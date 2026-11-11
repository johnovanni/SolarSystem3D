using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.Core.Orbits;

/// <summary>
/// Computes a body's position relative to its parent (the Sun for heliocentric bodies, the planet for moons).
/// All vectors are in AU in the J2000 ecliptic frame; all times are TT Julian dates.
/// </summary>
public interface IOrbitModel
{
    /// <summary>Position relative to the parent body.</summary>
    Vector3d PositionAt(double jdTT);

    /// <summary>Orbital period in days, or null for open (parabolic/hyperbolic) orbits.</summary>
    double? PeriodDays { get; }

    /// <summary>Points along the orbit, relative to the parent, for drawing. Clipped to <paramref name="maxRadiusAu"/>.</summary>
    IReadOnlyList<Vector3d> SamplePath(double jdTT, int segments, double maxRadiusAu);

    /// <summary>Osculating/mean element summary for display.</summary>
    OrbitSummary Describe(double jdTT);
}

/// <summary>Human-readable orbit description for the information panel.</summary>
public sealed record OrbitSummary(
    double? SemiMajorAxisAu,
    double Eccentricity,
    double InclinationDeg,
    double PeriapsisAu,
    double? ApoapsisAu,
    double? PeriodDays,
    string ReferencePlane,
    string Source);
