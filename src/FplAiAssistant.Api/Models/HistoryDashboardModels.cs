namespace FplAiAssistant.Api.Models;

/// <summary>One gameweek's snapshot in a manager's season-long history.</summary>
public record GameweekHistoryPoint(
    int Gameweek,
    /// <summary>Points scored in this single gameweek — what "moves up and down" week to week.</summary>
    int Points,
    /// <summary>Cumulative season total after this gameweek.</summary>
    int TotalPoints,
    int? OverallRank,
    decimal TeamValue,
    decimal Bank,
    int Transfers,
    int TransferCost,
    int PointsOnBench
);

public record TeamHistoryResponse(
    int TeamId,
    string ManagerName,
    string TeamName,
    IReadOnlyList<GameweekHistoryPoint> History
);

/// <summary>One manager's gameweek-by-gameweek series, for the league comparison chart.</summary>
public record ManagerGameweekSeries(
    int TeamId,
    string ManagerName,
    string TeamName,
    /// <summary>The manager's chosen favourite club's crest image URL, if they set one — null if not, or unresolved.</summary>
    string? CrestUrl,
    IReadOnlyList<GameweekHistoryPoint> History
);

public record LeagueHistoryResponse(
    int LeagueId,
    string LeagueName,
    IReadOnlyList<ManagerGameweekSeries> Series
);
