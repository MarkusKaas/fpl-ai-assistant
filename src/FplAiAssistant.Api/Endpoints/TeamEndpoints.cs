using System.Text.Json;
using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Endpoints;

public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{teamId:int}", async (
            int teamId,
            FplDbContext db,
            IFplTeamService teamService,
            IFplFixtureService fixtureService,
            IFplGameweekService gameweekService,
            ISquadAnalysisService analysisService,
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

            FplEntryInfoDto entryInfo;
            int gameweek;
            List<FplPickDto> picks;
            try
            {
                entryInfo = await teamService.GetEntryInfoAsync(teamId, ct);
                (gameweek, picks) = await teamService.GetCurrentPicksAsync(teamId, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                return Results.NotFound(new
                {
                    error = $"Couldn't load FPL team {teamId}. Double-check the team ID from your FPL team URL " +
                            "(fantasy.premierleague.com/entry/<id>/...).",
                });
            }

            var nextGameweek = await gameweekService.GetNextGameweekAsync(ct);
            var fixturesByTeam = await fixtureService.GetUpcomingFixturesByTeamAsync(nextGameweek, count: 3, cancellationToken: ct);

            var squadPlayers = pool.Where(p => picks.Any(pick => pick.Element == p.Id)).ToList();

            var dashboard = analysisService.BuildDashboard(entryInfo, gameweek, picks, squadPlayers, pool, fixturesByTeam);

            return Results.Ok(dashboard);
        })
        .WithName("GetTeamDashboard")
        .WithSummary("Loads a manager's current squad by public FPL team ID, enriches it with next-3-fixture difficulty, and suggests a captain and transfers.");

        group.MapGet("/{teamId:int}/history", async (
            int teamId,
            IFplTeamService teamService,
            CancellationToken ct) =>
        {
            FplEntryInfoDto entryInfo;
            IReadOnlyList<FplGameweekHistoryDto> history;
            try
            {
                entryInfo = await teamService.GetEntryInfoAsync(teamId, ct);
                history = await teamService.GetHistoryAsync(teamId, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                return Results.NotFound(new
                {
                    error = $"Couldn't load history for FPL team {teamId}. Double-check the team ID from your FPL team URL.",
                });
            }

            var points = history
                .OrderBy(h => h.Event)
                .Select(h => new GameweekHistoryPoint(
                    h.Event, h.Points, h.TotalPoints, h.OverallRank,
                    h.TeamValueTenths / 10m, h.BankTenths / 10m, h.Transfers, h.TransferCost, h.PointsOnBench))
                .ToList();

            return Results.Ok(new TeamHistoryResponse(
                TeamId: entryInfo.Id,
                ManagerName: $"{entryInfo.PlayerFirstName} {entryInfo.PlayerLastName}".Trim(),
                TeamName: entryInfo.Name,
                History: points));
        })
        .WithName("GetTeamHistory")
        .WithSummary("Loads a manager's season-long, gameweek-by-gameweek points and overall-rank history.");

        return group;
    }
}
