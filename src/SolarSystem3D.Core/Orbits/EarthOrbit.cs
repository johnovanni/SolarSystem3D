using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.Core.Orbits;

/// <summary>
/// Earth's heliocentric position: the Earth–Moon barycentre orbit minus Earth's small offset
/// from the barycentre caused by the Moon (Earth/Moon mass ratio 81.30056).
/// </summary>
public sealed class EarthOrbit : IOrbitModel
{
    private const double EarthMoonMassRatio = 81.30056;
    private readonly PlanetOrbit _barycentre;
    private readonly IOrbitModel _moon;

    public EarthOrbit(PlanetOrbit earthMoonBarycentre, IOrbitModel geocentricMoon)
    {
        _barycentre = earthMoonBarycentre;
        _moon = geocentricMoon;
    }

    public Vector3d PositionAt(double jdTT) =>
        _barycentre.PositionAt(jdTT) - _moon.PositionAt(jdTT) / (1 + EarthMoonMassRatio);

    public double? PeriodDays => _barycentre.PeriodDays;

    public IReadOnlyList<Vector3d> SamplePath(double jdTT, int segments, double maxRadiusAu) =>
        _barycentre.SamplePath(jdTT, segments, maxRadiusAu);

    public OrbitSummary Describe(double jdTT) => _barycentre.Describe(jdTT);
}
