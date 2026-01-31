namespace ExactaBetting.Core.Models;

/// <summary>
/// User-configurable settings for value bet filtering and dilution calculations.
/// </summary>
public sealed class ValueBetSettings
{
    /// <summary>Minimum value percentage to consider a bet (e.g., 10 = 10%).</summary>
    public decimal ValueThresholdPercent { get; set; } = 10.0m;

    /// <summary>Minimum pool size in currency units to avoid thin pools.</summary>
    public decimal MinimumPoolSize { get; set; } = 5000m;

    /// <summary>Maximum allowed dilution impact on dividend (e.g., 5 = 5%).</summary>
    public decimal MaxDilutionPercent { get; set; } = 5.0m;

    /// <summary>Assumed stake for dilution calculation.</summary>
    public decimal DefaultStakeForDilution { get; set; } = 100m;

    /// <summary>Odds type to use: "Base" or "Enhanced".</summary>
    public string OddsType { get; set; } = "Base";

    /// <summary>Maximum number of value bets to display.</summary>
    public int TopBetCount { get; set; } = 5;
}
