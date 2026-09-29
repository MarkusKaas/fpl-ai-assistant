using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface ILeagueAnalysisService
{
    LeagueDashboardResponse BuildLeagueDashboard(
        FplLeagueStandingsDto standings,
        IReadOnlyDictionary<int, FplEntryInfoDto> entryInfoByTeam,
        IReadOnlyDictionary<int, IReadOnlyList<FplPickDto>> picksByTeam,
        IReadOnlyList<Player> playerPool,
        int asOfGameweek,
        int? viewerTeamId = null);

    /// <summary>
    /// Builds a per-manager gameweek-points series for a season-long league comparison chart.
    /// A team whose history couldn't be loaded is left out rather than failing the whole chart.
    /// </summary>
    LeagueHistoryResponse BuildLeagueHistory(
        FplLeagueStandingsDto standings,
        IReadOnlyDictionary<int, FplEntryInfoDto> entryInfoByTeam,
        IReadOnlyDictionary<int, IReadOnlyList<FplGameweekHistoryDto>> historyByTeam,
        IReadOnlyDictionary<int, string> crestUrlsByTeamId);
}
