using System.Net.Http.Json;
using FplAiAssistant.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Reads a classic league's standings from the official, public FPL API — the
/// same data shown on a league's public standings page, no login needed:
///   GET https://fantasy.premierleague.com/api/leagues-classic/{leagueId}/standings/
/// Cached briefly since a mini-league's standings don't change more than once
/// per gameweek, and loading a league fans out to one call per team below it.
/// </summary>
public class FplLeagueService : IFplLeagueService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public FplLeagueService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<FplLeagueStandingsDto> GetClassicLeagueStandingsAsync(int leagueId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"fpl-league-{leagueId}";
        if (_cache.TryGetValue(cacheKey, out FplLeagueStandingsDto? cached) && cached is not null)
        {
            return cached;
        }

        var standings = await _httpClient.GetFromJsonAsync<FplLeagueStandingsDto>(
            $"https://fantasy.premierleague.com/api/leagues-classic/{leagueId}/standings/", cancellationToken)
            ?? throw new InvalidOperationException($"FPL league {leagueId} was not found.");

        _cache.Set(cacheKey, standings, TimeSpan.FromMinutes(10));
        return standings;
    }
}
