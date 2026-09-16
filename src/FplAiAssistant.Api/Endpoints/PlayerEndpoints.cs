using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Endpoints;

public static class PlayerEndpoints
{
    public static RouteGroupBuilder MapPlayerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (FplDbContext db, string? position, int? take) =>
        {
            var query = db.Players.Include(p => p.Team).AsQueryable();

            if (!string.IsNullOrWhiteSpace(position) && Enum.TryParse<Position>(position, true, out var parsed))
            {
                query = query.Where(p => p.Position == parsed);
            }

            var effectiveTake = (take is null or <= 0) ? 20 : Math.Min(take.Value, 100);

            var players = await query
                .OrderByDescending(p => p.Form)
                .Take(effectiveTake)
                .Select(p => new
                {
                    p.Id,
                    p.WebName,
                    Team = p.Team!.ShortName,
                    Position = p.Position.ToString(),
                    p.Price,
                    p.TotalPoints,
                    p.Form,
                    p.SelectedByPercent,
                })
                .ToListAsync();

            return Results.Ok(players);
        })
        .WithName("GetPlayers")
        .WithSummary("Lists players, optionally filtered by position (GK/Defender/Midfielder/Forward) and capped by 'take'.");

        return group;
    }
}
