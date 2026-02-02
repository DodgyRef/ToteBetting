using System.Text.Json.Serialization;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace ExactaBetting.App.Services;

/// <summary>
/// Fetches race data from the Tote GraphQL API using GraphQL.Client (same as working Tote.slnx)
/// so the request format and headers match exactly.
/// </summary>
public sealed class ToteGraphQLApiService : IToteApiService
{
    public const string ProductionEndpoint = "https://hub.production.racing.tote.co.uk/partner/gateway/graphql";
    public const string TestEndpoint = "https://playground.edge.tote.digital/partner/gateway/test/graphql/";
    public const string ApiKeyScheme = "Api-Key";
    /// <summary>API key — must match the value stored in the working Tote app (SecureStorage key / API key: a149b1adacad4436bd30d9adb91cc16d).</summary>
    public const string ApiKeyValue = "a149b1adacad4436bd30d9adb91cc16d";
    public static readonly string HttpClientName = "ToteGraphQL";

    private readonly GraphQLHttpClient _graphqlClient;
    private Dictionary<string, RaceData>? _cache;
    private Dictionary<string, (string TimeDisplay, string CountryCode)>? _eventInfoByRaceName;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ToteGraphQLApiService(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var options = new GraphQLHttpClientOptions { EndPoint = new Uri(ProductionEndpoint) };
        _graphqlClient = new GraphQLHttpClient(options, new SystemTextJsonSerializer(), httpClient);
    }

    public async Task<RaceData?> GetRaceDataAsync(string raceName, CancellationToken cancellationToken = default)
    {
        var races = await LoadRacesAsync(cancellationToken);
        return races.GetValueOrDefault(raceName);
    }

    public void InvalidateCache()
    {
        _cache = null;
        _eventInfoByRaceName = null;
    }

