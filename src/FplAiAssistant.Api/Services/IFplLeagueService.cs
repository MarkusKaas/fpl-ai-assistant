using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

/// <summary>Fetches a classic FPL league's standings from the official public API.</summary>
public interface IFplLeagueService
{
    Task<FplLeagueStandingsDto> GetClassicLeagueStandingsAsync(int leagueId, CancellationToken cancellationToken = default);
}
