using System.Text.Json.Serialization;

namespace FplAiAssistant.Api.Models;

// These DTOs mirror the subset of fields this app needs from the official,
// public Fantasy Premier League API:
//   GET https://fantasy.premierleague.com/api/bootstrap-static/
// No API key is required — it's a public read-only endpoint. Field names
// use the upstream API's snake_case, mapped via JsonPropertyName.

public class FplBootstrapDto
{
    [JsonPropertyName("elements")]
    public List<FplElementDto> Elements { get; set; } = new();

    [JsonPropertyName("teams")]
    public List<FplTeamDto> Teams { get; set; } = new();

    [JsonPropertyName("element_types")]
    public List<FplElementTypeDto> ElementTypes { get; set; } = new();

    [JsonPropertyName("events")]
    public List<FplEventDto> Events { get; set; } = new();
}

/// <summary>A single FPL gameweek ("event"), used to work out what "current" means.</summary>
public class FplEventDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("finished")]
    public bool Finished { get; set; }

    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; set; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; set; }
}

public class FplTeamDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("short_name")]
    public string ShortName { get; set; } = string.Empty;

    /// <summary>The club's crest code, used to build a badge image URL — distinct from <see cref="Id"/>.</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }
}

public class FplElementTypeDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("singular_name_short")]
    public string SingularNameShort { get; set; } = string.Empty;
}

public class FplElementDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("web_name")]
    public string WebName { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("second_name")]
    public string SecondName { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public int Team { get; set; }

    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }

    /// <summary>Price in tenths of a million, e.g. 85 == £8.5m.</summary>
    [JsonPropertyName("now_cost")]
    public int NowCost { get; set; }

    [JsonPropertyName("total_points")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("form")]
    public string Form { get; set; } = "0";

    [JsonPropertyName("selected_by_percent")]
    public string SelectedByPercent { get; set; } = "0";

    [JsonPropertyName("goals_scored")]
    public int GoalsScored { get; set; }

    [JsonPropertyName("assists")]
    public int Assists { get; set; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }
}
