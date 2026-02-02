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
    /// Gets the top Trifecta value bets for a race, filtered by settings.
    /// </summary>
    public async Task<IReadOnlyList<TrifectaValueBet>> GetTopTrifectaValueBetsAsync(
        string raceName,
        ValueBetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var race = await _toteApi.GetRaceDataAsync(raceName, cancellationToken);
        if (race is null || race.TrifectaOdds.Count == 0 || race.WinOdds.Count < 2 || race.TrifectaPoolNetAmount <= 0)
            return Array.Empty<TrifectaValueBet>();

        if (race.TrifectaPoolNetAmount < settings.MinimumPoolSize)
            return Array.Empty<TrifectaValueBet>();

        var stake = settings.DefaultStakeForDilution;
        var dilutionFactor = _calculator.GetDilutionFactor(race.TrifectaPoolNetAmount, stake);

        var dilutionPercent = (1m - dilutionFactor) * 100m;
        if (dilutionPercent > settings.MaxDilutionPercent)
            return Array.Empty<TrifectaValueBet>();

        var candidates = new List<TrifectaValueBet>();

        foreach (var kvp in race.TrifectaOdds)
        {
            var parts = kvp.Key.Split('-');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var first) || !int.TryParse(parts[1], out var second) || !int.TryParse(parts[2], out var third))
                continue;

            var toteOdds = kvp.Value;
            if (toteOdds <= 0) continue;

            var fairProb = _calculator.GetFairTrifectaProbability(first, second, third, race.WinOdds);
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
            if (!race.HorseNames.TryGetValue(third, out var thirdName))
                thirdName = $"#{third}";

            candidates.Add(new TrifectaValueBet
            {
                First = first,
                Second = second,
                Third = third,
                FirstName = firstName,
                SecondName = secondName,
                ThirdName = thirdName,
                ToteOdds = toteOdds,
                FairOdds = fairOdds,
                ValuePercent = valuePercent,
                PoolSize = race.TrifectaPoolNetAmount,
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
    /// Gets all Trifecta value calculations for a race (no pool/dilution/threshold/count filters), sorted by value % descending.
    /// </summary>
    public async Task<IReadOnlyList<TrifectaValueBet>> GetAllTrifectaValueCalculationsAsync(
        string raceName,
        ValueBetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var race = await _toteApi.GetRaceDataAsync(raceName, cancellationToken);
        if (race is null || race.TrifectaOdds.Count == 0 || race.WinOdds.Count < 2)
            return Array.Empty<TrifectaValueBet>();

        var stake = settings.DefaultStakeForDilution;
        var dilutionFactor = _calculator.GetDilutionFactor(race.TrifectaPoolNetAmount, stake);

        var all = new List<TrifectaValueBet>();

        foreach (var kvp in race.TrifectaOdds)
        {
            var parts = kvp.Key.Split('-');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var first) || !int.TryParse(parts[1], out var second) || !int.TryParse(parts[2], out var third))
                continue;

            var toteOdds = kvp.Value;
            if (toteOdds <= 0) continue;

            var fairProb = _calculator.GetFairTrifectaProbability(first, second, third, race.WinOdds);
            if (fairProb <= 0) continue;

            var fairOdds = _calculator.GetFairOdds(fairProb);
            var valuePercent = _calculator.GetValuePercent(fairOdds, toteOdds);
            var effectiveOdds = _calculator.GetEffectiveOdds(toteOdds, dilutionFactor);

            if (!race.HorseNames.TryGetValue(first, out var firstName))
                firstName = $"#{first}";
            if (!race.HorseNames.TryGetValue(second, out var secondName))
                secondName = $"#{second}";
            if (!race.HorseNames.TryGetValue(third, out var thirdName))
                thirdName = $"#{third}";

            all.Add(new TrifectaValueBet
            {
                First = first,
                Second = second,
                Third = third,
                FirstName = firstName,
                SecondName = secondName,
                ThirdName = thirdName,
                ToteOdds = toteOdds,
                FairOdds = fairOdds,
                ValuePercent = valuePercent,
                PoolSize = race.TrifectaPoolNetAmount,
                DilutionFactor = dilutionFactor,
                EffectiveOdds = effectiveOdds,
                RaceName = race.RaceName
            });
        }

        return all.OrderByDescending(b => b.ValuePercent).ToList();
    }

    /// <summary>
    /// Gets race data for a specific race (e.g. for spreadsheet export).
    /// </summary>
    public Task<RaceData?> GetRaceDataAsync(string raceName, CancellationToken cancellationToken = default)
    {
        return _toteApi.GetRaceDataAsync(raceName, cancellationToken);
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
