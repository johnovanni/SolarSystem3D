using AstroCore.Astronomy;
using SolarSystem3D.Core.Data;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.Tests;

public class MathTests
{
    [Theory]
    [InlineData(0.1, 0.0167)]
    [InlineData(3.0, 0.5)]
    [InlineData(-2.0, 0.97)]
    [InlineData(0.01, 0.999)]
    public void EllipticKeplerSolves(double m, double e)
    {
        double ea = Conic.SolveElliptic(m, e);
        Assert.Equal(Math.IEEERemainder(m, 2 * Math.PI), ea - e * Math.Sin(ea), 10);
    }

    [Theory]
    [InlineData(0.5, 1.5)]
    [InlineData(50, 1.01)]
    [InlineData(-10, 3)]
    public void HyperbolicKeplerSolves(double m, double e)
    {
        double h = Conic.SolveHyperbolic(m, e);
        Assert.Equal(m, e * Math.Sinh(h) - h, 8);
    }

    [Fact]
    public void JulianDayOfJ2000()
    {
        Assert.Equal(2451545.0, AstroTime.ToJulianDayUtc(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)), 9);
        var back = AstroTime.FromJulianDayUtc(2451545.0);
        Assert.Equal(new DateTime(2000, 1, 1, 12, 0, 0), back);
    }

    [Fact]
    public void ParabolicOrbitIsContinuousAcrossNearParabolic()
    {
        var para = KeplerOrbit.FromPerihelion(1.0, 1.0, 10, 20, 30, 2451545.0);
        var nearHyp = KeplerOrbit.FromPerihelion(1.0, 1.0000002, 10, 20, 30, 2451545.0);
        var a = para.PositionAt(2451545.0 + 40);
        var b = nearHyp.PositionAt(2451545.0 + 40);
        Assert.True((a - b).Length < 1e-4);
        Assert.Equal(1.0, para.PositionAt(2451545.0).Length, 9);
    }

    [Fact]
    public void OrbitPathIsClippedForOpenOrbits()
    {
        var hyp = KeplerOrbit.FromPerihelion(1.0, 1.5, 0, 0, 0, 2451545.0);
        var path = hyp.SamplePath(2451545.0, 100, 50);
        Assert.NotEmpty(path);
        Assert.All(path, p => Assert.True(p.Length <= 50.0001));
    }

    [Theory]
    [InlineData("     4 Vesta (A807 FA)", "4 Vesta")]
    [InlineData("(2024 YR4)", "2024 YR4")]
    [InlineData("99942 Apophis (2004 MN4)", "99942 Apophis")]
    public void AsteroidNamesAreCleaned(string raw, string expected) =>
        Assert.Equal(expected, CatalogReader.CleanAsteroidName(raw));

    [Theory]
    [InlineData("S2004_S_42", "S/2004 S 42")]
    [InlineData("Titan", "Titan")]
    public void SatelliteNamesAreFormatted(string raw, string expected) =>
        Assert.Equal(expected, CatalogReader.FormatSatelliteName(raw));
}
