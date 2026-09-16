using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

public class PlayerRetrievalService : IPlayerRetrievalService
{
    public IReadOnlyList<PlayerSummary> Retrieve(string question, IReadOnlyList<Player> pool, int take = 8)
    {
        ArgumentNullException.ThrowIfNull(pool);
        var normalized = (question ?? string.Empty).ToLowerInvariant();

        IEnumerable<Player> candidates = pool;

        // 1. Narrow by position if the question names one.
        var requestedPosition = DetectPosition(normalized);
        if (requestedPosition is not null)
        {
            candidates = candidates.Where(p => p.Position == requestedPosition);
        }

        // 2. "Differential" picks: low ownership, but still in form. Otherwise
        //    ignore ownership entirely — most questions aren't about it.
        var wantsDifferential = normalized.Contains("differential") || normalized.Contains("low owned") || normalized.Contains("under the radar");
        if (wantsDifferential)
        {
            candidates = candidates.Where(p => p.SelectedByPercent <= 10m);
        }

        // 3. Budget-conscious picks.
        var wantsBudget = normalized.Contains("budget") || normalized.Contains("cheap") || normalized.Contains("value");
        if (wantsBudget)
        {
            candidates = candidates.Where(p => p.Price <= 6.0m);
        }

        // 4. Rank by a simple blended score: recent form weighted a bit higher
        //    than season-long points, since "who should I pick this week" is
        //    mostly a recency question. Require some minutes so bench players
        //    with a lucky cameo don't dominate the list.
        var ranked = candidates
            .Where(p => p.MinutesPlayed >= 90)
            .OrderByDescending(p => (p.Form * 2m) + (p.TotalPoints / 10m))
            .Take(take)
            .ToList();

        // Fall back to the unfiltered pool's top scorers if the filters left nothing
        // (e.g. "differential goalkeeper under 4.5m" might be an empty set some weeks).
        if (ranked.Count == 0)
        {
            ranked = pool
                .Where(p => p.MinutesPlayed >= 90)
                .OrderByDescending(p => (p.Form * 2m) + (p.TotalPoints / 10m))
                .Take(take)
                .ToList();
        }

        return ranked.Select(ToSummary).ToList();
    }

    private static Position? DetectPosition(string normalizedQuestion)
    {
        if (normalizedQuestion.Contains("goalkeeper") || normalizedQuestion.Contains(" gk") || normalizedQuestion.Contains("keeper"))
            return Position.Goalkeeper;
        if (normalizedQuestion.Contains("defender") || normalizedQuestion.Contains(" def"))
            return Position.Defender;
        if (normalizedQuestion.Contains("midfielder") || normalizedQuestion.Contains(" mid"))
            return Position.Midfielder;
        if (normalizedQuestion.Contains("forward") || normalizedQuestion.Contains("striker") || normalizedQuestion.Contains(" fwd"))
            return Position.Forward;
        return null;
    }

    private static PlayerSummary ToSummary(Player p) => new(
        Name: p.WebName,
        Team: p.Team?.ShortName ?? "?",
        Position: p.Position.ToString(),
        Price: p.Price,
        TotalPoints: p.TotalPoints,
        Form: p.Form,
        SelectedByPercent: p.SelectedByPercent
    );
}
