using ExactaBetting.Core.Models;

namespace ExactaBetting.Core.Services;

/// <summary>
/// Orchestrates value bet analysis: fetches data, calculates value, filters, and ranks.
/// </summary>
public sealed class ValueBetService
{
    private readonly IToteApiService _toteApi;
    private readonly IValueCalculator _calculator;

    public ValueBetService(IToteApiService toteApi, IValueCalculator calculator)
    {
        _toteApi = toteApi;
        _calculator = calculator;
    }

    /// <summary>
    /// Gets the top value bets for a race, filtered by settings.
    /// </summary>
    public async Task<IReadOnlyList<ValueBet>> GetTopValueBetsAsync(
        string raceName,
        ValueBetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var race = await _toteApi.GetRaceDataAsync(raceName, cancellationToken);
        if (race is null || !race.HasValidData)
            return Array.Empty<ValueBet>();

        if (race.PoolNetAmount < settings.MinimumPoolSize)
            return Array.Empty<ValueBet>();

        var stake = settings.DefaultStakeForDilution;
        var dilutionFactor = _calculator.GetDilutionFactor(race.PoolNetAmount, stake);

        var dilutionPercent = (1m - dilutionFactor) * 100m;
        if (dilutionPercent > settings.MaxDilutionPercent)
            return Array.Empty<ValueBet>();

        var candidates = new List<ValueBet>();

        foreach (var kvp in race.ExactaOdds)
        {
            var parts = kvp.Key.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var first) || !int.TryParse(parts[1], out var second))
                continue;

            var toteOdds = kvp.Value;
            if (toteOdds <= 0) continue;

            var fairProb = _calculator.GetFairExactaProbability(first, second, race.WinOdds);
            if (fairProb <= 0) continue;

            var fairOdds = _calculator.GetFairOdds(fairProb);
            var valuePercent = _calculator.GetValuePercent(fairOdds, toteOdds);

            if (valuePercent < settings.ValueThresholdPercent)
                continue;

            var effectiveOdds = _calculator.GetEffectiveOdds(toteOdds, dilutionFactor);

            if (!race.HorseNames.TryGetValue(first, out var firstName))
                firstName = $"#{first}";
            if (!race.HorseNames.TryGetValue(second, out var secondName))
                secondName = $"#{second}";

            candidates.Add(new ValueBet
            {
                First = first,
                Second = second,
                FirstName = firstName,
                SecondName = secondName,                
                ToteOdds = toteOdds,
                FairOdds = fairOdds,
                ValuePercent = valuePercent,
                PoolSize = race.PoolNetAmount,
                DilutionFactor = dilutionFactor,
                EffectiveOdds = effectiveOdds,
                RaceName = race.RaceName
            });
        }

        return candidates
            .OrderByDescending(b => b.ValuePercent)
            .Take(settings.TopBetCount)
            .ToList();
    }

    /// <summary>
    /// Gets all exacta value calculations for a race (no threshold or count limit), sorted by value % descending.
    /// </summary>
    public async Task<IReadOnlyList<ValueBet>> GetAllValueCalculationsAsync(
        string raceName,
        ValueBetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var race = await _toteApi.GetRaceDataAsync(raceName, cancellationToken);
        if (race is null || !race.HasValidData)
            return Array.Empty<ValueBet>();

        if (race.PoolNetAmount < settings.MinimumPoolSize)
            return Array.Empty<ValueBet>();

        var stake = settings.DefaultStakeForDilution;
        var dilutionFactor = _calculator.GetDilutionFactor(race.PoolNetAmount, stake);

        var dilutionPercent = (1m - dilutionFactor) * 100m;
        if (dilutionPercent > settings.MaxDilutionPercent)
            return Array.Empty<ValueBet>();

        var all = new List<ValueBet>();

        foreach (var kvp in race.ExactaOdds)
        {
            var parts = kvp.Key.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var first) || !int.TryParse(parts[1], out var second))
                continue;

            var toteOdds = kvp.Value;
            if (toteOdds <= 0) continue;

            var fairProb = _calculator.GetFairExactaProbability(first, second, race.WinOdds);
            if (fairProb <= 0) continue;

            var fairOdds = _calculator.GetFairOdds(fairProb);
            var valuePercent = _calculator.GetValuePercent(fairOdds, toteOdds);
            var effectiveOdds = _calculator.GetEffectiveOdds(toteOdds, dilutionFactor);

            if (!race.HorseNames.TryGetValue(first, out var firstName))
                firstName = $"#{first}";
            if (!race.HorseNames.TryGetValue(second, out var secondName))
                secondName = $"#{second}";

            all.Add(new ValueBet
            {
                First = first,
                Second = second,
                FirstName = firstName,
                SecondName = secondName,
                ToteOdds = toteOdds,
                FairOdds = fairOdds,
                ValuePercent = valuePercent,
                PoolSize = race.PoolNetAmount,
                DilutionFactor = dilutionFactor,
                EffectiveOdds = effectiveOdds,
                RaceName = race.RaceName
            });
        }

        return all.OrderByDescending(b => b.ValuePercent).ToList();
    }

    /// <summary>
    /// Gets available races that have EXACTA data (with display name and country code).
    /// </summary>
    public Task<IReadOnlyList<AvailableRace>> GetAvailableRacesAsync(CancellationToken cancellationToken = default)
    {
        return _toteApi.GetAvailableRacesAsync(cancellationToken);
    }

    /// <summary>
    /// Clears cached race/event data so the next Load Races fetches fresh data from the API.
    /// </summary>
    public void InvalidateRaceDataCache()
    {
        _toteApi.InvalidateCache();
    }
}
