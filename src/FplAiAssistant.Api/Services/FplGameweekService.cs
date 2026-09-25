using System.Net.Http.Json;
using FplAiAssistant.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Reads gameweek ("event") metadata and team crest info from the official FPL
/// API's bootstrap-static endpoint. Cached briefly in memory since it barely
/// changes but gets read on every dashboard load.
/// </summary>
public class FplGameweekService : IFplGameweekService
{
    private const string BootstrapUrl = "https://fantasy.premierleague.com/api/bootstrap-static/";
    private const string CacheKey = "fpl-bootstrap";

    // Official Premier League club crest CDN — keyed by each team's "code"
    // (distinct from its FPL "id"), the same one fantasy.premierleague.com itself uses.
    private const string CrestUrlTemplate = "https://resources.premierleague.com/premierleague/badges/50/t{0}.png";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public FplGameweekService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<int> GetCurrentSquadGameweekAsync(CancellationToken cancellationToken = default)
    {
        var events = await GetEventsAsync(cancellationToken);
        var lastFinished = events.Where(e => e.Finished).OrderByDescending(e => e.Id).FirstOrDefault();
        return lastFinished?.Id ?? events.OrderBy(e => e.Id).First().Id;
    }

    public async Task<int> GetNextGameweekAsync(CancellationToken cancellationToken = default)
    {
        var events = await GetEventsAsync(cancellationToken);
        var next = events.FirstOrDefault(e => e.IsNext) ?? events.FirstOrDefault(e => e.IsCurrent);
        return next?.Id ?? events.OrderBy(e => e.Id).First().Id;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetTeamCrestUrlsAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetBootstrapAsync(cancellationToken);
        return bootstrap.Teams.ToDictionary(t => t.Id, t => string.Format(CrestUrlTemplate, t.Code));
    }

    private async Task<List<FplEventDto>> GetEventsAsync(CancellationToken cancellationToken)
    {
        var bootstrap = await GetBootstrapAsync(cancellationToken);
        return bootstrap.Events;
    }

    private async Task<FplBootstrapDto> GetBootstrapAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out FplBootstrapDto? cached) && cached is not null)
        {
            return cached;
        }

        var bootstrap = await _httpClient.GetFromJsonAsync<FplBootstrapDto>(BootstrapUrl, cancellationToken)
            ?? throw new InvalidOperationException("The FPL API returned an empty response.");

        _cache.Set(CacheKey, bootstrap, TimeSpan.FromMinutes(15));
        return bootstrap;
    }
}
