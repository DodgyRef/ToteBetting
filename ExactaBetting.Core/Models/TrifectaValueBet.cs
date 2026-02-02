namespace ExactaBetting.Core.Models;

/// <summary>
/// Represents a value bet recommendation for a Trifecta (first, second, third) combination.
/// </summary>
public sealed class TrifectaValueBet
{
    public int First { get; init; }
    public int Second { get; init; }
    public int Third { get; init; }
    public required string FirstName { get; init; }
    public required string SecondName { get; init; }
    public required string ThirdName { get; init; }
    public decimal ToteOdds { get; init; }
    public decimal FairOdds { get; init; }
    public decimal ValuePercent { get; init; }
    public decimal PoolSize { get; init; }
    public decimal DilutionFactor { get; init; }
    public decimal EffectiveOdds { get; init; }
    public required string RaceName { get; init; }

    public string Combination => $"{First}-{Second}-{Third}";
    public string DisplayName => $"{First}. {FirstName} → {Second}. {SecondName} → {Third}. {ThirdName}";
}
