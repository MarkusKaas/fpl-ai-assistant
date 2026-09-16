using FplAiAssistant.Api.Services;

namespace FplAiAssistant.Api.Endpoints;

public static class DataEndpoints
{
    public static RouteGroupBuilder MapDataEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/refresh", async (IFplDataService dataService, CancellationToken ct) =>
        {
            var (teams, players) = await dataService.RefreshAsync(ct);
            return Results.Ok(new { teamsWritten = teams, playersWritten = players });
        })
        .WithName("RefreshData")
        .WithSummary("Pulls the latest player/team data from the official FPL API into the local database.");

        return group;
    }
}
