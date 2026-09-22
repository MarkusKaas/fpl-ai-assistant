using System.Text.Json.Serialization;

namespace FplAiAssistant.Api.Models;

// Mirrors the subset of fields this app needs from the official, public
// Fantasy Premier League classic-league standings endpoint — the same data
// shown on a league's public standings page, no login required:
//   GET https://fantasy.premierleague.com/api/leagues-classic/{leagueId}/standings/

public class FplLeagueStandingsDto
{
    [JsonPropertyName("league")]
    public FplLeagueInfoDto League { get; set; } = new();

    [JsonPropertyName("standings")]
    public FplLeagueStandingsResultsDto Standings { get; set; } = new();
}

public class FplLeagueInfoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class FplLeagueStandingsResultsDto
{
    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }

    [JsonPropertyName("results")]
    public List<FplLeagueStandingEntryDto> Results { get; set; } = new();
}

public class FplLeagueStandingEntryDto
{
    /// <summary>The manager's FPL team ID — same id used by GET /api/entry/{id}/.</summary>
    [JsonPropertyName("entry")]
    public int Entry { get; set; }

    [JsonPropertyName("entry_name")]
    public string EntryName { get; set; } = string.Empty;

    [JsonPropertyName("player_name")]
    public string PlayerName { get; set; } = string.Empty;

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    /// <summary>Rank after the previous gameweek. 0 if there isn't one yet (e.g. gameweek 1).</summary>
    [JsonPropertyName("last_rank")]
    public int LastRank { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("event_total")]
    public int EventTotal { get; set; }
}
