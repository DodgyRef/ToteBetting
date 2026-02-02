using ExactaBetting.Core.Models;

namespace ExactaBetting.Core.Services;

/// <summary>
/// Implementation of value calculations for TOTE Exacta betting.
/// </summary>
public sealed class ValueCalculator : IValueCalculator
{
    public decimal GetFairExactaProbability(int first, int second, IReadOnlyDictionary<int, decimal> winOdds)
    {
        if (!winOdds.TryGetValue(first, out var oFirst) || !winOdds.TryGetValue(second, out var oSecond))
            return 0;

        if (oFirst <= 0 || oSecond <= 0) return 0;

        var pFirst = 1m / oFirst;
        var pSecond = 1m / oSecond;

        var pFirstNotWins = 1m - pFirst;
        if (pFirstNotWins <= 0) return 0;

        return pFirst * pSecond / pFirstNotWins;
    }

    public decimal GetFairTrifectaProbability(int first, int second, int third, IReadOnlyDictionary<int, decimal> winOdds)
    {
        if (!winOdds.TryGetValue(first, out var oFirst) || !winOdds.TryGetValue(second, out var oSecond) || !winOdds.TryGetValue(third, out var oThird))
            return 0;
        if (oFirst <= 0 || oSecond <= 0 || oThird <= 0) return 0;

        var pFirst = 1m / oFirst;
        var pSecond = 1m / oSecond;
        var pThird = 1m / oThird;

        var pFirstNotWins = 1m - pFirst;
        if (pFirstNotWins <= 0) return 0;

        var pFirstAndSecond = 1m - pFirst - pSecond;
        if (pFirstAndSecond <= 0) return 0;

        // Avoid divide-by-zero: both denominators already checked above
        return pFirst * (pSecond / pFirstNotWins) * (pThird / pFirstAndSecond);
    }

    public decimal GetFairOdds(decimal probability)
    {
        if (probability <= 0) return 0;
        return 1m / probability;
    }

    /// <summary>Value % when offered odds (tote) beat fair odds. Positive = value bet (tote &gt; fair).</summary>
    public decimal GetValuePercent(decimal fairOdds, decimal toteOdds)
    {
        if (fairOdds <= 0) return 0;
        return (toteOdds / fairOdds - 1m) * 100m;
    }

    public decimal GetDilutionFactor(decimal poolNetAmount, decimal stake)
    {
        var total = poolNetAmount + stake;
        if (total <= 0) return 1m;
        return poolNetAmount / total;
    }

    public decimal GetEffectiveOdds(decimal toteOdds, decimal dilutionFactor)
    {
        return toteOdds * dilutionFactor;
    }
}
