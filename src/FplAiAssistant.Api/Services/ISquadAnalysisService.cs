using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface ISquadAnalysisService
{
    /// <summary>
    /// Builds the full team dashboard payload: the enriched squad, a suggested
    /// captain, and up to <paramref name="transferSuggestionCount"/> transfer
    /// suggestions — from already-fetched data, so it's pure and easy to test.
    /// </summary>
    TeamDashboardResponse BuildDashboard(
        FplEntryInfoDto entryInfo,
        int asOfGameweek,
        IReadOnlyList<FplPickDto> picks,
        IReadOnlyList<Player> squadPlayers,
        IReadOnlyList<Player> fullPool,
        IReadOnlyDictionary<int, List<FixturePreview>> fixturesByTeam,
        int transferSuggestionCount = 3);
}
