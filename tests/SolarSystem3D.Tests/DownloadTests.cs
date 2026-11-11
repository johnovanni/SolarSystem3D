using SolarSystem3D.Core.Data;

namespace SolarSystem3D.Tests;

/// <summary>Live test of the JPL Small-Body Database download path (needs internet access).</summary>
[Trait("Category", "Network")]
public class DownloadTests
{
    [Fact]
    public async Task DownloadsAndLoadsSmallBodyCatalogs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SolarSystem3D-download-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CatalogStore(dir);
            using (var downloader = new CatalogDownloader(store))
            {
                int asteroids = await downloader.DownloadAsteroidsAsync(1000, 100);
                int comets = await downloader.DownloadCometsAsync();
                Assert.True(asteroids >= 1000);
                Assert.True(comets > 3000);
            }
            Assert.True(store.HasDownloadedCopy(CatalogStore.AsteroidsFile));
            var system = new SolarSystemFactory(store).Create(out var report);
            Assert.True(report.AsteroidsFromCache && report.CometsFromCache);
            Assert.Equal(9, system.DwarfPlanets.Count());
            Assert.True(system.Asteroids.Count() >= 900);
            Assert.NotNull(system.Find("1P/Halley"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
