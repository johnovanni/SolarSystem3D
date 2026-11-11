using SolarSystem3D.Core;
using AstroCore.Astronomy;
using SolarSystem3D.Core.Data;
using Xunit.Abstractions;

namespace SolarSystem3D.Tests;

/// <summary>
/// Compares computed positions with JPL Horizons state vectors (J2000 ecliptic, AU) for
/// 2026-11-10 00:00 TDB (JD 2461354.5), fetched from ssd.jpl.nasa.gov/api/horizons.api.
/// </summary>
public class EphemerisAccuracyTests
{
    private const double Jd = 2461354.5;
    private static readonly Lazy<SolarSystem> System = new(() =>
    {
        // Use only the embedded snapshot so the test is independent of any downloaded cache.
        var store = new CatalogStore(Path.Combine(Path.GetTempPath(), "SolarSystem3D-tests-empty-cache"));
        var s = new SolarSystemFactory(store).Create(out _);
        s.Update(Jd);
        return s;
    });

    private readonly ITestOutputHelper _out;
    public EphemerisAccuracyTests(ITestOutputHelper output) => _out = output;

    /// <summary>Angle in degrees between the computed and reference vectors (as seen from the parent).</summary>
    private static double AngleDeg(Vector3d a, Vector3d b) =>
        Math.Acos(Math.Clamp(Vector3d.Dot(a.Normalized(), b.Normalized()), -1, 1)) / Frames.Deg;

    private void Check(string name, Vector3d computed, Vector3d reference, double maxAngleDeg, double maxRelDistance)
    {
        double ang = AngleDeg(computed, reference);
        double rel = Math.Abs(computed.Length - reference.Length) / reference.Length;
        _out.WriteLine($"{name,-10} angle error {ang,8:F4}°   distance error {rel * 100,7:F3}%");
        Assert.True(ang <= maxAngleDeg, $"{name}: angular error {ang:F4}° exceeds {maxAngleDeg}°");
        Assert.True(rel <= maxRelDistance, $"{name}: distance error {rel:P3} exceeds {maxRelDistance:P3}");
    }

    [Theory]
    [InlineData("Mercury", 7.905690126265739E-02, 2.967145759131760E-01, 1.699796287646893E-02)]
    [InlineData("Earth", 6.726227034059016E-01, 7.270693863771039E-01, -4.613446748933096E-05)]
    [InlineData("Mars", -4.064255462371009E-01, 1.550867358036737E+00, 4.246600620868177E-02)]
    [InlineData("Jupiter", -3.715868902610422E+00, 3.806252250411208E+00, 6.732649458885819E-02)]
    [InlineData("Neptune", 2.983045879580505E+01, 1.506209688557514E+00, -7.184541035923401E-01)]
    [InlineData("Pluto", 2.007131551128247E+01, -2.932744486264410E+01, -2.666852518584616E+00)]
    public void PlanetsMatchHorizons(string name, double x, double y, double z)
    {
        var body = System.Value.Find(name)!;
        Check(name, body.Position, new Vector3d(x, y, z), maxAngleDeg: 0.1, maxRelDistance: 0.002);
    }

    [Theory]
    [InlineData("Ceres", -1.408803634573469E-01, 2.637482787039205E+00, 1.094469646723462E-01, 0.02)]
    [InlineData("Eris", 8.506314031511567E+01, 3.973078573508303E+01, -1.720599730248860E+01, 0.02)]
    [InlineData("2P/Encke", 1.562010961848464E+00, 6.418650553013605E-01, 2.531205262805021E-01, 1.0)]
    public void SmallBodiesMatchHorizons(string name, double x, double y, double z, double maxAngle)
    {
        var body = System.Value.Find(name)!;
        Assert.NotNull(body);
        Check(name, body.Position, new Vector3d(x, y, z), maxAngle, 0.02);
    }

    [Fact]
    public void MoonMatchesHorizons()
    {
        var moon = System.Value.Find("Moon")!;
        var earth = System.Value.Find("Earth")!;
        Check("Moon", moon.Position - earth.Position,
              new Vector3d(-1.518324045628562E-03, -2.186718077083572E-03, -2.324870968031709E-04), 0.05, 0.002);
    }

    /// <summary>Planet-centric satellite positions (JPL mean elements are not perturbation-complete, so tolerances are looser).</summary>
    [Theory]
    [InlineData("Phobos", 2.691650530838505E-05, 5.500743052627653E-05, -9.665159115949493E-06, 3, 0.05)]
    [InlineData("Io", 1.521460832178051E-03, -2.363764313718336E-03, -6.374278966848368E-05, 2, 0.02)]
    [InlineData("Callisto", 1.249385445761251E-02, 1.000681477993145E-04, 1.712311222851822E-04, 1, 0.02)]
    [InlineData("Titan", -7.532094671405597E-03, -2.851656539262713E-03, 2.219585068122440E-03, 2, 0.04)]
    [InlineData("Titania", 2.754166773315032E-03, -6.939743922500244E-04, -6.495731264256095E-04, 1, 0.02)]
    [InlineData("Triton", 7.861885298954206E-04, 1.949096366591529E-03, 1.097893606735228E-03, 1, 0.02)]
    [InlineData("Charon", 6.615892508199137E-05, 1.436665971288802E-06, -1.130397202913707E-04, 1, 0.02)]
    // Irregular moon: strongly perturbed by the Sun, so only phased to the present epoch.
    [InlineData("Himalia", -7.036482125676974E-02, 2.314629774231336E-03, 2.265575234319192E-02, 5, 0.15)]
    public void SatellitesMatchHorizons(string name, double x, double y, double z, double maxAngle, double maxRel)
    {
        var moon = System.Value.Find(name)!;
        Check(name, moon.Position - moon.Parent!.Position, new Vector3d(x, y, z), maxAngle, maxRel);
    }

    [Fact]
    public void CatalogsLoadEverything()
    {
        var s = System.Value;
        _out.WriteLine($"major {s.MajorBodies.Count}, moons {s.Moons.Count()}, asteroids {s.Asteroids.Count()}, comets {s.Comets.Count()}, dwarfs {s.DwarfPlanets.Count()}");
        Assert.True(s.Moons.Count() > 400);
        Assert.True(s.Asteroids.Count() > 5000);
        Assert.True(s.Comets.Count() > 4000);
        Assert.Equal(9, s.DwarfPlanets.Count());
        Assert.All(s.AllBodies, b => Assert.True(double.IsFinite(b.Position.X), $"{b.Name} has a non-finite position"));
    }
}
