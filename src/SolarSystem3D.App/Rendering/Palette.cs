using System.Windows.Media;
using SolarSystem3D.Core.Bodies;

namespace SolarSystem3D.App.Rendering;

/// <summary>Colours used by the scene.</summary>
public static class Palette
{
    public static readonly Color Background = Color.FromRgb(4, 6, 14);
    public static readonly Color Grid = Color.FromRgb(34, 44, 70);
    public static readonly Color GridLabel = Color.FromRgb(90, 110, 150);
    public static readonly Color DwarfOrbit = Color.FromRgb(120, 92, 150);
    public static readonly Color MoonOrbit = Color.FromRgb(78, 82, 92);
    public static readonly Color AsteroidOrbit = Color.FromRgb(92, 80, 60);
    public static readonly Color CometOrbit = Color.FromRgb(30, 72, 86);
    public static readonly Color Selection = Color.FromRgb(255, 230, 60);
    public static readonly Color MoonMarker = Color.FromRgb(190, 190, 196);
    public static readonly Color DwarfMarker = Color.FromRgb(200, 160, 235);
    public static readonly Color CometMarker = Color.FromRgb(120, 230, 255);

    public static readonly Color PlanetLabel = Color.FromRgb(235, 235, 240);
    public static readonly Color DwarfLabel = Color.FromRgb(205, 175, 235);
    public static readonly Color MoonLabel = Color.FromRgb(175, 180, 190);
    public static readonly Color SmallBodyLabel = Color.FromRgb(200, 185, 150);

    public static Color FromArgb(uint argb) =>
        Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    public static Color Dim(Color c, double f) =>
        Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

    /// <summary>Asteroid groups drawn as separate marker layers.</summary>
    public enum AsteroidGroup { MainBelt, NearEarth, MarsCrosser, Trojan, Outer }

    public static AsteroidGroup GroupOf(Asteroid a) => a.OrbitClass switch
    {
        "APO" or "ATE" or "AMO" or "IEO" => AsteroidGroup.NearEarth,
        "MCA" => AsteroidGroup.MarsCrosser,
        "TJN" => AsteroidGroup.Trojan,
        "TNO" or "CEN" or "PAA" or "HYA" => AsteroidGroup.Outer,
        _ => AsteroidGroup.MainBelt,
    };

    public static Color ColorOf(AsteroidGroup g) => g switch
    {
        AsteroidGroup.NearEarth => Color.FromRgb(255, 96, 80),
        AsteroidGroup.MarsCrosser => Color.FromRgb(240, 160, 80),
        AsteroidGroup.Trojan => Color.FromRgb(120, 200, 110),
        AsteroidGroup.Outer => Color.FromRgb(110, 150, 240),
        _ => Color.FromRgb(170, 150, 120),
    };

    public static string Describe(AsteroidGroup g) => g switch
    {
        AsteroidGroup.NearEarth => "Near-Earth",
        AsteroidGroup.MarsCrosser => "Mars-crosser",
        AsteroidGroup.Trojan => "Jupiter trojan",
        AsteroidGroup.Outer => "Centaur / TNO",
        _ => "Main belt",
    };
}
