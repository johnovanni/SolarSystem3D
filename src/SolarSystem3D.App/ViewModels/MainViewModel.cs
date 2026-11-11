using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarSystem3D.App.Rendering;
using SolarSystem3D.App.Simulation;
using SolarSystem3D.Core;
using AstroCore.Astronomy;
using SolarSystem3D.Core.Bodies;
using SolarSystem3D.Core.Data;
using AstroCore.Orbits;

namespace SolarSystem3D.App.ViewModels;

public sealed record InfoRow(string Label, string Value);

public sealed record LegendItem(string Name, System.Windows.Media.Color Color);

public sealed record AsteroidCatalogOption(int Count, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Main window state: clock, display options, selection, search and catalog management.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double AuKm = 149_597_870.7;
    private readonly CatalogStore _store = new();
    private SolarSystem? _system;

    public MainViewModel()
    {
        Clock.PropertyChanged += OnClockChanged;
    }

    public SimulationClock Clock { get; } = new();
    public DisplayOptions Display { get; } = new();
    public IReadOnlyList<TimeRate> Rates => TimeRate.All;
    public CatalogStore Store => _store;

    /// <summary>Colour legend for asteroid groups and comets.</summary>
    public IReadOnlyList<LegendItem> Legend { get; } =
        Enum.GetValues<Palette.AsteroidGroup>()
            .Select(g => new LegendItem(Palette.Describe(g), Palette.ColorOf(g)))
            .Append(new LegendItem("Comet", Palette.CometMarker))
            .Append(new LegendItem("Dwarf planet", Palette.DwarfMarker))
            .ToList();

    // ------------------------------------------------------------------ events for the scene controller

    /// <summary>Raised when the user asks to centre the view on a body.</summary>
    public event EventHandler<CelestialBody>? FocusRequested;
    public event EventHandler? ResetViewRequested;
    /// <summary>Raised after new asteroid/comet catalogs were downloaded.</summary>
    public event EventHandler? CatalogsReloadRequested;

    public void AttachSystem(SolarSystem system, CatalogLoadReport report)
    {
        _system = system;
        CatalogStatus = report.ToString();
        IsLoaded = true;
        RefreshInfo();
    }

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private string _statusText = "Loading catalogs…";
    [ObservableProperty] private string _frameRateText = "";
    [ObservableProperty] private string _focusName = "Sun";

    // ------------------------------------------------------------------ calendar & clock

    [ObservableProperty] private bool _useLocalTime;

    private DateTime DisplayTime => UseLocalTime ? Clock.Utc.ToLocalTime() : Clock.Utc;
    private string ZoneName => UseLocalTime ? TimeZoneInfo.Local.StandardName : "UTC";

    public string ClockText => DisplayTime.ToString("yyyy-MM-dd  HH:mm:ss", Inv) + "  " + (UseLocalTime ? "local" : "UTC");
    public string JulianDateText => $"JD (TT) {Clock.JulianDateTT:F5}";
    public string DateWarning => PlanetOrbit.IsWithinValidRange(Clock.JulianDateTT)
        ? ""
        : "Outside 1800–2050: planet positions use extrapolated elements and lose accuracy.";

    /// <summary>Calendar date shown in the date picker (in the selected time zone).</summary>
    public DateTime? SelectedDate
    {
        get => DisplayTime.Date;
        set
        {
            if (value is not DateTime d) return;
            var t = DisplayTime;
            SetDisplayTime(new DateTime(d.Year, d.Month, d.Day, t.Hour, t.Minute, t.Second, t.Millisecond));
        }
    }

    /// <summary>Time of day as HH:mm:ss (in the selected time zone).</summary>
    public string TimeText
    {
        get => DisplayTime.ToString("HH:mm:ss", Inv);
        set
        {
            if (TimeSpan.TryParseExact(value?.Trim(), new[] { @"hh\:mm\:ss", @"hh\:mm", @"h\:mm", @"h\:mm\:ss" }, Inv, out var ts) && ts < TimeSpan.FromDays(1))
                SetDisplayTime(DisplayTime.Date + ts);
            else
                OnPropertyChanged(nameof(TimeText)); // revert the text box
        }
    }

    private void SetDisplayTime(DateTime t)
    {
        try
        {
            Clock.Utc = UseLocalTime
                ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), TimeZoneInfo.Local)
                : DateTime.SpecifyKind(t, DateTimeKind.Utc);
        }
        catch (ArgumentException)
        {
            // Local time that does not exist (DST gap) — nudge forward an hour.
            Clock.Utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t.AddHours(1), DateTimeKind.Unspecified), TimeZoneInfo.Local);
        }
    }

    partial void OnUseLocalTimeChanged(bool value) => RaiseTimeProperties();

    private DateTime _lastInfoRefresh;

    private void OnClockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationClock.Utc))
        {
            RaiseTimeProperties();
            if (!Clock.IsRunning || (DateTime.UtcNow - _lastInfoRefresh).TotalMilliseconds > 250)
                RefreshInfo();
        }
        else if (e.PropertyName == nameof(SimulationClock.IsRunning) || e.PropertyName == nameof(SimulationClock.IsReversed))
        {
            OnPropertyChanged(nameof(PlayGlyph));
        }
    }

    private void RaiseTimeProperties()
    {
        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(JulianDateText));
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(DateWarning));
    }

    public string PlayGlyph => Clock.IsRunning ? "⏸" : "▶";

    [RelayCommand] private void TogglePlay() => Clock.IsRunning = !Clock.IsRunning;

    [RelayCommand]
    private void PlayForward() { Clock.IsReversed = false; Clock.IsRunning = true; }

    [RelayCommand]
    private void PlayBackward() { Clock.IsReversed = true; Clock.IsRunning = true; }

    [RelayCommand] private void Now() => Clock.SetNow();

    /// <summary>Steps the calendar: parameter like "+1d", "-1h", "+1mo", "-1y".</summary>
    [RelayCommand]
    private void Step(string? spec)
    {
        if (string.IsNullOrEmpty(spec)) return;
        int sign = spec[0] == '-' ? -1 : 1;
        string unit = spec.TrimStart('+', '-').TrimStart('1');
        var t = Clock.Utc;
        try
        {
            Clock.Utc = unit switch
            {
                "h" => t.AddHours(sign),
                "d" => t.AddDays(sign),
                "w" => t.AddDays(7 * sign),
                "mo" => t.AddMonths(sign),
                "y" => t.AddYears(sign),
                _ => t,
            };
        }
        catch (ArgumentOutOfRangeException) { }
    }

    // ------------------------------------------------------------------ selection & search

    [ObservableProperty] private CelestialBody? _selectedBody;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private CelestialBody? _selectedSearchResult;
    public ObservableCollection<CelestialBody> SearchResults { get; } = new();
    public ObservableCollection<InfoRow> InfoRows { get; } = new();
    public string SelectedTitle => SelectedBody?.Name ?? "Nothing selected";
    public string SelectedType => SelectedBody?.TypeDescription ?? "Click an object in the view, or search above.";

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        if (_system is null) return;
        foreach (var b in _system.Search(value, 60)) SearchResults.Add(b);
    }

    partial void OnSelectedSearchResultChanged(CelestialBody? value)
    {
        if (value is null) return;
        SelectedBody = value;
    }

    partial void OnSelectedBodyChanged(CelestialBody? value)
    {
        OnPropertyChanged(nameof(SelectedTitle));
        OnPropertyChanged(nameof(SelectedType));
        RefreshInfo();
    }

    [RelayCommand]
    private void CenterOnSelected()
    {
        if (SelectedBody != null) FocusRequested?.Invoke(this, SelectedBody);
    }

    [RelayCommand]
    private void CenterOnSun()
    {
        if (_system != null) FocusRequested?.Invoke(this, _system.Sun);
    }

    [RelayCommand] private void ResetView() => ResetViewRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Selects a body by name (used by the quick-jump buttons).</summary>
    [RelayCommand]
    private void GoTo(string? name)
    {
        var body = name is null ? null : _system?.Find(name);
        if (body is null) return;
        SelectedBody = body;
        FocusRequested?.Invoke(this, body);
    }

    public void RefreshInfo()
    {
        _lastInfoRefresh = DateTime.UtcNow;
        InfoRows.Clear();
        var b = SelectedBody;
        if (b is null || _system is null) return;
        double jd = Clock.JulianDateTT;

        static string Au(double au) => au < 0.01
            ? $"{au * AuKm:N0} km"
            : $"{au.ToString("F4", Inv)} AU ({(au * AuKm / 1e6).ToString("N1", Inv)} million km)";

        if (b.Parent is { } parent)
            InfoRows.Add(new("Distance from " + parent.Name, Au((b.Position - parent.Position).Length)));
        if (b is not Star)
            InfoRows.Add(new("Distance from Sun", Au(b.Position.Length)));
        var earth = _system.Find("Earth");
        if (earth != null && b != earth)
        {
            double d = (b.Position - earth.Position).Length;
            double lightMin = d * AuKm / 299_792.458 / 60;
            InfoRows.Add(new("Distance from Earth", Au(d)));
            InfoRows.Add(new("Light time from Earth", lightMin < 120 ? $"{lightMin:F1} min" : $"{lightMin / 60:F2} h"));
        }
        if (b is not Star)
        {
            var p = b.Position;
            double lon = Frames.NormalizeDegrees(Math.Atan2(p.Y, p.X) / Frames.Deg);
            double lat = Math.Asin(p.Z / Math.Max(p.Length, 1e-30)) / Frames.Deg;
            InfoRows.Add(new("Heliocentric ecl. lon / lat", $"{lon:F3}° / {lat:+0.000;-0.000}°"));
        }

        string radius = $"{b.RadiusKm:N1} km";
        if (b is Asteroid { RadiusIsEstimate: true } or NaturalSatellite { RadiusIsEstimate: true }) radius += " (estimate)";
        InfoRows.Add(new("Mean radius", radius));

        foreach (var (label, value) in b.GetDetails()) InfoRows.Add(new(label, value));

        if (b.Orbit != null)
        {
            var o = b.Orbit.Describe(jd);
            string unit(double au) => b is NaturalSatellite ? $"{au * AuKm:N0} km" : $"{au:F4} AU";
            if (o.SemiMajorAxisAu is double a) InfoRows.Add(new("Semi-major axis", unit(a)));
            InfoRows.Add(new("Eccentricity", o.Eccentricity.ToString("F5", Inv)));
            InfoRows.Add(new("Inclination", $"{o.InclinationDeg:F3}° to {o.ReferencePlane}"));
            InfoRows.Add(new(b.Parent is null ? "Perihelion" : "Periapsis", unit(o.PeriapsisAu)));
            if (o.ApoapsisAu is double q) InfoRows.Add(new(b.Parent is null ? "Aphelion" : "Apoapsis", unit(q)));
            if (o.PeriodDays is double per)
                InfoRows.Add(new("Orbital period", per < 1000 ? $"{per:F3} days" : $"{per / 365.25:F2} years"));
            else
                InfoRows.Add(new("Orbit", "Open (parabolic/hyperbolic) — will not return"));
            InfoRows.Add(new("Data source", o.Source));
        }
        InfoRows.Add(new("ID (JPL)", b.Id));
    }

    // ------------------------------------------------------------------ catalogs

    public IReadOnlyList<AsteroidCatalogOption> AsteroidCatalogOptions { get; } = new[]
    {
        new AsteroidCatalogOption(20_000, "20,000 brightest asteroids"),
        new AsteroidCatalogOption(50_000, "50,000 brightest asteroids"),
        new AsteroidCatalogOption(100_000, "100,000 brightest asteroids"),
        new AsteroidCatalogOption(200_000, "200,000 brightest (slow)"),
    };

    [ObservableProperty] private AsteroidCatalogOption? _selectedCatalogOption;
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCatalogsCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseBuiltInCatalogsCommand))]
    private bool _isDownloading;

    private bool CanDownload() => !IsDownloading && IsLoaded;

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadCatalogsAsync()
    {
        var option = SelectedCatalogOption ?? AsteroidCatalogOptions[0];
        IsDownloading = true;
        var progress = new Progress<string>(s => CatalogStatus = s);
        try
        {
            using var downloader = new CatalogDownloader(_store);
            int a = await downloader.DownloadAsteroidsAsync(option.Count, 2000, progress);
            int c = await downloader.DownloadCometsAsync(progress);
            CatalogStatus = $"Downloaded {a:N0} asteroids and {c:N0} comets from JPL. Loading…";
            CatalogsReloadRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CatalogStatus = "Download failed: " + ex.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void UseBuiltInCatalogs()
    {
        _store.ClearCache();
        CatalogStatus = "Using the built-in catalog snapshot. Loading…";
        CatalogsReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsLoadedChanged(bool value)
    {
        DownloadCatalogsCommand.NotifyCanExecuteChanged();
        UseBuiltInCatalogsCommand.NotifyCanExecuteChanged();
    }
}
