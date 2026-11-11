using System.Globalization;
using SolarSystem3D.Core.Astronomy;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.Core.Bodies;

/// <summary>The Sun — origin of the heliocentric frame.</summary>
public sealed class Star : CelestialBody
{
    public Star(string id, string name, double radiusKm) : base(id, name, radiusKm, null, null) { }
    public override BodyKind Kind => BodyKind.Star;
    public override string TypeDescription => "Star (G2V)";
}

/// <summary>A body with a known rotation pole, so rings or orientation can be drawn.</summary>
public interface IHasPole
{
    /// <summary>North pole RA/Dec (ICRF, degrees).</summary>
    (double Ra, double Dec) Pole { get; }
}

/// <summary>One of the eight major planets.</summary>
public sealed class Planet : CelestialBody, IHasPole
{
    public Planet(string id, string name, double radiusKm, IOrbitModel orbit, (double Ra, double Dec) pole)
        : base(id, name, radiusKm, null, orbit) => Pole = pole;

    public override BodyKind Kind => BodyKind.Planet;
    public (double Ra, double Dec) Pole { get; }

    /// <summary>Inner/outer ring radii in km, if the planet has a prominent ring system to draw.</summary>
    public (double Inner, double Outer)? Rings { get; init; }
}

/// <summary>An IAU dwarf planet (or a widely accepted candidate).</summary>
public sealed class DwarfPlanet : CelestialBody
{
    public DwarfPlanet(string id, string name, double radiusKm, IOrbitModel orbit, bool iauRecognized, string? orbitClass = null)
        : base(id, name, radiusKm, null, orbit)
    {
        IsIauRecognized = iauRecognized;
        OrbitClass = orbitClass;
    }

    public override BodyKind Kind => BodyKind.DwarfPlanet;
    public bool IsIauRecognized { get; }
    public string? OrbitClass { get; }
    public override string TypeDescription => IsIauRecognized ? "Dwarf planet" : "Dwarf planet (candidate)";
}

/// <summary>A natural satellite orbiting a planet or dwarf planet.</summary>
public sealed class NaturalSatellite : CelestialBody
{
    public NaturalSatellite(string id, string name, double radiusKm, CelestialBody parent, IOrbitModel orbit, bool radiusIsEstimate)
        : base(id, name, radiusKm, parent, orbit) => RadiusIsEstimate = radiusIsEstimate;

    public override BodyKind Kind => BodyKind.Moon;
    public bool RadiusIsEstimate { get; }

    /// <summary>
    /// Irregular moons are distant, often retrograde captured bodies whose orbits are dominated by solar
    /// perturbations. JPL gives their elements relative to the ecliptic rather than the planet's equator.
    /// </summary>
    public bool IsIrregular { get; init; }

    public override string TypeDescription => $"{(IsIrregular ? "Irregular moon" : "Moon")} of {Parent!.Name}";
}

/// <summary>A minor planet from the JPL Small-Body Database.</summary>
public sealed class Asteroid : CelestialBody
{
    public Asteroid(string id, string name, double radiusKm, IOrbitModel orbit, double absoluteMagnitude, string orbitClass, bool radiusIsEstimate)
        : base(id, name, radiusKm, null, orbit)
    {
        AbsoluteMagnitude = absoluteMagnitude;
        OrbitClass = orbitClass;
        RadiusIsEstimate = radiusIsEstimate;
    }

    public override BodyKind Kind => BodyKind.Asteroid;
    public double AbsoluteMagnitude { get; }
    public string OrbitClass { get; }
    public bool RadiusIsEstimate { get; }
    public bool IsNearEarth => OrbitClass is "APO" or "ATE" or "AMO" or "IEO";
    public override string TypeDescription => $"Asteroid — {OrbitClasses.Describe(OrbitClass)}";

    public override IEnumerable<(string Label, string Value)> GetDetails()
    {
        yield return ("Abs. magnitude H", AbsoluteMagnitude.ToString("F2", CultureInfo.InvariantCulture));
    }
}

/// <summary>A comet from the JPL Small-Body Database.</summary>
public sealed class Comet : CelestialBody
{
    public Comet(string id, string name, double radiusKm, KeplerOrbit orbit, string orbitClass)
        : base(id, name, radiusKm, null, orbit)
    {
        OrbitClass = orbitClass;
        PerihelionJd = orbit.Epoch;
    }

    public override BodyKind Kind => BodyKind.Comet;
    public string OrbitClass { get; }
    public double PerihelionJd { get; }

    /// <summary>
    /// Sun-grazing comets on parabolic orbits (mostly SOHO discoveries of the Kreutz group) almost never
    /// survive perihelion, so they are only meaningful near their perihelion date.
    /// </summary>
    public bool IsSungrazer => ((KeplerOrbit)Orbit!).PeriapsisDistance < 0.05 && OrbitClass is "PAR" or "HYP";

    /// <summary>"D/" comets are defunct — disintegrated or lost (e.g. the Shoemaker–Levy 9 fragments).</summary>
    public bool IsDefunct => Name.StartsWith("D/", StringComparison.Ordinal) || Name.Contains(" D/", StringComparison.Ordinal);

    /// <summary>Whether the comet is still worth plotting at <paramref name="jdTT"/>: sungrazers and defunct comets
    /// are only shown within 60 days of their perihelion.</summary>
    public bool IsPlausiblyPresent(double jdTT) => !(IsSungrazer || IsDefunct) || Math.Abs(jdTT - PerihelionJd) < 60;

    /// <summary>True for numbered periodic comets such as 1P/Halley.</summary>
    public bool IsNumberedPeriodic => Name.Length > 1 && char.IsDigit(Name[0]) && Name.Contains("P/", StringComparison.Ordinal);

    public override string TypeDescription => $"Comet — {OrbitClasses.Describe(OrbitClass)}";

    public override IEnumerable<(string Label, string Value)> GetDetails()
    {
        yield return ("Perihelion date", AstroTime.FromJulianDayUtc(PerihelionJd).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
    }
}

/// <summary>JPL SBDB orbit class codes.</summary>
public static class OrbitClasses
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MBA"] = "Main-belt", ["IMB"] = "Inner main-belt", ["OMB"] = "Outer main-belt",
        ["TJN"] = "Jupiter trojan", ["CEN"] = "Centaur", ["TNO"] = "Trans-Neptunian object",
        ["MCA"] = "Mars-crosser", ["APO"] = "Apollo (near-Earth)", ["ATE"] = "Aten (near-Earth)",
        ["AMO"] = "Amor (near-Earth)", ["IEO"] = "Atira (near-Earth)", ["AST"] = "Asteroid", ["HYA"] = "Hyperbolic asteroid",
        ["PAA"] = "Parabolic asteroid",
        ["JFc"] = "Jupiter-family", ["JFC"] = "Jupiter-family", ["HTC"] = "Halley-type", ["ETc"] = "Encke-type",
        ["CTc"] = "Chiron-type", ["COM"] = "Long-period", ["PAR"] = "Parabolic", ["HYP"] = "Hyperbolic",
    };

    public static string Describe(string code) => Names.TryGetValue(code, out var n) ? n : code;
}
