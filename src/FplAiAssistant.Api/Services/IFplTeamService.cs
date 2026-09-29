using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface IFplTeamService
{
    /// <summary>Public profile info (name, points, rank, bank, squad value) for an FPL team id.</summary>
    Task<FplEntryInfoDto> GetEntryInfoAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>The manager's current 15-man squad, and which gameweek that reflects.</summary>
    Task<(int Gameweek, List<FplPickDto> Picks)> GetCurrentPicksAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>The manager's season-long, gameweek-by-gameweek points/rank history.</summary>
    Task<IReadOnlyList<FplGameweekHistoryDto>> GetHistoryAsync(int teamId, CancellationToken cancellationToken = default);
}
