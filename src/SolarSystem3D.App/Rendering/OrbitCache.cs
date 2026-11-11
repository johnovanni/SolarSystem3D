using SolarSystem3D.Core.Astronomy;
using SolarSystem3D.Core.Bodies;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.App.Rendering;

/// <summary>
/// Caches orbit paths (relative to each body's parent). Paths of fixed Keplerian orbits never change;
/// slowly varying orbits are resampled only when the simulated time has moved far enough.
/// </summary>
public sealed class OrbitCache
{
    private readonly Dictionary<CelestialBody, (double Jd, IReadOnlyList<Vector3d> Path)> _cache = new();

    public void Clear() => _cache.Clear();

    public IReadOnlyList<Vector3d> GetPath(CelestialBody body, double jdTT, int segments, double maxRadiusAu = 100)
    {
        if (body.Orbit is null) return Array.Empty<Vector3d>();
        if (_cache.TryGetValue(body, out var entry) && Math.Abs(entry.Jd - jdTT) <= Tolerance(body.Orbit))
            return entry.Path;
        var path = body.Orbit.SamplePath(jdTT, segments, maxRadiusAu);
        _cache[body] = (jdTT, path);
        return path;
    }

    private static double Tolerance(IOrbitModel orbit) => orbit switch
    {
        KeplerOrbit k when k.NodeRate == 0 && k.ApsisRate == 0 => double.PositiveInfinity,
        KeplerOrbit => 5,
        LunarOrbit => 0.25,
        _ => 60,
    };
}
