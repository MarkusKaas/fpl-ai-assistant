namespace FplAiAssistant.Api.Models;

/// <summary>
/// A Premier League club, as reported by the official FPL "bootstrap-static" endpoint.
/// </summary>
public class Team
{
    /// <summary>The FPL team id (matches the upstream API, not an auto-generated key).</summary>
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public List<Player> Players { get; set; } = new();
}
