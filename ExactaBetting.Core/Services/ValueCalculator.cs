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

        if (oFirst <= 0) return 0;

        var pFirst = 1m / oFirst;
        var pSecond = 1m / oSecond;

        var pFirstNotWins = 1m - pFirst;
        if (pFirstNotWins <= 0) return 0;

        return pFirst * pSecond / pFirstNotWins;
    }

    public decimal GetFairOdds(decimal probability)
    {
        if (probability <= 0) return 0;
        return 1m / probability;
    }

    public decimal GetValuePercent(decimal fairOdds, decimal toteOdds)
    {
        if (toteOdds <= 0) return 0;
        return (fairOdds / toteOdds - 1m) * 100m;
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
