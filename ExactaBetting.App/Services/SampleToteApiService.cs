using System.Text.Json;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;

namespace ExactaBetting.App.Services;

/// <summary>
/// Sample implementation that loads race data from bundled JSON.
/// Uses GetEXACTAProducts.json which contains both WIN and EXACTA products.
/// Replace with your GraphQL subscription client for live data.
/// </summary>
public sealed class SampleToteApiService : IToteApiService
{
    private Dictionary<string, RaceData>? _cache;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public async Task<RaceData?> GetRaceDataAsync(string raceName, CancellationToken cancellationToken = default)
    {
        var races = await LoadRacesAsync(cancellationToken);
        return races.GetValueOrDefault(raceName);
    }

    public async Task<IReadOnlyList<string>> GetAvailableRacesAsync(CancellationToken cancellationToken = default)
    {
        var races = await LoadRacesAsync(cancellationToken);
        return races.Keys.OrderBy(k => k).ToList();
    }

    private async Task<IReadOnlyDictionary<string, RaceData>> LoadRacesAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
            return _cache;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null)
                return _cache;

            var (exactaByRace, winByRace) = await LoadProductsAsync(cancellationToken);
            var result = new Dictionary<string, RaceData>();

            foreach (var (raceName, exacta) in exactaByRace)
            {
                if (!exacta.HasLines || exacta.PoolNetAmount <= 0)
                    continue;

                var winOdds = winByRace.GetValueOrDefault(raceName);
                if (winOdds is null || winOdds.Count == 0)
                    winOdds = DeriveSyntheticWinOdds(exacta.HorseNames.Count);

                result[raceName] = new RaceData
                {
                    RaceName = raceName,
                    EventId = exacta.ProductId,
                    WinOdds = winOdds,
                    HorseNames = exacta.HorseNames,
                    ExactaOdds = exacta.ExactaOdds,
                    PoolNetAmount = exacta.PoolNetAmount
                };
            }

            _cache = result;
            return result;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static IReadOnlyDictionary<int, decimal> DeriveSyntheticWinOdds(int horseCount)
    {
        var d = new Dictionary<int, decimal>();
        for (var i = 1; i <= horseCount; i++)
            d[i] = 2m + i * 1.5m;
        return d;
    }

    /// <summary>
    /// Loads both WIN and EXACTA products from the single GetEXACTAProducts.json file.
    /// Uses WIN product lines for individual horse odds.
    /// </summary>
    private async Task<(Dictionary<string, (string ProductId, IReadOnlyDictionary<int, string> HorseNames, IReadOnlyDictionary<string, decimal> ExactaOdds, decimal PoolNetAmount, bool HasLines)> ExactaByRace, Dictionary<string, IReadOnlyDictionary<int, decimal>> WinByRace)> LoadProductsAsync(CancellationToken ct)
    {
        await using var stream = await OpenJsonAsync("GetEXACTAProducts.json", ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var products = root.GetProperty("data").GetProperty("products").GetProperty("nodes");

        var exactaByRace = new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, bool)>();
        var winByRace = new Dictionary<string, IReadOnlyDictionary<int, decimal>>();
        var oddsType = "Base";

        foreach (var product in products.EnumerateArray())
        {
            var name = product.GetProperty("name").GetString() ?? "";
            var betTypeCode = product.GetProperty("type").GetProperty("betType").GetProperty("code").GetString() ?? "";

            if (betTypeCode == "WIN")
            {
                var raceName = name.Replace(" - WIN", "");
                var lines = product.GetProperty("type").GetProperty("lines").GetProperty("nodes");
                var winOdds = new Dictionary<int, decimal>();
                var idx = 1;
                foreach (var line in lines.EnumerateArray())
                {
                    foreach (var odd in line.GetProperty("odds").EnumerateArray())
                    {
                        if (odd.GetProperty("name").GetString() == oddsType)
                        {
                            winOdds[idx] = odd.GetProperty("decimal").GetDecimal();
                            break;
                        }
                    }
                    idx++;
                }
                if (winOdds.Count > 0)
                    winByRace[raceName] = winOdds;
            }
            else if (betTypeCode == "EXACTA")
            {
                var raceName = name.Replace(" - EXACTA", "");
                var productId = product.GetProperty("id").GetString() ?? "";
                var type = product.GetProperty("type");
                var pool = type.GetProperty("pool").GetProperty("total").GetProperty("netAmount").GetProperty("decimalAmount");
                var poolNet = pool.ValueKind == JsonValueKind.Number ? pool.GetDecimal() : 0m;
                var linesNode = type.GetProperty("lines").GetProperty("nodes");
                var legsNode = type.GetProperty("legs").GetProperty("nodes");

                var horseNames = new Dictionary<int, string>();
                if (legsNode.GetArrayLength() > 0)
                {
                    var selections = legsNode[0].GetProperty("selections").GetProperty("nodes");
                    var idx = 1;
                    foreach (var sel in selections.EnumerateArray())
                    {
                        horseNames[idx++] = sel.GetProperty("name").GetString() ?? $"#{idx - 1}";
                    }
                }

                var exactaOdds = new Dictionary<string, decimal>();
                foreach (var line in linesNode.EnumerateArray())
                {
                    var id = line.GetProperty("id").GetString() ?? "";
                    var parts = id.Split('-');
                    if (parts.Length < 2)
                        continue;
                    var first = parts[^2];
                    var second = parts[^1];
                    var key = $"{first}-{second}";

                    foreach (var odd in line.GetProperty("odds").EnumerateArray())
                    {
                        if (odd.GetProperty("name").GetString() == oddsType)
                        {
                            exactaOdds[key] = odd.GetProperty("decimal").GetDecimal();
                            break;
                        }
                    }
                }

                var hasLines = exactaOdds.Count > 0;
                exactaByRace[raceName] = (productId, horseNames, exactaOdds, poolNet, hasLines);
            }
        }

        return (exactaByRace, winByRace);
    }

    private static async Task<Stream> OpenJsonAsync(string fileName, CancellationToken ct)
    {
        try
        {
            return await FileSystem.OpenAppPackageFileAsync(fileName);
        }
        catch
        {
            var basePath = AppContext.BaseDirectory;
            var path = Path.Combine(basePath, fileName);
            if (File.Exists(path))
                return File.OpenRead(path);
            path = Path.Combine(basePath, "..", "..", "..", "..", fileName);
            if (File.Exists(path))
                return File.OpenRead(path);
            path = Path.Combine(basePath, "..", "..", "..", "..", "Example Responses", fileName);
            if (File.Exists(path))
                return File.OpenRead(path);
            throw new FileNotFoundException($"Could not find {fileName}. Ensure it is bundled or in the application directory.");
        }
    }
}
