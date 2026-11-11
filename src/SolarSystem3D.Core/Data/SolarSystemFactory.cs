using SolarSystem3D.Core.Bodies;

namespace SolarSystem3D.Core.Data;

/// <summary>Summary of what was loaded, for the status bar.</summary>
public sealed record CatalogLoadReport(int Moons, int Asteroids, int DwarfPlanets, int Comets, bool SatellitesFromCache,
                                       bool AsteroidsFromCache, bool CometsFromCache)
{
    public override string ToString() =>
        $"{Moons:N0} moons{(SatellitesFromCache ? "" : "*")}, {DwarfPlanets + 1} dwarf planets, " +
        $"{Asteroids:N0} asteroids{(AsteroidsFromCache ? "" : "*")}, {Comets:N0} comets{(CometsFromCache ? "" : "*")}" +
        (SatellitesFromCache && AsteroidsFromCache && CometsFromCache ? "" : "   (* built-in snapshot)");
}

/// <summary>Builds a complete <see cref="SolarSystem"/> from the built-in data and the catalogs.</summary>
public sealed class SolarSystemFactory
{
    private readonly CatalogStore _store;

    public SolarSystemFactory(CatalogStore store) => _store = store;

    public SolarSystem Create(out CatalogLoadReport report)
    {
        var system = MajorBodyCatalog.CreateSkeleton();

        List<Asteroid> asteroids;
        List<DwarfPlanet> dwarfs;
        bool astCache;
        using (var r = _store.Open(CatalogStore.AsteroidsFile, out astCache))
            (asteroids, dwarfs) = CatalogReader.ReadAsteroids(r);

        // If a downloaded asteroid list somehow lacks the dwarf planets, take them from the snapshot.
        if (dwarfs.Count < MajorBodyCatalog.DwarfPlanets.Count)
        {
            using var r = CatalogStore.OpenEmbedded(CatalogStore.AsteroidsFile);
            var (_, embeddedDwarfs) = CatalogReader.ReadAsteroids(r);
            foreach (var d in embeddedDwarfs.Where(d => dwarfs.All(x => x.Id != d.Id))) dwarfs.Add(d);
        }
        system.AddRange(dwarfs);

        List<NaturalSatellite> moons;
        bool satCache;
        using (var r = _store.Open(CatalogStore.SatellitesFile, out satCache))
            moons = CatalogReader.ReadSatellites(r, system);
        system.AddRange(moons);

        List<Comet> comets;
        bool cometCache;
        using (var r = _store.Open(CatalogStore.CometsFile, out cometCache))
            comets = CatalogReader.ReadComets(r);

        system.ReplaceSmallBodies(asteroids.Cast<CelestialBody>().Concat(comets));

        report = new CatalogLoadReport(moons.Count + 1, asteroids.Count, dwarfs.Count, comets.Count, satCache, astCache, cometCache);
        return system;
    }

    /// <summary>Reloads only asteroids and comets into an existing system (after a download).</summary>
    public (int Asteroids, int Comets) ReloadSmallBodies(SolarSystem system)
    {
        List<Asteroid> asteroids;
        using (var r = _store.Open(CatalogStore.AsteroidsFile, out _))
            (asteroids, _) = CatalogReader.ReadAsteroids(r);
        List<Comet> comets;
        using (var r = _store.Open(CatalogStore.CometsFile, out _))
            comets = CatalogReader.ReadComets(r);
        system.ReplaceSmallBodies(asteroids.Cast<CelestialBody>().Concat(comets));
        return (asteroids.Count, comets.Count);
    }
}
