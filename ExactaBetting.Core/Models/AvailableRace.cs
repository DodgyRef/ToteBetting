namespace ExactaBetting.Core.Models;

/// <summary>
/// A race available for value bet analysis, with display name (including start time) and country for filtering.
/// </summary>
public sealed class AvailableRace
{
    /// <summary>Base race name used for API lookups (e.g. GetRaceDataAsync).</summary>
    public required string BaseName { get; init; }

    /// <summary>Display name shown in the dropdown (base name + start time, e.g. "SANTA ANITA PARK RACE 7 23:43").</summary>
    public required string DisplayName { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. "US", "GB", "IE").</summary>
    public required string CountryCode { get; init; }
}
