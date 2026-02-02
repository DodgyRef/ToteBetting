using System.Text.Json;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;

namespace ExactaBetting.App.Services;

/// <summary>
/// Sample implementation that loads race data from bundled JSON.
/// Prefers GetTRIFECTAProducts (WIN + EXACTA + TRIFECTA), falls back to GetEXACTAProducts.json.
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

    public void InvalidateCache()
    {
        _cache = null;
    }

    public async Task<IReadOnlyList<AvailableRace>> GetAvailableRacesAsync(CancellationToken cancellationToken = default)
    {
        var races = await LoadRacesAsync(cancellationToken);
        return races.Keys.OrderBy(k => k).Select(k =>
        {
            var race = races[k];
            var poolText = FormatPoolDisplay(race);
            return new AvailableRace { BaseName = k, DisplayName = $"{k}{poolText}".Trim(), CountryCode = "" };
        }).ToList();
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

            var (exactaByRace, winByRace, winPoolByRace, trifectaByRace) = await LoadProductsAsync(cancellationToken);
            var result = new Dictionary<string, RaceData>();
            var allRaceNames = exactaByRace.Keys.Union(trifectaByRace.Keys);

            foreach (var raceName in allRaceNames)
            {
                exactaByRace.TryGetValue(raceName, out var exacta);
                trifectaByRace.TryGetValue(raceName, out var trifecta);

                var hasExacta = (exacta.HorseNames?.Count ?? 0) > 0 && (exacta.HasLines || exacta.PoolNetAmount > 0);
                var hasTrifecta = (trifecta.HorseNames?.Count ?? 0) > 0 && (trifecta.HasLines || trifecta.PoolNetAmount > 0);
                if (!hasExacta && !hasTrifecta)
                    continue;

                var horseNames = (hasExacta ? exacta.HorseNames : null) ?? (hasTrifecta ? trifecta.HorseNames : null) ?? new Dictionary<int, string>();
                var winOdds = winByRace.GetValueOrDefault(raceName);
                if (winOdds is null || winOdds.Count == 0)
                    winOdds = DeriveSyntheticWinOdds(horseNames.Count);

                var productId = hasExacta ? exacta.ProductId : trifecta.ProductId;
                var winPool = winPoolByRace.GetValueOrDefault(raceName);
                var emptyOdds = (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>();
                result[raceName] = new RaceData
                {
                    RaceName = raceName,
                    EventId = productId,
                    WinOdds = winOdds,
                    HorseNames = horseNames,
                    ExactaOdds = (hasExacta ? exacta.ExactaOdds : null) ?? emptyOdds,
                    TrifectaOdds = (hasTrifecta ? trifecta.TrifectaOdds : null) ?? emptyOdds,
                    PoolGrossAmount = hasExacta ? exacta.PoolGrossAmount : 0m,
                    PoolNetAmount = hasExacta ? exacta.PoolNetAmount : 0m,
                    CarryInNetAmount = hasExacta ? exacta.CarryInNetAmount : 0m,
                    GuaranteeNetAmount = hasExacta ? exacta.GuaranteeNetAmount : 0m,
                    TopUpNetAmount = hasExacta ? exacta.TopUpNetAmount : 0m,
                    TrifectaPoolGrossAmount = hasTrifecta ? trifecta.PoolGrossAmount : 0m,
                    TrifectaPoolNetAmount = hasTrifecta ? trifecta.PoolNetAmount : 0m,
                    TrifectaCarryInNetAmount = hasTrifecta ? trifecta.CarryInNetAmount : 0m,
                    TrifectaGuaranteeNetAmount = hasTrifecta ? trifecta.GuaranteeNetAmount : 0m,
                    TrifectaTopUpNetAmount = hasTrifecta ? trifecta.TopUpNetAmount : 0m,
                    WinPoolGrossAmount = winPool.Gross,
                    WinPoolNetAmount = winPool.Net,
                    WinCarryInNetAmount = winPool.CarryIn,
                    WinGuaranteeNetAmount = winPool.Guarantee,
                    WinTopUpNetAmount = winPool.TopUp
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

    private static string FormatPoolDisplay(RaceData race)
    {
        var s = $"  Exacta Pool £{race.PoolNetAmount:N0}  Carry-in £{race.CarryInNetAmount:N0}  Guarantee £{race.GuaranteeNetAmount:N0}  Top-up £{race.TopUpNetAmount:N0}";
        if (race.TrifectaPoolNetAmount > 0)
            s += $"  Trifecta Pool £{race.TrifectaPoolNetAmount:N0}  Trif. Carry-in £{race.TrifectaCarryInNetAmount:N0}";
        return s;
    }

    private static IReadOnlyDictionary<int, decimal> DeriveSyntheticWinOdds(int horseCount)
    {
        var d = new Dictionary<int, decimal>();
        for (var i = 1; i <= horseCount; i++)
            d[i] = 2m + i * 1.5m;
        return d;
    }

    /// <summary>
    /// Loads WIN, EXACTA and TRIFECTA products. Prefers GetTRIFECTAProducts, then GetEXACTAProducts.json.
    /// Uses WIN product lines for individual horse odds. TRIFECTA line ids are parsed as first-second-third.
    /// </summary>
    private async Task<(Dictionary<string, (string ProductId, IReadOnlyDictionary<int, string> HorseNames, IReadOnlyDictionary<string, decimal> ExactaOdds, decimal PoolGrossAmount, decimal PoolNetAmount, decimal CarryInNetAmount, decimal GuaranteeNetAmount, decimal TopUpNetAmount, bool HasLines)> ExactaByRace, Dictionary<string, IReadOnlyDictionary<int, decimal>> WinByRace, Dictionary<string, (decimal Gross, decimal Net, decimal CarryIn, decimal Guarantee, decimal TopUp)> WinPoolByRace, Dictionary<string, (string ProductId, IReadOnlyDictionary<int, string> HorseNames, IReadOnlyDictionary<string, decimal> TrifectaOdds, decimal PoolGrossAmount, decimal PoolNetAmount, decimal CarryInNetAmount, decimal GuaranteeNetAmount, decimal TopUpNetAmount, bool HasLines)> TrifectaByRace)> LoadProductsAsync(CancellationToken ct)
    {
        Stream stream;
        try
        {
            stream = await OpenJsonAsync("GetTRIFECTAProducts", ct);
        }
        catch (FileNotFoundException)
        {
            stream = await OpenJsonAsync("GetEXACTAProducts.json", ct);
        }
        await using (stream)
        {
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var products = root.GetProperty("data").GetProperty("products").GetProperty("nodes");

            var exactaByRace = new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>();
            var winByRace = new Dictionary<string, IReadOnlyDictionary<int, decimal>>();
            var winPoolByRace = new Dictionary<string, (decimal Gross, decimal Net, decimal CarryIn, decimal Guarantee, decimal TopUp)>();
            var trifectaByRace = new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>();
            var oddsType = "Base";

            foreach (var product in products.EnumerateArray())
            {
                var name = product.GetProperty("name").GetString() ?? "";
                var betTypeCode = product.GetProperty("type").GetProperty("betType").GetProperty("code").GetString() ?? "";

                if (betTypeCode == "WIN")
                {
                    var raceName = name.Replace(" - WIN", "");
                    var type = product.GetProperty("type");
                    var poolNode = type.GetProperty("pool");
                    var gross = GetDecimalFromPoolPath(poolNode, "total", "grossAmount");
                    var net = GetDecimalFromPoolPath(poolNode, "total", "netAmount");
                    var carryIn = GetDecimalFromPoolPath(poolNode, "carryIn", "netAmount");
                    var guarantee = GetDecimalFromPoolPath(poolNode, "guarantee", "netAmount");
                    var topUp = GetDecimalFromPoolPath(poolNode, "guarantee", "topUpNetAmount");
                    winPoolByRace[raceName] = (gross, net, carryIn, guarantee, topUp);
                    var lines = type.GetProperty("lines").GetProperty("nodes");
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
                    var poolNode = type.GetProperty("pool");
                    var poolGross = GetDecimalFromPoolPath(poolNode, "total", "grossAmount");
                    var poolNet = GetDecimalFromPoolPath(poolNode, "total", "netAmount");
                    var carryIn = GetDecimalFromPoolPath(poolNode, "carryIn", "netAmount");
                    var guarantee = GetDecimalFromPoolPath(poolNode, "guarantee", "netAmount");
                    var topUp = GetDecimalFromPoolPath(poolNode, "guarantee", "topUpNetAmount");
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
                    exactaByRace[raceName] = (productId, horseNames, exactaOdds, poolGross, poolNet, carryIn, guarantee, topUp, hasLines);
                }
                else if (betTypeCode == "TRIFECTA")
                {
                    var raceName = name.Replace(" - TRIFECTA", "");
                    var productId = product.GetProperty("id").GetString() ?? "";
                    var type = product.GetProperty("type");
                    var poolNode = type.GetProperty("pool");
                    var poolGross = GetDecimalFromPoolPath(poolNode, "total", "grossAmount");
                    var poolNet = GetDecimalFromPoolPath(poolNode, "total", "netAmount");
                    var carryIn = GetDecimalFromPoolPath(poolNode, "carryIn", "netAmount");
                    var guarantee = GetDecimalFromPoolPath(poolNode, "guarantee", "netAmount");
                    var topUp = GetDecimalFromPoolPath(poolNode, "guarantee", "topUpNetAmount");
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

                    var trifectaOdds = new Dictionary<string, decimal>();
                    foreach (var line in linesNode.EnumerateArray())
                    {
                        var id = line.GetProperty("id").GetString() ?? "";
                        var parts = id.Split('-');
                        if (parts.Length < 3)
                            continue;
                        var first = parts[^3];
                        var second = parts[^2];
                        var third = parts[^1];
                        var key = $"{first}-{second}-{third}";

                        foreach (var odd in line.GetProperty("odds").EnumerateArray())
                        {
                            if (odd.GetProperty("name").GetString() == oddsType)
                            {
                                trifectaOdds[key] = odd.GetProperty("decimal").GetDecimal();
                                break;
                            }
                        }
                    }

                    var hasLines = trifectaOdds.Count > 0;
                    trifectaByRace[raceName] = (productId, horseNames, trifectaOdds, poolGross, poolNet, carryIn, guarantee, topUp, hasLines);
                }
            }

            return (exactaByRace, winByRace, winPoolByRace, trifectaByRace);
        }
    }

    private static decimal GetDecimalFromPoolPath(JsonElement poolNode, string parent, string amountKey)
    {
        try
        {
            var el = poolNode.GetProperty(parent).GetProperty(amountKey).GetProperty("decimalAmount");
            return el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : 0m;
        }
        catch
        {
            return 0m;
        }
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
