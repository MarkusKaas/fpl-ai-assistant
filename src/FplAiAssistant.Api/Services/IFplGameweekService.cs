namespace FplAiAssistant.Api.Services;

public interface IFplGameweekService
{
    /// <summary>
    /// The gameweek id whose picks represent a manager's *current* 15-man squad.
    /// Squads carry over between gameweeks until a transfer is made, so the most
    /// recently finished gameweek is the reliable source — the upcoming one isn't
    /// guaranteed to be publicly visible until its deadline passes.
    /// </summary>
    Task<int> GetCurrentSquadGameweekAsync(CancellationToken cancellationToken = default);

    /// <summary>The next gameweek that hasn't been played yet — the anchor for fixture lookahead.</summary>
    Task<int> GetNextGameweekAsync(CancellationToken cancellationToken = default);

    /// <summary>Team id → crest/badge image URL, for the small club badges shown on history charts.</summary>
    Task<IReadOnlyDictionary<int, string>> GetTeamCrestUrlsAsync(CancellationToken cancellationToken = default);
}
