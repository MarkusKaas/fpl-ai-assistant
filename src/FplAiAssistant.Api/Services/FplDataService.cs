using System.Net.Http.Json;
using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Pulls player/team data from the official, public Fantasy Premier League API
/// and stores a flattened copy locally. This is the "ingestion" half of the app —
/// everything the advice endpoint later retrieves comes from here.
///
/// Note on approach: for an MVP this does a full replace on every refresh rather
/// than a field-by-field upsert. That's simpler and fine at this data volume
/// (~700 players), but a production version would diff and update only changed
/// rows to avoid rewriting the whole table on every gameweek refresh.
/// </summary>
public class FplDataService : IFplDataService
{
    private const string BootstrapUrl = "https://fantasy.premierleague.com/api/bootstrap-static/";

    private readonly HttpClient _httpClient;
    private readonly FplDbContext _db;
    private readonly ILogger<FplDataService> _logger;

    public FplDataService(HttpClient httpClient, FplDbContext db, ILogger<FplDataService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
    }

    public async Task<(int TeamsWritten, int PlayersWritten)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching FPL bootstrap data from {Url}", BootstrapUrl);

        var bootstrap = await _httpClient.GetFromJsonAsync<FplBootstrapDto>(BootstrapUrl, cancellationToken)
            ?? throw new InvalidOperationException("The FPL API returned an empty response.");

        var positionByElementTypeId = bootstrap.ElementTypes.ToDictionary(
            et => et.Id,
            et => MapPosition(et.SingularNameShort));

        // Wipe and reload. Simple, correct, and fast enough for a dataset this size.
        _db.Players.RemoveRange(_db.Players);
        _db.Teams.RemoveRange(_db.Teams);
        await _db.SaveChangesAsync(cancellationToken);

        var teams = bootstrap.Teams.Select(t => new Team
        {
            Id = t.Id,
            Name = t.Name,
            ShortName = t.ShortName,
        }).ToList();
        await _db.Teams.AddRangeAsync(teams, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var players = bootstrap.Elements.Select(e => new Player
        {
            Id = e.Id,
            WebName = e.WebName,
            FirstName = e.FirstName,
            SecondName = e.SecondName,
            TeamId = e.Team,
            Position = positionByElementTypeId.GetValueOrDefault(e.ElementType, Position.Midfielder),
            Price = e.NowCost / 10m,
            TotalPoints = e.TotalPoints,
            Form = ParseDecimal(e.Form),
            SelectedByPercent = ParseDecimal(e.SelectedByPercent),
            GoalsScored = e.GoalsScored,
            Assists = e.Assists,
            MinutesPlayed = e.Minutes,
            LastUpdatedUtc = now,
        }).ToList();
        await _db.Players.AddRangeAsync(players, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refreshed {TeamCount} teams and {PlayerCount} players", teams.Count, players.Count);
        return (teams.Count, players.Count);
    }

    private static Position MapPosition(string singularNameShort) => singularNameShort switch
    {
        "GKP" => Position.Goalkeeper,
        "DEF" => Position.Defender,
        "MID" => Position.Midfielder,
        "FWD" => Position.Forward,
        _ => Position.Midfielder,
    };

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0m;
}
