using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface IFplFixtureService
{
    /// <summary>
    /// Upcoming fixtures per team, starting at <paramref name="fromGameweek"/>,
    /// capped at <paramref name="count"/> fixtures per team (a double gameweek
    /// counts as two — see README known limitations).
    /// </summary>
    Task<IReadOnlyDictionary<int, List<FixturePreview>>> GetUpcomingFixturesByTeamAsync(
        int fromGameweek, int count = 3, CancellationToken cancellationToken = default);
}
