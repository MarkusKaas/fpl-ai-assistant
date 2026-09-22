using System.Text.Json.Serialization;

namespace FplAiAssistant.Api.Models;

// Mirrors the subset of fields this app needs from the official, public
// Fantasy Premier League entry-history endpoint — the same season-long,
// gameweek-by-gameweek record shown on a manager's public "History" tab:
//   GET https://fantasy.premierleague.com/api/entry/{teamId}/history/

public class FplEntryHistoryDto
{
    [JsonPropertyName("current")]
    public List<FplGameweekHistoryDto> Current { get; set; } = new();
}

public class FplGameweekHistoryDto
{
    [JsonPropertyName("event")]
    public int Event { get; set; }

    /// <summary>Points scored in this single gameweek.</summary>
    [JsonPropertyName("points")]
    public int Points { get; set; }

    /// <summary>Cumulative points for the season up to and including this gameweek.</summary>
    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("overall_rank")]
    public int? OverallRank { get; set; }

    /// <summary>Money left in the bank after this gameweek, in tenths of a million.</summary>
    [JsonPropertyName("bank")]
    public int BankTenths { get; set; }

    /// <summary>Total squad value after this gameweek, in tenths of a million.</summary>
    [JsonPropertyName("value")]
    public int TeamValueTenths { get; set; }

    [JsonPropertyName("event_transfers")]
    public int Transfers { get; set; }

    [JsonPropertyName("event_transfers_cost")]
    public int TransferCost { get; set; }

    [JsonPropertyName("points_on_bench")]
    public int PointsOnBench { get; set; }
}
