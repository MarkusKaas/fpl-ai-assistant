using System.Net.Http.Json;
using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Reads a manager's team data from the official, public FPL API — the same
/// data shown on a manager's public profile page, no login or API key needed:
///   GET https://fantasy.premierleague.com/api/entry/{teamId}/
///   GET https://fantasy.premierleague.com/api/entry/{teamId}/event/{eventId}/picks/
/// </summary>
public class FplTeamService : IFplTeamService
{
    private readonly HttpClient _httpClient;
    private readonly IFplGameweekService _gameweekService;

    public FplTeamService(HttpClient httpClient, IFplGameweekService gameweekService)
    {
        _httpClient = httpClient;
        _gameweekService = gameweekService;
    }

    public async Task<FplEntryInfoDto> GetEntryInfoAsync(int teamId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<FplEntryInfoDto>(
            $"https://fantasy.premierleague.com/api/entry/{teamId}/", cancellationToken)
            ?? throw new InvalidOperationException($"FPL team {teamId} was not found.");
    }

    public async Task<(int Gameweek, List<FplPickDto> Picks)> GetCurrentPicksAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var gameweek = await _gameweekService.GetCurrentSquadGameweekAsync(cancellationToken);

        var response = await _httpClient.GetFromJsonAsync<FplPicksResponseDto>(
            $"https://fantasy.premierleague.com/api/entry/{teamId}/event/{gameweek}/picks/", cancellationToken)
            ?? throw new InvalidOperationException($"No picks found for FPL team {teamId} in gameweek {gameweek}.");

        return (gameweek, response.Picks);
    }

    public async Task<IReadOnlyList<FplGameweekHistoryDto>> GetHistoryAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<FplEntryHistoryDto>(
            $"https://fantasy.premierleague.com/api/entry/{teamId}/history/", cancellationToken)
            ?? throw new InvalidOperationException($"No history found for FPL team {teamId}.");

        return response.Current;
    }
}
