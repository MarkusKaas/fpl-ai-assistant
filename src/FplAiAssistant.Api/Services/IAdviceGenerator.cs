using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public interface IAdviceGenerator
{
    /// <summary>A short label identifying which implementation produced the answer
    /// (returned to the caller so it's obvious in the API response whether the
    /// answer came from Azure OpenAI or the offline fallback).</summary>
    string Name { get; }

    Task<string> GenerateAsync(string question, IReadOnlyList<PlayerSummary> context, CancellationToken cancellationToken = default);
}
