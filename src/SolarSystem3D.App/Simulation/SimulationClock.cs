using CommunityToolkit.Mvvm.ComponentModel;
using SolarSystem3D.Core.Astronomy;

namespace SolarSystem3D.App.Simulation;

/// <summary>A selectable simulation speed: how many simulated seconds pass per real second.</summary>
public sealed record TimeRate(string Name, double SecondsPerSecond)
{
    public static IReadOnlyList<TimeRate> All { get; } = new[]
    {
        new TimeRate("Real time", 1),
        new TimeRate("1 minute / s", 60),
        new TimeRate("10 minutes / s", 600),
        new TimeRate("1 hour / s", 3600),
        new TimeRate("6 hours / s", 6 * 3600),
        new TimeRate("1 day / s", 86400),
        new TimeRate("1 week / s", 7 * 86400),
        new TimeRate("1 month / s", 30.4375 * 86400),
        new TimeRate("1 year / s", 365.25 * 86400),
        new TimeRate("10 years / s", 3652.5 * 86400),
    };

    public override string ToString() => Name;
}

/// <summary>
/// The simulation's settable calendar and clock. Time is held as a UTC <see cref="DateTime"/>;
/// <see cref="JulianDateTT"/> gives the dynamical time used for the ephemerides.
/// </summary>
public sealed partial class SimulationClock : ObservableObject
{
    public static readonly DateTime MinTime = new(1000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MaxTime = new(3000, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [ObservableProperty] private DateTime _utc = DateTime.UtcNow;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isReversed;
    [ObservableProperty] private TimeRate _rate = TimeRate.All[5];

    public double JulianDateTT => AstroTime.ToJulianDayTT(Utc);

    partial void OnUtcChanged(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) Utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        else if (value < MinTime) Utc = MinTime;
        else if (value > MaxTime) Utc = MaxTime;
    }

    /// <summary>Advances the clock by the given real-time interval at the current rate.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (!IsRunning) return;
        double seconds = realElapsed.TotalSeconds * Rate.SecondsPerSecond * (IsReversed ? -1 : 1);
        AddSeconds(seconds);
    }

    public void AddSeconds(double seconds)
    {
        double ticks = seconds * TimeSpan.TicksPerSecond;
        long min = MinTime.Ticks - Utc.Ticks, max = MaxTime.Ticks - Utc.Ticks;
        Utc = Utc.AddTicks((long)Math.Clamp(ticks, min, max));
        if (Utc == MinTime || Utc == MaxTime) IsRunning = false;
    }

    public void SetNow() => Utc = DateTime.UtcNow;
}
