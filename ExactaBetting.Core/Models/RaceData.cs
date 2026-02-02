namespace ExactaBetting.Core.Models;

/// <summary>
/// Combined WIN and EXACTA data for a single race, used for value calculations.
/// </summary>
public sealed class RaceData
{
    public required string RaceName { get; init; }
    public required string EventId { get; init; }

    /// <summary>WIN odds by horse number (1-based). Key = runner number, Value = decimal odds.</summary>
    public required IReadOnlyDictionary<int, decimal> WinOdds { get; init; }

    /// <summary>Horse names by runner number (1-based).</summary>
    public required IReadOnlyDictionary<int, string> HorseNames { get; init; }

    /// <summary>EXACTA odds by (first, second) combination. Key = "first-second".</summary>
    public required IReadOnlyDictionary<string, decimal> ExactaOdds { get; init; }

    /// <summary>TRIFECTA odds by (first, second, third) combination. Key = "first-second-third".</summary>
    public IReadOnlyDictionary<string, decimal> TrifectaOdds { get; init; } = new Dictionary<string, decimal>();

    /// <summary>EXACTA pool gross amount (total staked).</summary>
    public decimal PoolGrossAmount { get; init; }

    /// <summary>EXACTA pool net amount for dilution calculation.</summary>
    public decimal PoolNetAmount { get; init; }

    /// <summary>EXACTA carry-in net amount (from previous pool).</summary>
    public decimal CarryInNetAmount { get; init; }

    /// <summary>EXACTA guarantee net amount (minimum pool guarantee).</summary>
    public decimal GuaranteeNetAmount { get; init; }

    /// <summary>EXACTA top-up net amount (added to meet guarantee).</summary>
    public decimal TopUpNetAmount { get; init; }

    /// <summary>TRIFECTA pool gross amount (total staked).</summary>
    public decimal TrifectaPoolGrossAmount { get; init; }

    /// <summary>TRIFECTA pool net amount for trifecta dilution calculation.</summary>
    public decimal TrifectaPoolNetAmount { get; init; }

    /// <summary>TRIFECTA carry-in net amount.</summary>
    public decimal TrifectaCarryInNetAmount { get; init; }

    /// <summary>TRIFECTA guarantee net amount.</summary>
    public decimal TrifectaGuaranteeNetAmount { get; init; }

    /// <summary>TRIFECTA top-up net amount.</summary>
    public decimal TrifectaTopUpNetAmount { get; init; }

    /// <summary>WIN pool gross amount (total staked).</summary>
    public decimal WinPoolGrossAmount { get; init; }

    /// <summary>WIN pool net amount (after deductions).</summary>
    public decimal WinPoolNetAmount { get; init; }

    /// <summary>WIN pool carry-in net amount.</summary>
    public decimal WinCarryInNetAmount { get; init; }

    /// <summary>WIN pool guarantee net amount.</summary>
    public decimal WinGuaranteeNetAmount { get; init; }

    /// <summary>WIN pool top-up net amount.</summary>
    public decimal WinTopUpNetAmount { get; init; }

    /// <summary>Whether the race has sufficient data for value analysis (EXACTA or TRIFECTA).</summary>
    public bool HasValidData => WinOdds.Count >= 2 && (
        (ExactaOdds.Count > 0 && PoolNetAmount > 0) ||
        (TrifectaOdds.Count > 0 && TrifectaPoolNetAmount > 0));
}
