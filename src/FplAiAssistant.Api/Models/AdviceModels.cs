namespace FplAiAssistant.Api.Models;

public record AdviceRequest(string Question);

public record PlayerSummary(
    string Name,
    string Team,
    string Position,
    decimal Price,
    int TotalPoints,
    decimal Form,
    decimal SelectedByPercent
);

public record AdviceResponse(
    string Question,
    string Answer,
    IReadOnlyList<PlayerSummary> ConsideredPlayers,
    string GeneratedBy
);
