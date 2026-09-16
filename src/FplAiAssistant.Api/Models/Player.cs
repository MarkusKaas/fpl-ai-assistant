namespace FplAiAssistant.Api.Models;

public enum Position
{
    Goalkeeper = 1,
    Defender = 2,
    Midfielder = 3,
    Forward = 4,
}

/// <summary>
/// A single FPL player, flattened from the official API into the shape this app needs.
/// </summary>
public class Player
{
    /// <summary>The FPL "element" id (matches the upstream API).</summary>
    public int Id { get; set; }

    public string WebName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string SecondName { get; set; } = string.Empty;

    public int TeamId { get; set; }

    public Team? Team { get; set; }

    public Position Position { get; set; }

    /// <summary>Price in millions, e.g. 8.5 (the upstream API reports this as tenths, e.g. 85).</summary>
    public decimal Price { get; set; }

    public int TotalPoints { get; set; }

    /// <summary>Rolling recent-form score as reported by the upstream API.</summary>
    public decimal Form { get; set; }

    /// <summary>Percentage of FPL managers who own this player, e.g. 23.4.</summary>
    public decimal SelectedByPercent { get; set; }

    public int GoalsScored { get; set; }

    public int Assists { get; set; }

    public int MinutesPlayed { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public string FullName => $"{FirstName} {SecondName}".Trim();
}
