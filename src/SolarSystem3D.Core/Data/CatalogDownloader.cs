using System.Text;
using System.Text.Json;

namespace SolarSystem3D.Core.Data;

/// <summary>
/// Downloads current asteroid and comet orbital elements from the JPL Small-Body Database Query API
/// (ssd-api.jpl.nasa.gov/sbdb_query.api) and stores them in the <see cref="CatalogStore"/> cache.
/// </summary>
/// <remarks>
/// Planetary satellites are not downloaded at run time: the built-in satellite catalog is JPL's mean-element
/// table with each moon's phase and rate calibrated against JPL Horizons, which a raw download would lose.
/// </remarks>
public sealed class CatalogDownloader : IDisposable
{
    private const string SbdbQuery = "https://ssd-api.jpl.nasa.gov/sbdb_query.api";

    private readonly HttpClient _http;
    private readonly CatalogStore _store;

    public CatalogDownloader(CatalogStore store)
    {
        _store = store;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SolarSystem3D/1.0");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Downloads the <paramref name="count"/> brightest numbered asteroids plus the brightest near-Earth asteroids.</summary>
    public async Task<int> DownloadAsteroidsAsync(int count, int nearEarthCount, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        const string fields = "spkid,full_name,a,e,i,om,w,ma,epoch,H,diameter,class";
        progress?.Report($"Downloading {count:N0} asteroids from JPL SBDB…");
        var main = await QuerySbdbAsync($"{SbdbQuery}?fields={fields}&sb-kind=a&sb-ns=n&sort=H&limit={count}", ct).ConfigureAwait(false);
        progress?.Report($"Downloading {nearEarthCount:N0} near-Earth asteroids…");
        var neo = await QuerySbdbAsync($"{SbdbQuery}?fields={fields}&sb-kind=a&sb-group=neo&sort=H&limit={nearEarthCount}", ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append("spkid\tname\ta\te\ti\tom\tw\tma\tepoch\tH\tdiameter\tclass\n");
        int n = AppendRows(sb, main.Concat(neo));
        await _store.SaveAsync(CatalogStore.AsteroidsFile, sb.ToString(), ct).ConfigureAwait(false);
        return n;
    }

    /// <summary>Downloads every comet in the SBDB.</summary>
    public async Task<int> DownloadCometsAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report("Downloading comets from JPL SBDB…");
        var rows = await QuerySbdbAsync($"{SbdbQuery}?fields=spkid,full_name,q,e,i,om,w,tp,epoch,class,diameter&sb-kind=c", ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.Append("spkid\tname\tq\te\ti\tom\tw\ttp\tepoch\tclass\tdiameter\n");
        int n = AppendRows(sb, rows);
        await _store.SaveAsync(CatalogStore.CometsFile, sb.ToString(), ct).ConfigureAwait(false);
        return n;
    }

    private async Task<List<JsonElement>> QuerySbdbAsync(string url, CancellationToken ct)
    {
        await using var stream = await _http.GetStreamAsync(url, ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            throw new InvalidDataException("Unexpected response from the JPL SBDB API.");
        return data.EnumerateArray().Select(r => r.Clone()).ToList();
    }

    private static int AppendRows(StringBuilder sb, IEnumerable<JsonElement> rows)
    {
        int n = 0;
        foreach (var row in rows)
        {
            bool first = true;
            foreach (var cell in row.EnumerateArray())
            {
                if (!first) sb.Append('\t');
                first = false;
                if (cell.ValueKind == JsonValueKind.String) sb.Append(cell.GetString()!.Trim().Replace('\t', ' '));
                else if (cell.ValueKind == JsonValueKind.Number) sb.Append(cell.GetRawText());
            }
            sb.Append('\n');
            n++;
        }
        return n;
    }
}
