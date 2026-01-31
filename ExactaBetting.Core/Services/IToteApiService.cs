using ExactaBetting.Core.Models;

namespace ExactaBetting.Core.Services;

/// <summary>
/// Interface for fetching race data from the TOTE GraphQL API.
/// Implement this with your real-time subscription logic.
/// </summary>
public interface IToteApiService
{
    /// <summary>
    /// Gets combined WIN and EXACTA data for a specific race by name.
    /// </summary>
    /// <param name="raceName">e.g. "KENILWORTH RACE 1"</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Race data or null if not found.</returns>
    Task<RaceData?> GetRaceDataAsync(string raceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available race names that have EXACTA data.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableRacesAsync(CancellationToken cancellationToken = default);
}
