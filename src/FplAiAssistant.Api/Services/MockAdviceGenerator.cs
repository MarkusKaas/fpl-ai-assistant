using System.Text;
using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Deterministic, zero-cost stand-in for <see cref="AzureOpenAiAdviceGenerator"/>.
/// Registered automatically whenever Azure OpenAI isn't configured, so the app
/// (and its GitHub Actions build) always runs end-to-end without needing a paid
/// API key — useful for demos, CI, and anyone cloning the repo to try it out.
/// </summary>
public class MockAdviceGenerator : IAdviceGenerator
{
    public string Name => "offline-mock";

    public Task<string> GenerateAsync(string question, IReadOnlyList<PlayerSummary> context, CancellationToken cancellationToken = default)
    {
        if (context.Count == 0)
        {
            return Task.FromResult(
                "No players matched that question closely enough to give a grounded answer. " +
                "Try naming a position, or run POST /api/data/refresh if the database is empty.");
        }

        var top = context[0];
        var sb = new StringBuilder();
        sb.Append($"[offline mode — configure AzureOpenAI in appsettings to get natural-language answers] ");
        sb.Append($"Based on recent form and points, {top.Name} ({top.Team}, {top.Position}) is the strongest match: ");
        sb.Append($"£{top.Price}m, {top.TotalPoints} points this season, form {top.Form}. ");

        if (context.Count > 1)
        {
            var others = string.Join(", ", context.Skip(1).Take(3).Select(p => p.Name));
            sb.Append($"Other options worth a look: {others}.");
        }

        return Task.FromResult(sb.ToString());
    }
}
