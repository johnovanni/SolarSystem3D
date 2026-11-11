using SolarSystem3D.Core.Bodies;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.Core.Data;

/// <summary>Built-in data for the Sun, planets, the Moon and Pluto.</summary>
public static class MajorBodyCatalog
{
    /// <summary>
    /// Rotation-pole directions (ICRF RA/Dec, degrees) used for "equatorial" satellite frames and rings.
    /// These are right-hand-rule spin poles; for Uranus this is the opposite of the IAU "north" pole
    /// (257.311°, −15.175°), matching the frame of JPL's Uranian satellite elements.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (double Ra, double Dec)> Poles =
        new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mercury"] = (281.0103, 61.4155),
            ["Venus"] = (272.76, 67.16),
            ["Earth"] = (0.0, 90.0),
            ["Mars"] = (317.681, 52.887),
            ["Jupiter"] = (268.057, 64.495),
            ["Saturn"] = (40.589, 83.537),
            ["Uranus"] = (77.311, 15.175),
            ["Neptune"] = (299.36, 43.46),
            ["Pluto"] = (132.993, -6.163),
        };

    /// <summary>Creates the solar system skeleton: Sun, eight planets, the Moon and Pluto.</summary>
    public static SolarSystem CreateSkeleton()
    {
        var sun = new Star("10", "Sun", 695_700) { ColorArgb = 0xFFFFD84D };
        var system = new SolarSystem(sun);

        // JPL approximate elements, Table 1 (J2000, valid 1800–2050):
        // a, ȧ | e, ė | I, İ | L, L̇ | ϖ, ϖ̇ | Ω, Ω̇
        var mercury = new PlanetOrbit(0.38709927, 0.00000037, 0.20563593, 0.00001906, 7.00497902, -0.00594749,
                                      252.25032350, 149472.67411175, 77.45779628, 0.16047689, 48.33076593, -0.12534081);
        var venus = new PlanetOrbit(0.72333566, 0.00000390, 0.00677672, -0.00004107, 3.39467605, -0.00078890,
                                    181.97909950, 58517.81538729, 131.60246718, 0.00268329, 76.67984255, -0.27769418);
        var emb = new PlanetOrbit(1.00000261, 0.00000562, 0.01671123, -0.00004392, -0.00001531, -0.01294668,
                                  100.46457166, 35999.37244981, 102.93768193, 0.32327364, 0.0, 0.0);
        var mars = new PlanetOrbit(1.52371034, 0.00001847, 0.09339410, 0.00007882, 1.84969142, -0.00813131,
                                   -4.55343205, 19140.30268499, -23.94362959, 0.44441088, 49.55953891, -0.29257343);
        var jupiter = new PlanetOrbit(5.20288700, -0.00011607, 0.04838624, -0.00013253, 1.30439695, -0.00183714,
                                      34.39644051, 3034.74612775, 14.72847983, 0.21252668, 100.47390909, 0.20469106);
        var saturn = new PlanetOrbit(9.53667594, -0.00125060, 0.05386179, -0.00050991, 2.48599187, 0.00193609,
                                     49.95424423, 1222.49362201, 92.59887831, -0.41897216, 113.66242448, -0.28867794);
        var uranus = new PlanetOrbit(19.18916464, -0.00196176, 0.04725744, -0.00004397, 0.77263783, -0.00242939,
                                     313.23810451, 428.48202785, 170.95427630, 0.40805281, 74.01692503, 0.04240589);
        var neptune = new PlanetOrbit(30.06992276, 0.00026291, 0.00859048, 0.00005105, 1.77004347, 0.00035372,
                                      -55.12002969, 218.45945325, 44.96476227, -0.32241464, 131.78422574, -0.00508664);
        var pluto = new PlanetOrbit(39.48211675, -0.00031596, 0.24882730, 0.00005170, 17.14001206, 0.00004818,
                                    238.92903833, 145.20780515, 224.06891629, -0.04062942, 110.30393684, -0.01183482);

        var lunar = new LunarOrbit();
        var earth = new Planet("399", "Earth", 6371.0, new EarthOrbit(emb, lunar), Poles["Earth"]) { ColorArgb = 0xFF4F8FE0 };

        var bodies = new List<CelestialBody>
        {
            new Planet("199", "Mercury", 2439.7, mercury, Poles["Mercury"]) { ColorArgb = 0xFFA8A8A8 },
            new Planet("299", "Venus", 6051.8, venus, Poles["Venus"]) { ColorArgb = 0xFFE8CDA0 },
            earth,
            new Planet("499", "Mars", 3389.5, mars, Poles["Mars"]) { ColorArgb = 0xFFD0603A },
            new Planet("599", "Jupiter", 69_911, jupiter, Poles["Jupiter"]) { ColorArgb = 0xFFD8B48A },
            new Planet("699", "Saturn", 58_232, saturn, Poles["Saturn"]) { ColorArgb = 0xFFE6D08A, Rings = (74_500, 140_220) },
            new Planet("799", "Uranus", 25_362, uranus, Poles["Uranus"]) { ColorArgb = 0xFF9FDCE6 },
            new Planet("899", "Neptune", 24_622, neptune, Poles["Neptune"]) { ColorArgb = 0xFF5A7FF0 },
            new DwarfPlanet("999", "Pluto", 1188.3, pluto, iauRecognized: true, "TNO") { ColorArgb = 0xFFD9C3A5 },
        };
        bodies.Add(new NaturalSatellite("301", "Moon", 1737.4, earth, lunar, radiusIsEstimate: false) { ColorArgb = 0xFFC8C8C8 });

        system.AddRange(bodies);
        return system;
    }

    /// <summary>
    /// SBDB SPK-IDs of the IAU dwarf planets (besides Pluto) and widely accepted candidates,
    /// with mean radii in km. These are promoted from the asteroid catalog to <see cref="DwarfPlanet"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Name, double RadiusKm, bool Iau, uint Color)> DwarfPlanets =
        new Dictionary<string, (string, double, bool, uint)>
        {
            ["20000001"] = ("Ceres", 469.7, true, 0xFFBFB6A8),
            ["20136199"] = ("Eris", 1163, true, 0xFFE8E4F0),
            ["20136108"] = ("Haumea", 780, true, 0xFFD8D8E8),
            ["20136472"] = ("Makemake", 715, true, 0xFFE0B090),
            ["20225088"] = ("Gonggong", 615, false, 0xFFD08070),
            ["20050000"] = ("Quaoar", 545, false, 0xFFC09080),
            ["20090482"] = ("Orcus", 455, false, 0xFFB0B0C0),
            ["20090377"] = ("Sedna", 500, false, 0xFFE07050),
        };

    /// <summary>SPK-ID of Pluto in SBDB (skipped because Pluto is built in).</summary>
    public const string PlutoSpkId = "20134340";
}
