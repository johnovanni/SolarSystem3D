namespace SolarSystem3D.Core.Astronomy;

/// <summary>Time-scale helpers: Julian dates and the UTC to TT (≈TDB) correction.</summary>
public static class AstroTime
{
    public const double J2000 = 2451545.0;
    public const double DaysPerJulianCentury = 36525.0;
    public const double DaysPerJulianYear = 365.25;

    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Julian Day number (UT) for a UTC <see cref="DateTime"/>.</summary>
    public static double ToJulianDayUtc(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
        return 2440587.5 + (utc - UnixEpoch).TotalDays;
    }

    public static DateTime FromJulianDayUtc(double jd)
    {
        double days = jd - 2440587.5;
        // Clamp to DateTime's range so a runaway animation never throws.
        double min = (DateTime.MinValue - UnixEpoch).TotalDays + 1;
        double max = (DateTime.MaxValue - UnixEpoch).TotalDays - 1;
        days = Math.Clamp(days, min, max);
        return UnixEpoch.AddDays(days);
    }

    /// <summary>Julian Day in Terrestrial Time (used as TDB; the difference is &lt; 2 ms).</summary>
    public static double ToJulianDayTT(DateTime utc)
    {
        double jdUtc = ToJulianDayUtc(utc);
        return jdUtc + DeltaTSeconds(utc.Year + (utc.DayOfYear - 0.5) / 365.25) / 86400.0;
    }

    /// <summary>ΔT = TT − UT in seconds (Espenak &amp; Meeus polynomial fits, simplified).</summary>
    public static double DeltaTSeconds(double year)
    {
        double t;
        if (year < -500 || year >= 2150)
        {
            double u = (year - 1820) / 100.0;
            return -20 + 32 * u * u;
        }
        if (year < 500)
        {
            double u = year / 100.0;
            return 10583.6 - 1014.41 * u + 33.78311 * u * u - 5.952053 * u * u * u;
        }
        if (year < 1600)
        {
            double u = (year - 1000) / 100.0;
            return 1574.2 - 556.01 * u + 71.23472 * u * u + 0.319781 * u * u * u;
        }
        if (year < 1700) { t = year - 1600; return 120 - 0.9808 * t - 0.01532 * t * t + t * t * t / 7129; }
        if (year < 1800) { t = year - 1700; return 8.83 + 0.1603 * t - 0.0059285 * t * t + 0.00013336 * t * t * t; }
        if (year < 1860) { t = year - 1800; return 13.72 - 0.332447 * t + 0.0068612 * t * t + 0.0041116 * t * t * t - 0.00037436 * t * t * t * t; }
        if (year < 1900) { t = year - 1860; return 7.62 + 0.5737 * t - 0.251754 * t * t + 0.01680668 * t * t * t; }
        if (year < 1920) { t = year - 1900; return -2.79 + 1.494119 * t - 0.0598939 * t * t + 0.0061966 * t * t * t; }
        if (year < 1941) { t = year - 1920; return 21.20 + 0.84493 * t - 0.076100 * t * t + 0.0020936 * t * t * t; }
        if (year < 1961) { t = year - 1950; return 29.07 + 0.407 * t - t * t / 233 + t * t * t / 2547; }
        if (year < 1986) { t = year - 1975; return 45.45 + 1.067 * t - t * t / 260 - t * t * t / 718; }
        if (year < 2005) { t = year - 2000; return 63.86 + 0.3345 * t - 0.060374 * t * t + 0.0017275 * t * t * t; }
        if (year < 2050)
        {
            // Observed ΔT has stayed near 69 s since 2016; the published polynomial over-predicts it.
            t = year - 2000;
            return Math.Min(62.92 + 0.32217 * t + 0.005589 * t * t, 69.2 + 0.1 * Math.Max(0, year - 2025));
        }
        double v = (year - 1820) / 100.0;
        return -20 + 32 * v * v - 0.5628 * (2150 - year);
    }

    /// <summary>Julian centuries since J2000 for a TT Julian date.</summary>
    public static double CenturiesSinceJ2000(double jdTT) => (jdTT - J2000) / DaysPerJulianCentury;
}
