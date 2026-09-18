using System.Text.Json;
using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Endpoints;

public static class LeagueEndpoints
{
    // Keep this small: a friends' mini-league is a handful of teams, but a big
    // public league could be thousands — and loading a league means one extra
    // HTTP call pair (entry + picks) per team, so this caps the blast radius.
    private const int MaxEntriesToCompare = 30;

    public static RouteGroupBuilder MapLeagueEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{leagueId:int}", async (
            int leagueId,
            int? highlightTeamId,
            FplDbContext db,
            IFplLeagueService leagueService,
            IFplTeamService teamService,
            IFplGameweekService gameweekService,
            ILeagueAnalysisService analysisService,
            CancellationToken ct) =>
        {
            var pool = await db.Players.Include(p => p.Team).ToListAsync(ct);
            if (pool.Count == 0)
            {
                return Results.Conflict(new
                {
                    error = "No player data loaded yet. Call POST /api/data/refresh first.",
                });
            }

            FplLeagueStandingsDto standings;
            try
            {
                standings = await leagueService.GetClassicLeagueStandingsAsync(leagueId, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                return Results.NotFound(new
                {
                    error = $"Couldn't load FPL league {leagueId}. Double-check the league ID from your league's URL " +
                            "(fantasy.premierleague.com/leagues/<id>/standings/c).",
                });
            }

            var entriesToLoad = standings.Standings.Results.Take(MaxEntriesToCompare).ToList();

            // Fetch every team's entry info + current picks concurrently — one call
            // pair per team is the whole cost of comparing a league. A single team
            // failing (private profile, transient error) is swallowed so it just
            // doesn't appear in the comparison, rather than failing the whole thing.
            var loadTasks = entriesToLoad.Select(async result =>
            {
                try
                {
                    var info = await teamService.GetEntryInfoAsync(result.Entry, ct);
                    var (_, picks) = await teamService.GetCurrentPicksAsync(result.Entry, ct);
                    return (TeamId: result.Entry, Info: info, Picks: (IReadOnlyList<FplPickDto>)picks);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
                {
                    return (TeamId: result.Entry, Info: (FplEntryInfoDto?)null, Picks: (IReadOnlyList<FplPickDto>?)null);
                }
            });

            var loaded = await Task.WhenAll(loadTasks);

            var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto>();
            var picksByTeam = new Dictionary<int, IReadOnlyList<FplPickDto>>();
            foreach (var (teamId, info, picks) in loaded)
            {
                if (info is not null) entryInfoByTeam[teamId] = info;
                if (picks is not null) picksByTeam[teamId] = picks;
            }

            var asOfGameweek = await gameweekService.GetCurrentSquadGameweekAsync(ct);

            var dashboard = analysisService.BuildLeagueDashboard(
                standings, entryInfoByTeam, picksByTeam, pool, asOfGameweek, highlightTeamId);

            return Results.Ok(dashboard);
        })
        .WithName("GetLeagueDashboard")
        .WithSummary("Loads a classic FPL league's standings and compares squad value, bank, gameweek points and captain choice across managers.");

        group.MapGet("/{leagueId:int}/history", async (
            int leagueId,
            IFplLeagueService leagueService,
            IFplTeamService teamService,
            IFplGameweekService gameweekService,
            ILeagueAnalysisService analysisService,
            CancellationToken ct) =>
        {
            FplLeagueStandingsDto standings;
            try
            {
                standings = await leagueService.GetClassicLeagueStandingsAsync(leagueId, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                return Results.NotFound(new
                {
                    error = $"Couldn't load FPL league {leagueId}. Double-check the league ID from your league's URL " +
                            "(fantasy.premierleague.com/leagues/<id>/standings/c).",
                });
            }

            var entriesToLoad = standings.Standings.Results.Take(MaxEntriesToCompare).ToList();

            // Same fan-out pattern as GET /{leagueId}: one call pair per team, concurrently,
            // with per-team failures swallowed so one bad team doesn't sink the whole chart.
            var loadTasks = entriesToLoad.Select(async result =>
            {
                try
                {
                    var info = await teamService.GetEntryInfoAsync(result.Entry, ct);
                    var history = await teamService.GetHistoryAsync(result.Entry, ct);
                    return (TeamId: result.Entry, Info: info, History: history);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
                {
                    return (TeamId: result.Entry, Info: (FplEntryInfoDto?)null, History: (IReadOnlyList<FplGameweekHistoryDto>?)null);
                }
            });

            var loaded = await Task.WhenAll(loadTasks);

            var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto>();
            var historyByTeam = new Dictionary<int, IReadOnlyList<FplGameweekHistoryDto>>();
            foreach (var (teamId, info, history) in loaded)
            {
                if (info is not null) entryInfoByTeam[teamId] = info;
                if (history is not null) historyByTeam[teamId] = history;
            }

            var crestUrlsByTeamId = await gameweekService.GetTeamCrestUrlsAsync(ct);

            var response = analysisService.BuildLeagueHistory(standings, entryInfoByTeam, historyByTeam, crestUrlsByTeamId);
            return Results.Ok(response);
        })
        .WithName("GetLeagueHistory")
        .WithSummary("Loads gameweek-by-gameweek points for every manager in a classic league, for a season-long comparison chart.");

        return group;
    }
}
