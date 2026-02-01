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
    /// Gets all available races that have EXACTA data, with display name (including start time) and country code.
    /// </summary>
    Task<IReadOnlyList<AvailableRace>> GetAvailableRacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears cached race and event data so the next load fetches fresh data from the API.
    /// Call when the user explicitly requests a refresh (e.g. Load Races).
    /// </summary>
    void InvalidateCache();
}
