using ExactaBetting.Core.Models;

namespace ExactaBetting.Core.Services;

/// <summary>
/// Calculates fair Exacta probabilities from WIN odds and computes value metrics.
/// </summary>
public interface IValueCalculator
{
    /// <summary>
    /// Computes the fair (implied) probability of Exacta (first, second) from WIN pool odds.
    /// Uses: P(A first, B second) ≈ P(A wins) × P(B wins) / (1 - P(A wins)).
    /// </summary>
    decimal GetFairExactaProbability(int first, int second, IReadOnlyDictionary<int, decimal> winOdds);

    /// <summary>
    /// Computes the fair (implied) probability of Trifecta (first, second, third) from WIN pool odds.
    /// Uses: P(A first, B second, C third) ≈ P(A) × P(B)/(1−P(A)) × P(C)/(1−P(A)−P(B)).
    /// </summary>
    decimal GetFairTrifectaProbability(int first, int second, int third, IReadOnlyDictionary<int, decimal> winOdds);

    /// <summary>
    /// Gets fair decimal odds from probability.
    /// </summary>
    decimal GetFairOdds(decimal probability);

    /// <summary>
    /// Computes value percentage: (ToteOdds / FairOdds - 1) × 100. Positive when offered (tote) odds beat fair odds.
    /// </summary>
    decimal GetValuePercent(decimal fairOdds, decimal toteOdds);

    /// <summary>
    /// Computes dilution factor when adding stake to pool: P_net / (P_net + stake).
    /// </summary>
    decimal GetDilutionFactor(decimal poolNetAmount, decimal stake);

    /// <summary>
    /// Effective odds after dilution: ToteOdds × DilutionFactor.
    /// </summary>
    decimal GetEffectiveOdds(decimal toteOdds, decimal dilutionFactor);
}
