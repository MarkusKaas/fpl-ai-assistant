using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Endpoints;

public static class AdviceEndpoints
{
    public static RouteGroupBuilder MapAdviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            AdviceRequest request,
            FplDbContext db,
            IPlayerRetrievalService retrieval,
            IAdviceGenerator generator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new { error = "Question must not be empty." });
            }

            var pool = await db.Players.Include(p => p.Team).ToListAsync(ct);
            if (pool.Count == 0)
            {
                return Results.Conflict(new
                {
                    error = "No player data loaded yet. Call POST /api/data/refresh first.",
                });
            }

            var context = retrieval.Retrieve(request.Question, pool);
            var answer = await generator.GenerateAsync(request.Question, context, ct);

            return Results.Ok(new AdviceResponse(
                Question: request.Question,
                Answer: answer,
                ConsideredPlayers: context,
                GeneratedBy: generator.Name
            ));
        })
        .WithName("GetAdvice")
        .WithSummary("Answers a free-text FPL question, grounded in retrieved player data (RAG pattern).");

        return group;
    }
}
