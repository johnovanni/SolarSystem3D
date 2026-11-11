namespace SolarSystem3D.Core.Astronomy;

/// <summary>Reference-frame rotations. The simulation frame is the J2000 mean ecliptic and equinox.</summary>
public static class Frames
{
    public const double Deg = Math.PI / 180.0;

    /// <summary>Mean obliquity of the ecliptic at J2000 (IAU 1976 value used by JPL).</summary>
    public const double ObliquityJ2000 = 23.4392911 * Deg;

    /// <summary>Rotates ICRF/J2000 equatorial coordinates into J2000 ecliptic coordinates.</summary>
    public static readonly Matrix3d EclipticFromEquatorial = Matrix3d.RotationX(-ObliquityJ2000);

    /// <summary>
    /// Matrix that maps coordinates measured in a plane (x axis toward the plane's ascending node on the
    /// ICRF equator, z axis along the plane's pole) into J2000 ecliptic coordinates.
    /// This is the convention JPL uses for its satellite "Laplace" and "equatorial" element frames.
    /// </summary>
    public static Matrix3d EclipticFromPlaneWithPole(double poleRaDeg, double poleDecDeg)
    {
        double a = poleRaDeg * Deg, d = poleDecDeg * Deg;
        var pole = new Vector3d(Math.Cos(d) * Math.Cos(a), Math.Cos(d) * Math.Sin(a), Math.Sin(d));
        var node = new Vector3d(-Math.Sin(a), Math.Cos(a), 0);
        var y = Vector3d.Cross(pole, node);
        return EclipticFromEquatorial * Matrix3d.FromColumns(node, y, pole);
    }

    /// <summary>Unit vector of a pole given in equatorial RA/Dec, expressed in J2000 ecliptic coordinates.</summary>
    public static Vector3d PoleVector(double poleRaDeg, double poleDecDeg)
    {
        double a = poleRaDeg * Deg, d = poleDecDeg * Deg;
        return EclipticFromEquatorial * new Vector3d(Math.Cos(d) * Math.Cos(a), Math.Cos(d) * Math.Sin(a), Math.Sin(d));
    }

    public static double NormalizeRadians(double x)
    {
        x %= 2 * Math.PI;
        return x < 0 ? x + 2 * Math.PI : x;
    }

    public static double NormalizeDegrees(double x)
    {
        x %= 360.0;
        return x < 0 ? x + 360.0 : x;
    }
}
