using System.Text.Json.Serialization;

namespace FplAiAssistant.Api.Models;

// Mirrors the subset of fields this app needs from the official, public
// Fantasy Premier League fixtures endpoint:
//   GET https://fantasy.premierleague.com/api/fixtures/?future=1
// No API key required — same public data the FPL "Fixtures" page uses,
// including the 1 (easiest) to 5 (hardest) FDR difficulty rating.
public class FplFixtureDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("event")]
    public int? Event { get; set; }

    [JsonPropertyName("team_h")]
    public int TeamH { get; set; }

    [JsonPropertyName("team_a")]
    public int TeamA { get; set; }

    [JsonPropertyName("team_h_difficulty")]
    public int TeamHDifficulty { get; set; }

    [JsonPropertyName("team_a_difficulty")]
    public int TeamADifficulty { get; set; }

    [JsonPropertyName("finished")]
    public bool Finished { get; set; }
}