    public async Task<IReadOnlyList<AvailableRace>> GetAvailableRacesAsync(CancellationToken cancellationToken = default)
    {
        var races = await LoadRacesAsync(cancellationToken);
        var eventInfo = _eventInfoByRaceName ?? new Dictionary<string, (string TimeDisplay, string CountryCode)>();
        var list = new List<AvailableRace>();
        foreach (var baseName in races.Keys.OrderBy(k => k))
        {
            var (timeDisplay, countryCode) = eventInfo.GetValueOrDefault(baseName, ("", ""));
            var race = races[baseName];
            var poolText = FormatPoolDisplay(race);
            var displayName = string.IsNullOrEmpty(timeDisplay)
                ? $"{baseName}{poolText}"
                : $"{baseName} {timeDisplay}{poolText}";
            list.Add(new AvailableRace { BaseName = baseName, DisplayName = displayName.Trim(), CountryCode = countryCode });
        }
        return list;
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
            _eventInfoByRaceName = await LoadEventsAsync(cancellationToken);
            var result = new Dictionary<string, RaceData>();

            foreach (var (raceName, exacta) in exactaByRace)
            {
                if (!exacta.HasLines || exacta.PoolNetAmount <= 0)
                    continue;

                var winOdds = winByRace.GetValueOrDefault(raceName);
                if (winOdds is null || winOdds.Count == 0)
                    winOdds = DeriveSyntheticWinOdds(exacta.HorseNames.Count);

                var trifecta = trifectaByRace.GetValueOrDefault(raceName);
                var winPool = winPoolByRace.GetValueOrDefault(raceName);

                result[raceName] = new RaceData
                {
                    RaceName = raceName,
                    EventId = exacta.ProductId,
                    WinOdds = winOdds,
                    HorseNames = exacta.HorseNames,
                    ExactaOdds = exacta.ExactaOdds,
                    TrifectaOdds = trifecta.TrifectaOdds ?? EmptyTrifectaOdds,
                    PoolGrossAmount = exacta.PoolGrossAmount,
                    PoolNetAmount = exacta.PoolNetAmount,
                    CarryInNetAmount = exacta.CarryInNetAmount,
                    GuaranteeNetAmount = exacta.GuaranteeNetAmount,
                    TopUpNetAmount = exacta.TopUpNetAmount,
                    TrifectaPoolGrossAmount = trifectaByRace.ContainsKey(raceName) ? trifecta.PoolGrossAmount : 0m,
                    TrifectaPoolNetAmount = trifectaByRace.ContainsKey(raceName) ? trifecta.PoolNetAmount : 0m,
                    TrifectaCarryInNetAmount = trifectaByRace.ContainsKey(raceName) ? trifecta.CarryInNetAmount : 0m,
                    TrifectaGuaranteeNetAmount = trifectaByRace.ContainsKey(raceName) ? trifecta.GuaranteeNetAmount : 0m,
                    TrifectaTopUpNetAmount = trifectaByRace.ContainsKey(raceName) ? trifecta.TopUpNetAmount : 0m,
                    WinPoolGrossAmount = winPool.Gross,
                    WinPoolNetAmount = winPool.Net,
                    WinCarryInNetAmount = winPool.CarryIn,
                    WinGuaranteeNetAmount = winPool.Guarantee,
                    WinTopUpNetAmount = winPool.TopUp
                };
            }

            foreach (var (raceName, trifecta) in trifectaByRace)
            {
                if (result.ContainsKey(raceName))
                    continue;
                if (!trifecta.HasLines || trifecta.PoolNetAmount <= 0)
                    continue;

                var winOdds = winByRace.GetValueOrDefault(raceName);
                if (winOdds is null || winOdds.Count == 0)
                    winOdds = DeriveSyntheticWinOdds(trifecta.HorseNames.Count);

                var winPool = winPoolByRace.GetValueOrDefault(raceName);
                result[raceName] = new RaceData
                {
                    RaceName = raceName,
                    EventId = trifecta.ProductId,
                    WinOdds = winOdds,
                    HorseNames = trifecta.HorseNames,
                    ExactaOdds = EmptyExactaOdds,
                    TrifectaOdds = trifecta.TrifectaOdds,
                    PoolGrossAmount = 0m,
                    PoolNetAmount = 0m,
                    CarryInNetAmount = 0m,
                    GuaranteeNetAmount = 0m,
                    TopUpNetAmount = 0m,
                    TrifectaPoolGrossAmount = trifecta.PoolGrossAmount,
                    TrifectaPoolNetAmount = trifecta.PoolNetAmount,
                    TrifectaCarryInNetAmount = trifecta.CarryInNetAmount,
                    TrifectaGuaranteeNetAmount = trifecta.GuaranteeNetAmount,
                    TrifectaTopUpNetAmount = trifecta.TopUpNetAmount,
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
        var parts = new List<string>();
        parts.Add($"Exacta Pool £{race.PoolNetAmount:N0}");
        parts.Add($"Carry-in £{race.CarryInNetAmount:N0}");
        parts.Add($"Guarantee £{race.GuaranteeNetAmount:N0}");
        parts.Add($"Top-up £{race.TopUpNetAmount:N0}");
        if (race.TrifectaPoolNetAmount > 0)
        {
            parts.Add($"Trifecta Pool £{race.TrifectaPoolNetAmount:N0}");
            parts.Add($"Trif. Carry-in £{race.TrifectaCarryInNetAmount:N0}");
        }
        return "  " + string.Join("  ", parts);
    }

    private static IReadOnlyDictionary<int, decimal> DeriveSyntheticWinOdds(int horseCount)
    {
        var d = new Dictionary<int, decimal>();
        for (var i = 1; i <= horseCount; i++)
            d[i] = 2m + i * 1.5m;
        return d;
    }

    private static readonly IReadOnlyDictionary<string, decimal> EmptyExactaOdds = new Dictionary<string, decimal>();
    private static readonly IReadOnlyDictionary<string, decimal> EmptyTrifectaOdds = new Dictionary<string, decimal>();

    private async Task<(Dictionary<string, (string ProductId, IReadOnlyDictionary<int, string> HorseNames, IReadOnlyDictionary<string, decimal> ExactaOdds, decimal PoolGrossAmount, decimal PoolNetAmount, decimal CarryInNetAmount, decimal GuaranteeNetAmount, decimal TopUpNetAmount, bool HasLines)> ExactaByRace, Dictionary<string, IReadOnlyDictionary<int, decimal>> WinByRace, Dictionary<string, (decimal Gross, decimal Net, decimal CarryIn, decimal Guarantee, decimal TopUp)> WinPoolByRace, Dictionary<string, (string ProductId, IReadOnlyDictionary<int, string> HorseNames, IReadOnlyDictionary<string, decimal> TrifectaOdds, decimal PoolGrossAmount, decimal PoolNetAmount, decimal CarryInNetAmount, decimal GuaranteeNetAmount, decimal TopUpNetAmount, bool HasLines)> TrifectaByRace)> LoadProductsAsync(CancellationToken ct)
    {
        var request = new GraphQLRequest { Query = GetRaceProductsQuery, Variables = null };
        var response = await _graphqlClient.SendQueryAsync<ProductsQueryData>(request, ct);

        if (response.Errors?.Length > 0)
            throw new InvalidOperationException($"GraphQL errors: {string.Join("; ", response.Errors.Select(e => e.Message))}");

        var products = response.Data?.Products?.Nodes;
        if (products is null || products.Count == 0)
            return (new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>(), new Dictionary<string, IReadOnlyDictionary<int, decimal>>(), new Dictionary<string, (decimal, decimal, decimal, decimal, decimal)>(), new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>());

        var exactaByRace = new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>();
        var winByRace = new Dictionary<string, IReadOnlyDictionary<int, decimal>>();
        var winPoolByRace = new Dictionary<string, (decimal Gross, decimal Net, decimal CarryIn, decimal Guarantee, decimal TopUp)>();
        var trifectaByRace = new Dictionary<string, (string, IReadOnlyDictionary<int, string>, IReadOnlyDictionary<string, decimal>, decimal, decimal, decimal, decimal, decimal, bool)>();
        const string oddsType = "Base";

        foreach (var product in products)
        {
            var name = product.Name ?? "";
            var betTypeCode = product.Type?.BetType?.Code ?? "";

            if (betTypeCode == "WIN")
            {
                var raceName = name.Replace(" - WIN", "");
                var pool = product.Type?.Pool;
                var gross = pool?.Total?.GrossAmount?.DecimalAmount ?? 0m;
                var net = pool?.Total?.NetAmount?.DecimalAmount ?? 0m;
                var carryIn = pool?.CarryIn?.NetAmount?.DecimalAmount ?? 0m;
                var guarantee = pool?.Guarantee?.NetAmount?.DecimalAmount ?? 0m;
                var topUp = pool?.Guarantee?.TopUpNetAmount?.DecimalAmount ?? 0m;
                winPoolByRace[raceName] = (gross, net, carryIn, guarantee, topUp);
                var lines = product.Type?.Lines?.Nodes ?? [];
                var winOdds = new Dictionary<int, decimal>();
                var idx = 1;
                foreach (var line in lines)
                {
                    var odd = line.Odds?.FirstOrDefault(o => o?.Name == oddsType);
                    if (odd != null)
                        winOdds[idx] = odd.Decimal;
                    idx++;
                }
                if (winOdds.Count > 0)
                    winByRace[raceName] = winOdds;
            }
            else if (betTypeCode == "EXACTA")
            {
                var raceName = name.Replace(" - EXACTA", "");
                var productId = product.Id ?? "";
                var pool = product.Type?.Pool;
                var poolGross = pool?.Total?.GrossAmount?.DecimalAmount ?? 0m;
                var poolNet = pool?.Total?.NetAmount?.DecimalAmount ?? 0m;
                var carryIn = pool?.CarryIn?.NetAmount?.DecimalAmount ?? 0m;
                var guarantee = pool?.Guarantee?.NetAmount?.DecimalAmount ?? 0m;
                var topUp = pool?.Guarantee?.TopUpNetAmount?.DecimalAmount ?? 0m;
                var linesNode = product.Type?.Lines?.Nodes ?? [];
                var legsNode = product.Type?.Legs?.Nodes ?? [];

                var horseNames = new Dictionary<int, string>();
                if (legsNode.Count > 0)
                {
                    var selections = legsNode[0].Selections?.Nodes ?? [];
                    var idx = 1;
                    foreach (var sel in selections)
                    {
                        horseNames[idx++] = sel?.Name ?? $"#{idx - 1}";
                    }
                }

                var exactaOdds = new Dictionary<string, decimal>();
                foreach (var line in linesNode)
                {
                    var id = line.Id ?? "";
                    var parts = id.Split('-');
                    if (parts.Length < 2) continue;
                    var first = parts[^2];
                    var second = parts[^1];
                    var key = $"{first}-{second}";
                    var odd = line.Odds?.FirstOrDefault(o => o?.Name == oddsType);
                    if (odd != null)
                        exactaOdds[key] = odd.Decimal;
                }

                var hasLines = exactaOdds.Count > 0;
                exactaByRace[raceName] = (productId, horseNames, exactaOdds, poolGross, poolNet, carryIn, guarantee, topUp, hasLines);
            }
            else if (betTypeCode == "TRIFECTA")
            {
                var raceName = name.Replace(" - TRIFECTA", "");
                var productId = product.Id ?? "";
                var pool = product.Type?.Pool;
                var poolGross = pool?.Total?.GrossAmount?.DecimalAmount ?? 0m;
                var poolNet = pool?.Total?.NetAmount?.DecimalAmount ?? 0m;
                var carryIn = pool?.CarryIn?.NetAmount?.DecimalAmount ?? 0m;
                var guarantee = pool?.Guarantee?.NetAmount?.DecimalAmount ?? 0m;
                var topUp = pool?.Guarantee?.TopUpNetAmount?.DecimalAmount ?? 0m;
                var linesNode = product.Type?.Lines?.Nodes ?? [];
                var legsNode = product.Type?.Legs?.Nodes ?? [];

                var horseNames = new Dictionary<int, string>();
                if (legsNode.Count > 0)
                {
                    var selections = legsNode[0].Selections?.Nodes ?? [];
                    var idx = 1;
                    foreach (var sel in selections)
                    {
                        horseNames[idx++] = sel?.Name ?? $"#{idx - 1}";
                    }
                }

                var trifectaOdds = new Dictionary<string, decimal>();
                foreach (var line in linesNode)
                {
                    var id = line.Id ?? "";
                    var parts = id.Split('-');
                    if (parts.Length < 3) continue;
                    var first = parts[^3];
                    var second = parts[^2];
                    var third = parts[^1];
                    var key = $"{first}-{second}-{third}";
                    var odd = line.Odds?.FirstOrDefault(o => o?.Name == oddsType);
                    if (odd != null)
                        trifectaOdds[key] = odd.Decimal;
                }

                var hasLines = trifectaOdds.Count > 0;
                trifectaByRace[raceName] = (productId, horseNames, trifectaOdds, poolGross, poolNet, carryIn, guarantee, topUp, hasLines);
            }
        }

        return (exactaByRace, winByRace, winPoolByRace, trifectaByRace);
    }

    private async Task<Dictionary<string, (string TimeDisplay, string CountryCode)>> LoadEventsAsync(CancellationToken ct)
    {
        var request = new GraphQLRequest { Query = GetUpcomingEventsQuery, Variables = null };
        var response = await _graphqlClient.SendQueryAsync<EventsQueryData>(request, ct);
        if (response.Errors?.Length > 0)
            return new Dictionary<string, (string, string)>();

        var nodes = response.Data?.Events?.Nodes;
        if (nodes is null || nodes.Count == 0)
            return new Dictionary<string, (string, string)>();

        var map = new Dictionary<string, (string TimeDisplay, string CountryCode)>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in nodes)
        {
            var name = evt.Name?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var timeDisplay = "";
            if (!string.IsNullOrEmpty(evt.ScheduledStartDateTime?.Iso8601)
                && DateTimeOffset.TryParse(evt.ScheduledStartDateTime.Iso8601, out var dt))
                timeDisplay = dt.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

            var countryCode = evt.Venue?.Country?.Alpha2Code?.Trim().ToUpperInvariant() ?? "";

            if (!map.ContainsKey(name))
                map[name] = (timeDisplay, countryCode);
        }
        return map;
    }

    private const string GetUpcomingEventsQuery = """
        query GetUpcomingEvents {
          events {
            nodes {
              id
              name
              scheduledStartDateTime { iso8601 }
              status
              venue {
                id
                name
                country { alpha2Code }
              }
            }
          }
        }
        """;

    private const string GetRaceProductsQuery = """
        query GetRaceProducts {
          products(betTypes: [WIN, EXACTA, TRIFECTA]) {
            pageInfo { hasNextPage }
            nodes {
              id
              name
              type {
                ... on BettingProduct {
                  betType { id code }
                  pool {
                    total { grossAmount { decimalAmount } netAmount { decimalAmount } }
                    carryIn { netAmount { decimalAmount } }
                    guarantee { netAmount { decimalAmount } topUpNetAmount { decimalAmount } }
                  }
                  selling { status }
                  result {
                    dividends {
                      nodes {
                        dividend { amount { decimalAmount } name status }
                      }
                    }
                  }
                  lines {
                    nodes {
                      id
                      odds { name decimal status }
                    }
                  }
                  legs {
                    nodes {
                      id
                      selections {
                        nodes { id name status totalUnits }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    #region Events response DTOs

    private sealed class EventsQueryData
    {
        [JsonPropertyName("events")]
        public EventsConnection? Events { get; set; }
    }

    private sealed class EventsConnection
    {
        [JsonPropertyName("nodes")]
        public List<EventNode>? Nodes { get; set; }
    }

    private sealed class EventNode
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("scheduledStartDateTime")]
        public ScheduledDateTimeNode? ScheduledStartDateTime { get; set; }

        [JsonPropertyName("venue")]
        public VenueNode? Venue { get; set; }
    }

    private sealed class ScheduledDateTimeNode
    {
        [JsonPropertyName("iso8601")]
        public string? Iso8601 { get; set; }
    }

    private sealed class VenueNode
    {
        [JsonPropertyName("country")]
        public CountryNode? Country { get; set; }
    }

    private sealed class CountryNode
    {
        [JsonPropertyName("alpha2Code")]
        public string? Alpha2Code { get; set; }
    }

    #endregion

    #region Response DTOs (match Tote API / GraphQL.Client serialization)

    private sealed class ProductsQueryData
    {
        [JsonPropertyName("products")]
        public ProductsConnection? Products { get; set; }
    }

    private sealed class ProductsConnection
    {
        [JsonPropertyName("nodes")]
        public List<ProductNode>? Nodes { get; set; }
    }

    private sealed class ProductNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public ProductTypeNode? Type { get; set; }
    }

    private sealed class ProductTypeNode
    {
        public BetTypeNode? BetType { get; set; }
        public PoolNode? Pool { get; set; }
        public LinesConnection? Lines { get; set; }
        public LegsConnection? Legs { get; set; }
    }

    private sealed class BetTypeNode
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
    }

    private sealed class PoolNode
    {
        public PoolTotalNode? Total { get; set; }
        [JsonPropertyName("carryIn")]
        public PoolTotalNode? CarryIn { get; set; }
        public PoolGuaranteeNode? Guarantee { get; set; }
    }

    private sealed class PoolTotalNode
    {
        [JsonPropertyName("grossAmount")]
        public MoneyNode? GrossAmount { get; set; }
        public MoneyNode? NetAmount { get; set; }
    }

    private sealed class PoolGuaranteeNode
    {
        public MoneyNode? NetAmount { get; set; }
        [JsonPropertyName("topUpNetAmount")]
        public MoneyNode? TopUpNetAmount { get; set; }
    }

    private sealed class MoneyNode
    {
        public decimal DecimalAmount { get; set; }
    }

    private sealed class LinesConnection
    {
        public List<LineNode>? Nodes { get; set; }
    }

    private sealed class LineNode
    {
        public string? Id { get; set; }
        public List<OddNode>? Odds { get; set; }
    }

    private sealed class OddNode
    {
        public string? Name { get; set; }
        [JsonPropertyName("decimal")]
        public decimal Decimal { get; set; }
        public string? Status { get; set; }
    }

    private sealed class LegsConnection
    {
        public List<LegNode>? Nodes { get; set; }
    }

    private sealed class LegNode
    {
        public string? Id { get; set; }
        public SelectionsConnection? Selections { get; set; }
    }

    private sealed class SelectionsConnection
    {
        public List<SelectionNode>? Nodes { get; set; }
    }

    private sealed class SelectionNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
    }

    #endregion
}
