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

    /// <summary>Pool net amount for dilution calculation.</summary>
    public decimal PoolNetAmount { get; init; }

    /// <summary>Carry-in net amount (from previous pool).</summary>
    public decimal CarryInNetAmount { get; init; }

    /// <summary>Guarantee net amount (minimum pool guarantee).</summary>
    public decimal GuaranteeNetAmount { get; init; }

    /// <summary>Top-up net amount (added to meet guarantee).</summary>
    public decimal TopUpNetAmount { get; init; }

    /// <summary>Whether the race has sufficient data for value analysis.</summary>
    public bool HasValidData => WinOdds.Count >= 2 && ExactaOdds.Count > 0 && PoolNetAmount > 0;
}
