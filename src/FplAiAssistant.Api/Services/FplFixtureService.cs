using System.Net.Http.Json;
using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Pulls upcoming fixtures from the official, public FPL fixtures endpoint and
/// turns them into a simple "next N fixtures per team" lookup, using the FPL
/// difficulty rating (FDR, 1 = easiest, 5 = hardest) already baked into the response —
/// no need to compute our own.
/// </summary>
public class FplFixtureService : IFplFixtureService
{
    private const string FixturesUrl = "https://fantasy.premierleague.com/api/fixtures/?future=1";
    private const string CacheKey = "fpl-fixtures";

    private readonly HttpClient _httpClient;
    private readonly FplDbContext _db;
    private readonly IMemoryCache _cache;

    public FplFixtureService(HttpClient httpClient, FplDbContext db, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<int, List<FixturePreview>>> GetUpcomingFixturesByTeamAsync(
        int fromGameweek, int count = 3, CancellationToken cancellationToken = default)
    {
        var fixtures = await GetFixturesAsync(cancellationToken);
        var teamNames = await _db.Teams.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.ShortName, cancellationToken);

        var byTeam = new Dictionary<int, List<FixturePreview>>();

        void AddPreview(int teamId, int opponentId, bool isHome, int difficulty, int gameweek)
        {
            if (!byTeam.TryGetValue(teamId, out var list))
            {
                list = new List<FixturePreview>();
                byTeam[teamId] = list;
            }

            if (list.Count < count)
            {
                list.Add(new FixturePreview(gameweek, teamNames.GetValueOrDefault(opponentId, "?"), isHome, difficulty));
            }
        }

        foreach (var fixture in fixtures.Where(f => f.Event is not null && f.Event >= fromGameweek).OrderBy(f => f.Event))
        {
            AddPreview(fixture.TeamH, fixture.TeamA, isHome: true, fixture.TeamHDifficulty, fixture.Event!.Value);
            AddPreview(fixture.TeamA, fixture.TeamH, isHome: false, fixture.TeamADifficulty, fixture.Event!.Value);
        }

        return byTeam;
    }

    private async Task<List<FplFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out List<FplFixtureDto>? cached) && cached is not null)
        {
            return cached;
        }

        var fixtures = await _httpClient.GetFromJsonAsync<List<FplFixtureDto>>(FixturesUrl, cancellationToken)
            ?? new List<FplFixtureDto>();

        _cache.Set(CacheKey, fixtures, TimeSpan.FromMinutes(15));
        return fixtures;
    }
}
