using System.Text;
using Azure;
using Azure.AI.OpenAI;
using FplAiAssistant.Api.Models;
using OpenAI.Chat;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Generates advice by grounding a chat completion in the players the
/// <see cref="IPlayerRetrievalService"/> retrieved — the "generation" half of
/// a retrieval-augmented-generation pipeline. The model is only ever shown the
/// small, pre-filtered slice of real data passed in as context; it isn't asked
/// to recall FPL stats from its own training data, which is what keeps the
/// numbers in its answer trustworthy.
/// </summary>
public class AzureOpenAiAdviceGenerator : IAdviceGenerator
{
    public string Name => "azure-openai";

    private readonly ChatClient _chatClient;

    public AzureOpenAiAdviceGenerator(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured.");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured.");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(deploymentName);
    }

    public async Task<string> GenerateAsync(string question, IReadOnlyList<PlayerSummary> context, CancellationToken cancellationToken = default)
    {
        var systemPrompt =
            "You are a concise Fantasy Premier League assistant. Base your answer strictly " +
            "on the player data provided below — do not invent stats, and do not rely on your " +
            "own knowledge of the current season. If the data doesn't support a confident answer, " +
            "say so. Keep the answer to 3-4 sentences.\n\n" +
            "Player data (form is a recent-form score, not season total):\n" +
            FormatContext(context);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(question),
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }

    private static string FormatContext(IReadOnlyList<PlayerSummary> context)
    {
        var sb = new StringBuilder();
        foreach (var p in context)
        {
            sb.AppendLine(
                $"- {p.Name} ({p.Team}, {p.Position}): £{p.Price}m, {p.TotalPoints} pts this season, " +
                $"form {p.Form}, owned by {p.SelectedByPercent}% of managers");
        }
        return sb.ToString();
    }
}
