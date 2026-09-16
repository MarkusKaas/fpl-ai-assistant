using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface IPlayerRetrievalService
{
    /// <summary>
    /// Given a free-text FPL question, picks a small, relevant set of players
    /// out of the full pool to use as grounding context for the advice generator.
    /// This is the "retrieval" step of a retrieval-augmented-generation pattern —
    /// deliberately simple (keyword + scoring heuristics, no embeddings) so it's
    /// fast, free to run, and easy to unit test without any external service.
    /// </summary>
    IReadOnlyList<PlayerSummary> Retrieve(string question, IReadOnlyList<Player> pool, int take = 8);
}
