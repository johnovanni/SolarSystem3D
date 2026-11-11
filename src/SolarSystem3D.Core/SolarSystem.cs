using SolarSystem3D.Core.Bodies;

namespace SolarSystem3D.Core;

/// <summary>
/// The collection of all simulated bodies. <see cref="Update"/> recomputes every position for a given
/// instant, updating parents before their satellites.
/// </summary>
public sealed class SolarSystem
{
    private readonly List<CelestialBody> _majorBodies = new();   // Sun, planets, dwarf planets, moons (ordered by depth)
    private readonly List<CelestialBody> _smallBodies = new();   // asteroids and comets (all heliocentric)
    private readonly Dictionary<string, CelestialBody> _byName = new(StringComparer.OrdinalIgnoreCase);

    public SolarSystem(Star sun)
    {
        Sun = sun;
        Register(sun);
    }

    public Star Sun { get; }
    public double JulianDateTT { get; private set; } = double.NaN;

    public IReadOnlyList<CelestialBody> MajorBodies => _majorBodies;
    public IReadOnlyList<CelestialBody> SmallBodies => _smallBodies;
    public IEnumerable<CelestialBody> AllBodies => _majorBodies.Concat(_smallBodies);

    public IEnumerable<Planet> Planets => _majorBodies.OfType<Planet>();
    public IEnumerable<DwarfPlanet> DwarfPlanets => _majorBodies.OfType<DwarfPlanet>();
    public IEnumerable<NaturalSatellite> Moons => _majorBodies.OfType<NaturalSatellite>();
    public IEnumerable<Asteroid> Asteroids => _smallBodies.OfType<Asteroid>();
    public IEnumerable<Comet> Comets => _smallBodies.OfType<Comet>();

    /// <summary>Raised after the set of bodies changes (e.g. catalogs reloaded).</summary>
    public event EventHandler? BodiesChanged;

    public void Add(CelestialBody body)
    {
        if (body is Asteroid or Comet) _smallBodies.Add(body);
        else
        {
            _majorBodies.Add(body);
            // Keep parents ahead of children.
            _majorBodies.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        }
        Register(body);
    }

    public void AddRange(IEnumerable<CelestialBody> bodies)
    {
        foreach (var b in bodies)
        {
            if (b is Asteroid or Comet) _smallBodies.Add(b);
            else _majorBodies.Add(b);
            Register(b);
        }
        // Stable sort by depth so planets stay in their natural order.
        var ordered = _majorBodies.OrderBy(b => b.Depth).ToList();
        _majorBodies.Clear();
        _majorBodies.AddRange(ordered);
        BodiesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces all asteroids and comets (e.g. after downloading a larger catalog).</summary>
    public void ReplaceSmallBodies(IEnumerable<CelestialBody> bodies)
    {
        foreach (var b in _smallBodies) _byName.Remove(b.Name);
        _smallBodies.Clear();
        foreach (var b in bodies)
        {
            _smallBodies.Add(b);
            Register(b);
        }
        if (!double.IsNaN(JulianDateTT)) Update(JulianDateTT);
        BodiesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Register(CelestialBody body) => _byName.TryAdd(body.Name, body);

    public CelestialBody? Find(string name) => _byName.TryGetValue(name, out var b) ? b : null;

    /// <summary>Case-insensitive substring search, major bodies first.</summary>
    public IEnumerable<CelestialBody> Search(string text, int max = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return Enumerable.Empty<CelestialBody>();
        text = text.Trim();
        return AllBodies
            .Where(b => b.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || b.Id == text)
            .OrderBy(b => b.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(b => b.Kind)
            .Take(max);
    }

    /// <summary>Recomputes all positions for the given TT Julian date.</summary>
    public void Update(double jdTT)
    {
        JulianDateTT = jdTT;
        foreach (var b in _majorBodies) b.UpdatePosition(jdTT);
        // Small bodies are independent and all orbit the Sun, so compute them in parallel.
        if (_smallBodies.Count > 2000)
            Parallel.For(0, _smallBodies.Count, i => _smallBodies[i].UpdatePosition(jdTT));
        else
            foreach (var b in _smallBodies) b.UpdatePosition(jdTT);
    }
}
