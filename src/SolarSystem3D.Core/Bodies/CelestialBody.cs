using SolarSystem3D.Core.Astronomy;
using SolarSystem3D.Core.Orbits;

namespace SolarSystem3D.Core.Bodies;

public enum BodyKind { Star, Planet, DwarfPlanet, Moon, Asteroid, Comet }

/// <summary>Base class for every object in the simulation.</summary>
public abstract class CelestialBody
{
    private readonly List<CelestialBody> _satellites = new();

    protected CelestialBody(string id, string name, double radiusKm, CelestialBody? parent, IOrbitModel? orbit)
    {
        Id = id;
        Name = name;
        RadiusKm = radiusKm;
        Parent = parent;
        Orbit = orbit;
        parent?._satellites.Add(this);
    }

    /// <summary>Stable identifier (JPL SPK-ID or NAIF code where available).</summary>
    public string Id { get; }
    public string Name { get; }
    public abstract BodyKind Kind { get; }
    public double RadiusKm { get; }
    public CelestialBody? Parent { get; }
    public IOrbitModel? Orbit { get; }
    public IReadOnlyList<CelestialBody> Satellites => _satellites;

    /// <summary>Suggested display colour as 0xAARRGGBB.</summary>
    public uint ColorArgb { get; init; } = 0xFFCCCCCC;

    /// <summary>Heliocentric position (AU, J2000 ecliptic) at the last <see cref="UpdatePosition"/>.</summary>
    public Vector3d Position { get; private set; }

    /// <summary>Depth in the parent chain (Sun = 0, planets = 1, moons = 2).</summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>Recomputes <see cref="Position"/>. The parent must already be updated.</summary>
    public void UpdatePosition(double jdTT)
    {
        var parentPos = Parent?.Position ?? Vector3d.Zero;
        Position = Orbit is null ? parentPos : parentPos + Orbit.PositionAt(jdTT);
    }

    /// <summary>Position relative to the parent computed directly (no caching).</summary>
    public Vector3d RelativePositionAt(double jdTT) => Orbit?.PositionAt(jdTT) ?? Vector3d.Zero;

    /// <summary>Text shown in the "type" line of the information panel.</summary>
    public virtual string TypeDescription => Kind.ToString();

    /// <summary>Extra kind-specific facts for the information panel.</summary>
    public virtual IEnumerable<(string Label, string Value)> GetDetails()
    {
        yield break;
    }

    public override string ToString() => Name;
}
