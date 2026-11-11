using System.Globalization;
using AstroCore.Astronomy;
using SolarSystem3D.Core.Bodies;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.Core.Data;

/// <summary>Turns the tab-separated catalog files into <see cref="CelestialBody"/> objects.</summary>
public static class CatalogReader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static IEnumerable<Dictionary<string, string>> ReadTsv(TextReader reader)
    {
        string[]? header = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var cells = line.Split('\t');
            if (header is null) { header = cells; continue; }
            var row = new Dictionary<string, string>(header.Length);
            for (int k = 0; k < header.Length; k++)
                row[header[k]] = k < cells.Length ? cells[k].Trim() : "";
            yield return row;
        }
    }

    private static double Num(Dictionary<string, string> row, string key, double fallback = double.NaN) =>
        row.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, Inv, out double v) ? v : fallback;

    // ---------------------------------------------------------------- satellites

    /// <summary>Reads JPL mean satellite elements and attaches each moon to its parent in <paramref name="system"/>.</summary>
    public static List<NaturalSatellite> ReadSatellites(TextReader reader, SolarSystem system)
    {
        var list = new List<NaturalSatellite>();
        foreach (var row in ReadTsv(reader))
        {
            string id = row["id"];
            if (id == "301") continue; // the Moon uses the built-in lunar theory
            var parent = system.Find(row["planet"]);
            if (parent is null) continue;

            string frameName = row["frame"];
            Matrix3d frame;
            string planeName;
            switch (frameName.ToLowerInvariant())
            {
                case "laplace":
                    double ra = Num(row, "pole_ra"), dec = Num(row, "pole_dec");
                    if (double.IsNaN(ra) || double.IsNaN(dec)) (ra, dec) = MajorBodyCatalog.Poles.GetValueOrDefault(parent.Name, (0, 90));
                    frame = Frames.EclipticFromPlaneWithPole(ra, dec);
                    planeName = $"Local Laplace plane of {parent.Name}";
                    break;
                case "equatorial":
                    var pole = MajorBodyCatalog.Poles.GetValueOrDefault(parent.Name, (0, 90));
                    frame = Frames.EclipticFromPlaneWithPole(pole.Item1, pole.Item2);
                    planeName = $"{parent.Name} equator";
                    break;
                default:
                    frame = Matrix3d.Identity;
                    planeName = "J2000 ecliptic";
                    break;
            }

            double period = Num(row, "P_days");
            double a = Num(row, "a_km");
            if (!(period > 0) || !(a > 0)) continue;

            // Prefer the phase and rate calibrated against JPL Horizons (see tools/calibrate_satellites.py).
            double mFit = Num(row, "M_fit_deg"), nFit = Num(row, "n_fit_deg_d");
            bool calibrated = !double.IsNaN(mFit) && !double.IsNaN(nFit);
            var orbit = KeplerOrbit.ForSatellite(a, Num(row, "e", 0), Num(row, "w_deg", 0), calibrated ? mFit : Num(row, "M_deg", 0),
                Num(row, "i_deg", 0), Num(row, "node_deg", 0), period, Num(row, "Papsis_yr", 0), Num(row, "Pnode_yr", 0),
                Num(row, "epoch_jd", AstroTime.J2000), frame, planeName, calibrated ? nFit : null);

            double radius = Num(row, "radius_km");
            bool estimate = double.IsNaN(radius);
            if (estimate) radius = 5;
            list.Add(new NaturalSatellite(id, FormatSatelliteName(row["name"]), radius, parent, orbit, estimate)
            {
                ColorArgb = 0xFFC8C8C8,
                IsIrregular = frameName.Equals("ecliptic", StringComparison.OrdinalIgnoreCase),
            });
        }
        return list;
    }

    private static readonly System.Text.RegularExpressions.Regex ProvisionalMoon =
        new(@"^S(\d{4})_([A-Z])_(\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Turns JPL's "S2004_S_42" into the conventional provisional designation "S/2004 S 42".</summary>
    public static string FormatSatelliteName(string raw)
    {
        var m = ProvisionalMoon.Match(raw.Trim());
        return m.Success ? $"S/{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}" : raw.Trim().Replace('_', ' ');
    }

    // ---------------------------------------------------------------- asteroids

    /// <summary>Converts an SBDB "full_name" such as "     4 Vesta (A807 FA)" into "4 Vesta".</summary>
    public static string CleanAsteroidName(string fullName)
    {
        string s = fullName.Trim();
        if (s.StartsWith('(') && s.EndsWith(')')) return s[1..^1];
        int p = s.IndexOf(" (", StringComparison.Ordinal);
        return p > 0 ? s[..p] : s;
    }

    /// <summary>Diameter from absolute magnitude assuming a geometric albedo of 0.14.</summary>
    public static double EstimateDiameterKm(double h) => 1329.0 / Math.Sqrt(0.14) * Math.Pow(10, -h / 5);

    /// <summary>
    /// Reads SBDB asteroid rows. Known dwarf planets are returned separately as <see cref="DwarfPlanet"/>s.
    /// </summary>
    public static (List<Asteroid> Asteroids, List<DwarfPlanet> DwarfPlanets) ReadAsteroids(TextReader reader)
    {
        var asteroids = new List<Asteroid>();
        var dwarfs = new List<DwarfPlanet>();
        var seen = new HashSet<string>();
        foreach (var row in ReadTsv(reader))
        {
            string id = row["spkid"];
            if (id == MajorBodyCatalog.PlutoSpkId || !seen.Add(id)) continue;
            double a = Num(row, "a"), e = Num(row, "e");
            if (double.IsNaN(a) || double.IsNaN(e) || e >= 1 || a <= 0) continue; // skip open-orbit "asteroids"

            var orbit = KeplerOrbit.FromMeanAnomaly(a, e, Num(row, "i"), Num(row, "om"), Num(row, "w"), Num(row, "ma"), Num(row, "epoch"));
            string cls = row.GetValueOrDefault("class", "AST");

            if (MajorBodyCatalog.DwarfPlanets.TryGetValue(id, out var dp))
            {
                dwarfs.Add(new DwarfPlanet(id, dp.Name, dp.RadiusKm, orbit, dp.Iau, cls) { ColorArgb = dp.Color });
                continue;
            }

            double h = Num(row, "H", 15);
            double d = Num(row, "diameter");
            bool estimate = double.IsNaN(d);
            if (estimate) d = EstimateDiameterKm(h);
            asteroids.Add(new Asteroid(id, CleanAsteroidName(row["name"]), d / 2, orbit, h, cls, estimate));
        }
        return (asteroids, dwarfs);
    }

    // ---------------------------------------------------------------- comets

    public static List<Comet> ReadComets(TextReader reader)
    {
        var list = new List<Comet>();
        var seen = new HashSet<string>();
        foreach (var row in ReadTsv(reader))
        {
            string id = row["spkid"];
            if (!seen.Add(id)) continue;
            double q = Num(row, "q"), e = Num(row, "e"), tp = Num(row, "tp");
            if (double.IsNaN(q) || double.IsNaN(e) || double.IsNaN(tp) || q <= 0) continue;
            var orbit = KeplerOrbit.FromPerihelion(q, e, Num(row, "i"), Num(row, "om"), Num(row, "w"), tp);
            double d = Num(row, "diameter");
            double radius = double.IsNaN(d) ? 2.0 : d / 2;
            list.Add(new Comet(id, row["name"].Trim(), radius, orbit, row.GetValueOrDefault("class", "COM")) { ColorArgb = 0xFF7FE8FF });
        }
        return list;
    }
}
