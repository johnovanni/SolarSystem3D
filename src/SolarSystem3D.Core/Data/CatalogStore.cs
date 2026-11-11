using System.Reflection;

namespace SolarSystem3D.Core.Data;

/// <summary>
/// Locates catalog files: a downloaded copy in %LOCALAPPDATA%\SolarSystem3D takes precedence over
/// the snapshot embedded in the assembly (so the program always works offline).
/// </summary>
public sealed class CatalogStore
{
    public const string SatellitesFile = "satellites.tsv";
    public const string AsteroidsFile = "asteroids.tsv";
    public const string CometsFile = "comets.tsv";

    public CatalogStore(string? cacheDirectory = null)
    {
        CacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SolarSystem3D");
    }

    public string CacheDirectory { get; }

    public string CachePath(string fileName) => Path.Combine(CacheDirectory, fileName);

    public bool HasDownloadedCopy(string fileName) => File.Exists(CachePath(fileName));

    public DateTime? DownloadedAtUtc(string fileName) =>
        HasDownloadedCopy(fileName) ? File.GetLastWriteTimeUtc(CachePath(fileName)) : null;

    /// <summary>Opens the downloaded copy if present, otherwise the embedded snapshot.</summary>
    public TextReader Open(string fileName, out bool fromCache)
    {
        string path = CachePath(fileName);
        if (File.Exists(path))
        {
            fromCache = true;
            return new StreamReader(path);
        }
        fromCache = false;
        return OpenEmbedded(fileName);
    }

    public static TextReader OpenEmbedded(string fileName)
    {
        var asm = typeof(CatalogStore).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
        return new StreamReader(asm.GetManifestResourceStream(resource)!);
    }

    /// <summary>Writes a file atomically into the cache directory.</summary>
    public async Task SaveAsync(string fileName, string content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDirectory);
        string path = CachePath(fileName);
        string tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Deletes downloaded copies so the embedded snapshot is used again.</summary>
    public void ClearCache()
    {
        foreach (var f in new[] { SatellitesFile, AsteroidsFile, CometsFile })
            if (File.Exists(CachePath(f))) File.Delete(CachePath(f));
    }
}
