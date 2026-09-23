namespace FplAiAssistant.Api.Models;

/// <summary>One manager's row in a league comparison — standings plus the same
/// squad-value/bank/captain signals shown on the single-team dashboard, so
/// friends in a mini-league can see not just who's ahead but why.</summary>
public record LeagueEntryView(
    int TeamId,
    string ManagerName,
    string TeamName,
    int Rank,
    /// <summary>Positive = moved up the league since last gameweek, negative = dropped, 0 = no data / unchanged.</summary>
    int RankChange,
    int Total,
    int GameweekPoints,
    decimal TeamValue,
    decimal Bank,
    string? CaptainName
);

public record LeagueDashboardResponse(
    int LeagueId,
    string LeagueName,
    int AsOfGameweek,
    /// <summary>The team ID the caller asked to highlight as "you", if any — echoed back for the UI.</summary>
    int? ViewerTeamId,
    IReadOnlyList<LeagueEntryView> Entries,
    LeagueEntryView? TopGameweekScorer,
    LeagueEntryView? MostValuableSquad,
    LeagueEntryView? BiggestClimber
);
