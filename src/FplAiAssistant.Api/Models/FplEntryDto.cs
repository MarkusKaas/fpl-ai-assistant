using System.Text.Json.Serialization;

namespace FplAiAssistant.Api.Models;

// Mirrors the subset of fields this app needs from the official, public
// Fantasy Premier League "entry" (manager/team) endpoints — the same data
// shown on a manager's public FPL profile page, no login required:
//   GET https://fantasy.premierleague.com/api/entry/{teamId}/
//   GET https://fantasy.premierleague.com/api/entry/{teamId}/event/{eventId}/picks/

public class FplEntryInfoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("player_first_name")]
    public string PlayerFirstName { get; set; } = string.Empty;

    [JsonPropertyName("player_last_name")]
    public string PlayerLastName { get; set; } = string.Empty;

    [JsonPropertyName("summary_overall_points")]
    public int OverallPoints { get; set; }

    [JsonPropertyName("summary_overall_rank")]
    public int? OverallRank { get; set; }

    [JsonPropertyName("summary_event_points")]
    public int GameweekPoints { get; set; }

    /// <summary>Money left in the bank, in tenths of a million (e.g. 1 == £0.1m).</summary>
    [JsonPropertyName("last_deadline_bank")]
    public int BankTenths { get; set; }

    /// <summary>Total squad value, in tenths of a million (e.g. 999 == £99.9m).</summary>
    [JsonPropertyName("last_deadline_value")]
    public int TeamValueTenths { get; set; }

    /// <summary>The manager's chosen favourite real-world club, if set — used only to show a crest badge on charts.</summary>
    [JsonPropertyName("favourite_team")]
    public int? FavouriteTeamId { get; set; }
}

public class FplPicksResponseDto
{
    [JsonPropertyName("picks")]
    public List<FplPickDto> Picks { get; set; } = new();
}

public class FplPickDto
{
    [JsonPropertyName("element")]
    public int Element { get; set; }

    /// <summary>1–11 = starting XI, 12–15 = bench (in bench-order).</summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("is_captain")]
    public bool IsCaptain { get; set; }

    [JsonPropertyName("is_vice_captain")]
    public bool IsViceCaptain { get; set; }
}
