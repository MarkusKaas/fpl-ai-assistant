namespace FplAiAssistant.Api.Models;

/// <summary>One upcoming fixture for a player's club, with FPL's own 1 (easiest) – 5 (hardest) difficulty rating.</summary>
public record FixturePreview(int Gameweek, string Opponent, bool IsHome, int Difficulty);

public record SquadPlayerView(
    int Id,
    string Name,
    string Team,
    string Position,
    decimal Price,
    decimal Form,
    int TotalPoints,
    bool IsStarting,
    bool IsCaptain,
    bool IsViceCaptain,
    decimal FixtureScore,
    IReadOnlyList<FixturePreview> NextFixtures
);

public record TransferSuggestion(
    SquadPlayerView Out,
    SquadPlayerView In,
    decimal ScoreImprovement,
    decimal PriceDifference,
    string Reason
);

/// <summary>
/// The best valid starting XI/formation from the manager's actual 15, independent
/// of any transfers — "who should I actually field this week". <see cref="Formation"/>
/// is "{def}-{mid}-{fwd}" (e.g. "3-4-3"), always one of the 8 formations legal in FPL.
/// </summary>
public record LineupSuggestion(
    string Formation,
    IReadOnlyList<SquadPlayerView> Starters,
    IReadOnlyList<SquadPlayerView> Bench,
    IReadOnlyList<string> ChangesFromCurrent
);

public record TeamDashboardResponse(
    int TeamId,
    string ManagerName,
    string TeamName,
    int OverallPoints,
    int? OverallRank,
    int GameweekPoints,
    decimal Bank,
    decimal TeamValue,
    int AsOfGameweek,
    IReadOnlyList<SquadPlayerView> Squad,
    SquadPlayerView? SuggestedCaptain,
    IReadOnlyList<TransferSuggestion> TransferSuggestions,
    LineupSuggestion? RecommendedLineup
);
